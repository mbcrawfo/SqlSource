using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Paths;
using SqlSource.Parsing;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation;

public sealed class SqlFileReaderTests
{
    [Fact]
    public void Read_FileWithNamedQueries_GivesEachQueryWithTheLocationOfItsName()
    {
        const string Text =
            "-- name: GetUser\n-- summary: Loads one user.\nSELECT 1; -- c\n\n-- name: ListUsers\nSELECT 2\nFROM t;\n";

        var file = Read(Text);

        file.NormalizedPath.ShouldBe(GeneratorHarness.UsersNormalizedPath);
        file.FileName.ShouldBe("Users.sql");
        file.Errors.ShouldBeEmpty();
        file.Queries.ShouldBe([
            TestModels.Query(
                "GetUser",
                "SELECT 1;",
                "Loads one user.",
                nameLocation: Location(new TextSpan(9, 7), 0, 9, 0, 16)
            ),
            TestModels.Query(
                "ListUsers",
                "SELECT 2\nFROM t;",
                nameLocation: Location(new TextSpan(70, 9), 4, 9, 4, 18)
            ),
        ]);
    }

    [Fact]
    public void Read_FileWithoutNameMarker_IsOneQueryNamedAfterTheFileAndLocatedAtItsStart()
    {
        var file = Read("SELECT 1;\n", "C:\\app\\Repo\\CountUsers.sql");

        file.FileName.ShouldBe("CountUsers.sql");
        file.Queries.ShouldBe([
            TestModels.Query(
                "CountUsers",
                "SELECT 1;",
                nameLocation: new LocationInfo("C:\\app\\Repo\\CountUsers.sql", new TextSpan(0, 0), default)
            ),
        ]);
    }

    [Fact]
    public void Read_QueryWithTokens_CopiesThem() =>
        Read("-- name: Q\n-- token: {{b:y}}\nSELECT {{a:x}}, {{b}};\n")
            .Queries.ShouldHaveSingleItem()
            .Tokens.ShouldBe([new SqlToken("a", "x"), new SqlToken("b", "y")]);

    // "-- column: b null" is the 17 characters from 33, on the third line.
    [Fact]
    public void Read_QueryWithColumnMarkers_CopiesThemWithTheirLocations() =>
        Read("-- name: Q\n-- column: a not null\n-- column: b null\nSELECT 1 AS a, 2 AS b;\n")
            .Queries.ShouldHaveSingleItem()
            .ColumnMarkers.ShouldBe([
                new ColumnMarker("a", false, null, false, Location(new TextSpan(11, 21), 1, 0, 1, 21)),
                new ColumnMarker("b", true, null, false, Location(new TextSpan(33, 17), 2, 0, 2, 17)),
            ]);

    [Fact]
    public void Read_QueryWithAShape_CopiesIt() =>
        Read("-- name: Q -> one-optional\nSELECT 1;\n")
            .Queries.ShouldHaveSingleItem()
            .Shape.ShouldBe(ResultShape.OneOptional);

    [Fact]
    public void Read_QueryWithParameters_CopiesThem() =>
        Read("-- name: Q\nSELECT @a, @b;\n")
            .Queries.ShouldHaveSingleItem()
            .Parameters.Select(static parameter => parameter.Name)
            .ShouldBe(["a", "b"]);

    [Fact]
    public void Read_FileWithErrors_GivesEachErrorWithItsLineAndColumnAndNoQueries()
    {
        const string Text =
            "-- name: GetUser\nSELECT 1;\n-- name: get-user\nSELECT 2;\n-- generator: nonsense\nSELECT 3;";

        var file = Read(Text);

        file.Queries.ShouldBeEmpty();
        file.Errors.ShouldBe([
            DiagnosticInfo.Create(SqlDiagnostics.InvalidName, Location(new TextSpan(36, 8), 2, 9, 2, 17), "get-user"),
            DiagnosticInfo.Create(
                SqlDiagnostics.UnknownGeneratorParameter,
                Location(new TextSpan(69, 8), 4, 14, 4, 22),
                "nonsense"
            ),
        ]);
    }

