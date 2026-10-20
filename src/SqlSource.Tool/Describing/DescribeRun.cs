using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// A run of <c>describe</c> after its plan: the steps of "a describe run" in docs/architecture/tool.md, in order, each
/// a unit of its own.
/// </summary>
/// <remarks>
/// The step that gives each file's outcome, <see cref="FileOutcomes" />, and the one that applies the outcomes to
/// the disk, <see cref="SidecarStore" />, stay apart: <c>--check</c> applies nothing.
/// </remarks>
internal static class DescribeRun
{
    /// <summary>
    /// Runs the steps in order, reports their errors and writes the summary.
    /// </summary>
    /// <param name="plan">The plan of the run.</param>
    /// <param name="options">What the command line says of the run, beside its plan.</param>
    /// <param name="host">The describers, the environment and standard output.</param>
    /// <param name="reporter">Where the errors go, with the run's trace.</param>
    /// <param name="counted">
    /// Given the counts of each selected database, as <c>run.end</c> holds them, before the summary is written: a
    /// summary that cannot be written does not lose them.
    /// </param>
    /// <param name="cancellationToken">Ends the run.</param>
    public static async Task RunAsync(
        RunPlan plan,
        DescribeOptions options,
        ToolHost host,
        Reporter reporter,
        Action<LogValue> counted,
        CancellationToken cancellationToken
    )
    {
        // 1 and 2: what is there, and what each query needs.
        var errors = new List<ToolDiagnostic>();
        var files = RunDecisions.Decide(plan, options.Mode, errors);
        foreach (var error in errors)
        {
            reporter.Report(error);
        }

        // What the run decided without asking a database.  A query to describe gets its event when that ends.
        var trace = reporter.Trace;
        foreach (
            var query in files
                .SelectMany(static file => file.Queries)
                .Where(static query => query.Planned.NeedsEntry && query.State != QueryState.ToDescribe)
        )
        {
            RunEvents.Query(trace, query);
        }

        // 3: the connection of every selected database is looked up, since the summary says which have none.
        var selected = RunDecisions.SelectedDatabases(plan);
        var connections = Connections.Resolve(
            [.. selected.Select(static database => database.Name)],
            [.. RunDecisions.DescribableDatabases(plan).Select(static database => database.Name)],
            options.Connections,
            host.GetEnvironmentVariable
        );
        await DatabaseRuns.RunAsync(selected, files, connections, host, reporter, cancellationToken);

        // 4: a run that was cancelled changes no file.
        cancellationToken.ThrowIfCancellationRequested();
        // A sidecar is deleted only by a run on the whole, which knows what every file needs, and never after an
        // error: the plan drops a setting or a claim it could not read, and the file then looks as if it needs no
        // entry.
        var mayDelete = !options.HasFilter && reporter.Count == 0;
        var outcomes = FileOutcomes.Decide(files, mayDelete, SidecarOnDisk.Read);
        if (options.Mode == RunMode.Check)
        {
            var checks = SidecarCheck.Compare(files, outcomes, DatabaseNames(options));
            for (var index = 0; index < checks.Length; index++)
            {
                var check = checks[index];
                // A sidecar that describe would report and leave could not be checked: an error, and no difference.
                if (check.Outcome.Failure is { } failure)
                {
                    reporter.Report(failure);
                }

                foreach (var difference in check.Differences)
                {
                    reporter.Report(difference);
                }

                if (CheckedAs(files[index], check) is { } action)
                {
                    RunEvents.Sidecar(trace, check.Outcome.SidecarPath, action);
                }
            }
        }
        else
        {
            SidecarStore.Apply(outcomes, reporter, host.Time, onTheWhole: !options.HasFilter, cancellationToken);
        }

        counted(RunSummary.Counts(selected, files, options.Mode));

        // 5.  The summary.  The exit code is Cli.ExitCodeOf's, from the reporter's counts.
        var lines = RunSummary.Lines(
            [.. plan.Databases],
            selected,
            files,
            connections,
            DatabaseNames(options),
            options.Mode
        );
        foreach (var line in lines)
        {
            await host.Out.WriteLineAsync(line);
        }
    }

    // The names of --database, for the steps that take no position.
    private static ImmutableArray<string> DatabaseNames(DescribeOptions options) =>
        [.. options.Databases.Select(static value => value.Text)];

    // What the log says of a sidecar that was checked: "differs" when a difference was reported for the file; "same"
    // only when every selected query of the file that needs an entry was described, so that each was compared; and
    // nothing otherwise, since a query that could not be checked is neither.
    private static string? CheckedAs(FileWork file, FileCheck check)
    {
        if (!check.WasCompared)
        {
            return null;
        }

        if (check.Differences.Count > 0)
        {
            return "differs";
        }

        return file
            .Queries.Where(static query => query.Planned is { NeedsEntry: true, IsSelected: true })
            .All(static query => query.State == QueryState.Described)
            ? "same"
            : null;
    }
}
