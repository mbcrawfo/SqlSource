using System;
using System.Buffers;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using SqlSource.Diagnostics;
using SqlSource.Generation.Paths;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Applies the outcomes of a run to the disk.  It is the one place that writes or deletes a sidecar, and the one
/// thing the tool writes into a project.
/// </summary>
/// <remarks>
/// <para>
/// A sidecar is written whole or not at all: to a temporary file in the same folder, which is then moved over the
/// old one, so that a run that is stopped leaves the old file or the new one.
/// </para>
/// <para>
/// A run that is killed between the write and the move leaves the temporary file.  The next run that looks at the
/// sidecar removes it, and a run that does not look at the sidecar changes nothing beside it.  A run on the whole
/// removes those of every sidecar in the folders of its files: the <c>.sql</c> file of one may be gone.
/// </para>
/// </remarks>
internal static class SidecarStore
{
    private const string Written = "written";

    private const string Deleted = "deleted";

    private const string TemporaryExtension = ".tmp";

    // How many digits a Guid has in the format "N".
    private const int IdLength = 32;

    private static readonly SearchValues<char> IdDigits = SearchValues.Create("0123456789abcdef");

    /// <summary>
    /// How long ago a temporary file was last written before it is taken for one that a run left behind.
    /// </summary>
    internal static readonly TimeSpan LeftoverAge = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Writes and deletes what the outcomes say, and reports <c>SQLSRC217</c> for a file that was held back though
    /// a query of it was described, and <c>SQLSRC218</c> for a file the system would not let it change, or would
    /// not let <see cref="FileOutcomes" /> open to see that it may be deleted.  A run that was cancelled changes no
    /// further file.
    /// </summary>
    /// <param name="outcomes">What the run does with the sidecar of each file of its plan.</param>
    /// <param name="reporter">Where the errors go, with the run's trace.</param>
    /// <param name="time">The clock, which says how old a temporary file is.</param>
    /// <param name="onTheWhole">Whether the run has no filter, and so knows every file of its projects.</param>
    /// <param name="cancellationToken">Ends the run.</param>
    public static void Apply(
        ImmutableArray<FileOutcome> outcomes,
        Reporter reporter,
        TimeProvider time,
        bool onTheWhole,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        RemoveLeftovers(outcomes, onTheWhole, time.GetUtcNow().UtcDateTime - LeftoverAge, cancellationToken);
        foreach (var outcome in outcomes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (outcome.Action)
            {
                case FileAction.Write:
                    var written = Write(
                        outcome.SidecarPath,
                        outcome.Text ?? throw new InvalidOperationException("A file to write has no text."),
                        reporter
                    );
                    RunEvents.Sidecar(reporter.Trace, outcome.SidecarPath, written ? Written : "failed");
                    break;
                case FileAction.Delete:
                    var deleted = Delete(outcome.SidecarPath, reporter);
                    RunEvents.Sidecar(reporter.Trace, outcome.SidecarPath, deleted ? Deleted : "failed");
                    break;
                case FileAction.Unreadable:
                    reporter.Report(
                        outcome.Failure ?? throw new InvalidOperationException("A file left unread has no error.")
                    );
                    RunEvents.Sidecar(reporter.Trace, outcome.SidecarPath, "failed");
                    break;
                case FileAction.HeldBack:
                    if (outcome.HasDescribed)
                    {
                        reporter.Report(NotWritten(outcome));
                    }

                    RunEvents.Sidecar(reporter.Trace, outcome.SidecarPath, "held-back");
                    break;
                case FileAction.Unchanged:
                    RunEvents.Sidecar(reporter.Trace, outcome.SidecarPath, "unchanged");
                    break;
                case FileAction.None:
                    // The run did not look at this sidecar: nothing is changed, and nothing is said.
                    break;
                default:
                    throw new InvalidOperationException($"Unknown action {outcome.Action}.");
            }
        }
    }

