using System;
using SqlSource.Diagnostics;

namespace SqlSource.Generation.Models;

/// <summary>The models of the queries of one claimed file, and the errors of their names.</summary>
/// <param name="NormalizedPath">The path of the <c>.sql</c> file, in the form <c>SqlPath.Normalize</c> gives.</param>
/// <param name="FileName">
/// The file's name with its extension, or empty when no entry of the file is usable, since it then has no models.
/// </param>
/// <param name="Queries">
/// The models of each query that has them, in the order of the file.  A query with an error has none.
/// </param>
/// <param name="Diagnostics">
/// The errors of the queries' names, each at its query's name, and <c>SQLSRC259</c> and <c>SQLSRC260</c>, each at its
/// marker.
/// </param>
internal sealed record FileModels(
    string NormalizedPath,
    string FileName,
    EquatableArray<QueryModels> Queries,
    EquatableArray<DiagnosticInfo> Diagnostics
)
{
    /// <summary>
    /// The models of the query at an index in the file, matched by index and name: the models are read from the same
    /// parse, so the two agree, and a mismatch is passed over rather than thrown on.  The models are in the order of
    /// the file's queries, so a search finds them.
    /// </summary>
    /// <param name="queryIndex">The query's index among the queries of the file.</param>
    /// <param name="name">The query's name.</param>
    /// <returns>The models, or null when the query has none or the model at the index has another name.</returns>
    public QueryModels? Find(int queryIndex, string name)
    {
        // A query has models at most once and the indices rise, so its models are at or before its own index, and
        // at it when every query before it has models, as is usual.
        var count = Math.Min(Queries.Count, queryIndex + 1);
        var at =
            count > 0 && Queries[count - 1].Index == queryIndex
                ? count - 1
                : SortedSearch.IndexOf(count, new QueryProbe(Queries, queryIndex));
        return at >= 0 && string.Equals(Queries[at].Name, name, StringComparison.Ordinal) ? Queries[at] : null;
    }

    // A struct, so that SortedSearch does not box it.
    private readonly struct QueryProbe(EquatableArray<QueryModels> queries, int queryIndex) : SortedSearch.IProbe
    {
        public int CompareAt(int index) => queries[index].Index.CompareTo(queryIndex);
    }
}
