using Shouldly;
using Xunit;

namespace SqlSource.Tests.Generator;

// Whether a generated method checks its arguments: the query's own directive decides, then the directive at the top
// of its file, then the project's SqlSourceTokenValidation property, and without any of them it does.
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
    // The file's directive beats the property.
    [InlineData(null, "no-token-validation", null, false)]
    [InlineData(null, "no-token-validation", "true", false)]
    [InlineData(null, "token-validation", "false", true)]
    // The query's directive beats both.
    [InlineData("no-token-validation", null, "true", false)]
    [InlineData("token-validation", null, "false", true)]
    [InlineData("no-token-validation", "token-validation", "true", false)]
    [InlineData("token-validation", "no-token-validation", "false", true)]
    public void Run_Method_ValidatesByQueryThenFileThenProject(
        string? queryDirective,
        string? fileDirective,
        string? property,
        bool expected
    )
    {
        var sql =
            (fileDirective is null ? string.Empty : "-- SqlSource: " + fileDirective + "\n")
            + "-- name: ListFrom\n"
            + (queryDirective is null ? string.Empty : "-- SqlSource: " + queryDirective + "\n")
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
    public void Run_PropertyOff_ChangesOnlyTheQueriesWithoutADirective()
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
                    "-- name: Plain\nSELECT {{a}};\n-- name: Checked\n-- SqlSource: token-validation\nSELECT {{b}};\n"
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
