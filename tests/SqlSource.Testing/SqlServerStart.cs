using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace SqlSource.Testing;

// Starts a SQL Server container of the tests, and a new one, once, when the first stopped before it was ready with
// sqlservr's fatal error: the crash of TD-0072, which comes at random on CI's runners and has no known cause.
internal static class SqlServerStart
{
    // The line that sqlservr writes to its log when it crashes.
    public const string FatalError = "This program has encountered a fatal error and cannot continue running";

    // How much of the log, from the fatal error on, a report carries: enough for its reason and its crash id.
    private const int LogExcerptLength = 4000;

    // Builds a container and starts it, or, after the crash, disposes it and builds and starts another.  A container
    // that fails is disposed before the exception leaves, since xunit does not dispose a fixture whose start threw.
    public static async Task<TContainer> StartAsync<TContainer>(Func<TContainer> build, CancellationToken token)
        where TContainer : IContainer
    {
        var started = await StartAsync(
            () => new Testcontainer<TContainer>(build()),
            message => ReportAsync(message, Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")),
            token
        );
        return started.Container;
    }

    internal static async Task<TStart> StartAsync<TStart>(
        Func<TStart> build,
        Func<string, Task> report,
        CancellationToken token
    )
        where TStart : IStartingContainer
    {
        var first = build();
        string? crash;
        try
        {
            await first.StartAsync(token);
            return first;
        }
        catch (Exception exception)
        {
            try
            {
                crash = token.IsCancellationRequested ? null : await CrashOfAsync(first, exception);
            }
            finally
            {
                await first.DisposeAsync();
            }

            if (crash is null)
            {
                throw;
            }
        }

        await report(
            $"The SQL Server container of {first.Image} stopped at its start with sqlservr's fatal error, so the tests "
                + $"start a new one, once (TD-0072).  Its log from the error on:\n{crash}"
        );

        var second = build();
        try
        {
            await second.StartAsync(token);
            return second;
        }
        catch
        {
            await second.DisposeAsync();
            throw;
        }
    }

    // Says that a container was started again where a reader of the run sees it.  dotnet test, as CI runs it, shows
    // neither xunit's diagnostic messages nor what a fixture writes to the console, so on GitHub the message is also
    // appended to the job's summary, the file that GITHUB_STEP_SUMMARY names, as a warning.
    internal static async Task ReportAsync(string message, string? summaryPath)
    {
        TestContext.Current.SendDiagnosticMessage(message);
        if (summaryPath is { Length: > 0 })
        {
            var quoted = string.Concat(message.Split('\n').Select(line => $"> {line}\n"));
            await File.AppendAllTextAsync(summaryPath, $"> [!WARNING]\n{quoted}\n");
        }
    }

    // The log from the fatal error on when the container stopped with it before it was ready, or null.  The log is
    // read only when the container has exited: Testcontainers says so by its exception, which it throws when its wait
    // finds the container gone even if the state it read just before still says running, or by that state.
    private static async Task<string?> CrashOfAsync(IStartingContainer container, Exception failure)
    {
        if (failure is not ContainerNotRunningException && container.State != TestcontainersStates.Exited)
        {
            return null;
        }

        var (stdout, stderr) = await container.GetLogsAsync();
        var log = stdout.Contains(FatalError, StringComparison.Ordinal) ? stdout : stderr;
        var at = log.IndexOf(FatalError, StringComparison.Ordinal);
        return at < 0 ? null : log[at..Math.Min(log.Length, at + LogExcerptLength)].TrimEnd();
    }
}

// What SqlServerStart needs of a container: Testcontainers' IContainer has some forty members, and the tests fake
// these alone.
internal interface IStartingContainer : IAsyncDisposable
{
    string Image { get; }

    TestcontainersStates State { get; }

    Task StartAsync(CancellationToken token);

    Task<(string Stdout, string Stderr)> GetLogsAsync();
}

// IStartingContainer over a container of Testcontainers.  A struct, since the container it holds is the caller's to
// dispose and the adapter has nothing to dispose of its own, which CA2000 cannot know of a class.
internal readonly struct Testcontainer<TContainer>(TContainer container) : IStartingContainer
    where TContainer : IContainer
{
    public TContainer Container => container;

    public string Image => container.Image.FullName;

    public TestcontainersStates State => container.State;

    public Task StartAsync(CancellationToken token) => container.StartAsync(token);

    public Task<(string Stdout, string Stderr)> GetLogsAsync() => container.GetLogsAsync(timestampsEnabled: false);

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
