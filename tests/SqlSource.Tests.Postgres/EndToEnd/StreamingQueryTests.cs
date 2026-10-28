using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tests.Postgres.EndToEnd.Types;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The queries of Queries/Streaming.sql, run on the seed of "app" through the methods that StreamingQueries gets, each
// returning an IAsyncEnumerable that yields a row as the reader reads it.  The method itself is not async: it checks
// its arguments at the call, and the iterator it returns opens a closed connection on its first MoveNextAsync and
// closes it again when the loop ends, however it ends.  The tests hold the connection's state in each of those ways,
// that a stream can be enumerated twice, and that a break, a throw and a cancellation leave a usable connection.
[Collection(UsesPostgres.Name)]
public sealed class StreamingQueryTests(PostgresContainer postgres)
{
    // The seed's three accounts.
    private static readonly CollectedAccount[] Accounts = [new(1, "alice"), new(2, "bob"), new(3, "carol")];

    // The count of the numbers that a break and a cancellation leave unread: far more than the driver buffers.
    private const int ManyRows = 10_000;

    [Fact]
    public async Task StreamAccounts_EveryAccount_YieldsTheArrayMethodsRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var streamed = await connection.StreamAccounts(cancellationToken: token).ToListAsync(token);

        streamed.ShouldBe(await connection.ListAsArray(cancellationToken: token));
        streamed.ShouldBe(Accounts);
    }

    [Fact]
    public async Task StreamAccountsNamedFrom_AParameter_YieldsTheAccountsFromThatId()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var streamed = await connection
            .StreamAccountsNamedFrom(new StreamAccountsNamedFromArgs(2), cancellationToken: token)
            .ToListAsync(token);

        streamed.ShouldBe([new CollectedAccount(2, "bob"), new CollectedAccount(3, "carol")]);
    }

    // The method is not async, so a null connection throws from the call itself and not from the first MoveNextAsync.
    [Fact]
    public void StreamAccounts_NullConnection_ThrowsAtTheCall() =>
        Should.Throw<ArgumentNullException>(() => StreamingQueriesExtensions.StreamAccounts(null!));

    [Fact]
    public async Task StreamAccountsNamedFrom_NullParameters_ThrowsAtTheCall()
    {
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        _ = Should.Throw<ArgumentNullException>(() => connection.StreamAccountsNamedFrom(null!));
    }

    [Fact]
    public async Task StreamFromTable_BlankToken_ThrowsAtTheCall()
    {
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        _ = Should.Throw<ArgumentException>(() => connection.StreamFromTable(" "));
    }

    [Fact]
    public async Task StreamFromTable_ATokenValue_YieldsTheIdsOfThatTable()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var streamed = await connection.StreamFromTable("accounts", cancellationToken: token).ToListAsync(token);

        streamed.Select(row => row.Id).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task StreamAccounts_ClosedConnection_IsOpenDuringTheLoopAndClosedAfter()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
        var stream = connection.StreamAccounts(cancellationToken: token);

        await using (var enumerator = stream.GetAsyncEnumerator(token))
        {
            connection.State.ShouldBe(ConnectionState.Closed);
        }

        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public async Task StreamNumbers_BreakPartway_LeavesTheConnectionUsable()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
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
        (await connection.ListAsArray(cancellationToken: token)).ShouldBe(Accounts);
    }

    // The break ends the stream while the transaction is open: the stream's reader is closed and the transaction is
    // the caller's, so the commit that follows commits what the transaction did.  A temporary table keeps what the
    // transaction writes out of the seed, which every test of the collection shares, and lives as long as the
    // connection.
    [Fact]
    public async Task StreamNumbers_BreakPartwayInATransaction_TheTransactionCommits()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using (var create = new NpgsqlCommand("CREATE TEMPORARY TABLE streamed (n integer)", connection))
        {
            _ = await create.ExecuteNonQueryAsync(token);
        }

        await using var transaction = await connection.BeginTransactionAsync(token);
        await using (var insert = new NpgsqlCommand("INSERT INTO streamed (n) VALUES (7)", connection, transaction))
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
        await using var select = new NpgsqlCommand("SELECT n FROM streamed", connection);
        (await select.ExecuteScalarAsync(token)).ShouldBe(7);
    }

    [Fact]
    public async Task StreamNumbers_MethodToken_Cancels()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
        var stream = connection.StreamNumbers(new StreamNumbersArgs(1_000_000), cancellationToken: cancellation.Token);

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
        var stream = connection.StreamNumbers(
            new StreamNumbersArgs(1_000_000),
            cancellationToken: TestContext.Current.CancellationToken
        );

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
        var rows = new List<StreamNullAtThirdDto>();

        _ = await Should.ThrowAsync<InvalidCastException>(async () =>
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
    public async Task StreamNoteAccounts_ConvertedColumn_ReadsAsTheArrayMethodDoes()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var streamed = await connection.StreamNoteAccounts(cancellationToken: token).ToListAsync(token);

        streamed.ShouldBe([new StreamNoteAccountsDto(new AccountId(1)), new StreamNoteAccountsDto(new AccountId(2))]);
    }
}
