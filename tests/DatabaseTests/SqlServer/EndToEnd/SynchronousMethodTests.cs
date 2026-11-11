using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.DatabaseTests.SqlServer.EndToEnd.Types;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The synchronous methods of the copies under method-type=sync of Queries/Methods.sql, Lists.sql and Notes.sql, run on
// the seed of "app" as MethodShapeTests and RunTimeArgumentTests run their originals' asynchronous methods: each shape
// with the rows it allows and the counts it refuses, on an open connection, which the method leaves open, and a closed
// one, which it opens and closes again whether it returns or throws; the error after the row; the caller's transaction,
// which SqlClient requires, and its rollback; a command timeout, which throws what the asynchronous method throws; the
// rollback checks, a later error that rolls the transaction back and a transaction that has completed; a list, a token
// and a conversion.  Then the copies under method-type=both: a query's two methods read the same row, and a stream has
// no synchronous method.  A test that writes runs in a transaction that is rolled back, or touches no row.  A test that
// needs UnpooledTransaction or LockOfBob, whose helpers are asynchronous, is asynchronous, and runs the synchronous
// method on the thread pool, since an asynchronous test may not block on it.
[Collection(UsesSqlServer.Name)]
public sealed class SynchronousMethodTests(SqlServerDatabases sqlServer)
{
    // The number of SQL Server's error "Divide by zero error encountered.".
    private const int DivideByZero = 8134;

    private const BindingFlags Statics = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    [Fact]
    public void TouchAccountSync_OneRowInATransaction_Returns1()
    {
        using var connection = EndToEndSources.App(sqlServer);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var count = connection.TouchAccountSync(new TouchAccountSyncArgs(1), transaction);

        count.ShouldBe(1);
        connection.State.ShouldBe(ConnectionState.Open);
        transaction.Rollback();
    }

    // No account has the id, so the update writes nothing, with no transaction to roll back.
    [Fact]
    public void TouchAccountSync_NoRowOnAClosedConnection_Returns0AndIsClosedAgain()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var count = connection.TouchAccountSync(new TouchAccountSyncArgs(99));

        count.ShouldBe(0);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The write is seen by the transaction's own read, and the rollback puts back the status it replaced.
    [Fact]
    public void SetAccountStatusSync_GivenATransaction_IsUndoneByItsRollback()
    {
        using var connection = EndToEndSources.App(sqlServer);
        connection.Open();

        using (var transaction = connection.BeginTransaction())
        {
            connection.SetAccountStatusSync(new SetAccountStatusSyncArgs("suspended", 1), transaction);

            StatusOfAlice(connection, transaction).ShouldBe("suspended");
            connection.State.ShouldBe(ConnectionState.Open);
            transaction.Rollback();
        }

        using var other = EndToEndSources.App(sqlServer);
        StatusOfAlice(other, null).ShouldBe("active");
    }