    [Fact]
    public void Read_WindowsLineEndings_CountLinesTheSameWay()
    {
        var file = Read("-- name: GetUser\r\nSELECT 1;\r\n-- name: 1x\r\nSELECT 2;\r\n");

        file.Errors.ShouldHaveSingleItem().Location.LineSpan.Start.ShouldBe(new LinePosition(2, 9));
    }

    [Fact]
    public void Read_QueryWithTokens_KeepsItsSegmentsBesideTheOtherQueries()
    {
        const string Text =
            "-- name: GetUser\nSELECT 1;\n-- name: ListFrom\nSELECT * FROM {{ table }} t WHERE {{filter}};\n"
            + "-- name: Count\nSELECT 3;\n";

        var file = Read(Text);

        file.Errors.ShouldBeEmpty();
        file.Queries.Select(query => query.Name).ShouldBe(["GetUser", "ListFrom", "Count"]);
        file.Queries[1]
            .Segments.ShouldBe([
                TestModels.Literal("SELECT * FROM "),
                new SqlSegment(SqlSegmentKind.Token, "table"),
                TestModels.Literal(" t WHERE "),
                new SqlSegment(SqlSegmentKind.Token, "filter"),
                TestModels.Literal(";"),
            ]);
    }

    [Fact]
    public void Read_ValidationGeneratorParameters_GiveEachQueryItsOwnOrTheFilesOrNone()
    {
        const string Text =
            "-- generator: no-token-validation\n-- name: FromFile\nSELECT {{a}};\n"
            + "-- name: Own\n-- generator: default\nSELECT {{b}};\n";

        var file = Read(Text);

        file.Errors.ShouldBeEmpty();
        file.Queries.Select(query => query.Markers.Parameters)
            .ShouldBe([GeneratorParameters.NoTokenValidation, GeneratorParameters.None]);
        Read("SELECT {{a}};\n").Queries.ShouldHaveSingleItem().Markers.ShouldBeSameAs(SettingsLevel.None);
    }

