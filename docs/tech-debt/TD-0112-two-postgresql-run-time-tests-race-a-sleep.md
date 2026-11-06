# TD-0112 - Two PostgreSQL run-time tests race a sleep

## Problem

Found by the maintainability review of 2026-11-06.  Two tests of [`RunTimeTests`](../../tests/DatabaseTests/Postgres/EndToEnd/RunTimeTests.cs) hold their command with `pg_sleep` of 30 seconds (`Sleep` and `ListThenSleep` of `tests/DatabaseTests/Postgres/EndToEnd/Queries/Methods.sql`), where `tests/AGENTS.md` says never to let a timer keep a command running, and to hold it on a row lock or an application lock:

- `Sleep_CommandTimeoutShorterThanTheSleep_Throws` races its one-second command timeout against the sleep.  If the test process is starved for about 29 seconds after it sends the command, the sleep returns its row and the test fails.  Its SQL Server twin, `RunTimeArgumentTests.TouchAccount_RowLockedPastACommandTimeoutOfOneSecond_ThrowsTheTimeout`, holds a row lock instead.
- `ListThenSleep_TokenCancelledDuringTheRead_ThrowsOperationCanceled` waits through `Eventually`, whose limit is `Eventually.Patience`, two minutes, to see the sleep before it cancels.  The class comment's claim that each sleep lasts "far longer than the test waits" does not hold: on a loaded runner where seeing the sleep and cancelling take more than 30 seconds, the query ends normally and the test fails.

## Why it exists

The tests were written before the rule on timers, and PostgreSQL's `pg_sleep` was the plain way to keep a command running.

## Impact

Low: a failure on a badly stalled runner, which a rerun passes.

## Proposed fix

Run `TouchAccount` with `commandTimeout: 1` against a row that another connection holds `FOR UPDATE`, as the SQL Server twin does.  Hold the last row of `ListThenSleep` on `pg_advisory_xact_lock_shared(key)` while the test holds the exclusive advisory lock on another connection, wait for the lock's `wait_event_type` instead of `PgSleep`, cancel, then release.  The change to `Methods.sql` rewrites its sidecar through `tools/describe-end-to-end.sh`.

## Trigger

A failure of either test, or the next change to `RunTimeTests` or its queries.
