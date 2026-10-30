using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using SqlSource.Generation.Converters;
using SqlSource.Settings.ClrTypes;

namespace SqlSource.Generation.Types.MappedTypes;

/// <summary>
/// Resolves the C# types that the type override, the column types and the markers name against the compilation, and
/// keeps of each what a model needs to know, never its symbol, with the converters declared on those of a referenced
/// assembly.
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
/// A short name, of one part without <c>global::</c>, is looked up by <see cref="ShortNameIndex" />, for the
/// top-level types of its name and arity in any namespace that the project can see.  One is the type, its metadata
/// name then looked up as a full name's, so that the project's own wins over a reference's of the same full name;
/// several are ambiguous, and their full names are the record's <see cref="MappedType.Candidates" />; none is not
/// found.  The record's <see cref="MappedType.Text" /> writes each short name found as its type's full name, from
/// <c>global::</c>, so that generated code needs no <c>using</c> and a short name and its full name are one text.
/// A short name whose type is of a referenced assembly that <see cref="TransitivePackageAssemblies" /> holds, one that
/// only a package the project does not list itself brings, is the record's <see cref="MappedType.Package" />: the first
/// in a name found, its own before its arguments', and none for a name that fails.
/// </para>
/// <para>
/// A name is accessible when each of its types is.  A part with arguments is constructed with them, and each argument
/// checked against its type parameter, by <see cref="GenericConstraints" />.  An argument that breaks a constraint
/// makes the name <see cref="MappedTypeStatus.NotAPropertyType" />.  The symbols serve the checks alone and are kept by
/// no result.
/// </para>
/// <para>
/// The ids of the warnings that a use of each type of a name gives, and whether a pragma can turn them off, are
/// <see cref="MappedTypeWarnings" />'s.
/// </para>
/// <para>
/// The named type that a name found, an array's element type or a generic's constructed type, gives, when it is of a
/// referenced assembly, each of its methods marked <c>[SqlSourceConverter]</c>, read by <see cref="ConverterReader" />
/// with no location, its faults kept.  The attribute is matched by its name, since each assembly has its own internal
/// copy.  A type of the project's own assembly gives none, since the attribute's step reads its methods, and a type
/// that two names find gives its converters once.  A name that fails, or is a keyword, gives none.
/// </para>
/// </remarks>
internal static class MappedTypeResolver
{
    // The converter attribute's class without its namespace.
    private const string ConverterAttributeName = "SqlSourceConverterAttribute";

