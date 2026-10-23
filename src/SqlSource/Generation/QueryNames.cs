using System;

namespace SqlSource.Generation;

/// <summary>
/// Finds a query of a file by its name, for the steps that walk a file's entries and its queries together.
/// </summary>
internal static class QueryNames
{
    /// <summary>
    /// The index of the first of <paramref name="queries" /> at or after <paramref name="from" /> whose name is
    /// <paramref name="name" />, compared ordinally, or -1 when there is none.
    /// </summary>
    public static int IndexOf(EquatableArray<SqlQuery> queries, string name, int from)
    {
        for (var index = from; index < queries.Count; index++)
        {
            if (string.Equals(queries[index].Name, name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
