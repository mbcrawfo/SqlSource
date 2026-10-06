namespace SqlSource.Parsing.Quoting;

/// <summary>
/// The ANSI form: <c>'it''s'</c>, <c>"a""b"</c>, and <c>`a``b`</c> where a backtick is a quote.
/// </summary>
internal sealed class DoubledQuoteReader : QuoteReader
{
    public override int FindEnd(string text, int start) => FindDoubledEnd(text, start);
}
