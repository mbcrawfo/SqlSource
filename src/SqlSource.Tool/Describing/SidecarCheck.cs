using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The last step of <c>describe --check</c>, in the place of <see cref="SidecarStore" />: compares what was
/// described with the sidecars on the disk.  It reads nothing, changes nothing and reports nothing: each difference
/// is an <c>SQLSRC219</c> in what it returns.
/// </summary>
/// <remarks>
/// It works from each selected query's described entry and not from a file's target: under a filter, a file with a
/// query that was left out has none.  What it compares is within what <c>describe</c> with the same filters would
/// change, but plain <c>describe</c> skips a query whose hash and database are as they were.  So the help line of a
/// difference names the command that mends it: <c>describe --force</c> for what the database reports, an update of
/// the tool for a sidecar of a higher format version, and plain <c>describe</c> for the rest.
/// </remarks>
internal static class SidecarCheck
{
    private static readonly ContinuationLine Describe = new(
        "help",
        "run 'dotnet sqlsource describe' and commit the sidecars"
    );

    // The tool skips a query whose SQL is as it was, so only --force describes it again.
    private static readonly ContinuationLine DescribeAgain = new(
        "help",
        "run 'dotnet sqlsource describe --force' and commit the sidecars"
    );

    // The same help as SQLSRC221, which a run that writes gives for the same sidecar.
    private static readonly ContinuationLine Update = new("help", "update the SqlSource.Tool package");

    // What differs, and the help that names the command which mends it: decided together.
    private sealed record Found(string Text, ContinuationLine Help);

    /// <summary>
    /// Compares each file, in the files' order, and sets <see cref="QueryWork.Differs" /> on each query that a
    /// difference was reported at, or at the whole of whose sidecar one was.
    /// </summary>
    /// <param name="files">The files, with every selected query that needs an entry described or failed.</param>
    /// <param name="outcomes">What <see cref="FileOutcomes" /> decided for the same files, in the same order.</param>
    /// <param name="databases">
    /// The names of <c>--database</c>.  An entry for a query that is gone is reported when its own database is one
    /// of them, or when there are none: a run for one database says nothing of another's entries.
    /// </param>
    public static ImmutableArray<FileCheck> Compare(
        ImmutableArray<FileWork> files,
        ImmutableArray<FileOutcome> outcomes,
        ImmutableArray<string> databases
    ) => [.. files.Select((file, index) => Compare(file, outcomes[index], databases))];

    private static FileCheck Compare(FileWork file, FileOutcome outcome, ImmutableArray<string> databases)
    {
        var sql = file.File.Path;

        // The one difference this step does not decide: a sidecar is deleted only where FileOutcomes says so.
        if (outcome.Action == FileAction.Delete)
        {
            return Of(outcome, [AtSidecar(outcome.SidecarPath, sql, "no query of the file needs an entry", Describe)]);
        }

        var selected = file
            .Queries.Where(static query => query.Planned is { NeedsEntry: true, IsSelected: true })
            .ToList();
        var described = selected.Where(static query => query.State == QueryState.Described).ToList();

        // A file none of whose queries could be described is compared with nothing: its errors stand.
        if (file.OnDisk is not { } onDisk || described.Count == 0)
        {
            return new FileCheck(
                file.File,
                outcome.SidecarPath,
                WasCompared: false,
                EquatableArray<ToolDiagnostic>.Empty
            );
        }

        if (onDisk.Sidecar is not { } sidecar)
        {
            // Once for the file: its queries are not reported one by one, and each that was described differs.
            foreach (var query in described)
            {
                query.Differs = true;
            }

            return Of(outcome, [WholeSidecar(onDisk, selected[0], sql)]);
        }

        var differences = new List<ToolDiagnostic>();
        foreach (var query in described)
        {
            if (DifferenceOf(query, sidecar) is { } found)
            {
                query.Differs = true;
                differences.Add(
                    ToolDiagnostic
                        .At(
                            ToolDiagnostics.SidecarOutOfDate,
                            query.Planned.Query.NameLocation,
                            query.Planned.Query.Name,
                            found.Text
                        )
                        .WithLines(found.Help)
                );
            }
        }

        differences.AddRange(EntriesWithoutAQuery(file, onDisk, sidecar, databases));
        return Of(outcome, differences);
    }

