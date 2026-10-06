using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlFileParserTests
{
    [Fact]
    public void Parse_FileWithoutNameMarker_IsOneBlockNamedAfterTheFile()
    {
        var block = Blocks("SELECT 1;\n", "GetUser.sql").ShouldHaveSingleItem();

        block.Name.ShouldBe("GetUser");
        block.NameSpan.ShouldBe(new TextSpan(0, 0));
        block.Summary.ShouldBeNull();
        block.KeepComments.ShouldBeFalse();
        block.TokenValidation.ShouldBeNull();
        block.Segments.ShouldBe([new SqlSegment(SqlSegmentKind.Literal, "SELECT 1;")]);
    }

    [Fact]
    public void Parse_FileWithoutNameMarker_ReadsSummaryAndDirectivesBeforeAndAmongItsSql()
    {
        const string Text =
            "-- summary: First.\nSELECT 1 -- c\n-- SqlSource: keep-comments no-token-validation\n"
            + "-- summary: Second.\nFROM t\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        block.Summary.ShouldBe("First. Second.");
        block.KeepComments.ShouldBeTrue();
        block.TokenValidation.ShouldBe(false);
        Sql(block).ShouldBe("SELECT 1 -- c\nFROM t");
    }

    [Theory]
    [InlineData("Query.sql", "Query")]
    [InlineData("Query.SQL", "Query")]
    [InlineData("Query", "Query")]
    [InlineData("_q1.sql", "_q1")]
    [InlineData("where.sql", "where")]
    public void Parse_FileWithoutNameMarker_TakesTheNameUpToTheLastDot(string fileName, string expected) =>
        Blocks("SELECT 1", fileName).ShouldHaveSingleItem().Name.ShouldBe(expected);

    [Theory]
    [InlineData("get-user.sql")]
    [InlineData("001_init.sql")]
    [InlineData("class.sql")]
    [InlineData("a.b.sql")]
    [InlineData(".sql")]
    [InlineData("")]
    public void Parse_FileWithoutNameMarkerAndUnusableFileName_IsAnError(string fileName)
    {
        Errors("SELECT 1", fileName)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidFileName, new TextSpan(0, 0), fileName)]);
    }

    [Fact]
    public void Parse_FileWithNameMarkers_IgnoresTheFileName() =>
        Blocks("-- name: A\nSELECT 1", "001-not-a-name.sql").ShouldHaveSingleItem().Name.ShouldBe("A");

    [Fact]
    public void Parse_NameMarkers_SplitTheFileIntoBlocks()
    {
        const string Text = "-- name: GetUser\nSELECT 1;\n\n  -- name: ListUsers\n  SELECT 2;\n";

        var blocks = Blocks(Text);

        blocks.Select(static block => block.Name).ShouldBe(["GetUser", "ListUsers"]);
        blocks.Select(Sql).ShouldBe(["SELECT 1;", "  SELECT 2;"]);
        blocks[0].NameSpan.ShouldBe(SpanOf(Text, "GetUser"));
        blocks[1].NameSpan.ShouldBe(SpanOf(Text, "ListUsers"));
    }

    [Theory]
    [InlineData("-- name: a\nSELECT 1\n-- name: A\nSELECT 2", "a", "A")]
    [InlineData("-- name: where\nSELECT 1\n-- name: Größe\nSELECT 2", "where", "Größe")]
    public void Parse_UnusualButValidNames_AreAccepted(string text, string first, string second) =>
        Blocks(text).Select(static block => block.Name).ShouldBe([first, second]);

    [Fact]
    public void Parse_Preamble_DiscardsItsCommentsEvenWhenPreserving()
    {
        const string Text =
            "-- Copyright\n/* header */\n-- SqlSource: keep-comments\n\n-- name: A\n-- kept\nSELECT 1\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        block.KeepComments.ShouldBeTrue();
        Sql(block).ShouldBe("-- kept\nSELECT 1");
    }

    [Fact]
    public void Parse_PreambleDirectives_ApplyToEveryBlock()
    {
        const string Text =
            "-- SqlSource: no-token-validation token-ignore=x\n"
            + "-- name: A\nSELECT {{x}}\n"
            + "-- name: B\nSELECT {{x}} {{y}}\n";

        var blocks = Blocks(Text);

        blocks[0].TokenValidation.ShouldBe(false);
        blocks[1].TokenValidation.ShouldBe(false);
        blocks[0].Segments.ShouldBe([new SqlSegment(SqlSegmentKind.Literal, "SELECT {{x}}")]);
        blocks[1]
            .Segments.ShouldBe([
                new SqlSegment(SqlSegmentKind.Literal, "SELECT {{x}} "),
                new SqlSegment(SqlSegmentKind.Token, "y"),
            ]);
    }

    [Fact]
    public void Parse_BlockValidationDirective_OverridesThePreamble()
    {
        const string Text =
            "-- SqlSource: no-token-validation\n"
            + "-- name: A\n-- SqlSource: token-validation\nSELECT 1\n"
            + "-- name: B\nSELECT 2\n";

        var blocks = Blocks(Text);

        blocks[0].TokenValidation.ShouldBe(true);
        blocks[1].TokenValidation.ShouldBe(false);
    }

    [Fact]
    public void Parse_BlockTokenIgnore_AddsToThePreamble()
    {
        const string Text =
            "-- SqlSource: token-ignore=x\n-- name: A\n-- SqlSource: token-ignore=y\nSELECT {{x}} {{y}} {{z}}\n";

        Blocks(Text)
            .ShouldHaveSingleItem()
            .Segments.ShouldBe([
                new SqlSegment(SqlSegmentKind.Literal, "SELECT {{x}} {{y}} "),
                new SqlSegment(SqlSegmentKind.Token, "z"),
            ]);
    }

    [Fact]
    public void Parse_BlockDirective_DoesNotLeakIntoTheNextBlock()
    {
        const string Text =
            "-- name: A\n-- SqlSource: keep-comments token-validation token-ignore=x\nSELECT 1 -- c\n"
            + "-- name: B\nSELECT {{x}} -- c\n";

        var blocks = Blocks(Text);

        blocks[1].KeepComments.ShouldBeFalse();
        blocks[1].TokenValidation.ShouldBeNull();
        Sql(blocks[1]).ShouldBe("SELECT {{x}}");
        blocks[1].Segments[1].ShouldBe(new SqlSegment(SqlSegmentKind.Token, "x"));
    }

    [Theory]
    [InlineData("SELECT 0;\nSELECT 9;\n-- name: A\nSELECT 1", "SELECT 0;\nSELECT 9;")]
    [InlineData("  'x' y\n/*+ h */\n-- name: A\nSELECT 1", "'x'")]
    [InlineData("-- c\n/*+ h */ z\n-- name: A\nSELECT 1", "/*+ h */")]
    public void Parse_SqlInThePreamble_IsReportedOnceAtItsStart(string text, string content) =>
        Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.SqlBeforeFirstName, SpanOf(text, content))]);

    [Fact]
    public void Parse_SummaryInThePreamble_IsAnError()
    {
        const string Text = "-- summary: nope\n-- name: A\nSELECT 1";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.SummaryBeforeFirstName, SpanOf(Text, "-- summary: nope")),
            ]);
    }

    [Theory]
    [InlineData("1abc")]
    [InlineData("get-user")]
    [InlineData("class")]
    [InlineData("A B")]
    [InlineData("@class")]
    [InlineData("A -- note")]
    [InlineData("😀")]
    [InlineData("A\u200B")]
    public void Parse_UnusableName_IsAnErrorAtTheName(string name)
    {
        var text = $"-- name: {name}\nSELECT 1";

        Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidName, SpanOf(text, name), name)]);
    }

    [Fact]
    public void Parse_NameMarkerWithoutAName_IsAnErrorAtTheMarker()
    {
        const string Text = "-- name:  \nSELECT 1";

        Errors(Text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidName, SpanOf(Text, "-- name:  "), string.Empty)]);
    }

    [Fact]
    public void Parse_RepeatedName_IsAnErrorAtTheSecondOccurrence()
    {
        const string Text = "-- name: A\nSELECT 1\n-- name: A\nSELECT 2";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.DuplicateName, new TextSpan(Text.LastIndexOf('A'), 1), "A"),
            ]);
    }

    [Theory]
    [InlineData("-- name: A\n-- name: B\nSELECT 1")]
    [InlineData("-- name: A\n-- c\n/* d */\n\n-- name: B\nSELECT 1")]
    [InlineData("-- name: A\n-- SqlSource: keep-comments\n-- c\n-- name: B\nSELECT 1")]
    [InlineData("-- name: B\nSELECT 1\n-- name: A")]
    [InlineData("-- name: B\nSELECT 1\n-- name: A\n-- summary: s\n")]
    public void Parse_BlockWithoutSql_IsAnErrorAtItsName(string text)
    {
        var errors = Errors(text);

        errors.ShouldBe([SqlParseError.Create(SqlParseErrorKind.EmptyBlock, SpanOf(text, "A"))]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \r\n\t")]
    [InlineData("-- only a comment\n/* and another */")]
    [InlineData("-- SqlSource: keep-comments\n-- c")]
    public void Parse_FileWithoutNameMarkerOrSql_IsAnErrorAtTheStart(string text) =>
        Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.EmptyBlock, new TextSpan(0, 0))]);

    [Fact]
    public void Parse_BlockHoldingOnlyAHint_IsNotEmpty() =>
        Sql(Blocks("-- name: A\n/*+ H */").ShouldHaveSingleItem()).ShouldBe("/*+ H */");

    [Theory]
    [InlineData("-- name: A\nSELECT 1\n\n-- summary: Loads B.\n-- name: B\nSELECT 2", "-- summary: Loads B.")]
    [InlineData(
        "-- name: A\nSELECT 1\n  -- SqlSource: token-ignore=x\n-- name: B\nSELECT 2",
        "-- SqlSource: token-ignore=x"
    )]
    [InlineData("-- name: A\nSELECT 1\n-- summary: last", "-- summary: last")]
    [InlineData("SELECT 1\n-- SqlSource: keep-comments\n", "-- SqlSource: keep-comments")]
    [InlineData("-- name: A\nSELECT 1\n-- summary: s\n-- a comment\n/* another */\n", "-- summary: s")]
    [InlineData("-- name: A\nSELECT 1\n-- SqlSource: bogus", "-- SqlSource: bogus")]
    [InlineData("-- name: A\nSELECT 1\n-- summary:", "-- summary:")]
    public void Parse_MarkerAfterTheLastSqlOfItsBlock_IsAnErrorAtTheMarker(string text, string marker) =>
        Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.MarkerAtEndOfBlock, SpanOf(text, marker))]);

    [Fact]
    public void Parse_SeveralMarkersAfterTheLastSqlOfABlock_AreEachAnError()
    {
        const string Text =
            "-- name: A\nSELECT 1\n-- SqlSource: no-token-validation\n-- summary: s\n-- name: B\nSELECT 2";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.MarkerAtEndOfBlock,
                    SpanOf(Text, "-- SqlSource: no-token-validation")
                ),
                SqlParseError.Create(SqlParseErrorKind.MarkerAtEndOfBlock, SpanOf(Text, "-- summary: s")),
            ]);
    }

    [Theory]
    [InlineData("-- name: A\nSELECT 1\n-- summary: s\n-- SqlSource: no-token-validation\nFROM t", "SELECT 1\nFROM t")]
    [InlineData("-- name: A\n-- summary: s\n-- SqlSource: no-token-validation\n/*+ H */", "/*+ H */")]
    [InlineData("-- name: A\n-- summary: s\n-- SqlSource: no-token-validation\n'x'\n-- c", "'x'")]
    public void Parse_MarkerWithSqlAfterItInItsBlock_IsAccepted(string text, string expectedSql)
    {
        var block = Blocks(text).ShouldHaveSingleItem();

        block.Summary.ShouldBe("s");
        block.TokenValidation.ShouldBe(false);
        Sql(block).ShouldBe(expectedSql);
    }

    [Fact]
    public void Parse_SummaryMarkers_AreJoinedWithOneSpace()
    {
        const string Text = "-- name: A\n-- summary: One.\n-- summary:\n-- summary:   Two.  \nSELECT 1\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        block.Summary.ShouldBe("One. Two.");
        Sql(block).ShouldBe("SELECT 1");
    }

    [Fact]
    public void Parse_BlockWithOnlyEmptySummaryMarkers_HasNoSummary() =>
        Blocks("-- name: A\n-- summary:\nSELECT 1").ShouldHaveSingleItem().Summary.ShouldBeNull();

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
        const string Text = "-- name: A\n-- SqlSource: keep-comments\nSELECT '{{a}}' -- {{b}}";

        Blocks(Text)
            .ShouldHaveSingleItem()
            .Segments.Where(static segment => segment.Kind == SqlSegmentKind.Token)
            .Select(static segment => segment.Text)
            .ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Parse_ReservedKeywordToken_IsAnErrorAtTheTokenInTheFile()
    {
        const string Text = "-- name: A\r\n/* c */ SELECT 1 -- x\r\n\r\n-- summary: s\r\n  FROM {{ class }}";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, SpanOf(Text, "{{ class }}"), "class"),
            ]);
    }

    [Fact]
    public void Parse_ReservedKeywordTokenAroundAStrippedComment_IsReportedOverTheWholeToken()
    {
        const string Token = "{{ /* c */ class }}";
        const string Text = "-- name: A\nSELECT " + Token;

        Errors(Text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, SpanOf(Text, Token), "class")]);
    }

    [Fact]
    public void Parse_ReservedKeywordTokenThatIsIgnored_IsLiteral()
    {
        const string Text = "-- name: A\n-- SqlSource: token-ignore=class\nSELECT {{class}}";

        Sql(Blocks(Text).ShouldHaveSingleItem()).ShouldBe("SELECT {{class}}");
    }

    [Theory]
    [InlineData("/*\n-- name: A\n*/\nSELECT 1", "SELECT 1")]
    [InlineData("SELECT 1 -- name: A", "SELECT 1")]
    [InlineData("SELECT '\n-- name: A\n'", "SELECT '\n-- name: A\n'")]
    [InlineData("-- name : A\nSELECT 1", "SELECT 1")]
    public void Parse_NameMarkerLookAlike_DoesNotStartABlock(string text, string expectedSql)
    {
        var block = Blocks(text).ShouldHaveSingleItem();

        block.Name.ShouldBe("Query");
        Sql(block).ShouldBe(expectedSql);
    }

    [Theory]
    [InlineData("-- SqlSource: keep-comment", nameof(SqlParseErrorKind.UnknownDirective), "keep-comment")]
    [InlineData("-- SqlSource:", nameof(SqlParseErrorKind.EmptyDirectiveLine), "-- SqlSource:")]
    [InlineData("-- SqlSource: token-ignore=", nameof(SqlParseErrorKind.InvalidDirectiveValue), "token-ignore=")]
    [InlineData(
        "-- SqlSource: token-validation no-token-validation",
        nameof(SqlParseErrorKind.ConflictingDirectives),
        "no-token-validation"
    )]
    public void Parse_DirectiveProblem_IsReportedAtItsPlaceInTheFile(string line, string kind, string place)
    {
        var text = "-- name: A\n" + line + "\nSELECT 1";

        var error = Errors(text).ShouldHaveSingleItem();

        error.Kind.ToString().ShouldBe(kind);
        error.Span.ShouldBe(SpanOf(text, place));
    }

    [Theory]
    [InlineData("-- name: 1x\nSELECT 'abc", nameof(SqlParseErrorKind.UnterminatedQuote))]
    [InlineData("-- name: 1x\nSELECT /* abc", nameof(SqlParseErrorKind.UnterminatedBlockComment))]
    public void Parse_LexerError_IsTheOnlyErrorReported(string text, string kind) =>
        Errors(text).ShouldHaveSingleItem().Kind.ToString().ShouldBe(kind);

    [Fact]
    public void Parse_SeveralErrors_AreAllReportedInFileOrderAndNoBlocksAreReturned()
    {
        const string Text =
            "-- name: 1x\nSELECT 1\n"
            + "-- name: B\n-- SqlSource: bogus\n"
            + "-- name: C\nSELECT {{class}}\n"
            + "-- name: C\nSELECT 3\n";

        var errors = Errors(Text);

        errors
            .Select(static error => error.Kind)
            .ShouldBe([
                SqlParseErrorKind.InvalidName,
                SqlParseErrorKind.EmptyBlock,
                SqlParseErrorKind.UnknownDirective,
                SqlParseErrorKind.ReservedTokenName,
                SqlParseErrorKind.DuplicateName,
            ]);
        errors.Select(static error => error.Span.Start).ShouldBeInOrder();
    }

    [Fact]
    public void Parse_MarkerWithUnusableName_StillStartsABlock()
    {
        Errors("-- name: 1x\nSELECT 1\n-- name: B\nSELECT 2")
            .ShouldHaveSingleItem()
            .Kind.ShouldBe(SqlParseErrorKind.InvalidName);
    }

    [Fact]
    public void Parse_WindowsLineEndings_GiveTheSameBlocksAsUnixLineEndings()
    {
        const string Unix =
            "-- SqlSource: no-token-validation\n\n-- name: A\n-- summary: S\nSELECT 1 -- c\nFROM {{t}}\n\n"
            + "-- name: B\nSELECT 'x\ny'\n";
        var windows = Unix.Replace("\n", "\r\n", StringComparison.Ordinal);

        var unixBlocks = Blocks(Unix);
        var windowsBlocks = Blocks(windows);

        windowsBlocks.Select(Sql).ShouldBe(["SELECT 1\nFROM {{t}}", "SELECT 'x\ny'"]);
        windowsBlocks.Select(Sql).ShouldBe(unixBlocks.Select(Sql));
        windowsBlocks.Select(static block => block.Summary).ShouldBe(["S", null]);
        windowsBlocks.Select(static block => block.TokenValidation).ShouldBe([false, false]);
    }

    [Theory]
    [InlineData("-- name: A\n-- summary: s\nSELECT {{a}} -- c\n-- name: B\nSELECT 2\n")]
    [InlineData("-- name: 1x\n-- SqlSource: bogus\nSELECT {{class}}\n")]
    [InlineData("SELECT 'abc")]
    public void Parse_SameTextTwice_GivesEqualResults(string text)
    {
        var first = SqlFileParser.Parse(text, "Query.sql");
        var second = SqlFileParser.Parse(new string(text.ToCharArray()), "Query.sql");

        second.ShouldNotBeSameAs(first);
        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void Parse_DifferentText_GivesUnequalResults() =>
        SqlFileParser.Parse("SELECT 1", "Query.sql").ShouldNotBe(SqlFileParser.Parse("SELECT 2", "Query.sql"));

    private static SqlBlock[] Blocks(string text, string fileName = "Query.sql")
    {
        var result = SqlFileParser.Parse(text, fileName);
        result.Errors.ShouldBeEmpty();
        return [.. result.Blocks];
    }

    private static SqlParseError[] Errors(string text, string fileName = "Query.sql")
    {
        var result = SqlFileParser.Parse(text, fileName);
        result.Blocks.ShouldBeEmpty();
        return [.. result.Errors];
    }

    private static string Sql(SqlBlock block) =>
        string.Concat(
            block.Segments.Select(static segment =>
                segment.Kind == SqlSegmentKind.Token ? "{{" + segment.Text + "}}" : segment.Text
            )
        );

    private static TextSpan SpanOf(string text, string value) =>
        new(text.IndexOf(value, StringComparison.Ordinal), value.Length);
}
