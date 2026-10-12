using SqlSource.Snapshot;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What a run does with the sidecar of one file, as data.  <see cref="SidecarStore" /> applies it to the disk, and
/// <see cref="SidecarCheck" /> compares in its place.
/// </summary>
/// <param name="File">The file of the plan.</param>
/// <param name="SidecarPath">The full path of its sidecar.</param>
/// <param name="Action">What is done.</param>
/// <param name="Target">
/// What the sidecar is to hold, for <see cref="FileAction.Write" /> and <see cref="FileAction.Unchanged" />.
/// </param>
/// <param name="Text">The target as the writer gives it.  Set with <paramref name="Target" />.</param>
/// <param name="HeldBackBy">The queries that held the file back, in the file's order.</param>
/// <param name="HasDescribed">Whether a query of the file was described in this run.</param>
/// <param name="Failure">
/// The <c>SQLSRC218</c> of <see cref="FileAction.Unreadable" />, which the step that applies the outcome reports.
/// </param>
internal sealed record FileOutcome(
    PlannedFile File,
    string SidecarPath,
    FileAction Action,
    Sidecar? Target,
    string? Text,
    EquatableArray<HeldBackQuery> HeldBackBy,
    bool HasDescribed,
    ToolDiagnostic? Failure = null
);
