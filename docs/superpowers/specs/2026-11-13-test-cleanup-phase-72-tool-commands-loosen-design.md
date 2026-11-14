# Test cleanup phase 72: tests/SqlSource.Tool.Tests/Commands, loosening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Commands`
- Findings: 6 (6 loosen)
- Kind: one kind, loosen
- Changed lines: about 60
- Guard: mutation (tool-root), with covers and run time for 6 findings

## Files

- `tests/SqlSource.Tool.Tests/Commands/Watch/WatchDuringAPassTests.cs`
- `tests/SqlSource.Tool.Tests/Commands/Watch/WatchFolderTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Commands-2-09

- test: `tests/SqlSource.Tool.Tests/Commands/Watch/WatchDuringAPassTests.cs` | `WatchDuringAPassTests.Watch_FolderThatAppearsLater_IsWatchedByTheNextEvaluation` | rows: all
- action: loosen
- evidence: read (WatchDuringAPassTests.cs:427 and :432 assert the open watches as an ordered list, `[root, app]` and `[root, app, shared]`; which folders are watched is the behaviour, and the order is that of `WatchSet.Folders`' ordinal sort (src/SqlSource.Tool/Watching/WatchSet.cs:345-350, sorted for the spelling of a path, not for an order of watches) and of the fake's appends.  Opening the new folders in another order, or sorting the set by depth, breaks the test with the same folders watched; `WatchTests.Watch_Start_DescribesThenWritesTheWatchingMarker` and `WatchEndingTests.Watch_StoppedDuringAPass_ExitsZeroAndWritesNoSidecar` assert the same lists with `ignoreOrder: true`.  The subject, src/SqlSource.Tool/Watching, is outside the tool-root scope, so the numbers, kills 17, say nothing of it); blind: no
- cover: none
- proposal: Pass `ignoreOrder: true` to the two assertions of the open folders.
- verdict: confirmed
- verifier's reason: WatchDuringAPassTests.cs:427 and :432 compare `Watcher.Open` folders as ordered lists; `FakeFolderWatcher.Open` (tests/SqlSource.Tool.Tests/FakeFolderWatcher.cs:18-28) is in the order of opening, and the first set is ordered by `WatchSet`'s ordinal sort (src/SqlSource.Tool/Watching/WatchSet.cs:345-350), so the order is incidental to which folders are watched.  `WatchTests.cs:43`, `WatchEndingTests.cs:182` and `WatchFolderTests.cs:101` assert the same kind of list with `ignoreOrder: true`.  That a kept folder is not reopened is held by `Watch_EvaluationThatKeepsAFolder_KeepsItsWatch`, not by this order.

### F-SqlSource.Tool.Tests.Commands-2-10

- test: `tests/SqlSource.Tool.Tests/Commands/Watch/WatchDuringAPassTests.cs` | `WatchDuringAPassTests.Watch_SaveInAFolderTheEvaluationAddedDuringThatPass_GivesOneMorePass` | rows: all
- action: loosen
- evidence: read (WatchDuringAPassTests.cs:342 and :351 assert the open folders in order, `[root, app]` and `[root, app, shared]`; the order is the set's ordinal sort and the order of opening, incidental to which folders are watched, as F-…-09 says; the rest of the test, the passes and the count of requests for `app`, holds whatever the order.  The subject is outside the tool-root scope); blind: no
- cover: none
- proposal: Pass `ignoreOrder: true` to the two assertions of the open folders.
- verdict: confirmed
- verifier's reason: WatchDuringAPassTests.cs:342 and :351 assert the open folders in order; the order is the sort of the first set and then the order of opening, and the test's other assertions, the passes (:357, :361) and the count of requests for `app` (:359), which says the shared folder kept its watch, hold whatever the order.

### F-SqlSource.Tool.Tests.Commands-2-11

- test: `tests/SqlSource.Tool.Tests/Commands/Watch/WatchDuringAPassTests.cs` | `WatchDuringAPassTests.Watch_FolderThatLeavesTheSet_IsClosedAfterTheNewOnesAreOpen` | rows: all
- action: loosen
- evidence: read (WatchDuringAPassTests.cs:403, :409 and :410 assert ordered lists; the behaviour the name promises is that `shared` was still open when `other` was asked for, which `openWhenAsked[otherFolder]` containing `sharedFolder` shows, and that the final set is `root`, `app` and `other`; the order within each list is the set's sort and the fake's appends.  The subject is outside the tool-root scope); blind: no
- cover: none
- proposal: Compare the three lists with `ignoreOrder: true`, keeping the count of requests at :411, which says that no folder was watched twice.
- verdict: confirmed
- verifier's reason: WatchDuringAPassTests.cs:403, :409 and :410 compare ordered lists.  The rule of the name, that `shared` is closed only after `other` is opened, is shown by `openWhenAsked[otherFolder]` holding `sharedFolder` (:395-400, :410), which a set comparison keeps; the final set and the count of requests at :411 hold with `ignoreOrder: true`, and the order within each list is the fake's appends over the set's sort.

### F-SqlSource.Tool.Tests.Commands-2-12

- test: `tests/SqlSource.Tool.Tests/Commands/Watch/WatchDuringAPassTests.cs` | `WatchDuringAPassTests.Watch_EvaluationThatKeepsAFolder_KeepsItsWatch` | rows: all
- action: loosen
- evidence: read (WatchDuringAPassTests.cs:372 asserts the first open folders as the ordered list `[Above, Folder.Path]`, where `WatchTests.Watch_Start_DescribesThenWritesTheWatchingMarker` asserts the same two with `ignoreOrder: true`; the assertions at :376-377, that the requests and the open watches are the very ones of the first evaluation, are the behaviour and stay as they are.  The subject is outside the tool-root scope); blind: no
- cover: none
- proposal: Pass `ignoreOrder: true` to the assertion at :372.
- verdict: confirmed
- verifier's reason: WatchDuringAPassTests.cs:372 asserts `[Above, Folder.Path]` in order, the same two folders `WatchTests.cs:43` asserts with `ignoreOrder: true`; the behaviour of the name is held at :376-377, which compare the requests and the open watches with the very list of the first evaluation, and those stay as they are.

### F-SqlSource.Tool.Tests.Commands-2-13

- test: `tests/SqlSource.Tool.Tests/Commands/Watch/WatchFolderTests.cs` | `WatchFolderTests.Watch_PolledFolderMadeInAFolderWatchedAlone_IsWatched` | rows: all
- action: loosen
- evidence: read (WatchFolderTests.cs:347 asserts that the pollers were made in the order `[root, libs, shared]`; that `libs` and `shared` got a poller by the pass the change started is the behaviour, and the order is that of the set's sort; the test's own `PollerOf` looks a poller up by its folder.  The subject is outside the tool-root scope); blind: no
- cover: none
- proposal: Pass `ignoreOrder: true` to the assertion at :347.
- verdict: confirmed
- verifier's reason: WatchFolderTests.cs:347 compares `FoldersOf(polled)`, an append-only list of the pollers made (:463-483), with `[root, libs, shared]` in order: root from the first pass, then libs and shared from the set's sort.  The behaviour is that libs and shared got a poller from the pass the change started, which a set comparison of the same three, counts included, keeps; `PollerOf` (:485-488) looks a poller up by its folder, so nothing depends on the order.

### F-SqlSource.Tool.Tests.Commands-2-14

- test: `tests/SqlSource.Tool.Tests/Commands/Watch/WatchFolderTests.cs` | `WatchFolderTests.Watch_PolledFolderDeletedBelowAFolderWatchedAlone_ReportsNoError` | rows: all
- action: loosen
- evidence: read (WatchFolderTests.cs:366 asserts the precondition that `root`, `libs` and `shared` are polled as an ordered list; the order is the set's sort and is no part of what the test is about, a deletion whose failed listing is no error.  The subject is outside the tool-root scope); blind: no
- cover: none
- proposal: Pass `ignoreOrder: true` to the assertion at :366.
- verdict: confirmed
- verifier's reason: WatchFolderTests.cs:366 asserts the precondition `[root, libs, shared]` in the set's sort order; the test is about a failed listing below a folder watched alone being no error (:372-375), and nothing after it depends on the order of the pollers.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-root`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-72/tool-root/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Commands.|FullyQualifiedName~SqlSource.Tool.Tests.OutcomeTests.|FullyQualifiedName~SqlSource.Tool.Tests.ToolHostTests.|FullyQualifiedName~SqlSource.Tool.Tests.UsageValueTests.' '*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-72/tool-root/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Commands.|FullyQualifiedName~SqlSource.Tool.Tests.OutcomeTests.|FullyQualifiedName~SqlSource.Tool.Tests.ToolHostTests.|FullyQualifiedName~SqlSource.Tool.Tests.UsageValueTests.' '*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-72/tool-root/before/reports/mutation-report.json artifacts/test-cleanup/phase-72/tool-root/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-72/tool-root/before/reports/mutation-report.json WatchDuringAPassTests.Watch_FolderThatAppearsLater_IsWatchedByTheNextEvaluation
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-72/tool-root/before/reports/mutation-report.json WatchDuringAPassTests.Watch_SaveInAFolderTheEvaluationAddedDuringThatPass_GivesOneMorePass
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-72/tool-root/before/reports/mutation-report.json WatchDuringAPassTests.Watch_FolderThatLeavesTheSet_IsClosedAfterTheNewOnesAreOpen
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-72/tool-root/before/reports/mutation-report.json WatchDuringAPassTests.Watch_EvaluationThatKeepsAFolder_KeepsItsWatch
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-72/tool-root/before/reports/mutation-report.json WatchFolderTests.Watch_PolledFolderMadeInAFolderWatchedAlone_IsWatched
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-72/tool-root/before/reports/mutation-report.json WatchFolderTests.Watch_PolledFolderDeletedBelowAFolderWatchedAlone_ReportsNoError
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-root`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-72/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-72/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-72/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-72/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-72/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-72/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tool.Tests.Commands-2-09, F-SqlSource.Tool.Tests.Commands-2-10, F-SqlSource.Tool.Tests.Commands-2-11, F-SqlSource.Tool.Tests.Commands-2-12, F-SqlSource.Tool.Tests.Commands-2-13, F-SqlSource.Tool.Tests.Commands-2-14.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tool.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
