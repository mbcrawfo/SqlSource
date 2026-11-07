using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The queries of Queries/Streaming.sql that have several statements, run on the seed of "app" through the methods that
// StreamingQueries gets: the iterator reads the rest of the batch after the last row, and in its finally when the loop
// is left at a row, by a break, a throw in its body or a disposal, so that an error the rest of the batch raises is
// thrown, in place of the body's exception too.  Under XACT_ABORT ON that error rolls back the caller's transaction,
// and with it off CAST('x' AS int) after the rows does too, where a THROW, a divide by zero or an overflow leaves it
// to commit.  When a row throws an exception of the iterator's own, the reader's disposal drops such an error, and the
// iterator throws TransactionRolledBackException when it rolled the transaction back.  A database error at a row or
// from a drain, in a transaction, reads the rest of the batch, and the iterator throws TransactionRolledBackException,
// with the database's error inside, when a later error there rolled the transaction back.
[Collection(UsesSqlServer.Name)]
public sealed class StreamingBatchTests(SqlServerDatabases sqlServer)
{
    // The number of SQL Server's error "Conversion failed when converting the varchar value 'x' to data type int.".
    private const int ConversionFailed = 245;

    // The number of SQL Server's error "Arithmetic overflow error for data type tinyint, value = 300.".
    private const int ArithmeticOverflow = 220;

