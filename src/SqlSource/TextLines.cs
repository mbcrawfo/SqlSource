using System;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource;

/// <summary>
/// Finds the lines and columns of a text without making a <see cref="SourceText" />.  A rule shared by the settings
/// files and the parser, so it is in the root namespace.
/// </summary>
internal static class TextLines
{
    /// <summary>
    /// The line and column of the start and the end of <paramref name="span" /> in <paramref name="text" />, the same
    /// as <c>SourceText.From(text).Lines.GetLinePositionSpan(span)</c> gives.  A line ends at <c>\r\n</c>, <c>\r</c>,
    /// <c>\n</c>, <c>U+0085</c>, <c>U+2028</c> or <c>U+2029</c>, and a position between the <c>\r</c> and the
    /// <c>\n</c> of one break is on the line before it.  Nothing is allocated.
    /// </summary>
    public static LinePositionSpan LineSpanOf(string text, TextSpan span)
    {
        var (startLine, startLineStart) = Locate(text, span.Start, 0, 0);
        var (endLine, endLineStart) = Locate(text, span.End, startLine, startLineStart);
        return new LinePositionSpan(
            new LinePosition(startLine, span.Start - startLineStart),
            new LinePosition(endLine, span.End - endLineStart)
        );
    }

    // The line of <position> and the offset that line starts at, counting from a line already known to be at or before
    // it.  A break counts once its last character is before the position.
    private static (int Line, int LineStart) Locate(string text, int position, int line, int lineStart)
    {
        var limit = Math.Min(position, text.Length);
        var index = lineStart;
        while (index < limit)
        {
            if (!IsBreak(text[index]))
            {
                index++;
                continue;
            }

            var end = index + 1;
            if (text[index] == '\r' && end < text.Length && text[end] == '\n')
            {
                end++;
            }

            if (end > position)
            {
                break;
            }

            (line, lineStart, index) = (line + 1, end, end);
        }

        return (line, lineStart);
    }

    private static bool IsBreak(char character) => character is '\n' or '\r' or '\u0085' or '\u2028' or '\u2029';
}
