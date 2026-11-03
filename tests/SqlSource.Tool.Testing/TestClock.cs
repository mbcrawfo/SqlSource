using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Testing;

// A clock that stands still until a test moves it, so that a time and a duration in a log are exact.  Its timers
// stand still with it: one fires, on the thread of Advance, only when Advance reaches its time, so a test of a wait
// picks the moment it ends.
internal sealed class TestClock : TimeProvider
{
    // Where a new clock stands, as a log writes a time.
    public const string StartIso = "2026-10-09T14:02:11.412Z";

    private static readonly DateTimeOffset Start = DateTimeOffset.ParseExact(
        StartIso,
        "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal
    );

    // Guards the time and the timers: a run reads the clock on its own threads while a test moves it.
    private readonly Lock _gate = new();
    private readonly List<Timer> _timers = [];
    private TimeSpan _elapsed;

    public DateTimeOffset Now
    {
        get
        {
            lock (_gate)
            {
                return Start + _elapsed;
            }
        }
    }

    // How many timers wait for a time, which only Advance can bring.
    public int PendingTimers
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count;
            }
        }
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() => Now;

    public override long GetTimestamp()
    {
        lock (_gate)
        {
            return _elapsed.Ticks;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new Timer(this, callback, state);
        _ = timer.Change(dueTime, period);
        return timer;
    }

    // Moves the clock on, and fires each timer whose time it reaches, in the order of their times, with the clock at
    // that time.  A callback runs outside the clock's lock, so it may read the clock or make another timer, which
    // fires within the same move when its time is reached too.
    public void Advance(TimeSpan by)
    {
        TimeSpan end;
        lock (_gate)
        {
            end = _elapsed + by;
        }

        while (true)
        {
            Timer? due;
            lock (_gate)
            {
                due = _timers.Where(timer => timer.Due <= end).OrderBy(timer => timer.Due).FirstOrDefault();
                if (due is null)
                {
                    _elapsed = end;
                    return;
                }

                _elapsed = due.Due;
                due.Fired();
            }

            due.Callback(due.State);
        }
    }

    private sealed class Timer(TestClock clock, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period;

        public TimerCallback Callback => callback;

        public object? State => state;

        // When it fires next, by the clock's elapsed time.  Read and written under the clock's lock.
        public TimeSpan Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock._gate)
            {
                _ = clock._timers.Remove(this);
                _period = period;
                if (dueTime != Timeout.InfiniteTimeSpan)
                {
                    Due = clock._elapsed + dueTime;
                    clock._timers.Add(this);
                }

                return true;
            }
        }

        // What firing does to the timer: it waits again a period, or no more.  Under the clock's lock.
        public void Fired()
        {
            _ = clock._timers.Remove(this);
            if (_period != Timeout.InfiniteTimeSpan && _period > TimeSpan.Zero)
            {
                Due += _period;
                clock._timers.Add(this);
            }
        }

        public void Dispose()
        {
            lock (clock._gate)
            {
                _ = clock._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
