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
    // An interface as a cast in the method's code writes it: from global::, with its type arguments, a nullable
    // annotation's ? and a keyword's @.
    private static readonly SymbolDisplayFormat CastFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );

    // An interface as a message names it: its own name with its type arguments, each so too, and a nullable
    // annotation's ?, without a keyword's @, which a message gives no name.
    private static readonly SymbolDisplayFormat CastNameFormat = SymbolDisplayFormat
        .MinimallyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        )
        .RemoveMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

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

        // A Nullable<T> is a value type that can be null.
        var canBeNull = !type.IsValueType;
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
            canBeNull = true;
        }

        if (FactoryTypesOf(type).Count == 0)
        {
            return null;
        }

        var location = ConnectionCandidateReader.SourceLocationOr(symbol, fallback);
        return (new CandidateHolder(symbol.Name, member, symbol.IsStatic, type.Name, location, canBeNull), type);
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
    // the order of the types the call looks in, each derived type's before its base's, then those of the interfaces of
    // the factory's class or struct that a call through it cannot name.  The members are tested cheapest first: a
    // member that is static, or that bears no mark and implements no marked interface member, is never asked whether
    // the type reaches it or whether a member hides it.  An interface's member that the factory's class or struct
    // implements is its implementation: one candidate.  An explicit implementation that bears the mark itself, which
    // nothing hides or reaches by name, is read as an unsupported candidate, as a repository's own one is.
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
                    && (
                        IsExplicitImplementation(member)
                        || (
                            ConnectionCandidateReader.IsReachable(member, type, compilation, factory)
                            && !MemberHiding.IsHidden(member, current, lookedIn, type, compilation, factory)
                        )
                    )
                    && ConnectionCandidateReader.ReadMarkedMember(member, isInherited, holder.Location, implementer)
                        is { } candidate
                )
                {
                    candidates.Add(candidate with { Tier = CandidateTier.Held, Holder = holder });
                }
            }
        }

        if (implementer is not null)
        {
            candidates.AddRange(
                ReadCastMembers(implementer, lookedIn, holder, isInherited, type, compilation, factory)
            );
        }

        return candidates;
    }

    // The marked instance members of the interfaces of the factory's class or struct that a call through it cannot
    // name, as held candidates reached through a cast of the holder to the interface: one that the class implements
    // explicitly, itself or in a base type, or that a derived interface implements explicitly; one whose
    // implementation a member that the call can reach hides, a new member of a derived class or of its name; and an
    // interface's default that nothing implements, unless a type parameter's constraint is the interface, through
    // which the call already reaches it.  A member that the class implements implicitly, and that nothing hides, is its
    // own member's candidate.  Each is the interface's member, located at its implementation.  A ref struct cannot be
    // cast to an interface, so each of its own is an unsupported candidate, which the build reports at the holder.
    private static IEnumerable<ConnectionCandidate> ReadCastMembers(
        INamedTypeSymbol implementer,
        List<INamedTypeSymbol> lookedIn,
        CandidateHolder holder,
        bool isInherited,
        INamedTypeSymbol type,
        Compilation compilation,
        ITypeSymbol factory
    )
    {
        foreach (var @interface in implementer.AllInterfaces)
        {
            foreach (var member in @interface.GetMembers())
            {
                if (
                    !member.IsStatic
                    && ConnectionCandidateReader.FindMarking(member) is not null
                    && implementer.FindImplementationForInterfaceMember(member) is { } implementation
                    && (
                        IsExplicitImplementation(implementation, member)
                        || (
                            SymbolEqualityComparer.Default.Equals(implementation, member)
                            && !lookedIn.Contains(@interface, SymbolEqualityComparer.Default)
                        )
                        || IsHiddenFromTheCall(implementation, lookedIn, type, compilation, factory)
                    )
                    && ConnectionCandidateReader.IsReachable(member, type, compilation, @interface)
                    && ConnectionCandidateReader.ReadMarkedMember(member, isInherited, holder.Location) is { } candidate
                )
                {
                    yield return candidate with
                    {
                        Type = implementer.IsRefLikeType ? CandidateType.Unsupported : candidate.Type,
                        Location = ConnectionCandidateReader.SourceLocationOr(implementation, holder.Location),
                        Tier = CandidateTier.Held,
                        Holder = holder,
                        CastTypeText = @interface.ToDisplayString(CastFormat),
                        CastTypeName = @interface.ToDisplayString(CastNameFormat),
                    };
                }
            }
        }
    }

    // Whether a call through the factory's class or struct names another member than the implementation: one that the
    // type can reach hides the implementation's override in the most derived of the classes the call looks in, or the
    // implementation itself when none overrides it.  The walk hides an implementation by its override, which is
    // marked as it is and called directly, so it is the override's hiding that is asked.
    private static bool IsHiddenFromTheCall(
        ISymbol implementation,
        List<INamedTypeSymbol> lookedIn,
        INamedTypeSymbol type,
        Compilation compilation,
        ITypeSymbol factory
    )
    {
        var called = LastOverrideOf(implementation, lookedIn);
        return MemberHiding.IsHidden(called, called.ContainingType, lookedIn, type, compilation, factory);
    }

    // The override of the implementation in the first of the types that declares one, each derived type before its
    // base, or the implementation itself when none does.
    private static ISymbol LastOverrideOf(ISymbol implementation, List<INamedTypeSymbol> lookedIn)
    {
        foreach (var current in lookedIn)
        {
            foreach (var member in current.GetMembers(implementation.Name))
            {
                for (
                    var overridden = ConnectionCandidateReader.OverriddenOf(member);
                    overridden is not null;
                    overridden = ConnectionCandidateReader.OverriddenOf(overridden)
                )
                {
                    if (SymbolEqualityComparer.Default.Equals(overridden, implementation))
                    {
                        return member;
                    }
                }
            }
        }

        return implementation;
    }

    // Whether the member is an explicit implementation of an interface's member, which no call can name.
    private static bool IsExplicitImplementation(ISymbol member) =>
        member switch
        {
            IMethodSymbol method => !method.ExplicitInterfaceImplementations.IsEmpty,
            IPropertySymbol property => !property.ExplicitInterfaceImplementations.IsEmpty,
            _ => false,
        };

    // Whether the implementation implements the interface member explicitly.
    private static bool IsExplicitImplementation(ISymbol implementation, ISymbol member) =>
        implementation switch
        {
            IMethodSymbol method => method.ExplicitInterfaceImplementations.Contains(
                member,
                SymbolEqualityComparer.Default
            ),
            IPropertySymbol property => property.ExplicitInterfaceImplementations.Contains(
                member,
                SymbolEqualityComparer.Default
            ),
            _ => false,
        };

    // The types that a call through a value of the factory's types looks in, each once: a class or struct and its base
    // types, whose interfaces' members it reaches only through their implementations, and an interface and every
    // interface it derives from, a derived one before its base.
    private static List<INamedTypeSymbol> LookedInTypesOf(List<INamedTypeSymbol> factoryTypes)
    {
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var types = new List<INamedTypeSymbol>();
        foreach (var factoryType in factoryTypes)
        {
            IEnumerable<INamedTypeSymbol> own =
                factoryType.TypeKind == TypeKind.Interface
                    ? factoryType.AllInterfaces.Insert(0, factoryType)
                    : MemberHiding.BaseTypesOf(factoryType);
            types.AddRange(own.Where(seen.Add));
        }

        return types;
    }

    // Whether the member is an interface's that the factory's class or struct implements with a member of its own, as a
    // type parameter's class constraint can: a class member hides an interface member there.  A default member that
    // the class does not implement is its own implementation, and stays the interface's to call.
    private static bool IsImplementedBy(ISymbol member, INamedTypeSymbol? implementer) =>
        implementer is not null
        && member.ContainingType.TypeKind == TypeKind.Interface
        && implementer.FindImplementationForInterfaceMember(member) is { } implementation
        && !SymbolEqualityComparer.Default.Equals(implementation, member);
}
