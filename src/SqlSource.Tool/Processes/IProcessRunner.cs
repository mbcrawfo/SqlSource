using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Processes;

/// <summary>
/// Runs a program and waits for it.  Every process the tool starts goes through the runner of its
/// <see cref="ToolHost" />, so that a test can answer in the program's place.
/// </summary>
internal interface IProcessRunner
{
    /// <summary>
    /// Runs the program to its end.  A program that cannot be started gives
    /// <see cref="ProcessResult.NotStarted" />, with the reason as its error output.
    /// </summary>
    /// <exception cref="System.OperationCanceledException">
    /// The token was cancelled.  The process, and every process it started, was killed first as far as the system
    /// allowed, and the process was waited for, a few seconds at most.
    /// </exception>
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}
