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
    [InlineData("SqlSourceInputModelSuffix", "Args")]
    [InlineData("SqlSourceOutputModelSuffix", "Row")]
    [InlineData("SqlSourceModelNamespace", "App.Models")]
    [InlineData("SqlSourceInputModelType", "sealed record")]
    [InlineData("SqlSourceOutputModelType", "Class")]
    [InlineData("SqlSourceCollectionType", "IReadOnlyList")]
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
    [InlineData("SqlSourceInputModelSuffix", "A B")]
    [InlineData("SqlSourceOutputModelSuffix", "A.B")]
    [InlineData("SqlSourceModelNamespace", "App.")]
    [InlineData("SqlSourceInputModelType", "struct")]
    [InlineData("SqlSourceOutputModelType", "sealed")]
    [InlineData("SqlSourceCollectionType", "HashSet")]
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
    [InlineData("InputModelSuffix = \"Args\", OutputModelSuffix = \"Row\", ModelNamespace = \"App.Models\"")]
    [InlineData("InputModelType = GeneratorModelType.Class, OutputModelType = GeneratorModelType.SealedRecord")]
    [InlineData("CollectionType = GeneratorCollectionType.IReadOnlyList")]
    [InlineData("MethodLocation = MethodLocation.Internal")]
    [InlineData("InputModelSuffix = \"\", ModelNamespace = \"  \"")]
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
    [InlineData("InputModelType = (GeneratorModelType)4", "4", "InputModelType")]
    [InlineData("OutputModelType = (GeneratorModelType)9", "9", "OutputModelType")]
    [InlineData("CollectionType = (GeneratorCollectionType)10", "10", "CollectionType")]
    [InlineData("MethodLocation = (MethodLocation)4", "4", "MethodLocation")]
    [InlineData("InputModelSuffix = \"A B\"", "A B", "InputModelSuffix")]
    [InlineData("OutputModelSuffix = \"A.B\"", "A.B", "OutputModelSuffix")]
    [InlineData("ModelNamespace = \"App.\"", "App.", "ModelNamespace")]
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

    // The markers that have no effect yet, in a preamble and in a query: the type's file is the one of the same file
    // without them.
    [Theory]
    [InlineData(
        "-- name: Q\nSELECT id FROM users WHERE id = @id;\n",
        "-- database: main\n-- output: models\n-- input-model-suffix: Args\n-- output-model-suffix: Row\n"
            + "-- model-namespace: App.Models\n-- input-model-type: sealed record\n-- output-model-type: class\n"
            + "-- collection-type: ImmutableArray\n\n"
            + "-- name: Q -> one\n-- param: @id int not null\n-- database: other\n-- output: codegen\n"
            + "-- input-model: QueryArgs\n-- output-model: App.Models.QueryRow\n-- collection-type: List\n"
            + "SELECT id FROM users WHERE id = @id;\n"
    )]
    [InlineData(
        "-- name: Q\nSELECT id FROM {{table}} WHERE {{filter}};\n",
        "-- output: models\n\n-- name: Q -> many\n-- param: @since timestamptz not null\n"
            + "-- param: @page int\n-- token: {{filter:created_at > @since}}\n"
            + "SELECT id FROM {{table:users}} WHERE {{filter}};\n"
    )]
    public void Run_MarkersWithoutAnEffect_GiveTheFileOfTheSameQueryWithoutThem(string plainSql, string markedSql)
    {
        var plain = GeneratorHarness.Run(Source(null), new SqlFile("/app/Repo/Q.sql", plainSql));

        var run = GeneratorHarness.Run(Source(null), new SqlFile("/app/Repo/Q.sql", markedSql));

        plain.Diagnostics.ShouldBeEmpty();
        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldBe(plain.Sources["App.Sample.g.cs"]);
    }

    [Fact]
    public void Run_MethodLocationAsAPropertyOrAMarker_IsNotASetting()
    {
        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
            [new SqlFile("/app/Repo/Q.sql", "-- name: Q\n-- method-location: public\nSELECT 1; -- c\n")],
            properties: new Dictionary<string, string?> { ["SqlSourceMethodLocation"] = "nope" }
        );

        // The property is not read, and the line is an ordinary comment.
        run.Diagnostics.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain("\"SELECT 1;\"");
    }

    private static string Source(string? argument) =>
        "using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate(SqlLocation = SqlLocation.Direct"
        + (argument is null ? string.Empty : ", " + argument)
        + ")]\npublic partial class Sample;\n";
}
