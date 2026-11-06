using System;
using System.Collections.Generic;
using System.IO;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Processes;
using SqlSource.Tool.Watching;

namespace SqlSource.Tool.Testing;

// A host for a test that calls a part of the tool and not the whole command: its output and its errors go nowhere,
// and a file it makes is made on disk, as the released tool makes it.  Its clock stands still, unless the test brings
// one.  Its descriptions of .NET and of the system are its own, so a test of the log can fix them.
internal static class Hosts
{
    public const string Runtime = ".NET of the test";

    public const string OperatingSystem = "the system of the test";

    // A test's host reads a text file only under the system's temporary folder, where a test makes its files, and finds
    // none elsewhere, so that no test reads a developer's own ".env".
    internal static string? ReadUnderTheTemporaryFolder(string path) =>
        IsUnderTheTemporaryFolder(path) ? ToolHost.ReadText(path) : null;

    private static bool IsUnderTheTemporaryFolder(string path)
    {
        var temporary = Path.GetFullPath(Path.GetTempPath());
        var relative = Path.GetRelativePath(temporary, Path.GetFullPath(path));
        return relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    // A test's host watches only under the system's temporary folder too, with the real watcher, and nothing elsewhere,
    // so that no test watches a developer's own folders.
    internal static IDisposable WatchUnderTheTemporaryFolder(WatchRequest request) =>
        IsUnderTheTemporaryFolder(request.Folder) ? ToolHost.WatchFolder(request) : NoWatch.Instance;

    private sealed class NoWatch : IDisposable
    {
        public static NoWatch Instance { get; } = new();

        public void Dispose() { }
    }

    public static ToolHost Create(
        string workingDirectory,
        IProcessRunner processes,
        string tempDirectory,
        int processorCount = 4,
        IReadOnlyDictionary<string, string>? environment = null,
        TimeProvider? time = null
    ) =>
        new(
            TextWriter.Null,
            TextWriter.Null,
            workingDirectory,
            name => environment?.GetValueOrDefault(name),
            processes,
            tempDirectory,
            processorCount,
            DescriberRegistry.Empty,
            static _ => LiveExchange.Instance,
            time ?? new TestClock(),
            ToolHost.NewFile,
            ReadUnderTheTemporaryFolder,
            ToolHost.OpenRead,
            Runtime,
            OperatingSystem,
            WatchUnderTheTemporaryFolder
        );

    // The environment of the test process, for a run of the real "dotnet".
    public static ToolHost Real(string workingDirectory, string tempDirectory, TimeProvider? time = null) =>
        new(
            TextWriter.Null,
            TextWriter.Null,
            workingDirectory,
            Environment.GetEnvironmentVariable,
            new ProcessRunner(),
            tempDirectory,
            Environment.ProcessorCount,
            DescriberRegistry.Empty,
            static _ => LiveExchange.Instance,
            time ?? new TestClock(),
            ToolHost.NewFile,
            ReadUnderTheTemporaryFolder,
            ToolHost.OpenRead,
            Runtime,
            OperatingSystem,
            WatchUnderTheTemporaryFolder
        );
}
