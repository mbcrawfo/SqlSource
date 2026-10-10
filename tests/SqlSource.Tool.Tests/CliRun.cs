using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SqlSource.Tool.Tests;

// Runs the whole command in process, as Cli.Run does, with a host of the test's own: writers that are strings, a
// working directory that is a temporary folder, and an environment that is a dictionary.
internal sealed class CliRun : IDisposable
{
    public TempFolder Folder { get; } = new();

    public Dictionary<string, string> Environment { get; } = [];

    // Answers every "dotnet msbuild" of the run.  A project it was told nothing about uses SqlSource.
    public FakeProcessRunner Processes { get; } = new();

    // Set to stand in for standard output, for a test of a writer that fails.
    public TextWriter? Out { get; init; }

    public Task<CliResult> RunAsync(params string[] args) => InvokeAsync(args, TestContext.Current.CancellationToken);

    public async Task<CliResult> RunCancelledAsync(params string[] args)
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        return await InvokeAsync(args, cancelled.Token);
    }

    public void Dispose() => Folder.Dispose();

    private async Task<CliResult> InvokeAsync(string[] args, CancellationToken cancellationToken)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        using var error = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        var host = new ToolHost(
            Out ?? output,
            error,
            Folder.Path,
            name => Environment.GetValueOrDefault(name),
            Processes,
            Folder.CreateFolder("tmp"),
            ProcessorCount: 4
        );

        var exitCode = await Cli.RunAsync(args, host, cancellationToken);

        return new CliResult(exitCode, output.ToString(), error.ToString());
    }
}
