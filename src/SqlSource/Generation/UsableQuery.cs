using SqlSource.Generation.Placement;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// One query of a file the type can use, with the settings in effect for it there and where its members go.
/// </summary>
/// <param name="FileIndex">The index of the query's file among the type's files.</param>
/// <param name="QueryIndex">The query's index among the queries of its file.</param>
/// <param name="Query">The query.</param>
/// <param name="Settings">The settings in effect for the query: its markers and its file's level.</param>
/// <param name="Placement">Where the query's members go and how visible they are, for its type.</param>
internal readonly record struct UsableQuery(
    int FileIndex,
    int QueryIndex,
    SqlQuery Query,
    QuerySettings Settings,
    QueryPlacement Placement
);
