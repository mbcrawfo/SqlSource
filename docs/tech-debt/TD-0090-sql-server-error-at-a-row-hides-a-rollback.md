# TD-0090: on SQL Server, a database error at a row can hide a later error that rolls back the transaction

## Problem

A generated SQL Server method that reads rows, as [`QueryMethodWriter`](../../src/SqlSource/Generation/Emitting/QueryMethodWriter.cs) writes it, checks the transaction it was given only when it throws an exception of its own: the `catch` that `AppendTransactionCheck` writes is filtered to every exception but a `DbException` and an `OperationCanceledException`.  When the read throws the database's error at a row, a `SqlException` such as 8134, a divide by zero, at the second row, with `XACT_ABORT` off so that the error alone leaves the transaction open, the method throws it as it is.  The reader's disposal then reads the rest of the batch, and SqlClient drops an error there that rolls the transaction back, such as a later `SELECT CAST('x' AS int);`.  The caller gets the `SqlException` 8134 with no inner exception, and nothing says the transaction is gone: its `Connection` is null, and `Commit` throws "This SqlTransaction has completed; it is no longer usable.".  A later generated SQL Server method given it refuses it with `InvalidOperationException` before it runs anything, the check that `AppendCompletedTransactionCheck` writes, but a later command of the caller's own, or one that Dapper runs, given it runs outside any transaction and commits on its own.  The same query without the later `CAST` leaves `Connection` set.

It applies to buffered and streaming methods alike: a streaming read that throws is not at a row, so the iterator's `finally` does not drain, and its per-row `catch` is around the row's checks and construction, not the read.

Observed by a probe on SQL Server 2025-CU9-GDR1 and the 2017-CU31-GDR19 floor.  [`MethodShapeTests`](../../tests/DatabaseTests/SqlServer/EndToEnd/MethodShapeTests.cs)' `ListFailingAtSecondThenFailConversion_InATransaction_ThrowsTheDivideByZeroAndRollsBack`, with `ListFailingAtSecondThenFailConversion` of [`Queries/Methods.sql`](../../tests/DatabaseTests/SqlServer/EndToEnd/Queries/Methods.sql), pins it.

## Why it exists

The owner chose that the rolled-back-transaction check wraps only the method's own exceptions.  Wrapping a `DbException` would change the type that every caller catches for a SQL error in a transaction that the error itself rolled back, for example a duplicate key under `SET XACT_ABORT ON`, and the database's error already reports that something failed.

## Impact

Low.  It needs a transaction given to the method, an error at a row that leaves it open, a later statement or row of the same batch whose error rolls it back, and a caller that catches the first error and goes on with the transaction.  When it happens, what the transaction did before is lost, what the caller then runs through its own commands or Dapper is committed statement by statement, and the `InvalidOperationException` of the commit, or of the next generated method, says nothing of the error that caused it.  The manual documents it, with its remedy, to check the transaction's `Connection` after catching, as a known issue of `docs/manual/sqlserver.md`.

## Proposed fix

After a `DbException` from a read, with a transaction given, dispose the reader and, when the transaction's `Connection` is null, throw `TransactionRolledBackException` with the database's error as its inner exception, or report the rollback another way the owner chooses.  Measure the allocation tests of `tests/DatabaseTests/QueryMethodAllocation` before and after.  Flip the test named above, and remove the known issue of `docs/manual/sqlserver.md`.

## Trigger

A user's report of a transaction rolled back after a database error with no word of it, or a change to which exceptions the check wraps.