    [Fact]
    public void Read_IgnoredToken_IsLiteralTextOfAConstant()
    {
        var file = Read("-- token-ignore: raw\nSELECT '{{raw}}';\n");

        file.Queries.ShouldHaveSingleItem().Segments.ShouldBe([TestModels.Literal("SELECT '{{raw}}';")]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n-- only a comment\n")]
    public void Read_UnreadableOrEmptyFile_IsAnErrorAtTheStartOfTheFile(string? text)
    {
        var file = Read(text);

        file.Queries.ShouldBeEmpty();
        file.Errors.ShouldBe([
            DiagnosticInfo.Create(SqlDiagnostics.EmptyBlock, Location(new TextSpan(0, 0), 0, 0, 0, 0)),
        ]);
    }

    [Theory]
    [InlineData(true, "SELECT 1; -- c")]
    [InlineData(false, null)]
    public void Read_CommentsWanted_BuildsTheKeptFormOfAQueryWithoutAList(bool commentsWanted, string? expected)
    {
        var query = Read("SELECT 1; -- c\n", commentsWanted: commentsWanted).Queries.ShouldHaveSingleItem();

        query.Segments.ShouldBe(TestModels.Array(TestModels.Literal("SELECT 1;")));
        query.KeptSegments.ShouldBe(expected is null ? null : TestModels.Array(TestModels.Literal(expected)));
    }

    // "{{class}}" is the 9 characters from 23, on the second line.  Only a type that keeps the comments reports it.
    [Fact]
    public void Read_ReservedTokenInAKeptComment_IsAnErrorOfTheQueryAndNotOfTheFile()
    {
        var file = Read("-- name: Q\nSELECT 1 -- {{class}}\n", commentsWanted: true);

        file.Errors.ShouldBeEmpty();
        file.Queries.ShouldHaveSingleItem()
            .KeptErrors.ShouldBe([
                DiagnosticInfo.Create(
                    SqlDiagnostics.ReservedTokenName,
                    Location(new TextSpan(23, 9), 1, 12, 1, 21),
                    "class"
                ),
            ]);
    }

    [Fact]
    public void Read_QueryWithoutAKeptForm_HasNoErrorsOfIt() =>
        Read("-- name: Q\nSELECT 1 -- {{class}}\n").Queries.ShouldHaveSingleItem().KeptErrors.ShouldBeEmpty();

    [Fact]
    public void Read_SameText_GivesEqualResults()
    {
        const string Text = SqlTexts.GetUser;

        Read(Text).ShouldBe(Read(Text));
    }

    [Fact]
    public void Read_Dialect_IsTheDialectTheFileIsParsedWith()
    {
        const string Text = "-- name: A\nSELECT 'a\\'b' # c\n";

        Read(Text, dialect: SqlDialect.MySql)
            .Queries.ShouldHaveSingleItem()
            .Segments.ShouldBe(TestModels.Array(TestModels.Literal("SELECT 'a\\'b'")));
        Read(Text).Errors.ShouldHaveSingleItem().Descriptor.ShouldBe(SqlDiagnostics.UnterminatedQuote);
    }

    [Fact]
    public void Read_Dialect_IsNamedOnTheFileByTheOneItWasReadBy()
    {
        Read("SELECT 1;\n", dialect: SqlDialect.MySql).Dialect.ShouldBe(SqlDialect.MySql);
        Read("-- dialect: postgres\nSELECT 1;\n", dialect: SqlDialect.MySql).Dialect.ShouldBe(SqlDialect.PostgreSql);
    }

    [Fact]
    public void Read_InvalidDialectOfTheFile_IsCarriedAndTheFileIsStillParsed()
    {
        var file = Read("SELECT 1;\n", invalidDialect: "pgsql");

        file.InvalidDialect.ShouldBe("pgsql");
        file.Queries.Count.ShouldBe(1);
        Read("SELECT 1;\n").InvalidDialect.ShouldBeNull();
    }

    [Fact]
    public void Read_FileParseInput_ReadsTheFileWithItsDialectAndCarriesItsInvalidValue()
    {
        var text = new InMemoryAdditionalText(GeneratorHarness.UsersPath, "SELECT 1 # c\n");

        var file = SqlFileReader.Read(
            new FileParseInput(text, GeneratorHarness.UsersNormalizedPath, SqlDialect.MySql, "nope", false),
            TestContext.Current.CancellationToken
        );

        file.NormalizedPath.ShouldBe(GeneratorHarness.UsersNormalizedPath);
        file.Queries.ShouldHaveSingleItem().Segments.ShouldBe(TestModels.Array(TestModels.Literal("SELECT 1")));
        file.InvalidDialect.ShouldBe("nope");
    }

    [Fact]
    public void Read_FileParseInputThatWantsComments_BuildsTheKeptForm()
    {
        var text = new InMemoryAdditionalText(GeneratorHarness.UsersPath, "SELECT 1; -- c\n");

        var file = SqlFileReader.Read(
            new FileParseInput(text, GeneratorHarness.UsersNormalizedPath, SqlDialect.Ansi, null, true),
            TestContext.Current.CancellationToken
        );

        file.Queries.ShouldHaveSingleItem()
            .KeptSegments.ShouldBe(TestModels.Array(TestModels.Literal("SELECT 1; -- c")));
    }

    private static ParsedSqlFile Read(
        string? text,
        string path = GeneratorHarness.UsersPath,
        SqlDialect dialect = SqlDialect.Ansi,
        string? invalidDialect = null,
        bool commentsWanted = false
    ) =>
        SqlFileReader.Read(
            new InMemoryAdditionalText(path, text),
            SqlPath.Normalize(path)!,
            dialect,
            invalidDialect,
            commentsWanted,
            TestContext.Current.CancellationToken
        );

    private static LocationInfo Location(TextSpan span, int startLine, int startColumn, int endLine, int endColumn) =>
        new(
            GeneratorHarness.UsersPath,
            span,
            new LinePositionSpan(new LinePosition(startLine, startColumn), new LinePosition(endLine, endColumn))
        );
}
