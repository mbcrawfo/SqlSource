using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Parsing.Tokens;
using Xunit;
using static SqlSource.Tests.Parsing.ParseOf;

namespace SqlSource.Tests.Parsing;

// The parser's tokens: as segments, reserved names, token markers and their defaults, token-ignore and the kept form.
public sealed class SqlFileParserTokenTests
{
    [Theory]
    // The query's own list decides, whatever the file's input says.
    [InlineData("-- name: Q\n-- generator: keep-comments\nSELECT 1 -- c\n", false, "SELECT 1 -- c")]
    [InlineData("-- name: Q\n-- generator: sort-input\nSELECT 1 -- c\n", true, null)]
    [InlineData("-- name: Q\n-- generator: default\nSELECT 1 -- c\n", true, null)]
    // The preamble's list, for a query without one.
    [InlineData("-- generator: keep-comments\n-- name: Q\nSELECT 1 -- c\n", false, "SELECT 1 -- c")]
    [InlineData("-- generator: keep-comments\n-- name: Q\n-- generator: default\nSELECT 1 -- c\n", true, null)]
    [InlineData("-- generator: sort-input\n-- name: Q\nSELECT 1 -- c\n", true, null)]
    // No list at all: the file's input.
    [InlineData("-- name: Q\nSELECT 1 -- c\n", true, "SELECT 1 -- c")]
    [InlineData("-- name: Q\nSELECT 1 -- c\n", false, null)]
    // Nothing to keep: one form serves.
    [InlineData("-- name: Q\nSELECT 1\n", true, null)]
    public void Parse_KeptForm_IsBuiltOnlyWhenItMayBeWanted(string text, bool commentsWanted, string? expected)
    {
        var block = Block(text, commentsWanted);

        Sql(block).ShouldBe("SELECT 1");
        Kept(block).ShouldBe(expected);
    }

    [Fact]
    public void Parse_TokenThatStandsOnlyInAComment_IsATokenOfTheKeptFormAlone()
    {
        var block = Block("-- name: Q\n-- generator: keep-comments\nSELECT 1 /* {{note}} */ FROM {{t:users}}\n", false);

        Sql(block).ShouldBe("SELECT 1   FROM {{t}}");
        Kept(block).ShouldBe("SELECT 1 /* {{note}} */ FROM {{t}}");
        Tokens(block).ShouldBe(["t=users"]);
    }

