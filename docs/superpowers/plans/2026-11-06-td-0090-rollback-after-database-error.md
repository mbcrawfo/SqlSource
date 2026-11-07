# TD-0090: report a rollback hidden behind a database error — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A generated SQL Server method that reads rows, given a transaction, throws `TransactionRolledBackException` with the database's error inside when a database error from its reader is followed by a later error of the batch that rolls the transaction back, and throws the database's error as it is otherwise.

**Architecture:** A new capability flag, `ChecksRollbackAfterDatabaseError`, turned on for SQL Server's methods that read rows, writes a `catch (DbException)` around every read of the reader: the rows' reads, the drain before a return, and on a stream the read of each row, the drain after the loop and the drain at an early exit.  The catch reads the rest of the batch itself with `NextResultAsync`, notes whether that raised a further database error, disposes the reader, and wraps only when it did and the transaction's `Connection` is then null.

**Tech Stack:** C# source generator (`src/SqlSource`), Microsoft.Data.SqlClient, xunit v3 + Shouldly, the golden corpus, SQL Server end-to-end tests in Docker.

**Spec:** `docs/tech-debt/TD-0090-sql-server-error-at-a-row-hides-a-rollback.md`, with the owner's answer to td-0090 Q1 (pending when this plan was written; it assumes option 1, and option 2 changes only the throw: see the end).

## Where the code differs from the spec

- The spec's fix, "dispose the reader and, when the transaction's `Connection` is null, throw", cannot tell an error that itself rolled the transaction back from one hidden behind it.  A probe on SQL Server 2025 showed that `Connection` is still set when the read throws, even under `SET XACT_ABORT ON` or for `CAST('x' AS int)` at a row, and null after the disposal in every case.  An explicit drain in the catch tells them apart: it raises a further error only when a later statement raised one.
- The probe also showed that an error thrown by `ExecuteReaderAsync` already carries every error of the batch in `SqlException.Errors` (8134 and 245), with `Connection` null at the throw, so that path hides nothing and is not changed.
- The spec names the rows' reads; the same hole is in every read of the reader (the drains too: rows, `CAST(300 AS tinyint)`, `CAST('x' AS int)` throws 220 from the drain and drops the 245 that rolls back), so every read gets the catch.

## Global Constraints

- The catch applies only when a `transaction` was given: `when (transaction is not null)`.  A call without one is unchanged.
- The drain in the catch passes `global::System.Threading.CancellationToken.None`, as the drain at an early exit does.
- An exception of the drain that is not a `DbException` (a reader closed by a broken connection throws `InvalidOperationException`) stops the drain and is swallowed: the method throws its first error, never the drain's.
- The method's own exceptions keep their catch (`AppendRollbackCheck`) unchanged.
- The message and constructor of `TransactionRolledBackException` do not change: `new global::SqlSource.TransactionRolledBackException("<QueryName>", exception)`.
- Every local name comes from `LocalNames` through `QueryMethodLocals` (`src/SqlSource/Generation/Emitting/QueryMethods/AGENTS.md`); a phase file asks a flag, never the engine.
- Generated text is spelled through `AsyncSpelling` for awaits; indentation through `GeneratedText`.
- Allocation: a hand-written method in `tests/DatabaseTests/QueryMethodAllocation/SqlServer/AllocationTests.cs` makes the same calls as the generated one; the margin never changes.
- Every commit passes `./pre-commit-validation.sh` after `./format.sh`.

## Review Focus

