using System;
using System.Collections.Generic;

namespace SqlSource.Tool.Describing.Postgres.Columns;

/// <summary>
/// Which type a column has.  The row description flattens a domain to its base type with the domain's modifier,
/// so only the catalog knows that a column is a domain.
/// </summary>
internal static class ColumnTypes
{
    /// <summary>
    /// The origin attribute's type when it is a domain whose chain of bases reaches the type the protocol
    /// reported; else the reported type.  Both are the server's answers: the one that names the domain says more,
    /// and an expression over a domain has no origin and says the base type, which is also true.
    /// </summary>
    /// <param name="column">The row of <c>columns</c>.</param>
    /// <param name="origin">Where the column comes from, or null.</param>
    /// <param name="rows">The rows of the closure, by OID.</param>
    /// <exception cref="InvalidOperationException">The rows lack a type of the origin's chain.</exception>
    public static long OidOf(
        PostgresColumnRow column,
        ColumnOrigin? origin,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    )
    {
        if (origin is null)
        {
            return column.TypeOid;
        }

        var oid = origin.Attribute.TypeOid;
        while (PostgresTypes.Row(oid, rows) is { Typtype: "d" } domain)
        {
            oid = domain.Typbasetype;
            if (oid == column.TypeOid)
            {
                return origin.Attribute.TypeOid;
            }
        }

        return column.TypeOid;
    }
}
