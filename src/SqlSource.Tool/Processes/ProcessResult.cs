namespace SqlSource.Tool.Processes;

/// <summary>
/// What a program left behind.
/// </summary>
/// <param name="ExitCode">Its exit code.  It is not zero for a program that was not started.</param>
/// <param name="Output">Everything it wrote to its standard output, read as UTF-8.</param>
/// <param name="Error">Everything it wrote to its error output, read as UTF-8.</param>
internal sealed record ProcessResult(int ExitCode, string Output, string Error)
{
    /// <summary>
    /// Whether the program was started.  No exit code can say it: on Windows a program may end with any number.
    /// </summary>
    public bool Started { get; init; } = true;

    /// <summary>
    /// What is left of a program that could not be started, with the reason as its error output.
    /// </summary>
    public static ProcessResult NotStarted(string reason) => new(-1, "", reason) { Started = false };
}
