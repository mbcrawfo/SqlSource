// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Generation.MSBuild;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The output that a test of the generator states is the project's SqlSourceOutput, as the compiler hands it over.
// The key is spelled out, as the provider spells out its own.
public sealed class TestOptionsProviderTests
{
    private const string OutputKey = "build_property.SqlSourceOutput";

    // A test's data cannot be of the generator's internal type, so the member is found by its spelling.
    [Theory]
    [InlineData("Sql")]
    [InlineData("Models")]
    [InlineData("CodeGen")]
    public void GlobalOptions_Output_IsTheProjectPropertySpelledAsAUserWritesIt(string spelled)
    {
        var output = Enum.Parse<OutputKind>(spelled);

        new TestOptionsProvider(output, null).GlobalOptions.TryGetValue(OutputKey, out var value).ShouldBeTrue();
        value.ShouldBe(spelled);
    }

    [Fact]
    public void GlobalOptions_NoOutput_LeavesThePropertyUnset() =>
        new TestOptionsProvider(null, null).GlobalOptions.TryGetValue(OutputKey, out _).ShouldBeFalse();

    [Fact]
    public void GlobalOptions_OutputAndAPropertyOfTheSameName_IsTheProperty()
    {
        var options = new TestOptionsProvider(
            OutputKind.Sql,
            null,
            properties: new Dictionary<string, string?> { ["SqlSourceOutput"] = "models" }
        );

        options.GlobalOptions.TryGetValue(OutputKey, out var value).ShouldBeTrue();
        value.ShouldBe("models");
    }

    [Fact]
    public void ReadProject_OutputOfTheProvider_IsReadAsThatOutput()
    {
        foreach (var output in Enum.GetValues<OutputKind>())
        {
            ProjectSettings.Read(new TestOptionsProvider(output, null).GlobalOptions).Level.Output.ShouldBe(output);
        }
    }
}
