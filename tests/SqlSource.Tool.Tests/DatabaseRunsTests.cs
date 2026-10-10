using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// Step 3 of the run: one session for each database, its queries one at a time, and what is reported on the way.
public sealed class DatabaseRunsTests : IDisposable
{
    private const string Secret = "Host=db;Password=s3cret";

    private const string See = "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc";

    private const string Users =
        "-- name: GetUser\nSELECT 1 WHERE @id = 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n";

    private readonly TempFolder _folder = new();
    private readonly FakeDescriber _describer = new();
    private readonly RecordingExchange _exchange = new();
    private readonly Dictionary<string, string> _environment = [];
    private readonly StringWriter _error = new(CultureInfo.InvariantCulture) { NewLine = "\n" };
    private readonly TestProject _project;
    private readonly string _users;

    public DatabaseRunsTests()
    {
        _project = Plans.Postgres(_folder);
        _users = _project.AddSql("Users.sql", Users);
    }

    public void Dispose()
    {
        _error.Dispose();
        _folder.Dispose();
    }

    private static ConnectionArgument Parse(string text)
    {
        ConnectionArgument.TryParse(text, out var argument).ShouldBeTrue();
        return argument.ShouldNotBeNull();
    }

    // Decides for the plan and runs its databases.  Both databases have a connection unless "given" says otherwise.
    private async Task<ImmutableArray<FileWork>> RunAsync(
        RunPlan? plan = null,
        bool registered = true,
        string[]? given = null,
        CancellationToken? cancellationToken = null
    )
    {
        plan ??= Plans.Of(_project);
        var files = RunDecisions.Decide(plan, force: false, []);
        var selected = RunDecisions.SelectedDatabases(plan);
        var connections = Connections.Resolve(
            [.. selected.Select(database => database.Name)],
            [.. (given ?? ["postgres=" + Secret, "billing=" + Secret]).Select(Parse)],
            name => _environment.GetValueOrDefault(name)
        );
        var host = Hosts.Create(_folder.Path, new FakeProcessRunner(), _folder.Path, environment: _environment) with
        {
            Describers = new DescriberRegistry(registered ? [_describer] : []),
            Exchange = _ => _exchange,
        };

        await DatabaseRuns.RunAsync(
            selected,
            files,
            connections,
            host,
            new Reporter(_error),
            cancellationToken ?? TestContext.Current.CancellationToken
        );
        return files;
    }

    private static QueryState[] States(ImmutableArray<FileWork> files) =>
        [.. files.SelectMany(file => file.Queries).Select(query => query.State)];

    [Fact]
    public async Task Run_NoDescriberForTheDialect_IsSqlsrc216AtEachQuery()
    {
        var files = await RunAsync(registered: false);

        States(files).ShouldBe([QueryState.Failed, QueryState.Failed]);
        _error
            .ToString()
            .ShouldBe(
                $"{_users}(1,10): error SQLSRC216: This version of sqlsource cannot describe 'postgres'\n{See}216\n"
                    + $"{_users}(4,10): error SQLSRC216: This version of sqlsource cannot describe "
                    + $"'postgres'\n{See}216\n"
            );
    }

    [Fact]
    public async Task Run_Describer_DescribesEachQueryInOneSessionForItsDatabase()
    {
        var files = await RunAsync();

        _error.ToString().ShouldBeEmpty();
        States(files).ShouldBe([QueryState.Described, QueryState.Described]);
        _describer.Opened.Select(request => request.Database).ShouldBe(["postgres", "billing"]);
        _describer.Opened.ShouldAllBe(request => request.Connection == Secret && request.Exchange == _exchange);
        _describer.Closed.ShouldBe(2);
        _exchange.Methods.ShouldBe(["open", "describe", "open", "describe"]);

        var getUser = _describer.Described[0];
        getUser.Name.ShouldBe("GetUser");
        getUser.Sql.ShouldBe("SELECT 1 WHERE @id = 1;");
        getUser.Parameters.ShouldHaveSingleItem().Name.ShouldBe("id");

        var entry = files[0].Queries[1].Entry.ShouldNotBeNull();
        entry.Name.ShouldBe("GetInvoice");
        entry.Hash.ShouldBe(files[0].Queries[1].Planned.Hash);
        entry.Engine.ShouldBe("postgres");
        entry.Database.ShouldBe("billing");
        entry.ServerVersion.ShouldBe("16.4");
    }

