using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tests.SqlServer.Describer;

// A session against a real server: what "open" knows of it, and each way a session is not opened.  SqlClient's own
// texts name the host, the login and the database, so a failure keeps the numbers and a fixed text alone.
[Collection(UsesSqlServer.Name)]
public sealed class SessionTests(SqlServerContainer sqlServer)
{
    [Fact]
    public async Task OpenAsync_Container_GivesTheVersionTheDriverAndTheSettings()
    {
        await using var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString);

        var server = session.Server;
        server.Version.ShouldBe(SqlServerContainer.ImageBuilds[SqlServerContainer.Image]);
        server.Driver.ShouldBe("Microsoft.Data.SqlClient");
        server.DriverVersion.ShouldNotBeNull().ShouldStartWith("7.1.1");
        server.Settings.ShouldBe([
            new ServerSetting("compatibility_level", sqlServer.Major >= 17 ? "170" : "140"),
            new ServerSetting("ANSI_NULLS", "ON"),
            new ServerSetting("QUOTED_IDENTIFIER", "ON"),
            new ServerSetting("collation", "SQL_Latin1_General_CP1_CI_AS"),
        ]);
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
    // gives the socket's error as Success, which names no reason, so the line ends with the fixed text alone there.
    [Fact]
    public async Task OpenAsync_UnreachableHost_IsSqlsrc224WithNoPartOfTheValue()
    {
        var connection = new SqlConnectionStringBuilder(
            "Data Source=127.0.0.1,1;Connect Timeout=3;Encrypt=False;Password=" + Secrets.Marker
        );

        var failure = await ShouldNotOpenAsync(connection);

        failure
            .ServerLines.ShouldHaveSingleItem()
            .ShouldMatch(@": SqlClient could not reach the server(: ConnectionRefused)?$");
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

    // A run that is cancelled while it opens a session ends as a cancellation, never as an answer of "open".
    [Fact]
    public async Task OpenAsync_TokenCancelled_ThrowsOperationCanceled() =>
        await Should.ThrowAsync<OperationCanceledException>(
            Sessions.OpenAsync(sqlServer.ConnectionString, new CancellingExchange())
        );

    // SqlClient reports a command that is cancelled while the server runs it as a SqlException: it is the cancellation
    // all the same, and no answer.  The command waits for longer than the test, so it is the token that ends it.
    [Fact]
    public async Task AnswerAsync_CommandCancelledWhileTheServerRunsIt_ThrowsOperationCanceled()
    {
        await using var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var token = cancel.Token;
        cancel.CancelAfter(TimeSpan.FromMilliseconds(200));

        var answered = SqlServerLive.AnswerAsync<SqlServerServerError?>(
            async () =>
            {
                await using var command = await session.Connection.CommandAsync("WAITFOR DELAY '00:00:30'", token);
                _ = await command.ExecuteNonQueryAsync(token);
                return null;
            },
            ConnectionErrors.Of,
            static error => error,
            token
        );

        _ = await Should.ThrowAsync<OperationCanceledException>(answered);
    }

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
            connection.InitialCatalog == SqlServerContainer.Database ? string.Empty : connection.InitialCatalog,
        ];
        foreach (var word in words.Where(static word => word.Length > 0))
        {
            Sessions.TextsOf(failure).ShouldAllBe(text => !text.Contains(word, StringComparison.Ordinal));
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
        failure.Arguments.ShouldBe([SqlServerContainer.Database]);
        failure.Step.ShouldBeNull();
        Sessions
            .TextsOf(failure)
            .ShouldAllBe(text =>
                !text.Contains(connection, StringComparison.Ordinal)
                && !text.Contains(Secrets.Marker, StringComparison.Ordinal)
            );
        return failure;
    }

    // Asks the server under a token that is already cancelled, as a run that was cancelled while it opened does.
    private sealed class CancellingExchange : IDescribeExchange
    {
        public async Task<TAnswer> AskAsync<TRequest, TAnswer>(
            string method,
            TRequest request,
            Func<TRequest, CancellationToken, Task<TAnswer>> live,
            CancellationToken cancellationToken
        )
        {
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();
            return await live(request, cancelled.Token);
        }
    }
}
