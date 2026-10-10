using SqlSource.Generation;

namespace SqlSource.Tool.Planning;

/// <summary>
/// One query of a claimed file, with what a run needs to know of it.
/// </summary>
/// <param name="Query">The query as the generator's parser read it.</param>
/// <param name="NeedsEntry">
/// Whether a sidecar must hold an entry for it: its output is <c>Models</c> or <c>CodeGen</c> for a type that claims
/// its file.
/// </param>
/// <param name="Database">
/// The database it belongs to, in the spelling the run first met.  Null for a query that needs no entry, which
/// belongs to none.
/// </param>
/// <param name="Hash">The hash of <c>SqlQueryHash</c>.  Null for a query that needs no entry.</param>
/// <param name="Problems">Why it cannot be described, when it cannot.</param>
/// <param name="IsSelected">Whether the filters of the run take it.  True in a run with no filter.</param>
internal sealed record PlannedQuery(
    SqlQuery Query,
    bool NeedsEntry,
    string? Database,
    string? Hash,
    QueryProblems Problems = QueryProblems.None,
    bool IsSelected = true
);
