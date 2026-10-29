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

    // The query's list replaces the file's, the file's beats the project's file, and the project's file alone decides
    // last: the token is validated.
    [Theory]
    // Nothing set.
    [InlineData(null, null, null)]
    // The project's file alone.
    [InlineData(null, null, "default")]
    [InlineData(null, null, "")]
    // The file's list beats the project's file, with or without the switch.
    [InlineData(null, "default", "no-token-validation")]
    [InlineData(null, "keep-comments", "no-token-validation")]
    // The query's list replaces the file's.
    [InlineData("default", null, "no-token-validation")]
    [InlineData("default", "no-token-validation", "no-token-validation")]
    public void Run_MethodWhoseNearestListValidates_ChecksTheToken(
        string? queryGeneratorParameter,
        string? fileGeneratorParameter,
        string? projectFile
    ) => ListFromAt(queryGeneratorParameter, fileGeneratorParameter, projectFile).ShouldContain(Check);

    // The same, where the nearest list that says anything turns validation off: the token is not checked.
    [Theory]
    // The project's file alone.
    [InlineData(null, null, "no-token-validation")]
    [InlineData(null, null, " No-Token-Validation ")]
    // The file's list.
    [InlineData(null, "no-token-validation", null)]
    // The query's list replaces the file's.
    [InlineData("no-token-validation", null, null)]
    [InlineData("no-token-validation", "default", null)]
    public void Run_MethodWhoseNearestListTurnsValidationOff_ChecksNothing(
        string? queryGeneratorParameter,
        string? fileGeneratorParameter,
        string? projectFile
    ) => ListFromAt(queryGeneratorParameter, fileGeneratorParameter, projectFile).ShouldNotContain("Throw");

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
        var source = run.Sources[GeneratorHarness.SampleFile];
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

    // The file of Sample for a query ListFrom with a token, with the file's and the query's generator parameters in
    // markers and the project's in its settings file, none where null.
    private static string ListFromAt(
        string? queryGeneratorParameter,
        string? fileGeneratorParameter,
        string? projectFile
    )
    {
        var sql =
            (fileGeneratorParameter is null ? string.Empty : "-- generator: " + fileGeneratorParameter + "\n")
            + "-- name: ListFrom\n"
            + (queryGeneratorParameter is null ? string.Empty : "-- generator: " + queryGeneratorParameter + "\n")
            + "SELECT * FROM {{table}};\n";

        var run = Run(OutputKind.Sql, projectFile, new SqlFile(GeneratorHarness.UsersPath, sql));

        run.ShouldBeClean();
        return run.Sources[GeneratorHarness.SampleFile];
    }
}
