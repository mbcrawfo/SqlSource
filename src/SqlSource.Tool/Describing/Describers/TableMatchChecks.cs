using System;
using System.Collections.Generic;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.Describers;

/// <summary>
/// The checks of the table match in the one order both describers run them: the values of the sidecar format's
/// <c>tableMatch</c>, in the format's order, where the first that fails is the answer.
/// </summary>
/// <remarks>
/// <para>
/// The order is the format's and is not to be changed: a sidecar says which check failed first.  An engine keeps its
/// own predicate for each check and hands each over as a delegate, and a delegate runs only when every check before it
/// passed, so a check may assume what the ones before it proved.  SQL Server's session relies on it twice: it asks the
/// server for <c>table</c> only when <see cref="OfTheOrigins" /> gives null, and the names check reads the origin of
/// every column.
/// </para>
/// <para>
/// The checks are in two calls because the first two need no table.
/// </para>
/// </remarks>
internal static class TableMatchChecks
{
    /// <summary>
    /// The six values that say a check failed, in the format's order: <c>no-origin</c>, <c>several-tables</c>,
    /// <c>names-differ</c>, <c>columns-differ</c>, <c>order-differs</c> and <c>nullability-differs</c>.
    /// </summary>
    public static IReadOnlyList<string> Order { get; } =
    [
        SidecarValues.TableMatch.NoOrigin,
        SidecarValues.TableMatch.SeveralTables,
        SidecarValues.TableMatch.NamesDiffer,
        SidecarValues.TableMatch.ColumnsDiffer,
        SidecarValues.TableMatch.OrderDiffers,
        SidecarValues.TableMatch.NullabilityDiffers,
    ];

    /// <summary>The first two checks, which need no table: the value of the first that fails, or null.</summary>
    /// <param name="everyColumnHasAnOrigin">
    /// Whether each column has an origin: <c>no-origin</c> when it is false.
    /// </param>
    /// <param name="oneTable">
    /// Whether the origins are one table: <c>several-tables</c> when it is false.  It runs only when every column has
    /// an origin.
    /// </param>
    public static string? OfTheOrigins(Func<bool> everyColumnHasAnOrigin, Func<bool> oneTable) =>
        FirstToFail(0, [everyColumnHasAnOrigin, oneTable]);

    /// <summary>
    /// The last four checks, which read the table: the value of the first that fails, or <c>matched</c>.
    /// </summary>
    /// <param name="sameNames">
    /// Whether each column's name is its origin's: <c>names-differ</c> when it is false.
    /// </param>
    /// <param name="sameColumns">
    /// Whether the origin columns are the table's, as lists: <c>columns-differ</c> when it is false.  It runs only
    /// when the names are the same.
    /// </param>
    /// <param name="sameOrder">
    /// Whether the origin columns are in the table's order: <c>order-differs</c> when it is false.  It runs only when
    /// the columns are the table's.
    /// </param>
    /// <param name="sameNullability">
    /// Whether each column is as nullable as the table's: <c>nullability-differs</c> when it is false.  It runs only
    /// when the order is the same.
    /// </param>
    public static string OfTheTable(
        Func<bool> sameNames,
        Func<bool> sameColumns,
        Func<bool> sameOrder,
        Func<bool> sameNullability
    ) => FirstToFail(2, [sameNames, sameColumns, sameOrder, sameNullability]) ?? SidecarValues.TableMatch.Matched;

    // The checks start at their place in Order, and none runs after the first that fails.
    private static string? FirstToFail(int first, Func<bool>[] checks)
    {
        for (var i = 0; i < checks.Length; i++)
        {
            if (!checks[i]())
            {
                return Order[first + i];
            }
        }

        return null;
    }
}
