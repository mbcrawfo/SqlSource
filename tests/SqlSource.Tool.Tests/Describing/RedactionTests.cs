using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
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
