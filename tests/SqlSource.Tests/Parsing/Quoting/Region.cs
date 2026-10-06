using System;
using SqlSource.Parsing.Quoting;

namespace SqlSource.Tests.Parsing.Quoting;

// What a reader makes of the first place in a text where its opening character stands: the region as written, or
// the name of the answer that is not a region.
internal static class Region
{
    public const string Unterminated = "<unterminated>";

    public const string NotAQuote = "<not a quote>";

    public static string Read(QuoteReader reader, string text, char opener)
    {
        var start = text.IndexOf(opener, StringComparison.Ordinal);
        var end = reader.FindEnd(text, start);
        return end switch
        {
            QuoteReader.Unterminated => Unterminated,
            QuoteReader.NotAQuote => NotAQuote,
            _ => text[start..end],
        };
    }
}
