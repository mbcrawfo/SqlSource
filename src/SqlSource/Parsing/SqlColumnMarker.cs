using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// What one <c>-- column:</c> marker says of a column of its query's result, with where the marker is.  It sets at
/// least one of <paramref name="IsNullable" />, <paramref name="ClrType" /> and <paramref name="ResetsType" />.
/// </summary>
/// <param name="Name">The column's name, as the database returns it, without the quotes it may be written in.</param>
/// <param name="IsNullable">True for <c>null</c>, false for <c>not null</c>, null when the marker says neither.</param>
/// <param name="ClrType">
/// The C# type after <c>-&gt;</c>, as <c>ClrTypeName.Text</c> writes it, or null when the marker gives none.
/// </param>
/// <param name="ResetsType">
/// Whether the marker says <c>-&gt; default</c>, which takes the column out of the column types of every level.
/// </param>
/// <param name="Span">The whole marker comment in the file.</param>
internal sealed record SqlColumnMarker(string Name, bool? IsNullable, string? ClrType, bool ResetsType, TextSpan Span);
