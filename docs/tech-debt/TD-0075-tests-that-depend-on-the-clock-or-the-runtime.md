# TD-0075 - Tests that depend on the clock or the runtime

## Problem

A maintainability review of `tests/` on 2026-10-22 found these tests, whose result depends on the speed of the machine:

1. [`SessionTests.AnswerAsync_CommandCancelledWhileTheServerRunsIt_ThrowsOperationCanceled`](../../tests/SqlSource.Tests.SqlServer/Describer/SessionTests.cs) cancels after 200 ms of the real clock.  On a slow runner the token fires before the command is sent, the test passes, and the `SqlException` path it is named for is not run.
2. [`TerminationTests`](../../tests/SqlSource.Tool.Tests/Commands/TerminationTests.cs) waits three seconds with `Task.Delay` to see that a run goes on, where `tests/AGENTS.md` has a test wait for what a program does through `Eventually`.  It runs on Windows alone.
3. [`ProgramsTests`](../../tests/SqlSource.Tool.Tests/ProgramsTests.cs) bounds a run under ten seconds with a `Stopwatch`, and [`SafetyTests`](../../tests/SqlSource.Tests.SqlServer/Describer/SafetyTests.cs) takes ten seconds as "at once".  Both say why in a comment.

## Why it exists

Each was the plain way to state its test when it was written, and each passes on the machines that run the tests today.

## Impact

Low.  1 can pass without testing what it names, and 2 and 3 can fail on a slow runner.

## Proposed fix

1. Cancel once `sys.dm_exec_requests` shows the `WAITFOR` running, through `Eventually`.
2. Wait through `Eventually` for a state the run shows, such as its folder and the MSBuild process under it.
3. Keep, or widen the bounds if a runner trips them.

## Trigger

A failure of one of these tests that a rerun clears.
