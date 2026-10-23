using System;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Testing;

// An exchange that lets a test act around each call: before runs, then the exchange it wraps answers, the live
// exchange unless inner says otherwise, and then after runs.  Each is given the call's method, its request and its
// token.  A test moves the clock in before, so that the duration the describer measures for a call is exact, and acts
// on the server in after, once the server has answered.  A hook runs for "open" too, before the test holds its session,
// so a hook that needs the session acts only once it is set.
internal sealed class HookedExchange(
    Func<string, object?, CancellationToken, Task>? before = null,
    Func<string, object?, CancellationToken, Task>? after = null,
    IDescribeExchange? inner = null
) : IDescribeExchange
{
    private readonly IDescribeExchange _inner = inner ?? LiveExchange.Instance;

    // An exchange that moves the clock by perCall before each call that inner answers, so that every call takes the
    // same time and the durations in the log are exact.
    public static HookedExchange Clocked(TestClock clock, TimeSpan perCall, IDescribeExchange inner) =>
        new(
            before: (_, _, _) =>
            {
                clock.Advance(perCall);
                return Task.CompletedTask;
            },
            inner: inner
        );

    public async Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        CancellationToken cancellationToken
    )
    {
        if (before is not null)
        {
            await before(method, request, cancellationToken);
        }

        var answer = await _inner.AskAsync(method, request, live, cancellationToken);
        if (after is not null)
        {
            await after(method, request, cancellationToken);
        }

        return answer;
    }
}
