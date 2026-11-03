using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Testing;
using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// What a change asks of a "describe --watch": a pass on the projects it already read, or a pass that runs their
// manifest targets again first, which a test sees as one more manifest run.
public sealed class WatchChangeTests : IAsyncDisposable
{
    private readonly WatchScene _watch = new();
    private readonly string _users;

    public WatchChangeTests()
    {
        _users = _watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
    }

    public ValueTask DisposeAsync() => _watch.DisposeAsync();

    private static FileChange Changed(string path) => WatchTests.Changed(path);

    private static FileChange Created(string path) => WatchTests.Created(path);

    // Starts the watch, makes the change, and says whether the pass it made ran the manifest target.
    private async Task<bool> EvaluatesAsync(params FileChange[] changes)
    {
        await _watch.StartAsync();
        var manifestRuns = _watch.ManifestRuns;

        await _watch.ChangeAsync(changes);

        _watch.Count(WatchSession.ChangeDetectedMarker).ShouldBe(1);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        return _watch.ManifestRuns > manifestRuns;
    }

    [Fact]
    public async Task Watch_ProjectFileChanged_RunsTheManifestTarget() =>
        (await EvaluatesAsync(Changed(_watch.Project.ProjectPath))).ShouldBeTrue();

    // A batch asks as much as the largest of its changes, not as the last: a project file saved, then a .sql file,
    // within one debounce.
    [Fact]
    public async Task Watch_ProjectFileThenSqlFileInOneBatch_RunsTheManifestTarget()
    {
        await _watch.StartAsync("--log", "run.log");
        var manifestRuns = _watch.ManifestRuns;

        await _watch.ChangeAsync(Changed(_watch.Project.ProjectPath), Changed(_users));

        _watch.ManifestRuns.ShouldBe(manifestRuns + 1);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        WatchTests.PassStarts(_watch.Run.Folder.PathOf("run.log")).ShouldBe([(1, 0, true), (2, 2, true)]);
    }

    [Fact]
    public async Task Watch_ProjectAssetsWritten_RunsTheManifestTarget() =>
        (await EvaluatesAsync(Created(_watch.Project.PathOf("obj/project.assets.json")))).ShouldBeTrue();

