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
// tool runs once, in the first framework that uses SqlSource, on the manifest of every framework that does, however
// the project is built.  Each test restores and builds the copy one to three times, a few seconds each.
public sealed class OnlineMultiTargetTests : IDisposable
{
    private static readonly Dictionary<string, string> NoVariables = [];

    private const string ManifestSuffix = ".SqlSource.manifest";

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

    // The files the run writes under the first framework's obj folder are that framework's build's, and the other
    // framework's manifest its: IncrementalClean keeps them from one build to the next, so the next build runs nothing,
    // and a clean deletes them.  The clean is offline, so that it runs no tool of its own.
    [Fact]
    public async Task Build_Twice_RunsTheToolOnceAndACleanDeletesWhatTheRunWrote()
    {
        await BuildOnlineAsync();
        await BuildOnlineAsync();

        RanOnceForTheFirstFramework();
        var written = RunFiles();
        written.Count.ShouldBe(5);
        written.Count(file => Path.GetFileName(Path.GetDirectoryName(file)) == "net8.0").ShouldBe(4);
        written
            .Where(file => Path.GetFileName(Path.GetDirectoryName(file)) == "net10.0")
            .ShouldHaveSingleItem()
            .ShouldEndWith(ManifestSuffix);

        await CleanAsync();

        RunFiles().ShouldBeEmpty();
    }

    // The one run is given the manifest of each framework, the run's own first, so that it plans for both.
    [Fact]
    public async Task Build_EveryFramework_GivesTheToolEachFrameworksManifest()
    {
        await BuildOnlineAsync("-p:OnlineMultiSecondFile=true");

        RanOnceForTheFirstFramework();
        var run = StandIn.RunsIn(_folder).Single();
        run.Arguments.Count(argument => argument == "--manifest").ShouldBe(2);
        var second = run.Manifests[1];
        second.ShouldContain("TargetFramework=net10.0");
        second.ShouldContain("Project=" + _project);
        second.ShouldContain("File=" + Path.Combine(_folder, "Second", "Second.sql"));
        run.Manifests[0].ShouldNotContain(line => line.Contains("Second", StringComparison.Ordinal));
    }

    // A sidecar the run created for a .sql file that the second framework alone lists goes to that framework's
    // compiler, and to no other: the first framework's build does not list the file.  A build of every framework
    // evaluates its inner builds after the run, so its patterns list the sidecar.
    [Fact]
    public async Task Build_EveryFrameworkWithAFileOfTheSecondAlone_GivesItsCreatedSidecarToThatCompilerAlone()
    {
        StandIn.Tell(_folder, create: new() { ["Queries/Users.sql.json"] = "{}", ["Second/Second.sql.json"] = "{}" });
        var second = Path.Combine(_folder, "Second", "Second.sql.json");

        await BuildOnlineAsync("-p:OnlineMultiSecondFile=true");

        RanOnceForTheFirstFramework();
        CompiledItems("net10.0").Count(item => item == second).ShouldBe(1);
        CompiledItems("net8.0").ShouldNotContain(second);
    }

    // A build of the second framework alone is evaluated before the run, which runs in the first framework's
    // configuration of its own, and is given the sidecar of a file it alone lists from what the run created.
    [Fact]
    public async Task Build_TheSecondFrameworkAloneWithAFileOfItsOwn_GivesItTheSidecarTheRunCreated()
    {
        StandIn.Tell(_folder, create: new() { ["Queries/Users.sql.json"] = "{}", ["Second/Second.sql.json"] = "{}" });
        var second = Path.Combine(_folder, "Second", "Second.sql.json");

        await BuildOnlineAsync("-p:OnlineMultiSecondFile=true", "-f", "net10.0");

        RanOnceForTheFirstFramework();
        CompiledItems("net10.0").Count(item => item == second).ShouldBe(1);
    }

