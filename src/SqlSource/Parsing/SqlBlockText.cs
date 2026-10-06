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
    /// Converts a span of <see cref="Text" /> that lies on one line into the matching span of the file's text.
    /// </summary>
    public TextSpan ToSourceSpan(TextSpan span) => new(Offsets[span.Start], span.Length);
}
