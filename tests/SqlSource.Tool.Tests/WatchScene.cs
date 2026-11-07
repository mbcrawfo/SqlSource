using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Testing;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests;

// A "describe --watch" of DescribeScene's project that a test drives: the changes come from a FakeFolderWatcher and
// the time from the scene's TestClock, so a test picks when each pass runs, and waits for it by its markers.
internal sealed class WatchScene : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(
        TestContext.Current.CancellationToken
    );

    private RunningCli? _running;

    public WatchScene(string directory = "")
    {
        Scene = new DescribeScene(directory);
        Run.Watch = Watcher.Watch;
    }

    public DescribeScene Scene { get; }

    public FakeFolderWatcher Watcher { get; } = new();

    public CliRun Run => Scene.Run;

    public TestClock Clock => Scene.Clock;

    public FakeDescriber Describer => Scene.Describer;

    public TestProject Project => Scene.Project;

    // The folder that holds the scene's folder, which a watch outside a repository watches alone, beside the folders
    // below it that it watches.
    public string Above => System.IO.Path.GetDirectoryName(Run.Folder.Path)!;

    public RunningCli Running => _running ?? throw new InvalidOperationException("The watch was not started.");

    public string Output => Running.Output;

    public string Errors => Running.Errors;

    // How many times the run ran a manifest target, which a pass does only when it evaluates the projects.
    public int ManifestRuns => Run.Processes.Requests.Count(FakeProcessRunner.IsManifestRun);

    // The members of each pass.start of the session's log, in order.
    public (int Pass, int Changes, bool Evaluated)[] PassStarts(string log = "run.log") =>
        [
            .. LogReader
                .Events(Run.Folder.PathOf(log), "pass.start")
                .Select(line =>
                    (
                        line.GetProperty("pass").GetInt32(),
                        line.GetProperty("changes").GetInt32(),
                        line.GetProperty("evaluated").GetBoolean()
                    )
                ),
        ];

    // Starts "describe --watch" with the arguments, and returns without waiting for anything.
    public RunningCli Begin(params string[] args)
    {
        _running = Run.Start(_stop.Token, ["describe", "--watch", .. args]);
        return _running;
    }

    // Starts "describe --watch" with the arguments, and waits for the first pass to end.
    public async Task StartAsync(params string[] args)
    {
        _ = Begin(args);
        await PassesAsync(1);
    }

    // Raises the changes, then moves the clock past the wait that they began, and waits for the pass to end.
    public async Task ChangeAsync(params FileChange[] changes)
    {
        var passes = Count(WatchSession.WatchingMarker);
        Raise(changes);
        await TimerAsync();
        Clock.Advance(WatchSession.Debounce);
        await PassesAsync(passes + 1);
    }

    public void Raise(params FileChange[] changes)
    {
        foreach (var change in changes)
        {
            Watcher.Raise(change);
        }
    }

    // Waits until the session waits for its time to pass.
    public Task TimerAsync() =>
        Eventually.TrueAsync(() => Clock.PendingTimers > 0, "The watch did not begin to wait after a change.");

    // Waits until as many passes as given have ended, by the marker that follows each.
    public Task PassesAsync(int count) =>
        Eventually.TrueAsync(
            () => Count(WatchSession.WatchingMarker) >= count || Running.Ended.IsCompleted,
            $"The watch did not end pass {count}.  It wrote: {Output}{Errors}"
        );

    // How many times the output holds the line.
    public int Count(string line) => Output.Split('\n').Count(written => written == line);

    // Stops the watch, as Ctrl+C does, and gives how it ended.
    public async Task<CliResult> StopAsync()
    {
        await _stop.CancelAsync();
        return await Eventually.WithinAsync(Running.Ended, "The watch did not end when it was stopped.");
    }

    public async ValueTask DisposeAsync()
    {
        // A test that failed half way leaves its watch going, which must end before its folder is deleted.
        if (_running is { Ended.IsCompleted: false })
        {
            await _stop.CancelAsync();
            _ = await Task.WhenAny(_running.Ended, Task.Delay(Eventually.Patience, CancellationToken.None));
        }

        _stop.Dispose();
        Scene.Dispose();
    }
}
