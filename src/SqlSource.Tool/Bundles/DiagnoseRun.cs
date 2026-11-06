using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Diagnostics;
using SqlSource.Generation.Paths;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.DotEnv;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// The runs of <c>diagnose</c>: one query of a project described again and written as a bundle, and a raw bundle
/// renamed with no database.
/// </summary>
/// <remarks>
/// <para>
/// A run of <c>--query</c> plans the unit as <c>describe</c> does, with the query's file as the one filter, and
/// describes the one query whatever its hash, through <see cref="DatabaseRuns" /> with a <see cref="RunCapture" />.  It
/// writes no sidecar and deletes none: nothing of it reaches <see cref="SidecarStore" />.  An error of the plan at
/// another <c>.sql</c> file is logged and not written; any other error written before the query is described stops
/// the run, as one that may change the query.  A session that does not open makes no bundle, and its error is
/// <c>describe</c>'s.  A query that the describer refuses makes a bundle with its
/// failure, which the run says on standard output: the error is logged and not printed.
/// </para>
/// <para>
/// The bundle is renamed by <see cref="BundleRenamer" />, which proves it by a replay, and written at <c>--out</c>
/// with its mapping beside it; or, under <c>--no-obfuscate</c>, written raw with no mapping.  A refusal of the
/// renaming writes nothing.  A run of <c>--scrub</c> reads a raw bundle as <c>replay</c> does, reports what
/// <c>replay</c> would, and renames and writes it the same way.  An <c>--out</c> that is a file of the run, or that
/// cannot be written, is <c>SQLSRC218</c>, and so is a mapping that cannot be written after its zip was.
/// </para>
/// </remarks>
internal static class DiagnoseRun
{
    private const string Prefix = "sqlsource: ";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Describes the query again and writes its bundle, renamed or raw.</summary>
    /// <param name="usage">The command line, which gives the unit, the projects and the connections.</param>
    /// <param name="query">The file and the name of the query.</param>
    /// <param name="output">The value of <c>--out</c>, which may be printed.</param>
    /// <param name="raw">Whether the bundle is written raw, under <c>--no-obfuscate</c>.</param>
    /// <param name="host">What the run takes from outside.</param>
    /// <param name="reporter">Where the errors go, with the run's trace.</param>
    /// <param name="cancellationToken">Ends the run.</param>
    public static async Task QueryAsync(
        Usage usage,
        DiagnoseCommand.QueryName query,
        UsageValue output,
        bool raw,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        var outPath = Path.GetFullPath(output.Text, host.WorkingDirectory);
        if (RefusedOutOf(outPath) is { } refused)
        {
            reporter.Report(refused);
            return;
        }

        if (await PlanAsync(usage, query, host, reporter, cancellationToken) is not { } planned)
        {
            return;
        }

        if (await DescribeAsync(planned, usage, host, reporter, cancellationToken) is not { } described)
        {
            return;
        }

        if (described.Failure is { } failure)
        {
            await host.Out.WriteLineAsync(Prefix + "the query was refused: " + failure.Descriptor.Id);
        }

        var file = planned.File;
        var text =
            FileText.Read(file.Path)?.Text.ToString()
            ?? throw new InvalidOperationException($"The file '{file.Path}' could not be read again.");
        var bundle = DiagnoseBundles.Raw(
            planned.Query,
            text,
            file.Dialect,
            described.Capture,
            described.Entry,
            described.Failure,
            host
        );
        if (raw)
        {
            if (Write(outPath, stream => BundleWriter.Write(bundle, stream), host, reporter))
            {
                await host.Out.WriteLineAsync(Prefix + $"raw bundle {UsageValue.Quoted(outPath)} written");
                await host.Out.WriteLineAsync(
                    Prefix
                        + "this bundle holds your own names and SQL, and must not be posted: open an issue that says a "
                        + "raw bundle is ready, without attaching it, and a maintainer will say how to send it"
                );
            }

            return;
        }

        // The describer the query was described with, which the proof replays the renamed bundle with.
        var describer = BundleDescribers.For(
            file.Dialect,
            host.Describers,
            bundle.ExplainSettings,
            described.Capture.Capture.Engine,
            outPath
        );
        if (!describer.Succeeded)
        {
            reporter.Report(describer.Failure);
            return;
        }

        await RenameAndWriteAsync(bundle, describer.Value, file.Dialect, outPath, host, reporter, cancellationToken);
    }

