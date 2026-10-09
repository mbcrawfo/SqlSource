using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Generator;

// The settings that have no effect yet are accepted at every level they have, and a value none of them takes is
// reported where it was written.
public class SettingLevelTests
{
    private const string Sql = "-- name: Q\nSELECT 1;\n";

    [Theory]
    [InlineData("SqlSourceOutput", "models")]
    [InlineData("SqlSourceOutput", " Code-Gen ")]
    [InlineData("SqlSourceDatabase", "not read by the generator, so never wrong")]
    public void Run_ValidSettingAsPropertyAndAsMetadata_ChangesNothing(string name, string value)
    {
        var plain = GeneratorHarness.Run(Source(null), new SqlFile("/app/Repo/Q.sql", Sql));

        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
            [new SqlFile("/app/Repo/Q.sql", Sql, Metadata: new Dictionary<string, string> { [name] = value })],
            properties: new Dictionary<string, string?> { [name] = value }
        );

        run.Diagnostics.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldBe(plain.Sources["App.Sample.g.cs"]);
    }

    [Theory]
    [InlineData("SqlSourceOutput", "model")]
    [InlineData("SqlSourceOutput", "sql models")]
    public void Run_InvalidSettingAsPropertyAndAsMetadata_IsReportedOnceWithoutAPosition(string name, string value)
    {
        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
            [new SqlFile("/app/Repo/Q.sql", Sql, Metadata: new Dictionary<string, string> { [name] = value })],
            properties: new Dictionary<string, string?> { [name] = value }
        );

        run.Diagnostics.ShouldBe(["SQLSRC014 (1,1)-(1,1): '" + value + "' is not a valid value of " + name]);
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources.Keys.ShouldContain("App.Sample.g.cs");
    }

    [Theory]
    [InlineData("Output = GeneratorOutput.Sql")]
    [InlineData("Output = GeneratorOutput.Models")]
    [InlineData("Output = GeneratorOutput.CodeGen")]
    public void Run_ValidAttributeProperty_ChangesNothing(string argument)
    {
        var plain = GeneratorHarness.Run(Source(null), new SqlFile("/app/Repo/Q.sql", Sql));

        var run = GeneratorHarness.Run(Source(argument), new SqlFile("/app/Repo/Q.sql", Sql));

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldBe(plain.Sources["App.Sample.g.cs"]);
    }

    [Theory]
    [InlineData("Output = (GeneratorOutput)7", "7", "Output")]
    [InlineData("Output = (GeneratorOutput)(-1)", "-1", "Output")]
    public void Run_AttributePropertyWithANumberThatIsNoMember_IsAnErrorAtTheAttribute(
        string argument,
        string value,
        string property
    )
    {
        var run = GeneratorHarness.Run(Source(argument), new SqlFile("/app/Repo/Q.sql", Sql));

        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.ShouldStartWith("SQLSRC006 /app/Repo/Sample.cs(5,2)-");
        diagnostic.ShouldEndWith(": '" + value + "' is not a valid value of " + property);
        run.Sources.Keys.ShouldNotContain("App.Sample.g.cs");
    }

    private static string Source(string? argument) =>
        "using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate(SqlLocation = SqlLocation.Direct"
        + (argument is null ? string.Empty : ", " + argument)
        + ")]\npublic partial class Sample;\n";
}
