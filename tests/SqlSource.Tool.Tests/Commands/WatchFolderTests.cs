using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Testing;
using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// A folder of the watched set that goes, comes back, appears late or cannot be watched: the next evaluation watches it
// again once it is there, and a folder that stays gone asks for no pass after the first.  The project is in App, in a
// repository whose root holds .git, and lists a settings file of libs/Shared, so that the root and libs are watched
// alone, beside the folders they hold.
public sealed class WatchFolderTests : IAsyncDisposable
{
    private readonly WatchScene _watch = new("App");
    private readonly string _libs;
    private readonly string _shared;
    private readonly string _settings;

    public WatchFolderTests()
    {
        _ = _watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
        _ = _watch.Run.Folder.CreateFolder(".git");
        _libs = _watch.Run.Folder.PathOf("libs");
        _shared = _watch.Run.Folder.PathOf("libs/Shared");
        _settings = _watch.Run.Folder.PathOf("libs/Shared/sqlsource.json");
        _watch.Project.ListSettings(_settings);
    }

    public ValueTask DisposeAsync() => _watch.DisposeAsync();

    private string LogPath => _watch.Run.Folder.PathOf("run.log");

    private static FileChange Deleted(string path) => new(FileChangeKind.Deleted, path, IsFolder: false);

    private static FileChange CreatedFolder(string path) => new(FileChangeKind.Created, path, IsFolder: true);

    private void WriteSettings() => _ = _watch.Run.Folder.WriteFile("libs/Shared/sqlsource.json", "{}");

    private IEnumerable<string> OpenFolders() => _watch.Watcher.Open.Select(request => request.Folder);

    private int RequestsOf(string folder) => _watch.Watcher.Requests.Count(request => request.Folder == folder);

    private Task StartAsync() => _watch.StartAsync("App/App.csproj", "--log", "run.log");

    // The folder, or the folder above it, deleted or renamed away: its watch is closed by the next evaluation, which a
    // watch on Linux would not say, and the folder is watched again once it is back.
    [Theory]
    [InlineData("libs/Shared", false)]
    [InlineData("libs/Shared", true)]
    [InlineData("libs", false)]
    [InlineData("libs", true)]
    public async Task Watch_WatchedFolderRemovedThenBack_IsWatchedAgain(string removed, bool renamed)
    {
        WriteSettings();
        await StartAsync();
        OpenFolders().ShouldContain(_shared);
        var path = _watch.Run.Folder.PathOf(removed);
        var away = path + "-old";

        Directory.Move(path, away);
        await _watch.ChangeAsync(
            renamed ? new FileChange(FileChangeKind.Renamed, away, IsFolder: true, path) : Deleted(path)
        );

        OpenFolders().ShouldNotContain(_shared);
        if (removed == "libs")
        {
            OpenFolders().ShouldNotContain(_libs);
        }

        Directory.Move(away, path);
        await _watch.ChangeAsync(
            renamed ? new FileChange(FileChangeKind.Renamed, path, IsFolder: true, away) : CreatedFolder(path)
        );

        OpenFolders().ShouldContain(_shared);
        RequestsOf(_shared).ShouldBe(2);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        WatchTests.PassStarts(LogPath).ShouldBe([(1, 0, true), (2, 1, true), (3, 1, true)]);
    }

