using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Projects;

// The search of build/SqlSource.targets for settings files, in a design-time build of the fixture SettingsFiles: the
// build an IDE runs to learn what the compiler is given, which the generator then runs in.  The properties are
// those Roslyn's language server and MSBuildWorkspace set; Visual Studio's CompileDesignTime calls the same Compile
// with SkipCompilerExecution and ProvideCommandLineArgs.  The copy is restored first, outside the repository.
public sealed class SettingsFileSearchTests : IDisposable
{
    // The settings files the search finds in the fixture before the compiler, relative to its folder, and those the
    // manifest lists.  Unused/sqlsource.json is in a folder that holds no .sql or C# file, and has none below it.
    internal static readonly string[] Found =
    [
        "sqlsource.json",
        "Shared/sqlsource.json",
        "App/sqlsource.json",
        "App/Queries/sqlsource.json",
        "App/Generated/sqlsource.json",
        "App/Code/sqlsource.json",
    ];

    // Beside the .sql file that a target of Directory.Build.targets adds from a hook of CoreCompile.
    private const string BesideTheFileAddedBeforeCompile = "App/Compiled/sqlsource.json";

    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    // The search runs from SqlSource.AfterSdk.targets, which the SDK imports after Directory.Build.targets, so it runs
    // after the target there that hooks CoreCompile, and finds the settings file beside the file that target adds.
    [Fact]
    public async Task Compile_DesignTimeBuild_GivesTheCompilerEverySettingsFileOnce()
    {
        var (fixture, files) = await SettingsFilesGivenAsync();

        files.ShouldBe(InFixture(fixture, [.. Found, BesideTheFileAddedBeforeCompile]), ignoreOrder: true);
    }

    // A project that sets AfterMicrosoftNETSdkTargets without keeping its value, as a global property does here,
    // loses the file that runs the search last.  The search then runs before GenerateMSBuildEditorConfigFileCore, and
    // finds every settings file but the one beside a file that a later target adds.
    [Fact]
    public async Task Compile_DesignTimeBuildWithoutTheTargetsImportedLast_SearchesBeforeTheFileTheCompilerReads()
    {
        var (fixture, files) = await SettingsFilesGivenAsync("-p:AfterMicrosoftNETSdkTargets=");

        files.ShouldBe(InFixture(fixture, Found), ignoreOrder: true);
    }

