using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests.Logging;

// The log of a run: "--log" or SQLSOURCE_LOG, one JSON object on a line for each thing the run did.
public sealed class RunLogTests : IDisposable
{
    private const string Users = "-- name: GetUser\nSELECT 1;\n\n-- name: ListUsers\nSELECT 2;\n";

    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public RunLogTests()
    {
        _users = _scene.Project.AddSql("Users.sql", Users);
        // Each describe takes 25 ms on the clock of the test.
        _scene.Describer.BeforeDescribe = _ =>
        {
            _scene.Clock.Advance(TimeSpan.FromMilliseconds(25));
            return Task.CompletedTask;
        };
    }

    private string Log => _scene.Run.Folder.PathOf("run.log");

    public void Dispose() => _scene.Dispose();

    private static string?[] Strings(JsonElement list) => [.. list.EnumerateArray().Select(item => item.GetString())];

    [Fact]
    public async Task Run_WithLog_StartsWithWhatRanAndEndsWithHowItEnded()
    {
        var result = await _scene.DescribeAsync("--log", "run.log");

        // The console is as without the option.
        result.ShouldBe(new CliResult(0, "postgres (postgres): 2 described, 0 skipped, 0 failed\n", ""));
        var events = LogReader.Read(Log);
        var start = events[0];
        start.GetProperty("event").GetString().ShouldBe("run.start");
        start.GetProperty("time").GetString().ShouldBe("2026-10-09T14:02:11.412Z");
        start.GetProperty("toolVersion").GetString().ShouldBe(PackageVersion.Prefix);
        start.GetProperty("formatVersion").GetInt32().ShouldBe(SidecarFormat.Version);
        start.GetProperty("runtime").GetString().ShouldBe(RuntimeInformation.FrameworkDescription);
        start.GetProperty("os").GetString().ShouldBe(RuntimeInformation.OSDescription);
        Strings(start.GetProperty("arguments")).ShouldBe(["describe", "--log", "run.log"]);
        var end = events[^1];
        end.GetProperty("event").GetString().ShouldBe("run.end");
        end.GetProperty("time").GetString().ShouldBe("2026-10-09T14:02:11.462Z");
        end.GetProperty("exitCode").GetInt32().ShouldBe(0);
        end.GetProperty("durationMs").GetInt64().ShouldBe(50);
    }

