using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// A run of a <c>.sql</c> file's text with one meaning.  The lexemes of a file cover it without gaps.
/// </summary>
/// <param name="Kind">What the text is.</param>
/// <param name="Span">Where it is, as offsets into the file's text.</param>
internal readonly record struct SqlLexeme(SqlLexemeKind Kind, TextSpan Span)
{
    /// <summary>
    /// Returns the part of this lexeme that counts as SQL content, or null when it has none.  Comments and whitespace
    /// are not content.
    /// </summary>
    public TextSpan? GetContentSpan(string text)
    {
        if (Kind is SqlLexemeKind.Quoted or SqlLexemeKind.Hint)
        {
            return Span;
        }

        if (Kind != SqlLexemeKind.Text)
        {
            return null;
        }

        var start = Span.Start;
        var end = Span.End;
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return start == end ? null : TextSpan.FromBounds(start, end);
    }
}
