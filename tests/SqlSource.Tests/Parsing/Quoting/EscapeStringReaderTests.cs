using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public class EscapeStringReaderTests
{
    private static readonly EscapeStringReader Plain = new(continues: false);

    private static readonly EscapeStringReader Continued = new(continues: true);

    [Theory]
    // Without the prefix a backslash is an ordinary character.
    [InlineData("'a\\' x", "'a\\'")]
    [InlineData("'it''s' x", "'it''s'")]
    [InlineData("x 'a\\' y", "'a\\'")]
    // With it, a backslash takes the next character with it.
    [InlineData("E'it\\'s' x", "'it\\'s'")]
    [InlineData("e'it\\'s' x", "'it\\'s'")]
    [InlineData("(E'a\\'b') x", "'a\\'b'")]
    [InlineData("E'a''b\\\\' x", "'a''b\\\\'")]
    [InlineData("x = E'a\\'b' y", "'a\\'b'")]
    public void FindEnd_String_TakesBackslashEscapesOnlyAfterThePrefix(string text, string expected)
    {
        Region.Read(Plain, text, '\'').ShouldBe(expected);
        Region.Read(Continued, text, '\'').ShouldBe(expected);
    }

    // The E ends an identifier, so it is not a prefix.
    [Theory]
    [InlineData("typeE'a\\' x", "'a\\'")]
    [InlineData("_e'a\\' x", "'a\\'")]
    [InlineData("a$E'a\\' x", "'a\\'")]
    [InlineData("1e'a\\' x", "'a\\'")]
    public void FindEnd_LetterEAtTheEndOfAnIdentifier_IsNotAPrefix(string text, string expected) =>
        Region.Read(Plain, text, '\'').ShouldBe(expected);

    [Theory]
    [InlineData("E'abc")]
    [InlineData("E'abc\\'")]
    [InlineData("E'abc\\")]
    [InlineData("'abc")]
    public void FindEnd_StringWithoutAClosingQuote_IsUnterminated(string text) =>
        Region.Read(Plain, text, '\'').ShouldBe(Region.Unterminated);

    [Theory]
    [InlineData("E'a'\n'b\\'c' x", "'a'\n'b\\'c'")]
    [InlineData("E'a'\r\n    'b\\'c' x", "'a'\r\n    'b\\'c'")]
    [InlineData("E'a' \n\n\t'b\\'c'\n 'd\\'e' x", "'a' \n\n\t'b\\'c'\n 'd\\'e'")]
    [InlineData("E'a'\r'b\\'c' x", "'a'\r'b\\'c'")]
    public void FindEnd_EscapeStringFollowedByALineBreakAndAQuote_ContinuesWithEscapes(string text, string expected) =>
        Region.Read(Continued, text, '\'').ShouldBe(expected);

    [Theory]
    // No line break between the two.
    [InlineData("E'a' 'b' x")]
    [InlineData("E'a''b' x", "'a''b'")]
    // Something other than whitespace between the two.
    [InlineData("E'a' -- c\n'b' x")]
    [InlineData("E'a' /* c */\n'b' x")]
    [InlineData("E'a',\n'b' x")]
    [InlineData("E'a'\n-- name: B\n'b' x")]
    // Nothing after the line break.
    [InlineData("E'a'\n")]
    [InlineData("E'a'\n x")]
    public void FindEnd_EscapeStringNotFollowedByAContinuation_EndsAtItsOwnQuote(
        string text,
        string expected = "'a'"
    ) => Region.Read(Continued, text, '\'').ShouldBe(expected);

    [Fact]
    public void FindEnd_ContinuationWithTheOptionOff_IsNotRead() =>
        Region.Read(Plain, "E'a'\n'b\\'c' x", '\'').ShouldBe("'a'");

    [Fact]
    public void FindEnd_StringWithoutThePrefix_IsNotContinued() =>
        Region.Read(Continued, "'a'\n'b\\' x", '\'').ShouldBe("'a'");

    [Fact]
    public void FindEnd_ContinuationWithoutAClosingQuote_IsUnterminated() =>
        Region.Read(Continued, "E'a'\n'b\\' x", '\'').ShouldBe(Region.Unterminated);
}
