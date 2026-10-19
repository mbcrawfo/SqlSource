using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// What one <c>-- column:</c> marker says of a column of its query's result, with where the marker is.
/// </summary>
/// <param name="Name">The column's name, as the database returns it, without the quotes it may be written in.</param>
/// <param name="IsNullable">True for <c>null</c>, false for <c>not null</c>.</param>
/// <param name="Span">The whole marker comment in the file.</param>
internal sealed record SqlColumnMarker(string Name, bool IsNullable, TextSpan Span);