    // A build of a project named by a path through a symbolic link to the folder above it, as /tmp is a link to
    // /private/tmp on macOS, applies the settings file above the project, which sets its dialect, and the one beside
    // it, which sets its output.  The compiler makes a path of the project's own full against its working folder,
    // where the link is resolved, so a settings file given by its full path through the link reached no .sql file,
    // and every query was SQLSRC209 for the default output and the dialect ansi.  The query has no sidecar, so the
    // build stops at it with SQLSRC241, which only the dialect and the output of the two files give.
    [Fact]
    public async Task Build_ProjectNamedThroughALink_AppliesItsSettingsFiles()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), LinkSkipReason);
        _ = _fixtures.Copy("Linked", "App/App.csproj");
        _ = Directory.CreateSymbolicLink(_fixtures.PathOf("Link"), _fixtures.PathOf("Linked"));

        var (exitCode, output) = await BuildLinkedAsync(_fixtures.PathOf("Link/App/App.csproj"));

        exitCode.ShouldBe(1, output);
        ErrorLines(output)
            .ShouldHaveSingleItem(output)
            .ShouldContain("Widgets.sql(1,10): error SQLSRC241:", Case.Sensitive, output);
    }

    // A settings file outside the deepest link above the project, which the search finds through the link, is given
    // by its full path: a path relative to the project would leave the link the other way, from the folder it leads
    // to, and name a file that is not there, which the compiler fails the build on with CS2001.  So is one beside a C#
    // file listed by its full path in a folder whose name starts with the link's, LinkShared beside Link, which only
    // a test of the folder's whole name tells apart.  The settings files inside the link are given by relative paths.
    [Fact]
    public async Task Compile_DesignTimeBuildThroughALink_GivesTheFilesOutsideTheLinkByTheirFullPaths()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), LinkSkipReason);
        _ = _fixtures.Copy("Linked", "App/App.csproj");
        _ = _fixtures.CopyBuildFolder("Deep/build");
        _ = _fixtures.WriteFile(
            "Deep/sqlsource.json",
            /*lang=json,strict*/
            """{ "dialect": "mysql" }"""
        );
        _ = _fixtures.WriteFile("Deep/LinkShared/sqlsource.json", "{}");
        var shared = _fixtures.WriteFile("Deep/LinkShared/Shared.cs", "namespace Linked;");
        _ = Directory.CreateSymbolicLink(_fixtures.PathOf("Deep/Link"), _fixtures.PathOf("Linked"));

        var files = await SettingsFilesAsGivenAsync(
            _fixtures.PathOf("Deep/Link/App/App.csproj"),
            FixtureProjects.PropertySwitch("LinkedCompile", shared)
        );

        files.ShouldBe(
            [
                "sqlsource.json",
                "../sqlsource.json",
                _fixtures.PathOf("Deep/sqlsource.json"),
                _fixtures.PathOf("Deep/LinkShared/sqlsource.json"),
            ],
            ignoreOrder: true
        );
    }

    // A known limit, which the owner accepted: a file the project lists by its full path through the link keeps the
    // link's spelling in the compiler, while a settings file inside the link, given by its relative path, is made full
    // where the link is resolved, so the two never meet.  Both of Linked's settings files are inside the link, so the
    // query gets no settings, SQLSRC209 for the default output and the dialect ansi.  Before the settings files were
    // given by relative paths, such a file got its settings and every file listed by a relative path lost them.
    [Fact]
    public async Task Build_FilesListedByFullPathsThroughALink_GetNoSettingsFileInsideTheLink()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), LinkSkipReason);
        _ = _fixtures.Copy("Linked", "App/App.csproj");
        _ = Directory.CreateSymbolicLink(_fixtures.PathOf("Link"), _fixtures.PathOf("Linked"));

        var (exitCode, output) = await BuildLinkedAsync(
            _fixtures.PathOf("Link/App/App.csproj"),
            "-p:LinkedFullPaths=true"
        );

        exitCode.ShouldBe(1, output);
        ErrorLines(output)
            .ShouldHaveSingleItem(output)
            .ShouldContain(
                _fixtures.PathOf("Link/App/Widgets.sql") + "(1,10): error SQLSRC209:",
                Case.Sensitive,
                output
            );
    }

    // A link needs a privilege on Windows, whose working folder keeps the spelling of a link anyway.
    private const string LinkSkipReason = "Making a symbolic link needs a privilege on Windows.";

    // "dotnet build" of the copy, with the generator beside the tests.
    private static Task<(int ExitCode, string Output)> BuildLinkedAsync(string project, params string[] arguments) =>
        FixtureProjects.BuildAsync(
            project,
            new Dictionary<string, string>(),
            [
                FixtureProjects.PropertySwitch(
                    "LinkedGenerator",
                    Path.Combine(AppContext.BaseDirectory, "SqlSource.dll")
                ),
                .. arguments,
            ]
        );

    // The errors of a build, each once: the console logger repeats them in its summary.
    private static string[] ErrorLines(string output) =>
        [
            .. output
                .Split('\n')
                .Select(line => line.TrimEnd('\r').Trim())
                .Where(line => line.Contains(": error ", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal),
        ];

    private static IEnumerable<string> InFixture(string fixture, IEnumerable<string> files) =>
        files.Select(file => Path.Combine(fixture, file.Replace('/', Path.DirectorySeparatorChar)));

    // The full paths of the settings files a design-time build gives the compiler, each as often as it is given.
    private async Task<(string Fixture, List<string> Files)> SettingsFilesGivenAsync(params string[] properties)
    {
        var project = _fixtures.Copy("SettingsFiles", "App/App.csproj");
        var folder = Path.GetDirectoryName(project)!;
        var files = await SettingsFilesAsGivenAsync(project, properties);
        return (Path.GetDirectoryName(folder)!, [.. files.Select(file => Path.GetFullPath(file, folder))]);
    }

    // The paths of the settings files a design-time build of a project gives the compiler, as it gives them, each as
    // often as it is given.
    private static async Task<List<string>> SettingsFilesAsGivenAsync(string project, params string[] properties)
    {
        var (exitCode, output) = await FixtureProjects.RunMSBuildAsync(
            project,
            [
                "-restore",
                "-t:Compile",
                "-p:DesignTimeBuild=true",
                "-p:BuildingProject=false",
                "-p:SkipCompilerExecution=true",
                "-p:ProvideCommandLineArgs=true",
                .. properties,
                "-getItem:CscCommandLineArgs",
            ]
        );

        exitCode.ShouldBe(0, output);
        const string Switch = "/additionalfile:";
        return
        [
            .. JsonDocument
                .Parse(output[output.IndexOf('{', StringComparison.Ordinal)..])
                .RootElement.GetProperty("Items")
                .GetProperty("CscCommandLineArgs")
                .EnumerateArray()
                .Select(item => item.GetProperty("Identity").GetString()!)
                .Where(argument => argument.StartsWith(Switch, StringComparison.Ordinal))
                .Select(argument => argument[Switch.Length..])
                .Where(path => Path.GetFileName(path) == "sqlsource.json"),
        ];
    }
}
