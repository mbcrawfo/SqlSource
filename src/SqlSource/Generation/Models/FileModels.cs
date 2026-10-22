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
);
