using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Generation.Chains;
using SqlSource.Generation.Paths;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Projects;

// The target SqlSourceWriteManifest of build/SqlSource.targets, run by name with the real "dotnet msbuild" on the
// projects of Fixtures/Projects.  Each test starts MSBuild once or twice, which takes a second or two.
public sealed class ManifestTargetTests : IDisposable
{
    // The line of the folders the build writes into, which is no setting and holds paths of the machine.
    private const string BuildFoldersLine = "SqlSourceBuildFolders=";

    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    private static string[] Constants(string[] lines) =>
        lines
            .Single(line => line.StartsWith("DefineConstants=", StringComparison.Ordinal))["DefineConstants=".Length..]
            .Split(';');

    // The folders of the build that the manifest holds, decoded as the generator decodes them.
    private async Task<string[]> BuildFoldersOfAsync(string project)
    {
        var read = ManifestReader.Read(string.Join('\n', await _fixtures.WriteManifestAsync(project)));
        read.Succeeded.ShouldBeTrue(read.Failure);
        return [.. BuildFolders.Of(read.Value.ShouldNotBeNull().BuildFolders).Folders];
    }

    [Fact]
    public async Task WriteManifest_ProjectWithOneFramework_HoldsWhatTheCompilerIsGiven()
    {
        var project = _fixtures.Copy("Single");
        var folder = Path.GetDirectoryName(project)!;

        var lines = await _fixtures.WriteManifestAsync(project);

        lines
            .Where(line =>
                !line.StartsWith("DefineConstants=", StringComparison.Ordinal)
                && !line.StartsWith(BuildFoldersLine, StringComparison.Ordinal)
            )
            .ShouldBe([
                "SqlSourceManifest=2",
                "Project=" + project,
                "TargetFramework=net10.0",
                "LangVersion=14.0",
                "File=" + Path.Combine(folder, "Queries", "Users.sql"),
                "Settings=" + Path.Combine(folder, "sqlsource.json"),
                "Compile=" + Path.Combine(folder, "UserRepository.cs"),
            ]);
        lines[4].ShouldStartWith("DefineConstants=");
        // Its value is the machine's: WriteManifest_ProjectWithOneFramework_HoldsTheBuildsFolders reads it.
        lines[5].ShouldStartWith(BuildFoldersLine);
        lines.Count(line => line.StartsWith(BuildFoldersLine, StringComparison.Ordinal)).ShouldBe(1);
        // The project's settings are in its sqlsource.json and its file's header, and no line holds a setting.
        lines.ShouldAllBe(line =>
            !line.StartsWith("Property.", StringComparison.Ordinal)
            && !line.StartsWith("File.", StringComparison.Ordinal)
        );
    }

    // The SDK adds the constants of the framework only on the way to a compile, so the manifest target depends on
    // that step, AddImplicitDefineConstants.  Without them the tool would not find an attribute under
    // "#if NET8_0_OR_GREATER".
    [Fact]
    public async Task WriteManifest_ProjectWithOneFramework_HasTheConstantsOfItsFramework()
    {
        var constants = Constants(await _fixtures.WriteManifestAsync(_fixtures.Copy("Single")));

        // Not DEBUG: MSBuild reads the environment for its properties, and a machine that has a variable named
        // Configuration, as the workflow of this repository has, evaluates the project in that configuration.
        constants.ShouldContain("TRACE");
        constants.ShouldContain("NET10_0");
        constants.ShouldContain("NET8_0_OR_GREATER");
        constants.ShouldAllBe(constant => constant.Length > 0);
    }

    // A line break inside a value would end its line of the manifest, and the next line would have no key.
    [Fact]
    public async Task WriteManifest_ConstantsWrittenOverSeveralLines_AreOnOneLine()
    {
        var lines = await _fixtures.WriteManifestAsync(_fixtures.Copy("Constants"));

        lines.ShouldAllBe(line => line.Contains('='));
        var constants = lines.Single(line => line.StartsWith("DefineConstants=", StringComparison.Ordinal));
        constants.ShouldContain("FEATURE_A");
        constants.ShouldContain("FEATURE_B FEATURE_C");
        constants.ShouldContain("NET10_0");
    }

