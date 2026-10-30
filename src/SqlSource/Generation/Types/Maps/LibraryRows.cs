using System;
using System.Collections.Generic;
using SqlSource.Settings.Libraries;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Types.Maps;

/// <summary>
/// The rows of the libraries that are on, which <see cref="PostgresTypeMap.Map" /> and
/// <see cref="SqlServerTypeMap.Map" /> ask after the type override and before the base map.  A row gives a library's
/// own C# type for a type of <c>pg_catalog</c>, a base type of PostGIS in any schema, a system type of SQL Server or
/// one of SQL Server's spatial CLR types of <c>sys</c>, and writes it <c>global::</c>-qualified: the generator names no
/// library type itself.  Its <c>Detail</c> is the library as a place, <see cref="PlaceOf" />, so that the type has a
/// place and the base map's type beside it, which a converter converts from and to.
/// </summary>
internal sealed class LibraryRows
{
    // PostGIS's types, by their InternalName.
    private const string Geometry = "geometry";

    private const string Geography = "geography";

    // PostGIS's rows, by InternalName, which every set of libraries with NetTopologySuite shares.  Declared before
    // Sets, which reads it when the type is initialized.
    private static readonly Dictionary<string, TypeMapping> PostgisRows = BuildPostgisRows();

    // SQL Server's spatial CLR types of sys, by name, which every set of libraries with NetTopologySuite shares.  Also
    // declared before Sets.
    private static readonly Dictionary<string, TypeMapping> SqlServerSpatialRows = BuildSqlServerSpatialRows();

    // One instance for each set of libraries, found by the bit of each library, which is 1 << its number: built once,
    // when the type is initialized, so that a set found twice is the same instance.  Slot 0 is the empty set, which
    // has no instance.
    private static readonly LibraryRows?[] Sets = BuildSets();

    private readonly Dictionary<string, TypeMapping> _rows;
    private readonly Dictionary<string, TypeMapping>? _postgisRows;
    private readonly Dictionary<string, TypeMapping> _sqlServerRows;
    private readonly Dictionary<string, TypeMapping>? _sqlServerSpatialRows;

    private LibraryRows(
        Dictionary<string, TypeMapping> rows,
        Dictionary<string, TypeMapping>? postgisRows,
        Dictionary<string, TypeMapping> sqlServerRows,
        Dictionary<string, TypeMapping>? sqlServerSpatialRows
    )
    {
        _rows = rows;
        _postgisRows = postgisRows;
        _sqlServerRows = sqlServerRows;
        _sqlServerSpatialRows = sqlServerSpatialRows;
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
    /// Whether <paramref name="type" /> is a type of PostGIS, <c>geometry</c> or <c>geography</c>: a base type of that
    /// name, ignoring case, in any schema, since the extension is installed in a schema of the user's choice.  A domain
    /// or a composite of the same name is not one.
    /// </summary>
    public static bool IsPostgis(PostgresType type) =>
        type.Kind.Equals(SidecarValues.Kind.Base, StringComparison.Ordinal)
        && (
            type.InternalName.Equals(Geometry, StringComparison.OrdinalIgnoreCase)
            || type.InternalName.Equals(Geography, StringComparison.OrdinalIgnoreCase)
        );

    /// <summary>
    /// The library as the place that chose a type, which <c>SQLSRC250</c> to <c>SQLSRC252</c> word: <c>the NodaTime
    /// library</c> or <c>the NetTopologySuite library</c>.
    /// </summary>
    public static string PlaceOf(SqlLibrary library) =>
        library switch
        {
            SqlLibrary.NetTopologySuite => "the NetTopologySuite library",
            SqlLibrary.NodaTime or _ => "the NodaTime library",
        };

    /// <summary>
    /// The library's mapping of <paramref name="type" />, or null when it has none: a type of <c>pg_catalog</c> that
    /// a row names, by its <c>InternalName</c> ignoring case, as the base map's rows are, or a type of PostGIS,
    /// <see cref="IsPostgis" />, in any schema.
    /// </summary>
    public TypeMapping? Find(PostgresType type)
    {
        if (PostgresTypeMap.IsSystemType(type) && _rows.TryGetValue(type.InternalName, out var row))
        {
            return row;
        }

        return
            _postgisRows is not null && IsPostgis(type) && _postgisRows.TryGetValue(type.InternalName, out var postgis)
            ? postgis
            : null;
    }

    /// <summary>
    /// The library's mapping of <paramref name="type" />, or null when it has none: for a CLR user type, one of
    /// <c>sys</c> that a spatial row names, <c>geography</c> or <c>geometry</c>, by its name ignoring case; for any
    /// other type, a system type that a row names, by its <see cref="SqlServerTypeMap.BaseName" /> ignoring case,
    /// which drops its facets.  The caller asks for an alias type by its base, without the user type.
    /// </summary>
    public TypeMapping? Find(SqlServerType type)
    {
        if (type.UserType is { Name.Length: > 0 } userType)
        {
            return
                _sqlServerSpatialRows is not null
                && !string.IsNullOrEmpty(userType.AssemblyQualifiedName)
                && SystemSchemas.IsSqlServer(userType.Schema)
                && _sqlServerSpatialRows.TryGetValue(userType.Name, out var spatial)
                ? spatial
                : null;
        }

        return
            !string.IsNullOrEmpty(type.Name)
            && _sqlServerRows.TryGetValue(SqlServerTypeMap.BaseName(type.Name), out var row)
            ? row
            : null;
    }

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

            var hasNetTopologySuite = (mask & (1 << (int)SqlLibrary.NetTopologySuite)) != 0;
            sets[mask] = new LibraryRows(
                rows,
                hasNetTopologySuite ? PostgisRows : null,
                sqlServerRows,
                hasNetTopologySuite ? SqlServerSpatialRows : null
            );
        }

