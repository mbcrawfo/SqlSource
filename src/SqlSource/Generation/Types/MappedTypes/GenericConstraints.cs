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
/// constraint that forbids null, whose break is a warning there; the nullability inside an argument is not checked
/// (TD-0067).
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
                if (!Converts(compilation, argument, substituted))
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

    // The type with the arguments of the constructed type and its containing types put for their type parameters.
    // A type with no type parameter in it is itself.
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
                        return level.TypeArguments[parameter.Ordinal];
                    }
                }

                return type;
            case IArrayTypeSymbol array:
                var element = Substitute(compilation, array.ElementType, constructed);
                return ReferenceEquals(element, array.ElementType)
                    ? type
                    : compilation.CreateArrayTypeSymbol(element, array.Rank);
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

                if (named.Arity == 0)
                {
                    return definition;
                }

                var arguments = named.TypeArguments;
                var substituted = new ITypeSymbol[arguments.Length];
                var changed = !ReferenceEquals(definition, named.ConstructedFrom);
                for (var index = 0; index < arguments.Length; index++)
                {
                    substituted[index] = Substitute(compilation, arguments[index], constructed);
                    changed |= !ReferenceEquals(substituted[index], arguments[index]);
                }

                return changed ? definition.Construct(substituted) : type;
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

    // A reference type written with a ?, or System.Nullable<T>.
    private static bool IsNullable(ITypeSymbol type) =>
        type.NullableAnnotation == NullableAnnotation.Annotated || IsNullableValue(type);

    private static bool IsNullableValue(ITypeSymbol type) =>
        type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
}
