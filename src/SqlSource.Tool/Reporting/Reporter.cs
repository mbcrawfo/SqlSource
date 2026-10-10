using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SqlSource.Tool.Reporting;

/// <summary>
/// Writes every error of a run, in the compiler's format, and counts them.  The exit code comes from the count.
/// </summary>
/// <remarks>
/// <para>
/// The first line of an error starts in the first column and every other line of it with four spaces.  That is all
/// a reader may rely on to tell them apart, so no text of an error may hold a line break: each is written as a space.
/// </para>
/// <para>
/// Several threads may report.  An error is one write, under a lock, so the lines of two errors never mix, whatever
/// the writer is.
/// </para>
/// </remarks>
internal sealed class Reporter(TextWriter error)
{
    private const string Indent = "    ";

    private readonly object _gate = new();

    private int _count;

    /// <summary>
    /// How many errors were written.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    public void Report(ToolDiagnostic diagnostic)
    {
        var text = Format(diagnostic, error.NewLine);
        lock (_gate)
        {
            error.Write(text);
            _count++;
        }
    }

    private static string Format(ToolDiagnostic diagnostic, string newLine)
    {
        var descriptor = diagnostic.Descriptor;
        object?[] arguments = [.. diagnostic.Arguments.Select(OneLine.Of)];
        var message = string.Format(
            CultureInfo.InvariantCulture,
            descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            arguments
        );

        var text = new StringBuilder();
        _ = text.Append(Origin(diagnostic)).Append(": error ").Append(descriptor.Id).Append(": ").Append(message);
        _ = text.Append(newLine);
        foreach (var line in diagnostic.Lines)
        {
            _ = text.Append(Indent).Append(OneLine.Of(line.Label)).Append(": ").Append(OneLine.Of(line.Text));
            _ = text.Append(newLine);
        }

        _ = text.Append(Indent).Append("see: ").Append(descriptor.HelpLinkUri).Append(newLine);
        return text.ToString();
    }

    // MSBuild's own form: a file with a position has no space before the colon, and anything else has one.
    private static string Origin(ToolDiagnostic diagnostic)
    {
        if (diagnostic.Path is null)
        {
            return "sqlsource ";
        }

        var path = OneLine.Of(diagnostic.Path);
        return diagnostic.Position is { } position
            ? string.Create(CultureInfo.InvariantCulture, $"{path}({position.Line + 1},{position.Character + 1})")
            : path + " ";
    }
}
