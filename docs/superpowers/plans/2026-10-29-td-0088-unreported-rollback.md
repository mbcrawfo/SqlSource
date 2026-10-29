# TD-0088 Unreported Rollback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A SQL Server method that reads rows and throws an exception of its own, inside a transaction that an error of the rest of its batch then rolls back, throws an exception that says the transaction was rolled back, with its own exception inside.

**Architecture:** `QueryMethodWriter` wraps the read of a SQL Server method that reads rows in a `try` whose `catch`, filtered to a given transaction and to an exception that is neither a `DbException` nor an `OperationCanceledException`, disposes the reader, reads the transaction's `Connection`, and throws `TransactionRolledBackException` when it is null, or rethrows.  A buffered method's `try` covers its whole read; a streaming iterator's covers the building of each row, since C# allows no `catch` around a `yield return`.  The exception is a class of `AttributeSource.Text`, beside `UnexpectedRowCountException`.

**Tech Stack:** C# source generator (Roslyn), Microsoft.Data.SqlClient, xUnit v3 with Shouldly, Testcontainers.

**Spec:** [`docs/tech-debt/TD-0088-sql-server-unreported-error-rolls-back-the-transaction.md`](../../tech-debt/TD-0088-sql-server-unreported-error-rolls-back-the-transaction.md), as td-0085 left it.

## Where the code differs from the spec

- **The streaming check is a `catch` around the building of each row, not a check in the `finally`.**  A `finally` cannot see the exception in flight, which the Proposed fix makes the inner exception, and C# forbids a `catch` on a `try` that holds a `yield return`.  The `finally` that drains at an early exit is unchanged: after a break or a body exception the drain throws what the rest of the batch raises, so nothing there goes unreported.  By the owner's rule (always throw), the new exception replaces the iterator's own exception in flight, keeping it as its inner exception.
- **"The method's own exception" is every exception of the read but a `DbException` and an `OperationCanceledException`.**  A converter's exception can be of any type, so the filter excludes what is not the method's own: the database's or driver's error, which already reports an error, and the caller's cancellation.  The owner chose this (Q2, "(1)"): a database error at a row followed by a dropped error is out of this change, and Task 2's probe decides whether it becomes a new tech-debt item in this PR.
- **The exception's type is a new generated `TransactionRolledBackException : InvalidOperationException`.**  The owner chose it (Q1, "(a)") over a plain `InvalidOperationException`.

## Global Constraints

- `AttributeSource.Text` compiles as C# 7.3: no `#nullable`, no `string?`, nothing newer; a type added there is added to `AttributeSource.GeneratedTypes`.
- Generated method code may use C# 12 and nothing newer, writes framework types from `global::`, and takes every local's name from `LocalNames` (`Locals` in `QueryMethodWriter`); a new local gets a row in the four awkward-name tests (`Run_TokenWithAnAwkwardName_Compiles`/`…OnTheNewestCSharp`, `Run_TokenWithAnAwkwardNameInAQueryMethod_Compiles`/`…OnTheNewestCSharp`).
- A compiler warning inside generated code is a bug.
- PostgreSQL's generated code does not change.
- SQL Server sidecars are written by `tools/describe-end-to-end.sh` alone.
- A change to the calls a generated method makes changes the hand-written method of `tests/DatabaseTests/QueryMethodAllocation/SqlServer/AllocationTests.cs` with it, never the margin.

## Review Focus

1. A method given no transaction throws its own exception exactly as before (no wrapping, no disposal in a catch).  Pinned by Task 2's `…WithoutATransaction…` case.
2. An error that leaves the transaction open (XACT_ABORT off, `THROW`) leaves the method's own exception as it is, and the transaction commits.  Pinned by Task 2's `GetTwoThenRaise` case.
3. A `SqlException` thrown by a read or the drain is never wrapped, even when the transaction is gone.  Pinned by the existing `ListThenFailConversion`/`StreamThenFailConversion` tests, which must still expect `SqlException`.
4. A cancellation is never wrapped.  Pinned by the existing `ListAccountIdsCancellingOnRead` and `StreamNumbers_*_Cancels` tests, which run without a transaction; Task 1's writer test pins the filter text.
5. A token named `exception` does not hide the catch's variable.  Pinned by Task 1's awkward-name rows.

---

### Task 1: The exception and the generated check

**Files:**
- Modify: `src/SqlSource/Generation/AttributeSource.cs` (new metadata name constant, `GeneratedTypes`, the class in `Text`)
- Modify: `src/SqlSource/Generation/Emitting/QueryMethodWriter.cs`
- Test: `tests/SqlSource.Tests/Generation/AttributeSourceTests.cs`, `tests/SqlSource.Tests/Generation/Emitting/QueryMethodWriterTests.cs`, `tests/SqlSource.Tests/Generator/GeneratedSourceMethodTests.cs`, `tests/SqlSource.Tests/Generation/Emitting/MethodWriterTests.cs`

