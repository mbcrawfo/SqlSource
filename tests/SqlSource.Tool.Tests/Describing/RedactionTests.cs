using System;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// No value of a connection reaches the log: not from the command line, and not from a describer.
public sealed class RedactionTests : IDisposable
{
    private readonly DescribeScene _scene = new();

    public RedactionTests()
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
        var line = LogReader.Read(Log).Single(e => e.GetProperty("event").GetString() == "postgres.connect ***");
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
            .Read(Log)
            .Single(e => e.GetProperty("event").GetString() == "postgres.connect")
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
            .Read(Log)
            .Single(e => e.GetProperty("event").GetString() == "postgres.connect")
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
        var events = LogReader
            .Read(Log)
            .Where(e => e.GetProperty("event").GetString() == "postgres.parameters")
            .ToList();
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
        var events = LogReader.Read(Log).Where(e => e.GetProperty("event").GetString() == "postgres.probe").ToList();
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
        var events = LogReader.Read(Log).Where(e => e.GetProperty("event").GetString() == "postgres.declared").ToList();
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
        var events = LogReader.Read(Log).Where(e => e.GetProperty("event").GetString() == "postgres.columns").ToList();
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
        var events = LogReader.Read(Log).Where(e => e.GetProperty("event").GetString() == "postgres.table").ToList();
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
        var events = LogReader.Read(Log).Where(e => e.GetProperty("event").GetString() == "postgres.column").ToList();
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
        var logged = LogReader.Read(Log).Single(e => e.GetProperty("event").GetString() == "postgres.table-match");
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
        var logged = LogReader.Read(Log).Single(e => e.GetProperty("event").GetString() == "postgres.table-match");
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
            .Read(Log)
            .Single(e => e.GetProperty("event").GetString() == "postgres.driver")
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

    [Fact]
    public void Of_TextThatHoldsTheSecretTwice_HasTheMarkInBothPlaces() =>
        Redaction.Of("a s3cret and a s3cret", new ConnectionSecret("s3cret")).ShouldBe("a *** and a ***");

    // The edges of the rule, which holds for a part of any length: the text's own ends count as no letter, and an
    // edge of the part that is no letter, digit or "_" is not checked.
    [Theory]
    [InlineData("pw", "pw", true)]
    [InlineData("pw x", "pw", true)]
    [InlineData("x pw", "pw", true)]
    [InlineData("x!pw", "!pw", true)]
    [InlineData("aaa", "aa", false)]
    public void HoldsToken_PartAtAnEdge_IsWhetherItStandsAsAToken(string text, string part, bool expected) =>
        Redaction.HoldsToken(text, part).ShouldBe(expected);

    // A part that holds another as a token is replaced first.
    [Fact]
    public void Of_PartThatHoldsAShorterOneAsAToken_IsReplacedWhole() =>
        Redaction.Of("pass-word", new ConnectionSecret("Host=db", ["pass", "pass-word"])).ShouldBe("***");

    [Fact]
    public void Of_TwoPartsOneInsideTheOther_AreEachTheMark() =>
        Redaction.Of("hunter2 hunter", new ConnectionSecret("Host=db", ["hunter", "hunter2"])).ShouldBe("*** ***");

    [Fact]
    public void Of_FailureWhoseServerLinesHoldAPart_HasTheMarkThereAndTheWordInsideANameLeft()
    {
        var secret = new ConnectionSecret("Host=db;Password=test", ["test"]);
        var failure = FakeDescriber.Failure("GetUser", "28P01: password test refused", "at latest_orders");

        Redaction.Of(failure, secret).ServerLines.ShouldBe(["28P01: password *** refused", "at latest_orders"]);
    }

    // A describer cuts what a server said into lines, so a part with a line break is in two of them.
    [Fact]
    public void Of_FailureWithAPartOfTwoLinesCutOverTwo_HasTheMarkOnce()
    {
        var secret = new ConnectionSecret("Host=db;Password=a\r\nb", ["a\r\nb"]);
        var failure = FakeDescriber.Failure("GetUser", "x a", "b y");

        Redaction.Of(failure, secret).ServerLines.ShouldBe(["x *** y"]);
    }

    // A describer may write the value again as its driver does, which is not the text of the value.
    [Fact]
    public void Of_RewrittenValueInAMessage_IsTheMark() =>
        Redaction
            .Of(
                "bad 'Host=db;Port=5432;Password=hunter22'",
                new ConnectionSecret(
                    "host=db;password=hunter22",
                    ["hunter22"],
                    forms: ["Host=db;Port=5432;Password=hunter22"]
                )
            )
            .ShouldBe("bad '***'");

    [Fact]
    public void Of_ValueTrimmedInAMessage_IsTheMark() =>
        Redaction
            .Of("cannot use Host=db;Database=app", new ConnectionSecret("Host=db;Database=app \n"))
            .ShouldBe("cannot use ***");

    // A value that was in two lines leaves one, as it does for a part.
    [Fact]
    public void Of_Failure_RewriteCutOverTwoServerLines_IsTheMark()
    {
        var secret = new ConnectionSecret(
            "host=db;password=hunter22",
            forms: ["Host=db;Port=5432\r\nPassword=hunter22"]
        );
        var failure = FakeDescriber.Failure("GetUser", "a Host=db;Port=5432", "Password=hunter22 b");

        Redaction.Of(failure, secret).ServerLines.ShouldBe(["a *** b"]);
    }

    [Fact]
    public void OfJoined_NothingFound_IsTheSameTexts()
    {
        var secret = new ConnectionSecret("Host=db;Password=hunter22", ["hunter22"]);

        Redaction.OfJoined(["a", "latest_hunt", "er22x", ""], secret).ShouldBe(["a", "latest_hunt", "er22x", ""]);
    }

