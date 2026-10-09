using System;

namespace SqlSource.Tool;

/// <summary>
/// The <c>sqlsource</c> command.
/// </summary>
public static class Cli
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="args">The command line, without the name of the program.</param>
    /// <returns>The exit code of the process.</returns>
    public static int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return 0;
    }
}
