using System;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Tests.SqlServer.EndToEnd.Types;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The run-time arguments of the generated methods on SQL Server, through MethodQueries' methods on the seed of "app":
// the transaction, which SqlClient requires of a command on a connection that has one, commandTimeout, and the
// exception of a token cancelled before the call, while the command runs and during the read, each passed on as
// SqlClient raises it.  Then the connection rule: the method opens a closed connection and closes it again, however
// it ends, and leaves an open one open, with its transaction.
[Collection(UsesSqlServer.Name)]
public sealed class RunTimeArgumentTests(SqlServerContainer sqlServer)
{
    // How a call of the connection rule's tests ends.
    public enum Outcome
    {
        Returns,
        Throws,
        IsCancelled,
    }

    [Fact]
    public async Task SetAccountStatus_GivenATransaction_IsUndoneByItsRollback()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        var token = TestContext.Current.CancellationToken;
        await connection.OpenAsync(token);

        await using (var transaction = await connection.BeginTransactionAsync(token))
        {
            await connection.SetAccountStatus(
                new SetAccountStatusArgs("suspended", 1),
                transaction: transaction,
                cancellationToken: token
            );

            (await StatusOfAliceAsync(connection, transaction)).ShouldBe("suspended");
            await transaction.RollbackAsync(token);
        }

