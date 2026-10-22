# TD-0075 - Tests that depend on the clock or the runtime

## Problem

A maintainability review of `tests/` on 2026-10-22 found these tests, whose result depends on the speed of the machine or on an order .NET does not promise:

1. [`SessionTests.AnswerAsync_CommandCancelledWhileTheServerRunsIt_ThrowsOperationCanceled`](../../tests/SqlSource.Tests.SqlServer/Describer/SessionTests.cs) cancels after 200 ms of the real clock.  On a slow runner the token fires before the command is sent, the test passes, and the `SqlException` path it is named for is not run.
2. [`TerminationTests`](../../tests/SqlSource.Tool.Tests/Commands/TerminationTests.cs) waits three seconds with `Task.Delay` to see that a run goes on, where `tests/AGENTS.md` has a test wait for what a program does through `Eventually`.  It runs on Windows alone.
3. `PropertiesOf` of both [`ModelShapeTests`](../../tests/SqlSource.Tests.Postgres/EndToEnd/ModelShapeTests.cs), of [`AllTypesQueryTests`](../../tests/SqlSource.Tests.SqlServer.SqlClient61/AllTypesQueryTests.cs) and `NamesOf` of both end-to-end `AllTypesQueryTests` compare the order of `Type.GetProperties()`, which .NET does not promise to be the order of declaration.  `RealisticModels` orders by `MetadataToken`, which is.
4. [`ProgramsTests`](../../tests/SqlSource.Tool.Tests/ProgramsTests.cs) bounds a run under ten seconds with a `Stopwatch`, and [`SafetyTests`](../../tests/SqlSource.Tests.SqlServer/Describer/SafetyTests.cs) takes ten seconds as "at once".  Both say why in a comment.

## Why it exists

Each was the plain way to state its test when it was written, and each passes on the machines that run the tests today.

## Impact

Low.  1 can pass without testing what it names, 2 and 4 can fail on a slow runner, and 3 can fail on a runtime that orders members otherwise.

## Proposed fix

1. Cancel once `sys.dm_exec_requests` shows the `WAITFOR` running, through `Eventually`.
2. Wait through `Eventually` for a state the run shows, such as its folder and the MSBuild process under it.
3. Order by `MetadataToken` in the shared helper that TD-0074's item 2 proposes.
4. Keep, or widen the bounds if a runner trips them.

## Trigger

A failure of one of these tests that a rerun clears, or the shared model helper of TD-0074.
