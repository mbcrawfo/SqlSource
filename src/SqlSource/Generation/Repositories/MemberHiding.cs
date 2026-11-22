using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// Whether a more derived member hides a member from the code that a repository's method holds, the one rule of hiding
/// of <see cref="ConnectionCandidateReader" /> and <see cref="HeldFactoryReader" />, which alone call it.
/// </summary>
internal static class MemberHiding
{
    /// <summary>The type and its base types, the type first.</summary>
    /// <param name="type">The type.</param>
    public static List<INamedTypeSymbol> BaseTypesOf(INamedTypeSymbol type)
    {
        var types = new List<INamedTypeSymbol>();
        for (var current = type; current is not null; current = current.BaseType)
        {
            types.Add(current);
        }

        return types;
    }

    /// <summary>
    /// Whether a member that the type can reach, of a type that derives from the member's, hides the member: a method
    /// or an indexer hides one of its signature, a method every member of its name that is not a method, and any other
    /// member every member of its name.  An accessor or a backing field is hidden with the member it belongs to.  Two
    /// interfaces that neither derives from the other hide nothing of each other, so a call that both answer is
    /// ambiguous.  Only the members of the name are looked at.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="declaring">The type the walk found it in.</param>
    /// <param name="lookedIn">The types that the lookup searches.</param>
    /// <param name="type">The repository.</param>
    /// <param name="compilation">The compilation.</param>
    /// <param name="throughType">The type of the value the member is used through, or null for the type itself.</param>
    public static bool IsHidden(
        ISymbol member,
        INamedTypeSymbol declaring,
        IReadOnlyList<INamedTypeSymbol> lookedIn,
        INamedTypeSymbol type,
        Compilation compilation,
        ITypeSymbol? throughType
    )
    {
        var hidden = ConnectionCandidateReader.OwnerOf(member) ?? member;
        for (var index = 0; index < lookedIn.Count; index++)
        {
            var derived = lookedIn[index];
            if (!DerivesFrom(derived, declaring))
            {
                continue;
            }

            var named = derived.GetMembers(hidden.Name);
            for (var each = 0; each < named.Length; each++)
            {
                // The member's own access and not its getter's: C# binds to a property it can reach, then fails on a
                // getter it cannot.
                if (Hides(named[each], hidden) && compilation.IsSymbolAccessibleWithin(named[each], type, throughType))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // A member that no code can name hides only its own kind: an indexer, or a destructor or operator of its signature.
    private static bool Hides(ISymbol hiding, ISymbol hidden) =>
        (hiding, hidden) switch
        {
            (IPropertySymbol { IsIndexer: true } one, IPropertySymbol { IsIndexer: true } other) => HaveOneSignature(
                one.Parameters,
                other.Parameters
            ),
            (IMethodSymbol one, IMethodSymbol other) => (
                one.CanBeReferencedByName || one.MethodKind == other.MethodKind
            )
                && one.Arity == other.Arity
                && HaveOneSignature(one.Parameters, other.Parameters),
            _ => hiding.CanBeReferencedByName,
        };

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

    // Each parameter's type and kind of reference.  The names are equal, since the members were found by the name.
    private static bool HaveOneSignature(
        ImmutableArray<IParameterSymbol> first,
        ImmutableArray<IParameterSymbol> second
    ) =>
        first.Length == second.Length
        && first
            .Zip(second, static (one, other) => (One: one, Other: other))
            .All(static pair => pair.One.RefKind == pair.Other.RefKind && AreOneType(pair.One.Type, pair.Other.Type));

    // Whether two parameter types are one in a signature: a type parameter of each method is the one of its position,
    // since each method declares its own; an array or a generic type is one when its parts are, the type it is nested
    // in among them.
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
                    && one.TypeArguments.Zip(other.TypeArguments, AreOneType).All(static same => same)
                    && AreOneContainer(one.ContainingType, other.ContainingType),
            _ => SymbolEqualityComparer.Default.Equals(first, second),
        };

    // Whether two types are nested in one type, or neither in any.
    private static bool AreOneContainer(INamedTypeSymbol? first, INamedTypeSymbol? second) =>
        first is null || second is null ? first is null && second is null : AreOneType(first, second);
}
