# PostgresExamples

A small project that uses SqlSource with PostgreSQL and Dapper: what you write, what you get, and how to run the tool.  It builds with the rest of the repository, so what it shows is what SqlSource does today.  For what SqlSource is and how to install it, start with the [repository's README](../../README.md); the [manual](../../docs/manual/README.md) has everything this page does not explain.

## What is in it

| Path | What it shows |
|----|----|
| [`schema.sql`](schema.sql) | The database: users, their orders and the lines of each order, with a few rows |
| [`docker-compose.yml`](docker-compose.yml) | Starts PostgreSQL with that schema |
| [`Sql/`](Sql) | The output `sql`: `CountUsers` as a constant, and `CountRows` as a method that fills in a [token](../../docs/manual/parameters-and-tokens.md#tokens), in a nested class that the attribute [names](../../docs/manual/settings.md#setting-nested-class) `Counts`.  They are not described, so they have no sidecar and need no connection |
| [`Models/`](Models) | The output `models`: each query gets a model of its parameters and one of its rows.  `GetOrder` has a nullable column and a `timestamptz` read as a `DateTimeOffset` by the project's [type override](../../docs/manual/settings.md#setting-type-overrides); `ListOrderTotals` has a sum that the tool shows is never null, `CountShippedOrders` has a count that the tool shows is never null, and `ListUserDisplayNames` makes a column not null with a [`-- column:` marker](../../docs/manual/generator.md#column-marker); `GetOrderTags` has an array column, read into the default record; `GetUser` is `SELECT *`, so its rows are the [table's model](../../docs/manual/generator.md#table-models), `UsersDto` |
| [`CodeGen/`](CodeGen) | The default output, `codegen`: `ListOrderLines` gets its models and a method that runs it, an extension method of the connection, which returns every row by the [result shape](../../docs/manual/sql-files.md#result-shapes) after the query's name, `many` |
| [`DapperSetup.cs`](DapperSetup.cs), [`UtcDateTimeOffsetHandler.cs`](UtcDateTimeOffsetHandler.cs) | What Dapper needs once, before the first query: see [the recipe](../../docs/manual/recipes.md#dapper-with-postgresql) |

Each folder's `.sql` files belong to the one class beside them, whose `[SqlSourceGenerate]` attribute sets the folder's output, and whose methods run the queries: in `Sql/` and `Models/` the generated SQL through Dapper, into the generated models, and in `CodeGen/` the generated method.  The queries write their parameters in camelCase, `@orderId`: in `Models/` Dapper binds it to the property `OrderId` of the input model, and in `CodeGen/` the generated method binds it from that property itself.  Nothing here runs: the project compiles, so a model that changes shape breaks its build.

## Your own project

[`PostgresExamples.csproj`](PostgresExamples.csproj) takes the generator from this repository, so that it builds with the generator of the same commit and shows what SqlSource does now.  The repository's own build files give it its language version, its analyzers and the versions of its packages.  A project of your own adds the packages instead, here with the versions this example builds with:

```bash
dotnet add package SqlSource --prerelease
dotnet add package Dapper --version 2.1.89
dotnet add package Npgsql --version 10.0.3
```

The rest of the project file, nullable reference types, the dialect and the type override, is what your project sets too: see [the settings](../../docs/manual/settings.md#all-settings).  It also keeps [SQLSRC240](../../docs/manual/diagnostics.md#sqlsrc240) a warning, which only this repository needs, so that it builds while its version moves ahead of the committed sidecars.

## Start the database

From this folder:

```bash
docker compose up -d --wait
```

PostgreSQL listens on `localhost:55432`, with the database, user and password all `examples`.  They are for this example alone.  If the port is taken, change `55432` under `ports` in `docker-compose.yml`, and the port in the connection strings below.  The schema is applied the first time the database starts; after a change to `schema.sql`, start again from nothing:

```bash
docker compose down -v
```

## Describe the queries

The tool asks the database about each query of `Models/` and `CodeGen/`, and records the answer in a `.sql.json` file beside each of their `.sql` files, its sidecar.  The project has one database, so the tool takes its connection from `SQLSOURCE_CONNECTION`.  In a project of your own, with the tool installed:

```bash
export SQLSOURCE_CONNECTION="Host=localhost;Port=55432;Database=examples;Username=examples;Password=examples"
dotnet sqlsource describe
```

The tool describes the queries that have no current entry in their sidecar.  `dotnet sqlsource describe --check` compares the sidecars with what the database says now, and `--force` describes every query again.

This example's sidecars are committed and current.  They are written by `tools/describe-end-to-end.sh`, which starts a database of its own: see [the recording scripts](../../tools/README.md#recording-scripts).  To compare them with the Compose database, run the tool from its source, from the repository's root, with `SQLSOURCE_CONNECTION` set as above, since the repository pins no published tool and another version of it writes another version into every sidecar:

```bash
dotnet run --project src/SqlSource.Tool -- describe --check examples/PostgresExamples/PostgresExamples.csproj
```

See [the tool](../../docs/manual/tool.md) for the commands and the connections.

## Read a sidecar

Open [`Models/Orders.sql.json`](Models/Orders.sql.json).  It is the tool's record of what the database said about each query: its parameters, and its columns, each with its type and whether it can be null.  You never edit a sidecar: the tool writes it, and the build reports a query whose entry is missing or older than its SQL.  See [the sidecar](../../docs/manual/concepts.md#sidecar).

## Build, and see the generated code

```bash
dotnet build
```

Your IDE shows the generated code among the project's dependencies, under its analyzers or source generators, and goes to it from a use such as `Sql.GetOrder` or `GetOrderDto`.  To write it to disk, build with `EmitCompilerGeneratedFiles`:

```bash
dotnet build -p:EmitCompilerGeneratedFiles=true
```

The files are under `obj/Debug/net10.0/generated/`: one for each class, with the SQL of its queries, one for each model, and one with the attribute and its enums.  `GetOrderDto`, for instance, is a record with the properties `Id`, `Status`, `PlacedAt`, `ShippedAt` and `CustomerName`.  See [the generator](../../docs/manual/generator.md).
