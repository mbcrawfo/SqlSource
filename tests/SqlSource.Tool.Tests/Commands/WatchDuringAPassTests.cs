using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Testing;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// A change made while a pass of "describe --watch" runs, the first included and while it evaluates the projects too,
// gives exactly one more pass, and an evaluation opens the watches of its folders before the pass reads a file.
public sealed class WatchDuringAPassTests : IAsyncDisposable
{
    private readonly WatchScene _watch = new();
    private readonly string _users;

    public WatchDuringAPassTests()
    {
        _users = _watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
    }

    public ValueTask DisposeAsync() => _watch.DisposeAsync();

    private static FileChange Changed(string path) => WatchTests.Changed(path);

    private static FileChange Created(string path) => WatchTests.Created(path);

    private static string NewSql => "-- name: GetNew\nSELECT 5;\n";

    // A folder that an evaluation makes in the run's temporary folder, as ProjectEvaluator names it.
    private static string ManifestFolder =>
        "tmp/" + ProjectEvaluator.ManifestFolderPrefix + "0123456789abcdef0123456789abcdef";

    private static (int Pass, int Changes, bool Evaluated)[] PassStarts(WatchScene watch) =>
        WatchTests.PassStarts(watch.Run.Folder.PathOf("run.log"));

    // Holds each describe of the scene until it is let go, and says when the first came.
    private static DescribeHold HoldDescribes(WatchScene watch)
    {
        var hold = new DescribeHold();
        watch.Describer.BeforeDescribe = request =>
        {
            _ = hold.Arrived.TrySetResult(true);
            return hold.Released.Task;
        };
        return hold;
    }

    // Holds the scene's MSBuild runs at a gate.
    private static ProcessGate HoldMSBuild(WatchScene watch)
    {
        var gate = new ProcessGate();
        watch.Run.Processes.BeforeAnswer = gate.PassAsync;
        return gate;
    }

    // Lets go of each run of MSBuild until the manifest target, and returns with that one held.
    private static async Task ReachTheManifestTargetAsync(ProcessGate gate)
    {
        var request = await gate.ArrivalAsync();
        while (!FakeProcessRunner.IsManifestRun(request))
        {
            gate.Release(request);
            request = await gate.ArrivalAsync();
        }
    }

    // Moves the clock past the wait that the changes of the last pass began, and waits for the pass they make.
    private static async Task NextPassAsync(WatchScene watch, int passes)
    {
        await watch.TimerAsync();
        watch.Clock.Advance(WatchSession.Debounce);
        await watch.PassesAsync(passes);
    }

    // A save made while the first pass describes, as when the watch starts with the editor, under the system's events
    // and under polling.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Watch_SaveDuringTheFirstPass_GivesOneMorePassThatDescribesIt(bool polls)
    {
        if (polls)
        {
            _watch.Run.Environment[WatchSession.PollingVariable] = "true";
        }

        var hold = HoldDescribes(_watch);
        _ = _watch.Begin("--log", "run.log");
        _ = await Eventually.WithinAsync(hold.Arrived.Task, "The first pass did not describe.");

        await File.WriteAllTextAsync(_users, SqlTexts.TwoUsers, TestContext.Current.CancellationToken);
        _watch.Raise(Changed(_users));
        hold.Released.SetResult();
        await _watch.PassesAsync(1);
        await NextPassAsync(_watch, 2);

        _watch.Count(WatchSession.ChangeDetectedMarker).ShouldBe(1);
        _watch.Clock.PendingTimers.ShouldBe(0);
        SidecarFiles.Read(_users).Queries.Select(entry => entry.Name).ShouldBe(["GetUser", "ListUsers"]);
        _watch.Watcher.Requests.ShouldNotBeEmpty();
        _watch.Watcher.Requests.ShouldAllBe(request => request.Poll == polls);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        PassStarts(_watch).ShouldBe([(1, 0, true), (2, 1, false)]);
    }

    // A new .sql file in the project's folder while the first evaluation runs: the set before it knows no project, so
    // the change is read by the set the evaluation gives, and the next pass evaluates.
    [Fact]
    public async Task Watch_SqlFileCreatedDuringTheFirstEvaluation_GivesOneMorePassThatEvaluates()
    {
        var gate = HoldMSBuild(_watch);
        _ = _watch.Begin("--log", "run.log");
        await ReachTheManifestTargetAsync(gate);

        var added = _watch.Run.Folder.WriteFile("New.sql", NewSql);
        _watch.Raise(Created(added));
        gate.Open();
        await _watch.PassesAsync(1);
        _watch.Describer.Described.Select(request => request.Name).ShouldBe(["GetUser"]);
        // The project's globs list the new file from now on.
        _watch.Project.ListSql(added);
        var manifestRuns = _watch.ManifestRuns;
        await NextPassAsync(_watch, 2);

        _watch.ManifestRuns.ShouldBe(manifestRuns + 1);
        _watch.Describer.Described.Select(request => request.Name).ShouldBe(["GetUser", "GetNew"]);
        _watch.Clock.PendingTimers.ShouldBe(0);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        PassStarts(_watch).ShouldBe([(1, 0, true), (2, 1, true)]);
    }

