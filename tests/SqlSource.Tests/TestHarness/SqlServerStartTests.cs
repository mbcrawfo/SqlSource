using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using Xunit;

namespace SqlSource.Tests.TestHarness;

// The start of a SQL Server container and its one retry after the crash of TD-0072, over fakes: the crash does not
// happen on a developer's machine, so no container is started here.
public sealed class SqlServerStartTests
{
    private const string ImageName = DefaultImages.SqlServer;

    private const string CrashLog =
        $"SQL Server 2025 will run as non-root by default.\n{SqlServerStart.FatalError}\n"
        + "Reason: 0x00000002\nCrash Id: 1234\n";

    private readonly List<string> _reports = [];

    [Fact]
    public async Task StartAsync_FirstStarts_ReturnsItAndReportsNothing()
    {
        var first = new FakeStartingContainer { Image = ImageName };
        var builds = new Builds(first);

        var started = await StartAsync(builds);

        started.ShouldBeSameAs(first);
        builds.Count.ShouldBe(1);
        first.Starts.ShouldBe(1);
        first.Disposals.ShouldBe(0);
        _reports.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartAsync_FirstCrashes_DisposesItAndStartsAnother(bool inStderr)
    {
        var first = Crashed(inStderr ? ("", CrashLog) : (CrashLog, ""));
        var second = new FakeStartingContainer { Image = ImageName };
        var builds = new Builds(first, second);

        var started = await StartAsync(builds);

        started.ShouldBeSameAs(second);
        builds.Count.ShouldBe(2);
        first.Starts.ShouldBe(1);
        first.Disposals.ShouldBe(1);
        second.Starts.ShouldBe(1);
        second.Disposals.ShouldBe(0);
        var report = _reports.ShouldHaveSingleItem();
        report.ShouldContain(ImageName);
        report.ShouldContain(SqlServerStart.FatalError);
        report.ShouldContain("Crash Id: 1234");
    }

    // Testcontainers reads the state, then runs its wait, which finds the container gone: its exception says the
    // container stopped while the state it read still says running.
    [Fact]
    public async Task StartAsync_NotRunningWhileStateSaysRunning_DisposesItAndStartsAnother()
    {
        var first = new FakeStartingContainer
        {
            Image = ImageName,
            Failure = new ContainerNotRunningException("id", CrashLog, "", 1, new InvalidOperationException()),
            State = TestcontainersStates.Running,
            Logs = (CrashLog, ""),
        };
        var second = new FakeStartingContainer { Image = ImageName };
        var builds = new Builds(first, second);

        var started = await StartAsync(builds);

        started.ShouldBeSameAs(second);
        builds.Count.ShouldBe(2);
        first.Disposals.ShouldBe(1);
        _ = _reports.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task StartAsync_BothCrash_DisposesBothAndThrows()
    {
        var first = Crashed((CrashLog, ""));
        var second = Crashed((CrashLog, ""));
        var builds = new Builds(first, second);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => StartAsync(builds));

        thrown.ShouldBeSameAs(second.Failure);
        builds.Count.ShouldBe(2);
        first.Disposals.ShouldBe(1);
        second.Disposals.ShouldBe(1);
        _reports.Count.ShouldBe(1);
    }

    [Fact]
    public async Task StartAsync_ExitsWithoutFatalError_DisposesAndThrows()
    {
        var first = Crashed(("ERROR: the server could not bind its port\n", ""));
        var builds = new Builds(first);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => StartAsync(builds));

        thrown.ShouldBeSameAs(first.Failure);
        builds.Count.ShouldBe(1);
        first.Disposals.ShouldBe(1);
        _reports.ShouldBeEmpty();
    }

    [Fact]
    public async Task StartAsync_FailsWhileRunning_DisposesAndThrows()
    {
        var first = new FakeStartingContainer
        {
            Image = ImageName,
            Failure = new TimeoutException("The wait strategy timed out."),
            State = TestcontainersStates.Running,
            Logs = (CrashLog, ""),
        };
        var builds = new Builds(first);

        _ = await Should.ThrowAsync<TimeoutException>(() => StartAsync(builds));

        builds.Count.ShouldBe(1);
        first.LogReads.ShouldBe(0);
        first.Disposals.ShouldBe(1);
        _reports.ShouldBeEmpty();
    }

    [Fact]
    public async Task StartAsync_Cancelled_DisposesAndDoesNotRetry()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var first = Crashed((CrashLog, ""));
        first.Failure = new OperationCanceledException(cancelled.Token);
        var builds = new Builds(first);

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            SqlServerStart.StartAsync(builds.Next, Report, cancelled.Token)
        );

        builds.Count.ShouldBe(1);
        first.Disposals.ShouldBe(1);
        _reports.ShouldBeEmpty();
    }

    [Fact]
    public async Task StartAsync_LogReadFails_DisposesAndThrows()
    {
        var first = Crashed((CrashLog, ""));
        first.LogFailure = new InvalidOperationException("The log cannot be read.");
        var builds = new Builds(first);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => StartAsync(builds));

        thrown.ShouldBeSameAs(first.LogFailure);
        builds.Count.ShouldBe(1);
        first.Disposals.ShouldBe(1);
        _reports.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReportAsync_SummaryPath_AppendsAWarning()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await File.WriteAllTextAsync(path, "before\n", token);

            await SqlServerStart.ReportAsync("one\ntwo", path);

            (await File.ReadAllTextAsync(path, token)).ShouldBe("before\n> [!WARNING]\n> one\n> two\n\n");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReportAsync_NoSummaryPath_Completes() =>
        await Should.NotThrowAsync(() => SqlServerStart.ReportAsync("one", summaryPath: null));

    private Task<FakeStartingContainer> StartAsync(Builds builds) =>
        SqlServerStart.StartAsync(builds.Next, Report, TestContext.Current.CancellationToken);

    private Task Report(string message)
    {
        _reports.Add(message);
        return Task.CompletedTask;
    }

    // A container that stopped before it was ready, with that log.
    private static FakeStartingContainer Crashed((string Stdout, string Stderr) logs) =>
        new()
        {
            Image = ImageName,
            Failure = new InvalidOperationException("The container exited."),
            State = TestcontainersStates.Exited,
            Logs = logs,
        };

    // Hands out the containers in order, one for each build, and fails a build past the last.
    private sealed class Builds(params FakeStartingContainer[] containers)
    {
        public int Count { get; private set; }

        public FakeStartingContainer Next() =>
            Count < containers.Length
                ? containers[Count++]
                : throw new InvalidOperationException("More containers were built than the test expects.");
    }
}
