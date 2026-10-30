# The spike of phase 7, the library maps - appendix

Date: 2026-10-30

Appendix to [the phase 7 design](2026-10-26-query-generation-phase-7-library-maps-design.md) of the [query generation epic](2026-10-07-query-generation-epic-design.md), written by sub-phase 7.0.  Before any generator code of the library maps, throwaway console programs ran the calls the generated methods will make: through Npgsql's NodaTime and NetTopologySuite plugins, through the JSON types on both drivers, through PostGIS, and through `NetTopologySuite.IO.SqlServerBytes`.  Each engine ran on its floor and default image with its floor and newest driver.  A metadata reader went through every package version the probe's markers and the libraries' floors depend on.  Together they answer the six questions of the design's 7.0 and its "Verify first" items.  The programs were not kept; what they ran and what they answered are here.  Each answer is what the code did; where the combinations differ, the section says which does what.

## What ran

| | Version |
|----|----|
| PostgreSQL, default | `postgres:18.6`, `SHOW server_version` = `18.6 (Debian 18.6-1.pgdg13+2)` |
| PostgreSQL, floor | `postgres:14.24`, `14.24 (Debian 14.24-1.pgdg13+2)` |
| PostGIS | Images built from `postgres:14.24`, `15.19`, `16.15`, `17.11` and `18.6` with PostGIS `3.6.4+dfsg-2.pgdg13+1`, on arm64 and amd64 ([question 4](#4-postgis)); `postgis_full_version()` = `POSTGIS="3.6.4 94d984b"` |
| SQL Server, default | `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04`, `ProductVersion` `17.0.5015.4` |
| SQL Server, floor | `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`, `14.0.3560.2` |
| Npgsql, with `Npgsql.NodaTime` and `Npgsql.NetTopologySuite` of the same version | `10.0.3` and `8.0.9`; `9.0.5` for the questions that name Npgsql 9 |
| NodaTime | `3.1.9` with `Npgsql.NodaTime` 8.0.9, `3.2.0` with 9.0.5, `3.2.2` with 10.0.3, each the plugin's own dependency; `3.3.7` on SQL Server |
| NetTopologySuite | `2.5.0` with `Npgsql.NetTopologySuite` 8.0.9, `2.6.0` with 10.0.3 and on SQL Server |
| `NetTopologySuite.IO.SqlServerBytes` | `2.1.0` |
| Microsoft.Data.SqlClient | `7.1.1` and `6.1.7` |
| System.Text.Json | `10.0.12`, the host's |
| Host | .NET 10.0.12, `net10.0`, Apple silicon, Docker 29.4; SQL Server and the amd64 PostGIS builds under emulation |

Each engine's probes were one source file linked into one console project per driver version, outside the repository, so that no central package version applied.  Every probe caught its own exception and printed the call, the value with its runtime type, or the exception's type and message.  Connections were `DbConnection`s and commands came from `CreateCommand()`, as generated code makes them.  PostgreSQL parameters were `NpgsqlParameter<T>` with `DataTypeName` set, as 8.3 binds them; SQL Server parameters were `SqlParameter`s with the `SqlDbType` and facets 8.4 gives them.  A surprising result was checked a second way: a value read back by the server's own text, a T-SQL literal through `sqlcmd`, or `new SqlDateTime(...)`.  The markers and floors were read from each package's `.nupkg` with `System.Reflection.Metadata`; nothing of a package was loaded.

**The servers agreed on every probe of each driver**, but for `json`, which SQL Server 2017 lacks.  **So did the drivers**, but for `JsonNode` on Npgsql before 10 ([question 3](#3-json)).  The full and the slim data source builder agreed throughout.  So each answer below holds on every combination of its engine unless it says otherwise.

## 1. NodaTime on PostgreSQL

The calls: `NpgsqlDataSourceBuilder` and `NpgsqlSlimDataSourceBuilder`, the slim one with `EnableArrays()`, `EnableRanges()` and `EnableMultiranges()`, each with `UseNodaTime()`.  Each value went in by `NpgsqlParameter<T> { DataTypeName = "<type>" }` into a column of the type, and came out by `GetFieldValue<T>`.

- **Every default of the design's table is what the plugin boxes**, on Npgsql 8, 9 and 10, full and slim.  `GetFieldType` and `GetValue` give `Instant`, `LocalDateTime`, `LocalDate`, `LocalTime`, `OffsetTime`, `Period`, `Interval`, `NpgsqlRange<LocalDateTime>` and `DateInterval`, and `Interval[]`, `NpgsqlRange<LocalDateTime>[]` and `DateInterval[]` for the multiranges.
- **Every default round-trips equal** through `NpgsqlParameter<T>` and `GetFieldValue<T>`: a value; `T[]` of `<type>[]`; an array holding a `NULL`, as `T?[]`, or as `T[]` with a null element for the reference types `Period` and `DateInterval`; and a `NULL`.  A `NULL` is written as `TypedValue = null` and read as `null` by `GetFieldValue<T?>`.  So do arrays of multiranges, `Interval[][]` of `tstzmultirange[]`.  `Period` keeps its units (`P1Y2M3DT4H5M6S789s`).  `DateInterval`'s inclusive end is stored as `[start,end+1)` and read back equal.
- **The override alternatives round-trip equal**: `timestamptz` as `ZonedDateTime` (read with zone UTC), `OffsetDateTime` (offset 0), `DateTimeOffset` and `DateTime` (`Kind` `Utc`); `interval` as `Duration`; `timestamp` as `DateTime` (`Kind` `Unspecified`).
  - An `interval` with months read as `Duration` throws `InvalidCastException: Cannot read PostgreSQL interval with non-zero months to NodaTime Duration. Try reading as a NodaTime Period instead.`
  - A `ZonedDateTime` or `OffsetDateTime` that is not UTC fails before the query is sent.  The exception is **`ArgumentException`**, not `InvalidCastException`: `Cannot write ZonedDateTime with Zone=Europe/Berlin to PostgreSQL type 'timestamp with time zone', only UTC is supported.` and `Cannot write OffsetDateTime with Offset=+05:30 ... only offset 0 (UTC) is supported.`
- **The ranges.**  These led to owner decision 1 of [What the later sub-phases take from this](#what-the-later-sub-phases-take-from-this): the defaults became `NpgsqlRange<Instant>` and `NpgsqlRange<LocalDate>`.
  - **An empty range cannot be read as `Interval` or `DateInterval`.**  An empty `tstzrange` throws `ArgumentOutOfRangeException: The end parameter must be equal to or later than the start parameter (Parameter 'end')`.  An empty `daterange` throws `ArgumentException: End date must not be earlier than the start date (Parameter 'end')`.  Both come from `GetFieldValue<T>`, `GetFieldValue<T?>` and the boxed `GetValue` alike, so a Dapper reader on a data source with the plugin fails the same way.
  - **A `(a,b]` `tstzrange` reads as the `Interval` `[a+1ns, b+1ns)`**, since `Interval` is half-open.  Written back, PostgreSQL's microseconds drop the nanosecond and store `[a,b)`, so a read and a write of the same value change the range.
  - Unbounded ranges read as an `Interval` without that start or end, both ways.
  - **`NpgsqlRange<Instant>` for `tstzrange` and `NpgsqlRange<LocalDate>` for `daterange`**, and their arrays for the two multiranges, are read and written by the plugin on Npgsql 8 and 10.  They round-trip an empty range (`IsEmpty`), an unbounded one, `[a,b)` and a `NULL` equal.  `(a,b]` keeps its bounds on `tstzrange`; PostgreSQL itself canonicalizes it to `[a+1,b+1)` on `daterange`.
  - **A multirange never holds an empty range.**  PostgreSQL drops it whether the driver writes it, a literal holds it or `tstzmultirange(...)` builds it, so `Interval[]` and `DateInterval[]` never meet the empty case.  The 1 ns shift of a `(a,b]` `tstzrange`, above, still applies to `Interval[]`.
- **Arrays report `System.Array`.**  For an array column, `GetFieldType` and `GetColumnSchema()[i].DataType` give `System.Array`, not `T[]`, while `GetValue` gives `T[]`.  A multirange itself reports `Interval[]`.  This is the driver's, with or without a plugin; the generated code reads by `GetFieldValue<T[]>` and never asks.
- **Without `UseNodaTime()`**, full or slim, `GetFieldType` of `timestamptz` is `System.DateTime`.  Reading an `Instant` throws `System.InvalidCastException: Reading as 'NodaTime.Instant' is not supported for fields having DataTypeName 'timestamp with time zone'`.  Writing one throws `System.InvalidCastException: Writing values of 'NodaTime.Instant' is not supported for parameters having DataTypeName 'timestamp with time zone'.`  The words are the same on 8 and 10; they are the README FAQ's case.
- **The plugin that pairs with Npgsql 8** is `Npgsql.NodaTime` 8.0.x, whose every version depends on NodaTime 3.1.9 or later ([question 6](#6-the-probes-markers-and-the-libraries-floors)).

## 2. NodaTime on SQL Server

The calls: each edge of the design's table, by its exact read and write expressions.  Each value was bound as 8.4 binds it, `SqlDbType` `Date`, `Time`, `DateTime2`, `DateTime`, `SmallDateTime` or `DateTimeOffset`, with `Scale` 7 where the type takes one.  It was written into a column of each database type the table names, read by `GetFieldValue<B>` and converted back.  The stored text was read by `CONVERT(nvarchar(40), col, 121)`.

- **Every default reads back without loss inside its type's range.**  The minimum and maximum of `date`, `time(7)`, `datetime2(7)` and `datetimeoffset(7)` round-trip, and so do the bounds of `datetime` (`1753-01-01`, `9999-12-31 23:59:59.997`) and `smalldatetime` (`1900-01-01`, `2079-06-06 23:59`).  `time` `00:00` and `23:59:59.9999999` round-trip.
- **Nanoseconds below a tick are truncated by the conversion on write**, never rounded: `.999999999` is written as `.9999999`.
- **SqlClient rounds `datetime` and `smalldatetime` before it sends**, by `SqlDbType`: `.1234567` becomes `.123`, `12:34:56` becomes `12:35`, and `23:59:59.999` rolls over to the next day.  It also refuses values out of range before it sends: `SqlTypeException` for `datetime` before 1753, and `OverflowException` for `smalldatetime` outside its range or rounding past it (`2079-06-06 23:59:30`).
- **SqlClient's `datetime` rounding at the top of the range is uneven.**  `9999-12-31 23:59:59.999` throws `OverflowException: SqlDateTime overflow`, but `DateTime.MaxValue` (`.9999999`) is stored as `.997`; `new SqlDateTime(...)` agrees.  The server's own conversion of a `datetime2` stores `.997` for both.
- **An offset with seconds is changed without an error.**  `OffsetDateTime.ToDateTimeOffset()` truncates `+05:30:15` to `+05:30` and keeps the local time, so the stored instant moves by 15 seconds.  An offset beyond `DateTimeOffset`'s, `+18:00`, throws `InvalidOperationException: Offset out of range for DateTimeOffset`.  A local minimum at `+14:00` and a local maximum at `-14:00`, whose instant `DateTimeOffset` cannot hold, throw `ArgumentOutOfRangeException`.  `-14:00` and `+14:00` themselves round-trip.
- **A value outside `DateTime`'s range fails in the conversion on write**, with an exception that differs by edge.  `LocalDate.ToDateTimeUnspecified()` throws `ArgumentOutOfRangeException`.  `LocalDateTime.ToDateTimeUnspecified()`, `Instant.ToDateTimeUtc()` and `Instant.ToDateTimeOffset()` throw `InvalidOperationException`.
- **A `Duration` outside `time` fails in SqlClient**, not at the server.  24 hours, 25 hours and -1 second each throw `OverflowException: SqlDbType.Time overflow`.
- **`Instant` through `datetime2` round-trips.**  `ToDateTimeUtc()` sends `Kind` `Utc` unchanged, SqlClient reads `Kind` `Unspecified`, and `DateTime.SpecifyKind` makes the read work.  `Instant` through `datetimeoffset` drops a stored offset of `-05:00` and keeps the instant.

## 3. JSON

**PostgreSQL**, with no `EnableDynamicJson()` unless a case says so.  Each `T` was written by `NpgsqlParameter<T> { DataTypeName = "json" | "jsonb" }` and read by `GetFieldValue<T>`.

- **`JsonElement`, `JsonDocument` and `string` are read and written by the driver** with `DataTypeName` `json` and `jsonb`, on Npgsql 8, 9 and 10.  `jsonb` gives back PostgreSQL's normalized text, `{"Id": 1, "Name": "Widget"}`, so a `string` read differs from what was sent in white space.
- **`JsonNode`, `JsonObject` and `JsonArray` are read and written by the driver on Npgsql 10 alone.**  On 8 and 9 both directions throw `InvalidCastException` (`Writing values of 'System.Text.Json.Nodes.JsonObject' is not supported ...`, `Reading as 'System.Text.Json.Nodes.JsonNode' is not supported ...`), with an inner `NotSupportedException` naming `EnableDynamicJson`.  With `EnableDynamicJson()` they work on all three.  This is the design's table.
- **A JSON `null`** (`'null'::jsonb`, which is not `IsDBNull`) reads as a `JsonElement` of `ValueKind` `Null` on all three, and as a C# `null` `JsonNode` wherever `JsonNode` is read.
- **`jsonb[]`** round-trips as `JsonElement[]` on all three.
- **A class needs the opt-in through the driver.**  `GetFieldValue<OrderMetadata>` and a parameter of it throw `InvalidCastException` with an inner `NotSupportedException` on all three, and round-trip with `EnableDynamicJson()`.
- **The serializer's route needs no opt-in.**  A `string` of `JsonSerializer.Serialize(OrderMetadata)` sent with `DataTypeName` `jsonb` is `pg_typeof` `jsonb`, and with `json` is `json`.  Read back by `JsonSerializer.Deserialize<OrderMetadata>(reader.GetFieldValue<string>(i))` it is equal, on 8, 9 and 10.

**SQL Server.**  A `string` was bound as `SqlDbType.NVarChar` with `Size` -1 and read by `GetFieldValue<string>`.

- **`json` on 2025 and `nvarchar(max)` on 2017 and 2025 are read as `string`.**  `GetFieldType` of `json` is `System.String` and `GetDataTypeName` is `json`; its provider-specific type is `System.String`, where `nvarchar(max)`'s is `SqlString`.  2017 has no `json`: `CREATE TABLE` fails with error 2715.
- **2025's `json` refuses every top-level scalar**, a JSON `null` included: `null`, `"text"` and `1.10` are refused with error 13609 `JSON text is not properly formatted`.  `sqlcmd` agrees for `INSERT N'null'`, `DECLARE @x json = N'null'` and `CAST(N'"text"' AS json)`.  So a `JsonElement` of kind `Null`, and `JsonSerializer.Serialize` of a value that serializes to a scalar, cannot be written to a `json` column.  `nvarchar(max)` stores and reads all of them.
- **2025's `json` normalizes the text it stores.**  White space goes, escapes such as `A` are unescaped, and of duplicate keys the first is kept: `{"a":1,"a":2}` reads `{"a":1}`.  A `JsonElement` still round-trips its structure.
- **The design's generated reads and writes round-trip** through `json` and `nvarchar(max)`, for `JsonElement` (`JsonDocument.Parse(v)` and `RootElement.Clone()`, `GetRawText()`), `JsonDocument`, `JsonNode`, `JsonObject` and `JsonArray` (`JsonNode.Parse`, `ToJsonString()`), and `OrderMetadata` by the serializer.
- **A `JsonNode` cannot hold a JSON `null`.**  `JsonNode.Parse("null")` returns a C# `null`, so a stored JSON `null` reads like a SQL `NULL`.  On the write side `ToJsonString()` cannot be called on it.
- **A wrong shape fails at the cast**: `(JsonObject?)JsonNode.Parse(v)` of an array, and the reverse, throw `InvalidCastException`.
- **The serializer reads camelCase JSON as empty properties**, with no error, as the design says.  `Deserialize<OrderMetadata>("null")` is `null`, and of an array throws `JsonException`.

## 4. PostGIS

**The image.**  A Dockerfile from the pinned `postgres` image, installing PostGIS by an exact package version from the image's own pgdg source, or from pgdg's archive once that version has left it:

```dockerfile
ARG PG=18.6
FROM postgres:${PG}

ARG POSTGIS=3.6.4+dfsg-2.pgdg13+1

# The archive line is the image's own pgdg line, with its key, pointed at apt-archive.postgresql.org, which keeps every
# version pgdg has published; it needs https, so ca-certificates comes first.
RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends ca-certificates; \
    sed -n 's|^deb \(\[[^]]*\]\) http://apt.postgresql.org/pub/repos/apt \([a-z]*\)-pgdg .*|deb \1 https://apt-archive.postgresql.org/pub/repos/apt \2-pgdg-archive main|p' \
        /etc/apt/sources.list.d/pgdg.list > /etc/apt/sources.list.d/pgdg-archive.list; \
    test -s /etc/apt/sources.list.d/pgdg-archive.list; \
    apt-get update; \
    apt-get install -y --no-install-recommends \
        "postgresql-${PG_MAJOR}-postgis-3=${POSTGIS}" \
        "postgresql-${PG_MAJOR}-postgis-3-scripts=${POSTGIS}"; \
    rm -rf /var/lib/apt/lists/*
```

- **It meets root `AGENTS.md` rule 5.**  It built on `postgres:14.24`, `15.19`, `16.15`, `17.11` and `18.6`, natively on arm64 and as `linux/amd64`: 10 of 10, the slowest amd64 build 61 seconds.  The PostgreSQL version is the base tag's and PostGIS's an exact package version.  `SHOW server_version` equals the base tag on all ten, the PostGIS step upgraded or removed no package of the base, and `postgis_full_version()` is 3.6.4 everywhere.
- **The rest of what PostGIS brings is not pinned.**  It adds 95 packages, the same set and versions on every major and both architectures.  Six come from pgdg and move with it: `libgeos3.14.1` and `libgeos-c1t64` `3.14.1-2.pgdg13+1`, `libproj25` and `proj-data` `9.8.1-1.pgdg13+1`, `gdal-data` and `gdal-plugins` `3.13.2+dfsg-1.pgdg13+1`.  87 come from Debian trixie and trixie-security, as the base image's own packages do.  `postgis_full_version()` reports GEOS 3.14.1 and PROJ 9.8.1 at run time against 3.13.1 and 9.6.0 at compile time, so a later build may bring a newer GEOS or PROJ under the same PostGIS.
- **A pinned version outlives its removal from `apt.postgresql.org`.**  `apt-archive.postgresql.org`'s `trixie-pgdg-archive` lists `postgresql-1[4-8]-postgis-3` and `-scripts` at 3.6.4 and 3.6.2 on both architectures, with the six pgdg dependencies.  The live `apt.postgresql.org` lists 3.6.2, 3.6.3 and 3.6.4 today.
  - Installing from the archive needs `ca-certificates` first: the base images have none, and the archive redirects `http` to `https`, so `apt-get update` fails with `certificate verify failed`.
  - The probes ran on a first form of the Dockerfile with no archive line, which installed 3.6.4 from `apt.postgresql.org` alone; that form fails with `Version ... was not found` once its version has left.  The Dockerfile above adds the archive, and built on `postgres:14.24` and `18.6` with 3.6.4 and with 3.6.1, a version the live source no longer lists, leaving each server's version as its base tag's.

**Through the plugin.**  Npgsql and `Npgsql.NetTopologySuite` 8.0.9 and 10.0.3 on the 14.24 and 18.6 PostGIS images, on a data source with `UseNetTopologySuite()`; all four agree but for one message.

- **Reads.**  `GetFieldType` is `Geometry`, and `GetValue` the concrete subtype, for `geometry`, `geography`, `geometry(Point,4326)`, `geography(Point,4326)` and a domain over `geometry`.  `GetFieldValue<Geometry>` and `<Point>` read a point and `<Polygon>` a polygon; a shape of another kind throws `InvalidCastException: Unable to cast object of type 'Point' to type 'Polygon'`.
- **Arrays.**  `geometry[]` reads as `Geometry[]` with a null element kept; `Point[]` throws when an element is not a point.  `GetFieldType` is `System.Array`, as in question 1.
- **SRID** is kept both ways, 4326 and 3857.  A `geography` of SRID 0 is stored as 4326, and one of 3857 is refused by the server: `22023: Only lon/lat coordinate systems are supported in geography`.  A point of SRID 0 into `geometry(Point,4326)` takes the column's 4326; a polygon into it is refused, `22023: Geometry type (Polygon) does not match column type (Point)`.
- **Z and M are kept** with the default `UseNetTopologySuite()`, on `geometry` and `geography`: `POINT ZM(1 2 3 4)` reads as a `CoordinateZM` with both, and one written comes back with `ST_NDims` 4.  NetTopologySuite's `AsText()` drops the M; the coordinates hold it.
- **`NULL`.**  A null `TypedValue` of `NpgsqlParameter<Geometry>` or `<Geometry[]>` is sent as a `NULL` of the named type.  `GetFieldValue<Geometry>` of a `NULL` throws `InvalidCastException: Column 'geometry' is null.`, as for every type.
- **`DataTypeName`.**  `geometry`, `public.geometry`, `geography`, `public.geography`, `geometry[]`, `public._geometry`, a domain's name with or without its schema, and `geometry(Point,4326)` all bind.  So do `NpgsqlParameter<Point>` and `<Polygon>`.
- **PostGIS in another schema**, `CREATE EXTENSION postgis SCHEMA gis` with `gis` not on the `search_path`, reads the same, with `GetDataTypeName` `gis.geometry`.  `DataTypeName` `geometry`, `gis.geometry`, `geography`, `gis.geography`, `geometry[]`, `gis._geometry` and `gis.geometry[]` all bind, and the server sees `gis.geometry`.  `public.geometry` throws `NotSupportedException`.
- **Without the plugin**, every read throws, `GetValue` and `GetFieldType` included: `InvalidCastException: Reading as 'System.Object' is not supported for fields having DataTypeName 'public.geometry'`.  `GetDataTypeName` still answers.  A write throws `Writing values of 'NetTopologySuite.Geometries.Geometry' is not supported for parameters having DataTypeName 'public.geometry'`.

**The describer.**  The tool's `describe --force`, on 14.24 and 18.6 with PostGIS in `public` and in `gis`, gave the same entries but for the server's version.

- **A column** of PostGIS has `kind` `base`, `schema` `public` or `gis`, `internalName` `geometry` or `geography`, and a `name` with the schema and the modifier: `public.geometry(Point,4326)`, `gis.geometry(Point,4326)`.  An array is `kind` `array`, `internalName` `_geometry`, with an `element`; a domain is `kind` `domain` with a `base`.
- **A parameter**, once the check passes (below), has a `bindName` equal to its `name`: `public.geometry`, `gis.geography`, `gis.geometry[]`, `public.geom_d`.  It carries no modifier, as PostgreSQL gives none for a parameter.  Each of those names bound in the plugin probes above.
- **How a library row matches.**  By `internalName` with `kind` `base`, in any schema, as `PostgresTypeMap.Map` matches the extension rows `citext` and `hstore` through `TypeKeyNames.IsExtension`.  The `name` and the map's `KeyOf` carry the schema, and the `name` the modifier, so neither matches both schemas.  The same list decides whether a type override's bare key `geometry` reaches `gis.geometry` (`QueryTyper.EntryOf`).
- **A modifier cannot be written in a `-- param:` marker.**  `-- param: @v geometry(Point,4326)` is `SQLSRC228 ... it is not written as a type name`, since the marker's modifier takes digits alone (`DeclaredTypeText.TryReadModifier`).  The parameter's type is `geometry` either way.  An unqualified `geometry` marker where PostGIS is in `gis` and `gis` is not on the `search_path` is `SQLSRC228 ... the database has no such type` (`42704`), as for any type.

**The tool's check.**

- **The tool refuses every PostGIS parameter as `SQLSRC232`**, in both schemas, for `geometry`, `geography`, a typmod column's parameter, `geometry[]` and a domain.  The message: `Npgsql cannot bind the type 'public.geometry' of the parameter '@g' of the query 'InsertGeometry'`, with the server line `Writing is not supported for parameters having DataTypeName 'public.geometry'.`  A refused query keeps its whole file's sidecar from being written (`SQLSRC217`), as for any refused query.
- **With `Npgsql.NetTopologySuite` 10.0.3 and `builder.UseNetTopologySuite()` in `PostgresConnection.BuildSource`, every PostGIS parameter is described and checked**, in both schemas.  The column entries are byte for byte those of the unchanged tool.
- **The plugin changes nothing else.**  A parameter of `seg`, an extension type the base map lacks, is `SQLSRC232` with the same message before and after.  On plain `postgres:18.6`, with no `geometry` type, the changed tool describes and exits 0.  The change also changes `src/SqlSource.Tool/packages.lock.json`.  It was reverted after the probe.

## 5. NetTopologySuite on SQL Server

Both servers and both SqlClients gave identical lines.  Microsoft.SqlServer.Types was not referenced and never loaded.

- **The read path works.**  A geometry is read by `SqlServerBytesReader { IsGeography = true | false }.Read(((SqlDataReader)reader).GetSqlBytes(i).Value)`.  It round-trips points, polygons, a line string and a multipoint, and the server's bytes equal the writer's.
- **Without Microsoft.SqlServer.Types**, `GetFieldValue<SqlBytes>`, `GetFieldValue<byte[]>` and `GetValue` throw `FileNotFoundException` for `Microsoft.SqlServer.Types, Version=10.0.0.0`, as the design says.  `GetFieldType` and `GetProviderSpecificFieldType` return `null`; `GetDataTypeName` is `tempdb.sys.geography` or `geometry`.
- **`DbDataReader.GetStream(i)` and `GetBytes` return the same bytes with no cast to `SqlDataReader`**, and `SqlServerBytesReader.Read(Stream)` exists.  So the read may not need the design's cast.  This was not run through a connection that wraps SqlClient's.
- **The write needs no Microsoft.SqlServer.Types.**  `SqlDbType.Udt`, `UdtTypeName` `geography` or `geometry`, and `Value = new SqlBytes(writer.Write(g))` insert, and `SqlBytes.Null` inserts a `NULL`.  On a `NULL` row `GetSqlBytes(i).IsNull` is true and `.Value` throws `SqlNullValueException`, so `IsDBNull` comes first.
- **The writer throws for a clockwise `geography` polygon**: `ArgumentException: ... the shell of a polygon must be oriented counter-clockwise`.  A clockwise `geometry` polygon is written.
- **A `geography` of SRID 0, NetTopologySuite's default, is refused by the server**, with an unhelpful TDS error 8023, `... not a valid instance of data type geography`.  `geometry` keeps SRID 0.
- **Z and M are kept by default.**  The reader's and the writer's `HandleOrdinates` are both `XYZM`.  A ZM point round-trips with the defaults, and the server reports `HasZ` 1 and `HasM` 1; with `XY` both are dropped.  So the generated instances need no ordinates setting.
- **A subtype is the cast.**  `(Point)Read(...)` reads a point, and `(Polygon)` of a point throws `InvalidCastException`.
- **Shared instances are safe across threads.**  The source of 2.1.0 shows that `Read` and `Write` write no instance field, and the fields reflection lists match it.  One shared reader and one shared writer, used by 16 tasks of 10 000 iterations each over mixed shapes, gave no mismatch and no exception against the single-threaded results.  Each iteration was a `Write`, a `Read` of the server's bytes and a `Read` of its own, in memory, without a round trip to the server.
- **The reader takes `NtsGeometryServices.Instance` when it is constructed, not when it reads.**  A reader built before `Instance` was replaced kept the old coordinate sequence factory and precision model; one built after took the new ones.  The writer uses no services.

## 6. The probe's markers and the libraries' floors

Each package was read in the `lib/` folder a `net8.0` project takes.

**The markers**, all public, all found by the full metadata name `DriverProbes.Has` takes:

| Package | Marker | In |
|----|----|----|
| `NodaTime` | `NodaTime.Instant` | Every version read, 2.0.0 to 3.3.7 |
| `Npgsql.NodaTime` | `Npgsql.NpgsqlNodaTimeExtensions`, `UseNodaTime(this INpgsqlTypeMapper)` | Every 8.0.x, 9.0.x and 10.0.x, same name and signature |
| `NetTopologySuite` | `NetTopologySuite.Geometries.Geometry` | 1.15.3 and 2.0.0 to 2.6.0; it does not tell 1.x from 2.x |
| `Npgsql.NetTopologySuite` | `Npgsql.NpgsqlNetTopologySuiteExtensions`, `UseNetTopologySuite(this INpgsqlTypeMapper, CoordinateSequenceFactory, PrecisionModel, Ordinates, bool)` | Every 8.0.x, 9.0.x and 10.0.x, same name and signature |
| `NetTopologySuite.IO.SqlServerBytes` | `NetTopologySuite.IO.SqlServerBytesReader` | 1.15.1, 2.0.0 and 2.1.0; it does not tell 1.x from 2.x |
| Npgsql 10, for `JsonNode` | `NpgsqlTypes.NpgsqlCube`, the existing `NpgsqlProbe.AtLeast10` | Absent from every `lib/` of 8.0.9 and 9.0.5, present in 10.0.3 |

**The assembly versions.**  NodaTime's identity version is its package version, to the patch: 3.1.9 is `3.1.9.0`.  Every NetTopologySuite 2.x is `2.0.0.0`, and 1.15.3 is `1.15.3.0`.  `NetTopologySuite.IO.SqlServerBytes` 2.0.0 and 2.1.0 are both `2.0.0.0`, and 1.15.1 is `1.0.0.0`.  Both plugins' identity versions are their package versions.  The informational version, read as `SqlClientProbe` reads SqlClient's, is the package version and `+commit` for NodaTime 2.4 and later, for NetTopologySuite (`2.5.0 (compatible with JTS 1.19)+...`) and for `SqlServerBytes` (`2.1.0+...`).

**The members the generated code calls**, with the signatures it calls:

- NodaTime's 14, `LocalDate.FromDateTime` to `Duration.ToTimeSpan` as the design's SQL Server table names them, are in every version from 2.0.0.
- NetTopologySuite's `Geometry`, `Point`, `Polygon` and `NtsGeometryServices.Instance` are in every version from 2.0.0.  In 1.15.3 the types exist in the GeoAPI world, and `NtsGeometryServices.Instance` returns `GeoAPI.IGeometryServices`.
- `SqlServerBytesReader.Read(byte[])` returning `Geometry`, `SqlServerBytesWriter.Write(Geometry)`, and `IsGeography` and `HandleOrdinates` on both, are in 2.0.0 and 2.1.0.  1.15.1 has them over GeoAPI's types.  2.0.0 and 2.1.0 have the same public API.

**The plugins' dependencies**, each a minimum with no upper bound:

| Package | Needs |
|----|----|
| `Npgsql.NodaTime` 8.0.0 to 8.0.9 | NodaTime 3.1.9 |
| `Npgsql.NodaTime` 9.0.0 to 9.0.5 | NodaTime 3.2.0 |
| `Npgsql.NodaTime` 10.0.0 to 10.0.3 | NodaTime 3.2.2 |
| `Npgsql.NetTopologySuite` 8.0.0 to 9.0.5 | NetTopologySuite 2.5.0 |
| `Npgsql.NetTopologySuite` 10.0.0 to 10.0.3 | NetTopologySuite 2.6.0 |
| `NetTopologySuite.IO.SqlServerBytes` 2.0.0 and 2.1.0 | NetTopologySuite [2.0.0, 3.0.0) |

**The floors.**  The generated code's members put each floor at 2.0.0; the plugins raise NodaTime's and NetTopologySuite's.

| Library | Floor | Why | What tells it |
|----|----|----|----|
| NodaTime | 3.1.9 | What `Npgsql.NodaTime` 8 needs; the members are older | No symbol: 3.1.0 to 3.1.14 have the same public API.  The identity version, `3.1.9.0` or later |
| NetTopologySuite | 2.5.0 | What `Npgsql.NetTopologySuite` 8 needs; the members are in 2.0.0 | `NetTopologySuite.Algorithm.Hull.ConcaveHull`, first in 2.5.0 |
| `NetTopologySuite.IO.SqlServerBytes` | 2.1.0 | 2.1.0 writes M values from the coordinates and checks a `geography` polygon for emptiness before its ring orientation, by its commits since 2.0.0.  Question 5's Z and M answer is 2.1.0's | No symbol and no identity version: the informational version, `2.1.0` |

The floors of NodaTime and NetTopologySuite are the plugins' minimums, so NuGet holds them on PostgreSQL.  On SQL Server, where nothing holds them, the probe does.  One floor per library on both engines is the design's amended call (below), so each engine's floor project references the same versions.

## What the later sub-phases take from this

**Owner decisions.**

1. **The NodaTime range defaults** (question 1).  The owner chose on 2026-10-30, "B: NpgsqlRange": `tstzrange` is `NpgsqlRange<Instant>`, `tsrange` stays `NpgsqlRange<LocalDateTime>`, `daterange` is `NpgsqlRange<LocalDate>`, and the multiranges are their arrays.  `Interval` and `DateInterval` stay reachable by a type override, with the empty-range throw and the 1 ns shift in the manual.  The design's NodaTime table and the epic's Decided item say so.
2. **The `-- param:` modifier limit** (question 4), found by this branch's review.  The owner chose "Record as TD": [TD-0093](../../tech-debt/TD-0093-a-param-marker-takes-a-modifier-of-numbers-alone.md), with a known issue in the manual.

**Amended in the design**, each a designer's call or a "Verify first" the spike answered:

- **7.1**: the range defaults of owner decision 1; each NodaTime row, its arrays, its `NULL` and each alternative confirmed on Npgsql 8 and 10, and the boxed types on 9 too; the `ArgumentException` of a write not in UTC; the paths of the end-to-end and floor projects.
- **7.2**: what the manual says of the SQL Server edges at their limits: an offset with seconds truncated on write, values outside `DateTime`'s range, SqlClient's rounding and refusals of `datetime` and `smalldatetime`, and a `Duration` outside `time`.
- **7.3**: SQL Server's `json` refuses a top-level scalar, a JSON `null` included, and the method lets the server's error through; a JSON `null` read into a not-null `JsonNode`, `JsonObject` or `JsonArray` throws, as into a not-null class, since a `JsonNode` cannot hold one; `json` and `jsonb` normalize what they store.
- **7.4**: the PostGIS image and its Dockerfile; a library row matched by `internalName` and `kind`; the tool's plugin, which makes the check pass; the spatial read through `GetStream`, without the cast, if it holds through a wrapping connection; the class of the shared instances given a static constructor, so that they are created at the first spatial query; a `geography` of SRID 0 refused by SQL Server.
- **The floors and markers**: NodaTime 3.1.9 by the identity version, NetTopologySuite 2.5.0 by `ConcaveHull`, `NetTopologySuite.IO.SqlServerBytes` 2.1.0 by the informational version, one floor for both engines.

**Exact values** the later sub-phases use:

- **Packages.**  Main projects: NodaTime 3.3.7, `Npgsql.NodaTime` and `Npgsql.NetTopologySuite` 10.0.3, NetTopologySuite 2.6.0, `NetTopologySuite.IO.SqlServerBytes` 2.1.0.  Floor projects: `Npgsql.NodaTime` and `Npgsql.NetTopologySuite` 8.0.9, NodaTime 3.1.9, NetTopologySuite 2.5.0, `NetTopologySuite.IO.SqlServerBytes` 2.1.0.
- **The repository pins transitive packages centrally** (`CentralPackageTransitivePinningEnabled` in `Directory.Packages.props`).  Once NodaTime and NetTopologySuite have central versions, every project that gets them through a plugin gets the central version, not the plugin's own: the PostgreSQL main project runs `Npgsql.NodaTime` 10.0.3 on NodaTime 3.3.7, which this spike did not run (it ran 3.2.2).  So each floor project, `Postgres.Npgsql8` and `SqlServer.SqlClient61`, references NodaTime 3.1.9 and NetTopologySuite 2.5.0 itself, with `VersionOverride`, or it tests the central versions instead of the floors.
- **PostGIS**: `3.6.4+dfsg-2.pgdg13+1`, on `postgres:14.24` and `18.6` in CI and `15.19`, `16.15` and `17.11` at night.
- **The markers** of question 6's table.

**Where merged code differs from the design**, as of `58a5ee22`:

- The end-to-end projects are `tests/DatabaseTests/Postgres` and `tests/DatabaseTests/SqlServer`, and their floor projects `tests/DatabaseTests/Postgres.Npgsql8` and `tests/DatabaseTests/SqlServer.SqlClient61`.
- The base maps are `src/SqlSource/Generation/Types/Maps/PostgresTypeMap.cs` and `SqlServerTypeMap.cs`, and `TypeKeyNames` is in `src/SqlSource/Settings/TypeOverrides/`.
- The probes are in `src/SqlSource/Generation/Types/Drivers/`, and the converters in `src/SqlSource/Generation/Converters/`.
- Microsoft.Data.SqlClient 6.1.8 is published; the floor project pins 6.1.7, which this spike ran.

**Found outside phase 7**: a `-- param:` marker cannot name a type whose modifier is not digits, such as `geometry(Point,4326)` (question 4).  It is the marker grammar's.  The owner chose to record it as tech debt, [TD-0093](../../tech-debt/TD-0093-a-param-marker-takes-a-modifier-of-numbers-alone.md), with a known issue in the manual, and to leave the grammar as it is.
