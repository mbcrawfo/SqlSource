using System;
using Shouldly;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests;

// TestClock stands in for the machine's clock.  This holds its start to the text the tests expect in a log.
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
}
