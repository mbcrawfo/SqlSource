using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests.Commands.Watch;

// What ends a "describe --watch", and what does not: an error is reported in its pass and the watch goes on, a unit
// that cannot be found ends it before its first pass, and only a stop ends it after that, with 0.
public sealed class WatchEndingTests : IAsyncDisposable
{
    private const string OneDescribed = "postgres (postgres): 1 described, 0 skipped, 0 failed\n";

    private readonly WatchScene _watch = new();
    private readonly string _users;

    public WatchEndingTests()
    {
        _users = _watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
    }

    public ValueTask DisposeAsync() => _watch.DisposeAsync();

    private static FileChange Changed(string path) => WatchTests.Changed(path);

    [Fact]
    public async Task Watch_PassWithAnError_KeepsWatching()
    {
        _ = _watch.Run.Environment.Remove("SQLSOURCE_CONNECTION_POSTGRES");
        await _watch.StartAsync();
        _watch.Errors.ShouldContain($"{_users}(1,10): error SQLSRC213: ");
        _watch.Output.ShouldEndWith($"{WatchSession.WatchingMarker}\n");

        _watch.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Secrets.Connection;
        await _watch.ChangeAsync(Changed(_users));

        _watch.Output.ShouldEndWith($"{OneDescribed}{WatchSession.WatchingMarker}\n");
        _watch.Errors.Split("error SQLSRC213").Length.ShouldBe(2);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // A .env that is gone gives no value, and the next pass says the database has none.
    [Fact]
    public async Task Watch_DotEnvDeleted_NextPassReportsNoConnectionAndKeepsWatching()
    {
        _ = _watch.Run.Environment.Remove("SQLSOURCE_CONNECTION_POSTGRES");
        var dotEnv = _watch.Run.Folder.WriteFile(".env", "SQLSOURCE_CONNECTION_POSTGRES=Host=db\n");
        await _watch.StartAsync();
        _watch.Errors.ShouldBeEmpty();
        File.Delete(dotEnv);
        await File.WriteAllTextAsync(_users, SqlTexts.TwoUsers, TestContext.Current.CancellationToken);

        await _watch.ChangeAsync(new FileChange(FileChangeKind.Deleted, dotEnv, IsFolder: false));

        // Against the one query to describe, the second.
        _watch.Errors.ShouldContain($"{_users}(4,10): error SQLSRC213: ");
        _watch.Output.ShouldEndWith($"{WatchSession.WatchingMarker}\n");
        await _watch.ChangeAsync(Changed(_users));
        _watch.Count(WatchSession.WatchingMarker).ShouldBe(3);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // The projects are evaluated again for every pass until the evaluation reports nothing, whatever the change.
    [Fact]
    public async Task Watch_ProjectNotRestored_IsReportedEachPassUntilRestored()
    {
        var project = _watch.Run.Processes.Projects[_watch.Project.ProjectPath];
        var assets = _watch.Project.PathOf("obj/project.assets.json");
        project.Imported = "";
        project.ProjectAssetsFile = assets;
        project.PackageReferences.Add("SqlSource");
        var notRestored = $"error SQLSRC220: '{_watch.Project.ProjectPath}' has not been restored";
        await _watch.StartAsync();
        _watch.Errors.Split(notRestored).Length.ShouldBe(2);

        await _watch.ChangeAsync(Changed(_watch.Run.Folder.PathOf(".env")));
        _watch.Errors.Split(notRestored).Length.ShouldBe(3);

        project.Imported = "true";
        _ = _watch.Run.Folder.WriteFile("obj/project.assets.json", "{}");
        await _watch.ChangeAsync(WatchTests.Created(assets));

        _watch.Errors.Split(notRestored).Length.ShouldBe(3);
        _watch.Output.ShouldEndWith($"{OneDescribed}{WatchSession.WatchingMarker}\n");
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task Watch_DescriberThrows_IsSqlsrc200AndKeepsWatching()
    {
        _watch.Describer.Exceptions["GetUser"] = new InvalidOperationException("boom");
        await _watch.StartAsync();
        _watch.Errors.ShouldContain("error SQLSRC200: ");
        _watch.Output.ShouldEndWith($"{WatchSession.WatchingMarker}\n");

        _ = _watch.Describer.Exceptions.Remove("GetUser");
        await _watch.ChangeAsync(Changed(_users));

        _watch.Output.ShouldEndWith($"{OneDescribed}{WatchSession.WatchingMarker}\n");
        SidecarFiles.Read(_users).Queries.ShouldHaveSingleItem().Name.ShouldBe("GetUser");
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // An exception of the tool's own, not a describer's, is the pass's error too.
    [Fact]
    public async Task Watch_ExceptionInAPass_IsSqlsrc200AndKeepsWatching()
    {
        var fails = true;
        _watch.Run.ReadTextFile = path =>
            fails && Path.GetFileName(path) == ".env"
                ? throw new NotSupportedException("no reading today")
                : Hosts.ReadUnderTheTemporaryFolder(path);
        await _watch.StartAsync();
        _watch.Errors.ShouldStartWith(
            "sqlsource : error SQLSRC200: sqlsource failed unexpectedly: System.NotSupportedException: "
                + "no reading today\n"
        );

        fails = false;
        await _watch.ChangeAsync(Changed(_users));

        _watch.Output.ShouldEndWith($"{OneDescribed}{WatchSession.WatchingMarker}\n");
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // Each pass has its own count of errors: a sidecar is deleted only by a pass that reported none.
    [Fact]
    public async Task Watch_ErrorInOnePass_DoesNotStopTheNextFromDeletingASidecar()
    {
        var plain = _watch.Project.AddSql("Plain.sql", SqlTexts.Plain);
        var stale = SidecarFormat.PathFor(plain);
        await File.WriteAllTextAsync(stale, SidecarFiles.Empty(), TestContext.Current.CancellationToken);
        _watch.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");
        await _watch.StartAsync();
        _watch.Errors.ShouldContain("error SQLSRC999: ");
        File.Exists(stale).ShouldBeTrue();

        _ = _watch.Describer.Failures.Remove("GetUser");
        await _watch.ChangeAsync(Changed(_users));

        File.Exists(stale).ShouldBeFalse();
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task Watch_Stopped_ExitsZeroWithoutTheRunAgainLine()
    {
        _watch.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");
        await _watch.StartAsync();

        var result = await _watch.StopAsync();

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldContain("error SQLSRC999: ");
        result.Error.ShouldNotContain(DescribeScene.RunAgain);
        result.Error.ShouldNotContain("run again");
    }

    // A stop ends the pass under way, which writes nothing.
    [Fact]
    public async Task Watch_StoppedDuringAPass_ExitsZeroAndWritesNoSidecar()
    {
        var gate = new ProcessGate();
        _watch.Run.Processes.BeforeAnswer = gate.PassAsync;
        _ = _watch.Begin();
        _ = await gate.ArrivalAsync();

        var result = await _watch.StopAsync();

        result.ShouldBe(new CliResult(0, "", ""));
        File.Exists(SidecarFormat.PathFor(_users)).ShouldBeFalse();
        // The unit's folder, and the folder above it outside a repository, were watched from before the first pass,
        // and are no longer.
        _watch
            .Watcher.Requests.Select(request => (request.Folder, request.Recursive))
            .ShouldBe([(_watch.Above, false), (_watch.Run.Folder.Path, true)], ignoreOrder: true);
        _watch.Watcher.Open.ShouldBeEmpty();
    }

    [Fact]
    public async Task Watch_UnitNotFound_ExitsOneBeforeAnyPass()
    {
        using var run = new CliRun();
        var watcher = new FakeFolderWatcher();
        run.Watch = watcher.Watch;

        var result = await Eventually.WithinAsync(
            run.Start(TestContext.Current.CancellationToken, "describe", "--watch").Ended,
            "The watch did not end."
        );

        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"sqlsource : error SQLSRC201: '{run.Folder.Path}' holds no .sln, .slnx or .csproj file\n"
                    + Expected.See("SQLSRC201")
            )
        );
        watcher.Requests.ShouldBeEmpty();
    }
}