    [Fact]
    public async Task Run_ErrorWithLog_IsADiagnosticEventWithItsPlaceItsMessageAndItsLines()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser", "relation \"users\" does not exist");

        var result = await _scene.DescribeAsync("--log", "run.log");

        result.ExitCode.ShouldBe(1);
        var events = LogReader.Read(Log);
        var diagnostics = events.Where(line => line.GetProperty("event").GetString() == "diagnostic").ToArray();
        diagnostics.Select(line => line.GetProperty("id").GetString()).ShouldBe(["SQLSRC999", "SQLSRC217"]);
        var rejected = diagnostics[0];
        rejected.GetProperty("path").GetString().ShouldBe(_users);
        rejected.GetProperty("line").GetInt32().ShouldBe(1);
        rejected.GetProperty("column").GetInt32().ShouldBe(10);
        rejected.GetProperty("message").GetString().ShouldBe("The server rejected the query 'GetUser'");
        var lines = rejected.GetProperty("lines").EnumerateArray().ToArray();
        lines.Select(line => line.GetProperty("label").GetString()).ShouldBe(["query", "step", "server", "help"]);
        lines[2].GetProperty("text").GetString().ShouldBe("relation \"users\" does not exist");
        // An error at a file and at no place in it.
        diagnostics[1].GetProperty("line").ValueKind.ShouldBe(JsonValueKind.Null);
        diagnostics[1].GetProperty("column").ValueKind.ShouldBe(JsonValueKind.Null);
        events[^1].GetProperty("exitCode").GetInt32().ShouldBe(1);
    }

    private static string? Text(JsonElement line, string name) => line.GetProperty(name).GetString();

    // A run that skips GetUser, describes ListUsers and fails Bad.
    private async Task<JsonElement[]> RunThatDescribesSkipsAndFailsAsync()
    {
        (await _scene.DescribeAsync()).ExitCode.ShouldBe(0);
        await File.WriteAllTextAsync(
            _users,
            Users.Replace("SELECT 2;", "SELECT 22;", StringComparison.Ordinal) + "\n-- name: Bad\nSELECT 4;\n",
            TestContext.Current.CancellationToken
        );
        _scene.Describer.Failures["Bad"] = FakeDescriber.Failure("Bad");

        (await _scene.DescribeAsync("--log", "run.log")).ExitCode.ShouldBe(1);
        return LogReader.Read(Log);
    }

    [Fact]
    public async Task Run_ThatDescribesSkipsAndFails_WritesItsEventsInTheOrderThingsHappened()
    {
        var events = await RunThatDescribesSkipsAndFailsAsync();

        events
            .Select(line => Text(line, "event"))
            .ShouldBe([
                "run.start",
                "project",
                "file",
                "query",
                "database",
                "database.open",
                "query",
                "diagnostic",
                "query",
                "diagnostic",
                "sidecar",
                "run.end",
            ]);
        // The second describe took 25 ms more on the clock than the first: every event has the time it was written.
        Text(events[6], "time").ShouldBe("2026-10-09T14:02:11.487Z");
    }

    [Fact]
    public async Task Run_ThatDescribesSkipsAndFails_SaysOfTheProjectAndTheFileWhatItRead()
    {
        var events = await RunThatDescribesSkipsAndFailsAsync();

        var project = events[1];
        Text(project, "path").ShouldBe(_scene.Project.ProjectPath);
        project.GetProperty("inRun").GetBoolean().ShouldBeTrue();
        project.GetProperty("usesSqlSource").GetBoolean().ShouldBeTrue();
        Text(project, "targetFramework").ShouldBe("net10.0");
        project.GetProperty("sqlFiles").GetInt32().ShouldBe(1);
        project.GetProperty("compileFiles").GetInt32().ShouldBe(1);
        var file = events[2];
        Text(file, "path").ShouldBe(_users);
        Text(file, "project").ShouldBe(_scene.Project.ProjectPath);
        Text(file, "dialect").ShouldBe("postgres");
        Text(file, "state").ShouldBe("ready");
        file.GetProperty("queries").GetInt32().ShouldBe(3);
        var claim = file.GetProperty("claims").EnumerateArray().ShouldHaveSingleItem();
        Text(claim, "project").ShouldBe(_scene.Project.ProjectPath);
        Text(claim, "source").ShouldBe(_scene.Project.PathOf("Queries.cs"));
        claim.GetProperty("line").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Run_ThatDescribesSkipsAndFails_SaysOfEachQueryWhatItDecided()
    {
        var events = await RunThatDescribesSkipsAndFailsAsync();

        var skipped = events[3];
        Text(skipped, "file").ShouldBe(_users);
        Text(skipped, "name").ShouldBe("GetUser");
        Text(skipped, "database").ShouldBe("postgres");
        Text(skipped, "hash").ShouldNotBeNull().Length.ShouldBe(64);
        skipped.GetProperty("selected").GetBoolean().ShouldBeTrue();
        Text(skipped, "decision").ShouldBe("skipped");
        skipped.TryGetProperty("durationMs", out _).ShouldBeFalse();
        skipped.TryGetProperty("error", out _).ShouldBeFalse();
        var described = events[6];
        Text(described, "name").ShouldBe("ListUsers");
        Text(described, "decision").ShouldBe("described");
        described.GetProperty("durationMs").GetInt64().ShouldBe(25);
        var failed = events[8];
        Text(failed, "name").ShouldBe("Bad");
        Text(failed, "decision").ShouldBe("failed");
        Text(failed, "error").ShouldBe("SQLSRC999");
        failed.TryGetProperty("durationMs", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_ThatDescribesSkipsAndFails_SaysOfTheDatabaseTheSidecarAndTheEnd()
    {
        var events = await RunThatDescribesSkipsAndFailsAsync();

        var database = events[4];
        Text(database, "name").ShouldBe("postgres");
        Text(database, "engine").ShouldBe("postgres");
        // Where the connection came from, and never what it is.
        Text(database, "connectionSource").ShouldBe("SQLSOURCE_CONNECTION_POSTGRES");
        database.GetProperty("describer").GetBoolean().ShouldBeTrue();
        var open = events[5];
        Text(open, "name").ShouldBe("postgres");
        Text(open, "serverVersion").ShouldBe("16.4");
        Text(open, "driver").ShouldBe("FakeDriver");
        Text(open, "driverVersion").ShouldBe("1.2.3");
        open.GetProperty("settings").ValueKind.ShouldBe(JsonValueKind.Object);
        var sidecar = events[10];
        Text(sidecar, "path").ShouldBe(_users + ".json");
        Text(sidecar, "action").ShouldBe("held-back");
        var counts = events[11].GetProperty("databases").EnumerateArray().ShouldHaveSingleItem();
        Text(counts, "name").ShouldBe("postgres");
        Text(counts, "engine").ShouldBe("postgres");
        counts.GetProperty("described").GetInt32().ShouldBe(1);
        counts.GetProperty("skipped").GetInt32().ShouldBe(1);
        counts.GetProperty("failed").GetInt32().ShouldBe(1);
    }

    private string? ActionOf(string sidecarPath) =>
        LogReader
            .Read(Log)
            .Where(line => Text(line, "event") == "sidecar" && Text(line, "path") == sidecarPath)
            .Select(line => Text(line, "action"))
            .ShouldHaveSingleItem();

    [Fact]
    public async Task Run_Sidecars_AreWrittenThenUnchangedThenDeleted()
    {
        _ = await _scene.DescribeAsync("--log", "run.log");
        ActionOf(_users + ".json").ShouldBe("written");

        _ = await _scene.DescribeAsync("--log", "run.log");
        ActionOf(_users + ".json").ShouldBe("unchanged");

        await File.WriteAllTextAsync(
            _users,
            "-- name: GetUser\n-- output: sql\nSELECT 1;\n",
            TestContext.Current.CancellationToken
        );
        _ = await _scene.DescribeAsync("--log", "run.log");
        ActionOf(_users + ".json").ShouldBe("deleted");
        File.Exists(_users + ".json").ShouldBeFalse();
    }

    // "written" would say what did not happen.
    [Fact]
    public async Task Run_SidecarThatCannotBeWritten_HasTheActionFailed()
    {
        _ = Directory.CreateDirectory(_users + ".json");

        var result = await _scene.DescribeAsync("--log", "run.log");

        result.Error.ShouldContain("error SQLSRC218: ");
        ActionOf(_users + ".json").ShouldBe("failed");
    }

    [Fact]
    public async Task Run_Check_SaysOfEachSidecarWhetherItIsTheSameAndCountsInItsOwnWords()
    {
        (await _scene.DescribeAsync()).ExitCode.ShouldBe(0);
        var other = _scene.Project.AddSql("Zebras.sql", "-- name: GetZebra\nSELECT 5;\n");

        var result = await _scene.DescribeAsync("--check", "--log", "run.log");

        result.ExitCode.ShouldBe(2);
        ActionOf(_users + ".json").ShouldBe("same");
        ActionOf(other + ".json").ShouldBe("differs");
        File.Exists(other + ".json").ShouldBeFalse();
        var counts = LogReader.Read(Log)[^1].GetProperty("databases").EnumerateArray().ShouldHaveSingleItem();
        counts.GetProperty("same").GetInt32().ShouldBe(2);
        counts.GetProperty("differ").GetInt32().ShouldBe(1);
        counts.GetProperty("failed").GetInt32().ShouldBe(0);
    }

    // "same" would say that every query of the file was compared, and one could not be: the file has no event.
    [Fact]
    public async Task Run_CheckOfAFileOneOfWhoseQueriesFailsToDescribe_HasNoSidecarEventAndSaysNothingOfTheSidecar()
    {
        (await _scene.DescribeAsync()).ExitCode.ShouldBe(0);
        _scene.Describer.Failures["ListUsers"] = FakeDescriber.Failure("ListUsers");

        var result = await _scene.DescribeAsync("--check", "--verbose", "--log", "run.log");

        result.ExitCode.ShouldBe(1);
        LogReader.Read(Log).ShouldNotContain(line => Text(line, "event") == "sidecar");
        result.Out.ShouldNotContain("sidecar '");
        LogReader
            .Read(Log)
            .Where(line => Text(line, "event") == "query")
            .Select(line => $"{Text(line, "name")} {Text(line, "decision")}")
            .ShouldBe(["GetUser described", "ListUsers failed"]);
    }

    // A difference is a difference whatever else failed in the file.
    [Fact]
    public async Task Run_CheckOfAFileWithADifferenceAndAQueryThatFails_HasTheSidecarEventDiffers()
    {
        (await _scene.DescribeAsync()).ExitCode.ShouldBe(0);
        await File.WriteAllTextAsync(
            _users,
            Users.Replace("SELECT 1;", "SELECT 11;", StringComparison.Ordinal),
            TestContext.Current.CancellationToken
        );
        _scene.Describer.Failures["ListUsers"] = FakeDescriber.Failure("ListUsers");

        _ = await _scene.DescribeAsync("--check", "--log", "run.log");

        ActionOf(_users + ".json").ShouldBe("differs");
    }

    [Fact]
    public async Task Run_DescriberThatLogs_HasItsEventsUnderItsEngineBetweenTheRunsOwn()
    {
        _scene.Describer.OnOpen = (log, request) => log.Write("connect", new LogMember("database", request.Database));
        _scene.Describer.OnDescribe = (log, request) => log.Write("columns", new LogMember("query", request.Name));

        _ = await _scene.DescribeAsync("--log", "run.log");

        var events = LogReader.Read(Log);
        events
            .Select(line => Text(line, "event"))
            .Skip(3)
            .ShouldBe([
                "database",
                "postgres.connect",
                "database.open",
                "postgres.columns",
                "query",
                "postgres.columns",
                "query",
                "sidecar",
                "run.end",
            ]);
        Text(events[6], "query").ShouldBe("GetUser");
        Text(events[7], "name").ShouldBe("GetUser");
    }

    // A describer cannot write an event of the run's, nor make a reader take its member for the run's name or time.
    [Fact]
    public async Task Run_DescriberThatLogsMembersNamedEventAndTime_HasAnEventOfItsEnginesAndTheRunsOwnTime()
    {
        _scene.Describer.OnOpen = (log, _) => log.Write("x", new("event", "run.end"), new("time", "never"));

        _ = await _scene.DescribeAsync("--log", "run.log");

        var line = LogReader.Read(Log).Single(e => Text(e, "event") == "postgres.x");
        Text(line, "_event").ShouldBe("run.end");
        Text(line, "_time").ShouldBe("never");
        Text(line, "time").ShouldBe("2026-10-09T14:02:11.412Z");
    }

    [Fact]
    public async Task Run_DatabaseWithoutADescriber_SaysSoAndFailsItsQueriesWithSqlsrc216()
    {
        _scene.Run.Describers.Clear();

        _ = await _scene.DescribeAsync("--log", "run.log");

        var events = LogReader.Read(Log);
        var database = events.Single(line => Text(line, "event") == "database");
        database.GetProperty("describer").GetBoolean().ShouldBeFalse();
        events.ShouldNotContain(line => Text(line, "event") == "database.open");
        events
            .Where(line => Text(line, "event") == "query")
            .Select(line => $"{Text(line, "decision")} {Text(line, "error")}")
            .ShouldBe(["failed SQLSRC216", "failed SQLSRC216"]);
    }

    [Fact]
    public async Task Run_DatabaseWithoutAConnection_HasTheSourceNoneAndFailsItsQueriesWithSqlsrc213()
    {
        _ = _scene.Run.Environment.Remove("SQLSOURCE_CONNECTION_POSTGRES");

        _ = await _scene.DescribeAsync("--log", "run.log");

        var events = LogReader.Read(Log);
        Text(events.Single(line => Text(line, "event") == "database"), "connectionSource").ShouldBe("none");
        events
            .Where(line => Text(line, "event") == "query")
            .Select(line => Text(line, "error"))
            .ShouldBe(["SQLSRC213", "SQLSRC213"]);
    }

    [Fact]
    public async Task Run_SessionThatDoesNotOpen_HasTheErrorsIdInDatabaseOpen()
    {
        _scene.Describer.OpenFailure = FakeDescriber.Failure("GetUser", "password authentication failed");

        _ = await _scene.DescribeAsync("--log", "run.log");

        var open = LogReader.Read(Log).Single(line => Text(line, "event") == "database.open");
        Text(open, "name").ShouldBe("postgres");
        Text(open, "error").ShouldBe("SQLSRC999");
        open.TryGetProperty("serverVersion", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_ProjectThatProjectOptionLeavesOut_IsInTheLogAndWasNotAsked()
    {
        var lib = Plans.Postgres(_scene.Run.Folder, "Lib", "Lib").AnsweredBy(_scene.Run.Processes);
        _ = _scene.Run.Folder.WriteFile(
            "App.slnx",
            "<Solution><Project Path=\"App.csproj\" /><Project Path=\"Lib/Lib.csproj\" /></Solution>"
        );

        _ = await _scene.DescribeAsync("App.slnx", "--project", "App.csproj", "--log", "run.log");

        var projects = LogReader.Read(Log).Where(line => Text(line, "event") == "project").ToArray();
        projects.Select(line => Text(line, "path")).ShouldBe([lib.ProjectPath, _scene.Project.ProjectPath]);
        projects[0].GetProperty("inRun").GetBoolean().ShouldBeFalse();
        projects[0].TryGetProperty("usesSqlSource", out _).ShouldBeFalse();
        projects[1].GetProperty("inRun").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Run_ExitCodeTwo_IsTheExitCodeOfRunEnd()
    {
        var result = await _scene.DescribeAsync("--check", "--log", "run.log");

        result.ExitCode.ShouldBe(2);
        LogReader.Read(Log)[^1].GetProperty("exitCode").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Run_LogOptionAndVariable_WritesToTheOptionsFileAlone()
    {
        _scene.Run.Environment["SQLSOURCE_LOG"] = "variable.log";

        _ = await _scene.DescribeAsync("--log", "run.log");

        File.Exists(Log).ShouldBeTrue();
        File.Exists(_scene.Run.Folder.PathOf("variable.log")).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_LogVariableAlone_WritesToItsFile()
    {
        _scene.Run.Environment["SQLSOURCE_LOG"] = "run.log";

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(0);
        LogReader.Names(Log)[0].ShouldBe("run.start");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Run_NoLogOptionAndNoVariable_WritesNoFile(string? variable)
    {
        if (variable is not null)
        {
            _scene.Run.Environment["SQLSOURCE_LOG"] = variable;
        }

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(0);
        Directory.GetFiles(_scene.Run.Folder.Path, "*.log").ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_SecondRunWithTheSameLog_EmptiesTheFile()
    {
        _ = await _scene.DescribeAsync("--log", "run.log");

        _ = await _scene.DescribeAsync("--log", "run.log");

        LogReader.Names(Log).Count(name => name == "run.start").ShouldBe(1);
    }

    // "tmp" is the folder of the run's temporary files, so it is a directory.
    [Theory]
    [InlineData("tmp")]
    [InlineData("no-such-folder/run.log")]
    public async Task Run_LogThatCannotBeWritten_IsSqlsrc218AndTheRunDoesNotStart(string given)
    {
        var path = _scene.Run.Folder.PathOf(given);

        var result = await _scene.DescribeAsync("--log", given);

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith($"{path} : error SQLSRC218: '{path}' could not be written: ");
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    // The log's name forgotten before a .sql filter: the file of the run must not be emptied, and nothing runs.
    [Theory]
    [InlineData("option")]
    [InlineData("variable")]
    public async Task Run_LogThatIsAFileOfTheRun_IsSqlsrc218AndTheFileIsAsItWas(string source)
    {
        var before = await File.ReadAllBytesAsync(_users, TestContext.Current.CancellationToken);
        if (source == "variable")
        {
            _scene.Run.Environment["SQLSOURCE_LOG"] = "Users.sql";
        }

        var result =
            source == "option" ? await _scene.DescribeAsync("--log", "Users.sql") : await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith(
            $"{_users} : error SQLSRC218: '{_users}' could not be written: "
                + "a log is not written over a .sql file, a sidecar, a project or a solution"
        );
        (await File.ReadAllBytesAsync(_users, TestContext.Current.CancellationToken)).ShouldBe(before);
        _scene.Describer.Described.ShouldBeEmpty();
        _scene.Describer.Opened.ShouldBeEmpty();
        File.Exists(_users + ".json").ShouldBeFalse();
    }

    [Fact]
    public async Task Run_DescriberThatThrowsWithLog_IsADiagnosticOfSqlsrc200AndRunEndSaysOne()
    {
        _scene.Describer.Exceptions["GetUser"] = new InvalidOperationException("boom");

        var result = await _scene.DescribeAsync("--log", "run.log");

        result.ExitCode.ShouldBe(1);
        var events = LogReader.Read(Log);
        events[^2].GetProperty("id").GetString().ShouldBe("SQLSRC200");
        events[^1].GetProperty("event").GetString().ShouldBe("run.end");
        events[^1].GetProperty("exitCode").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Run_CancelledWithLog_EndsWithRunEndAndOne()
    {
        using var cancel = new CancellationTokenSource();
        _scene.Describer.BeforeDescribe = _ => cancel.CancelAsync();

        var result = await _scene.Run.RunAsync(cancel.Token, "describe", "--log", "run.log");

        result.ExitCode.ShouldBe(1);
        var end = LogReader.Read(Log)[^1];
        end.GetProperty("event").GetString().ShouldBe("run.end");
        end.GetProperty("exitCode").GetInt32().ShouldBe(1);
    }

    // A command line that was wrong is not logged at all: the run ended before the log was opened.
    [Theory]
    [InlineData("describe", "--log", "run.log", "--conection", "x")]
    [InlineData("describe", "--log", "run.log", "--database", "not a name")]
    public async Task Run_WrongCommandLineWithLog_WritesNoFile(params string[] args)
    {
        var result = await _scene.Run.RunAsync(args);

        result.ExitCode.ShouldBe(1);
        File.Exists(Log).ShouldBeFalse();
    }

    // A user's log of the last run must survive a look at the help.
    [Theory]
    [InlineData("describe", "--help")]
    [InlineData("--version")]
    [InlineData("--help")]
    public async Task Run_NoDescribeWithTheLogVariable_LeavesTheFileAsItWas(params string[] args)
    {
        _scene.Run.Environment["SQLSOURCE_LOG"] = "run.log";
        await File.WriteAllTextAsync(Log, "of the last run\n", TestContext.Current.CancellationToken);

        var result = await _scene.Run.RunAsync(args);

        result.ExitCode.ShouldBe(0);
        (await File.ReadAllTextAsync(Log, TestContext.Current.CancellationToken)).ShouldBe("of the last run\n");
    }

    [Fact]
    public async Task Run_LogWithAnEmptyValue_IsAWrongCommandLine()
    {
        var result = await _scene.DescribeAsync("--log", "");

        result.ShouldBe(new CliResult(1, "", "sqlsource: the value of option '--log' at position 3 is not a path\n"));
    }

    [Fact]
    public async Task Run_LogGivenTwice_IsAWrongCommandLine()
    {
        var result = await _scene.DescribeAsync("--log", "a.log", "--log=b.log");

        result.ShouldBe(new CliResult(1, "", "sqlsource: option '--log' is given more than once\n"));
        Directory.GetFiles(_scene.Run.Folder.Path, "*.log").ShouldBeEmpty();
    }

    // "/dev/full" takes no byte: every write fails as on a full disk.  Linux has it; the build server runs there.
    [Fact]
    public async Task Run_LogThatStopsBeingWritable_FinishesTheRunAndIsOneSqlsrc218()
    {
        Assert.SkipUnless(File.Exists("/dev/full"), "The system has no device that is always full.");

        var result = await _scene.DescribeAsync("--log", "/dev/full");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe("postgres (postgres): 2 described, 0 skipped, 0 failed\n");
        result.Error.ShouldStartWith("/dev/full : error SQLSRC218: '/dev/full' could not be written: ");
        result.Error.Split("error SQLSRC").Length.ShouldBe(2);
        File.Exists(_users + ".json").ShouldBeTrue();
    }
}
