using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Logging;

// No value of a connection reaches the log of a whole run: not from the command line, and not from a describer.
public sealed class RunLogRedactionTests : IDisposable
{
    private readonly DescribeScene _scene = new();

    public RunLogRedactionTests()
    {
        _scene.Run.Environment.Clear();
        _ = _scene.Project.AddSql("Users.sql", SqlTexts.GetUser);
    }

    private string Log => _scene.Run.Folder.PathOf("run.log");

    public void Dispose() => _scene.Dispose();

    // Whichever way the option was written, the token keeps the option and loses the value, and with it the name.
    [Theory]
    [InlineData("--connection|***", "--connection", "postgres=" + Secrets.ConnectionWithMarker)]
    [InlineData("--connection=***", "--connection=postgres=" + Secrets.ConnectionWithMarker)]
    [InlineData("--connection:***", "--connection:postgres=" + Secrets.ConnectionWithMarker)]
    public async Task Run_ConnectionOnTheCommandLine_IsLoggedWithNeitherItsNameNorItsValue(
        string logged,
        params string[] args
    )
    {
        var result = await _scene.DescribeAsync(["--log", "run.log", .. args]);

        result.ExitCode.ShouldBe(0);
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe(Secrets.ConnectionWithMarker);
        LogReader
            .Read(Log)[0]
            .GetProperty("arguments")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe(["describe", "--log", "run.log", .. logged.Split('|')]);
        (await File.ReadAllTextAsync(Log, TestContext.Current.CancellationToken)).ShouldNotContain(Secrets.Marker);
    }

    // The value was typed where "name=value" stands and the shell split it: the line is wrong, and nothing is logged.
    [Fact]
    public async Task Run_WrongCommandLineThatHoldsAValue_WritesNoLog()
    {
        var result = await _scene.DescribeAsync(
            "--log",
            "run.log",
            "--connection",
            "postgres=Host=db;",
            "Password=" + Secrets.Marker
        );

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldNotContain(Secrets.Marker);
        File.Exists(Log).ShouldBeFalse();
    }

    // A value with the two characters that JSON writes another way.
    private const string Escaped = "Host=db;Password=\"s3cret\\marker\"";

