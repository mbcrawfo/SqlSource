using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.DatabaseTests.Postgres.EndToEnd.Types;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The synchronous methods of the copies under method-type=sync of Queries/Methods.sql, Lists.sql and Notes.sql, run on
// the seed of "app" as MethodQueryTests and RunTimeTests run their originals' asynchronous methods: each shape with the
// rows it allows and the counts it refuses, on an open connection, which the method leaves open, and a closed one,
// which it opens and closes again whether it returns or throws; the error after the row; the caller's transaction and
// its rollback; a command timeout, which throws what the asynchronous method throws; a list, a token and a conversion.
// Then the copies under method-type=both: a query's two methods read the same row, and a stream has no synchronous
// method.  A test that writes runs in a transaction that is rolled back, or touches no row.
[Collection(UsesPostgres.Name)]
public sealed class SynchronousMethodTests(PostgresDatabases postgres)
{
    // The account whose row the timeout test holds, as RunTimeTests holds it.
    private const int LockedAccountId = 2;

    [Fact]
    public void TouchAccountSync_OneRowInATransaction_Returns1()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var count = connection.TouchAccountSync(new TouchAccountSyncArgs(1), transaction);

        count.ShouldBe(1);
        UpdatedInThisTransaction(connection, transaction, 1).ShouldBeTrue();
        connection.State.ShouldBe(ConnectionState.Open);
        transaction.Rollback();
    }

    [Fact]
    public void TouchAccountSync_NoRowOnAClosedConnection_Returns0AndIsClosedAgain()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var count = connection.TouchAccountSync(new TouchAccountSyncArgs(99));

        count.ShouldBe(0);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void TouchAccountQuietlySync_InATransaction_UpdatesTheRow()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.OpenConnection();
        using var transaction = connection.BeginTransaction();

        connection.TouchAccountQuietlySync(new TouchAccountQuietlySyncArgs(2), transaction);

        UpdatedInThisTransaction(connection, transaction, 2).ShouldBeTrue();
        connection.State.ShouldBe(ConnectionState.Open);
        transaction.Rollback();
    }

    [Fact]
    public void TouchAccountQuietlySync_NoRowOnAClosedConnection_IsClosedAgain()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        connection.TouchAccountQuietlySync(new TouchAccountQuietlySyncArgs(99));

        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void GetAccountInRangeSync_OneRowOnAClosedConnection_ReadsItAndIsClosedAgain()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var account = connection.GetAccountInRangeSync(new GetAccountInRangeSyncArgs(2, 2));

        account.ShouldBe(new GetAccountInRangeSyncDto(2, "bob"));
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void GetAccountInRangeSync_OneRowOnAnOpenConnection_ReadsItAndIsLeftOpen()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.OpenConnection();

        var account = connection.GetAccountInRangeSync(new GetAccountInRangeSyncArgs(3, 5));

        account.ShouldBe(new GetAccountInRangeSyncDto(3, "carol"));
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public void GetAccountInRangeSync_NoRowOnAClosedConnection_ThrowsUnexpectedRowCountAndIsClosedAgain()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var exception = Should.Throw<UnexpectedRowCountException>(() =>
            connection.GetAccountInRangeSync(new GetAccountInRangeSyncArgs(4, 9))
        );

        exception.QueryName.ShouldBe("GetAccountInRangeSync");
        exception.Message.ShouldBe("The query 'GetAccountInRangeSync' returned no rows.");
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void GetAccountInRangeSync_TwoRowsOnAnOpenConnection_ThrowsUnexpectedRowCountAndIsLeftOpen()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.OpenConnection();

        var exception = Should.Throw<UnexpectedRowCountException>(() =>
            connection.GetAccountInRangeSync(new GetAccountInRangeSyncArgs(1, 2))
        );

        exception.QueryName.ShouldBe("GetAccountInRangeSync");
        exception.Message.ShouldBe("The query 'GetAccountInRangeSync' returned more than one row.");
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public void FindAccountInRangeSync_OneRowOnAnOpenConnection_ReadsItAndIsLeftOpen()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.OpenConnection();

        var account = connection.FindAccountInRangeSync(new FindAccountInRangeSyncArgs(3, 5));

        account.ShouldBe(new FindAccountInRangeSyncDto(3, "carol"));
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public void FindAccountInRangeSync_NoRowOnAClosedConnection_ReturnsNullAndIsClosedAgain()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var account = connection.FindAccountInRangeSync(new FindAccountInRangeSyncArgs(4, 9));

        account.ShouldBeNull();
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void FindAccountInRangeSync_TwoRowsOnAClosedConnection_ThrowsUnexpectedRowCountAndIsClosedAgain()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var exception = Should.Throw<UnexpectedRowCountException>(() =>
            connection.FindAccountInRangeSync(new FindAccountInRangeSyncArgs(2, 3))
        );

        exception.QueryName.ShouldBe("FindAccountInRangeSync");
        exception.Message.ShouldBe("The query 'FindAccountInRangeSync' returned more than one row.");
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The token's sample filters by the parameter.
    [Fact]
    public void ListAccountsWhereSync_SampleFilterOnAClosedConnection_ReadsTheAccountAndIsClosedAgain()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var accounts = connection.ListAccountsWhereSync(new ListAccountsWhereSyncArgs(2), "id = @accountId");

        accounts.ShouldBe([new ListAccountsWhereSyncDto(2, "bob")]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // Another fragment leaves out the parameter, which the method still sends, and the server never reads.
    [Fact]
    public void ListAccountsWhereSync_AnotherFilterOnAnOpenConnection_ReadsItsRowsAndIsLeftOpen()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.OpenConnection();

        var accounts = connection.ListAccountsWhereSync(new ListAccountsWhereSyncArgs(2), "status <> 'active'");

        accounts.ShouldBe([new ListAccountsWhereSyncDto(2, "bob"), new ListAccountsWhereSyncDto(3, "carol")]);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    // The division by zero after the row is thrown by the second read, which looks for a second row.
    [Fact]
    public void GetThenRaiseSync_ErrorAfterTheRowOnAClosedConnection_ThrowsTheErrorAndIsClosedAgain()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var thrown = Should.Throw<PostgresException>(() => connection.GetThenRaiseSync());

        thrown.SqlState.ShouldBe(PostgresErrorCodes.DivisionByZero);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The write is seen by the transaction's own read, and the rollback puts back the version of the row it replaced.
    [Fact]
    public void TouchAccountSync_InATransaction_IsUndoneByItsRollback()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.OpenConnection();
        var before = XminOf(connection, null, 1);
        using var transaction = connection.BeginTransaction();

        var count = connection.TouchAccountSync(new TouchAccountSyncArgs(1), transaction);
        var during = XminOf(connection, transaction, 1);
        transaction.Rollback();

        count.ShouldBe(1);
        during.ShouldNotBe(before);
        connection.State.ShouldBe(ConnectionState.Open);
        XminOf(connection, null, 1).ShouldBe(before);
    }

    // Both updates wait on the row's lock, which outlasts the test, and Npgsql cancels each command when its timeout
    // passes: the synchronous method throws what the asynchronous one does, with the same inner exception and SQL
    // state.  The source has no command timeout of its own, so only the call's ends the wait.  The synchronous call
    // runs on the thread pool, so that the test's wait for it has a limit.  The connections are disposed after the
    // lock, so that a call that outlasts the wait ends before its connection does.
    [Fact]
    public async Task TouchAccountSync_RowLockedPastACommandTimeoutOfOneSecond_ThrowsWhatTheAsyncMethodThrows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.AppWithoutCommandTimeout(postgres);
        await using var asyncConnection = source.CreateConnection();
        await using var syncConnection = source.CreateConnection();
        await using var holder = await source.OpenConnectionAsync(token);
        await using var rowLock = await RunTimeTests.HoldAsync(
            holder,
            "SELECT 1 FROM accounts WHERE id = $1 FOR UPDATE",
            LockedAccountId,
            token
        );

        var expected = await Should.ThrowAsync<NpgsqlException>(() =>
            Eventually.WithinAsync(
                asyncConnection.TouchAccount(
                    new TouchAccountArgs(LockedAccountId),
                    commandTimeout: 1,
                    cancellationToken: token
                ),
                "the asynchronous update outlasted its command timeout of one second"
            )
        );
        var thrown = await Should.ThrowAsync<NpgsqlException>(() =>
            Eventually.WithinAsync(
                Task.Run(
                    () => syncConnection.TouchAccountSync(new TouchAccountSyncArgs(LockedAccountId), commandTimeout: 1),
                    token
                ),
                "the synchronous update outlasted its command timeout of one second"
            )
        );

        thrown.GetType().ShouldBe(expected.GetType());
        thrown.SqlState.ShouldBe(expected.SqlState);
        _ = thrown.InnerException.ShouldBeOfType<TimeoutException>();
        _ = expected.InnerException.ShouldBeOfType<TimeoutException>();
        syncConnection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void ListAccountsByIdsSync_TwoIds_FindsTheRows()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var accounts = connection.ListAccountsByIdsSync(new ListAccountsByIdsSyncArgs([1, 3]));

        accounts.ShouldBe([new ListedAccount(1, "alice", "active"), new ListedAccount(3, "carol", "suspended")]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The parameter is sent as the int that AccountId's ToDatabase returns, and the count read through NoteCount's
    // FromDatabase.
    [Fact]
    public void GetAccountNoteCountSync_AnAccountId_ReadsItsNoteCount()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.OpenConnection();

        var count = connection.GetAccountNoteCountSync(new GetAccountNoteCountSyncArgs(new AccountId(1)));

        count.ShouldBe(new GetAccountNoteCountSyncDto(new AccountId(1), new NoteCount(2)));
    }

    // The synchronous call runs on the thread pool, since an asynchronous test may not block on it.
    [Fact]
    public async Task GetAccountInRangeBoth_BesideItsAsyncMethod_ReadsTheSameRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
        var arguments = new GetAccountInRangeBothArgs(2, 2);

        var synchronous = await Task.Run(() => connection.GetAccountInRangeBoth(arguments), token);
        var asynchronous = await connection.GetAccountInRangeBothAsync(arguments, cancellationToken: token);

        synchronous.ShouldBe(new GetAccountInRangeBothDto(2, "bob"));
        asynchronous.ShouldBe(synchronous);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void StreamAccountsBoth_UnderBoth_HasNoSynchronousMethod() =>
        typeof(StreamingQueriesExtensions).GetMethod("StreamAccountsBoth").ShouldBeNull();

    [Fact]
    public async Task StreamAccountsBothAsync_EveryAccount_YieldsEveryRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var streamed = await connection.StreamAccountsBothAsync(cancellationToken: token).ToListAsync(token);

        streamed.ShouldBe([
            new CollectedAccount(1, "alice"),
            new CollectedAccount(2, "bob"),
            new CollectedAccount(3, "carol"),
        ]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // Whether the transaction wrote the account's row, as MethodQueryTests reads it.
    private static bool UpdatedInThisTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction, int id)
    {
        using var command = new NpgsqlCommand(
            "SELECT xmin::text::bigint = pg_current_xact_id()::text::bigint % 4294967296 FROM accounts WHERE id = $1",
            connection,
            transaction
        );
        _ = command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = id });
        return (bool)command.ExecuteScalar()!;
    }

    // The xmin of the account's row that the read sees, as RunTimeTests reads it.
    private static string XminOf(NpgsqlConnection connection, NpgsqlTransaction? transaction, int id)
    {
        using var command = new NpgsqlCommand("SELECT xmin::text FROM accounts WHERE id = $1", connection, transaction);
        _ = command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = id });
        return (string)command.ExecuteScalar()!;
    }
}
