namespace SqlSource.Parsing;

/// <summary>
/// What one <c>-- param:</c> marker declares.
/// </summary>
/// <param name="Name">The parameter's name as the marker writes it, without the prefix.</param>
/// <param name="Type">The database type, as written, or null when the marker gives none.</param>
/// <param name="Nullable">True for <c>null</c>, false for <c>not null</c>, null when the marker says neither.</param>
/// <param name="Marker">The marker.</param>
internal readonly record struct SqlParameterDeclaration(string Name, string? Type, bool? Nullable, SqlMarker Marker);
