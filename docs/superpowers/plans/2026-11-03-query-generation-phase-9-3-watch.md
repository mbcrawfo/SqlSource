# `describe --watch` Implementation Plan (phase 9.3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `dotnet sqlsource describe --watch` runs a normal pass, then waits for changes and runs another pass after each, until it is stopped, so that a saved `.sql` file gets its sidecar refreshed without running the tool by hand.

**Architecture:** A new folder `src/SqlSource.Tool/Watching/` holds the file events (`FileChange`), what is watched and what a change does (`WatchSet`), the real watchers (`FileSystemWatcher` and a poller of the tool's own) and the loop (`WatchSession`).  Every file event comes through a new last member of `ToolHost`, `Watch`, so the tests drive a fake watcher.  A pass is a whole `describe` run on the cached manifests, with its own reporter; the manifest targets run again only when a change calls for it.

**Tech Stack:** C# (`net8.0`), System.CommandLine 2.0.12, `FileSystemWatcher`, `TimeProvider`, xunit v3, Shouldly.

**Spec:** `docs/superpowers/specs/2026-11-02-query-generation-phase-9-build-integration-design.md`, sections "`describe --watch`" and "9.3 `describe --watch`", as amended by the 9.0 spike (`docs/superpowers/specs/2026-11-02-query-generation-phase-9-spike.md`, item 6).

## Where the merged code differs from the spec, and the calls this plan makes

- **The spec's three rows that change only connections or the plan are one effect.**  `DescribeRun.RunAsync` already reloads `.env` through `ConnectionEnvironment.Load` on every run, and a pass always plans again from the manifests it holds, so "plans again" and "reads the connections again" are both "run a pass on the cached manifests".  `WatchEffect` has three values: `None`, `Describe`, `Evaluate`.
- **Project files, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props` and `obj/project.assets.json` are recognised by name**, not by a list of the unit's projects: the tool keeps manifests only of the projects that use SqlSource, and a project that gains the package must still wake the watcher.  A `.csproj`, `.sln` or `.slnx` anywhere under a watched folder is a project file.
- **The `Directory.*` files above a project are watched up to the repository's root**, the folders `DotEnvSearch.FoldersOf(projectFolder)` gives (the project's folder alone outside a repository), so the watcher never watches a home folder or `/`.
- **The cache is kept only while it is clean.**  The manifest targets run again before a pass when a change calls for it, and also when the last evaluation reported an error, so that a project not restored stays reported in every pass until it is fixed.
- **Each pass has its own `Reporter`** on the session's trace: `DescribeRun` deletes sidecars only when its reporter counted no error, and an error of one pass must not stop the next from deleting.
- **The watcher's own error is a new diagnostic, `SQLSRC287`**, since `src/SqlSource.Tool/AGENTS.md` sends every error through the reporter with a descriptor.  The design's diagnostics table lists none for 9.3; 9.2's new ids are targets ids (`SQLSRC0xx`), so `SQLSRC287` does not collide.
- **`Usage` gains `Flags`**, the flag options given, so that `CheckUsage` can refuse `--watch` with `--check` (a flag has no value, and `Usage.Values` holds values only).  9.2 needs the same for `--online`; `9-3-notes.md` says so.
- **Polling** lists the watched folders every second (designer's call: the spec gives no interval; `PhysicalFileProvider`'s 4 s gave changes 4 to 8 s late in the spike).

## Global Constraints

- Markers, exact: `sqlsource: change detected; describing` before every pass after the first, and `sqlsource: watching for changes; press Ctrl+C to stop` after every pass.  Both are `internal const string` in `src/SqlSource.Tool/Watching/WatchSession.cs`, named `ChangeDetectedMarker` and `WatchingMarker`; 9.4's documentation test reads them.
- Debounce: a pass starts 300 ms after the last change it will cover; a change during a pass schedules one more pass after it.  The clock is `ToolHost.Time`, never `Task.Delay` without it or `Stopwatch`.
- `--watch` with `--check` is a usage error.  With `--force` the first pass forces and the rest are normal passes.  `--project`, `--database` and `.sql` paths narrow every pass; `--connection` and `--verbose` apply to every pass; `--log` is one file for the session.
- Ctrl+C, `SIGTERM` or `SIGHUP` stops the pass under way and ends the watcher with `0`.  A wrong command line, and a unit that cannot be found, end it before the first pass with `1`.  Every other error, a project not restored and an exception (`SQLSRC200`) included, is reported in its pass and the watcher keeps going.  A watcher never writes `Cli.RunAgainLine`.
- A `Changed`, a `Created` or a `Renamed` to a listed path is a write of it; a `Renamed` from a listed path, or its `Deleted`, is its deletion, decided when the pass starts: still gone → `Evaluate`, there again → `Describe`.  A `Created` never means a file is new.
- Ignored: sidecars (`*.sql.json`), the store's temporary files (`<sidecar>.<32 hex digits>.tmp`), and everything under a folder named `bin`, `obj` (but a file `obj/project.assets.json`) or `.git`.
- `DOTNET_USE_POLLING_FILE_WATCHER` set to `true` or `1`, ignoring case, makes the tool poll; read through `ToolHost.GetEnvironmentVariable`.
- A new log event follows `src/SqlSource.Tool/AGENTS.md`'s "What a run says": built in `Logging/RunEvents.cs`, values are `LogValue`s, members are added and never renamed.
- Nothing reads `Console`, `Environment` or the current directory outside `ToolHost.Create` and `Cli.Run`.  No test waits for a time to pick an order (`tests/SqlSource.Tool.Tests/AGENTS.md`).
- 9-2 owns `Reporting/Reporter.cs` and `Describing/SidecarStore.cs`: do not change them.  Changes to `DescribeCommand.Create`, `CheckUsage`, `UsageCheck.cs`, `docs/manual/tool.md`'s command reference and `docs/architecture/tool.md` stay in blocks of their own.
- Every commit passes `./pre-commit-validation.sh` (root `AGENTS.md`, Commits).  Run `./format.sh` before validating (`./format.sh --check` reports what it would change).

## Review Focus

1. **The watcher's own writes must never wake it**: a pass writes a sidecar through a temporary file and a rename, which raise `Created`, `Changed` and `Renamed` under a watched folder; a loop of passes would be the worst failure.  Test in Task 4.
2. **A `dotnet build` beside the watcher** writes hundreds of files under `bin` and `obj`: no pass.  Test in Task 3 (classification) and Task 4 (no pass).
3. **Many saves during a long pass** give exactly one more pass, not one each.  Test in Task 4.
4. **A path whose case differs from the manifest's**, as a case-insensitive file system on macOS or Windows reports it: compared with `RunProjects.PathComparer` (ignoring case).  Test in Task 3.
5. **The `.env` deleted between passes**: the next pass reads no file, reports the database without a connection, and the watcher goes on.  Test in Task 4.

---

### Task 1: The `--watch` option and its usage rule

**Files:**
- Modify: `src/SqlSource.Tool/Usage.cs` (new init property `Flags`)
- Modify: `src/SqlSource.Tool/UsageCheck.cs` (record each flag given)
- Modify: `src/SqlSource.Tool/DescribeCommand.cs` (`WatchOption`, its declaration in `Create`, its rule in `CheckUsage`, `Watches(ParseResult)`)
- Test: `tests/SqlSource.Tool.Tests/Commands/UsageCheckTests.cs`, and the vocabulary of `Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine`

**Interfaces:**
- Produces: `internal const string DescribeCommand.WatchOption = "--watch"`; `public static bool DescribeCommand.Watches(ParseResult parsed)` (true when `Runs(parsed)` and `--watch` was given); `public IReadOnlyList<Option> Usage.Flags { get; init; } = [];` (each flag option given, in order, once each).

- [ ] **Step 1: Write the failing tests**
  - `CheckUsage_WatchWithCheck_IsRefused`: `describe --watch --check` and `describe --check --watch` each give exit `1`, standard error exactly `sqlsource: options '--watch' and '--check' cannot be used together\n`, and run nothing (`Processes.Requests` empty).
  - `CheckUsage_WatchWithForce_IsAccepted`: `UsageCheck.Check` + `CheckUsage` of `describe --watch --force` give no message.
  - `--watch` joins the token vocabulary of `Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine`.
  - `Help_Describe_ListsWatch`: `describe --help` output contains `--watch`.
- [ ] **Step 2: Run them and see them fail**: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~UsageCheckTests"` → FAIL (`--watch` is an unknown option).
- [ ] **Step 3: Implement.**  `--watch` is a flag (`Arity = ArgumentArity.Zero`), declared in its own block after `log` in `Create` and added to the command after `log`; description: `"Describe again each time a file of the run changes, until stopped with Ctrl+C."`.  `UsageCheck` adds the option to `Flags` where it accepts a flag; the rule in `CheckUsage` is its own block before the loop.
- [ ] **Step 4: Run the tool's tests**: `dotnet test tests/SqlSource.Tool.Tests` → PASS.
- [ ] **Step 5: Format, validate, commit** `feat(tool): accept --watch, and refuse it beside --check`.

### Task 2: File events through `ToolHost`

**Files:**
- Create: `src/SqlSource.Tool/Watching/FileChange.cs`, `FileChangeKind.cs`, `WatchRequest.cs`, `WatchFailure.cs`, `SystemFolderWatcher.cs`, `PollingFolderWatcher.cs`
- Modify: `src/SqlSource.Tool/ToolHost.cs` (new last member `Watch`, `ToolHost.Create` passes `ToolHost.WatchFolder`)
- Modify: every `new ToolHost(` (`tests/SqlSource.Tool.Testing/Hosts.cs` and any other the compiler finds); `tests/SqlSource.Tool.Testing/CliRun.cs` (`Watch` override property)
- Test: `tests/SqlSource.Tool.Tests/Watching/SystemFolderWatcherTests.cs`, `tests/SqlSource.Tool.Tests/Watching/PollingFolderWatcherTests.cs`, `tests/SqlSource.Tool.Tests/ToolHostTests.cs`

**Interfaces:**
- Produces:
  - `internal enum FileChangeKind { Changed, Created, Deleted, Renamed }`
  - `internal sealed record FileChange(FileChangeKind Kind, string Path, bool IsFolder, string? OldPath = null)`: full paths; `IsFolder` true when the path was a folder when the event was read (false for a `Deleted`); `OldPath` only for `Renamed`.
  - `internal sealed record WatchFailure(string Folder, bool Overflow, string Message)`: `Overflow` for a lost batch of events (`InternalBufferOverflowException`), which is never reported; else the exception's message.
  - `internal sealed record WatchRequest(string Folder, bool Recursive, bool Poll, Action<FileChange> Changed, Action<WatchFailure> Failed)`
  - `ToolHost.Watch`: `Func<WatchRequest, IDisposable>`; disposing stops the watch.  The real one, `internal static IDisposable ToolHost.WatchFolder(WatchRequest request)`, starts a `SystemFolderWatcher` or, when `Poll`, a `PollingFolderWatcher`.
  - `PollingFolderWatcher(WatchRequest request, TimeProvider time, TimeSpan interval)`: `PollingFolderWatcher.Interval = TimeSpan.FromSeconds(1)`; `internal void Poll()` compares the listing with the last one and raises `Created` (with `IsFolder`), `Deleted` and `Changed` (length or last write time differs).  A recursive listing does not go into a folder named `bin`, `obj` or `.git`, but lists the file `obj/project.assets.json`.
  - `Hosts.Create` and `Hosts.Real` give `Hosts.WatchUnderTheTemporaryFolder`, the real watcher for a folder under the system's temporary folder and a watch that raises nothing elsewhere; `CliRun.Watch` (`Func<WatchRequest, IDisposable>?`) replaces it for a test.
- [ ] **Step 1: Write the failing tests**
  - `SystemFolderWatcherTests.Watch_FileWrittenInAFolderBelow_RaisesItsPath`: a temporary folder with a sub-folder, a recursive request, then `File.WriteAllText` of `sub/Users.sql`; waits with `Eventually` with a patience of 30 s for a change whose `Path` is that file (any kind but `Deleted`, since macOS and Linux raise different kinds: spike item 6), and disposes the watch.  The only test with the real `FileSystemWatcher`.
  - `PollingFolderWatcherTests`: `Poll_FileCreated_RaisesCreated`, `Poll_FileRewritten_RaisesChanged` (set `File.SetLastWriteTimeUtc` forward so the test does not depend on the clock's resolution), `Poll_FileDeleted_RaisesDeleted`, `Poll_FolderCreated_RaisesCreatedFolder`, `Poll_FileUnderBinObjOrGit_RaisesNothing`, `Poll_ProjectAssetsWritten_RaisesChanged`, `Poll_NotRecursive_RaisesNothingForAFileBelow`; each calls `Poll()` itself, never waits.
  - `ToolHostTests`: `Create_Watch_IsTheRealWatcher` in the manner of the tests there.
- [ ] **Step 2: Run them and see them fail** (types missing).
- [ ] **Step 3: Implement.**  `SystemFolderWatcher` wraps one `FileSystemWatcher` (`IncludeSubdirectories` = `Recursive`, `NotifyFilter` = file name, folder name, last write, size, `InternalBufferSize` = 64 KiB), maps `Changed`, `Created`, `Deleted`, `Renamed` to `FileChange` with `IsFolder = Directory.Exists(path)` (false for `Deleted`), and `Error` to `WatchFailure` (`Overflow` when the exception is an `InternalBufferOverflowException`).  `PollingFolderWatcher` polls on a timer of the `TimeProvider` it is given (`TimeProvider.System` from `WatchFolder`); a listing that throws an `IOException` or `UnauthorizedAccessException` raises one `WatchFailure` and the next poll tries again.
- [ ] **Step 4: Run** `dotnet test tests/SqlSource.Tool.Tests` → PASS.
- [ ] **Step 5: Format, validate, commit** `feat(tool): file events come through ToolHost.Watch`.

### Task 3: What is watched, and what a change does

**Files:**
- Create: `src/SqlSource.Tool/Watching/WatchEffect.cs`, `src/SqlSource.Tool/Watching/WatchSet.cs`
- Test: `tests/SqlSource.Tool.Tests/Watching/WatchSetTests.cs`

**Interfaces:**
- Consumes: `FileChange`, `FileChangeKind` (Task 2); `ProjectManifest`; `DotEnvSearch.FoldersOf`; `RunProjects.PathComparer`.
- Produces:
  - `internal enum WatchEffect { None, Describe, Evaluate }` (ordered: a larger value covers a smaller).
  - `internal sealed class WatchSet`, `public static WatchSet Of(string unitPath, string unitFolder, ImmutableArray<ProjectManifest> manifests)`.
  - `public ImmutableArray<(string Folder, bool Recursive)> Folders`: the unit's folder and each project's folder, recursive; each other folder that holds a listed file, each folder of `DotEnvSearch.FoldersOf(unitFolder)` and of `DotEnvSearch.FoldersOf(projectFolder)` for each project, not recursive; none below a recursive one, each once (ignoring case), sorted ordinally so that two sets compare.
  - `public WatchEffect Classify(FileChange change)`, by the table below; a `Deleted`, or the old path of a `Renamed`, that names a listed path or a folder above one gives `None` here and adds the listed paths to `Gone`.
  - `public ImmutableArray<string> Gone(FileChange change)`: the listed paths a deletion may have removed (empty for every other change).
  - "Listed" is a `.sql` file, settings file or `Compile` item of any manifest; every comparison of paths ignores case (`RunProjects.PathComparer`).

  | Change (after the ignore rules of Global Constraints) | Effect |
  |----|----|
  | `Changed`, `Created`, or `Renamed` to a listed path | `Describe` |
  | Any kind of change to a `.csproj`, `.sln`, `.slnx`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, or a file `obj/project.assets.json` | `Evaluate` |
  | `Created` or `Renamed` to a `.sql`, `.cs` or `sqlsource.json` path under a project's folder that no manifest lists | `Evaluate` |
  | `Created` or `Renamed` to a folder (`IsFolder`) under a project's folder | `Evaluate` |
  | Any kind of change to `.env` in a folder of `DotEnvSearch.FoldersOf(unitFolder)` | `Describe` |
  | Anything else | `None` |

- [ ] **Step 1: Write the failing tests** (a `[Theory]` per row where it reads better), with a manifest built in the test (project `/w/App/App.csproj`, `.sql` `/w/App/Queries/Users.sql`, a linked `.sql` `/w/Shared/Common.sql`, settings `/w/App/sqlsource.json`, compile `/w/App/UserQueries.cs`; on Windows the same under a drive):
  - each row's effect; `Changed` of `/w/App/Queries/Users.sql.json` and of `/w/App/Queries/Users.sql.json.0123456789abcdef0123456789abcdef.tmp` → `None`; anything under `/w/App/bin/`, `/w/App/obj/` (`obj/App.AssemblyInfo.cs` → `None`, `obj/project.assets.json` → `Evaluate`) and `/w/.git/` → `None`; a `Created` folder under `bin` → `None`.
  - `Classify_ListedPathInAnotherCase_IsDescribe` (`/w/app/queries/users.sql`).
  - `Classify_ChangedOfAnUnlistedSqlFile_IsNone`; `Classify_DeletedOfAnUnlistedFile_IsNone`.
  - `Gone_DeletedListedFile_IsThatFile`; `Gone_RenamedFromListedFile_IsThatFile`; `Gone_DeletedFolderAboveListedFiles_IsEach`.
  - `Folders_ProjectUnit_AreItsFolderRecursiveAndTheFoldersOutside`: `/w/App` recursive, `/w/Shared` not recursive, and the `.env`/`Directory.*` folders not recursive (with no `.git` above, `FoldersOf` gives the folder alone).
  - `Folders_SolutionInTheRoot_IsTheRootAloneForEveryProjectBelow`.
- [ ] **Step 2: Run them and see them fail.**
- [ ] **Step 3: Implement** `WatchSet` as a pure class: no file is read but through `DotEnvSearch.FoldersOf`, which looks for `.git`.
- [ ] **Step 4: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~WatchSetTests"` → PASS.
- [ ] **Step 5: Format, validate, commit** `feat(tool): what the watcher watches, and what a change does`.

### Task 4: The watch session

**Files:**
- Create: `src/SqlSource.Tool/Watching/WatchSession.cs`
- Modify: `src/SqlSource.Tool/DescribeCommand.cs` (split `PlanUnitAsync`; `Bind` runs a `WatchSession` when `Watches(parsed)`)
- Modify: `src/SqlSource.Tool/Cli.cs` (watch exit code, no `RunAgainLine`)
- Modify: `src/SqlSource.Tool/Logging/RunEvents.cs` (`PassStarted`, `PassEnded`)
- Modify: `tests/SqlSource.Tool.Testing/TestClock.cs` (timers), `tests/SqlSource.Tool.Testing/CliRun.cs` (a run that a test watches while it goes)
- Create: `tests/SqlSource.Tool.Tests/FakeFolderWatcher.cs`, `tests/SqlSource.Tool.Tests/WatchScene.cs`
- Test: `tests/SqlSource.Tool.Tests/Commands/WatchTests.cs` (split by subject if it passes about 600 lines), `tests/SqlSource.Tool.Tests/TestClockTests.cs`

**Interfaces:**
- Consumes: Tasks 1 to 3.
- Produces:
  - `WatchSession.ChangeDetectedMarker`, `WatchSession.WatchingMarker` (`internal const string`, texts in Global Constraints); `internal static readonly TimeSpan WatchSession.Debounce = TimeSpan.FromMilliseconds(300)`.
  - `internal static Task WatchSession.RunAsync(RunUnit unit, Usage usage, ParseResult parsed, ToolHost host, Reporter session, Action<LogValue> counted, CancellationToken cancellationToken)`: returns only by `OperationCanceledException`.
  - From `DescribeCommand`, for the session: `internal static RunUnit? FindUnit(Usage usage, ToolHost host, Reporter reporter)`; `internal static Task<ImmutableArray<ProjectManifest>> FindManifestsAsync(RunUnit unit, Usage usage, ToolHost host, Reporter reporter, CancellationToken cancellationToken)`; `internal static Task<RunPlan?> PlanFromAsync(RunUnit unit, ImmutableArray<ProjectManifest> manifests, Usage usage, ToolHost host, Reporter reporter, CancellationToken cancellationToken)` (writes "no project of ... uses SqlSource" as today); `internal static Task DescribePlanAsync(RunPlan plan, ParseResult parsed, Usage usage, RunUnit unit, RunMode mode, ToolHost host, Reporter reporter, Action<LogValue> counted, CancellationToken cancellationToken)`.  A run without `--watch` behaves exactly as before.
  - Events, in `RunEvents`: `pass.start` with `pass` (number, from 1), `changes` (number of changes with an effect since the last pass; 0 for the first) and `evaluated` (bool: the manifest targets ran for this pass); `pass.end` with `pass`, `errors` (the pass reporter's count) and `durationMs` (by `ToolHost.Time`).  Event only, no `--verbose` line.
  - `TestClock`: `CreateTimer` returns timers that fire, on the thread of `Advance`, when `Advance` reaches their time; `public int PendingTimers { get; }`.  A clock no test advances behaves as today.
  - `CliRun.Start(CancellationToken cancellationToken, params string[] args)` → `RunningCli` with `string Output`, `string Errors` (thread-safe reads while the run goes) and `Task<CliResult> Ended`.
  - `FakeFolderWatcher` (the `CliRun.Watch` of a test): records each `WatchRequest` while it is open (`Open`), and `Raise(FileChange)` / `Fail(WatchFailure)` call the handlers of every open request whose folder holds the path.
  - `WatchScene`: a `DescribeScene` with a `FakeFolderWatcher`; `StartAsync(params string[] args)` starts `describe --watch ...` and waits for the first `WatchingMarker`; `ChangeAsync(params FileChange[] changes)` raises them, waits for the session's debounce timer (`Clock.PendingTimers > 0`), advances the clock by `WatchSession.Debounce`, and waits for one more `WatchingMarker`; `StopAsync()` cancels and returns the `CliResult`.  Every wait is `Eventually`.

  The loop, which the tests pin:
  1. `FindUnit`; none → return (the reporter has the error; `Cli` exits `1`).
  2. Pass 1, `Evaluate`, mode `Force` under `--force` else `Describe`; then the watchers of `WatchSet.Folders` (with `Poll` from `DOTNET_USE_POLLING_FILE_WATCHER`), then `WatchingMarker`.
  3. A change's effect is decided when the event arrives, against the `WatchSet` of the last pass, and `None` is dropped.  A `WatchFailure` is `Evaluate` (Task 5 reports it).  Each kept change records `ToolHost.Time.GetTimestamp()` and wakes the loop.
  4. The loop waits until `Debounce` has passed since the last kept change (`Task.Delay(remaining, host.Time, token)`, again while a change came meanwhile), then writes `ChangeDetectedMarker`, takes every pending change and runs a pass: `Evaluate` when a change asks, when the last evaluation reported an error, or when a gone path is still missing (`File.Exists`); else on the cached manifests.  After the pass: the watchers are replaced when `WatchSet.Folders` changed, then `WatchingMarker`.
  5. A pass has its own `Reporter(host.Error, session.Trace)`.  It catches `DescriberFaultException` and reports its diagnostic, and every other exception but an `OperationCanceledException` of the token as `UnexpectedFailure.Of(exception, host.GetEnvironmentVariable)`, as `Cli.RunCommandAsync` does, and the watcher goes on.  No `catch (InvalidOperationException)`.
  6. `Cli`: a command line that `Watches` sets `MayPoint` false, and a watch that was cancelled exits `0`.

- [ ] **Step 1: Write the failing tests** (`WatchTests`, through `Cli.RunAsync` with `FakeFolderWatcher`, `FakeProcessRunner` and `FakeDescriber`; manifest runs are counted from `Processes.Requests` with `FakeProcessRunner.IsManifestRun`):
  - `Watch_Start_DescribesThenWritesTheWatchingMarker`: output is the summary then exactly `WatchingMarker + "\n"`.
  - `Watch_ListedSqlFileWritten_DescribesItWithoutAManifestRun`: rewrite `Users.sql` on disk, `ChangeAsync(Changed Users.sql)`; output gains exactly `ChangeDetectedMarker\n<summary>\nWatchingMarker\n`; the sidecar has the new query; manifest runs unchanged.
  - One test per row of Task 3's table through `ChangeAsync`, asserting a manifest run (`Evaluate`) or none (`Describe`): project file, `Directory.Build.props` above, `obj/project.assets.json`, a new `.sql` under the project, a created folder under the project, a settings file listed, a `Compile` item listed, `.env` written (the next pass uses the new value: `Describer.Opened`).
  - `Watch_SaveByRenameOverAListedFile_RunsNoManifestTarget`: `Renamed Users.sql → Users.sql___jb_old___`, `Renamed Users.sql___jb_tmp___ → Users.sql`, `Deleted Users.sql___jb_old___`, in one `ChangeAsync`, with `Users.sql` on disk: one pass, no manifest run.
  - `Watch_ListedFileDeletedAndStillGone_RunsTheManifestTarget`.
  - `Watch_OwnSidecarWrites_RunNoPass` (Review Focus 1): after a pass, raise the sidecar's `Created`, `Changed` and `Renamed` from its temporary file, then `Changed` of `Users.sql` in one batch: one pass, whose `pass.start` in the log has `changes` `1`.
  - `Watch_BuildOutputsWritten_RunNoPass` (Review Focus 2): the same with 200 changes under `bin/` and `obj/`.
  - `Watch_ChangesWithinTheDebounce_GiveOnePass`: raise, advance 200 ms, raise, advance 200 ms: a timer is pending again and no `ChangeDetectedMarker` was written; advance 100 ms: one pass whose `changes` is `2`.
  - `Watch_ChangesDuringAPass_GiveExactlyOneMorePass` (Review Focus 3): a project-file change makes the pass run a manifest target, held by `ProcessGate`; during it raise five changes of `Users.sql`; release; advance the debounce: exactly one more pass, then no timer pending.
  - `Watch_PassWithAnError_KeepsWatching`: the database has no connection (`SQLSRC213` on standard error), the watcher goes on, and the next pass after the variable is set describes.
  - `Watch_DotEnvDeleted_NextPassReportsNoConnectionAndKeepsWatching` (Review Focus 5).
  - `Watch_ProjectNotRestored_IsReportedEachPassUntilRestored`.
  - `Watch_DescriberThrows_IsSqlsrc200AndKeepsWatching`.
  - `Watch_ErrorInOnePass_DoesNotStopTheNextFromDeletingASidecar`.
  - `Watch_Stopped_ExitsZeroWithoutTheRunAgainLine`: after a pass with an error, cancel: exit `0`, standard error has no `RunAgain` line.
  - `Watch_StoppedDuringAPass_ExitsZeroAndWritesNoSidecar` (cancel while a `ProcessGate` holds the pass).
  - `Watch_UnitNotFound_ExitsOneBeforeAnyPass` (an empty folder: `SQLSRC201`, exit `1`, no marker, no watch opened).
  - `Watch_Force_ForcesTheFirstPassOnly`: describer calls of pass 1 include a current query, of pass 2 only the changed one.
  - `Watch_Filters_NarrowEveryPass`: `--database billing` and a `.sql` path: the second pass describes only what they select.
  - `Watch_Log_IsOneFileForTheSession`: `--log`: `run.start`, `pass.start` (1, 0, true), …, `pass.end`, `pass.start` (2, 1, false), …, `pass.end`, `run.end` with exit code `0`, in that order.
  - `Watch_Verbose_AppliesToEveryPass`.
  - `TestClockTests`: a timer fires when `Advance` reaches it, not before; a disposed timer does not fire.
- [ ] **Step 2: Run them and see them fail.**
- [ ] **Step 3: Implement** the test helpers, the split of `DescribeCommand`, `WatchSession`, the two events and the `Cli` changes.  The `Cli` change is one block of its own in `OpenLog` and `RunAsync`.
- [ ] **Step 4: Run** `dotnet test tests/SqlSource.Tool.Tests` → PASS, three times in a row, to see that no test of the session depends on timing.
- [ ] **Step 5: Format, validate, commit** `feat(tool): describe --watch runs a pass after each change`.

### Task 5: When the watcher fails, and polling

**Files:**
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs` (`WatchFailed`, `SQLSRC287`, in `All`), `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md` (section `SQLSRC287`), `src/SqlSource/Diagnostics/AGENTS.md` (`SQLSRC200` to `SQLSRC287` are taken)
- Modify: `src/SqlSource.Tool/Watching/WatchSession.cs`
- Test: `tests/SqlSource.Tool.Tests/Commands/WatchTests.cs` (or its split), `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs` if it lists ids

**Interfaces:**
- Produces: `ToolDiagnostics.WatchFailed`: id `SQLSRC287`, title `"The watcher failed"`, message `"watching '{0}' for changes failed: {1}"` (the folder, the failure's message), with the continuation line `help: set DOTNET_USE_POLLING_FILE_WATCHER=true to poll for changes instead`; an error, `NotConfigurable`.

- [ ] **Step 1: Write the failing tests**
  - `Watch_WatcherError_IsReportedOnceAndTheNextPassEvaluates`: `Fail(new WatchFailure(folder, false, "The configured user limit (128) on the number of inotify watches has been reached, or the operating system failed to allocate a required resource."))` twice: standard error has the `SQLSRC287` error once with the folder, the message and the help; a pass runs with a manifest run; the watcher goes on, and on stop exits `0`.
  - `Watch_Overflow_IsNotReportedAndTheNextPassEvaluates`.
  - `Watch_PollingVariable_PollsEachFolder`: `DOTNET_USE_POLLING_FILE_WATCHER` `true`, `1`, `TRUE` → every `WatchRequest.Poll` true; `false`, `0`, unset → false.
- [ ] **Step 2: Run them and see them fail.**
- [ ] **Step 3: Implement.**  A failure that is not an overflow is reported once for each distinct folder and message in the session, through the session's reporter, and schedules a pass with `Evaluate`.  The diagnostic's four places per `src/SqlSource/Diagnostics/AGENTS.md`; the manual's section says what it means (the system refused to watch the folder, for example its limit of watched folders on Linux), that the watcher goes on but may miss changes in that folder, and the fix (raise `fs.inotify.max_user_watches`, or set the variable to poll).
- [ ] **Step 4: Run** `dotnet test tests/SqlSource.Tool.Tests` and `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Diagnostics"` → PASS.
- [ ] **Step 5: Format, validate, commit** `feat(tool): report a watcher that fails, and poll on request`.

### Task 6: A watcher against each database

**Files:**
- Modify: `tests/DatabaseTests/Postgres/Describer/DescribeCommandTests.cs`, `tests/DatabaseTests/SqlServer/Describer/DescribeCommandTests.cs`

**Interfaces:**
- Consumes: `CliRun.Start` (Task 4); the real watcher of `Hosts` (Task 2).

- [ ] **Step 1: Write the test** `Watch_SqlFileChanged_RewritesItsSidecar` in each: `_run.Time = TimeProvider.System`; a `.sql` file of one query; `Start` `describe --watch --connection app=<container>`; wait (`Eventually`, the default patience) for the sidecar and the `WatchingMarker`; rewrite the file with another column in the query; wait until the sidecar's entry has the new column; cancel; exit `0`.
- [ ] **Step 2: Run** `dotnet test tests/DatabaseTests/Postgres --filter "FullyQualifiedName~DescribeCommandTests"` and the same for `tests/DatabaseTests/SqlServer` → PASS.  Then each on its floor image: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` and `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` → PASS.
- [ ] **Step 3: Format, validate, commit** `test(database): a watcher rewrites a sidecar against each database`.

### Task 7: Documentation

**Files:**
- Modify: `docs/manual/tool.md` (a section "Describe on save" with the rules of watch mode; the synopsis gains `[--watch]`; the `--watch` row of the options table, in its own row; exit codes: a watcher stopped by Ctrl+C, `SIGTERM` or `SIGHUP` exits `0`; logs: one file for the whole session), `docs/manual/README.md` ("What works today": a row "Describing on save with `describe --watch`", Available, linking to the new section; the "Build integration" row stays 9-2's), `docs/manual/diagnostics.md` checked
- Modify: `docs/architecture/tool.md` (a section "Watch mode" in the body, as built: `ToolHost.Watch`, `WatchSet`, the session's loop, the cache, a reporter per pass, the events; the folder `Watching/` in "Folders"; the placeholder "Build integration options" narrowed to `--manifest` and `--online`)
- Modify: `src/SqlSource.Tool/AGENTS.md` (under "The host": every file event comes through `ToolHost.Watch`; a rule that the markers are a contract with the VS Code recipe and its test), `src/SqlSource.Tool/README.md` if its list of common commands should name `--watch`, `tests/README.md` if it describes where a kind of tool test goes
- Test: the documentation tests of `tests/SqlSource.Tests/Documentation/`

- [ ] **Step 1: Write** the manual's section from the spec's rules, stating effects only: the command; that a pass runs after each save and keeps going after errors; what is watched (a `.sql` file, a settings file, a C# file, a project or solution file, `Directory.Build.*`, a restore, the `.env`); what is ignored; the two markers, word for word; the half second it waits; the options' rules; stopping and the exit code; that two watchers, or a watcher beside a build, do no harm and one is enough; `DOTNET_USE_POLLING_FILE_WATCHER`, which the SDK's container image sets to `true` (set it to `false` to use the system's events); the inotify limit; that only Rider on macOS was checked with an editor's saves.  No internals, no `ToolHost`, no tech-debt id.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Documentation"` → PASS.
- [ ] **Step 3: Format, run** `./pre-commit-validation.sh --full` → PASS, **commit** `docs: describe --watch in the manual and the architecture`.
