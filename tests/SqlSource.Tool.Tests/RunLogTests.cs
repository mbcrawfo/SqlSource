using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tool.Tests;

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
