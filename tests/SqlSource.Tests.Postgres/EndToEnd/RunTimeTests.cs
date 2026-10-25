using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// What a method does with the arguments given at run time, the transaction, the command's timeout and the token, and
// with the connection: it opens a closed connection and closes it again, whether it returns or throws, and leaves an
// open one open, in its transaction.  Each test reads the connection's state after the call.  The queries that wait are
// Sleep and ListThenSleep of Queries/Methods.sql, and each sleeps for far longer than the test waits for it.
[Collection(UsesPostgres.Name)]
public sealed class RunTimeTests(PostgresContainer postgres)
{
    // Far longer than a test waits: a test that sees the sleep end has failed.
    private const int LongSleep = 30;

    // Enough rows to fill the server's send buffer many times over: it sends each buffer as it fills, so the client has
    // all but the last when the last row sleeps, on 14 as on 18, and the rest come with the answer to a cancel.
    private const int RowsBeforeTheSleep = 10_000;

    // The write is seen by the transaction's own read, and the rollback puts back the version of the row it replaced.
    [Fact]
    public async Task TouchAccount_InATransaction_IsUndoneByItsRollback()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        var before = await XminOfAsync(connection, null, 1, token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var count = await connection.TouchAccount(new TouchAccountArgs(1), transaction, cancellationToken: token);
        var during = await XminOfAsync(connection, transaction, 1, token);
        await transaction.RollbackAsync(token);

        count.ShouldBe(1);
        during.ShouldNotBe(before);
        connection.State.ShouldBe(ConnectionState.Open);
        (await XminOfAsync(connection, null, 1, token)).ShouldBe(before);
    }

    // Npgsql cancels the command when its timeout passes, and throws.
    [Fact]
    public async Task Sleep_CommandTimeoutShorterThanTheSleep_Throws()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var exception = await Should.ThrowAsync<NpgsqlException>(() =>
            connection.Sleep(new SleepArgs(LongSleep), commandTimeout: 1, cancellationToken: token)
        );

        _ = exception.InnerException.ShouldBeOfType<TimeoutException>();
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public async Task Sleep_TokenCancelledBeforeTheCall_ThrowsOperationCanceled()
    {
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            connection.Sleep(new SleepArgs(LongSleep), cancellationToken: new CancellationToken(canceled: true))
        );

        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The token is cancelled once the server sleeps before the last row, when the method has read most of the rows
    // before it and waits in the read for the next.
    [Fact]
    public async Task ListThenSleep_TokenCancelledDuringTheRead_ThrowsOperationCanceled()
    {
        var token = TestContext.Current.CancellationToken;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
        await using var source = EndToEndSources.App(postgres);
        await using var watch = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            var run = connection.ListThenSleep(
                new ListThenSleepArgs(RowsBeforeTheSleep, LongSleep),
                cancellationToken: cancel.Token
            );
            await Eventually.TrueAsync(
                async () => await IsSleepingBeforeTheLastRowAsync(watch, token),
                "the query never reached the sleep before its last row"
            );
            await cancel.CancelAsync();
            _ = await run;
        });

        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public async Task GetAccount_ClosedConnection_IsClosedAgain()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.GetAccount(new GetAccountArgs(1), cancellationToken: token);

        accounts.ShouldBe([new GetAccountDto("alice", 1, "alice@example.com")]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public async Task GetAccountInRange_ClosedConnectionAndAnException_IsClosedAgain()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        _ = await Should.ThrowAsync<UnexpectedRowCountException>(() =>
            connection.GetAccountInRange(new GetAccountInRangeArgs(1, 3), cancellationToken: token)
        );

        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public async Task GetAccount_OpenConnection_IsLeftOpen()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var accounts = await connection.GetAccount(new GetAccountArgs(3), cancellationToken: token);

        accounts.ShouldBe([new GetAccountDto("carol", 3, "carol@example.com")]);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    // The transaction is still the connection's after the call, and rolls back.
    [Fact]
    public async Task GetAccount_OpenConnectionInATransaction_IsLeftOpenWithTheTransaction()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var accounts = await connection.GetAccount(new GetAccountArgs(2), transaction, cancellationToken: token);

        accounts.ShouldBe([new GetAccountDto("bob", 2, "bob@example.com")]);
        connection.State.ShouldBe(ConnectionState.Open);
        transaction.Connection.ShouldBeSameAs(connection);
        await transaction.RollbackAsync(token);
    }

    // The model calls the email not null, and a null given anyway is sent as NULL, which equals no row's email.
    [Fact]
    public async Task FindAccountName_NullEmail_SendsNullAndFindsNoRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var names = await connection.FindAccountName(new FindAccountNameArgs(null!), cancellationToken: token);

        names.ShouldBeEmpty();
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // Npgsql writes a timestamptz at offset zero alone, and refuses another offset before the command is sent.
    [Fact]
    public async Task ListClosedBefore_OffsetNotZero_ThrowsBeforeSending()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            connection.ListClosedBefore(
                new ListClosedBeforeArgs(new DateTimeOffset(2024, 10, 1, 0, 0, 0, TimeSpan.FromHours(2))),
                cancellationToken: token
            )
        );

        exception.Message.ShouldContain("Offset=02:00:00");
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The xmin of the account's row that the read sees: the id of the transaction that wrote that version of it.
    private static async Task<string> XminOfAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int accountId,
        CancellationToken token
    )
    {
        await using var command = new NpgsqlCommand(
            "SELECT xmin::text FROM accounts WHERE id = $1",
            connection,
            transaction
        );
        _ = command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = accountId });
        return (string)(await command.ExecuteScalarAsync(token))!;
    }

    // Whether a backend of "app" is in ListThenSleep's sleep, which it reaches only after the rows before the last.
    // The tests of the collection run one at a time, so no other backend runs the query.
    private static async Task<bool> IsSleepingBeforeTheLastRowAsync(NpgsqlDataSource watch, CancellationToken token)
    {
        await using var command = watch.CreateCommand(
            """
            SELECT count(*) > 0
            FROM pg_catalog.pg_stat_activity
            WHERE datname = $1 AND wait_event = 'PgSleep' AND query LIKE '%generate_series%'
            """
        );
        _ = command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = PostgresContainer.AppDatabase });
        return (bool)(await command.ExecuteScalarAsync(token))!;
    }
}
