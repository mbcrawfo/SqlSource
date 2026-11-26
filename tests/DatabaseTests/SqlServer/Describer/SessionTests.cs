using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Parsing;
using SqlSource.Testing;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Describer;

// A session against a real server: what "open" knows of it, and each way a session is not opened.  SqlClient's own
// texts name the host, the login and the database, so a failure keeps the numbers and a fixed text alone.
[Collection(UsesSqlServer.Name)]
public sealed class SessionTests(SqlServerDatabases sqlServer)
{
    [Fact]
    public async Task OpenAsync_Container_GivesTheVersionTheDriverAndTheSettings()
    {
        await using var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString);

        var server = session.Server;
        server.Version.ShouldBe(SqlServerContainer.ImageBuilds[EngineImage.SqlServer.Image]);
        server.Driver.ShouldBe("Microsoft.Data.SqlClient");
        server.DriverVersion.ShouldNotBeNull().ShouldStartWith("7.1.1");
        // A new database takes the compatibility level of its server's major: 140 on 2017, 150 on 2019 and so on.
        server.Settings.ShouldBe([
            new ServerSetting("compatibility_level", (sqlServer.Major * 10).ToString(CultureInfo.InvariantCulture)),
            new ServerSetting("ANSI_NULLS", "ON"),
            new ServerSetting("QUOTED_IDENTIFIER", "ON"),
            new ServerSetting("collation", "SQL_Latin1_General_CP1_CI_AS"),
        ]);
    }

    // The edition is the server's, read so that an Azure SQL Database, 5, or Managed Instance, 8, which reports a
    // version of its own, is accepted whatever version it reports: the container's Developer edition is 3.
    [Fact]
    public async Task OpenAsync_Container_AnswersItsEdition()
    {
        await using var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString);

        var opened = await new SqlServerLive(session.Connection).OpenAsync(TestContext.Current.CancellationToken);

        opened.Error.ShouldBeNull();
        opened.EngineEdition.ShouldBe(3);
    }

    // The server's message names the login, and the client is told state 1 whatever the cause.
    [Fact]
    public async Task OpenAsync_WrongPassword_IsSqlsrc224WithNoPartOfTheValue()
    {
        var connection = new SqlConnectionStringBuilder(sqlServer.ConnectionString) { Password = Secrets.Marker };

        var failure = await ShouldNotOpenAsync(connection);

        failure.ServerLines.ShouldBe(["Msg 18456, Level 14, State 1: the server refused the login"]);
    }

    // The server's messages name the database and the login.
    [Fact]
    public async Task OpenAsync_MissingDatabase_IsSqlsrc224WithNoPartOfTheValue()
    {
        var connection = new SqlConnectionStringBuilder(sqlServer.ConnectionString) { InitialCatalog = "sqlsrc_nope" };

        var failure = await ShouldNotOpenAsync(connection);

        failure.ServerLines.ShouldBe([
            "Msg 4060, Level 11, State 1: the server cannot open the database for the login",
            "Msg 18456, Level 14, State 1: the server refused the login",
        ]);
    }

    // The port 1 of this machine refuses at once.  SqlClient's message names the host and the port.  On Linux SqlClient
    // gives the socket's error as Success, which names no reason, and the error the number 0, so the line is the fixed
    // text alone there.  On Windows SqlClient retries a refused port until the connect timeout ends and reports
    // WAIT_TIMEOUT, 258, which is the text of a connection that timed out.  On macOS it does either from one run to the
    // next, so either line passes there.
    [Fact]
    public async Task OpenAsync_UnreachableHost_IsSqlsrc224WithNoPartOfTheValue()
    {
        var connection = new SqlConnectionStringBuilder(
            "Data Source=127.0.0.1,1;Connect Timeout=3;Encrypt=False;Password=" + Secrets.Marker
        );

        var failure = await ShouldNotOpenAsync(connection);

        const string TimedOut = "^Msg 258, Level 20, State 0: the connection to the server timed out$";
        const string Refused = @"(^|: )SqlClient could not reach the server(: ConnectionRefused)?$";
        var expected = Refused;
        if (OperatingSystem.IsWindows())
        {
            expected = TimedOut;
        }
        else if (OperatingSystem.IsMacOS())
        {
            expected = $"{TimedOut}|{Refused}";
        }

        failure.ServerLines.ShouldHaveSingleItem().ShouldMatch(expected);
    }

    // The container's certificate is its own and signed by no authority this machine trusts, so a string that asks for
    // encryption and does not trust the certificate fails the TLS handshake.
    [Fact]
    public async Task OpenAsync_CertificateNotTrusted_IsSqlsrc224WithNoPartOfTheValue()
    {
        var connection = new SqlConnectionStringBuilder(sqlServer.ConnectionString)
        {
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = false,
        };

        var failure = await ShouldNotOpenAsync(connection);

        failure
            .ServerLines.ShouldHaveSingleItem()
            .ShouldEndWith(": the server's certificate is not trusted, or the TLS handshake failed");
    }

    // SqlClient's text for a keyword it does not know quotes the keyword, and for some values the value.
    [Fact]
    public async Task OpenAsync_UnreadableConnectionString_IsSqlsrc224WithTheFixedLine()
    {
        var connection = "Data Source=127.0.0.1,1;Password=" + Secrets.Marker + ";Nonsense=" + Secrets.Marker;

        var (opened, capture) = await Sessions.OpenCapturedAsync(connection);

        var failure = ShouldNotHaveOpened(opened, connection);
        failure.ServerLines.ShouldBe([ConnectionErrors.UnreadableConnectionString]);
        CaptureJson.Write(capture).ShouldNotContain(Secrets.Marker);
    }

    // A run that is cancelled while it opens a session ends as a cancellation, never as an answer of "open", and the
    // exchange is given the run's own token.
    [Fact]
    public async Task OpenAsync_TokenCancelled_ThrowsOperationCanceled()
    {
        List<CancellationToken> tokens = [];
        var exchange = TokenExchange(tokens);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            Sessions.OpenAsync(sqlServer.ConnectionString, exchange, cancellationToken: cancel.Token)
        );

        tokens.ShouldBe([cancel.Token]);
    }

    // A describe that is cancelled ends as a cancellation, and its call is given the describe's own token.
    [Fact]
    public async Task DescribeAsync_TokenCancelled_ThrowsOperationCanceledWithTheTokenOfTheDescribe()
    {
        List<CancellationToken> tokens = [];
        var exchange = TokenExchange(tokens);
        await using var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString, exchange);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            session.DescribeAsync(
                DescribeRequests.Of(SqlDialectRules.SqlServer, "UPDATE dbo.users SET name = @name WHERE id = @id"),
                cancel.Token
            )
        );

        tokens.ShouldBe([TestContext.Current.CancellationToken, cancel.Token]);
    }

    // SqlClient reports a command that is cancelled while the server runs it as a SqlException: it is the cancellation
    // all the same, and no answer.  The command asks for an application lock that a second session holds, with no
    // timeout of its own or of SqlClient, so the token alone ends it, and the token is cancelled once the server shows
    // the session waiting on the lock, so that path is the one that runs.  A lock wait has no end of its own, where
    // SQL Server ends a WAITFOR early when it runs short of threads.
    [Fact]
    public async Task AnswerAsync_CommandCancelledWhileTheServerRunsIt_ThrowsOperationCanceled()
    {
        var resource = $"{nameof(SessionTests)}-{Guid.NewGuid():N}";
        await using var holder = new SqlConnection(sqlServer.ConnectionString);
        await holder.OpenAsync(TestContext.Current.CancellationToken);
        _ = await holder.ExecuteAsync(AppLock(resource));

        await using var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var token = cancel.Token;
        await using var read = await session.Connection.CommandAsync("SELECT CAST(@@SPID AS int)", token);
        var id = (int)await read.ExecuteScalarAsync(token);
        SqlException? thrown = null;

        var answered = SqlServerLive.AnswerAsync<SqlServerServerError?>(
            async () =>
            {
                await using var command = await session.Connection.CommandAsync(AppLock(resource), token);
                command.CommandTimeout = 0;
                try
                {
                    _ = await command.ExecuteNonQueryAsync(token);
                }
                catch (SqlException exception)
                {
                    thrown = exception;
                    throw;
                }

                return null;
            },
            ConnectionErrors.Of,
            static error => error,
            session.Connection,
            token
        );
        try
        {
            await Eventually.TrueAsync(
                async () => await sqlServer.ScalarAsync(RunningWaitOf(id)) is 1,
                "The server never showed the session waiting on the lock."
            );
        }
        finally
        {
            // A wait that was never seen is ended too, so that the session is not closed under a running command.
            await cancel.CancelAsync();
        }

        _ = await Should.ThrowAsync<OperationCanceledException>(answered);
        _ = thrown.ShouldNotBeNull("the token was cancelled before the server ran the command");
    }

    // A probe that is cancelled while the server runs it is the cancellation, and never a probe that does not bind,
    // though SqlClient reports it as an error below the class that ends the connection.
    [Fact]
    public async Task ProbeAsync_CancelledWhileTheServerRunsIt_ThrowsOperationCanceled()
    {
        await using var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var id = await SessionIdAsync(session);

        await using var locked = await LockedTable.HoldAsync(sqlServer);
        var probed = ProbeAsync(session, locked, cancel.Token);
        try
        {
            await WaitOnTheLockAsync(id);
        }
        finally
        {
            // A wait that was never seen is ended too, so that the lock is not released under a running probe.
            await cancel.CancelAsync();
        }

        _ = await Should.ThrowAsync<OperationCanceledException>(probed);
    }

    // A probe that runs out of the connection's command timeout says nothing of the batch: its answer is the timeout,
    // and never a probe that does not bind, though SqlClient reports it as an error below the class that ends the
    // connection.  The lock holds the probe past the timeout, which ends it whenever the server took the lock.
    [Fact]
    public async Task ProbeAsync_TimedOut_AnswersTheTimeoutAndNoBinding()
    {
        var connection = new SqlConnectionStringBuilder(sqlServer.ConnectionString) { CommandTimeout = 1 };
        await using var session = await Sessions.OpenSessionAsync(connection.ConnectionString);

        await using var locked = await LockedTable.HoldAsync(sqlServer);
        var answer = await ProbeAsync(session, locked, TestContext.Current.CancellationToken);

        answer.Binds.ShouldBeNull();
        LinesOf(answer.Error).ShouldBe(["Msg -2, Level 11, State 0: " + ConnectionTexts.NoAnswerInTime]);
    }

    // A probe whose session the server ends says nothing of the batch: its answer is the lost connection, and never a
    // probe that does not bind.
    [Fact]
    public async Task ProbeAsync_SessionKilledWhileTheServerRunsIt_AnswersTheLostConnectionAndNoBinding()
    {
        await using var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString);
        var id = await SessionIdAsync(session);

        await using var locked = await LockedTable.HoldAsync(sqlServer);
        var probed = ProbeAsync(session, locked, TestContext.Current.CancellationToken);
        await WaitOnTheLockAsync(id);
        await sqlServer.ExecuteAsync(string.Create(CultureInfo.InvariantCulture, $"KILL {id}"));
        var answer = await probed;

        answer.Binds.ShouldBeNull();
        var errors = answer.Error.ShouldNotBeNull().Errors;
        errors.ShouldNotBeEmpty();
        errors.ShouldAllBe(static row => row.Message == ConnectionTexts.LostConnection);
    }

    // A session that the server ended between two calls: the next call fails on the connection, and every call after
    // it finds the connection closed, each a SQLSRC225 that the connection was lost and never a bug.  SqlClient opens
    // again a connection that the server ended while it was idle, unless ConnectRetryCount is 0, so the test's is.
    [Fact]
    public async Task DescribeAsync_SessionKilledBetweenCalls_IsSqlsrc225ThatTheConnectionWasLost()
    {
        var token = TestContext.Current.CancellationToken;
        var connection = new SqlConnectionStringBuilder(sqlServer.ConnectionString) { ConnectRetryCount = 0 };
        await using var session = await Sessions.OpenSessionAsync(connection.ConnectionString);
        var id = await SessionIdAsync(session);
        var request = DescribeRequests.Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users");

        await sqlServer.ExecuteAsync(string.Create(CultureInfo.InvariantCulture, $"KILL {id}"));
        var first = await session.DescribeAsync(request, token);
        var second = await session.DescribeAsync(request, token);

        foreach (var described in (IEnumerable<Outcome<QueryDescription, DescribeFailure>>)[first, second])
        {
            var failure = described.Failure.ShouldNotBeNull();
            failure.Descriptor.Id.ShouldBe("SQLSRC225");
            failure.Step.ShouldBe(DescribeStep.DescribeParameters);
            failure.ServerLines.ShouldNotBeEmpty();
            failure.ServerLines.ShouldAllBe(static line =>
                line.EndsWith(ConnectionTexts.LostConnection, StringComparison.Ordinal)
            );
        }
    }

    // The probe of the locked table that "parameters" would send for a reused parameter, on the session's connection.
    private static Task<SqlServerProbeAnswer> ProbeAsync(
        SqlServerSession session,
        LockedTable locked,
        CancellationToken token
    ) =>
        new SqlServerLive(session.Connection).ProbeAsync(
            new SqlServerProbeRequest($"SELECT id FROM dbo.{locked.Name} WHERE id = @a", "@a int"),
            token
        );

    private static async Task<int> SessionIdAsync(SqlServerSession session)
    {
        var token = TestContext.Current.CancellationToken;
        await using var read = await session.Connection.CommandAsync("SELECT CAST(@@SPID AS int)", token);
        return (int)await read.ExecuteScalarAsync(token);
    }

    private Task WaitOnTheLockAsync(int session) =>
        Eventually.TrueAsync(
            async () => await sqlServer.ScalarAsync(RunningWaitOf(session)) is 1,
            "The server never showed the session waiting on the lock."
        );

    private static EquatableArray<string> LinesOf(SqlServerServerError? error) =>
        SqlServerErrors.ServerLines(error.ShouldNotBeNull());

    // A table of the test's own whose schema a second connection holds locked by an ALTER in a transaction it leaves
    // open, so that a probe of it waits with no end of its own.  Disposing it rolls the ALTER back and drops the table.
    private sealed class LockedTable : IAsyncDisposable
    {
        private readonly SqlServerDatabases _sqlServer;
        private readonly SqlConnection _holder;
        private SqlTransaction? _transaction;

        private LockedTable(SqlServerDatabases sqlServer, SqlConnection holder, string name)
        {
            _sqlServer = sqlServer;
            _holder = holder;
            Name = name;
        }

        public string Name { get; }

        public static async Task<LockedTable> HoldAsync(SqlServerDatabases sqlServer)
        {
            var token = TestContext.Current.CancellationToken;
            var name = $"td_probe_{Guid.NewGuid():N}";
            await sqlServer.ExecuteAsync($"CREATE TABLE dbo.{name} (id int NOT NULL PRIMARY KEY)");
            var locked = new LockedTable(sqlServer, new SqlConnection(sqlServer.ConnectionString), name);
            try
            {
                await locked._holder.OpenAsync(token);
                locked._transaction = (SqlTransaction)await locked._holder.BeginTransactionAsync(token);
                _ = await locked._holder.ExecuteAsync(
                    $"ALTER TABLE dbo.{name} ADD x int NULL",
                    transaction: locked._transaction
                );
                return locked;
            }
            catch
            {
                await locked.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_transaction is not null)
                {
                    await _transaction.DisposeAsync();
                }

                await _holder.DisposeAsync();
            }
            finally
            {
                await _sqlServer.ExecuteAsync($"DROP TABLE dbo.{Name}");
            }
        }
    }

    // Takes the session's exclusive application lock of the resource, waiting for it as long as it takes.
    private static string AppLock(string resource) =>
        "EXEC sp_getapplock "
        + $"@Resource = N'{resource}', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = -1";

    // How many requests of the session wait on a lock: 1 while the server runs the test's command.
    private static string RunningWaitOf(int session) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id = {session} AND wait_type LIKE 'LCK[_]M[_]%'"
        );

    // No session, SQLSRC224 at no step for the database, and nothing of the value in any text of the failure or in
    // the capture: not the password, the host, or the database's name when it is not the logical name, which the run
    // prints whatever the describer does and which the database of the fixtures shares.
    private static async Task<DescribeFailure> ShouldNotOpenAsync(SqlConnectionStringBuilder connection)
    {
        var (opened, capture) = await Sessions.OpenCapturedAsync(connection.ConnectionString);

        var failure = ShouldNotHaveOpened(opened, connection.ConnectionString);
        string[] words =
        [
            connection.Password,
            connection.DataSource.Split(',')[0],
            connection.InitialCatalog == SqlServerDatabases.Database ? string.Empty : connection.InitialCatalog,
        ];
        foreach (var word in words.Where(static word => word.Length > 0))
        {
            Failures.TextsOf(failure).ShouldAllBe(text => !text.Contains(word, StringComparison.Ordinal));
            CaptureJson.Write(capture).ShouldNotContain(word);
        }

        return failure;
    }

    private static DescribeFailure ShouldNotHaveOpened(
        Outcome<IDescribeSession, DescribeFailure> opened,
        string connection
    )
    {
        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.Arguments.ShouldBe([SqlServerDatabases.Database]);
        failure.Step.ShouldBeNull();
        Failures
            .TextsOf(failure)
            .ShouldAllBe(text =>
                !text.Contains(connection, StringComparison.Ordinal)
                && !text.Contains(Secrets.Marker, StringComparison.Ordinal)
            );
        return failure;
    }

    // Asks the server as the live exchange does, and keeps the token each call was given, in order.
    private static HookedExchange TokenExchange(List<CancellationToken> tokens) =>
        new(
            before: (_, _, token) =>
            {
                tokens.Add(token);
                return Task.CompletedTask;
            }
        );
}
