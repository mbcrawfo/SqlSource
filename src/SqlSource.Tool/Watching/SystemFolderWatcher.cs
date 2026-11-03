using System;
using System.IO;

namespace SqlSource.Tool.Watching;

/// <summary>
/// A watch over the system's file events, which is one <see cref="FileSystemWatcher" />.  macOS and Linux raise
/// different kinds for the same save, and Linux misses the files written into a folder before its own watch is added,
/// so what a reader makes of the events is its business and not this class's: it passes each on as it came.
/// </summary>
internal sealed class SystemFolderWatcher : IDisposable
{
    // The most that the system holds for the tool between two reads, in place of the default of 8 KiB.
    private const int BufferSize = 64 * 1024;

    private readonly WatchRequest _request;
    private readonly FileSystemWatcher _watcher;

    public SystemFolderWatcher(WatchRequest request)
    {
        _request = request;
        _watcher = new FileSystemWatcher(request.Folder)
        {
            IncludeSubdirectories = request.Recursive,
            NotifyFilter =
                NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = BufferSize,
        };
        _watcher.Changed += (_, e) => Raise(FileChangeKind.Changed, e.FullPath);
        _watcher.Created += (_, e) => Raise(FileChangeKind.Created, e.FullPath);
        _watcher.Deleted += (_, e) => Raise(FileChangeKind.Deleted, e.FullPath);
        _watcher.Renamed += (_, e) => Raise(FileChangeKind.Renamed, e.FullPath, e.OldFullPath);
        _watcher.Error += (_, e) =>
        {
            var exception = e.GetException();
            _request.Failed(
                new WatchFailure(_request.Folder, exception is InternalBufferOverflowException, exception.Message)
            );
        };
        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose() => _watcher.Dispose();

    private void Raise(FileChangeKind kind, string path, string? oldPath = null) =>
        _request.Changed(new FileChange(kind, path, kind != FileChangeKind.Deleted && Directory.Exists(path), oldPath));
}
