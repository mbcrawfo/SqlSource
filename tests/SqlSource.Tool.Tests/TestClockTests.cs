using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests;

// TestClock stands in for the machine's clock.  This holds its start to the text the tests expect in a log, and its
// timers to the moves of the clock.
public sealed class TestClockTests
{
    [Fact]
    public void Now_NewClock_IsTheInstantOfStartIso()
    {
        var clock = new TestClock();

        TestClock.StartIso.ShouldBe("2026-10-09T14:02:11.412Z");
        clock.Now.ShouldBe(new DateTimeOffset(2026, 10, 9, 14, 2, 11, 412, TimeSpan.Zero));
        clock.GetUtcNow().Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Advance_ToATimersTime_FiresItThereAndNotBefore()
    {
        var clock = new TestClock();
        var fired = new List<TimeSpan>();
        using var timer = clock.CreateTimer(
            _ => fired.Add(TimeSpan.FromTicks(clock.GetTimestamp())),
            null,
            TimeSpan.FromMilliseconds(300),
            Timeout.InfiniteTimeSpan
        );
        clock.PendingTimers.ShouldBe(1);

        clock.Advance(TimeSpan.FromMilliseconds(299));
        fired.ShouldBeEmpty();
        clock.Advance(TimeSpan.FromMilliseconds(201));

        fired.ShouldBe([TimeSpan.FromMilliseconds(300)]);
        clock.PendingTimers.ShouldBe(0);
        clock.GetTimestamp().ShouldBe(TimeSpan.FromMilliseconds(500).Ticks);
    }

    [Fact]
    public void Advance_PeriodicTimer_FiresOnceForEachPeriodReached()
    {
        var clock = new TestClock();
        var fired = 0;
        using var timer = clock.CreateTimer(_ => fired++, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        clock.Advance(TimeSpan.FromSeconds(3.5));

        fired.ShouldBe(3);
        clock.PendingTimers.ShouldBe(1);
    }

    [Fact]
    public void Advance_DisposedTimer_DoesNotFire()
    {
        var clock = new TestClock();
        var fired = false;
        var timer = clock.CreateTimer(
            _ => fired = true,
            null,
            TimeSpan.FromMilliseconds(300),
            Timeout.InfiniteTimeSpan
        );

        timer.Dispose();
        clock.Advance(TimeSpan.FromSeconds(1));

        fired.ShouldBeFalse();
        clock.PendingTimers.ShouldBe(0);
    }

    // A wait on the clock, as the watch's, ends when the clock is moved past it, and never by itself.
    [Fact]
    public void Advance_PastADelay_EndsIt()
    {
        var clock = new TestClock();
        var delay = Task.Delay(TimeSpan.FromMilliseconds(300), clock, TestContext.Current.CancellationToken);
        delay.IsCompleted.ShouldBeFalse();

        clock.Advance(TimeSpan.FromMilliseconds(300));

        delay.IsCompletedSuccessfully.ShouldBeTrue();
    }
}
