using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Testing;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests.Watching;

// The one test that starts the real FileSystemWatcher.  macOS and Linux raise different kinds for the same write, and
// macOS may name the folder through /private, so it looks for the file by its name under the folder and accepts any
// kind but Deleted.
public sealed class SystemFolderWatcherTests
{
    [Fact]
    public async Task Watch_FileWrittenInAFolderBelow_RaisesItsPath()
    {
        using var folder = new TempFolder();
        _ = folder.CreateFolder("sub");
        var path = folder.PathOf("sub/Users.sql");
        var seen = new ConcurrentQueue<FileChange>();
        var request = new WatchRequest(
            folder.Path,
            Recursive: true,
            Poll: false,
            seen.Enqueue,
            failure => throw new InvalidOperationException(failure.Message)
        );

        using var watch = new SystemFolderWatcher(request);
        await File.WriteAllTextAsync(path, "SELECT 1", TestContext.Current.CancellationToken);

        await Eventually.TrueAsync(
            async () =>
            {
                if (seen.Any(IsTheFile))
                {
                    return true;
                }

                // The system may start delivering a moment after the watch starts, so a write that came first can be
                // missed.  Write again until one is seen.
                await File.WriteAllTextAsync(path, "SELECT 1", TestContext.Current.CancellationToken);
                return false;
            },
            "no event for the file",
            TimeSpan.FromSeconds(30)
        );

        seen.Any(IsTheFile).ShouldBeTrue();
    }

    private static bool IsTheFile(FileChange change) =>
        change.Kind != FileChangeKind.Deleted
        && string.Equals(Path.GetFileName(change.Path), "Users.sql", StringComparison.Ordinal)
        && string.Equals(Path.GetFileName(Path.GetDirectoryName(change.Path)), "sub", StringComparison.Ordinal);
}
