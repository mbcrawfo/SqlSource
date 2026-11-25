using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Settings.ClrTypes;

namespace SqlSource.Generation.Types.MappedTypes;

/// <summary>
/// Constructs a generic type that a mapped type names with its arguments, and checks each argument against its type
/// parameter as the compiler checks it where a model writes the type.
/// </summary>
/// <remarks>
/// The checks are the <c>class</c>, <c>struct</c>, <c>unmanaged</c>, <c>notnull</c> and <c>new()</c> constraints, and
/// each constraint type, with the arguments put for its type parameters, by an identity, implicit reference or boxing
/// conversion.  A model is <c>#nullable enable</c>, so an argument that can be null, written with a <c>?</c>, fails a
/// constraint that forbids null, whose break is a warning there, and so does a <c>?</c> inside an argument that the
/// constraint type's arguments do not take, by the variance of each type parameter on the way (CS8631).
/// </remarks>
internal static class GenericConstraints
{
    private const string SetsRequiredMembersAttributeFullName =
        "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";

    /// <summary>
    /// The definition at the part of the given index, inside its containing types, constructed with the arguments of
    /// each part that has some, taken in order from <paramref name="next" />.  Null when an argument breaks a
    /// constraint of its type parameter.  A type nested in a constructed one is found among that one's members, so
    /// that its own type parameters' constraints have the outer arguments in them.
    /// </summary>
    public static INamedTypeSymbol? Construct(
        Compilation compilation,
        INamedTypeSymbol definition,
        EquatableArray<ClrNamePart> parts,
        int partIndex,
        List<ITypeSymbol> arguments,
        ref int next
    )
    {
        var current = definition;
        if (definition.ContainingType is { } containing)
        {
            if (Construct(compilation, containing, parts, partIndex - 1, arguments, ref next) is not { } outer)
            {
                return null;
            }

            if (!SymbolEqualityComparer.Default.Equals(outer, containing))
            {
                current = outer.GetTypeMembers(definition.Name, definition.Arity).FirstOrDefault() ?? definition;
            }
        }

        var count = parts[partIndex].Arguments.Count;
        if (count == 0)
        {
            return current;
        }

        var own = new ITypeSymbol[count];
        arguments.CopyTo(next, own, 0, count);
        next += count;
        var constructed = current.Construct(own);
        return MeetsConstraints(compilation, constructed) ? constructed : null;
    }

    // Whether each argument of the type meets the constraints of its type parameter.
    private static bool MeetsConstraints(Compilation compilation, INamedTypeSymbol constructed)
    {
        var parameters = constructed.TypeParameters;
        var arguments = constructed.TypeArguments;
        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            var argument = arguments[index];
            if (!MeetsFlags(parameter, argument))
            {
                return false;
            }

            var constraints = parameter.ConstraintTypes;
            for (var constraint = 0; constraint < constraints.Length; constraint++)
            {
                var substituted = Substitute(compilation, constraints[constraint], constructed);
                if (!Converts(compilation, argument, substituted) || !KeepsInnerNullability(argument, substituted))
                {
                    return false;
                }

                // A constraint written without a ? forbids null, unless it is a type parameter whose argument can
                // be null.
                if (
                    IsNullable(argument)
                    && parameter.ConstraintNullableAnnotations[constraint] == NullableAnnotation.NotAnnotated
                    && !IsNullable(substituted)
                )
                {
                    return false;
                }
            }
        }

