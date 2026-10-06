namespace SqlSource.Parsing.Quoting;

/// <summary>
/// A <c>'...'</c> string that takes backslash escapes only after PostgreSQL's <c>E</c> prefix, as in
/// <c>E'it\'s'</c>.  The prefix stays in the text before the region.
/// </summary>
/// <param name="continues">
/// Whether an <c>E</c> string goes on after whitespace that holds a line break, as PostgreSQL reads it:
/// <c>E'a'</c>, a new line, <c>'b\'c'</c> is one string, and its second part takes backslash escapes too.
/// </param>
internal sealed class EscapeStringReader(bool continues) : QuoteReader
{
    public override int FindEnd(string text, int start)
    {
        if (!HasPrefix(text, start))
        {
            return FindDoubledEnd(text, start);
        }

        var end = FindBackslashEnd(text, start);
        while (continues && end >= 0 && FindContinuation(text, end) is var next && next >= 0)
        {
            end = FindBackslashEnd(text, next);
        }

        return end;
    }

    private static bool HasPrefix(string text, int quote) =>
        quote > 0 && text[quote - 1] is 'E' or 'e' && (quote < 2 || !IsIdentifierCharacter(text[quote - 2]));

    // The offset of the quote that continues a string that closed at end, or -1.  Only whitespace may come between
    // the two, with at least one line break.  A comment there ends the string: a region that could hold a comment
    // could hold a marker, and a marker must never be inside a quoted region.
    private static int FindContinuation(string text, int end)
    {
        var index = end;
        var lineBreak = false;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            lineBreak |= text[index] is '\r' or '\n';
            index++;
        }

        return lineBreak && index < text.Length && text[index] == '\'' ? index : -1;
    }
}
