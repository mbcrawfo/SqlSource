namespace SqlSource.Parsing.Quoting;

/// <summary>
/// A bracketed identifier, <c>[order details]</c>.
/// </summary>
/// <param name="doubledCloserEscapes">
/// True for SQL Server, where <c>]]</c> stands for one <c>]</c>.  False for SQLite, where the first <c>]</c> ends the
/// identifier.
/// </param>
internal sealed class BracketReader(bool doubledCloserEscapes) : QuoteReader
{
    public override int FindEnd(string text, int start)
    {
        var index = start + 1;
        while (true)
        {
            var close = text.IndexOf(']', index);
            if (close < 0)
            {
                return Unterminated;
            }

            if (doubledCloserEscapes && close + 1 < text.Length && text[close + 1] == ']')
            {
                index = close + 2;
            }
            else
            {
                return close + 1;
            }
        }
    }
}
