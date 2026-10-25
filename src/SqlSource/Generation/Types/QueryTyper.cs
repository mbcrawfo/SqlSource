using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using SqlSource.Diagnostics;
using SqlSource.Generation.MSBuild;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Settings.ColumnTypes;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Types;

/// <summary>
/// Gives each parameter and column of a file's usable entries its C# type: for a parameter, the one its
/// <c>-- param:</c> marker gives after <c>-&gt;</c>; for a column, the one its query's <c>-- column:</c> marker gives
/// after <c>-&gt;</c>, else the column types' for its source, from the most specific level that names it; else the
/// type override's, from the most specific level that names its key; or else the base map of its engine's, under the
/// column of the project's driver of that engine.
/// </summary>
/// <remarks>
/// A query's parameters are typed before its columns, each in order, and its first failure is its one error, at its
/// name: the query then has no types.  A type of PostgreSQL goes to <see cref="PostgresTypeMap" />, one of SQL Server
/// to <see cref="SqlServerTypeMap" />.  The map of a query's type override is the entries of its file's engine, and its
/// column types those of every level, each merged once for each distinct level of markers, since the queries without
/// markers of their own share their file header's.  An empty map, as in a project without the override, is not asked
/// at all.  A column's marker of <c>-&gt; default</c> skips the column types alone.
/// </remarks>
internal static class QueryTyper
{
    // The help of a type that only a sidecar edited by hand, or written by a newer tool, has.  Each help is given
    // without its period.
    private const string Describe = "Run 'sqlsource describe'";

    // The first case of SQLSRC251, and its help.  The model's step reports the second.
    private const string NotVisible = "this project cannot see that type";

    private const string MakeItVisible =
        "Make it, and each type it is nested in, public, or internal when it is this project's own, or give this "
        + "project its assembly's internals with InternalsVisibleTo";

    // The place of a parameter's C# type that its -- param: marker gives, as SQLSRC250 to SQLSRC252 name it.
    private const string ParamMarker = "the -- param: marker of the parameter";

