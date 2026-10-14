using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using SqlSource.Tool.Processes;
using Xunit;

namespace SqlSource.Tool.Tests;

// Holds each request of a FakeProcessRunner until the test lets it go, so that a test picks the order of the answers
// and how many requests wait at one time, with no sleep.  PassAsync is the runner's BeforeAnswer, so a held request
// counts as running.
internal sealed class ProcessGate
{
    // How long a test waits for a request that does not come, before it fails.  No test passes by this time.
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly Channel<ProcessRequest> _arrivals = Channel.CreateUnbounded<ProcessRequest>();

    // By the request itself and not by its value: two runs may ask the same.
    private readonly ConcurrentDictionary<ProcessRequest, TaskCompletionSource> _held = new(
        ReferenceEqualityComparer.Instance
    );

    private volatile bool _open;

    // The next request that came and now waits, in the order they came.
    public async Task<ProcessRequest> ArrivalAsync() =>
        await _arrivals.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(Patience);

    // The next requests that came and now wait, as many as asked for.
    public async Task<ProcessRequest[]> ArrivalsAsync(int count)
    {
        var requests = new ProcessRequest[count];
        for (var index = 0; index < count; index++)
        {
            requests[index] = await ArrivalAsync();
        }

        return requests;
    }

    // Lets a request that waits be answered.
    public void Release(ProcessRequest request)
    {
        if (!_held.TryRemove(request, out var held))
        {
            throw new InvalidOperationException("The request does not wait at the gate.");
        }

        held.SetResult();
    }

    // Lets every request that waits be answered, and holds none from now on.
    public void Open()
    {
        _open = true;
        foreach (var request in _held.Keys)
        {
            if (_held.TryRemove(request, out var held))
            {
                held.SetResult();
            }
        }
    }

    // What the runner waits for before it answers.
    internal Task PassAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        if (_open)
        {
            return Task.CompletedTask;
        }

        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _held[request] = held;
        // Open may have run between the look at it and here.
        if (_open && _held.TryRemove(request, out _))
        {
            return Task.CompletedTask;
        }

        _ = _arrivals.Writer.TryWrite(request);
        return held.Task.WaitAsync(cancellationToken);
    }
}
