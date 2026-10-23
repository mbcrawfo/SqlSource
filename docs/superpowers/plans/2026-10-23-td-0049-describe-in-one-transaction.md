# TD-0049: One Transaction for Each Describe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run every call of one PostgreSQL describe, from `PREPARE` to `DEALLOCATE`, in one transaction that is rolled back whatever happens, so that a pooler in transaction mode keeps the describe on one backend, and resolve TD-0049.

**Architecture:** `PostgresConnection` holds the describe's transaction: `prepare` begins it with a savepoint, a call that fails on the server rolls back to the savepoint before it answers, `explain` sets its `SET LOCAL`s and rolls back to the savepoint after its plan, and `deallocate` frees the statement inside the transaction and then ends it.  Nothing a request or an answer holds changes, so the exchange, the captures and the session's order of calls stay as they are; only the `live` functions and the connection change.

**Tech Stack:** C#, Npgsql 10.0.3 (`NpgsqlTransaction.SaveAsync`/`RollbackAsync(name)`), xunit v3, Shouldly, Testcontainers PostgreSQL 14.24 and 18.6.

**Spec:** [`docs/tech-debt/TD-0049-describe-behind-a-pooler-can-reach-another-backend.md`](../../tech-debt/TD-0049-describe-behind-a-pooler-can-reach-another-backend.md) (Problem and Proposed fix), and the unit's instructions: prefer the full fix, re-record the captures it changes, prove it against PgBouncer in transaction mode, keep the manual's "connect directly" advice unless the owner agrees otherwise.

## What the merged code says that the item does not

- **`PREPARE` and `DEALLOCATE` are not transactional; `ROLLBACK TO SAVEPOINT` clears an aborted transaction and undoes `SET LOCAL`.**  Tried on `postgres:14.24`: a statement prepared in a transaction that is rolled back is still listed, `DEALLOCATE` in an aborted transaction is `25P02`, and after `ROLLBACK TO SAVEPOINT` the statement is still there and `plan_cache_mode` is back to `auto`.  So the savepoint can be taken before the `PREPARE`, and one savepoint serves the whole describe.
- **A transaction keeps the locks the `PREPARE` took.**  Parse analysis takes `AccessShareLock` on every relation the query names, and the planning of `explain` on every partition, until the transaction ends.  Another session's `DROP TABLE` or `ALTER TABLE` of such a relation now waits for the describe instead of landing between two calls.  `SessionTests.DescribeAsync_TableDroppedBetweenColumnsAndTables_IsSqlsrc225AtCatalogAndNoThrow` drops a table between `columns` and `tables` from a hook, which would now wait on the describe that waits on the hook: it becomes a test that the drop waits.  The `SchemaChanged` paths stay, for captures and for a type that the query names only in a cast, which nothing locks.  The manual's sentences that say a table or a column can change between two of the tool's questions (`diagnostics.md`, SQLSRC225 and SQLSRC231) become a type alone.
- **The `output-differs` check of `NullabilityWalk.OutputFits` was a guard against TD-0049.**  It stays, as a guard that the root's references agree with the row description; its comments, the nullability page and `src/SqlSource.Tool/AGENTS.md` stop naming TD-0049.  Removing it would change what a query with column aliases is answered, which is not this unit's: the owner is asked (below).
- **PgBouncer in statement mode refuses a transaction.**  Today it gives `SQLSRC236` (the statement is not listed); after the fix the `BEGIN` fails at `prepare`.  Task 4 records what it gives, and the owner is asked how the manual should speak of poolers.

## Global Constraints

