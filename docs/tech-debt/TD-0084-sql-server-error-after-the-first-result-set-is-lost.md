# TD-0084: on SQL Server, an error after the first result set is lost

## Problem

A generated method that reads rows reads the first result set of its query and disposes its reader, as [`QueryMethodWriter`](../../src/SqlSource/Generation/Emitting/QueryMethodWriter.cs) writes it, and calls no `NextResultAsync`.  On SQL Server, an error that a statement after that result set raises is then reported by no call.  `StreamThenRaise`, in [`Queries/Streaming.sql`](../../tests/SqlSource.Tests.SqlServer/EndToEnd/Queries/Streaming.sql), selects the accounts, sets `CONTEXT_INFO` and runs `THROW 50000, 'after the rows', 1;`.  The two `StreamThenRaise_ErrorAfterTheRows_*` tests of [`StreamingQueryTests`](../../tests/SqlSource.Tests.SqlServer/EndToEnd/StreamingQueryTests.cs) pin what its streaming method does: it yields the three accounts, the `MoveNextAsync` that ends the loop returns `false`, `DisposeAsync` returns normally, the connection is usable after, and the mark on the session shows the batch ran as far as the `THROW`.  A probe during the same work, not committed, showed an array method of the same SQL losing the error too, so it holds for the buffered methods as well.  The `one` and `one-optional` methods dispose their reader the same way after their second read, and are not tested.  The likely cause is that Microsoft.Data.SqlClient, when a reader is disposed, reads the rest of the batch and drops an error that follows the last result set it was asked for, which `NextResultAsync` would raise; the tests do not show the cause.

## Why it exists

The methods were written to read one result set, as [`D-0006`](../deferred/D-0006-multiple-result-sets-on-sql-server.md) leaves several result sets for later, and nothing tested a statement that fails after the rows until the streaming methods did.  It was found by the end-to-end tests of [streaming query methods](../superpowers/specs/2026-10-26-streaming-query-methods-design.md), whose scope is one collection type.  Reporting the error means changing every method that reads rows, buffered and streaming alike, with its generated code, its allocation tests and the manual's account of a query of several statements, which is a change of its own.

## Impact

Medium.  A query whose later statement can fail is uncommon, and the manual says that such an error is not reported, but the loss is silent: the caller sees the rows and success, while the batch stopped at the error, so a statement after it did not run and one before it, outside a transaction, stays done.  It risks a wrong result in a caller that relies on a later statement of a query having run.

## Proposed fix

After the rows a method reads, and before the reader is disposed, call `NextResultAsync` with the method's token until it returns `false`, so that the driver raises an error that a later result or statement holds.  In a streaming method, do it after the last row and in the `finally` that a `break` or a disposal runs, so that a loop that ends early drains the later results too; decide whether an error raised there, during a disposal, should be thrown or kept from hiding the exception that ended the loop.  Measure the allocations of both engines' `AllocationTests` before and after, add an end-to-end case for each shape that reads rows, `many` buffered and streaming, `one` and `one-optional`, on SQL Server and on PostgreSQL, where Npgsql may already report such an error, and update the account of a query of several statements in `docs/manual/sqlserver.md`, `docs/manual/generator.md` and the streaming rows of `docs/manual/settings.md`, with the known issue of `sqlserver.md` removed.

## Trigger

A user's report of a lost error, or the work of `D-0006` on several result sets, which reads the later results anyway.
