using System.Collections.Immutable;
using SqlSource.Diagnostics;
using SqlSource.Generation.Placement;

namespace SqlSource.Generation;

/// <summary>
/// Which files and queries of a type it can use, the settings in effect for each query, and where its members go.
/// </summary>
internal static class UsableQueries
{
    /// <summary>
    /// Each query of the files the type can use, in file order and then query order, with its indices, its settings
    /// and its placement, from the type's <see cref="TypePlacement" />, which the sequence also gives.  The files that
    /// <see cref="TypePlacement.Of" /> leaves out give their diagnostics to <paramref name="diagnostics" /> when it is
    /// given, before this returns.  The sequence holds no list, so it can be walked again at no cost.
    /// </summary>
    /// <param name="type">The type with its parsed files.</param>
    /// <param name="diagnostics">Where the diagnostics go, or null when the caller does not report them.</param>
    public static UsableQuerySequence Of(TypeQueries type, ImmutableArray<DiagnosticInfo>.Builder? diagnostics) =>
        new(type, TypePlacement.Of(type.Type.Type, type.Files, type.FileSettings, diagnostics));
}
