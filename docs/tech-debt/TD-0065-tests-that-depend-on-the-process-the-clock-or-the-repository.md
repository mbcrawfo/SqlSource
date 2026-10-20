# TD-0065 - Tests that depend on the process, the clock or the repository

## Problem

A maintainability review of `tests/` on 2026-10-19 found these.  None fails today; each can make a test hang, flake or test something other than what the tool does.

1. **`dotnet msbuild` is run with no timeout.**  [`FixtureProjects`](../../tests/SqlSource.Tool.Testing/Fixtures/FixtureProjects.cs) reads a run's output synchronously and calls `WaitForExit()` with no token, no limit and no kill, about twenty-five times from the synchronous facts of `ManifestTargetTests`.  `ToolProcessTests` and `TerminationTests` start, drain and kill a process each in a way of its own.
2. **Tests run `dotnet` from the path, and the tool prefers `DOTNET_HOST_PATH`.**  [`Programs`](../../tests/SqlSource.Tool.Testing/Programs.cs), `FixtureProjects` and `TerminationTests` start a bare `dotnet`, while `ProjectEvaluator` takes `DOTNET_HOST_PATH` first, so `FixtureProjectTests`, through the tool, and `ManifestTargetTests`, directly, can run different SDKs.
3. **An end-to-end run cannot be cancelled.**  [`EndToEndTool`](../../tests/SqlSource.Tests.Postgres/EndToEnd/EndToEndTool.cs) passes `CancellationToken.None` to a run of MSBuild and PostgreSQL.
4. **The real clock is the default of the shared helpers.**  `CliRun`, `Hosts`, `PostgresScript` and the container tests' `Sessions` default to `TimeProvider.System`, and [`TestClock`](../../tests/SqlSource.Tool.Tests/TestClock.cs) is in `tests/SqlSource.Tool.Tests`, where neither library can use it.  So `PostgresEventsTests` and `PostgresEventsPlanTests` can check only that `durationMs` is a number, and every `CliRun` made outside `DescribeScene` runs on the machine's clock.
5. **Four polling loops with four limits**: 30 s and 5 s in `ProcessRunnerTests`, 120 s after a fixed 3 s delay in `TerminationTests`, and 5 s in the container's `SessionTests`; `ProcessGate` waits 30 s.  A kill on a loaded Windows runner can take more than 5 s.
6. **Tests read the repository by two routes.**  [`AppContainer`](../../tests/SqlSource.Tests.Postgres.Npgsql8/AppContainer.cs) reads `DefaultImage` out of the text of `PostgresContainer.cs` with a regex at run time; `DiagnosticsDocumentTests` reads `docs/manual` from a copy in the output folder while `UserDocs` reads it through `RepositoryRoot`.
7. **Three ways of leaving objects on the one server.**  [`ScratchSchema`](../../tests/SqlSource.Tests.Postgres/Describer/ScratchSchema.cs) holds the objects that several classes add, but `ColumnTests`, `TableMatchTests`, `ExplainTests` and `SetOperationTests` create objects inline and leave them, `ColumnTests` alters `inh_child` by the server's major, and `ParameterTests`, `ColumnTests` and `DescribeCommandTests` drop theirs in a `finally`.  What a test sees can depend on which class ran first.

## Why it exists

Each was the shortest way to a passing test when it was written, and the helpers that would make the safe way short did not exist yet.

## Impact

Low.  1 and 3 can hang a run until the CI job's own timeout; 2 can test an SDK the tool would not use; 5 and 7 can flake; 4 leaves durations untested.

## Proposed fix

1. One `Programs.RunAsync(start, patience)` in `tests/SqlSource.Tool.Testing` that drains both streams, waits on the test's token and kills the process tree; make the manifest tests async and use it in `ToolProcessTests` and `TerminationTests`.
2. One `Programs.Dotnet` that picks the program as `ProjectEvaluator` does.
3. Pass `TestContext.Current.CancellationToken`.
4. Move `TestClock` into `tests/SqlSource.Tool.Testing`, make it the default there, and assert the durations.
5. One `Eventually` helper in `tests/SqlSource.Tool.Testing` with one limit.
6. Move `DefaultImage` into `tests/SqlSource.Testing` and point the `sed` of `tools/record-fixtures.sh` and `tools/describe-end-to-end.sh` at it; read `docs/manual` through `RepositoryRoot` alone.
7. Move every object left on the server into `ScratchSchema`, and keep a drop in `finally` for objects that change what other tests see.

## Trigger

The first hang or flake of one of these tests in CI; 6 when the default image next moves.
