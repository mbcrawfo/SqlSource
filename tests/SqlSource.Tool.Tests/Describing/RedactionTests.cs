using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Describers;
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

    // Whichever way the option was written, the token keeps the option and the name and loses the value.
    [Theory]
    [InlineData("--connection|postgres=***", "--connection", "postgres=" + Secrets.ConnectionWithMarker)]
    [InlineData("--connection=postgres=***", "--connection=postgres=" + Secrets.ConnectionWithMarker)]
    [InlineData("--connection:postgres=***", "--connection:postgres=" + Secrets.ConnectionWithMarker)]
    public async Task Run_ConnectionOnTheCommandLine_IsLoggedWithItsNameAndNoValue(string logged, params string[] args)
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
