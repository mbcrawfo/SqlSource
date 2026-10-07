namespace SqlSource.Parsing.Quoting;

/// <summary>
/// Finds where one kind of quoted region ends.  A quoted region is a string or a quoted identifier: the lexer copies
/// it as written and looks for no comment inside it.
/// </summary>
/// <remarks>
/// A reader holds no state between calls and allocates nothing, so one instance serves every file.  It is called at
/// the character that opens the region, and may look at the characters before it for a prefix such as <c>E</c>.
/// </remarks>
internal abstract class QuoteReader
{
    /// <summary>The region is opened and never closed.</summary>
    public const int Unterminated = -1;

    /// <summary>The character opens no region here, and is plain text.</summary>
    public const int NotAQuote = -2;

    /// <summary>
    /// Returns the offset after the closing delimiter of the region that opens at <paramref name="start" />, or
    /// <see cref="Unterminated" />, or <see cref="NotAQuote" />.
    /// </summary>
    public abstract int FindEnd(string text, int start);

    /// <summary>
    /// The reader of a part that continues the region that opens at <paramref name="start" />, or null when a
    /// later part would be read as the region itself.  A reader reads one part: whether another follows, and after
    /// what, is for the lexer and the rules of the dialect.
    /// </summary>
    public virtual QuoteReader? FindContinuation(string text, int start) => null;

    /// <summary>True for a character that can be part of an unquoted identifier.</summary>
    protected static bool IsIdentifierCharacter(char value) => char.IsLetterOrDigit(value) || value is '_' or '$';

    /// <summary>
    /// The end of a region that closes with the character it opens with, where that character doubled stands for
    /// itself.
    /// </summary>
    protected static int FindDoubledEnd(string text, int start)
    {
        var quote = text[start];
        var index = start + 1;
        while (true)
        {
            var close = text.IndexOf(quote, index);
            if (close < 0)
            {
                return Unterminated;
            }

            if (close + 1 < text.Length && text[close + 1] == quote)
            {
                index = close + 2;
            }
            else
            {
                return close + 1;
            }
        }
    }

    /// <summary>
    /// As <see cref="FindDoubledEnd" />, and a backslash takes the character after it with it, whatever that is.
    /// </summary>
    protected static int FindBackslashEnd(string text, int start)
    {
        var quote = text[start];
        var index = start + 1;
        while (index < text.Length)
        {
            var current = text[index];
            if (current == '\\')
            {
                index += 2;
            }
            else if (current != quote)
            {
                index++;
            }
            else if (index + 1 < text.Length && text[index + 1] == quote)
            {
                index += 2;
            }
            else
            {
                return index + 1;
            }
        }

        return Unterminated;
    }
}
