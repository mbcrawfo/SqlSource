using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// A .sql file that two projects of a solution claim, under a run that --project narrows to one of them: the run keeps
// what the project it leaves out needs of the file's one sidecar.
public sealed class SharedFileRunTests : IDisposable
{
    private const string SharedSql = "-- name: Both\n-- output: models\nSELECT 1;\n\n-- name: OtherOnly\nSELECT 2;\n";

    private readonly CliRun _run = new();
    private readonly FakeDescriber _describer = new();
    private readonly TestProject _a;
    private readonly string _shared;

    // A finds the output sql for the file, and B finds models, by a nearer settings file that only B lists: the
    // query OtherOnly needs an entry for B alone.
    public SharedFileRunTests()
    {
        _run.Describers.Add(_describer);
        _run.Environment["SQLSOURCE_CONNECTION"] = Secrets.Connection;
        var farther = _run.Folder.WriteFile(
            "sqlsource.json", /*lang=json,strict*/
            """{"dialect": "postgres", "output": "sql"}"""
        );
        var nearer = _run.Folder.WriteFile(
            "Shared/sqlsource.json", /*lang=json,strict*/
            """{"output": "models"}"""
        );
        _shared = _run.Folder.WriteFile("Shared/One.sql", SharedSql);
        _a = Claiming(Plans.Postgres(_run.Folder, "A"), farther);
        var b = Claiming(Plans.Postgres(_run.Folder, "B"), farther);
        b.ListSettings(nearer);
        _ = _run.Folder.WriteFile(
            "App.slnx",
            "<Solution><Project Path=\"A/A.csproj\" /><Project Path=\"B/B.csproj\" /></Solution>"
        );
    }

    public void Dispose() => _run.Dispose();

    private TestProject Claiming(TestProject project, string settings)
    {
        project.ListSettings(settings);
        project.ListSql(_shared);
        _ = project.AddSource("Shared.cs", Plans.TypeNamed("Shared", "Source = \"../Shared\""));
        _ = project.AnsweredBy(_run.Processes);
        return project;
    }

    private string Sidecar() => File.ReadAllText(SidecarFormat.PathFor(_shared));

    private async Task DescribeTheSolutionAsync()
    {
        var result = await _run.RunAsync("describe");
        result.ExitCode.ShouldBe(0, result.Error);
        _ = SidecarFiles.Read(_shared).Find("OtherOnly").ShouldNotBeNull();
        _describer.Described.Clear();
    }

    [Fact]
    public async Task Run_Project_KeepsTheCurrentEntryThatTheProjectLeftOutNeeds()
    {
        await DescribeTheSolutionAsync();
        var before = Sidecar();

        var result = await _run.RunAsync("describe", "--project", _a.ProjectPath);

        result.ShouldBe(new CliResult(0, "postgres (postgres): 0 described, 1 skipped, 0 failed\n", ""));
        _describer.Described.ShouldBeEmpty();
        Sidecar().ShouldBe(before);
    }

    // The run does not describe a query for a project it leaves out, so a file whose entry for it is stale is left
    // as it was, entry and all, rather than written without it.
    [Fact]
    public async Task Run_Project_LeavesAFileWhoseEntryForTheProjectLeftOutIsStale()
    {
        await DescribeTheSolutionAsync();
        var before = Sidecar();
        await File.WriteAllTextAsync(
            _shared,
            SharedSql
                .Replace("SELECT 1", "SELECT 10", StringComparison.Ordinal)
                .Replace("SELECT 2", "SELECT 20", StringComparison.Ordinal),
            TestContext.Current.CancellationToken
        );

        var result = await _run.RunAsync("describe", "--project", _a.ProjectPath);

        _describer.Described.Select(request => request.Name).ShouldBe(["Both"]);
        result.Error.ShouldContain("SQLSRC217");
        Sidecar().ShouldBe(before);
    }

    // A watch's pass plans as a run does.
    [Fact]
    public async Task Watch_Project_KeepsTheCurrentEntryThatTheProjectLeftOutNeeds()
    {
        await DescribeTheSolutionAsync();
        var before = Sidecar();
        var watcher = new FakeFolderWatcher();
        _run.Watch = watcher.Watch;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = _run.Start(stop.Token, "describe", "--watch", "--project", _a.ProjectPath);
        await Eventually.TrueAsync(
            () => running.Output.Contains(WatchSession.WatchingMarker, StringComparison.Ordinal),
            $"The watch did not end its first pass.  It wrote: {running.Output}{running.Errors}"
        );

        await stop.CancelAsync();
        var result = await Eventually.WithinAsync(running.Ended, "The watch did not end when it was stopped.");

        result.ExitCode.ShouldBe(0, result.Error);
        _describer.Described.ShouldBeEmpty();
        Sidecar().ShouldBe(before);
    }

    [Fact]
    public async Task Check_Project_FindsNoDifferenceInTheEntryThatTheProjectLeftOutNeeds()
    {
        await DescribeTheSolutionAsync();

        var result = await _run.RunAsync("describe", "--check", "--project", _a.ProjectPath);

        result.ShouldBe(new CliResult(0, "postgres (postgres): 1 same, 0 differ, 0 failed\n", ""));
    }
}