- Nothing a `.sql` file holds may make the describer run a statement: the `PREPARE` still goes through `PostgresConnection.ExecuteAsOneStatementAsync`, and the tool's own texts (`SAVEPOINT`, `DEALLOCATE`) hold no user text.
- No request or answer record changes; `PostgresCallNames` gains nothing.  `tools/record-fixtures.sh postgres` must give no diff, or its diff is committed and explained.
- Every `live` function still answers through `PostgresLive.AnswerAsync`; a cancellation passes through and never waits on the server.
- The savepoint's name is `sqlsource_describe`, a constant of `PostgresConnection`.
- Validate with `./pre-commit-validation.sh` on the exact tree, and run `tests/SqlSource.Tests.Postgres` with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` before a push.
- The manual's "connect directly to the server" advice and `PostgresErrors.SessionNotKeptHelp` stay unless the owner answers otherwise.

## Review Focus

1. **A connection lost in the middle of a describe**: the call that finds out answers the error, the restore that follows fails silently, `deallocate` answers an error and ends the transaction without throwing, and every later describe answers `SQLSRC225` with the lost-connection text.  Pinned by the existing `SessionTests.DescribeAsync_ServerEndsTheSessionBetweenPrepareAndColumns_IsSqlsrc225AndNoThrow` and `DescribeAsync_AfterTheServerEndedTheSession_IsSqlsrc225EachTimeAndNoThrow`, which must still pass.
2. **A call that fails and a call after it**: `partitions` failing must not make `relations` fail with `25P02`.  Pinned by `TransactionTests.LiveCalls_CallThatFails_LeavesTheNextCallAndDeallocateWorking` (Task 1).
3. **A describe cancelled in the middle**: nothing waits on the server, and the session closes.  Pinned by `TransactionTests.DescribeAsync_CancelledAfterColumns_ThrowsAndTheSessionCloses` (Task 1).
4. **A `prepare` that fails** (a text of two statements, `42P05`, a statement that is not listed) leaves no transaction, so the next describe can begin one.  Pinned by `TransactionTests.PrepareAsync_TextOfTwoStatements_LeavesNoTransaction` and the existing session tests that describe after a failure.
5. **A failed `check`** (class `42`) leaves the statement freed: the existing `NpgsqlCheckTests` describe through `Sessions.DescribeAsync`, which asserts no prepared statement is left.

---

### Task 1: The describe's transaction

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs` (replace `BeginAsync`)
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs` (`PrepareAsync`, every later call, `ExplainAsync`, `DeallocateAsync`, remarks)
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs` (class remarks; the TD-0049 comment above `ColumnsAsync`; the comment of `WalkAsync` on `26000`)
- Create: `tests/SqlSource.Tests.Postgres/Describer/TransactionTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/ExplainTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/SessionTests.cs` (the table-drop test)

