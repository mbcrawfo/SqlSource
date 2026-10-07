using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public class EscapeStringReaderTests
{
    private static readonly EscapeStringReader Reader = new("Ee");

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
    public void FindEnd_String_TakesBackslashEscapesOnlyAfterThePrefix(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    // The E ends an identifier, so it is not a prefix.
    [Theory]
    [InlineData("typeE'a\\' x", "'a\\'")]
    [InlineData("_e'a\\' x", "'a\\'")]
    [InlineData("a$E'a\\' x", "'a\\'")]
    [InlineData("1e'a\\' x", "'a\\'")]
    public void FindEnd_LetterEAtTheEndOfAnIdentifier_IsNotAPrefix(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    [Theory]
    [InlineData("E'abc")]
    [InlineData("E'abc\\'")]
    [InlineData("E'abc\\")]
    [InlineData("'abc")]
    public void FindEnd_StringWithoutAClosingQuote_IsUnterminated(string text) =>
        Region.Read(Reader, text, '\'').ShouldBe(Region.Unterminated);

    // A reader reads one part.  Whether a string goes on after a gap is for the lexer.
    [Theory]
    [InlineData("E'a'\n'b\\'c' x")]
    [InlineData("E'a' -- c\n'b\\'c' x")]
    [InlineData("E'a'\r\n    'b\\'c' x")]
    public void FindEnd_EscapeStringFollowedByAnotherPart_EndsAtItsOwnQuote(string text) =>
        Region.Read(Reader, text, '\'').ShouldBe("'a'");

    [Theory]
    [InlineData("E'a' x", 1)]
    [InlineData("x = e'a\\'b' y", 5)]
    public void FindContinuation_StringWithThePrefix_NamesAReaderThatTakesBackslashEscapes(string text, int start)
    {
        var continuation = Reader.FindContinuation(text, start).ShouldNotBeNull();

        _ = continuation.ShouldBeOfType<BackslashQuoteReader>();
        continuation.FindContinuation(text, start).ShouldBeNull();
    }

    [Theory]
    [InlineData("'a' x", 0)]
    [InlineData("typeE'a' x", 5)]
    [InlineData("b'a' x", 1)]
    public void FindContinuation_StringWithoutThePrefix_NamesNone(string text, int start) =>
        Reader.FindContinuation(text, start).ShouldBeNull();

    [Fact]
    public void FindContinuation_AnyReaderWithAPrefix_NamesTheOneSharedReader() =>
        Reader.FindContinuation("E'a'", 1).ShouldBeSameAs(new EscapeStringReader("Ee").FindContinuation("e'b'", 1));

    // CockroachDB's bytes literal.  The prefix is the lower-case letter only: B'...' is a bit string.
    [Theory]
    [InlineData("b'a\\'b' x", "'a\\'b'")]
    [InlineData("E'a\\'b' x", "'a\\'b'")]
    [InlineData("B'a\\' x", "'a\\'")]
    [InlineData("ab'a\\' x", "'a\\'")]
    [InlineData("x'a\\' x", "'a\\'")]
    public void FindEnd_ReaderWithTheBytesPrefix_TakesBackslashEscapesAfterItToo(string text, string expected) =>
        Region.Read(new EscapeStringReader("Eeb"), text, '\'').ShouldBe(expected);

    [Fact]
    public void FindContinuation_ReaderWithTheBytesPrefix_NamesAReaderAfterABytesLiteral()
    {
        var reader = new EscapeStringReader("Eeb");

        _ = reader.FindContinuation("b'a'", 1).ShouldBeOfType<BackslashQuoteReader>();
        reader.FindContinuation("B'a'", 1).ShouldBeNull();
    }
}
