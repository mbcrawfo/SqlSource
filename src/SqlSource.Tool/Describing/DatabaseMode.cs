using SqlSource.Tool.Describing.Capture;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What a run's options make of each database that <see cref="DatabaseRuns" /> runs.
/// </summary>
/// <param name="Online">What <c>--online</c> said: the help for a database that cannot be opened.</param>
/// <param name="Offline">
/// Whether the run is offline (<see cref="DescribeRun" />): a database with no connection fails its queries and
/// reports nothing, where the run would report <c>SQLSRC213</c>.
/// </param>
/// <param name="Capture">
/// Where the run records its one session, as <c>diagnose</c>'s does, or null for a run that records nothing.  A second
/// session for it is a fault of the tool.
/// </param>
internal sealed record DatabaseMode(OnlineMode Online, bool Offline, RunCapture? Capture);
