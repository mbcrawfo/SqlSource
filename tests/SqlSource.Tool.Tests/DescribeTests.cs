using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// The describe command from the command line to the exit code.  RunUnitTests holds every case of finding the unit.
public class DescribeTests
{
    private const string Secret = "s3cret";

    [Fact]
    public async Task Run_HelpOfTheTool_ListsTheCommand()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("--help");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("describe");
    }

    [Fact]
    public async Task Run_Help_ShowsThePath()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("describe", "--help");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("sqlsource describe [<path>]");
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_VersionBeforeACommand_PrintsTheVersionAndRunsNothing()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("--version", "describe");

        result.ShouldBe(new CliResult(0, Cli.Version + "\n", ""));
    }

    [Fact]
    public async Task Run_UnknownOptionWhereThePathCouldStand_IsNotTakenForAPath()
    {
        using var run = new CliRun();
        _ = run.Folder.WriteFile("-x/App.csproj");

        var result = await run.RunAsync("describe", "-x");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '-x'\n"));
    }

    [Fact]
    public async Task Run_PathThatStartsWithAHyphen_IsGivenWithAFolderBeforeIt()
    {
        using var run = new CliRun();
        _ = run.Folder.WriteFile("-x/App.csproj");

        var result = await run.RunAsync("describe", "./-x");

        result.ShouldBe(new CliResult(0, "", ""));
    }

    [Fact]
    public async Task Run_SecondPath_IsReportedByItsPositionAndNotItsText()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("describe", "App.csproj", Secret);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 3\n"));
    }

    [Fact]
    public async Task Run_UnitFound_PrintsNothing()
    {
        using var run = new CliRun();
        _ = run.Folder.WriteFile("App.csproj");

        var result = await run.RunAsync("describe");

        result.ShouldBe(new CliResult(0, "", ""));
    }

    [Fact]
    public async Task Run_NoUnit_ReportsItAndExitsWithOne()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("describe");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldBe(
            $"sqlsource : error SQLSRC201: '{run.Folder.Path}' holds no .sln, .slnx or .csproj file\n"
                + "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc201\n"
        );
    }
}
