using System.Collections.Immutable;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What the command line says of a run, beside what the plan already holds.
/// </summary>
/// <param name="Mode">Whether the run skips what is current, describes it all the same, or only compares.</param>
/// <param name="HasFilter">
/// Whether the run has <c>--project</c>, <c>--database</c> or a <c>.sql</c> path.  The plan's filters do not hold
/// <c>--project</c>, and a sidecar is deleted only in a run with none of the three.
/// </param>
/// <param name="Sources">Where the run looks for its connections: <c>--connection</c> and the <c>.env</c> file.</param>
/// <param name="Databases">
/// The values of <c>--database</c>, each name once ignoring case, in the order given; the first of a name is kept.
/// </param>
/// <param name="Online">
/// What <c>--online</c> said: <see cref="OnlineMode.Off" /> for a run that is not part of a build.
/// </param>
internal sealed record DescribeOptions(
    RunMode Mode,
    bool HasFilter,
    ConnectionSources Sources,
    ImmutableArray<UsageValue> Databases,
    OnlineMode Online
);
