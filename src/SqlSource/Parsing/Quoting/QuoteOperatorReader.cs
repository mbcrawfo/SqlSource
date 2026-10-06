namespace SqlSource.Parsing.Quoting;

/// <summary>
/// A <c>'...'</c> string that is read by Oracle's quote operator after a <c>q</c> prefix, as in <c>q'[it's]'</c>
/// and <c>nq'!it's!'</c>.  The prefix stays in the text before the region.
/// </summary>
/// <remarks>
/// The character after the quote is the opening delimiter.  The region ends at the first closing delimiter that is
/// directly followed by a quote: delimiters are not counted for depth, so <c>q'[a[b]c]'</c> is one string.
/// </remarks>
internal sealed class QuoteOperatorReader : QuoteReader
{
    public override int FindEnd(string text, int start)
    {
        var open = start + 1;
        if (!HasPrefix(text, start) || open >= text.Length || text[open] is ' ' or '\t' or '\r' or '\n')
        {
            return FindDoubledEnd(text, start);
        }

        var close = GetClosingDelimiter(text[open]);
        for (var index = text.IndexOf(close, open + 1); index >= 0; index = text.IndexOf(close, index + 1))
        {
            if (index + 1 < text.Length && text[index + 1] == '\'')
            {
                return index + 2;
            }
        }

        return Unterminated;
    }

    private static char GetClosingDelimiter(char open) =>
        open switch
        {
            '[' => ']',
            '{' => '}',
            '<' => '>',
            '(' => ')',
            _ => open,
        };

    // q or Q, or one of those after n or N, where the prefix does not end an identifier.
    private static bool HasPrefix(string text, int quote)
    {
        if (quote == 0 || text[quote - 1] is not ('q' or 'Q'))
        {
            return false;
        }

        var before = quote - 2;
        if (before >= 0 && text[before] is 'n' or 'N')
        {
            before--;
        }

        return before < 0 || !IsIdentifierCharacter(text[before]);
    }
}
