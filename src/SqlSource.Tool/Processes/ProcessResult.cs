namespace SqlSource.Tool.Processes;

/// <summary>
/// What a program left behind.
/// </summary>
/// <param name="ExitCode">Its exit code, or <see cref="NotStarted" />.</param>
/// <param name="Output">Everything it wrote to its standard output, read as UTF-8.</param>
/// <param name="Error">Everything it wrote to its error output, read as UTF-8.</param>
internal sealed record ProcessResult(int ExitCode, string Output, string Error)
{
    /// <summary>
    /// The exit code of a program that could not be started.  No program gives it: an exit code is not negative
    /// on Unix, and on Windows a negative one is a crash.
    /// </summary>
    public const int NotStarted = -1;
}
