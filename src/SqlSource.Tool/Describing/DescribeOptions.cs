using System.Collections.Immutable;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What the command line says of a run, beside what the plan already holds.
/// </summary>
/// <param name="Force">Whether a selected query is described though its entry is current.</param>
/// <param name="HasFilter">
/// Whether the run has <c>--project</c>, <c>--database</c> or a <c>.sql</c> path.  The plan's filters do not hold
/// <c>--project</c>, and a sidecar is deleted only in a run with none of the three.
/// </param>
/// <param name="Connections">The values of <c>--connection</c>.</param>
/// <param name="Databases">The names of <c>--database</c>, each once ignoring case, in the order given.</param>
internal sealed record DescribeOptions(
    bool Force,
    bool HasFilter,
    ImmutableArray<ConnectionArgument> Connections,
    ImmutableArray<string> Databases
);
