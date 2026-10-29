using System;
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

    public (string Stdout, string Stderr) Logs { get; init; } = ("", "");

    public int Starts { get; private set; }

    public int LogReads { get; private set; }

    public int Disposals { get; private set; }

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

    public ValueTask DisposeAsync()
    {
        Disposals++;
        return ValueTask.CompletedTask;
    }
}