    // No account has the id, so the update writes nothing, with no transaction to roll back.
    [Fact]
    public void SetAccountStatusSync_NoRowOnAClosedConnection_IsClosedAgain()
    {
        using var connection = EndToEndSources.App(sqlServer);

        connection.SetAccountStatusSync(new SetAccountStatusSyncArgs("suspended", 99));

        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void GetAccountInRangeSync_OneRowOnAClosedConnection_ReadsItAndIsClosedAgain()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var account = connection.GetAccountInRangeSync(new GetAccountInRangeSyncArgs(2, 2));

        account.ShouldBe(new GetAccountInRangeSyncDto(2, "bob"));
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void GetAccountInRangeSync_OneRowOnAnOpenConnection_ReadsItAndIsLeftOpen()
    {
        using var connection = EndToEndSources.App(sqlServer);
        connection.Open();

        var account = connection.GetAccountInRangeSync(new GetAccountInRangeSyncArgs(3, 4));

        account.ShouldBe(new GetAccountInRangeSyncDto(3, "carol"));
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public void GetAccountInRangeSync_NoRowOnAClosedConnection_ThrowsUnexpectedRowCountAndIsClosedAgain()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var thrown = Should.Throw<UnexpectedRowCountException>(() =>
            connection.GetAccountInRangeSync(new GetAccountInRangeSyncArgs(4, 5))
        );

        thrown.QueryName.ShouldBe("GetAccountInRangeSync");
        thrown.Message.ShouldBe("The query 'GetAccountInRangeSync' returned no rows.");
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void GetAccountInRangeSync_TwoRowsOnAnOpenConnection_ThrowsUnexpectedRowCountAndIsLeftOpen()
    {
        using var connection = EndToEndSources.App(sqlServer);
        connection.Open();

        var thrown = Should.Throw<UnexpectedRowCountException>(() =>
            connection.GetAccountInRangeSync(new GetAccountInRangeSyncArgs(1, 2))
        );

        thrown.QueryName.ShouldBe("GetAccountInRangeSync");
        thrown.Message.ShouldBe("The query 'GetAccountInRangeSync' returned more than one row.");
        connection.State.ShouldBe(ConnectionState.Open);
    }

    // SqlClient refuses a command on a connection whose transaction it is not given, with the message of its
    // synchronous ExecuteReader, which the asynchronous method's test reads from ExecuteReaderAsync.
    [Fact]
    public void GetAccountInRangeSync_NotGivenTheConnectionsTransaction_ThrowsSqlClientsError()
    {
        using var connection = EndToEndSources.App(sqlServer);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var thrown = Should.Throw<InvalidOperationException>(() =>
            connection.GetAccountInRangeSync(new GetAccountInRangeSyncArgs(2, 2))
        );

        thrown.Message.ShouldContain("ExecuteReader requires the command to have a transaction");
        connection.State.ShouldBe(ConnectionState.Open);
        transaction.Rollback();
    }

    [Fact]
    public void FindAccountInRangeSync_OneRowOnAnOpenConnection_ReadsItAndIsLeftOpen()
    {
        using var connection = EndToEndSources.App(sqlServer);
        connection.Open();

        var account = connection.FindAccountInRangeSync(new FindAccountInRangeSyncArgs(3, 4));

        account.ShouldBe(new FindAccountInRangeSyncDto(3, "carol"));
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public void FindAccountInRangeSync_NoRowOnAClosedConnection_ReturnsNullAndIsClosedAgain()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var account = connection.FindAccountInRangeSync(new FindAccountInRangeSyncArgs(4, 5));

        account.ShouldBeNull();
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void FindAccountInRangeSync_TwoRowsOnAClosedConnection_ThrowsUnexpectedRowCountAndIsClosedAgain()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var thrown = Should.Throw<UnexpectedRowCountException>(() =>
            connection.FindAccountInRangeSync(new FindAccountInRangeSyncArgs(2, 3))
        );

        thrown.QueryName.ShouldBe("FindAccountInRangeSync");
        thrown.Message.ShouldBe("The query 'FindAccountInRangeSync' returned more than one row.");
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The token's default filters by the parameter.
    [Fact]
    public void ListAccountsWhereSync_TheDefaultOnAClosedConnection_UsesTheParameterAndIsClosedAgain()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var accounts = connection.ListAccountsWhereSync(new ListAccountsWhereSyncArgs(2), "WHERE id <= @lastId");

        accounts.ShouldBe([new ListAccountsWhereSyncDto(1, "alice"), new ListAccountsWhereSyncDto(2, "bob")]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The declared @lastId is sent all the same, and SQL Server accepts a parameter that the text does not use.
    [Fact]
    public void ListAccountsWhereSync_AnotherFragmentOnAnOpenConnection_LeavesTheParameterUnusedAndIsLeftOpen()
    {
        using var connection = EndToEndSources.App(sqlServer);
        connection.Open();

        var accounts = connection.ListAccountsWhereSync(new ListAccountsWhereSyncArgs(1), "WHERE status = 'suspended'");

        accounts.ShouldBe([new ListAccountsWhereSyncDto(3, "carol")]);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    // The method reads the results after its second read, so the THROW after the row is thrown, and the mark shows
    // that the batch ran as far as it.
    [Fact]
    public void GetThenRaiseSync_ErrorAfterTheRowOnAnOpenConnection_ThrowsTheErrorAndIsLeftOpen()
    {
        using var connection = EndToEndSources.App(sqlServer);
        connection.Open();
        SessionMark.Clear(connection);

        var thrown = Should.Throw<SqlException>(() => connection.GetThenRaiseSync());

        thrown.Number.ShouldBe(50000);
        thrown.Message.ShouldBe("after the rows");
        SessionMark.Read(connection).ShouldBe(5);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public void GetThenRaiseSync_ErrorAfterTheRowOnAClosedConnection_ThrowsTheErrorAndIsClosedAgain()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var thrown = Should.Throw<SqlException>(() => connection.GetThenRaiseSync());

        thrown.Number.ShouldBe(50000);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The second row makes one throw its refusal, and the reader's disposal reads the failed conversion after the
    // rows, which with XACT_ABORT off rolls back the transaction: the method throws the rollback, with the refusal
    // inside it, as GetTwoThenFailConversion's asynchronous method does.
    [Fact]
    public async Task GetTwoThenFailConversionSync_SecondRowInATransaction_ThrowsTheRollbackWithTheRowCountInside()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);

        var thrown = await Should.ThrowAsync<TransactionRolledBackException>(() =>
            Task.Run(() => session.Connection.GetTwoThenFailConversionSync(session.Transaction), token)
        );

        thrown.QueryName.ShouldBe("GetTwoThenFailConversionSync");
        thrown.Message.ShouldBe(
            "The transaction was rolled back by an error that the query 'GetTwoThenFailConversionSync' raised after "
                + "the inner exception was thrown."
        );
        var inner = thrown.InnerException.ShouldBeOfType<UnexpectedRowCountException>();
        inner.Message.ShouldBe("The query 'GetTwoThenFailConversionSync' returned more than one row.");
        await session.ShouldHaveRolledBackAsync(token);
    }

    // The divide by zero at the second row leaves the transaction open, and the method reads the rest of the batch,
    // whose failed conversion rolls it back: the method throws the rollback, with the divide by zero inside it, as
    // ListFailingAtSecondThenFailConversion's asynchronous method does.
    [Fact]
    public async Task ListFailingAtSecondThenFailConversionSync_InATransaction_ThrowsTheRollbackWithTheDivideByZero()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);

        var thrown = await Should.ThrowAsync<TransactionRolledBackException>(() =>
            Task.Run(() => session.Connection.ListFailingAtSecondThenFailConversionSync(session.Transaction), token)
        );

        thrown.QueryName.ShouldBe("ListFailingAtSecondThenFailConversionSync");
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DivideByZero);
        await session.ShouldHaveRolledBackAsync(token);
    }

    // A none method given a transaction that has committed is refused before anything runs: the mark that its batch
    // sets on the session is not there, and the error after it is not raised.
    [Fact]
    public async Task RunThenRaiseSync_CommittedTransaction_IsRefusedAndRunsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);
        await session.ShouldCommitAsync(token);
        await SessionMark.ClearAsync(session.Connection, token);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            Task.Run(() => session.Connection.RunThenRaiseSync(session.Transaction), token)
        );

        thrown.GetType().ShouldBe(typeof(InvalidOperationException));
        thrown.Message.ShouldBe(
            "The transaction given to the query 'RunThenRaiseSync' has completed; it is no longer usable."
        );
        (await SessionMark.ReadAsync(session.Connection, token)).ShouldBe(0);
    }

    // Both updates of bob wait on the lock of his row, which outlasts the test, until the command timeout ends each:
    // the synchronous method throws what the asynchronous one does, SqlClient's timeout with its number and its inner
    // exception.  The connections have no command timeout of their own, so only the call's ends the wait, and they are
    // disposed after the lock, so that a call that outlasts the wait ends before its connection does.
    [Fact]
    public async Task TouchAccountSync_RowLockedPastACommandTimeoutOfOneSecond_ThrowsWhatTheAsyncMethodThrows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var asyncConnection = EndToEndSources.AppWithoutCommandTimeout(sqlServer);
        await using var syncConnection = EndToEndSources.AppWithoutCommandTimeout(sqlServer);
        await using var lockOfBob = await RunTimeArgumentTests.LockOfBob.TakeAsync(sqlServer, token);

        var expected = await Should.ThrowAsync<SqlException>(() =>
            Eventually.WithinAsync(
                asyncConnection.TouchAccount(new TouchAccountArgs(2), commandTimeout: 1, cancellationToken: token),
                "The asynchronous update outlasted its command timeout of one second."
            )
        );
        var thrown = await Should.ThrowAsync<SqlException>(() =>
            Eventually.WithinAsync(
                Task.Run(() => syncConnection.TouchAccountSync(new TouchAccountSyncArgs(2), commandTimeout: 1), token),
                "The synchronous update outlasted its command timeout of one second."
            )
        );

        thrown.GetType().ShouldBe(expected.GetType());
        thrown.Number.ShouldBe(-2);
        thrown.Number.ShouldBe(expected.Number);
        thrown.Message.ShouldStartWith("Execution Timeout Expired.");
        expected.Message.ShouldStartWith("Execution Timeout Expired.");
        thrown.InnerException?.GetType().ShouldBe(expected.InnerException?.GetType());
        syncConnection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void ListAccountsByIdsSync_TwoIds_FindsTheRows()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var accounts = connection.ListAccountsByIdsSync(new ListAccountsByIdsSyncArgs([1, 3]));

        accounts.ShouldBe([new ListedAccount(1, "alice", "active"), new ListedAccount(3, "carol", "suspended")]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The parameter is sent as the int that AccountId's ToDatabase returns, and the count read through NoteCount's
    // FromDatabase.
    [Fact]
    public void GetAccountNoteCountSync_AnAccountId_ReadsItsNoteCount()
    {
        using var connection = EndToEndSources.App(sqlServer);
        connection.Open();

        var count = connection.GetAccountNoteCountSync(new GetAccountNoteCountSyncArgs(new AccountId(1)));

        count.ShouldBe(new GetAccountNoteCountSyncDto(new AccountId(1), new NoteCount(2)));
    }

    // The synchronous call runs on the thread pool, since an asynchronous test may not block on it.
    [Fact]
    public async Task GetAccountInRangeBoth_BesideItsAsyncMethod_ReadsTheSameRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var arguments = new GetAccountInRangeBothArgs(2, 2);

        var synchronous = await Task.Run(() => connection.GetAccountInRangeBoth(arguments), token);
        var asynchronous = await connection.GetAccountInRangeBothAsync(arguments, cancellationToken: token);

        synchronous.ShouldBe(new GetAccountInRangeBothDto(2, "bob"));
        asynchronous.ShouldBe(synchronous);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The methods are internal, the project's default visibility, so the lookup takes the non-public ones too, and
    // finds the asynchronous method that it does not find a synchronous twin beside.
    [Fact]
    public void StreamAccountsBoth_UnderBoth_HasNoSynchronousMethod()
    {
        typeof(StreamingQueriesExtensions).GetMethod("StreamAccountsBoth", Statics).ShouldBeNull();
        _ = typeof(StreamingQueriesExtensions).GetMethod("StreamAccountsBothAsync", Statics).ShouldNotBeNull();
    }

    [Fact]
    public async Task StreamAccountsBothAsync_EveryAccount_YieldsEveryRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var streamed = await connection.StreamAccountsBothAsync(cancellationToken: token).ToListAsync(token);

        streamed.ShouldBe(StreamingRows.Accounts);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    private static string? StatusOfAlice(SqlConnection connection, IDbTransaction? transaction) =>
        connection.ExecuteScalar<string>("SELECT status FROM dbo.accounts WHERE id = 1", transaction: transaction);
}