    [Fact]
    public async Task Run_DatabaseWithoutAConnection_IsSqlsrc213OnceAndItsQueriesFail()
    {
        _ = _project.AddSql("More.sql", "-- name: More\nSELECT 3;\n");

        var files = await RunAsync(given: ["billing=" + Secret]);

        States(files).ShouldBe([QueryState.Failed, QueryState.Failed, QueryState.Described]);
        _describer.Opened.ShouldHaveSingleItem().Database.ShouldBe("billing");
        var more = _project.PathOf("More.sql");
        _error
            .ToString()
            .ShouldBe(
                $"{more}(1,10): error SQLSRC213: No connection is given for the database 'postgres'\n"
                    + "    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES\n"
                    + $"{See}213\n"
            );
    }

    [Fact]
    public async Task Run_ConnectionForANameThatIsNoDatabase_IsListedInTheHelpOfSqlsrc213()
    {
        _ = await RunAsync(given: ["billing=" + Secret, "postgress=" + Secret, "reports=" + Secret]);

        _error
            .ToString()
            .ShouldContain(
                "    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES.  "
                    + "--connection was given for postgress, reports, which are no databases of this run\n"
            );
    }

    [Fact]
    public async Task Run_TwoDatabasesWithOneVariable_IsSqlsrc214AtEachThatHasAQueryToDescribe()
    {
        var project = Plans.Postgres(_folder, "Other");
        var sql = project.AddSql(
            "Q.sql",
            "-- name: A\n-- database: app-v2\nSELECT 1;\n\n-- name: B\n-- database: app_v2\nSELECT 2;\n"
        );
        _environment["SQLSOURCE_CONNECTION_APP_V2"] = Secret;

        var files = await RunAsync(Plans.Of(project), given: []);

        States(files).ShouldBe([QueryState.Failed, QueryState.Failed]);
        _describer.Opened.ShouldBeEmpty();
        _error
            .ToString()
            .ShouldBe(
                $"{sql}(1,10): error SQLSRC214: The databases 'app-v2' and 'app_v2' both read their connection from "
                    + $"SQLSOURCE_CONNECTION_APP_V2\n{See}214\n"
                    + $"{sql}(5,10): error SQLSRC214: The databases 'app_v2' and 'app-v2' both read their connection "
                    + $"from SQLSOURCE_CONNECTION_APP_V2\n{See}214\n"
            );
    }

    [Fact]
    public async Task Run_VariableWithNoNameAndTwoDatabases_IsSqlsrc215ForTheOneWithoutAConnection()
    {
        _environment["SQLSOURCE_CONNECTION"] = Secret;

        var files = await RunAsync(given: ["postgres=" + Secret]);

        States(files).ShouldBe([QueryState.Described, QueryState.Failed]);
        _error
            .ToString()
            .ShouldBe(
                $"{_users}(4,10): error SQLSRC215: SQLSOURCE_CONNECTION is for a run with one database, and this "
                    + "run has 2: postgres, billing\n"
                    + "    help: set SQLSOURCE_CONNECTION_POSTGRES for 'postgres' and SQLSOURCE_CONNECTION_BILLING for "
                    + "'billing', or pass --connection <name>=<connection string> for each\n"
                    + $"{See}215\n"
            );
    }

    [Fact]
    public async Task Run_SessionThatCannotBeOpened_IsReportedOnceAndTheDatabasesQueriesFail()
    {
        _ = _project.AddSql("More.sql", "-- name: More\nSELECT 3;\n");
        _describer.OpenFailure = new DescribeFailure(
            FakeDescriber.Rejected,
            new EquatableArray<string>(["(open)"]),
            Step: null,
            new EquatableArray<string>(["28P01: password authentication failed"]),
            Help: null
        );

        var files = await RunAsync(given: ["postgres=" + Secret]);

        States(files).ShouldAllBe(state => state == QueryState.Failed);
        _describer.Described.ShouldBeEmpty();
        var more = _project.PathOf("More.sql");
        // SQLSRC213 for billing comes after, at its own query.
        _error
            .ToString()
            .ShouldStartWith(
                $"{more}(1,10): error SQLSRC999: The server rejected the query '(open)'\n"
                    + "    query: More, database postgres, postgres\n"
                    + "    server: 28P01: password authentication failed\n"
                    + "    see: https://example.test/sqlsrc999\n"
            );
    }

