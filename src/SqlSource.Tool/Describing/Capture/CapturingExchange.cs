using System;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// The exchange that records: it asks another exchange, the live one in a real run, and adds each call with its
/// request and its answer to a capture.
/// </summary>
/// <remarks>
/// A call that throws adds nothing: what a server can cause is an answer, and an exception is a bug of the
/// describer or the end of the run.  It looks at a request and an answer only to write them.
/// </remarks>
/// <param name="inner">The exchange that answers.</param>
/// <param name="capture">Where each call is added, in the order the calls are made.</param>
internal sealed class CapturingExchange(IDescribeExchange inner, DescribeCapture capture) : IDescribeExchange
{
    public async Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        CancellationToken cancellationToken
    )
    {
        var answer = await inner.AskAsync(method, request, live, cancellationToken);
        capture.Add(new CaptureCall(method, CaptureJson.ToElement(request), CaptureJson.ToElement(answer)));
        return answer;
    }
}
