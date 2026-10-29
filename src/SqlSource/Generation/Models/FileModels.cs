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
        var (low, high) = (0, Queries.Count - 1);
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var models = Queries[middle];
            if (models.Index == queryIndex)
            {
                return string.Equals(models.Name, name, StringComparison.Ordinal) ? models : null;
            }

            if (models.Index < queryIndex)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return null;
    }
}
