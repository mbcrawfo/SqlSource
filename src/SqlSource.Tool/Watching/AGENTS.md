# AGENTS.md - src/SqlSource.Tool/Watching

How a watch works: [watch mode](../../../docs/architecture/tool.md#watch-mode).

- **`WatchSession.ChangeDetectedMarker` and `WatchSession.WatchingMarker` are a contract with users' editors.**  The manual's [describe on save](../../../docs/manual/tool.md#describe-on-save) quotes them, and the VS Code recipe of phase 9.4 reads them in its problem matcher, with a documentation test that holds the two together.  Change them only with both.
- **A pass reports on a `Reporter` of its own over the session's trace**, never on the session's: an error of one pass must not stop the next from deleting a sidecar.
- **The callbacks of a `WatchRequest` run on the watch's own thread.**  They classify the change under the session's lock and return; nothing waits or runs a pass there.
- **What is ignored is decided twice**: by `WatchSet.IsIgnored`, and by `PollingFolderWatcher`, which lists nothing under `bin`, `obj` or `.git` but `obj/project.assets.json`.  A change to one is a change to both.
- **The kind of an event is not what happened.**  macOS and Linux raise different kinds for one save, so a `Changed`, a `Created` and a `Renamed` to a path are each a write of it ([what is watched](../../../docs/architecture/tool.md#what-is-watched)).  `tests/SqlSource.Tool.Tests/Watching/WatchSetTests.cs` holds a case of each.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
