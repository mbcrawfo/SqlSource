using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What every call a describer makes to an engine goes through, so that phase 3 can record a run and replay it.
/// </summary>
/// <remarks>
/// A request and an answer of a real describer are plain data that can be written as JSON, and a call that can fail
/// on the server returns the failure as its answer.
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
