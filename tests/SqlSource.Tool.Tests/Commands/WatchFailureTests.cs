using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Testing;
using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// A watcher that fails in a "describe --watch", and the variable that makes the watch poll.
public sealed class WatchFailureTests : IAsyncDisposable
{
    private const string InotifyLimit =
        "The configured user limit (128) on the number of inotify watches has been reached, "
        + "or the operating system failed to allocate a required resource.";

    private readonly WatchScene _watch = new();

    public WatchFailureTests()
    {
        _ = _watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
    }

    public ValueTask DisposeAsync() => _watch.DisposeAsync();

    private string Folder => _watch.Run.Folder.Path;

    [Fact]
    public async Task Watch_WatcherError_IsReportedOnceAndTheNextPassEvaluates()
    {
        await _watch.StartAsync();
        var manifestRuns = _watch.ManifestRuns;
        var passes = _watch.Count(WatchSession.WatchingMarker);

        _watch.Watcher.Fail(new WatchFailure(Folder, Overflow: false, InotifyLimit));
        _watch.Watcher.Fail(new WatchFailure(Folder, Overflow: false, InotifyLimit));
        await _watch.TimerAsync();
        _watch.Clock.Advance(WatchSession.Debounce);
        await _watch.PassesAsync(passes + 1);

        _watch.Errors.ShouldBe(
            $"{Folder} : error SQLSRC287: watching '{Folder}' for changes failed: {InotifyLimit}\n"
                + "    help: set DOTNET_USE_POLLING_FILE_WATCHER=true to poll for changes instead\n"
                + "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/diagnostics.md#sqlsrc287\n"
        );
        _watch.ManifestRuns.ShouldBeGreaterThan(manifestRuns);
        _watch.Watcher.Open.ShouldNotBeEmpty();
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task Watch_WatcherErrorWithANewMessage_IsReportedAgain()
    {
        await _watch.StartAsync();

        _watch.Watcher.Fail(new WatchFailure(Folder, Overflow: false, "one"));
        _watch.Watcher.Fail(new WatchFailure(Folder, Overflow: false, "two"));

        _watch.Errors.Split('\n').Count(line => line.Contains("error SQLSRC287", StringComparison.Ordinal)).ShouldBe(2);
        _ = await _watch.StopAsync();
    }

    [Fact]
    public async Task Watch_Overflow_IsNotReportedAndTheNextPassEvaluates()
    {
        await _watch.StartAsync();
        var manifestRuns = _watch.ManifestRuns;
        var passes = _watch.Count(WatchSession.WatchingMarker);

        _watch.Watcher.Fail(new WatchFailure(Folder, Overflow: true, "Too many changes at once in directory"));
        await _watch.TimerAsync();
        _watch.Clock.Advance(WatchSession.Debounce);
        await _watch.PassesAsync(passes + 1);

        _watch.Errors.ShouldBeEmpty();
        _watch.ManifestRuns.ShouldBeGreaterThan(manifestRuns);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("TRUE", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("yes", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public async Task Watch_PollingVariable_PollsEachFolderOnlyWhenItAsksTo(string? value, bool polls)
    {
        if (value is not null)
        {
            _watch.Run.Environment["DOTNET_USE_POLLING_FILE_WATCHER"] = value;
        }

        await _watch.StartAsync();

        _watch.Watcher.Requests.ShouldNotBeEmpty();
        _watch.Watcher.Requests.ShouldAllBe(request => request.Poll == polls);
        _ = await _watch.StopAsync();
    }
}
