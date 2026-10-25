using System;
using System.Collections.Generic;
using SqlSource.Generation.Paths;
using SqlSource.Settings;

namespace SqlSource.Generation.Models;

/// <summary>
/// The generated model that one request got: what a type that claims a query needs in order to name the model in its
/// methods.  A request that differs from the first of its model (<c>SQLSRC257</c>), or whose model is not generated,
/// has none.
/// </summary>
/// <param name="ClaimingType">The type that asked for the model, as <see cref="ModelSource.ClaimingType" />.</param>
/// <param name="NormalizedPath">The path of the query's file, in the form <c>SqlPath.Normalize</c> gives.</param>
/// <param name="QueryIndex">The query's index among the queries of its file.</param>
/// <param name="Role">Whether the model is of the query's parameters or of a row.</param>
/// <param name="TypeText">
/// The model as code names it from anywhere: <c>global::</c> and its full name as code writes it.
/// </param>
/// <param name="Kind">The generated model's shape, which the request got whichever it asked for.</param>
internal sealed record ModelAssignment(
    string ClaimingType,
    string NormalizedPath,
    int QueryIndex,
    ModelRole Role,
    string TypeText,
    ModelKind Kind
)
{
    /// <summary>
    /// The order of the assignments of a <see cref="ModelSet" />: by claiming type, ordinally, then by path in the
    /// order of <see cref="SqlPath.Comparer" />, then by the query's index and the role.
    /// </summary>
    public static IComparer<ModelAssignment> Order { get; } =
        Comparer<ModelAssignment>.Create(
            static (left, right) =>
            {
                var byType = string.CompareOrdinal(left.ClaimingType, right.ClaimingType);
                if (byType != 0)
                {
                    return byType;
                }

                var byPath = SqlPath.Comparer.Compare(left.NormalizedPath, right.NormalizedPath);
                if (byPath != 0)
                {
                    return byPath;
                }

                var byIndex = left.QueryIndex.CompareTo(right.QueryIndex);
                return byIndex != 0 ? byIndex : left.Role.CompareTo(right.Role);
            }
        );

    /// <summary>
    /// The index in <paramref name="sorted" />, which is ordered by <see cref="Order" />, of the first assignment of
    /// <paramref name="claimingType" />, or -1 when it has none.  Its assignments are the run from there.
    /// </summary>
    public static int FirstOf(EquatableArray<ModelAssignment> sorted, string claimingType)
    {
        // The first index whose type is not before the type.
        var (low, high) = (0, sorted.Count);
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (string.CompareOrdinal(sorted[middle].ClaimingType, claimingType) < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low < sorted.Count && string.Equals(sorted[low].ClaimingType, claimingType, StringComparison.Ordinal)
            ? low
            : -1;
    }
}