    /// <summary>
    /// Renames a raw bundle and writes it with its mapping, with no database and no project.
    /// </summary>
    /// <param name="scrub">The value of <c>--scrub</c>: the bundle, a zip or a folder.</param>
    /// <param name="output">The value of <c>--out</c>, which may be printed.</param>
    /// <param name="host">What the run takes from outside.</param>
    /// <param name="reporter">Where the errors go, with the run's trace.</param>
    /// <param name="cancellationToken">Ends the run.</param>
    public static async Task ScrubAsync(
        UsageValue scrub,
        UsageValue output,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        var outPath = Path.GetFullPath(output.Text, host.WorkingDirectory);
        if (RefusedOutOf(outPath) is { } refused)
        {
            reporter.Report(refused);
            return;
        }

        // An error at the bundle is replay's, named as replay names it.
        var path = Path.GetFullPath(scrub.Text, host.WorkingDirectory);
        void Report(ToolDiagnostic error) => reporter.Report(ReplayCommand.AsNamed(error, scrub, path));

        var read = BundleReader.Read(path, host.OpenReadFile, host.ReadTextFile);
        if (!read.Succeeded)
        {
            Report(read.Failure);
            return;
        }

        var bundle = read.Value;
        var capture = BundleReplay.CaptureOf(bundle, path);
        if (!capture.Succeeded)
        {
            Report(capture.Failure);
            return;
        }

        var engine = BundleReplay.EngineOf(bundle);
        var planned = BundleReplay.Plan(bundle, engine, path, cancellationToken);
        if (!planned.Succeeded)
        {
            Report(planned.Failure);
            return;
        }

        var dialect = planned.Value.Dialect;
        var describer = BundleDescribers.For(dialect, host.Describers, bundle.ExplainSettings, engine, path);
        if (!describer.Succeeded)
        {
            Report(describer.Failure);
            return;
        }

        var expected = BundleReplay.ExpectedOf(bundle, path);
        if (!expected.Succeeded)
        {
            Report(expected.Failure);
            return;
        }

        if (bundle.Info is not { Renamed: false })
        {
            Report(BundleErrors.NotRaw(path, isRenamed: bundle.Info is not null));
            return;
        }

        await RenameAndWriteAsync(bundle, describer.Value, dialect, outPath, host, reporter, cancellationToken);
    }

    // The plan with the query's file as its one filter, and the query of the file it names, or null when the plan or
    // the file has an error, which is reported.  An error of the plan at another .sql file is logged and not written.
    private static async Task<PlannedTarget?> PlanAsync(
        Usage usage,
        DiagnoseCommand.QueryName query,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        if (DescribeCommand.FindUnit(usage, host, reporter) is not { } unit)
        {
            return null;
        }

        var manifests = (
            await DescribeCommand.FindManifestsAsync(unit, usage, host, reporter, cancellationToken)
        ).Manifests;
        var filters = RunFilters.Create([query.File], [], host.WorkingDirectory);
        if (
            await DescribeCommand.PlanFromAsync(
                unit,
                manifests,
                filters,
                isOnline: false,
                elsewhereIsLogged: true,
                host,
                reporter,
                cancellationToken
            )
            is not { } plan
        )
        {
            return null;
        }

        // A file that no type claims is the plan's SQLSRC212, and one that is not ready has the plan's error.
        var filter = filters.Files.Single();
        var fileIndex = IndexOf(
            plan.Files,
            file => SqlPath.Comparer.Equals(file.NormalizedPath, filter.NormalizedPath)
        );
        if (filter.NormalizedPath is null || fileIndex < 0 || plan.Files[fileIndex].State != PlannedFileState.Ready)
        {
            return null;
        }

        var found = plan.Files[fileIndex];
        var queryIndex = IndexOf(
            found.Queries,
            planned => string.Equals(planned.Query.Name, query.Query, StringComparison.Ordinal)
        );
        if (queryIndex < 0)
        {
            reporter.Report(NotInFile(query, found, "the file has no query of that name"));
            return null;
        }

        var target = found.Queries[queryIndex];
        if (!target.NeedsEntry)
        {
            reporter.Report(NotInFile(query, found, "the query's output is sql, which needs no description"));
            return null;
        }

        // A query that cannot be described has the plan's error.
        return target.Problems == QueryProblems.None ? new PlannedTarget(unit, plan, fileIndex, queryIndex) : null;
    }

