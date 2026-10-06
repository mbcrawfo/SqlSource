using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlLexerTests
{
    [Fact]
    public void Lex_EmptyText_ReturnsNoLexemes() => Lex(string.Empty).ShouldBeEmpty();

    [Fact]
    public void Lex_PlainSql_ReturnsOneTextLexeme() => Lex("SELECT 1").ShouldBe(["Text:SELECT 1"]);

    [Theory]
    [InlineData("a -- c\nb", "Text:\nb")]
    [InlineData("a -- c\r\nb", "Text:\r\nb")]
    [InlineData("a -- c\rb", "Text:\rb")]
    public void Lex_LineComment_StopsBeforeTheLineTerminator(string text, string rest) =>
        Lex(text).ShouldBe(["Text:a ", "LineComment:-- c", rest]);

    [Fact]
    public void Lex_LineCommentAtEndOfText_RunsToTheEnd() => Lex("a --c").ShouldBe(["Text:a ", "LineComment:--c"]);

    [Theory]
    [InlineData("'it''s' x", "Quoted:'it''s'")]
    [InlineData("\"a\"\"b\" x", "Quoted:\"a\"\"b\"")]
    [InlineData("`a``b` x", "Quoted:`a``b`")]
    [InlineData("'' x", "Quoted:''")]
    public void Lex_QuotedRegion_EndsAtTheUndoubledQuote(string text, string quoted) =>
        Lex(text).ShouldBe([quoted, "Text: x"]);

    [Theory]
    [InlineData("'-- x /* y */' z", "Quoted:'-- x /* y */'")]
    [InlineData("\"-- x /* y */\" z", "Quoted:\"-- x /* y */\"")]
    [InlineData("`-- x` z", "Quoted:`-- x`")]
    [InlineData("'a\n-- b\n' z", "Quoted:'a\n-- b\n'")]
    public void Lex_CommentSyntaxInsideQuotedRegion_IsNotAComment(string text, string quoted) =>
        Lex(text).ShouldBe([quoted, "Text: z"]);

    [Theory]
    [InlineData("E'it\\'s' -- c", "Text:E")]
    [InlineData("e'it\\'s' -- c", "Text:e")]
    public void Lex_EscapeString_TreatsBackslashAsAnEscape(string text, string prefix) =>
        Lex(text).ShouldBe([prefix, "Quoted:'it\\'s'", "Text: ", "LineComment:-- c"]);

    [Fact]
    public void Lex_EscapeStringWithDoubledQuote_EndsAtTheUndoubledQuote() =>
        Lex("E'a''b\\\\' x").ShouldBe(["Text:E", "Quoted:'a''b\\\\'", "Text: x"]);

    [Fact]
    public void Lex_LetterEAtTheEndOfAnIdentifier_IsNotAnEscapeStringPrefix() =>
        Lex("typeE'a\\' -- c").ShouldBe(["Text:typeE", "Quoted:'a\\'", "Text: ", "LineComment:-- c"]);

    [Theory]
    [InlineData("$$ -- x $$ y", "Quoted:$$ -- x $$")]
    [InlineData("$fn$ /* x */ $fn$ y", "Quoted:$fn$ /* x */ $fn$")]
    [InlineData("$a$ $b$ -- c $a$ y", "Quoted:$a$ $b$ -- c $a$")]
    [InlineData("$_t1$ ' $_t1$ y", "Quoted:$_t1$ ' $_t1$")]
    [InlineData("$größe$ -- x $größe$ y", "Quoted:$größe$ -- x $größe$")]
    public void Lex_DollarQuote_EndsAtTheSameTag(string text, string quoted) => Lex(text).ShouldBe([quoted, "Text: y"]);

    [Theory]
    [InlineData("v$session$ -- c", "Text:v$session$ ")]
    [InlineData("$1 -- c", "Text:$1 ")]
    [InlineData("$5.00 -- c", "Text:$5.00 ")]
    [InlineData("$$ -- c", "Text:$$ ")]
    [InlineData("$a$ -- c", "Text:$a$ ")]
    [InlineData("$a$ $b$ -- c", "Text:$a$ $b$ ")]
    public void Lex_DollarThatOpensNoQuote_IsText(string text, string expectedText) =>
        Lex(text).ShouldBe([expectedText, "LineComment:-- c"]);

    [Theory]
    [InlineData("/* a */ x", "BlockComment:/* a */")]
    [InlineData("/**/ x", "BlockComment:/**/")]
    [InlineData("/* a\n b */ x", "BlockComment:/* a\n b */")]
    [InlineData("/* a /* b */ c */ x", "BlockComment:/* a /* b */ c */")]
    [InlineData("/* it's -- \"x */ x", "BlockComment:/* it's -- \"x */")]
    [InlineData("/* /*+ h */ */ x", "BlockComment:/* /*+ h */ */")]
    public void Lex_BlockComment_EndsAtItsMatchingClose(string text, string comment) =>
        Lex(text).ShouldBe([comment, "Text: x"]);

    [Theory]
    [InlineData("/*+ INDEX(t) */ x", "Hint:/*+ INDEX(t) */")]
    [InlineData("/*! STRAIGHT_JOIN */ x", "Hint:/*! STRAIGHT_JOIN */")]
    [InlineData("/*+*/ x", "Hint:/*+*/")]
    public void Lex_BlockCommentStartingWithPlusOrBang_IsAHint(string text, string hint) =>
        Lex(text).ShouldBe([hint, "Text: x"]);

    [Theory]
    [InlineData("-- /* x\ny", "LineComment:-- /* x")]
    [InlineData("-- it's\ny", "LineComment:-- it's")]
    [InlineData("-- $$\ny", "LineComment:-- $$")]
    public void Lex_QuoteOrCommentSyntaxInsideLineComment_IsPartOfTheComment(string text, string comment) =>
        Lex(text).ShouldBe([comment, "Text:\ny"]);

    // The Lex_KnownLimit tests pin where the lexer reads SQL differently from some databases.  Each is deliberate.
    [Theory]
    [InlineData("# x -- y", "Text:# x ", "LineComment:-- y")]
    [InlineData("[a--b]", "Text:[a", "LineComment:--b]")]
    [InlineData("5--3", "Text:5", "LineComment:--3")]
    public void Lex_KnownLimit_FollowsAnsiRules(string text, string first, string second) =>
        Lex(text).ShouldBe([first, second]);

    [Fact]
    public void Lex_KnownLimit_BackslashInPlainStringIsNotAnEscape()
    {
        var result = SqlLexer.Lex("'a\\'b'");

        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(5, 1)));
    }

    [Fact]
    public void Lex_KnownLimit_CommentOpenerInsideCommentMustBeClosed()
    {
        var result = SqlLexer.Lex("/* a /* b */ SELECT 1");

        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedBlockComment, new TextSpan(0, 2)));
    }

    [Theory]
    [InlineData("'abc", 0)]
    [InlineData("x \"abc", 2)]
    [InlineData("`abc", 0)]
    [InlineData("'abc''", 0)]
    [InlineData("E'abc\\'", 1)]
    [InlineData("E'abc\\", 1)]
    public void Lex_UnterminatedQuote_ReportsTheOpeningQuote(string text, int position)
    {
        var result = SqlLexer.Lex(text);

        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(position, 1)));
        result.Lexemes.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData("SELECT /* x", 7)]
    [InlineData("/*+ x", 0)]
    [InlineData("/*/", 0)]
    [InlineData("/* /* x */", 0)]
    public void Lex_UnterminatedBlockComment_ReportsTheOpener(string text, int position)
    {
        var result = SqlLexer.Lex(text);

        result.Error.ShouldBe(
            SqlParseError.Create(SqlParseErrorKind.UnterminatedBlockComment, new TextSpan(position, 2))
        );
        result.Lexemes.Count.ShouldBe(0);
    }

    [Fact]
    public void Lex_AnyText_CoversItWithoutGaps()
    {
        const string Text = "SELECT 'a', \"b\" /* c */ -- d\r\nFROM $$e$$ /*+ f */ `g`\n";

        var lexemes = SqlLexer.Lex(Text).Lexemes;

        var position = 0;
        foreach (var lexeme in lexemes)
        {
            lexeme.Span.Start.ShouldBe(position);
            lexeme.Span.Length.ShouldBeGreaterThan(0);
            position = lexeme.Span.End;
        }

        position.ShouldBe(Text.Length);
    }

    [Theory]
    [InlineData("-")]
    [InlineData("x -")]
    [InlineData("/")]
    [InlineData("$")]
    [InlineData("a$")]
    [InlineData("$a")]
    [InlineData("E")]
    [InlineData("*")]
    public void Lex_TextEndingWhereAConstructCouldStart_IsText(string text) => Lex(text).ShouldBe(["Text:" + text]);

    [Fact]
    public void Lex_NonAsciiText_IsKeptIntact() =>
        Lex("SELECT 'é😀' -- ñ").ShouldBe(["Text:SELECT ", "Quoted:'é😀'", "Text: ", "LineComment:-- ñ"]);

    [Fact]
    public void Lex_DeeplyNestedBlockComments_IsOneComment()
    {
        const int Depth = 100_000;
        var text = string.Concat(Enumerable.Repeat("/*", Depth)) + string.Concat(Enumerable.Repeat("*/", Depth));

        var result = SqlLexer.Lex(text);

        result.Error.ShouldBeNull();
        result.Lexemes.Count.ShouldBe(1);
        result.Lexemes[0].ShouldBe(new SqlLexeme(SqlLexemeKind.BlockComment, new TextSpan(0, text.Length)));
    }

    [Theory]
    [InlineData("  SELECT 1 \n", 2, 8)]
    [InlineData("'a'", 0, 3)]
    [InlineData("/*+ h */", 0, 8)]
    public void GetContentSpan_LexemeWithContent_ReturnsItWithoutSurroundingWhitespace(
        string text,
        int start,
        int length
    ) => SqlLexer.Lex(text).Lexemes[0].GetContentSpan(text).ShouldBe(new TextSpan(start, length));

    [Theory]
    [InlineData(" \r\n\t ")]
    [InlineData("-- c")]
    [InlineData("/* c */")]
    public void GetContentSpan_WhitespaceOrComment_ReturnsNull(string text) =>
        SqlLexer.Lex(text).Lexemes[0].GetContentSpan(text).ShouldBeNull();

    private static string[] Lex(string text)
    {
        var result = SqlLexer.Lex(text);
        result.Error.ShouldBeNull();
        return
        [
            .. result.Lexemes.Select(lexeme =>
                $"{lexeme.Kind}:{text.Substring(lexeme.Span.Start, lexeme.Span.Length)}"
            ),
        ];
    }
}
