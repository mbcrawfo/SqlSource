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

    [Fact]
    public async Task WaitFiveSeconds_CommandTimeoutOfOneSecond_ThrowsTheTimeout()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            connection.WaitFiveSeconds(commandTimeout: 1, cancellationToken: TestContext.Current.CancellationToken)
        );

        thrown.Number.ShouldBe(-2);
        thrown.Message.ShouldStartWith("Execution Timeout Expired.");
    }

    // SqlClient sends the server an attention, and raises the server's error, not an OperationCanceledException:
    // CancelledWhileItRunsAsync asserts it.
    [Fact]
    public async Task WaitFiveSeconds_TokenCancelledWhileItRuns_ThrowsSqlException()
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

    // The connection is of a pool of its own, so that a cancel can find the session that the method opens.
    [Theory]
    [InlineData(Outcome.Returns)]
    [InlineData(Outcome.Throws)]
    [InlineData(Outcome.IsCancelled)]
    public async Task ClosedConnection_AfterTheMethod_IsClosed(Outcome outcome)
    {
        await using var connection = OfItsOwnPool();

        await CallAsync(connection, outcome, null);

        connection.State.ShouldBe(ConnectionState.Closed);
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

    // Runs WaitFiveSeconds and cancels its token once a second connection sees the method's session waiting in the
    // WAITFOR, so that the cancel always falls while the command runs, then asserts SqlClient's exception.  An open
    // connection's session is its ServerProcessId; a closed one, which the method opens, is of its own pool, and its
    // session is the one of its program_name.
    private async Task CancelledWhileItRunsAsync(SqlConnection connection, DbTransaction? transaction)
    {
        var session =
            connection.State == ConnectionState.Open
                ? string.Create(CultureInfo.InvariantCulture, $"s.session_id = {connection.ServerProcessId}")
                : $"s.program_name = N'{new SqlConnectionStringBuilder(connection.ConnectionString).ApplicationName}'";
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var call = connection.WaitFiveSeconds(transaction: transaction, cancellationToken: cancellation.Token);
        try
        {
            await Eventually.TrueAsync(
                async () => await sqlServer.ScalarAsync(WaitingInTheWaitFor(session)) is 1,
                "The server never showed the method's session waiting in its WAITFOR."
            );
        }
        finally
        {
            // A wait that was never seen is cancelled too, so that the call ends before the test does.
            await cancellation.CancelAsync();
        }

        var thrown = await Should.ThrowAsync<SqlException>(() => call);

        thrown.Number.ShouldBe(0);
        thrown.Class.ShouldBe((byte)11);
        thrown.Message.ShouldContain("Operation cancelled by user");
    }

    // How many requests of the session wait in a WAITFOR: 1 while the server runs WaitFiveSeconds.
    private static string WaitingInTheWaitFor(string session) =>
        "SELECT COUNT(*) FROM sys.dm_exec_requests AS r "
        + "JOIN sys.dm_exec_sessions AS s ON s.session_id = r.session_id "
        + $"WHERE {session} AND r.wait_type = 'WAITFOR'";

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
}
