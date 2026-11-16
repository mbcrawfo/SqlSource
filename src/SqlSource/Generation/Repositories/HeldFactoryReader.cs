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
/// candidates, with the rules of what a member gives of <see cref="ConnectionCandidateReader" />.  Only
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

    /// <summary>
    /// The attribute of an interface member that the member implements in the type, a factory: such a member is marked
    /// as the interface member is, <c>Database</c> too, as an override of a marked member is.
    /// </summary>
    /// <param name="member">A member of the factory or of a base type, or one that such a member overrides.</param>
    /// <param name="type">The factory.</param>
    internal static AttributeData? ImplementedMarkingOf(ISymbol member, INamedTypeSymbol type)
    {
        foreach (var @interface in type.AllInterfaces)
        {
            foreach (var implemented in @interface.GetMembers(member.Name))
            {
                if (
                    ConnectionCandidateReader.MarkingOf(implemented) is { } marking
                    && SymbolEqualityComparer.Default.Equals(
                        type.FindImplementationForInterfaceMember(implemented),
                        member
                    )
                )
                {
                    return marking;
                }
            }
        }

        return null;
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

    // The marked instance members of the factory that the type can reach through the holder, as held candidates.  An
    // interface's member that a class or struct of the factory implements, explicitly too, is its implementation, or
    // none when the type cannot reach it: one candidate.
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
        var candidates = new List<ConnectionCandidate>();
        foreach (var member in VisibleMembersOf(factoryTypes, factory, type, compilation))
        {
            if (
                !member.IsStatic
                && !IsImplementedBy(member, implementer)
                && ConnectionCandidateReader.ReadMarkedMember(member, isInherited, holder.Location, implementer)
                    is { IsMarked: true } candidate
            )
            {
                candidates.Add(candidate with { Tier = CandidateTier.Held, Holder = holder });
            }
        }

        return candidates;
    }

    // The members of the factory's types, of their base types and of their interfaces, each type once, a derived
    // interface before its base, that the type can reach through the holder and that no more derived member it can
    // reach hides: a method hides a method of its signature alone, and any other member every member of its name.
    private static IEnumerable<ISymbol> VisibleMembersOf(
        List<INamedTypeSymbol> factoryTypes,
        ITypeSymbol factory,
        INamedTypeSymbol type,
        Compilation compilation
    )
    {
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var hidingNames = new HashSet<string>(StringComparer.Ordinal);
        var hidingMethods = new List<IMethodSymbol>();
        foreach (var current in factoryTypes.SelectMany(TypesOf).Where(seen.Add))
        {
            var reachable = current
                .GetMembers()
                .Where(member => ConnectionCandidateReader.IsReachable(member, type, compilation, factory))
                .ToList();
            foreach (var member in reachable)
            {
                if (
                    !hidingNames.Contains(member.Name)
                    && (
                        member is not IMethodSymbol method
                        || !hidingMethods.Exists(hiding => HaveOneSignature(hiding, method))
                    )
                )
                {
                    yield return member;
                }
            }

            foreach (var member in reachable)
            {
                if (member is IMethodSymbol method)
                {
                    hidingMethods.Add(method);
                }
                else
                {
                    _ = hidingNames.Add(member.Name);
                }
            }
        }
    }

    // The type, each of its base types, then each of its interfaces, a derived interface before its base.
    private static IEnumerable<INamedTypeSymbol> TypesOf(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            yield return current;
        }

        foreach (var @interface in type.AllInterfaces)
        {
            yield return @interface;
        }
    }

    // The name, the arity, and each parameter's type and kind of reference.
    private static bool HaveOneSignature(IMethodSymbol first, IMethodSymbol second) =>
        first.Name == second.Name
        && first.Arity == second.Arity
        && first.Parameters.Length == second.Parameters.Length
        && first
            .Parameters.Zip(second.Parameters, static (one, other) => (One: one, Other: other))
            .All(static pair =>
                pair.One.RefKind == pair.Other.RefKind
                && SymbolEqualityComparer.Default.Equals(pair.One.Type, pair.Other.Type)
            );

    // Whether the member is an interface's that the factory's class or struct implements.
    private static bool IsImplementedBy(ISymbol member, INamedTypeSymbol? implementer) =>
        implementer is not null
        && member.ContainingType.TypeKind == TypeKind.Interface
        && implementer.FindImplementationForInterfaceMember(member) is not null;
}