1. A broken connection during a read in a transaction: the drain's `InvalidOperationException` must not replace the database's error.  Pinned by the writer's unit test (the drain's `catch (global::System.Exception)` after its `catch (DbException)`).
2. A database error that itself rolled the transaction back (`SET XACT_ABORT ON`, or `CAST('x' AS int)` at a row) stays a `SqlException`.  Pinned by `ListFailingAtSecondThenFailConversion_InATransactionUnderXactAbort_ThrowsTheDivideByZero` and the existing `ListThenFailConversion`, `StreamThenFailConversion`, `StreamThenRaise`/XACT_ABORT tests, which must pass unchanged.
3. A later error that leaves the transaction open (`CAST(300 AS tinyint)`) after a database error: the database's error is thrown as it is and the transaction commits.  Pinned by `ListFailingAtSecondThenOverflow_InATransaction_ThrowsTheDivideByZeroAndCommits`.
4. No transaction: unchanged.  Pinned by `ListFailingAtSecondThenFailConversion_NoTransaction_ThrowsTheDivideByZero`.
5. A cancellation at a read in a transaction stays the caller's cancellation.  `OperationCanceledException` is not a `DbException`, so the new catch never sees it; the existing cancellation tests must pass unchanged.

---

### Task 1: The flag, and the check around a buffered read

