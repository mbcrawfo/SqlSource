using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Shouldly;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests;

// The rules of tests/AGENTS.md about how the test projects share code that a test can check, read from the
// repository itself.
public class TestLayoutTests
{
    private static readonly string Tests = Path.Combine(RepositoryRoot.Folder, "tests");
    private static readonly string GeneratorTests = Path.Combine(Tests, "SqlSource.Tests", "Generator");

    // tests/SqlSource.Tests.RoslynFloor compiles every file of Generator/; a helper there would be compiled twice.
    [Fact]
    public void GeneratorFolder_HoldsOnlyTestClasses() =>
        Directory
            .EnumerateFiles(GeneratorTests, "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(name => !name!.EndsWith("Tests.cs", StringComparison.Ordinal))
            .ShouldBeEmpty();

    // A test project shares code through SqlSource.Testing or SqlSource.Tool.Testing, never by compiling another
    // project's file.  RoslynFloor's compile of the generator's tests is the one exception.
    [Fact]
    public void TestProjects_CompileNoFileOfAnotherProject() =>
        Directory
            .EnumerateDirectories(Tests)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj"))
            .SelectMany(project =>
                XDocument
                    .Load(project)
                    .Descendants("Compile")
                    .Select(item => (string?)item.Attribute("Include"))
                    .Where(include => include?.StartsWith("../", StringComparison.Ordinal) == true)
                    .Select(include => $"{Path.GetFileName(project)}: {include}")
            )
            .ShouldBe(["SqlSource.Tests.RoslynFloor.csproj: ../SqlSource.Tests/Generator/**/*.cs"]);
}
