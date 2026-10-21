using System;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing;

namespace SqlSource.Tool.Tests;

// An exchange that takes the same time for every call: it moves the clock by that much and asks the exchange it wraps,
// so that the duration the describer measures for a call is exact.
internal sealed class ClockedExchange(IDescribeExchange inner, TestClock clock, TimeSpan perCall) : IDescribeExchange
{
    public Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        CancellationToken cancellationToken
    )
    {
        clock.Advance(perCall);
        return inner.AskAsync(method, request, live, cancellationToken);
    }
}
