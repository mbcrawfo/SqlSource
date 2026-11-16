# Use NodaTime and NetTopologySuite

Read and send dates and times as NodaTime's types, such as `Instant` and `LocalDate`, and PostGIS's or SQL Server's `geometry` and `geography` as NetTopologySuite's.

**Use this when** your project uses NodaTime for dates and times, or NetTopologySuite for spatial data.  Each database needs its own steps, so each library has a section for each database.

<a id="nodatime-postgresql"></a>

## NodaTime on PostgreSQL

Reference `Npgsql.NodaTime`, and call `UseNodaTime()` on the data source that the query methods run on.  The deliveries of the PostgreSQL example project do both:

```sql postgres
-- The queries of the deliveries, at the folder's output, methods, which DeliveryQueries calls.  The project's type
-- override makes every timestamptz a DateTimeOffset, for Dapper, and would win over NodaTime, so the file's type
-- override gives timestamptz back to its default, and the file's timestamptz is NodaTime's Instant.  The project
-- references Npgsql's plugins for NodaTime and NetTopologySuite, so location, a geography, is NetTopologySuite's
-- Geometry.  details, a jsonb, is the project's class DeliveryDetails, which the methods read and send through
-- System.Text.Json.
-- type-override: timestamptz=default

-- name: RecordDelivery -> rowcount
-- summary: Records where and when an order arrived.
-- rowcount: the method returns the count of rows inserted.  A parameter written to a column has the column's type,
-- so @deliveredAt is an Instant and @location a Geometry with no marker; no column type reaches a parameter, so the
-- marker gives @details the class.
-- param: @details -> DeliveryDetails
INSERT INTO deliveries (order_id, delivered_at, location, details)
VALUES (@orderId, @deliveredAt, @location, @details);

-- name: FindDelivery -> one-optional
-- summary: The delivery of an order, or null when it has not arrived.
-- one-optional: the method returns the row, or null when there is none.  The marker gives the column details the
-- class.  order_id is left out: the table's columns in its own order would give the rows the table's model,
-- DeliveriesDto.
-- column: details -> DeliveryDetails
SELECT delivered_at, location, details
FROM deliveries
WHERE order_id = @orderId;
```

```csharp
using Npgsql;

namespace PostgresExamples.Methods;

// The data source of the connections given to the classes of Methods/.  The project references Npgsql's plugins for
// NodaTime and NetTopologySuite, so the methods read and send their types, which Npgsql reads only on a data source
// taught them by UseNodaTime() and UseNetTopologySuite().  DapperSetup's data source calls neither, since after
// UseNodaTime() Npgsql gives Dapper an Instant for every timestamptz, which the models' DateTimeOffset cannot take.
// See site/reference/databases/postgresql/query-methods.md, "What a method reads and throws".
public static class StoreDataSource
{
    public static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _ = builder.UseNodaTime();
        _ = builder.UseNetTopologySuite();
        return builder.Build();
    }
}
```

**What you get.**  A PostgreSQL project that references `Npgsql.NodaTime` gets NodaTime's types in its query methods, such as `Instant` for a `timestamptz` and `LocalDate` for a `date`.  You write no type override.  Here `RecordDeliveryParams.DeliveredAt` and `FindDeliveryDto.DeliveredAt` are an `Instant`.

**The rules you need.**

