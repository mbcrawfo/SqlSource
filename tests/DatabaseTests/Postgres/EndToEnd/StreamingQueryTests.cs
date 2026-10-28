using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.DatabaseTests.Postgres.EndToEnd.Types;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The queries of Queries/Streaming.sql, run on the seed of "app" through the methods that StreamingQueries gets, each
// returning an IAsyncEnumerable that yields a row as the reader reads it.  The method itself is not async: it checks
// its arguments at the call, and the iterator it returns opens a closed connection on its first MoveNextAsync and
// closes it again when the loop ends, however it ends.  The tests hold the connection's state in each of those ways,
// that a stream can be enumerated twice, that a break leaves the connection usable, and that a throw or a cancellation
// closes again a connection that the method opened.  Then a query whose rows fail past the row where a loop stops,
// whose error the disposal throws.
[Collection(UsesPostgres.Name)]
public sealed class StreamingQueryTests(PostgresContainer postgres)
{
    // The seed's three accounts.
    private static readonly CollectedAccount[] Accounts = [new(1, "alice"), new(2, "bob"), new(3, "carol")];

    // The count of the numbers that a break leaves unread.  The query sorts them, so the server is still sending when
    // the loop stops, and a stream that cancelled its command on the break would abort the transaction of the caller;
    // with 10,000 rows the server has sent them all by then and a cancel changes nothing.
    private const int ManyRows = 1_000_000;

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
        _ = Should.Throw<ArgumentNullException>(() => StreamingQueriesExtensions.StreamAccounts(null!));

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

        await stream.GetAsyncEnumerator(token).DisposeAsync();

        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // Npgsql runs one command at a time on a connection, and the loop's body, while the reader reads, is where a caller
    // would run a second.
    [Fact]
    public async Task StreamAccounts_ACommandInTheLoopBody_ThrowsNpgsqlsError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var thrown = await Should.ThrowAsync<NpgsqlOperationInProgressException>(async () =>
        {
            await foreach (var account in connection.StreamAccounts(cancellationToken: token))
            {
                await using var other = new NpgsqlCommand("SELECT 1", connection);
                _ = await other.ExecuteScalarAsync(token);
            }
        });

        thrown.Message.ShouldStartWith("A command is already in progress");
        connection.State.ShouldBe(ConnectionState.Open);
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

    // The break ends the stream while the transaction is open and the server is still sending rows: the stream's
    // reader is closed and its command is not cancelled, which would abort the transaction, so the commit that follows
    // commits what the transaction did.  A temporary table keeps what the
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

    // A loop that reads every row gets the rows before the row of 500,000 and then the division by zero that the server
    // raises for it, which the tests below leave to the rows still to come.
    [Fact]
    public async Task StreamNumbersFailingHalfway_ReadToTheEnd_ThrowsAtTheFailingRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
        var rows = 0;

        var thrown = await Should.ThrowAsync<PostgresException>(async () =>
        {
            await foreach (var row in connection.StreamNumbersFailingHalfway(cancellationToken: token))
            {
                rows++;
            }
        });

        thrown.SqlState.ShouldBe(PostgresErrorCodes.DivisionByZero);
        rows.ShouldBe(499_999);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // A break disposes the stream, whose reader reads the rows still to come, and the row of 500,000 is among them: the
    // division by zero that the server raises for it is thrown at the break, by the loop.
    [Fact]
    public async Task StreamNumbersFailingHalfway_BreakBeforeTheFailingRow_ThrowsTheServersError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        var rows = 0;

        var thrown = await Should.ThrowAsync<PostgresException>(async () =>
        {
            await foreach (var row in connection.StreamNumbersFailingHalfway(cancellationToken: token))
            {
                if (++rows == 10)
                {
                    break;
                }
            }
        });

        thrown.SqlState.ShouldBe(PostgresErrorCodes.DivisionByZero);
        rows.ShouldBe(10);
        connection.State.ShouldBe(ConnectionState.Open);
        (await connection.ListAsArray(cancellationToken: token)).ShouldBe(Accounts);
    }

    // The body's exception disposes the stream as a break does, and the server's error for a row still to come is
    // thrown in its place: the body's exception is lost.
    [Fact]
    public async Task StreamNumbersFailingHalfway_AThrowInTheLoopBody_ThrowsTheServersErrorInItsPlace()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
        var rows = new List<StreamNumbersFailingHalfwayDto>();

        var thrown = await Should.ThrowAsync<PostgresException>(async () =>
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

        thrown.SqlState.ShouldBe(PostgresErrorCodes.DivisionByZero);
        rows.ShouldBe([new StreamNumbersFailingHalfwayDto(1, 0)]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The server's error for a row still to come aborts the transaction the stream ran in, as any error of a statement
    // does on PostgreSQL: the next command in it is refused until it is rolled back.
    [Fact]
    public async Task StreamNumbersFailingHalfway_BreakInATransaction_AbortsTheTransaction()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var rows = 0;

        _ = await Should.ThrowAsync<PostgresException>(async () =>
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

        await using var select = new NpgsqlCommand("SELECT 1", connection, transaction);
        var refused = await Should.ThrowAsync<PostgresException>(() => select.ExecuteScalarAsync(token));
        refused.SqlState.ShouldBe(PostgresErrorCodes.InFailedSqlTransaction);
        rows.ShouldBe(10);
    }

    [Fact]
    public async Task StreamNumbers_MethodToken_Cancels()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
        var stream = connection.StreamNumbers(new StreamNumbersArgs(ManyRows), cancellationToken: cancellation.Token);

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
            new StreamNumbersArgs(ManyRows),
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

    // StreamNoteAccounts is a query of Notes.sql, which converts account_id to an AccountId through the converters of
    // the type, and gives this one its own collection type: its method is in NoteQueriesExtensions.
    [Fact]
    public async Task StreamNoteAccounts_ConvertedColumn_YieldsAccountIds()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var streamed = await connection.StreamNoteAccounts(cancellationToken: token).ToListAsync(token);

        streamed.ShouldBe([new StreamNoteAccountsDto(new AccountId(1)), new StreamNoteAccountsDto(new AccountId(2))]);
    }
}
