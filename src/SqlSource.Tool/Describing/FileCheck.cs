using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What <c>--check</c> found for the sidecar of one file, as data.
/// </summary>
/// <param name="Outcome">
/// What <see cref="FileOutcomes" /> decided for the file, with the file of the plan and the full path of its
/// sidecar.
/// </param>
/// <param name="WasCompared">
/// Whether the sidecar was compared with anything.  It was when <see cref="FileOutcomes" /> decided to delete it,
/// which is for a file with no query that needs an entry, and when a selected query of the file was described.  It
/// was not for a file that is not ready, or none of whose selected queries could be described, or that needs no
/// entry and whose sidecar is not to be deleted.
/// </param>
/// <param name="Differences">An <c>SQLSRC219</c> for each difference, in the order they are to be reported.</param>
internal sealed record FileCheck(FileOutcome Outcome, bool WasCompared, EquatableArray<ToolDiagnostic> Differences);
