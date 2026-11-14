# Use NodaTime and NetTopologySuite

<a id="nodatime-postgresql"></a>

## NodaTime on PostgreSQL

A PostgreSQL project that references `Npgsql.NodaTime` gets NodaTime's types, `Instant` for a `timestamptz` and `LocalDate` for a `date`, in its query methods, with no type override to write.  Its application calls `UseNodaTime()` on the `NpgsqlDataSourceBuilder` of the data source that the methods run on, the one its repositories take, and a query whose output is `models` keeps the types it had.  To turn it off for a project, a folder, a file or a query, use the library types.  See [NodaTime on PostgreSQL](../reference/databases/postgresql/libraries.md#nodatime) and [library types](../reference/library-types-and-json.md#setting-library-types).

<a id="nodatime-sql-server"></a>

## NodaTime on SQL Server

A SQL Server project that references `NodaTime` gets NodaTime's types, `LocalDateTime` for a `datetime2` and `LocalDate` for a `date`, in its query methods when it turns NodaTime on, with `"libraryTypes": { "nodaTime": "on" }` in a settings file.  The query methods convert each value themselves, so there is nothing to call on the connection, and a type override gives a column an `Instant` or a `Duration` whether NodaTime is on or not.  See [NodaTime on SQL Server](../reference/databases/sqlserver/libraries.md#nodatime).

<a id="nts-postgresql"></a>

## NetTopologySuite on PostgreSQL

A PostgreSQL project that references `Npgsql.NetTopologySuite` gets NetTopologySuite's `Geometry` for PostGIS's `geometry` and `geography` in its query methods, read and sent by the plugin, so its application calls `UseNetTopologySuite()` on the `NpgsqlDataSourceBuilder` of the data source that the methods run on.

<a id="nts-sql-server"></a>

## NetTopologySuite on SQL Server

A SQL Server project that references `NetTopologySuite.IO.SqlServerBytes`, and NetTopologySuite 2.5.0 or later by name, since `NetTopologySuite.IO.SqlServerBytes` alone brings 2.0.0, gets it for `geography` and `geometry` when it turns NetTopologySuite on, with `"libraryTypes": { "netTopologySuite": "on" }`, and the query methods read and send each value themselves, with nothing to call on the connection.  On both, a type override gives a column a shape, such as `NetTopologySuite.Geometries.Point`.  See [NetTopologySuite on PostgreSQL](../reference/databases/postgresql/libraries.md#nettopologysuite) and [NetTopologySuite on SQL Server](../reference/databases/sqlserver/libraries.md#nettopologysuite).

<a id="ef-core-and-dapper"></a>

## With Entity Framework Core or Dapper in the same project

Two things catch a PostgreSQL project out.  Entity Framework Core's `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime` and `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` bring the plugins, so the library is on for the query methods, while `UseNodaTime()` or `UseNetTopologySuite()` inside EF Core's `UseNpgsql(...)` teaches EF Core's own data source alone: call it on the builder of the data source the methods run on too, or turn the library off.  And on a data source that calls `UseNodaTime()`, Npgsql gives Dapper an `Instant` for a `timestamptz`, so a query read through Dapper into a `DateTime` fails: read Dapper's queries on another data source, or give them NodaTime's types with the type override.  See [what a method reads and throws](../reference/databases/postgresql/query-methods.md#method-results).

The deliveries of [the PostgreSQL example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/PostgresExamples/README.md) and [the SQL Server example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/SqlServerExamples/README.md), `Methods/DeliveryQueries.sql`, show NodaTime, NetTopologySuite and System.Text.Json in a whole project.
