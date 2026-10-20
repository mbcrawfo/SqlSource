using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// Step 3 of the run: one session for each database, its queries one at a time, and what is reported on the way.
public sealed class DatabaseRunsTests : IDisposable
{
    private const string Users =
        "-- name: GetUser\nSELECT 1 WHERE @id = 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n";

    private readonly TempFolder _folder = new();
    private readonly FakeDescriber _describer = new();
    private readonly DescribeCapture _capture = new(SidecarValues.Engine.Postgres);
    private readonly CapturingExchange _exchange;
    private readonly Dictionary<string, string> _environment = [];
    private readonly StringWriter _error = new(CultureInfo.InvariantCulture) { NewLine = "\n" };
    private readonly TestProject _project;
    private readonly string _users;
    private ImmutableArray<FileWork> _files;

    public DatabaseRunsTests()
    {
        _exchange = new CapturingExchange(LiveExchange.Instance, _capture);
        _project = Plans.Postgres(_folder);
        _users = _project.AddSql("Users.sql", Users);
    }

    public void Dispose()
    {
        _error.Dispose();
        _folder.Dispose();
    }

    // Decides for the plan and runs its databases.  Both databases have a connection unless "given" says otherwise.
    private async Task<ImmutableArray<FileWork>> RunAsync(
        RunPlan? plan = null,
        bool registered = true,
        string[]? given = null,
        CancellationToken? cancellationToken = null,
        IQueryDescriber? describer = null
    )
    {
        plan ??= Plans.Of(_project);
        var files = _files = RunDecisions.Decide(plan, RunMode.Describe, []);
        var selected = RunDecisions.SelectedDatabases(plan);
        var connections = Connections.Resolve(
            [.. selected.Select(database => database.Name)],
            [.. RunDecisions.DescribableDatabases(plan).Select(database => database.Name)],
            [
                .. (
                    given ?? ["postgres=" + Secrets.ConnectionWithWord, "billing=" + Secrets.ConnectionWithWord]
                ).Select(ConnectionArguments.Parse),
            ],
            name => _environment.GetValueOrDefault(name)
        );
        var host = Hosts.Create(_folder.Path, new FakeProcessRunner(), _folder.Path, environment: _environment) with
        {
            Describers = new DescriberRegistry(registered ? [describer ?? _describer] : []),
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
                $"{_users}(1,10): error SQLSRC216: This version of sqlsource cannot describe 'postgres'\n"
                    + Expected.See("SQLSRC216")
                    + $"{_users}(4,10): error SQLSRC216: This version of sqlsource cannot describe "
                    + $"'postgres'\n{Expected.See("SQLSRC216")}"
            );
    }

    [Fact]
    public async Task Run_Describer_DescribesEachQueryInOneSessionForItsDatabase()
    {
        var files = await RunAsync();

        _error.ToString().ShouldBeEmpty();
        States(files).ShouldBe([QueryState.Described, QueryState.Described]);
        _describer.Opened.Select(request => request.Database).ShouldBe(["postgres", "billing"]);
        _describer.Opened.ShouldAllBe(request =>
            request.Connection == Secrets.ConnectionWithWord && request.Exchange == _exchange
        );
        _describer.Closed.ShouldBe(2);
        _capture.Calls.Select(call => call.Method).ShouldBe(["open", "describe", "open", "describe"]);

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

        var files = await RunAsync(given: ["billing=" + Secrets.ConnectionWithWord]);

        States(files).ShouldBe([QueryState.Failed, QueryState.Failed, QueryState.Described]);
        _describer.Opened.ShouldHaveSingleItem().Database.ShouldBe("billing");
        var more = _project.PathOf("More.sql");
        _error
            .ToString()
            .ShouldBe(
                $"{more}(1,10): error SQLSRC213: No connection is given for the database 'postgres'\n"
                    + "    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES\n"
                    + Expected.See("SQLSRC213")
            );
    }

