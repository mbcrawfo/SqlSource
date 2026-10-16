using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// The describe command from the command line to the exit code.  RunUnitTests holds every case of finding the unit.
public sealed class DescribeTests : IDisposable
{
    private readonly CliRun _run = new();

    public void Dispose() => _run.Dispose();

    [Fact]
    public async Task Run_HelpOfTheTool_ListsTheCommand()
    {
        var result = await _run.RunAsync("--help");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("describe");
    }

    [Fact]
    public async Task Run_Help_ShowsThePath()
    {
        var result = await _run.RunAsync("describe", "--help");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("sqlsource describe [<path>...]");
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_VersionBeforeACommand_PrintsTheVersionAndRunsNothing()
    {
        var result = await _run.RunAsync("--version", "describe");

        result.ShouldBe(new CliResult(0, Cli.Version + "\n", ""));
    }

    [Fact]
    public async Task Run_UnknownOptionWhereThePathCouldStand_IsNotTakenForAPath()
    {
        _ = _run.Folder.WriteFile("-x/App.csproj");

        var result = await _run.RunAsync("describe", "-x");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '-x'\n"));
    }

    [Fact]
    public async Task Run_PathThatStartsWithAHyphen_IsGivenWithAFolderBeforeIt()
    {
        _ = _run.Folder.WriteFile("-x/App.csproj");

        var result = await _run.RunAsync("describe", "./-x");

        result.ShouldBe(new CliResult(0, "", ""));
    }

    [Fact]
    public async Task Run_SecondPath_IsReportedByItsPositionAndNotItsText()
    {
        var result = await _run.RunAsync("describe", "App.csproj", Secrets.Word);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 3\n"));
    }

    [Fact]
    public async Task Run_UnitFound_PrintsNothing()
    {
        _ = _run.Folder.WriteFile("App.csproj");

        var result = await _run.RunAsync("describe");

        result.ShouldBe(new CliResult(0, "", ""));
    }

    [Fact]
    public async Task Run_Help_ShowsTheProjectOption()
    {
        var result = await _run.RunAsync("describe", "--help");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("--project <path>");
        result.Out.ShouldContain("--check");
        result.Out.ShouldContain("--log <path>");
        result.Out.ShouldContain("--verbose");
        _run.Processes.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("describe", "--project")]
    [InlineData("describe", "--project=")]
    [InlineData("describe", "App.slnx", "--project", Secrets.Word, "--project")]
    public async Task Run_ProjectOptionWithoutAValue_NamesTheOptionAndRunsNothing(params string[] args)
    {
        _ = _run.Folder.WriteFile("App.slnx", "<Solution />");

        var result = await _run.RunAsync(args);

        result.ShouldBe(new CliResult(1, "", "sqlsource: option '--project' needs a value\n"));
        _run.Processes.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_Solution_ReadsItsProjectsAndPrintsNothing()
    {
        _ = _run.Folder.WriteFile("A/A.csproj");
        _ = _run.Folder.WriteFile("B/B.csproj");
        _ = _run.Folder.WriteFile(
            "App.slnx",
            "<Solution><Project Path=\"A/A.csproj\" /><Project Path=\"B/B.csproj\" /></Solution>"
        );

        var result = await _run.RunAsync("describe");

        result.ShouldBe(new CliResult(0, "", ""));
        // Two runs of MSBuild for each of the two projects.
        _run.Processes.Requests.Count.ShouldBe(4);
        Directory.EnumerateFileSystemEntries(_run.Folder.PathOf("tmp")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ProjectOptionGivenTwice_RestrictsTheRunToBoth()
    {
        var a = _run.Folder.WriteFile("A/A.csproj");
        var c = _run.Folder.WriteFile("C/C.csproj");
        _ = _run.Folder.WriteFile(
            "App.slnx",
            "<Solution><Project Path=\"A/A.csproj\" /><Project Path=\"B/B.csproj\" />"
                + "<Project Path=\"C/C.csproj\" /></Solution>"
        );

        var result = await _run.RunAsync("describe", "--project", "A/A.csproj", "App.slnx", "--project=" + c);

        result.ShouldBe(new CliResult(0, "", ""));
        _run.Processes.Requests.Select(request => request.Arguments[1]).Distinct().ShouldBe([a, c], ignoreOrder: true);
    }

    [Fact]
    public async Task Run_ProjectOptionOutsideTheSolution_IsSqlsrc207AndExitsWithOne()
    {
        var solution = _run.Folder.WriteFile("App.slnx", "<Solution><Project Path=\"A/A.csproj\" /></Solution>");

        var result = await _run.RunAsync("describe", "--project", "Other.csproj");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldBe(
            $"sqlsource : error SQLSRC207: '{_run.Folder.PathOf("Other.csproj")}' is not a project of '{solution}'\n"
                + Expected.See("SQLSRC207")
                + DescribeScene.RunAgain
        );
    }

    [Fact]
    public async Task Run_ProjectThatDoesNotUseSqlSource_IsSqlsrc204AndExitsWithOne()
    {
        var project = _run.Folder.WriteFile("App.csproj");
        // Restored, and nothing of the package.
        _run.Processes.Default = new FakeProject { Imported = "", ProjectAssetsFile = project };

        var result = await _run.RunAsync("describe");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith($"{project} : error SQLSRC204: '{project}' does not use SqlSource\n");
    }

    // A run that had nothing to do says so, and is no error: a solution may gain its first query later.
    [Fact]
    public async Task Run_SolutionWhereNoProjectUsesSqlSource_SaysSoAndExitsWithZero()
    {
        _ = _run.Folder.WriteFile("A/A.csproj");
        var solution = _run.Folder.WriteFile("App.slnx", "<Solution><Project Path=\"A/A.csproj\" /></Solution>");
        _run.Processes.Default = new FakeProject { Imported = "", ProjectAssetsFile = solution };

        var result = await _run.RunAsync("describe");

        result.ShouldBe(new CliResult(0, $"sqlsource: no project of '{solution}' uses SqlSource\n", ""));
    }

    [Fact]
    public async Task Run_SolutionWithNoProject_SaysThatNoProjectUsesSqlSource()
    {
        var solution = _run.Folder.WriteFile("App.slnx", "<Solution />");

        var result = await _run.RunAsync("describe");

        result.ShouldBe(new CliResult(0, $"sqlsource: no project of '{solution}' uses SqlSource\n", ""));
        _run.Processes.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_SolutionThatCannotBeRead_IsSqlsrc223AndSaysNothingElse()
    {
        var solution = _run.Folder.WriteFile("App.sln", "this is not a solution\n");

        var result = await _run.RunAsync("describe");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith($"{solution} : error SQLSRC223: '{solution}' cannot be read: ");
    }

    [Fact]
    public async Task Run_CancelledWhileMSBuildRuns_ExitsWithOneAndPrintsNothing()
    {
        _ = _run.Folder.WriteFile("App.csproj");
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _run.Processes.BeforeAnswer = async (_, token) =>
        {
            await cancel.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
        };

        var result = await _run.RunAsync(cancel.Token, "describe");

        result.ShouldBe(new CliResult(1, "", ""));
        Directory.EnumerateFileSystemEntries(_run.Folder.PathOf("tmp")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_NoUnit_ReportsItAndExitsWithOne()
    {
        var result = await _run.RunAsync("describe");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldBe(
            $"sqlsource : error SQLSRC201: '{_run.Folder.Path}' holds no .sln, .slnx or .csproj file\n"
                + Expected.See("SQLSRC201")
                + DescribeScene.RunAgain
        );
    }

    [Fact]
    public async Task Run_ResponseFileWhereThePathStands_IsAPathLikeAnyOther()
    {
        var path = _run.Folder.WriteFile("@args.rsp", "--help");
        _ = _run.Folder.WriteFile("args.rsp", "--help");

        // System.CommandLine would read the file and run what it holds.
        var result = await _run.RunAsync("describe", "@args.rsp");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith($"sqlsource : error SQLSRC203: '{path}' is not a .sln, .slnx or .csproj file");
    }

    // A run of the tests has no describer unless the test adds one, so a query that must be described is SQLSRC216.
    [Fact]
    public async Task Run_ProjectWithAQueryToDescribe_IsSqlsrc216AndASummaryLine()
    {
        var project = Plans.Postgres(_run.Folder).AnsweredBy(_run.Processes);
        var sql = project.AddSql("One.sql", "-- name: One\nSELECT 1;\n");

        var result = await _run.RunAsync("describe", project.ProjectPath);

        result.ShouldBe(
            new CliResult(
                1,
                "postgres (postgres): 0 described, 0 skipped, 1 failed\n",
                $"{sql}(1,10): error SQLSRC216: This version of sqlsource cannot describe 'postgres'\n"
                    + Expected.See("SQLSRC216")
                    + DescribeScene.RunAgain
            )
        );
    }

    [Fact]
    public async Task Run_ProjectWhoseQueriesNeedNoEntry_PrintsNothing()
    {
        var project = Plans.Postgres(_run.Folder).AnsweredBy(_run.Processes);
        project.Properties["SqlSourceOutput"] = "sql";
        _ = project.AddSql("One.sql", "-- name: One\nSELECT 1;\n");

        var result = await _run.RunAsync("describe", project.ProjectPath);

        result.ShouldBe(new CliResult(0, "", ""));
    }

    [Fact]
    public async Task Run_ProjectWithAFileThatDoesNotParse_ReportsItAsABuildWouldAndExitsOne()
    {
        var project = Plans.Postgres(_run.Folder).AnsweredBy(_run.Processes);
        var sql = project.AddSql("Broken.sql", "-- name: One\nSELECT 'open\n");

        var result = await _run.RunAsync("describe", project.ProjectPath);

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith($"{sql}(2,8): error SQLSRC101: ");
        result.Error.ShouldEndWith(Expected.See("SQLSRC101") + DescribeScene.RunAgain);
    }

    [Fact]
    public async Task Plan_Project_GivesThePlanOfTheCommandLine()
    {
        var project = Plans.Postgres(_run.Folder).AnsweredBy(_run.Processes);
        var sql = project.AddSql("One.sql", "SELECT 1;");

        var (plan, result) = await _run.PlanAsync("describe", project.ProjectPath);

        result.ShouldBe(new CliResult(0, "", ""));
        plan.ShouldNotBeNull().Files.ShouldHaveSingleItem().Path.ShouldBe(sql);
    }
}