    // The place of a column's C# type that a -- column: marker of its query gives, by its name or by its source.
    private const string QueryColumnMarker = "a -- column: marker of the query";

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
        var engine = SqlDescribableDialects.EngineOf(file.Dialect);
        var prefix = SqlDialectRules.For(new SqlDialectChoice(file.Dialect, SqlDialectOptions.None)).ParameterPrefix;
        var levels = new List<(SettingsLevel Markers, LevelTypes Types)>();
        var next = 0;
        foreach (var entry in check.UsableEntries)
        {
            // The entries are in the order of their queries in the file, so each search starts after the last.
            var at = QueryNames.IndexOf(file.Queries, entry.Name, next);
            if (at < 0)
            {
                continue;
            }

            next = at + 1;
            var query = file.Queries[at];
            var level = LevelTypesOf(query.Markers, input, engine, levels);
            var typing = new Typing(
                input.NpgsqlProbe.Column,
                input.SqlClientProbe.Column,
                input.SqlClientProbe.VersionText,
                level.Lookup,
                level.ColumnTypes,
                input.MappedTypes,
                query.NameLocation,
                diagnostics
            );
            if (TypeQuery(entry, query, prefix, typing) is { } types)
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

    // The query's types, or null after its first failure is reported.  A parameter whose marker gives a C# type, found
    // by its name ignoring case, has that type, which the message of its failure names it by, with its prefix.
    private static QueryTypes? TypeQuery(SidecarEntry entry, SqlQuery query, char prefix, Typing typing)
    {
        var declared = query.Parameters;
        var parameters = ImmutableArray.CreateBuilder<PropertyType>(entry.Parameters.Count);
        foreach (var parameter in entry.Parameters)
        {
            var chosen =
                parameter.Type is { } sidecarType && MarkedParameter(declared, parameter.Name) is { } marked
                    ? Chosen(
                        marked.ClrType!,
                        ParamMarker,
                        prefix + marked.Name,
                        sidecarType,
                        MessageSubject.Parameter,
                        parameter.Name,
                        typing
                    )
                    : TypeOf(parameter.Type, MessageSubject.Parameter, parameter.Name, typing);
            if (chosen is not { } type)
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
            if (TypeColumn(column, query.ColumnMarkers, typing) is not { } type)
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

    // A column's type: the one its query's marker gives, which the message of its failure names it by; else, unless
    // that marker says -> default, the column types' entry for its source, which the message names by its key; else
    // the type override's or the map's.
    private static PropertyType? TypeColumn(SidecarColumn column, EquatableArray<ColumnMarker> markers, Typing typing)
    {
        var marker = TypeMarkerOf(markers, column.Name);
        if (marker is { ClrType: { } marked })
        {
            return Chosen(
                marked,
                QueryColumnMarker,
                marker.Name,
                column.Type,
                MessageSubject.Column,
                column.Name,
                typing
            );
        }

        if (marker is null && SourceTypeOf(column, typing.ColumnTypes) is { Type: { } type } source)
        {
            return Chosen(
                type,
                ColumnTypePlaceOf(source.Place),
                source.Key,
                column.Type,
                MessageSubject.Column,
                column.Name,
                typing
            );
        }

        return TypeOf(column.Type, MessageSubject.Column, column.Name, typing);
    }

    // The query's -- column: marker of the column that gives it a C# type or -> default, or null.
    private static ColumnMarker? TypeMarkerOf(EquatableArray<ColumnMarker> markers, string column)
    {
        for (var index = 0; index < markers.Count; index++)
        {
            var marker = markers[index];
            if ((marker.ClrType is not null || marker.ResetsType) && marker.Names(column))
            {
                return marker;
            }
        }

        return null;
    }

    // The column types' entry for the column's source, or null for a column without a table and a column of origin,
    // which no key names, and for one whose winning entry is default.
    private static ColumnType? SourceTypeOf(SidecarColumn column, EquatableArray<ColumnType> columnTypes) =>
        columnTypes.Count > 0 && column.Origin is { Table: { } table, Column: { } source } origin
            ? ColumnTypeMap.Find(columnTypes, origin.Schema, table, source)
            : null;

    // The place that wrote an entry of the column types, as SQLSRC250 to SQLSRC252 name it.  The attribute has no
    // level for them.
    private static string ColumnTypePlaceOf(SettingPlace place) =>
        place switch
        {
            SettingPlace.Property => "the property " + MSBuildSettings.ColumnTypesName,
            SettingPlace.Metadata => "the metadata " + MSBuildSettings.ColumnTypesName + " of the file",
            SettingPlace.FileHeader => "a -- column: marker in the file header",
            SettingPlace.Query => QueryColumnMarker,
            SettingPlace.Attribute or _ => "the attribute",
        };

    // The query's parameter of the name, ignoring case, when its marker gives it a C# type, or null.
    private static SqlQueryParameter? MarkedParameter(EquatableArray<SqlQueryParameter> declared, string name)
    {
        for (var index = 0; index < declared.Count; index++)
        {
            var parameter = declared[index];
            if (
                parameter.ClrType is not null
                && string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase)
            )
            {
                return parameter;
            }
        }

        return null;
    }

    /// <summary>
    /// The property type of a parameter or column whose C# type a marker or a setting chose, over the type override
    /// and the map, or null after its error is added.  The type keeps the base map's type of its database type beside
    /// it, for its converters, as a type override's does, the key of that database type as its <c>Key</c>, which a
    /// converter's message names, and <paramref name="key" /> as its <c>Mapped</c>, which the model's message names.
    /// </summary>
    /// <param name="clrType">The C# type, as <c>ClrTypeName.Text</c> writes it.</param>
    /// <param name="place">Where it was chosen, as <c>SQLSRC250</c> to <c>SQLSRC252</c> name it.</param>
    /// <param name="key">What their messages name as mapped: <c>@author</c>, or the key of a column.</param>
    /// <param name="type">The database type.</param>
    /// <param name="owner"><see cref="MessageSubject.Parameter" /> or <see cref="MessageSubject.Column" />.</param>
    /// <param name="name">The parameter's or the column's name.</param>
    /// <param name="typing">What the typing of the query reads, and where its error goes.</param>
    private static PropertyType? Chosen(
        string clrType,
        string place,
        string key,
        SidecarType type,
        string owner,
        string name,
        Typing typing
    )
    {
        // A type of an engine this version does not know has no base map: its error is the map's.
        if (type is not (PostgresType or SqlServerType))
        {
            return TypeOf(type, owner, name, typing);
        }

        var mapping = MappingOf(typing.MappedTypes, clrType, key, place);
        if (mapping is { Kind: TypeMappingKind.Mapped, Text: { } text })
        {
            return new PropertyType(
                text,
                mapping.IsValueType,
                mapping.IsPublic,
                mapping.WarningIds,
                KeyOf(type),
                place,
                DatabaseTypeOf(type, typing),
                key
            );
        }

        var engine = type is SqlServerType ? DatabaseEngine.SqlServer : DatabaseEngine.PostgreSql;
        typing.Diagnostics.Add(Diagnose(mapping, MessageSubject.Of(owner, name), type.Name, typing, engine));
        return null;
    }

    // The key that an entry of the type override for the type would have, which a converter's message names as its
    // database type: of an array of PostgreSQL its element's with [], as the type override finds one.
    private static string KeyOf(SidecarType type) =>
        type switch
        {
            SqlServerType sqlServer => SqlServerTypeMap.KeyOf(sqlServer),
            PostgresType { Element: { } element } array
                when array.Kind.Equals(SidecarValues.Kind.Array, StringComparison.Ordinal) => PostgresTypeMap.KeyOf(
                element
            ) + "[]",
            PostgresType postgres => PostgresTypeMap.KeyOf(postgres),
            _ => type.Name,
        };

    // The mapping of a C# type that a level chose for a key, by the compilation's answer for its name.  A found type is
    // written as the resolver wrote it, its short names in full; a failure keeps the name for its message, and the
    // place that chose it in its detail.
    private static TypeMapping MappingOf(EquatableArray<MappedType> mappedTypes, string name, string key, string place)
    {
        var mapped = MappedTypeResolver.Find(mappedTypes, name);
        var kind = (mapped?.Status ?? MappedTypeStatus.NotFound) switch
        {
            MappedTypeStatus.Found => TypeMappingKind.Mapped,
            MappedTypeStatus.NotAccessible => TypeMappingKind.MappedTypeNotAccessible,
            MappedTypeStatus.NotAPropertyType => TypeMappingKind.MappedTypeNotAPropertyType,
            MappedTypeStatus.NotFound or MappedTypeStatus.Ambiguous or _ => TypeMappingKind.MappedTypeNotFound,
        };
        return kind == TypeMappingKind.Mapped
            ? new TypeMapping(kind, key, mapped!.Text, mapped.IsValueType, mapped.IsPublic, mapped.WarningIds, place)
            : new TypeMapping(kind, key, name, false, false, EquatableArray<string>.Empty, place);
    }

    // The types that a short name in the C# type could mean, or null when it has none.
    private static EquatableArray<string>? CandidatesOf(EquatableArray<MappedType> mappedTypes, string? name) =>
        name is not null && MappedTypeResolver.Find(mappedTypes, name) is { Candidates.Count: > 0 } mapped
            ? mapped.Candidates
            : null;

    // The property type of one parameter or column, by the map of its type's engine, or null after its error is
    // added.  A type of an engine this version does not know, or a parameter's that is null, is only in a sidecar
    // edited by hand, and describing again mends it.
    private static PropertyType? TypeOf(SidecarType? type, string owner, string name, Typing typing)
    {
        TypeMapping mapping;
        DatabaseEngine engine;
        switch (type)
        {
            case PostgresType postgres:
                mapping = PostgresTypeMap.Map(postgres, typing.Column, typing.Lookup?.OfPostgres);
                engine = DatabaseEngine.PostgreSql;
                break;
            case SqlServerType sqlServer:
                mapping = SqlServerTypeMap.Map(sqlServer, typing.SqlClientColumn, typing.Lookup?.OfSqlServer);
                engine = DatabaseEngine.SqlServer;
                break;
            default:
                typing.Diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.TypeHasNoCSharpType,
                        typing.Location,
                        MessageSubject.Of(owner, name),
                        type?.Name ?? "null",
                        Describe
                    )
                );
                return null;
        }

        if (mapping is { Kind: TypeMappingKind.Mapped, Text: { } text })
        {
            // A type of the map has no place; one that a type override, a column type or a marker chose has the base
            // map's type beside it.
            return new PropertyType(
                text,
                mapping.IsValueType,
                mapping.IsPublic,
                mapping.WarningIds,
                mapping.Key,
                mapping.Detail,
                mapping.Detail is null ? null : DatabaseTypeOf(type, typing)
            );
        }

        typing.Diagnostics.Add(Diagnose(mapping, MessageSubject.Of(owner, name), type.Name, typing, engine));
        return null;
    }