    [Fact]
    public async Task Watch_SqlFileCreatedDuringALaterEvaluation_GivesOneMorePassThatEvaluates()
    {
        await _watch.StartAsync("--log", "run.log");
        var gate = HoldMSBuild(_watch);
        _watch.Raise(Changed(_watch.Project.ProjectPath));
        await _watch.TimerAsync();
        _watch.Clock.Advance(WatchSession.Debounce);
        await ReachTheManifestTargetAsync(gate);

        var added = _watch.Run.Folder.WriteFile("New.sql", NewSql);
        _watch.Raise(Created(added));
        gate.Open();
        await _watch.PassesAsync(2);
        _watch.Project.ListSql(added);
        var manifestRuns = _watch.ManifestRuns;
        await NextPassAsync(_watch, 3);

        _watch.ManifestRuns.ShouldBe(manifestRuns + 1);
        _watch.Describer.Described.Select(request => request.Name).ShouldBe(["GetUser", "GetNew"]);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        PassStarts(_watch).ShouldBe([(1, 0, true), (2, 1, true), (3, 1, true)]);
    }

    // A change during an evaluation asks as much as the larger of the two sets says: a save of a file that the project
    // listed before the evaluation, and no longer lists, still gives a pass.
    [Fact]
    public async Task Watch_SaveDuringAnEvaluationOfAFileListedBefore_GivesOneMorePass()
    {
        await _watch.StartAsync("--log", "run.log");
        var gate = HoldMSBuild(_watch);
        _watch.Raise(Changed(_watch.Project.ProjectPath));
        await _watch.TimerAsync();
        _watch.Clock.Advance(WatchSession.Debounce);
        await ReachTheManifestTargetAsync(gate);

        _watch.Project.Unlist(_users);
        _watch.Raise(Changed(_users));
        gate.Open();
        await _watch.PassesAsync(2);
        await NextPassAsync(_watch, 3);

        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        PassStarts(_watch).ShouldBe([(1, 0, true), (2, 1, true), (3, 1, false)]);
    }

    // A listed file deleted during an evaluation that no longer lists it is gone by the set before it, so the next
    // pass evaluates, as for any listed file that is still gone.
    [Fact]
    public async Task Watch_ListedFileDeletedDuringAnEvaluation_GivesOneMorePassThatEvaluates()
    {
        await _watch.StartAsync("--log", "run.log");
        var gate = HoldMSBuild(_watch);
        _watch.Raise(Changed(_watch.Project.ProjectPath));
        await _watch.TimerAsync();
        _watch.Clock.Advance(WatchSession.Debounce);
        await ReachTheManifestTargetAsync(gate);

        _watch.Project.Unlist(_users);
        File.Delete(_users);
        _watch.Raise(new FileChange(FileChangeKind.Deleted, _users, IsFolder: false));
        gate.Open();
        await _watch.PassesAsync(2);
        await NextPassAsync(_watch, 3);

        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        PassStarts(_watch).ShouldBe([(1, 0, true), (2, 1, true), (3, 1, true)]);
    }

