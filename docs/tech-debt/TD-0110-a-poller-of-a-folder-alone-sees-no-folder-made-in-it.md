# TD-0110 - A poller of a folder alone sees no folder made in it

## Problem

Found by the maintainability review of 2026-11-06.  [`PollingFolderWatcher`](../../src/SqlSource.Tool/Watching/PollingFolderWatcher.cs), with `Recursive` false, skips every folder before it lists anything, so a poller of a folder alone never raises `Created` or `Deleted` for a folder in it.  Two parts of `describe --watch` count on that event:

- [`WatchSet.Folders`](../../src/SqlSource.Tool/Watching/WatchSet.cs) puts each folder above a folder of the set, up to the repository's root, into the set alone, so that its watch sees the folder below it made or deleted.
- [`WatchSession.ChangesAWatchedFolder`](../../src/SqlSource.Tool/Watching/WatchSession.cs) asks for an evaluation when a folder of the set that has no watch is created or renamed to.  That is the only way a folder that was missing when the set was watched gets a watch before something else asks for an evaluation.

Under the system's events both hold.  Under `DOTNET_USE_POLLING_FILE_WATCHER`, a folder of the set that does not exist when the watch starts, outside every folder watched with its folders below, and whose nearest existing folder above is watched alone, is never watched when it is made.  For example: the watch is of the project `src/App/App.csproj`, which lists `../../shared/queries/Orders.sql` explicitly, and neither `shared` nor the file exists yet.  `mkdir -p shared/queries` and a save of `Orders.sql` start no pass until some other change asks for an evaluation.

A folder watched alone that is deleted under polling is seen another way: its poller fails its listing, which asks for an evaluation.  But that failure is reported as `SQLSRC287`, "The watcher failed", once for each folder and message, where the system's events take the same deletion silently.  A folder below one watched with its folders below is a `Deleted` of that watch under either.

[`docs/architecture/tool.md`](../architecture/tool.md#the-session), "the creation of the topmost new folder is the one that a watch sees", holds under the system's events alone.

## Why it exists

The poller was written to list a folder's files the way `FileSystemWatcher` without `IncludeSubdirectories` reports them, and the session's reliance on a folder's own creation was written against the system's events.

## Impact

Low.  It takes polling, a project that names a file outside its folder in a folder that does not exist yet, and a repository root that is not the project's folder.  The save is seen by the next pass that something else starts, and a restart of the watch sees it at once.  The `SQLSRC287` of a deleted folder is noise the user cannot act on.

## Proposed fix

Let a poller of a folder alone list the names of its direct sub-folders, without their contents, and raise `Created` and `Deleted` for them, as it does for a path whose kind changes.  Then a deleted folder is seen by the folder above it, as under the system's events, and a failed listing of a folder whose deletion was just seen can be left unreported.  Qualify or keep the sentence of `docs/architecture/tool.md` to match.

## Trigger

A report of a watch under polling that misses a new folder, or the next change to `PollingFolderWatcher`.