    // What the base map alone reads the type as, with no lookup of the override at any level, and the element's value
    // type for an array or a domain over one.
    private static DatabaseType DatabaseTypeOf(SidecarType type, Typing typing)
    {
        if (type is SqlServerType sqlServer)
        {
            return DatabaseTypeOf(SqlServerTypeMap.Map(sqlServer, typing.SqlClientColumn, null), null);
        }

        var postgres = (PostgresType)type;
        var array = postgres;
        while (array.Kind.Equals(SidecarValues.Kind.Domain, StringComparison.Ordinal) && array.Base is { } baseType)
        {
            array = baseType;
        }

        var element =
            array.Kind.Equals(SidecarValues.Kind.Array, StringComparison.Ordinal) && array.Element is { } item
                ? PostgresTypeMap.Map(item, typing.Column)
                : null;
        return DatabaseTypeOf(PostgresTypeMap.Map(postgres, typing.Column), element);
    }

    private static DatabaseType DatabaseTypeOf(TypeMapping mapping, TypeMapping? element) =>
        mapping is { Kind: TypeMappingKind.Mapped, Text: { } text }
            ? new DatabaseType(
                text,
                mapping.IsValueType,
                element is { Kind: TypeMappingKind.Mapped, IsValueType: true }
            )
            : new DatabaseType(null, false, false);