    private async Task<string> LoggedWithAsync(string value)
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = value;
        _ = await _scene.DescribeAsync("--log", "run.log");
        return await File.ReadAllTextAsync(Log, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Run_DescriberThatLogsItsConnection_HasTheMarkInEveryPlaceAndTheValueNowhere()
    {
        _scene.Describer.OnOpen = (log, request) =>
            log.Write(
                "connect " + request.Connection,
                new(request.Connection, request.Connection),
                new("message", "cannot connect with '" + request.Connection + "'"),
                new("list", LogValue.ListOf([LogValue.MapOf(new LogMember("deep", request.Connection))]))
            );

        var text = await LoggedWithAsync(Secrets.ConnectionWithMarker);

        text.ShouldNotContain(Secrets.Marker);
        var line = LogReader.Events(Log, "postgres.connect ***").ShouldHaveSingleItem();
        line.GetProperty("***").GetString().ShouldBe("***");
        line.GetProperty("message").GetString().ShouldBe("cannot connect with '***'");
        line.GetProperty("list")[0].GetProperty("deep").GetString().ShouldBe("***");
    }

    // The value is removed before the line is written, so the file holds it neither as it is nor as JSON escapes it.
    [Fact]
    public async Task Run_DescriberThatLogsAConnectionThatJsonEscapes_LeavesTheValueInNeitherForm()
    {
        _scene.Describer.OnOpen = (log, request) =>
            log.Write("connect", new LogMember("with", "<" + request.Connection + ">"));

        var text = await LoggedWithAsync(Escaped);

        text.ShouldNotContain(Secrets.Word);
        LogReader
            .Events(Log, "postgres.connect")
            .ShouldHaveSingleItem()
            .GetProperty("with")
            .GetString()
            .ShouldBe("<***>");
    }

    // A password that the describer said is secret is removed where it stands as a token.
    [Fact]
    public async Task Run_DescriberThatLogsAPasswordItSaidIsSecret_HasTheMarkThereAndTheWordInsideANameLeft()
    {
        _scene.Describer.Secrets = ["test"];
        _scene.Describer.OnOpen = (log, _) =>
            log.Write("connect", new LogMember("message", "password test refused, table latest_orders"));

        _ = await LoggedWithAsync("Host=db;Password=test");

        LogReader
            .Events(Log, "postgres.connect")
            .ShouldHaveSingleItem()
            .GetProperty("message")
            .GetString()
            .ShouldBe("password *** refused, table latest_orders");
    }

    // The events of the SQL Server describer hold the SQL, the declarations, the server's rows and its messages, so the
    // value is searched in each: the fake describer writes them through its log as the real one does.  The scene's fake
    // describer is PostgreSQL's, so the events of these tests are named postgres.parameters, postgres.probe,
    // postgres.declared, postgres.columns, postgres.table, postgres.column and postgres.table-match.
    [Fact]
    public async Task Run_DescriberThatLogsAParametersEventWithItsConnection_HasTheMarkInEachString()
    {
        _scene.Describer.OnOpen = (log, request) =>
        {
            var value = request.Connection;
            var sql = "SELECT '" + value + "'";
            var declarations = "@p nvarchar(10) /* " + value + " */";
            var rows = new SqlServerParametersAnswer(
                [new SqlServerParameterRow("@" + value, value, 4, 10, 0, value, value, value)],
                null
            );
            SqlServerEvents.Parameters(log, "GetUser", new(sql, declarations), rows, 1);
            var error = new SqlServerServerError([
                new SqlServerErrorRow(102, 15, 1, "Incorrect syntax near '" + value + "'."),
            ]);
            SqlServerEvents.Parameters(log, "GetUser", new(sql, declarations), new(null, error), 1);
        };

        var text = await LoggedWithAsync(Secrets.ConnectionWithMarker);

        text.ShouldNotContain(Secrets.Marker);
        var events = LogReader.Events(Log, "postgres.parameters");
        events.Count.ShouldBe(2);
        events[0].GetProperty("sql").GetString().ShouldBe("SELECT '***'");
        events[0].GetProperty("params").GetString().ShouldBe("@p nvarchar(10) /* *** */");
        var row = events[0].GetProperty("rows")[0];
        row.GetProperty("name").GetString().ShouldBe("@***");
        row.GetProperty("suggestedSystemTypeName").GetString().ShouldBe("***");
        row.GetProperty("suggestedUserTypeSchema").GetString().ShouldBe("***");
        row.GetProperty("suggestedUserTypeName").GetString().ShouldBe("***");
        row.GetProperty("suggestedAssemblyQualifiedTypeName").GetString().ShouldBe("***");
        events[1].GetProperty("sql").GetString().ShouldBe("SELECT '***'");
        events[1].GetProperty("error")[0].GetProperty("message").GetString().ShouldBe("Incorrect syntax near '***'.");
    }

    [Fact]
    public async Task Run_DescriberThatLogsAProbeEventWithItsConnection_HasTheMarkInEachString()
    {
        _scene.Describer.OnOpen = (log, request) =>
        {
            var value = request.Connection;
            var sql = "SELECT '" + value + "'";
            var declarations = "@p_1 nvarchar(10) /* " + value + " */";
            SqlServerEvents.Probe(log, "GetUser", new(sql, declarations), new(true, null), 1);
            var error = new SqlServerServerError([new SqlServerErrorRow(53, 20, 0, "Cannot reach '" + value + "'.")]);
            SqlServerEvents.Probe(log, "GetUser", new(sql, declarations), new(null, error), 1);
        };

        var text = await LoggedWithAsync(Secrets.ConnectionWithMarker);

        text.ShouldNotContain(Secrets.Marker);
        var events = LogReader.Events(Log, "postgres.probe");
        events.Count.ShouldBe(2);
        events[0].GetProperty("sql").GetString().ShouldBe("SELECT '***'");
        events[0].GetProperty("params").GetString().ShouldBe("@p_1 nvarchar(10) /* *** */");
        events[1].GetProperty("error")[0].GetProperty("message").GetString().ShouldBe("Cannot reach '***'.");
    }

    [Fact]
    public async Task Run_DescriberThatLogsADeclaredEventWithItsConnection_HasTheMarkInEachString()
    {
        _scene.Describer.OnOpen = (log, request) =>
        {
            var value = request.Connection;
            var sql = "SELECT @p AS [1] /* " + value + " */";
            var declaration = "@p " + value;
            var rows = new SqlServerDeclaredAnswer([new(1, value, 4, 10, 0, value, value, value)], null, null);
            SqlServerEvents.Declared(log, "GetUser", new(sql, declaration), rows, 1);
            var errors = new SqlServerDeclaredAnswer(
                null,
                [new(2715, 1, "Cannot find data type " + value + ".")],
                null
            );
            SqlServerEvents.Declared(log, "GetUser", new(sql, declaration), errors, 1);
            var error = new SqlServerServerError([new SqlServerErrorRow(53, 20, 0, "Cannot reach '" + value + "'.")]);
            SqlServerEvents.Declared(log, "GetUser", new(sql, declaration), new(null, null, error), 1);
        };

        var text = await LoggedWithAsync(Secrets.ConnectionWithMarker);

        text.ShouldNotContain(Secrets.Marker);
        var events = LogReader.Events(Log, "postgres.declared");
        events.Count.ShouldBe(3);
        events[0].GetProperty("sql").GetString().ShouldBe("SELECT @p AS [1] /* *** */");
        events[0].GetProperty("params").GetString().ShouldBe("@p ***");
        var row = events[0].GetProperty("rows")[0];
        row.GetProperty("systemTypeName").GetString().ShouldBe("***");
        row.GetProperty("userTypeSchema").GetString().ShouldBe("***");
        row.GetProperty("userTypeName").GetString().ShouldBe("***");
        row.GetProperty("assemblyQualifiedTypeName").GetString().ShouldBe("***");
        events[1].GetProperty("errors")[0].GetProperty("message").GetString().ShouldBe("Cannot find data type ***.");
        events[2].GetProperty("error")[0].GetProperty("message").GetString().ShouldBe("Cannot reach '***'.");
    }

    // A row of "columns" or "table" whose every string is the value.
    private static SqlServerColumnRow ColumnRowOf(string value) =>
        new(
            false,
            1,
            value,
            true,
            value,
            4,
            10,
            0,
            value,
            value,
            value,
            value,
            value,
            value,
            value,
            value,
            false,
            false,
            value
        );

    // The answers of "columns" and "table" in each of their three shapes: rows, the describe's error rows, and the
    // error of the call.
    private static SqlServerColumnsAnswer[] ColumnsAnswersOf(string value) =>
        [
            new([ColumnRowOf(value)], null, null),
            new(null, [new SqlServerDescribeError(208, 7, "Invalid object name '" + value + "'.")], null),
            new(
                null,
                null,
                new SqlServerServerError([new SqlServerErrorRow(53, 20, 0, "Cannot reach '" + value + "'.")])
            ),
        ];

    private static void ShouldHaveTheMarkInEachRowString(JsonElement row)
    {
        foreach (
            var name in new[]
            {
                "name",
                "systemTypeName",
                "userTypeSchema",
                "userTypeName",
                "assemblyQualifiedTypeName",
                "sourceServer",
                "sourceDatabase",
                "sourceSchema",
                "sourceTable",
                "sourceColumn",
                "sourceType",
            }
        )
        {
            row.GetProperty(name).GetString().ShouldBe("***", name);
        }
    }

    [Fact]
    public async Task Run_DescriberThatLogsAColumnsEventWithItsConnection_HasTheMarkInEachString()
    {
        _scene.Describer.OnOpen = (log, request) =>
        {
            var value = request.Connection;
            var sql = "SELECT '" + value + "'";
            var declarations = "@p nvarchar(10) /* " + value + " */";
            foreach (var answer in ColumnsAnswersOf(value))
            {
                SqlServerEvents.Columns(log, "GetUser", new(sql, declarations), answer, 1);
            }
        };

        var text = await LoggedWithAsync(Secrets.ConnectionWithMarker);

        text.ShouldNotContain(Secrets.Marker);
        var events = LogReader.Events(Log, "postgres.columns");
        events.Count.ShouldBe(3);
        events[0].GetProperty("sql").GetString().ShouldBe("SELECT '***'");
        events[0].GetProperty("params").GetString().ShouldBe("@p nvarchar(10) /* *** */");
        ShouldHaveTheMarkInEachRowString(events[0].GetProperty("rows")[0]);
        events[1].GetProperty("errors")[0].GetProperty("message").GetString().ShouldBe("Invalid object name '***'.");
        events[2].GetProperty("error")[0].GetProperty("message").GetString().ShouldBe("Cannot reach '***'.");
    }

    [Fact]
    public async Task Run_DescriberThatLogsATableEventWithItsConnection_HasTheMarkInEachString()
    {
        _scene.Describer.OnOpen = (log, request) =>
        {
            var value = request.Connection;
            foreach (var answer in ColumnsAnswersOf(value))
            {
                SqlServerEvents.Table(log, "GetUser", new(value, value), answer, 1);
            }
        };

        var text = await LoggedWithAsync(Secrets.ConnectionWithMarker);

        text.ShouldNotContain(Secrets.Marker);
        var events = LogReader.Events(Log, "postgres.table");
        events.Count.ShouldBe(3);
        events[0].GetProperty("schema").GetString().ShouldBe("***");
        events[0].GetProperty("table").GetString().ShouldBe("***");
        ShouldHaveTheMarkInEachRowString(events[0].GetProperty("rows")[0]);
        events[1].GetProperty("errors")[0].GetProperty("message").GetString().ShouldBe("Invalid object name '***'.");
        events[2].GetProperty("error")[0].GetProperty("message").GetString().ShouldBe("Cannot reach '***'.");
    }

    [Fact]
    public async Task Run_DescriberThatLogsAColumnEventWithItsConnection_HasTheMarkInEachString()
    {
        _scene.Describer.OnOpen = (log, request) =>
        {
            var value = request.Connection;
            var origin = new SidecarOrigin(value, value, value);
            var withOrigin = new SidecarColumn(0, value, new OtherEngineType(value), true, value, origin, false, false);
            SqlServerEvents.Column(log, "GetUser", withOrigin, null);
            SqlServerEvents.Column(log, "GetUser", withOrigin with { Origin = null }, "no origin in " + value);
        };

        var text = await LoggedWithAsync(Secrets.ConnectionWithMarker);

        text.ShouldNotContain(Secrets.Marker);
        var events = LogReader.Events(Log, "postgres.column");
        events.Count.ShouldBe(2);
        events[0].GetProperty("name").GetString().ShouldBe("***");
        events[0].GetProperty("type").GetString().ShouldBe("***");
        events[0].GetProperty("nullableSource").GetString().ShouldBe("***");
        var logged = events[0].GetProperty("origin");
        logged.GetProperty("schema").GetString().ShouldBe("***");
        logged.GetProperty("table").GetString().ShouldBe("***");
        logged.GetProperty("column").GetString().ShouldBe("***");
        events[1].GetProperty("noOrigin").GetString().ShouldBe("no origin in ***");
    }

    [Fact]
    public async Task Run_DescriberThatLogsATableMatchEventWithItsConnection_HasTheMarkInEachString()
    {
        _scene.Describer.OnOpen = (log, request) =>
        {
            var value = request.Connection;
            SqlServerEvents.TableMatch(log, "GetUser", new SidecarTable(value, value), value);
        };

        var text = await LoggedWithAsync(Secrets.ConnectionWithMarker);

        text.ShouldNotContain(Secrets.Marker);
        var logged = LogReader.Events(Log, "postgres.table-match").ShouldHaveSingleItem();
        logged.GetProperty("tableMatch").GetString().ShouldBe("***");
        logged.GetProperty("table").GetProperty("schema").GetString().ShouldBe("***");
        logged.GetProperty("table").GetProperty("table").GetString().ShouldBe("***");
    }

    [Fact]
    public async Task Run_PostgresDescriberThatLogsATableMatchEventWithItsConnection_HasTheMarkInResultAndTableMatch()
    {
        _scene.Describer.OnOpen = (log, request) =>
        {
            var value = request.Connection;
            PostgresEvents.TableMatch(log, "GetUser", new SidecarTable(value, value), value);
        };

        var text = await LoggedWithAsync(Secrets.ConnectionWithMarker);

        text.ShouldNotContain(Secrets.Marker);
        var logged = LogReader.Events(Log, "postgres.table-match").ShouldHaveSingleItem();
        logged.GetProperty("result").GetString().ShouldBe("***");
        logged.GetProperty("tableMatch").GetString().ShouldBe("***");
        logged.GetProperty("table").GetProperty("schema").GetString().ShouldBe("***");
        logged.GetProperty("table").GetProperty("table").GetString().ShouldBe("***");
    }

    // SqlClient's events reach the log through the describer's log too, so a command's text that held the value would
    // be written with the mark: the listener forwards one as SqlClient writes it.
    [Fact]
    public async Task Run_DescriberThatLogsADriverEventWithItsConnection_HasTheMarkInItsCommandText()
    {
        _scene.Describer.OnOpen = (log, request) =>
        {
            using var listener = SqlClientRunListener.For(log).ShouldNotBeNull();
            listener.Forward(
                "BeginExecute",
                EventLevel.Informational,
                1,
                ["objectId", "dataSource", "database", "commandText", "message"],
                [9, request.Connection, request.Connection, "SELECT '" + request.Connection + "'", request.Connection]
            );
        };

        var text = await LoggedWithAsync(Secrets.ConnectionWithMarker);

        text.ShouldNotContain(Secrets.Marker);
        LogReader
            .Events(Log, "postgres.driver")
            .ShouldHaveSingleItem()
            .GetProperty("commandText")
            .GetString()
            .ShouldBe("SELECT '***'");
    }

    [Fact]
    public async Task Run_VerboseWithAServerVersionThatHoldsAPassword_SaysTheMark()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = "Host=db;Password=" + Secrets.Marker;
        _scene.Describer.Secrets = [Secrets.Marker];
        _scene.Describer.Server = _scene.Describer.Server with { Version = "16.4 " + Secrets.Marker };

        var result = await _scene.DescribeAsync("--verbose");

        result.Out.ShouldNotContain(Secrets.Marker);
        result.Out.ShouldContain("sqlsource: database 'postgres': server 16.4 ***\n");
    }

