using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
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
/// </remarks>
internal static class MappedTypeResolver
{
    private const string ObsoleteAttributeName = "ObsoleteAttribute";

    private const string SystemNamespace = "System";

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
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var found = ClrTypeName.TryParse(name.AsSpan(), out var parsed)
                ? ResolveType(compilation, parsed!, Use.Whole, builder)
                : Resolution.Failed(MappedTypeStatus.NotFound);
            types.Add(new MappedType(name, found.Status, found.IsValueType, found.IsPublic, found.IsObsolete));
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

    private static Resolution ResolveType(Compilation compilation, ClrTypeName name, Use use, StringBuilder builder)
    {
        // A keyword names a built-in type, which is public and never obsolete.
        if (name.Keyword is { } keyword)
        {
            return new Resolution(
                MappedTypeStatus.Found,
                name.Ranks.Count == 0 && keyword is not ("string" or "object"),
                true,
                false
            );
        }

        if (FindNamed(compilation, name.Parts, builder, out var status) is not { } type)
        {
            return Resolution.Failed(status);
        }

        var isPublic = true;
        var isObsolete = false;
        foreach (var part in name.Parts)
        {
            foreach (var argument in part.Arguments)
            {
                var resolved = ResolveType(
                    compilation,
                    argument.Type,
                    argument.Nullable ? Use.NullableArgument : Use.Argument,
                    builder
                );
                status = First(status, resolved.Status);
                isPublic &= resolved.IsPublic;
                isObsolete |= resolved.IsObsolete;
            }
        }

        if (!compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
        {
            status = First(status, MappedTypeStatus.NotAccessible);
        }

        if (!CanBeAPropertyType(type, name, use))
        {
            status = First(status, MappedTypeStatus.NotAPropertyType);
        }

        for (var current = type; current is not null; current = current.ContainingType)
        {
            isPublic &= current.DeclaredAccessibility == Accessibility.Public;
            isObsolete |= IsMarkedObsolete(current);
        }

        return status == MappedTypeStatus.Found
            ? new Resolution(status, name.Ranks.Count == 0 && type.IsValueType, isPublic, isObsolete)
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

    private static bool IsMarkedObsolete(INamedTypeSymbol type) =>
        type.GetAttributes()
            .Any(static attribute =>
                attribute.AttributeClass
                    is {
                        Name: ObsoleteAttributeName,
                        ContainingNamespace: { Name: SystemNamespace, ContainingNamespace.IsGlobalNamespace: true },
                    }
            );

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

    private readonly record struct Resolution(MappedTypeStatus Status, bool IsValueType, bool IsPublic, bool IsObsolete)
    {
        public static Resolution Failed(MappedTypeStatus status) => new(status, false, false, false);
    }
}
