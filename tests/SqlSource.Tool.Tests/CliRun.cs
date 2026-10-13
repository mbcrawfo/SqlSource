using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
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

    // The describers of the run.  None, as in the released tool, unless a test adds one.
    public List<IQueryDescriber> Describers { get; } = [];

    public CliRun()
    {
        Exchange = new CapturingExchange(LiveExchange.Instance, Capture);
    }

    // What every describer of the run asked and was answered.  The engine is a name only: a run may have several.
    public DescribeCapture Capture { get; } = new("postgres");

    // Every database of the run calls through this one, which asks the describer and keeps the call.
    public CapturingExchange Exchange { get; }

    // The clock of the run.  The machine's, unless a test brings one it can move.
    public TimeProvider Time { get; set; } = TimeProvider.System;

    // Set to stand in for standard output, for a test of a writer that fails.
    public TextWriter? Out { get; set; }

    public Task<CliResult> RunAsync(params string[] args) => InvokeAsync(args, TestContext.Current.CancellationToken);

    // A run that the test ends itself.
    public Task<CliResult> RunAsync(CancellationToken cancellationToken, params string[] args) =>
        InvokeAsync(args, cancellationToken);

    public async Task<CliResult> RunCancelledAsync(params string[] args)
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        return await InvokeAsync(args, cancelled.Token);
    }

    public void Dispose() => Folder.Dispose();

    // The plan that "describe" builds for a command line, with what it wrote.  Nothing a run prints says what it
    // selected, so a test of that reads the plan.
    public async Task<(RunPlan? Plan, CliResult Result)> PlanAsync(params string[] args)
    {
        using var output = NewWriter();
        using var error = NewWriter();
        var host = CreateHost(output, error);
        var reporter = new Reporter(host.Error);
        var parsed = Cli.BuildCommands().Parse(args, Cli.Parser);

        var plan = await DescribeCommand.PlanAsync(parsed, host, reporter, TestContext.Current.CancellationToken);

        return (plan, new CliResult(reporter.Count > 0 ? 1 : 0, output.ToString(), error.ToString()));
    }

    private async Task<CliResult> InvokeAsync(string[] args, CancellationToken cancellationToken)
    {
        using var output = NewWriter();
        using var error = NewWriter();

        var exitCode = await Cli.RunAsync(args, CreateHost(output, error), cancellationToken);

        return new CliResult(exitCode, output.ToString(), error.ToString());
    }

    private static StringWriter NewWriter() => new(CultureInfo.InvariantCulture) { NewLine = "\n" };

    private ToolHost CreateHost(TextWriter output, TextWriter error) =>
        Hosts.Create(Folder.Path, Processes, Folder.CreateFolder("tmp"), environment: Environment) with
        {
            Out = Out ?? output,
            Error = error,
            Describers = new DescriberRegistry(Describers),
            Exchange = _ => Exchange,
            Time = Time,
        };
}