**Interfaces:**
- Produces, on `PostgresConnection`:
  - `public const string DescribeSavepoint = "sqlsource_describe";`
  - `public async Task BeginDescribeAsync(CancellationToken cancellationToken)`: opens the connection, begins a transaction and takes the savepoint (one round trip: Npgsql sends the `BEGIN` with the `SAVEPOINT`); on a failure of either it disposes the transaction and rethrows, so none is held.  Throws `InvalidOperationException` when a describe's transaction is already held.
  - `public bool InDescribe { get; }`: whether a describe's transaction is held.
  - `public async Task RestoreDescribeAsync(CancellationToken cancellationToken)`: `ROLLBACK TO SAVEPOINT`; throws `InvalidOperationException` when none is held.
  - `public async ValueTask EndDescribeAsync()`: disposes the held transaction, which rolls it back (Npgsql's dispose sends no error on a broken connection), and holds none afterwards; nothing when none is held.
  - `BeginAsync` is removed.
- Produces, in `PostgresLive`: a private `AskInDescribeAsync<TAnswer>(Func<Task<TAnswer>> call, Func<PostgresServerError, TAnswer> failed, CancellationToken token)`: `AnswerAsync` with `ConnectionErrors.Of`, where an `NpgsqlException` of the call first restores the savepoint when `InDescribe`, an `NpgsqlException` of that restore is dropped (the call's own error is the answer; a lost connection answers the next call), and the exception is rethrown for `AnswerAsync` to answer.

- [ ] **Step 1: Write the failing container tests** in `TransactionTests.cs` (`[Collection(UsesPostgres.Name)]`, `PostgresContainer postgres`), with a helper `InTransactionAsync(PostgresConnection)` that runs `SELECT pg_catalog.now() <> pg_catalog.statement_timestamp()` on the connection (true only inside a transaction begun by an earlier statement), and a helper that reads `xact_start` of the session's backend from a connection of the test's own (`pg_stat_activity` by `pg_backend_pid()` read on the session's connection before the describe):
  - `DescribeAsync_GatedQuery_RunsEveryCallFromPrepareToDeallocateInOneTransaction`: a `HookedExchange` `after` hook records, for each call but `open`, the backend's `xact_start` read from the test's own connection.  Query `SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id`, so that `explain` is asked.  Assert the calls are `prepare, columns, tables, types, spellings, check, explain, …, deallocate`, every value but the one after `deallocate` is the same non-null timestamp, and the one after `deallocate` is null.
  - `LiveCalls_CallThatFails_LeavesTheNextCallAndDeallocateWorking`: on a `PostgresLive` over a new `PostgresConnection`, `PrepareAsync("sqlsource_1", "SELECT id FROM users", [])` answers no error; `ColumnsAsync(new PostgresColumnsRequest("sqlsource_none", 0))` answers `26000`; `TablesAsync` of the `users` OID then answers rows and no error (not `25P02`); `DeallocateAsync("sqlsource_1")` answers no error; afterwards `pg_prepared_statements` counts 0 and `InTransactionAsync` is false.
  - `PrepareAsync_TextOfTwoStatements_LeavesNoTransaction`: `PrepareAsync` of `SELECT 1; SELECT 2` answers `42601`, `connection.InDescribe` is false and `InTransactionAsync` is false; a second `PrepareAsync` of `SELECT 1` then answers no error.
  - `DescribeAsync_CancelledAfterColumns_ThrowsAndTheSessionCloses`: a hook `after` `columns` cancels a `CancellationTokenSource` linked to the test's token; `session.DescribeAsync` with it throws `OperationCanceledException`; `await session.DisposeAsync()` completes; from the test's own connection the backend is gone within ten seconds (`Eventually` of `tests/SqlSource.Testing` if it fits, else a bounded poll of `pg_stat_activity`).
  - `ExplainAsync_WithoutPrepare_Throws`: `ExplainAsync` on a fresh `PostgresLive` throws `InvalidOperationException`.

- [ ] **Step 2: Run them and see the first three fail**

Run: `dotnet test tests/SqlSource.Tests.Postgres --filter "FullyQualifiedName~TransactionTests"`
Expected: `RunsEveryCall…` fails (each call its own transaction), `LiveCalls…` fails or passes for the wrong reason, `PrepareAsync_TextOfTwoStatements…` and `ExplainAsync_WithoutPrepare…` fail to compile until `InDescribe` exists.

- [ ] **Step 3: Implement the four members of `PostgresConnection`** above, with remarks: what the transaction is for (a pooler in transaction mode keeps a transaction on one backend), that a prepared statement survives the rollback so `deallocate` frees it inside the transaction, that the savepoint is taken before the `PREPARE` since rolling back to it frees nothing the `PREPARE` made, and that the transaction holds the locks the `PREPARE` took until it ends.

- [ ] **Step 4: Change `PostgresLive`.**
  - `PrepareAsync`: `BeginDescribeAsync` first; on an answer with an error (`StatementNotListed`) or an `NpgsqlException` of the `PREPARE` or the read, `EndDescribeAsync` before answering.  A cancellation passes through and ends nothing.
  - `ColumnsAsync`, `TablesAsync`, `RelationsAsync`, `PartitionsAsync`, `TypesAsync`, `SpellingsAsync`, `CheckAsync`, `RefusedAsync`: through `AskInDescribeAsync`.
  - `ExplainAsync`: throws `InvalidOperationException` unless `InDescribe` (an `EXPLAIN` without the describe's transaction would lose its `SET LOCAL`s); runs the `SET LOCAL`s and the `EXPLAIN`, then `RestoreDescribeAsync`, through `AskInDescribeAsync`.  Its remarks say the settings end with the savepoint and the plan is still made inside a transaction that is rolled back (TD-0047's sentence stays).
  - `DeallocateAsync`: `DEALLOCATE` inside the transaction, then `EndDescribeAsync`; when the `DEALLOCATE` throws an `NpgsqlException`, `EndDescribeAsync` and rethrow; a cancellation ends nothing.
  - The remarks of `StatementNotListed` and `PrepareAsync` stop saying a later call can reach another backend in transaction mode: the read runs in the transaction of the `PREPARE`.

- [ ] **Step 5: Change the existing tests.**
  - `ExplainTests.ExplainAsync_StatementThatWasNotPrepared_AnswersTheErrorAndLeavesNoTransaction` becomes `ExplainAsync_StatementThatWasNotPrepared_AnswersTheErrorAndTheDescribeGoesOn`: prepare `Statement`, explain `walk_nothing` → `26000`; `SHOW plan_cache_mode` is `auto` and `InDescribe` is still true.
  - `SessionTests.DescribeAsync_TableDroppedBetweenColumnsAndTables_IsSqlsrc225AtCatalogAndNoThrow` becomes `DescribeAsync_TableDroppedDuringTheDescribe_WaitsForIt`: the hook after `columns` runs `SET lock_timeout = '200ms'; DROP TABLE session_gone` on the test's own connection and records the `PostgresException`'s SQLSTATE, `55P03`; the describe succeeds; afterwards `DROP TABLE session_gone` succeeds.

- [ ] **Step 6: Run the describer's container tests on both images**

Run: `dotnet test tests/SqlSource.Tests.Postgres` and `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test tests/SqlSource.Tests.Postgres`
Expected: PASS.  A test that pinned the driver's events of a describe may now see `BEGIN`/`SAVEPOINT`/`ROLLBACK` commands: read it, and change the expectation only where the new command is the tool's own text.

- [ ] **Step 7: Validate and commit**

```bash
./pre-commit-validation.sh
git add src/SqlSource.Tool/Describing/Postgres tests/SqlSource.Tests.Postgres/Describer
git commit -m "Describe each PostgreSQL query in one transaction (TD-0049)"
```

### Task 2: The driver's savepoint events

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/NpgsqlRunLogger.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/NpgsqlRunLoggerTests.cs`

- [ ] **Step 1: Read Npgsql 10.0.3's `NpgsqlEventId.cs` and `LogMessages.cs`** (the tag `v10.0.3` of `npgsql/npgsql`) for the ids of creating a savepoint and rolling back to one, and their message templates.
- [ ] **Step 2: If each message is a fixed text with the savepoint's name**, add the two ids to `Forwarded["Npgsql.Transaction"]` and a test beside the existing allowlist tests that a message of each id is forwarded; otherwise leave the list and write why in the class's remarks.
- [ ] **Step 3: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~NpgsqlRunLogger"`, expected PASS; validate and commit (`git commit -m "Forward the driver's savepoint events"`).

### Task 3: Captures, docs, and the item resolved

**Files:**
- Delete: `docs/tech-debt/TD-0049-describe-behind-a-pooler-can-reach-another-backend.md`; its row in `docs/tech-debt/README.md`
- Modify: `src/SqlSource.Tool/AGENTS.md` (the rules of `explain`'s transaction, the walk's `output-differs`, the prepared statement and the session, the connection)
- Modify: `docs/architecture/postgres-describer.md` (one describe, the prepared statement and the session, the connection, the table of calls: `explain` "under the describe's savepoint")
- Modify: `docs/architecture/postgres-nullability.md` (`OutputFits` paragraph, and the plan's transaction where it says so)
- Modify: `src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.cs` (comment of `OutputFits`), `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs` (comment)
- Modify: `docs/manual/postgresql.md` (the pooler known issue is removed; the "see known issues for a pooler" sentence of nullability; "What describing does": each query is described in a transaction that is rolled back, and a change to a table the query reads waits for it)
- Modify: `docs/manual/diagnostics.md` (SQLSRC231: the pooler paragraph is removed, the schema-change sentence is about a type; SQLSRC225: the same sentence; SQLSRC236: the sentence on SQLSRC231 is removed, the rest waits for the owner)
- Fixtures: `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/`

- [ ] **Step 1: Record the fixtures** with `tools/record-fixtures.sh postgres` and read `git status`/`git diff`.  Expected: no change.  Any change is read, explained in the commit message, and committed.
- [ ] **Step 2: Edit the docs and comments** as listed, then `git grep -n "TD-0049"` and `git grep -n -i "pooler"`: the only hits left are the SQLSRC236 text the owner decides on, `PostgresErrors.SessionNotKeptHelp`, the manual's "connect directly" advice, and `docs/superpowers/`.
- [ ] **Step 3: Validate and commit** (`git commit -m "Resolve TD-0049: the describe of a query keeps one transaction"`).

### Task 4: Proof through PgBouncer (no file of the repository changes)

- [ ] **Step 1:** With the scratch scripts (PostgreSQL 18.6, `edoburu/pgbouncer:v1.26.0-p0` with `POOL_MODE=transaction` and `DEFAULT_POOL_SIZE=4`, eight `pgbench` clients on each of `realistic`, `app`, `billing`), run `describe --check tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj` through the pooler three times on the branch.  Before the fix, on `main`, each run failed with 54 to 65 `SQLSRC236` and 6 to 11 `SQLSRC231`.  Expected: exit 0 each time.
- [ ] **Step 2:** The same with `POOL_MODE=statement`, and record the first error.
- [ ] **Step 3:** Put both results in the PR description, remove the containers, and ask the owner, through the manager, whether the manual should now support poolers in transaction mode and how SQLSRC236 describes a pooler, and whether `output-differs` should stay.
