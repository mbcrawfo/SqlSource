// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The settings files, sqlsource.json, that apply to a .sql file's folder and every folder below it: what the chain of
// a file's folder gives its parse.  The dialect comes from the header's marker, then the chain, then the metadata of
// the file's item, then the project's property, and keep-comments from the first list of the chain's and the
// metadata's, then the property's.
public sealed class SettingsFileTests
{
    // ANSI and MySQL read the backticks as the quotes of an identifier, and the comment starts after it.  PostgreSQL
    // has no such quote, so the two dashes inside them start the comment.
    private const string Query = "SELECT `--x` AS a -- c\n";

    private const string Postgres = "SELECT `";

    private const string NotPostgres = "SELECT `--x` AS a";

    private const string QueryPath = "/app/Repo/Q.sql";

    private const string Kept = "-- name: Q\nSELECT 1 -- kept\n";

    [Fact]
    public void Run_DialectInTheFolder_ParsesTheFileWithIt()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                "/app/Repo/sqlsource.json", /*lang=json,strict*/
                """{"dialect":"postgres"}"""
            ),
            new SqlFile(QueryPath, Query)
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + Postgres + "\";");
    }

    [Fact]
    public void Run_DialectInAFolderAbove_ParsesTheFileWithIt()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                "/app/sqlsource.json", /*lang=json,strict*/
                """{"dialect":"postgres"}"""
            ),
            new SqlFile(QueryPath, Query)
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + Postgres + "\";");
    }

    [Fact]
    public void Run_DialectInTheFolder_OverridesTheProperty()
    {
        var run = Run(
            "mysql",
            GeneratorHarness.SettingsFile(
                "/app/Repo/sqlsource.json", /*lang=json,strict*/
                """{"dialect":"postgres"}"""
            ),
            new SqlFile(QueryPath, Query)
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + Postgres + "\";");
    }

    [Fact]
    public void Run_DialectInTheFolder_OverridesTheMetadata()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                "/app/Repo/sqlsource.json", /*lang=json,strict*/
                """{"dialect":"postgres"}"""
            ),
            new SqlFile(QueryPath, Query, "mysql")
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + Postgres + "\";");
    }

    [Fact]
    public void Run_DialectMarker_OverridesTheFolder()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                "/app/Repo/sqlsource.json", /*lang=json,strict*/
                """{"dialect":"postgres"}"""
            ),
            new SqlFile(QueryPath, "-- dialect: ansi\n" + Query)
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + NotPostgres + "\";");
    }

    // The metadata said something, and what it said is wrong, whatever the folder's file says.
    [Fact]
    public void Run_InvalidMetadataUnderTheFoldersDialect_IsStillReported()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                "/app/Repo/sqlsource.json", /*lang=json,strict*/
                """{"dialect":"postgres"}"""
            ),
            new SqlFile(QueryPath, Query, "nope")
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC011 (1,1)-(1,1): 'nope' is not a SQL dialect.");
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + Postgres + "\";");
    }

    // Stage one: the parse builds the form of the query with its comments, which the folder's list may ask for.
    [Fact]
    public void Run_KeepCommentsInTheFolder_ParsesTheFormWithComments()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                "/app/Repo/sqlsource.json", /*lang=json,strict*/
                """{"generatorParameters":["keep-comments"]}"""
            ),
            new SqlFile(QueryPath, Kept)
        );

        run.Diagnostics.ShouldBeEmpty();
        StepOutputs.Single<FileParseInput>(run, TrackingNames.FileParseInput).CommentsWanted.ShouldBeTrue();
        _ = StepOutputs
            .Single<ParsedSqlFile>(run, TrackingNames.ParsedFile)
            .Queries.ShouldHaveSingleItem()
            .KeptSegments.ShouldNotBeNull();
    }

    // The nearer file's list replaces the farther one's and the property's whole, so no level outside the file can
    // ask for comments, and the form with them is not built.
    [Fact]
    public void Run_EmptyListInANearerFile_ParsesNoFormWithComments()
    {
        var run = Run(
            null,
            [
                GeneratorHarness.SettingsFile(
                    "/app/sqlsource.json", /*lang=json,strict*/
                    """{"generatorParameters":["keep-comments"]}"""
                ),
                GeneratorHarness.SettingsFile(
                    "/app/Repo/sqlsource.json", /*lang=json,strict*/
                    """{"generatorParameters":[]}"""
                ),
                new SqlFile(QueryPath, Kept),
            ],
            generatorParameters: "keep-comments"
        );

        run.Diagnostics.ShouldBeEmpty();
        StepOutputs.Single<FileParseInput>(run, TrackingNames.FileParseInput).CommentsWanted.ShouldBeFalse();
        StepOutputs
            .Single<ParsedSqlFile>(run, TrackingNames.ParsedFile)
            .Queries.ShouldHaveSingleItem()
            .KeptSegments.ShouldBeNull();
    }

    [Fact]
    public void Run_KeepCommentsInTheFolder_OverridesTheMetadatasListForTheParse()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                "/app/Repo/sqlsource.json", /*lang=json,strict*/
                """{"generatorParameters":["keep-comments"]}"""
            ),
            new SqlFile(
                QueryPath,
                Kept,
                Metadata: new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = "sort-input" }
            )
        );

        run.Diagnostics.ShouldBeEmpty();
        StepOutputs.Single<FileParseInput>(run, TrackingNames.FileParseInput).CommentsWanted.ShouldBeTrue();
    }

    private static GeneratorRun Run(string? dialect, params SqlFile[] files) => Run(dialect, files, null);

    private static GeneratorRun Run(string? dialect, SqlFile[] files, string? generatorParameters) =>
        GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            files,
            generatorParameters: generatorParameters,
            dialect: dialect
        );
}
