using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

// A test without a dialect in its name reads by the ANSI rules, which are the default.  An internal type cannot be
// the parameter of a public test method, so a row names its dialects, separated by commas.  An option of a dialect
// follows it after a plus sign, as in "MySql+AnsiQuotes".
public class SqlLexerTests
{
    private const string AllDialects = "Ansi,SqlServer,PostgreSql,MySql,MariaDb,Sqlite,Oracle";

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
        var result = SqlLexer.Lex("'a\\'b'", SqlDialectRules.Ansi);

        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(5, 1)));
    }

    [Fact]
    public void Lex_KnownLimit_CommentOpenerInsideCommentMustBeClosed()
    {
        var result = SqlLexer.Lex("/* a /* b */ SELECT 1", SqlDialectRules.Ansi);

        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedBlockComment, new TextSpan(0, 2)));
    }

    [Fact]
    public void Lex_KnownLimit_OracleQuoteLiteralIsNotRecognised()
    {
        var result = SqlLexer.Lex("q'[it's]'", SqlDialectRules.Ansi);

        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(8, 1)));
    }

    // The silent form of the limits above: when the misread quotes happen to balance, SQL is read as a comment.
    [Theory]
    [InlineData("'a\\'b -- c', 2", "LineComment:-- c', 2")]
    [InlineData("[a'b], 'x -- y'", "LineComment:-- y'")]
    [InlineData("q'[it's -- x]', q'[y's]'", "LineComment:-- x]', q'[y's]'")]
    public void Lex_KnownLimit_MisreadQuotesThatBalance_TurnSqlIntoAComment(string text, string comment) =>
        Lex(text)[^1].ShouldBe(comment);

    [Theory]
    [InlineData("'abc", 0)]
    [InlineData("x \"abc", 2)]
    [InlineData("`abc", 0)]
    [InlineData("'abc''", 0)]
    [InlineData("E'abc\\'", 1)]
    [InlineData("E'abc\\", 1)]
    public void Lex_UnterminatedQuote_ReportsTheOpeningQuote(string text, int position)
    {
        var result = SqlLexer.Lex(text, SqlDialectRules.Ansi);

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
        var result = SqlLexer.Lex(text, SqlDialectRules.Ansi);

        result.Error.ShouldBe(
            SqlParseError.Create(SqlParseErrorKind.UnterminatedBlockComment, new TextSpan(position, 2))
        );
        result.Lexemes.Count.ShouldBe(0);
    }

    // Each construct of the dialect table, read by every dialect.  A row lists the dialects that share a reading, and
    // its lexemes are separated by " | ".
    [Theory]
    // A backslash in a plain string.
    [InlineData("MySql,MariaDb", "'a\\'b' -- c'", "Quoted:'a\\'b' | Text:  | LineComment:-- c'")]
    [InlineData("Ansi,SqlServer,PostgreSql,Sqlite,Oracle", "'a\\'b' -- c'", "Quoted:'a\\' | Text:b | Quoted:' -- c'")]
    // A backslash in a double-quoted region.
    [InlineData("MySql,MariaDb", "\"a\\\"b\" -- c\"", "Quoted:\"a\\\"b\" | Text:  | LineComment:-- c\"")]
    [InlineData(
        "Ansi,SqlServer,PostgreSql,Sqlite,Oracle",
        "\"a\\\"b\" -- c\"",
        "Quoted:\"a\\\" | Text:b | Quoted:\" -- c\""
    )]
    // The options of MySQL and MariaDB.  ANSI_QUOTES takes the backslash escape from "...", and
    // NO_BACKSLASH_ESCAPES takes it from both.
    [InlineData("MySql+AnsiQuotes,MariaDb+AnsiQuotes", "'a\\'b' -- c'", "Quoted:'a\\'b' | Text:  | LineComment:-- c'")]
    [InlineData(
        "MySql+NoBackslashEscapes,MariaDb+NoBackslashEscapes,"
            + "MySql+AnsiQuotes+NoBackslashEscapes,MariaDb+AnsiQuotes+NoBackslashEscapes",
        "'a\\'b' -- c'",
        "Quoted:'a\\' | Text:b | Quoted:' -- c'"
    )]
    [InlineData(
        "MySql+AnsiQuotes,MariaDb+AnsiQuotes,MySql+NoBackslashEscapes,MariaDb+NoBackslashEscapes,"
            + "MySql+AnsiQuotes+NoBackslashEscapes,MariaDb+AnsiQuotes+NoBackslashEscapes",
        "\"a\\\"b\" -- c\"",
        "Quoted:\"a\\\" | Text:b | Quoted:\" -- c\""
    )]
    // An option leaves the rest of the dialect alone.
    [InlineData(
        "MySql+AnsiQuotes+NoBackslashEscapes,MariaDb+AnsiQuotes+NoBackslashEscapes",
        "1 # c -- d\n2",
        "Text:1  | LineComment:# c -- d | Text:\n2"
    )]
    [InlineData("MySql+AnsiQuotes+NoBackslashEscapes,MariaDb+AnsiQuotes+NoBackslashEscapes", "5--3", "Text:5--3")]
    // PostgreSQL's E prefix.  MySQL and MariaDB take the backslash with or without it.
    [InlineData(
        "Ansi,PostgreSql,MySql,MariaDb",
        "E'a\\'b' -- c'",
        "Text:E | Quoted:'a\\'b' | Text:  | LineComment:-- c'"
    )]
    [InlineData("SqlServer,Sqlite,Oracle", "E'a\\'b' -- c'", "Text:E | Quoted:'a\\' | Text:b | Quoted:' -- c'")]
    // An E string continued on the next line.
    [InlineData("PostgreSql", "E'a'\n'b\\'c' -- d'", "Text:E | Quoted:'a'\n'b\\'c' | Text:  | LineComment:-- d'")]
    [InlineData(
        "Ansi",
        "E'a'\n'b\\'c' -- d'",
        "Text:E | Quoted:'a' | Text:\n | Quoted:'b\\' | Text:c | Quoted:' -- d'"
    )]
    // Oracle's quote operator.
    [InlineData("Oracle", "q'[it's -- x]', q'[y's]'", "Text:q | Quoted:'[it's -- x]' | Text:, q | Quoted:'[y's]'")]
    [InlineData(
        "Ansi,SqlServer,PostgreSql,Sqlite",
        "q'[it's -- x]', q'[y's]'",
        "Text:q | Quoted:'[it' | Text:s  | LineComment:-- x]', q'[y's]'"
    )]
    // A backtick.
    [InlineData("Ansi,MySql,MariaDb,Sqlite", "`a -- b` c", "Quoted:`a -- b` | Text: c")]
    [InlineData("SqlServer,PostgreSql,Oracle", "`a -- b` c", "Text:`a  | LineComment:-- b` c")]
    // A bracketed identifier.
    [InlineData("SqlServer,Sqlite", "[a'b], 'x -- y'", "Quoted:[a'b] | Text:,  | Quoted:'x -- y'")]
    [InlineData(
        "Ansi,PostgreSql,MySql,MariaDb,Oracle",
        "[a'b], 'x -- y'",
        "Text:[a | Quoted:'b], ' | Text:x  | LineComment:-- y'"
    )]
    [InlineData("SqlServer", "[a]]--b] c", "Quoted:[a]]--b] | Text: c")]
    [InlineData("Sqlite", "[a]]--b] c", "Quoted:[a] | Text:] | LineComment:--b] c")]
    // A dollar quote.
    [InlineData("Ansi,PostgreSql,MySql", "$$ -- x $$ y", "Quoted:$$ -- x $$ | Text: y")]
    [InlineData("SqlServer,MariaDb,Sqlite,Oracle", "$$ -- x $$ y", "Text:$$  | LineComment:-- x $$ y")]
    // A comment opener inside a block comment.
    [InlineData("Ansi,SqlServer,PostgreSql", "/* a /* b */ c */ d", "BlockComment:/* a /* b */ c */ | Text: d")]
    [InlineData("MySql,MariaDb,Sqlite,Oracle", "/* a /* b */ c */ d", "BlockComment:/* a /* b */ | Text: c */ d")]
    // Two dashes with no whitespace after them.
    [InlineData("MySql,MariaDb", "5--3", "Text:5--3")]
    [InlineData("Ansi,SqlServer,PostgreSql,Sqlite,Oracle", "5--3", "Text:5 | LineComment:--3")]
    // A hash sign.
    [InlineData("MySql,MariaDb", "1 # c -- d\n2", "Text:1  | LineComment:# c -- d | Text:\n2")]
    [InlineData(
        "Ansi,SqlServer,PostgreSql,Sqlite,Oracle",
        "1 # c -- d\n2",
        "Text:1 # c  | LineComment:-- d | Text:\n2"
    )]
    // The hints every dialect keeps.
    [InlineData(AllDialects, "/*+ h */ x", "Hint:/*+ h */ | Text: x")]
    [InlineData(AllDialects, "/*! h */ x", "Hint:/*! h */ | Text: x")]
    // MariaDB's own executable comment.
    [InlineData("MariaDb", "/*M! h */ x", "Hint:/*M! h */ | Text: x")]
    [InlineData("Ansi,SqlServer,PostgreSql,MySql,Sqlite,Oracle", "/*M! h */ x", "BlockComment:/*M! h */ | Text: x")]
    // Oracle's line hint.  MySQL and MariaDB do not read it as a comment at all: no whitespace follows the dashes.
    [InlineData("Oracle", "--+ h\nx", "Hint:--+ h | Text:\nx")]
    [InlineData("Ansi,SqlServer,PostgreSql,Sqlite", "--+ h\nx", "LineComment:--+ h | Text:\nx")]
    [InlineData("MySql,MariaDb", "--+ h\nx", "Text:--+ h\nx")]
    public void Lex_Construct_IsReadByTheRulesOfTheDialect(string dialects, string text, string expected)
    {
        foreach (var dialect in dialects.Split(','))
        {
            string.Join(" | ", Lex(text, Rules(dialect))).ShouldBe(expected, dialect);
        }
    }

    [Theory]
    [InlineData("5 -- 3", "Text:5  | LineComment:-- 3")]
    [InlineData("5 --\t3", "Text:5  | LineComment:--\t3")]
    [InlineData("5 --\n3", "Text:5  | LineComment:-- | Text:\n3")]
    [InlineData("5 --\r\n3", "Text:5  | LineComment:-- | Text:\r\n3")]
    [InlineData("5 --", "Text:5  | LineComment:--")]
    [InlineData("5 --\u00013", "Text:5  | LineComment:--\u00013")]
    [InlineData("5 --- 3", "Text:5 - | LineComment:-- 3")]
    [InlineData("5 --3 -- c", "Text:5 --3  | LineComment:-- c")]
    [InlineData("5 --'a -- b'", "Text:5 -- | Quoted:'a -- b'")]
    public void Lex_TwoDashesInMySql_AreACommentOnlyBeforeWhitespaceAControlCharacterOrTheEnd(
        string text,
        string expected
    )
    {
        string.Join(" | ", Lex(text, SqlDialectRules.MySql)).ShouldBe(expected);
        string.Join(" | ", Lex(text, SqlDialectRules.MariaDb)).ShouldBe(expected);
    }

    [Theory]
    [InlineData("# c\nx", "LineComment:# c | Text:\nx")]
    [InlineData("#c\r\nx", "LineComment:#c | Text:\r\nx")]
    [InlineData("#", "LineComment:#")]
    [InlineData("x #'a\n'b'", "Text:x  | LineComment:#'a | Text:\n | Quoted:'b'")]
    [InlineData("'a # b' # c", "Quoted:'a # b' | Text:  | LineComment:# c")]
    [InlineData("/* # */ x", "BlockComment:/* # */ | Text: x")]
    public void Lex_HashInMySql_StartsACommentToTheEndOfItsLine(string text, string expected) =>
        string.Join(" | ", Lex(text, SqlDialectRules.MySql)).ShouldBe(expected);

    [Theory]
    [InlineData(nameof(SqlDialect.SqlServer), "x [abc", 2)]
    [InlineData(nameof(SqlDialect.SqlServer), "[abc]]", 0)]
    [InlineData(nameof(SqlDialect.Sqlite), "[abc", 0)]
    [InlineData(nameof(SqlDialect.Oracle), "q'[abc]", 1)]
    [InlineData(nameof(SqlDialect.Oracle), "nq'[abc' x", 2)]
    [InlineData(nameof(SqlDialect.MySql), "'abc\\'", 0)]
    [InlineData(nameof(SqlDialect.MariaDb), "\"abc\\\"", 0)]
    // The second part of a continued string is not closed.  The error is at the quote that opened the string.
    [InlineData(nameof(SqlDialect.PostgreSql), "E'a'\n'b\\'", 1)]
    // Two readings that differ from the database, which docs/tech-debt/TD-0004 lists.  A MySQL comment for a version
    // ends at the first */, inside a string of its body too, so the quote after it opens a string.
    [InlineData(nameof(SqlDialect.MySql), "/*!50700 '*/' */", 12)]
    // CockroachDB reads b'\'' as one literal.  The PostgreSQL rules have no backslash escape there.
    [InlineData(nameof(SqlDialect.PostgreSql), "b'\\''", 1)]
    public void Lex_UnterminatedQuoteOfADialect_ReportsTheOpeningDelimiter(string dialect, string text, int position)
    {
        var result = SqlLexer.Lex(text, Rules(dialect));

        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(position, 1)));
        result.Lexemes.Count.ShouldBe(0);
    }

    // SQLite itself accepts a block comment that runs to the end of the input.  It would take every later query of
    // the file with it, so it is an error here, as in every dialect.
    [Fact]
    public void Lex_UnterminatedBlockComment_IsAnErrorInEveryDialect()
    {
        foreach (var dialect in Enum.GetValues<SqlDialect>())
        {
            var result = SqlLexer.Lex("SELECT 1 /* x", SqlDialectRules.For(dialect));

            result.Error.ShouldBe(
                SqlParseError.Create(SqlParseErrorKind.UnterminatedBlockComment, new TextSpan(9, 2)),
                dialect.ToString()
            );
        }
    }

    [Fact]
    public void Lex_AnyText_CoversItWithoutGapsInEveryDialect()
    {
        const string Text =
            "SELECT 'a', \"b\" /* c */ -- d\r\nFROM $$e$$ /*+ f */ `g` [h] # i\n, q'[j]', E'k'\n'l' /*M! m */ --+ n\n";

        foreach (var dialect in Enum.GetValues<SqlDialect>())
        {
            var result = SqlLexer.Lex(Text, SqlDialectRules.For(dialect));

            result.Error.ShouldBeNull(dialect.ToString());
            var position = 0;
            foreach (var lexeme in result.Lexemes)
            {
                lexeme.Span.Start.ShouldBe(position, dialect.ToString());
                lexeme.Span.Length.ShouldBeGreaterThan(0, dialect.ToString());
                position = lexeme.Span.End;
            }

            position.ShouldBe(Text.Length, dialect.ToString());
        }
    }

    [Theory]
    [InlineData("-")]
    [InlineData("x -")]
    [InlineData("/")]
    [InlineData("$")]
    [InlineData("a$")]
    [InlineData("$a")]
    [InlineData("E")]
    [InlineData("q")]
    [InlineData("nq")]
    [InlineData("]")]
    [InlineData("*")]
    public void Lex_TextEndingWhereAConstructCouldStart_IsTextInEveryDialect(string text)
    {
        foreach (var dialect in Enum.GetValues<SqlDialect>())
        {
            Lex(text, SqlDialectRules.For(dialect)).ShouldBe(["Text:" + text], dialect.ToString());
        }
    }

    [Fact]
    public void Lex_NonAsciiText_IsKeptIntact() =>
        Lex("SELECT 'é😀' -- ñ").ShouldBe(["Text:SELECT ", "Quoted:'é😀'", "Text: ", "LineComment:-- ñ"]);

    // A character beyond the table of openers opens nothing, in any dialect.
    [Fact]
    public void Lex_NonAsciiCharacterThatLooksLikeAQuote_IsText()
    {
        foreach (var dialect in Enum.GetValues<SqlDialect>())
        {
            Lex("a ‘b’ ＄ c", SqlDialectRules.For(dialect)).ShouldBe(["Text:a ‘b’ ＄ c"], dialect.ToString());
        }
    }

    [Fact]
    public void Lex_DeeplyNestedBlockComments_IsOneComment()
    {
        const int Depth = 100_000;
        var text = string.Concat(Enumerable.Repeat("/*", Depth)) + string.Concat(Enumerable.Repeat("*/", Depth));

        var result = SqlLexer.Lex(text, SqlDialectRules.Ansi);

        result.Error.ShouldBeNull();
        result.Lexemes.Count.ShouldBe(1);
        result.Lexemes[0].ShouldBe(new SqlLexeme(SqlLexemeKind.BlockComment, new TextSpan(0, text.Length)));
    }

    [Fact]
    public void TryReadLeadingComment_CommentsBeforeTheFirstSql_AreReadOneAtATime()
    {
        const string Text = "  -- a\n\n/* b */ -- c\nSELECT 1 -- d\n";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        var comments = ReadLeadingComments(lexer, Text);

        comments.ShouldBe(["LineComment:-- a", "BlockComment:/* b */", "LineComment:-- c"]);
        lexer.Position.ShouldBe(Text.IndexOf("\nSELECT", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SELECT 1 -- a")]
    [InlineData("'a' -- b")]
    [InlineData("/*+ h */ -- a")]
    [InlineData("/*! h */ -- a")]
    [InlineData("- - a")]
    [InlineData("/ * a */")]
    [InlineData("   ")]
    [InlineData("")]
    public void TryReadLeadingComment_TextThatDoesNotStartWithAComment_ReadsNothing(string text)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        lexer.TryReadLeadingComment(out _).ShouldBeFalse();

        lexer.Position.ShouldBe(0);
    }

    // What counts as a comment is decided by the rules the lexer holds.
    [Theory]
    [InlineData(nameof(SqlDialect.MySql), "# a\nx", true)]
    [InlineData(nameof(SqlDialect.Ansi), "# a\nx", false)]
    [InlineData(nameof(SqlDialect.MySql), "--a\nx", false)]
    [InlineData(nameof(SqlDialect.Ansi), "--a\nx", true)]
    [InlineData(nameof(SqlDialect.Oracle), "--+ a\nx", false)]
    [InlineData(nameof(SqlDialect.Ansi), "--+ a\nx", true)]
    [InlineData(nameof(SqlDialect.MariaDb), "/*M! a */ x", false)]
    [InlineData(nameof(SqlDialect.MySql), "/*M! a */ x", true)]
    public void TryReadLeadingComment_CommentFormOfOneDialect_IsReadOnlyByThatDialect(
        string dialect,
        string text,
        bool expected
    ) => new SqlLexer(text, Rules(dialect)).TryReadLeadingComment(out _).ShouldBe(expected);

    [Fact]
    public void TryReadLeadingComment_RulesChangedBetweenComments_ApplyToTheTextAfterTheLastLexeme()
    {
        const string Text = "-- a\n# b\nSELECT 'c\\'d' # e\n";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        lexer.TryReadLeadingComment(out _).ShouldBeTrue();
        lexer.TryReadLeadingComment(out _).ShouldBeFalse();
        lexer.Rules = SqlDialectRules.MySql;
        lexer.TryReadLeadingComment(out _).ShouldBeTrue();
        lexer.TryReadLeadingComment(out _).ShouldBeFalse();
        var result = lexer.ReadToEnd();

        result.Error.ShouldBeNull();
        Describe(result, Text)
            .ShouldBe([
                "LineComment:-- a",
                "Text:\n",
                "LineComment:# b",
                "Text:\nSELECT ",
                "Quoted:'c\\'d'",
                "Text: ",
                "LineComment:# e",
                "Text:\n",
            ]);
    }

    [Fact]
    public void TryReadLeadingComment_ThenReadToEnd_GivesTheLexemesOfLexingInOneCall()
    {
        const string Text = "/* a */\n-- b\nSELECT 'c' -- d\n";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = ReadLeadingComments(lexer, Text);

        lexer.ReadToEnd().ShouldBe(SqlLexer.Lex(Text, SqlDialectRules.Ansi));
    }

    [Fact]
    public void TryReadLeadingComment_UnterminatedBlockComment_ReadsNothingAndReadToEndReportsIt()
    {
        var lexer = new SqlLexer("-- a\n/* b", SqlDialectRules.Ansi);

        lexer.TryReadLeadingComment(out _).ShouldBeTrue();
        lexer.TryReadLeadingComment(out _).ShouldBeFalse();
        lexer.TryReadLeadingComment(out _).ShouldBeFalse();

        var result = lexer.ReadToEnd();
        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedBlockComment, new TextSpan(5, 2)));
        result.Lexemes.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData("  SELECT 1 \n", 2, 8)]
    [InlineData("'a'", 0, 3)]
    [InlineData("/*+ h */", 0, 8)]
    public void GetContentSpan_LexemeWithContent_ReturnsItWithoutSurroundingWhitespace(
        string text,
        int start,
        int length
    ) => SqlLexer.Lex(text, SqlDialectRules.Ansi).Lexemes[0].GetContentSpan(text).ShouldBe(new TextSpan(start, length));

    [Theory]
    [InlineData(" \r\n\t ")]
    [InlineData("-- c")]
    [InlineData("/* c */")]
    public void GetContentSpan_WhitespaceOrComment_ReturnsNull(string text) =>
        SqlLexer.Lex(text, SqlDialectRules.Ansi).Lexemes[0].GetContentSpan(text).ShouldBeNull();

    [Fact]
    public void GetContentSpan_HashCommentOrLineHint_FollowsItsKind()
    {
        SqlLexer.Lex("# c", SqlDialectRules.MySql).Lexemes[0].GetContentSpan("# c").ShouldBeNull();
        SqlLexer.Lex("--+ h", SqlDialectRules.Oracle).Lexemes[0].GetContentSpan("--+ h").ShouldBe(new TextSpan(0, 5));
    }

    private static SqlDialectRules Rules(string choice)
    {
        var parts = choice.Split('+');
        var options = parts
            .Skip(1)
            .Aggregate(SqlDialectOptions.None, (all, option) => all | Enum.Parse<SqlDialectOptions>(option));

        return SqlDialectRules.For(new SqlDialectChoice(Enum.Parse<SqlDialect>(parts[0]), options));
    }

    private static string[] Lex(string text) => Lex(text, SqlDialectRules.Ansi);

    private static string[] Lex(string text, SqlDialectRules rules)
    {
        var result = SqlLexer.Lex(text, rules);
        result.Error.ShouldBeNull();
        return Describe(result, text);
    }

    private static string[] Describe(SqlLexResult result, string text) =>
        [.. result.Lexemes.Select(lexeme => Describe(lexeme, text))];

    private static string Describe(SqlLexeme lexeme, string text) =>
        $"{lexeme.Kind}:{text.Substring(lexeme.Span.Start, lexeme.Span.Length)}";

    private static string[] ReadLeadingComments(SqlLexer lexer, string text)
    {
        var comments = new System.Collections.Generic.List<string>();
        while (lexer.TryReadLeadingComment(out var comment))
        {
            comments.Add(Describe(comment, text));
        }

        return [.. comments];
    }
}
