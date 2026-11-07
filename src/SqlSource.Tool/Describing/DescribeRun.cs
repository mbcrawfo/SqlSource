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
    /// What starts each line of the summary under <c>--online</c>, which the build's <c>SqlSourceRunOnline</c> reads.
    /// </summary>
    internal const string SummaryPrefix = Cli.LinePrefix + "summary: ";

    /// <summary>
    /// Runs the steps in order, reports their errors and writes the summary.
    /// </summary>
    /// <param name="plan">The plan of the run.</param>
    /// <param name="options">What the command line says of the run, beside its plan.</param>
    /// <param name="host">The describers, the environment, the <c>.env</c> file and standard output.</param>
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
        var connections = ConnectionsOf(
            plan,
            selected,
            RunDecisions.DescribableDatabases(plan),
            options.Connections,
            options.UnitFolder,
            options.EnvFile,
            host,
            reporter
        );
        // Found before the databases are run, which changes the queries' states.
        var offline = OfflineDatabases(options.Online, files, selected, connections);
        await DatabaseRuns.RunAsync(
            selected,
            files,
            connections,
            host,
            reporter,
            cancellationToken,
            options.Online,
            offline: !offline.IsEmpty
        );

        // 4: a run that was cancelled changes no file.
        cancellationToken.ThrowIfCancellationRequested();
        // A sidecar is deleted only by a run on the whole, which knows what every file needs, and never after an
        // error: the plan drops a setting or a claim it could not read, and the file then looks as if it needs no
        // entry.  An offline run deletes nothing either, as a run that reported SQLSRC213 would not.
        var mayDelete = !options.HasFilter && reporter.Count == 0 && offline.IsEmpty;
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

                if (CheckedAs(files[index], check) is { } differs)
                {
                    RunEvents.SidecarChecked(trace, check.Outcome.SidecarPath, differs);
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
            options.Databases,
            options.Mode
        );
        if (!offline.IsEmpty)
        {
            // In place of the summary: the build reads this line to know that the tool ran and found no database.
            await host.Out.WriteLineAsync(
                $"{Cli.LinePrefix}offline: no connection for {string.Join(", ", offline.Select(OneLine.Of))}; "
                    + "the build uses the committed sidecars"
            );
            return;
        }

        // A project with nothing to describe has no summary line, and a build would take the silence for a tool that
        // did not start.
        if (lines.IsEmpty && options.Online != OnlineMode.Off)
        {
            await host.Out.WriteLineAsync(Cli.LinePrefix + "online: nothing to describe");
        }

        // The build's Exec reads a line that starts "error " or "warning " as one of the build, and a database may be
        // named so: under --online every line of the summary starts with a prefix that ends in a colon.
        var prefix = options.Online == OnlineMode.Off ? "" : SummaryPrefix;
        foreach (var line in lines)
        {
            await host.Out.WriteLineAsync(prefix + line);
        }
    }

    /// <summary>
    /// The connections of the selected databases, from the command line, the environment and then the run's
    /// <c>.env</c> file, with what is wrong with that file reported.  A file that cannot be read gives no
    /// value.  <c>diagnose</c> looks up the connection of its one database here.
    /// </summary>
    /// <param name="plan">The plan of the run, whose databases a value of <c>--connection</c> may name.</param>
    /// <param name="selected">The databases whose connection is looked up.</param>
    /// <param name="describable">
    /// The selected databases that can be described, which decide whether <c>SQLSOURCE_CONNECTION</c> is for one.
    /// </param>
    /// <param name="given">The values of <c>--connection</c>.</param>
    /// <param name="unitFolder">The full path of the folder of the run's unit.</param>
    /// <param name="envFile">The full path of the run's <c>.env</c> file, or null when it reads none.</param>
    /// <param name="host">The environment and the files.</param>
    /// <param name="reporter">Where the errors of the <c>.env</c> file go.</param>
    internal static Connections ConnectionsOf(
        RunPlan plan,
        ImmutableArray<PlannedDatabase> selected,
        ImmutableArray<PlannedDatabase> describable,
        ImmutableArray<ConnectionArgument> given,
        string unitFolder,
        string? envFile,
        ToolHost host,
        Reporter reporter
    )
    {
        var environment = ConnectionEnvironment.Load(unitFolder, envFile, host);
        foreach (var error in environment.Errors)
        {
            reporter.Report(error);
        }

        return Connections.Resolve(
            [.. selected.Select(static database => database.Name)],
            [.. describable.Select(static database => database.Name)],
            [.. plan.Databases.Select(static database => database.Name)],
            given,
            environment
        );
    }

    // The databases with a query to describe, in the plan's order, when the run is offline; none otherwise.  Under
    // "auto" the run is offline when none of them has a connection, SQLSOURCE_CONNECTION is not set for several
    // (SQLSRC215), and no variable that two of them share is set (SQLSRC214): the user gave a connection in each of
    // those, so the run reports the error, as a run by hand does.  A run where every entry is current has no such
    // database and is no offline run.
    private static ImmutableArray<string> OfflineDatabases(
        OnlineMode online,
        ImmutableArray<FileWork> files,
        ImmutableArray<PlannedDatabase> selected,
        Connections connections
    )
    {
        if (online != OnlineMode.Auto || connections.UnnamedIsSetAndNotUsed)
        {
            return [];
        }

        var wanting = selected
            .Where(database =>
                files
                    .SelectMany(static file => file.Queries)
                    .Any(query =>
                        query.State == QueryState.ToDescribe
                        && DatabaseName.Comparer.Equals(query.Planned.Database, database.Name)
                    )
            )
            .Select(static database => database.Name)
            .ToImmutableArray();
        return wanting.Any(name => IsGiven(connections.For(name), connections.Environment)) ? [] : wanting;
    }

    // Whether a database has a connection, or shares a variable that is set with another database.
    private static bool IsGiven(DatabaseConnection connection, ConnectionEnvironment environment) =>
        connection.Value is not null
        || (connection.SharesVariableWith is not null && environment.Find(connection.Variable) is not null);

    // The names of --database, for the steps that take no position.
    private static ImmutableArray<string> DatabaseNames(DescribeOptions options) =>
        [.. options.Databases.Select(static value => value.Text)];

    // What the log says of a sidecar that was checked: that it differs, when a difference was reported for the file;
    // that it is the same, only when every selected query of the file that needs an entry was described, so that each
    // was compared; and nothing otherwise, since a query that could not be checked is neither.
    private static bool? CheckedAs(FileWork file, FileCheck check)
    {
        if (!check.WasCompared)
        {
            return null;
        }

        if (check.Differences.Count > 0)
        {
            return true;
        }

        return file
            .Queries.Where(static query => query.Planned is { NeedsEntry: true, IsSelected: true })
            .All(static query => query.State == QueryState.Described)
            ? false
            : null;
    }
}
