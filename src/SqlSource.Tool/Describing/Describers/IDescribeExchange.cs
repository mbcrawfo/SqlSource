using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Describing.Describers;

/// <summary>
/// What every call a describer makes to an engine goes through, so that a run can be recorded and replayed.  It
/// has three shapes: <see cref="LiveExchange" /> asks the engine, <c>CapturingExchange</c> asks another exchange
/// and keeps each call, and <c>ReplayingExchange</c> answers from what was kept.
/// </summary>
/// <remarks>
/// A request and an answer are plain records that <c>CaptureJson.Options</c> writes and reads, and a call that can
/// fail on the server returns the failure as its answer.  No exchange looks at either beyond writing it.  The two
/// other shapes are named and not linked: this folder uses nothing of <c>Describing/Capture/</c>.
/// </remarks>
internal interface IDescribeExchange
{
    /// <summary>Asks the engine one question.</summary>
    /// <param name="method">The name of the question, which with the request keys a recorded answer.</param>
    /// <param name="request">What is asked.</param>
    /// <param name="live">Asks the engine itself.</param>
    /// <param name="cancellationToken">Ends the call.</param>
    Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        CancellationToken cancellationToken
    );
}
