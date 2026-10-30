using System;
using System.Collections.Generic;
using SqlSource.Settings.Libraries;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Types.Maps;

/// <summary>
/// The rows of the libraries that are on, which <see cref="PostgresTypeMap.Map" /> and
/// <see cref="SqlServerTypeMap.Map" /> ask after the type override and before the base map.  A row gives a library's
/// own C# type for a type of <c>pg_catalog</c> or a system type of SQL Server, and writes it <c>global::</c>-qualified:
/// the generator names no library type itself.  Its <c>Detail</c> is the library as a place,
/// <see cref="PlaceOf" />, so that the type has a place and the base map's type beside it, which a converter converts
/// from and to.
/// </summary>
internal sealed class LibraryRows
{
    // One instance for each set of libraries, found by the bit of each library, which is 1 << its number: built once,
    // when the type is initialized, so that a set found twice is the same instance.  Slot 0 is the empty set, which
    // has no instance.
    private static readonly LibraryRows?[] Sets = BuildSets();

    private readonly Dictionary<string, TypeMapping> _rows;
    private readonly Dictionary<string, TypeMapping> _sqlServerRows;

    private LibraryRows(Dictionary<string, TypeMapping> rows, Dictionary<string, TypeMapping> sqlServerRows)
    {
        _rows = rows;
        _sqlServerRows = sqlServerRows;
    }

    /// <summary>
    /// The rows of <paramref name="on" />: the same instance each time for the same libraries, whatever their order and
    /// however often one repeats, and null when there are none.
    /// </summary>
    public static LibraryRows? Of(IReadOnlyList<SqlLibrary> on)
    {
        var mask = 0;
        for (var i = 0; i < on.Count; i++)
        {
            mask |= 1 << (int)on[i];
        }

        return Sets[mask];
    }

    /// <summary>
    /// The library as the place that chose a type, which <c>SQLSRC250</c> to <c>SQLSRC252</c> word: <c>the NodaTime
    /// library</c>.
    /// </summary>
    public static string PlaceOf(SqlLibrary library) =>
        library switch
        {
            SqlLibrary.Json => "the System.Text.Json library",
            SqlLibrary.NodaTime or _ => "the NodaTime library",
        };

    /// <summary>
    /// The library's mapping of <paramref name="type" />, or null when it has none: a type of <c>pg_catalog</c> that
    /// a row names, by its <c>InternalName</c> ignoring case, as the base map's rows are.
    /// </summary>
    public TypeMapping? Find(PostgresType type) =>
        PostgresTypeMap.IsSystemType(type) && _rows.TryGetValue(type.InternalName, out var row) ? row : null;

    /// <summary>
    /// The library's mapping of <paramref name="type" />, or null when it has none: a system type that a row names, by
    /// its <see cref="SqlServerTypeMap.BaseName" /> ignoring case, which drops its facets.  The caller asks only for a
    /// type that is not a CLR user type.
    /// </summary>
    public TypeMapping? Find(SqlServerType type) =>
        !string.IsNullOrEmpty(type.Name)
        && _sqlServerRows.TryGetValue(SqlServerTypeMap.BaseName(type.Name), out var row)
            ? row
            : null;

    // Every set of libraries, indexed by its mask: the rows of a set are those of its libraries, side by side.  A type
    // that two libraries give is the later library's, which no pair of libraries does today.
    private static LibraryRows?[] BuildSets()
    {
        var libraries = (SqlLibrary[])Enum.GetValues(typeof(SqlLibrary));
        var sets = new LibraryRows?[1 << libraries.Length];
        for (var mask = 1; mask < sets.Length; mask++)
        {
            var rows = new Dictionary<string, TypeMapping>(StringComparer.OrdinalIgnoreCase);
            var sqlServerRows = new Dictionary<string, TypeMapping>(StringComparer.OrdinalIgnoreCase);
            foreach (var library in libraries)
            {
                if ((mask & (1 << (int)library)) == 0)
                {
                    continue;
                }

                var (postgres, sqlServer) = RowsOf(library);
                foreach (var row in postgres)
                {
                    rows[row.Key] = row.Value;
                }

                foreach (var row in sqlServer)
                {
                    sqlServerRows[row.Key] = row.Value;
                }
            }

            sets[mask] = new LibraryRows(rows, sqlServerRows);
        }

        return sets;
    }