**Files:**
- Modify: `src/SqlSource/Generation/Emitting/QueryMethods/ExecutionCapabilities.cs` (new flag `ChecksRollbackAfterDatabaseError = 16`)
- Modify: `src/SqlSource/Generation/Emitting/QueryMethods/ExecutionCapabilityRules.cs` (SQL Server, `readsRows`, adds the flag; the remark of `ChecksRollbackAtRead` loses its TD-0090 sentence and the new flag gets its own `<para>`)
- Modify: `src/SqlSource/Generation/Emitting/QueryMethods/QueryMethodLocals.cs` (`Exception` taken when either flag is on; new `RaisedLater`, `names.Take("raisedLater")` under the new flag)
- Modify: `src/SqlSource/Generation/Emitting/QueryMethods/ExecutionCapabilityWriter.cs` (new `AppendDatabaseErrorCheck`; the `<exception>` text of `TransactionRolledBackException`)
- Modify: `src/SqlSource/Generation/Emitting/QueryMethods/RowsWriter.cs` (the buffered `try` exists when either flag is on; the new catch is written before `AppendRollbackCheck`)
- Test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/QueryMethodWriterTests.cs`, `ExecutionCapabilityRulesTests.cs`
- Golden: `tests/SqlSource.Tests/Generator/Golden/sqlserver/*.txt` (regenerated)
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/Queries/Methods.sql` (+ its sidecar, by `tools/describe-end-to-end.sh`), `tests/DatabaseTests/SqlServer/EndToEnd/MethodShapeTests.cs`, `tests/DatabaseTests/SqlServer/README.md` (the `Methods.sql` row and the `MethodShapeTests` paragraph)

**Interfaces:**
- Produces: `ExecutionCapabilities.ChecksRollbackAfterDatabaseError`; `QueryMethodLocals.RaisedLater` (string, empty when the flag is off); `ExecutionCapabilityWriter.AppendDatabaseErrorCheck(in QueryMethodContext context, string indent)`, which appends one `catch` clause at `indent`, to follow a `try`'s closing brace.

The catch it writes, for a method `GetUser` at indent `I`:

```csharp
catch (global::System.Data.Common.DbException exception) when (transaction is not null)
{
    var raisedLater = false;
    try
    {
        while (await reader.NextResultAsync(global::System.Threading.CancellationToken.None).ConfigureAwait(false))
        {
        }
    }
    catch (global::System.Data.Common.DbException)
    {
        raisedLater = true;
    }
    catch (global::System.Exception)
    {
    }

    await reader.DisposeAsync().ConfigureAwait(false);
    if (raisedLater && transaction.Connection is null)
    {
        throw new global::SqlSource.TransactionRolledBackException("GetUser", exception);
    }

    throw;
}
```

The buffered method's `try` then has this catch first and `AppendRollbackCheck`'s second.  The new `<exception>` text: "The query threw an exception of its own, or the database raised an error, its inner exception, and an error that the rest of the query raised then rolled back the transaction."

- [ ] **Step 1: Add the queries.**  To `Methods.sql`, after `ListFailingAtSecondThenFailConversion` (whose comment now says the method reports the rollback), add:
  - `ListFailingAtSecondThenOverflow`: `SELECT id, 10 / (id - 2) AS quotient FROM dbo.accounts ORDER BY id; SELECT CAST(300 AS tinyint);`
  - `ListThenOverflowThenFailConversion`: `SELECT id, name FROM dbo.accounts ORDER BY id; SELECT CAST(300 AS tinyint); SELECT CAST('x' AS int);`
  - `GetFailingAtSecondThenFailConversion -> one`: `SELECT TOP (2) id, 10 / (id - 2) AS quotient FROM dbo.accounts ORDER BY id; SELECT CAST('x' AS int);`
  Each with a `-- summary:` in the file's style.  Run `tools/describe-end-to-end.sh` and keep only the changes to `Methods.sql.json` (any other change: stop and report).
- [ ] **Step 2: Write the failing end-to-end tests** in `MethodShapeTests.cs` (constants `Overflow = 220` beside the others), each on `UnpooledTransaction`:
  - Rename and flip `ListFailingAtSecondThenFailConversion_InATransaction_ThrowsTheDivideByZeroAndRollsBack` to `..._InATransaction_ThrowsTheRollbackWithTheDivideByZeroInside`: `ShouldThrowAsync<TransactionRolledBackException>`, `QueryName` is the query's, `InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DivideByZero)`, `ShouldHaveRolledBackAsync`.  Its comment says what is observed and no longer names TD-0090.
  - `ListFailingAtSecondThenFailConversion_InATransactionUnderXactAbort_ThrowsTheDivideByZero`: `xactAbort: true`, `SqlException` of `Number` 8134, `ShouldHaveRolledBackAsync`.
  - `ListFailingAtSecondThenFailConversion_NoTransaction_ThrowsTheDivideByZero`: `EndToEndSources.App`, `SqlException` 8134.
  - `ListFailingAtSecondThenOverflow_InATransaction_ThrowsTheDivideByZeroAndCommits`: `SqlException` 8134, `ShouldCommitAsync`.
  - `ListThenOverflowThenFailConversion_InATransaction_ThrowsTheRollbackWithTheOverflowInside`: `TransactionRolledBackException`, inner `SqlException` of `Number` 220, `ShouldHaveRolledBackAsync`.
  - `GetFailingAtSecondThenFailConversion_InATransaction_ThrowsTheRollbackWithTheDivideByZeroInside`: as the first, for `one`.
- [ ] **Step 3: Run them and see the three that expect `TransactionRolledBackException` fail** (they get `SqlException`): `dotnet test --project tests/DatabaseTests/SqlServer --filter-class '*MethodShapeTests'`.
- [ ] **Step 4: Write the writer's unit tests** in `QueryMethodWriterTests.cs`: a helper `DatabaseErrorCheck(string indent, string exception = "exception", string raisedLater = "raisedLater")` that spells the catch above; `Append_CheckingRollbackAfterDatabaseErrorAlone_CatchesTheDatabasesErrorAroundTheRows` (`WriteWith(GetUser, ExecutionCapabilities.ChecksRollbackAfterDatabaseError)` contains `"}\n" + DatabaseErrorCheck(...)`, no `is not (global::System.Data.Common.DbException`, braces balance); the existing SQL Server buffered expectations gain the catch before `TransactionCheck`; a token named `raisedLater` renames the local (a row in `Run_TokenWithAnAwkwardName_Compiles`, `Run_TokenWithAnAwkwardNameInAQueryMethod_Compiles` and their `_CompilesOnTheNewestCSharp` siblings, as `AGENTS.md` says).  In `ExecutionCapabilityRulesTests.cs`, SQL Server's `many`, `one` and `one-optional` have the flag, `none`, `rowcount` and PostgreSQL do not.
- [ ] **Step 5: Implement** the flag, the rule, the locals, `AppendDatabaseErrorCheck` and the `RowsWriter` change.
- [ ] **Step 6: Regenerate the goldens** with `SQLSOURCE_UPDATE_GOLDEN=1 dotnet test --project tests/SqlSource.Tests` (never on the solution), read the diff: only SQL Server's buffered methods gain the catch and the `<exception>` text changes.
- [ ] **Step 7: Run** `dotnet test --project tests/SqlSource.Tests` and `dotnet test --project tests/DatabaseTests/SqlServer --filter-class '*MethodShapeTests'`: all pass.
- [ ] **Step 8: Update `tests/DatabaseTests/SqlServer/README.md`**: the `Methods.sql` row names the three new queries; the `MethodShapeTests` sentences of the paragraph at the `StreamingQueryTests` bullet say what the six tests hold and drop the TD-0090 link.
- [ ] **Step 9: Commit** after `./format.sh` and `./pre-commit-validation.sh`: `fix(generator): report a rollback hidden behind a database error of a buffered read (TD-0090)`.

### Task 2: The check around a stream's reads

**Files:**
- Modify: `src/SqlSource/Generation/Emitting/QueryMethods/RowsWriter.cs` (`AppendStream`), `ExecutionCapabilityWriter.cs` (`AppendDrainAfterLoop`, `AppendLeftAtARowEnd`), `QueryMethodLocals.cs` (new `HasRow`, `names.Take("hasRow")`, when the method streams and has the flag)
- Test: `QueryMethodWriterTests.cs`; goldens; `tests/DatabaseTests/SqlServer/EndToEnd/Queries/Streaming.sql` (+ sidecar), `StreamingQueryTests.cs`, `tests/DatabaseTests/SqlServer/README.md`

**Interfaces:**
- Consumes: `AppendDatabaseErrorCheck`, `QueryMethodLocals.RaisedLater`, `ChecksRollbackAfterDatabaseError` (Task 1).
- Produces: `QueryMethodLocals.HasRow`.

Under the flag, a stream's loop reads its row in a `try` of its own, as C# allows no `catch` on a `try` that holds a `yield return`:

```csharp
while (true)
{
    bool hasRow;
    try
    {
        hasRow = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }
    <AppendDatabaseErrorCheck>

    if (!hasRow)
    {
        break;
    }

    <the row into its local, as today, then the yield>
}
```

The drain after the loop and the drain in the `finally` at a row are each wrapped in `try { <drain> }` followed by `AppendDatabaseErrorCheck`.  Without the flag the stream is written as today.

- [ ] **Step 1: Add the queries** to `Streaming.sql`: `StreamFailingAtSecondThenFailConversion` (the batch of `ListFailingAtSecondThenFailConversion`, `-> many` streamed as the file's others are) and `StreamThenOverflowThenFailConversion` (the batch of `ListThenOverflowThenFailConversion`); describe as in Task 1.
- [ ] **Step 2: Write the failing tests** in `StreamingQueryTests.cs`, on `UnpooledTransaction` with `xactAbort: false`:
  - `StreamFailingAtSecondThenFailConversion_InATransaction_YieldsTheFirstRowThenThrowsTheRollback`: one row yielded, then `TransactionRolledBackException`, inner `SqlException` 8134, `ShouldHaveRolledBackAsync`.
  - `StreamFailingAtSecondThenFailConversion_BreakAtTheFirstRowInATransaction_ThrowsTheRollback`: `break` at the first row; the `await foreach` throws `TransactionRolledBackException`, inner 8134, rolled back.
  - `StreamThenOverflowThenFailConversion_InATransaction_ThrowsTheRollbackAfterTheRows`: every row yielded, then `TransactionRolledBackException`, inner `SqlException` 220, rolled back.
  Run: `dotnet test --project tests/DatabaseTests/SqlServer --filter-class '*StreamingQueryTests'`; expected: the three fail with `SqlException`.
- [ ] **Step 3: Unit tests**: `Append_StreamsCheckingRollbackAfterDatabaseErrorAlone_ReadsEachRowInATry` (the loop above with `DatabaseErrorCheck`, no `atRow`, braces balance); the SQL Server stream's expectations (and `Append_EveryCapability_IsTheSqlServerMethod`'s `all`) gain the flag; a token named `hasRow` renames the local (the four rows of Task 1's tests).
- [ ] **Step 4: Implement**, regenerate the goldens, read their diff (only SQL Server streams change).
- [ ] **Step 5: Run** `tests/SqlSource.Tests` and `StreamingQueryTests`: all pass, the existing ones unchanged.
- [ ] **Step 6: README**: `Streaming.sql`'s row and the `StreamingQueryTests` sentences name the new queries and tests.
- [ ] **Step 7: Commit**: `fix(generator): report a rollback hidden behind a database error of a stream's read (TD-0090)`.

### Task 3: The allocation tests' hand-written methods

**Files:**
- Modify: `tests/DatabaseTests/QueryMethodAllocation/SqlServer/AllocationTests.cs`

- [ ] **Step 1:** Give every hand-written SQL Server method that has the rollback catch the same `catch (DbException exception) when (transaction is not null)` before it, and `StreamAccountsByHand` the read in its own `try` and the two wrapped drains, as the generated methods have, with a comment in the file's style ("The generated method's check after a database error.").
- [ ] **Step 2: Measure**: `dotnet test --project tests/DatabaseTests/QueryMethodAllocation -p:RunQueryMethodAllocation=true --filter-namespace SqlSource.DatabaseTests.QueryMethodAllocation.SqlServer --output Detailed`.  Expected: 32 pass.  Report each test's commonest bytes of a generated call against the baseline below.
- [ ] **Step 3: Commit**: `test(allocation): the hand-written SQL Server methods check after a database error`.

Baseline before the change (commonest bytes of a generated call, equal to the hand-written one unless said): `SearchAccounts` 6816 open / 7200 closed; `SearchAccountsUnpooled` 6896 / 7280; `StreamAccounts` 5216 / 5600; `GetNodaValues` 7496 / 7880; `NumbersAsList` 6040 / 6424; `NumbersAsIList` 6040 / 6424; `NumbersAsImmutableArray` 6008 / 6392; `NumbersNoPoolAsICollection` 6040 / 6424; at 300 rows `NumbersAsList` 15544 / 15928 against 21464 / 21848, `NumbersAsImmutableArray` 15512 / 15896 against 23832 / 24216.

### Task 4: The docs, and resolving TD-0090

**Files:**
- Modify: `docs/manual/sqlserver.md` (the `TransactionRolledBackException` bullet of "What a method reads and throws": a database error from a read followed by a later error that rolls back is reported too, and an error that itself rolls back is thrown as it is; remove the known issue "A transaction can be rolled back by an error hidden behind an error at a row." and nothing else of the section)
- Modify: `docs/manual/generator.md` (the `TransactionRolledBackException` section: its `InnerException` can be the database's error; line 649's sentence)
- Modify: `docs/manual/settings.md` (the streamed-rows item on a row's error: the database's error at a row, followed by an error that rolls back, is `TransactionRolledBackException` too)
- Modify: `docs/architecture/generator.md` (step 6 of the query method: the new catch, how it tells the cases apart, and why `ExecuteReaderAsync`'s errors need none; drop the TD-0090 sentence; the streaming paragraph)
- Delete: `docs/tech-debt/TD-0090-sql-server-error-at-a-row-hides-a-rollback.md`; remove its row from `docs/tech-debt/README.md`
- Check: `grep -rn "TD-0090" . --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj` finds only `docs/superpowers/plans/`.

- [ ] **Step 1:** Edit the docs.  The manual says effects, never internals: what a caller catches and what `InnerException` holds.
- [ ] **Step 2:** `./format.sh`, `./pre-commit-validation.sh` (the documentation link tests run).
- [ ] **Step 3: Commit**: `docs: a rollback behind a database error is reported (resolves TD-0090)`.

## If the owner chooses option 2

Only the throw differs: in `AppendDatabaseErrorCheck`, when `raisedLater && transaction.Connection is null`, write `exception.Data["SqlSource.TransactionRolledBack"] = "<QueryName>";` and fall through to `throw;`.  The tests then expect `SqlException` and assert the `Data` entry; the docs describe the entry in place of the exception; the `<exception>` text of `TransactionRolledBackException` keeps today's words.
