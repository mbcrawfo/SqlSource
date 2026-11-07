using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Testing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Watching;
using Xunit;
using static SqlSource.Tool.Tests.FileChanges;

namespace SqlSource.Tool.Tests.Commands.Watch;

// A watcher that fails in a "describe --watch", and the variable that makes the watch poll.
public sealed class WatchFailureTests : IAsyncDisposable
{
    private const string InotifyLimit =
        "The configured user limit (65536) on the number of inotify watches has been reached, "
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
        // Written by the session's loop, before the pass it starts, and not on the watch's thread.
        _watch.Errors.ShouldBeEmpty();
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

    // A watch that polls already does what the help line says, so a folder it cannot list is reported without it.
    [Theory]
    [InlineData("true")]
    [InlineData("1")]
    public async Task Watch_WatcherErrorWhilePolling_HasNoHelpLine(string polling)
    {
        _watch.Run.Environment[WatchSession.PollingVariable] = polling;
        await _watch.StartAsync();
        var passes = _watch.Count(WatchSession.WatchingMarker);
        var message = $"Could not find a part of the path '{Folder}'.";

        _watch.Watcher.Fail(new WatchFailure(Folder, Overflow: false, message));
        await _watch.TimerAsync();
        _watch.Clock.Advance(WatchSession.Debounce);
        await _watch.PassesAsync(passes + 1);

        _watch.Errors.ShouldBe(
            $"{Folder} : error SQLSRC287: watching '{Folder}' for changes failed: {message}\n"
                + "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/diagnostics.md#sqlsrc287\n"
        );
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task Watch_WatcherErrorWithANewMessage_IsReportedAgain()
    {
        await _watch.StartAsync();

        var passes = _watch.Count(WatchSession.WatchingMarker);

        _watch.Watcher.Fail(new WatchFailure(Folder, Overflow: false, "one"));
        _watch.Watcher.Fail(new WatchFailure(Folder, Overflow: false, "two"));
        await _watch.TimerAsync();
        _watch.Clock.Advance(WatchSession.Debounce);
        await _watch.PassesAsync(passes + 1);

        _watch.Errors.Split('\n').Count(line => line.Contains("error SQLSRC287", StringComparison.Ordinal)).ShouldBe(2);
        _ = await _watch.StopAsync();
    }

    // A failure that comes while a pass runs, the first or a later one, is written after that pass's output, and not in
    // the middle of it.  Every pass reports the query, which the server rejects.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Watch_WatcherErrorDuringAPass_IsWrittenAfterThePassesOutput(bool first)
    {
        _watch.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");
        var arrived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task Hold(DescribeRequest request)
        {
            _ = arrived.TrySetResult(true);
            return released.Task;
        }

        if (first)
        {
            _watch.Describer.BeforeDescribe = Hold;
            _ = _watch.Begin();
        }
        else
        {
            await _watch.StartAsync();
            _watch.Describer.BeforeDescribe = Hold;
            _watch.Raise(Changed(_watch.Project.PathOf("Users.sql")));
            await _watch.TimerAsync();
            _watch.Clock.Advance(WatchSession.Debounce);
        }

        _ = await Eventually.WithinAsync(arrived.Task, "The pass did not describe.");
        _watch.Watcher.Fail(new WatchFailure(Folder, Overflow: false, InotifyLimit));
        var beforeTheEnd = _watch.Errors;
        released.SetResult();
        await _watch.PassesAsync(first ? 1 : 2);

        beforeTheEnd.ShouldNotContain("SQLSRC287");
        var errors = _watch.Errors;
        errors
            .IndexOf("error SQLSRC287", StringComparison.Ordinal)
            .ShouldBeGreaterThan(errors.LastIndexOf("'GetUser'", StringComparison.Ordinal), errors);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // A failure that comes while the watch waits is written before the output of the pass it starts.
    [Fact]
    public async Task Watch_WatcherErrorWhileWaiting_IsWrittenBeforeTheNextPassesOutput()
    {
        _watch.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");
        await _watch.StartAsync();

        _watch.Watcher.Fail(new WatchFailure(Folder, Overflow: false, InotifyLimit));
        await _watch.TimerAsync();
        _watch.Clock.Advance(WatchSession.Debounce);
        await _watch.PassesAsync(2);

        var errors = _watch.Errors;
        var failure = errors.IndexOf("error SQLSRC287", StringComparison.Ordinal);
        failure.ShouldBeGreaterThan(errors.IndexOf("'GetUser'", StringComparison.Ordinal), errors);
        failure.ShouldBeLessThan(errors.LastIndexOf("'GetUser'", StringComparison.Ordinal), errors);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
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
