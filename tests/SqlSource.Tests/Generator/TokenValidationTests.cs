using Shouldly;
using Xunit;

namespace SqlSource.Tests.Generator;

// Whether a generated method checks its arguments: a list of generator parameters decides whole, the query's before
// the file's, and the project's SqlSourceTokenValidation property decides for a query that has neither.  Without any
// of them it checks.
public class TokenValidationTests
{
    private const string Source = """
        using SqlSource;

        namespace App;

        [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
        public partial class Sample
        {
            public static string List() => ListFrom("users");
        }
        """;

    private const string Check = "global::System.ArgumentException.ThrowIfNullOrWhiteSpace(table);";

    [Theory]
    // Nothing set: validate.
    [InlineData(null, null, null, true)]
    // The property alone.
    [InlineData(null, null, "false", false)]
    [InlineData(null, null, " False ", false)]
    [InlineData(null, null, "true", true)]
    [InlineData(null, null, "", true)]
    // The file's list beats the property, with or without the switch.
    [InlineData(null, "no-token-validation", null, false)]
    [InlineData(null, "no-token-validation", "true", false)]
    [InlineData(null, "default", "false", true)]
    [InlineData(null, "keep-comments", "false", true)]
    // The query's list replaces the file's.
    [InlineData("no-token-validation", null, "true", false)]
    [InlineData("default", null, "false", true)]
    [InlineData("no-token-validation", "default", "true", false)]
    [InlineData("default", "no-token-validation", "false", true)]
    [InlineData("keep-comments", "no-token-validation", "false", true)]
    public void Run_Method_ValidatesByQueryThenFileThenProject(
        string? queryGeneratorParameter,
        string? fileGeneratorParameter,
        string? property,
        bool expected
    )
    {
        var sql =
            (fileGeneratorParameter is null ? string.Empty : "-- generator: " + fileGeneratorParameter + "\n")
            + "-- name: ListFrom\n"
            + (queryGeneratorParameter is null ? string.Empty : "-- generator: " + queryGeneratorParameter + "\n")
            + "SELECT * FROM {{table}};\n";

        var run = Run(property, new SqlFile("/app/Repo/Users.sql", sql));

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        var source = run.Sources["App.Sample.g.cs"];
        if (expected)
        {
            source.ShouldContain(Check);
        }
        else
        {
            source.ShouldNotContain("Throw");
        }
    }

    [Fact]
    public void Run_PropertyOff_ChangesOnlyTheQueriesWithoutAGeneratorParameter()
    {
        var run = GeneratorHarness.Run(
            [
                new SourceFile(
                    GeneratorHarness.SourcePath,
                    """
                    using SqlSource;

                    namespace App;

                    [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
                    public partial class Sample;
                    """
                ),
            ],
            [
                new SqlFile(
                    "/app/Repo/Users.sql",
                    "-- name: Plain\nSELECT {{a}};\n-- name: Checked\n-- generator: default\nSELECT {{b}};\n"
                ),
            ],
            tokenValidation: "false"
        );

        run.CompilationErrors.ShouldBeEmpty();
        var source = run.Sources["App.Sample.g.cs"];
        source.ShouldNotContain("ThrowIfNullOrWhiteSpace(a);");
        source.ShouldContain("ThrowIfNullOrWhiteSpace(b);");
    }

    [Theory]
    [InlineData("off")]
    [InlineData("0")]
    [InlineData(" yes ")]
    public void Run_PropertyThatIsNotTrueOrFalse_IsAnErrorWithoutAPositionAndTheMethodsValidate(string property)
    {
        var run = Run(property, new SqlFile("/app/Repo/Users.sql", "-- name: ListFrom\nSELECT * FROM {{table}};\n"));

        run.Diagnostics.ShouldBe([
            "SQLSRC010 (1,1)-(1,1): The MSBuild property SqlSourceTokenValidation is '"
                + property
                + "'.  It must be 'true' or 'false'.",
        ]);

        // The members are still there, so the build reports this error and not one for each use of a query.
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain(Check);
    }

    [Fact]
    public void Run_InvalidPropertyInAProjectWithoutAnAttributedType_IsStillAnError()
    {
        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, "public class Sample;")],
            [],
            tokenValidation: "nope"
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC010 ");
        run.Sources.Keys.ShouldBe(["SqlSourceGenerateAttribute.g.cs"]);
    }

    private static GeneratorRun Run(string? property, SqlFile file) =>
        GeneratorHarness.Run([new SourceFile(GeneratorHarness.SourcePath, Source)], [file], tokenValidation: property);
}