    // The rows of one library, for PostgreSQL and for SQL Server.
    private static (Dictionary<string, TypeMapping> Postgres, Dictionary<string, TypeMapping> SqlServer) RowsOf(
        SqlLibrary library
    ) =>
        library switch
        {
            SqlLibrary.Json => (JsonRows(), JsonSqlServerRows()),
            SqlLibrary.NodaTime or _ => (NodaTimeRows(), NodaTimeSqlServerRows()),
        };

    // JSON is System.Text.Json's JsonElement on both engines: json and jsonb on PostgreSQL, json on SQL Server.  The
    // base map has the other types that read as text, jsonpath and xml, as a string, and keeps them.
    private static Dictionary<string, TypeMapping> JsonRows() => JsonRowsOf("json", "jsonb");

    private static Dictionary<string, TypeMapping> JsonSqlServerRows() => JsonRowsOf("json");

    private static Dictionary<string, TypeMapping> JsonRowsOf(params string[] keys)
    {
        var rows = new Dictionary<string, TypeMapping>(StringComparer.OrdinalIgnoreCase);
        var place = PlaceOf(SqlLibrary.Json);
        foreach (var key in keys)
        {
            rows.Add(
                key,
                TypeMapping.Mapped(key, "global::System.Text.Json.JsonElement", true) with
                {
                    Detail = place,
                    Library = SqlLibrary.Json,
                }
            );
        }

        return rows;
    }

    private static Dictionary<string, TypeMapping> NodaTimeSqlServerRows()
    {
        const string Noda = "global::NodaTime.";
        var rows = new Dictionary<string, TypeMapping>(StringComparer.OrdinalIgnoreCase);
        var place = PlaceOf(SqlLibrary.NodaTime);

        void Add(string text, params string[] keys)
        {
            foreach (var key in keys)
            {
                rows.Add(
                    key,
                    TypeMapping.Mapped(key, Noda + text, true) with
                    {
                        Detail = place,
                        Library = SqlLibrary.NodaTime,
                    }
                );
            }
        }

        Add("LocalDate", "date");
        Add("LocalTime", "time");
        Add("LocalDateTime", "datetime2", "datetime", "smalldatetime");
        Add("OffsetDateTime", "datetimeoffset");
        return rows;
    }

    private static Dictionary<string, TypeMapping> NodaTimeRows()
    {
        const string Noda = "global::NodaTime.";
        const string Range = "global::NpgsqlTypes.NpgsqlRange<" + Noda;
        var rows = new Dictionary<string, TypeMapping>(StringComparer.OrdinalIgnoreCase);
        var place = PlaceOf(SqlLibrary.NodaTime);

        void Add(string key, string text, bool isValueType) =>
            rows.Add(
                key,
                TypeMapping.Mapped(key, text, isValueType) with
                {
                    Detail = place,
                    Library = SqlLibrary.NodaTime,
                }
            );

        Add("timestamptz", Noda + "Instant", true);
        Add("timestamp", Noda + "LocalDateTime", true);
        Add("date", Noda + "LocalDate", true);
        Add("time", Noda + "LocalTime", true);
        Add("timetz", Noda + "OffsetTime", true);
        Add("interval", Noda + "Period", false);
        Add("tstzrange", Range + "Instant>", true);
        Add("tsrange", Range + "LocalDateTime>", true);
        Add("daterange", Range + "LocalDate>", true);
        Add("tstzmultirange", Range + "Instant>[]", false);
        Add("tsmultirange", Range + "LocalDateTime>[]", false);
        Add("datemultirange", Range + "LocalDate>[]", false);
        return rows;
    }
}
