namespace SqlSource.Parsing.Markers;

/// <summary>
/// What one <c>-- param:</c> marker declares.
/// </summary>
/// <param name="Name">The parameter's name as the marker writes it, without the prefix.</param>
/// <param name="Type">The database type, as written, or null when the marker gives none.</param>
/// <param name="Nullable">True for <c>null</c>, false for <c>not null</c>, null when the marker says neither.</param>
/// <param name="ClrType">
/// The C# type after <c>-&gt;</c>, as <c>ClrTypeName.Text</c> writes it, or null when the marker gives none.
/// </param>
/// <param name="Marker">The marker.</param>
/// <param name="IsJson">
/// Whether the marker writes the word <c>json</c> before the C# type, the JSON opt-in.  Never true without a C# type.
/// </param>
internal readonly record struct SqlParameterDeclaration(
    string Name,
    string? Type,
    bool? Nullable,
    string? ClrType,
    SqlMarker Marker,
    bool IsJson = false
);
