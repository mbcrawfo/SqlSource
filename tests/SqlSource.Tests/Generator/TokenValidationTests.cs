// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// Whether a generated method checks its arguments: the generator parameters merge per parameter, so the nearest level
// that names token-validation decides, the query's before the file's and the file's before the generatorParameters
// of the project's settings file.  When none names it the method checks.  GeneratorParameterLevelTests has the levels
// between the file and the project.
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

    // The generator parameters merge per parameter: the query's state of token-validation beats the file's, the
    // file's beats the project's file, and a level that does not name it leaves it to the one below.  Here the nearest
    // level that names it leaves validation on, or none does: the token is validated.
    [Theory]
    // Nothing set.
    [InlineData(null, null, null)]
    // The project's file alone.
    [InlineData(null, null, "default")]
    [InlineData(null, null, "")]
    // The file's state beats the project's file.
    [InlineData(null, "default", "token-validation=off")]
    [InlineData(null, "token-validation", "token-validation=off")]
    // The query's state beats the file's.
    [InlineData("default", null, "token-validation=off")]
    [InlineData("default", "token-validation=off", "token-validation=off")]
    [InlineData("token-validation=default", "token-validation=off", null)]
    public void Run_MethodWhoseNearestStateValidates_ChecksTheToken(
        string? queryGeneratorParameter,
        string? fileGeneratorParameter,
        string? projectFile
    ) => ListFromAt(queryGeneratorParameter, fileGeneratorParameter, projectFile).ShouldContain(Check);

    // The same, where the nearest level that names token-validation turns it off: the token is not checked.
    [Theory]
    // The project's file alone.
    [InlineData(null, null, "token-validation=off")]
    [InlineData(null, null, " Token-Validation=Off ")]
    // The file's state, or the project's file's under a file that does not name it.
    [InlineData(null, "token-validation=off", null)]
    [InlineData(null, "keep-comments", "token-validation=off")]
    // The query's state beats the file's, or the file's reaches a query that does not name it.
    [InlineData("token-validation=off", null, null)]
    [InlineData("token-validation=off", "default", null)]
    [InlineData("sort-input", "token-validation=off", null)]
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
            generatorParameters: "token-validation=off"
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
