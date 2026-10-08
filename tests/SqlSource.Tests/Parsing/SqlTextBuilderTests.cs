using System;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlTextBuilderTests
{
    [Theory]
    [InlineData("SELECT 1 -- c", "SELECT 1")]
    [InlineData("SELECT 1 -- c\nFROM t -- d", "SELECT 1\nFROM t")]
    [InlineData("SELECT 1\n-- c\nFROM t", "SELECT 1\nFROM t")]
    [InlineData("-- c\nSELECT 1\n  -- d", "SELECT 1")]
    public void Build_Stripping_DeletesLineComments(string text, string expected) => Build(text).ShouldBe(expected);

    [Theory]
    [InlineData("SELECT/**/1", "SELECT 1")]
    [InlineData("SELECT a /* x\n y */ FROM t", "SELECT a   FROM t")]
    [InlineData("/* c */\nSELECT 1", "SELECT 1")]
    [InlineData("SELECT 1 /* a /* b */ c */", "SELECT 1")]
    public void Build_Stripping_ReplacesABlockCommentWithOneSpace(string text, string expected) =>
        Build(text).ShouldBe(expected);

    [Theory]
    [InlineData("SELECT 1\n\n  \n\t\nFROM t", "SELECT 1\nFROM t")]
    [InlineData("\n\nSELECT 1\n\n", "SELECT 1")]
    [InlineData("SELECT 1\n/* a */ -- b\nFROM t", "SELECT 1\nFROM t")]
    public void Build_Stripping_RemovesBlankLines(string text, string expected) => Build(text).ShouldBe(expected);

    [Theory]
    [InlineData("-- c\n/* d */")]
    [InlineData("")]
    [InlineData(" \n ")]
    public void Build_NothingButCommentsAndWhitespace_IsEmpty(string text) => Build(text).ShouldBeEmpty();

    [Theory]
    [InlineData("SELECT /*+ H */ 1 /*! M */")]
    [InlineData("SELECT '-- x', \"/* y */\", `-- z` FROM t")]
    [InlineData("SELECT $$ -- x $$")]
    [InlineData("  SELECT 1\n    FROM t")]
    [InlineData("SELECT  1   FROM\tt")]
    public void Build_Stripping_KeepsHintsQuotedRegionsAndSpacing(string text) => Build(text).ShouldBe(text);

    [Theory]
    [InlineData("SELECT 1\r\nFROM t\r\n", "SELECT 1\nFROM t")]
    [InlineData("SELECT 1\rFROM t\r", "SELECT 1\nFROM t")]
    [InlineData("SELECT 'a\r\nb', 'c\rd'", "SELECT 'a\nb', 'c\nd'")]
    [InlineData("SELECT 1\r\n-- summary: x\r\nFROM t", "SELECT 1\nFROM t")]
    [InlineData("/*+ a\r\n b */ 1", "/*+ a\n b */ 1")]
    public void Build_AnyLineTerminator_BecomesLineFeed(string text, string expected) => Build(text).ShouldBe(expected);

    [Theory]
    [InlineData("SELECT 1   \nFROM t\t", "SELECT 1\nFROM t")]
    [InlineData("SELECT 'a  \n\n  b'  \n", "SELECT 'a  \n\n  b'")]
    public void Build_TrailingBlanks_AreRemovedUnlessTheLineEndsInsideAQuotedRegion(string text, string expected) =>
        Build(text).ShouldBe(expected);

    // A hint can hold executable SQL (MySQL's /*! ... */), string literals included, so nothing inside it is cleaned.
    [Theory]
    [InlineData("/*+ a  \n\n b */")]
    [InlineData("SELECT /*! 'line1\n\nline2   \nx' */ 1")]
    public void Build_Hint_IsKeptAsWritten(string text)
    {
        Build(text).ShouldBe(text);
        Build(text, keepComments: true).ShouldBe(text);
    }

    [Theory]
    [InlineData("-- summary: x\nSELECT 1\n  -- generator: keep-comments\nFROM t\n-- name: Next", "SELECT 1\nFROM t")]
    [InlineData("SELECT 1\n-- summary: x   ", "SELECT 1")]
    [InlineData("\t-- summary: x\nSELECT 1", "SELECT 1")]
    [InlineData("-- dialect: mysql\nSELECT 1", "SELECT 1")]
    public void Build_MarkerLines_AreRemoved(string text, string expected)
    {
        Build(text).ShouldBe(expected);
        Build(text, keepComments: true).ShouldBe(expected);
    }

    // The old marker is an ordinary comment: removed by default, kept when comments are.
    [Fact]
    public void Build_LineOfTheOldMarker_IsAComment()
    {
        const string Text = "-- SqlSource: keep-comments\nSELECT 1";

        Build(Text).ShouldBe("SELECT 1");
        Build(Text, keepComments: true).ShouldBe(Text);
    }

    [Theory]
    [InlineData("SELECT 1 -- c\n/* b */\nFROM t")]
    [InlineData("-- c\nSELECT 1\n\nFROM t")]
    [InlineData("/* a\n\n b */ SELECT 1")]
    public void Build_Preserving_KeepsCommentsAndInnerBlankLines(string text) =>
        Build(text, keepComments: true).ShouldBe(text);

    [Theory]
    [InlineData("\n\nSELECT 1\n\nFROM t\n\n", "SELECT 1\n\nFROM t")]
    [InlineData("SELECT 1   \n-- c  ", "SELECT 1\n-- c")]
    [InlineData("/* a\r\n b */\r\nSELECT 1", "/* a\n b */\nSELECT 1")]
    [InlineData("-- summary: x\n-- keep\nSELECT 1\n-- generator: keep-comments", "-- keep\nSELECT 1")]
    [InlineData("SELECT 1\n-- summary: x\nFROM t", "SELECT 1\nFROM t")]
    public void Build_Preserving_StillCleansLineEndsAndOuterBlankLines(string text, string expected) =>
        Build(text, keepComments: true).ShouldBe(expected);

    [Fact]
    public void Build_NonAsciiText_IsKeptIntact() => Build("SELECT 'ñ😀' -- é").ShouldBe("SELECT 'ñ😀'");

    [Fact]
    public void Build_LexemeRange_UsesOnlyThatRange()
    {
        const string Text = "A\n-- name: X\nB\n";
        var lexemes = SqlLexer.Lex(Text, SqlDialectRules.Ansi).Lexemes;

        SqlTextBuilder.Build(Text, lexemes, 0, 1, keepComments: false).Text.ShouldBe("A");
        SqlTextBuilder.Build(Text, lexemes, 2, 3, keepComments: false).Text.ShouldBe("B");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToSourceSpan_SpanInBuiltText_MapsToTheSameTextInTheFile(bool keepComments)
    {
        const string Text = "-- c\r\n/* x */ SELECT {{a}} -- d\r\n\r\n-- summary: s\r\n  FROM {{b}}";
        var lexemes = SqlLexer.Lex(Text, SqlDialectRules.Ansi).Lexemes;

        var built = SqlTextBuilder.Build(Text, lexemes, 0, lexemes.Count, keepComments);

        foreach (var token in new[] { "{{a}}", "{{b}}" })
        {
            var inBuilt = new TextSpan(built.Text.IndexOf(token, StringComparison.Ordinal), token.Length);
            var inFile = new TextSpan(Text.IndexOf(token, StringComparison.Ordinal), token.Length);
            built.ToSourceSpan(inBuilt).ShouldBe(inFile);
        }
    }

    // Every character of the built text, other than a line break, must map to the place in the file it came from.  A
    // stripped block comment is the one exception: its single space maps to the start of the comment.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToSourceSpan_EveryCharacterOfTheBuiltText_MapsToItsPlaceInTheFile(bool keepComments)
    {
        const string Text =
            "  -- lead\r\n\r\n-- summary: s\r\nSELECT 'a  \r\n\r\n b', /* c */ x   \r\n"
            + "\t-- generator: token-ignore=q\r\n"
            + "\r\n  /*+ h\r\n  i */ FROM t -- d  \r\n   \r\nWHERE {{y}} = $$ z\n $$  \r\n";
        var lexemes = SqlLexer.Lex(Text, SqlDialectRules.Ansi).Lexemes;

        var built = SqlTextBuilder.Build(Text, lexemes, 0, lexemes.Count, keepComments);

        for (var index = 0; index < built.Text.Length; index++)
        {
            var character = built.Text[index];
            if (character == '\n')
            {
                continue;
            }

            var source = Text[built.ToSourceSpan(new TextSpan(index, 1)).Start];
            var isStrippedComment = !keepComments && character == ' ' && source == '/';
            (source == character || isStrippedComment).ShouldBeTrue($"offset {index} maps to '{source}'");
        }
    }

    [Theory]
    [InlineData("SELECT 1 # c\n# d\nFROM t # e", false, "SELECT 1\nFROM t")]
    [InlineData("SELECT 1 # c\n# d\nFROM t # e", true, "SELECT 1 # c\n# d\nFROM t # e")]
    public void Build_HashCommentOfMySql_IsTreatedAsALineComment(string text, bool keepComments, string expected) =>
        Build(text, keepComments, SqlDialectRules.MySql).ShouldBe(expected);

    [Fact]
    public void Build_LineHintOfOracle_IsCopiedAsWritten() =>
        Build("SELECT --+ FULL(e)  \n  1 -- c\nFROM e", rules: SqlDialectRules.Oracle)
            .ShouldBe("SELECT --+ FULL(e)  \n  1\nFROM e");

    [Fact]
    public void Build_HintOfMariaDb_IsCopiedAsWritten() =>
        Build("SELECT /*M! SQL_NO_CACHE */ 1 /* c */", rules: SqlDialectRules.MariaDb)
            .ShouldBe("SELECT /*M! SQL_NO_CACHE */ 1");

    private static string Build(string text, bool keepComments = false, SqlDialectRules? rules = null)
    {
        var lexed = SqlLexer.Lex(text, rules ?? SqlDialectRules.Ansi);
        lexed.Error.ShouldBeNull();
        return SqlTextBuilder.Build(text, lexed.Lexemes, 0, lexed.Lexemes.Count, keepComments).Text;
    }
}
