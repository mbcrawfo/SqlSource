using System.Collections.Immutable;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Describing;

/// <summary>
/// One claimed file of a run.
/// </summary>
/// <param name="File">The file of the plan.</param>
/// <param name="OnDisk">
/// The sidecar beside it.  Null when the run did not read it: the file is not ready, or no query of it that needs
/// an entry is selected.
/// </param>
/// <param name="Queries">Every query of the file, in the file's order.</param>
internal sealed record FileWork(PlannedFile File, SidecarOnDisk? OnDisk, ImmutableArray<QueryWork> Queries);
