using System.IO;
using System.Linq;
using System.Threading;
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
        result.Out.ShouldContain("sqlsource describe [<path>...]");
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
    public async Task Run_Help_ShowsTheProjectOption()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("describe", "--help");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("--project <path>");
        run.Processes.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("describe", "--project")]
    [InlineData("describe", "--project=")]
    [InlineData("describe", "App.slnx", "--project", Secret, "--project")]
    public async Task Run_ProjectOptionWithoutAValue_NamesTheOptionAndRunsNothing(params string[] args)
    {
        using var run = new CliRun();
        _ = run.Folder.WriteFile("App.slnx", "<Solution />");

        var result = await run.RunAsync(args);

        result.ShouldBe(new CliResult(1, "", "sqlsource: option '--project' needs a value\n"));
        run.Processes.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_Solution_ReadsItsProjectsAndPrintsNothing()
    {
        using var run = new CliRun();
        _ = run.Folder.WriteFile("A/A.csproj");
        _ = run.Folder.WriteFile("B/B.csproj");
        _ = run.Folder.WriteFile(
            "App.slnx",
            "<Solution><Project Path=\"A/A.csproj\" /><Project Path=\"B/B.csproj\" /></Solution>"
        );

        var result = await run.RunAsync("describe");

        result.ShouldBe(new CliResult(0, "", ""));
        // Two runs of MSBuild for each of the two projects.
        run.Processes.Requests.Count.ShouldBe(4);
        Directory.EnumerateFileSystemEntries(run.Folder.PathOf("tmp")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ProjectOptionGivenTwice_RestrictsTheRunToBoth()
    {
        using var run = new CliRun();
        var a = run.Folder.WriteFile("A/A.csproj");
        var c = run.Folder.WriteFile("C/C.csproj");
        _ = run.Folder.WriteFile(
            "App.slnx",
            "<Solution><Project Path=\"A/A.csproj\" /><Project Path=\"B/B.csproj\" />"
                + "<Project Path=\"C/C.csproj\" /></Solution>"
        );

        var result = await run.RunAsync("describe", "--project", "A/A.csproj", "App.slnx", "--project=" + c);

        result.ShouldBe(new CliResult(0, "", ""));
        run.Processes.Requests.Select(request => request.Arguments[1]).Distinct().ShouldBe([a, c], ignoreOrder: true);
    }

    [Fact]
    public async Task Run_ProjectOptionOutsideTheSolution_IsSqlsrc207AndExitsWithOne()
    {
        using var run = new CliRun();
        var solution = run.Folder.WriteFile("App.slnx", "<Solution><Project Path=\"A/A.csproj\" /></Solution>");

        var result = await run.RunAsync("describe", "--project", "Other.csproj");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldBe(
            $"sqlsource : error SQLSRC207: '{run.Folder.PathOf("Other.csproj")}' is not a project of '{solution}'\n"
                + "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc207\n"
        );
    }

    [Fact]
    public async Task Run_ProjectThatDoesNotUseSqlSource_IsSqlsrc204AndExitsWithOne()
    {
        using var run = new CliRun();
        var project = run.Folder.WriteFile("App.csproj");
        // Restored, and nothing of the package.
        run.Processes.Default = new FakeProject { Imported = "", ProjectAssetsFile = project };

        var result = await run.RunAsync("describe");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith($"{project} : error SQLSRC204: '{project}' does not use SqlSource\n");
    }

    // A run that had nothing to do says so, and is no error: a solution may gain its first query later.
    [Fact]
    public async Task Run_SolutionWhereNoProjectUsesSqlSource_SaysSoAndExitsWithZero()
    {
        using var run = new CliRun();
        _ = run.Folder.WriteFile("A/A.csproj");
        var solution = run.Folder.WriteFile("App.slnx", "<Solution><Project Path=\"A/A.csproj\" /></Solution>");
        run.Processes.Default = new FakeProject { Imported = "", ProjectAssetsFile = solution };

        var result = await run.RunAsync("describe");

        result.ShouldBe(new CliResult(0, $"sqlsource: no project of '{solution}' uses SqlSource\n", ""));
    }

    [Fact]
    public async Task Run_SolutionWithNoProject_SaysThatNoProjectUsesSqlSource()
    {
        using var run = new CliRun();
        var solution = run.Folder.WriteFile("App.slnx", "<Solution />");

        var result = await run.RunAsync("describe");

        result.ShouldBe(new CliResult(0, $"sqlsource: no project of '{solution}' uses SqlSource\n", ""));
        run.Processes.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_SolutionThatCannotBeRead_IsSqlsrc223AndSaysNothingElse()
    {
        using var run = new CliRun();
        var solution = run.Folder.WriteFile("App.sln", "this is not a solution\n");

        var result = await run.RunAsync("describe");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith($"{solution} : error SQLSRC223: '{solution}' cannot be read: ");
    }

    [Fact]
    public async Task Run_CancelledWhileMSBuildRuns_ExitsWithOneAndPrintsNothing()
    {
        using var run = new CliRun();
        _ = run.Folder.WriteFile("App.csproj");
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        run.Processes.BeforeAnswer = async (_, token) =>
        {
            await cancel.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
        };

        var result = await run.RunAsync(cancel.Token, "describe");

        result.ShouldBe(new CliResult(1, "", ""));
        Directory.EnumerateFileSystemEntries(run.Folder.PathOf("tmp")).ShouldBeEmpty();
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

    [Fact]
    public async Task Run_ResponseFileWhereThePathStands_IsAPathLikeAnyOther()
    {
        using var run = new CliRun();
        var path = run.Folder.WriteFile("@args.rsp", "--help");
        _ = run.Folder.WriteFile("args.rsp", "--help");

        // System.CommandLine would read the file and run what it holds.
        var result = await run.RunAsync("describe", "@args.rsp");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith($"sqlsource : error SQLSRC203: '{path}' is not a .sln, .slnx or .csproj file");
    }

    private const string Queries = "[SqlSource.SqlSourceGenerate]\ninternal static partial class Queries;\n";

    // The released tool has no describer, so a query that must be described is SQLSRC216.
    [Fact]
    public async Task Run_ProjectWithAQueryToDescribe_IsSqlsrc216AndASummaryLine()
    {
        using var run = new CliRun();
        var project = new TestProject(run.Folder).AnsweredBy(run.Processes);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource("Queries.cs", Queries);
        var sql = project.AddSql("One.sql", "-- name: One\nSELECT 1;\n");

        var result = await run.RunAsync("describe", project.ProjectPath);

        result.ShouldBe(
            new CliResult(
                1,
                "postgres (postgres): 0 described, 0 skipped, 1 failed\n",
                $"{sql}(1,10): error SQLSRC216: This version of sqlsource cannot describe 'postgres'\n"
                    + "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc216\n"
            )
        );
    }

    [Fact]
    public async Task Run_ProjectWhoseQueriesNeedNoEntry_PrintsNothing()
    {
        using var run = new CliRun();
        var project = new TestProject(run.Folder).AnsweredBy(run.Processes);
        project.Properties["SqlSourceDialect"] = "postgres";
        project.Properties["SqlSourceOutput"] = "sql";
        _ = project.AddSource("Queries.cs", Queries);
        _ = project.AddSql("One.sql", "-- name: One\nSELECT 1;\n");

        var result = await run.RunAsync("describe", project.ProjectPath);

        result.ShouldBe(new CliResult(0, "", ""));
    }

    [Fact]
    public async Task Run_ProjectWithAFileThatDoesNotParse_ReportsItAsABuildWouldAndExitsOne()
    {
        using var run = new CliRun();
        var project = new TestProject(run.Folder).AnsweredBy(run.Processes);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource("Queries.cs", Queries);
        var sql = project.AddSql("Broken.sql", "-- name: One\nSELECT 'open\n");

        var result = await run.RunAsync("describe", project.ProjectPath);

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith($"{sql}(2,8): error SQLSRC101: ");
        result.Error.ShouldEndWith("/docs/diagnostics.md#sqlsrc101\n");
    }

    [Fact]
    public async Task Plan_Project_GivesThePlanOfTheCommandLine()
    {
        using var run = new CliRun();
        var project = new TestProject(run.Folder).AnsweredBy(run.Processes);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource("Queries.cs", Queries);
        var sql = project.AddSql("One.sql", "SELECT 1;");

        var (plan, result) = await run.PlanAsync("describe", project.ProjectPath);

        result.ShouldBe(new CliResult(0, "", ""));
        plan.ShouldNotBeNull().Files.ShouldHaveSingleItem().Path.ShouldBe(sql);
    }
}
