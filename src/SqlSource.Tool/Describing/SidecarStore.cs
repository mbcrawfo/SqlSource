using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using SqlSource.Diagnostics;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Applies the outcomes of a run to the disk.  It is the one place that writes or deletes a sidecar, and the one
/// thing the tool writes into a project.
/// </summary>
/// <remarks>
/// A sidecar is written whole or not at all: to a temporary file in the same folder, which is then moved over the
/// old one, so that a run that is stopped leaves the old file or the new one.
/// </remarks>
internal static class SidecarStore
{
    private const string Written = "written";

    private const string Deleted = "deleted";

    /// <summary>
    /// Writes and deletes what the outcomes say, and reports <c>SQLSRC217</c> for a file that was held back though
    /// a query of it was described, and <c>SQLSRC218</c> for a file the system would not let it change.  A run
    /// that was cancelled changes no further file.
    /// </summary>
    public static void Apply(
        ImmutableArray<FileOutcome> outcomes,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        foreach (var outcome in outcomes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (outcome.Action)
            {
                case FileAction.Write:
                    Write(
                        outcome.SidecarPath,
                        outcome.Text ?? throw new InvalidOperationException("A file to write has no text."),
                        reporter
                    );
                    break;
                case FileAction.Delete:
                    Delete(outcome.SidecarPath, reporter);
                    break;
                case FileAction.HeldBack when outcome.HasDescribed:
                    reporter.Report(NotWritten(outcome));
                    break;
                case FileAction.None:
                case FileAction.Unchanged:
                case FileAction.HeldBack:
                    // Nothing is changed, and nothing is said.
                    break;
                default:
                    throw new InvalidOperationException($"Unknown action {outcome.Action}.");
            }
        }
    }

    private static void Write(string path, string text, Reporter reporter)
    {
        // Beside the file, so that the move is within one volume.  The name ends in neither ".sql" nor ".json":
        // nothing takes a file that a stopped run left for a query file or a sidecar.
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            // UTF-8 without a byte order mark, and the line endings of the writer.
            File.WriteAllText(temporary, text);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The error of the write is the one to report, so the removal of what it left may fail unseen.
            _ = TryDelete(temporary);
            reporter.Report(Failure(path, Written, exception));
        }
    }

    private static void Delete(string path, Reporter reporter)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            reporter.Report(Failure(path, Deleted, exception));
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
        ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, path, path, tried, exception.Message);

    private static ToolDiagnostic NotWritten(FileOutcome outcome) =>
        ToolDiagnostic
            .ForFile(ToolDiagnostics.SidecarNotWritten, outcome.File.Path, outcome.File.Path)
            .WithLines([
                .. outcome.HeldBackBy.Select(static query => new ContinuationLine(
                    "query",
                    $"{query.Name}: {(query.Failed ? "failed" : "not in this run")}"
                )),
                new ContinuationLine("help", "describe the whole file, or fix the queries that failed"),
            ]);
}
