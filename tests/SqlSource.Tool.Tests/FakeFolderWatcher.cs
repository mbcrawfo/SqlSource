using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SqlSource.Tool.Watching;

namespace SqlSource.Tool.Tests;

// The watch of a test's "--watch" run, in place of the system's: it keeps each request while it is open, and a test
// raises a change or a failure through it, on the test's thread, to every open request whose folder holds the path.
internal sealed class FakeFolderWatcher
{
    private readonly Lock _gate = new();
    private readonly List<WatchRequest> _open = [];
    private readonly List<WatchRequest> _requests = [];

    // The requests that are open now, in the order they were made.
    public IReadOnlyList<WatchRequest> Open
    {
        get
        {
            lock (_gate)
            {
                return [.. _open];
            }
        }
    }

    // Every request that was made, open or closed since.
    public IReadOnlyList<WatchRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    // The CliRun.Watch of the test.
    public IDisposable Watch(WatchRequest request)
    {
        lock (_gate)
        {
            _open.Add(request);
            _requests.Add(request);
        }

        return new Closer(this, request);
    }

    // Tells the open requests whose folder holds the path, or the old path of a rename, of the change.  A change that
    // no open request holds is a mistake of the test, which would otherwise pass on a change the run never saw.
    public void Raise(FileChange change)
    {
        var holders = Open.Where(request =>
                Holds(request, change.Path) || (change.OldPath is { } old && Holds(request, old))
            )
            .ToList();
        if (holders.Count == 0)
        {
            throw new InvalidOperationException($"No open watch holds '{change.Path}'.");
        }

        foreach (var request in holders)
        {
            request.Changed(change);
        }
    }

    // Tells the open requests of the failure's folder that their watch failed.
    public void Fail(WatchFailure failure)
    {
        var holders = Open.Where(request => SameFolder(request.Folder, failure.Folder)).ToList();
        if (holders.Count == 0)
        {
            throw new InvalidOperationException($"No open watch is of '{failure.Folder}'.");
        }

        foreach (var request in holders)
        {
            request.Failed(failure);
        }
    }

    private static bool Holds(WatchRequest request, string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (parent is null)
        {
            return false;
        }

        if (SameFolder(parent, request.Folder))
        {
            return true;
        }

        var relative = Path.GetRelativePath(request.Folder, parent);
        return request.Recursive
            && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    private static bool SameFolder(string one, string other) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(one),
            Path.TrimEndingDirectorySeparator(other),
            StringComparison.OrdinalIgnoreCase
        );

    private void Close(WatchRequest request)
    {
        lock (_gate)
        {
            // By the request itself: a request is a record, and the run may open the same folder again.
            _ = _open.RemoveAll(open => ReferenceEquals(open, request));
        }
    }

    private sealed class Closer(FakeFolderWatcher watcher, WatchRequest request) : IDisposable
    {
        public void Dispose() => watcher.Close(request);
    }
}
