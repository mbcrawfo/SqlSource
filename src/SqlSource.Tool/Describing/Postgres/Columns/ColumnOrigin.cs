using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres.Columns;

/// <summary>
/// The column of a relation that a column of a result comes from (a table, a partitioned table, a view, a
/// materialized view or a foreign table), as the catalog holds both.
/// </summary>
/// <param name="Table">The relation.</param>
/// <param name="Attribute">Its column.</param>
internal sealed record ColumnOrigin(PostgresTableRow Table, PostgresAttributeRow Attribute)
{
    /// <summary>
    /// The origin of each column, in order, or the end of a sentence that says what the answer of <c>tables</c>
    /// lacks of the first column whose origin it does not hold (<see cref="MissingFrom" />).  It asks
    /// <see cref="Find" /> only of a column that <see cref="MissingFrom" /> passed, so nothing it gives throws.
    /// </summary>
    /// <param name="columns">The rows of <c>columns</c>.</param>
    /// <param name="tables">The answer of <c>tables</c>, by OID.</param>
    public static Outcome<ColumnOrigin?[], string> FindAll(
        IReadOnlyList<PostgresColumnRow> columns,
        IReadOnlyDictionary<long, PostgresTableRow> tables
    )
    {
        var origins = new ColumnOrigin?[columns.Count];
        for (var index = 0; index < columns.Count; index++)
        {
            if (MissingFrom(columns[index], tables) is { } missing)
            {
                return Outcome<ColumnOrigin?[], string>.Failed(missing);
            }

            origins[index] = Find(columns[index], tables);
        }

        return Outcome<ColumnOrigin?[], string>.Of(origins);
    }

    /// <summary>
    /// What the answer of <c>tables</c> lacks of the column's origin, or null: when <see cref="Find" /> would answer,
    /// with an origin or with none.  A table or a column goes when another session drops it between two calls of a
    /// describe, which is a failure of the describe and not a bug of the describer.
    /// </summary>
    /// <param name="column">The row of <c>columns</c>.</param>
    /// <param name="tables">The answer of <c>tables</c>, by OID.</param>
    /// <returns>
    /// The end of a sentence that names the column: <c>the table of the column 'id' no longer exists</c>, or
    /// <c>the column 'id' no longer exists in its table 'public.users'</c>.
    /// </returns>
    public static string? MissingFrom(PostgresColumnRow column, IReadOnlyDictionary<long, PostgresTableRow> tables)
    {
        var (expected, table, attribute) = Lookup(column, tables);
        if (!expected || attribute is not null)
        {
            return null;
        }

        return table is null
            ? $"the table of the column '{column.Name}' no longer exists"
            : $"the column '{column.Name}' no longer exists in its table '{table.Schema}.{table.Name}'";
    }

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
        var (expected, table, attribute) = Lookup(column, tables);
        if (!expected)
        {
            return null;
        }

        if (table is null)
        {
            throw new InvalidOperationException(
                $"The answer to 'tables' holds no table {column.TableOid} for the column '{column.Name}'."
            );
        }

        return attribute is null
            ? throw new InvalidOperationException(
                $"The answer to 'tables' holds no attribute {column.AttributeNumber} of the table '{table.Name}' "
                    + $"for the column '{column.Name}'."
            )
            : new ColumnOrigin(table, attribute);
    }

    // Whether the column has an origin to find, and what the answer holds of it.  A column that names no table, an
    // expression, and a system column, which names its table with an attribute number below zero, have none.
    private static (bool Expected, PostgresTableRow? Table, PostgresAttributeRow? Attribute) Lookup(
        PostgresColumnRow column,
        IReadOnlyDictionary<long, PostgresTableRow> tables
    )
    {
        if (column.TableOid == 0 || column.AttributeNumber <= 0)
        {
            return (false, null, null);
        }

        _ = tables.TryGetValue(column.TableOid, out var table);
        return (true, table, table?.Columns.FirstOrDefault(attribute => attribute.Attnum == column.AttributeNumber));
    }
}
