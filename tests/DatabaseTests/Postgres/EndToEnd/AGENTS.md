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

## `all_types` and the recipe

- **A column of `all_types` changes `EndToEnd/app/schema.sql`, `EndToEnd/app/seed.sql` and `tests/SqlSource.Testing/EndToEnd/AllTypesRows.cs` together**, with its parameter, declared `null`, in `InsertEveryType`, and its parameter in `InsertAllTypes` or its row in `WriteSideLeftOut` of `EndToEnd/AllTypesQueryTests.cs`.
- **A type added to the map adds its key to `MapWritten` or `MapLeftOut` of `EndToEnd/ModelShapeTests.cs`.**
- **A change to `WriteSideLeftOut` changes the list of parameters Dapper cannot send in `docs/manual/recipes.md#dapper-with-postgresql`.**
- **`EndToEnd/DapperRecipeTests.cs` writes each of its columns by the recipe's remedy and proves the rest of that recipe**, and `EndToEnd/Types/TypedParameter.cs` and `EndToEnd/Types/EmailHandler.cs` are the same code as its samples, in this repository's layout: a change to one changes the other.

## Allocations

- **`AllocationTests.cs` holds each generated method to a hand-written one that makes the same calls**, within a margin.  A change to the calls a method makes changes the hand-written method with it, never the margin.
- **The streaming `StreamAccounts` has an allocation pair of its own**, against a hand-written static local async iterator, within the same margin.
- **A pooled result member that copies its rows out needs a test against a hand-written baseline of its own**, as `ToArray`, `ToList` and `ToImmutableArray` have, since the pairs against `RowBufferByHand` cannot see what the buffer costs.  Each `Least*SavingInBytesPerCall` is set from a measure, raised when the pool saves more, and never lowered to make a change pass.
- **It measures through `CallAllocations` of `tests/SqlSource.Testing`**, by the commonest difference of pairs of calls, never by a total, a median of blocks or the least of the calls ([why](../../SqlSource.Testing/EndToEnd/CallAllocations.cs)).

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
