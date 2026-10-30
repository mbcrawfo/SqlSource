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
                Compare(left, right.ClaimingType, right.NormalizedPath, right.QueryIndex, right.Role)
        );

    /// <summary>
    /// The index in <paramref name="sorted" />, which is ordered by <see cref="Order" />, of the assignment of the
    /// query at <paramref name="queryIndex" /> of the file at <paramref name="normalizedPath" /> in the role
    /// <paramref name="role" /> to <paramref name="claimingType" />, or -1 when there is none.
    /// </summary>
    public static int IndexOf(
        EquatableArray<ModelAssignment> sorted,
        string claimingType,
        string normalizedPath,
        int queryIndex,
        ModelRole role
    ) => SortedSearch.IndexOf(sorted.Count, new KeyProbe(sorted, claimingType, normalizedPath, queryIndex, role));

    /// <summary>
    /// The index in <paramref name="sorted" />, which is ordered by <see cref="Order" />, of the first assignment of
    /// <paramref name="claimingType" />, or -1 when it has none.  Its assignments are the run from there.
    /// </summary>
    public static int FirstOf(EquatableArray<ModelAssignment> sorted, string claimingType)
    {
        var at = SortedSearch.LowerBound(sorted.Count, new TypeProbe(sorted, claimingType));
        return at < sorted.Count && string.Equals(sorted[at].ClaimingType, claimingType, StringComparison.Ordinal)
            ? at
            : -1;
    }

    // How the item sorts against the key, by the one order that Order gives.  The role is compared as its number, so
    // that no value is boxed.
    private static int Compare(
        ModelAssignment item,
        string claimingType,
        string normalizedPath,
        int queryIndex,
        ModelRole role
    )
    {
        var byType = string.CompareOrdinal(item.ClaimingType, claimingType);
        if (byType != 0)
        {
            return byType;
        }

        var byPath = SqlPath.Comparer.Compare(item.NormalizedPath, normalizedPath);
        if (byPath != 0)
        {
            return byPath;
        }

        var byIndex = item.QueryIndex.CompareTo(queryIndex);
        return byIndex != 0 ? byIndex : ((int)item.Role).CompareTo((int)role);
    }

    // Structs, so that SortedSearch does not box them.
    private readonly struct KeyProbe(
        EquatableArray<ModelAssignment> sorted,
        string claimingType,
        string normalizedPath,
        int queryIndex,
        ModelRole role
    ) : SortedSearch.IProbe
    {
        public int CompareAt(int index) => Compare(sorted[index], claimingType, normalizedPath, queryIndex, role);
    }

    private readonly struct TypeProbe(EquatableArray<ModelAssignment> sorted, string claimingType) : SortedSearch.IProbe
    {
        public int CompareAt(int index) => string.CompareOrdinal(sorted[index].ClaimingType, claimingType);
    }
}
