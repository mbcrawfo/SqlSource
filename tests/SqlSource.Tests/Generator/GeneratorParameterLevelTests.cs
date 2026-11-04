// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Generation;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The list of generator parameters in effect for a query, for a type that claims its file, is the one from the most
// specific level that gives a list, whole: the query's markers, the preamble's, the attribute, the settings file of
// the file's folder, then the project's settings file.  Two parameters change a query's generated code, keep-comments
// its SQL and no-token-validation its method's checks, and each row shows both.
public sealed class GeneratorParameterLevelTests
{
    private const string On = "keep-comments no-token-validation";

    private const string Other = "sort-input";

    private const string Default = "default";

    private const string ReservedTokenInAComment = "-- name: Q\nSELECT 1 -- {{class}}\n";

    private const string DefaultOpenedInAComment = "-- name: Q\nSELECT * -- e.g. {{note: here\nFROM t WHERE {{w}}\n";

    private const string CommentInATokensHead = "-- name: Q\nSELECT * FROM {{t/* c */}}\n";

    private const string Check = "global::System.ArgumentException.ThrowIfNullOrWhiteSpace(table);";

    // The type Plain, which turns comments off, and Kept, which keeps them as the settings files do.
    // Removed with the attribute level in Task 3.
    private const string PlainAndKeptSource = """
        using SqlSource;

        namespace App;

        [SqlSourceGenerate(Source = ".", SqlLocation = SqlLocation.Direct, Parameters = "default")]
        public partial class Plain
        {
            public const string Sql = Q;
        }

        [SqlSourceGenerate(Source = ".", SqlLocation = SqlLocation.Direct)]
        public partial class Kept { }
        """;

    // The levels whose most specific list keeps comments: the comment stays and the token is not checked.
    [Theory]
    // project's file, folder's file, attribute, preamble, query
    [InlineData(On, null, null, null, null)]
    [InlineData(null, On, null, null, null)]
    // Removed with the attribute level in Task 3.
    [InlineData(null, null, On, null, null)]
    // Removed with the attribute level in Task 3.
    [InlineData(Default, Other, On, null, null)]
    [InlineData(null, null, null, On, null)]
    [InlineData(Default, Other, null, On, null)]
    [InlineData(null, null, null, Default, On)]
    public void Run_GeneratorParametersWhoseMostSpecificListKeepsComments_KeepTheComment(
        string? projectFile,
        string? folderFile,
        string? attribute,
        string? preamble,
        string? query
    )
    {
        var generated = ListFromAt(projectFile, folderFile, attribute, preamble, query);

        generated.ShouldContain("-- kept");
        generated.ShouldNotContain("Throw");
    }

    // The levels whose most specific list does not keep comments, though a farther one does, and the run where no level
    // gives a list, so that the default leaves them out: the list comes whole from the most specific level, so the
    // comment goes and the token is checked.
    [Theory]
    // project's file, folder's file, attribute, preamble, query
    [InlineData(null, null, null, null, null)]
    [InlineData(On, Default, null, null, null)]
    [InlineData(On, Other, null, null, null)]
    [InlineData(On, On, null, Default, null)]
    [InlineData(On, On, null, On, Default)]
    // Removed with the attribute level in Task 3.
    [InlineData(Other, On, Default, null, null)]
    // Removed with the attribute level in Task 3.
    [InlineData(On, On, On, Default, null)]
    // Removed with the attribute level in Task 3.
    [InlineData(On, On, On, On, Default)]
    // Removed with the attribute level in Task 3.
    [InlineData(On, On, On, On, Other)]
    public void Run_GeneratorParametersWhoseMostSpecificListLeavesCommentsOut_DropTheCommentAndCheckTheToken(
        string? projectFile,
        string? folderFile,
        string? attribute,
        string? preamble,
        string? query
    )
    {
        var generated = ListFromAt(projectFile, folderFile, attribute, preamble, query);

        generated.ShouldNotContain("-- kept");
        generated.ShouldContain(Check);
    }