    // Removes the temporary files that an earlier run left, each folder read once.  A filter narrows what the run
    // may change to the sidecars it looks at; a run on the whole knows every file of its projects, and takes those
    // of every sidecar in the folders of its files.
    private static void RemoveLeftovers(
        ImmutableArray<FileOutcome> outcomes,
        bool onTheWhole,
        DateTime before,
        CancellationToken cancellationToken
    )
    {
        var folders = outcomes
            .Where(outcome => onTheWhole || outcome.Action != FileAction.None)
            .GroupBy(
                static outcome => Path.GetDirectoryName(outcome.SidecarPath) ?? "",
                static outcome => Path.GetFileName(outcome.SidecarPath),
                StringComparer.Ordinal
            );
        foreach (var folder in folders)
        {
            Func<string, bool> isWanted = onTheWhole
                ? SqlPath.IsSidecarFile
                : folder.ToHashSet(StringComparer.Ordinal).Contains;
            RemoveLeftovers(folder.Key, isWanted, before, cancellationToken);
        }
    }

    // Removes the temporary files that an earlier run left in the folder, of the sidecars whose names are wanted.
    // One that was written after the time given is left: another run may be writing the sidecar now, and its move
    // would fail.  A file that cannot be removed is no error of this run, and a run that was cancelled removes no
    // further file.
    private static void RemoveLeftovers(
        string folder,
        Func<string, bool> isWanted,
        DateTime before,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var leftovers = Directory
                .EnumerateFiles(folder, "*" + TemporaryExtension)
                .Where(file =>
                    SidecarOfTemporary(Path.GetFileName(file)) is { } sidecar
                    && isWanted(sidecar)
                    && File.GetLastWriteTimeUtc(file) < before
                );
            foreach (var file in leftovers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = TryDelete(file);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The folder cannot be listed.  What the run does with the sidecar itself says so, if it matters.
        }
    }

    // The name of the sidecar that a temporary file of Write is of, or nothing when the name is not one that Write
    // gives: the sidecar's name, a point, the 32 digits of a Guid, and ".tmp".  What the system matches a pattern
    // with is wider.
    private static string? SidecarOfTemporary(string fileName)
    {
        var sidecarLength = fileName.Length - 1 - IdLength - TemporaryExtension.Length;
        return
            sidecarLength > 0
            && fileName[sidecarLength] == '.'
            && fileName.EndsWith(TemporaryExtension, StringComparison.Ordinal)
            && fileName.AsSpan(sidecarLength + 1, IdLength).IndexOfAnyExcept(IdDigits) < 0
            ? fileName[..sidecarLength]
            : null;
    }

    private static bool Write(string path, string text, Reporter reporter)
    {
        // Beside the file, so that the move is within one volume.  The name ends in neither ".sql" nor ".json":
        // nothing takes a file that a stopped run left for a query file or a sidecar.  SidecarOfTemporary knows it.
        var temporary = $"{path}.{Guid.NewGuid():N}{TemporaryExtension}";
        try
        {
            // UTF-8 without a byte order mark, and the line endings of the writer.
            File.WriteAllText(temporary, text);
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The error of the write is the one to report, so the removal of what it left may fail unseen.
            _ = TryDelete(temporary);
            reporter.Report(Failure(path, Written, exception));
            return false;
        }
    }

    private static bool Delete(string path, Reporter reporter)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            reporter.Report(Failure(path, Deleted, exception));
            return false;
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static ToolDiagnostic Failure(string path, string tried, Exception exception) =>
        ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, path, tried, exception.Message);

    private static ToolDiagnostic NotWritten(FileOutcome outcome) =>
        ToolDiagnostic
            .ForFile(ToolDiagnostics.SidecarNotWritten, outcome.File.Path)
            .WithLines([
                .. outcome.HeldBackBy.Select(static query => new ContinuationLine(
                    "query",
                    $"{query.Name}: {(query.Failed ? "failed" : "not in this run")}"
                )),
                new ContinuationLine("help", "describe the whole file, or fix the queries that failed"),
            ]);
}