    /// <summary>
    /// One record for each of <paramref name="names" />, in the same order, with the converters of their types of a
    /// referenced assembly, in the order of <see cref="ConverterSet.Comparer" />, and
    /// <see cref="ResolvedTypes.Empty" /> at once for no names.  A short name that finds a type of one of
    /// <paramref name="packageAssemblies" /> marks its record.  Never throws but for
    /// <paramref name="cancellationToken" />.
    /// </summary>
    public static ResolvedTypes Resolve(
        Compilation compilation,
        EquatableArray<string> names,
        TransitivePackageAssemblies packageAssemblies,
        CancellationToken cancellationToken
    )
    {
        if (names.Count == 0)
        {
            return ResolvedTypes.Empty;
        }

        var types = ImmutableArray.CreateBuilder<MappedType>(names.Count);
        var builder = new StringBuilder();
        var warningIds = new List<string>();
        var read = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        ImmutableArray<Converter>.Builder? converters = null;
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            warningIds.Clear();
            var found = ClrTypeName.TryParse(name.AsSpan(), out var parsed)
                ? ResolveType(compilation, packageAssemblies, parsed!, Use.Whole, builder, warningIds)
                : Resolution.Failed(MappedTypeStatus.NotFound);
            var isFound = found.Status == MappedTypeStatus.Found;
            types.Add(
                new MappedType(
                    name,
                    found.Status,
                    found.IsValueType,
                    found.IsPublic,
                    isFound ? MappedTypeWarnings.Distinct(warningIds) : EquatableArray<string>.Empty,
                    isFound && found.Written is { } written ? written.Text : name,
                    found.Status == MappedTypeStatus.Ambiguous && !found.Candidates.IsDefaultOrEmpty
                        ? new EquatableArray<string>(found.Candidates)
                        : EquatableArray<string>.Empty,
                    found.Package,
                    found.ElementIsValueType
                )
            );

            if (
                found.Named is { } named
                && !SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, compilation.Assembly)
                && read.Add(named)
            )
            {
                AddConverters(compilation, named, converters ??= ImmutableArray.CreateBuilder<Converter>());
            }
        }

        return new ResolvedTypes(
            new EquatableArray<MappedType>(types.MoveToImmutable()),
            converters is null ? EquatableArray<Converter>.Empty : ConverterSet.Sort(converters.ToImmutable())
        );
    }

    /// <summary>
    /// The record of <paramref name="name" /> among <paramref name="types" />, or null.  The names must be in ordinal
    /// order, as <see cref="MappedTypeNames.Collect" /> gives them, for the search to find one.
    /// </summary>
    public static MappedType? Find(EquatableArray<MappedType> types, string name)
    {
        var at = SortedSearch.IndexOf(types, name, static type => type.Name, StringComparer.Ordinal);
        return at < 0 ? null : types[at];
    }

    // Each method of the type marked [SqlSourceConverter], as a converter of a mapped type.
    private static void AddConverters(
        Compilation compilation,
        INamedTypeSymbol type,
        ImmutableArray<Converter>.Builder converters
    )
    {
        foreach (var member in type.GetMembers())
        {
            if (member is IMethodSymbol method && IsMarkedAsAConverter(method))
            {
                converters.Add(ConverterReader.Read(method, compilation, ConverterOrigin.MappedType, location: null));
            }
        }
    }

    // The name alone first, so that the full name is made only for an attribute that can be the converter's.
    private static bool IsMarkedAsAConverter(IMethodSymbol method) =>
        method
            .GetAttributes()
            .Any(static attribute =>
                attribute.AttributeClass is { Name: ConverterAttributeName } attributeClass
                && attributeClass
                    .ToDisplayString()
                    .Equals(AttributeSource.ConverterAttributeMetadataName, StringComparison.Ordinal)
            );

    // The resolution of the name, after the ids of the warnings a use of each of its types gives are added to
    // warningIds.  Its Written is the name with each short name in it written in full, or null when it has none.
    private static Resolution ResolveType(
        Compilation compilation,
        TransitivePackageAssemblies packageAssemblies,
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
                use == Use.Whole ? null : AsWritten(compilation, KeywordType(compilation, keyword), name, use),
                null,
                ElementIsValueType: IsOneRank(name) && keyword is not ("string" or "object")
            );
        }

        var type = LookUp(
            compilation,
            packageAssemblies,
            name,
            builder,
            out var status,
            out var package,
            out var candidates
        );

        // A failure of the name's own lookup, an ambiguity across references among them, returns here before its
        // arguments are read, so the candidates gathered below are always those of an argument of a type found.
        if (type is null)
        {
            return Resolution.Failed(status, candidates);
        }

        var arguments = ResolveArguments(compilation, packageAssemblies, name, builder, warningIds);
        status = First(status, arguments.Status);
        package ??= arguments.Package;
        var isPublic = arguments.IsPublic;

        // An argument that failed is the name's failure, which comes before a broken constraint, so the type is
        // constructed only when each argument is found.
        var constructed = type;
        if (status == MappedTypeStatus.Found && arguments.Symbols is { } symbols)
        {
            var next = 0;
            if (
                GenericConstraints.Construct(compilation, type, name.Parts, name.Parts.Count - 1, symbols, ref next) is
                { } built
            )
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

        if (!CanBeUsed(type, name, use, ref isPublic, warningIds))
        {
            status = First(status, MappedTypeStatus.NotAPropertyType);
        }

        if (status != MappedTypeStatus.Found)
        {
            return Resolution.Failed(status, arguments.Ambiguous);
        }

        var writtenParts = arguments.WrittenParts;
        var full = name.IsShort ? InFull(type, name, writtenParts) : WrittenWith(name, writtenParts);
        var isValueType = name.Ranks.Count == 0 && type.IsValueType;
        if (use == Use.Whole)
        {
            return new Resolution(
                status,
                isValueType,
                isPublic,
                null,
                constructed,
                full,
                Package: package,
                ElementIsValueType: IsOneRank(name) && type.IsValueType
            );
        }

        var symbol = AsWritten(compilation, constructed, name, use);
        return new Resolution(status, isValueType, isPublic, symbol, null, full, Package: package);
    }

    // Whether the name is an array of one rank of one dimension, [] or ?[], whose element is the type the name names.
    private static bool IsOneRank(ClrTypeName name) => name.Ranks.Count == 1 && name.Ranks[0].Dimensions == 1;

    // The named type of the name, the full name's by its metadata name or the short name's one candidate, with its
    // status.  A short name of no type is not found, and one of several is ambiguous, their full names its
    // candidates.  The package is the short name's binding, when its type is one that only a package brings.
    private static INamedTypeSymbol? LookUp(
        Compilation compilation,
        TransitivePackageAssemblies packageAssemblies,
        ClrTypeName name,
        StringBuilder builder,
        out MappedTypeStatus status,
        out PackageBinding? package,
        out ImmutableArray<string> candidates
    )
    {
        package = null;
        candidates = default;
        if (!name.IsShort)
        {
            return FindNamed(compilation, name.Parts, builder, out status);
        }

        var part = name.Parts[0];
        var metadataNames = ShortNameIndex.MetadataNamesOf(compilation, part.Identifier, part.Arguments.Count);
        switch (metadataNames.Length)
        {
            case 0:
                status = MappedTypeStatus.NotFound;
                return null;
            case 1:
                var type = Find(compilation, compilation.GetTypesByMetadataName(metadataNames[0]), out status);
                package = PackageOf(compilation, packageAssemblies, type, metadataNames, part.Identifier);
                return type;
            default:
                status = MappedTypeStatus.Ambiguous;
                candidates = FullNames(metadataNames, part.Identifier);
                return null;
        }
    }

    // The arguments of each part of the name, resolved in order, after the ids of the warnings a use of each of their
    // types gives are added to warningIds.
    private static Arguments ResolveArguments(
        Compilation compilation,
        TransitivePackageAssemblies packageAssemblies,
        ClrTypeName name,
        StringBuilder builder,
        List<string> warningIds
    )
    {
        var status = MappedTypeStatus.Found;
        var isPublic = true;
        PackageBinding? package = null;
        List<ITypeSymbol>? arguments = null;
        ImmutableArray<string> ambiguous = default;
        ImmutableArray<ClrNamePart>.Builder? writtenParts = null;

        for (var index = 0; index < name.Parts.Count; index++)
        {
            var part = name.Parts[index];
            ImmutableArray<ClrTypeArgument>.Builder? writtenArguments = null;
            for (var at = 0; at < part.Arguments.Count; at++)
            {
                var argument = part.Arguments[at];
                var resolved = ResolveType(
                    compilation,
                    packageAssemblies,
                    argument.Type,
                    argument.Nullable ? Use.NullableArgument : Use.Argument,
                    builder,
                    warningIds
                );
                status = First(status, resolved.Status);
                isPublic &= resolved.IsPublic;
                if (ambiguous.IsDefault && !resolved.Candidates.IsDefault)
                {
                    ambiguous = resolved.Candidates;
                }

                package ??= resolved.Package;

                if (resolved.Symbol is { } argumentType)
                {
                    (arguments ??= []).Add(argumentType);
                }

                // The arguments before the first written in full are copied as they are.
                if (resolved.Written is not null && writtenArguments is null)
                {
                    writtenArguments = ImmutableArray.CreateBuilder<ClrTypeArgument>(part.Arguments.Count);
                    for (var before = 0; before < at; before++)
                    {
                        writtenArguments.Add(part.Arguments[before]);
                    }
                }

                writtenArguments?.Add(resolved.Written is { } inFull ? argument with { Type = inFull } : argument);
            }

            if (writtenArguments is not null && writtenParts is null)
            {
                writtenParts = ImmutableArray.CreateBuilder<ClrNamePart>(name.Parts.Count);
                for (var before = 0; before < index; before++)
                {
                    writtenParts.Add(name.Parts[before]);
                }
            }

            writtenParts?.Add(
                writtenArguments is null
                    ? part
                    : part with
                    {
                        Arguments = new EquatableArray<ClrTypeArgument>(writtenArguments.MoveToImmutable()),
                    }
            );
        }

        return new Arguments(status, isPublic, arguments, ambiguous, package, writtenParts);
    }

    // Whether the type can be used where the name stands, after the ids of the warnings a use of it and of each type
    // it is nested in gives are added to warningIds, and isPublic is cleared when one of them is not public.
    private static bool CanBeUsed(
        INamedTypeSymbol type,
        ClrTypeName name,
        Use use,
        ref bool isPublic,
        List<string> warningIds
    )
    {
        var canBeUsed = CanBeAPropertyType(type, name, use);
        for (var current = type; current is not null; current = current.ContainingType)
        {
            isPublic &= current.DeclaredAccessibility == Accessibility.Public;
            canBeUsed &= MappedTypeWarnings.Add(current, warningIds);
        }

        // [Experimental] on a module or an assembly marks each of its types.
        canBeUsed &= MappedTypeWarnings.Add(type.ContainingModule, warningIds);
        canBeUsed &= MappedTypeWarnings.Add(type.ContainingAssembly, warningIds);
        return canBeUsed;
    }

    // The binding of a short name whose one candidate is the type, when the type is of a referenced assembly that the
    // set holds.  Null otherwise, and for a type of the project, which wins over a reference's of its full name.
    private static PackageBinding? PackageOf(
        Compilation compilation,
        TransitivePackageAssemblies packageAssemblies,
        INamedTypeSymbol? type,
        ImmutableArray<string> candidates,
        string identifier
    )
    {
        if (
            type is null
            || SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly)
            || !packageAssemblies.Contains(type.ContainingAssembly.Identity.Name)
        )
        {
            return null;
        }

        return new PackageBinding(
            identifier,
            FullNames(candidates, identifier)[0],
            type.ContainingAssembly.Identity.Name
        );
    }

    // The short name as the type's full name: its namespaces, then its one part, with its arguments as written in
    // full.
    private static ClrTypeName InFull(
        INamedTypeSymbol type,
        ClrTypeName name,
        ImmutableArray<ClrNamePart>.Builder? writtenParts
    )
    {
        var parts = ImmutableArray.CreateBuilder<ClrNamePart>();
        for (
            var space = type.ContainingNamespace;
            space is { IsGlobalNamespace: false };
            space = space.ContainingNamespace
        )
        {
            parts.Add(new ClrNamePart(space.Name, EquatableArray<ClrTypeArgument>.Empty));
        }

        parts.Reverse();
        parts.Add(writtenParts is null ? name.Parts[0] : writtenParts[0]);
        return new ClrTypeName(null, new EquatableArray<ClrNamePart>(parts.ToImmutable()), name.Ranks, IsShort: false);
    }

    // The name with its parts as written in full, or null when no argument had a short name.
    private static ClrTypeName? WrittenWith(ClrTypeName name, ImmutableArray<ClrNamePart>.Builder? writtenParts) =>
        writtenParts is null
            ? null
            : name with
            {
                Parts = new EquatableArray<ClrNamePart>(writtenParts.MoveToImmutable()),
            };

    // The full names of a short name's types as a user writes them, from their metadata names, in ordinal order: the
    // namespace and the name, without the arity, and global:: before a type of the global namespace.
    private static ImmutableArray<string> FullNames(ImmutableArray<string> metadataNames, string identifier)
    {
        var names = new string[metadataNames.Length];
        for (var index = 0; index < metadataNames.Length; index++)
        {
            var metadataName = metadataNames[index];
            var dot = metadataName.LastIndexOf('.');
            names[index] = dot < 0 ? "global::" + identifier : metadataName.Substring(0, dot + 1) + identifier;
        }

        Array.Sort(names, StringComparer.Ordinal);
        return ImmutableArray.Create(names);
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

            return Find(compilation, candidates, out status);
        }

        status = MappedTypeStatus.NotFound;
        return null;
    }

    // Of the types of one metadata name, the one, or the one Choose picks of several.  None is not found.
    private static INamedTypeSymbol? Find(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> candidates,
        out MappedTypeStatus status
    )
    {
        switch (candidates.Length)
        {
            case 0:
                status = MappedTypeStatus.NotFound;
                return null;
            case 1:
                status = MappedTypeStatus.Found;
                return candidates[0];
            default:
                return Choose(compilation, candidates, out status);
        }
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
    // a failure.  Named is the named type the whole name found, without its ranks, whose converters the resolver reads,
    // and null for an argument, a keyword or a failure.  Written is the name with each short name in it written in
    // full, and null when it has none or for a failure.  Candidates are the full names of the first short name in it
    // that several types answer, and default when there is none.  Package is the first short name in it that finds a
    // type only a package brings, and null when there is none or for a failure.
    private readonly record struct Resolution(
        MappedTypeStatus Status,
        bool IsValueType,
        bool IsPublic,
        ITypeSymbol? Symbol,
        INamedTypeSymbol? Named,
        ClrTypeName? Written = null,
        ImmutableArray<string> Candidates = default,
        PackageBinding? Package = null,
        bool ElementIsValueType = false
    )
    {
        public static Resolution Failed(MappedTypeStatus status, ImmutableArray<string> candidates = default) =>
            new(status, false, false, null, null, null, candidates);
    }

    // The name's arguments, across its parts: the first failure of them, whether each is public, the symbol of each
    // that has one, the candidates of the first that is ambiguous, the first package binding, and the parts with each
    // short name in their arguments written in full, or null when there is none.
    private readonly record struct Arguments(
        MappedTypeStatus Status,
        bool IsPublic,
        List<ITypeSymbol>? Symbols,
        ImmutableArray<string> Ambiguous,
        PackageBinding? Package,
        ImmutableArray<ClrNamePart>.Builder? WrittenParts
    );
}
