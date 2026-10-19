using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Settings;

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
/// No generic type is constructed, and its constraints are not checked (TD-0056): a name is accessible when each of
/// its types is, and each type's own flags say the rest.
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
                true
            );
        }

        if (FindNamed(compilation, name.Parts, builder, out var status) is not { } type)
        {
            return Resolution.Failed(status);
        }

        var isPublic = true;
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

        return status == MappedTypeStatus.Found
            ? new Resolution(status, name.Ranks.Count == 0 && type.IsValueType, isPublic)
            : Resolution.Failed(status);
    }

    // A static class, a ref struct and System.Void can be no property's type, nor an element's or an argument's.
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

        if (type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
        {
            return true;
        }

        // The first rank written is the one whose ? makes the element nullable: int?[][].
        return name.Ranks.Count == 0 ? use == Use.Argument : !name.Ranks[0].NullableElement;
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

    private readonly record struct Resolution(MappedTypeStatus Status, bool IsValueType, bool IsPublic)
    {
        public static Resolution Failed(MappedTypeStatus status) => new(status, false, false);
    }
}
