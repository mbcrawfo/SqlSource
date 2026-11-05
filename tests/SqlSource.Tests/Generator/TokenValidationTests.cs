// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

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

        [SqlSourceGenerate(Source = ".")]
        public partial class Sample
        {
            public static string List() => ListFrom("users");
        }
        """;

    private const string Check = "global::System.ArgumentException.ThrowIfNullOrWhiteSpace(table);";

    // The generator parameters merge per parameter: the query's state of no-token-validation beats the file's, the
    // file's beats the project's file, and a level that does not name it leaves it to the one below.  Here the nearest
    // level that names it leaves validation on, or none does: the token is validated.
    [Theory]
    // Nothing set.
    [InlineData(null, null, null)]
    // The project's file alone.
    [InlineData(null, null, "default")]
    [InlineData(null, null, "")]
    // The file's state beats the project's file.
    [InlineData(null, "default", "no-token-validation")]
    [InlineData(null, "no-token-validation=off", "no-token-validation")]
    // The query's state beats the file's.
    [InlineData("default", null, "no-token-validation")]
    [InlineData("default", "no-token-validation", "no-token-validation")]
    [InlineData("no-token-validation=default", "no-token-validation", null)]
    public void Run_MethodWhoseNearestStateValidates_ChecksTheToken(
        string? queryGeneratorParameter,
        string? fileGeneratorParameter,
        string? projectFile
    ) => ListFromAt(queryGeneratorParameter, fileGeneratorParameter, projectFile).ShouldContain(Check);

    // The same, where the nearest level that names no-token-validation turns it on: the token is not checked.
    [Theory]
    // The project's file alone.
    [InlineData(null, null, "no-token-validation")]
    [InlineData(null, null, " No-Token-Validation ")]
    // The file's state, or the project's file's under a file that does not name it.
    [InlineData(null, "no-token-validation", null)]
    [InlineData(null, "keep-comments", "no-token-validation")]
    // The query's state beats the file's, or the file's reaches a query that does not name it.
    [InlineData("no-token-validation", null, null)]
    [InlineData("no-token-validation", "default", null)]
    [InlineData("sort-input", "no-token-validation", null)]
    public void Run_MethodWhoseNearestStateTurnsValidationOff_ChecksNothing(
        string? queryGeneratorParameter,
        string? fileGeneratorParameter,
        string? projectFile
    ) => ListFromAt(queryGeneratorParameter, fileGeneratorParameter, projectFile).ShouldNotContain("Throw");

    [Fact]
    public void Run_ProjectsFileOff_ChangesOnlyTheQueriesThatDoNotResetIt()
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
