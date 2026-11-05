// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Generation;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The generator parameters in effect for a query, for a type that claims its file, merge per option: each level, from
// the project's settings file through the settings file of the file's folder and the file header to the query's
// markers, turns the options it names on or off, or gives them their default, over the levels before it, and default
// alone gives every option its default at its level.  Two parameters change a query's generated code, keep-comments
// its SQL and no-token-validation its method's checks, and each row shows both.
public sealed class GeneratorParameterLevelTests
{
    private const string On = "keep-comments no-token-validation";

    private const string Off = "keep-comments=off no-token-validation=off";

    private const string Other = "sort-input";

    private const string Default = "default";

    private const string ReservedTokenInAComment = "-- name: Q\nSELECT 1 -- {{class}}\n";

    private const string DefaultOpenedInAComment = "-- name: Q\nSELECT * -- e.g. {{note: here\nFROM t WHERE {{w}}\n";

    private const string CommentInATokensHead = "-- name: Q\nSELECT * FROM {{t/* c */}}\n";

    private const string Check = "global::System.ArgumentException.ThrowIfNullOrWhiteSpace(table);";

    // The levels whose nearest state of each option is on: the comment stays and the token is not checked.
    [Theory]
    // project's file, folder's file, file header, query
    [InlineData(On, null, null, null)]
    [InlineData(null, On, null, null)]
    [InlineData(null, null, On, null)]
    [InlineData(Default, Other, On, null)]
    [InlineData(null, null, Default, On)]
    [InlineData(On, Other, null, null)]
    [InlineData(On, On, On, Other)]
    [InlineData(Off, null, null, On)]
    public void Run_GeneratorParametersWhoseNearestStateIsOn_KeepTheComment(
        string? projectFile,
        string? folderFile,
        string? header,
        string? query
    )
    {
        var generated = ListFromAt(projectFile, folderFile, header, query);

        generated.ShouldContain("-- kept");
        generated.ShouldNotContain("Throw");
    }

    // The levels whose nearest state of each option is off or the default, though a farther one turns it on, and the
    // run where no level names them, so that the default leaves them out: the comment goes and the token is checked.
    [Theory]
    // project's file, folder's file, file header, query
    [InlineData(null, null, null, null)]
    [InlineData(On, Default, null, null)]
    [InlineData(On, On, Default, null)]
    [InlineData(On, On, On, Default)]
    [InlineData(On, null, null, Off)]
    public void Run_GeneratorParametersWhoseNearestStateIsOffOrTheDefault_DropTheCommentAndCheckTheToken(
        string? projectFile,
        string? folderFile,
        string? header,
        string? query
    )
    {
        var generated = ListFromAt(projectFile, folderFile, header, query);

        generated.ShouldNotContain("-- kept");
        generated.ShouldContain(Check);
    }

    // The query turns one option off and leaves the other as the file header gives it.
    [Fact]
    public void Run_QueryTurnsOneParameterOff_KeepsTheOtherFromAbove()
    {
        var generated = ListFromAt(null, null, On, "keep-comments=off");

        generated.ShouldNotContain("-- kept");
        generated.ShouldNotContain("Throw");
    }

    // A keyword in braces in a kept comment is SQLSRC114 wherever keep-comments is given, and the file
    // then gives no members.
    [Theory]
    [InlineData(SettingLevel.ProjectFile, "(2,13)-(2,22)")]
    [InlineData(SettingLevel.FolderFile, "(2,13)-(2,22)")]
    [InlineData(SettingLevel.Header, "(4,13)-(4,22)")]
    public void Run_ReservedTokenInAKeptComment_IsReportedAndTheFileGivesNoMembers(SettingLevel level, string place)
    {
        var run = RunKeepingCommentsAt(level, ReservedTokenInAComment);

        run.Diagnostics.ShouldBe([
            "SQLSRC114 /app/Repo/Q.sql" + place + ": The token name 'class' is a reserved C# keyword",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("string Q");
    }

    // The token and the comment overlap, so with the comments kept the query reads other tokens: the comment opens a
    // default that the SQL closes, or stands in a token's head.  It is SQLSRC120 where the project's settings file
    // keeps comments, and the file then gives no members.
    [Theory]
    [InlineData(DefaultOpenedInAComment, "(2,10)-(2,30)", "note")]
    [InlineData(CommentInATokensHead, "(2,18)-(2,25)", "t")]
    public void Run_TokenThatCrossesAKeptComment_IsReportedAndTheFileGivesNoMembers(
        string sql,
        string place,
        string token
    )
    {
        var run = RunKeepingCommentsAt(SettingLevel.ProjectFile, sql);

        run.Diagnostics.ShouldBe([
            "SQLSRC120 /app/Repo/Q.sql"
                + place
                + ": Token '"
                + token
                + "' and a comment overlap, so the token is read differently where comments are kept.  Keep a comment "
                + "wholly inside the token's default, or outside the token.",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("string Q");
    }

    // No level may keep comments, so the form with them is not built, and the keyword in the comment is never
    // scanned.
    [Fact]
    public void Run_ReservedTokenInACommentWhereNoTypeKeepsComments_IsNoErrorAndBuildsNoKeptForm()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            [new SqlFile(GeneratorHarness.UsersPath, ReservedTokenInAComment)],
            generatorParameters: null
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("public const string Q = \"SELECT 1\";");
        var query = StepOutputs.Single<ParsedSqlFile>(run, TrackingNames.ParsedFile).Queries.ShouldHaveSingleItem();
        query.KeptSegments.ShouldBeNull();
        query.KeptErrors.ShouldBeEmpty();
    }

    // The file Q.sql of this text at sql, claimed by Sample alone, under keep-comments given at the level.
    private static GeneratorRun RunKeepingCommentsAt(SettingLevel level, string sql)
    {
        var settings = new LevelSettings().At(
            level,
            Setting.GeneratorParameters,
            LevelSettings.WordsAt(level, "keep-comments")
        );

        return GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            settings.Files(new SqlFile("/app/Repo/Q.sql", sql)),
            settings: settings.ProjectSettings
        );
    }

    // The file of Sample for a query ListFrom with a token and a kept comment, with each level's generator parameters
    // given, none where null.
    private static string ListFromAt(string? projectFile, string? folderFile, string? header, string? query)
    {
        var settings = new LevelSettings();
        if (folderFile is not null)
        {
            _ = settings.At(
                SettingLevel.FolderFile,
                Setting.GeneratorParameters,
                LevelSettings.WordsAt(SettingLevel.FolderFile, folderFile)
            );
        }

        var sql =
            (header is null ? string.Empty : "-- generator: " + header + "\n")
            + "-- name: ListFrom\n"
            + (query is null ? string.Empty : "-- generator: " + query + "\n")
            + "SELECT * FROM {{table}}; -- kept\n";

        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            settings.Files(new SqlFile(GeneratorHarness.UsersPath, sql)),
            generatorParameters: projectFile
        );

        run.ShouldBeClean();
        return run.Sources[GeneratorHarness.SampleFile];
    }
}
