using System;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests;

// Eventually is the one wait of the tests that polls, and Patience the one limit of every wait.
public sealed class EventuallyTests
{
    [Fact]
    public async Task TrueAsync_ConditionThatComesTrue_Returns()
    {
        var calls = 0;

        await Eventually.TrueAsync(() => ++calls == 3, "never true");

        calls.ShouldBe(3);
    }

    [Fact]
    public async Task ValueAsync_AttemptThatGivesAValue_GivesIt()
    {
        var calls = 0;

        var value = await Eventually.ValueAsync(() => Task.FromResult<int?>(++calls < 3 ? null : 7), "no value");

        value.ShouldBe(7);
        calls.ShouldBe(3);
    }

    [Fact]
    public async Task TrueAsync_ConditionThatStaysFalse_ThrowsTheTimeoutWithItsMessage()
    {
        var thrown = await Should.ThrowAsync<TimeoutException>(() =>
            Eventually.TrueAsync(() => Task.FromResult(false), "never true", TimeSpan.FromMilliseconds(200))
        );

        thrown.Message.ShouldBe("never true");
    }

    [Fact]
    public async Task ValueAsync_AttemptThatHangs_ThrowsTheTimeout()
    {
        var hangs = new TaskCompletionSource<int?>();

        var thrown = await Should.ThrowAsync<TimeoutException>(() =>
            Eventually.ValueAsync(() => hangs.Task, "no value", TimeSpan.FromMilliseconds(200))
        );

        thrown.Message.ShouldBe("no value");
    }

    [Fact]
    public void Patience_IsTwoMinutes() => Eventually.Patience.ShouldBe(TimeSpan.FromMinutes(2));
}
