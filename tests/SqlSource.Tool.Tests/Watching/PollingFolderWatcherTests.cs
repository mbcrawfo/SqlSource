using System;
using System.Collections.Generic;
using System.IO;
using Shouldly;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests.Watching;

// Each test lists the folder itself with Poll and never waits for the timer, which its clock does not fire.
public sealed class PollingFolderWatcherTests
{
    [Fact]
    public void Poll_FileCreated_RaisesCreated()
    {
        using var scene = new Scene();
        using var watch = scene.Start();

        var path = scene.Folder.WriteFile("Users.sql", "SELECT 1");
        watch.Poll();

        scene.Seen.ShouldBe([new FileChange(FileChangeKind.Created, path, IsFolder: false)]);
    }

    [Fact]
    public void Poll_FileRewritten_RaisesChanged()
    {
        using var scene = new Scene();
        var path = scene.Folder.WriteFile("Users.sql", "SELECT 1");
        using var watch = scene.Start();

        File.WriteAllText(path, "SELECT 2");
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));
        watch.Poll();

        scene.Seen.ShouldBe([new FileChange(FileChangeKind.Changed, path, IsFolder: false)]);
    }

    [Fact]
    public void Poll_FileDeleted_RaisesDeleted()
    {
        using var scene = new Scene();
        var path = scene.Folder.WriteFile("Users.sql", "SELECT 1");
        using var watch = scene.Start();

        File.Delete(path);
        watch.Poll();

        scene.Seen.ShouldBe([new FileChange(FileChangeKind.Deleted, path, IsFolder: false)]);
    }

    [Fact]
    public void Poll_FolderCreated_RaisesCreatedFolder()
    {
        using var scene = new Scene();
        using var watch = scene.Start();

        var path = scene.Folder.CreateFolder("queries");
        watch.Poll();

        scene.Seen.ShouldBe([new FileChange(FileChangeKind.Created, path, IsFolder: true)]);
    }

    [Fact]
    public void Poll_NothingChanged_RaisesNothing()
    {
        using var scene = new Scene();
        _ = scene.Folder.WriteFile("queries/Users.sql", "SELECT 1");
        using var watch = scene.Start();

        watch.Poll();
        watch.Poll();

        scene.Seen.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("bin/Debug/App.dll")]
    [InlineData("obj/Debug/App.dll")]
    [InlineData("obj/project.nuget.cache")]
    [InlineData("obj/x/project.nuget.cache")]
    [InlineData(".git/index")]
    [InlineData("src/App/bin/App.dll")]
    public void Poll_FileUnderBinObjOrGit_RaisesNothing(string relativePath)
    {
        using var scene = new Scene();
        _ = scene.Folder.CreateFolder("src/App");
        using var watch = scene.Start();

        _ = scene.Folder.WriteFile(relativePath, "x");
        watch.Poll();

        scene.Seen.ShouldBeEmpty();
    }

    // At any depth below obj, as an artifacts layout or a custom MSBuildProjectExtensionsPath puts it.
    [Theory]
    [InlineData("src/App/obj/project.assets.json")]
    [InlineData("src/App/obj/x/project.assets.json")]
    [InlineData("artifacts/obj/App/project.assets.json")]
    public void Poll_ProjectAssetsWritten_RaisesChanged(string relativePath)
    {
        using var scene = new Scene();
        var path = scene.Folder.WriteFile(relativePath, "{}");
        using var watch = scene.Start();

        File.WriteAllText(path, "{ }");
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));
        watch.Poll();

        scene.Seen.ShouldBe([new FileChange(FileChangeKind.Changed, path, IsFolder: false)]);
    }

    [Theory]
    [InlineData("src/App/obj/x/project.assets.json")]
    [InlineData("artifacts/obj/App/project.assets.json")]
    public void Poll_ProjectAssetsCreatedBelowObj_RaisesCreated(string relativePath)
    {
        using var scene = new Scene();
        _ = scene.Folder.CreateFolder(Path.GetDirectoryName(relativePath)!);
        using var watch = scene.Start();

        var path = scene.Folder.WriteFile(relativePath, "{}");
        watch.Poll();

        scene.Seen.ShouldBe([new FileChange(FileChangeKind.Created, path, IsFolder: false)]);
    }

    // The assets file below a bin or .git folder of obj is passed over, as WatchSet ignores it.
    [Theory]
    [InlineData("src/App/obj/bin/project.assets.json")]
    [InlineData("src/App/obj/.git/project.assets.json")]
    [InlineData("src/App/bin/obj/project.assets.json")]
    public void Poll_ProjectAssetsBelowBinOrGit_RaisesNothing(string relativePath)
    {
        using var scene = new Scene();
        _ = scene.Folder.CreateFolder(Path.GetDirectoryName(relativePath)!);
        using var watch = scene.Start();

        _ = scene.Folder.WriteFile(relativePath, "{}");
        watch.Poll();

        scene.Seen.ShouldBeEmpty();
    }

    [Fact]
    public void Poll_NotRecursive_RaisesNothingForAFileBelow()
    {
        using var scene = new Scene();
        _ = scene.Folder.CreateFolder("queries");
        using var watch = scene.Start(recursive: false);

        _ = scene.Folder.WriteFile("queries/Users.sql", "SELECT 1");
        var path = scene.Folder.WriteFile("Orders.sql", "SELECT 2");
        watch.Poll();

        scene.Seen.ShouldBe([new FileChange(FileChangeKind.Created, path, IsFolder: false)]);
    }

    // A folder that is gone raises one failure however often it is listed, and its changes, against the last listing
    // that worked, once it is back; gone again, it raises a failure again.
    [Fact]
    public void Poll_FolderGone_RaisesOneFailureUntilItIsBack()
    {
        using var scene = new Scene();
        var queries = scene.Folder.CreateFolder("queries");
        using var watch = scene.Start();
        Directory.Delete(queries);
        Directory.Delete(scene.Folder.Path);

        watch.Poll();
        watch.Poll();
        watch.Poll();

        var failure = scene.Failures.ShouldHaveSingleItem();
        failure.Folder.ShouldBe(scene.Folder.Path);
        failure.Overflow.ShouldBeFalse();
        failure.Message.ShouldNotBeEmpty();
        scene.Seen.ShouldBeEmpty();

        var users = scene.Folder.WriteFile("Users.sql", "SELECT 1");
        watch.Poll();

        scene.Seen.ShouldBe([
            new FileChange(FileChangeKind.Created, users, IsFolder: false),
            new FileChange(FileChangeKind.Deleted, queries, IsFolder: false),
        ]);
        scene.Failures.Count.ShouldBe(1);

        File.Delete(users);
        Directory.Delete(scene.Folder.Path);
        watch.Poll();
        watch.Poll();

        scene.Failures.Count.ShouldBe(2);
        _ = Directory.CreateDirectory(scene.Folder.Path);
    }

    // A clock whose timers never fire, so that only the test polls.
    private sealed class StillClock : TimeProvider { }

    private sealed class Scene : IDisposable
    {
        public TempFolder Folder { get; } = new();

        public List<FileChange> Seen { get; } = [];

        public List<WatchFailure> Failures { get; } = [];

        public PollingFolderWatcher Start(bool recursive = true) =>
            new(
                new WatchRequest(Folder.Path, recursive, Poll: true, Seen.Add, Failures.Add),
                new StillClock(),
                PollingFolderWatcher.Interval
            );

        public void Dispose() => Folder.Dispose();
    }
}
