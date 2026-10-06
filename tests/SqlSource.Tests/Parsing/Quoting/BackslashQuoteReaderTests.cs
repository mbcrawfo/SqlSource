using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public class BackslashQuoteReaderTests
{
    private static readonly BackslashQuoteReader Reader = new();

    [Theory]
    [InlineData("'abc' x", '\'', "'abc'")]
    [InlineData("'a\\'b' x", '\'', "'a\\'b'")]
    [InlineData("'a\\\\' x", '\'', "'a\\\\'")]
    [InlineData("'a\\\\\\'b' x", '\'', "'a\\\\\\'b'")]
    [InlineData("'it''s' x", '\'', "'it''s'")]
    [InlineData("'a''\\' b' x", '\'', "'a''\\' b'")]
    [InlineData("\"a\\\"b\" x", '"', "\"a\\\"b\"")]
    [InlineData("\"a\"\"b\" x", '"', "\"a\"\"b\"")]
    [InlineData("'a\\\nb' x", '\'', "'a\\\nb'")]
    [InlineData("'a\\nb -- c' x", '\'', "'a\\nb -- c'")]
    public void FindEnd_ClosedRegion_EndsAfterTheQuoteThatIsNeitherEscapedNorDoubled(
        string text,
        char opener,
        string expected
    ) => Region.Read(Reader, text, opener).ShouldBe(expected);

    [Theory]
    [InlineData("'abc")]
    [InlineData("'abc\\'")]
    [InlineData("'abc\\")]
    [InlineData("'abc''")]
    public void FindEnd_RegionWithoutAClosingQuote_IsUnterminated(string text) =>
        Region.Read(Reader, text, '\'').ShouldBe(Region.Unterminated);
}
