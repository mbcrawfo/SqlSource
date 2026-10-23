using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Logging;

// Where the log of a run goes out: "--log" and SQLSOURCE_LOG, the paths a run refuses, and a log that cannot be written
// or closed.
public sealed class RunLogDestinationTests : IDisposable
{
    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public RunLogDestinationTests()
    {
        _users = _scene.Project.AddSql("Users.sql", SqlTexts.TwoUsers);
    }

    private string Log => _scene.Run.Folder.PathOf("run.log");

    public void Dispose() => _scene.Dispose();

    // A path with "=" or ";" is more likely a connection string typed in the wrong place than a path.
    [Fact]
    public async Task Run_LogPathWithAnEqualsSign_IsRefusedByItsPositionAndNoFileIsMade()
    {
        var result = await _scene.DescribeAsync("--log", "a=b.log");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldBe(
            "sqlsource: the value of option '--log' at position 3 holds '=' or ';', which a log's path may not\n"
        );
        File.Exists(_scene.Run.Folder.PathOf("a=b.log")).ShouldBeFalse();
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
        File.Exists(SidecarFormat.PathFor(_users)).ShouldBeFalse();
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

    // A log that cannot take a line, as when the disk is full, must not end the run: the run finishes and says so once.
    [Fact]
    public async Task Run_LogThatStopsBeingWritable_FinishesTheRunAndIsOneSqlsrc218()
    {
        _scene.Run.CreateFile = static _ => new FullStream();

        var result = await _scene.DescribeAsync("--log", "run.log");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe("postgres (postgres): 2 described, 0 skipped, 0 failed\n");
        result.Error.ShouldStartWith($"{Log} : error SQLSRC218: '{Log}' could not be written: No space left on device");
        result.Error.Split("error SQLSRC").Length.ShouldBe(2);
        File.Exists(SidecarFormat.PathFor(_users)).ShouldBeTrue();
    }

    // A log whose close fails is reported once the run has ended, after run.end, which holds the run's own exit code:
    // no line written before the close can hold what the close did.
    [Fact]
    public async Task Run_LogWhoseCloseFails_IsOneSqlsrc218AndItsRunEndHoldsTheRunsExitCode()
    {
        _scene.Run.CreateFile = static path => new UnclosableStream(path);

        var result = await _scene.DescribeAsync("--log", "run.log");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            $"{Log} : error SQLSRC218: '{Log}' could not be written: the file could not be closed"
        );
        result.Error.Split("error SQLSRC").Length.ShouldBe(2);
        var end = LogReader.Read(Log)[^1];
        end.GetProperty("event").GetString().ShouldBe("run.end");
        end.GetProperty("exitCode").GetInt32().ShouldBe(0);
    }

    // A file that takes every line and whose close then fails, as when the system reports a failed write only then.
    private sealed class UnclosableStream(string path)
        : FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                throw new IOException("the file could not be closed");
            }
        }
    }

    // A stream that takes no byte, whatever the system.
    private sealed class FullStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) => throw Full();

        public override void Write(ReadOnlySpan<byte> buffer) => throw Full();

        public override void Flush() => throw Full();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        private static IOException Full() => new("No space left on device");
    }
}
