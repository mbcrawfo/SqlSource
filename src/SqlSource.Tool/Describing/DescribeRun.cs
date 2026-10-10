using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// A run of <c>describe</c> after its plan: the steps of the spec, in order, each a unit of its own.
/// </summary>
/// <remarks>
/// The step that gives each file's outcome, <see cref="FileOutcomes" />, and the one that applies the outcomes to
/// the disk, <see cref="SidecarStore" />, stay apart: <c>--check</c> applies nothing.
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
        // --check describes every selected query, as --force does.
        var files = RunDecisions.Decide(plan, options.Force || options.Check, errors, options.Check);
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
            options.Connections,
            host.GetEnvironmentVariable
        );
        await DatabaseRuns.RunAsync(selected, files, connections, host, reporter, cancellationToken);

        // 4: a run that was cancelled changes no file.
        cancellationToken.ThrowIfCancellationRequested();
        // Nothing is deleted after an error: the plan drops a setting or a claim it could not read, and the file
        // then looks as if it needs no entry.
        var outcomes = FileOutcomes.Decide(files, options.HasFilter, reporter.Count > 0, SidecarOnDisk.Read);
        if (options.Check)
        {
            var checks = SidecarCheck.Compare(files, outcomes, options.Databases);
            for (var index = 0; index < checks.Length; index++)
            {
                var check = checks[index];
                foreach (var difference in check.Differences)
                {
                    reporter.Report(difference);
                }

                if (CheckedAs(files[index], check) is { } action)
                {
                    RunEvents.Sidecar(trace, check.SidecarPath, action);
                }
            }
        }
        else
        {
            SidecarStore.Apply(outcomes, reporter, cancellationToken);
        }

        trace.Databases = RunSummary.Counts(selected, files, options.Check);

        // 5.  The exit code is the reporter's: 1 when anything but SQLSRC219 was reported, else 2 when that was.
        var lines = RunSummary.Lines(
            [.. plan.Databases],
            selected,
            files,
            connections,
            options.Databases,
            options.Check
        );
        foreach (var line in lines)
        {
            await host.Out.WriteLineAsync(line);
        }
    }

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
