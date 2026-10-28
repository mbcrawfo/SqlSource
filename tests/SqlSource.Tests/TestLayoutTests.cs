using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Shouldly;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests;

// The rules of tests/AGENTS.md about how the test projects share code that a test can check, read from the
// repository itself.
public sealed class TestLayoutTests
{
    private static readonly string Tests = RepositoryRoot.Path("tests");
    private static readonly string GeneratorTests = Path.Combine(Tests, "SqlSource.Tests", "Generator");

    // The first line of each file of Generator/, so that a reader of one sees that RoslynFloor compiles it too.
    private const string SharedHeader =
        "// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  "
        + "See AGENTS.md beside it.";

    // tests/SqlSource.Tests.RoslynFloor compiles every file of Generator/; a helper there would be compiled twice.
    [Fact]
    public void GeneratorFolder_Files_AreAllTestClasses() =>
        Directory
            .EnumerateFiles(GeneratorTests, "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(name => !name!.EndsWith("Tests.cs", StringComparison.Ordinal))
            .ShouldBeEmpty();

    // A test project shares code through SqlSource.Testing or SqlSource.Tool.Testing, never by compiling another
    // project's file.  RoslynFloor's compile of the generator's tests is the one exception.  The database projects
    // sit one level down, in tests/DatabaseTests/.
    [Fact]
    public void TestProjects_CompileIncludes_ReachNoFileOfAnotherProject() =>
        Directory
            .EnumerateDirectories(Tests)
            .Concat(Directory.EnumerateDirectories(Path.Combine(Tests, "DatabaseTests")))
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj"))
            .SelectMany(project =>
                IncludesFromOutside(Path.GetDirectoryName(project)!, XDocument.Load(project))
                    .Select(include => $"{Path.GetFileName(project)}: {include}")
            )
            .ShouldBe(["SqlSource.Tests.RoslynFloor.csproj: ../SqlSource.Tests/Generator/**/*.cs"]);

    // Each spelling of a file outside the project that MSBuild and an IDE write: a relative path with either
    // separator, from one of MSBuild's folder properties, or absolute.
    [Theory]
    [InlineData("../Other/File.cs")]
    [InlineData(@"..\Other\File.cs")]
    [InlineData("$(MSBuildThisFileDirectory)../Other/File.cs")]
    [InlineData(@"$(MSBuildProjectDirectory)\..\Other\File.cs")]
    [InlineData("Sub/../../Other/File.cs")]
    [InlineData("/elsewhere/File.cs")]
    public void IncludesFromOutside_FileOfAnotherFolder_IsFound(string include) =>
        IncludesFromOutside("/repo/tests/App", Project(include)).ShouldBe([include]);

    [Theory]
    [InlineData("File.cs")]
    [InlineData(@"Sub\File.cs")]
    [InlineData("$(MSBuildThisFileDirectory)Sub/File.cs")]
    [InlineData("Sub/../File.cs")]
    public void IncludesFromOutside_FileOfTheProject_IsNotFound(string include) =>
        IncludesFromOutside("/repo/tests/App", Project(include)).ShouldBeEmpty();

    private static XDocument Project(string include) =>
        new(
            new XElement(
                "Project",
                new XElement("ItemGroup", new XElement("Compile", new XAttribute("Include", include)))
            )
        );

    private static IEnumerable<string> IncludesFromOutside(string projectFolder, XDocument project) =>
        ProjectIncludes.FromOutside(projectFolder, project, static item => item.Name.LocalName == "Compile");

    // The harness is referenced by the projects that pin the oldest supported driver, so it brings no driver, no Dapper
    // and nothing of the tool.
    [Fact]
    public void TestHarness_References_BringNoDriverAndNothingOfTheTool()
    {
        var project = XDocument.Load(RepositoryRoot.Path("tests/DatabaseTests/TestHarness/TestHarness.csproj"));

        Includes(project, "PackageReference")
            .ShouldBe(
                [
                    "Shouldly",
                    "Testcontainers.MsSql",
                    "Testcontainers.PostgreSql",
                    "xunit.v3.assert",
                    "xunit.v3.extensibility.core",
                ],
                ignoreOrder: true
            );
        Includes(project, "ProjectReference")
            .Select(Path.GetFileName)
            .ShouldBe(["SqlSource.csproj", "SqlSource.Testing.csproj"], ignoreOrder: true);
    }

    // SqlSource.Testing is referenced by RoslynFloor, so it brings no Testcontainers, which the harness carries for it.
    [Fact]
    public void SqlSourceTesting_PackageReferences_BringNoTestcontainers() =>
        Includes(
                XDocument.Load(RepositoryRoot.Path("tests/SqlSource.Testing/SqlSource.Testing.csproj")),
                "PackageReference"
            )
            .Where(include => include.StartsWith("Testcontainers.", StringComparison.Ordinal))
            .ShouldBeEmpty();

    private static IEnumerable<string> Includes(XDocument project, string item) =>
        project
            .Descendants()
            .Where(element => element.Name.LocalName == item)
            .Select(element => (string)element.Attribute("Include")!);

    [Fact]
    public void GeneratorTests_FirstLine_SaysTheyAreShared() =>
        Directory
            .EnumerateFiles(GeneratorTests, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadLines(file).FirstOrDefault() != SharedHeader)
            .Select(Path.GetFileName)
            .ShouldBeEmpty();
}
