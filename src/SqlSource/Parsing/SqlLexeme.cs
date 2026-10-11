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
        if (Kind is SqlLexemeKind.Quoted or SqlLexemeKind.Hint or SqlLexemeKind.Parameter)
        {
            return Span;
        }

        if (Kind != SqlLexemeKind.Text)
        {
            return null;
        }

        var content = SqlText.Trim(text, Span.Start, Span.End);
        return content.IsEmpty ? null : content;
    }
}