    // A driver that rejects a wrong connection string can quote it, and does so when the session is opened.
    [Fact]
    public async Task Run_SessionThatCannotBeOpenedAndHoldsTheConnection_IsPrintedWithoutIt()
    {
        _describer.OpenFailure = new DescribeFailure(
            FakeDescriber.Rejected,
            new EquatableArray<string>([$"open on {Secret}"]),
            Step: null,
            new EquatableArray<string>([$"invalid connection string: {Secret}"]),
            $"check {Secret}"
        );

        _ = await RunAsync(given: ["postgres=" + Secret]);

        _error.ToString().ShouldNotContain("s3cret");
        _error
            .ToString()
            .ShouldStartWith(
                $"{_users}(1,10): error SQLSRC999: The server rejected the query 'open on ***'\n"
                    + "    query: GetUser, database postgres, postgres\n"
                    + "    server: invalid connection string: ***\n"
                    + "    help: check ***\n"
                    + "    see: https://example.test/sqlsrc999\n"
            );
    }

    [Fact]
    public async Task Run_QueryThatFails_IsReportedInTheFormatOfADescribersErrorAndTheSessionGoesOn()
    {
        _ = _project.AddSql("More.sql", "-- name: Bad\nSELECT 3;\n\n-- name: Good\nSELECT 4;\n");
        _describer.Failures["Bad"] = FakeDescriber.Failure("Bad", "42P01: relation \"users\" does not exist", "LINE 1");

        var files = await RunAsync();

        States(files).ShouldBe([QueryState.Failed, QueryState.Described, QueryState.Described, QueryState.Described]);
        _describer.Closed.ShouldBe(2);
        _error
            .ToString()
            .ShouldBe(
                $"{_project.PathOf("More.sql")}(1,10): error SQLSRC999: The server rejected the query 'Bad'\n"
                    + "    query: Bad, database postgres, postgres 16.4, FakeDriver 1.2.3\n"
                    + "    step: describe columns\n"
                    + "    server: 42P01: relation \"users\" does not exist\n"
                    + "    server: LINE 1\n"
                    + "    help: check the SQL\n"
                    + "    see: https://example.test/sqlsrc999\n"
            );
    }

    [Fact]
    public async Task Run_DriverWithoutAVersion_IsNamedAlone()
    {
        _describer.Server = _describer.Server with { DriverVersion = null };
        _describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        _ = await RunAsync();

        _error.ToString().ShouldContain("    query: GetUser, database postgres, postgres 16.4, FakeDriver\n");
    }

    // The describer's rule is to put the value in nothing it returns.  The run does not rely on it.
    [Fact]
    public async Task Run_FailureThatHoldsTheConnection_IsPrintedWithoutIt()
    {
        _describer.Server = new ServerInfo($"16.4 at {Secret}", Secret, Secret, EquatableArray<ServerSetting>.Empty);
        _describer.Failures["GetUser"] = new DescribeFailure(
            FakeDescriber.Rejected,
            new EquatableArray<string>([$"GetUser on {Secret}"]),
            DescribeStep.Catalog,
            new EquatableArray<string>([$"could not connect to {Secret}"]),
            $"check {Secret}"
        );

        _ = await RunAsync();

        _error.ToString().ShouldNotContain("s3cret");
        _error
            .ToString()
            .ShouldBe(
                $"{_users}(1,10): error SQLSRC999: The server rejected the query 'GetUser on ***'\n"
                    + "    query: GetUser, database postgres, postgres 16.4 at ***, *** ***\n"
                    + "    step: catalog\n"
                    + "    server: could not connect to ***\n"
                    + "    help: check ***\n"
                    + "    see: https://example.test/sqlsrc999\n"
            );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public async Task Run_DescriberThatThrows_IsAFaultWithoutTheConnection(string? debug)
    {
        if (debug is not null)
        {
            _environment[Cli.DebugVariable] = debug;
        }

        _describer.Exceptions["GetUser"] = new ArgumentException($"Keyword not supported in '{Secret}'");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault.InnerException.ShouldBeNull();
        fault.Message.ShouldNotContain("s3cret");
        fault.Diagnostic.Descriptor.Id.ShouldBe("SQLSRC200");
        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.ArgumentException", "Keyword not supported in '***'"]);
        (fault.Diagnostic.Lines.Count > 0).ShouldBe(debug is not null);
        fault.Diagnostic.Lines.ShouldAllBe(line => line.Label == "trace" && !line.Text.Contains("s3cret"));
        // The session is closed all the same.
        _describer.Closed.ShouldBe(1);
    }

    // A value that spans lines is in the stack trace across the lines it is cut into.
    [Fact]
    public async Task Run_DescriberThatThrowsWithAConnectionOfSeveralLines_IsAFaultWithoutIt()
    {
        const string Spread = "Host=db;\r\nPassword=s3cret";
        _environment[Cli.DebugVariable] = "1";
        _describer.Exceptions["GetUser"] = new ArgumentException($"Bad '{Spread}'", new IOException($"inner {Spread}"));

        var fault = await Should.ThrowAsync<DescriberFaultException>(() =>
            RunAsync(given: ["postgres=" + Spread, "billing=" + Spread])
        );

        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.ArgumentException", "Bad '***'"]);
        fault.Diagnostic.Lines.Count.ShouldBeGreaterThan(0);
        fault.Diagnostic.Lines.ShouldAllBe(line => !line.Text.Contains("s3cret") && !line.Text.Contains("Host=db"));
        fault.Diagnostic.Lines.ShouldContain(line => line.Text.Contains("inner ***"));
    }

    [Fact]
    public async Task Run_DescriberThatThrowsOnOpen_IsAFaultWithoutTheConnection()
    {
        _describer.OpenException = new InvalidOperationException($"cannot parse {Secret}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.InvalidOperationException", "cannot parse ***"]);
    }

