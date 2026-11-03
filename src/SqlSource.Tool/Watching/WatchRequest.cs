using System;

namespace SqlSource.Tool.Watching;

/// <summary>
/// What <see cref="ToolHost.Watch" /> is asked to watch, and whom it tells.  Both callbacks run on a thread of the
/// watcher's own, so they return quickly and do not wait for the code that started the watch.
/// </summary>
/// <param name="Folder">The full path of the folder to watch, which exists.</param>
/// <param name="Recursive">True to watch the folders below it too.</param>
/// <param name="Poll">True to list the folder on a timer in place of asking the system for its events.</param>
/// <param name="Changed">Called with each event.</param>
/// <param name="Failed">Called when the watch fails or loses events.</param>
internal sealed record WatchRequest(
    string Folder,
    bool Recursive,
    bool Poll,
    Action<FileChange> Changed,
    Action<WatchFailure> Failed
);