    [Fact]
    public async Task WriteManifest_NoFileNamed_WritesOneUnderObj()
    {
        var project = _fixtures.Copy("Single");
        var obj = Path.Combine(Path.GetDirectoryName(project)!, "obj");

        var (exitCode, output) = await FixtureProjects.RunMSBuildAsync(project, ["-t:SqlSourceWriteManifest"]);

        exitCode.ShouldBe(0, output);
        // In the folder of the configuration and the framework, whichever configuration the machine evaluates in.
        var file = Directory
            .GetFiles(obj, "App.csproj.SqlSource.manifest", SearchOption.AllDirectories)
            .ShouldHaveSingleItem();
        Path.GetFileName(Path.GetDirectoryName(file)).ShouldBe("net10.0");
        (await File.ReadAllLinesAsync(file, TestContext.Current.CancellationToken))[0].ShouldBe("SqlSourceManifest=2");
    }

    [Fact]
    public async Task WriteManifest_FileNamed_WritesNothingIntoTheProject()
    {
        var project = _fixtures.Copy("Single");

        _ = await _fixtures.WriteManifestAsync(project);

        Directory.Exists(Path.Combine(Path.GetDirectoryName(project)!, "obj")).ShouldBeFalse();
        Directory.Exists(Path.Combine(Path.GetDirectoryName(project)!, "bin")).ShouldBeFalse();
    }

    // A project with several frameworks has no target of the package until it is given one of them.
    [Fact]
    public async Task WriteManifest_SeveralFrameworksAndNoneGiven_Fails()
    {
        var (exitCode, output) = await FixtureProjects.RunMSBuildAsync(
            _fixtures.Copy("Multi"),
            ["-t:SqlSourceWriteManifest"]
        );

        exitCode.ShouldNotBe(0);
        output.ShouldContain("MSB4057");
    }

    // TD-0026: the manifest is the first framework's.  What the project lists for another alone is not in it.
    [Fact]
    public async Task WriteManifest_SeveralFrameworksAndTheFirstGiven_IsTheManifestOfThatFramework()
    {
        var project = _fixtures.Copy("Multi");
        var folder = Path.GetDirectoryName(project)!;

        var lines = await _fixtures.WriteManifestAsync(project, "TargetFramework=net8.0");

        lines.ShouldContain("TargetFramework=net8.0");
        Constants(lines).ShouldContain("NET8_0");
        Constants(lines).ShouldNotContain("NET10_0");
        lines
            .Where(line => line.StartsWith("File=", StringComparison.Ordinal))
            .ShouldBe(["File=" + Path.Combine(folder, "Queries", "Users.sql")]);
        lines
            .Where(line => line.StartsWith("Compile=", StringComparison.Ordinal))
            .ShouldBe(["Compile=" + Path.Combine(folder, "Queries.cs")]);
    }

    // The fixture's target hooks SqlSourceListFiles, which the manifest's target depends on first.
    [Fact]
    public async Task WriteManifest_FileAddedByATargetThatHooksSqlSourceListFiles_IsListed()
    {
        var project = _fixtures.Copy("LateFile");
        var folder = Path.GetDirectoryName(project)!;

        var lines = await _fixtures.WriteManifestAsync(project);

        lines.ShouldContain("File=" + Path.Combine(folder, "Generated", "Late.sql"));
        lines.ShouldContain("File=" + Path.Combine(folder, "Queries", "Users.sql"));
    }

    // TD-0025: the target runs no hook of a build.
    [Fact]
    public async Task WriteManifest_FileAddedByATargetThatHooksTheBuild_IsNotListed()
    {
        var project = _fixtures.Copy("BuildHookFile");
        var folder = Path.GetDirectoryName(project)!;

        var lines = await _fixtures.WriteManifestAsync(project);

        lines
            .Where(line => line.StartsWith("File=", StringComparison.Ordinal))
            .ShouldBe(["File=" + Path.Combine(folder, "Queries", "Users.sql")]);
    }

    [Fact]
    public async Task WriteManifest_ProjectThatListsItsOwnFiles_HoldsTheFilesItLists()
    {
        var project = _fixtures.Copy("OwnFiles");
        var folder = Path.GetDirectoryName(project)!;

        var lines = await _fixtures.WriteManifestAsync(project);

        lines
            .Where(line => line.StartsWith("File=", StringComparison.Ordinal))
            .ShouldBe([
                "File=" + Path.Combine(folder, "Queries", "One.sql"),
                "File=" + Path.Combine(folder, "Queries", "Two.sql"),
            ]);
    }

