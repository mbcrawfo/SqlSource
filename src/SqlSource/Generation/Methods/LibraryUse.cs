using System.Collections.Generic;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;

namespace SqlSource.Generation.Methods;

/// <summary>
/// A library as a query of one engine uses it, or as it is older than its floor for that engine's queries: a
/// library's packages differ by engine, so that NetTopologySuite.IO.SqlServerBytes, which only SQL Server's queries
/// use, never stops a PostgreSQL query.
/// </summary>
/// <param name="Library">The library.</param>
/// <param name="Engine">The engine of the queries.</param>
internal readonly record struct LibraryUse(SqlLibrary Library, DatabaseEngine Engine)
{
    /// <summary>The order of the uses: by the library, then the engine, in the order of their members.</summary>
    public static readonly IComparer<LibraryUse> Order = Comparer<LibraryUse>.Create(
        // By the values, since an enum's own CompareTo boxes.
        static (left, right) =>
            left.Library != right.Library
                ? ((int)left.Library).CompareTo((int)right.Library)
                : ((int)left.Engine).CompareTo((int)right.Engine)
    );
}
