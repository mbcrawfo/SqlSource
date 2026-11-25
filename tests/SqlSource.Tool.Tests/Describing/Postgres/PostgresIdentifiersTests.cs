using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// How PostgreSQL writes a name: the one reader of a quoted name, the bare-name rule, PostgreSQL's rule for a character
// of a word, and the number of a $n.
public sealed class PostgresIdentifiersTests
{
    [Theory]
    [InlineData("users", "\"users\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("", "\"\"")]
    public void Quote_Name_IsInDoubleQuotesWithEachQuoteDoubled(string name, string quoted) =>
        PostgresIdentifiers.Quote(name).ShouldBe(quoted);

    [Theory]
    [InlineData("\"ab\" x", 4)]
    [InlineData("\"a\"\"b\"", 6)]
    [InlineData("\"\"", 2)]
    [InlineData("'it''s' x", 7)]
    public void SkipQuoted_ClosedQuote_EndsAfterIt(string text, int end)
    {
        PostgresIdentifiers.SkipQuoted(text, 0, out var after).ShouldBeTrue();

        after.ShouldBe(end);
    }

    [Theory]
    [InlineData("\"open")]
    [InlineData("\"a\"\"")]
    [InlineData("'open")]
    public void SkipQuoted_UnclosedQuote_IsFalseAtTheEnd(string text)
    {
        PostgresIdentifiers.SkipQuoted(text, 0, out var after).ShouldBeFalse();

        after.ShouldBe(text.Length);
    }

    [Fact]
    public void SkipQuoted_QuoteInsideTheText_StartsThere()
    {
        PostgresIdentifiers.SkipQuoted("x.\"y\".z", 2, out var after).ShouldBeTrue();

        after.ShouldBe(5);
    }

    [Theory]
    [InlineData("\"ab\"", "ab")]
    [InlineData("\"a\"\"b\"", "a\"b")]
    [InlineData("\"\"\"\"", "\"")]
    [InlineData("\"\"", "")]
    [InlineData("\"A b.c\"", "A b.c")]
    public void Unquote_QuotedName_IsTheNameWithEachDoubledQuoteMadeOne(string quoted, string name) =>
        PostgresIdentifiers.Unquote(quoted).ShouldBe(name);

    [Theory]
    [InlineData("id")]
    [InlineData("_x1")]
    [InlineData("a_b_9")]
    public void IsBareName_LowerCaseLettersDigitsAndUnderscore_IsTrue(string name) =>
        PostgresIdentifiers.IsBareName(name).ShouldBeTrue();

    [Theory]
    [InlineData("Id")]
    [InlineData("1x")]
    [InlineData("café")]
    [InlineData("a$b")]
    [InlineData("a b")]
    [InlineData("")]
    public void IsBareName_AnyOtherName_IsFalse(string name) => PostgresIdentifiers.IsBareName(name).ShouldBeFalse();

    // PostgreSQL's scan.l: a word starts with an ASCII letter, "_" or a byte above 0x7F, and goes on with those, digits
    // and "$".  In a C# string every character above U+007F counts, a half of a surrogate pair included.
    [Theory]
    [InlineData('a', true, true)]
    [InlineData('Z', true, true)]
    [InlineData('1', false, true)]
    [InlineData('_', true, true)]
    [InlineData('$', false, true)]
    [InlineData('é', true, true)]
    [InlineData('€', true, true)]
    [InlineData('\uD83D', true, true)]
    [InlineData(' ', false, false)]
    [InlineData('.', false, false)]
    [InlineData('"', false, false)]
    public void IsWordStartAndIsWordPart_Character_IsPostgreSqlsRule(char character, bool start, bool part)
    {
        PostgresIdentifiers.IsWordStart(character).ShouldBe(start);
        PostgresIdentifiers.IsWordPart(character).ShouldBe(part);
    }

    [Theory]
    [InlineData("$12 x", 0, 12)]
    [InlineData("x $3", 2, 3)]
    [InlineData("$0", 0, 0)]
    public void ParameterNumberAt_DollarAndDigits_IsTheNumber(string text, int at, int number) =>
        PostgresIdentifiers.ParameterNumberAt(text, at).ShouldBe(number);

    [Theory]
    [InlineData("$x", 0)]
    [InlineData("$", 0)]
    [InlineData("a1", 0)]
    [InlineData("$1", 2)]
    [InlineData("$99999999999", 0)]
    public void ParameterNumberAt_NoNumberThere_IsNull(string text, int at) =>
        PostgresIdentifiers.ParameterNumberAt(text, at).ShouldBeNull();

    [Theory]
    [InlineData("ab_1 x", 0, 4)]
    [InlineData("ab", 2, 2)]
    [InlineData("+a", 0, 0)]
    [InlineData("a$1;b", 0, 3)]
    [InlineData("x \u00E9\u4E2D y", 2, 4)]
    public void WordEnd_AtAWordADigitAndASymbol_EndsAfterTheRun(string text, int from, int end) =>
        PostgresIdentifiers.WordEnd(text, from).ShouldBe(end);

