using System;
using System.IO;
using System.Linq;
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
}