    // What the run itself writes of a database is a describer's text too.
    [Fact]
    public async Task Run_DescriberThatGivesItsConnectionAsItsServer_HasTheMarkInDatabaseOpen()
    {
        _scene.Describer.Server = new ServerInfo(
            Secrets.ConnectionWithMarker,
            Secrets.ConnectionWithMarker,
            Secrets.ConnectionWithMarker,
            new EquatableArray<ServerSetting>([
                new ServerSetting(Secrets.ConnectionWithMarker, Secrets.ConnectionWithMarker),
            ])
        );

        var text = await LoggedWithAsync(Secrets.ConnectionWithMarker);

        text.ShouldNotContain(Secrets.Marker);
        var open = LogReader.Events(Log, "database.open").ShouldHaveSingleItem();
        open.GetProperty("serverVersion").GetString().ShouldBe("***");
        open.GetProperty("driver").GetString().ShouldBe("***");
        open.GetProperty("driverVersion").GetString().ShouldBe("***");
        open.GetProperty("settings").GetProperty("***").GetString().ShouldBe("***");
    }

    // A line of "--verbose" that holds what a describer gave is written as an error is.
    [Fact]
    public async Task Run_VerboseWithADescriberThatGivesItsConnectionAsItsServersVersion_SaysTheMark()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Secrets.ConnectionWithMarker;
        _scene.Describer.Server = _scene.Describer.Server with { Version = Secrets.ConnectionWithMarker };

