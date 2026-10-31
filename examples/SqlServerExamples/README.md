# SqlServerExamples

A small project that uses SqlSource with SQL Server: what you write, what you get, and how to run the tool.  Its queries run through the methods SqlSource generates, and, in two folders, through Dapper over the generated SQL and models.  It builds with the rest of the repository, so what it shows is what SqlSource does today.  For what SqlSource is and how to install it, start with the [repository's README](../../README.md); the [manual](../../docs/manual/README.md) has everything this page does not explain, and [its page on SQL Server](../../docs/manual/sqlserver.md) what is particular to it.

## What is in it

| Path | What it shows |
|----|----|
| [`schema.sql`](schema.sql) | The database: users, their orders, the lines of each order and the delivery of an order, with a few rows |
| [`docker-compose.yml`](docker-compose.yml) | Starts SQL Server and applies that schema to the database `examples` |
| [`Sql/`](Sql) | The output `sql`: `CountUsers` as a constant, and `CountRows` as a method that fills in a [token](../../docs/manual/parameters-and-tokens.md#tokens), in a nested class that the attribute [names](../../docs/manual/settings.md#setting-nested-class) `Counts`.  They are not described, so they have no sidecar and need no connection |
| [`Models/`](Models) | The output `models`: each query gets a model of its parameters and one of its rows.  `GetOrder` has a nullable column and a `date` read as a `DateOnly` by the project's [type override](../../docs/manual/settings.md#setting-type-overrides); `ListOrdersPlacedSince` declares the type of `TOP`'s parameter with [`-- param:`](../../docs/manual/parameters-and-tokens.md#param-marker) and sends a `datetime2` parameter as the recipe says; `CountShippedOrders` has a count that the tool shows is never null, and `ListOrderMonths` makes a column not null with a [`-- column:` marker](../../docs/manual/generator.md#column-marker); `AddOrder` reads the rows of an `OUTPUT` clause; `GetUser` is `SELECT *`, so its rows are the [table's model](../../docs/manual/generator.md#table-models), `UsersDto` |
| [`CodeGen/`](CodeGen) | The default output, `codegen`: each query gets its models and a [method](../../docs/manual/generator.md#query-methods) that runs it, an extension method of the connection, which [`StoreQueries`](CodeGen/StoreQueries.cs) calls with the caller's `CancellationToken`.  The [result shape](../../docs/manual/sql-files.md#result-shapes) after a query's name says what its method returns: `ListOrderLines` and `ListLatestOrderPerProduct` are `many` by default and return an array, `FindUserByEmail` is `one-optional` and returns the row or null, `ChangeUserEmail` is `rowcount` and returns the count of rows updated, and `ListUsersNewestFirst` returns an `IReadOnlyList` by its [`-- collection-type:` marker](../../docs/manual/settings.md#setting-collection-type).  `StreamUsersOldestFirst`'s marker, `-- collection-type: IAsyncEnumerable`, makes its method [stream its rows](../../docs/manual/settings.md#streaming-rows), which `WriteUserNamesAsync` takes in an `await foreach`, one at a time, as each is read.  `ListUsersSortedBy` has a [token](../../docs/manual/parameters-and-tokens.md#tokens), a `string` argument of its method.  `OrderLines.sql` gives each `decimal` the type [`Price`](CodeGen/Price.cs) with a type override, and the method reads `unit_price` through `Price`'s [converters](../../docs/manual/settings.md#converters).  `users.email` is of the [alias type](../../docs/manual/settings.md#setting-type-overrides) `dbo.Email` over `nvarchar`, which the header of `Users.sql` alone gives the type [`EmailAddress`](CodeGen/EmailAddress.cs), `-- type-override: dbo.Email=EmailAddress`, so the methods read and send each email through its converters, and `Models/` still reads it as a `string`; SQL Server gives a parameter compared with or written to a column of an alias type that alias type, so the queries need no `-- param:` marker for `@email`.  The project's [column types](../../docs/manual/settings.md#setting-column-types) give the column `order_lines.order_id` the type [`OrderId`](CodeGen/OrderId.cs), by its short name, in every query that reads it, so `ListOrderLinesDto.OrderId` is an `OrderId`; `ListOrderLines` gives its parameter that type with a [`-- param:` marker](../../docs/manual/parameters-and-tokens.md#param-marker), `-- param: @orderId -> OrderId`, so `ListOrderLinesParams.OrderId` is one too, and `ListLatestOrderPerProduct` gives it to a computed column, which the column types do not reach, with a [`-- column:` marker](../../docs/manual/generator.md#column-marker), `-- column: latest_order_id -> OrderId`.  The methods read and send each `OrderId` through its converters.  `Deliveries.sql` shows the libraries that the methods read and send.  On SQL Server a library is off until it is asked for, so the project's `sqlsource.json` turns on NodaTime, NetTopologySuite and System.Text.Json with its [library types](../../docs/manual/settings.md#setting-library-types), `"libraryTypes": { "nodaTime": "on", "netTopologySuite": "on", "json": "on" }`.  So `delivered_at`, a `datetimeoffset`, is [NodaTime's](../../docs/manual/sqlserver.md#nodatime) `OffsetDateTime`, and the project's type override still makes every `date` a `DateOnly`; `location`, a `geography`, is [NetTopologySuite's](../../docs/manual/sqlserver.md#nettopologysuite) `Geometry`; and `events`, a `json`, SQL Server 2025's type, is a `JsonElement`.  `details`, a `json` too, is [`DeliveryDetails`](CodeGen/DeliveryDetails.cs), a class of the project, by `FindDelivery`'s `-- column: details -> DeliveryDetails` and `RecordDelivery`'s `-- param: @details -> DeliveryDetails`, and the methods read and send it as [JSON](../../docs/manual/sqlserver.md#json), through System.Text.Json.  The methods convert each of these values themselves, so nothing is registered on the connection |
| [`DapperSetup.cs`](DapperSetup.cs), [`DateOnlyHandler.cs`](DateOnlyHandler.cs), [`TypedParameter.cs`](TypedParameter.cs) | What Dapper needs once, before the first query, and the parameter it cannot send as it is: see [the recipe](../../docs/manual/recipes.md#dapper-with-sql-server) |

Each folder's `.sql` files belong to the one class beside them, whose `[SqlSourceGenerate]` attribute sets the folder's output, and whose methods run the queries: in `Sql/` and `Models/` the generated SQL through Dapper, into the generated models, and in `CodeGen/` the generated methods.  The queries write their parameters in camelCase, `@orderId`: in `Models/` Dapper binds it to the property `OrderId` of the input model, and in `CodeGen/` the generated method binds it from that property itself.  Nothing here runs: the project compiles, so a model that changes shape breaks its build.

## Your own project

[`SqlServerExamples.csproj`](SqlServerExamples.csproj) takes the generator from this repository, so that it builds with the generator of the same commit and shows what SqlSource does now.  The repository's own build files give it its language version, its analyzers and the versions of its packages.  A project of your own adds the packages instead, here with the versions this example builds with:

```bash
dotnet add package SqlSource --prerelease
dotnet add package Dapper --version 2.1.89
dotnet add package Microsoft.Data.SqlClient --version 7.1.1
dotnet add package NodaTime --version 3.3.7
dotnet add package NetTopologySuite --version 2.6.0
dotnet add package NetTopologySuite.IO.SqlServerBytes --version 2.1.0
```

SqlSource supports Microsoft.Data.SqlClient 6.1 and later: see [which version of Microsoft.Data.SqlClient](../../docs/manual/sqlserver.md#which-sqlclient).  Dapper is for `Sql/` and `Models/` alone: a project that runs every query through its method needs only SqlSource and Microsoft.Data.SqlClient, and the libraries it uses.  `NodaTime`, `NetTopologySuite` and `NetTopologySuite.IO.SqlServerBytes` give the methods their types once `sqlsource.json` turns them on.  `NetTopologySuite` is added by name because `NetTopologySuite.IO.SqlServerBytes` alone brings NetTopologySuite 2.0.0, and SqlSource needs 2.5.0 or later: see [SQLSRC282](../../docs/manual/diagnostics.md#sqlsrc282); System.Text.Json, which reads and sends `DeliveryDetails` and `JsonElement`, needs no package.  Nothing is registered on the connection: the methods convert each value themselves.  The rest of the project file is nullable reference types, which your project sets too, and the dialect, the type override, the column types and the library types are in [`sqlsource.json`](sqlsource.json), which your project keeps beside its project file: see [the settings](../../docs/manual/settings.md#all-settings).  It also keeps [SQLSRC240](../../docs/manual/diagnostics.md#sqlsrc240) a warning, which only this repository needs, so that it builds while its version moves ahead of the committed sidecars.

## Start the database

From this folder:

```bash
docker compose up -d
docker compose wait schema
```

The first command starts SQL Server and, once it answers, a second service that creates the database `examples` and applies `schema.sql` to it.  The second command, which needs Docker Compose 2.20 or later, returns when that service has finished, and prints its exit code; run the tool after it.  An exit code other than 0 means the schema was not applied: `docker compose logs schema` shows why, and `docker compose down -v` starts again from nothing.  The image is built for x64 alone, so on Apple silicon Docker runs it under emulation, and the first start, while the image is pulled, is slow.

SQL Server listens on `localhost,51433`, with the user `sa` and the password `Examples-Passw0rd`.  They are for this example alone.  If the port is taken, change `51433` under `ports` in `docker-compose.yml`, and the port in the connection strings below.  The schema is applied only when the database is created, so a second `up` keeps your data; after a change to `schema.sql`, start again from nothing:

```bash
docker compose down -v
```

## Describe the queries

The tool asks the database about each query of `Models/` and `CodeGen/`, and records the answer in a `.sql.json` file beside each of their `.sql` files, its sidecar.  The project has one database, so the tool takes its connection from `SQLSOURCE_CONNECTION`.  In a project of your own, with the tool installed:

```bash
export SQLSOURCE_CONNECTION="Server=localhost,51433;Database=examples;User ID=sa;Password=Examples-Passw0rd;TrustServerCertificate=True"
dotnet sqlsource describe
```

`TrustServerCertificate=True` is there because the container's server encrypts the connection with a certificate it made for itself, which nothing on your machine trusts.  Use it for a local database of your own like this one; a connection to a real server leaves it out, so that the driver checks the server's certificate.

The tool describes the queries that have no current entry in their sidecar.  `dotnet sqlsource describe --check` compares the sidecars with what the database says now, and `--force` describes every query again.

This example's sidecars are committed and current.  They are written by `tools/describe-end-to-end.sh`, which starts a database of its own: see [the recording scripts](../../tools/README.md#recording-scripts).  To compare them with the Compose database, run the tool from its source, from the repository's root, with `SQLSOURCE_CONNECTION` set as above, since the repository pins no published tool and another version of it writes another version into every sidecar:

```bash
dotnet run --project src/SqlSource.Tool -- describe --check examples/SqlServerExamples/SqlServerExamples.csproj
```

See [the tool](../../docs/manual/tool.md) for the commands and the connections.

## Read a sidecar

Open [`Models/Orders.sql.json`](Models/Orders.sql.json).  It is the tool's record of what the database said about each query: its parameters, and its columns, each with its type and whether it can be null.  You never edit a sidecar: the tool writes it, and the build reports a query whose entry is missing or older than its SQL.  See [the sidecar](../../docs/manual/concepts.md#sidecar).

## Build, and see the generated code

```bash
dotnet build
```

Your IDE shows the generated code among the project's dependencies, under its analyzers or source generators, and goes to it from a use such as `Sql.GetOrder`, `GetOrderDto` or `ListOrderLines`.  To write it to disk, build with `EmitCompilerGeneratedFiles`:

```bash
dotnet build -p:EmitCompilerGeneratedFiles=true
```

The files are under `obj/Debug/net10.0/generated/`: one for each class, with the SQL of its queries whose output is `sql` or `models`, one with the methods of `CodeGen/`'s `StoreQueries`, in the class `StoreQueriesExtensions`, one for each model, and one with the attribute, its enums and the exception a method throws.  `GetOrderDto`, for instance, is a record with the properties `Id`, `Status`, `PlacedAt`, `ShippedOn` and `CustomerName`.  See [the generator](../../docs/manual/generator.md).

## Call the methods

Nothing here runs, but code of yours that uses `CodeGen/` would give `StoreQueries` a connection and call its methods:

```csharp
await using var connection = new SqlConnection(connectionString);
var queries = new StoreQueries(connection);

FindUserByEmailDto? ada = await queries.FindUserByEmailAsync(new EmailAddress("ada@example.com"), cancellationToken);
ListUsersSortedByDto[] users = await queries.ListUsersSortedByAsync("created_at DESC", cancellationToken);
await queries.WriteUserNamesAsync(Console.Out, cancellationToken);
FindDeliveryDto? delivery = await queries.FindDeliveryAsync(1, cancellationToken);
```

Each method opens the connection when it is closed and closes it again before it returns; `StreamUsersOldestFirst`, which `WriteUserNamesAsync` calls, opens it when its loop starts and closes it when the loop ends.  To run several queries on one open connection or in a transaction, open the connection yourself and pass the transaction to each generated method: see [the connection](../../docs/manual/generator.md#method-connection) and [the recipe](../../docs/manual/recipes.md#queries-in-a-transaction).
