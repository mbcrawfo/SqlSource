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
    private static readonly string Tests = Path.Combine(RepositoryRoot.Folder, "tests");
    private static readonly string GeneratorTests = Path.Combine(Tests, "SqlSource.Tests", "Generator");

    private static readonly string[] FolderProperties = ["$(MSBuildThisFileDirectory)", "$(MSBuildProjectDirectory)"];

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
    // project's file.  RoslynFloor's compile of the generator's tests is the one exception.
    [Fact]
    public void TestProjects_Includes_CompileNoFileOfAnotherProject() =>
        Directory
            .EnumerateDirectories(Tests)
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

    // The Include of each Compile item of the project that names a file outside the project's folder, however it is
    // spelled: the path is resolved as MSBuild resolves it, from the project's folder.
    private static IEnumerable<string> IncludesFromOutside(string projectFolder, XDocument project)
    {
        var folder = Path.GetFullPath(projectFolder) + Path.DirectorySeparatorChar;
        return project
            .Descendants("Compile")
            .Select(item => (string?)item.Attribute("Include"))
            .OfType<string>()
            .Where(include => !Resolve(folder, include).StartsWith(folder, StringComparison.Ordinal));
    }

    // MSBuild accepts either separator on every system, and the two properties name the project's folder.
    private static string Resolve(string folder, string include)
    {
        var path = include.Replace('\\', '/');
        foreach (var property in FolderProperties)
        {
            if (path.StartsWith(property, StringComparison.Ordinal))
            {
                path = path[property.Length..].TrimStart('/');
                break;
            }
        }

        return Path.GetFullPath(Path.Combine(folder, path));
    }

    [Fact]
    public void GeneratorTests_FirstLine_SaysTheyAreShared() =>
        Directory
            .EnumerateFiles(GeneratorTests, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadLines(file).FirstOrDefault() != SharedHeader)
            .Select(Path.GetFileName)
            .ShouldBeEmpty();
}
