using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests.Commands.Watch;

// "describe --watch": a pass, then a pass after each change that asks for one, with the options of the command line in
// each.  The changes come from a FakeFolderWatcher, and the time from the scene's clock.
public sealed class WatchTests : IAsyncDisposable
{
    private const string OneDescribed = "postgres (postgres): 1 described, 0 skipped, 0 failed";

    private readonly WatchScene _watch = new();
    private readonly string _users;

    public WatchTests()
    {
        _users = _watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
    }

    public ValueTask DisposeAsync() => _watch.DisposeAsync();

    private string LogPath => _watch.Run.Folder.PathOf("run.log");

    internal static FileChange Changed(string path) => new(FileChangeKind.Changed, path, IsFolder: false);

    internal static FileChange Created(string path) => new(FileChangeKind.Created, path, IsFolder: false);

    // The members of each pass.start of the log, in order.
    internal static (int Pass, int Changes, bool Evaluated)[] PassStarts(string log) =>
        [
            .. LogReader
                .Events(log, "pass.start")
                .Select(line =>
                    (
                        line.GetProperty("pass").GetInt32(),
                        line.GetProperty("changes").GetInt32(),
                        line.GetProperty("evaluated").GetBoolean()
                    )
                ),
        ];

    [Fact]
    public async Task Watch_Start_DescribesThenWritesTheWatchingMarker()
    {
        AboveTheTempFolder.SkipWhenInARepository(_watch.Run.Folder.Path);
        await _watch.StartAsync();

        _watch.Output.ShouldBe($"{OneDescribed}\n{WatchSession.WatchingMarker}\n");
        _watch
            .Watcher.Open.Select(request => (request.Folder, request.Recursive, request.Poll))
            .ShouldBe([(_watch.Above, false, false), (_watch.Run.Folder.Path, true, false)], ignoreOrder: true);
        SidecarFiles.Read(_users).Queries.ShouldHaveSingleItem().Name.ShouldBe("GetUser");
        var result = await _watch.StopAsync();
        result.ShouldBe(new CliResult(0, $"{OneDescribed}\n{WatchSession.WatchingMarker}\n", ""));
        _watch.Watcher.Open.ShouldBeEmpty();
    }