        return true;
    }

    // The class, struct, unmanaged, notnull and new() constraints.  A class constraint written without a ? forbids
    // null, and unmanaged is a struct constraint too.
    private static bool MeetsFlags(ITypeParameterSymbol parameter, ITypeSymbol argument)
    {
        if (
            parameter.HasReferenceTypeConstraint
            && (
                !argument.IsReferenceType
                || (
                    IsNullable(argument)
                    && parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.NotAnnotated
                )
            )
        )
        {
            return false;
        }

        if (
            (parameter.HasValueTypeConstraint || parameter.HasUnmanagedTypeConstraint)
            && (!argument.IsValueType || IsNullable(argument))
        )
        {
            return false;
        }

        if (parameter.HasUnmanagedTypeConstraint && !argument.IsUnmanagedType)
        {
            return false;
        }

        if (parameter.HasNotNullConstraint && IsNullable(argument))
        {
            return false;
        }

        return !parameter.HasConstructorConstraint || HasPublicParameterlessConstructor(argument);
    }

    // An identity, implicit reference or boxing conversion, as a constraint type asks.  System.Nullable<T> boxes, for
    // a constraint, only to object and System.ValueType (CS0313, CS0312), though the language boxes it to whatever
    // T boxes to.
    private static bool Converts(Compilation compilation, ITypeSymbol argument, ITypeSymbol constraint)
    {
        if (IsNullableValue(argument) && !SymbolEqualityComparer.Default.Equals(argument, constraint))
        {
            return constraint.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType;
        }

        var conversion = compilation.ClassifyConversion(argument, constraint);
        return conversion.IsIdentity || (conversion.IsImplicit && (conversion.IsReference || conversion.IsBoxing));
    }

    // Whether a type that converts to another, nullability aside, keeps the nullability of the other's type arguments
    // and array elements, as the compiler's conversion does with nullability: a pair at an invariant type parameter,
    // or a value type at a variant one, has the same nullability all through; at an out parameter the source's
    // converts to the target's, and at an in parameter the reverse.  An argument whose base types and interfaces have
    // none of the target's definition keeps it, since the conversion is then not by a type argument.
    private static bool KeepsInnerNullability(ITypeSymbol source, ITypeSymbol target)
    {
        switch (target)
        {
            case IArrayTypeSymbol targetArray:
                if (source is not IArrayTypeSymbol sourceArray)
                {
                    return true;
                }

                return sourceArray.ElementType.IsValueType
                    ? SameNullability(sourceArray.ElementType, targetArray.ElementType)
                    : ConvertsWithNullability(sourceArray.ElementType, targetArray.ElementType);
            case INamedTypeSymbol targetNamed when IsGeneric(targetNamed):
                var candidates = TypeAndBaseTypes(source)
                    .Concat(source.AllInterfaces)
                    .Where(candidate =>
                        SymbolEqualityComparer.Default.Equals(
                            candidate.OriginalDefinition,
                            targetNamed.OriginalDefinition
                        )
                    )
                    .ToList();
                return candidates.Count == 0
                    || candidates.Exists(candidate => ArgumentsKeepNullability(candidate, targetNamed));
            default:
                return true;
        }
    }

    private static IEnumerable<INamedTypeSymbol> TypeAndBaseTypes(ITypeSymbol type)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }

    // The type arguments of two types of one definition, those of their containing types too, which are invariant.
    private static bool ArgumentsKeepNullability(INamedTypeSymbol source, INamedTypeSymbol target)
    {
        var own = true;
        for (
            INamedTypeSymbol? from = source, to = target;
            from is not null && to is not null;
            from = from.ContainingType, to = to.ContainingType, own = false
        )
        {
            var parameters = to.TypeParameters;
            var equatable = own && IsEquatable(to);
            for (var index = 0; index < parameters.Length; index++)
            {
                var variance = VarianceKind.None;
                if (own)
                {
                    variance = equatable ? VarianceKind.In : parameters[index].Variance;
                }
                if (!ArgumentKeepsNullability(from.TypeArguments[index], to.TypeArguments[index], variance))
                {
                    return false;
                }
            }
        }

        return true;
    }

    // System.IEquatable<T>, whose T the compiler takes as in for nullability alone, so that string, an
    // IEquatable<string?>, meets IEquatable<string>.
    private static bool IsEquatable(INamedTypeSymbol type) =>
        type is { Name: "IEquatable", Arity: 1, ContainingType: null }
        && type.ContainingNamespace is { Name: "System", ContainingNamespace.IsGlobalNamespace: true };

    // A variance converts a reference type alone; a value type needs an identity.
    private static bool ArgumentKeepsNullability(ITypeSymbol argument, ITypeSymbol constraint, VarianceKind variance)
    {
        if (argument.IsValueType)
        {
            return SameNullability(argument, constraint);
        }

        return variance switch
        {
            VarianceKind.Out => ConvertsWithNullability(argument, constraint),
            VarianceKind.In => ConvertsWithNullability(constraint, argument),
            VarianceKind.None or _ => SameNullability(argument, constraint),
        };
    }

    // An implicit reference conversion with nullability: a source that can be null does not convert to a target that
    // cannot.
    private static bool ConvertsWithNullability(ITypeSymbol source, ITypeSymbol target) =>
        !(
            source.NullableAnnotation == NullableAnnotation.Annotated
            && target.NullableAnnotation == NullableAnnotation.NotAnnotated
        ) && KeepsInnerNullability(source, target);

    // An identity conversion with nullability: two types, the same nullability aside, whose reference types inside
    // are annotated alike.  An oblivious annotation matches any.
    private static bool SameNullability(ITypeSymbol source, ITypeSymbol target)
    {
        if (
            !source.IsValueType
            && source.NullableAnnotation != NullableAnnotation.None
            && target.NullableAnnotation != NullableAnnotation.None
            && source.NullableAnnotation != target.NullableAnnotation
        )
        {
            return false;
        }

        switch (source, target)
        {
            case (IArrayTypeSymbol sourceArray, IArrayTypeSymbol targetArray):
                return SameNullability(sourceArray.ElementType, targetArray.ElementType);
            case (INamedTypeSymbol sourceNamed, INamedTypeSymbol targetNamed):
                for (
                    INamedTypeSymbol? from = sourceNamed, to = targetNamed;
                    from is not null && to is not null;
                    from = from.ContainingType, to = to.ContainingType
                )
                {
                    for (var index = 0; index < from.TypeArguments.Length; index++)
                    {
                        if (!SameNullability(from.TypeArguments[index], to.TypeArguments[index]))
                        {
                            return false;
                        }
                    }
                }

                return true;
            default:
                return true;
        }
    }

    // A named type with a type argument, its own or a containing type's.
    private static bool IsGeneric(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0)
            {
                return true;
            }
        }

        return false;
    }

    // The type with the arguments of the constructed type and its containing types put for their type parameters.
    // A type with no type parameter in it is itself.  A type parameter used with a ?, T?, is its argument with a ?
    // when that is a reference type, and an argument used where nullable annotations are off is oblivious, as the
    // compiler substitutes them.
    private static ITypeSymbol Substitute(Compilation compilation, ITypeSymbol type, INamedTypeSymbol constructed)
    {
        switch (type)
        {
            case ITypeParameterSymbol
            {
                TypeParameterKind: TypeParameterKind.Type,
                DeclaringType: { } declaring
            } parameter:
                for (var level = constructed; level is not null; level = level.ContainingType)
                {
                    if (SymbolEqualityComparer.Default.Equals(level.OriginalDefinition, declaring.OriginalDefinition))
                    {
                        return WithUseAnnotation(level.TypeArguments[parameter.Ordinal], type.NullableAnnotation);
                    }
                }

                return type;
            case IArrayTypeSymbol array:
                var element = Substitute(compilation, array.ElementType, constructed);
                return ReferenceEquals(element, array.ElementType)
                    ? type
                    : compilation
                        .CreateArrayTypeSymbol(element, array.Rank, element.NullableAnnotation)
                        .WithNullableAnnotation(array.NullableAnnotation);
            case INamedTypeSymbol named:
                var definition = named.ConstructedFrom;
                if (named.ContainingType is { } containing)
                {
                    var outer = (INamedTypeSymbol)Substitute(compilation, containing, constructed);
                    if (!ReferenceEquals(outer, containing))
                    {
                        definition =
                            outer.GetTypeMembers(named.Name, named.Arity).FirstOrDefault() ?? named.ConstructedFrom;
                    }
                }

                var changed = !ReferenceEquals(definition, named.ConstructedFrom);
                if (named.Arity == 0)
                {
                    return changed ? definition.WithNullableAnnotation(type.NullableAnnotation) : type;
                }

                var arguments = named.TypeArguments;
                var substituted = new ITypeSymbol[arguments.Length];
                for (var index = 0; index < arguments.Length; index++)
                {
                    substituted[index] = Substitute(compilation, arguments[index], constructed);
                    changed |= !ReferenceEquals(substituted[index], arguments[index]);
                }

                return changed
                    ? definition.Construct(substituted).WithNullableAnnotation(type.NullableAnnotation)
                    : type;
            default:
                return type;
        }
    }

    // A value type has the constructor unless it declares one that is not public, and a class that is not abstract
    // when it declares a public one; neither when a required member is left that the constructor does not set
    // (CS9040).  An array, an interface and a type parameter have none.
    private static bool HasPublicParameterlessConstructor(ITypeSymbol argument)
    {
        if (argument is not INamedTypeSymbol named || named.TypeKind == TypeKind.Interface)
        {
            return false;
        }

        IMethodSymbol? constructor = null;
        foreach (var candidate in named.InstanceConstructors)
        {
            if (candidate.Parameters.Length == 0)
            {
                constructor = candidate;
                break;
            }
        }

        if (named.IsValueType)
        {
            if (constructor is not null && constructor.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }
        else if (named.IsAbstract || constructor is not { DeclaredAccessibility: Accessibility.Public })
        {
            return false;
        }

        return !HasRequiredMembers(named) || (constructor is not null && SetsRequiredMembers(constructor));
    }

    private static bool HasRequiredMembers(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (
                current
                    .GetMembers()
                    .Any(static member =>
                        member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true }
                    )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool SetsRequiredMembers(IMethodSymbol constructor) =>
        constructor
            .GetAttributes()
            .Any(static attribute =>
                attribute
                    .AttributeClass?.ToDisplayString()
                    .Equals(SetsRequiredMembersAttributeFullName, StringComparison.Ordinal) == true
            );

    // The argument put for a type parameter used with the annotation: annotated when either is, else oblivious when
    // either is.  A value type keeps its own, since T? of an unconstrained T is T itself for one.
    private static ITypeSymbol WithUseAnnotation(ITypeSymbol argument, NullableAnnotation use)
    {
        if (
            argument.IsValueType
            || argument.NullableAnnotation == NullableAnnotation.Annotated
            || use == NullableAnnotation.NotAnnotated
        )
        {
            return argument;
        }

        return argument.WithNullableAnnotation(use);
    }

    // A reference type written with a ?, or System.Nullable<T>.
    private static bool IsNullable(ITypeSymbol type) =>
        type.NullableAnnotation == NullableAnnotation.Annotated || IsNullableValue(type);

    private static bool IsNullableValue(ITypeSymbol type) =>
        type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
}
