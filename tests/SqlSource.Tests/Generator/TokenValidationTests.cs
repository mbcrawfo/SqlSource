// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// Whether a generated method checks its arguments: a list of generator parameters decides whole, the query's before
// the file's, and the generatorParameters of the project's settings file decide for a query that has neither.
// Without any of them it checks.  GeneratorParameterLevelTests has the levels between the file and the project.
public sealed class TokenValidationTests
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
    // The project's file alone.
    [InlineData(null, null, "no-token-validation", false)]
    [InlineData(null, null, " No-Token-Validation ", false)]
    [InlineData(null, null, "default", true)]
    [InlineData(null, null, "", true)]
    // The file's list beats the project's file, with or without the switch.
    [InlineData(null, "no-token-validation", null, false)]
    [InlineData(null, "default", "no-token-validation", true)]
    [InlineData(null, "keep-comments", "no-token-validation", true)]
    // The query's list replaces the file's.
    [InlineData("no-token-validation", null, null, false)]
    [InlineData("default", null, "no-token-validation", true)]
    [InlineData("no-token-validation", "default", null, false)]
    [InlineData("default", "no-token-validation", "no-token-validation", true)]
    public void Run_Method_ValidatesByQueryThenFileThenProject(
        string? queryGeneratorParameter,
        string? fileGeneratorParameter,
        string? projectFile,
        bool expected
    )
    {
        var sql =
            (fileGeneratorParameter is null ? string.Empty : "-- generator: " + fileGeneratorParameter + "\n")
            + "-- name: ListFrom\n"
            + (queryGeneratorParameter is null ? string.Empty : "-- generator: " + queryGeneratorParameter + "\n")
            + "SELECT * FROM {{table}};\n";

        var run = Run(OutputKind.Sql, projectFile, new SqlFile(GeneratorHarness.UsersPath, sql));

        run.ShouldBeClean();
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
    public void Run_ProjectsFileOff_ChangesOnlyTheQueriesWithoutAGeneratorParameter()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            [
                new SqlFile(
                    GeneratorHarness.UsersPath,
                    "-- name: Plain\nSELECT {{a}};\n-- name: Checked\n-- generator: default\nSELECT {{b}};\n"
                ),
            ],
            generatorParameters: "no-token-validation"
        );

        run.CompilationErrors.ShouldBeEmpty();
        var source = run.Sources["App.Sample.g.cs"];
        source.ShouldNotContain("ThrowIfNullOrWhiteSpace(a);");
        source.ShouldContain("ThrowIfNullOrWhiteSpace(b);");
    }

    // The file under the generator parameters of the project's settings file, or none when they are null.
    private static GeneratorRun Run(OutputKind? output, string? projectFile, SqlFile file) =>
        GeneratorHarness.Run(
            output,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [file],
            generatorParameters: projectFile
        );
}
