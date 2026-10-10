using System;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Tests.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests;

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
    ];

    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    // The lines of one file: its File line and the ten under it, with the values that are not empty.
    private static string[] FileLines(string path, params (string Name, string Value)[] metadata) =>
        [
            "File=" + path,
            .. Names.Select(name => $"File.{name}=" + metadata.FirstOrDefault(pair => pair.Name == name).Value),
        ];

    private static string[] PropertyLines(params (string Name, string Value)[] properties) =>
        [.. Names.Select(name => $"Property.{name}=" + properties.FirstOrDefault(pair => pair.Name == name).Value)];

    private static string[] Constants(string[] lines) =>
        lines
            .Single(line => line.StartsWith("DefineConstants=", StringComparison.Ordinal))["DefineConstants=".Length..]
            .Split(';');

    [Fact]
    public void WriteManifest_ProjectWithOneFramework_HoldsWhatTheCompilerIsGiven()
    {
        var project = _fixtures.Copy("Single");
        var folder = Path.GetDirectoryName(project)!;

        var lines = _fixtures.WriteManifest(project);

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

    // Run by name, a project has TRACE and DEBUG alone: the SDK adds the constants of the framework on the way to a
    // compile.  Without them the tool would not find an attribute under "#if NET8_0_OR_GREATER".
    [Fact]
    public void WriteManifest_ProjectWithOneFramework_HasTheConstantsOfItsFramework()
    {
        var constants = Constants(_fixtures.WriteManifest(_fixtures.Copy("Single")));

        constants.ShouldContain("DEBUG");
        constants.ShouldContain("NET10_0");
        constants.ShouldContain("NET8_0_OR_GREATER");
        constants.ShouldAllBe(constant => constant.Length > 0);
    }

    [Fact]
    public void WriteManifest_NoFileNamed_WritesOneUnderObj()
    {
        var project = _fixtures.Copy("Single");
        var file = Path.Combine(
            Path.GetDirectoryName(project)!,
            "obj",
            "Debug",
            "net10.0",
            "App.csproj.SqlSource.manifest"
        );

        var (exitCode, output) = FixtureProjects.RunMSBuild(project, ["-t:SqlSourceWriteManifest"]);

        exitCode.ShouldBe(0, output);
        File.ReadLines(file).First().ShouldBe("SqlSourceManifest=1");
    }

    [Fact]
    public void WriteManifest_FileNamed_WritesNothingIntoTheProject()
    {
        var project = _fixtures.Copy("Single");

        _ = _fixtures.WriteManifest(project);

        Directory.Exists(Path.Combine(Path.GetDirectoryName(project)!, "obj")).ShouldBeFalse();
        Directory.Exists(Path.Combine(Path.GetDirectoryName(project)!, "bin")).ShouldBeFalse();
    }

    // A project with several frameworks has no target of the package until it is given one of them.
    [Fact]
    public void WriteManifest_SeveralFrameworksAndNoneGiven_Fails()
    {
        var (exitCode, output) = FixtureProjects.RunMSBuild(_fixtures.Copy("Multi"), ["-t:SqlSourceWriteManifest"]);

        exitCode.ShouldNotBe(0);
        output.ShouldContain("MSB4057");
    }

    // TD-0026: the manifest is the first framework's.  What the project lists for another alone is not in it.
    [Fact]
    public void WriteManifest_SeveralFrameworksAndTheFirstGiven_IsTheManifestOfThatFramework()
    {
        var project = _fixtures.Copy("Multi");
        var folder = Path.GetDirectoryName(project)!;

        var lines = _fixtures.WriteManifest(project, "TargetFramework=net8.0");

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
    public void WriteManifest_FileAddedByATargetThatHooksTheTrim_IsListedWithTrimmedMetadata()
    {
        var project = _fixtures.Copy("LateFile");
        var folder = Path.GetDirectoryName(project)!;

        var lines = _fixtures.WriteManifest(project);

        var late = FileLines(Path.Combine(folder, "Generated", "Late.sql"), ("SqlSourceDialect", "mssql"));
        var at = Array.IndexOf(lines, late[0]);
        at.ShouldBeGreaterThan(0);
        lines.Skip(at).Take(late.Length).ShouldBe(late);
        lines.ShouldContain("File=" + Path.Combine(folder, "Queries", "Users.sql"));
    }

    // TD-0025: the target runs no hook of a build.
    [Fact]
    public void WriteManifest_FileAddedByATargetThatHooksTheBuild_IsNotListed()
    {
        var project = _fixtures.Copy("BuildHookFile");
        var folder = Path.GetDirectoryName(project)!;

        var lines = _fixtures.WriteManifest(project);

        lines
            .Where(line => line.StartsWith("File=", StringComparison.Ordinal))
            .ShouldBe(["File=" + Path.Combine(folder, "Queries", "Users.sql")]);
    }

    [Fact]
    public void WriteManifest_ProjectThatListsItsOwnFiles_HoldsTheFilesItLists()
    {
        var project = _fixtures.Copy("OwnFiles");
        var folder = Path.GetDirectoryName(project)!;

        var lines = _fixtures.WriteManifest(project);

        lines
            .Where(line => line.StartsWith("File=", StringComparison.Ordinal))
            .ShouldBe([
                "File=" + Path.Combine(folder, "Queries", "One.sql"),
                "File=" + Path.Combine(folder, "Queries", "Two.sql"),
            ]);
    }

    // MSBuild splits a list at a semicolon and reads "%41" as "A".  A path is none of its lists.
    [Fact]
    public void WriteManifest_PathsWithCharactersThatMSBuildReads_AreListedWhole()
    {
        const string Odd = "q;=%41 'é";
        var project = _fixtures.Copy("OddPaths");
        var folder = Path.Combine(Path.GetDirectoryName(project)!, Odd);

        var lines = _fixtures.WriteManifest(project);

        lines.ShouldContain("File=" + Path.Combine(folder, "a;b=c%41 'é.sql"));
        lines.ShouldContain("Compile=" + Path.Combine(folder, "a;b=c%41 'é.cs"));
    }

    [Fact]
    public void WriteManifest_ValuesThatHoldASemicolon_AreEachOneLine()
    {
        var project = _fixtures.Copy("Single");

        var lines = _fixtures.WriteManifest(
            project,
            "SqlSourceGeneratorParameters=a%3Bb",
            "SqlSourceModelNamespace=A=B"
        );

        lines.ShouldContain("Property.SqlSourceGeneratorParameters=a;b");
        lines.ShouldContain("Property.SqlSourceModelNamespace=A=B");
    }
}
