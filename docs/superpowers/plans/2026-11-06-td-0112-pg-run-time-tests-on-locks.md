# TD-0112 PostgreSQL run-time tests on locks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The two PostgreSQL run-time tests that hold a command with `pg_sleep` hold it on a lock instead, so that no stalled runner can see the command end before the test acts, and TD-0112 is resolved.

**Architecture:** The command-timeout test runs `TouchAccount` on a row another connection holds `FOR UPDATE`, as SQL Server's `RunTimeArgumentTests` does.  `ListThenSleep` becomes `ListThenWait`, which takes `pg_advisory_xact_lock_shared(@lockKey)` before its last row while the test holds the exclusive transaction-level lock on another connection; the test waits through `Eventually` until a backend is blocked by the holder, cancels, and the holder's rollback releases the lock.  The query `Sleep` goes: with the timeout on `TouchAccount` it has no user left but the cancelled-before-the-call test, which moves to `TouchAccount` too.

**Tech Stack:** C#, xunit v3, Shouldly, Npgsql, PostgreSQL 14 to 18.

**Spec:** `docs/tech-debt/TD-0112-two-postgresql-run-time-tests-race-a-sleep.md`

## Global Constraints

- Never let a timer keep a command running (`tests/AGENTS.md`, "Flaky tests"); wait for the state through `Eventually` before acting.
- Sidecars are written by `tools/describe-end-to-end.sh` alone, never by hand (`tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md`).
- Query parameters are camelCase (`@lockKey`).
- Resolve the item as `docs/tech-debt/AGENTS.md` says: delete the file, its row in `docs/tech-debt/README.md` (that row only), and every `TD-0112` reference.
- Run `tests/DatabaseTests/Postgres` on the default image and on `postgres:14.24` before the push (`tests/DatabaseTests/AGENTS.md`).

## Where the code differs from the spec

- The spec keeps the name `ListThenSleep`; once it sleeps no more the name is wrong, so it becomes `ListThenWait` (`ListThenWaitArgs(Count, LockKey)`).
- The spec does not say what becomes of `Sleep`.  It has no use left (the cancelled-before-the-call test sends nothing to the server), so it is removed and that test calls `TouchAccount`.

## Review Focus

1. The wait never sees the blocked backend (wrong `wait_event` filter on 14): the test fails through `Eventually`'s message, and cancels anyway, so no call is left waiting; the floor-image run checks the filter.
2. The cancel arrives after the server has sent every row but the last: the method still throws `OperationCanceledException`, as today.
3. A lock left held after a failure: the holder is a transaction disposed by `await using`, so every path rolls it back.
4. The method's backend is not the one found: the filter is `$1 = ANY(pg_catalog.pg_blocking_pids(pid))` with the holder's `ProcessID`, so only a backend blocked by this test's holder counts.
5. A row lock taken by the timeout test leaks into the next test of the collection: the holder transaction is disposed at the test's end.

---

### Task 1: Locks in place of sleeps

**Files:**
- Modify: `tests/DatabaseTests/Postgres/EndToEnd/Queries/Methods.sql` (header comment; remove `Sleep`; `ListThenSleep` → `ListThenWait`)
- Rewrite: `tests/DatabaseTests/Postgres/EndToEnd/Queries/Methods.sql.json` by `tools/describe-end-to-end.sh` (commit only its PostgreSQL `Methods.sql.json` change; revert anything else it rewrites that differs only by the run)
- Modify: `tests/DatabaseTests/Postgres/EndToEnd/RunTimeTests.cs`
- Modify: `tests/DatabaseTests/Postgres/README.md` (the `Methods.sql` row and the `RunTimeTests` bullet)
- Delete: `docs/tech-debt/TD-0112-two-postgresql-run-time-tests-race-a-sleep.md`; modify `docs/tech-debt/README.md` (its row)

**Interfaces:**
- Produces: `ListThenWait(ListThenWaitArgs(int Count, long LockKey))` returning the list of `n`.

- [ ] **Step 1: The query.**  `ListThenWait`:
  ```sql
  SELECT n FROM generate_series(1, @count) AS n
  LEFT JOIN LATERAL (SELECT pg_advisory_xact_lock_shared(@lockKey) WHERE n = @count) AS w ON true;
  ```
  The `WHERE` with no `FROM` evaluates the call only for the last row.  Remove `Sleep`; the file's header says one query waits on a lock for the cancellation.
- [ ] **Step 2: The tests.**
  - `TouchAccount_RowLockedPastACommandTimeoutOfOneSecond_Throws`: a second connection of the source begins a transaction and runs `SELECT 1 FROM accounts WHERE id = 2 FOR UPDATE`; `TouchAccount(new TouchAccountArgs(2), commandTimeout: 1, …)` on a closed connection throws `NpgsqlException` whose `InnerException` is `TimeoutException`; the connection is `Closed`.
  - `TouchAccount_TokenCancelledBeforeTheCall_ThrowsOperationCanceled`: as the old `Sleep_…` test.
  - `ListThenWait_TokenCancelledDuringTheRead_ThrowsOperationCanceled`: the holder takes `pg_advisory_xact_lock($1)` with `LockKey` in a transaction; the call runs with `RowsBeforeTheWait` rows; `Eventually` waits until `SELECT count(*) > 0 FROM pg_catalog.pg_stat_activity WHERE wait_event_type = 'Lock' AND $1 = ANY(pg_catalog.pg_blocking_pids(pid))` with the holder's `ProcessID`, then cancels however it ends; throws `OperationCanceledException`; connection `Closed`.  The holder is disposed after the call has ended.
  - The class comment says what holds each command, with no `TD-0112`; `LongSleep` goes.
- [ ] **Step 3: Sidecar.**  Run `tools/describe-end-to-end.sh`; expected: `Methods.sql.json` loses `Sleep` and `ListThenSleep` and gains `ListThenWait` with `lockKey` of `bigint`.
- [ ] **Step 4: Run.**  `dotnet test --project tests/DatabaseTests/Postgres -- --filter-class '*RunTimeTests'` (or the project's filter form), then the same with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`; expected: all pass.
- [ ] **Step 5: Docs and the item.**  README row and bullet say a row lock and an advisory lock; delete the item and its row; `grep -rn TD-0112 .` finds nothing outside `docs/superpowers/`.
- [ ] **Step 6: Commit** after `./format.sh` and `./pre-commit-validation.sh`.