    // A descriptor of the fake's id with the format a describer built.  The id is no constant, so release tracking
    // has no row for it.
    private static DiagnosticDescriptor WithFormat(string format) =>
        new(
            FakeDescriber.Rejected.Id,
            "Query was rejected",
            format,
            "SqlSource",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: "https://example.test/sqlsrc999"
        );

    private static string Printed(ToolDiagnostic diagnostic)
    {
        using var error = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        new Reporter(error).Report(diagnostic);
        return error.ToString();
    }

    // Neither the format nor the argument holds the value: together they do.
    [Fact]
    public void Of_ValueThatTheFormatAndAnArgumentMakeTogether_IsRemovedFromTheMessage()
    {
        var diagnostic = ToolDiagnostic.Create(WithFormat("half: Pass{0}"), "word=x");

        var printed = Printed(Redaction.Of(diagnostic, new ConnectionSecret("Password=x")));

        printed.ShouldContain("half: ***\n");
        printed.ShouldNotContain("Password=x");
    }

    [Fact]
    public void Of_ValueThatTwoArgumentsMakeTogether_IsRemovedFromTheMessage()
    {
        var diagnostic = ToolDiagnostic.Create(WithFormat("rejected {0}{1} today"), "Pass", "word=x");

        var printed = Printed(Redaction.Of(diagnostic, new ConnectionSecret("Password=x")));

        printed.ShouldContain("rejected *** today\n");
        printed.ShouldNotContain("Password=x");
    }

    // Reporter writes a line break as a space, which can make a password that holds a space.
    [Fact]
    public void Of_PasswordWithASpaceSplitByALineBreak_IsTheMarkInTheArgumentAndTheLine()
    {
        var diagnostic = ToolDiagnostic
            .Create(WithFormat("rejected {0}"), "my\npass")
            .WithLines(new ContinuationLine("server", "said my\r\npass"));

        var printed = Printed(Redaction.Of(diagnostic, new ConnectionSecret("Host=db;Password=my pass", ["my pass"])));

        printed.ShouldContain("rejected ***\n");
        printed.ShouldContain("    server: said ***\n");
        printed.ShouldNotContain("my pass");
    }

    // A message that holds the value is printed as the one argument of its error, on one line, so a line break of
    // the format can make a password that holds a space there too.
    [Fact]
    public void Of_MessageThatHoldsTheValueAndAPasswordSplitByALineBreakOfTheFormat_IsTheMarkInBoth()
    {
        var diagnostic = ToolDiagnostic.Create(
            WithFormat("rejected {0}{1}, said my\npass"),
            "Host=db;Password=my",
            " pass"
        );

        var printed = Printed(Redaction.Of(diagnostic, new ConnectionSecret("Host=db;Password=my pass", ["my pass"])));

        printed.ShouldContain("rejected ***, said ***\n");
        printed.ShouldNotContain("my pass");
    }

    // A format that a describer built holds a password with a space, cut by a line break, and nothing of the whole
    // value: the message is searched on one line too, and printed as its one argument.
    [Fact]
    public void Of_FormatThatHoldsAPasswordSplitByALineBreak_IsTheMarkInTheMessage()
    {
        var diagnostic = ToolDiagnostic.Create(WithFormat("rejected {0}, said my\npass"), "x");

        var printed = Printed(Redaction.Of(diagnostic, new ConnectionSecret("Host=db;Password=my pass", ["my pass"])));

        printed.ShouldStartWith("sqlsource : error SQLSRC999: rejected x, said ***\n");
        printed.ShouldNotContain("pass");
    }

    // The descriptor of a failure is searched the same way, before an error is made of it.
    [Fact]
    public void Of_FailureWhoseFormatHoldsAPasswordSplitByALineBreak_HasTheMarkInTheFormat()
    {
        var failure = FakeDescriber.Failure("GetUser") with { Descriptor = WithFormat("rejected {0}, said my\npass") };

        Redaction
            .Of(failure, new ConnectionSecret("Host=db;Password=my pass", ["my pass"]))
            .Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture)
            .ShouldBe("rejected {0}, said ***");
    }

    // The label of a line is printed on one line too.
    [Fact]
    public void Of_LineWhoseLabelHoldsAPasswordSplitByALineBreak_IsTheMarkInTheLabel()
    {
        var diagnostic = ToolDiagnostic
            .Create(WithFormat("rejected {0}"), "x")
            .WithLines(new ContinuationLine("said my\npass", "x"));

        var printed = Printed(Redaction.Of(diagnostic, new ConnectionSecret("Host=db;Password=my pass", ["my pass"])));

        printed.ShouldContain("    said ***: x\n");
        printed.ShouldNotContain("my pass");
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
        var open = LogReader.Read(Log).Single(e => e.GetProperty("event").GetString() == "database.open");
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
            .Read(Log)
            .Where(e => e.GetProperty("event").GetString() == "diagnostic")
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
        var diagnostic = LogReader.Read(Log).Single(e => e.GetProperty("event").GetString() == "diagnostic");
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

        var open = LogReader.Read(Log).Single(e => e.GetProperty("event").GetString() == "database.open");
        open.GetProperty("error").GetString().ShouldBe("***");
    }

    [Fact]
    public async Task Run_QueryThatFailsWithAConnectionThatIsTheErrorsId_HasTheMarkAsTheError()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        _ = await LoggedWithAsync(FakeDescriber.Rejected.Id);

        var query = LogReader.Read(Log).Single(e => e.GetProperty("event").GetString() == "query");
        query.GetProperty("error").GetString().ShouldBe("***");
    }
}
