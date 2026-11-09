using System;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using SqlSource.Diagnostics;
using SqlSource.Generation.Types.Drivers;
using SqlSource.Generation.Types.MappedTypes;
using SqlSource.Generation.Types.Maps;
using SqlSource.Settings;
using SqlSource.Settings.ClrTypes;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Generation.Types;

/// <summary>
/// Gives a type's failure its diagnostic, by the failure's kind and the engine.
/// </summary>
internal static class TypeDiagnostics
{
    // The help of a type that only a sidecar edited by hand, or written by a newer tool, has.  Each help is given
    // without its period.
    internal const string Describe = "Run 'sqlsource describe'";

    // The first case of SQLSRC251, and its help.  The model's step reports the second.
    private const string NotVisible = "this project cannot see that type";

    private const string MakeItVisible =
        "Make it, and each type it is nested in, public, or internal when it is this project's own, or give this "
        + "project its assembly's internals with InternalsVisibleTo";

    // The failure's diagnostic.  NeedsDriver and Unreadable occur for both engines, and their helps name the engine's
    // driver and system schema; NeedsSqlServerTypes and NeedsNewerSqlClient are SQL Server's alone, and
    // NotWrittenByTheTool and the type override's failures read the same for both.  Every other kind is PostgreSQL's
    // alone.
    public static DiagnosticInfo Diagnose(
        TypeMapping failure,
        string subject,
        string typeName,
        DatabaseEngine engine,
        LocationInfo at,
        NpgsqlColumn column,
        string sqlClientVersion,
        EquatableArray<MappedType> mappedTypes,
        bool isPostgis = false
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
                (column == NpgsqlColumn.None ? "Reference Npgsql, then map" : "Map")
                    + " it with the type override, as "
                    + key
                    + "=NpgsqlTypes.NpgsqlRange<T> with the data source's EnableUnmappedTypes()"
            ),
            TypeMappingKind.NeedsDriver => engine switch
            {
                DatabaseEngine.PostgreSql => DiagnosticInfo.Create(
                    SqlDiagnostics.TypeNeedsNpgsql,
                    at,
                    subject,
                    typeName,
                    "Reference Npgsql"
                ),
                DatabaseEngine.SqlServer => DiagnosticInfo.Create(
                    SqlDiagnostics.TypeNeedsSqlClient,
                    at,
                    subject,
                    typeName,
                    "Reference Microsoft.Data.SqlClient"
                ),
                _ => throw UnexpectedEnumValueException.Of(engine),
            },
            TypeMappingKind.NeedsNewerSqlClient => DiagnosticInfo.Create(
                SqlDiagnostics.TypeNeedsNewerSqlClient,
                at,
                subject,
                typeName,
                failure.Detail ?? "",
                sqlClientVersion
            ),
            // A spatial type, which NetTopologySuite's row reads in a methods query, or the package's type.
            TypeMappingKind.NeedsSqlServerTypes when SqlServerNames.IsSpatial(key) => DiagnosticInfo.Create(
                SqlDiagnostics.TypeHasNoCSharpType,
                at,
                subject,
                typeName,
                "Reference "
                    + LibraryProbe.SqlServerSpatialPackages
                    + ", and turn the netTopologySuite library on, to read it as "
                    + "NetTopologySuite's Geometry in a methods query, or reference the package "
                    + "Microsoft.SqlServer.Types and map it with the type override, as "
                    + key
                    + "="
                    + failure.Detail
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
                CandidatesText(CandidatesOf(mappedTypes, failure.Text))
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
            // PostGIS's types are read by the plugin of NetTopologySuite, through its row, in a methods query.
            TypeMappingKind.Unreadable when isPostgis => DiagnosticInfo.Create(
                SqlDiagnostics.TypeHasNoCSharpType,
                at,
                subject,
                typeName,
                "Reference Npgsql.NetTopologySuite to read it as NetTopologySuite's Geometry in a methods query, "
                    + "or map it with the type override, as "
                    + AsAKey(key, engine)
                    + "=<a C# type>"
            ),
            TypeMappingKind.Mapped or TypeMappingKind.Unreadable => DiagnosticInfo.Create(
                SqlDiagnostics.TypeHasNoCSharpType,
                at,
                subject,
                typeName,
                "Map it with the type override, as " + AsAKey(key, engine) + "=<a C# type>"
            ),
            _ => throw UnexpectedEnumValueException.Of(failure.Kind),
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

        return engine switch
        {
            DatabaseEngine.PostgreSql => TypeKeys.TryNormalizePostgres(key.AsSpan(), out _)
                ? key
                : SystemSchemas.Postgres + "." + key,
            // A bare key that SQL Server reads as another name, as rowversion is read as timestamp, names that type
            // and not this one, which sys. names as it is.
            DatabaseEngine.SqlServer => TypeKeys.TryNormalizeSqlServer(key.AsSpan(), out var normalized)
            && string.Equals(normalized, key, StringComparison.Ordinal)
                ? key
                : SystemSchemas.SqlServer + "." + key,
            _ => throw UnexpectedEnumValueException.Of(engine),
        };
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
            [subject, failure.Key, ClrTypeName.AsWritten(failure.Text ?? ""), failure.Detail ?? "", .. more]
        );

    // The types that a short name in the C# type could mean, or null when it has none.
    private static EquatableArray<string>? CandidatesOf(EquatableArray<MappedType> mappedTypes, string? name) =>
        name is not null && MappedTypeResolver.Find(mappedTypes, name) is { Candidates.Count: > 0 } mapped
            ? mapped.Candidates
            : null;
}
