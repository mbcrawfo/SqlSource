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
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// A session against a real server: what "open" knows of it, each way a session is not opened, and what a describe
// says once the server has ended the session.
[Collection(UsesPostgres.Name)]
public sealed class SessionTests(PostgresContainer postgres)
{
    [Fact]
    public async Task OpenAsync_Container_GivesTheVersionTheDriverAndTheThreeSettings()
    {
        await using var session = await Sessions.OpenSessionAsync(postgres.ConnectionString);

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
        var (opened, capture) = await Sessions.OpenCapturedAsync(postgres.ConnectionString);

        await using var session = opened;
        var call = capture.Calls.ShouldHaveSingleItem();
        call.Method.ShouldBe("open");
        CaptureJson.TextOf(call.Request).ShouldBe("{}");
        CaptureJson.Write(capture).ShouldNotContain(postgres.ConnectionString);
    }

    // The server's message names the user, and its SQLSTATE alone is kept.
    [Fact]
    public async Task OpenAsync_WrongPassword_IsSqlsrc224WithTheSqlStateAndAFixedText()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Password = Secrets.Marker };

        var failure = await ShouldNotOpenAsync(connection.ConnectionString);

        failure.ServerLines.ShouldBe(["28P01: the server refused the password"]);
        ShouldNotName(failure, connection.Host, connection.Username);
    }

    // The server's message names the database.
    [Fact]
    public async Task OpenAsync_MissingDatabase_IsSqlsrc224WithTheSqlStateAndAFixedText()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = "sqlsrc_nope" };

        var failure = await ShouldNotOpenAsync(connection.ConnectionString);

        failure.ServerLines.ShouldBe(["3D000: the server has no such database"]);
        ShouldNotName(failure, connection.Host, connection.Username, connection.Database);
    }

    // The port 1 of this machine refuses at once.  The driver's message names the host and the port.
    [Fact]
    public async Task OpenAsync_UnreachableHost_IsSqlsrc224WithAFixedTextAndTheSocketsError()
    {
        var failure = await ShouldNotOpenAsync(
            "Host=127.0.0.1;Port=1;Timeout=5;Username=sqlsrc_unreached;Password=" + Secrets.Marker
        );

        failure.ServerLines.ShouldBe(["Npgsql could not reach the server: ConnectionRefused"]);
        ShouldNotName(failure, "127.0.0.1", "sqlsrc_unreached");
    }

    // The server asks for a password and the string names a password file instead of a password: the driver reads
    // the file then, and for one that is not there it throws what is no NpgsqlException and quotes the path.  The
    // line is the tool's own.
    [Fact]
    public async Task OpenAsync_PasswordFileThatIsNotThere_IsSqlsrc224WithTheFixedLine()
    {
        PasswordFiles.SkipUnlessRead();
        using var folder = new TempFolder();
        var path = folder.PathOf("passfile-that-is-not-there");
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Password = null,
            Passfile = path,
        };

        var failure = await ShouldNotOpenAsync(connection.ConnectionString);

        failure.ServerLines.ShouldBe([PostgresConnection.UnreadableFile]);
        Sessions.TextsOf(failure).ShouldAllBe(text => !text.Contains(path, StringComparison.Ordinal));
    }

    // The exchange asks the server, so that the session's connection is open, and then fails.  The caller of the
    // session sees that exception, and the connection is not left behind: the server ends the backend when the
    // driver closes it, which takes a moment, so it is asked until none is left.
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
        await Eventually.TrueAsync(
            async () => await BackendsOfAsync(own, application, token) == 0,
            "the failed session left its connection open"
        );
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
    // describe that finds it out has the SQLSTATE the server said last, 57P01, or, when the driver's socket closed
    // before it read that, a lost connection: either with the describer's own text.  Every call after it has the
    // tool's own text: the driver would throw what is no error of a server.
    [Fact]
    public async Task DescribeAsync_AfterTheServerEndedTheSession_IsSqlsrc225EachTimeAndNoThrow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await Sessions.OpenSessionAsync(postgres.ConnectionString);
        _ = (await session.DescribeAsync(DescribeRequests.Of(Sql), token)).Value.ShouldNotBeNull();

        await Sessions.EndBackendAsync(session, postgres.ConnectionString);
        var found = await session.DescribeAsync(DescribeRequests.Of(Sql), token);
        var later = await session.DescribeAsync(DescribeRequests.Of(Sql), token);

        var ended = ShouldBeRejected(found, DescribeStep.DescribeParameters);
        ended
            .ServerLines.ShouldHaveSingleItem()
            .ShouldBeOneOf("57P01: the server ended the session", PostgresConnection.LostConnection);
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString);
        ShouldNotName(ended, connection.Host, connection.Username);
        ShouldBeRejected(later, DescribeStep.DescribeParameters)
            .ServerLines.ShouldBe([PostgresConnection.LostConnection]);
    }

    // The statement is prepared and the session ends before its columns are read.  "columns" answers the error that
    // is reported, and "deallocate", which is asked whatever became of the describe, answers one too and replaces
    // nothing.
    [Fact]
    public async Task DescribeAsync_ServerEndsTheSessionBetweenPrepareAndColumns_IsSqlsrc225AndNoThrow()
    {
        PostgresSession? ending = null;
        var exchange = new AfterLiveExchange(
            async (method, _, _) =>
            {
                if (method == "prepare" && ending is { } open)
                {
                    await Sessions.EndBackendAsync(open, postgres.ConnectionString);
                }
            }
        );
        var (opened, capture) = await Sessions.OpenCapturedAsync(postgres.ConnectionString, exchange);
        await using var session = opened;
        ending = session;

        var described = await session.DescribeAsync(DescribeRequests.Of(Sql), TestContext.Current.CancellationToken);

        _ = ShouldBeRejected(described, DescribeStep.DescribeColumns);
        capture.Calls.Select(call => call.Method).ShouldBe(["open", "prepare", "columns", "deallocate"]);
        CaptureJson.TextOf(capture.Calls[3].Answer).ShouldContain(PostgresConnection.LostConnection);
        CaptureJson.Write(capture).ShouldNotContain(new NpgsqlConnectionStringBuilder(postgres.ConnectionString).Host!);
    }

    // Another session drops the table the query reads after "columns" named it: "tables" answers nothing for it.  That
    // is SQLSRC225 at the catalog and not a throw, "types" and "check" are not asked, the statement is freed, and the
    // session describes another query afterwards.
    [Fact]
    public async Task DescribeAsync_TableDroppedBetweenColumnsAndTables_IsSqlsrc225AtCatalogAndNoThrow()
    {
        await postgres.ExecuteAsync("CREATE TABLE IF NOT EXISTS session_gone (id int)");
        try
        {
            var dropped = false;
            var exchange = new AfterLiveExchange(
                async (method, _, _) =>
                {
                    if (method == "columns" && !dropped)
                    {
                        dropped = true;
                        await postgres.ExecuteAsync("DROP TABLE session_gone");
                    }
                }
            );
            var (opened, capture) = await Sessions.OpenCapturedAsync(postgres.ConnectionString, exchange);
            await using var session = opened;
            var token = TestContext.Current.CancellationToken;

            var described = await session.DescribeAsync(DescribeRequests.Of("SELECT id FROM session_gone"), token);
            var later = await session.DescribeAsync(DescribeRequests.Of("SELECT 1 AS one"), token);

            var failure = ShouldBeRejected(described, DescribeStep.Catalog);
            failure.Arguments.ShouldBe([DescribeRequests.Query, "the table of the column 'id' no longer exists"]);
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
        finally
        {
            // A run that fails before the hook drops the table leaves none for the next test.
            await postgres.ExecuteAsync("DROP TABLE IF EXISTS session_gone");
        }
    }

    // The read of "prepare" matches the statement's name and its text, so that a statement of the same name that an
    // earlier run left on another backend is not taken for this one.  The server lists the text of a PREPARE exactly
    // as the driver sent it: the type list's line break, a CRLF, white space at either end, a trailing ";" and a
    // comment after it, and a character outside the basic plane are each kept as they are.
    [Theory]
    [InlineData("SELECT 1 AS one")]
    [InlineData("SELECT id FROM users WHERE id = @id AND name = @name")]
    [InlineData("\r\n  SELECT id\r\nFROM users\r\nWHERE id = @id  \r\n")]
    [InlineData("DELETE FROM samples WHERE flag; -- all of them\n")]
    [InlineData("SELECT '\U0001F600 é' AS face WHERE @n::int = 1 /* trailing */  ")]
    public async Task DescribeAsync_StatementPrepared_IsListedWithTheTextAsSentAndDescribed(string sql)
    {
        PostgresSession? reading = null;
        PostgresPrepareRequest? sent = null;
        string? listed = null;
        var exchange = new AfterLiveExchange(
            async (method, request, token) =>
            {
                if (request is PostgresPrepareRequest prepare && reading is { } open)
                {
                    sent = prepare;
                    await using var read = await open.Connection.CommandAsync(
                        "SELECT statement FROM pg_catalog.pg_prepared_statements WHERE name = @name",
                        token
                    );
                    _ = read.Parameters.AddWithValue("name", prepare.Statement);
                    listed = (string?)await read.ExecuteScalarAsync(token);
                }
            }
        );
        await using var session = await Sessions.OpenSessionAsync(postgres.ConnectionString, exchange);
        reading = session;

        var described = await session.DescribeAsync(DescribeRequests.Of(sql), TestContext.Current.CancellationToken);

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        var prepared = sent.ShouldNotBeNull();
        listed.ShouldBe(ParameterPlaceholders.PrepareText(prepared.Statement, prepared.Types, prepared.Sql));
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

    // Asks the server, and then runs the test's hook with the call's method and request.  The hook also runs after
    // "open", before the test holds its session, so a hook that needs the session acts only once it is set.
    private sealed class AfterLiveExchange(Func<string, object?, CancellationToken, Task> after) : IDescribeExchange
    {
        public async Task<TAnswer> AskAsync<TRequest, TAnswer>(
            string method,
            TRequest request,
            Func<TRequest, CancellationToken, Task<TAnswer>> live,
            CancellationToken cancellationToken
        )
        {
            var answer = await LiveExchange.Instance.AskAsync(method, request, live, cancellationToken);
            await after(method, request, cancellationToken);
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

    // No text of the failure holds any of the words: a host, a user or a database's own name.
    private static void ShouldNotName(DescribeFailure failure, params string?[] words)
    {
        foreach (var word in words)
        {
            var named = word.ShouldNotBeNull();
            Sessions.TextsOf(failure).ShouldAllBe(text => !text.Contains(named, StringComparison.Ordinal));
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
                !text.Contains(connection, StringComparison.Ordinal)
                && !text.Contains(Secrets.Marker, StringComparison.Ordinal)
            );
        return failure;
    }
}
