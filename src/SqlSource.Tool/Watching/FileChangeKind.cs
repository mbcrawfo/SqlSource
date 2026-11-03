namespace SqlSource.Tool.Watching;

/// <summary>
/// What a watcher saw happen to a path.  The systems raise different kinds for the same save, so a reader treats
/// <see cref="Changed" />, <see cref="Created" /> and <see cref="Renamed" /> alike as a write of the path.
/// </summary>
internal enum FileChangeKind
{
    Changed,
    Created,
    Deleted,
    Renamed,
}