    // No file where the sidecar would be, at the first selected query; or a file that is no sidecar of this format,
    // at the file.
    private static ToolDiagnostic WholeSidecar(SidecarOnDisk onDisk, QueryWork first, string sql)
    {
        if (!onDisk.Exists)
        {
            return ToolDiagnostic
                .At(
                    ToolDiagnostics.SidecarOutOfDate,
                    first.Planned.Query.NameLocation,
                    sql,
                    "the sidecar does not exist"
                )
                .WithLines(Describe);
        }

        if (onDisk.FormatVersion is not { } version)
        {
            return AtSidecar(onDisk.Path, sql, "the sidecar cannot be read", Describe);
        }

        // A lower version is written again by describe; a higher one is a newer tool's, which describe leaves alone.
        return AtSidecar(
            onDisk.Path,
            sql,
            string.Create(
                CultureInfo.InvariantCulture,
                $"the sidecar has format {version}, and this tool writes {SidecarFormat.Version}"
            ),
            version > SidecarFormat.Version ? Update : Describe
        );
    }

    // What differs for a query that was described, or null.
    private static Found? DifferenceOf(QueryWork query, Sidecar sidecar)
    {
        if (sidecar.Find(query.Planned.Query.Name) is not { } committed)
        {
            return new Found("the sidecar has no entry", Describe);
        }

        var described =
            query.Entry ?? throw new InvalidOperationException($"'{query.Planned.Query.Name}' has no entry.");
        return SidecarComparer.FindDifference(committed, described) is { } difference ? FoundOf(difference) : null;
    }

    // The hash, the engine and the database are the project's: the database says nothing of them, and describe
    // mends them.  Two hashes of 64 characters tell nobody anything.  What the database says is skipped by describe
    // while the query's SQL is as it was, so it needs --force.
    private static Found FoundOf(SidecarDifference difference) =>
        difference.Path switch
        {
            SidecarKeys.Hash => new Found("the query has changed since it was described", Describe),
            SidecarKeys.Engine or SidecarKeys.Database => new Found(
                $"{difference.Path} is {difference.Committed}, and the project says {difference.Described}",
                Describe
            ),
            _ => new Found(
                $"{difference.Path} is {difference.Committed}, and the database says {difference.Described}",
                DescribeAgain
            ),
        };

    // An entry for a name that no query of the file has, or for a query that needs none: writing drops it.
    private static IEnumerable<ToolDiagnostic> EntriesWithoutAQuery(
        FileWork file,
        SidecarOnDisk onDisk,
        Sidecar sidecar,
        ImmutableArray<string> databases
    )
    {
        SourceText? text = null;
        foreach (var entry in sidecar.Queries)
        {
            var query = file.Queries.FirstOrDefault(candidate =>
                string.Equals(candidate.Planned.Query.Name, entry.Name, StringComparison.Ordinal)
            );
            if (query is { Planned.NeedsEntry: true })
            {
                continue;
            }

            if (
                !databases.IsEmpty
                && (
                    entry.Database is not { } database
                    || !databases.Contains(database, StringComparer.OrdinalIgnoreCase)
                )
            )
            {
                continue;
            }

            text ??= SourceText.From(onDisk.Text ?? "");
            yield return ToolDiagnostic
                .At(
                    ToolDiagnostics.SidecarOutOfDate,
                    onDisk.Path,
                    text.Lines.GetLinePosition(entry.NameSpan.Start),
                    entry.Name,
                    query is null ? "the query is not in the file" : "the query needs no entry"
                )
                .WithLines(Describe);
        }
    }

    private static ToolDiagnostic AtSidecar(string sidecarPath, string sql, string what, ContinuationLine help) =>
        ToolDiagnostic.ForFile(ToolDiagnostics.SidecarOutOfDate, sidecarPath, sql, what).WithLines(help);

    private static FileCheck Of(FileOutcome outcome, IEnumerable<ToolDiagnostic> differences) =>
        new(outcome.File, outcome.SidecarPath, WasCompared: true, new EquatableArray<ToolDiagnostic>([.. differences]));
}
