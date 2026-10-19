using System;
using SqlSource.Diagnostics;
using SqlSource.Generation.Paths;

namespace SqlSource.Generation.Models;

/// <summary>The query a request of a model comes from, and the type that asked for it.</summary>
/// <param name="NormalizedPath">The path of the query's file, in the form <c>SqlPath.Normalize</c> gives.</param>
/// <param name="FileName">The file's name with its extension, as the model's documentation names it.</param>
/// <param name="QueryIndex">The query's index among the queries of its file.</param>
/// <param name="QueryName">The query's name.</param>
/// <param name="ClaimingType">
/// The type that asked for the model: its namespace and its names, from the outermost, joined by <c>.</c>, as values.
/// </param>
/// <param name="NameLocation">Where the query's name is in its file.</param>
/// <param name="MarkerLocation">
/// Where the <c>-- input-model:</c> or <c>-- output-model:</c> marker that named the model is, or null.
/// </param>
/// <param name="Summary">The text of the query's <c>-- summary:</c> markers, or null.</param>
internal sealed record ModelSource(
    string NormalizedPath,
    string FileName,
    int QueryIndex,
    string QueryName,
    string ClaimingType,
    LocationInfo NameLocation,
    LocationInfo? MarkerLocation,
    string? Summary
)
{
    /// <summary>
    /// The order of the requests of one model: by path, in the order of <see cref="SqlPath.Comparer" />, then by
    /// the query's index in its file, then by the claiming type, ordinally.  It decides which request comes first,
    /// and so is the same whatever the order of the types and files of the compilation.
    /// </summary>
    public static Comparison<ModelSource> Comparison { get; } =
        static (left, right) =>
        {
            var byPath = SqlPath.Comparer.Compare(left.NormalizedPath, right.NormalizedPath);
            if (byPath != 0)
            {
                return byPath;
            }

            var byIndex = left.QueryIndex.CompareTo(right.QueryIndex);
            return byIndex != 0 ? byIndex : string.CompareOrdinal(left.ClaimingType, right.ClaimingType);
        };
}
