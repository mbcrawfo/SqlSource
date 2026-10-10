using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What <c>--check</c> found for the sidecar of one file, as data.
/// </summary>
/// <param name="File">The file of the plan.</param>
/// <param name="SidecarPath">The full path of its sidecar.</param>
/// <param name="WasCompared">
/// Whether the sidecar was compared with anything.  It was not for a file that is not ready, that has no selected
/// query that needs an entry, or none of whose selected queries could be described.
/// </param>
/// <param name="Differences">An <c>SQLSRC219</c> for each difference, in the order they are to be reported.</param>
internal sealed record FileCheck(
    PlannedFile File,
    string SidecarPath,
    bool WasCompared,
    EquatableArray<ToolDiagnostic> Differences
);
