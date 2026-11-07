using System.Threading;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool;

/// <summary>
/// What every step of a run is given beside its own values: what it takes from outside, where its errors go, and
/// what ends it.  A command's action makes it from the token System.CommandLine gives it, and each pass of a watch
/// makes one with the pass's own reporter.
/// </summary>
/// <remarks>Nothing in <c>Logging/</c> names it.</remarks>
/// <param name="Host">
/// What the run takes from outside: the describers, the environment, the files, standard output and the clock.
/// </param>
/// <param name="Reporter">Where the errors go, with the run's trace.</param>
/// <param name="CancellationToken">Ends the run.</param>
internal sealed record RunContext(ToolHost Host, Reporter Reporter, CancellationToken CancellationToken);
