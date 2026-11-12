# AGENTS.md - src/SqlSource.Tool/Watching

How a watch works: [watch mode](../../../docs/architecture/tool.md#watch-mode).

- **`WatchSession.ChangeDetectedMarker` and `WatchSession.WatchingMarker` are a contract with users' editors.**  The manual's [describe on save](../../../site/tool.md#describe-on-save) quotes them, the VS Code recipe in `site/recipes.md` reads them in its problem matcher, and `EditorRecipeTests` of `tests/SqlSource.Repository.Tests/Watching/` holds the two together.  Change them only with both.
- **A pass reports on a `Reporter` of its own over the session's trace**, never on the session's: an error of one pass must not stop the next from deleting a sidecar.
- **The callbacks of a `WatchRequest` run on the watch's own thread.**  They classify the change, or keep it while an evaluation runs, under the session's lock and return; nothing waits or runs a pass there.
- **No watch is made or closed while the session's lock is held.**  Closing a poller waits for its poll, whose report waits for the lock, so the two would hang; collect under the lock and close after it.
- **What is ignored is decided twice**: by `WatchSet.IsIgnored`, and by `PollingFolderWatcher`, which lists nothing under `bin`, `obj` or `.git` but a `project.assets.json` at any depth below `obj`, outside a `bin` or `.git` folder.  A change to one is a change to both.  The run's `.env` file passes both wherever it is: `WatchSet` watches its folder alone and lets it through.
- **The kind of an event is not what happened.**  macOS and Linux raise different kinds for one save, so a `Changed`, a `Created` and a `Renamed` to a path are each a write of it ([what is watched](../../../docs/architecture/tool.md#what-is-watched)).  `tests/SqlSource.Tool.Tests/Watching/WatchSetTests.cs` holds a case of each.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
