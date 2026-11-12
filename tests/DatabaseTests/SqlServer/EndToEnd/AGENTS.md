# AGENTS.md - tests/DatabaseTests/SqlServer/EndToEnd

`tests/DatabaseTests/SqlServer/EndToEnd/` is SQL Server's end-to-end project ([what it covers](../README.md#the-end-to-end-project)).

## Sidecars and majors

- **Every sidecar of `EndToEnd/Queries/`, `EndToEnd/NodaTime/Queries/`, `EndToEnd/NetTopologySuite/Queries/` and `EndToEnd/Json/Queries/` is written by `tools/describe-end-to-end.sh` alone, never by hand.**
- **`CheckTests` runs `--check` over the real project, never a copy, through `CliRun.Real`**, as `ErrorPathTests` does.
- **No entry differs between the supported majors**, since `CheckTests` runs on each, 2017 and 2025 in CI and 2019 and 2022 every night: a parameter that two majors type differently declares its type with `-- param:`.
- **A query of a type that 2017 lacks goes into `Queries/NewTypes.sql`, or `Json/Queries/JsonColumns.sql` for one of System.Text.Json on `json`**, which `CheckTests` leaves out below 17, and its test skips by `SkipBelow(17, ...)`.
- **Every batch of `EndToEnd/app/schema.sql` and `EndToEnd/billing/schema.sql` runs on SQL Server 2017**; a type that 2017 lacks goes into `EndToEnd/app/new-types.sql`.

## Queries and tests

- **A query writes its parameters in camelCase, `@accountId`**, so that its input model binds through Dapper.
- **A query runs through its generated method, the project's settings file sets `methods`.**
- **A Dapper test runs the constant of a copy of its query in the same file, `<Query>ForDapper`, marked `-- output: models` and naming the original's models by `-- input-model:` and `-- output-model:` unless they are a table's, with `-- member-location: class`**, which puts the constant on the type, since a `methods` query has a method and no constant.  `VariantCopyTests` holds each copy's SQL to its original's.
- **A variant of a query, under another generator parameter, collection type, output or member location, is a copy of it in its file, `<Query><Variant>`, never a second claim of the file**, since a file gives every claim the same settings.
- **A test of a query reads, asserts the values of at least one row, `NULL`s included, and takes its connection from `EndToEndSources`.**
- **A test that writes runs in a transaction that is rolled back and passes it as `transaction` to every call**, since SqlClient refuses a command without it, or, for a collection method, which commits its own, writes into a temporary table of its connection, named by a token, as `EndToEnd/BatchQueryTests.cs` does; a test of what the pool keeps writes into a table of `app` named for it, which it drops, since the pool drops a session's temporary tables.
- **A test of a repository method, which takes no transaction, writes only a column's own value**, as `TouchNote` of `Queries/Repository.sql` does, so that it leaves the seed as it was.
- **A test that must show that a commit succeeds commits only into a temporary table of its own connection**, as the transaction test of `EndToEnd/StreamingQueryTests.cs` does, since a rollback cannot show that the transaction was not aborted.
- **A test that changes a setting of the session, such as `SET XACT_ABORT ON`, takes a connection that is not pooled from `EndToEndSources.AppUnpooled`**, as `EndToEnd/UnpooledTransaction.cs` does, so that no other test gets the session; a test of what the pool keeps of a session takes a pool of its own by a unique `ApplicationName`.
- **`EndToEnd/ErrorPaths/` is a fixture project that the test project never compiles or describes**: the `.csproj` removes it from `Compile`, `AdditionalFiles` and `None` and copies it to the output as `Fixtures/Projects/ErrorPaths/`.
- **A change to `EndToEnd/ListQueryTests.cs` changes its copy in `tests/DatabaseTests/SqlServer.SqlClient61` too**, which runs the same lists on SqlClient 6.1.
- **A query added here that has a pair under `method-type=both`, neither a stream nor a `<Query>Sync` copy, gets its scenarios in `tests/DatabaseTests/SqlServer.SyncAsync` in the same change**, or that project's coverage test fails at night.
- **`RowBufferQueryTests` is the eight generated methods and their twins handed to `RowBufferReads`** of `tests/DatabaseTests/TestHarness`, which holds the body, so a check of what every collection type returns goes there.

## NodaTime

- **NodaTime is on in `EndToEnd/NodaTime/` alone**, by its `sqlsource.json`, which also gives `date` back to the type map over the project's `DateOnly`.  A query that needs NodaTime's types goes in that folder, whose files `CheckTests` names below 2025 with those of `EndToEnd/Queries/`.
- **A column of `noda_values` changes `EndToEnd/app/schema.sql`, the queries of `NodaTime/Queries/` and the rows of `NodaTime/NodaTimeQueryTests.cs` and of its copy in `tests/DatabaseTests/SqlServer.SqlClient61` together.**

## NetTopologySuite

- **NetTopologySuite is on in `EndToEnd/NetTopologySuite/` alone**, by its `sqlsource.json`.  A query that needs its geometries goes in that folder, whose files `CheckTests` names below 2025 with those of `EndToEnd/Queries/`.
- **A column of `spatial_values` changes `EndToEnd/app/schema.sql`, the queries of `NetTopologySuite/Queries/` and `NetTopologySuite/NetTopologySuiteQueryTests.cs` and its copy in `tests/DatabaseTests/SqlServer.SqlClient61` together.**
- **A subtype by a type override goes in a column of its own type, `point_value geography` or `polygon_value geometry`**, since an override is keyed by the database type, and a file header's override of `geography` or `geometry` makes every column of it that subtype.
- **`tests/DatabaseTests/TestHarness/EndToEnd/WrappingConnection.cs` keeps its reader to the members `DbDataReader` requires and its disposal**, so that the read through it takes `DbDataReader`'s `GetStream`; a query run through it has no parameter.

## JSON

- **System.Text.Json is on in `EndToEnd/Json/` alone**, by its `sqlsource.json`, which also holds the opt-in of `json_values.settings_meta`: a query that needs its types or a class in a JSON column goes in that folder, whose files `CheckTests` names below 2025, `JsonColumns.sql` left out.
- **A column of `json_values` or `json_columns` changes `EndToEnd/app/schema.sql` or `EndToEnd/app/new-types.sql`, the queries of `Json/Queries/` and `Json/JsonQueryTests.cs`, and its copy in `tests/DatabaseTests/SqlServer.SqlClient61`, together**; a class of `Json/Types/` changes that project's copy too.

## `all_types` and the recipe

- **A column of `all_types` changes `EndToEnd/app/schema.sql`, `EndToEnd/app/seed.sql` and `tests/DatabaseTests/TestHarness/EndToEnd/SqlServerAllTypesRows.cs` together**, which holds its expectation and its property in `AllTypesProperties`, with its parameter in `WriteAllTypes`, declared `null`, and in `InsertAllTypes` or its row in `WriteSideLeftOut` of `EndToEnd/AllTypesQueryTests.cs`.
- **`EndToEnd/ModelShapeTests.cs` and `tests/DatabaseTests/SqlServer.SqlClient61/AllTypesQueryTests.cs` compare the model with `AllTypesProperties`** and keep no list of their own.
- **A change to `WriteSideLeftOut` changes the list of parameters Dapper cannot send in `site/recipes.md#dapper-with-sql-server`.**
- **`EndToEnd/DapperRecipeTests.cs` proves what that part of the recipe says**, and `EndToEnd/Types/TypedParameter.cs`, `EndToEnd/Types/DateOnlyHandler.cs` and `EndToEnd/Types/TimeOnlyHandler.cs` are the same code as its samples, in this repository's layout: a change to one changes the other.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
