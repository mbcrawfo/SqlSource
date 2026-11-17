using System;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Testing;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// Which failed runs end with the one line that tells a user how to get a record of the run.
public sealed class ExitCodeTests : IDisposable
{
    private readonly DescribeScene _scene = new();

    public ExitCodeTests()
    {
        _ = _scene.Project.AddSql("Users.sql", SqlTexts.GetUser);
    }

    public void Dispose() => _scene.Dispose();

    [Fact]
    public async Task Run_CheckThatCouldNotDescribe_ExitsOneWithTheLine()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldEndWith(DescribeScene.RunAgain);
    }

    private static void ShouldExitOneWithoutTheLine(CliResult result, string id)
    {
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain($"error {id}: ");
        result.Error.ShouldNotContain("run again");
    }

    [Fact]
    public async Task Run_ErrorWithTheLogOption_ExitsOneWithoutTheLine()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await _scene.DescribeAsync("--log", "run.log");

        ShouldExitOneWithoutTheLine(result, "SQLSRC999");
    }

    [Fact]
    public async Task Run_ErrorWithTheLogVariable_ExitsOneWithoutTheLine()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");
        _scene.Run.Environment["SQLSOURCE_LOG"] = "run.log";

        var result = await _scene.DescribeAsync();

        ShouldExitOneWithoutTheLine(result, "SQLSRC999");
    }

    [Fact]
    public async Task Run_LogThatCannotBeOpened_ExitsOneWithoutTheLine()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await _scene.DescribeAsync("--log", "tmp");

        ShouldExitOneWithoutTheLine(result, "SQLSRC218");
    }

    [Fact]
    public async Task Run_LogThatIsAFileOfTheRun_ExitsOneWithoutTheLine()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await _scene.DescribeAsync("--log", "Users.sql");

        ShouldExitOneWithoutTheLine(result, "SQLSRC218");
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
}