    // A project whose first framework has no SqlSource, as a PackageReference under a condition on the framework
    // gives, runs the tool once, in the first framework that has it, on that framework's manifest alone, whether
    // every framework is built or that one alone.
    [Theory]
    [InlineData]
    [InlineData("-f", "net10.0")]
    public async Task Build_SqlSourceForTheSecondFrameworkAlone_RunsTheToolOnceForIt(params string[] arguments)
    {
        await BuildOnlineAsync(["-p:OnlineMultiSecondOnly=true", .. arguments]);

        var run = StandIn.RunsIn(_folder).ShouldHaveSingleItem();
        run.Manifests.ShouldHaveSingleItem().ShouldContain("TargetFramework=net10.0");
        run.Arguments[2].ShouldContain("net10.0");
        CompiledItems("net10.0").ShouldContain(Sidecar);
    }

    // A build of the framework without SqlSource alone has none of its targets, and runs nothing of it.
    [Fact]
    public async Task Build_TheFrameworkWithoutSqlSourceAlone_RunsNothing()
    {
        await BuildOnlineAsync("-p:OnlineMultiSecondOnly=true", "-f", "net8.0");

        StandIn.RunsIn(_folder).ShouldBeEmpty();
    }

    // A TargetFrameworks that holds the run's framework alone, however it is written, gives the run no other
    // framework to ask: MSBuild would run the call batched over an empty list once, with the framework empty, and
    // call the project's outer build.  The SDK refuses a framework listed twice, and NuGet a list with none.
    [Theory]
    [InlineData("net10.0")]
    [InlineData("net10.0;")]
    [InlineData(" net10.0 ")]
    public async Task Build_OneFrameworkInTargetFrameworks_AsksNoOtherFrameworkForItsManifest(string frameworks)
    {
        var (exitCode, output) = await FixtureProjects.BuildAsync(
            _project,
            NoVariables,
            StandIn.CommandSwitch,
            "-p:SqlSourceOnline=true",
            FixtureProjects.PropertySwitch("OnlineMultiFrameworks", frameworks),
            "-v:d"
        );

        exitCode.ShouldBe(0, output);
        output.ShouldNotContain("(SqlSourceFrameworkOnlineInputs target(s))");
        var run = StandIn.RunsIn(_folder).ShouldHaveSingleItem();
        run.Manifests.ShouldHaveSingleItem().ShouldContain("TargetFramework=net10.0");
        CompiledItems("net10.0").ShouldContain(Sidecar);
    }

    // A C# file of the second framework alone is an input of the run, which runs again when it changes, since a
    // changed claim in it can need another sidecar.
    [Fact]
    public async Task Build_ChangedFileOfTheSecondFrameworkAlone_RunsTheToolAgain()
    {
        await BuildOnlineAsync("-p:OnlineMultiSecondFile=true");
        var file = Path.Combine(_folder, "Second", "SecondRepository.cs");
        await File.AppendAllTextAsync(file, "\n// Changed.\n", TestContext.Current.CancellationToken);

        await BuildOnlineAsync("-p:OnlineMultiSecondFile=true");

        var runs = StandIn.RunsIn(_folder);
        runs.Count.ShouldBe(2);
        runs.ShouldAllBe(run => run.Manifests.Count == 2);
    }