        await using var other = EndToEndSources.App(sqlServer);
        (await StatusOfAliceAsync(other, null)).ShouldBe("active");
    }

    [Fact]
    public async Task GetAccountInRange_NotGivenTheConnectionsTransaction_ThrowsSqlClientsError()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        var token = TestContext.Current.CancellationToken;

        await EndToEndSources.RolledBackAsync(
            connection,
            async _ =>
            {
                var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
                    connection.GetAccountInRange(new GetAccountInRangeArgs(2, 2), cancellationToken: token)
                );

                thrown.Message.ShouldContain("ExecuteReader requires the command to have a transaction");
            },
            token
        );
    }

    [Fact]
    public async Task TouchAccount_NotGivenTheConnectionsTransaction_ThrowsSqlClientsError()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        var token = TestContext.Current.CancellationToken;

        await EndToEndSources.RolledBackAsync(
            connection,
            async _ =>
            {
                var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
                    connection.TouchAccount(new TouchAccountArgs(1), cancellationToken: token)
                );

                thrown.Message.ShouldContain("ExecuteNonQuery requires the command to have a transaction");
            },
            token
        );
    }

    // The method's update of bob waits on the row's lock until the command timeout ends it.
    [Fact]
    public async Task TouchAccount_RowLockedPastACommandTimeoutOfOneSecond_ThrowsTheTimeout()
    {
        var token = TestContext.Current.CancellationToken;
        await using var lockOfBob = await LockOfBob.TakeAsync(sqlServer, token);
        await using var connection = EndToEndSources.App(sqlServer);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            connection.TouchAccount(new TouchAccountArgs(2), commandTimeout: 1, cancellationToken: token)
        );

        thrown.Number.ShouldBe(-2);
        thrown.Message.ShouldStartWith("Execution Timeout Expired.");
    }

    // SqlClient sends the server an attention, and raises the server's error, not an OperationCanceledException:
    // CancelledWhileItRunsAsync asserts it.
    [Fact]
    public async Task TouchAccount_TokenCancelledWhileItWaitsOnALock_ThrowsSqlException()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await CancelledWhileItRunsAsync(connection, null);
    }

    // On a closed connection the method's OpenAsync sees the token, and on an open one its ExecuteReaderAsync.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAccountInRange_TokenCancelledBeforeTheCall_ThrowsTaskCanceledException(bool open)
    {
        await using var connection = EndToEndSources.App(sqlServer);
        if (open)
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
        }

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<TaskCanceledException>(() =>
            connection.GetAccountInRange(new GetAccountInRangeArgs(2, 2), cancellationToken: cancellation.Token)
        );

        connection.State.ShouldBe(open ? ConnectionState.Open : ConnectionState.Closed);
    }

    // The converter of CancelOnRead cancels the token as the first row is read, and the ReadAsync of the second row
    // throws.  The method opened the connection, and closes it again.
    [Fact]
    public async Task ListAccountIdsCancellingOnRead_TokenCancelledDuringTheRead_ThrowsTaskCanceledException()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        CancelOnRead.Source = cancellation;
        try
        {
            _ = await Should.ThrowAsync<TaskCanceledException>(() =>
                connection.ListAccountIdsCancellingOnRead(cancellationToken: cancellation.Token)
            );

            cancellation.IsCancellationRequested.ShouldBeTrue();
            connection.State.ShouldBe(ConnectionState.Closed);
        }
        finally
        {
            CancelOnRead.Source = null;
        }
    }

    // The connection is of a pool of its own, so that a cancel can find the session that the method opens, and the
    // pool is cleared at the end, so that no idle session of it lingers.
    [Theory]
    [InlineData(Outcome.Returns)]
    [InlineData(Outcome.Throws)]
    [InlineData(Outcome.IsCancelled)]
    public async Task ClosedConnection_AfterTheMethod_IsClosed(Outcome outcome)
    {
        await using var connection = OfItsOwnPool();
        try
        {
            await CallAsync(connection, outcome, null);

            connection.State.ShouldBe(ConnectionState.Closed);
        }
        finally
        {
            SqlConnection.ClearPool(connection);
        }
    }

    [Theory]
    [InlineData(Outcome.Returns)]
    [InlineData(Outcome.Throws)]
    [InlineData(Outcome.IsCancelled)]
    public async Task OpenConnection_AfterTheMethod_IsLeftOpen(Outcome outcome)
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await CallAsync(connection, outcome, null);

        connection.State.ShouldBe(ConnectionState.Open);
        (await connection.ExecuteScalarAsync<int>("SELECT 1")).ShouldBe(1);
    }

    [Theory]
    [InlineData(Outcome.Returns)]
    [InlineData(Outcome.Throws)]
    [InlineData(Outcome.IsCancelled)]
    public async Task OpenConnectionInATransaction_AfterTheMethod_IsLeftOpenInTheTransaction(Outcome outcome)
    {
        await using var connection = EndToEndSources.App(sqlServer);
        var token = TestContext.Current.CancellationToken;
        await connection.OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        await CallAsync(connection, outcome, transaction);

        connection.State.ShouldBe(ConnectionState.Open);
        (await connection.ExecuteScalarAsync<int>("SELECT @@TRANCOUNT", transaction: transaction)).ShouldBe(1);
        await transaction.RollbackAsync(token);
    }

    private static Task<string?> StatusOfAliceAsync(SqlConnection connection, IDbTransaction? transaction) =>
        connection.ExecuteScalarAsync<string>("SELECT status FROM dbo.accounts WHERE id = 1", transaction: transaction);

    // A connection of "app" whose ApplicationName no other connection has: the server shows it as the program_name of
    // the session that the connection opens, which is how a test finds a session opened by the method itself, whose
    // ServerProcessId it cannot read before the call.  Each has a pool of its own, which no other test is given.
    private SqlConnection OfItsOwnPool() =>
        new(
            new SqlConnectionStringBuilder(sqlServer.ConnectionStringOf(SqlServerContainer.AppDatabase))
            {
                ApplicationName = $"{nameof(RunTimeArgumentTests)}-{Guid.NewGuid():N}",
            }.ConnectionString
        );

    // Runs TouchAccount on bob while LockOfBob holds his row, and cancels its token once a second connection sees the
    // method's session waiting on the lock, so that the cancel always falls while the command runs, then asserts
    // SqlClient's exception.  It is an update, which waits before the command answers: a SELECT sends its columns
    // first, and waits in the read of its first row, whose cancel is a TaskCanceledException.  An open connection's
    // session is its ServerProcessId; a closed one, which the method opens, is of its own pool, and its session is the
    // one of its program_name.
    private async Task CancelledWhileItRunsAsync(SqlConnection connection, DbTransaction? transaction)
    {
        var token = TestContext.Current.CancellationToken;
        var session =
            connection.State == ConnectionState.Open
                ? string.Create(CultureInfo.InvariantCulture, $"s.session_id = {connection.ServerProcessId}")
                : $"s.program_name = N'{new SqlConnectionStringBuilder(connection.ConnectionString).ApplicationName}'";
        await using var lockOfBob = await LockOfBob.TakeAsync(sqlServer, token);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var call = connection.TouchAccount(
            new TouchAccountArgs(2),
            transaction: transaction,
            cancellationToken: cancellation.Token
        );
        bool endedFirst;
        try
        {
            // The wait ends too when the call does, so that a call that fails at once is not waited for.
            await Eventually.TrueAsync(
                async () => call.IsCompleted || await sqlServer.ScalarAsync(WaitingOnALock(session)) is 1,
                "The server never showed the method's session waiting on the lock of bob's row."
            );
        }
        finally
        {
            // However the wait ends, the call is cancelled and waited for, so that it ends before the test does and a
            // failure of the wait is the one reported.
            endedFirst = call.IsCompleted;
            await cancellation.CancelAsync();
            await ((Task)call).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        if (endedFirst)
        {
            // A call that ended before its wait was seen reports its own failure.
            _ = await call;
            throw new ShouldAssertException("The method ended before the server showed it waiting on the lock.");
        }

        var thrown = await Should.ThrowAsync<SqlException>(() => call);

        thrown.Number.ShouldBe(0);
        thrown.Class.ShouldBe((byte)11);
        thrown.Message.ShouldContain("Operation cancelled by user");
    }

    // How many requests of the session wait on a lock: 1 while LockOfBob holds the row that the method updates.
    private static string WaitingOnALock(string session) =>
        "SELECT COUNT(*) FROM sys.dm_exec_requests AS r "
        + "JOIN sys.dm_exec_sessions AS s ON s.session_id = r.session_id "
        + $"WHERE {session} AND r.wait_type LIKE 'LCK[_]M[_]%'";

    // Calls a method that returns its row, that throws because it read two, or whose token is cancelled while its
    // command runs, and asserts each.
    private async Task CallAsync(SqlConnection connection, Outcome outcome, DbTransaction? transaction)
    {
        var token = TestContext.Current.CancellationToken;
        switch (outcome)
        {
            case Outcome.Returns:
                var account = await connection.GetAccountInRange(
                    new GetAccountInRangeArgs(2, 2),
                    transaction: transaction,
                    cancellationToken: token
                );
                account.ShouldBe(new GetAccountInRangeDto(2, "bob"));
                break;
            case Outcome.Throws:
                var refused = await Should.ThrowAsync<UnexpectedRowCountException>(() =>
                    connection.GetAccountInRange(
                        new GetAccountInRangeArgs(1, 2),
                        transaction: transaction,
                        cancellationToken: token
                    )
                );
                refused.QueryName.ShouldBe("GetAccountInRange");
                break;
            case Outcome.IsCancelled:
                await CancelledWhileItRunsAsync(connection, transaction);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
        }
    }

    // Bob's row, changed by an update in a transaction of a connection of its own, so that another update of it waits
    // on the lock until the lock is disposed, which rolls the update back.  The tests of a command that runs wait on
    // it: a lock wait has no end of its own, where SQL Server ends a WAITFOR early when it runs short of threads, as it
    // did on the busy runners of CI, and the method then returned its row.
    private sealed class LockOfBob(SqlServerContainer sqlServer) : IAsyncDisposable
    {
        private readonly SqlConnection _connection = EndToEndSources.App(sqlServer);
        private DbTransaction? _transaction;

        public static async Task<LockOfBob> TakeAsync(SqlServerContainer sqlServer, CancellationToken cancellationToken)
        {
            var lockOfBob = new LockOfBob(sqlServer);
            try
            {
                await lockOfBob.HoldAsync(cancellationToken);
                return lockOfBob;
            }
            catch
            {
                await lockOfBob.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_transaction is not null)
            {
                await _transaction.DisposeAsync();
            }

            await _connection.DisposeAsync();
        }

        private async Task HoldAsync(CancellationToken cancellationToken)
        {
            await _connection.OpenAsync(cancellationToken);
            _transaction = await _connection.BeginTransactionAsync(cancellationToken);
            _ = await _connection.ExecuteAsync(
                "UPDATE dbo.accounts SET status = 'suspended' WHERE id = 2",
                transaction: _transaction
            );
        }
    }
}
