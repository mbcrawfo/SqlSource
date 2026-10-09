using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot;

/// <summary>
/// The description of one query.  A member is nullable wherever a reader may find <c>null</c> or nothing.
/// </summary>
/// <param name="Name">The query's name: the entry's key.</param>
/// <param name="NameSpan">
/// Where the key is in the text that was read, quotes included.  <c>default</c> in an entry the tool builds; the
/// writer does not read it.
/// </param>
/// <param name="Hash">The hash of the query that was described.</param>
/// <param name="Engine">The canonical name of the engine, which picks the shape of every type in the entry.</param>
/// <param name="Database">The logical name of the database the query was described against.</param>
/// <param name="ServerVersion">The server's version.  Informational.</param>
/// <param name="ResultKind">Whether the statement produces rows.</param>
/// <param name="MatchesTable">The table whose column list the result is, or null.</param>
/// <param name="Plan">Whether the nullability plan walk ran.  Provenance, kept as written.</param>
/// <param name="TableMatch">The first failing check of the table match.  Provenance, kept as written.</param>
/// <param name="Parameters">The parameters, in order.</param>
/// <param name="Columns">The columns, in order.  Null when there are no rows.</param>
internal sealed record SidecarEntry(
    string Name,
    TextSpan NameSpan,
    string Hash,
    string Engine,
    string? Database,
    string? ServerVersion,
    SidecarResultKind ResultKind,
    SidecarTable? MatchesTable,
    string? Plan,
    string? TableMatch,
    EquatableArray<SidecarParameter> Parameters,
    EquatableArray<SidecarColumn>? Columns
);