    // One type asks for comments and the other does not.  The token stands only in a comment, so one gets a method
    // and the other a constant, each from its own form of the same file.
    // Removed with the attribute level in Task 3.
    [Fact]
    public void Run_TwoTypesClaimOneFileAndOneKeepsComments_EachGetsItsOwnSql()
    {
        const string Source = """
            using SqlSource;

            namespace App;

            [SqlSourceGenerate(Source = ".", SqlLocation = SqlLocation.Direct)]
            public partial class Plain
            {
                public const string Sql = Q;
            }

            [SqlSourceGenerate(Source = ".", SqlLocation = SqlLocation.Direct, Parameters = "keep-comments")]
            public partial class Kept
            {
                public static string Sql() => Q("x");
            }
            """;

        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            new SqlFile("/app/Repo/Q.sql", "-- name: Q\nSELECT 1 /* {{note}} */ FROM t;\n")
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.Plain.g.cs"].ShouldContain("public const string Q = \"SELECT 1   FROM t;\";");
        run.Sources["App.Kept.g.cs"].ShouldContain("public static string Q(string note)");
    }

    // The project's settings file keeps comments and one type turns them off.  The keyword stands only in a comment,
    // so it is an error only for the type that keeps it, whose file then gives no members, and the other type uses the
    // file.
    // Removed with the attribute level in Task 3.
    [Fact]
    public void Run_ReservedTokenInACommentWhereTheProjectFileKeepsComments_IsReportedOnlyForATypeThatKeepsComments()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, PlainAndKeptSource)],
            [new SqlFile("/app/Repo/Q.sql", ReservedTokenInAComment)],
            generatorParameters: "keep-comments"
        );

        ShouldReportTheTokenForKeptAlone(run);
    }

    // The same, where the settings file of the file's folder keeps comments.
    // Removed with the attribute level in Task 3.
    [Fact]
    public void Run_ReservedTokenInACommentWhereTheFolderFileKeepsComments_IsReportedOnlyForATypeThatKeepsComments()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, PlainAndKeptSource)],
            [
                new SqlFile("/app/Repo/Q.sql", ReservedTokenInAComment),
                GeneratorHarness.SettingsFile(
                    "/app/Repo/sqlsource.json", /*lang=json,strict*/
                    """{"generatorParameters": ["keep-comments"]}"""
                ),
            ]
        );

        ShouldReportTheTokenForKeptAlone(run);
    }

    // The token and the comment overlap, so with the comments kept the query reads other tokens: the comment opens a
    // default that the SQL closes, or stands in a token's head.  It is an error only for the type that keeps the
    // comments, whose file then gives no members, and the other type emits the query with its token.
    // Removed with the attribute level in Task 3.
    [Theory]
    [InlineData(DefaultOpenedInAComment, "(2,10)-(2,30)", "note", "w")]
    [InlineData(CommentInATokensHead, "(2,18)-(2,25)", "t", "t")]
    public void Run_TokenThatCrossesAComment_IsReportedOnlyForATypeThatKeepsComments(
        string sql,
        string place,
        string token,
        string parameter
    )
    {
        const string Source = """
            using SqlSource;

            namespace App;

            [SqlSourceGenerate(Source = ".", SqlLocation = SqlLocation.Direct, Parameters = "default")]
            public partial class Plain
            {
                public static string Sql() => Q("x");
            }

            [SqlSourceGenerate(Source = ".", SqlLocation = SqlLocation.Direct)]
            public partial class Kept { }
            """;

        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [new SqlFile("/app/Repo/Q.sql", sql)],
            generatorParameters: "keep-comments"
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC120 /app/Repo/Q.sql"
                + place
                + ": Token '"
                + token
                + "' and a comment overlap, so the token is read differently where comments are kept.  Keep a comment "
                + "wholly inside the token's default, or outside the token.",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.Plain.g.cs"].ShouldContain("public static string Q(string " + parameter + ")");
        run.Sources["App.Kept.g.cs"].ShouldNotContain("string Q");
    }

    // A keyword in braces in a kept comment is SQLSRC114 wherever the list that keeps comments is given, and the file
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
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
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

    // Removed with the attribute level in Task 3.
    [Fact]
    public void Run_AttributeWithAWordThatIsNoParameter_IsAnErrorAtTheAttribute()
    {
        var run = Run(OutputKind.Sql, null, "keep-comments nope");

        run.Diagnostics.ShouldHaveSingleItem()
            .ShouldBe("SQLSRC006 /app/Repo/Sample.cs(5,2)-(5,102): 'nope' is not a valid value of Parameters");
    }

    // Removed with the attribute level in Task 3.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Run_AttributeParametersWithoutText_IsNotSet(string value)
    {
        var run = Run(OutputKind.Sql, "keep-comments", value);

        run.Diagnostics.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("-- kept");
    }

    // Removed with the attribute level in Task 3.
    private static string Source(string? attribute) =>
        GeneratorHarness.SampleSource(attribute is null ? null : "Parameters = \"" + attribute + "\"");

    // The query ListFrom under the generator parameters of the project's settings file and of the attribute.
    // Removed with the attribute level in Task 3.
    private static GeneratorRun Run(OutputKind? output, string? projectFile, string? attribute) =>
        GeneratorHarness.Run(
            output,
            [new SourceFile(GeneratorHarness.SourcePath, Source(attribute))],
            [new SqlFile(GeneratorHarness.UsersPath, "-- name: ListFrom\nSELECT * FROM {{table}}; -- kept\n")],
            generatorParameters: projectFile
        );

    // The reserved token is reported once, for Kept, whose file gives no members, and Plain uses the file.
    // Removed with the attribute level in Task 3.
    private static void ShouldReportTheTokenForKeptAlone(GeneratorRun run)
    {
        run.Diagnostics.ShouldBe([
            "SQLSRC114 /app/Repo/Q.sql(2,13)-(2,22): The token name 'class' is a reserved C# keyword",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.Plain.g.cs"].ShouldContain("public const string Q = \"SELECT 1\";");
        run.Sources["App.Kept.g.cs"].ShouldNotContain("string Q");
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
    private static string ListFromAt(
        string? projectFile,
        string? folderFile,
        string? attribute,
        string? preamble,
        string? query
    )
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
            (preamble is null ? string.Empty : "-- generator: " + preamble + "\n")
            + "-- name: ListFrom\n"
            + (query is null ? string.Empty : "-- generator: " + query + "\n")
            + "SELECT * FROM {{table}}; -- kept\n";

        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, Source(attribute))],
            settings.Files(new SqlFile(GeneratorHarness.UsersPath, sql)),
            generatorParameters: projectFile
        );

        run.ShouldBeClean();
        return run.Sources[GeneratorHarness.SampleFile];
    }
}