    [Fact]
    public async Task Run_ConnectionForANameThatIsNoDatabase_IsListedInTheHelpOfSqlsrc213()
    {
        _ = await RunAsync(
            given:
            [
                "billing=" + Secrets.ConnectionWithWord,
                "postgress=" + Secrets.ConnectionWithWord,
                "reports=" + Secrets.ConnectionWithWord,
            ]
        );

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
        _environment["SQLSOURCE_CONNECTION_APP_V2"] = Secrets.ConnectionWithWord;

        var files = await RunAsync(Plans.Of(project), given: []);

        States(files).ShouldBe([QueryState.Failed, QueryState.Failed]);
        _describer.Opened.ShouldBeEmpty();
        _error
            .ToString()
            .ShouldBe(
                $"{sql}(1,10): error SQLSRC214: The databases 'app-v2' and 'app_v2' both read their connection from "
                    + $"SQLSOURCE_CONNECTION_APP_V2\n{Expected.See("SQLSRC214")}"
                    + $"{sql}(5,10): error SQLSRC214: The databases 'app_v2' and 'app-v2' both read their connection "
                    + $"from SQLSOURCE_CONNECTION_APP_V2\n{Expected.See("SQLSRC214")}"
            );
    }

    [Fact]
    public async Task Run_VariableWithNoNameAndTwoDatabases_IsSqlsrc215ForTheOneWithoutAConnection()
    {
        _environment["SQLSOURCE_CONNECTION"] = Secrets.ConnectionWithWord;

        var files = await RunAsync(given: ["postgres=" + Secrets.ConnectionWithWord]);

        States(files).ShouldBe([QueryState.Described, QueryState.Failed]);
        _error
            .ToString()
            .ShouldBe(
                $"{_users}(4,10): error SQLSRC215: SQLSOURCE_CONNECTION is for a run with one database, and this "
                    + "run has 2: postgres, billing\n"
                    + "    help: set SQLSOURCE_CONNECTION_POSTGRES for 'postgres' and SQLSOURCE_CONNECTION_BILLING for "
                    + "'billing', or pass --connection <name>=<connection string> for each\n"
                    + Expected.See("SQLSRC215")
            );
    }

    // The database "ansi" is in the run through a file that cannot be described, so the variable is not for it.
    [Fact]
    public async Task Run_VariableWithNoNameAndADatabaseThatCannotBeDescribed_IsSqlsrc215ThatLeavesItOut()
    {
        _ = _project.AddSql("Old.sql", "-- dialect: ansi\n-- name: Old\nSELECT 1;\n");
        _environment["SQLSOURCE_CONNECTION"] = Secrets.ConnectionWithWord;

        _ = await RunAsync(given: ["postgres=" + Secrets.ConnectionWithWord]);

        _error
            .ToString()
            .ShouldBe(
                $"{_users}(4,10): error SQLSRC215: SQLSOURCE_CONNECTION is for a run with one database, and this "
                    + "run has 2: postgres, billing\n"
                    + "    help: set SQLSOURCE_CONNECTION_POSTGRES for 'postgres' and SQLSOURCE_CONNECTION_BILLING for "
                    + "'billing', or pass --connection <name>=<connection string> for each\n"
                    + Expected.See("SQLSRC215")
            );
    }