    [Theory]
    [InlineData("SELECT {{class}} -- c\n", "{{class}}")]
    [InlineData("SELECT 1 -- {{class}}\n", "{{class}}")]
    public void Parse_ReservedTokenInEitherForm_IsReportedOnce(string sql, string at)
    {
        var text = "-- name: Q\n-- generator: keep-comments\n" + sql;

        Errors(text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, TextSpans.Of(text, at), "class")]);
    }

    [Fact]
    public void Parse_Tokens_BecomeSegmentsInOrder()
    {
        const string Text = "-- name: A\nSELECT * FROM {{table}} WHERE {{ col }} = 1 AND {{table}}.x = 2";

        Blocks(Text)
            .ShouldHaveSingleItem()
            .Segments.ShouldBe([
                new SqlSegment(SqlSegmentKind.Literal, "SELECT * FROM "),
                new SqlSegment(SqlSegmentKind.Token, "table"),
                new SqlSegment(SqlSegmentKind.Literal, " WHERE "),
                new SqlSegment(SqlSegmentKind.Token, "col"),
                new SqlSegment(SqlSegmentKind.Literal, " = 1 AND "),
                new SqlSegment(SqlSegmentKind.Token, "table"),
                new SqlSegment(SqlSegmentKind.Literal, ".x = 2"),
            ]);
    }

    [Fact]
    public void Parse_TokenInAStrippedComment_IsNotAToken()
    {
        const string Text = "-- name: A\nSELECT 1 -- {{x}}\n/* {{y}} */\n";

        Blocks(Text).ShouldHaveSingleItem().Segments.ShouldBe([new SqlSegment(SqlSegmentKind.Literal, "SELECT 1")]);
    }

    [Fact]
    public void Parse_TokenInAStringLiteralOrAPreservedComment_IsAToken()
    {
        const string Text = "-- name: A\n-- generator: keep-comments\nSELECT '{{a}}' -- {{b}}";

        var block = Blocks(Text).ShouldHaveSingleItem();

        TokenNames(block.Segments).ShouldBe(["a"]);
        TokenNames(block.KeptSegments.ShouldNotBeNull()).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Parse_ReservedKeywordToken_IsAnErrorAtTheTokenInTheFile()
    {
        const string Text = "-- name: A\r\n/* c */ SELECT 1 -- x\r\n\r\n-- summary: s\r\n  FROM {{ class }}";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, TextSpans.Of(Text, "{{ class }}"), "class"),
            ]);
    }

    [Fact]
    public void Parse_ReservedKeywordTokenAroundAStrippedComment_IsReportedOverTheWholeToken()
    {
        const string Token = "{{ /* c */ class }}";
        const string Text = "-- name: A\nSELECT " + Token;

        Errors(Text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, TextSpans.Of(Text, Token), "class")]);
    }

    // The first token is in both forms of the SQL and the second in the kept form alone.
    [Fact]
    public void Parse_ReservedKeywordTokensUnderKeepComments_AreEachReportedOnce()
    {
        const string Text = "-- name: A\n-- generator: keep-comments\nSELECT {{class}} -- {{ class }}\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, TextSpans.Of(Text, "{{class}}"), "class"),
                SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, TextSpans.Of(Text, "{{ class }}"), "class"),
            ]);
    }

    [Fact]
    public void Parse_ReservedKeywordTokenThatIsIgnored_IsLiteral()
    {
        const string Text = "-- name: A\n-- token-ignore: class\nSELECT {{class}}";

        Sql(Blocks(Text).ShouldHaveSingleItem()).ShouldBe("SELECT {{class}}");
    }

    private static string[] Tokens(SqlBlock block) =>
        [.. block.Tokens.Select(static token => token.Name + "=" + (token.Default ?? "<none>"))];

    [Fact]
    public void Parse_Tokens_AreListedOnceInOrderOfFirstAppearanceWithTheirDefaults()
    {
        const string Text =
            "-- name: Q\n-- token: {{filter:AND x = 1}}\nSELECT {{cols}} FROM {{table:users}} "
            + "WHERE 1 = 1 {{filter}} {{table}} {{filter}}\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        Tokens(block).ShouldBe(["cols=<none>", "table=users", "filter=AND x = 1"]);
        Sql(block).ShouldBe("SELECT {{cols}} FROM {{table}} WHERE 1 = 1 {{filter}} {{table}} {{filter}}");
    }

    [Fact]
    public void Parse_TokenMarkerForATokenTheQueryLacks_IsNotAnError() =>
        Tokens(Blocks("-- name: Q\n-- token: {{other:x}}\nSELECT {{a}}\n").ShouldHaveSingleItem())
            .ShouldBe(["a=<none>"]);

    [Fact]
    public void Parse_TheSameDefaultTwice_IsNotAConflict() =>
        Tokens(
                Blocks("-- name: Q\n-- token: {{a:x}}\n-- token: {{a:x}}\nSELECT {{a:x}} {{a: x }}\n")
                    .ShouldHaveSingleItem()
            )
            .ShouldBe(["a=x"]);

    [Theory]
    // Two inline defaults: at the second.
    [InlineData("-- name: Q\nSELECT {{a:x}} {{a:y}}\n", "{{a:y}}", "{{a:y}}")]
    // Two markers: at the second marker's value.
    [InlineData("-- name: Q\n-- token: {{a:x}}\n-- token: {{a:y}}\nSELECT {{a}}\n", "{{a:y}}", "token: {{a:y}}")]
    // A marker, then an inline default: at the inline one.
    [InlineData("-- name: Q\n-- token: {{a:x}}\nSELECT {{a:y}}\n", "{{a:y}}", "{{a:y}}")]
    // An inline default, then a marker: at the marker's value.
    [InlineData("-- name: Q\nSELECT {{a:y}}\n-- token: {{a:x}}\nFROM t\n", "{{a:x}}", "token: {{a:x}}")]
    public void Parse_TwoDefaultsForOneTokenThatDiffer_ConflictAtTheSecond(string text, string at, string argument) =>
        Errors(text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.ConflictingSettings, TextSpans.Of(text, at), argument)]);

    [Theory]
    [InlineData("{{a}}")]
    [InlineData("{{a:x}} y")]
    [InlineData("x {{a:y}}")]
    [InlineData("{{a:x}}{{b:y}}")]
    [InlineData("see below")]
    [InlineData("{{a:'open}}")]
    [InlineData("{{a:/* open}}")]
    public void Parse_TokenMarkerThatIsNotOneTokenWithADefault_IsInvalid(string value)
    {
        var text = "-- name: Q\n-- token: " + value + "\nSELECT {{a}}\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(text, value),
                    "token: " + value,
                    ""
                ),
            ]);
    }

    [Fact]
    public void Parse_TokenMarkerWithAReservedName_IsAnError()
    {
        const string Text = "-- name: Q\n-- token: {{class:x}}\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, TextSpans.Of(Text, "{{class:x}}"), "class"),
            ]);
    }

    [Fact]
    public void Parse_TokenMarkerInAFileWithoutNameMarker_IsAllowed() =>
        Tokens(Blocks("-- token: {{a:x}}\nSELECT {{a}}\n").ShouldHaveSingleItem()).ShouldBe(["a=x"]);

    [Fact]
    public void Parse_TokenIgnoreMarkers_KeepEachNamedTokenAsText()
    {
        const string Text =
            "-- name: Q\n-- token-ignore: a\n-- TOKEN-IGNORE: b\nSELECT '{{a}}', '{{b:x}}', {{c}}\n"
            + "-- name: R\nSELECT {{a}}\n";

        var blocks = Blocks(Text);

        Sql(blocks[0]).ShouldBe("SELECT '{{a}}', '{{b:x}}', {{c}}");
        Tokens(blocks[0]).ShouldBe(["c=<none>"]);
        // One query's marker does not reach the next.
        Tokens(blocks[1]).ShouldBe(["a=<none>"]);
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("a-b")]
    [InlineData("{{a}}")]
    [InlineData("1a")]
    public void Parse_TokenIgnoreMarkerThatIsNotOneName_IsInvalid(string value)
    {
        var text = "-- name: Q\n-- token-ignore: " + value + "\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(text, value),
                    "token-ignore: " + value,
                    ""
                ),
            ]);
    }

    [Fact]
    public void Parse_TokenIgnoreAsAGeneratorParameter_IsNotKnown()
    {
        const string Text = "-- name: Q\n-- generator: token-ignore=a\nSELECT {{a}}\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.UnknownGeneratorParameter,
                    TextSpans.Of(Text, "token-ignore=a"),
                    "token-ignore=a"
                ),
            ]);
    }

    private static string[] TokenNames(EquatableArray<SqlSegment> segments) =>
        [
            .. segments
                .Where(static segment => segment.Kind == SqlSegmentKind.Token)
                .Select(static segment => segment.Text),
        ];

    private static SqlBlock Block(string text, bool commentsWanted) =>
        SqlFileParser.Parse(text, "Query.sql", default, commentsWanted).Blocks.ShouldHaveSingleItem();
}
