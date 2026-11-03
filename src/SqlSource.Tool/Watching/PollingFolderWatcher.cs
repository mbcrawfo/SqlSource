using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace SqlSource.Tool.Watching;

/// <summary>
/// A watch that lists the folder on a timer and compares each listing with the one before, for a file system that
/// raises no events, as a bind mount into a container often does.  It sees a file created, deleted or rewritten
/// and a folder created, and none of the finer kinds of the system's events.
/// </summary>
internal sealed class PollingFolderWatcher : IDisposable
{
    /// <summary>
    /// How often a real watch lists the folder.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    // A folder whose files are not listed: the build's output and the repository's own files change a lot and name no
    // query.
    private static readonly string[] IgnoredFolders = ["bin", "obj", ".git"];

    // The one file of an ignored folder that is listed, because a restore writes it and a build of the project follows:
    // at any depth below an obj folder, as an artifacts layout or a custom MSBuildProjectExtensionsPath puts it.  It is
    // looked for below every folder of obj but bin and .git.
    private const string AssetsFolder = "obj";
    private const string AssetsFile = "project.assets.json";

    private readonly WatchRequest _request;
    private readonly ITimer _timer;
    private readonly object _gate = new();
    private Dictionary<string, Entry>? _last;
    private bool _disposed;

    public PollingFolderWatcher(WatchRequest request, TimeProvider time, TimeSpan interval)
    {
        _request = request;
        // The first listing is what the folder holds when the watch starts, and raises nothing.
        Poll();
        _timer = time.CreateTimer(_ => Poll(), null, interval, interval);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }

        _timer.Dispose();
    }

    /// <summary>
    /// Lists the folder and raises what differs from the last listing that worked.  A listing that fails raises one
    /// <see cref="WatchFailure" />, and the next poll tries again against the same last listing.
    /// </summary>
    internal void Poll()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            Dictionary<string, Entry> current;
            try
            {
                current = List();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _request.Failed(new WatchFailure(_request.Folder, Overflow: false, exception.Message));
                return;
            }

            var last = _last;
            _last = current;
            if (last is null)
            {
                return;
            }

            Compare(last, current);
        }
    }

    private void Compare(Dictionary<string, Entry> last, Dictionary<string, Entry> current)
    {
        foreach (var (path, entry) in current.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!last.TryGetValue(path, out var before) || before.IsFolder != entry.IsFolder)
            {
                _request.Changed(new FileChange(FileChangeKind.Created, path, entry.IsFolder));
            }
            else if (!entry.IsFolder && before != entry)
            {
                _request.Changed(new FileChange(FileChangeKind.Changed, path, IsFolder: false));
            }
        }

        foreach (var (path, before) in last.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!current.TryGetValue(path, out var entry) || entry.IsFolder != before.IsFolder)
            {
                _request.Changed(new FileChange(FileChangeKind.Deleted, path, IsFolder: false));
            }
        }
    }

    private Dictionary<string, Entry> List()
    {
        var listing = new Dictionary<string, Entry>(StringComparer.Ordinal);
        Add(listing, new DirectoryInfo(_request.Folder), assetsOnly: false);
        return listing;
    }

    private void Add(Dictionary<string, Entry> listing, DirectoryInfo folder, bool assetsOnly)
    {
        foreach (var info in folder.EnumerateFileSystemInfos())
        {
            if (info is DirectoryInfo directory)
            {
                // A link to a folder is not followed, which a loop of links would never leave.
                if (!_request.Recursive || directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                var ignored = IgnoredFolders.Contains(directory.Name, StringComparer.Ordinal);
                if (assetsOnly || ignored)
                {
                    // Below obj, every folder but bin and .git may hold the assets file.
                    if (directory.Name == AssetsFolder || (assetsOnly && !ignored))
                    {
                        Add(listing, directory, assetsOnly: true);
                    }

                    continue;
                }

                listing[directory.FullName] = Entry.OfFolder;
                Add(listing, directory, assetsOnly: false);
            }
            else if (info is FileInfo file && (!assetsOnly || file.Name == AssetsFile))
            {
                try
                {
                    listing[file.FullName] = new Entry(IsFolder: false, file.Length, file.LastWriteTimeUtc);
                }
                catch (FileNotFoundException)
                {
                    // A file deleted since the listing began, or a link to nothing, is not in the folder.
                }
            }
        }
    }

    private readonly record struct Entry(bool IsFolder, long Length, DateTime LastWriteUtc)
    {
        public static Entry OfFolder { get; } = new(IsFolder: true, 0, default);
    }
}
