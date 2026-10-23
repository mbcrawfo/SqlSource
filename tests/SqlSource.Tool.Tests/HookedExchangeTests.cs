using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests;

// HookedExchange is how a test acts around a describer's call: on the clock before it, or on the server after it.
public sealed class HookedExchangeTests
{
    [Fact]
    public async Task AskAsync_BeforeAndAfter_RunAroundTheInnerCall()
    {
        List<string> order = [];
        var exchange = new HookedExchange(
            before: (method, request, _) =>
            {
                order.Add($"before {method} {request}");
                return Task.CompletedTask;
            },
            after: (method, request, _) =>
            {
                order.Add($"after {method} {request}");
                return Task.CompletedTask;
            },
            inner: new Inner(order)
        );

        var answer = await exchange.AskAsync("ask", 5, Live(order), TestContext.Current.CancellationToken);

        answer.ShouldBe("answer 5");
        order.ShouldBe(["before ask 5", "inner", "live", "after ask 5"]);
    }

    [Fact]
    public async Task AskAsync_NoInner_CallsTheLiveFunction()
    {
        List<string> order = [];
        var exchange = new HookedExchange();

        var answer = await exchange.AskAsync("ask", 6, Live(order), TestContext.Current.CancellationToken);

        answer.ShouldBe("answer 6");
        order.ShouldBe(["live"]);
    }

    [Fact]
    public async Task AskAsync_Hooks_ReceiveTheCallsToken()
    {
        using var source = new CancellationTokenSource();
        CancellationToken? before = null;
        CancellationToken? after = null;
        var exchange = new HookedExchange(
            before: (_, _, token) =>
            {
                before = token;
                return Task.CompletedTask;
            },
            after: (_, _, token) =>
            {
                after = token;
                return Task.CompletedTask;
            }
        );

        _ = await exchange.AskAsync("ask", 7, Live([]), source.Token);

        before.ShouldBe(source.Token);
        after.ShouldBe(source.Token);
    }

    private static Func<int, CancellationToken, Task<string>> Live(List<string> order) =>
        (request, _) =>
        {
            order.Add("live");
            return Task.FromResult($"answer {request}");
        };

    // An exchange that notes its call and then asks for the answer.
    private sealed class Inner(List<string> order) : IDescribeExchange
    {
        public Task<TAnswer> AskAsync<TRequest, TAnswer>(
            string method,
            TRequest request,
            Func<TRequest, CancellationToken, Task<TAnswer>> live,
            CancellationToken cancellationToken
        )
        {
            order.Add("inner");
            return live(request, cancellationToken);
        }
    }
}
