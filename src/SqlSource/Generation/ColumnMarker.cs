using SqlSource.Diagnostics;

namespace SqlSource.Generation;

/// <summary>
/// What one <c>-- column:</c> marker says of a column of its query's result, with where the marker is.
/// </summary>
/// <param name="Name">The column's name, as the database returns it, without the quotes it may be written in.</param>
/// <param name="IsNullable">True for <c>null</c>, false for <c>not null</c>.</param>
/// <param name="Location">The whole marker comment in the file.</param>
internal sealed record ColumnMarker(string Name, bool IsNullable, LocationInfo Location);
