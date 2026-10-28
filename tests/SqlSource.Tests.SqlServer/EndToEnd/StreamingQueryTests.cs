using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Tests.SqlServer.EndToEnd.Types;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The queries of Queries/Streaming.sql, run on the seed of "app" through the methods that StreamingQueries gets, each
// returning an IAsyncEnumerable that yields a row as the reader reads it.  The method itself is not async: it checks
// its arguments at the call, and the iterator it returns opens a closed connection on its first MoveNextAsync and
// closes it again when the loop ends, however it ends.  The tests hold the connection's state in each of those ways,
// that a stream can be enumerated twice, and that a break, a throw and a cancellation leave a usable connection.  Then
// the queries of several statements, whose later statements run when the reader is disposed.
[Collection(UsesSqlServer.Name)]
public sealed class StreamingQueryTests(SqlServerContainer sqlServer)
{
    // The seed's three accounts.
    private static readonly CollectedAccount[] Accounts = [new(1, "alice"), new(2, "bob"), new(3, "carol")];

    // The count of the numbers that a break leaves unread.  The query sorts them, so the server has all of them before
    // it sends the first and is still sending when the loop stops.
    private const int ManyRows = 1_000_000;

    [Fact]
    public async Task StreamAccounts_EveryAccount_YieldsTheArrayMethodsRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);

        var streamed = await connection.StreamAccounts(cancellationToken: token).ToListAsync(token);

        streamed.ShouldBe(await MarkerCollectionQueries.ListAccountsInArray(connection, cancellationToken: token));
        streamed.ShouldBe(Accounts);
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

        first.ShouldBe(Accounts);
        second.ShouldBe(Accounts);
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

    [Fact]
    public async Task StreamNumbers_BreakPartway_LeavesTheConnectionUsable()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        var rows = 0;

        await foreach (var row in connection.StreamNumbers(new StreamNumbersArgs(ManyRows), cancellationToken: token))
        {
            if (++rows == 10)
            {
                break;
            }
        }

        rows.ShouldBe(10);
        connection.State.ShouldBe(ConnectionState.Open);
        (await MarkerCollectionQueries.ListAccountsInArray(connection, cancellationToken: token)).ShouldBe(Accounts);
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
                new StreamNumbersArgs(ManyRows),
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

    // SqlClient's ReadAsync throws a TaskCanceledException for a token cancelled between two rows, as the methods that
    // buffer their rows do.
    [Fact]
    public async Task StreamNumbers_MethodToken_Cancels()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var connection = EndToEndSources.App(sqlServer);
        var stream = connection.StreamNumbers(new StreamNumbersArgs(ManyRows), cancellationToken: cancellation.Token);

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
            new StreamNumbersArgs(ManyRows),
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

    // Notes.sql converts account_id to an AccountId through the converters of the type, and so does this query.
    [Fact]
    public async Task StreamNoteAccounts_ConvertedColumn_YieldsAccountIds()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var streamed = await connection.StreamNoteAccounts(cancellationToken: token).ToListAsync(token);

        streamed.ShouldBe([new StreamNoteAccountsDto(new AccountId(1)), new StreamNoteAccountsDto(new AccountId(2))]);
    }

    // The stream reads the first result set and no more, and the second statement's result set is read and discarded
    // when the reader is disposed.
    [Fact]
    public async Task StreamThenSelect_TwoStatements_YieldsTheRowsOfTheFirst()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var streamed = await connection.StreamThenSelect(cancellationToken: token).ToListAsync(token);

        streamed.ShouldBe(Accounts);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The rows of the first statement are yielded and nothing is thrown, though the batch ran as far as the THROW: the
    // statement before it set a mark on the session, which the test reads back on the same connection, open for that.
    // The batch's error is not reported, as SqlClient reads the rest of the batch when the reader is disposed and drops
    // an error that follows the last result set that it was asked for.  The mark shows that the batch reached the
    // THROW, and not that the THROW ran.
    [Fact]
    public async Task StreamThenRaise_ErrorAfterTheRows_RunsTheBatchToTheThrowAndIsNotThrown()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await ClearMarkAsync(connection, token);

        var streamed = await connection.StreamThenRaise(cancellationToken: token).ToListAsync(token);

        streamed.ShouldBe(Accounts);
        (await ReadMarkAsync(connection, token)).ShouldBe(2);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    // The same stream by hand, to see which calls would throw: the MoveNextAsync that ends the loop returns false, and
    // so does the DisposeAsync after it, on an open connection that the disposal leaves usable, with the mark of the
    // statement before the THROW set.
    [Fact]
    public async Task StreamThenRaise_ErrorAfterTheRows_ThrowsFromNeitherMoveNextAsyncNorDisposeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await ClearMarkAsync(connection, token);
        var enumerator = connection.StreamThenRaise(cancellationToken: token).GetAsyncEnumerator(token);
        var rows = new List<CollectedAccount>();

        while (await enumerator.MoveNextAsync())
        {
            rows.Add(enumerator.Current);
        }

        await enumerator.DisposeAsync();

        rows.ShouldBe(Accounts);
        (await ReadMarkAsync(connection, token)).ShouldBe(2);
        connection.State.ShouldBe(ConnectionState.Open);
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
        await ClearMarkAsync(connection, token);

        var rows = 0;
        await foreach (
            var row in connection.StreamNumbersThenMark(
                new StreamNumbersThenMarkArgs(ManyRows),
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
        (await ReadMarkAsync(connection, token)).ShouldBe(1);
    }

    // The mark of the queries that end with SET CONTEXT_INFO: the first byte of the session's context, which stays on
    // an open connection and which the pool resets when the connection is returned to it.
    private static async Task ClearMarkAsync(SqlConnection connection, CancellationToken token)
    {
        await using var clear = new SqlCommand("SET CONTEXT_INFO 0x00", connection);
        _ = await clear.ExecuteNonQueryAsync(token);
    }

    private static async Task<object?> ReadMarkAsync(SqlConnection connection, CancellationToken token)
    {
        await using var select = new SqlCommand("SELECT CAST(SUBSTRING(CONTEXT_INFO(), 1, 1) AS int)", connection);
        return await select.ExecuteScalarAsync(token);
    }
}
