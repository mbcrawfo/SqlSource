using SqlSource.Snapshot;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Describing;

/// <summary>
/// One query of a run, and what the run has done with it so far.  <see cref="RunDecisions" /> makes it and
/// <see cref="DatabaseRuns" /> moves it from <see cref="QueryState.ToDescribe" /> to its end.
/// </summary>
internal sealed class QueryWork(PlannedQuery planned, QueryState state, SidecarEntry? entry = null)
{
    public PlannedQuery Planned { get; } = planned;

    public QueryState State { get; set; } = state;

    /// <summary>The entry the file's target holds for the query: the kept one, or the new one.</summary>
    public SidecarEntry? Entry { get; set; } = entry;
}
