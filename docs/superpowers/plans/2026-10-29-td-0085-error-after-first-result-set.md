# TD-0085: report a SQL Server error after the first result set — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A generated SQL Server method that reads rows, buffered or streaming, drains the results after its rows with `NextResultAsync` and the method's token, so that an error a later statement raises is thrown instead of lost.

**Architecture:** `QueryMethodWriter.AppendExecution` writes `while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false)) { }` after the rows on every path that returns, for `DatabaseEngine.SqlServer` alone.  A PostgreSQL query is one statement ([SQLSRC230](../../manual/diagnostics.md#sqlsrc230)), so its reader never has a second result and its code is unchanged.  A streaming method drains after its last row; and, by the owner's answer, on an early exit too, Task 3.

**Tech Stack:** C# source generator (`netstandard2.0`, Roslyn 4.8.0), Microsoft.Data.SqlClient 7.1 (6.1 floor), Npgsql 10 (8 floor), xUnit v3, Shouldly, Testcontainers.

**Spec:** [`docs/tech-debt/TD-0085-sql-server-error-after-the-first-result-set-is-lost.md`](../../tech-debt/TD-0085-sql-server-error-after-the-first-result-set-is-lost.md), with [TD-0088](../../tech-debt/TD-0088-sql-server-unreported-error-rolls-back-the-transaction.md) for the cases that flip.

## Where the code differs from the spec's assumptions

- **PostgreSQL has no query of several statements.**  The spec asks for "the same cases on PostgreSQL"; the tool refuses a second statement there.  The PostgreSQL cases are one statement whose error the server raises after the rows the method reads: `(SELECT … ORDER BY id) UNION ALL (SELECT id / 0, … WHERE id = 1)`, whose second branch runs only after the first's rows are sent.  They pin that every shape throws the `PostgresException` `22012`, as Npgsql already reports it; the generated PostgreSQL code does not change.
- **The drain is on the paths that return, not on a throw.**  `one` throwing `UnexpectedRowCountException` (no row, or a second) throws it as today, without draining: the method's own exception is not replaced by a later statement's.  `one-optional` drains before `return null`.  A throw from a row (a null, a converter) is not followed by a drain either.

## Global Constraints

- Generated code compiles as C# 12 at most, every `await` has `.ConfigureAwait(false)`, every framework type from `global::`, the token is `cancellationToken`.
- No new local name: the drain uses the reader's local and the fixed `cancellationToken`, so `LocalNames` and the awkward-name tests are untouched.
- Allocation hand-written methods make the generated method's calls: a change to the calls changes the hand-written method, never `MarginInBytesPerCall`.
- Test names `Method_Scenario_Outcome`, assertions Shouldly, every test class `sealed`.
- Commits pass `./pre-commit-validation.sh`; run `./format.sh` (the repository's formatter) before validating.
- Resolving TD-0085 deletes its file, its row in `docs/tech-debt/README.md` and every `TD-0085` reference outside the history of plans and specs; TD-0088's file, row and known issues are rewritten to what remains.

## Review Focus

1. A batch whose later statement is a `SELECT` that fails (`SELECT CAST('x' AS int)`): the drain must throw, not stop at a result set with no rows — `ListThenFailConversion` and `StreamThenFailConversion` cover it.
2. A later result set with rows (`StreamThenSelect`): the drain skips it silently and returns the rows of the first.
3. `one-optional` with no row and a later error: the method throws the error rather than returning null — `FindNoneThenRaise` covers it.
4. A token cancelled after the last row and before the drain: the drain throws what SqlClient throws for a cancelled token, as `ReadAsync` would — no test; the drain takes the same token as every read.
5. The driver floor, SqlClient 6.1: `SqlServer.SqlClient61` compiles the new code and its tests pass.

---

### Task 1: Buffered SQL Server methods drain after their rows

**Files:**
- Modify: `src/SqlSource/Generation/Emitting/QueryMethodWriter.cs` (`AppendExecution`, a new `AppendDrain`)
- Test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethodWriterTests.cs`
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/Queries/Methods.sql` (+ `FindNoneThenRaise -> one-optional`, sidecar by `tools/describe-end-to-end.sh`), `tests/DatabaseTests/SqlServer/EndToEnd/MethodShapeTests.cs`
- Modify: `tests/DatabaseTests/QueryMethodAllocation/SqlServer/AllocationTests.cs` (each buffered hand-written method)

**Interfaces:**
- Produces: `private static void AppendDrain(StringBuilder builder, string indent, QueryMethod method, Locals locals)`, writing nothing unless `method.Engine == DatabaseEngine.SqlServer`, else `while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))\n{\n}\n` at `indent` with the reader's local name, followed by a blank line where the next line is a `return`.

- [ ] **Step 1: Write the failing writer tests** in `QueryMethodWriterTests`: `Append_SqlServerShapeMany_DrainsTheLaterResultsBeforeItReturns` (the drain line before `return rows.ToArray();`), `Append_SqlServerShapeOne_DrainsAfterTheSecondRead` (after the more-than-one `if`, before `return row;`, and not before the no-row throw), `Append_SqlServerShapeOneOptional_DrainsBeforeEachReturn` (before `return null;` and before `return row;`), and `Append_PostgresShapeMany_DoesNotDrain` (`NextResultAsync` absent).
- [ ] **Step 2: Run them; expect FAIL.**  `dotnet test --project tests/SqlSource.Tests --filter-class "*QueryMethodWriterTests"`
- [ ] **Step 3: Implement `AppendDrain` and call it** in `AppendExecution`: buffered `Many` before its `return`, `OneOptional` inside the no-row block before `return null;`, and `One`/`OneOptional` before `return row;`.
- [ ] **Step 4: Run the writer tests and the generator tests; expect PASS.**  `dotnet test --project tests/SqlSource.Tests` (and `tests/SqlSource.Tests.RoslynFloor`).
- [ ] **Step 5: Flip the end-to-end cases** in `MethodShapeTests`:
  - `ListThenRaise_ErrorAfterTheRows_ThrowsTheError`, `GetThenRaise_ErrorAfterTheRow_ThrowsTheError`, `FindThenRaise_ErrorAfterTheRow_ThrowsTheError`: `SqlException` with `Number` 50000 and `Message` `"after the rows"`, mark 3, 5, 6.
  - New `FindNoneThenRaise_NoRowAndAnErrorAfter_ThrowsTheError`, on a new query `FindNoneThenRaise -> one-optional` (`WHERE id = 0`, `SET CONTEXT_INFO 0x08`, the same `THROW`): mark 8.
  - `ListThenRaise_ErrorAfterTheRowsInATransactionUnderXactAbort_ThrowsAndRollsItBack`: throws 50000, then `session.ShouldHaveRolledBack…` as today.
  - `ListThenFailConversion_ErrorAfterTheRowsInATransaction_ThrowsAndRollsItBack`: throws `Number` 245, rolled back.
  - Comments say what is pinned now; `UnpooledTransaction`'s comment loses "unreported" where it no longer holds (rename `ShouldHaveRolledBackUnreportedAsync` only if every caller is now reported; the streaming break case under `XACT_ABORT ON` may keep it until Task 3).
- [ ] **Step 6: Add `reader.NextResultAsync` loops to the buffered hand-written methods** of the SQL Server allocation tests, at the place the generated method drains, and say so in each method's comment.
- [ ] **Step 7: Run `tests/DatabaseTests/SqlServer` and `tests/DatabaseTests/SqlServer.SqlClient61`; expect PASS.**  Commit.

### Task 2: Streaming SQL Server methods drain after their last row

**Files:**
- Modify: `src/SqlSource/Generation/Emitting/QueryMethodWriter.cs` (streaming branch of `AppendExecution`)
- Test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethodWriterTests.cs`, `tests/DatabaseTests/SqlServer/EndToEnd/StreamingQueryTests.cs`, `tests/DatabaseTests/QueryMethodAllocation/SqlServer/AllocationTests.cs` (`StreamAccounts`' hand-written iterator)

**Interfaces:**
- Consumes: `AppendDrain` from Task 1.

- [ ] **Step 1: Failing writer test** `Append_SqlServerStreams_DrainsAfterTheReadLoop`: the drain follows the `while (ReadAsync)` loop's closing brace, inside the `try`.
- [ ] **Step 2: Run; FAIL.  Step 3: call `AppendDrain` after the loop when `streams`.  Step 4: PASS.**
- [ ] **Step 5: Flip** in `StreamingQueryTests`:
  - `StreamThenRaise_ErrorAfterTheRows_YieldsTheRowsThenThrows` (all three rows yielded, then `SqlException` 50000, mark 2, connection open after).
  - `StreamThenRaise_ErrorAfterTheRows_ThrowsFromTheLastMoveNextAsync` (by hand: three `true`, the fourth `MoveNextAsync` throws 50000, `DisposeAsync` returns normally).
  - `StreamThenRaise_ErrorAfterTheRowsInATransaction_ThrowsAndTheTransactionCommits`, `StreamThenRaise_…UnderXactAbort_ThrowsAndRollsItBack`, `StreamThenFailConversion_…_ThrowsAndRollsItBack` (245), `StreamThenOverflow_…_ThrowsAndTheTransactionCommits` and `…UnderXactAbort_ThrowsAndRollsItBack` (220).
  - `StreamThenSelect_TwoStatements_YieldsTheRowsOfTheFirst` stays as is.
- [ ] **Step 6: Drain in the hand-written `StreamAccounts` iterator** after its loop.
- [ ] **Step 7: Run both SQL Server projects; PASS.**  Commit.

### Task 3: An early exit from a streaming loop drains and throws

The owner chose option 1 on 2026-10-29: after any early exit (a `break`, a body exception, a disposal) the iterator drains the rest of the batch and throws what the database raises, even when that replaces the loop body's exception.

**Files:** `QueryMethodWriter.cs` (streaming branch of `AppendExecution`, a new local name in `Locals`), `QueryMethodWriterTests.cs`, the awkward-name tests that `src/SqlSource/Generation/Emitting/AGENTS.md` names, `StreamingQueryTests.cs`, `Queries/Streaming.sql` (SQL Server), the hand-written `StreamAccounts` iterator.

- [ ] **Step 1: Failing writer test** `Append_SqlServerStreams_DrainsInAFinallyWhenLeftAtARow`: a `bool` local from `LocalNames` (`atRow`) declared `false` before the read loop, set `true` before `yield return` and `false` after it, the loop and the end-of-rows drain inside a `try` whose `finally` drains when it is true.  PostgreSQL's streaming iterator has neither.
- [ ] **Step 2: Implement; run; PASS.**  The local takes its name from `LocalNames` and gets a row in the four awkward-name tests.
- [ ] **Step 3: Flip** `StreamNumbersFailingHalfway_BreakBeforeTheFailingRow_ThrowsTheError` (8134 at the `break`, connection usable after), `…_BreakInATransaction_ThrowsAndTheTransactionCommits`, `…_BreakInATransactionUnderXactAbort_ThrowsAndRollsItBack`, `…_AThrowInTheLoopBody_ThrowsTheServersErrorInItsPlace` (8134 replaces the body's exception).  Add `StreamThenRaise_BreakAtTheFirstRow_ThrowsTheError` (50000).  `StreamNumbers_BreakPartway_*` and `StreamNumbersThenMark_BreakPartway_*` throw nothing still.
- [ ] **Step 4: The hand-written `StreamAccounts` iterator gets the same local and `finally`.**  Run the SQL Server projects and the SQL Server allocation tests; commit.

### Task 4: PostgreSQL cases

**Files:**
- Modify: `tests/DatabaseTests/Postgres/EndToEnd/Queries/Methods.sql`, `Queries/Streaming.sql` (and their sidecars, by `tools/describe-end-to-end.sh`), `MethodShapeTests.cs`, `StreamingQueryTests.cs`

- [ ] **Step 1: Add the queries**, each `(SELECT id, name FROM app.accounts ORDER BY id) UNION ALL (SELECT id / 0, name FROM app.accounts WHERE id = 1)` (or the first account alone for `one` and `one-optional`) under `ListThenRaise`, `GetThenRaise -> one`, `FindThenRaise -> one-optional`, `RunThenRaise -> none`, `CountThenRaise -> rowcount` and the streaming `StreamThenRaise`, with an output model as the SQL Server ones have.
- [ ] **Step 2: Record the sidecars**: `tools/describe-end-to-end.sh` (Docker).
- [ ] **Step 3: Tests**, one per query, each `ShouldThrowAsync<PostgresException>` with `SqlState` `22012`; the streaming one yields the three accounts first.
- [ ] **Step 4: Run `tests/DatabaseTests/Postgres` and `tests/DatabaseTests/Postgres.Npgsql8`; PASS.**  Commit.

### Task 5: Documents and tech debt

- [ ] `docs/manual/sqlserver.md`: the method-results bullet of a query of several statements says an error a later statement raises is thrown by every shape; remove the known issue "An error raised after the first result set is lost"; rewrite the rollback known issue to what remains (per Task 3).
- [ ] `docs/manual/generator.md:571`: the same, in one sentence.
- [ ] `docs/manual/settings.md` streaming rows: the several-statements bullet says the error is thrown when the loop reaches the end of the rows; the early-exit bullet per Task 3.
- [ ] `docs/architecture/generator.md`: step 5 of "A method" names the drain and why only SQL Server; "A method that streams" says where it drains.
- [ ] `tests/DatabaseTests/SqlServer/README.md` and `tests/DatabaseTests/Postgres/README.md`: the rows of `Methods.sql`, `MethodShapeTests` and `StreamingQueryTests`.
- [ ] `tests/DatabaseTests/QueryMethodAllocation/README.md` if a call count or description changes; the comments on the hand-written methods.
- [ ] Delete `TD-0085-…md` and its row; rewrite TD-0088's file and row to the early-exit case alone (or resolve it if Task 3's option 1 covers it); TD-0087's references to the SQL Server behaviour.  `grep -rn TD-0085 .` shows only plan and spec history.
- [ ] Examples: none describe this (checked); no change.
- [ ] Commit.

### Task 6: Measure and validate

- [ ] Run both engines' allocation tests (README commands) and record the generated and hand-written bytes per call, before (measured on `main` at 4297212a) and after, for the PR description.
- [ ] `./format.sh`, `./pre-commit-validation.sh --full`, and the floor images: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` for `tests/DatabaseTests/Postgres`, `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` for `tests/DatabaseTests/SqlServer`.