    // A change held through an evaluation keeps the time it came, and the next pass waits for Debounce after the last
    // change, whichever came last.
    [Fact]
    public async Task Watch_ChangesAroundAnEvaluation_TheWaitIsFromTheLastOfThem()
    {
        var gate = HoldMSBuild(_watch);
        _ = _watch.Begin();
        await ReachTheManifestTargetAsync(gate);

        _watch.Raise(Changed(_users));
        _watch.Clock.Advance(TimeSpan.FromMilliseconds(200));
        _watch.Watcher.Fail(new WatchFailure(_watch.Run.Folder.Path, Overflow: true, "lost"));
        gate.Open();
        await _watch.PassesAsync(1);
        await _watch.TimerAsync();
        _watch.Clock.Advance(TimeSpan.FromMilliseconds(100));

        _watch.Clock.PendingTimers.ShouldBe(1);
        _watch.Clock.Advance(TimeSpan.FromMilliseconds(200));
        await _watch.PassesAsync(2);
        _watch.Count(WatchSession.ChangeDetectedMarker).ShouldBe(1);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // An evaluation that throws still ends what it held: a change that came during it is kept, and the next pass
    // evaluates again.
    [Fact]
    public async Task Watch_EvaluationThatThrows_KeepsTheChangesThatCameDuringIt()
    {
        await _watch.StartAsync("--log", "run.log");
        var throws = true;
        _watch.Run.Processes.BeforeAnswer = (request, _) =>
        {
            if (!throws || !FakeProcessRunner.IsManifestRun(request))
            {
                return Task.CompletedTask;
            }

            throws = false;
            _watch.Raise(Changed(_users));
            return Task.FromException(new NotSupportedException("no manifest today"));
        };

        await _watch.ChangeAsync(Changed(_watch.Project.ProjectPath));
        _watch.Errors.ShouldContain("error SQLSRC200: sqlsource failed unexpectedly: System.NotSupportedException: ");
        await NextPassAsync(_watch, 3);

        _watch.Count(WatchSession.ChangeDetectedMarker).ShouldBe(2);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        PassStarts(_watch).ShouldBe([(1, 0, true), (2, 1, true), (3, 1, true)]);
    }

    // A watch that fails while the projects are evaluated is reported, and the next pass evaluates.
    [Fact]
    public async Task Watch_WatchFailureDuringTheFirstEvaluation_IsReportedAndTheNextPassEvaluates()
    {
        var gate = HoldMSBuild(_watch);
        _ = _watch.Begin("--log", "run.log");
        await ReachTheManifestTargetAsync(gate);

        _watch.Watcher.Fail(new WatchFailure(_watch.Run.Folder.Path, Overflow: false, "boom"));
        gate.Open();
        await _watch.PassesAsync(1);
        await NextPassAsync(_watch, 2);

        _watch.Errors.ShouldContain($"error SQLSRC287: watching '{_watch.Run.Folder.Path}' for changes failed: boom");
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        PassStarts(_watch).ShouldBe([(1, 0, true), (2, 1, true)]);
    }

    // The evaluation writes its manifests into a folder of its own in the tool's temporary folder, which a test's run
    // keeps in its working directory: the tool's own writes start no pass.
    [Fact]
    public async Task Watch_WritesInTheToolsTemporaryFolder_RunNoPass()
    {
        var gate = HoldMSBuild(_watch);
        _ = _watch.Begin();
        await ReachTheManifestTargetAsync(gate);

        var temporary = _watch.Run.Folder.PathOf(ManifestFolder);
        _watch.Raise(
            new FileChange(FileChangeKind.Created, temporary, IsFolder: true),
            Created(Path.Combine(temporary, "manifest.txt")),
            new FileChange(
                FileChangeKind.Renamed,
                Path.Combine(temporary, "b.cs"),
                IsFolder: false,
                Path.Combine(temporary, "a.cs")
            )
        );
        gate.Open();
        await _watch.PassesAsync(1);

        _watch.Clock.PendingTimers.ShouldBe(0);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        _watch.Count(WatchSession.ChangeDetectedMarker).ShouldBe(0);
    }

    // A listed file moved into an evaluation's folder is not the tool's own write: it is gone from the project.
    [Fact]
    public async Task Watch_ListedFileMovedIntoAnEvaluationsFolder_IsAChange()
    {
        await _watch.StartAsync("--log", "run.log");
        _ = _watch.Run.Folder.CreateFolder(ManifestFolder);
        var moved = _watch.Run.Folder.PathOf(ManifestFolder + "/Users.sql");
        File.Move(_users, moved);

        await _watch.ChangeAsync(new FileChange(FileChangeKind.Renamed, moved, IsFolder: false, _users));

        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        PassStarts(_watch).ShouldBe([(1, 0, true), (2, 1, true)]);
    }

    // A project in the tool's temporary folder, as one in /tmp on Linux, outside a folder that an evaluation makes: its
    // changes are not the tool's own.  Each name is one the rule must not take for an evaluation's folder.
    [Theory]
    [InlineData("tmp/App")]
    [InlineData("tmp/sqlsource-app")]
    [InlineData("tmp/sqlsource-0123456789abcdef0123456789abcdef0")]
    [InlineData("tmp/sqlsource-0123456789abcdef0123456789abcdeg")]
    [InlineData("tmp/xqlsource-0123456789abcdef0123456789abcdef")]
    public async Task Watch_ProjectInTheTemporaryFolder_GivesAPass(string directory)
    {
        await using var watch = new WatchScene(directory);
        var users = watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
        await watch.StartAsync(directory + "/App.csproj");
        await File.WriteAllTextAsync(users, SqlTexts.TwoUsers, TestContext.Current.CancellationToken);

        await watch.ChangeAsync(Changed(users));

        SidecarFiles.Read(users).Queries.Select(entry => entry.Name).ShouldBe(["GetUser", "ListUsers"]);
        (await watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // A save in a folder that an evaluating pass adds to the set, after the evaluation and while the pass describes.
    [Fact]
    public async Task Watch_SaveInAFolderTheEvaluationAdded_DuringThatPass_GivesOneMorePass()
    {
        await using var watch = new WatchScene("App");
        var users = watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
        var shared = watch.Run.Folder.WriteFile("Shared/sqlsource.json", "{}");
        await watch.StartAsync("App/App.csproj", "--log", "run.log");
        var app = Path.GetDirectoryName(watch.Project.ProjectPath);
        watch.Watcher.Open.Select(request => request.Folder).ShouldBe([app]);

        watch.Project.ListSettings(shared);
        await File.WriteAllTextAsync(users, SqlTexts.TwoUsers, TestContext.Current.CancellationToken);
        var hold = HoldDescribes(watch);
        watch.Raise(Changed(watch.Project.ProjectPath));
        await watch.TimerAsync();
        watch.Clock.Advance(WatchSession.Debounce);
        _ = await Eventually.WithinAsync(hold.Arrived.Task, "The second pass did not describe.");
        watch.Watcher.Open.Select(request => request.Folder).ShouldBe([app, Path.GetDirectoryName(shared)]);
        watch.Raise(Changed(shared));
        hold.Released.SetResult();
        await watch.PassesAsync(2);
        await NextPassAsync(watch, 3);

        watch.Count(WatchSession.ChangeDetectedMarker).ShouldBe(2);
        // The folder that both sets hold kept its watch.
        watch.Watcher.Requests.Count(request => request.Folder == app).ShouldBe(1);
        (await watch.StopAsync()).ExitCode.ShouldBe(0);
        PassStarts(watch).ShouldBe([(1, 0, true), (2, 1, true), (3, 1, false)]);
    }

    // An evaluation that keeps the folders keeps their watches: none is closed and opened again, so nothing is missed
    // between the two, and a poller does not take a new first listing.
    [Fact]
    public async Task Watch_EvaluationThatKeepsAFolder_KeepsItsWatch()
    {
        await _watch.StartAsync();
        var first = _watch.Watcher.Open.ShouldHaveSingleItem();

        await _watch.ChangeAsync(Changed(_watch.Project.ProjectPath));

        _watch.Watcher.Requests.ShouldHaveSingleItem().ShouldBeSameAs(first);
        _watch.Watcher.Open.ShouldHaveSingleItem().ShouldBeSameAs(first);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // A folder that the set no longer holds loses its watch, after the folders that the new set adds are watched, so
    // that no change falls between the two.
    [Fact]
    public async Task Watch_FolderThatLeavesTheSet_IsClosedAfterTheNewOnesAreOpen()
    {
        await using var watch = new WatchScene("App");
        _ = watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
        var shared = watch.Run.Folder.WriteFile("Shared/sqlsource.json", "{}");
        var other = watch.Run.Folder.WriteFile("Other/sqlsource.json", "{}");
        watch.Project.ListSettings(shared);
        var app = Path.GetDirectoryName(watch.Project.ProjectPath)!;
        var sharedFolder = Path.GetDirectoryName(shared)!;
        var otherFolder = Path.GetDirectoryName(other)!;
        var openWhenAsked = new Dictionary<string, string[]>();
        watch.Run.Watch = request =>
        {
            openWhenAsked[request.Folder] = [.. watch.Watcher.Open.Select(open => open.Folder)];
            return watch.Watcher.Watch(request);
        };
        await watch.StartAsync("App/App.csproj");
        watch.Watcher.Open.Select(request => request.Folder).ShouldBe([app, sharedFolder]);

        watch.Project.Unlist(shared);
        watch.Project.ListSettings(other);
        await watch.ChangeAsync(Changed(watch.Project.ProjectPath));

        watch.Watcher.Open.Select(request => request.Folder).ShouldBe([app, otherFolder]);
        openWhenAsked[otherFolder].ShouldBe([app, sharedFolder]);
        watch.Watcher.Requests.Count.ShouldBe(3);
        (await watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // A folder of the set that did not exist when it was watched is watched by a later evaluation once it does.
    [Fact]
    public async Task Watch_FolderThatAppearsLater_IsWatchedByTheNextEvaluation()
    {
        await using var watch = new WatchScene("App");
        _ = watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
        var shared = watch.Run.Folder.PathOf("Shared/sqlsource.json");
        watch.Project.ListSettings(shared);
        var app = Path.GetDirectoryName(watch.Project.ProjectPath);
        await watch.StartAsync("App/App.csproj");
        watch.Watcher.Open.Select(request => request.Folder).ShouldBe([app]);

        _ = watch.Run.Folder.WriteFile("Shared/sqlsource.json", "{}");
        await watch.ChangeAsync(Changed(watch.Project.ProjectPath));

        watch.Watcher.Open.Select(request => request.Folder).ShouldBe([app, Path.GetDirectoryName(shared)]);
        (await watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    private sealed class DescribeHold
    {
        public TaskCompletionSource<bool> Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
