using System;

namespace SqlSource.Parsing;

/// <summary>
/// Where a list stands in the SQL of a query, as <see cref="SqlListUses" /> reads it.  SQL Server refuses a subquery
/// inside an aggregate, a <c>GROUP BY</c> and an <c>OUTPUT</c> clause, and a subquery is the only empty form of a list
/// that a <c>NOT</c> reaches.  A use that is not a list stands in none of these places.
/// </summary>
[Flags]
internal enum SqlListPlace
{
    /// <summary>None of the places below.</summary>
    None = 0,

    /// <summary>A <c>NOT</c> reaches the list, as in <c>id NOT IN @ids</c> or <c>NOT (id IN @ids)</c>.</summary>
    UnderNot = 1,

    /// <summary>
    /// The list is inside a call to a built-in aggregate, or its <c>WITHIN GROUP</c>, that no <c>OVER</c> follows, as
    /// in <c>SUM(CASE WHEN id IN @ids THEN 1 END)</c>.
    /// </summary>
    InAggregate = 2,

    /// <summary>The list is in a <c>GROUP BY</c> clause.</summary>
    InGroupBy = 4,

    /// <summary>The list is in an <c>OUTPUT</c> clause.</summary>
    InOutput = 8,
}
