using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using Xunit;

namespace SqlSource.Tests.TestHarness;

// The start of a SQL Server container and its one retry after the crash of TD-0072, and the save of each crash, over
// fakes: the crash does not happen on a developer's machine, so no container is started here.
public sealed class SqlServerStartTests : IDisposable
{
    private const string ImageName = DefaultImages.SqlServer;

    private const string CrashLog =
        $"SQL Server 2025 will run as non-root by default.\n{SqlServerStart.FatalError}\n"
        + "Reason: 0x00000002\n       Crash Id: 1234-abcd\n";

    private readonly List<string> _reports = [];

    private readonly string _crashes = Path.Combine(Path.GetTempPath(), $"sqlserver-start-tests-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_crashes))
        {
            Directory.Delete(_crashes, recursive: true);
        }
    }

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
        report.ShouldContain(SqlServerStart.FatalError);
        report.ShouldStartWith(
            $"The SQL Server container of {ImageName} stopped at its start with sqlservr's fatal error, so the tests "
                + "start a new one, once (TD-0072).  Crash Id: 1234-abcd.  Its console log and the archive of "
                + $"/var/opt/mssql/log, with the dump, are saved in {Path.Combine(_crashes, "1234-abcd")}."
        );
    }

    [Fact]
    public async Task StartAsync_FirstCrashes_SavesItsConsoleAndLogFolderBeforeItIsDisposed()
    {
        var first = Crashed(("out\n", CrashLog));
        first.Archive = [7, 8, 9];
        var builds = new Builds(first, new FakeStartingContainer { Image = ImageName });

        _ = await StartAsync(builds);

        first.ArchivedFolders.ShouldBe(["/var/opt/mssql/log"]);
        first.DisposalsWhenArchived.ShouldBe(0);
        var folder = Path.Combine(_crashes, "1234-abcd");
        var token = TestContext.Current.CancellationToken;
        (await File.ReadAllTextAsync(Path.Combine(folder, SqlServerStart.ConsoleFile), token)).ShouldBe(
            $"stdout:\nout\n\nstderr:\n{CrashLog}"
        );
        (await File.ReadAllBytesAsync(Path.Combine(folder, SqlServerStart.ArchiveFile), token)).ShouldBe([7, 8, 9]);
    }

    // A log with no crash id, or one that is not a guid's characters, names the folder by the container, so that
    // nothing in the log can name a folder outside the crashes' own.
    [Theory]
    [InlineData("Reason: 0x00000002\n")]
    [InlineData("Crash Id: ../../escaped\n")]
    public async Task StartAsync_NoCrashId_NamesTheFolderByTheContainer(string tail)
    {
        var first = Crashed(($"{SqlServerStart.FatalError}\n{tail}", ""));
        var builds = new Builds(first, new FakeStartingContainer { Image = ImageName });

        _ = await StartAsync(builds);

        Directory.GetDirectories(_crashes).ShouldBe([Path.Combine(_crashes, first.Id)]);
        File.Exists(Path.Combine(_crashes, first.Id, SqlServerStart.ArchiveFile)).ShouldBeTrue();
        _reports.ShouldHaveSingleItem().ShouldContain("Crash Id: none in its log.");
    }

    [Fact]
    public async Task StartAsync_SaveFails_ReportsItAndStartsAnother()
    {
        var first = Crashed((CrashLog, ""));
        first.ArchiveFailure = new IOException("No space left on device.");
        var second = new FakeStartingContainer { Image = ImageName };
        var builds = new Builds(first, second);

        var started = await StartAsync(builds);

        started.ShouldBeSameAs(second);
        first.Disposals.ShouldBe(1);
        var report = _reports.ShouldHaveSingleItem();
        report.ShouldContain(
            $"Its console log and /var/opt/mssql/log could not be saved in {Path.Combine(_crashes, "1234-abcd")}: "
                + "No space left on device."
        );
        report.ShouldContain(SqlServerStart.FatalError);
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
        var second = Crashed((CrashLog.Replace("1234-abcd", "5678-ef90", StringComparison.Ordinal), ""));
        var builds = new Builds(first, second);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => StartAsync(builds));

        thrown.ShouldBeSameAs(second.Failure);
        builds.Count.ShouldBe(2);
        first.Disposals.ShouldBe(1);
        second.Disposals.ShouldBe(1);
        second.DisposalsWhenArchived.ShouldBe(0);
        _reports.Count.ShouldBe(2);
        _reports[1]
            .ShouldStartWith(
                $"The second SQL Server container of {ImageName} stopped at its start with sqlservr's fatal "
                    + "error too, so the tests that need it fail (TD-0072).  Crash Id: 5678-ef90."
            );
        Directory
            .GetDirectories(_crashes)
            .Order(StringComparer.Ordinal)
            .ShouldBe([Path.Combine(_crashes, "1234-abcd"), Path.Combine(_crashes, "5678-ef90")]);
    }

    [Fact]
    public async Task StartAsync_SecondFailsWithoutCrash_ReportsTheFirstAloneAndThrows()
    {
        var first = Crashed((CrashLog, ""));
        var second = new FakeStartingContainer
        {
            Image = ImageName,
            Failure = new TimeoutException("The wait strategy timed out."),
        };
        var builds = new Builds(first, second);

        _ = await Should.ThrowAsync<TimeoutException>(() => StartAsync(builds));

        second.ArchivedFolders.ShouldBeEmpty();
        second.Disposals.ShouldBe(1);
        _ = _reports.ShouldHaveSingleItem();
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
        first.ArchivedFolders.ShouldBeEmpty();
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
        first.ArchivedFolders.ShouldBeEmpty();
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
            SqlServerStart.StartAsync(builds.Next, Report, _crashes, cancelled.Token)
        );

        builds.Count.ShouldBe(1);
        first.ArchivedFolders.ShouldBeEmpty();
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
    public void CrashFolderOf_Variable_IsItsFullPath() =>
        SqlServerStart.CrashFolderOf("crashes").ShouldBe(Path.GetFullPath("crashes"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CrashFolderOf_NoVariable_IsInTheTemporaryFolder(string? variable) =>
        SqlServerStart
            .CrashFolderOf(variable)
            .ShouldBe(Path.Combine(Path.GetTempPath(), "sqlsource-sqlserver-crashes"));

    [Fact]
    public async Task ReportAsync_NoSummaryPath_Completes() =>
        await Should.NotThrowAsync(() => SqlServerStart.ReportAsync("one", summaryPath: null));

    private Task<FakeStartingContainer> StartAsync(Builds builds) =>
        SqlServerStart.StartAsync(builds.Next, Report, _crashes, TestContext.Current.CancellationToken);

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
