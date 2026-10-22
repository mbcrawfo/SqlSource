using System;
using System.IO;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Processes;

namespace SqlSource.Tool;

/// <summary>
/// What the tool takes from outside itself.  Nothing in the tool reads <see cref="Console" />,
/// <see cref="Environment" /> or the current directory except <see cref="Create" /> and
/// <see cref="Cli.Run(string[])" />, the entry of a real run, so that a test can give a run its own.
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
    Func<string, Stream> CreateFile
)
{
    /// <summary>
    /// The host of a real run: the console, the current directory, the environment and the processes of the
    /// machine.  Its describers are PostgreSQL's and SQL Server's.
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
            NewFile
        );

    /// <summary>
    /// The <see cref="CreateFile"/> of a real run: makes the file on disk, or empties the one that is there, opened
    /// for writing and shared for reading.
    /// </summary>
    internal static Stream NewFile(string path) =>
        new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
}
