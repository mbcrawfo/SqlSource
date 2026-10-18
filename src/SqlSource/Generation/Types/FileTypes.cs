using SqlSource.Diagnostics;

namespace SqlSource.Generation.Types;

/// <summary>The C# types of the queries of one claimed file: what the models of its queries are built from.</summary>
/// <param name="NormalizedPath">The path of the <c>.sql</c> file, in the form <c>SqlPath.Normalize</c> gives.</param>
/// <param name="Queries">
/// The types of each query whose entry is usable, in the order of the file.  A query with an error has none.
/// </param>
/// <param name="Diagnostics">The errors of the queries, each at its query's name: one at most for a query.</param>
internal sealed record FileTypes(
    string NormalizedPath,
    EquatableArray<QueryTypes> Queries,
    EquatableArray<DiagnosticInfo> Diagnostics
);
