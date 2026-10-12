using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// The three exit codes, and the one line that tells a user of a failed run how to get a record of it.
public sealed class ExitCodeTests : IDisposable
{
    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public ExitCodeTests()
    {
        _users = _scene.Project.AddSql("Users.sql", SqlTexts.GetUser);
    }

    public void Dispose() => _scene.Dispose();

    [Fact]
    public async Task Run_NothingReported_ExitsZeroWithoutTheLine()
    {
        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 1 described, 0 skipped, 0 failed\n", ""));
    }

    [Fact]
    public async Task Run_ErrorWithoutALog_ExitsOneAndEndsWithTheLine()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith($"{_users}(1,10): error SQLSRC999: ");
        result.Error.ShouldEndWith($"    see: https://example.test/sqlsrc999\n{DescribeScene.RunAgain}");
        result.Error.Split(DescribeScene.RunAgain).Length.ShouldBe(2);
    }

    // An exception is an error of the run too, and a log is what a maintainer will ask for.
    [Fact]
    public async Task Run_DescriberThatThrows_ExitsOneAndEndsWithTheLine()
    {
        _scene.Describer.Exceptions["GetUser"] = new InvalidOperationException("boom");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldEndWith(Expected.See("SQLSRC200") + DescribeScene.RunAgain);
    }

    // The line is for a difference that could not be checked, not for one that was found.
    [Fact]
    public async Task Run_CheckThatFindsADifference_ExitsTwoWithoutTheLine()
    {
        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("error SQLSRC219: ");
        result.Error.ShouldNotContain("run again");
    }

    [Fact]
    public async Task Run_CheckThatCouldNotDescribe_ExitsOneWithTheLine()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldEndWith(DescribeScene.RunAgain);
    }

    [Theory]
    [InlineData("option", "SQLSRC999")]
    [InlineData("variable", "SQLSRC999")]
    [InlineData("a log that cannot be opened", "SQLSRC218")]
    [InlineData("a log that is a file of the run", "SQLSRC218")]
    public async Task Run_ErrorWithALogAskedFor_ExitsOneWithoutTheLine(string kind, string id)
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");
        if (kind == "variable")
        {
            _scene.Run.Environment["SQLSOURCE_LOG"] = "run.log";
        }

        var result = kind switch
        {
            "option" => await _scene.DescribeAsync("--log", "run.log"),
            "variable" => await _scene.DescribeAsync(),
            "a log that is a file of the run" => await _scene.DescribeAsync("--log", "Users.sql"),
            _ => await _scene.DescribeAsync("--log", "tmp"),
        };

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain($"error {id}: ");
        result.Error.ShouldNotContain("run again");
    }

    [Theory]
    [InlineData("describe", "--conection", "x")]
    [InlineData("describe", "--database", "not a name")]
    [InlineData("--oops")]
    public async Task Run_WrongCommandLine_ExitsOneWithoutTheLine(params string[] args)
    {
        var result = await _scene.Run.RunAsync(args);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith("sqlsource: ");
        result.Error.ShouldNotContain("run again");
    }

    // A run that was cancelled prints nothing more, whatever it had reported.
    [Fact]
    public async Task Run_CancelledAfterAnError_ExitsOneWithoutTheLine()
    {
        _ = _scene.Project.AddSql("More.sql", "-- name: First\nSELECT 0;\n");
        _scene.Describer.Failures["First"] = FakeDescriber.Failure("First");
        using var cancel = new CancellationTokenSource();
        _scene.Describer.BeforeDescribe = request =>
            request.Name == "GetUser" ? cancel.CancelAsync() : Task.CompletedTask;

        var result = await _scene.Run.RunAsync(cancel.Token, "describe");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC999: ");
        result.Error.ShouldNotContain("run again");
    }

    [Fact]
    public async Task Run_CancelledBeforeItStarted_ExitsOneAndPrintsNothing()
    {
        var result = await _scene.Run.RunCancelledAsync("describe");

        result.ShouldBe(new CliResult(1, "", ""));
    }

    // An error of a run that never reached a database is still an error of "describe".
    [Fact]
    public async Task Run_NoUnit_ExitsOneWithTheLine()
    {
        File.Delete(_scene.Project.ProjectPath);

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC201: ");
        result.Error.ShouldEndWith(DescribeScene.RunAgain);
    }
}
