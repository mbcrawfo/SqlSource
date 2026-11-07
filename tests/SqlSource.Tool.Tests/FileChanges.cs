using SqlSource.Tool.Watching;

namespace SqlSource.Tool.Tests;

// The changes a watcher reports, as the watch tests raise them.
internal static class FileChanges
{
    public static FileChange Changed(string path) => new(FileChangeKind.Changed, path, IsFolder: false);

    public static FileChange Created(string path) => new(FileChangeKind.Created, path, IsFolder: false);

    public static FileChange Deleted(string path) => new(FileChangeKind.Deleted, path, IsFolder: false);

    public static FileChange CreatedFolder(string path) => new(FileChangeKind.Created, path, IsFolder: true);

    public static FileChange Renamed(string from, string to, bool isFolder = false) =>
        new(FileChangeKind.Renamed, to, isFolder, from);
}
