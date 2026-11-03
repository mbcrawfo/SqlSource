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

    private static readonly string[] RunFileSuffixes = [".manifest", ".stamp", ".sidecars", ".online"];

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
        CompiledItems("net8.0").Count(item => item == Sidecar).ShouldBe(1);
        CompiledItems("net10.0").Count(item => item == Sidecar).ShouldBe(1);
        var lines = await File.ReadAllLinesAsync(log, TestContext.Current.CancellationToken);
        Index(lines, "Done building target \"SqlSourceRunOnlineBeforeDispatch\"")
            .ShouldBeLessThan(Index(lines, "Target \"DispatchToInnerBuilds:"));
        IndexesOf(lines, "Target \"OnlineMultiMeet:").Count.ShouldBe(2);
        IndexesOf(lines, "Target \"OnlineMultiMeet:")
            .Max()
            .ShouldBeLessThan(IndexesOf(lines, "Done building target \"OnlineMultiMeet\"").Min());
    }

    // The run of the tool runs no target of a framework's own build early: each framework searches for settings files
    // after its hooks of CoreCompile, so a settings file that only a .sql file such a hook adds reaches is given to
    // both compilers, as in a build that is not online.
    [Fact]
    public async Task Build_AHookAddsASqlFile_GivesEveryFrameworkTheSettingsFileThatReachesIt()
    {
        _ = _fixtures.WriteFile("Late/Late.sql", "-- name: GetLate\nSELECT 1;\n");
        var settings = _fixtures.WriteFile("Late/sqlsource.json", "{}\n");

        var (exitCode, output) = await FixtureProjects.BuildAsync(
            _project,
            NoVariables,
            StandIn.CommandSwitch,
            "-p:SqlSourceOnline=true",
            "-p:OnlineMultiLate=true"
        );

        exitCode.ShouldBe(0, output);
        RanOnceForTheFirstFramework();
        CompiledItems("net8.0").ShouldContain(settings);
        CompiledItems("net10.0").ShouldContain(settings);
    }

    // The files the run writes under the first framework's obj folder are that framework's build's: IncrementalClean
    // keeps them from one build to the next, so the next build runs nothing, and a clean deletes them.  The clean is
    // offline, so that it runs no tool of its own.
    [Fact]
    public async Task Build_Twice_RunsTheToolOnceAndACleanDeletesWhatTheRunWrote()
    {
        await BuildOnlineAsync();
        await BuildOnlineAsync();

        RanOnceForTheFirstFramework();
        var written = RunFiles();
        written.Count.ShouldBe(4);
        written.ShouldAllBe(file => Path.GetFileName(Path.GetDirectoryName(file)) == "net8.0");

        await CleanAsync();

        RunFiles().ShouldBeEmpty();
    }

    // A manifest the build is given by name is not the build's own, so a clean leaves it, as it does in a build that
    // runs the tool itself.  It is in the first framework's obj folder, which a first build finds, and its path is
    // relative to the project, as the build's own are: the temporary folder of macOS is under a link, and MSBuild
    // compares a full path given with the obj folder as the link resolves.
    [Fact]
    public async Task Build_GivenAManifestFile_LeavesItToAClean()
    {
        await BuildOnlineAsync();
        var folder = Path.GetDirectoryName(
            RunFiles().Single(file => file.EndsWith(".stamp", StringComparison.Ordinal))
        )!;
        var manifest = Path.Combine(folder, "given.manifest");

        await BuildOnlineAsync(
            "-p:SqlSourceManifestFile=" + Path.GetRelativePath(_folder, manifest).Replace('\\', '/')
        );
        File.Exists(manifest).ShouldBeTrue();
        await CleanAsync();

        File.Exists(manifest).ShouldBeTrue();
        RunFiles().ShouldBeEmpty();
    }

    // Each framework's build calls the run's configuration for the run and for the sidecars it created when the
    // build goes online, and an offline build calls neither, which would cost it an evaluation of the project.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Build_OfOneFramework_CallsTheRunsConfigurationOnlyOnline(bool online)
    {
        var (exitCode, output) = await FixtureProjects.BuildAsync(
            _project,
            NoVariables,
            StandIn.CommandSwitch,
            online ? "-p:SqlSourceOnline=true" : "-p:SqlSourceOnline=false",
            "-f",
            "net10.0",
            "-v:d"
        );

        exitCode.ShouldBe(0, output);
        StandIn.RunsIn(_folder).Count.ShouldBe(online ? 1 : 0);
        output.Contains("(SqlSourceRunOnline target(s))", StringComparison.Ordinal).ShouldBe(online);
        output.Contains("(SqlSourceCreatedSidecars target(s))", StringComparison.Ordinal).ShouldBe(online);
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

    // Each way of building the project gives every framework's compiler the sidecar that its run created once, though
    // the project lists no sidecar, and gives none a sidecar that existed before the build and that the project does
    // not list, as an offline build gives none, though the run writes it again.  A full build evaluates its inner
    // builds after the run, a build of one framework before it.
    [Theory]
    [InlineData("every framework", true)]
    [InlineData("every framework", false)]
    [InlineData("-f net10.0", true)]
    [InlineData("-f net10.0", false)]
    [InlineData("a ProjectReference", true)]
    [InlineData("a ProjectReference", false)]
    public async Task Build_ProjectThatListsNoSidecar_GivesTheCompilersOnlyTheSidecarTheRunCreated(
        string way,
        bool created
    )
    {
        if (!created)
        {
            _ = _fixtures.WriteFile("OnlineMulti/Queries/Users.sql.json", "{ }");
        }

        var (project, frameworks, arguments) = way switch
        {
            "every framework" => (_project, new[] { "net8.0", "net10.0" }, Array.Empty<string>()),
            "-f net10.0" => (_project, new[] { "net10.0" }, new[] { "-f", "net10.0" }),
            _ => (_fixtures.Copy("OnlineReference", "Reference.csproj"), new[] { "net10.0" }, Array.Empty<string>()),
        };
        string[] ownItems = ["-p:SqlSourceIncludeFiles=false", "-p:OnlineMultiOwnItems=true", .. arguments];

        var (exitCode, output) = await FixtureProjects.BuildAsync(
            project,
            NoVariables,
            [StandIn.CommandSwitch, "-p:SqlSourceOnline=true", .. ownItems]
        );
        exitCode.ShouldBe(0, output);
        var online = frameworks.Select(CompiledItems).ToList();
        (exitCode, output) = await FixtureProjects.BuildAsync(
            project,
            NoVariables,
            ["-p:SqlSourceOnline=false", .. ownItems]
        );
        exitCode.ShouldBe(0, output);

        RanOnceForTheFirstFramework();
        (await File.ReadAllTextAsync(Sidecar, TestContext.Current.CancellationToken)).ShouldBe(
            "{}",
            "the run wrote the sidecar"
        );
        foreach (var (framework, items) in frameworks.Zip(online))
        {
            items.Count(item => item == Sidecar).ShouldBe(created ? 1 : 0, framework);
            if (!created)
            {
                items.ShouldBe(CompiledItems(framework), framework);
            }
        }
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
    // An online build of every framework with the stand-in, which must pass.
    private async Task BuildOnlineAsync(params string[] arguments)
    {
        var (exitCode, output) = await FixtureProjects.BuildAsync(
            _project,
            NoVariables,
            [StandIn.CommandSwitch, "-p:SqlSourceOnline=true", .. arguments]
        );
        exitCode.ShouldBe(0, output);
    }

    // A clean of every framework, offline, which must pass.
    private async Task CleanAsync()
    {
        var (exitCode, output) = await FixtureProjects.BuildAsync(
            _project,
            NoVariables,
            "-t:Clean",
            "-p:SqlSourceOnline=false"
        );
        exitCode.ShouldBe(0, output);
    }

    // The files the run writes under obj, the build's own manifest, the stamp, the list of sidecars and the value
    // file, wherever the configuration of the build puts them.
    private List<string> RunFiles()
    {
        var obj = Path.Combine(_folder, "obj");
        return
        [
            .. Directory
                .EnumerateFiles(obj, "App.csproj.SqlSource.*", SearchOption.AllDirectories)
                .Where(file => RunFileSuffixes.Any(suffix => file.EndsWith(suffix, StringComparison.Ordinal))),
        ];
    }

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
