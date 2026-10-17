using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The one reader of a quoted name, the one bare-name rule and PostgreSQL's rule for a character of a word, which
// every reader of the describer calls.
public sealed class PostgresIdentifiersTests
{
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
}
