namespace SqlSource.Parsing.Markers;

/// <summary>
/// What one <c>-- column:</c> marker of a query declares.  It sets at least one of <paramref name="IsNullable" />,
/// <paramref name="ClrType" /> and <paramref name="ResetsType" />.
/// </summary>
/// <param name="Name">The column's name, as the database returns it, without the quotes it may be written in.</param>
/// <param name="IsNullable">True for <c>null</c>, false for <c>not null</c>, null when the marker says neither.</param>
/// <param name="ClrType">
/// The C# type after <c>-&gt;</c>, as <c>ClrTypeName.Text</c> writes it, or null when the marker gives none.
/// </param>
/// <param name="ResetsType">
/// Whether the marker says <c>-&gt; default</c>, which takes the column out of the column types of every level.
/// </param>
/// <param name="Marker">The marker.</param>
internal readonly record struct SqlColumnDeclaration(
    string Name,
    bool? IsNullable,
    string? ClrType,
    bool ResetsType,
    SqlMarker Marker
);
