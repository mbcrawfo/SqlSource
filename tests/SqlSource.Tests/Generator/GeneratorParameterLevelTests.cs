using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Generator;

// The list of generator parameters in effect for a query, for a type that claims its file, is the one from the most
// specific level that gives a list, whole: the query's markers, the preamble's, the attribute, the metadata of the
// file's item, the project's property.  Two parameters have an effect today, and each row shows both.
public class GeneratorParameterLevelTests
{
    private const string On = "keep-comments no-token-validation";

    private const string Other = "sort-input";

    private const string Default = "default";

    private const string Check = "global::System.ArgumentException.ThrowIfNullOrWhiteSpace(table);";

    [Theory]
    // property, metadata, attribute, preamble, query
    [InlineData(null, null, null, null, null, false)]
    [InlineData(On, null, null, null, null, true)]
    [InlineData(On, Default, null, null, null, false)]
    [InlineData(On, Other, null, null, null, false)]
    [InlineData(null, On, null, null, null, true)]
    [InlineData(Other, On, Default, null, null, false)]
    [InlineData(null, null, On, null, null, true)]
    [InlineData(Default, Other, On, null, null, true)]
    [InlineData(On, On, On, Default, null, false)]
    [InlineData(null, null, null, On, null, true)]
    [InlineData(On, On, On, On, Default, false)]
    [InlineData(On, On, On, On, Other, false)]
    [InlineData(null, null, null, Default, On, true)]
    public void Run_GeneratorParameters_ComeWholeFromTheMostSpecificLevelThatGivesAList(
        string? property,
        string? metadata,
        string? attribute,
        string? preamble,
        string? query,
        bool expected
    )
    {
        var sql =
            (preamble is null ? string.Empty : "-- generator: " + preamble + "\n")
            + "-- name: ListFrom\n"
            + (query is null ? string.Empty : "-- generator: " + query + "\n")
            + "SELECT * FROM {{table}}; -- kept\n";
        var file = new SqlFile(
            "/app/Repo/Users.sql",
            sql,
            Metadata: metadata is null
                ? null
                : new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = metadata }
        );

        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source(attribute))],
            [file],
            generatorParameters: property
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        var generated = run.Sources["App.Sample.g.cs"];
        if (expected)
        {
            generated.ShouldContain("-- kept");
            generated.ShouldNotContain("Throw");
        }
        else
        {
            generated.ShouldNotContain("-- kept");
            generated.ShouldContain(Check);
        }
    }

    // One type asks for comments and the other does not.  The token stands only in a comment, so one gets a method
    // and the other a constant, each from its own form of the same file.
    [Fact]
    public void Run_TwoTypesClaimOneFileAndOneKeepsComments_EachGetsItsOwnSql()
    {
        const string Source = """
            using SqlSource;

            namespace App;

            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
            public partial class Plain
            {
                public const string Sql = Q;
            }

            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct, Parameters = "keep-comments")]
            public partial class Kept
            {
                public static string Sql() => Q("x");
            }
            """;

        var run = GeneratorHarness.Run(
            Source,
            new SqlFile("/app/Repo/Q.sql", "-- name: Q\nSELECT 1 /* {{note}} */ FROM t;\n")
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.Plain.g.cs"].ShouldContain("public const string Q = \"SELECT 1   FROM t;\";");
        run.Sources["App.Kept.g.cs"].ShouldContain("public static string Q(string note)");
    }

    [Fact]
    public void Run_PropertyWithAWordThatIsNoParameter_IsAnErrorWithoutAPositionAndTheOthersApply()
    {
        var run = Run("keep-comments nope token-validation", null);

        run.Diagnostics.ShouldBe([
            "SQLSRC014 (1,1)-(1,1): 'nope' is not a valid value of SqlSourceGeneratorParameters",
            "SQLSRC014 (1,1)-(1,1): 'token-validation' is not a valid value of SqlSourceGeneratorParameters",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain("-- kept");
    }

    [Fact]
    public void Run_TheSameInvalidWordInThePropertyAndInTwoFiles_IsReportedOnce()
    {
        var metadata = new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = "nope" };

        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
            [
                new SqlFile("/app/Repo/A.sql", "SELECT 1;\n", Metadata: metadata),
                new SqlFile("/app/Repo/B.sql", "SELECT 2;\n", Metadata: metadata),
            ],
            generatorParameters: "nope"
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC014 (1,1)-(1,1): 'nope' is not a valid value of SqlSourceGeneratorParameters",
        ]);
    }

    // A file that no type claims is never read, and neither is what MSBuild says about it.
    [Fact]
    public void Run_InvalidMetadataOfAFileThatNoTypeClaims_IsNotReported()
    {
        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
            [
                new SqlFile("/app/Repo/A.sql", "SELECT 1;\n"),
                new SqlFile(
                    "/app/Migrations/001.sql",
                    "SELECT 2;\n",
                    Metadata: new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = "nope" }
                ),
            ]
        );

        run.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Run_DefaultBesideAnotherWordInTheProperty_IsReportedWholeAndGivesNoList()
    {
        var run = Run("default keep-comments", null);

        run.Diagnostics.ShouldBe([
            "SQLSRC014 (1,1)-(1,1): 'default keep-comments' is not a valid value of SqlSourceGeneratorParameters",
        ]);
        run.Sources["App.Sample.g.cs"].ShouldNotContain("-- kept");
    }

    [Fact]
    public void Run_AttributeWithAWordThatIsNoParameter_IsAnErrorAtTheAttribute()
    {
        var run = Run(null, "keep-comments nope");

        run.Diagnostics.ShouldHaveSingleItem()
            .ShouldBe("SQLSRC006 /app/Repo/Sample.cs(5,2)-(5,88): 'nope' is not a valid value of Parameters");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Run_AttributeParametersWithoutText_IsNotSet(string value)
    {
        var run = Run("keep-comments", value);

        run.Diagnostics.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain("-- kept");
    }

    private static string Source(string? attribute) =>
        "using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate(SqlLocation = SqlLocation.Direct"
        + (attribute is null ? string.Empty : ", Parameters = \"" + attribute + "\"")
        + ")]\npublic partial class Sample;\n";

    private static GeneratorRun Run(string? property, string? attribute) =>
        GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source(attribute))],
            [new SqlFile("/app/Repo/Users.sql", "-- name: ListFrom\nSELECT * FROM {{table}}; -- kept\n")],
            generatorParameters: property
        );
}
