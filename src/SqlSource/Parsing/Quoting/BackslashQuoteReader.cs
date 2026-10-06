namespace SqlSource.Parsing.Quoting;

/// <summary>
/// The MySQL and MariaDB form of a string: <c>'it\'s'</c> and <c>"a\"b"</c>.  A doubled quote still stands for one.
/// </summary>
internal sealed class BackslashQuoteReader : QuoteReader
{
    public override int FindEnd(string text, int start) => FindBackslashEnd(text, start);
}
