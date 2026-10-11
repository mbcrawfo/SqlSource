using System;
using System.Globalization;

namespace SqlSource.Tool.Tests;

// A clock that stands still until a test moves it, so that a time and a duration in a log are exact.
internal sealed class TestClock : TimeProvider
{
    // Where a new clock stands, as a log writes a time.
    public const string StartIso = "2026-10-09T14:02:11.412Z";

    private TimeSpan _elapsed;

    public DateTimeOffset Now { get; private set; } =
        DateTimeOffset.ParseExact(
            StartIso,
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal
        );

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() => Now;

    public override long GetTimestamp() => _elapsed.Ticks;

    public void Advance(TimeSpan by)
    {
        Now += by;
        _elapsed += by;
    }
}