    [Fact]
    public async Task Run_SessionThatCannotBeOpened_IsReportedOnceAndTheDatabasesQueriesFail()
    {
        _ = _project.AddSql("More.sql", "-- name: More\nSELECT 3;\n");
        _describer.OpenFailure = FakeDescriber.Failure(
            "(open)",
            step: null,
            help: null,
            "28P01: password authentication failed"
        );

        var files = await RunAsync(given: ["postgres=" + Secrets.ConnectionWithWord]);

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
        _describer.OpenFailure = FakeDescriber.Failure(
            $"open on {Secrets.ConnectionWithWord}",
            step: null,
            $"check {Secrets.ConnectionWithWord}",
            $"invalid connection string: {Secrets.ConnectionWithWord}"
        );

        _ = await RunAsync(given: ["postgres=" + Secrets.ConnectionWithWord]);

        _error.ToString().ShouldNotContain(Secrets.Word);
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

    // A driver's message for a wrong password can quote the password alone.
    [Fact]
    public async Task Run_SessionThatCannotBeOpenedAndHoldsAPasswordTheDescriberGave_IsPrintedWithoutIt()
    {
        _describer.Secrets = ["hunter2"];
        _describer.OpenFailure = FakeDescriber.Failure(
            "(open)",
            step: null,
            help: null,
            "28P01: password 'hunter2' was refused"
        );

        _ = await RunAsync(given: ["postgres=Host=db;Password=hunter2"]);

        _error.ToString().ShouldContain("    server: 28P01: password '***' was refused\n");
        _error.ToString().ShouldNotContain("hunter2");
    }

    // The run prints the database's name and the engine's whatever the describer does, so a password equal to them
    // is not removed from them.
    [Fact]
    public async Task Run_QueryThatFailsWithAPasswordThatIsTheDatabasesNameAndTheEngines_PrintsTheQueryLineAsItIs()
    {
        _describer.Secrets = ["postgres"];
        _describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        _ = await RunAsync(given: ["postgres=Host=db;Password=postgres"]);

        _error.ToString().ShouldContain("    query: GetUser, database postgres, postgres 16.4, FakeDriver 1.2.3\n");
    }

    // The same for the database's name alone, which is not the engine's.
    [Fact]
    public async Task Run_QueryThatFailsWithAPasswordThatIsTheDatabasesName_PrintsTheQueryLineAsItIs()
    {
        _describer.Secrets = ["billing"];
        _describer.Failures["GetInvoice"] = FakeDescriber.Failure("GetInvoice");

        _ = await RunAsync(given: ["postgres=Host=db;Password=billing", "billing=Host=db;Password=billing"]);

        _error.ToString().ShouldContain("    query: GetInvoice, database billing, postgres 16.4, FakeDriver 1.2.3\n");
    }

    // The guard that asks for the parts knows only the whole value.
    [Fact]
    public async Task Run_DescriberThatThrowsWhenAskedForTheSecrets_IsOneFaultWithoutTheValue()
    {
        _describer.SecretsException = new InvalidOperationException($"cannot read {Secrets.ConnectionWithWord}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault.InnerException.ShouldBeNull();
        fault.Diagnostic.Descriptor.Id.ShouldBe("SQLSRC200");
        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.InvalidOperationException", "cannot read ***"]);
        _error.ToString().ShouldBeEmpty();
        _describer.Opened.ShouldBeEmpty();
    }

    // The list of the parts is the describer's too, and it is read under the same guard.
    [Fact]
    public async Task Run_DescriberWhoseSecretsThrowWhenTheyAreRead_IsOneFaultWithoutTheValue()
    {
        _describer.Secrets = new ThrowsWhenRead($"cannot list {Secrets.ConnectionWithWord}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault.InnerException.ShouldBeNull();
        fault.Diagnostic.Descriptor.Id.ShouldBe("SQLSRC200");
        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.InvalidOperationException", "cannot list ***"]);
        _error.ToString().ShouldBeEmpty();
        _describer.Opened.ShouldBeEmpty();
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
        _describer.Server = new ServerInfo(
            $"16.4 at {Secrets.ConnectionWithWord}",
            Secrets.ConnectionWithWord,
            Secrets.ConnectionWithWord,
            EquatableArray<ServerSetting>.Empty
        );
        _describer.Failures["GetUser"] = FakeDescriber.Failure(
            $"GetUser on {Secrets.ConnectionWithWord}",
            DescribeStep.Catalog,
            $"check {Secrets.ConnectionWithWord}",
            $"could not connect to {Secrets.ConnectionWithWord}"
        );
        // A query that is described would put the server's version, which holds the value, into its entry, and that
        // ends the run.
        _describer.Failures["GetInvoice"] = FakeDescriber.Failure("GetInvoice");

        _ = await RunAsync();

        _error.ToString().ShouldNotContain(Secrets.Word);
        _error
            .ToString()
            .ShouldBe(
                $"{_users}(1,10): error SQLSRC999: The server rejected the query 'GetUser on ***'\n"
                    + "    query: GetUser, database postgres, postgres 16.4 at ***, *** ***\n"
                    + "    step: catalog\n"
                    + "    server: could not connect to ***\n"
                    + "    help: check ***\n"
                    + "    see: https://example.test/sqlsrc999\n"
                    + $"{_users}(4,10): error SQLSRC999: The server rejected the query 'GetInvoice'\n"
                    + "    query: GetInvoice, database billing, postgres 16.4 at ***, *** ***\n"
                    + "    step: describe columns\n"
                    + "    help: check the SQL\n"
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

        _describer.Exceptions["GetUser"] = new ArgumentException(
            $"Keyword not supported in '{Secrets.ConnectionWithWord}'"
        );

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault.InnerException.ShouldBeNull();
        fault.Message.ShouldNotContain(Secrets.Word);
        fault.Diagnostic.Descriptor.Id.ShouldBe("SQLSRC200");
        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.ArgumentException", "Keyword not supported in '***'"]);
        (fault.Diagnostic.Lines.Count > 0).ShouldBe(debug is not null);
        fault.Diagnostic.Lines.ShouldAllBe(line => line.Label == "trace" && !line.Text.Contains(Secrets.Word));
        // The session is closed all the same.
        _describer.Closed.ShouldBe(1);
    }

    // The settings of the server are a describer's text: the event that names them is built inside the guard, so that
    // a setting without a name is the describer's fault and not a bare exception.
    [Fact]
    public async Task Run_ServerWithASettingThatHasNoName_IsOneSqlsrc200AndClosesTheSession()
    {
        _describer.Server = _describer.Server with
        {
            Settings = new EquatableArray<ServerSetting>([new ServerSetting(null!, "x")]),
        };

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault.Diagnostic.Descriptor.Id.ShouldBe("SQLSRC200");
        fault.Diagnostic.Arguments[0].ShouldBe("System.NullReferenceException");
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
        fault.Diagnostic.Lines.ShouldAllBe(line => !line.Text.Contains(Secrets.Word) && !line.Text.Contains("Host=db"));
        fault.Diagnostic.Lines.ShouldContain(line => line.Text.Contains("inner ***"));
    }

    [Fact]
    public async Task Run_DescriberThatThrowsOnOpen_IsAFaultWithoutTheConnection()
    {
        _describer.OpenException = new InvalidOperationException($"cannot parse {Secrets.ConnectionWithWord}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.InvalidOperationException", "cannot parse ***"]);
    }

    // The describer of the tests throws from a task.  One that throws before it has a task to return, a method that
    // is not async for one, is the same fault, and its text is held to the same.
    [Fact]
    public async Task Run_DescriberThatThrowsOnOpenBeforeItReturnsATask_IsAFaultWithoutTheConnection()
    {
        var fault = await Should.ThrowAsync<DescriberFaultException>(() =>
            RunAsync(describer: new ThrowsBeforeItReturns())
        );

        fault.InnerException.ShouldBeNull();
        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.InvalidOperationException", "cannot parse ***"]);
    }

    [Fact]
    public async Task Run_DescriberThatReturnsNothing_IsAFault()
    {
        _describer.OpensNothing = true;

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault
            .Diagnostic.Arguments.ToArray()
            .ShouldBe(["System.InvalidOperationException", "A describer returned nothing."]);
    }

    private async Task ShouldBeTheFaultOfNothingAsync()
    {
        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault
            .Diagnostic.Arguments.ToArray()
            .ShouldBe(["System.InvalidOperationException", "A describer returned nothing."]);
    }

    [Fact]
    public async Task Run_DescriberThatFailsWithNoFailure_IsAFault()
    {
        _describer.OpenFailsWithNothing = true;

        await ShouldBeTheFaultOfNothingAsync();
    }

    // An outcome that says it succeeded and holds no value is nothing too: the run reads the value, not the claim.
    [Fact]
    public async Task Run_DescriberThatOpensWithNoSession_IsAFault()
    {
        _describer.OpensWithNoSession = true;

        await ShouldBeTheFaultOfNothingAsync();
    }

    [Fact]
    public async Task Run_SessionThatReturnsNothingForAQuery_IsAFault()
    {
        _describer.DescribeResult = static () => null;

        await ShouldBeTheFaultOfNothingAsync();
    }

    [Fact]
    public async Task Run_SessionThatDescribesWithNoDescription_IsAFault()
    {
        _describer.DescribeResult = static () => Outcome<QueryDescription, DescribeFailure>.Of(null!);

        await ShouldBeTheFaultOfNothingAsync();
    }

    [Fact]
    public async Task Run_SessionThatFailsAQueryWithNoFailure_IsAFault()
    {
        _describer.DescribeResult = static () => Outcome<QueryDescription, DescribeFailure>.Failed(null!);

        await ShouldBeTheFaultOfNothingAsync();
    }

    [Fact]
    public async Task Run_SessionThatThrowsWhenItIsClosed_IsAFaultWithoutTheConnection()
    {
        _describer.CloseException = new IOException($"lost {Secrets.ConnectionWithWord}");

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

    // A description that holds the value would be written into a file that is committed.
    private async Task<DescriberFaultException> RunExpectingTheValueInTheDescriptionAsync(string secret)
    {
        var fault = await Should.ThrowAsync<DescriberFaultException>(() =>
            RunAsync(given: ["postgres=" + secret, "billing=" + secret])
        );

        fault.InnerException.ShouldBeNull();
        fault.Message.ShouldNotContain(Secrets.Word);
        fault.Diagnostic.Descriptor.Id.ShouldBe("SQLSRC200");
        fault
            .Diagnostic.Arguments.ToArray()
            .ShouldBe([
                "System.InvalidOperationException",
                "The describer put the connection's value in its description of the query 'GetUser'.",
            ]);
        fault.Diagnostic.Arguments.ShouldAllBe(argument => !argument.Contains(Secrets.Word));
        _files[0].Queries[0].State.ShouldNotBe(QueryState.Described);
        _files[0].Queries[0].Entry.ShouldBeNull();
        _describer.Closed.ShouldBe(1);
        return fault;
    }

    [Fact]
    public async Task Run_DescriptionWithTheValueInTheServersVersion_IsAFault()
    {
        _describer.Server = FakeDescriber.ServerWith($"16.4 at {Secrets.ConnectionWithWord}");

        _ = await RunExpectingTheValueInTheDescriptionAsync(Secrets.ConnectionWithWord);
    }

    [Fact]
    public async Task Run_DescriptionWithTheValueInAColumnsName_IsAFault()
    {
        _describer.Descriptions["GetUser"] = FakeDescriber.NoRows with
        {
            ResultKind = SidecarResultKind.Rows,
            Columns = new EquatableArray<SidecarColumn>([
                new SidecarColumn(
                    0,
                    $"id of {Secrets.ConnectionWithWord}",
                    new OtherEngineType("int4"),
                    null,
                    null,
                    null,
                    null,
                    null
                ),
            ]),
        };

        _ = await RunExpectingTheValueInTheDescriptionAsync(Secrets.ConnectionWithWord);
    }

    // A description is searched for the whole value alone: a password that equals a name of the catalog is no fault.
    [Fact]
    public async Task Run_DescriptionWithAPasswordTheDescriberGaveAsAColumnsName_IsDescribed()
    {
        _describer.Secrets = ["admin"];
        _describer.Descriptions["GetUser"] = FakeDescriber.NoRows with
        {
            ResultKind = SidecarResultKind.Rows,
            Columns = new EquatableArray<SidecarColumn>([
                new SidecarColumn(0, "admin", new OtherEngineType("int4"), null, null, null, null, null),
            ]),
        };

        _ = await RunAsync(given: ["postgres=Host=db;Password=admin", "billing=Host=db;Password=admin"]);

        _error.ToString().ShouldBeEmpty();
        _files[0].Queries[0].State.ShouldBe(QueryState.Described);
        _files[0].Queries[0].Entry.ShouldNotBeNull().Columns.ShouldNotBeNull()[0].Name.ShouldBe("admin");
    }

    // The writer escapes a quote and a backslash, so the value is in the file as it escapes it.
    [Fact]
    public async Task Run_DescriptionWithAValueOfAQuoteAndABackslashInTheServersVersion_IsAFault()
    {
        const string Awkward = "Pwd=\"s3cret\\\"\r\n";
        _describer.Server = FakeDescriber.ServerWith($"16.4 {Awkward}");

        _ = await RunExpectingTheValueInTheDescriptionAsync(Awkward);
    }

    [Fact]
    public async Task Run_DescriptionWithoutTheValue_IsDescribedAsBefore()
    {
        _describer.Server = FakeDescriber.ServerWith();
        _describer.Descriptions["GetUser"] = FakeDescriber.NoRows with
        {
            ResultKind = SidecarResultKind.Rows,
            Columns = new EquatableArray<SidecarColumn>([
                new SidecarColumn(0, "id", new OtherEngineType("int4"), null, null, null, null, null),
            ]),
        };

        var files = await RunAsync();

        States(files).ShouldBe([QueryState.Described, QueryState.Described]);
        _error.ToString().ShouldBeEmpty();
    }

    // A database is opened, and needs a connection, only for a query to describe.
    [Fact]
    public async Task Run_DatabaseWithNothingToDescribe_NeedsNoConnectionAndIsNotOpened()
    {
        _ = _project.AddSql("Tokens.sql", "-- name: Find\n-- database: billing\nSELECT 1 {{where}};\n");
        var plan = Plans.Of(_project);
        _ = SidecarFiles.WriteCurrent(plan.Files.Single(file => file.Path == _users));

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
        _describer.CloseException = new IOException($"lost {Secrets.ConnectionWithWord}");

        _ = await Should.ThrowAsync<OperationCanceledException>(() => RunAsync(cancellationToken: cancel.Token));

        _describer.Closed.ShouldBe(1);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_DescriberThatThrowsAndTheSessionThrowsWhenItIsClosed_IsAFaultOfTheFirstException()
    {
        _describer.Exceptions["GetUser"] = new ArgumentException($"broken {Secrets.ConnectionWithWord}");
        _describer.CloseException = new IOException($"lost {Secrets.ConnectionWithWord}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.ArgumentException", "broken ***"]);
        _describer.Closed.ShouldBe(1);
    }

    // A describer whose OpenAsync is not async.
    private sealed class ThrowsBeforeItReturns : IQueryDescriber
    {
        public SqlDialect Dialect => SqlDialect.PostgreSql;

        public IReadOnlyList<string> SecretsOf(string connection) => [];

        public Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
            OpenRequest request,
            IRunLog log,
            CancellationToken cancellationToken
        ) => throw new InvalidOperationException($"cannot parse {request.Connection}");
    }

    // A list of a describer's parts that throws whenever it is read.
    private sealed class ThrowsWhenRead(string message) : IReadOnlyList<string>
    {
        public int Count => throw new InvalidOperationException(message);

        public string this[int index] => throw new InvalidOperationException(message);

        public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException(message);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
