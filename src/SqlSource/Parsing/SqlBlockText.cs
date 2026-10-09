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

    // The last run that starts at or before the offset is the one that holds it.
    private int ToSource(int offset)
    {
        var low = 0;
        var high = runs.Length - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (runs[middle].Output <= offset)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return runs[low].Source + (offset - runs[low].Output);
    }
}
