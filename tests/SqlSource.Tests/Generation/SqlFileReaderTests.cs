using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Parsing;
using SqlSource.Tests.Generator;
using Xunit;

namespace SqlSource.Tests.Generation;

public class SqlFileReaderTests
{
    private const string Path = "/app/Repo/Users.sql";

    [Fact]
    public void Read_FileWithNamedQueries_GivesEachQueryWithTheLocationOfItsName()
    {
        const string Text =
            "-- name: GetUser\n-- summary: Loads one user.\nSELECT 1; -- c\n\n-- name: ListUsers\nSELECT 2\nFROM t;\n";

        var file = Read(Text);

        file.NormalizedPath.ShouldBe("app/Repo/Users.sql");
        file.FileName.ShouldBe("Users.sql");
        file.Errors.ShouldBeEmpty();
        file.Queries.ShouldBe([
            new SqlQuery(
                "GetUser",
                Location(new TextSpan(9, 7), 0, 9, 0, 16),
                "Loads one user.",
                TestModels.Array(Literal("SELECT 1;")),
                null
            ),
            new SqlQuery(
                "ListUsers",
                Location(new TextSpan(70, 9), 4, 9, 4, 18),
                null,
                TestModels.Array(Literal("SELECT 2\nFROM t;")),
                null
            ),
        ]);
    }

    [Fact]
    public void Read_FileWithoutNameMarker_IsOneQueryNamedAfterTheFileAndLocatedAtItsStart()
    {
        var file = Read("SELECT 1;\n", "C:\\app\\Repo\\CountUsers.sql");

        file.FileName.ShouldBe("CountUsers.sql");
        file.Queries.ShouldBe([
            new SqlQuery(
                "CountUsers",
                new LocationInfo("C:\\app\\Repo\\CountUsers.sql", new TextSpan(0, 0), default),
                null,
                TestModels.Array(Literal("SELECT 1;")),
                null
            ),
        ]);
    }

    [Fact]
    public void Read_FileWithErrors_GivesEachErrorWithItsLineAndColumnAndNoQueries()
    {
        const string Text =
            "-- name: GetUser\nSELECT 1;\n-- name: get-user\nSELECT 2;\n-- SqlSource: nonsense\nSELECT 3;";

        var file = Read(Text);

        file.Queries.ShouldBeEmpty();
        file.Errors.ShouldBe([
            DiagnosticInfo.Create(SqlDiagnostics.InvalidName, Location(new TextSpan(36, 8), 2, 9, 2, 17), "get-user"),
            DiagnosticInfo.Create(
                SqlDiagnostics.UnknownDirective,
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
                Literal("SELECT * FROM "),
                new SqlSegment(SqlSegmentKind.Token, "table"),
                Literal(" t WHERE "),
                new SqlSegment(SqlSegmentKind.Token, "filter"),
                Literal(";"),
            ]);
    }

    [Fact]
    public void Read_ValidationDirectives_GiveEachQueryItsOwnOrTheFilesOrNone()
    {
        const string Text =
            "-- SqlSource: no-token-validation\n-- name: FromFile\nSELECT {{a}};\n"
            + "-- name: Own\n-- SqlSource: token-validation\nSELECT {{b}};\n";

        var file = Read(Text);

        file.Errors.ShouldBeEmpty();
        file.Queries.Select(query => query.TokenValidation).ShouldBe([false, true]);
        Read("SELECT {{a}};\n").Queries.ShouldHaveSingleItem().TokenValidation.ShouldBeNull();
    }

    [Fact]
    public void Read_IgnoredToken_IsLiteralTextOfAConstant()
    {
        var file = Read("-- SqlSource: token-ignore=raw\nSELECT '{{raw}}';\n");

        file.Queries.ShouldHaveSingleItem().Segments.ShouldBe([Literal("SELECT '{{raw}}';")]);
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

    [Fact]
    public void Read_SameText_GivesEqualResults()
    {
        const string Text = "-- name: GetUser\nSELECT 1;\n";

        Read(Text).ShouldBe(Read(Text));
    }

    private static SqlSegment Literal(string text) => new(SqlSegmentKind.Literal, text);

    private static ParsedSqlFile Read(string? text, string path = Path) =>
        SqlFileReader.Read(
            new InMemoryAdditionalText(path, text),
            SqlPath.Normalize(path)!,
            TestContext.Current.CancellationToken
        );

    private static LocationInfo Location(TextSpan span, int startLine, int startColumn, int endLine, int endColumn) =>
        new(
            Path,
            span,
            new LinePositionSpan(new LinePosition(startLine, startColumn), new LinePosition(endLine, endColumn))
        );
}
