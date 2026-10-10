using System;

namespace SqlSource.Tool.Reporting;

/// <summary>
/// Keeps a text of the tool's output on its line.  The first column is all that tells the first line of an error
/// from a continuation line, so a line break inside a path, a message or an option would start a line that a
/// build reads as an error of its own.
/// </summary>
internal static class OneLine
{
    /// <summary>
    /// The text with each line break written as a space.
    /// </summary>
    public static string Of(string text) =>
        text.AsSpan().IndexOfAny('\r', '\n') < 0
            ? text
            : text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');
}
