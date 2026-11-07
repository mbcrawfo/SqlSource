# TD-0110 Poller Sees Its Sub-Folders Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Under `DOTNET_USE_POLLING_FILE_WATCHER`, a watch of a folder alone sees a folder made or deleted in it, as the system's events do, so `describe --watch` watches a folder of its set made after the watch started, and a polled folder whose deletion the folder above saw is not reported as `SQLSRC287`.

**Architecture:** `PollingFolderWatcher` with `Recursive` false lists the names of its direct sub-folders, without their contents, so the existing compare raises `Created` and `Deleted` for them.  `WatchSession.ReportFailures` leaves out a failure of a folder that is gone when the folder above it has an open watch of the folder alone: under polling that watch now sees the deletion, and under the system's events the same deletion is silent.

**Tech Stack:** C# / .NET, xUnit v3, Shouldly, `TestClock`.

**Spec:** `docs/tech-debt/TD-0110-a-poller-of-a-folder-alone-sees-no-folder-made-in-it.md`

## Where the code differs from the spec

- td-0111 has merged: `PollingFolderWatcherTests.Scene.Start` already passes a `TestClock`; no cherry-pick is needed.
- The spec's "a failed listing of a folder whose deletion was just seen" cannot be decided by order, since the folder's own poller and the poller above it run on separate timers and either may list first, and the session reports a failure 300 ms after it.  The plan decides it by state at report time instead: the folder does not exist and the nearest folder above it that exists has an open watch of the folder alone, which sees (or has seen) the topmost deleted folder.  A folder that exists but cannot be read, or one with no such watch above (a repository's root), is still reported.

## Global Constraints

- Tests never depend on a real timer: drive pollers with `Poll()` or `TestClock.Advance(PollingFolderWatcher.Interval)` (`tests/AGENTS.md`).
- Test names are `Method_Scenario_Outcome`; assertions are Shouldly's; classes are `sealed`.
- `./format.sh` then `./pre-commit-validation.sh` before each commit.
- Resolve TD-0110 as `docs/tech-debt/AGENTS.md` says: delete the item file, remove its `README.md` row (keep the others'), search for `TD-0110`, remove its known issue from `docs/manual/tool.md` (the last two sentences of the "A few changes to folders far from your project" bullet).

## Review Focus

- An ignored name (`obj`) made in a folder watched alone must still be raised: an artifacts layout watches `<repo>/artifacts` alone until `obj` is made.  Test in Task 1.
- A file below a new sub-folder of a folder watched alone must raise nothing.  Covered by the existing `Poll_NotRecursive_RaisesNothingForAFileBelow` and the new creation test.
- A folder that exists but cannot be listed is still reported.  Pinned by the existing `WatchFailureTests` (folders that exist) staying green.
- A gone folder with no watch of the folder above (the top of the set) is still reported.  Test in Task 2.
- A link to a folder in a folder watched alone is listed by name only and never followed.

---

### Task 1: The poller of a folder alone lists its sub-folders

**Files:**
- Modify: `src/SqlSource.Tool/Watching/PollingFolderWatcher.cs` (class summary, `Add`)
- Test: `tests/SqlSource.Tool.Tests/Watching/PollingFolderWatcherTests.cs`

**Interfaces:** none new; `FileChange(FileChangeKind.Created, path, IsFolder: true)` and `FileChange(FileChangeKind.Deleted, path, IsFolder: false)` as today.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Poll_NotRecursiveFolderCreated_RaisesCreatedFolderAlone()
{
    // scene.Start(recursive: false); create "queries" and write "queries/Users.sql"; Poll
    scene.Seen.ShouldBe([new FileChange(FileChangeKind.Created, queries, IsFolder: true)]);
}

[Fact]
public void Poll_NotRecursiveFolderDeleted_RaisesDeleted()
{
    // create "queries" with a file in it; Start(recursive: false); Directory.Delete(queries, true); Poll
    scene.Seen.ShouldBe([new FileChange(FileChangeKind.Deleted, queries, IsFolder: false)]);
}

// An artifacts layout watches artifacts alone until a restore makes obj in it.
[Theory]
[InlineData("obj")] [InlineData("bin")] [InlineData(".git")]
public void Poll_NotRecursiveIgnoredFolderCreated_RaisesCreatedFolder(string name)
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~PollingFolderWatcherTests"`
Expected: the three new tests fail (nothing raised).

- [ ] **Step 3: Implement**

In `Add`, when `!_request.Recursive`, put every `DirectoryInfo` in the listing as `Entry.OfFolder` and do not descend (a link included, since its name alone is listed).  The recursive branch is unchanged.  Rewrite the class summary: a watch of the folder alone lists the names of the folders in it, not what they hold, so it sees a folder made or deleted in it, as the system's events do.

- [ ] **Step 4: Run the class again**

Expected: all pass, the existing `Poll_NotRecursive_RaisesNothingForAFileBelow` included.

- [ ] **Step 5: Commit** `fix(tool): a poller of a folder alone sees a folder made or deleted in it (TD-0110)`

### Task 2: The session leaves unreported a polled folder whose deletion the folder above sees

**Files:**
- Modify: `src/SqlSource.Tool/Watching/WatchSession.cs` (`ReportFailures`, the comment of `ChangesAWatchedFolder`)
- Test: `tests/SqlSource.Tool.Tests/Commands/Watch/WatchFolderTests.cs`

**Interfaces:** consumes Task 1's poller.  New private `bool SeenFromAbove(WatchFailure failure)` in `WatchSession`, on the loop: `!Directory.Exists(failure.Folder)` and `_watches` holds the nearest folder above it that exists with `Recursive == false` and a non-null watch (the nearest that exists, since a folder deleted with its parent may fail during the evaluation that closes the parent's watch).

- [ ] **Step 1: Write the failing tests** in `WatchFolderTests`

```csharp
// The spec's example: a folder of the set made inside a folder watched alone, under polling, is watched by the pass
// its creation starts.  Every folder but the project's polls on the test's clock.
[Fact]
public async Task Watch_PolledFolderMadeInAFolderWatchedAlone_IsWatched()
{
    // The fixture's repository (root holds .git), libs/Shared not made; settings listed; polling on.
    // Run.Watch: project folder -> _watch.Watcher.Watch(request), else a PollingFolderWatcher on _watch.Clock,
    // recording each request's folder.
    // Start; Directory.CreateDirectory(_shared); WriteSettings(); advance Interval; PassAsync(..., 2)
    // requested folders contain _libs and _shared; PassStarts == [(1,0,true),(2,1,true)]
}

// A polled folder deleted below a folder watched alone: the watch above sees it, so its own failed listing is no
// error, and the pass evaluates.
[Fact]
public async Task Watch_PolledFolderDeletedBelowAFolderWatchedAlone_ReportsNoFailure()
{
    // WriteSettings(); polling with real pollers as above; Start; Directory.Delete(_shared, true);
    // advance Interval; PassAsync(..., 2)
    _watch.Errors.ShouldNotContain("SQLSRC287");
    // PassStarts' second pass evaluates
}
```

Change `Watch_PolledFolderThatStaysGone_GivesOnePass` to match: `Shared` is outside a repository, so the run's folder above it is watched alone, and its failure is now not reported (`SQLSRC287` count 0); keep its pass counts, and rename it `Watch_PolledFolderThatStaysGone_GivesOnePassAndNoError`, its comment saying why.  Add `Watch_PolledTopFolderGone_IsReported`: a poller of the repository's root (nothing above it watched) whose folder is gone reports `SQLSRC287` once.

- [ ] **Step 2: Run and see the new tests fail**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Commands.Watch"`
Expected: the creation test passes after Task 1 alone only if the poller fix suffices (it should); the deletion test fails on `SQLSRC287`.

- [ ] **Step 3: Implement** `SeenFromAbove` and filter it out in `ReportFailures` before `_reported.Add`, so a later failure with the same message is still reported.  Rewrite the `ChangesAWatchedFolder` comment's last clause ("a poller of a folder alone sees no folder made in it") to state that a poller of a folder alone lists the folders in it.

- [ ] **Step 4: Run `Commands.Watch` and `Watching` tests**: all pass.

- [ ] **Step 5: Commit** `fix(tool): a polled folder deleted below a folder watched alone is no SQLSRC287 (TD-0110)`

### Task 3: Documents and resolution

**Files:**
- Modify: `docs/architecture/tool.md` (the poller bullet of "Every watch comes through `ToolHost.Watch`", the session paragraph's "topmost new folder" sentence, the `WatchFailure` paragraph)
- Modify: `docs/manual/tool.md` (known issue: drop the polling sentences), `docs/manual/diagnostics.md` (SQLSRC287's polling paragraph: a deleted folder is reported only when the watch was not looking at the folder that held it)
- Delete: `docs/tech-debt/TD-0110-a-poller-of-a-folder-alone-sees-no-folder-made-in-it.md`; remove its row from `docs/tech-debt/README.md`
- Check `src/SqlSource.Tool/Watching/AGENTS.md` still holds (it does: the poller lists nothing *under* `bin`, `obj`, `.git`)

- [ ] **Step 1:** Edit each, then `grep -rn "TD-0110" --exclude-dir=.git .` finds only this plan.
- [ ] **Step 2:** `./format.sh`, `./pre-commit-validation.sh` passes.
- [ ] **Step 3: Commit** `docs: a poller of a folder alone sees its folders; resolve TD-0110`
