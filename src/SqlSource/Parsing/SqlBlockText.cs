using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The SQL of one block after comment handling and whitespace clean-up.
/// </summary>
/// <param name="Text">The SQL.</param>
/// <param name="Offsets">For each character of <paramref name="Text" />, its offset in the file's text.</param>
internal sealed record SqlBlockText(string Text, ImmutableArray<int> Offsets)
{
    /// <summary>
    /// Converts a non-empty span of <see cref="Text" /> that lies on one line into the span of the file's text that it
    /// came from.  The two can differ in length: a stripped block comment is one space here.
    /// </summary>
    public TextSpan ToSourceSpan(TextSpan span) => TextSpan.FromBounds(Offsets[span.Start], Offsets[span.End - 1] + 1);
}
