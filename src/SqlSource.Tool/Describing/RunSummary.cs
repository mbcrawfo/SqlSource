using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using SqlSource.Parsing;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Step 5 of a run: the lines that end it, so that a filter that matched nothing is seen.
/// </summary>
internal static class RunSummary
{
    /// <summary>
    /// A line for each selected database, in the plan's order, then one for each name of <c>--database</c> that
    /// is no selected database, in the order given.  The counts are of selected queries that need an entry.
    /// </summary>
    /// <remarks>
    /// A name that is no database of the plan may be a secret typed after <c>--database</c> by mistake, so it is
    /// printed only when it is close to a database of the plan, which tells a user that it was misspelt.  Any other
    /// is named by its position, as <c>--database at position 3</c>.
    /// </remarks>
    /// <param name="planned">The databases of the plan.</param>
    /// <param name="selected">The selected ones of them.</param>
    /// <param name="files">The files, with every query described, kept, failed or left out.</param>
    /// <param name="connections">The connections of the selected databases.</param>
    /// <param name="named">The values of <c>--database</c>, each name once ignoring case, the first kept.</param>
    /// <param name="mode">
    /// The run's mode: the counts of <c>--check</c> are <c>same</c>, <c>differ</c> and <c>failed</c>.
    /// </param>
    public static ImmutableArray<string> Lines(
        ImmutableArray<PlannedDatabase> planned,
        ImmutableArray<PlannedDatabase> selected,
        ImmutableArray<FileWork> files,
        Connections connections,
        ImmutableArray<UsageValue> named,
        RunMode mode
    )
    {
        var check = mode == RunMode.Check;
        var zeros = check ? "0 same, 0 differ, 0 failed" : "0 described, 0 skipped, 0 failed";
        var lines = ImmutableArray.CreateBuilder<string>();
        foreach (var database in selected)
        {
            if (check)
            {
                // The "no connection" form never shows: a check describes every query, so a database with no
                // connection has an error and a failed query.
                var (same, differ, notDescribed) = CountChecked(files, database.Name);
                lines.Add(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Label(database)}: {same} same, {differ} differ, {notDescribed} failed"
                    )
                );
                continue;
            }

            var (described, skipped, failed) = Count(files, database.Name);
            // The second form has no place for a failure: it is for a database with nothing to describe.
            lines.Add(
                connections.For(database.Name).Value is null && failed == 0
                    ? string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Label(database)}: no connection, {skipped} skipped"
                    )
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Label(database)}: {described} described, {skipped} skipped, {failed} failed"
                    )
            );
        }

        foreach (
            var name in named.Where(candidate =>
                !selected.Any(database => DatabaseName.Comparer.Equals(database.Name, candidate.Text))
            )
        )
        {
            // With its engine when the plan knows the database, though no query of it is selected.
            lines.Add(
                planned.FirstOrDefault(database => DatabaseName.Comparer.Equals(database.Name, name.Text)) is { } known
                    ? $"{Label(known)}: {zeros}"
                    : $"{Named(name, planned)}: {zeros}"
            );
        }

        return lines.ToImmutable();
    }

    /// <summary>
    /// The counts of each selected database as <c>run.end</c> holds them, in the plan's order: the numbers of the
    /// summary's lines, under the names of those lines.
    /// </summary>
    /// <param name="selected">The selected databases.</param>
    /// <param name="files">The files, with every query described, kept, failed or left out.</param>
    /// <param name="mode">The run's mode: <c>--check</c> counts in its own words.</param>
    public static LogValue Counts(
        ImmutableArray<PlannedDatabase> selected,
        ImmutableArray<FileWork> files,
        RunMode mode
    )
    {
        var check = mode == RunMode.Check;
        return LogValue.ListOf(selected.Select(database => Counts(database, files, check)));
    }

    private static LogValue Counts(PlannedDatabase database, ImmutableArray<FileWork> files, bool check)
    {
        var name = new LogMember("name", database.Name);
        var engine = new LogMember("engine", SqlDialectName.Canonical(database.Dialect));
        if (check)
        {
            var (same, differ, notDescribed) = CountChecked(files, database.Name);
            return LogValue.MapOf(name, engine, new("same", same), new("differ", differ), new("failed", notDescribed));
        }

        var (described, skipped, failed) = Count(files, database.Name);
        return LogValue.MapOf(
            name,
            engine,
            new("described", described),
            new("skipped", skipped),
            new("failed", failed)
        );
    }

    // What a name of --database that no database of the plan equals is called: itself when it is close to one, as a
    // misspelt name is, and its position otherwise.
    private static string Named(UsageValue name, ImmutableArray<PlannedDatabase> planned) =>
        planned.Any(database => NearName.IsNear(name.Text, database.Name, ignoreCase: true))
            ? name.Text
            : string.Create(CultureInfo.InvariantCulture, $"--database at position {name.Position}");

    private static string Label(PlannedDatabase database) =>
        $"{database.Name} ({SqlDialectName.Canonical(database.Dialect)})";

    private static (int Described, int Skipped, int Failed) Count(ImmutableArray<FileWork> files, string database)
    {
        var states = Selected(files, database).Select(static query => query.State).ToList();
        var described = states.Count(static state => state == QueryState.Described);
        var skipped = states.Count(static state => state == QueryState.Skipped);
        // Every other state of a selected query that needs an entry is a failure.
        var failed = states.Count - described - skipped;

        return (described, skipped, failed);
    }

    // Under --check a selected query that needs an entry was described, and is the same or differs, or it failed.
    private static (int Same, int Differ, int Failed) CountChecked(ImmutableArray<FileWork> files, string database)
    {
        var queries = Selected(files, database).ToList();
        var described = queries.Where(static query => query.State == QueryState.Described).ToList();
        var differ = described.Count(static query => query.Differs);

        return (described.Count - differ, differ, queries.Count - described.Count);
    }

    // A selected query that needs an entry was described, was skipped, or failed.
    private static IEnumerable<QueryWork> Selected(ImmutableArray<FileWork> files, string database) =>
        files
            .SelectMany(static file => file.Queries)
            .Where(query =>
                query.Planned is { NeedsEntry: true, IsSelected: true, Database: { } name }
                && DatabaseName.Comparer.Equals(name, database)
            );
}
