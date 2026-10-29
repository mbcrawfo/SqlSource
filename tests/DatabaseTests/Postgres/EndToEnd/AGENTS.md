# AGENTS.md - tests/DatabaseTests/Postgres/EndToEnd

`tests/DatabaseTests/Postgres/EndToEnd/` is the end-to-end project ([what it covers](../README.md#the-end-to-end-project)).

## Sidecars

- **Every sidecar of `EndToEnd/Queries/` is written by `tools/describe-end-to-end.sh` alone, never by hand.**
- **`CheckTests` runs `--check` over the real project, never a copy, through `CliRun.Real`**, which reads the test process's environment and runs the real MSBuild, as `ErrorPathTests` does.

## Queries and tests

- **A query writes its parameters in camelCase, `@accountId`**, so that an input model binds through Dapper.
- **A Dapper test runs the constants of a second claim of its file at the output `models`**, since a `codegen` query has a method and no constant.  The claim is in the namespace of the `codegen` class whose models it shares, as `EndToEnd/AllTypesSql.cs` and `EndToEnd/NoteSql.cs` are.
- **A test of a method calls the method, never Dapper in its place**; one that reads asserts the values of at least one row, `NULL`s included.
- **Every test takes its data source from `EndToEndSources`**, and a test that writes runs in a transaction that is rolled back.
- **A test that must show that a commit succeeds commits only into a temporary table of its own connection**, as the transaction test of `EndToEnd/StreamingQueryTests.cs` does, since a rollback cannot show that the transaction was not aborted.
- **`EndToEnd/ErrorPaths/` is a fixture project that the test project never compiles or describes**: the `.csproj` removes it from `Compile`, `AdditionalFiles` and `None` and copies it to the output as `Fixtures/Projects/ErrorPaths/`.
- **`RowBufferQueryTests` is the eight generated methods and their twins handed to `RowBufferReads`** of `tests/DatabaseTests/TestHarness`, which holds the body, so a check of what every collection type returns goes there.

## `all_types` and the recipe

- **A column of `all_types` changes `EndToEnd/app/schema.sql`, `EndToEnd/app/seed.sql` and `tests/DatabaseTests/TestHarness/EndToEnd/AllTypesRows.cs` together**, with its parameter, declared `null`, in `InsertEveryType`, and its parameter in `InsertAllTypes` or its row in `WriteSideLeftOut` of `EndToEnd/AllTypesQueryTests.cs`.
- **A type added to the map adds its key to `MapWritten` or `MapLeftOut` of `EndToEnd/ModelShapeTests.cs`.**
- **A change to `WriteSideLeftOut` changes the list of parameters Dapper cannot send in `docs/manual/recipes.md#dapper-with-postgresql`.**
- **`EndToEnd/DapperRecipeTests.cs` writes each of its columns by the recipe's remedy and proves the rest of that recipe**, and `EndToEnd/Types/TypedParameter.cs` and `EndToEnd/Types/EmailHandler.cs` are the same code as its samples, in this repository's layout: a change to one changes the other.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
