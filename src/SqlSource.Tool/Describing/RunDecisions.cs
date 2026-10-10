using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Steps 1 and 2 of a run: reads the sidecars that are there, and says of each query what the run does with it.
/// </summary>
internal static class RunDecisions
{
    /// <summary>
    /// Decides for every query of the plan.  A sidecar is read only for a file that is ready and has a selected
    /// query that needs an entry: a filter narrows what a run reads as well as what it changes.
    /// </summary>
    /// <param name="plan">The plan of the run.</param>
    /// <param name="force">Whether a selected query is described though its entry is current.</param>
    /// <param name="errors">Gains <c>SQLSRC221</c> for each sidecar of a newer tool.</param>
    public static ImmutableArray<FileWork> Decide(RunPlan plan, bool force, ICollection<ToolDiagnostic> errors)
    {
        var files = ImmutableArray.CreateBuilder<FileWork>(plan.Files.Count);
        foreach (var file in plan.Files)
        {
            var ready = file.State == PlannedFileState.Ready;
            var onDisk =
                ready && file.Queries.Any(static query => query.NeedsEntry && query.IsSelected)
                    ? SidecarOnDisk.Read(file.Path)
                    : null;
            if (onDisk?.NewerFormat is { } newer)
            {
                errors.Add(
                    ToolDiagnostic
                        .ForFile(
                            ToolDiagnostics.SidecarOfNewerTool,
                            onDisk.Path,
                            onDisk.Path,
                            newer.ToString(CultureInfo.InvariantCulture),
                            SidecarFormat.Version.ToString(CultureInfo.InvariantCulture)
                        )
                        .WithLines(new ContinuationLine("help", "update the SqlSource.Tool package"))
                );
            }

            files.Add(
                new FileWork(file, onDisk, [.. file.Queries.Select(query => Decide(query, ready, onDisk, force))])
            );
        }

        return files.MoveToImmutable();
    }

    /// <summary>
    /// The selected databases: those of the plan that have a selected query that needs an entry, in the plan's
    /// order.  A query that the plan could not describe counts, as it does in the summary.
    /// </summary>
    public static ImmutableArray<PlannedDatabase> SelectedDatabases(RunPlan plan)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in plan.Files)
        {
            foreach (var query in file.Queries)
            {
                if (query is { NeedsEntry: true, IsSelected: true, Database: { } database })
                {
                    _ = names.Add(database);
                }
            }
        }

        return [.. plan.Databases.Where(database => names.Contains(database.Name))];
    }

    // The rows of the spec's table, in its order.
    private static QueryWork Decide(PlannedQuery query, bool ready, SidecarOnDisk? onDisk, bool force)
    {
        if (!query.NeedsEntry)
        {
            return new QueryWork(query, QueryState.NotNeeded);
        }

        // A file that is not ready has the plan's error, and a sidecar of a newer tool has SQLSRC221.
        if (!ready || query.Problems != QueryProblems.None || (query.IsSelected && onDisk?.NewerFormat is not null))
        {
            return new QueryWork(query, QueryState.Failed);
        }

        if (query.IsSelected && force)
        {
            return new QueryWork(query, QueryState.ToDescribe);
        }

        if (
            query is { Hash: { } hash, Database: { } database }
            && onDisk?.Usable?.Find(query.Query.Name) is { } entry
            && entry.IsCurrentFor(hash, database)
        )
        {
            return new QueryWork(query, QueryState.Skipped, entry);
        }

        return new QueryWork(query, query.IsSelected ? QueryState.ToDescribe : QueryState.LeftOut);
    }
}
