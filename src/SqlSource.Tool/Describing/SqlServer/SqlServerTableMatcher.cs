using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>
/// Whether a query's result is exactly one table's or view's column list, so that the generator can name the model
/// after it and share it between every query that matches it.
/// </summary>
/// <remarks>
/// <para>
/// The checks are the values of the sidecar format's <c>tableMatch</c>, in the format's order, and the first that
/// fails is the answer: the order is the format's and is not to be changed.  A view may match as a table may.
/// </para>
/// <para>
/// The table's column list and each column's nullability are the server's description of <c>SELECT *</c> from it,
/// the answer of <c>table</c>, and not the catalog's.  So a period column declared <c>HIDDEN</c> is out of the list,
/// as <c>SELECT *</c> leaves it out; a computed column is in it; and a column is the table's in nullability when it
/// has what the server says of it read whole.  The server judges a computed column by its expression, so a
/// <c>PERSISTED NOT NULL</c> one may be nullable in every query, and against the catalog's flag no table with one
/// would ever match.
/// </para>
/// </remarks>
internal static class SqlServerTableMatcher
{
    /// <summary>
    /// The table every column comes from, its schema and table with no column, or null when a column has no origin or
    /// the columns come from two.  The session asks <c>table</c> for it alone.
    /// </summary>
    /// <param name="columns">The columns of a statement with rows, in order.</param>
    public static SidecarOrigin? TableOf(IReadOnlyList<SidecarColumn> columns) =>
        columns.Count == 0 ? null : OneTable(columns).Table;

    /// <summary>
    /// The table whose column list the columns are, or null, and <c>matched</c> or the first check that failed.
    /// </summary>
    /// <param name="columns">The columns of a statement with rows, in order, as they will be recorded.</param>
    /// <param name="table">
    /// The visible rows of <c>table</c>'s answer for <see cref="TableOf" />'s table, or null when it was not asked
    /// because the first two checks failed.  A hidden row is no column of the table.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// There is no column, or the first two checks passed with no table: a bug of the describer.
    /// </exception>
    public static (SidecarTable? Table, string Result) Match(
        IReadOnlyList<SidecarColumn> columns,
        IReadOnlyList<SqlServerColumnRow>? table
    )
    {
        if (columns.Count == 0)
        {
            throw new InvalidOperationException("The table match was asked for no columns.");
        }

        var (found, failed) = OneTable(columns);
        if (failed is not null)
        {
            return (null, failed);
        }

        if (found is null || table is null)
        {
            throw new InvalidOperationException(
                "The table match was asked without the table that every column comes from."
            );
        }

        string[] origins = [.. columns.Select(static column => column.Origin!.Column!)];
        SqlServerColumnRow[] visible = [.. table.Where(static row => !row.IsHidden)];

        // The name as the server returned it: an alias and an override suffix are each another name.
        if (!columns.Select(static column => column.Name).SequenceEqual(origins, StringComparer.Ordinal))
        {
            return (null, SidecarValues.TableMatch.NamesDiffer);
        }

        // As lists and not as sets: a column that is left out and a column that is there twice both differ.
        string?[] names = [.. visible.Select(static row => row.Name)];
        if (
            !origins
                .Order(StringComparer.Ordinal)
                .SequenceEqual(names.Order(StringComparer.Ordinal), StringComparer.Ordinal)
        )
        {
            return (null, SidecarValues.TableMatch.ColumnsDiffer);
        }

        if (!origins.SequenceEqual(names, StringComparer.Ordinal))
        {
            return (null, SidecarValues.TableMatch.OrderDiffers);
        }

        // In the table's order now, so each column's row is the one in its place.  A column the server cannot say of
        // is nullable, as the describer records it.
        return columns
            .Select(static column => column.Nullable)
            .SequenceEqual(visible.Select(static row => (bool?)(row.IsNullable ?? true)))
            ? (new SidecarTable(found.Schema, found.Table), SidecarValues.TableMatch.Matched)
            : (null, SidecarValues.TableMatch.NullabilityDiffers);
    }

    // The table of the columns, or the first of no-origin and several-tables that fails.  Schemas and tables are
    // compared ordinally, as the server gave both.
    private static (SidecarOrigin? Table, string? Failed) OneTable(IReadOnlyList<SidecarColumn> columns)
    {
        if (columns.Any(static column => column.Origin is not { Schema: not null, Table: not null, Column: not null }))
        {
            return (null, SidecarValues.TableMatch.NoOrigin);
        }

        var first = columns[0].Origin!;
        return columns.All(column =>
            string.Equals(column.Origin!.Schema, first.Schema, StringComparison.Ordinal)
            && string.Equals(column.Origin.Table, first.Table, StringComparison.Ordinal)
        )
            ? (new SidecarOrigin(first.Schema, first.Table, null), null)
            : (null, SidecarValues.TableMatch.SeveralTables);
    }
}
