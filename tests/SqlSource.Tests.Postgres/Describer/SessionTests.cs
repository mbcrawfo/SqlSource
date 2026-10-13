using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// A session against a real server: what "open" knows of it, each way a session is not opened, and what a describe
// says once the server has ended the session.
[Collection(UsesPostgres.Name)]
public sealed class SessionTests(PostgresContainer postgres)
{
    private const string Marker = "s3cret-marker";

    [Fact]
    public async Task OpenAsync_Container_GivesTheVersionTheDriverAndTheThreeSettings()
    {
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);

        await using var session = opened.Value.ShouldNotBeNull();
        var server = session.Server;
        server.Version.ShouldNotContain(" ");
        char.IsAsciiDigit(server.Version[0]).ShouldBeTrue(server.Version);
        server.Driver.ShouldBe("Npgsql");
        server.DriverVersion.ShouldBe(typeof(NpgsqlConnection).Assembly.GetName().Version!.ToString(3));
        server.Settings.ShouldBe([
            new ServerSetting("search_path", "\"$user\", public"),
            new ServerSetting("plan_cache_mode", "auto"),
            new ServerSetting("standard_conforming_strings", "on"),
        ]);
    }

    [Fact]
    public async Task OpenAsync_ThroughACapturingExchange_KeepsAnEmptyRequestAndNothingOfTheConnection()
    {
        var capture = new DescribeCapture("postgres");

        var opened = await Sessions.OpenAsync(
            postgres.ConnectionString,
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        await using var session = opened.Value.ShouldNotBeNull();
        var call = capture.Calls.ShouldHaveSingleItem();
        call.Method.ShouldBe("open");
        CaptureJson.TextOf(call.Request).ShouldBe("{}");
        CaptureJson.Write(capture).ShouldNotContain(postgres.ConnectionString);
    }

    [Fact]
    public async Task OpenAsync_WrongPassword_IsSqlsrc224WithTheServersMessage()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Password = Marker };

        var failure = await ShouldNotOpenAsync(connection.ConnectionString);

        failure.ServerLines[0].ShouldStartWith("28P01: password authentication failed");
    }

    [Fact]
    public async Task OpenAsync_MissingDatabase_IsSqlsrc224WithTheServersMessage()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = "nope" };

        var failure = await ShouldNotOpenAsync(connection.ConnectionString);

        failure.ServerLines.ShouldBe(["3D000: database \"nope\" does not exist"]);
    }

    // The port 1 of this machine refuses at once.
    [Fact]
    public async Task OpenAsync_UnreachableHost_IsSqlsrc224WithTheDriversMessage()
    {
        var failure = await ShouldNotOpenAsync("Host=127.0.0.1;Port=1;Timeout=5;Username=app;Password=" + Marker);

        failure.ServerLines.ShouldBe(["Failed to connect to 127.0.0.1:1"]);
    }

    // The exchange asks the server, so that the session's connection is open, and then fails.  The caller of the
    // session sees that exception, and the connection is not left behind: the server ends the backend when the
    // driver closes it, which takes a moment, so it is asked until none is left or a few seconds have passed.
    [Fact]
    public async Task OpenAsync_ExchangeThatThrowsAfterTheServerAnswered_RethrowsItAndLeavesNoConnection()
    {
        var token = TestContext.Current.CancellationToken;
        var application = "sqlsource-test-" + Guid.NewGuid().ToString("N");
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { ApplicationName = application };
        var thrown = new InvalidOperationException("the exchange failed after the server answered");
        await using var own = NpgsqlDataSource.Create(postgres.ConnectionString);
        var exchange = new ThrowsAfterTheServerAnswered(
            thrown,
            async () => await BackendsOfAsync(own, application, token)
        );

        var caught = await Should.ThrowAsync<InvalidOperationException>(
            Sessions.OpenAsync(connection.ConnectionString, exchange)
        );

        caught.ShouldBeSameAs(thrown);
        exchange.BackendsWhileOpen.ShouldBe(
            1,
            "the session did not connect, so the count of none below proves nothing"
        );
        var backends = await BackendsOfAsync(own, application, token);
        for (var attempt = 0; backends > 0 && attempt < 100; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), token);
            backends = await BackendsOfAsync(own, application, token);
        }

        backends.ShouldBe(0, "the failed session left its connection open");
    }

    private static async Task<long> BackendsOfAsync(
        NpgsqlDataSource source,
        string application,
        CancellationToken token
    )
    {
        await using var count = source.CreateCommand(
            "SELECT count(*) FROM pg_catalog.pg_stat_activity WHERE application_name = @application"
        );
        _ = count.Parameters.AddWithValue("application", application);
        return (long)(await count.ExecuteScalarAsync(token)).ShouldNotBeNull();
    }

    private const string Sql = "SELECT 1 FROM users WHERE id = @id";

    // A server ends a backend when it restarts, when an administrator says so and after an idle timeout.  The
    // describe that finds it out has what the server said last, 57P01, or what the driver says of a socket that
    // closed before it read that, so its lines are not compared.  Every call after it has the tool's own text: the
    // driver would throw what is no error of a server.
    [Fact]
    public async Task Describe_AfterTheServerEndedTheSession_IsSqlsrc225EachTimeAndNoThrow()
    {
        var token = TestContext.Current.CancellationToken;
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        _ = (await session.DescribeAsync(Requests.Of(Sql), token)).Value.ShouldNotBeNull();

        await Sessions.EndBackendAsync(session, postgres.ConnectionString);
        var found = await session.DescribeAsync(Requests.Of(Sql), token);
        var later = await session.DescribeAsync(Requests.Of(Sql), token);

        _ = ShouldBeRejected(found, DescribeStep.DescribeParameters);
        ShouldBeRejected(later, DescribeStep.DescribeParameters)
            .ServerLines.ShouldBe([PostgresConnection.LostConnection]);
    }

    // The statement is prepared and the session ends before its columns are read.  "columns" answers the error that
    // is reported, and "deallocate", which is asked whatever became of the describe, answers one too and replaces
    // nothing.
    [Fact]
    public async Task Describe_ServerEndsTheSessionBetweenPrepareAndColumns_IsSqlsrc225AndNoThrow()
    {
        var capture = new DescribeCapture("postgres");
        var exchange = new EndsTheBackendAfterPrepare(postgres.ConnectionString);
        var opened = await Sessions.OpenAsync(postgres.ConnectionString, new CapturingExchange(exchange, capture));
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        exchange.Session = session;

        var described = await session.DescribeAsync(Requests.Of(Sql), TestContext.Current.CancellationToken);

        _ = ShouldBeRejected(described, DescribeStep.DescribeColumns);
        capture.Calls.Select(call => call.Method).ShouldBe(["open", "prepare", "columns", "deallocate"]);
        CaptureJson.TextOf(capture.Calls[3].Answer).ShouldContain(PostgresConnection.LostConnection);
    }

    // Another session drops the table the query reads after "columns" named it: "tables" answers nothing for it.  That
    // is SQLSRC225 at the catalog and not a throw, "types" and "check" are not asked, the statement is freed, and the
    // session describes another query afterwards.
    [Fact]
    public async Task Describe_TableDroppedBetweenColumnsAndTables_IsSqlsrc225AtCatalogAndNoThrow()
    {
        await ExecuteAsync("CREATE TABLE IF NOT EXISTS session_gone (id int)");
        var capture = new DescribeCapture("postgres");
        var exchange = new DropsTheTableAfterColumns(postgres.ConnectionString);
        var opened = await Sessions.OpenAsync(postgres.ConnectionString, new CapturingExchange(exchange, capture));
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        var token = TestContext.Current.CancellationToken;

        var described = await session.DescribeAsync(Requests.Of("SELECT id FROM session_gone"), token);
        var later = await session.DescribeAsync(Requests.Of("SELECT 1 AS one"), token);

        var failure = ShouldBeRejected(described, DescribeStep.Catalog);
        failure.Arguments.ShouldBe([Requests.Query, "the table of the column 'id' no longer exists"]);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.SchemaChangedHelp);
        capture
            .Calls.Take(5)
            .Select(call => call.Method)
            .ShouldBe(["open", "prepare", "columns", "tables", "deallocate"]);
        later.Failure.ShouldBeNull();
        _ = later.Value.ShouldNotBeNull();
        (await Sessions.PreparedStatementsAsync(session)).ShouldBe(0);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static DescribeFailure ShouldBeRejected(
        Outcome<QueryDescription, DescribeFailure> described,
        DescribeStep step
    )
    {
        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(step);
        return failure;
    }

    // Asks the server, and ends the session's backend when "prepare" has answered.
    private sealed class EndsTheBackendAfterPrepare(string connection) : IDescribeExchange
    {
        public PostgresSession? Session { get; set; }

        public async Task<TAnswer> AskAsync<TRequest, TAnswer>(
            string method,
            TRequest request,
            Func<TRequest, CancellationToken, Task<TAnswer>> live,
            CancellationToken cancellationToken
        )
        {
            var answer = await LiveExchange.Instance.AskAsync(method, request, live, cancellationToken);
            if (method == "prepare" && Session is { } session)
            {
                await Sessions.EndBackendAsync(session, connection);
            }

            return answer;
        }
    }

    // Asks the server, and drops session_gone from a connection of its own when "columns" has answered, once.
    private sealed class DropsTheTableAfterColumns(string connection) : IDescribeExchange
    {
        private bool _dropped;

        public async Task<TAnswer> AskAsync<TRequest, TAnswer>(
            string method,
            TRequest request,
            Func<TRequest, CancellationToken, Task<TAnswer>> live,
            CancellationToken cancellationToken
        )
        {
            var answer = await LiveExchange.Instance.AskAsync(method, request, live, cancellationToken);
            if (method == "columns" && !_dropped)
            {
                _dropped = true;
                await using var source = NpgsqlDataSource.Create(connection);
                await using var command = source.CreateCommand("DROP TABLE session_gone");
                _ = await command.ExecuteNonQueryAsync(cancellationToken);
            }

            return answer;
        }
    }

    // Asks the server, counts the backends of the test's application name while the connection is open, and then
    // throws what it was given.
    private sealed class ThrowsAfterTheServerAnswered(Exception exception, Func<Task<long>> countBackends)
        : IDescribeExchange
    {
        public long BackendsWhileOpen { get; private set; } = -1;

        public async Task<TAnswer> AskAsync<TRequest, TAnswer>(
            string method,
            TRequest request,
            Func<TRequest, CancellationToken, Task<TAnswer>> live,
            CancellationToken cancellationToken
        )
        {
            _ = await live(request, cancellationToken);
            BackendsWhileOpen = await countBackends();
            throw exception;
        }
    }

    // No session, SQLSRC224 at no step for the database, and the connection's whole value in no text of it.
    private static async Task<DescribeFailure> ShouldNotOpenAsync(string connection)
    {
        var opened = await Sessions.OpenAsync(connection);

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.Arguments.ShouldBe([PostgresContainer.Database]);
        failure.Step.ShouldBeNull();
        Sessions
            .TextsOf(failure)
            .ShouldAllBe(text =>
                !text.Contains(connection, StringComparison.Ordinal) && !text.Contains(Marker, StringComparison.Ordinal)
            );
        return failure;
    }
}
