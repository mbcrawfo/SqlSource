using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Parsing;
using SqlSource.Settings;
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
                null,
                TestModels.Array(Literal("SELECT 1;")),
                EquatableArray<SqlToken>.Empty,
                null,
                EquatableArray<SqlQueryParameter>.Empty
            ),
            new SqlQuery(
                "ListUsers",
                Location(new TextSpan(70, 9), 4, 9, 4, 18),
                null,
                null,
                TestModels.Array(Literal("SELECT 2\nFROM t;")),
                EquatableArray<SqlToken>.Empty,
                null,
                EquatableArray<SqlQueryParameter>.Empty
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
                null,
                TestModels.Array(Literal("SELECT 1;")),
                EquatableArray<SqlToken>.Empty,
                null,
                EquatableArray<SqlQueryParameter>.Empty
            ),
        ]);
    }

    [Fact]
    public void Read_QueryWithTokens_CopiesThem() =>
        Read("-- name: Q\n-- token: {{b:y}}\nSELECT {{a:x}}, {{b}};\n")
            .Queries.ShouldHaveSingleItem()
            .Tokens.ShouldBe([new SqlToken("a", "x"), new SqlToken("b", "y")]);

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
                Literal("SELECT * FROM "),
                new SqlSegment(SqlSegmentKind.Token, "table"),
                Literal(" t WHERE "),
                new SqlSegment(SqlSegmentKind.Token, "filter"),
                Literal(";"),
            ]);
    }

    [Fact]
    public void Read_ValidationGeneratorParameters_GiveEachQueryItsOwnOrTheFilesOrNone()
    {
        const string Text =
            "-- generator: no-token-validation\n-- name: FromFile\nSELECT {{a}};\n"
            + "-- name: Own\n-- generator: token-validation\nSELECT {{b}};\n";

        var file = Read(Text);

        file.Errors.ShouldBeEmpty();
        file.Queries.Select(query => query.TokenValidation).ShouldBe([false, true]);
        Read("SELECT {{a}};\n").Queries.ShouldHaveSingleItem().TokenValidation.ShouldBeNull();
    }

    [Fact]
    public void Read_IgnoredToken_IsLiteralTextOfAConstant()
    {
        var file = Read("-- token-ignore: raw\nSELECT '{{raw}}';\n");

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

    [Fact]
    public void Read_Dialect_IsTheDialectTheFileIsParsedWith()
    {
        const string Text = "-- name: A\nSELECT 'a\\'b' # c\n";

        Read(Text, dialect: SqlDialect.MySql)
            .Queries.ShouldHaveSingleItem()
            .Segments.ShouldBe(TestModels.Array(Literal("SELECT 'a\\'b'")));
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
    public void Read_FileDialect_ReadsTheFileWithItsDialectAndCarriesItsInvalidValue()
    {
        var text = new InMemoryAdditionalText(Path, "SELECT 1 # c\n");

        var file = SqlFileReader.Read(
            new FileDialect(text, SqlDialect.MySql, "nope"),
            "app/Repo/Users.sql",
            TestContext.Current.CancellationToken
        );

        file.Queries.ShouldHaveSingleItem().Segments.ShouldBe(TestModels.Array(Literal("SELECT 1")));
        file.InvalidDialect.ShouldBe("nope");
    }

    private static ParsedSqlFile Read(
        string? text,
        string path = Path,
        SqlDialect dialect = SqlDialect.Ansi,
        string? invalidDialect = null
    ) =>
        SqlFileReader.Read(
            new InMemoryAdditionalText(path, text),
            SqlPath.Normalize(path)!,
            dialect,
            invalidDialect,
            TestContext.Current.CancellationToken
        );

    private static LocationInfo Location(TextSpan span, int startLine, int startColumn, int endLine, int endColumn) =>
        new(
            Path,
            span,
            new LinePositionSpan(new LinePosition(startLine, startColumn), new LinePosition(endLine, endColumn))
        );
}
