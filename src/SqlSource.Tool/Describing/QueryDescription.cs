using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What a database said of one query.  It is engine-neutral but for its types, which are the sidecar's own records,
/// so that a describer's answer and an entry say a type one way.
/// </summary>
/// <param name="ResultKind">Whether the statement produces rows.</param>
/// <param name="Parameters">
/// The parameters the describer could type, in any order.  A parameter's ordinal and nullability are not the
/// describer's: the run takes them from the query's list.
/// </param>
/// <param name="Columns">The columns, in order.  Null when there are no rows.</param>
/// <param name="MatchesTable">The table whose column list the result is, or null.</param>
/// <param name="Plan">Whether the nullability plan walk ran.  Provenance.</param>
/// <param name="TableMatch">The first failing check of the table match.  Provenance.</param>
internal sealed record QueryDescription(
    SidecarResultKind ResultKind,
    EquatableArray<DescribedParameter> Parameters,
    EquatableArray<SidecarColumn>? Columns,
    SidecarTable? MatchesTable,
    string? Plan,
    string? TableMatch
);
