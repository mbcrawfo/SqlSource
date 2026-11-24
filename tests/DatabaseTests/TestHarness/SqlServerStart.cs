using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace SqlSource.DatabaseTests.TestHarness;

// Starts a SQL Server container of the tests, and a new one, once, when the first stopped before it was ready with
// sqlservr's fatal error: the crash of TD-0072, which comes at random on CI's runners and has no known cause.  Before
// it disposes a crashed container, the first or the second, it saves the container's console log and its folder of
// logs and dumps, for CI to keep as an artifact.
internal static partial class SqlServerStart
{
    // The line that sqlservr writes to its log when it crashes.
    public const string FatalError = "This program has encountered a fatal error and cannot continue running";

    // Names the folder a crash is saved under, one folder for each crash; CI sets it to the folder it uploads.
    public const string CrashesVariable = "SQLSOURCE_TESTS_SQLSERVER_CRASHES";

    // Where sqlservr writes its error log, and where handle-crash.sh leaves the dump and what it captured.
    public const string LogFolder = "/var/opt/mssql/log";

    // The files of a saved crash: the container's console, stdout then stderr, and the tar archive of LogFolder.
    public const string ConsoleFile = "console.log";
    public const string ArchiveFile = "mssql-log.tar";

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
            CrashFolder,
            token
        );
        return started.Container;
    }

    // The folder that crashes are saved under, read each time since a test of the variable sets it.
    internal static string CrashFolder => CrashFolderOf(Environment.GetEnvironmentVariable(CrashesVariable));

    // The folder that the variable names, or one in the system's temporary folder when it names none.
    internal static string CrashFolderOf(string? variable) =>
        variable is { Length: > 0 }
            ? Path.GetFullPath(variable)
            : Path.Combine(Path.GetTempPath(), "sqlsource-sqlserver-crashes");

    internal static async Task<TStart> StartAsync<TStart>(
        Func<TStart> build,
        Func<string, Task> report,
        string crashFolder,
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
                crash = token.IsCancellationRequested ? null : await CrashOfAsync(first, exception, crashFolder, token);
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
                + $"start a new one, once (TD-0072).  {crash}"
        );

        var second = build();
        try
        {
            await second.StartAsync(token);
            return second;
        }
        catch (Exception exception)
        {
            string? again;
            try
            {
                again = token.IsCancellationRequested
                    ? null
                    : await CrashOfAsync(second, exception, crashFolder, token);
            }
            finally
            {
                await second.DisposeAsync();
            }

            if (again is not null)
            {
                await report(
                    $"The second SQL Server container of {second.Image} stopped at its start with sqlservr's fatal "
                        + $"error too, so the tests that need it fail (TD-0072).  {again}"
                );
            }

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

    // What a report says of a container that stopped with the fatal error before it was ready, after its crash is
    // saved: its crash id, where it was saved and its log from the error on.  Null when it did not crash so.  The log
    // is read only when the container has exited: Testcontainers says so by its exception, which it throws when its
    // wait finds the container gone even if the state it read just before still says running, or by that state.
    private static async Task<string?> CrashOfAsync(
        IStartingContainer container,
        Exception failure,
        string crashFolder,
        CancellationToken token
    )
    {
        if (failure is not ContainerNotRunningException && container.State != TestcontainersStates.Exited)
        {
            return null;
        }

        var (stdout, stderr) = await container.GetLogsAsync();
        var log = stdout.Contains(FatalError, StringComparison.Ordinal) ? stdout : stderr;
        var at = log.IndexOf(FatalError, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        var crashId = CrashIdPattern().Match(log) is { Success: true } match ? match.Groups[1].Value : null;
        var folder = Path.Combine(crashFolder, crashId ?? container.Id);
        var saved = await SaveAsync(container, folder, stdout, stderr, token);
        var excerpt = log[at..Math.Min(log.Length, at + LogExcerptLength)].TrimEnd();
        return $"Crash Id: {crashId ?? "none in its log"}.  {saved}  Its log from the error on:\n{excerpt}";
    }

    // Saves the container's console and the archive of LogFolder in the folder, and says what it saved where, and why
    // it could not save the rest: a crash that cannot be saved is still started again.  The archive is written under a
    // name of its own and renamed once whole, so that ArchiveFile is never a part of one.
    private static async Task<string> SaveAsync(
        IStartingContainer container,
        string folder,
        string stdout,
        string stderr,
        CancellationToken token
    )
    {
        try
        {
            _ = Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(
                Path.Combine(folder, ConsoleFile),
                $"stdout:\n{stdout}\nstderr:\n{stderr}",
                token
            );
        }
        catch (Exception exception) when (IsSaveFailure(exception))
        {
            return $"Neither its console log nor the archive of {LogFolder} could be saved in {folder}: "
                + exception.Message;
        }

        var archive = Path.Combine(folder, ArchiveFile);
        var part = archive + ".part";
        try
        {
            await using (var file = File.Create(part))
            {
                await container.SaveArchiveAsync(LogFolder, file, token);
            }

            File.Move(part, archive, overwrite: true);
            return $"Its console log and the archive of {LogFolder}, with the dump, are saved in {folder}.";
        }
        catch (Exception exception) when (IsSaveFailure(exception))
        {
            try
            {
                File.Delete(part);
            }
            catch (Exception deletion) when (deletion is IOException or UnauthorizedAccessException)
            {
                // A part left behind is not named ArchiveFile, so no reader takes it for the archive.
            }

            return $"Its console log is saved in {folder}, but the archive of {LogFolder} could not be: "
                + exception.Message;
        }
    }

    // What a save of a crash can fail with: the file system's errors, and the Docker client's.
    private static bool IsSaveFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or HttpRequestException or DockerApiException;

    // sqlservr's "Crash Id: <guid>" line.  Only a guid's characters are taken, since the id names a folder.
    [GeneratedRegex(@"Crash Id:[ \t]*([0-9A-Fa-f-]{1,64})")]
    private static partial Regex CrashIdPattern();
}

// What SqlServerStart needs of a container: Testcontainers' IContainer has some forty members, and the tests fake
// these alone.
internal interface IStartingContainer : IAsyncDisposable
{
    string Id { get; }

    string Image { get; }

    TestcontainersStates State { get; }

    Task StartAsync(CancellationToken token);

    Task<(string Stdout, string Stderr)> GetLogsAsync();

    // Writes the tar archive of a folder of the container to the destination, as docker cp does, which works on a
    // container that has exited.
    Task SaveArchiveAsync(string path, Stream destination, CancellationToken token);
}

// IStartingContainer over a container of Testcontainers.  A struct, since the container it holds is the caller's to
// dispose and the adapter has nothing to dispose of its own, which CA2000 cannot know of a class.
internal readonly struct Testcontainer<TContainer>(TContainer container) : IStartingContainer
    where TContainer : IContainer
{
    public TContainer Container => container;

    public string Id => container.Id;

    public string Image => container.Image.FullName;

    public TestcontainersStates State => container.State;

    public Task StartAsync(CancellationToken token) => container.StartAsync(token);

    public Task<(string Stdout, string Stderr)> GetLogsAsync() => container.GetLogsAsync(timestampsEnabled: false);

    // Testcontainers reads one file of a container, never a folder, so the archive comes through a Docker client
    // built as Testcontainers builds its own.
    public async Task SaveArchiveAsync(string path, Stream destination, CancellationToken token)
    {
        using var client = TestcontainersSettings
            .OS.DockerEndpointAuthConfig.GetDockerClientBuilder(ResourceReaper.DefaultSessionId)
            .Build();
        var response = await client.Containers.GetArchiveFromContainerAsync(
            container.Id,
            new ContainerPathStatParameters { Path = path },
            statOnly: false,
            token
        );
        await using var archive = response.Stream ?? throw new IOException($"Docker gave no archive of {path}.");
        await archive.CopyToAsync(destination, token);
    }

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
