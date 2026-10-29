using System;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The SQL of one block after comment handling and whitespace clean-up.
/// </summary>
/// <param name="text">The SQL.</param>
/// <param name="runs">
/// Where the SQL came from.  Each entry says that the text from offset <c>Output</c> onward was copied from the file
/// starting at offset <c>Source</c>, up to the next entry.  The entries are ordered by <c>Output</c>.
/// </param>
/// <param name="parameters">Where each parameter of the SQL is in the text, prefix included, in order.</param>
internal sealed class SqlBlockText(string text, (int Output, int Source)[] runs, TextSpan[] parameters)
{
    public string Text { get; } = text;

    /// <summary>
    /// Where each parameter of the SQL is in <see cref="Text" />, prefix included, in order.
    /// </summary>
    public TextSpan[] Parameters { get; } = parameters;

    /// <summary>
    /// Converts a non-empty span of <see cref="Text" /> that lies on one line into the span of the file's text that it
    /// came from.  The two can differ in length: a stripped block comment is one space here.
    /// </summary>
    public TextSpan ToSourceSpan(TextSpan span) =>
        TextSpan.FromBounds(ToSource(span.Start), ToSource(span.End - 1) + 1);

    /// <summary>
    /// Converts an offset of the file's text into the offset of <see cref="Text" /> where the text copied from before
    /// it ends.  Text that is not copied, such as a stripped comment, maps to where the copied text before it ends.
    /// </summary>
    public int ToOutput(int sourceOffset)
    {
        // The runs' Source offsets increase as their Output offsets do, so the same search finds the run.
        var run = FindRun(sourceOffset, bySource: true);
        if (run < 0)
        {
            return 0;
        }

        // A run ends where the next begins, or where the text does, less the line breaks written between two lines,
        // which belong to no run.  A run's text holds no line break but as a run of its own, of one character.
        var (output, source) = runs[run];
        var end = Math.Min(
            run + 1 < runs.Length ? runs[run + 1].Output : Text.Length,
            output + (sourceOffset - source)
        );
        while (end - 1 > output && Text[end - 1] == '\n')
        {
            end--;
        }

        return end;
    }

    // The last run that starts at or before the offset is the one that holds it.
    private int ToSource(int offset)
    {
        var (output, source) = runs[Math.Max(FindRun(offset, bySource: false), 0)];
        return source + (offset - output);
    }

    // The index of the last run that starts at or before the offset, in the source or in the output, or -1 when
    // there is none.
    private int FindRun(int offset, bool bySource) =>
        SortedSearch.LowerBound(runs.Length, new RunProbe(runs, offset, bySource)) - 1;

    // Negative for a run that starts at or before the offset.  A struct, so that SortedSearch does not box it.
    private readonly struct RunProbe((int Output, int Source)[] runs, int offset, bool bySource) : SortedSearch.IProbe
    {
        public int CompareAt(int index)
        {
            var start = bySource ? runs[index].Source : runs[index].Output;
            return start <= offset ? -1 : 1;
        }
    }
}
