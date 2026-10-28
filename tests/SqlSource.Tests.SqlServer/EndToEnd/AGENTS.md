# AGENTS.md - tests/SqlSource.Tests.SqlServer/EndToEnd

`tests/SqlSource.Tests.SqlServer/EndToEnd/` is SQL Server's end-to-end project ([what it covers](../README.md#the-end-to-end-project)).

## Sidecars and majors

- **Every sidecar of `EndToEnd/Queries/` is written by `tools/describe-end-to-end.sh` alone, never by hand.**
- **`CheckTests` runs `--check` over the real project, never a copy, through `CliRun.Real`**, as `ErrorPathTests` does.
- **No entry differs between the supported majors**, since `CheckTests` runs on each, 2017 and 2025 in CI and 2019 and 2022 every night: a parameter that two majors type differently declares its type with `-- param:`.
- **A query of a type that 2017 lacks goes into `Queries/NewTypes.sql`**, which `CheckTests` leaves out below 17, and its test skips by `SkipBelow(17, ...)`.
- **Every batch of `EndToEnd/app/schema.sql` and `EndToEnd/billing/schema.sql` runs on SQL Server 2017**; a type that 2017 lacks goes into `EndToEnd/app/new-types.sql`.

## Queries and tests

- **A query writes its parameters in camelCase, `@accountId`**, so that its input model binds through Dapper.
- **A query runs through its generated method, the project's default being `codegen`.**  A file whose SQL a Dapper test runs is claimed a second time at the output `models`, by a class of `EndToEnd/DapperQueries.cs` in the namespace of its first claim, so that the two share its models, since a `codegen` query has a method and no constant.
- **A test of a query reads, asserts the values of at least one row, `NULL`s included, and takes its connection from `EndToEndSources`.**
- **A test that writes runs in a transaction that is rolled back and passes it as `transaction` to every call**, of a method or of Dapper, since SqlClient refuses a command without it.
- **`EndToEnd/ErrorPaths/` is a fixture project that the test project never compiles or describes**: the `.csproj` removes it from `Compile`, `AdditionalFiles` and `None` and copies it to the output as `Fixtures/Projects/ErrorPaths/`.

## `all_types` and the recipe

- **A column of `all_types` changes `EndToEnd/app/schema.sql`, `EndToEnd/app/seed.sql` and `tests/SqlSource.Testing/EndToEnd/SqlServerAllTypesRows.cs` together**, which holds its expectation and its property in `AllTypesProperties`, with its parameter in `WriteAllTypes`, declared `null`, and in `InsertAllTypes` or its row in `WriteSideLeftOut` of `EndToEnd/AllTypesQueryTests.cs`.
- **`EndToEnd/ModelShapeTests.cs` and `tests/SqlSource.Tests.SqlServer.SqlClient61/AllTypesQueryTests.cs` compare the model with `AllTypesProperties`** and keep no list of their own.
- **A change to `WriteSideLeftOut` changes the list of parameters Dapper cannot send in `docs/manual/recipes.md#dapper-with-sql-server`.**
- **`EndToEnd/DapperRecipeTests.cs` proves what that part of the recipe says**, and `EndToEnd/Types/TypedParameter.cs`, `EndToEnd/Types/DateOnlyHandler.cs` and `EndToEnd/Types/TimeOnlyHandler.cs` are the same code as its samples, in this repository's layout: a change to one changes the other.

## Allocations

- **`AllocationTests.cs` holds each generated method to a hand-written one that makes the same calls**, within a margin below the smallest object.  A change to the calls a method makes changes the hand-written method with it, never the margin.
- **A pooled result member that copies its rows out needs a test against a hand-written baseline of its own**, as `ToArray`, `ToList` and `ToImmutableArray` have, since the pairs against `RowBufferByHand` cannot see what the buffer costs.  Each `Least*SavingInBytesPerCall` is set from a measure, raised when the pool saves more, and never lowered to make a change pass.
- **It measures through `CallAllocations` of `tests/SqlSource.Testing`**, by the commonest difference of pairs of calls, never by a total, a median of blocks or the least of the calls ([why](../../SqlSource.Testing/EndToEnd/CallAllocations.cs)).

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
