using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// One query of a file the type can use, with the settings in effect for it there.
/// </summary>
/// <param name="FileIndex">The index of the query's file among the type's files.</param>
/// <param name="QueryIndex">The query's index among the queries of its file.</param>
/// <param name="Query">The query.</param>
/// <param name="Settings">The settings in effect for the query: its markers, the type's and its file's levels.</param>
internal readonly record struct UsableQuery(int FileIndex, int QueryIndex, SqlQuery Query, QuerySettings Settings);
