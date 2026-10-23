using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// Step 3 of the run: a run that is cancelled.
public sealed class DatabaseRunsCancellationTests : IDisposable
{
    private readonly DatabaseRunsScene _scene = new();

    public void Dispose() => _scene.Dispose();

    [Fact]
    public async Task Run_CancelledWhileAQueryIsDescribed_EndsAsCancelledAndNotAsAFault()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _scene.Describer.BeforeDescribe = _ => cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => _scene.RunAsync(cancellationToken: cancel.Token));

        _ = _scene.Describer.Described.ShouldHaveSingleItem();
        _scene.Describer.Closed.ShouldBe(1);
        _scene.Error.ToString().ShouldBeEmpty();
    }

    // The first exception is the one the run ends with: a close that fails after it is dropped.
    [Fact]
    public async Task Run_CancelledAndTheSessionThrowsWhenItIsClosed_EndsAsCancelled()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _scene.Describer.BeforeDescribe = _ => cancel.CancelAsync();
        _scene.Describer.CloseException = new IOException($"lost {Secrets.ConnectionWithWord}");

        _ = await Should.ThrowAsync<OperationCanceledException>(() => _scene.RunAsync(cancellationToken: cancel.Token));

        _scene.Describer.Closed.ShouldBe(1);
        _scene.Error.ToString().ShouldBeEmpty();
    }
}
