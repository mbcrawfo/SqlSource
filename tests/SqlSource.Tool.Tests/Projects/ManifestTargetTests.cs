using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Projects;

// The target SqlSourceWriteManifest of build/SqlSource.targets, run by name with the real "dotnet msbuild" on the
// projects of Fixtures/Projects.  Each test starts MSBuild once or twice, which takes a second or two.
public sealed class ManifestTargetTests : IDisposable
{
    private static readonly string[] Names =
    [
        "SqlSourceDialect",
        "SqlSourceDatabase",
        "SqlSourceOutput",
        "SqlSourceGeneratorParameters",
        "SqlSourceInputModelSuffix",
        "SqlSourceOutputModelSuffix",
        "SqlSourceModelNamespace",
        "SqlSourceInputModelType",
        "SqlSourceOutputModelType",
        "SqlSourceCollectionType",
        "SqlSourceTypeOverrides",
    ];

    // The settings that are properties of the project and never metadata of a file.
    private static readonly string[] PropertyOnlyNames = ["SqlSourceNestedClass"];

    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    // The lines of one file: its File line and the eleven under it, with the values that are not empty.
    private static string[] FileLines(string path, params (string Name, string Value)[] metadata) =>
        [
            "File=" + path,
            .. Names.Select(name => $"File.{name}=" + metadata.FirstOrDefault(pair => pair.Name == name).Value),
        ];

    private static string[] PropertyLines(params (string Name, string Value)[] properties) =>
        [
            .. Names
                .Concat(PropertyOnlyNames)
                .Select(name => $"Property.{name}=" + properties.FirstOrDefault(pair => pair.Name == name).Value),
        ];

    private static string[] Constants(string[] lines) =>
        lines
            .Single(line => line.StartsWith("DefineConstants=", StringComparison.Ordinal))["DefineConstants=".Length..]
            .Split(';');

    [Fact]
    public async Task WriteManifest_ProjectWithOneFramework_HoldsWhatTheCompilerIsGiven()
    {
        var project = _fixtures.Copy("Single");
        var folder = Path.GetDirectoryName(project)!;

        var lines = await _fixtures.WriteManifestAsync(project);

        lines
            .Where(line => !line.StartsWith("DefineConstants=", StringComparison.Ordinal))
            .ShouldBe([
                "SqlSourceManifest=1",
                "Project=" + project,
                "TargetFramework=net10.0",
                "LangVersion=14.0",
                // Each was written on a line of its own, and is trimmed.
                .. PropertyLines(("SqlSourceDialect", "postgres")),
                .. FileLines(Path.Combine(folder, "Queries", "Users.sql"), ("SqlSourceDatabase", "billing")),
                "Compile=" + Path.Combine(folder, "UserRepository.cs"),
            ]);
        lines[4].ShouldStartWith("DefineConstants=");
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

    // A line break inside a value would end its line of the manifest, and the next line would have no key.  The
    // trims of the package do not reach a property of the SDK.
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
        (await File.ReadAllLinesAsync(file, TestContext.Current.CancellationToken))[0].ShouldBe("SqlSourceManifest=1");
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

    [Fact]
    public async Task WriteManifest_FileAddedByATargetThatHooksTheTrim_IsListedWithTrimmedMetadata()
    {
        var project = _fixtures.Copy("LateFile");
        var folder = Path.GetDirectoryName(project)!;

        var lines = await _fixtures.WriteManifestAsync(project);

        var late = FileLines(Path.Combine(folder, "Generated", "Late.sql"), ("SqlSourceDialect", "mssql"));
        var at = Array.IndexOf(lines, late[0]);
        at.ShouldBeGreaterThan(0);
        lines.Skip(at).Take(late.Length).ShouldBe(late);
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

    // MSBuild splits a list at a semicolon and reads "%41" as "A".  A path is none of its lists.
    [Fact]
    public async Task WriteManifest_PathsWithCharactersThatMSBuildReads_AreListedWhole()
    {
        const string Odd = "q;=%41 'é";
        var project = _fixtures.Copy("OddPaths");
        var folder = Path.Combine(Path.GetDirectoryName(project)!, Odd);

        var lines = await _fixtures.WriteManifestAsync(project);

        lines.ShouldContain("File=" + Path.Combine(folder, "a;b=c%41 'é.sql"));
        lines.ShouldContain("Compile=" + Path.Combine(folder, "a;b=c%41 'é.cs"));
    }

    [Fact]
    public async Task WriteManifest_ValuesThatHoldASemicolon_AreEachOneLine()
    {
        var project = _fixtures.Copy("Single");

        var lines = await _fixtures.WriteManifestAsync(
            project,
            "SqlSourceGeneratorParameters=a%3Bb",
            "SqlSourceModelNamespace=A=B"
        );

        lines.ShouldContain("Property.SqlSourceGeneratorParameters=a;b");
        lines.ShouldContain("Property.SqlSourceModelNamespace=A=B");
    }
}
