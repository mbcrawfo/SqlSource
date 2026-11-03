using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Projects;

// The online build of a project with two frameworks, net8.0 and net10.0, run for real: "dotnet build" of a copy of the
// fixture OnlineMulti, with no obj, whose one .sql file has no sidecar until the stand-in of the tool creates it.  The
// tool runs once, for the first framework, however the project is built.  Each test restores and builds once, a few
// seconds.
public sealed class OnlineMultiTargetTests : IDisposable
{
    private static readonly Dictionary<string, string> NoVariables = [];

    private readonly FixtureProjects _fixtures = new();
    private readonly string _project;
    private readonly string _folder;

    public OnlineMultiTargetTests()
    {
        _project = _fixtures.Copy("OnlineMulti");
        _folder = Path.GetDirectoryName(_project)!;
        StandIn.Tell(_folder, create: new() { ["Queries/Users.sql.json"] = "{}" });
    }

    public void Dispose() => _fixtures.Dispose();

    private string Sidecar => Path.Combine(_folder, "Queries", "Users.sql.json");

    // The outer build runs the tool before it dispatches the inner builds, and they then build side by side: under
    // OnlineMultiMeet each waits after its CoreCompile until the other has reached it, which an inner build that
    // waited for the other's whole build never would.  Two nodes, whatever the machine's processors.
    [Fact]
    public async Task Build_EveryFramework_RunsTheToolOnceForTheFirstBeforeTheInnerBuildsRunTogether()
    {
        var log = _fixtures.PathOf("diag.log");

        var (exitCode, output) = await FixtureProjects.BuildAsync(
            _project,
            NoVariables,
            StandIn.CommandSwitch,
            "-p:SqlSourceOnline=true",
            "-p:OnlineMultiMeet=true",
            "-m:2",
            $"-flp:v=diag;logfile={log}"
        );

        exitCode.ShouldBe(0, output);
        RanOnceForTheFirstFramework();
        CompiledItems("net8.0").ShouldContain(Sidecar);
        CompiledItems("net10.0").ShouldContain(Sidecar);
        var lines = await File.ReadAllLinesAsync(log, TestContext.Current.CancellationToken);
        Index(lines, "Done building target \"SqlSourceRunOnlineBeforeDispatch\"")
            .ShouldBeLessThan(Index(lines, "Target \"DispatchToInnerBuilds:"));
        IndexesOf(lines, "Target \"OnlineMultiMeet:").Count.ShouldBe(2);
        IndexesOf(lines, "Target \"OnlineMultiMeet:")
            .Max()
            .ShouldBeLessThan(IndexesOf(lines, "Done building target \"OnlineMultiMeet\"").Min());
    }

    // A build of one framework has no outer build, and its framework runs the tool in the first one's configuration.
    [Fact]
    public async Task Build_TheSecondFrameworkAlone_RunsTheToolOnceForTheFirst()
    {
        var (exitCode, output) = await FixtureProjects.BuildAsync(
            _project,
            NoVariables,
            StandIn.CommandSwitch,
            "-p:SqlSourceOnline=true",
            "-f",
            "net10.0"
        );

        exitCode.ShouldBe(0, output);
        RanOnceForTheFirstFramework();
        CompiledItems("net10.0").ShouldContain(Sidecar);
    }

    // A project that references this one builds it for the framework nearest its own, net10.0, alone.
    [Fact]
    public async Task Build_OfAProjectThatReferencesIt_RunsTheToolOnceForTheFirst()
    {
        var reference = _fixtures.Copy("OnlineReference", "Reference.csproj");

        var (exitCode, output) = await FixtureProjects.BuildAsync(
            reference,
            NoVariables,
            StandIn.CommandSwitch,
            "-p:SqlSourceOnline=true"
        );

        exitCode.ShouldBe(0, output);
        RanOnceForTheFirstFramework();
        CompiledItems("net10.0").ShouldContain(Sidecar);
    }

    // One run, on the manifest of the net8.0 build.
    private void RanOnceForTheFirstFramework()
    {
        var run = StandIn.RunsIn(_folder).ShouldHaveSingleItem();
        run.Manifest.ShouldNotBeNull().ShouldContain("TargetFramework=net8.0");
        run.Arguments[2].ShouldContain("net8.0");
    }

    // The full paths of the AdditionalFiles items a framework's compiler was given, which the fixture's
    // Directory.Build.targets writes.
    private string[] CompiledItems(string framework) =>
        File.ReadAllLines(Path.Combine(_folder, "obj", $"compiled.{framework}.txt"));

    private static int Index(string[] lines, string text)
    {
        var index = Array.FindIndex(lines, line => line.Contains(text, StringComparison.Ordinal));
        index.ShouldBeGreaterThanOrEqualTo(0, $"No line of the log holds {text}.");
        return index;
    }

    private static List<int> IndexesOf(string[] lines, string text) =>
        [
            .. lines
                .Select((line, index) => (line, index))
                .Where(pair => pair.line.Contains(text, StringComparison.Ordinal))
                .Select(pair => pair.index),
        ];
}
