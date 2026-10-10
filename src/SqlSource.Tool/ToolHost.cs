using System;
using System.IO;
using SqlSource.Tool.Processes;

namespace SqlSource.Tool;

/// <summary>
/// What the tool takes from outside itself.  Nothing in the tool reads <see cref="Console" />,
/// <see cref="Environment" /> or the current directory except through a host, so that a test can give a run its own.
/// </summary>
/// <param name="Out">Where everything but an error goes.</param>
/// <param name="Error">Where an error goes.</param>
/// <param name="WorkingDirectory">The full path that a relative path is resolved against.</param>
/// <param name="GetEnvironmentVariable">The value of an environment variable, or null when it is not set.</param>
/// <param name="Processes">Runs every program the tool starts.</param>
/// <param name="TempDirectory">The full path of the directory the tool makes its temporary folders in.</param>
/// <param name="ProcessorCount">How many processors the machine has, which bounds what the tool runs at once.</param>
internal sealed record ToolHost(
    TextWriter Out,
    TextWriter Error,
    string WorkingDirectory,
    Func<string, string?> GetEnvironmentVariable,
    IProcessRunner Processes,
    string TempDirectory,
    int ProcessorCount
)
{
    /// <summary>
    /// The host of a real run: the console, the current directory, the environment and the processes of the
    /// machine.
    /// </summary>
    public static ToolHost Create() =>
        new(
            Console.Out,
            Console.Error,
            Directory.GetCurrentDirectory(),
            Environment.GetEnvironmentVariable,
            new ProcessRunner(),
            Path.GetTempPath(),
            Environment.ProcessorCount
        );
}
