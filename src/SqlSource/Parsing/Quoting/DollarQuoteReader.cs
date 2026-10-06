using System;

namespace SqlSource.Parsing.Quoting;

/// <summary>
/// PostgreSQL's dollar quote, <c>$tag$ ... $tag$</c>, where the tag may be empty.
/// </summary>
/// <remarks>
/// A dollar sign has other meanings: a parameter such as <c>$1</c>, a money literal, a character of an identifier.
/// So one that follows an identifier character, or that opens a quote which is never closed, is plain text.
/// </remarks>
internal sealed class DollarQuoteReader : QuoteReader
{
    public override int FindEnd(string text, int start)
    {
        if (start > 0 && IsIdentifierCharacter(text[start - 1]))
        {
            return NotAQuote;
        }

        var tagEnd = start + 1;
        if (tagEnd < text.Length && (char.IsLetter(text[tagEnd]) || text[tagEnd] == '_'))
        {
            while (tagEnd < text.Length && (char.IsLetterOrDigit(text[tagEnd]) || text[tagEnd] == '_'))
            {
                tagEnd++;
            }
        }

        if (tagEnd >= text.Length || text[tagEnd] != '$')
        {
            return NotAQuote;
        }

        var delimiter = text.AsSpan(start, tagEnd - start + 1);
        var body = tagEnd + 1;
        var close = text.AsSpan(body).IndexOf(delimiter, StringComparison.Ordinal);
        return close < 0 ? NotAQuote : body + close + delimiter.Length;
    }
}
