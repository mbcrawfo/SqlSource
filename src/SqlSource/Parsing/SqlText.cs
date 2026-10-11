using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The small readings of a text that the lexer, the readers of markers and of tokens and the builder of a block's
/// SQL share.  Each takes offsets and allocates nothing.
/// </summary>
internal static class SqlText
{
    // What ends a line of SQL.  A C# documentation comment has more of them, and XmlDocWriter holds its own list.
    private static readonly char[] LineTerminators = ['\r', '\n'];

    /// <summary>
    /// The span from <paramref name="start" /> up to <paramref name="end" /> without the white space at either
    /// side.  When there is nothing else it is empty, at <paramref name="end" />.
    /// </summary>
    public static TextSpan Trim(string text, int start, int end)
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

    /// <summary>
    /// The offset of the first line terminator from <paramref name="start" /> up to <paramref name="end" />, or
    /// <paramref name="end" /> when there is none.
    /// </summary>
    public static int FindLineEnd(string text, int start, int end)
    {
        var terminator = text.IndexOfAny(LineTerminators, start, end - start);
        return terminator < 0 ? end : terminator;
    }

    /// <summary>
    /// The character at <paramref name="index" />, or <c>'\0'</c> at the end of the text and past it.
    /// </summary>
    public static char CharAt(string text, int index) => index < text.Length ? text[index] : '\0';
}