    // Every sqlsource.json above a .sql file or a Compile item, to the top of the drive, and none in a folder that
    // holds neither: above the project, in its folder and below, beside a .sql file linked from outside the folder,
    // beside a C# file, and beside a .sql file that a target adds from the hook the package names.  The project
    // lists the one above it itself, by a relative path, and it is listed once.  The lines come after the File lines
    // and before the Compile lines.
    [Fact]
    public async Task WriteManifest_SettingsFilesAboveTheFiles_AreListedBetweenTheSqlFilesAndTheCompileFiles()
    {
        var project = _fixtures.Copy("SettingsFiles", "App/App.csproj");
        var fixture = Path.GetDirectoryName(Path.GetDirectoryName(project))!;

        var lines = await _fixtures.WriteManifestAsync(project);

        lines
            .Where(line => line.StartsWith("Settings=", StringComparison.Ordinal))
            .ShouldBe(
                [
                    .. SettingsFileSearchTests.Found.Select(file =>
                        "Settings=" + Path.Combine(fixture, file.Replace('/', Path.DirectorySeparatorChar))
                    ),
                ],
                ignoreOrder: true
            );
        var settings = Array.FindIndex(lines, line => line.StartsWith("Settings=", StringComparison.Ordinal));
        settings.ShouldBe(Array.FindLastIndex(lines, line => line.StartsWith("File=", StringComparison.Ordinal)) + 1);
        Array
            .FindLastIndex(lines, line => line.StartsWith("Settings=", StringComparison.Ordinal))
            .ShouldBe(Array.FindIndex(lines, line => line.StartsWith("Compile=", StringComparison.Ordinal)) - 1);
    }

    // MSBuild splits a list at a semicolon and reads "%41" as "A".  A path is none of its lists.
    [Fact]
    public async Task WriteManifest_PathsWithCharactersThatMSBuildReads_AreListedWhole()
    {
        const string Odd = "q;=%41 'é";
        var project = _fixtures.Copy("OddPaths");
        var folder = Path.Combine(Path.GetDirectoryName(project)!, Odd);

        var lines = await _fixtures.WriteManifestAsync(project);

        lines.ShouldContain("File=" + Path.Combine(folder, "a;b=c%41 'é.sql"));
        lines.ShouldContain("Settings=" + Path.Combine(folder, "sqlsource.json"));
        lines.ShouldContain("Compile=" + Path.Combine(folder, "a;b=c%41 'é.cs"));
    }

    // The tool reads the folders of the build from the manifest as the generator reads them from the compiler's
    // options: the line holds the obj and bin folders of the project.
    [Fact]
    public async Task WriteManifest_ProjectWithOneFramework_HoldsTheBuildsFolders()
    {
        var project = _fixtures.Copy("Single");
        var folder = Path.GetDirectoryName(project)!;

        var folders = await BuildFoldersOfAsync(project);

        folders.ShouldContain(SqlPath.Normalize(Path.Combine(folder, "obj") + Path.DirectorySeparatorChar));
        folders.ShouldContain(SqlPath.Normalize(Path.Combine(folder, "bin") + Path.DirectorySeparatorChar));
    }

    // The folder of the project holds a semicolon, "%41" that MSBuild would read as "A", an equals sign, a quote and
    // an accented letter.  The target escapes the folder's %, |, # and ;, and the manifest line then writes the
    // property through Escape, which WriteLinesToFile undoes, so the line holds the target's escapes and BuildFolders
    // decodes them.
    [Fact]
    public async Task WriteManifest_BuildFoldersOfAnOddPath_DecodeToTheRealFolders()
    {
        var project = _fixtures.Copy("OddPaths", folder: "q;=%41 'é");
        var folder = Path.GetDirectoryName(project)!;

        var folders = await BuildFoldersOfAsync(project);

        folders.ShouldContain(SqlPath.Normalize(Path.Combine(folder, "obj") + Path.DirectorySeparatorChar));
        folders.ShouldContain(SqlPath.Normalize(Path.Combine(folder, "bin") + Path.DirectorySeparatorChar));
    }
}