    // Each manifest's path reaches the tool whole, from a folder whose name the shell reads a $ of and MSBuild a % of,
    // and a project whose name the shell reads a $( and a ` of, which the name of each manifest holds.  A build of the
    // second framework alone is evaluated before the run, so it is given the sidecar of a file that it alone lists
    // from what the run created, by the full path the other framework's configuration gave.
    [Fact]
    public async Task Build_ProjectFolderWithShellCharacters_GivesTheToolEveryManifest()
    {
        const string OddName = "p $(q)`r`.csproj";
        var copied = _fixtures.Copy("OnlineMulti", folder: "a $b %c");
        var folder = Path.GetDirectoryName(copied)!;
        var project = Path.Combine(folder, OddName);
        File.Move(copied, project);
        StandIn.Tell(folder, create: new() { ["Queries/Users.sql.json"] = "{}", ["Second/Second.sql.json"] = "{}" });

        var (exitCode, output) = await FixtureProjects.BuildAsync(
            project,
            NoVariables,
            StandIn.CommandSwitch,
            "-p:SqlSourceOnline=true",
            "-p:OnlineMultiSecondFile=true",
            "-f",
            "net10.0"
        );

        exitCode.ShouldBe(0, output);
        var run = StandIn.RunsIn(folder).ShouldHaveSingleItem();
        run.Manifests.Count.ShouldBe(2);
        run.Manifests[0].ShouldContain("TargetFramework=net8.0");
        run.Manifests[1].ShouldContain("TargetFramework=net10.0");
        run.Manifests.ShouldAllBe(lines => lines.Contains("Project=" + project));
        run.Arguments.Where((_, index) => index > 0 && run.Arguments[index - 1] == "--manifest")
            .Select(Path.GetFileName)
            .ShouldBe([OddName + ManifestSuffix, OddName + ManifestSuffix]);
        var compiled = await File.ReadAllLinesAsync(
            Path.Combine(folder, "obj", "compiled.net10.0.txt"),
            TestContext.Current.CancellationToken
        );
        compiled.Count(item => item == Path.Combine(folder, "Queries", "Users.sql.json")).ShouldBe(1);
        compiled.Count(item => item == Path.Combine(folder, "Second", "Second.sql.json")).ShouldBe(1);
    }

    // Every framework's build lists every manifest the run wrote among the files it wrote, so that a clean of the
    // project deletes each framework's from its obj folder.
    [Fact]
    public async Task Clean_DeletesEveryFrameworksManifest()
    {
        await BuildOnlineAsync();
        string[] manifests =
        [
            Path.Combine(_folder, "obj", "Debug", "net8.0", "App.csproj" + ManifestSuffix),
            Path.Combine(_folder, "obj", "Debug", "net10.0", "App.csproj" + ManifestSuffix),
        ];
        manifests.ShouldAllBe(manifest => File.Exists(manifest));

        await CleanAsync();

        manifests.ShouldAllBe(manifest => !File.Exists(manifest));
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

    // A manifest's name given to the build is the run's own manifest, the first the tool is given, and the other
    // framework writes its own under its obj folder, so that the tool reads each framework's.
    [Fact]
    public async Task Build_GivenAManifestFile_GivesTheToolItAndTheOtherFrameworksOwn()
    {
        var given = Path.Combine(_folder, "obj", "given.manifest");

        await BuildOnlineAsync("-p:SqlSourceManifestFile=obj/given.manifest");

        var run = StandIn.RunsIn(_folder).ShouldHaveSingleItem();
        run.Manifests.Count.ShouldBe(2);
        run.Manifests[0].ShouldContain("TargetFramework=net8.0");
        run.Manifests[1].ShouldContain("TargetFramework=net10.0");
        Path.GetFullPath(run.Arguments[2], _folder).ShouldBe(given);
        Path.GetFullPath(run.Arguments[4], _folder)
            .ShouldBe(Path.Combine(_folder, "obj", "Debug", "net10.0", "App.csproj" + ManifestSuffix));
        (await File.ReadAllLinesAsync(given, TestContext.Current.CancellationToken)).ShouldContain(
            "TargetFramework=net8.0"
        );
    }

    // Each framework's build asks each framework whether it has SqlSource, and calls the run's configuration for the
    // run and for the sidecars it created, when the build goes online, and an offline build calls none of them, which
    // would cost it an evaluation of the project.
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
        output.Contains("(SqlSourceOnlineFramework target(s))", StringComparison.Ordinal).ShouldBe(online);
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

    // One run, on the manifest of the net8.0 build first.
    private void RanOnceForTheFirstFramework()
    {
        var run = StandIn.RunsIn(_folder).ShouldHaveSingleItem();
        run.Manifests.Count.ShouldBeGreaterThan(0);
        run.Manifests[0].ShouldContain("TargetFramework=net8.0");
        run.Arguments[2].ShouldContain("net8.0");
    }

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
