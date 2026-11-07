using System;
using System.Collections.Generic;
using System.IO;
using Shouldly;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests.Watching;

// Each test lists the folder itself with Poll.  The watcher's timer is TestClock's, which fires only when a test
// advances the clock, and none does, so no other listing lands between two steps of a test's change.
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

    // A watch of the folder alone sees a folder made in it, and nothing of what it holds.
    [Fact]
    public void Poll_NotRecursiveFolderCreated_RaisesCreatedFolderAlone()
    {
        using var scene = new Scene();
        using var watch = scene.Start(recursive: false);

        var queries = scene.Folder.CreateFolder("queries");
        _ = scene.Folder.WriteFile("queries/Users.sql", "SELECT 1");
        watch.Poll();

        scene.Seen.ShouldBe([new FileChange(FileChangeKind.Created, queries, IsFolder: true)]);
    }

    [Fact]
    public void Poll_NotRecursiveFolderDeleted_RaisesDeleted()
    {
        using var scene = new Scene();
        _ = scene.Folder.WriteFile("queries/Users.sql", "SELECT 1");
        var queries = scene.Folder.PathOf("queries");
        using var watch = scene.Start(recursive: false);

        Directory.Delete(queries, recursive: true);
        watch.Poll();

        scene.Seen.ShouldBe([new FileChange(FileChangeKind.Deleted, queries, IsFolder: false)]);
    }

    // A folder of any name: artifacts, watched alone above an assets folder artifacts/obj/App, sees obj deleted.
    [Theory]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData(".git")]
    public void Poll_NotRecursiveIgnoredFolderCreated_RaisesCreatedFolder(string name)
    {
        using var scene = new Scene();
        using var watch = scene.Start(recursive: false);

        var folder = scene.Folder.CreateFolder(name);
        watch.Poll();

        scene.Seen.ShouldBe([new FileChange(FileChangeKind.Created, folder, IsFolder: true)]);
    }

    // A link to a folder is listed by its name, and what it leads to is not.
    [Fact]
    public void Poll_NotRecursiveLinkToAFolderCreated_RaisesCreatedFolderAlone()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Making a symbolic link needs a privilege on Windows.");
        using var scene = new Scene();
        using var other = new TempFolder();
        _ = other.WriteFile("Users.sql", "SELECT 1");
        using var watch = scene.Start(recursive: false);

        var link = scene.Folder.PathOf("linked");
        _ = Directory.CreateSymbolicLink(link, other.Path);
        watch.Poll();

        scene.Seen.ShouldBe([new FileChange(FileChangeKind.Created, link, IsFolder: true)]);
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

    private sealed class Scene : IDisposable
    {
        public TempFolder Folder { get; } = new();

        public List<FileChange> Seen { get; } = [];

        public List<WatchFailure> Failures { get; } = [];

        public PollingFolderWatcher Start(bool recursive = true) =>
            new(
                new WatchRequest(Folder.Path, recursive, Poll: true, Seen.Add, Failures.Add),
                new TestClock(),
                PollingFolderWatcher.Interval
            );

        public void Dispose() => Folder.Dispose();
    }
}