        return sets;
    }

    // The rows of one library, for PostgreSQL's pg_catalog and for SQL Server.  PostGIS's rows match in any schema, and
    // SQL Server's spatial rows only a CLR type of sys, so they are apart, in PostgisRows and SqlServerSpatialRows.
    private static (Dictionary<string, TypeMapping> Postgres, Dictionary<string, TypeMapping> SqlServer) RowsOf(
        SqlLibrary library
    ) =>
        library switch
        {
            SqlLibrary.NetTopologySuite => (Empty(), Empty()),
            SqlLibrary.NodaTime or _ => (NodaTimeRows(), NodaTimeSqlServerRows()),
        };

    // No rows, which a set only reads to copy, so it needs no comparer: NetTopologySuite has none in pg_catalog, and no
    // row of a SQL Server system type that is not a CLR type.
    private static Dictionary<string, TypeMapping> Empty() => [];

    // PostGIS's geometry and geography are both NetTopologySuite's Geometry, which the plugin reads and writes.  The
    // base map has no C# type for either.
    private static Dictionary<string, TypeMapping> BuildPostgisRows() => GeometryRows(Geometry, Geography);

    // SQL Server's geography and geometry, CLR types of sys, are both NetTopologySuite's Geometry, which the method
    // reads and binds through NetTopologySuite.IO.SqlServerBytes.  The base map has no C# type for either without
    // Microsoft.SqlServer.Types.
    private static Dictionary<string, TypeMapping> BuildSqlServerSpatialRows() =>
        GeometryRows(SqlServerNames.Geography, SqlServerNames.Geometry);

    // A row of NetTopologySuite's Geometry, a reference type, for each key.
    private static Dictionary<string, TypeMapping> GeometryRows(params string[] keys)
    {
        var rows = new Dictionary<string, TypeMapping>(StringComparer.OrdinalIgnoreCase);
        var place = PlaceOf(SqlLibrary.NetTopologySuite);
        foreach (var key in keys)
        {
            rows.Add(
                key,
                TypeMapping.Mapped(key, "global::NetTopologySuite.Geometries.Geometry", false) with
                {
                    Detail = place,
                    Library = SqlLibrary.NetTopologySuite,
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
