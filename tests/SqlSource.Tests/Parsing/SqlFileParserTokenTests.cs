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

    // The token is in both forms, and the kept form adds nothing for it: a file with an error has no blocks.
    [Fact]
    public void Parse_ReservedTokenInTheSqlUnderKeepComments_IsOneErrorOfTheFile()
    {
        const string Text = "-- name: Q\n-- generator: keep-comments\nSELECT {{class}} -- c\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, TextSpans.Of(Text, "{{class}}"), "class"),
            ]);
    }

    // The token is in the kept form alone, which a type that claims the file may not emit, so its error stays with
    // the block.
    [Fact]
    public void Parse_ReservedTokenInAKeptCommentAlone_IsAnErrorOfTheKeptForm()
    {
        const string Text = "-- name: Q\n-- generator: keep-comments\nSELECT 1 -- {{class}}\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        Sql(block).ShouldBe("SELECT 1");
        _ = block.KeptSegments.ShouldNotBeNull();
        block.KeptErrors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, TextSpans.Of(Text, "{{class}}"), "class"),
        ]);
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

    [Fact]
    public void Parse_ReservedKeywordTokenThatIsIgnored_IsLiteral()
    {
        const string Text = "-- name: A\n-- token-ignore: class\nSELECT {{class}}";

        Sql(Blocks(Text).ShouldHaveSingleItem()).ShouldBe("SELECT {{class}}");
    }

    // The comment is stripped with the braces it holds, so the default of f would run on to the braces of o and take
    // that token with it.
    [Fact]
    public void Parse_LineCommentThatHoldsTheClosingBraces_IsReported()
    {
        const string Text = "-- name: Q\nSELECT 1 {{f:AND a = 1 -- note}}\nORDER BY {{o:name}}\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, TextSpans.Of(Text, "-- note}}"), "f"),
            ]);
    }

    [Theory]
    [InlineData("SELECT 1 {{f:a -- note}}", "-- note}}", "f")]
    [InlineData("SELECT {{f:a /* }} */ b}}", "/* }} */", "f")]
    [InlineData("SELECT {{f:a\n-- }}\n}}", "-- }}", "f")]
    [InlineData("SELECT {{f:a}} {{g: b -- }}", "-- }}", "g")]
    [InlineData("-- token-ignore: f\nSELECT {{f:a -- }}", "-- }}", "f")]
    [InlineData("SELECT {{f:a -- }}\r\nORDER BY {{g:b}}\r", "-- }}", "f")]
    public void Parse_CommentThatHoldsBracesInsideAnOpenDefault_IsReported(string sql, string comment, string name)
    {
        var text = "-- name: Q\n" + sql + "\n";

        Errors(text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, TextSpans.Of(text, comment), name)]);
    }

    // The scanner reads an ignored token to the next "}}" as it does any other, so a hidden "}}" takes g with it.
    [Fact]
    public void Parse_CommentThatHoldsTheBracesOfAnIgnoredTokenBeforeAnotherToken_IsReported()
    {
        const string Text = "-- name: Q\n-- token-ignore: f\nSELECT {{f:a -- }}\nORDER BY {{g:b}}\n";

        Errors(Text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, TextSpans.Of(Text, "-- }}"), "f")]);
    }

    // Under postgres a line comment may stand in the gap between the parts of a continued string.  The lexer gives
    // each part as a quoted region, its prefix in the text before it, and the comment as a line comment between them,
    // so it is reported as any other.
    [Fact]
    public void Parse_CommentInTheGapOfAContinuedStringInADefault_IsReported()
    {
        const string Text = "-- name: Q\nSELECT {{f:E'a' -- }}\n'b'}}\n";

        SqlLexer
            .Lex(Text, SqlDialectRules.PostgreSql)
            .Lexemes.Select(lexeme => lexeme.Kind + ":" + Text.Substring(lexeme.Span.Start, lexeme.Span.Length))
            .ShouldBe([
                "LineComment:-- name: Q",
                "Text:\nSELECT {{f:E",
                "Quoted:'a'",
                "Text: ",
                "LineComment:-- }}",
                "Text:\n",
                "Quoted:'b'",
                "Text:}}\n",
            ]);
        Errors(Text, dialect: SqlDialect.PostgreSql)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, TextSpans.Of(Text, "-- }}"), "f")]);
    }

    // A marker is a line comment, and is gone from the SQL as one, so its braces do not end the default either.
    [Fact]
    public void Parse_TokenMarkerInsideAnOpenDefault_IsReported()
    {
        const string Text = "-- name: Q\nSELECT {{f:a\n-- token: {{g:b}}\n}}\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.TokenCrossesComment,
                    TextSpans.Of(Text, "-- token: {{g:b}}"),
                    "f"
                ),
            ]);
    }

    // The comment sits between the braces and the colon, not inside the default, and its braces still do not end the
    // token.
    [Fact]
    public void Parse_CommentBetweenTheNameAndTheColonThatHoldsBraces_IsReported()
    {
        const string Text = "-- name: Q\nSELECT {{f/* }} */:a}}\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, TextSpans.Of(Text, "/* }} */"), "f"),
            ]);
    }

    // The mirror: a comment opens a default that the SQL after it closes.  Without comments the query is as written;
    // with them the default runs on and takes the SQL after the comment, so the error belongs to the kept form.
    [Theory]
    [InlineData(
        "SELECT * -- e.g. {{note: here\nFROM t WHERE {{w}}",
        "-- e.g. {{note: here",
        "note",
        "SELECT *\nFROM t WHERE {{w}}"
    )]
    [InlineData("SELECT {{f:a}} /* {{g: x */ FROM {{t}}", "/* {{g: x */", "g", "SELECT {{f}}   FROM {{t}}")]
    [InlineData("SELECT 1 /* {{a: */ x /* }} */", "/* {{a: */", "a", "SELECT 1   x")]
    [InlineData(
        "-- token-ignore: note\nSELECT * -- {{note: here\nFROM {{w}}",
        "-- {{note: here",
        "note",
        "SELECT *\nFROM {{w}}"
    )]
    public void Parse_CommentThatOpensADefaultThatTheSqlCloses_IsAnErrorOfTheKeptForm(
        string sql,
        string comment,
        string name,
        string stripped
    )
    {
        var text = "-- name: Q\n-- generator: keep-comments\n" + sql + "\n";

        var block = Blocks(text).ShouldHaveSingleItem();

        Sql(block).ShouldBe(stripped);
        block.KeptErrors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, TextSpans.Of(text, comment), name),
        ]);
    }

    [Theory]
    [InlineData("SELECT 1 -- {{x:y}}")]
    [InlineData("SELECT {{f:a /* note */ b}}")]
    [InlineData("SELECT {{f:a /* {{g: */ b}}")]
    [InlineData("SELECT 1 -- {{note: here")]
    [InlineData("SELECT 1 /*+ {{g: */ FROM {{t}}")]
    [InlineData("SELECT 1 -- {{note here\nFROM {{w}}")]
    public void Parse_CommentThatOpensNoDefaultThatTheSqlCloses_IsNoErrorOfTheKeptForm(string sql)
    {
        var block = Blocks("-- name: Q\n-- generator: keep-comments\n" + sql + "\n").ShouldHaveSingleItem();

        block.KeptErrors.ShouldBeEmpty();
    }

    // A comment in a token's head, before its default or its closing braces, is one space without comments, which a
    // head may hold, and is text that ends the head where comments are kept: the token is gone there.
    [Theory]
    [InlineData("SELECT {{f/* c */}}", "/* c */")]
    [InlineData("SELECT {{/* c */f}}", "/* c */")]
    [InlineData("SELECT {{f/* c */:a}}", "/* c */")]
    [InlineData("SELECT {{ f /* c\n d */ : a }}", "/* c\n d */")]
    public void Parse_CommentInATokensHead_IsAnErrorOfTheKeptForm(string sql, string comment)
    {
        var text = "-- name: Q\n-- generator: keep-comments\n" + sql + "\n";

        var block = Blocks(text).ShouldHaveSingleItem();

        TokenNames(block.Segments).ShouldBe(["f"]);
        TokenNames(block.KeptSegments.ShouldNotBeNull()).ShouldBeEmpty();
        block.KeptErrors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, TextSpans.Of(text, comment), "f"),
        ]);
    }

    [Fact]
    public void Parse_CommentsOnBothSidesOfATokensName_AreEachAnErrorOfTheKeptForm()
    {
        const string Text = "-- name: Q\n-- generator: keep-comments\nSELECT {{/* a */f/* b */}}, {{g}}\n";

        Blocks(Text)
            .ShouldHaveSingleItem()
            .KeptErrors.ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, TextSpans.Of(Text, "/* a */"), "f"),
                SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, TextSpans.Of(Text, "/* b */"), "f"),
            ]);
    }

    // The scan's error comes first from the checks and last in the file, so the errors are put in the file's order.
    [Fact]
    public void Parse_KeptErrorsOfSeveralChecks_AreInTheOrderOfTheFile()
    {
        const string Text = "-- name: Q\n-- generator: keep-comments\nSELECT {{f/* c */}} -- {{class}}\n";

        Blocks(Text)
            .ShouldHaveSingleItem()
            .KeptErrors.ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, TextSpans.Of(Text, "/* c */"), "f"),
                SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, TextSpans.Of(Text, "{{class}}"), "class"),
            ]);
    }

    // An ignored token is read to its closing braces as any other is, so a comment in its head changes what the two
    // forms read too: without comments the ignored f takes {{g}} into its text, with them f is no token and g is.
    [Theory]
    [InlineData("SELECT {{f/* c */:x {{g}} y}}", new string[0], new[] { "g" })]
    [InlineData("SELECT {{f/* c */}}", new string[0], new string[0])]
    [InlineData("SELECT {{f/* c */:x}}, {{h}}", new[] { "h" }, new[] { "h" })]
    public void Parse_CommentInTheHeadOfAnIgnoredToken_IsAnErrorOfTheKeptForm(
        string sql,
        string[] stripped,
        string[] kept
    )
    {
        var text = "-- name: Q\n-- generator: keep-comments\n-- token-ignore: f\n" + sql + "\n";

        var block = Blocks(text).ShouldHaveSingleItem();

        TokenNames(block.Segments).ShouldBe(stripped);
        TokenNames(block.KeptSegments.ShouldNotBeNull()).ShouldBe(kept);
        block.KeptErrors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, TextSpans.Of(text, "/* c */"), "f"),
        ]);
    }

    // A comment inside the default is in the default in both forms.  A hint is kept in both.  A line comment ends at
    // a line break, which no head may hold, so neither form has a token.
    [Theory]
    [InlineData("SELECT {{f:a/* c */}}")]
    [InlineData("-- token-ignore: f\nSELECT {{f:a/* c */}}")]
    [InlineData("SELECT {{f/*+ c */}}")]
    [InlineData("SELECT {{f -- c\n}}")]
    public void Parse_CommentOutsideATokensHead_IsNoErrorOfTheKeptForm(string sql) =>
        Blocks("-- name: Q\n-- generator: keep-comments\n" + sql + "\n")
            .ShouldHaveSingleItem()
            .KeptErrors.ShouldBeEmpty();

    // Where no level may keep comments the kept form is not built, so the mirror is not looked for.
    [Fact]
    public void Parse_CommentThatOpensADefaultWhereCommentsAreNotWanted_IsNoError()
    {
        var block = Block("-- name: Q\nSELECT * -- e.g. {{note: here\nFROM t WHERE {{w}}\n", commentsWanted: false);

        block.KeptSegments.ShouldBeNull();
        block.KeptErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_HashCommentOfMySqlThatHoldsTheClosingBraces_IsReported()
    {
        const string Text = "-- name: Q\nSELECT {{f:a # }}\n";

        Errors(Text, dialect: SqlDialect.MySql)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.TokenCrossesComment, TextSpans.Of(Text, "# }}"), "f")]);
    }

    [Theory]
    [InlineData("SELECT 1 -- {{x}}")]
    [InlineData("SELECT {{f:a}} -- see }}")]
    [InlineData("SELECT {{f:a -- note\n}}")]
    [InlineData("SELECT {{f:a /*+ }} */ b}}")]
    [InlineData("SELECT {{f:a # }}")]
    public void Parse_CommentThatHidesNoClosingBracesOfADefault_IsNotReported(string sql) =>
        Blocks("-- name: Q\n" + sql + "\n").ShouldHaveSingleItem();

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

    // A marker's default loses its comments as an inline one does: a block comment leaves one space, and the default
    // is trimmed.  A hint is not a comment, and both keep it.
    [Theory]
    [InlineData("a /* c */ b", "f=a   b")]
    [InlineData("/* c */ a", "f=a")]
    [InlineData("a /* c */", "f=a")]
    [InlineData("/* c */", "f=")]
    [InlineData("hint /*+ x */ b", "f=hint /*+ x */ b")]
    public void Parse_MarkerDefaultWithACommentBesideTheSameInlineDefault_IsOneDefault(string written, string token) =>
        Tokens(
                Blocks("-- name: Q\n-- token: {{f:" + written + "}}\nSELECT {{f:" + written + "}}\n")
                    .ShouldHaveSingleItem()
            )
            .ShouldBe([token]);

    [Fact]
    public void Parse_MarkerDefaultWithALineComment_IsTheDefaultWithoutIt() =>
        Tokens(Blocks("-- name: Q\n-- token: {{f:a -- c}}\nSELECT {{f}}\n").ShouldHaveSingleItem()).ShouldBe(["f=a"]);

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