**Interfaces:**
- Produces: `AttributeSource.TransactionRolledBackMetadataName = "SqlSource.TransactionRolledBackException"`; generated `internal sealed class SqlSource.TransactionRolledBackException : global::System.InvalidOperationException` with `public TransactionRolledBackException(string queryName, global::System.Exception innerException)` and `public string QueryName { get; }`; message exactly `"The transaction was rolled back by an error that the query '" + queryName + "' raised after the inner exception was thrown."`.

- [ ] **Step 1: Failing tests**
  - `AttributeSourceTests`: `Text_CompiledAsCSharp73_DeclaresTransactionRolledBackException` (as the `UnexpectedRowCountException` one), and `TransactionRolledBackException_Created_NamesTheQueryAndKeepsTheInnerException`: message as above for query `"GetUser"`, `InnerException` is the given one, `QueryName == "GetUser"`, assignable to `InvalidOperationException`.  `GeneratedTypes` contains the new name (extend any test that lists it).
  - `QueryMethodWriterTests`, for `SqlServerGetUser` with each of `Many`, `One`, `OneOptional`: the read, from the line after `await using var readerScope = …` through its `return`, is inside `try { … }` at one more indent, followed by exactly this catch (at the method body's indent, `"        "` for a buffered method):

    ```
    catch (global::System.Exception exception) when (transaction is not null && exception is not (global::System.Data.Common.DbException or global::System.OperationCanceledException))
    {
        await reader.DisposeAsync().ConfigureAwait(false);
        if (transaction.Connection is null)
        {
            throw new global::SqlSource.TransactionRolledBackException("GetUser", exception);
        }

        throw;
    }
    ```
    Update the existing SQL Server drain tests' expected text to the new indent.
  - `Append_SqlServerStreams_ChecksTheTransactionWhenARowFails`: `SqlServerStream with { Output = VectorOutput(isNullable: false) }` contains, inside the `while`, `global::App.GetUserDto row;`, then `try` holding the vector check and `row = new global::App.GetUserDto(…);`, then the same catch, a blank line, `atRow = true;`.  Update `Append_SqlServerStreams_DrainsInAFinallyWhenLeftAtARow` to match.
  - `Append_PostgresShapeReadingRows_DoesNotCheckTheTransaction` (theory over `Many`, `One`, `OneOptional`, plus `Stream`): no `TransactionRolledBackException`, no `catch (`.  `None`/`RowCount` on SQL Server: none either.
  - Each SQL Server shape that reads rows documents `<exception cref="global::SqlSource.TransactionRolledBackException">` with the text "The query threw an exception of its own, its inner exception, and an error that the rest of the query raised rolled back the transaction."; PostgreSQL's methods do not.
  - Awkward-name rows `"exception"` in the four awkward-name tests, with the check that it becomes `exception1`, as `atRow` does.

- [ ] **Step 2: Run the tests, see them fail.**  `dotnet test --project tests/SqlSource.Tests --filter-class "*QueryMethodWriterTests" ` (and the other three classes).

- [ ] **Step 3: Implement.**
  - `Locals` takes `Exception = names.Take("exception")` when the method checks the transaction: `ChecksTransaction(method) => method.Engine == DatabaseEngine.SqlServer && method.Shape is ResultShape.Many or ResultShape.One or ResultShape.OneOptional`.
  - `AppendTransactionCheck(builder, indent, method, locals)` writes the catch above.  `AppendExecution` writes, for a buffered SQL Server method, `try {` after the reader's scope, the existing read one indent deeper, `}` and the catch.  `AppendStream`, when `tracksRow`, declares `<row type> <row>;`, wraps the vector checks and `row = <AppendRow>;` in `try`/catch, then a blank line and the `atRow` lines.
  - The class and its doc comment in `AttributeSource.Text`, in C# 7.3, after `UnexpectedRowCountException`; the summary says what throws it and that it derives from `InvalidOperationException` so a caller in another assembly catches that.
  - Update the comments above `AppendStream` and `AppendDrain` to say what the catch does.

- [ ] **Step 4: Run `dotnet test --project tests/SqlSource.Tests`; all pass, no warnings in generated code.**

- [ ] **Step 5: Commit** `feat: throw when a SQL Server transaction was rolled back after a method's own exception`.

### Task 2: End-to-end cases and allocations

**Files:**
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/Queries/Methods.sql`, `Queries/Streaming.sql` and their sidecars (by `tools/describe-end-to-end.sh` only)
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/MethodShapeTests.cs`, `StreamingQueryTests.cs`
- Modify: `tests/DatabaseTests/QueryMethodAllocation/SqlServer/AllocationTests.cs` (every hand-written SQL Server method that reads rows gets the same `try`/catch), `tests/DatabaseTests/QueryMethodAllocation/README.md` (the sentence on what the hand-written methods make)

**Interfaces:**
- Consumes: `SqlSource.TransactionRolledBackException` (Task 1); `UnpooledTransaction.BeginAsync(sqlServer, xactAbort, token)`, `ShouldHaveRolledBackAsync`, `ShouldCommitAsync`.

- [ ] **Step 1: Queries.**  In `Methods.sql`, each with `-- output-model: CollectedAccount` where its columns are `id, name`, and a summary in the file's style:
  - `GetTwoThenFailConversion -> one`: `SELECT id, name FROM dbo.accounts WHERE id <= 2 ORDER BY id; SELECT CAST('x' AS int);`
  - `GetTwoThenRaise -> one`: the same rows, then `THROW 50000, 'after the rows', 1;`
  - `ListNullAtThirdThenFailConversion` (many, default collection): `SELECT id, CASE WHEN id = 3 THEN NULL ELSE name END AS name FROM dbo.accounts ORDER BY id; SELECT CAST('x' AS int);` with `-- column: name not null`
  - `ListFailingAtSecondThenFailConversion` (many): `SELECT id, 10 / (id - 2) AS quotient FROM dbo.accounts ORDER BY id; SELECT CAST('x' AS int);` — the probe for owner question Q2.

  In `Streaming.sql`: `StreamNullAtThirdThenFailConversion`, as `ListNullAtThirdThenFailConversion`.  Run `tools/describe-end-to-end.sh` (it writes every sidecar; commit only the changes it makes).

- [ ] **Step 2: Tests**, each in the files' style with a comment saying what it pins:
  - `GetTwoThenFailConversion_SecondRowInATransaction_ThrowsTheRollbackWithTheRowCountInside`: XACT_ABORT off; `TransactionRolledBackException` whose `InnerException` is `UnexpectedRowCountException` and `QueryName == "GetTwoThenFailConversion"`; `ShouldHaveRolledBackAsync`.
  - `GetTwoThenFailConversion_SecondRowWithoutATransaction_ThrowsTheRowCount`: `UnexpectedRowCountException` itself.
  - `GetTwoThenRaise_SecondRowInATransaction_ThrowsTheRowCountAndCommits`: XACT_ABORT off; `UnexpectedRowCountException`; `ShouldCommitAsync`.
  - `GetTwoThenRaise_SecondRowInATransactionUnderXactAbort_ThrowsTheRollback`: XACT_ABORT ON; `TransactionRolledBackException` (inner `UnexpectedRowCountException`); `ShouldHaveRolledBackAsync`.
  - `ListNullAtThirdThenFailConversion_InATransaction_ThrowsTheRollbackWithTheNullInside`: inner `SqlNullValueException`; rolled back.
  - `StreamNullAtThirdThenFailConversion_InATransaction_ThrowsTheRollbackAfterTwoRows`: rows 1 and 2 yielded, then `TransactionRolledBackException` with inner `SqlNullValueException`; rolled back.
  - `ListFailingAtSecondThenFailConversion_InATransaction_…`: pin what is observed (expected: `SqlException` 8134, transaction rolled back), and report it.

- [ ] **Step 3: Run** `dotnet test --project tests/DatabaseTests/SqlServer` on the default image and on `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`; all pass.

- [ ] **Step 4: Allocations.**  Mirror the catch in every SQL Server hand-written method that reads rows; run both engines' allocation tests (commands in `tests/DatabaseTests/QueryMethodAllocation/README.md`) and report each test's generated and hand-written bytes, to compare with the baseline taken before Task 1.

- [ ] **Step 5: Commit** `test: pin the rolled-back transaction after a SQL Server method's own exception`.

### Task 3: Documentation and resolving TD-0088

**Files:**
- Modify: `docs/manual/sqlserver.md` (the method-results bullet "An error that the method does not throw…" becomes what the method throws; the known issue and its sample go), `docs/manual/settings.md` (the known issue "On SQL Server, a loop over streamed rows that throws for a row…" goes; the streaming bullets say what is thrown), `docs/manual/generator.md` (a section for `TransactionRolledBackException` beside `UnexpectedRowCountException`, and its cross-assembly known issue covers both), `docs/manual/README.md` (the table row names it), `README.md:221` (each project has its own copy of both exceptions), `docs/architecture/generator.md` (steps 5, the streaming paragraph, "The exception and the drivers"), `tests/DatabaseTests/SqlServer/README.md` (the `Methods.sql` and streaming rows)
- Delete: `docs/tech-debt/TD-0088-sql-server-unreported-error-rolls-back-the-transaction.md`; its row in `docs/tech-debt/README.md`

- [ ] **Step 1: Edit the pages.**  User pages describe the effect: when a SQL Server method throws an exception of its own in a transaction, and an error the rest of the batch raised has rolled the transaction back, the method throws `TransactionRolledBackException` with its exception as `InnerException`; without a transaction, or with it still open, the method's exception is thrown as it is.  No id, no internals.
- [ ] **Step 2: `git grep -n "TD-0088"`** shows only plan and spec history; `git grep -n -i "does not throw"` in `docs/manual` shows no leftover of the known issues.
- [ ] **Step 3: Run `./pre-commit-validation.sh`; it passes.**
- [ ] **Step 4: Commit** `docs: resolve TD-0088`.
