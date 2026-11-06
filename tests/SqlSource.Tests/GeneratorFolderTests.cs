using System;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Repository;
using Xunit;

namespace SqlSource.Tests;

// The rules of Generator/AGENTS.md that a test can check: tests/SqlSource.Tests.RoslynFloor compiles every file of
// Generator/ again, so the folder holds test classes alone, and each says so on its first line.
public sealed class GeneratorFolderTests
{
    private static readonly string GeneratorTests = RepositoryRoot.Path("tests", "SqlSource.Tests", "Generator");

    // The first line of each file of Generator/, so that a reader of one sees that RoslynFloor compiles it too.
    private const string SharedHeader =
        "// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  "
        + "See AGENTS.md beside it.";

    // tests/SqlSource.Tests.RoslynFloor compiles every file of Generator/; a helper there would be compiled twice.
    [Fact]
    public void GeneratorFolder_Files_AreAllTestClasses() =>
        Directory
            .EnumerateFiles(GeneratorTests, "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(name => !name!.EndsWith("Tests.cs", StringComparison.Ordinal))
            .ShouldBeEmpty();

    [Fact]
    public void GeneratorTests_FirstLine_SaysTheyAreShared() =>
        Directory
            .EnumerateFiles(GeneratorTests, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadLines(file).FirstOrDefault() != SharedHeader)
            .Select(Path.GetFileName)
            .ShouldBeEmpty();
}