    // downcase_identifier of a UTF-8 database folds ASCII letters alone.
    [Theory]
    [InlineData("OLD", "old")]
    [InlineData("Abc_1$", "abc_1$")]
    [InlineData("ÉtÉ", "ÉtÉ")]
    public void FoldedWord_Word_HasItsAsciiLettersInLowerCase(string word, string name) =>
        PostgresIdentifiers.FoldedWord(word).ShouldBe(name);

    // truncate_identifier keeps 63 bytes and never cuts a character.
    [Fact]
    public void Truncated_LongName_KeepsTheBytesOfWholeCharactersThatFit()
    {
        PostgresIdentifiers.Truncated(new string('a', 70)).ShouldBe(new string('a', 63));
        PostgresIdentifiers.Truncated(new string('a', 62) + "é").ShouldBe(new string('a', 62));
        PostgresIdentifiers.Truncated(new string('a', 60) + "\uD83D\uDE00").ShouldBe(new string('a', 60));
        PostgresIdentifiers
            .Truncated(new string('a', 59) + "\uD83D\uDE00")
            .ShouldBe(new string('a', 59) + "\uD83D\uDE00");
        PostgresIdentifiers.Truncated("old").ShouldBe("old");
    }

    [Theory]
    [InlineData("SELECT \"Old\"\"x\"", "Old\"x")]
    [InlineData("SELECT U&\"\\006Fld\"", "old")]
    [InlineData("SELECT u&\"a\\\\b\"", "a\\b")]
    [InlineData("SELECT U&\"!006Fld\" UESCAPE '!'", "old")]
    [InlineData("SELECT U&\"!006Fld\"/* c */ UESCAPE /* c */ '!' x", "old")]
    [InlineData("SELECT U&\"\\0041\" uescapes", "A")]
    // A U& that continues a word opens no escapes.
    [InlineData("SELECT xU&\"\\0041\"", "\\0041")]
    public void TryReadQuotedName_QuotedIdentifier_IsTheNameTheServerReads(string sql, string name)
    {
        var lexemes = DescriberSql.Lex(sql, SqlDialectRules.PostgreSql);
        var index = Enumerable.Range(0, lexemes.Count).First(at => sql[lexemes[at].Span.Start] == '"');

        PostgresIdentifiers.TryReadQuotedName(sql, lexemes, index, out var read, out _).ShouldBeTrue();
        read.ShouldBe(name);
    }

    [Theory]
    [InlineData("SELECT U&\"\\zz\"")]
    [InlineData("SELECT U&\"\\DC00\"")]
    [InlineData("SELECT U&\"!006F\" UESCAPE '0'")]
    [InlineData("SELECT U&\"!006F\" UESCAPE '+'")]
    [InlineData("SELECT U&\"!006F\" UESCAPE ' '")]
    [InlineData("SELECT U&\"!006F\" UESCAPE '!!'")]
    [InlineData("SELECT U&\"!006F\" UESCAPE x")]
    public void TryReadQuotedName_EscapesThatCannotBeDecoded_IsNoName(string sql)
    {
        var lexemes = DescriberSql.Lex(sql, SqlDialectRules.PostgreSql);
        var index = Enumerable.Range(0, lexemes.Count).First(at => sql[lexemes[at].Span.Start] == '"');

        PostgresIdentifiers.TryReadQuotedName(sql, lexemes, index, out var read, out _).ShouldBeTrue();
        read.ShouldBeNull();
    }

    [Theory]
    [InlineData("SELECT 'old'")]
    [InlineData("SELECT U&'old'")]
    [InlineData("SELECT old")]
    public void TryReadQuotedName_NoQuotedIdentifier_IsFalse(string sql)
    {
        var lexemes = DescriberSql.Lex(sql, SqlDialectRules.PostgreSql);

        PostgresIdentifiers.TryReadQuotedName(sql, lexemes, lexemes.Count - 1, out _, out _).ShouldBeFalse();
    }

    // The end is past a UESCAPE clause, so a reader of names takes the clause as the identifier's.
    [Fact]
    public void TryReadQuotedName_UnicodeEscapeClause_EndsAfterItsString()
    {
        const string Sql = "SELECT U&\"!0041\" UESCAPE '!' x";
        var lexemes = DescriberSql.Lex(Sql, SqlDialectRules.PostgreSql);

        PostgresIdentifiers.TryReadQuotedName(Sql, lexemes, 1, out _, out var end).ShouldBeTrue();
        end.ShouldBe(lexemes.Count - 1);

        const string Plain = "\"a\" x";
        PostgresIdentifiers
            .TryReadQuotedName(Plain, DescriberSql.Lex(Plain, SqlDialectRules.PostgreSql), 0, out _, out end)
            .ShouldBeTrue();
        end.ShouldBe(1);
    }
}
