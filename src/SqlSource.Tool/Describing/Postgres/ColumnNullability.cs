using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Whether a column can be <c>NULL</c>, as far as the catalog can answer, and where the answer came from.  The
/// describer records; the generator decides what an unknown means.
/// </summary>
/// <remarks>
/// An answer is never "not nullable" unless the catalog says <c>NOT NULL</c>, the origin is a table, and no outer
/// join can have touched the column.  Until the walk exists, a gated query makes every column of a table
/// nullable.
/// </remarks>
internal static class ColumnNullability
{
    /// <summary>The first row of the table that fits.</summary>
    /// <param name="origin">Where the column comes from, or null.</param>
    /// <param name="gated">Whether <see cref="OuterJoinGate" /> found a word in the query.</param>
    public static (bool? Nullable, string Source) Of(ColumnOrigin? origin, bool gated) =>
        origin switch
        {
            null => (null, SidecarValues.NullableSource.NoOrigin),

            // PostgreSQL records no NOT NULL for a view's column and does not enforce a foreign table's.
            { Table.Relkind: "v" or "m" or "f" } => (null, SidecarValues.NullableSource.View),
            _ when gated => (true, SidecarValues.NullableSource.Heuristic),
            { } found => (!found.Attribute.NotNull, SidecarValues.NullableSource.Catalog),
        };
}
