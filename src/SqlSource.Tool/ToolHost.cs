using System;
using System.IO;
using System.Runtime.InteropServices;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Processes;
using SqlSource.Tool.Watching;

namespace SqlSource.Tool;

/// <summary>
/// What the tool takes from outside itself.  Nothing in the tool reads <see cref="Console" />,
/// <see cref="Environment" />, <see cref="RuntimeInformation" /> or the current directory except
/// <see cref="Create" /> and <see cref="Cli.Run(string[])" />, the entry of a real run, so that a test can give a run
/// its own.
/// </summary>
/// <param name="Out">Where everything but an error goes.</param>
/// <param name="Error">Where an error goes.</param>
/// <param name="WorkingDirectory">The full path that a relative path is resolved against.</param>
/// <param name="GetEnvironmentVariable">The value of an environment variable, or null when it is not set.</param>
/// <param name="Processes">Runs every program the tool starts.</param>
/// <param name="TempDirectory">The full path of the directory the tool makes its temporary folders in.</param>
/// <param name="ProcessorCount">How many processors the machine has, which bounds what the tool runs at once.</param>
/// <param name="Describers">The describers of the run, by dialect.</param>
/// <param name="Exchange">Gives the exchange that the describer of a database, by its name, calls through.</param>
/// <param name="Time">The clock: the time of a log's events, and how long a step took.</param>
/// <param name="CreateFile">
/// Makes the file at a full path for writing, or empties the one that is there, and gives its stream.
/// </param>
/// <param name="ReadTextFile">
/// The text of the file at a full path, or null when there is no file there: none, or a folder.  It throws an
/// <see cref="IOException" /> or an <see cref="UnauthorizedAccessException" /> when the file cannot be read.
/// </param>
/// <param name="OpenReadFile">
/// The file at a full path opened for reading, or null when there is no file there: none, or a folder.  It throws an
/// <see cref="IOException" /> or an <see cref="UnauthorizedAccessException" /> when the file cannot be opened.
/// </param>
/// <param name="Runtime">The .NET that runs the tool, as the log's <c>run.start</c> names it.</param>
/// <param name="OperatingSystem">The system the tool runs on, as the log's <c>run.start</c> names it.</param>
/// <param name="Watch">
/// Starts watching a folder for changes to its files and tells the request of each; disposing what it gives stops
/// the watch.
/// </param>
internal sealed record ToolHost(
    TextWriter Out,
    TextWriter Error,
    string WorkingDirectory,
    Func<string, string?> GetEnvironmentVariable,
    IProcessRunner Processes,
    string TempDirectory,
    int ProcessorCount,
    DescriberRegistry Describers,
    Func<string, IDescribeExchange> Exchange,
    TimeProvider Time,
    Func<string, Stream> CreateFile,
    Func<string, string?> ReadTextFile,
    Func<string, Stream?> OpenReadFile,
    string Runtime,
    string OperatingSystem,
    Func<WatchRequest, IDisposable> Watch
)
{
    /// <summary>
    /// The host of a real run: the console, the current directory, the environment, the processes and the
    /// descriptions of .NET and of the system of the machine.  Its describers are PostgreSQL's and SQL Server's.
    /// </summary>
    public static ToolHost Create() =>
        new(
            Console.Out,
            Console.Error,
            Directory.GetCurrentDirectory(),
            Environment.GetEnvironmentVariable,
            new ProcessRunner(),
            Path.GetTempPath(),
            Environment.ProcessorCount,
            new DescriberRegistry([new PostgresDescriber(), new SqlServerDescriber()]),
            static _ => LiveExchange.Instance,
            TimeProvider.System,
            NewFile,
            ReadText,
            OpenRead,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            WatchFolder
        );

    /// <summary>
    /// The <see cref="CreateFile"/> of a real run: makes the file on disk, or empties the one that is there, opened
    /// for writing and shared for reading.
    /// </summary>
    internal static Stream NewFile(string path) =>
        new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);

    /// <summary>
    /// The <see cref="Watch"/> of a real run: the system's file events of the folder, or its listing on a timer when
    /// the request asks to poll.
    /// </summary>
    internal static IDisposable WatchFolder(WatchRequest request) =>
        request.Poll
            ? new PollingFolderWatcher(request, TimeProvider.System, PollingFolderWatcher.Interval)
            : new SystemFolderWatcher(request);

    /// <summary>
    /// The <see cref="ReadTextFile"/> of a real run: the text of the file, or null when the path is a folder or
    /// there is no file.
    /// </summary>
    internal static string? ReadText(string path)
    {
        if (Directory.Exists(path))
        {
            return null;
        }

        try
        {
            return File.ReadAllText(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// The <see cref="OpenReadFile"/> of a real run: the file opened for reading and shared for reading, or null when
    /// the path is a folder or there is no file.
    /// </summary>
    internal static Stream? OpenRead(string path)
    {
        if (Directory.Exists(path))
        {
            return null;
        }

        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }
}