    [Fact]
    public async Task Watch_ListedSqlFileWritten_DescribesItWithoutAManifestRun()
    {
        await _watch.StartAsync();
        var before = _watch.Output;
        var manifestRuns = _watch.ManifestRuns;
        await File.WriteAllTextAsync(_users, SqlTexts.TwoUsers, TestContext.Current.CancellationToken);

        await _watch.ChangeAsync(Changed(_users));

        _watch.Output.ShouldBe(
            before
                + $"{WatchSession.ChangeDetectedMarker}\n"
                + "postgres (postgres): 1 described, 1 skipped, 0 failed\n"
                + $"{WatchSession.WatchingMarker}\n"
        );
        SidecarFiles.Read(_users).Queries.Select(entry => entry.Name).ShouldBe(["GetUser", "ListUsers"]);
        _watch.ManifestRuns.ShouldBe(manifestRuns);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // A pass writes its sidecar through a temporary file and a rename, which a watch of the folder sees.  Were they
    // changes, each pass would make another.
    [Fact]
    public async Task Watch_OwnSidecarWrites_RunNoPass()
    {
        await _watch.StartAsync("--log", "run.log");
        var sidecar = SidecarFormat.PathFor(_users);
        var temporary = sidecar + ".0123456789abcdef0123456789abcdef.tmp";
        await File.WriteAllTextAsync(_users, SqlTexts.TwoUsers, TestContext.Current.CancellationToken);

        await _watch.ChangeAsync(
            Created(temporary),
            Changed(temporary),
            Created(sidecar),
            Changed(sidecar),
            new FileChange(FileChangeKind.Renamed, sidecar, IsFolder: false, temporary),
            new FileChange(FileChangeKind.Deleted, temporary, IsFolder: false),
            Changed(_users)
        );

        _watch.Count(WatchSession.ChangeDetectedMarker).ShouldBe(1);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        PassStarts(LogPath).ShouldBe([(1, 0, true), (2, 1, false)]);
    }

    // A build beside the watch writes hundreds of files under bin and obj.
    [Fact]
    public async Task Watch_BuildOutputsWritten_RunNoPass()
    {
        await _watch.StartAsync("--log", "run.log");
        var outputs = Enumerable
            .Range(0, 100)
            .SelectMany(index =>
                new[]
                {
                    Created(_watch.Project.PathOf($"bin/Debug/net10.0/App{index}.dll")),
                    Changed(_watch.Project.PathOf($"obj/Debug/net10.0/App{index}.cs")),
                }
            );

        await _watch.ChangeAsync([.. outputs, Changed(_users)]);

        _watch.Count(WatchSession.ChangeDetectedMarker).ShouldBe(1);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        PassStarts(LogPath).ShouldBe([(1, 0, true), (2, 1, false)]);
    }

    // A pass starts when the clock is past the last change by the debounce, and not before.
    [Fact]
    public async Task Watch_ChangesWithinTheDebounce_GiveOnePass()
    {
        await _watch.StartAsync("--log", "run.log");

        _watch.Raise(Changed(_users));
        await _watch.TimerAsync();
        _watch.Clock.Advance(TimeSpan.FromMilliseconds(200));
        _watch.Raise(Changed(_users));
        _watch.Clock.Advance(TimeSpan.FromMilliseconds(200));

        await _watch.TimerAsync();
        _watch.Count(WatchSession.ChangeDetectedMarker).ShouldBe(0);
        _watch.Clock.Advance(TimeSpan.FromMilliseconds(100));
        await _watch.PassesAsync(2);
        _watch.Count(WatchSession.ChangeDetectedMarker).ShouldBe(1);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        PassStarts(LogPath).ShouldBe([(1, 0, true), (2, 2, false)]);
    }

    // Saves during a long pass make one more pass after it, not one each.
    [Fact]
    public async Task Watch_ChangesDuringAPass_GiveExactlyOneMorePass()
    {
        await _watch.StartAsync("--log", "run.log");
        var gate = new ProcessGate();
        _watch.Run.Processes.BeforeAnswer = gate.PassAsync;

        _watch.Raise(Changed(_watch.Project.ProjectPath));
        await _watch.TimerAsync();
        _watch.Clock.Advance(WatchSession.Debounce);
        _ = await gate.ArrivalAsync();
        for (var save = 0; save < 5; save++)
        {
            _watch.Raise(Changed(_users));
        }

        gate.Open();
        await _watch.PassesAsync(2);
        _watch.Clock.PendingTimers.ShouldBe(1);
        _watch.Clock.Advance(WatchSession.Debounce);
        await _watch.PassesAsync(3);

        _watch.Clock.PendingTimers.ShouldBe(0);
        _watch.Count(WatchSession.ChangeDetectedMarker).ShouldBe(2);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        PassStarts(LogPath).ShouldBe([(1, 0, true), (2, 1, true), (3, 5, false)]);
    }

    [Fact]
    public async Task Watch_Force_ForcesTheFirstPassOnly()
    {
        await File.WriteAllTextAsync(_users, SqlTexts.TwoUsers, TestContext.Current.CancellationToken);
        _ = await _watch.Scene.DescribeAsync();
        _watch.Describer.Described.Clear();

        await _watch.StartAsync("--force");
        _watch.Describer.Described.Select(request => request.Name).ShouldBe(["GetUser", "ListUsers"]);
        _watch.Describer.Described.Clear();
        await File.WriteAllTextAsync(
            _users,
            SqlTexts.WithSecondQueryEdited(SqlTexts.TwoUsers),
            TestContext.Current.CancellationToken
        );
        await _watch.ChangeAsync(Changed(_users));

        _watch.Describer.Described.Select(request => request.Name).ShouldBe(["ListUsers"]);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task Watch_Filters_NarrowEveryPass()
    {
        await File.WriteAllTextAsync(_users, SqlTexts.Users, TestContext.Current.CancellationToken);
        var invoices = _watch.Project.AddSql("Invoices.sql", SqlTexts.ListInvoices);

        await _watch.StartAsync("--database", "billing", "Users.sql");
        _watch.Describer.Described.Select(request => request.Name).ShouldBe(["GetInvoice"]);
        _watch.Describer.Described.Clear();
        var edited = SqlTexts
            .WithSecondQueryEdited(SqlTexts.Users)
            .Replace("SELECT 1;", "SELECT 11;", StringComparison.Ordinal);
        await File.WriteAllTextAsync(_users, edited, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            invoices,
            SqlTexts.ListInvoices.Replace("SELECT 3;", "SELECT 33;", StringComparison.Ordinal),
            TestContext.Current.CancellationToken
        );
        await _watch.ChangeAsync(Changed(_users), Changed(invoices));

        _watch.Describer.Described.Select(request => request.Name).ShouldBe(["GetInvoice"]);
        File.Exists(SidecarFormat.PathFor(invoices)).ShouldBeFalse();
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // One log for the session: run.start, then each pass between its two events, then run.end.
    [Fact]
    public async Task Watch_Log_IsOneFileForTheSession()
    {
        await _watch.StartAsync("--log", "run.log");
        await File.WriteAllTextAsync(_users, SqlTexts.TwoUsers, TestContext.Current.CancellationToken);
        await _watch.ChangeAsync(Changed(_users));

        (await _watch.StopAsync()).ExitCode.ShouldBe(0);

        var names = LogReader.Names(LogPath);
        names
            .Where(name => name is "run.start" or "pass.start" or "pass.end" or "run.end")
            .ShouldBe(["run.start", "pass.start", "pass.end", "pass.start", "pass.end", "run.end"]);
        names[0].ShouldBe("run.start");
        names[^1].ShouldBe("run.end");
        // The first event after run.start opens a pass, and the last before run.end closes one.
        names[1].ShouldBe("pass.start");
        names[^2].ShouldBe("pass.end");
        PassStarts(LogPath).ShouldBe([(1, 0, true), (2, 1, false)]);
        var ends = LogReader.Events(LogPath, "pass.end");
        ends.Select(line => (line.GetProperty("pass").GetInt32(), line.GetProperty("errors").GetInt32()))
            .ShouldBe([(1, 0), (2, 0)]);
        ends.ShouldAllBe(line => line.GetProperty("durationMs").ValueKind == JsonValueKind.Number);
        LogReader.Events(LogPath, "run.end").ShouldHaveSingleItem().GetProperty("exitCode").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Watch_Verbose_AppliesToEveryPass()
    {
        await _watch.StartAsync("--verbose");
        await _watch.ChangeAsync(Changed(_users));

        _watch
            .Output.Split('\n')
            .Count(line => line.StartsWith($"sqlsource: file '{_users}': ", StringComparison.Ordinal))
            .ShouldBe(2);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }
}
