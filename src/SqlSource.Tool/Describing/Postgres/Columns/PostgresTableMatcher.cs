using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Postgres.Columns;

/// <summary>
/// Whether a query's result is exactly one relation's column list, so that the generator can name the model after
/// the relation and share it between every query that matches it.
/// </summary>
/// <remarks>
/// The checks are the values of the sidecar format's <c>tableMatch</c>, in the format's order, which
/// <see cref="TableMatchChecks" /> holds, and the first that fails is the answer: the order is the format's and is
/// not to be changed.  A view, a materialized view and a foreign table may match as a table may.  The last check
/// reads the nullability each column was given, so the match is made after everything that sets one.  It asks the
/// server nothing: an origin holds its relation with every attribute that is not dropped, as <c>tables</c>
/// answered it.
/// </remarks>
internal static class PostgresTableMatcher
{
    /// <summary>
    /// The relation whose column list the columns are, or null, and <c>matched</c> or the first check that failed.
    /// </summary>
    /// <param name="columns">The columns of a statement with rows, in order, as they will be recorded.</param>
    /// <param name="origins">Where each column comes from, or null, in the same order.</param>
    /// <exception cref="InvalidOperationException">
    /// There is no column, or not one origin for each: a bug of the describer.
    /// </exception>
    public static (SidecarTable? Table, string Result) Match(
        IReadOnlyList<SidecarColumn> columns,
        IReadOnlyList<ColumnOrigin?> origins
    ) =>
        MatchBy(
            columns,
            origins,
            // What the relation's column has is what a query that is not gated records for it: the rule of a NOT
            // NULL, of one that is not validated and of a view is ColumnNullability's, and is not written twice.
            found =>
                columns
                    .Select(static column => column.Nullable)
                    .SequenceEqual(found.Select(static origin => ColumnNullability.Of(origin, gated: false).Nullable))
        );

    /// <summary>
    /// Whether the columns are one relation's whole column list, each under its name and in its order: every check of
    /// <see cref="Match" /> but nullability.  Such a query gets the model named after the relation, which every query
    /// of the list shares, so the walk proves none of its view's or written columns not null and it keeps the
    /// relation's answers.
    /// </summary>
    /// <param name="columns">The columns of a statement, in order.</param>
    /// <param name="origins">Where each column comes from, or null, in the same order.</param>
    public static bool ListsARelation(IReadOnlyList<SidecarColumn> columns, IReadOnlyList<ColumnOrigin?> origins) =>
        columns.Count > 0 && MatchBy(columns, origins, static _ => true).Result == SidecarValues.TableMatch.Matched;

    private static (SidecarTable? Table, string Result) MatchBy(
        IReadOnlyList<SidecarColumn> columns,
        IReadOnlyList<ColumnOrigin?> origins,
        Func<ColumnOrigin[], bool> sameNullability
    )
    {
        if (columns.Count == 0 || columns.Count != origins.Count)
        {
            throw new InvalidOperationException(
                $"The table match was asked for {columns.Count} columns with {origins.Count} origins."
            );
        }

        ColumnOrigin[] found = [.. origins.OfType<ColumnOrigin>()];
        var failed = TableMatchChecks.OfTheOrigins(
            () => found.Length == columns.Count,
            () => found.All(origin => origin.Table.Oid == found[0].Table.Oid)
        );
        if (failed is not null)
        {
            return (null, failed);
        }

        var table = found[0].Table;
        int[] numbers = [.. found.Select(static origin => origin.Attribute.Attnum)];
        var result = TableMatchChecks.OfTheTable(
            // The name as the server returned it: an alias and an override suffix are each another name.
            () =>
                columns
                    .Select(static column => column.Name)
                    .SequenceEqual(found.Select(static origin => origin.Attribute.Name), StringComparer.Ordinal),
            // As lists and not as sets: a column that is left out and a column that is there twice both differ.
            () => numbers.Order().SequenceEqual(table.Columns.Select(static attribute => attribute.Attnum).Order()),
            () => numbers.SequenceEqual(numbers.Order()),
            () => sameNullability(found)
        );
        return result == SidecarValues.TableMatch.Matched
            ? (new SidecarTable(table.Schema, table.Name), result)
            : (null, result);
    }
}
