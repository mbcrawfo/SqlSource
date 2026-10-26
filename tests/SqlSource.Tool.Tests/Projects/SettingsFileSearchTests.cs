using System;
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
    // The settings files the search finds in the fixture, relative to its folder.  Unused/sqlsource.json is in a
    // folder that holds no .sql or C# file, and has none below it.
    internal static readonly string[] Found =
    [
        "sqlsource.json",
        "Shared/sqlsource.json",
        "App/sqlsource.json",
        "App/Queries/sqlsource.json",
        "App/Generated/sqlsource.json",
        "App/Code/sqlsource.json",
    ];

    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    [Fact]
    public async Task DesignTimeBuild_GivesTheCompilerEverySettingsFileOnce()
    {
        var project = _fixtures.Copy("SettingsFiles", "App/App.csproj");
        var folder = Path.GetDirectoryName(project)!;
        var fixture = Path.GetDirectoryName(folder)!;

        var (exitCode, output) = await FixtureProjects.RunMSBuildAsync(
            project,
            [
                "-restore",
                "-t:Compile",
                "-p:DesignTimeBuild=true",
                "-p:BuildingProject=false",
                "-p:SkipCompilerExecution=true",
                "-p:ProvideCommandLineArgs=true",
                "-getItem:CscCommandLineArgs",
            ]
        );

        exitCode.ShouldBe(0, output);
        const string Switch = "/additionalfile:";
        var arguments = JsonDocument
            .Parse(output[output.IndexOf('{', StringComparison.Ordinal)..])
            .RootElement.GetProperty("Items")
            .GetProperty("CscCommandLineArgs")
            .EnumerateArray()
            .Select(item => item.GetProperty("Identity").GetString()!)
            .ToList();
        arguments
            .Where(argument => argument.StartsWith(Switch, StringComparison.Ordinal))
            .Select(argument => Path.GetFullPath(argument[Switch.Length..], folder))
            .Where(path => Path.GetFileName(path) == "sqlsource.json")
            .ShouldBe(
                Found.Select(file => Path.Combine(fixture, file.Replace('/', Path.DirectorySeparatorChar))),
                ignoreOrder: true
            );
    }
}
