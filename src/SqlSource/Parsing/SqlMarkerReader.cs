using System;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// Recognises marker comments.
/// </summary>
internal static class SqlMarkerReader
{
    private static readonly (string Keyword, SqlMarkerKind Kind)[] Keywords =
    [
        ("name:", SqlMarkerKind.Name),
        ("summary:", SqlMarkerKind.Summary),
        ("sqlsource:", SqlMarkerKind.Directives),
    ];

    /// <summary>
    /// Returns the marker that <paramref name="lexeme" /> is, or null.  A marker is a line comment that is the first
    /// thing on its line and reads <c>--</c>, optional blanks, a keyword, a colon.
    /// </summary>
    public static SqlMarker? Read(string text, SqlLexeme lexeme)
    {
        if (lexeme.Kind != SqlLexemeKind.LineComment || !StartsLine(text, lexeme.Span.Start))
        {
            return null;
        }

        var end = lexeme.Span.End;
        var keywordStart = lexeme.Span.Start + 2;
        while (keywordStart < end && text[keywordStart] is ' ' or '\t')
        {
            keywordStart++;
        }

        foreach (var (keyword, kind) in Keywords)
        {
            var valueStart = keywordStart + keyword.Length;
            if (
                valueStart <= end
                && string.Compare(text, keywordStart, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase)
                    == 0
            )
            {
                return new SqlMarker(kind, lexeme.Span, Trim(text, valueStart, end));
            }
        }

        return null;
    }

    // Only blanks may come before the comment on its line.  The lexer guarantees that a line break found this way is
    // not inside a quoted region or a block comment: those end with their closing delimiter, never with a blank.
    private static bool StartsLine(string text, int start)
    {
        var index = start - 1;
        while (index >= 0 && text[index] is ' ' or '\t')
        {
            index--;
        }

        return index < 0 || text[index] is '\n' or '\r';
    }

    private static TextSpan Trim(string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return TextSpan.FromBounds(start, end);
    }
}
