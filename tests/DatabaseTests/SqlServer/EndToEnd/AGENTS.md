# AGENTS.md - tests/DatabaseTests/SqlServer/EndToEnd

`tests/DatabaseTests/SqlServer/EndToEnd/` is SQL Server's end-to-end project ([what it covers](../README.md#the-end-to-end-project)).

## Sidecars and majors

- **Every sidecar of `EndToEnd/Queries/` and `EndToEnd/NodaTime/Queries/` is written by `tools/describe-end-to-end.sh` alone, never by hand.**
- **`CheckTests` runs `--check` over the real project, never a copy, through `CliRun.Real`**, as `ErrorPathTests` does.
- **No entry differs between the supported majors**, since `CheckTests` runs on each, 2017 and 2025 in CI and 2019 and 2022 every night: a parameter that two majors type differently declares its type with `-- param:`.
- **A query of a type that 2017 lacks goes into `Queries/NewTypes.sql`**, which `CheckTests` leaves out below 17, and its test skips by `SkipBelow(17, ...)`.
- **Every batch of `EndToEnd/app/schema.sql` and `EndToEnd/billing/schema.sql` runs on SQL Server 2017**; a type that 2017 lacks goes into `EndToEnd/app/new-types.sql`.

## Queries and tests

- **A query writes its parameters in camelCase, `@accountId`**, so that its input model binds through Dapper.
- **A query runs through its generated method, the project's default being `codegen`.**  A file whose SQL a Dapper test runs is claimed a second time at the output `models`, by a class of `EndToEnd/DapperQueries.cs` in the namespace of its first claim, so that the two share its models, since a `codegen` query has a method and no constant.
- **A test of a query reads, asserts the values of at least one row, `NULL`s included, and takes its connection from `EndToEndSources`.**
- **A test that writes runs in a transaction that is rolled back and passes it as `transaction` to every call**, of a method or of Dapper, since SqlClient refuses a command without it.
- **A test that must show that a commit succeeds commits only into a temporary table of its own connection**, as the transaction test of `EndToEnd/StreamingQueryTests.cs` does, since a rollback cannot show that the transaction was not aborted.
- **A test that changes a setting of the session, such as `SET XACT_ABORT ON`, takes a connection that is not pooled from `EndToEndSources.AppUnpooled`**, as `EndToEnd/UnpooledTransaction.cs` does, so that no other test gets the session.
- **`EndToEnd/ErrorPaths/` is a fixture project that the test project never compiles or describes**: the `.csproj` removes it from `Compile`, `AdditionalFiles` and `None` and copies it to the output as `Fixtures/Projects/ErrorPaths/`.
- **`RowBufferQueryTests` is the eight generated methods and their twins handed to `RowBufferReads`** of `tests/DatabaseTests/TestHarness`, which holds the body, so a check of what every collection type returns goes there.

## NodaTime

- **NodaTime is on in `EndToEnd/NodaTime/` alone**, by its `sqlsource.json`, which also gives `date` back to the type map over the project's `DateOnly`.  A query that needs NodaTime's types goes in that folder, whose files `CheckTests` names below 2025 with those of `EndToEnd/Queries/`.
- **A column of `noda_values` changes `EndToEnd/app/schema.sql`, the queries of `NodaTime/Queries/` and the rows of `NodaTime/NodaTimeQueryTests.cs` and of its copy in `tests/DatabaseTests/SqlServer.SqlClient61` together.**

## `all_types` and the recipe

- **A column of `all_types` changes `EndToEnd/app/schema.sql`, `EndToEnd/app/seed.sql` and `tests/DatabaseTests/TestHarness/EndToEnd/SqlServerAllTypesRows.cs` together**, which holds its expectation and its property in `AllTypesProperties`, with its parameter in `WriteAllTypes`, declared `null`, and in `InsertAllTypes` or its row in `WriteSideLeftOut` of `EndToEnd/AllTypesQueryTests.cs`.
- **`EndToEnd/ModelShapeTests.cs` and `tests/DatabaseTests/SqlServer.SqlClient61/AllTypesQueryTests.cs` compare the model with `AllTypesProperties`** and keep no list of their own.
- **A change to `WriteSideLeftOut` changes the list of parameters Dapper cannot send in `docs/manual/recipes.md#dapper-with-sql-server`.**
- **`EndToEnd/DapperRecipeTests.cs` proves what that part of the recipe says**, and `EndToEnd/Types/TypedParameter.cs`, `EndToEnd/Types/DateOnlyHandler.cs` and `EndToEnd/Types/TimeOnlyHandler.cs` are the same code as its samples, in this repository's layout: a change to one changes the other.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
