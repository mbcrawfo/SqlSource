using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// A marker comment found in a <c>.sql</c> file.
/// </summary>
/// <param name="Kind">Which marker it is.</param>
/// <param name="Span">The whole comment.</param>
/// <param name="ValueSpan">The text after the colon, without surrounding whitespace.  May be empty.</param>
internal readonly record struct SqlMarker(SqlMarkerKind Kind, TextSpan Span, TextSpan ValueSpan);