    // Describes the one query, every other left out, through an exchange that records the session.  Null when the
    // query was not described or refused: an error of the run, which is reported.
    private static async Task<DescribedTarget?> DescribeAsync(
        PlannedTarget target,
        Usage usage,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        // Whatever its hash, as --force describes it.  A sidecar of a newer tool is SQLSRC221 here too.
        var errors = new List<ToolDiagnostic>();
        var files = RunDecisions.Decide(target.Plan, RunMode.Force, errors);
        foreach (var error in errors)
        {
            reporter.Report(error);
        }

        var work = files[target.FileIndex].Queries[target.QueryIndex];
        foreach (var other in files.SelectMany(static file => file.Queries))
        {
            if (!ReferenceEquals(other, work) && other.State == QueryState.ToDescribe)
            {
                other.State = QueryState.LeftOut;
            }
        }

        if (work.State != QueryState.ToDescribe)
        {
            return null;
        }

        var database = target.Plan.Databases.Single(planned =>
            DatabaseName.Comparer.Equals(planned.Name, target.Query.Database)
        );
        ImmutableArray<PlannedDatabase> selected = [database];
        var connections = DescribeRun.ConnectionsOf(
            target.Plan,
            selected,
            selected,
            DescribeCommand.ConnectionsOf(usage),
            DescribeCommand.UnitFolderOf(target.Unit),
            DotEnvSearch.FileOf(option: null, DescribeCommand.UnitFolderOf(target.Unit), host.WorkingDirectory),
            host,
            reporter
        );
        // An error written by now, of the query's file, a settings file, a project, a C# file or the .env file, may
        // change the query, so no bundle is made of it: the exit code is 0 exactly when a bundle is written.  An error
        // of the plan at another .sql file was not written.
        if (reporter.Count > reporter.UnwrittenCount)
        {
            return null;
        }

        var capture = new RunCapture(SqlDialectName.Canonical(database.Dialect));
        await DatabaseRuns.RunAsync(selected, files, connections, host, reporter, cancellationToken, capture: capture);

        // A session that did not open, or a query that failed before the describer was asked, was reported.
        if (capture.Server is null || capture.Text is null)
        {
            return null;
        }

        return work switch
        {
            { State: QueryState.Described, Entry: { } entry } => new DescribedTarget(capture, entry, null),
            { State: QueryState.Failed, Failure: { } failure } => new DescribedTarget(capture, null, failure),
            _ => null,
        };
    }

    // Renames the raw bundle and writes it, then its mapping beside it.  A refusal writes nothing.
    private static async Task RenameAndWriteAsync(
        Bundle raw,
        IQueryDescriber describer,
        SqlDialect dialect,
        string outPath,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        var renamed = await BundleRenamer.RenameAsync(
            raw,
            EngineRenamings.For(SqlDescribableDialects.EngineOf(dialect)),
            describer,
            RenamingKey.Random(),
            cancellationToken
        );
        if (!renamed.Succeeded)
        {
            reporter.Report(BundleErrors.CannotBeMadeSafe(outPath, renamed.Failure));
            return;
        }

        var (bundle, mapping) = renamed.Value;
        if (!Write(outPath, stream => BundleWriter.Write(bundle, stream), host, reporter))
        {
            return;
        }

        await host.Out.WriteLineAsync(Prefix + $"bundle {UsageValue.Quoted(outPath)} written");
        var mappingPath = outPath + BundleFiles.MappingSuffix;
        var text = Utf8.GetBytes(MappingFile.Write(mapping));
        if (!Write(mappingPath, stream => stream.Write(text, 0, text.Length), host, reporter))
        {
            return;
        }

        await host.Out.WriteLineAsync(Prefix + $"mapping {UsageValue.Quoted(mappingPath)} written");
        await host.Out.WriteLineAsync(
            Prefix
                + "the mapping holds your own names: keep it private, outside your project's folder, since a build "
                + "may copy a .json file into its output"
        );
    }

    // The SQLSRC218 of an --out that names a file of the run, which is refused before anything is described, or null.
    private static ToolDiagnostic? RefusedOutOf(string outPath) =>
        RunLogFile.IsAFileOfTheRun(outPath)
            ? ToolDiagnostic.NotChanged(
                outPath,
                "written",
                "a bundle is not written over a .sql file, a sidecar, a project or a solution"
            )
            : null;

    // Makes the file, or empties the one that is there, and writes it.  False when the system would not, which is
    // SQLSRC218 at the file.
    private static bool Write(string path, Action<Stream> write, ToolHost host, Reporter reporter)
    {
        try
        {
            using var stream = host.CreateFile(path);
            write(stream);
            return true;
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            reporter.Report(ToolDiagnostic.NotChanged(path, "written", exception.Message));
            return false;
        }
    }

    // SQLSRC292 at the file, which the message names in full, or by its position when its token holds "=" or ";".
    private static ToolDiagnostic NotInFile(DiagnoseCommand.QueryName query, PlannedFile file, string reason) =>
        query.File.MayBePrintedAsPath
            ? ToolDiagnostic.AtFile(ToolDiagnostics.QueryNotInFile, file.Path, query.Query, file.Path, reason)
            : ToolDiagnostic.Create(
                ToolDiagnostics.QueryNotInFile,
                query.Query,
                query.File.NamedAsPath(file.Path),
                reason
            );

    private static int IndexOf<T>(EquatableArray<T> items, Func<T, bool> matches)
        where T : IEquatable<T>
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (matches(items[index]))
            {
                return index;
            }
        }

        return -1;
    }

    // The query of the plan that the run describes: in the plan's files and in that file's queries, by index, as the
    // run's decisions hold them.
    private sealed record PlannedTarget(RunUnit Unit, RunPlan Plan, int FileIndex, int QueryIndex)
    {
        public PlannedFile File => Plan.Files[FileIndex];

        public PlannedQuery Query => File.Queries[QueryIndex];
    }

    // What describing the query gave: the session's capture, and the entry or the describer's refusal.
    private sealed record DescribedTarget(RunCapture Capture, SidecarEntry? Entry, DescribeFailure? Failure);
}
