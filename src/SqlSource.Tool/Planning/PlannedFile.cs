using SqlSource.Parsing;

namespace SqlSource.Tool.Planning;

/// <summary>
/// One <c>.sql</c> file that a type of the run claims.
/// </summary>
/// <param name="Path">The full path, as the project lists it.</param>
/// <param name="NormalizedPath">The path in the form <c>SqlPath.Normalize</c> gives, which is what is compared.</param>
/// <param name="ProjectPath">The full path of the project it is planned under: the first that claims it.</param>
/// <param name="Dialect">The dialect it was read by.</param>
/// <param name="State">Whether its queries can be described.</param>
/// <param name="Queries">Its queries, in the file's order.  Every one, whatever the filters of the run.</param>
internal sealed record PlannedFile(
    string Path,
    string NormalizedPath,
    string ProjectPath,
    SqlDialect Dialect,
    PlannedFileState State,
    EquatableArray<PlannedQuery> Queries
);
