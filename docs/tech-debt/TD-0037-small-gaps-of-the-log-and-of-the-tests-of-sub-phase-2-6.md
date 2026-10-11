# TD-0037 - Small gaps of the log and of the tests of sub-phase 2.6

## Problem

The reviews of sub-phase 2.6 found these, and the pull request leaves them.

1. **Untested under `--check`.**  [`SidecarCheck`](../../src/SqlSource.Tool/Describing/SidecarCheck.cs) and its tests in [`CheckTests`](../../tests/SqlSource.Tool.Tests/Commands/CheckTests.cs) do not cover: a file that is not `Ready` (a sentence of the spec); the order of the differences of two queries of one file that both differ; a sidecar that the system will not open, of which only a directory is tested.  The `engine` arm of a difference's text cannot be reached without a hand edit of a sidecar, since the query's hash holds the engine and a changed engine is reported as a changed query first.
2. **Untested: the `--database` guard** of [`SidecarCheck.EntriesWithoutAQuery`](../../src/SqlSource.Tool/Describing/SidecarCheck.cs) for an entry of a query that is gone, in [`CheckFilterTests`](../../tests/SqlSource.Tool.Tests/Commands/CheckFilterTests.cs): with the database's name in another case, with an entry that has no database, and under a `.sql` path or `--project` with no `--database`.
3. **Untested values of members that are a contract of the log**, in [`RunLogTests`](../../tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs): `usesSqlSource` null and false; `state` other than `ready`; `decision` `left-out`; `selected` false; a delete that fails.  All are built in [`RunEvents`](../../src/SqlSource.Tool/Logging/RunEvents.cs).
4. **Untested lines of `--verbose`**, in [`VerboseTests`](../../tests/SqlSource.Tool.Tests/Commands/VerboseTests.cs): `has parse errors`, `cannot be described`, `could not be read`, `deleted`, `could not be changed` and `no session`, from [`RunEvents`](../../src/SqlSource.Tool/Logging/RunEvents.cs).
5. **`RunEvents.Opened` runs outside the guard of `DatabaseRuns`.**  [`DatabaseRuns`](../../src/SqlSource.Tool/Describing/DatabaseRuns.cs) calls it from `DescribeAllAsync` without `Guard.Call`, so a describer that gives a null name for a setting ends the run with a plain `SQLSRC200`, and not as a describer's fault with the connection's value removed.
6. **`Logging/` references `Describing`, `Planning` and `Projects`, which call back into it.**  [`RunEvents`](../../src/SqlSource.Tool/Logging/RunEvents.cs) takes `QueryWork`, `PlannedFile`, `ProjectEvaluation` and the like, and those steps call `RunEvents`: the namespaces depend on each other in a circle.
7. **The one test of `Cli`'s report of a log that fails while the run goes on needs `/dev/full`**, and is skipped on macOS and Windows: `Run_LogThatStopsBeingWritable_FinishesTheRunAndIsOneSqlsrc218` in [`RunLogTests`](../../tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs).  [`RunLogFile`](../../src/SqlSource.Tool/Logging/RunLogFile.cs) opens its own stream, so no test of `Cli` can give it one that fails.
8. **A member named `_event` beside one named `event` is written twice.**  [`RunLogFile`](../../src/SqlSource.Tool/Logging/RunLogFile.cs) writes a top-level member that has one of its own three names, `event`, `time` and `truncated`, with an underscore before it, and does not look whether the event already has a member of that new name.  A describer that writes both `event` and `_event` gets a line with two `_event` keys, and a reader that takes the last one loses the first.

## Why it exists

Each was found by a review of a task, or of the whole branch, after the code and its tests were written, and none changes what a user sees.  Several of them (1 to 4) are cases of tests, and the plan of the sub-phase had room for the main case of each; 5 cannot be reached by the tools' one describer, the fake of the tests; 6 is the shape the events took when `RunEvents` became the one place that builds them; 7 needs a seam that the log did not have; 8 needs a describer that sets out to do it, and shadows only a member of its own event.

## Impact

Low.  The untested paths are small and read straight from the code, but a later edit can break one without a test failing.  5 is a trap for phase 3, whose describer writes the first real events and may give a setting.  6 costs nothing to run and makes a change to either side harder to place.  7 means the path of a log that fails mid-run is not run on a developer's machine, only on the build server, which is Linux.

## Proposed fix

1. Add the cases of 1 to 4, each to the class named.
2. For 5, call `RunEvents.Opened` through the guard, as the call to `session.Server` that precedes it is.
3. For 6, give `RunEvents` the plain data it needs, or move the events that take a type of another namespace to the namespace that owns the type.
4. For 7, let `Cli` open the log through a function of the host that a test can replace with one that gives a stream that fails.
5. For 8, have the writer refuse, or number, a member whose new name the event already holds.

## Trigger

Phase 3, whose describer writes the first real events and the first real settings.
