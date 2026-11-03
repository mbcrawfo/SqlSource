namespace SqlSource.Tool.Watching;

/// <summary>
/// One event of a watched folder.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="Path">The full path it happened to.</param>
/// <param name="IsFolder">
/// True when the path was a folder when the event was read, and false for a <see cref="FileChangeKind.Deleted" />,
/// since nothing is there to ask.
/// </param>
/// <param name="OldPath">
/// The full path a <see cref="FileChangeKind.Renamed" /> came from, and null for any other kind.
/// </param>
internal sealed record FileChange(FileChangeKind Kind, string Path, bool IsFolder, string? OldPath = null);
