using System;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Snapshot;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Step 4 of a run: gives each file its outcome, with its target.  It changes nothing on the disk.
/// </summary>
internal static class FileOutcomes
{
    /// <summary>
    /// Decides for each file, in the files' order.
    /// </summary>
    /// <param name="files">The files, with every query described, kept, failed or left out.</param>
    /// <param name="runHasFilter">
    /// Whether the run has <c>--project</c>, <c>--database</c> or a <c>.sql</c> path.  A sidecar is deleted only in
    /// a run with none: a run on a part does not know what the rest needs of a file.
    /// </param>
    /// <param name="anythingWasReported">
    /// Whether the run has reported an error before this step.  A sidecar is deleted only when it has not: the plan
    /// drops a setting it cannot read and a claim it cannot read, so a file can look as if it needs no entry only
    /// because of the error.
    /// </param>
    /// <param name="exists">Whether a file is on the disk.</param>
    public static ImmutableArray<FileOutcome> Decide(
        ImmutableArray<FileWork> files,
        bool runHasFilter,
        bool anythingWasReported,
        Func<string, bool> exists
    ) => [.. files.Select(file => Decide(file, runHasFilter, anythingWasReported, exists))];

    private static FileOutcome Decide(
        FileWork file,
        bool runHasFilter,
        bool anythingWasReported,
        Func<string, bool> exists
    )
    {
        var path = SidecarFormat.PathFor(file.File.Path);
        if (file.File.State != PlannedFileState.Ready)
        {
            return Of(file, path, FileAction.None);
        }

        var needing = file.Queries.Where(static query => query.Planned.NeedsEntry).ToList();
        if (needing.Count == 0)
        {
            // A sidecar that would be empty.  Not in a run that failed: it may need no entry only by an error.
            var deletes = !runHasFilter && !anythingWasReported && exists(path);
            return Of(file, path, deletes ? FileAction.Delete : FileAction.None);
        }

        if (!needing.Exists(static query => query.Planned.IsSelected))
        {
            return Of(file, path, FileAction.None);
        }

        if (needing.Exists(static query => query.State == QueryState.ToDescribe))
        {
            throw new InvalidOperationException($"A query of '{file.File.Path}' is still to be described.");
        }

        var heldBackBy = needing
            .Where(static query => query.State is QueryState.Failed or QueryState.LeftOut)
            .Select(static query => new HeldBackQuery(query.Planned.Query.Name, query.State == QueryState.Failed))
            .ToImmutableArray();
        if (!heldBackBy.IsEmpty)
        {
            return new FileOutcome(
                file.File,
                path,
                FileAction.HeldBack,
                Target: null,
                Text: null,
                new EquatableArray<HeldBackQuery>(heldBackBy),
                needing.Exists(static query => query.State == QueryState.Described)
            );
        }

        // Every query that needs an entry was described or has a current one.  An entry on the disk for a query
        // that is gone, or that needs none, is in no target, so writing drops it.
        var target = new Sidecar(
            SidecarFormat.Version,
            PackageVersion.Prefix,
            new EquatableArray<SidecarEntry>([
                .. needing.Select(static query =>
                    query.Entry ?? throw new InvalidOperationException($"'{query.Planned.Query.Name}' has no entry.")
                ),
            ])
        );
        var text = SidecarWriter.Write(target);
        return new FileOutcome(
            file.File,
            path,
            file.OnDisk?.Text is { } onDisk && SameText(onDisk, text) ? FileAction.Unchanged : FileAction.Write,
            target,
            text,
            EquatableArray<HeldBackQuery>.Empty,
            needing.Exists(static query => query.State == QueryState.Described)
        );
    }

    private static FileOutcome Of(FileWork file, string path, FileAction action) =>
        new(
            file.File,
            path,
            action,
            Target: null,
            Text: null,
            EquatableArray<HeldBackQuery>.Empty,
            HasDescribed: false
        );

    // A checkout may give the file other line endings than the writer's.
    private static bool SameText(string onDisk, string written) =>
        string.Equals(onDisk.Replace("\r\n", "\n", StringComparison.Ordinal), written, StringComparison.Ordinal);
}