    // A NULL in a NOT NULL column at the third row, and then a failed conversion after the rows, which the reader's
    // disposal reads and which with XACT_ABORT off rolls back the transaction: the first two rows are handed to the
    // loop, and then the iterator throws the rollback, with the NULL's exception inside it, so the caller learns of
    // the rollback from the loop.
    [Fact]
    public async Task StreamNullAtThirdThenFailConversion_InATransaction_ThrowsTheRollbackAfterTwoRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);
        var rows = new List<CollectedAccount>();

        var thrown = await Should.ThrowAsync<TransactionRolledBackException>(async () =>
        {
            await foreach (
                var row in session.Connection.StreamNullAtThirdThenFailConversion(
                    transaction: session.Transaction,
                    cancellationToken: token
                )
            )
            {
                rows.Add(row);
            }
        });

        rows.ShouldBe([StreamingRows.Accounts[0], StreamingRows.Accounts[1]]);
        thrown.QueryName.ShouldBe("StreamNullAtThirdThenFailConversion");
        _ = thrown.InnerException.ShouldBeOfType<SqlNullValueException>();
        await session.ShouldHaveRolledBackAsync(token);
    }

    // The stream yields the rows of the first result set alone, and reads past the second's after the last row.
    [Fact]
    public async Task StreamThenSelect_TwoStatements_YieldsTheRowsOfTheFirst()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var streamed = await connection.StreamThenSelect(cancellationToken: token).ToListAsync(token);

        streamed.ShouldBe(StreamingRows.Accounts);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The rows of the first statement are yielded, and the iterator reads the results after them, so the THROW is
    // thrown once the rows are read: the statement before it set a mark on the session, which the test reads back on
    // the same connection, open for that.
    [Fact]
    public async Task StreamThenRaise_ErrorAfterTheRows_YieldsTheRowsThenThrows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await SessionMark.ClearAsync(connection, token);
        var streamed = new List<CollectedAccount>();

        var thrown = await Should.ThrowAsync<SqlException>(async () =>
        {
            await foreach (var row in connection.StreamThenRaise(cancellationToken: token))
            {
                streamed.Add(row);
            }
        });

        thrown.Number.ShouldBe(50000);
        thrown.Message.ShouldBe("after the rows");
        streamed.ShouldBe(StreamingRows.Accounts);
        (await SessionMark.ReadAsync(connection, token)).ShouldBe(2);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    // The same stream by hand, to see which call throws: each of the three rows is a MoveNextAsync that returns true,
    // the fourth MoveNextAsync, which would end the loop, throws the error, and the DisposeAsync after it returns
    // normally.
    [Fact]
    public async Task StreamThenRaise_ErrorAfterTheRows_ThrowsFromTheLastMoveNextAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        var enumerator = connection.StreamThenRaise(cancellationToken: token).GetAsyncEnumerator(token);
        var rows = new List<CollectedAccount>();

        for (var index = 0; index < StreamingRows.Accounts.Length; index++)
        {
            (await enumerator.MoveNextAsync()).ShouldBeTrue();
            rows.Add(enumerator.Current);
        }

        var thrown = await Should.ThrowAsync<SqlException>(async () => await enumerator.MoveNextAsync());
        await enumerator.DisposeAsync();

        thrown.Number.ShouldBe(50000);
        rows.ShouldBe(StreamingRows.Accounts);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    // A break at the first row disposes the stream at that row, and the iterator reads the rest of the batch, whose
    // THROW the break throws.
    [Fact]
    public async Task StreamThenRaise_BreakAtTheFirstRow_ThrowsTheError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await SessionMark.ClearAsync(connection, token);
        var streamed = new List<CollectedAccount>();

        var thrown = await Should.ThrowAsync<SqlException>(async () =>
        {
            await foreach (var row in connection.StreamThenRaise(cancellationToken: token))
            {
                streamed.Add(row);
                if (streamed.Count == 1)
                {
                    break;
                }
            }
        });

        thrown.Number.ShouldBe(50000);
        thrown.Message.ShouldBe("after the rows");
        streamed.ShouldBe([StreamingRows.Accounts[0]]);
        (await SessionMark.ReadAsync(connection, token)).ShouldBe(2);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    // A token that the loop's body cancelled before the break does not stop the iterator reading the rest of the
    // batch, so the THROW after the rows is thrown at the break as it is without the cancel.
    [Fact]
    public async Task StreamThenRaise_BreakAfterTheTokenIsCancelled_ThrowsTheError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await SessionMark.ClearAsync(connection, token);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var rows = 0;

        var thrown = await Should.ThrowAsync<SqlException>(async () =>
        {
            await foreach (var row in connection.StreamThenRaise(cancellationToken: cancellation.Token))
            {
                if (++rows == 1)
                {
                    await cancellation.CancelAsync();
                    break;
                }
            }
        });

        thrown.Number.ShouldBe(50000);
        thrown.Message.ShouldBe("after the rows");
        rows.ShouldBe(1);
        (await SessionMark.ReadAsync(connection, token)).ShouldBe(2);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    // With XACT_ABORT off, as SqlClient leaves it, the THROW ends the batch and leaves the transaction that the stream
    // ran in open: the loop throws the error, and the commit after it commits what the transaction did.  The temporary
    // table is the one of the transaction test of StreamNumbers.
    [Fact]
    public async Task StreamThenRaise_ErrorAfterTheRowsInATransaction_ThrowsAndTheTransactionCommits()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(token);
        await using (var create = new SqlCommand("CREATE TABLE #streamed (n int)", connection, transaction))
        {
            _ = await create.ExecuteNonQueryAsync(token);
        }

        await using (var insert = new SqlCommand("INSERT INTO #streamed (n) VALUES (7)", connection, transaction))
        {
            _ = await insert.ExecuteNonQueryAsync(token);
        }

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            connection.StreamThenRaise(transaction: transaction, cancellationToken: token).ToListAsync(token).AsTask()
        );
        await transaction.CommitAsync(token);

        thrown.Number.ShouldBe(50000);
        await using var select = new SqlCommand("SELECT n FROM #streamed", connection);
        (await select.ExecuteScalarAsync(token)).ShouldBe(7);
    }

    // Under SET XACT_ABORT ON the THROW rolls back the transaction that the stream ran in, and the loop throws the
    // error, so the caller learns of the rollback from the loop.
    [Fact]
    public async Task StreamThenRaise_ErrorAfterTheRowsInATransactionUnderXactAbort_ThrowsAndRollsItBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: true, token);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            session
                .Connection.StreamThenRaise(transaction: session.Transaction, cancellationToken: token)
                .ToListAsync(token)
                .AsTask()
        );

        thrown.Number.ShouldBe(50000);
        await session.ShouldHaveRolledBackAsync(token);
    }

    // With XACT_ABORT off, as SqlClient leaves it, where the THROW of StreamThenRaise leaves the transaction to commit,
    // the failed conversion after the rows rolls it back, and the loop throws the error: so whether the error rolls
    // back the transaction depends on the error, not on XACT_ABORT alone.  The conversion is a SELECT, whose result set
    // the iterator moves to before its error is raised.
    [Fact]
    public async Task StreamThenFailConversion_ErrorAfterTheRowsInATransaction_ThrowsAndRollsItBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            session
                .Connection.StreamThenFailConversion(transaction: session.Transaction, cancellationToken: token)
                .ToListAsync(token)
                .AsTask()
        );

        thrown.Number.ShouldBe(ConversionFailed);
        await session.ShouldHaveRolledBackAsync(token);
    }

    // An overflow after the rows, a value too large for tinyint, ends its statement alone with XACT_ABORT off, as a
    // divide by zero does: the loop throws the error, and the transaction stays open and commits, where the failed
    // conversion rolls it back.
    [Fact]
    public async Task StreamThenOverflow_ErrorAfterTheRowsInATransaction_ThrowsAndTheTransactionCommits()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            session
                .Connection.StreamThenOverflow(transaction: session.Transaction, cancellationToken: token)
                .ToListAsync(token)
                .AsTask()
        );

        thrown.Number.ShouldBe(ArithmeticOverflow);
        await session.ShouldCommitAsync(token);
    }

    // Under SET XACT_ABORT ON the same overflow rolls the transaction back, and the loop throws it.
    [Fact]
    public async Task StreamThenOverflow_ErrorAfterTheRowsInATransactionUnderXactAbort_ThrowsAndRollsItBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: true, token);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            session
                .Connection.StreamThenOverflow(transaction: session.Transaction, cancellationToken: token)
                .ToListAsync(token)
                .AsTask()
        );

        thrown.Number.ShouldBe(ArithmeticOverflow);
        await session.ShouldHaveRolledBackAsync(token);
    }

    // The read of the second row throws the divide by zero, the database's error, which with XACT_ABORT off leaves the
    // transaction open, and the iterator reads the rest of the batch, whose failed conversion rolls it back: the first
    // row is handed to the loop, and then the loop throws the rollback, with the divide by zero inside it.
    [Fact]
    public async Task StreamFailingAtSecondThenFailConversion_InATransaction_YieldsTheFirstRowThenThrowsTheRollback()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);
        var rows = new List<StreamFailingAtSecondThenFailConversionDto>();

        var thrown = await Should.ThrowAsync<TransactionRolledBackException>(async () =>
        {
            await foreach (
                var row in session.Connection.StreamFailingAtSecondThenFailConversion(
                    transaction: session.Transaction,
                    cancellationToken: token
                )
            )
            {
                rows.Add(row);
            }
        });

        rows.ShouldBe([new StreamFailingAtSecondThenFailConversionDto(1, -10)]);
        thrown.QueryName.ShouldBe("StreamFailingAtSecondThenFailConversion");
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(StreamingRows.DivideByZero);
        await session.ShouldHaveRolledBackAsync(token);
    }

    // A break at the first row disposes the stream at that row, and the drain of the rest of the batch throws the
    // divide by zero of the second; the iterator reads on, and the failed conversion rolls back the transaction, so the
    // break throws the rollback, with the divide by zero inside it.
    [Fact]
    public async Task StreamFailingAtSecondThenFailConversion_BreakAtTheFirstRowInATransaction_ThrowsTheRollback()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);
        var rows = new List<StreamFailingAtSecondThenFailConversionDto>();

        var thrown = await Should.ThrowAsync<TransactionRolledBackException>(async () =>
        {
            await foreach (
                var row in session.Connection.StreamFailingAtSecondThenFailConversion(
                    transaction: session.Transaction,
                    cancellationToken: token
                )
            )
            {
                rows.Add(row);
                if (rows.Count == 1)
                {
                    break;
                }
            }
        });

        rows.ShouldBe([new StreamFailingAtSecondThenFailConversionDto(1, -10)]);
        thrown.QueryName.ShouldBe("StreamFailingAtSecondThenFailConversion");
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(StreamingRows.DivideByZero);
        await session.ShouldHaveRolledBackAsync(token);
    }

    // The drain after the loop throws the overflow, a database error after the rows, and the iterator reads on, to the
    // failed conversion, which rolls back the transaction with XACT_ABORT off: every row is handed to the loop, and
    // then the loop throws the rollback, with the overflow inside it.
    [Fact]
    public async Task StreamThenOverflowThenFailConversion_InATransaction_ThrowsTheRollbackAfterTheRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);
        var rows = new List<CollectedAccount>();

        var thrown = await Should.ThrowAsync<TransactionRolledBackException>(async () =>
        {
            await foreach (
                var row in session.Connection.StreamThenOverflowThenFailConversion(
                    transaction: session.Transaction,
                    cancellationToken: token
                )
            )
            {
                rows.Add(row);
            }
        });

        rows.ShouldBe(StreamingRows.Accounts);
        thrown.QueryName.ShouldBe("StreamThenOverflowThenFailConversion");
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(ArithmeticOverflow);
        await session.ShouldHaveRolledBackAsync(token);
    }

    // A token cancelled before a break, with a million numbers still to send, cancels nothing: the iterator reads the
    // rest of the batch without it, so the statement after the numbers runs and marks the session.
    [Fact]
    public async Task StreamNumbersThenMark_BreakAfterTheTokenIsCancelled_RunsTheStatementAfterTheNumbers()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await SessionMark.ClearAsync(connection, token);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);

        var rows = 0;
        await foreach (
            var row in connection.StreamNumbersThenMark(
                new StreamNumbersThenMarkArgs(StreamingRows.ManyRows),
                cancellationToken: cancellation.Token
            )
        )
        {
            if (++rows == 10)
            {
                await cancellation.CancelAsync();
                break;
            }
        }

        rows.ShouldBe(10);
        (await SessionMark.ReadAsync(connection, token)).ShouldBe(1);
    }

    // A break reads and discards the rest of the result, and the statements after it run: SET CONTEXT_INFO marks the
    // session of the connection, which stays open, and the mark is there only when the batch ran to its end, where a
    // command cancelled by the break, with a million numbers still to send, would not have reached it.
    [Fact]
    public async Task StreamNumbersThenMark_BreakPartway_RunsTheStatementAfterTheNumbers()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await SessionMark.ClearAsync(connection, token);

        var rows = 0;
        await foreach (
            var row in connection.StreamNumbersThenMark(
                new StreamNumbersThenMarkArgs(StreamingRows.ManyRows),
                cancellationToken: token
            )
        )
        {
            if (++rows == 10)
            {
                break;
            }
        }

        rows.ShouldBe(10);
        (await SessionMark.ReadAsync(connection, token)).ShouldBe(1);
    }
}
