using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using SqlSource.DatabaseTests.TestHarness;

namespace SqlSource.Tests.TestHarness;

// The one fake of IStartingContainer, for the tests of EngineImage and SqlServerStart.  Its start
// throws the failure it is given, or succeeds when it has none, and it counts what is done to it.
internal sealed class FakeStartingContainer(Exception? failure = null) : IStartingContainer
{
    // Thrown by the start, which succeeds when it is null.
    public Exception? Failure { get; set; } = failure;

    // Thrown by the read of the log, which succeeds when it is null.
    public Exception? LogFailure { get; set; }

    // Thrown by the save of an archive, which writes Archive when it is null.
    public Exception? ArchiveFailure { get; set; }

    public (string Stdout, string Stderr) Logs { get; init; } = ("", "");

    // The bytes of the archive of any folder.
    public byte[] Archive { get; set; } = [1, 2, 3];

    // The folders whose archive was saved, in order.
    public List<string> ArchivedFolders { get; } = [];

    // How many times it had been disposed when its last archive was saved.
    public int DisposalsWhenArchived { get; private set; }

    public int Starts { get; private set; }

    public int LogReads { get; private set; }

    public int Disposals { get; private set; }

    public string Id { get; init; } = "fake-container";

    public string Image { get; init; } = "fake:1";

    public TestcontainersStates State { get; init; } = TestcontainersStates.Running;

    public Task StartAsync(CancellationToken token)
    {
        Starts++;
        return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
    }

    public Task<(string Stdout, string Stderr)> GetLogsAsync()
    {
        LogReads++;
        return LogFailure is null ? Task.FromResult(Logs) : Task.FromException<(string, string)>(LogFailure);
    }

    public async Task SaveArchiveAsync(string path, Stream destination, CancellationToken token)
    {
        ArchivedFolders.Add(path);
        DisposalsWhenArchived = Disposals;
        if (ArchiveFailure is not null)
        {
            throw ArchiveFailure;
        }

        await destination.WriteAsync(Archive, token);
    }

    public ValueTask DisposeAsync()
    {
        Disposals++;
        return ValueTask.CompletedTask;
    }
}