    // The failure's diagnostic.  NeedsDriver and Unreadable occur for both engines, and their helps name the engine's
    // driver and system schema; NeedsSqlServerTypes and NeedsNewerSqlClient are SQL Server's alone, and
    // NotWrittenByTheTool and the type override's failures read the same for both.  Every other kind is PostgreSQL's
    // alone.
    private static DiagnosticInfo Diagnose(
        TypeMapping failure,
        string subject,
        string typeName,
        Typing typing,
        DatabaseEngine engine
    )
    {
        var key = failure.Key;
        var at = typing.Location;
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
                (typing.Column == NpgsqlColumn.None ? "Reference Npgsql, then map" : "Map")
                    + " it with the type override, as "
                    + key
                    + "=NpgsqlTypes.NpgsqlRange<T> with the data source's EnableUnmappedTypes()"
            ),
            TypeMappingKind.NeedsDriver when engine == DatabaseEngine.SqlServer => DiagnosticInfo.Create(
                SqlDiagnostics.TypeNeedsSqlClient,
                at,
                subject,
                typeName,
                "Reference Microsoft.Data.SqlClient"
            ),
            TypeMappingKind.NeedsNewerSqlClient => DiagnosticInfo.Create(
                SqlDiagnostics.TypeNeedsNewerSqlClient,
                at,
                subject,
                typeName,
                failure.Detail ?? "",
                typing.SqlClientVersion
            ),
            TypeMappingKind.NeedsDriver => DiagnosticInfo.Create(
                SqlDiagnostics.TypeNeedsNpgsql,
                at,
                subject,
                typeName,
                "Reference Npgsql"
            ),
            // A type of the package, which SqlClient reads only when the application references it.
            TypeMappingKind.NeedsSqlServerTypes => DiagnosticInfo.Create(
                SqlDiagnostics.TypeHasNoCSharpType,
                at,
                subject,
                typeName,
                "Reference the package Microsoft.SqlServer.Types and map it with the type override, as "
                    + key
                    + "="
                    + failure.Detail
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
                at,
                CandidatesText(CandidatesOf(typing.MappedTypes, failure.Text))
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
            TypeMappingKind.ArrayNamedByItsOwnKey => DiagnosticInfo.Create(
                SqlDiagnostics.ArrayEntryMapsNothing,
                at,
                subject,
                key,
                failure.Detail ?? "",
                failure.Text ?? ""
            ),
            TypeMappingKind.Mapped or TypeMappingKind.Unreadable or _ => DiagnosticInfo.Create(
                SqlDiagnostics.TypeHasNoCSharpType,
                at,
                subject,
                typeName,
                "Map it with the type override, as " + AsAKey(key, engine) + "=<a C# type>"
            ),
        };
    }

    // The key as an entry names it: the key of a type of the engine's system schema, pg_catalog or sys, is its bare
    // name, which a key may be only for a name the engine's map knows, so a catalog table's row type, pg_class, is
    // written pg_catalog.pg_class, and a type of a newer server, sys.newtype, as is one of SQL Server whose bare name
    // is an alias of another, sys.rowversion.  The key of any other type has its
    // schema already, which is found by splitting the key, as a dot inside quotes is no schema's.
    private static string AsAKey(string key, DatabaseEngine engine)
    {
        if (!TypeKeyText.TrySplit(key.AsSpan(), out var schema, out _, out _) || !schema.IsEmpty)
        {
            return key;
        }

        if (engine == DatabaseEngine.SqlServer)
        {
            // A bare key that SQL Server reads as another name, as rowversion is read as timestamp, names that type
            // and not this one, which sys. names as it is.
            return
                TypeKeys.TryNormalizeSqlServer(key.AsSpan(), out var normalized)
                && string.Equals(normalized, key, StringComparison.Ordinal)
                ? key
                : SystemSchemas.SqlServer + "." + key;
        }

        return TypeKeys.TryNormalizePostgres(key.AsSpan(), out _) ? key : SystemSchemas.Postgres + "." + key;
    }

    // SQLSRC250's last argument: the types a short name in the type could mean, after ", and it could be ", the first
    // few by name and the rest by their count, or nothing.
    private static string CandidatesText(EquatableArray<string>? candidates)
    {
        if (candidates is not { Count: > 0 } names)
        {
            return string.Empty;
        }

        const int Listed = 3;
        var shown = Math.Min(names.Count, Listed);
        var builder = new StringBuilder(", and it could be ");
        for (var index = 0; index < shown; index++)
        {
            if (index > 0)
            {
                _ = builder.Append(index == shown - 1 && names.Count == shown ? " or " : ", ");
            }

            _ = builder.Append(names[index]);
        }

        if (names.Count > shown)
        {
            _ = builder
                .Append(" or ")
                .Append((names.Count - shown).ToString(CultureInfo.InvariantCulture))
                .Append(" more");
        }

        return builder.ToString();
    }

    // A failure of a mapped type keeps the type that was chosen in its text, which the message gives as a user writes
    // it, and the place that chose it in its detail.  SQLSRC250 adds its candidates after them, and
    // SQLSRC251 its case and help.
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

    // The lookup of the type override for the markers, of the file's engine's entries, and the merged column types,
    // made once for each distinct level of markers, by reference: a query without markers of its own holds its file
    // header's level.  No lookup for an empty map.  A map of defaults alone is asked, since a default of an array's
    // own key maps nothing and is reported.
    private static LevelTypes LevelTypesOf(
        SettingsLevel markers,
        FileTypesInput input,
        DatabaseEngine engine,
        List<(SettingsLevel Markers, LevelTypes Types)> levels
    )
    {
        foreach (var (level, types) in levels)
        {
            if (ReferenceEquals(level, markers))
            {
                return types;
            }
        }

        var map = QuerySettings.ResolveTypeOverrides(markers, input.Metadata, input.Property, engine);
        var made = new LevelTypes(
            map.Count > 0 ? new OverrideLookup(map, input.MappedTypes, engine) : null,
            QuerySettings.ResolveColumnTypes(markers, input.Metadata, input.Property)
        );
        levels.Add((markers, made));
        return made;
    }

    // What one level of markers gives the typing of its queries: the lookup of the type override, and the column
    // types, empty when no level has any.
    private readonly record struct LevelTypes(OverrideLookup? Lookup, EquatableArray<ColumnType> ColumnTypes);

    // What the typing of one query reads, and where its error goes.  SqlClientVersion is the probe's VersionText, for
    // the message of a type that the project's SqlClient is too old for, ColumnTypes the merged column types of the
    // query's level of markers, and MappedTypes the compilation's answers for every C# type that a level or a marker
    // chose.
    private readonly record struct Typing(
        NpgsqlColumn Column,
        SqlClientColumn SqlClientColumn,
        string SqlClientVersion,
        OverrideLookup? Lookup,
        EquatableArray<ColumnType> ColumnTypes,
        EquatableArray<MappedType> MappedTypes,
        LocationInfo Location,
        ImmutableArray<DiagnosticInfo>.Builder Diagnostics
    );

    /// <summary>
    /// The type override's answer for a type, at one level of the map's walk.  For a type of PostgreSQL: for an array,
    /// the entry of its element's key with <c>[]</c>, and for any other type, the entry of its key, each then, for a
    /// type of an extension outside <c>pg_catalog</c>, of its bare name.  For a type of SQL Server: the entry of the
    /// key the map asks, with no rule for an array or an extension, which SQL Server has none of.  A key whose
    /// winning entry is <c>default</c> has none, so the walk goes on as for a key without an entry.  An entry's type
    /// is the one the compilation resolved, written with its short names in full, or the failure of its status.
    /// </summary>
    /// <remarks>
    /// An array is found by its element's key with <c>[]</c> alone, in every schema, and never by its own key, its
    /// name in the catalog.  For an array of <c>pg_catalog</c> the two are one: <c>_int4</c> and
    /// <c>pg_catalog._pg_class</c> are read as <c>int4[]</c> and <c>pg_class[]</c>, so no map holds its own.  An
    /// array of another schema keeps its own key, <c>public._user_status</c>, which is another key than
    /// <c>public.user_status[]</c> and could name a type of that name that is no array.  An entry of it that wins,
    /// <c>default</c> included, is the array's failure, <see cref="TypeMappingKind.ArrayNamedByItsOwnKey" />, which
    /// names the key to write.  An array without its element has no key to write, and is left to the base map.
    /// </remarks>
    private sealed class OverrideLookup
    {
        private readonly EquatableArray<TypeOverride> _map;
        private readonly EquatableArray<MappedType> _mappedTypes;

        // The map is of one engine's entries, so the lookup makes the delegate of that engine alone, once.
        public OverrideLookup(
            EquatableArray<TypeOverride> map,
            EquatableArray<MappedType> mappedTypes,
            DatabaseEngine engine
        )
        {
            _map = map;
            _mappedTypes = mappedTypes;
            if (engine == DatabaseEngine.SqlServer)
            {
                OfSqlServer = Find;
            }
            else
            {
                OfPostgres = Find;
            }
        }

        /// <summary>The lookup of a PostgreSQL file's types, or null in a SQL Server file.</summary>
        public PostgresOverrideOf? OfPostgres { get; }

        /// <summary>The lookup of a SQL Server file's types, or null in a PostgreSQL file.</summary>
        public SqlServerOverrideOf? OfSqlServer { get; }

        public TypeMapping? Find(PostgresType type)
        {
            if (!type.Kind.Equals(SidecarValues.Kind.Array, StringComparison.Ordinal))
            {
                return EntryOf(type, string.Empty) is { } entry ? EntryMapping(entry) : null;
            }

            if (type.Element is not { } element)
            {
                return null;
            }

            if (TypeOverrideMap.Winner(_map, PostgresTypeMap.KeyOf(type)) is { } own)
            {
                return new TypeMapping(
                    TypeMappingKind.ArrayNamedByItsOwnKey,
                    own.Key,
                    PostgresTypeMap.KeyOf(element) + "[]",
                    false,
                    false,
                    EquatableArray<string>.Empty,
                    PlaceOf(own.Place)
                );
            }

            return EntryOf(element, "[]") is { } ofElement ? EntryMapping(ofElement) : null;
        }

        public TypeMapping? Find(string sqlServerKey) =>
            TypeOverrideMap.Find(_map, sqlServerKey) is { } entry ? EntryMapping(entry) : null;

        // The entry of the type's key with the suffix, or for a type of an extension, of its bare name with it:
        // citext[] as well as public.citext[].  A key longer than the name is qualified.
        private TypeOverride? EntryOf(PostgresType type, string suffix)
        {
            var key = PostgresTypeMap.KeyOf(type);
            var bare = TypeKeyText.WritePart(type.InternalName);
            return TypeOverrideMap.Find(_map, key + suffix)
                ?? (key.Length > bare.Length && IsExtension(type) ? TypeOverrideMap.Find(_map, bare + suffix) : null);
        }

        private static bool IsExtension(PostgresType type) =>
            type.Kind.Equals(SidecarValues.Kind.Base, StringComparison.Ordinal)
            && TypeKeyNames.IsExtension(type.InternalName);

        private TypeMapping? EntryMapping(TypeOverride entry) =>
            entry.Type is { } name ? MappingOf(_mappedTypes, name, entry.Key, PlaceOf(entry.Place)) : null;

        // The place that wrote an entry, as SQLSRC250 to SQLSRC252 and SQLSRC261 name it.  The attribute has no level
        // for the type override.
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
