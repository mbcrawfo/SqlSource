using System;
using System.Collections.Generic;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// A column of a sidecar, from what <c>columns</c> answered for it and the rows of <c>types</c>.
/// </summary>
internal static class PostgresColumns
{
    /// <summary>The column, with its type, its facets and its origin.</summary>
    /// <param name="column">The row of <c>columns</c>.</param>
    /// <param name="origin">Where the column comes from, or null.</param>
    /// <param name="rows">The rows of the closure, by OID.</param>
    /// <exception cref="InvalidOperationException">
    /// The rows lack a type of the column, or the type is a pseudo-type.
    /// </exception>
    public static SidecarColumn Build(
        PostgresColumnRow column,
        ColumnOrigin? origin,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    ) =>
        new(
            column.Ordinal,
            column.Name,
            PostgresTypes.Build(ColumnTypes.OidOf(column, origin, rows), rows, FacetsOf(column, rows)),
            null,
            SidecarValues.NullableSource.NoOrigin,
            origin is null ? null : new SidecarOrigin(origin.Table.Schema, origin.Table.Name, origin.Attribute.Name),
            origin is null ? null : origin.Attribute.Identity.Length > 0,
            origin is null ? null : origin.Attribute.Generated.Length > 0
        );

    // The facets are read by the type the protocol reported, which is the one whose modifier they decode.
    private static PostgresFacets FacetsOf(PostgresColumnRow column, IReadOnlyDictionary<long, PostgresTypeRow> rows) =>
        rows.TryGetValue(column.TypeOid, out var row)
            ? ColumnFacets.Of(row.Typname, column)
            : throw new InvalidOperationException(
                $"The answer to 'types' holds no row for the type {column.TypeOid} of the column '{column.Name}'."
            );
}
