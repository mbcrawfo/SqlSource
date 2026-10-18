using SqlSource.Diagnostics;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Sidecars;

/// <summary>
/// What the checks of one claimed file's sidecar gave.
/// </summary>
/// <param name="NormalizedPath">The path of the <c>.sql</c> file, in the form <c>SqlPath.Normalize</c> gives.</param>
/// <param name="Diagnostics">The problems the checks found, in the <c>.sql</c> file or in its sidecar.</param>
/// <param name="UsableEntries">
/// The entries that passed every check, in the order of their queries in the file: the only entries a later step may
/// read.
/// </param>
internal sealed record SidecarCheckResult(
    string NormalizedPath,
    EquatableArray<DiagnosticInfo> Diagnostics,
    EquatableArray<SidecarEntry> UsableEntries
);
