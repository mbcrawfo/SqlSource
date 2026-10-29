// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

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
    private const string PlainAndKeptSource = """
        using SqlSource;

        namespace App;

        [SqlSourceGenerate(SqlLocation = SqlLocation.Direct, Parameters = "default")]
        public partial class Plain
        {
            public const string Sql = Q;
        }

        [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
        public partial class Kept { }
        """;

    // The levels whose most specific list keeps comments: the comment stays and the token is not checked.
    [Theory]
    // project's file, folder's file, attribute, preamble, query
    [InlineData(On, null, null, null, null)]
    [InlineData(null, On, null, null, null)]
    [InlineData(null, null, On, null, null)]
    [InlineData(Default, Other, On, null, null)]
    [InlineData(null, null, null, On, null)]
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
    [InlineData(Other, On, Default, null, null)]
    [InlineData(On, On, On, Default, null)]
    [InlineData(On, On, On, On, Default)]
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

            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct, Parameters = "default")]
            public partial class Plain
            {
                public static string Sql() => Q("x");
            }

            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
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

    [Fact]
    public void Run_AttributeWithAWordThatIsNoParameter_IsAnErrorAtTheAttribute()
    {
        var run = Run(OutputKind.Sql, null, "keep-comments nope");

        run.Diagnostics.ShouldHaveSingleItem()
            .ShouldBe("SQLSRC006 /app/Repo/Sample.cs(5,2)-(5,88): 'nope' is not a valid value of Parameters");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Run_AttributeParametersWithoutText_IsNotSet(string value)
    {
        var run = Run(OutputKind.Sql, "keep-comments", value);

        run.Diagnostics.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("-- kept");
    }

    private static string Source(string? attribute) =>
        GeneratorHarness.SampleSource(attribute is null ? null : "Parameters = \"" + attribute + "\"");

    // The query ListFrom under the generator parameters of the project's settings file and of the attribute.
    private static GeneratorRun Run(OutputKind? output, string? projectFile, string? attribute) =>
        GeneratorHarness.Run(
            output,
            [new SourceFile(GeneratorHarness.SourcePath, Source(attribute))],
            [new SqlFile(GeneratorHarness.UsersPath, "-- name: ListFrom\nSELECT * FROM {{table}}; -- kept\n")],
            generatorParameters: projectFile
        );

    // The reserved token is reported once, for Kept, whose file gives no members, and Plain uses the file.
    private static void ShouldReportTheTokenForKeptAlone(GeneratorRun run)
    {
        run.Diagnostics.ShouldBe([
            "SQLSRC114 /app/Repo/Q.sql(2,13)-(2,22): The token name 'class' is a reserved C# keyword",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.Plain.g.cs"].ShouldContain("public const string Q = \"SELECT 1\";");
        run.Sources["App.Kept.g.cs"].ShouldNotContain("string Q");
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