    // Outside a repository the folder above each watched folder is watched too, so that a folder deleted and made
    // again, whose own watch says nothing on Linux, is seen and watched again.
    [Fact]
    public async Task Watch_WatchedFolderDeletedAndMadeAgainOutsideARepository_IsWatchedAgain()
    {
        await using var watch = new WatchScene("App");
        _ = watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
        var settings = watch.Run.Folder.WriteFile("libs/Shared/sqlsource.json", "{}");
        var shared = Path.GetDirectoryName(settings)!;
        watch.Project.ListSettings(settings);
        await watch.StartAsync("App/App.csproj");
        watch
            .Watcher.Open.Select(request => request.Folder)
            .ShouldBe(
                [
                    watch.Run.Folder.Path,
                    Path.GetDirectoryName(watch.Project.ProjectPath),
                    watch.Run.Folder.PathOf("libs"),
                    shared,
                ],
                ignoreOrder: true
            );
        var first = watch.Watcher.Open.Single(request => request.Folder == shared);

        Directory.Delete(shared, recursive: true);
        _ = watch.Run.Folder.WriteFile("libs/Shared/sqlsource.json", "{}");
        await watch.ChangeAsync(Deleted(shared), CreatedFolder(shared));

        watch.Watcher.Requests.Count(request => request.Folder == shared).ShouldBe(2);
        watch.Watcher.Open.Single(request => request.Folder == shared).ShouldNotBeSameAs(first);
        (await watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // A folder beside a watched one whose name starts the same, deleted or renamed away, is not the watched folder or
    // above it: it closes no watch and asks for nothing, so a save beside it is a pass that does not evaluate.
    [Theory]
    [InlineData("lib", false)]
    [InlineData("lib", true)]
    [InlineData("libs-old", false)]
    [InlineData("libs-old", true)]
    [InlineData("libs/Shar", false)]
    [InlineData("libs/Shar", true)]
    public async Task Watch_FolderBesideAWatchedOneRemoved_AsksNothing(string removed, bool renamed)
    {
        WriteSettings();
        await StartAsync();
        var path = _watch.Run.Folder.PathOf(removed);
        var change = renamed
            ? new FileChange(FileChangeKind.Renamed, path + "-gone", IsFolder: true, path)
            : Deleted(path);

        await _watch.ChangeAsync(change, WatchTests.Changed(_settings));

        RequestsOf(_shared).ShouldBe(1);
        RequestsOf(_libs).ShouldBe(1);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        WatchTests.PassStarts(LogPath).ShouldBe([(1, 0, true), (2, 1, false)]);
    }

    // A folder of the set made while its watch is open, as a poller reports a path that became a folder, or FSEvents
    // a folder made before its stream began: it is watched already, so it asks for nothing.
    [Theory]
    [InlineData("libs")]
    [InlineData("libs/Shared")]
    public async Task Watch_WatchedFolderCreated_AsksNothing(string created)
    {
        WriteSettings();
        await StartAsync();

        await _watch.ChangeAsync(CreatedFolder(_watch.Run.Folder.PathOf(created)), WatchTests.Changed(_settings));

        RequestsOf(_shared).ShouldBe(1);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        WatchTests.PassStarts(LogPath).ShouldBe([(1, 0, true), (2, 1, false)]);
    }

    // Deleted and made again before an evaluation, as a script that writes the folder anew does: the watch of the old
    // folder is closed, and the new folder watched.
    [Fact]
    public async Task Watch_WatchedFolderDeletedAndMadeAgain_IsWatchedAgain()
    {
        WriteSettings();
        await StartAsync();
        var first = _watch.Watcher.Open.Single(request => request.Folder == _shared);

        Directory.Delete(_shared, recursive: true);
        WriteSettings();
        await _watch.ChangeAsync(Deleted(_shared), CreatedFolder(_shared));

        RequestsOf(_shared).ShouldBe(2);
        _watch.Watcher.Open.Single(request => request.Folder == _shared).ShouldNotBeSameAs(first);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // A folder of the set that did not exist when the watch started is watched once it is made, without a save of a
    // project file.
    [Fact]
    public async Task Watch_FolderOfTheSetMadeLater_IsWatchedByThePassItStarts()
    {
        await StartAsync();
        OpenFolders().ShouldNotContain(_shared);
        OpenFolders().ShouldNotContain(_libs);

        WriteSettings();
        await _watch.ChangeAsync(CreatedFolder(_libs));

        OpenFolders().ShouldContain(_libs);
        OpenFolders().ShouldContain(_shared);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        WatchTests.PassStarts(LogPath).ShouldBe([(1, 0, true), (2, 1, true)]);
    }

    // A folder whose watch could not start is tried again by the next evaluation, which its failure starts.
    [Fact]
    public async Task Watch_FolderWhoseWatchThrewOnce_IsWatchedByTheNextEvaluation()
    {
        WriteSettings();
        var attempts = 0;
        _watch.Run.Watch = request =>
            request.Folder == _shared && ++attempts == 1
                ? throw new IOException("no watch today")
                : _watch.Watcher.Watch(request);
        await StartAsync();
        OpenFolders().ShouldNotContain(_shared);

        await _watch.TimerAsync();
        _watch.Clock.Advance(WatchSession.Debounce);
        await _watch.PassesAsync(2);

        OpenFolders().ShouldContain(_shared);
        attempts.ShouldBe(2);
        _watch.Clock.PendingTimers.ShouldBe(0);
        _watch.Errors.ShouldContain($"error SQLSRC287: watching '{_shared}' for changes failed: no watch today");
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // A watch that cannot start again is tried by each evaluation, and asks for no pass after the first failure:
    // otherwise each evaluation would start another.  Its error is written once.
    [Fact]
    public async Task Watch_FolderWhoseWatchAlwaysThrows_IsTriedByEachEvaluationWithoutALoop()
    {
        WriteSettings();
        var attempts = 0;
        _watch.Run.Watch = request =>
        {
            if (request.Folder != _shared)
            {
                return _watch.Watcher.Watch(request);
            }

            attempts++;
            throw new IOException("no watch today");
        };
        await StartAsync();
        await _watch.TimerAsync();
        _watch.Clock.Advance(WatchSession.Debounce);
        await _watch.PassesAsync(2);

        attempts.ShouldBe(2);
        _watch.Clock.PendingTimers.ShouldBe(0);

        await _watch.ChangeAsync(WatchTests.Changed(_watch.Project.ProjectPath));

        attempts.ShouldBe(3);
        _watch.Clock.PendingTimers.ShouldBe(0);
        _watch.Errors.Split('\n').Count(line => line.Contains("error SQLSRC287", StringComparison.Ordinal)).ShouldBe(1);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        WatchTests.PassStarts(LogPath).ShouldBe([(1, 0, true), (2, 1, true), (3, 1, true)]);
    }

    // A polling watch of a folder that stays gone, with nothing above it watched: one more pass, and none however long
    // it stays gone, so that the next save is a pass of its own.  The project's folder is watched by the test's
    // watcher, since each evaluation of the scene writes the project's settings file.
    [Fact]
    public async Task Watch_PolledFolderThatStaysGone_GivesOnePass()
    {
        await using var watch = new WatchScene("App");
        var users = watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
        var settings = watch.Run.Folder.WriteFile("Shared/sqlsource.json", "{}");
        var shared = Path.GetDirectoryName(settings)!;
        watch.Project.ListSettings(settings);
        watch.Run.Environment[WatchSession.PollingVariable] = "true";
        watch.Run.Watch = request =>
            request.Folder == shared
                ? new PollingFolderWatcher(request, watch.Clock, PollingFolderWatcher.Interval)
                : watch.Watcher.Watch(request);
        await watch.StartAsync("App/App.csproj", "--log", "run.log");
        var pollers = watch.Clock.PendingTimers;
        pollers.ShouldBe(1);

        Directory.Delete(shared, recursive: true);
        watch.Clock.Advance(PollingFolderWatcher.Interval);
        await PassAsync(watch, pollers, 2);
        for (var second = 0; second < 5; second++)
        {
            watch.Clock.Advance(PollingFolderWatcher.Interval);
        }

        await File.WriteAllTextAsync(users, SqlTexts.TwoUsers, TestContext.Current.CancellationToken);
        watch.Raise(WatchTests.Changed(users));
        await PassAsync(watch, pollers, 3);

        watch.Count(WatchSession.ChangeDetectedMarker).ShouldBe(2);
        watch.Errors.Split('\n').Count(line => line.Contains("error SQLSRC287", StringComparison.Ordinal)).ShouldBe(1);
        (await watch.StopAsync()).ExitCode.ShouldBe(0);
        WatchTests.PassStarts(watch.Run.Folder.PathOf("run.log")).ShouldBe([(1, 0, true), (2, 1, true), (3, 1, false)]);
    }

    // A stale poller closed while it reports a change: its report waits for the session's lock, and closing it waits
    // for its report, so the session must not hold the lock while it closes a watch.  The poll is held inside its
    // report until the close begins, which forces the one order that would hang.
    [Fact]
    public async Task Watch_StalePollerClosedWhileItReports_DoesNotHang()
    {
        WriteSettings();
        _watch.Run.Environment[WatchSession.PollingVariable] = "true";
        using var reporting = new ManualResetEventSlim();
        using var closing = new ManualResetEventSlim();
        var held = false;
        _watch.Run.Watch = request =>
        {
            if (request.Folder != _shared)
            {
                return _watch.Watcher.Watch(request);
            }

            if (held)
            {
                return new PollingFolderWatcher(request, _watch.Clock, PollingFolderWatcher.Interval);
            }

            held = true;
            var poller = new PollingFolderWatcher(
                request with
                {
                    Changed = change =>
                    {
                        if (!reporting.IsSet)
                        {
                            reporting.Set();
                            _ = closing.Wait(Eventually.Patience, TestContext.Current.CancellationToken);
                        }

                        request.Changed(change);
                    },
                },
                _watch.Clock,
                PollingFolderWatcher.Interval
            );
            return new OnDispose(() =>
            {
                closing.Set();
                poller.Dispose();
            });
        };
        await StartAsync();
        _ = _watch.Run.Folder.WriteFile("libs/Shared/New.sql", "SELECT 1;");

        var poll = Task.Run(() => _watch.Clock.Advance(PollingFolderWatcher.Interval), CancellationToken.None);
        await Eventually.TrueAsync(() => reporting.IsSet, "The poll did not report the new file.");
        _watch.Raise(Deleted(_shared));
        await Eventually.TrueAsync(() => _watch.Clock.PendingTimers > 1, "The watch did not begin to wait.");
        var pass = Task.Run(() => _watch.Clock.Advance(WatchSession.Debounce), CancellationToken.None);

        _ = await Eventually.WithinAsync(
            Task.WhenAll(poll, pass).ContinueWith(_ => true, TaskScheduler.Default),
            "The session hung closing a poller that was reporting a change.",
            TimeSpan.FromSeconds(30)
        );
        await _watch.PassesAsync(2);
        closing.IsSet.ShouldBeTrue();
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // Runs the action when it is disposed.
    private sealed class OnDispose(Action action) : IDisposable
    {
        public void Dispose() => action();
    }

    // Waits for the session's timer beside the pollers', moves the clock past it, and waits for the pass.
    private static async Task PassAsync(WatchScene watch, int pollers, int passes)
    {
        await Eventually.TrueAsync(
            () => watch.Clock.PendingTimers > pollers,
            "The watch did not begin to wait after a change."
        );
        watch.Clock.Advance(WatchSession.Debounce);
        await watch.PassesAsync(passes);
    }
}
