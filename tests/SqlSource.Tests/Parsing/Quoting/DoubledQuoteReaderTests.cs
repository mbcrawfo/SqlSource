using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public class DoubledQuoteReaderTests
{
    private static readonly DoubledQuoteReader Reader = new();

    [Theory]
    [InlineData("'abc' x", '\'', "'abc'")]
    [InlineData("x 'abc'", '\'', "'abc'")]
    [InlineData("''", '\'', "''")]
    [InlineData("'it''s' x", '\'', "'it''s'")]
    [InlineData("'''' x", '\'', "''''")]
    [InlineData("'a''' x", '\'', "'a'''")]
    [InlineData("\"a\"\"b\" x", '"', "\"a\"\"b\"")]
    [InlineData("`a``b` x", '`', "`a``b`")]
    [InlineData("'a\nb' x", '\'', "'a\nb'")]
    [InlineData("'a -- b /* c' x", '\'', "'a -- b /* c'")]
    [InlineData("'a\"b`c' x", '\'', "'a\"b`c'")]
    public void FindEnd_ClosedRegion_EndsAfterTheQuoteThatIsNotDoubled(string text, char opener, string expected) =>
        Region.Read(Reader, text, opener).ShouldBe(expected);

    // A backslash is an ordinary character here: the quote after it closes the region.
    [Theory]
    [InlineData("'a\\' x", "'a\\'")]
    [InlineData("'a\\'b' x", "'a\\'")]
    [InlineData("'\\\\' x", "'\\\\'")]
    public void FindEnd_Backslash_IsNotAnEscape(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    [Theory]
    [InlineData("'abc")]
    [InlineData("'")]
    [InlineData("'abc''")]
    [InlineData("'abc''def")]
    public void FindEnd_RegionWithoutAClosingQuote_IsUnterminated(string text) =>
        Region.Read(Reader, text, '\'').ShouldBe(Region.Unterminated);
}
