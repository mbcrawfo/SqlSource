using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SqlSource.Diagnostics;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Reporting;

/// <summary>
/// Writes every error of a run, in the compiler's format, and counts them.  The exit code comes from the count.
/// </summary>
/// <remarks>
/// <para>
/// The format is a contract with MSBuild and with editors:
/// <c>&lt;path&gt;(&lt;line&gt;,&lt;column&gt;): error &lt;id&gt;: &lt;message&gt;</c>, or <c>&lt;path&gt; : error</c>,
/// or <c>sqlsource : error</c>; then continuation lines, each four spaces, a label, a colon, a space and a text; then
/// <c>see:</c>, indented the same, with the descriptor's help link, last.
/// </para>
/// <para>
/// The first line of an error starts in the first column and every other line of it with four spaces.  That is all
/// a reader may rely on to tell them apart, so no text of an error may hold a line break: each is written as a space.
/// </para>
/// <para>
/// Several threads may report.  An error is one write, under a lock, so the lines of two errors never mix, whatever
/// the writer is.
/// </para>
/// <para>
/// Under <c>--online</c> an error is one line: the continuation lines follow the first, each as <c>; label: text</c>,
/// and <c>; see: link</c> last.  A build reads what the tool writes one line at a time and takes each line with
/// <c>error</c> in it for an error of its own, so no line but a first line may be written.
/// </para>
/// <para>
/// An error may be <see cref="ReportWithoutWriting">reported without being written</see>, as the build's run does for
/// an error that the compiler reports too: it has every effect of an error but its line, so the run deletes nothing
/// after it, and the exit code does not count it, since a build reads a non-zero exit code with no error line as a
/// tool that failed.  <c>diagnose</c> reports so an error of the plan at another <c>.sql</c> file than its query's.
/// </para>
/// </remarks>
/// <param name="error">Where the errors are written.</param>
/// <param name="trace">
/// What the run says of itself beside its errors, when it has one: every error is then also an event of its log.
/// </param>
/// <param name="oneLine">Whether every error is written on one line, as the build that runs the tool reads it.</param>
internal sealed class Reporter(TextWriter error, RunTrace? trace = null, bool oneLine = false)
{
    private const string Indent = "    ";

    private readonly object _gate = new();

    private int _count;
    private int _unwritten;
    private int _differences;

    /// <summary>
    /// How many errors were reported, whether or not their lines were written.  A run that has any deletes no
    /// sidecar.
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

    /// <summary>
    /// How many of the errors counted by <see cref="Count" /> were reported without being written.  The exit code is
    /// 1 only for an error that was written.
    /// </summary>
    public int UnwrittenCount
    {
        get
        {
            lock (_gate)
            {
                return _unwritten;
            }
        }
    }

    /// <summary>
    /// How many of the errors written were <c>SQLSRC219</c>, a sidecar that <c>--check</c> found out of date.  A
    /// run that wrote no other error exits with <c>2</c>.
    /// </summary>
    public int DifferenceCount
    {
        get
        {
            lock (_gate)
            {
                return _differences;
            }
        }
    }

    /// <summary>
    /// What the run says of itself beside its errors.  <c>Cli</c> makes the reporter of a <c>describe</c> that runs
    /// with the run's, which has its log when there is one.  A reporter that was given none says nothing.
    /// </summary>
    public RunTrace Trace { get; } = trace ?? new RunTrace(NullRunLog.Instance);

    public void Report(ToolDiagnostic diagnostic) => Report(diagnostic, write: true);

    /// <summary>
    /// Reports an error that is not written: it is counted and logged as any is, and its line is left out because
    /// another reports it, as the compiler does an error of the generator's own that the run found in the same file.
    /// </summary>
    public void ReportWithoutWriting(ToolDiagnostic diagnostic) => Report(diagnostic, write: false);

    private void Report(ToolDiagnostic diagnostic, bool write)
    {
        var message = MessageOf(diagnostic);
        var text = oneLine
            ? FormatOnOneLine(diagnostic, message, error.NewLine)
            : Format(diagnostic, message, error.NewLine);
        lock (_gate)
        {
            if (write)
            {
                error.Write(text);
            }
            else
            {
                _unwritten++;
            }

            _count++;
            if (ReferenceEquals(diagnostic.Descriptor, ToolDiagnostics.SidecarOutOfDate))
            {
                _differences++;
            }

            // Under the same lock as the console, to the trace the reporter was made with.
            Trace.Log.Write(
                "diagnostic",
                new("id", diagnostic.Descriptor.Id),
                new("path", diagnostic.Path),
                new("line", LogValue.Of(diagnostic.Position?.Line + 1)),
                new("column", LogValue.Of(diagnostic.Position?.Character + 1)),
                new("message", message),
                new(
                    "lines",
                    LogValue.ListOf(
                        diagnostic.Lines.Select(static line =>
                            LogValue.MapOf(new("label", OneLine.Of(line.Label)), new("text", OneLine.Of(line.Text)))
                        )
                    )
                )
            );
        }
    }

    private static string MessageOf(ToolDiagnostic diagnostic)
    {
        object?[] arguments = [.. diagnostic.Arguments.Select(OneLine.Of)];
        return string.Format(
            CultureInfo.InvariantCulture,
            diagnostic.Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            arguments
        );
    }

    private static string Format(ToolDiagnostic diagnostic, string message, string newLine)
    {
        var descriptor = diagnostic.Descriptor;
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

    private static string FormatOnOneLine(ToolDiagnostic diagnostic, string message, string newLine)
    {
        var text = new StringBuilder();
        _ = text.Append(Origin(diagnostic)).Append(": error ").Append(diagnostic.Descriptor.Id).Append(": ");
        _ = text.Append(message);
        foreach (var line in diagnostic.Lines)
        {
            _ = text.Append("; ").Append(OneLine.Of(line.Label)).Append(": ").Append(OneLine.Of(line.Text));
        }

        return text.Append("; see: ").Append(diagnostic.Descriptor.HelpLinkUri).Append(newLine).ToString();
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
