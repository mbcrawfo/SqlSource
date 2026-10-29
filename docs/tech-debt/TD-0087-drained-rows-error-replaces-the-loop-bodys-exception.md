# TD-0087: an error in the rows a stream discards replaces the loop body's exception

## Problem

A streaming method's iterator, as [`QueryMethodWriter`](../../src/SqlSource/Generation/Emitting/QueryMethodWriter.cs) writes it, holds its command and reader in `await using` declarations inside a `try` whose `finally` closes a connection it opened.  When the caller's loop ends early, at a `break` or an exception in its body, the `await foreach` disposes the iterator, which disposes the reader, and the reader reads and discards the rows still to come; the command is not cancelled, by the owner's design in the [streaming design](../superpowers/specs/2026-10-26-streaming-query-methods-design.md).  On PostgreSQL, an error that the server raises for one of those rows is thrown by Npgsql's disposal of the reader.  An exception thrown from the disposal in the `finally` of an `await foreach` replaces the one in flight, as C# does for any `finally`, so an exception that the loop's body threw is lost, with nothing of it left on the error that replaces it.  `StreamNumbersFailingHalfway`, in [`Queries/Streaming.sql`](../../tests/DatabaseTests/Postgres/EndToEnd/Queries/Streaming.sql), sorts a million numbers and divides by zero at the row of 500,000, and [`StreamingQueryTests`](../../tests/DatabaseTests/Postgres/EndToEnd/StreamingQueryTests.cs)' `StreamNumbersFailingHalfway_AThrowInTheLoopBody_ThrowsTheServersErrorInItsPlace` pins that an `InvalidOperationException` thrown by the body on the first row reaches the caller as the `PostgresException` `22012`.  Its neighbours pin that a `break` after 10 rows throws the same error and leaves the connection usable, and that the error aborts the caller's transaction, whose next command is refused with `25P02`.  On SQL Server, SqlClient's disposal drops such an error, so the iterator drains the rest of the batch with `NextResultAsync` when its loop ends early, by the owner's choice that the database's error is thrown even in place of the body's: the [same tests](../../tests/DatabaseTests/SqlServer/EndToEnd/StreamingQueryTests.cs) pin that the `break` throws the divide by zero and that it replaces the body's exception there too.

## Why it exists

The owner chose to drain rather than cancel, since a cancel aborts a PostgreSQL transaction at every early exit, and, once the drain was found to raise the error, to keep the database's error rather than hide it: a failure in the rows of a query is real, and on PostgreSQL it aborts the transaction whether or not it is thrown.  The iterator cannot keep the body's exception, since it sees only its own disposal and not why the caller's loop ended.  The manual documents what happens on each engine and the remedy, as a known issue of the settings.

## Impact

Low.  It needs a query whose rows fail beyond the row where the loop stops, such as a division or a cast in a column of a large sorted result, and a body that throws.  The cost is in diagnosis: the caller sees the database's error where its own exception, which may say why the loop stopped, was thrown, and a `catch` of its own exception's type does not run.  Both engines behave alike: PostgreSQL through Npgsql's disposal of the reader, and SQL Server through the drain of its iterator's `finally`.

## Proposed fix

Decide between two ways, each a change to the iterator that `QueryMethodWriter.AppendBody` and `AppendExecution` write, with the allocation pair of `StreamAccounts` in both engines' allocation tests of `tests/DatabaseTests/QueryMethodAllocation` measured before and after.  Either acts only when the iterator ends before its last row, which SQL Server's `atRow` local already tells apart, so a loop that reads to the end still throws the error at its row.

1. Drop the error on an early exit: catch a `DbException` from PostgreSQL's disposal of the reader and from SQL Server's drain in the `finally`, so the `break` throws nothing and the body's exception survives.  The PostgreSQL transaction is still aborted, and a SQL Server one may be rolled back, now with no error to say so, which the manual would then have to say.
2. Keep the error but not in place of the body's exception: since the iterator cannot see that exception, this needs the caller's help, for instance a generated helper that runs the loop's body as a delegate and throws an `AggregateException` of both, which changes how a stream is consumed.

Flip the PostgreSQL and SQL Server tests named above, and rewrite the streaming rows of `docs/manual/settings.md`, its known issue, and the bullets of `docs/manual/postgresql.md` and `docs/architecture/generator.md` that describe the error.

## Trigger

A user's report of a loop's exception lost to a database error, or a change to how a streaming method ends early, such as cancelling the command.
