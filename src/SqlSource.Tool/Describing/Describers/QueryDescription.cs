using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.Describers;

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
/// <param name="Plan">
/// What became of the nullability plan walk, a value of <c>SidecarValues.Plan</c>, or null for a statement without
/// rows.  Provenance.
/// </param>
/// <param name="TableMatch">
/// <c>matched</c>, or the first check of the table match that failed, a value of <c>SidecarValues.TableMatch</c>; null
/// for a statement without rows.  Provenance.
/// </param>
internal sealed record QueryDescription(
    SidecarResultKind ResultKind,
    EquatableArray<DescribedParameter> Parameters,
    EquatableArray<SidecarColumn>? Columns,
    SidecarTable? MatchesTable,
    string? Plan,
    string? TableMatch
);
