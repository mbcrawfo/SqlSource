using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Diagnostics;

/// <summary>
/// A position in a file, as data.  A <see cref="Location" /> holds on to a syntax tree and has no value equality, so
/// it cannot be cached by the generator pipeline.
/// </summary>
/// <param name="Path">The file's path.</param>
/// <param name="Span">The position as offsets into the file's text.</param>
/// <param name="LineSpan">The same position as lines and columns.</param>
internal sealed record LocationInfo(string Path, TextSpan Span, LinePositionSpan LineSpan)
{
    public static LocationInfo From(Location location) =>
        new(location.GetLineSpan().Path, location.SourceSpan, location.GetLineSpan().Span);

    public static LocationInfo From(string path, SourceText text, TextSpan span) =>
        new(path, span, text.Lines.GetLinePositionSpan(span));

    public Location ToLocation() => Location.Create(Path, Span, LineSpan);
}
