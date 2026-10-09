namespace SqlSource.Tool.Tests;

// What a run left behind.  Lines end with "\n" on every operating system.
internal sealed record CliResult(int ExitCode, string Out, string Error);