    // A file the project may now list, which the evaluation lists.
    [Fact]
    public async Task Watch_NewSqlFileUnderTheProject_RunsTheManifestTargetAndDescribesIt()
    {
        await _watch.StartAsync();
        var added = _watch.Project.AddSql("New.sql", "-- name: GetNew\nSELECT 5;\n");
        var manifestRuns = _watch.ManifestRuns;

        await _watch.ChangeAsync(Created(added));

        _watch.ManifestRuns.ShouldBeGreaterThan(manifestRuns);
        _watch.Describer.Described.Select(request => request.Name).ShouldBe(["GetUser", "GetNew"]);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task Watch_FolderCreatedUnderTheProject_RunsTheManifestTarget() =>
        (
            await EvaluatesAsync(new FileChange(FileChangeKind.Created, _watch.Project.PathOf("More"), IsFolder: true))
        ).ShouldBeTrue();

    [Fact]
    public async Task Watch_ListedSettingsFileChanged_RunsNoManifestTarget() =>
        (await EvaluatesAsync(Changed(_watch.Project.SettingsPath))).ShouldBeFalse();

    [Fact]
    public async Task Watch_ListedCompileFileChanged_RunsNoManifestTarget() =>
        (await EvaluatesAsync(Changed(_watch.Project.PathOf("Queries.cs")))).ShouldBeFalse();

    [Fact]
    public async Task Watch_DirectoryBuildPropsAboveTheProject_RunsTheManifestTarget()
    {
        // The repository's root, above the project's folder, holds the file: the watch goes up to it.
        await using var watch = new WatchScene("App");
        _ = watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
        _ = watch.Run.Folder.CreateFolder(".git");
        await watch.StartAsync("App/App.csproj");
        var manifestRuns = watch.ManifestRuns;

        await watch.ChangeAsync(Changed(watch.Run.Folder.PathOf("Directory.Build.props")));

        watch.ManifestRuns.ShouldBeGreaterThan(manifestRuns);
        (await watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // A project of the solution outside the solution's folder, which has no manifest while it is not restored: its
    // restore still starts a pass, which describes it.
    [Fact]
    public async Task Watch_RestoreOfAProjectOutsideTheSolutionsFolder_RunsTheManifestTarget()
    {
        await using var watch = new WatchScene("src/Other");
        _ = watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
        _ = watch.Run.Folder.WriteFile(
            "build/App.slnx",
            "<Solution><Project Path=\"../src/Other/App.csproj\" /></Solution>"
        );
        var project = watch.Run.Processes.Projects[watch.Project.ProjectPath];
        var assets = watch.Project.PathOf("obj/project.assets.json");
        project.Imported = "";
        project.ProjectAssetsFile = assets;
        project.PackageReferences.Add("SqlSource");
        await watch.StartAsync("build/App.slnx");
        watch.Errors.ShouldContain($"error SQLSRC220: '{watch.Project.ProjectPath}' has not been restored");
        var manifestRuns = watch.ManifestRuns;

        project.Imported = "true";
        _ = watch.Run.Folder.WriteFile("src/Other/obj/project.assets.json", "{}");
        await watch.ChangeAsync(Created(assets));

        watch.ManifestRuns.ShouldBeGreaterThan(manifestRuns);
        watch.Output.ShouldEndWith(
            $"postgres (postgres): 1 described, 0 skipped, 0 failed\n{WatchSession.WatchingMarker}\n"
        );
        (await watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // A project in a folder of its repository with an artifacts layout, which restores outside every folder of the
    // unit: its restore still starts a pass, which describes it.  So does one outside a repository, whose artifacts
    // folder is beside the Directory.Build.props that sets UseArtifactsOutput.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Watch_RestoreIntoAnArtifactsFolder_RunsTheManifestTarget(bool repository)
    {
        await using var watch = new WatchScene("src/App");
        _ = watch.Project.AddSql("Users.sql", SqlTexts.GetUser);
        _ = repository
            ? watch.Run.Folder.CreateFolder(".git")
            : watch.Run.Folder.WriteFile("Directory.Build.props", "<Project />");
        var project = watch.Run.Processes.Projects[watch.Project.ProjectPath];
        var assets = watch.Run.Folder.PathOf("artifacts/obj/App/project.assets.json");
        project.Imported = "";
        project.ProjectAssetsFile = assets;
        project.PackageReferences.Add("SqlSource");
        await watch.StartAsync("src/App/App.csproj");
        watch.Errors.ShouldContain($"error SQLSRC220: '{watch.Project.ProjectPath}' has not been restored");
        var manifestRuns = watch.ManifestRuns;

        project.Imported = "true";
        _ = watch.Run.Folder.WriteFile("artifacts/obj/App/project.assets.json", "{}");
        await watch.ChangeAsync(Created(assets));

        watch.ManifestRuns.ShouldBeGreaterThan(manifestRuns);
        watch.Output.ShouldEndWith(
            $"postgres (postgres): 1 described, 0 skipped, 0 failed\n{WatchSession.WatchingMarker}\n"
        );
        // The folder that the restore made is watched in place of the one above it.
        watch
            .Watcher.Open.Select(request => request.Folder)
            .ShouldContain(watch.Run.Folder.PathOf("artifacts/obj/App"));
        (await watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // The .env is read again for each pass, without the manifest target.
    [Fact]
    public async Task Watch_DotEnvWritten_TheNextPassUsesItsValue()
    {
        _ = _watch.Run.Environment.Remove("SQLSOURCE_CONNECTION_POSTGRES");
        var dotEnv = _watch.Run.Folder.WriteFile(".env", "SQLSOURCE_CONNECTION_POSTGRES=Host=first\n");
        await _watch.StartAsync();
        var manifestRuns = _watch.ManifestRuns;
        await File.WriteAllTextAsync(
            dotEnv,
            "SQLSOURCE_CONNECTION_POSTGRES=Host=second\n",
            TestContext.Current.CancellationToken
        );
        await File.WriteAllTextAsync(_users, SqlTexts.TwoUsers, TestContext.Current.CancellationToken);

        await _watch.ChangeAsync(Changed(dotEnv));

        _watch.Describer.Opened.Select(request => request.Connection).ShouldBe(["Host=first", "Host=second"]);
        _watch.ManifestRuns.ShouldBe(manifestRuns);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // How JetBrains' editors save: the file is moved aside, a new one moved into its place, and the old one deleted.
    [Fact]
    public async Task Watch_SaveByRenameOverAListedFile_RunsNoManifestTarget()
    {
        var old = _users + "___jb_old___";
        var written = _users + "___jb_tmp___";
        await File.WriteAllTextAsync(_users, SqlTexts.TwoUsers, TestContext.Current.CancellationToken);

        var evaluates = await EvaluatesAsync(
            new FileChange(FileChangeKind.Renamed, old, IsFolder: false, _users),
            new FileChange(FileChangeKind.Renamed, _users, IsFolder: false, written),
            new FileChange(FileChangeKind.Deleted, old, IsFolder: false)
        );

        evaluates.ShouldBeFalse();
        _watch.Describer.Described.Select(request => request.Name).ShouldBe(["GetUser", "ListUsers"]);
    }

    // A listed file that is still gone when the pass starts: the project may no longer list it.
    [Fact]
    public async Task Watch_ListedFileDeletedAndStillGone_RunsTheManifestTarget()
    {
        var other = _watch.Project.AddSql("Other.sql", "-- name: GetOther\nSELECT 6;\n");
        await _watch.StartAsync();
        var manifestRuns = _watch.ManifestRuns;
        File.Delete(other);

        await _watch.ChangeAsync(new FileChange(FileChangeKind.Deleted, other, IsFolder: false));

        _watch.ManifestRuns.ShouldBeGreaterThan(manifestRuns);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // The folders of a project are watched as they are when the pass ends: one that is not there has no watch.
    [Fact]
    public async Task Watch_ListedFolderThatDoesNotExist_IsNotWatched()
    {
        var gone = Path.Combine(Path.GetTempPath(), "sqlsource-tests-gone-" + Guid.NewGuid().ToString("N"));
        _watch.Project.ListSettings(Path.Combine(gone, "sqlsource.json"));

        await _watch.StartAsync();

        _watch.Watcher.Requests.Select(request => request.Folder).ShouldNotContain(gone);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
    }

    // A watch that the system refuses to start may miss a change, so a pass that evaluates follows, once.
    [Fact]
    public async Task Watch_WatchThatCannotStart_IsFollowedByOnePassThatEvaluates()
    {
        _watch.Run.Watch = _ => throw new IOException("The configured user limit (128) has been reached.");
        await _watch.StartAsync();
        var manifestRuns = _watch.ManifestRuns;

        await _watch.TimerAsync();
        _watch.Clock.Advance(WatchSession.Debounce);
        await _watch.PassesAsync(2);

        _watch.ManifestRuns.ShouldBeGreaterThan(manifestRuns);
        _watch.Clock.PendingTimers.ShouldBe(0);
        (await _watch.StopAsync()).ExitCode.ShouldBe(0);
        _watch.Count(WatchSession.ChangeDetectedMarker).ShouldBe(1);
    }
}