    // Review focus 2.
    [Fact]
    public async Task Run_DescriberThatReturnsNothing_IsAFault()
    {
        _describer.OpensNothing = true;

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault
            .Diagnostic.Arguments.ToArray()
            .ShouldBe(["System.InvalidOperationException", "A describer returned nothing."]);
    }

    // Review focus 2.
    [Fact]
    public async Task Run_SessionThatThrowsWhenItIsClosed_IsAFaultWithoutTheConnection()
    {
        _describer.CloseException = new IOException($"lost {Secret}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.IO.IOException", "lost ***"]);
    }

    [Fact]
    public async Task Run_DescriptionWithAParameterTheQueryLacks_IsAFault()
    {
        _describer.Descriptions["GetUser"] = FakeDescriber.NoRows with
        {
            Parameters = new EquatableArray<DescribedParameter>([new DescribedParameter("nope", null, null)]),
        };

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault
            .Diagnostic.Arguments[1]
            .ShouldBe("The describer gave the parameter 'nope', which the query 'GetUser' does not have.");
    }

    // Review focus 5: a database is opened, and needs a connection, only for a query to describe.
    [Fact]
    public async Task Run_DatabaseWithNothingToDescribe_NeedsNoConnectionAndIsNotOpened()
    {
        _ = _project.AddSql("Tokens.sql", "-- name: Find\n-- database: billing\nSELECT 1 {{where}};\n");
        var plan = Plans.Of(_project);
        _ = TestSidecar.WriteCurrent(plan.Files.Single(file => file.Path == _users));

        var files = await RunAsync(plan, given: []);

        _error.ToString().ShouldBeEmpty();
        _describer.Opened.ShouldBeEmpty();
        States(files).ShouldBe([QueryState.Failed, QueryState.Skipped, QueryState.Skipped]);
    }

    [Fact]
    public async Task Run_CancelledWhileAQueryIsDescribed_EndsAsCancelledAndNotAsAFault()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _describer.BeforeDescribe = _ => cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => RunAsync(cancellationToken: cancel.Token));

        _ = _describer.Described.ShouldHaveSingleItem();
        _describer.Closed.ShouldBe(1);
        _error.ToString().ShouldBeEmpty();
    }

    // The first exception is the one the run ends with: a close that fails after it is dropped.
    [Fact]
    public async Task Run_CancelledAndTheSessionThrowsWhenItIsClosed_EndsAsCancelled()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _describer.BeforeDescribe = _ => cancel.CancelAsync();
        _describer.CloseException = new IOException($"lost {Secret}");

        _ = await Should.ThrowAsync<OperationCanceledException>(() => RunAsync(cancellationToken: cancel.Token));

        _describer.Closed.ShouldBe(1);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_DescriberThatThrowsAndTheSessionThrowsWhenItIsClosed_IsAFaultOfTheFirstException()
    {
        _describer.Exceptions["GetUser"] = new ArgumentException($"broken {Secret}");
        _describer.CloseException = new IOException($"lost {Secret}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.ArgumentException", "broken ***"]);
        _describer.Closed.ShouldBe(1);
    }

    [Fact]
    public void Of_TextThatHoldsTheSecretTwice_HasTheMarkInBothPlaces() =>
        Redaction.Of("a s3cret and a s3cret", "s3cret").ShouldBe("a *** and a ***");
}
