using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.DatabaseTests.SqlServer.EndToEnd.Types;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The queries of Queries/Streaming.sql, run on the seed of "app" through the methods that StreamingQueries gets, each
// returning an IAsyncEnumerable that yields a row as the reader reads it.  The method itself is not async: it checks
// its arguments at the call, and the iterator it returns opens a closed connection on its first MoveNextAsync and
// closes it again when the loop ends, however it ends.  The tests hold the connection's state in each of those ways,
// that a stream can be enumerated twice, that a break leaves the connection usable, and that a throw or a cancellation
// closes again a connection that the method opened.  Then a query whose rows fail past the row where a loop stops.
// A transaction that has completed is refused at the first MoveNextAsync.  The queries of several statements are
// StreamingBatchTests'.
[Collection(UsesSqlServer.Name)]
public sealed class StreamingQueryTests(SqlServerDatabases sqlServer)
{
    [Fact]
    public async Task StreamAccounts_EveryAccount_YieldsTheArrayMethodsRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);

        var streamed = await connection.StreamAccounts(cancellationToken: token).ToListAsync(token);

        streamed.ShouldBe(await MarkerCollectionQueries.ListAccountsInArray(connection, cancellationToken: token));
        streamed.ShouldBe(StreamingRows.Accounts);
    }

    [Fact]
    public async Task StreamAccountsNamedFrom_AParameter_YieldsTheAccountsFromThatId()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var streamed = await connection
            .StreamAccountsNamedFrom(new StreamAccountsNamedFromArgs(2), cancellationToken: token)
            .ToListAsync(token);

        streamed.ShouldBe([new CollectedAccount(2, "bob"), new CollectedAccount(3, "carol")]);
    }

    // The method is not async, so a null connection throws from the call itself and not from the first MoveNextAsync.
    [Fact]
    public void StreamAccounts_NullConnection_ThrowsAtTheCall() =>
        _ = Should.Throw<ArgumentNullException>(() => StreamingQueriesExtensions.StreamAccounts(null!));

    [Fact]
    public async Task StreamAccountsNamedFrom_NullParameters_ThrowsAtTheCall()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        _ = Should.Throw<ArgumentNullException>(() => connection.StreamAccountsNamedFrom(null!));
    }

    [Fact]
    public async Task StreamFromTable_BlankToken_ThrowsAtTheCall()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        _ = Should.Throw<ArgumentException>(() => connection.StreamFromTable(" "));
    }

    [Fact]
    public async Task StreamFromTable_ATokenValue_YieldsTheIdsOfThatTable()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var streamed = await connection.StreamFromTable("dbo.accounts", cancellationToken: token).ToListAsync(token);

        streamed.Select(row => row.Id).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task StreamAccounts_ClosedConnection_IsOpenDuringTheLoopAndClosedAfter()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var stateInTheLoop = new List<ConnectionState>();

        await foreach (var account in connection.StreamAccounts(cancellationToken: token))
        {
            stateInTheLoop.Add(connection.State);
        }

        stateInTheLoop.ShouldBe([ConnectionState.Open, ConnectionState.Open, ConnectionState.Open]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public async Task StreamAccounts_ClosedConnectionAndABreak_IsClosedAfterTheLoop()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var rows = 0;

        await foreach (var account in connection.StreamAccounts(cancellationToken: token))
        {
            if (++rows == 1)
            {
                break;
            }
        }

        rows.ShouldBe(1);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public async Task StreamAccounts_ClosedConnectionAndAThrowInTheLoopBody_IsClosedAfter()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await foreach (var account in connection.StreamAccounts(cancellationToken: token))
            {
                if (account.Id == 1)
                {
                    throw new InvalidOperationException("The body throws on the first row.");
                }
            }
        });

        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public async Task StreamAccounts_OpenConnection_StaysOpen()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);

        var streamed = await connection.StreamAccounts(cancellationToken: token).ToListAsync(token);

        streamed.Count.ShouldBe(3);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    // The iterator is made by each GetAsyncEnumerator, so a stream that is returned once runs its query each time it
    // is enumerated.
    [Fact]
    public async Task StreamAccounts_EnumeratedTwice_RunsTheQueryEachTime()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var stream = connection.StreamAccounts(cancellationToken: token);
        var first = new List<CollectedAccount>();
        var second = new List<CollectedAccount>();

        await foreach (var account in stream)
        {
            first.Add(account);
        }

        await foreach (var account in stream)
        {
            second.Add(account);
        }

        first.ShouldBe(StreamingRows.Accounts);
        second.ShouldBe(StreamingRows.Accounts);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The connection is opened by the first MoveNextAsync and not by GetAsyncEnumerator, so an enumerator that is
    // disposed before it has moved has opened nothing and has nothing to close.
    [Fact]
    public async Task StreamAccounts_DisposedBeforeTheFirstRow_OpensNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var stream = connection.StreamAccounts(cancellationToken: token);

        await stream.GetAsyncEnumerator(token).DisposeAsync();

        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // A transaction that has committed is refused when the loop starts, at the first MoveNextAsync, and not at the
    // call, so one that completes between the call and the loop is refused too.  The connection, closed after the
    // commit, is not opened.
    [Fact]
    public async Task StreamAccounts_TransactionCompletedBeforeTheLoop_IsRefusedAtTheFirstMoveNextAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);
        var stream = session.Connection.StreamAccounts(transaction: session.Transaction, cancellationToken: token);
        await session.ShouldCommitAsync(token);
        await session.Connection.CloseAsync();
        await using var enumerator = stream.GetAsyncEnumerator(token);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () => await enumerator.MoveNextAsync());

        thrown.GetType().ShouldBe(typeof(InvalidOperationException));
        thrown.Message.ShouldBe(
            "The transaction given to the query 'StreamAccounts' has completed; it is no longer usable."
        );
        session.Connection.State.ShouldBe(ConnectionState.Closed);
    }

    // SQL Server has no second command on a connection that a reader is reading, and the loop's body is where a caller
    // would run one.
    [Fact]
    public async Task StreamAccounts_ACommandInTheLoopBody_ThrowsSqlClientsError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await foreach (var account in connection.StreamAccounts(cancellationToken: token))
            {
                await using var other = new SqlCommand("SELECT 1", connection);
                _ = await other.ExecuteScalarAsync(token);
            }
        });

        thrown.Message.ShouldContain("There is already an open DataReader associated with this Connection");
        connection.State.ShouldBe(ConnectionState.Open);
    }

    // A break disposes the stream at its row, and the iterator reads the rest of the batch as the reader's disposal
    // would, with no token: a token that the loop's body cancelled before the break neither throws nor cancels it.
    [Fact]
    public async Task StreamAccounts_BreakAfterTheTokenIsCancelled_ThrowsNothing()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var rows = 0;

        await foreach (var account in connection.StreamAccounts(cancellationToken: cancellation.Token))
        {
            if (++rows == 1)
            {
                await cancellation.CancelAsync();
                break;
            }
        }

        rows.ShouldBe(1);
        connection.State.ShouldBe(ConnectionState.Open);
        (
            await MarkerCollectionQueries.ListAccountsInArray(
                connection,
                cancellationToken: TestContext.Current.CancellationToken
            )
        ).ShouldBe(StreamingRows.Accounts);
    }

    // The same with a throw in the loop's body in place of the break: the body's exception is the one the loop throws.
    [Fact]
    public async Task StreamAccounts_ThrowInTheLoopBodyAfterTheTokenIsCancelled_ThrowsTheBodysException()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await foreach (var account in connection.StreamAccounts(cancellationToken: cancellation.Token))
            {
                if (account.Id == 1)
                {
                    await cancellation.CancelAsync();
                    throw new InvalidOperationException("The body throws after cancelling.");
                }
            }
        });

        thrown.Message.ShouldBe("The body throws after cancelling.");
        connection.State.ShouldBe(ConnectionState.Open);
        (
            await MarkerCollectionQueries.ListAccountsInArray(
                connection,
                cancellationToken: TestContext.Current.CancellationToken
            )
        ).ShouldBe(StreamingRows.Accounts);
    }

    [Fact]
    public async Task StreamNumbers_BreakPartway_LeavesTheConnectionUsable()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        var rows = 0;

        await foreach (
            var row in connection.StreamNumbers(new StreamNumbersArgs(StreamingRows.ManyRows), cancellationToken: token)
        )
        {
            if (++rows == 10)
            {
                break;
            }
        }

        rows.ShouldBe(10);
        connection.State.ShouldBe(ConnectionState.Open);
        (await MarkerCollectionQueries.ListAccountsInArray(connection, cancellationToken: token)).ShouldBe(
            StreamingRows.Accounts
        );
    }

    // The break ends the stream while the transaction is open and the server is still sending rows, and the commit that
    // follows commits what the transaction did.  A temporary table that the transaction creates keeps what it writes
    // out of the seed, which every test of the collection shares, and lives as long as the connection.  A cancel would
    // not show here, since SQL Server keeps the transaction of a command that is cancelled: StreamNumbersThenMark holds
    // that the command is not.
    [Fact]
    public async Task StreamNumbers_BreakPartwayInATransaction_TheTransactionCommits()
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

        var rows = 0;
        await foreach (
            var row in connection.StreamNumbers(
                new StreamNumbersArgs(StreamingRows.ManyRows),
                transaction: transaction,
                cancellationToken: token
            )
        )
        {
            if (++rows == 10)
            {
                break;
            }
        }

        await transaction.CommitAsync(token);

        rows.ShouldBe(10);
        await using var select = new SqlCommand("SELECT n FROM #streamed", connection);
        (await select.ExecuteScalarAsync(token)).ShouldBe(7);
    }

    // A loop that reads every row gets the rows before the row of 500,000 and then the divide by zero that the server
    // raises for it, error 8134, which the tests below leave to the rows still to come.
    [Fact]
    public async Task StreamNumbersFailingHalfway_ReadToTheEnd_ThrowsAtTheFailingRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var rows = 0;

        var thrown = await Should.ThrowAsync<SqlException>(async () =>
        {
            await foreach (var row in connection.StreamNumbersFailingHalfway(cancellationToken: token))
            {
                rows++;
            }
        });

        thrown.Number.ShouldBe(StreamingRows.DivideByZero);
        rows.ShouldBe(499_999);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // A break disposes the stream at the row it yielded, and the iterator reads the rows still to come, among them the
    // row of 500,000, whose divide by zero the break throws.  The connection is usable after.
    [Fact]
    public async Task StreamNumbersFailingHalfway_BreakBeforeTheFailingRow_ThrowsTheError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        var rows = 0;

        var thrown = await Should.ThrowAsync<SqlException>(async () =>
        {
            await foreach (var row in connection.StreamNumbersFailingHalfway(cancellationToken: token))
            {
                if (++rows == 10)
                {
                    break;
                }
            }
        });

        thrown.Number.ShouldBe(StreamingRows.DivideByZero);
        rows.ShouldBe(10);
        connection.State.ShouldBe(ConnectionState.Open);
        (await MarkerCollectionQueries.ListAccountsInArray(connection, cancellationToken: token)).ShouldBe(
            StreamingRows.Accounts
        );
    }

    // The divide by zero of a row still to come ends that statement alone, as SqlClient leaves XACT_ABORT off: the
    // break throws it, and the transaction the stream ran in commits after.  The temporary table is the one of the
    // transaction test of StreamNumbers.  PostgreSQL aborts the transaction here.
    [Fact]
    public async Task StreamNumbersFailingHalfway_BreakInATransaction_ThrowsAndTheTransactionCommits()
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

        var rows = 0;
        var thrown = await Should.ThrowAsync<SqlException>(async () =>
        {
            await foreach (
                var row in connection.StreamNumbersFailingHalfway(transaction: transaction, cancellationToken: token)
            )
            {
                if (++rows == 10)
                {
                    break;
                }
            }
        });
        await transaction.CommitAsync(token);

        thrown.Number.ShouldBe(StreamingRows.DivideByZero);
        rows.ShouldBe(10);
        await using var select = new SqlCommand("SELECT n FROM #streamed", connection);
        (await select.ExecuteScalarAsync(token)).ShouldBe(7);
    }

    // Under SET XACT_ABORT ON the same divide by zero rolls the transaction back, and the break throws it, so the
    // caller learns of the rollback from the loop.
    [Fact]
    public async Task StreamNumbersFailingHalfway_BreakInATransactionUnderXactAbort_ThrowsAndRollsItBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: true, token);

        var rows = 0;
        var thrown = await Should.ThrowAsync<SqlException>(async () =>
        {
            await foreach (
                var row in session.Connection.StreamNumbersFailingHalfway(
                    transaction: session.Transaction,
                    cancellationToken: token
                )
            )
            {
                if (++rows == 10)
                {
                    break;
                }
            }
        });

        thrown.Number.ShouldBe(StreamingRows.DivideByZero);
        rows.ShouldBe(10);
        await session.ShouldHaveRolledBackAsync(token);
    }

    // The body's exception disposes the stream at its row as a break does, and the iterator reads the rows still to
    // come, whose divide by zero the loop throws in the place of the body's exception, as PostgreSQL does.
    [Fact]
    public async Task StreamNumbersFailingHalfway_AThrowInTheLoopBody_ThrowsTheServersErrorInItsPlace()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var rows = new List<StreamNumbersFailingHalfwayDto>();

        var thrown = await Should.ThrowAsync<SqlException>(async () =>
        {
            await foreach (var row in connection.StreamNumbersFailingHalfway(cancellationToken: token))
            {
                rows.Add(row);
                if (row.N == 1)
                {
                    throw new InvalidOperationException("The body throws on the first row.");
                }
            }
        });

        thrown.Number.ShouldBe(StreamingRows.DivideByZero);
        rows.ShouldBe([new StreamNumbersFailingHalfwayDto(1, 0)]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // SqlClient's ReadAsync throws a TaskCanceledException for a token cancelled between two rows, as the methods that
    // buffer their rows do.
    [Fact]
    public async Task StreamNumbers_MethodToken_Cancels()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var connection = EndToEndSources.App(sqlServer);
        var stream = connection.StreamNumbers(
            new StreamNumbersArgs(StreamingRows.ManyRows),
            cancellationToken: cancellation.Token
        );

        _ = await Should.ThrowAsync<TaskCanceledException>(async () =>
        {
            await foreach (var row in stream)
            {
                await cancellation.CancelAsync();
            }
        });

        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The token of the call that enumerates, which the iterator's [EnumeratorCancellation] receives.
    [Fact]
    public async Task StreamNumbers_WithCancellation_Cancels()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var connection = EndToEndSources.App(sqlServer);
        var stream = connection.StreamNumbers(
            new StreamNumbersArgs(StreamingRows.ManyRows),
            cancellationToken: TestContext.Current.CancellationToken
        );

        _ = await Should.ThrowAsync<TaskCanceledException>(async () =>
        {
            await foreach (var row in stream.WithCancellation(cancellation.Token))
            {
                await cancellation.CancelAsync();
            }
        });

        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The query yields a NULL for the third row of a column that its marker says is never NULL: the first two rows are
    // handed to the loop before the third fails, which is what a stream is for.
    [Fact]
    public async Task StreamNullAtThird_NullInANotNullColumn_ThrowsAtThatRowAfterTheOthers()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var rows = new List<StreamNullAtThirdDto>();

        _ = await Should.ThrowAsync<SqlNullValueException>(async () =>
        {
            await foreach (var row in connection.StreamNullAtThird(cancellationToken: token))
            {
                rows.Add(row);
            }
        });

        rows.ShouldBe([new StreamNullAtThirdDto(1, "alice"), new StreamNullAtThirdDto(2, "bob")]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // StreamNoteAccounts is a query of Notes.sql, which converts account_id to an AccountId through the converters of
    // the type, and gives this one its own collection type: its method is in NoteQueriesExtensions.
    [Fact]
    public async Task StreamNoteAccounts_ConvertedColumn_YieldsAccountIds()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var streamed = await connection.StreamNoteAccounts(cancellationToken: token).ToListAsync(token);

        streamed.ShouldBe([new StreamNoteAccountsDto(new AccountId(1)), new StreamNoteAccountsDto(new AccountId(2))]);
    }
}
