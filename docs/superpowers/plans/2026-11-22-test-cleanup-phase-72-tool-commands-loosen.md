# Test cleanup phase 72: tool commands loosen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop six watch tests from failing when the same folders are watched in another order, while a missing folder, or a folder closed before the new ones open, still fails them.

**Architecture:** Ten assertions in two test classes take `ignoreOrder: true`, with no source or helper changes.  The guard is the `tool-root` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/*.cs`, and, since the subject is outside that scope, a hand check of `WatchSession.Watch`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-72-tool-commands-loosen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the assertions of the six findings change.
- The epic's phase table: change only row 72.

Checked against the spec on `61b6ac47`: each of the ten assertions still compares the folders as an ordered list, `WatchSet.Folders` is still sorted by ordinal and `FakeFolderWatcher.Open` is still in the order of opening, so every finding holds.  The spec names no document to update, and none names these tests.

## Review Focus

- A folder missing from the set still fails each of the six tests: `ignoreOrder: true` keeps the exact members and their count.
- A folder that leaves the set and is closed before the new ones open still fails `Watch_FolderThatLeavesTheSet_IsClosedAfterTheNewOnesAreOpen`, through `openWhenAsked[otherFolder]`.
- A folder watched twice still fails: the lists keep their counts, and `Requests.Count` stays in that test.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-72/tool-root/before` (Windows): 571 mutants tested, 429 killed, 0 survived, 142 timeouts, 110 compile errors and 110 ignored in the report; the tested run took 1 h 10 min 4 s.
- [x] `kills` on the "before" report, each test 1 row, 0 alone, 0 survivors under it: `Watch_FolderThatAppearsLater_IsWatchedByTheNextEvaluation` 16 kills and 67 timeouts under it, `Watch_SaveInAFolderTheEvaluationAddedDuringThatPass_GivesOneMorePass` 17 and 75, `Watch_FolderThatLeavesTheSet_IsClosedAfterTheNewOnesAreOpen` 20 and 67, `Watch_EvaluationThatKeepsAFolder_KeepsItsWatch` 8 and 67, `Watch_PolledFolderMadeInAFolderWatchedAlone_IsWatched` 11 and 75, `Watch_PolledFolderDeletedBelowAFolderWatchedAlone_ReportsNoError` 11 and 75.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-72/coverage-before/SqlSource.Tool.Tests` (7,223 tests, 19 skipped, 2 min 55 s): 1,216 of 1,224 lines over the 14 files of `src/SqlSource.Tool/*.cs` that have executable lines.
- [x] Run time of the test project, without coverage: 2 min 30 s.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Commands/Watch/WatchDuringAPassTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Commands/Watch/WatchFolderTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 72's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests.Commands-2-09):** `Watch_FolderThatAppearsLater_IsWatchedByTheNextEvaluation` compares its two lists of open folders with `ignoreOrder: true`.
- [x] **Step 2 (2-10):** `Watch_SaveInAFolderTheEvaluationAddedDuringThatPass_GivesOneMorePass` does the same for its two.
- [x] **Step 3 (2-11):** `Watch_FolderThatLeavesTheSet_IsClosedAfterTheNewOnesAreOpen` does the same for its three, and keeps `Requests.Count.ShouldBe(4)`.
- [x] **Step 4 (2-12):** `Watch_EvaluationThatKeepsAFolder_KeepsItsWatch` does the same for the first open folders, and keeps its comparisons with `first`.
- [x] **Step 5 (2-13, 2-14):** `Watch_PolledFolderMadeInAFolderWatchedAlone_IsWatched` and `Watch_PolledFolderDeletedBelowAFolderWatchedAlone_ReportsNoError` do the same for their list of three pollers.
- [x] **Step 6:** Set row 72 of the epic's table to `done`, and run `./format.sh`.

### Task 2: The guard's "after" half

- [x] **Step 1:** A hand check of the six tests, with `WatchSession.Watch` changed and put back.  Unchanged, 6 pass.  With the folders opened in reverse order, 6 pass.  With the first folder of the set not watched, all 6 fail.  With a folder that left the set closed before the new ones open, `Watch_FolderThatLeavesTheSet_IsClosedAfterTheNewOnesAreOpen` fails and the other 5 pass.
- [x] **Step 2:** The mutation run with output `artifacts/test-cleanup/phase-72/tool-root/after` (Windows): 571 mutants tested, 429 killed, 0 survived, 142 timeouts, 110 compile errors and 110 ignored in the report; the tested run took 1 h 12 min 51 s.  `compare`: 571 detected before, 571 still detected, 0 lost, 0 not comparable.  `kills` after, each test 1 row, 0 alone, 0 survivors under it, with the timeouts under it as before: the four tests of `WatchDuringAPassTests` have the kills they had, and the two of `WatchFolderTests` have 26 each against 11, which is how the run credited kills that other tests also make.
- [x] **Step 3:** Coverage into `artifacts/test-cleanup/phase-72/coverage-after/SqlSource.Tool.Tests` (7,223 tests, 19 skipped, 3 min 8 s, against 2 min 55 s before): 1,216 of 1,224 lines, before and after, the same figure in each of the 14 files.
- [x] **Step 4:** The test project's run time, on the same machine: 2 min 41 s, against 2 min 30 s before.
- [ ] **Step 5:** The pull request gives the change, the compare result, the coverage figures and the run times.  The phase removes no test or row.