        var result = await _scene.DescribeAsync("--verbose");

        result.Out.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldNotContain(Secrets.Marker);
        result.Out.ShouldContain("sqlsource: database 'postgres': server ***\n");
    }

    // Every text of a failure and of an exception is in a "diagnostic", and was cleaned before it was reported.
    [Fact]
    public async Task Run_DescriberThatReturnsAndThrowsItsConnection_LeavesNoValueInADiagnostic()
    {
        // After "Users.sql", so that GetUser fails before Throws ends the run.
        _ = _scene.Project.AddSql("Zebra.sql", "-- name: Throws\nSELECT 2;\n");
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure(
            Secrets.ConnectionWithMarker,
            DescribeStep.Explain,
            "try " + Secrets.ConnectionWithMarker,
            "server said " + Secrets.ConnectionWithMarker
        );
        _scene.Describer.Exceptions["Throws"] = new InvalidOperationException("bad " + Secrets.ConnectionWithMarker);
        _scene.Run.Environment["SQLSOURCE_DEBUG"] = "1";

        var text = await LoggedWithAsync(Secrets.ConnectionWithMarker);

        text.ShouldNotContain(Secrets.Marker);
        LogReader
            .Events(Log, "diagnostic")
            .Select(e => e.GetProperty("id").GetString())
            .ShouldBe(["SQLSRC999", "SQLSRC200"]);
    }

    // The id of a "diagnostic" is the descriptor's, which a describer may have built from what a server said.
    [Fact]
    public async Task Run_SessionThatDoesNotOpenWithADescriptorThatHoldsTheConnection_HasTheMarkAsTheDiagnosticsId()
    {
        _scene.Describer.OpenFailure = FakeDescriber.Failure("GetUser") with
        {
            Descriptor = FakeDescriber.RejectedWith(Secrets.ConnectionWithMarker),
        };

        var text = await LoggedWithAsync(Secrets.ConnectionWithMarker);

        text.ShouldNotContain(Secrets.Marker);
        var diagnostic = LogReader.Events(Log, "diagnostic").ShouldHaveSingleItem();
        diagnostic.GetProperty("id").GetString().ShouldBe("***");
        diagnostic.GetProperty("message").GetString().ShouldBe("*** rejected the query 'GetUser'");
    }

    // The id of a describer's descriptor is a describer's text.  The tests give a database a connection whose value
    // is the id of the descriptor of FakeDescriber, so that an event written through the plain trace would hold it.
    [Fact]
    public async Task Run_SessionThatDoesNotOpenWithAConnectionThatIsTheErrorsId_HasTheMarkAsTheError()
    {
        _scene.Describer.OpenFailure = FakeDescriber.Failure("GetUser");

        _ = await LoggedWithAsync(FakeDescriber.Rejected.Id);

        var open = LogReader.Events(Log, "database.open").ShouldHaveSingleItem();
        open.GetProperty("error").GetString().ShouldBe("***");
    }

    [Fact]
    public async Task Run_QueryThatFailsWithAConnectionThatIsTheErrorsId_HasTheMarkAsTheError()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        _ = await LoggedWithAsync(FakeDescriber.Rejected.Id);

        var query = LogReader.Events(Log, "query").ShouldHaveSingleItem();
        query.GetProperty("error").GetString().ShouldBe("***");
    }
}
