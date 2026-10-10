using System;

namespace SqlSource.Tool.Planning;

/// <summary>
/// Why a query that needs an entry cannot be described.  A query can have both.
/// </summary>
[Flags]
internal enum QueryProblems
{
    /// <summary>Nothing: almost every query.</summary>
    None = 0,

    /// <summary>A token of the query has no default to stand in its place (<c>SQLSRC210</c>).</summary>
    TokenWithoutDefault = 1,

    /// <summary>The query's database has another dialect than the query's file (<c>SQLSRC211</c>).</summary>
    DatabaseDialectConflict = 2,
}
