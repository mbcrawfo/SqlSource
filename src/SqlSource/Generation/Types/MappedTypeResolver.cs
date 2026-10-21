using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Settings.ClrTypes;

namespace SqlSource.Generation.Types;

/// <summary>
/// Resolves the C# types that the type override names against the compilation, and keeps of each what a model needs
/// to know, never its symbol.
/// </summary>
/// <remarks>
/// <para>
/// A keyword is its special type.  A name of n parts is looked up by its metadata name with
/// <see cref="Compilation.GetTypesByMetadataName" />, as a namespace of its first k parts and a type nested in the
/// rest, <c>A.B+C`1</c>, from k = n - 1 down to 0, so that the longest namespace wins and a nested type is found; a
/// part with arguments is never a namespace's.  Of several types, the project's own wins, then the one of a
/// reference that the project can see, and several that it can see are ambiguous.  The arguments are resolved the
/// same way, and of two failures in a name, the first in the order of <see cref="MappedTypeStatus" /> is the name's.
/// </para>
/// <para>
/// A name is accessible when each of its types is.  A part with arguments is constructed with them, and each argument
/// checked against its type parameter as the compiler checks it where a model writes the type: the <c>class</c>,
/// <c>struct</c>, <c>unmanaged</c>, <c>notnull</c> and <c>new()</c> constraints, and each constraint type, with the
/// arguments put for its type parameters, by an identity, implicit reference or boxing conversion.  A model is
/// <c>#nullable enable</c>, so an argument that can be null, written with a <c>?</c>, fails a constraint that forbids
/// null, whose break is a warning there; the nullability inside an argument is not checked (TD-0067).  An argument
/// that breaks a constraint makes the name <see cref="MappedTypeStatus.NotAPropertyType" />.  The symbols serve the
/// checks alone and are kept by no result.
/// </para>
/// <para>
/// A use of a type marked <c>[Obsolete]</c> or <c>[Experimental]</c>, or nested in one, or of an assembly or module
/// marked <c>[Experimental]</c>, is a warning, or an error that a <c>#pragma warning disable</c> of its id turns off,
/// and the ids of each type of a name are the name's.  A type cannot be a property's type when a use of it is an
/// error that no pragma turns off, as for <c>[Obsolete]</c> as an error (CS0619), or when it is marked under an id
/// that a pragma cannot name, whose warning, or error, a model could not turn off.
/// </para>
/// </remarks>
internal static class MappedTypeResolver
{
    private const string ObsoleteAttributeName = "ObsoleteAttribute";

    private const string ExperimentalAttributeName = "ExperimentalAttribute";

    private const string ExperimentalAttributeFullName = "System.Diagnostics.CodeAnalysis.ExperimentalAttribute";

    private const string DiagnosticIdArgument = "DiagnosticId";

    private const string SystemNamespace = "System";

    private const string SetsRequiredMembersAttributeFullName =
        "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";

    // The ids of a use of a type marked [Obsolete] without a message and with one, and of [Experimental] with no id.
    private const string ObsoleteId = "CS0612";

    private const string ObsoleteWithMessageId = "CS0618";

    private const string ExperimentalId = "CS9204";

    /// <summary>
    /// One record for each of <paramref name="names" />, in the same order, and <see cref="EquatableArray{T}.Empty" />
    /// at once for no names.  Never throws but for <paramref name="cancellationToken" />.
    /// </summary>
    public static EquatableArray<MappedType> Resolve(
        Compilation compilation,
        EquatableArray<string> names,
        CancellationToken cancellationToken
    )
    {
        if (names.Count == 0)
        {
            return EquatableArray<MappedType>.Empty;
        }

        var types = ImmutableArray.CreateBuilder<MappedType>(names.Count);
        var builder = new StringBuilder();
        var warningIds = new List<string>();
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            warningIds.Clear();
            var found = ClrTypeName.TryParse(name.AsSpan(), out var parsed)
                ? ResolveType(compilation, parsed!, Use.Whole, builder, warningIds)
                : Resolution.Failed(MappedTypeStatus.NotFound);
            types.Add(
                new MappedType(
                    name,
                    found.Status,
                    found.IsValueType,
                    found.IsPublic,
                    found.Status == MappedTypeStatus.Found ? Distinct(warningIds) : EquatableArray<string>.Empty
                )
            );
        }

