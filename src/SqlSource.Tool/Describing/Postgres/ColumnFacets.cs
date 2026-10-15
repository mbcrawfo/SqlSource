namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// A column's facets, for a capture recorded before the row description's modifier was read: Npgsql gives three
/// numbers that are the modifier decoded, and each is taken only for the types that carry it.  A column whose capture
/// holds the modifier has its facets from <see cref="TypeModifiers" />, and none of what follows applies to it.
/// </summary>
/// <remarks>
/// Three of Npgsql's answers are not the server's.  It gives no scale for a <c>numeric</c> whose scale is zero.  It
/// reads the scale as sixteen bits where PostgreSQL 15 stores eleven, so a negative one arrives 2048 too high.  And
/// an <c>interval</c> with fields and no precision, <c>interval year to month</c>, has the precision 65535.  An
/// array is given the numbers of its element, which are dropped: the internal name of an array carries none.
/// </remarks>
internal static class ColumnFacets
{
    private const int NoIntervalPrecision = 65535;
    private const int FirstNegativeScale = 1024;
    private const int ScaleRange = 2048;

    /// <summary>The facets of a column.</summary>
    /// <param name="typname">
    /// The internal name of the type the protocol reported: <c>varchar</c>, <c>numeric</c>.
    /// </param>
    /// <param name="column">The column, with Npgsql's three numbers.</param>
    public static PostgresFacets Of(string typname, PostgresColumnRow column) =>
        typname switch
        {
            "bpchar" or "varchar" or "bit" or "varbit" => new PostgresFacets(column.Length, null, null),
            "numeric" when column.Precision is { } precision => new PostgresFacets(
                null,
                precision,
                ScaleOf(column.Scale)
            ),
            "time" or "timetz" or "timestamp" or "timestamptz" => new PostgresFacets(null, column.Precision, null),
            "interval" when column.Precision != NoIntervalPrecision => new PostgresFacets(null, column.Precision, null),
            _ => PostgresFacets.None,
        };

    private static int ScaleOf(int? scale) =>
        scale switch
        {
            null => 0,
            >= FirstNegativeScale => scale.Value - ScaleRange,
            _ => scale.Value,
        };
}
