using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The column of a table or a view that a column of a result comes from, as the catalog holds both.
/// </summary>
/// <param name="Table">The relation.</param>
/// <param name="Attribute">Its column.</param>
internal sealed record ColumnOrigin(PostgresTableRow Table, PostgresAttributeRow Attribute)
{
    /// <summary>
    /// The origin of a column, or null: for a column that names no table, an expression for one, and for a system
    /// column, <c>ctid</c> or <c>xmin</c>, which names its table with an attribute number below zero.
    /// </summary>
    /// <param name="column">The row of <c>columns</c>.</param>
    /// <param name="tables">The answer of <c>tables</c>, by OID.</param>
    /// <exception cref="InvalidOperationException">
    /// The answer lacks the table or the attribute that the column names: a bug of the describer.
    /// </exception>
    public static ColumnOrigin? Find(PostgresColumnRow column, IReadOnlyDictionary<long, PostgresTableRow> tables)
    {
        if (column.TableOid == 0 || column.AttributeNumber <= 0)
        {
            return null;
        }

        if (!tables.TryGetValue(column.TableOid, out var table))
        {
            throw new InvalidOperationException(
                $"The answer to 'tables' holds no table {column.TableOid} for the column '{column.Name}'."
            );
        }

        return table.Columns.FirstOrDefault(attribute => attribute.Attnum == column.AttributeNumber) is { } found
            ? new ColumnOrigin(table, found)
            : throw new InvalidOperationException(
                $"The answer to 'tables' holds no attribute {column.AttributeNumber} of the table '{table.Name}' "
                    + $"for the column '{column.Name}'."
            );
    }
}