- **Call `UseNodaTime()` on the `NpgsqlDataSourceBuilder` of the data source that the methods run on.**  That is the data source that your repositories take.  See [NodaTime on PostgreSQL](../reference/databases/postgresql/libraries.md#nodatime).
- **A query whose output is `models` keeps the types it had.**  Only query methods get NodaTime's types.
- **Your type override wins over NodaTime.**  The example project makes every `timestamptz` a `DateTimeOffset`, so its file gives `timestamptz` back with `-- type-override: timestamptz=default`.  See [NodaTime on PostgreSQL](../reference/databases/postgresql/libraries.md#nodatime).
- **To turn NodaTime off, use the library types.**  See [library types](../reference/library-types-and-json.md#setting-library-types).

<a id="nodatime-sql-server"></a>

## NodaTime on SQL Server

Reference `NodaTime`, and turn it on with `"libraryTypes": { "nodaTime": "on" }` in a settings file.  The SQL Server example project turns it on:

```jsonc
{
  "$schema": "https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json",
  // No settings file above this folder applies to the project.
  "root": true,
  // The dialect of every query of the project.
  "dialect": "mssql",
  "typeOverrides": {
    // Every date is a DateOnly, which Dapper reads through DateOnlyHandler.
    "date": "System.DateOnly",
  },
  "columnTypes": {
    // order_lines.order_id is an OrderId, by its short name, in every query that reads it.
    "order_lines.order_id": "OrderId",
  },
  "libraryTypes": {
    // SQL Server turns a library on only when asked, so the project turns on NodaTime, NetTopologySuite and
    // System.Text.Json, whose types only the methods of Methods/ get.  The type override above still wins over NodaTime
    // for every date, which stays a DateOnly.
    "nodaTime": "on",
    "netTopologySuite": "on",
    "json": "on",
  },
}
```

```sql mssql
-- The queries of the deliveries, at the folder's output, methods, which DeliveryQueries calls.  The project's library
-- types turn on NodaTime, NetTopologySuite and System.Text.Json, so delivered_at, a datetimeoffset, is NodaTime's
-- OffsetDateTime, location, a geography, NetTopologySuite's Geometry, and events, a json, a JsonElement.  details, a
-- json too, is the project's class DeliveryDetails.  The methods convert each of them themselves, so nothing is
-- registered on the connection.

-- name: RecordDelivery -> rowcount
-- summary: Records where and when an order arrived.
-- rowcount: the method returns the count of rows inserted.  SQL Server gives a parameter written to a column the
-- column's type, so @deliveredAt is an OffsetDateTime, @location a Geometry and @events a JsonElement with no marker;
-- no column type reaches a parameter, so the marker gives @details the class.
-- param: @details -> DeliveryDetails
INSERT INTO deliveries (order_id, delivered_at, location, details, events)
VALUES (@orderId, @deliveredAt, @location, @details, @events);

-- name: FindDelivery -> one-optional
-- summary: The delivery of an order, or null when it has not arrived.
-- one-optional: the method returns the row, or null when there is none.  The marker gives the column details the
-- class.  order_id is left out: the table's columns in its own order would give the rows the table's model,
-- DeliveriesDto.
-- column: details -> DeliveryDetails
SELECT delivered_at, location, details, events
FROM deliveries
WHERE order_id = @orderId;
```

**What you get.**  A SQL Server project that references `NodaTime` and turns it on gets NodaTime's types in its query methods.  For example, a `datetime2` is a `LocalDateTime`, and a `date` a `LocalDate`.  Here `delivered_at` is a `datetimeoffset`, so `RecordDeliveryParams.DeliveredAt` and `FindDeliveryDto.DeliveredAt` are an `OffsetDateTime`.  Every `date` of the example project stays a `DateOnly`, since its type override wins over NodaTime.

**The rules you need.**

- **On SQL Server, NodaTime is off until you turn it on**, whatever the project references.  See [NodaTime on SQL Server](../reference/databases/sqlserver/libraries.md#nodatime).
- **The query methods convert each value themselves.**  So there is nothing to call on the connection.
- **A type override gives a column an `Instant` or a `Duration`, whether NodaTime is on or not.**  See [NodaTime on SQL Server](../reference/databases/sqlserver/libraries.md#nodatime).

<a id="nts-postgresql"></a>

## NetTopologySuite on PostgreSQL

Reference `Npgsql.NetTopologySuite`, and call `UseNetTopologySuite()` on the data source that the query methods run on.  This query of the PostgreSQL example project reads a `geography`, and its data source calls `UseNetTopologySuite()`:

```sql postgres
-- name: FindDelivery -> one-optional
-- summary: The delivery of an order, or null when it has not arrived.
-- one-optional: the method returns the row, or null when there is none.  The marker gives the column details the
-- class.  order_id is left out: the table's columns in its own order would give the rows the table's model,
-- DeliveriesDto.
-- column: details -> DeliveryDetails
SELECT delivered_at, location, details
FROM deliveries
WHERE order_id = @orderId;
```

```csharp
using Npgsql;

namespace PostgresExamples.Methods;

// The data source of the connections given to the classes of Methods/.  The project references Npgsql's plugins for
// NodaTime and NetTopologySuite, so the methods read and send their types, which Npgsql reads only on a data source
// taught them by UseNodaTime() and UseNetTopologySuite().  DapperSetup's data source calls neither, since after
// UseNodaTime() Npgsql gives Dapper an Instant for every timestamptz, which the models' DateTimeOffset cannot take.
// See site/reference/databases/postgresql/query-methods.md, "What a method reads and throws".
public static class StoreDataSource
{
    public static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _ = builder.UseNodaTime();
        _ = builder.UseNetTopologySuite();
        return builder.Build();
    }
}
```

**What you get.**  A PostgreSQL project that references `Npgsql.NetTopologySuite` gets NetTopologySuite's `Geometry` for PostGIS's `geometry` and `geography` in its query methods.  Here `FindDeliveryDto.Location` is a `Geometry`.

**The rules you need.**

- **The plugin reads and sends each value.**  So call `UseNetTopologySuite()` on the `NpgsqlDataSourceBuilder` of the data source that the methods run on.  The example project references the plugin of NodaTime too, so its data source calls `UseNodaTime()` as well.
- **A type override gives a column a shape, such as `NetTopologySuite.Geometries.Point`.**  See [NetTopologySuite on PostgreSQL](../reference/databases/postgresql/libraries.md#nettopologysuite).

<a id="nts-sql-server"></a>

## NetTopologySuite on SQL Server

Reference `NetTopologySuite.IO.SqlServerBytes` and NetTopologySuite 2.5.0 or later, and turn NetTopologySuite on in a settings file.  The settings file of the SQL Server example project turns it on, and its query reads a `geography`:

```jsonc
  "libraryTypes": {
    // SQL Server turns a library on only when asked, so the project turns on NodaTime, NetTopologySuite and
    // System.Text.Json, whose types only the methods of Methods/ get.  The type override above still wins over NodaTime
    // for every date, which stays a DateOnly.
    "nodaTime": "on",
    "netTopologySuite": "on",
    "json": "on",
  },
```

```sql mssql
-- name: FindDelivery -> one-optional
-- summary: The delivery of an order, or null when it has not arrived.
-- one-optional: the method returns the row, or null when there is none.  The marker gives the column details the
-- class.  order_id is left out: the table's columns in its own order would give the rows the table's model,
-- DeliveriesDto.
-- column: details -> DeliveryDetails
SELECT delivered_at, location, details, events
FROM deliveries
WHERE order_id = @orderId;
```

**What you get.**  A SQL Server project that references `NetTopologySuite.IO.SqlServerBytes`, and turns NetTopologySuite on, gets NetTopologySuite's `Geometry` for `geography` and `geometry` in its query methods.  Here `FindDeliveryDto.Location` is a `Geometry`.

**The rules you need.**

- **Reference NetTopologySuite 2.5.0 or later by name.**  `NetTopologySuite.IO.SqlServerBytes` alone brings 2.0.0.
- **Turn NetTopologySuite on with `"libraryTypes": { "netTopologySuite": "on" }`.**
- **The query methods read and send each value themselves.**  So there is nothing to call on the connection.
- **On both, a type override gives a column a shape**, such as `NetTopologySuite.Geometries.Point`.  See [NetTopologySuite on PostgreSQL](../reference/databases/postgresql/libraries.md#nettopologysuite) and [NetTopologySuite on SQL Server](../reference/databases/sqlserver/libraries.md#nettopologysuite).

<a id="ef-core-and-dapper"></a>

## With Entity Framework Core or Dapper in the same project

Two things catch a PostgreSQL project out.  The PostgreSQL example project gives Dapper a data source of its own, which calls neither `UseNodaTime()` nor `UseNetTopologySuite()`:

```csharp
using Dapper;
using Npgsql;

namespace PostgresExamples;

// What the manual's guide to Dapper sets up once, before the first query: Dapper matches the columns in snake_case to
// the models' properties, and reads a timestamptz, which the project's type override makes a DateTimeOffset, through
// UtcDateTimeOffsetHandler.  Its data source calls neither UseNodaTime() nor UseNetTopologySuite(), which
// Methods.StoreDataSource.CreateDataSource calls for the generated methods: after UseNodaTime(), Npgsql gives Dapper
// an Instant for every timestamptz, which UtcDateTimeOffsetHandler cannot read.  See
// site/guides/dapper.md#models-through-dapper and site/guides/dapper.md#dapper-with-postgresql.
public static class DapperSetup
{
    static DapperSetup()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new UtcDateTimeOffsetHandler());
    }

    public static NpgsqlDataSource CreateDataSource(string connectionString) =>
        NpgsqlDataSource.Create(connectionString);
}
```

**What you get.**  Dapper's queries run on the data source of `DapperSetup`.  The query methods run on the data source of `StoreDataSource`, which calls both, as [NodaTime on PostgreSQL](#nodatime-postgresql) shows.

**The rules you need.**

- **Entity Framework Core's `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime` and `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` bring the plugins.**  So the library is on for the query methods.
- **`UseNodaTime()` or `UseNetTopologySuite()` inside EF Core's `UseNpgsql(...)` teaches EF Core's own data source alone.**  Call it on the builder of the data source that the methods run on too, or turn the library off.
- **On a data source that calls `UseNodaTime()`, Npgsql gives Dapper an `Instant` for a `timestamptz`.**  So a query that Dapper reads into a `DateTime` fails.  Read the queries of Dapper on another data source, or give them NodaTime's types with the type override.  See [what a method reads and throws](../reference/databases/postgresql/query-methods.md#method-results).

The deliveries of [the PostgreSQL example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/PostgresExamples/README.md) and [the SQL Server example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/SqlServerExamples/README.md), `Methods/DeliveryQueries.sql`, show NodaTime, NetTopologySuite and System.Text.Json in a whole project.

## If it does not work

- **`InvalidCastException` for a NodaTime or NetTopologySuite type, on PostgreSQL.**  The data source that the method ran on does not call `UseNodaTime()` or `UseNetTopologySuite()`.  Call it on the builder of that data source.  See [`InvalidCastException`](troubleshooting.md#invalidcastexception).
- **The same `InvalidCastException` in a project with Entity Framework Core.**  `UseNodaTime()` or `UseNetTopologySuite()` inside `UseNpgsql(...)` teaches EF Core's own data source alone.  Call it on the data source of the methods too, or turn the library off.  See [With Entity Framework Core or Dapper in the same project](#ef-core-and-dapper).
- **A query that Dapper reads into a `DateTime` fails, on PostgreSQL.**  Its data source calls `UseNodaTime()`, so Npgsql gives Dapper an `Instant` for a `timestamptz`.  Read the queries of Dapper on another data source.  See [With Entity Framework Core or Dapper in the same project](#ef-core-and-dapper).
- **[SQLSRC282](../diagnostics.md#sqlsrc282) for NetTopologySuite, on SQL Server.**  `NetTopologySuite.IO.SqlServerBytes` alone brings NetTopologySuite 2.0.0.  Reference NetTopologySuite 2.5.0 or later by name.  See [NetTopologySuite on SQL Server](#nts-sql-server).
