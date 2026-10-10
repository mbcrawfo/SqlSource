using System;
using System.IO;

namespace SqlSource.Tool;

/// <summary>
/// What the tool takes from outside itself.  Nothing in the tool reads <see cref="Console" />,
/// <see cref="Environment" /> or the current directory except through a host, so that a test can give a run its own.
/// </summary>
/// <param name="Out">Where everything but an error goes.</param>
/// <param name="Error">Where an error goes.</param>
/// <param name="WorkingDirectory">The full path that a relative path is resolved against.</param>
/// <param name="GetEnvironmentVariable">The value of an environment variable, or null when it is not set.</param>
internal sealed record ToolHost(
    TextWriter Out,
    TextWriter Error,
    string WorkingDirectory,
    Func<string, string?> GetEnvironmentVariable
)
{
    /// <summary>
    /// The host of a real run: the console, the current directory and the environment of the process.
    /// </summary>
    public static ToolHost Create() =>
        new(Console.Out, Console.Error, Directory.GetCurrentDirectory(), Environment.GetEnvironmentVariable);
}