        return new EquatableArray<MappedType>(types.MoveToImmutable());
    }

    /// <summary>
    /// The record of <paramref name="name" /> among <paramref name="types" />, or null.  The names must be in ordinal
    /// order, as <see cref="MappedTypeNames.Collect" /> gives them, for the search to find one.
    /// </summary>
    public static MappedType? Find(EquatableArray<MappedType> types, string name)
    {
        var (low, high) = (0, types.Count - 1);
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var order = string.CompareOrdinal(types[middle].Name, name);
            if (order == 0)
            {
                return types[middle];
            }

            if (order < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return null;
    }

    // The resolution of the name, after the ids of the warnings a use of each of its types gives are added to
    // warningIds.
    private static Resolution ResolveType(
        Compilation compilation,
        ClrTypeName name,
        Use use,
        StringBuilder builder,
        List<string> warningIds
    )
    {
        // A keyword names a built-in type, which is public and never obsolete.
        if (name.Keyword is { } keyword)
        {
            return new Resolution(
                MappedTypeStatus.Found,
                name.Ranks.Count == 0 && keyword is not ("string" or "object"),
                true,
                use == Use.Whole ? null : AsWritten(compilation, KeywordType(compilation, keyword), name, use)
            );
        }

        if (FindNamed(compilation, name.Parts, builder, out var status) is not { } type)
        {
            return Resolution.Failed(status);
        }

        var isPublic = true;
        List<ITypeSymbol>? arguments = null;
        foreach (var part in name.Parts)
        {
            foreach (var argument in part.Arguments)
            {
                var resolved = ResolveType(
                    compilation,
                    argument.Type,
                    argument.Nullable ? Use.NullableArgument : Use.Argument,
                    builder,
                    warningIds
                );
                status = First(status, resolved.Status);
                isPublic &= resolved.IsPublic;
                if (resolved.Symbol is { } argumentType)
                {
                    (arguments ??= []).Add(argumentType);
                }
            }
        }

        // An argument that failed is the name's failure, which comes before a broken constraint, so the type is
        // constructed only when each argument is found.
        var constructed = type;
        if (status == MappedTypeStatus.Found && arguments is not null)
        {
            var next = 0;
            if (Construct(compilation, type, name.Parts, name.Parts.Count - 1, arguments, ref next) is { } built)
            {
                constructed = built;
            }
            else
            {
                status = MappedTypeStatus.NotAPropertyType;
            }
        }

        if (!compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
        {
            status = First(status, MappedTypeStatus.NotAccessible);
        }

        var canBeUsed = CanBeAPropertyType(type, name, use);
        for (var current = type; current is not null; current = current.ContainingType)
        {
            isPublic &= current.DeclaredAccessibility == Accessibility.Public;
            canBeUsed &= AddWarningIds(current, warningIds);
        }

        // [Experimental] on a module or an assembly marks each of its types.
        canBeUsed &= AddWarningIds(type.ContainingModule, warningIds);
        canBeUsed &= AddWarningIds(type.ContainingAssembly, warningIds);
        if (!canBeUsed)
        {
            status = First(status, MappedTypeStatus.NotAPropertyType);
        }

        if (status != MappedTypeStatus.Found)
        {
            return Resolution.Failed(status);
        }

        var symbol = use == Use.Whole ? null : AsWritten(compilation, constructed, name, use);
        return new Resolution(status, name.Ranks.Count == 0 && type.IsValueType, isPublic, symbol);
    }

    // The type of a keyword.
    private static ITypeSymbol KeywordType(Compilation compilation, string keyword) =>
        keyword switch
        {
            "bool" => compilation.GetSpecialType(SpecialType.System_Boolean),
            "byte" => compilation.GetSpecialType(SpecialType.System_Byte),
            "sbyte" => compilation.GetSpecialType(SpecialType.System_SByte),
            "short" => compilation.GetSpecialType(SpecialType.System_Int16),
            "ushort" => compilation.GetSpecialType(SpecialType.System_UInt16),
            "int" => compilation.GetSpecialType(SpecialType.System_Int32),
            "uint" => compilation.GetSpecialType(SpecialType.System_UInt32),
            "long" => compilation.GetSpecialType(SpecialType.System_Int64),
            "ulong" => compilation.GetSpecialType(SpecialType.System_UInt64),
            "nint" => compilation.CreateNativeIntegerTypeSymbol(signed: true),
            "nuint" => compilation.CreateNativeIntegerTypeSymbol(signed: false),
            "char" => compilation.GetSpecialType(SpecialType.System_Char),
            "float" => compilation.GetSpecialType(SpecialType.System_Single),
            "double" => compilation.GetSpecialType(SpecialType.System_Double),
            "decimal" => compilation.GetSpecialType(SpecialType.System_Decimal),
            "string" => compilation.GetSpecialType(SpecialType.System_String),
            _ => compilation.GetSpecialType(SpecialType.System_Object),
        };

    // The type with the name's ranks around it, from the last rank written in, and a ? where the name writes one: the
    // first rank's before the element, or the argument's after the whole.
    private static ITypeSymbol AsWritten(Compilation compilation, ITypeSymbol type, ClrTypeName name, Use use)
    {
        if (name.Ranks.Count > 0)
        {
            if (name.Ranks[0].NullableElement)
            {
                type = AsNullable(compilation, type);
            }

            for (var index = name.Ranks.Count - 1; index >= 0; index--)
            {
                type = compilation.CreateArrayTypeSymbol(type, name.Ranks[index].Dimensions);
            }
        }

        return use == Use.NullableArgument ? AsNullable(compilation, type) : type;
    }

    // System.Nullable<T> of a value type, and a reference type annotated as one that can be null.
    private static ITypeSymbol AsNullable(Compilation compilation, ITypeSymbol type) =>
        type.IsValueType
            ? compilation.GetSpecialType(SpecialType.System_Nullable_T).Construct(type)
            : type.WithNullableAnnotation(NullableAnnotation.Annotated);

    // The definition at the part of the given index, inside its containing types, constructed with the arguments of
    // each part that has some, taken in order from next.  Null when an argument breaks a constraint of its type
    // parameter.  A type nested in a constructed one is found among that one's members, so that its own type
    // parameters' constraints have the outer arguments in them.
    private static INamedTypeSymbol? Construct(
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

    // A static class, a ref struct and System.Void can be no property's type, nor an element's or an argument's.  An
    // interface with a static abstract member that has no implementation can be no argument (CS8920).
    // System.Nullable<T> can be none as the whole type, which a nullable column makes Nullable<T>? (CS0453), nor
    // where a ? follows it.  An unbound generic cannot be written in the grammar, and is refused all the same.
    private static bool CanBeAPropertyType(INamedTypeSymbol type, ClrTypeName name, Use use)
    {
        if (type.IsStatic || type.IsRefLikeType || type.SpecialType == SpecialType.System_Void)
        {
            return false;
        }

        if (type.IsUnboundGenericType)
        {
            return false;
        }

        if (use != Use.Whole && name.Ranks.Count == 0 && HasStaticAbstractMember(type))
        {
            return false;
        }

        if (type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
        {
            return true;
        }

        // The first rank written is the one whose ? makes the element nullable: int?[][].
        return name.Ranks.Count == 0 ? use == Use.Argument : !name.Ranks[0].NullableElement;
    }

    // Whether the interface, or one it inherits, has a static abstract member that the interface does not implement.
    private static bool HasStaticAbstractMember(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Interface)
        {
            return false;
        }

        if (type.GetMembers().Any(static member => member is { IsStatic: true, IsAbstract: true }))
        {
            return true;
        }

        return type.AllInterfaces.Any(inherited =>
            inherited
                .GetMembers()
                .Any(member =>
                    member is { IsStatic: true, IsAbstract: true }
                    && type.FindImplementationForInterfaceMember(member) is null
                )
        );
    }

    // The type of the parts' metadata name, trying the longest namespace first.  Null, with the reason, when none is
    // found or several are.
    private static INamedTypeSymbol? FindNamed(
        Compilation compilation,
        EquatableArray<ClrNamePart> parts,
        StringBuilder builder,
        out MappedTypeStatus status
    )
    {
        // A part with arguments is a type's, so no namespace reaches past the first of them.
        var longest = parts.Count - 1;
        for (var index = 0; index < longest; index++)
        {
            if (parts[index].Arguments.Count > 0)
            {
                longest = index;
                break;
            }
        }

        for (var namespaceParts = longest; namespaceParts >= 0; namespaceParts--)
        {
            var candidates = compilation.GetTypesByMetadataName(MetadataName(parts, namespaceParts, builder));
            if (candidates.IsEmpty)
            {
                continue;
            }

            if (candidates.Length == 1)
            {
                status = MappedTypeStatus.Found;
                return candidates[0];
            }

            return Choose(compilation, candidates, out status);
        }

        status = MappedTypeStatus.NotFound;
        return null;
    }

    // Of several types of one metadata name, the project's own, or else the one the project can see.  Of none it can
    // see, the first, which the check of accessibility then refuses; of several, none.
    private static INamedTypeSymbol? Choose(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> candidates,
        out MappedTypeStatus status
    )
    {
        status = MappedTypeStatus.Found;
        var own = candidates.FirstOrDefault(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate.ContainingAssembly, compilation.Assembly)
        );
        if (own is not null)
        {
            return own;
        }

        var accessible = candidates
            .Where(candidate => compilation.IsSymbolAccessibleWithin(candidate, compilation.Assembly))
            .Take(2)
            .ToList();
        switch (accessible.Count)
        {
            case 0:
                return candidates[0];
            case 1:
                return accessible[0];
            default:
                status = MappedTypeStatus.Ambiguous;
                return null;
        }
    }

    // The first parts joined by '.' as a namespace, then the rest joined by '+' as nested types, each with its
    // arity after a '`' when it has arguments.
    private static string MetadataName(EquatableArray<ClrNamePart> parts, int namespaceParts, StringBuilder builder)
    {
        _ = builder.Clear();
        for (var index = 0; index < parts.Count; index++)
        {
            if (index > 0)
            {
                _ = builder.Append(index <= namespaceParts ? '.' : '+');
            }

            _ = builder.Append(parts[index].Identifier);
            if (parts[index].Arguments.Count > 0)
            {
                _ = builder.Append('`').Append(parts[index].Arguments.Count.ToString(CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    // Adds the ids of the warnings that a use of the symbol gives for its own [Obsolete] or [Experimental], and says
    // whether a pragma can turn each off.  An [Obsolete] with no DiagnosticId is CS0612 or CS0618 by its message, both
    // added, and an [Experimental] with no id CS9204.  An argument of another shape gives no id and no error.  A
    // symbol that is null, as a type of no module, adds none.
    private static bool AddWarningIds(ISymbol? symbol, List<string> warningIds)
    {
        if (symbol is null)
        {
            return true;
        }

        var canBeUsed = true;
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass)
            {
                continue;
            }

            if (
                attributeClass is
                {
                    Name: ObsoleteAttributeName,
                    ContainingNamespace: { Name: SystemNamespace, ContainingNamespace.IsGlobalNamespace: true },
                }
            )
            {
                var arguments = attribute.ConstructorArguments;
                if (arguments.Length > 1 && ValueOf(arguments[1]) is true)
                {
                    canBeUsed = false;
                    continue;
                }

                if (DiagnosticIdOf(attribute) is { Length: > 0 } id)
                {
                    canBeUsed &= TryAdd(id, warningIds);
                }
                else
                {
                    warningIds.Add(ObsoleteId);
                    warningIds.Add(ObsoleteWithMessageId);
                }
            }
            else if (
                attributeClass.Name.Equals(ExperimentalAttributeName, StringComparison.Ordinal)
                && attributeClass.ToDisplayString().Equals(ExperimentalAttributeFullName, StringComparison.Ordinal)
            )
            {
                var arguments = attribute.ConstructorArguments;
                if (arguments.Length > 0 && ValueOf(arguments[0]) is string { Length: > 0 } id)
                {
                    canBeUsed &= TryAdd(id, warningIds);
                }
                else
                {
                    warningIds.Add(ExperimentalId);
                }
            }
        }

        return canBeUsed;
    }

    // The named argument's value, or null when it is not given: the default pair's constant is not primitive.
    private static string? DiagnosticIdOf(AttributeData attribute) =>
        ValueOf(
            attribute
                .NamedArguments.FirstOrDefault(static argument =>
                    argument.Key.Equals(DiagnosticIdArgument, StringComparison.Ordinal)
                )
                .Value
        ) as string;

    // The value of a primitive constant, or null for any other.  Value throws for an array, which an attribute of the
    // name declared in the project or a reference can take.
    private static object? ValueOf(TypedConstant constant) =>
        constant.Kind == TypedConstantKind.Primitive && !constant.IsNull ? constant.Value : null;

    // A pragma names an id as an identifier; one that is not, as A-B, is read otherwise, and 1234 as CS1234.
    private static bool TryAdd(string id, List<string> warningIds)
    {
        if (!SyntaxFacts.IsValidIdentifier(id))
        {
            return false;
        }

        warningIds.Add(id);
        return true;
    }

    // The ids in ordinal order, each once.
    private static EquatableArray<string> Distinct(List<string> ids)
    {
        if (ids.Count == 0)
        {
            return EquatableArray<string>.Empty;
        }

        ids.Sort(StringComparer.Ordinal);
        var distinct = ImmutableArray.CreateBuilder<string>(ids.Count);
        foreach (var id in ids)
        {
            if (distinct.Count == 0 || !distinct[distinct.Count - 1].Equals(id, StringComparison.Ordinal))
            {
                distinct.Add(id);
            }
        }

        return new EquatableArray<string>(distinct.ToImmutable());
    }

    // Of two statuses, the failure first in the order of the enum; Found only when both are.
    private static MappedTypeStatus First(MappedTypeStatus current, MappedTypeStatus next)
    {
        if (current == MappedTypeStatus.Found)
        {
            return next;
        }

        return next == MappedTypeStatus.Found || current < next ? current : next;
    }

    // Where a type stands in a name: the whole, an argument, or an argument a ? follows.
    private enum Use
    {
        Whole,
        Argument,
        NullableArgument,
    }

    // Symbol is the type as a model writes it, for the name that has it as an argument, and null for the whole name or
    // a failure.
    private readonly record struct Resolution(
        MappedTypeStatus Status,
        bool IsValueType,
        bool IsPublic,
        ITypeSymbol? Symbol
    )
    {
        public static Resolution Failed(MappedTypeStatus status) => new(status, false, false, null);
    }
}
