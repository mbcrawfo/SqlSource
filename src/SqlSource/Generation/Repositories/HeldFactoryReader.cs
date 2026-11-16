using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using SqlSource.Diagnostics;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// Reads the marked instance members of the factories that a repository holds, the <see cref="CandidateTier.Held" />
/// candidates, with the rules of what a member gives of <see cref="ConnectionCandidateReader" />, which it calls.  Only
/// <see cref="RepositoryShapeReader" /> calls it.
/// </summary>
internal static class HeldFactoryReader
{
    /// <summary>
    /// The marked instance members of the factories that the type holds, each with its <see cref="CandidateHolder" />,
    /// in the order the holders are declared: the type's own fields and properties first, then each base type's, then
    /// the primary constructor's parameters.  A holder is a field, property or parameter that is not marked, whose type
    /// is not a type of a source, and that has at least one such member.  The parameters are holders only when no field
    /// or property is one, and then only those that no argument of the base type and no initializer reads, as in
    /// <see cref="ConnectionCandidateReader.Read" />.
    /// </summary>
    /// <param name="type">The repository.</param>
    /// <param name="compilation">
    /// The compilation, which says which members of a base type and of a factory the type can reach and what an
    /// initializer reads.
    /// </param>
    /// <param name="fallback">The location of a holder that has none in source: the type's attribute.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static EquatableArray<ConnectionCandidate> Read(
        INamedTypeSymbol type,
        Compilation compilation,
        LocationInfo fallback,
        CancellationToken cancellationToken
    )
    {
        var held = ImmutableArray.CreateBuilder<ConnectionCandidate>();
        foreach (var (member, isInherited) in ConnectionCandidateReader.ReachableMembersOf(type, compilation))
        {
            if (HolderOf(member, fallback) is (var holder, var holderType))
            {
                held.AddRange(ReadFactory(holderType, holder, isInherited, type, compilation));
            }
        }

        if (held.Count > 0)
        {
            return new EquatableArray<ConnectionCandidate>(held.ToImmutable());
        }

        var primary = ConnectionCandidateReader.PrimaryConstructorOf(type, cancellationToken);
        foreach (var parameter in primary?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty)
        {
            if (
                HolderOf(parameter, fallback) is (var holder, var holderType)
                && ReadFactory(holderType, holder, isInherited: false, type, compilation) is { Count: > 0 } candidates
                && !ConnectionCandidateReader.IsReadBesides(parameter, type, compilation, cancellationToken)
            )
            {
                held.AddRange(candidates);
            }
        }

        return new EquatableArray<ConnectionCandidate>(held.ToImmutable());
    }

    // A field, property or parameter that can hold a factory, with the type its value is used as: the type of a
    // Nullable<T> value is T.  It holds one only when it is not marked, since a marked one is a source of the type's
    // own, and when its type has a factory's type to read, as FactoryTypesOf says.
    private static (CandidateHolder Holder, ITypeSymbol Type)? HolderOf(ISymbol symbol, LocationInfo fallback)
    {
        var value = symbol is IParameterSymbol parameter
            ? (CandidateMember.Parameter, parameter.Type)
            : ConnectionCandidateReader.ValueOf(symbol);
        if (value is not (var member, var type) || ConnectionCandidateReader.FindMarking(symbol) is not null)
        {
            return null;
        }

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        if (FactoryTypesOf(type).Count == 0)
        {
            return null;
        }

        var location = ConnectionCandidateReader.SourceLocationOr(symbol, fallback);
        return (new CandidateHolder(symbol.Name, member, symbol.IsStatic, type.Name, location), type);
    }

    // The types whose members a value of the type has: the type, or each type a type parameter is constrained to.  Not
    // a type of a source, whose members are no factory's, nor one the compiler knows as special, such as an int, which
    // cannot carry the mark.
    private static List<INamedTypeSymbol> FactoryTypesOf(ITypeSymbol type)
    {
        var types = type is ITypeParameterSymbol parameter ? parameter.ConstraintTypes : ImmutableArray.Create(type);
        return types
            .OfType<INamedTypeSymbol>()
            .Where(static each =>
                each.SpecialType == SpecialType.None && ConnectionCandidateReader.HeldKindOf(each) is null
            )
            .ToList();
    }

    // The marked instance members of the factory that the type can reach through the holder, as held candidates, in
    // the order of the types the call looks in, each derived type's before its base's.  The members are tested
    // cheapest first: a member that is static, or that bears no mark and implements no marked interface member, is
    // never asked whether the type reaches it or whether a member hides it.  An interface's member that the factory's
    // class or struct implements is its implementation: one candidate.
    private static List<ConnectionCandidate> ReadFactory(
        ITypeSymbol factory,
        CandidateHolder holder,
        bool isInherited,
        INamedTypeSymbol type,
        Compilation compilation
    )
    {
        var factoryTypes = FactoryTypesOf(factory);
        var implementer = factoryTypes.FirstOrDefault(static each => each.TypeKind != TypeKind.Interface);
        var lookedIn = LookedInTypesOf(factoryTypes);
        var candidates = new List<ConnectionCandidate>();
        foreach (var current in lookedIn)
        {
            foreach (var member in current.GetMembers())
            {
                if (
                    !member.IsStatic
                    && ConnectionCandidateReader.FindMarking(member, implementer) is not null
                    && !IsImplementedBy(member, implementer)
                    && ConnectionCandidateReader.IsReachable(member, type, compilation, factory)
                    && !IsHidden(member, current, lookedIn, type, compilation, factory)
                    && ConnectionCandidateReader.ReadMarkedMember(member, isInherited, holder.Location, implementer)
                        is { } candidate
                )
                {
                    candidates.Add(candidate with { Tier = CandidateTier.Held, Holder = holder });
                }
            }
        }

        return candidates;
    }

    // The types that a call through a value of the factory's types looks in, each once: a class or struct and its base
    // types, whose interfaces' members it reaches only through their implementations, and an interface and every
    // interface it derives from, a derived one before its base.
    private static List<INamedTypeSymbol> LookedInTypesOf(List<INamedTypeSymbol> factoryTypes)
    {
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var types = new List<INamedTypeSymbol>();
        foreach (var factoryType in factoryTypes)
        {
            var own =
                factoryType.TypeKind == TypeKind.Interface
                    ? factoryType.AllInterfaces.Insert(0, factoryType)
                    : BaseTypesOf(factoryType);
            types.AddRange(own.Where(seen.Add));
        }

        return types;
    }

    private static ImmutableArray<INamedTypeSymbol> BaseTypesOf(INamedTypeSymbol type)
    {
        var types = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        for (var current = type; current is not null; current = current.BaseType)
        {
            types.Add(current);
        }

        return types.ToImmutable();
    }

    // Whether a member that the type can reach, of a type that derives from the member's, hides the member as C# hides
    // it: a method hides a method of its signature and every member of its name that is not a method, and any other
    // member every member of its name.  Two interfaces that neither derives from the other hide nothing of each other,
    // so a call that both answer is ambiguous, and both are candidates.  Only the members of the name are looked at.
    private static bool IsHidden(
        ISymbol member,
        INamedTypeSymbol declaring,
        List<INamedTypeSymbol> lookedIn,
        INamedTypeSymbol type,
        Compilation compilation,
        ITypeSymbol factory
    )
    {
        foreach (var derived in lookedIn)
        {
            if (!DerivesFrom(derived, declaring))
            {
                continue;
            }

            foreach (var hiding in derived.GetMembers(member.Name))
            {
                if (
                    (
                        hiding is not IMethodSymbol hidingMethod
                        || member is not IMethodSymbol method
                        || HaveOneSignature(hidingMethod, method)
                    ) && ConnectionCandidateReader.IsReachable(hiding, type, compilation, factory)
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Whether the type is a class that derives from the other, or an interface that extends it, and is not it.
    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol other)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, other))
            {
                return true;
            }
        }

        return other.TypeKind == TypeKind.Interface
            && type.AllInterfaces.Contains(other, SymbolEqualityComparer.Default);
    }

    // The arity, and each parameter's type and kind of reference.  The names are equal, since the members were found by
    // the name.
    private static bool HaveOneSignature(IMethodSymbol first, IMethodSymbol second) =>
        first.Arity == second.Arity
        && first.Parameters.Length == second.Parameters.Length
        && first
            .Parameters.Zip(second.Parameters, static (one, other) => (One: one, Other: other))
            .All(static pair => pair.One.RefKind == pair.Other.RefKind && AreOneType(pair.One.Type, pair.Other.Type));

    // Whether two parameter types are one in a signature: a type parameter of each method is the one of its position,
    // since each method declares its own; an array or a generic type is one when its parts are.
    private static bool AreOneType(ITypeSymbol first, ITypeSymbol second) =>
        (first, second) switch
        {
            (
                ITypeParameterSymbol { TypeParameterKind: TypeParameterKind.Method } one,
                ITypeParameterSymbol { TypeParameterKind: TypeParameterKind.Method } other
            ) => one.Ordinal == other.Ordinal,
            (IArrayTypeSymbol one, IArrayTypeSymbol other) => one.Rank == other.Rank
                && AreOneType(one.ElementType, other.ElementType),
            (INamedTypeSymbol { IsGenericType: true } one, INamedTypeSymbol { IsGenericType: true } other) =>
                SymbolEqualityComparer.Default.Equals(one.OriginalDefinition, other.OriginalDefinition)
                    && one.TypeArguments.Zip(other.TypeArguments, AreOneType).All(static same => same),
            _ => SymbolEqualityComparer.Default.Equals(first, second),
        };

    // Whether the member is an interface's that the factory's class or struct implements, as a type parameter's class
    // constraint can: a class member hides an interface member there.
    private static bool IsImplementedBy(ISymbol member, INamedTypeSymbol? implementer) =>
        implementer is not null
        && member.ContainingType.TypeKind == TypeKind.Interface
        && implementer.FindImplementationForInterfaceMember(member) is not null;
}
