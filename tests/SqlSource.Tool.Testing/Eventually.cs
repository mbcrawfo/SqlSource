using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SqlSource.Tool.Testing;

// The waits of the tests, each on the test's token and none for longer than Patience: a test that waits for
// something that never comes fails with what it waited for, and a run that is cancelled ends at once.
internal static class Eventually
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    // The one limit of every wait.  It is long because a loaded machine is slow, and costs nothing when the wait ends.
    public static TimeSpan Patience { get; } = TimeSpan.FromMinutes(2);

    // Tries at once and then every Interval, until the condition holds.
    public static Task TrueAsync(Func<bool> condition, string otherwise) =>
        TrueAsync(() => Task.FromResult(condition()), otherwise);

    public static Task TrueAsync(Func<Task<bool>> condition, string otherwise) =>
        TrueAsync(condition, otherwise, Patience);

    // The first value that the attempt gives, which is not null.
    public static Task<T> ValueAsync<T>(Func<Task<T?>> attempt, string otherwise)
        where T : struct => ValueAsync(attempt, otherwise, Patience);

    // The waits above with a patience of their own, which a test of the timeout cannot spend two minutes on.
    internal static async Task TrueAsync(Func<Task<bool>> condition, string otherwise, TimeSpan patience) =>
        _ = await ValueAsync<bool>(async () => await condition() ? true : null, otherwise, patience);

    internal static async Task<T> ValueAsync<T>(Func<Task<T?>> attempt, string otherwise, TimeSpan patience)
        where T : struct
    {
        var test = TestContext.Current.CancellationToken;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(test);
        limit.CancelAfter(patience);
        try
        {
            while (true)
            {
                // An attempt that hangs is no longer than the patience either: it is abandoned at the limit, and a
                // later failure of it goes unobserved.
                if (await attempt().WaitAsync(limit.Token) is { } value)
                {
                    return value;
                }

                await Task.Delay(Interval, limit.Token);
            }
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested && !test.IsCancellationRequested)
        {
            throw new TimeoutException(otherwise);
        }
    }

    // A task that the test does not make end itself: a wait for something to happen, as a message or a signal.
    public static Task<T> WithinAsync<T>(Task<T> task, string otherwise) => WithinAsync(task, otherwise, Patience);

    internal static async Task<T> WithinAsync<T>(Task<T> task, string otherwise, TimeSpan patience)
    {
        var test = TestContext.Current.CancellationToken;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(test);
        limit.CancelAfter(patience);
        try
        {
            return await task.WaitAsync(limit.Token);
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested && !test.IsCancellationRequested)
        {
            throw new TimeoutException(otherwise);
        }
    }
}
