using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SqlSource.Diagnostics;
using SqlSource.Generation.MSBuild;
using SqlSource.Settings;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Types;

/// <summary>
/// Gives each parameter and column of a file's usable entries its C# type: the type override's, from the most specific
/// level that names its key, or else the base map's, under the column of the project's driver.
/// </summary>
/// <remarks>
/// A query's parameters are typed before its columns, each in order, and its first failure is its one error, at its
/// name: the query then has no types.  The map of the type override in effect is merged once for each distinct level
/// of markers, since the queries without markers of their own share their file header's.  A project without the
/// override asks no map at all.
/// </remarks>
internal static class QueryTyper
{
    private const string ParameterOwner = "parameter";

    private const string ColumnOwner = "column";

    // The help of a type that only a sidecar edited by hand, or written by a newer tool, has.  Each help is given
    // without its period.
    private const string Describe = "Run 'sqlsource describe'";

    // The first case of SQLSRC251, and its help.  The model's step reports the second.
    private const string NotVisible = "this project cannot see that type";

    private const string MakeItVisible =
        "Make it, and each type it is nested in, public, or internal when it is this project's own, or give this "
        + "project its assembly's internals with InternalsVisibleTo";

    /// <summary>The types of the file's queries whose entries are usable, and the errors of the others.</summary>
    public static FileTypes Type(FileTypesInput input)
    {
        var check = input.Check;
        if (input.File is not { } file)
        {
            return new FileTypes(
                check.NormalizedPath,
                EquatableArray<QueryTypes>.Empty,
                EquatableArray<DiagnosticInfo>.Empty
            );
        }

        var queries = ImmutableArray.CreateBuilder<QueryTypes>(check.UsableEntries.Count);
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var lookups = new List<(SettingsLevel Markers, OverrideOf? Lookup)>();
        var next = 0;
        foreach (var entry in check.UsableEntries)
        {
            // The entries are in the order of their queries in the file, so each search starts after the last.
            var at = IndexOf(file.Queries, entry.Name, next);
            if (at < 0)
            {
                continue;
            }

            next = at + 1;
            var query = file.Queries[at];
            var typing = new Typing(
                input.Probe.Column,
                LookupOf(query.Markers, input, lookups),
                query.NameLocation,
                diagnostics
            );
            if (TypeQuery(entry, typing) is { } types)
            {
                queries.Add(types);
            }
        }

        return new FileTypes(
            check.NormalizedPath,
            new EquatableArray<QueryTypes>(queries.ToImmutable()),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable())
        );
    }

    // The query's types, or null after its first failure is reported.
    private static QueryTypes? TypeQuery(SidecarEntry entry, Typing typing)
    {
        var parameters = ImmutableArray.CreateBuilder<PropertyType>(entry.Parameters.Count);
        foreach (var parameter in entry.Parameters)
        {
            if (TypeOf(parameter.Type, ParameterOwner, parameter.Name, typing) is not { } type)
            {
                return null;
            }

            parameters.Add(type);
        }

        if (entry.Columns is not { } entryColumns)
        {
            return new QueryTypes(entry.Name, new EquatableArray<PropertyType>(parameters.MoveToImmutable()), null);
        }

        var columns = ImmutableArray.CreateBuilder<PropertyType>(entryColumns.Count);
        foreach (var column in entryColumns)
        {
            if (TypeOf(column.Type, ColumnOwner, column.Name, typing) is not { } type)
            {
                return null;
            }

            columns.Add(type);
        }

        return new QueryTypes(
            entry.Name,
            new EquatableArray<PropertyType>(parameters.MoveToImmutable()),
            new EquatableArray<PropertyType>(columns.MoveToImmutable())
        );
    }

    // The property type of one parameter or column, or null after its error is added.  A type that is not of
    // PostgreSQL, or a parameter's that is null, is only in a sidecar edited by hand, and describing again mends it.
    private static PropertyType? TypeOf(SidecarType? type, string owner, string name, Typing typing)
    {
        if (type is not PostgresType postgres)
        {
            typing.Diagnostics.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.TypeHasNoCSharpType,
                    typing.Location,
                    Subject(owner, name),
                    type?.Name ?? "null",
                    Describe
                )
            );
            return null;
        }

        var mapping = PostgresTypeMap.Map(postgres, typing.Column, typing.Lookup);
        if (mapping is { Kind: TypeMappingKind.Mapped, Text: { } text })
        {
            return new PropertyType(
                text,
                mapping.IsValueType,
                mapping.IsPublic,
                mapping.IsObsolete,
                mapping.Key,
                mapping.Detail
            );
        }

        typing.Diagnostics.Add(Diagnose(mapping, Subject(owner, name), postgres.Name, typing.Location, typing.Column));
        return null;
    }

    private static DiagnosticInfo Diagnose(
        TypeMapping failure,
        string subject,
        string typeName,
        LocationInfo at,
        DriverColumn column
    )
    {
        var key = failure.Key;
        return failure.Kind switch
        {
            TypeMappingKind.CastToOid or TypeMappingKind.CastToText => DiagnosticInfo.Create(
                SqlDiagnostics.TypeHasNoCSharpType,
                at,
                subject,
                typeName,
                "Cast it to " + failure.Detail + " in the query"
            ),
            TypeMappingKind.NeedsNpgsql10 => DiagnosticInfo.Create(
                SqlDiagnostics.TypeHasNoCSharpType,
                at,
                subject,
                typeName,
                "Update Npgsql to 10, or cast it to " + failure.Detail + " in the query"
            ),
            TypeMappingKind.NotWrittenByTheTool => DiagnosticInfo.Create(
                SqlDiagnostics.TypeHasNoCSharpType,
                at,
                subject,
                typeName,
                Describe
            ),
            // NpgsqlRange<T> is a type of the driver, which a project without it cannot name.
            TypeMappingKind.UserDefinedRange => DiagnosticInfo.Create(
                SqlDiagnostics.TypeHasNoCSharpType,
                at,
                subject,
                typeName,
                (column == DriverColumn.None ? "Reference Npgsql, then map" : "Map")
                    + " it with the type override, as "
                    + key
                    + "=NpgsqlTypes.NpgsqlRange<T> with the data source's EnableUnmappedTypes()"
            ),
            TypeMappingKind.NeedsDriver => DiagnosticInfo.Create(
                SqlDiagnostics.TypeNeedsNpgsql,
                at,
                subject,
                typeName,
                "Reference Npgsql"
            ),
            TypeMappingKind.DiffersByDriver => DiagnosticInfo.Create(
                SqlDiagnostics.TypeNeedsNpgsql,
                at,
                subject,
                typeName,
                failure.Detail is { } entries
                    ? "Reference Npgsql, or map it with the type override for the Npgsql the application runs on: "
                        + entries
                    : "Reference Npgsql"
            ),
            TypeMappingKind.MappedTypeNotFound => MappedTypeFailure(
                SqlDiagnostics.MappedTypeNotFound,
                failure,
                subject,
                at
            ),
            TypeMappingKind.MappedTypeNotAccessible => MappedTypeFailure(
                SqlDiagnostics.MappedTypeNotAccessible,
                failure,
                subject,
                at,
                NotVisible,
                MakeItVisible
            ),
            TypeMappingKind.MappedTypeNotAPropertyType => MappedTypeFailure(
                SqlDiagnostics.MappedTypeNotAPropertyType,
                failure,
                subject,
                at
            ),
            TypeMappingKind.Mapped or TypeMappingKind.Unreadable or _ => DiagnosticInfo.Create(
                SqlDiagnostics.TypeHasNoCSharpType,
                at,
                subject,
                typeName,
                "Map it with the type override, as " + AsAKey(key) + "=<a C# type>"
            ),
        };
    }

    // The key as an entry names it: the key of a type of pg_catalog is its bare name, which a key may be only for a
    // name the map knows, so a catalog table's row type, pg_class, is written pg_catalog.pg_class.  The key of any
    // other type has its schema already.
    private static string AsAKey(string key) =>
        key.IndexOf('.') >= 0 || TypeKeys.TryNormalize(key.AsSpan(), out _) ? key : "pg_catalog." + key;

    // A failure of a mapped type keeps the type the override names in its text, which the message gives as a user
    // writes it, and the place that wrote the entry in its detail.  SQLSRC251 adds its case and help after them.
    private static DiagnosticInfo MappedTypeFailure(
        DiagnosticDescriptor descriptor,
        TypeMapping failure,
        string subject,
        LocationInfo at,
        params string[] more
    ) =>
        DiagnosticInfo.Create(
            descriptor,
            at,
            [subject, failure.Key, PostgresTypeMap.AsWritten(failure.Text ?? ""), failure.Detail ?? "", .. more]
        );

    // "column 'id'", as a message names a parameter or column.
    private static string Subject(string owner, string name) => owner + " '" + name + "'";

    // The lookup of the override in effect for the markers, made once for each distinct level of markers, by
    // reference: a query without markers of its own holds its file header's level.
    private static OverrideOf? LookupOf(
        SettingsLevel markers,
        FileTypesInput input,
        List<(SettingsLevel Markers, OverrideOf? Lookup)> lookups
    )
    {
        foreach (var (level, lookup) in lookups)
        {
            if (ReferenceEquals(level, markers))
            {
                return lookup;
            }
        }

        var map = QuerySettings.ResolveTypeOverrides(markers, input.Metadata, input.Property);
        OverrideOf? made = map.Count == 0 ? null : new OverrideLookup(map, input.MappedTypes).Find;
        lookups.Add((markers, made));
        return made;
    }

    private static int IndexOf(EquatableArray<SqlQuery> queries, string name, int from)
    {
        for (var index = from; index < queries.Count; index++)
        {
            if (string.Equals(queries[index].Name, name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    // What the typing of one query reads, and where its error goes.
    private readonly record struct Typing(
        DriverColumn Column,
        OverrideOf? Lookup,
        LocationInfo Location,
        ImmutableArray<DiagnosticInfo>.Builder Diagnostics
    );

    /// <summary>
    /// The type override's answer for a type, at one level of the map's walk: the entry of the type's key, then, for
    /// a type of an extension outside <c>pg_catalog</c>, of its bare name, then, for an array, of its element's key
    /// with <c>[]</c>, looked up the same way.  An entry's type is the one the compilation resolved, or the failure
    /// of its status.
    /// </summary>
    /// <remarks>
    /// An array of <c>pg_catalog</c> is found by its element's key alone: its own, <c>_int4</c> or
    /// <c>pg_catalog._pg_class</c>, is read as <c>int4[]</c> or <c>pg_class[]</c>, so no map holds it.  An array of
    /// another schema keeps its own key, <c>public._tag</c>, which is another key than <c>public.tag[]</c>.
    /// </remarks>
    private sealed class OverrideLookup(EquatableArray<TypeOverride> map, EquatableArray<MappedType> mappedTypes)
    {
        public TypeMapping? Find(PostgresType type)
        {
            var entry =
                EntryOf(type, string.Empty)
                ?? (
                    type.Kind.Equals(SidecarValues.Kind.Array, StringComparison.Ordinal) && type.Element is { } element
                        ? EntryOf(element, "[]")
                        : null
                );
            return entry is null ? null : MappingOf(entry);
        }

        // The entry of the type's key with the suffix, or for a type of an extension, of its bare name with it:
        // citext[] as well as public.citext[].  A key longer than the name is qualified.
        private TypeOverride? EntryOf(PostgresType type, string suffix)
        {
            var key = PostgresTypeMap.KeyOf(type);
            return TypeOverrideMap.Find(map, key + suffix)
                ?? (
                    key.Length > type.InternalName.Length && IsExtension(type)
                        ? TypeOverrideMap.Find(map, type.InternalName + suffix)
                        : null
                );
        }

        private static bool IsExtension(PostgresType type) =>
            type.Kind.Equals(SidecarValues.Kind.Base, StringComparison.Ordinal)
            && TypeKeys.IsExtension(type.InternalName);

        private TypeMapping? MappingOf(TypeOverride entry)
        {
            if (entry.Type is not { } name)
            {
                return null;
            }

            var mapped = MappedTypeResolver.Find(mappedTypes, name);
            var kind = (mapped?.Status ?? MappedTypeStatus.NotFound) switch
            {
                MappedTypeStatus.Found => TypeMappingKind.Mapped,
                MappedTypeStatus.NotAccessible => TypeMappingKind.MappedTypeNotAccessible,
                MappedTypeStatus.NotAPropertyType => TypeMappingKind.MappedTypeNotAPropertyType,
                MappedTypeStatus.NotFound or MappedTypeStatus.Ambiguous or _ => TypeMappingKind.MappedTypeNotFound,
            };
            var place = PlaceOf(entry.Place);
            return kind == TypeMappingKind.Mapped
                ? new TypeMapping(kind, entry.Key, name, mapped!.IsValueType, mapped.IsPublic, mapped.IsObsolete, place)
                : new TypeMapping(kind, entry.Key, name, false, false, false, place);
        }

        // The place that wrote an entry, as SQLSRC250 to SQLSRC252 name it.  The attribute has no level for the type
        // override.
        private static string PlaceOf(SettingPlace place) =>
            place switch
            {
                SettingPlace.Property => "the property " + MSBuildSettings.TypeOverridesName,
                SettingPlace.Metadata => "the metadata " + MSBuildSettings.TypeOverridesName + " of the file",
                SettingPlace.FileHeader => "a -- type-override: marker in the file header",
                SettingPlace.Query => "a -- type-override: marker of the query",
                SettingPlace.Attribute or _ => "the attribute",
            };
    }
}
