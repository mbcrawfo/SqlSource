using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SqlSource.Tool.Reporting;

/// <summary>
/// Writes every error of a run, in the compiler's format, and counts them.  The exit code comes from the count.
/// </summary>
/// <remarks>
/// The first line of an error starts in the first column and every other line of it with four spaces.  That is all
/// a reader may rely on to tell them apart, so no text of an error may hold a line break: each is written as a space.
/// </remarks>
internal sealed class Reporter(TextWriter error)
{
    private const string Indent = "    ";

    /// <summary>
    /// How many errors were written.
    /// </summary>
    public int Count { get; private set; }

    public void Report(ToolDiagnostic diagnostic)
    {
        var descriptor = diagnostic.Descriptor;
        object?[] arguments = [.. diagnostic.Arguments.Select(OnOneLine)];
        var message = string.Format(
            CultureInfo.InvariantCulture,
            descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            arguments
        );

        error.WriteLine($"{Origin(diagnostic)}: error {descriptor.Id}: {message}");
        foreach (var line in diagnostic.Lines)
        {
            error.WriteLine($"{Indent}{line.Label}: {OnOneLine(line.Text)}");
        }

        error.WriteLine($"{Indent}see: {descriptor.HelpLinkUri}");
        Count++;
    }

    // MSBuild's own form: a file with a position has no space before the colon, and anything else has one.
    private static string Origin(ToolDiagnostic diagnostic)
    {
        if (diagnostic.Path is null)
        {
            return "sqlsource ";
        }

        return diagnostic.Position is { } position
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{diagnostic.Path}({position.Line + 1},{position.Character + 1})"
            )
            : diagnostic.Path + " ";
    }

    private static string OnOneLine(string text) =>
        text.AsSpan().IndexOfAny('\r', '\n') < 0
            ? text
            : text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');
}
