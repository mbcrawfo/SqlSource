using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// "--verbose": prose on standard output for the person watching, one line for each thing the run decided.
public sealed class VerboseTests : IDisposable
{
    // The plan holds a database in the order it first holds a query of it: GetUser comes before GetInvoice.
    private const string Summary =
        "postgres (postgres): 2 described, 0 skipped, 0 failed\nbilling (postgres): 1 described, 0 skipped, 0 failed\n";

    private readonly DescribeScene _scene = new();
    private readonly TestProject _lib;
    private readonly string _items;
    private readonly string _solution;
    private readonly string _tools;
    private readonly string _users;

    public VerboseTests()
    {
        // Two projects that use SqlSource and one that does not, in a solution; two databases.
        _users = _scene.Project.AddSql(
            "Users.sql",
            "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n"
        );
        _lib = Plans.Postgres(_scene.Run.Folder, "Lib", "Lib").AnsweredBy(_scene.Run.Processes);
        _items = _lib.AddSql("Items.sql", "-- name: GetItem\nSELECT 3;\n");
        // A file that the project lists and that stands outside the folder of its type: no type claims it.
        _lib.ListSql(_scene.Run.Folder.WriteFile("Elsewhere/Loose.sql", "-- name: Loose\nSELECT 9;\n"));
        _tools = _scene.Run.Folder.WriteFile("Tools/Tools.csproj", "<Project />");
        _scene.Run.Processes.Projects[_tools] = new FakeProject
        {
            Imported = "",
            ProjectAssetsFile = _scene.Run.Folder.WriteFile("Tools/obj/project.assets.json", "{}"),
        };
        _solution = _scene.Run.Folder.WriteFile(
            "All.slnx",
            "<Solution><Project Path=\"App.csproj\" /><Project Path=\"Lib/Lib.csproj\" />"
                + "<Project Path=\"Tools/Tools.csproj\" /></Solution>"
        );
        _scene.Describer.BeforeDescribe = _ =>
        {
            _scene.Clock.Advance(TimeSpan.FromMilliseconds(25));
            return Task.CompletedTask;
        };
    }

    public void Dispose() => _scene.Dispose();

    private static string Line(string text) => "sqlsource: " + text + "\n";

    [Fact]
    public async Task Run_Verbose_SaysOfEachProjectFileDatabaseQueryAndSidecarWhatItDecided()
    {
        var app = _scene.Project.ProjectPath;
        var queries = _scene.Project.PathOf("Queries.cs");

        var result = await _scene.DescribeAsync("All.slnx", "--verbose");

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
        result.Out.ShouldBe(
            Line($"unit '{_solution}', a solution, from the path given")
                + Line($"project '{app}': net10.0, 1 .sql files")
                + Line($"project '{_lib.ProjectPath}': net10.0, 2 .sql files")
                + Line($"project '{_tools}' is left out: it does not use SqlSource")
                + Line($"project '{_lib.ProjectPath}': 1 .sql files that no type claims")
                + Line($"file '{_users}': postgres, ready, claimed by {queries}(1)")
                + Line($"file '{_items}': postgres, ready, claimed by {_lib.PathOf("Queries.cs")}(1)")
                + Line("database 'postgres' (postgres): connection from SQLSOURCE_CONNECTION_POSTGRES")
                + Line("database 'postgres': server 16.4")
                + Line($"query 'GetUser' in '{_users}': described in 25 ms")
                + Line($"query 'GetItem' in '{_items}': described in 25 ms")
                + Line("database 'billing' (postgres): connection from SQLSOURCE_CONNECTION_BILLING")
                + Line("database 'billing': server 16.4")
                + Line($"query 'GetInvoice' in '{_users}': described in 25 ms")
                + Line($"sidecar '{_users}.json': written")
                + Line($"sidecar '{_items}.json': written")
                + Summary
        );
    }

    [Fact]
    public async Task Run_SecondVerboseRun_SaysWhatWasSkippedAndWhatIsUnchanged()
    {
        _ = await _scene.DescribeAsync("All.slnx");

        var result = await _scene.DescribeAsync(
            "All.slnx",
            "--verbose",
            "--database",
            "postgres",
            "--project",
            "App.csproj"
        );

        result.Out.ShouldContain(Line($"project '{_lib.ProjectPath}' is left out: --project does not name it"));
        result.Out.ShouldContain(Line($"query 'GetUser' in '{_users}': skipped, its entry is current"));
        result.Out.ShouldContain(Line($"query 'GetInvoice' in '{_users}': not selected"));
        result.Out.ShouldContain(Line($"sidecar '{_users}.json': unchanged"));
        // Nothing of "postgres" is asked of a database, so no session is opened and none is spoken of.
        result.Out.ShouldNotContain("server 16.4");
    }

    [Fact]
    public async Task Run_VerboseRunThatFails_SaysWhatFailedAndWhatWasLeftAsItWas()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");
        _ = _scene.Run.Environment.Remove("SQLSOURCE_CONNECTION_BILLING");

        var result = await _scene.DescribeAsync("App.csproj", "--verbose");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldContain(Line("unit '" + _scene.Project.ProjectPath + "', a project, from the path given"));
        result.Out.ShouldContain(Line($"query 'GetUser' in '{_users}': failed"));
        result.Out.ShouldContain(Line("database 'billing' (postgres): no connection"));
        result.Out.ShouldContain(Line($"query 'GetInvoice' in '{_users}': failed"));
        result.Out.ShouldContain(Line($"sidecar '{_users}.json': left as it was"));
    }

    [Fact]
    public async Task Run_VerboseCheck_SaysOfEachSidecarWhetherItIsTheSame()
    {
        _ = await _scene.DescribeAsync("All.slnx");
        File.Delete(_items + ".json");

        var result = await _scene.DescribeAsync("All.slnx", "--verbose", "--check");

        result.ExitCode.ShouldBe(2);
        result.Out.ShouldContain(Line($"sidecar '{_users}.json': the same"));
        result.Out.ShouldContain(Line($"sidecar '{_items}.json': different"));
    }

    [Fact]
    public async Task Run_WithoutVerbose_PrintsTheSummaryAlone()
    {
        var result = await _scene.DescribeAsync("All.slnx");

        result.ShouldBe(new CliResult(0, Summary, ""));
    }

    // "--log" without "--verbose" keeps the console quiet, and "--verbose" without "--log" writes nothing to disk.
    [Fact]
    public async Task Run_LogWithoutVerbose_LeavesTheConsoleAsItIsWithoutEither()
    {
        var result = await _scene.DescribeAsync("All.slnx", "--log", "run.log");

        result.ShouldBe(new CliResult(0, Summary, ""));
        File.Exists(_scene.Run.Folder.PathOf("run.log")).ShouldBeTrue();
    }

    [Fact]
    public async Task Run_VerboseWithoutLog_WritesNoFileButTheSidecars()
    {
        _ = await _scene.DescribeAsync("All.slnx", "--verbose");

        Directory.GetFiles(_scene.Run.Folder.Path, "*.log", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_VerboseInTheWorkingDirectory_SaysTheUnitWasFoundThere()
    {
        File.Delete(_solution);

        var result = await _scene.DescribeAsync("--verbose");

        result.Out.ShouldStartWith(
            Line($"unit '{_scene.Project.ProjectPath}', a project, found in the working directory")
        );
    }
}
