using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// A run of <c>describe</c> after its plan: the steps of the spec, in order, each a unit of its own.
/// </summary>
/// <remarks>
/// The step that gives each file's outcome, <see cref="FileOutcomes" />, and the one that applies the outcomes to
/// the disk, <see cref="SidecarStore" />, stay apart: sub-phase 2.6 compares where this one writes.
/// </remarks>
internal static class DescribeRun
{
    public static async Task RunAsync(
        RunPlan plan,
        DescribeOptions options,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        // 1 and 2: what is there, and what each query needs.
        var errors = new List<ToolDiagnostic>();
        var files = RunDecisions.Decide(plan, options.Force, errors);
        foreach (var error in errors)
        {
            reporter.Report(error);
        }

        // 3: the connection of every selected database is looked up, since the summary says which have none.
        var selected = RunDecisions.SelectedDatabases(plan);
        var connections = Connections.Resolve(
            [.. selected.Select(static database => database.Name)],
            options.Connections,
            host.GetEnvironmentVariable
        );
        await DatabaseRuns.RunAsync(selected, files, connections, host, reporter, cancellationToken);

        // 4: a run that was cancelled changes no file.
        cancellationToken.ThrowIfCancellationRequested();
        var outcomes = FileOutcomes.Decide(files, options.HasFilter, File.Exists);
        SidecarStore.Apply(outcomes, reporter, cancellationToken);

        // 5.  The exit code is the reporter's: 1 when anything was reported.
        var lines = RunSummary.Lines([.. plan.Databases], selected, files, connections, options.Databases);
        foreach (var line in lines)
        {
            await host.Out.WriteLineAsync(line);
        }
    }
}
