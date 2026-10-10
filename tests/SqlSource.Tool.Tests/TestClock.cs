using System;

namespace SqlSource.Tool.Tests;

// A clock that stands still until a test moves it, so that a time and a duration in a log are exact.
internal sealed class TestClock : TimeProvider
{
    private TimeSpan _elapsed;

    public DateTimeOffset Now { get; private set; } = new(2026, 10, 9, 14, 2, 11, 412, TimeSpan.Zero);

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() => Now;

    public override long GetTimestamp() => _elapsed.Ticks;

    public void Advance(TimeSpan by)
    {
        Now += by;
        _elapsed += by;
    }
}
