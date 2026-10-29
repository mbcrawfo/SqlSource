# TD-0088: on SQL Server, an error after a method's own exception can roll back the caller's transaction unannounced

## Problem

A generated SQL Server method that reads rows, as [`QueryMethodWriter`](../../src/SqlSource/Generation/Emitting/QueryMethodWriter.cs) writes it, reads the rest of its batch with `NextResultAsync` before it returns, and a streaming iterator does so after its last row and when its loop ends early, so an error that a later statement or an unread row raises is thrown.  A method that throws an exception of its own before it has read the rest of the batch does not drain it: `one`'s `UnexpectedRowCountException` for no row or a second, a null in a column whose property cannot be null, a converter's exception, at a row of a buffered method or of a streaming loop.  The reader's disposal then reads the rest of the batch, and SqlClient drops an error that it raises there.  Such an error can roll back the transaction that the method was given, depending on the error and on `XACT_ABORT`: under `SET XACT_ABORT ON` nearly every error does, and with it off some do, such as `CAST('x' AS int)`, and others do not, such as a `THROW`, a divide by zero or `CAST(300 AS tinyint)`.  A rolled-back `SqlTransaction` is completed: its `Connection` is null after the method, a later command given it runs in autocommit, outside any transaction, with no error, and only `Commit` throws `InvalidOperationException`, "This SqlTransaction has completed; it is no longer usable.".  The caller gets the method's exception, which says nothing of the rollback, so a caller that catches it and goes on with the transaction loses what the transaction did before and commits what it does after statement by statement.

Observed by a branch review's probes, through SqlClient 7.1.1 directly and through a generated method, of an error after the rows of a batch, which ran `SELECT 1; SELECT <expr>;` or the rows of a query and then the statement, or in the rows that a streaming loop left unread.  "Both" is SQL Server 2025-CU9-GDR1 and the 2017-CU31-GDR19 floor, and "2025" is 2025-CU9-GDR1 alone; "not probed" is a case that was run with `XACT_ABORT` off only.  These are observations, not a rule, and deadlocks and errors of severity 20 and above were not tried.

| Error | Where | `XACT_ABORT` off | `XACT_ABORT ON` | Images |
|---|---|---|---|---|
| Divide by zero (8134), overflow (8115) | Unread rows | Open | Rolled back | Both |
| `CAST('x' AS int)` (245) | Unread rows | Rolled back | Rolled back | Both |
| `THROW`, divide by zero | After the rows | Open | Rolled back | Both |
| `RAISERROR` at severity 16 | After the rows | Open | Open | Both |
| `CAST('x' AS int)` (245), buffered and streaming | After the rows | Rolled back | Rolled back | Both |
| `'x'` cast to `bigint`, `decimal`, `float`, `datetime`, `date`, `datetime2`, `uniqueidentifier` or `xml`, and `CAST(N'x' AS int)` | After the rows | Rolled back | Not probed | Both |
| `CAST(300 AS tinyint)`, `CAST(CAST(99999999999 AS bigint) AS int)`, `CAST(1000 AS decimal(3,0))`, `CAST('2020-13-45' AS datetime)` (242) | After the rows | Open | Rolled back | Both |
| `CAST('99999999999' AS int)`, `CAST('1e500' AS float)`, `CAST('2020-02-30' AS datetime)` | After the rows | Open | Not probed | 2025 |
| `CAST('abc' AS int)`, `CAST('2020-02-30' AS date)`, `CAST('2020-13-45' AS date)`, `CAST('12:99' AS time)` | After the rows | Rolled back | Not probed | 2025 |

No test pins the residual case.  [`UnpooledTransaction`](../../tests/DatabaseTests/SqlServer/EndToEnd/UnpooledTransaction.cs) holds the rolled-back state for the cases that now throw: [`MethodShapeTests`](../../tests/DatabaseTests/SqlServer/EndToEnd/MethodShapeTests.cs)' `ListThenRaise` and `ListThenFailConversion`, and [`StreamingQueryTests`](../../tests/DatabaseTests/SqlServer/EndToEnd/StreamingQueryTests.cs)' `StreamThenRaise`, `StreamThenFailConversion`, `StreamThenOverflow` and `StreamNumbersFailingHalfway` at a `break`, each under the setting that rolls it back.

## Why it exists

A branch review found the rollback with nothing thrown, after the errors themselves were recorded as lost.  Draining the rest of the batch after the rows, and at a streaming loop's early exit, made those errors thrown; the owner chose that a method's own exception is thrown as it is, without draining first, so that a later statement's error does not replace it, which leaves this case.

## Impact

Low.  It needs a transaction given to the method, an exception of the method's own, from a wrong count of rows, a bad row or a converter, and a later statement or unread row of the same batch that raises an error that rolls the transaction back, and a caller that catches the method's exception and goes on with the transaction.  When it happens, it is a silent loss of data until the commit, and the commit's `InvalidOperationException` says nothing of the error that caused it.  The manual documents it, with its remedy, to check the transaction's `Connection` after catching, as a known issue of `docs/manual/sqlserver.md` and `docs/manual/settings.md`.

## Proposed fix

Detect the rollback where a SQL Server method throws its own exception: once the reader is disposed, when a transaction was given and its `Connection` is null, throw an exception that says the transaction was rolled back by an error the query raised, with the method's exception as its inner exception.  A buffered method needs a `try`/`catch` around its read whose `catch` disposes the reader first; a streaming method checks in the `finally` around its read loop that already drains at an early exit, when the iterator's own code threw, and, by the owner's rule that the database's error is thrown even in place of another, the exception may replace the one in flight.  The check costs one property read and allocates nothing on a call that does not throw, which both engines' allocation tests of `tests/DatabaseTests/QueryMethodAllocation` should confirm.  Add the end-to-end cases, `one` with a second row and a later `CAST('x' AS int)` among them, and rewrite the known issues of `docs/manual/sqlserver.md` and `docs/manual/settings.md`.

## Trigger

A user's report of a transaction rolled back with no word of it, or a change to how a method throws its own exceptions.
