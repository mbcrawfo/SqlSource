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

    private static IEnumerable<string> InFixture(string fixture, IEnumerable<string> files) =>
        files.Select(file => Path.Combine(fixture, file.Replace('/', Path.DirectorySeparatorChar)));

    // The full paths of the settings files a design-time build gives the compiler, each as often as it is given.
    private async Task<(string Fixture, List<string> Files)> SettingsFilesGivenAsync(params string[] properties)
    {
        var project = _fixtures.Copy("SettingsFiles", "App/App.csproj");
        var folder = Path.GetDirectoryName(project)!;

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
        var files = JsonDocument
            .Parse(output[output.IndexOf('{', StringComparison.Ordinal)..])
            .RootElement.GetProperty("Items")
            .GetProperty("CscCommandLineArgs")
            .EnumerateArray()
            .Select(item => item.GetProperty("Identity").GetString()!)
            .Where(argument => argument.StartsWith(Switch, StringComparison.Ordinal))
            .Select(argument => Path.GetFullPath(argument[Switch.Length..], folder))
            .Where(path => Path.GetFileName(path) == "sqlsource.json")
            .ToList();
        return (Path.GetDirectoryName(folder)!, files);
    }
}
