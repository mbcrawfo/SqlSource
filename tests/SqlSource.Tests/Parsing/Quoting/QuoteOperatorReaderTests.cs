using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public class QuoteOperatorReaderTests
{
    private static readonly QuoteOperatorReader Reader = new();

    [Theory]
    [InlineData("q'[it's]' x", "'[it's]'")]
    [InlineData("q'{it's}' x", "'{it's}'")]
    [InlineData("q'<it's>' x", "'<it's>'")]
    [InlineData("q'(it's)' x", "'(it's)'")]
    [InlineData("Q'[it's]' x", "'[it's]'")]
    public void FindEnd_BracketDelimiter_EndsAtItsPairFollowedByAQuote(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    [Theory]
    [InlineData("q'!it's!' x", "'!it's!'")]
    [InlineData("q'|a'b|' x", "'|a'b|'")]
    [InlineData("q'aitsa' x", "'aitsa'")]
    [InlineData("q']x]' x", "']x]'")]
    [InlineData("q''it's'' x", "''it's''")]
    [InlineData("q'\"it's\"' x", "'\"it's\"'")]
    public void FindEnd_AnyOtherDelimiter_EndsAtTheSameCharacterFollowedByAQuote(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    [Theory]
    // The closing delimiter in the body, not followed by a quote.
    [InlineData("q'[a]b]' x", "'[a]b]'")]
    [InlineData("q'[a[b]c]' x", "'[a[b]c]'")]
    [InlineData("q'!a!b!' x", "'!a!b!'")]
    // Comment syntax and other quotes in the body.
    [InlineData("q'[it's -- x /* y]' z", "'[it's -- x /* y]'")]
    [InlineData("q'[a\nb]' x", "'[a\nb]'")]
    [InlineData("q'[]' x", "'[]'")]
    // The first closing delimiter that a quote follows ends it, whatever comes after.
    [InlineData("q'[a]'b]' x", "'[a]'")]
    public void FindEnd_Body_IsEverythingUpToTheFirstCloserFollowedByAQuote(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    [Theory]
    [InlineData("nq'[it's]' x", "'[it's]'")]
    [InlineData("Nq'[it's]' x", "'[it's]'")]
    [InlineData("NQ'[it's]' x", "'[it's]'")]
    [InlineData("(nq'[it's]') x", "'[it's]'")]
    [InlineData("=q'[it's]' x", "'[it's]'")]
    public void FindEnd_NationalPrefixOrAPrefixAfterPunctuation_IsRead(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    // Each of these is an ordinary string that ends at the first quote that is not doubled.
    [Theory]
    [InlineData("'[it's]' x", "'[it'")]
    [InlineData("x'[it's]' x", "'[it'")]
    [InlineData("seq'[it's]' x", "'[it'")]
    [InlineData("a_q'[it's]' x", "'[it'")]
    [InlineData("anq'[it's]' x", "'[it'")]
    [InlineData("n'[it's]' x", "'[it'")]
    [InlineData("q 'a' x", "'a'")]
    public void FindEnd_StringWithoutThePrefix_IsAnOrdinaryString(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    // Oracle does not take whitespace as a delimiter.
    [Theory]
    [InlineData("q' a' x", "' a'")]
    [InlineData("q'\ta' x", "'\ta'")]
    [InlineData("q'\na' x", "'\na'")]
    [InlineData("q'\r\na' x", "'\r\na'")]
    public void FindEnd_WhitespaceAfterTheQuote_IsNotADelimiter(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    [Theory]
    [InlineData("q'[abc")]
    [InlineData("q'[abc]")]
    [InlineData("q'[abc'")]
    [InlineData("q'[abc] '")]
    [InlineData("q'[")]
    [InlineData("q'")]
    // A quote is a delimiter like any other: this opens a string that would close at the next two quotes.
    [InlineData("q'' x")]
    [InlineData("'abc")]
    public void FindEnd_StringWithoutItsClosingDelimiter_IsUnterminated(string text) =>
        Region.Read(Reader, text, '\'').ShouldBe(Region.Unterminated);
}
