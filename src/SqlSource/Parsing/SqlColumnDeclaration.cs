namespace SqlSource.Parsing;

/// <summary>
/// What one <c>-- column:</c> marker declares.
/// </summary>
/// <param name="Name">The column's name, as the database returns it, without the quotes it may be written in.</param>
/// <param name="IsNullable">True for <c>null</c>, false for <c>not null</c>.</param>
/// <param name="Marker">The marker.</param>
internal readonly record struct SqlColumnDeclaration(string Name, bool IsNullable, SqlMarker Marker);
