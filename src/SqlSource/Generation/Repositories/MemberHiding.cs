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
    /// Whether a member that the type can reach, of a type that the lookup prefers to the member's, hides the member
    /// from the read or call that a repository's method writes.  A method hides a method of its signature or whose call
    /// it can take, and a value of its name.  A member that is not a method hides a value of its name, and a method
    /// only when it can be invoked.  A generic nested type hides nothing, since the code names no type arguments.  An
    /// indexer hides an indexer of its signature, and an accessor or a backing field is hidden with the member it
    /// belongs to.  Two interfaces that neither derives from the other hide nothing of each other, so a call that both
    /// answer is ambiguous.  Only the members of the name are looked at.
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
            if (!DerivesFrom(derived, declaring) && !IsClassOverInterface(derived, declaring))
            {
                continue;
            }

            var named = derived.GetMembers(hidden.Name);
            for (var each = 0; each < named.Length; each++)
            {
                // The member's own access and not its getter's: C# binds to a property it can reach, then fails on a
                // getter it cannot.
                if (
                    Hides(named[each], hidden, compilation)
                    && compilation.IsSymbolAccessibleWithin(named[each], type, throughType)
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    // A member that no code can name hides only its own kind: an indexer, or a destructor or operator of its signature.
    // An override takes no call: C# leaves it out of overload resolution for the virtual it overrides, which is walked.
    private static bool Hides(ISymbol hiding, ISymbol hidden, Compilation compilation) =>
        (hiding, hidden) switch
        {
            (IPropertySymbol { IsIndexer: true } one, IPropertySymbol { IsIndexer: true } other) => HaveOneSignature(
                one.Parameters,
                other.Parameters
            ),
            (IMethodSymbol one, IMethodSymbol other) => (
                (one.CanBeReferencedByName || one.MethodKind == other.MethodKind)
                && one.Arity == other.Arity
                && HaveOneSignature(one.Parameters, other.Parameters)
            ) || (one.CanBeReferencedByName && !one.IsOverride && TakesTheCallOf(other, one, compilation)),
            (_, IMethodSymbol) => hiding.CanBeReferencedByName && IsInvocable(hiding),
            (INamedTypeSymbol { Arity: > 0 }, _) => false,
            _ => hiding.CanBeReferencedByName,
        };

    // A type parameter's class constraint is preferred to its interface constraints, but for object's members.
    private static bool IsClassOverInterface(INamedTypeSymbol derived, INamedTypeSymbol declaring) =>
        declaring.TypeKind == TypeKind.Interface
        && derived.TypeKind == TypeKind.Class
        && derived.SpecialType != SpecialType.System_Object;

    private static bool IsInvocable(ISymbol member) =>
        (
            member switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                IEventSymbol @event => @event.Type,
                _ => null,
            }
        )?.TypeKind
            is TypeKind.Delegate
                or TypeKind.Dynamic
                or TypeKind.FunctionPointer;

    // Whether C# would bind the call that the build writes for the method to the overload of a more derived type: with
    // no argument, or with one CancellationToken, the method's or a default of its type.
    private static bool TakesTheCallOf(IMethodSymbol called, IMethodSymbol overload, Compilation compilation)
    {
        if (called.MethodKind != MethodKind.Ordinary || called.IsGenericMethod)
        {
            return false;
        }

        if (called.Parameters.IsEmpty)
        {
            return !overload.IsGenericMethod && overload.Parameters.All(IsOmittable);
        }

        if (
            !ConnectionCandidateReader.TakesToken(called)
            || overload.Parameters.IsEmpty
            || !TakesAValue(overload.Parameters[0])
            || !overload.Parameters.Skip(1).All(IsOmittable)
        )
        {
            return false;
        }

        return TakesTheToken(overload, called.Parameters[0].Type, compilation);
    }

    private static bool IsOmittable(IParameterSymbol parameter) =>
        (parameter.IsOptional || parameter.IsParams) && TakesAValue(parameter);

    private static bool TakesAValue(IParameterSymbol parameter) =>
        parameter.RefKind is RefKind.None or RefKind.In or RefKind.RefReadOnlyParameter;

    // The token as the first argument, in the parameter's own form or as one element of a params collection.  A
    // collection whose element type is not known counts as taking it, so that a call never binds to another method.
    private static bool TakesTheToken(IMethodSymbol overload, ITypeSymbol token, Compilation compilation)
    {
        var parameter = overload.Parameters[0];
        return Accepts(parameter.Type)
            || (parameter.IsParams && (ElementOf(parameter.Type) is not { } element || Accepts(element)));

        bool Accepts(ITypeSymbol target) =>
            overload.IsGenericMethod
                ? CanInfer(overload, target, token)
                : compilation.ClassifyCommonConversion(token, target).IsImplicit;
    }

    // Whether the method's one type parameter can be inferred from the token: the parameter is the type parameter, or
    // a generic interface of it that the token implements.  A constraint does not matter: the call binds to the
    // overload, and fails there.
    private static bool CanInfer(IMethodSymbol overload, ITypeSymbol target, ITypeSymbol token) =>
        overload.Arity == 1
        && (
            target is ITypeParameterSymbol { TypeParameterKind: TypeParameterKind.Method }
            || (
                target is INamedTypeSymbol { Arity: 1 } generic
                && generic.TypeArguments[0] is ITypeParameterSymbol { TypeParameterKind: TypeParameterKind.Method }
                && token.AllInterfaces.Any(each =>
                    SymbolEqualityComparer.Default.Equals(each.OriginalDefinition, generic.OriginalDefinition)
                )
            )
        );

    // The element type of a params array or collection, or null when it does not say.
    private static ITypeSymbol? ElementOf(ITypeSymbol collection) =>
        collection switch
        {
            IArrayTypeSymbol array => array.ElementType,
            INamedTypeSymbol { IsRefLikeType: true, Arity: 1 } span => span.TypeArguments[0],
            INamedTypeSymbol named => named
                .AllInterfaces.Add(named)
                .FirstOrDefault(static each =>
                    each.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                )
                ?.TypeArguments[0],
            _ => null,
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
