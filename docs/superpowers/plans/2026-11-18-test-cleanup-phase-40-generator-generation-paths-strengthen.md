# Test cleanup phase 40: generator generation paths strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Give `SqlPathTests.IsRooted_PathFromARoot_IsTrue` the drive letters `a`, `z` and `A`, and list the files of `SqlPathTests.FindRepeated_SeveralRepeatedPaths_AreInTheOrderOfTheSortedPaths` so that the later path repeats first, so that the four survivors the findings name are killed.

**Architecture:** Three `InlineData` rows are added and one test's file list is reordered; there is no source change.  The guard is the `generator-generation-paths` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Paths/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-40-generator-generation-paths-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Add only the rows `"a:/app/Users.sql"`, `"z:/app/Users.sql"` and `"A:\\app\\Users.sql"` to `IsRooted_PathFromARoot_IsTrue`, and keep its other rows.
- List the files of `FindRepeated_SeveralRepeatedPaths_AreInTheOrderOfTheSortedPaths` as `c/C.sql, b/B.sql, c/C.sql, b/b.sql, a/A.sql`, and keep its expected `b/B.sql, c/C.sql`.
- The epic's phase table: change only row 40.

## Review Focus

None: the change adds three rows and reorders one list.  Checked against the spec on `073af753`.  `IsAsciiLetter` is still `SqlPath.cs:33`, taking `a` to `z` and `A` to `Z`, and `FindRepeated` still adds a path at its second file and sorts at `SqlPath.cs:246`.  Both findings hold.  Neither test branches on its row, so TD-0089 is unchanged.  No change to the scope's source or tests merged since `0e5e4467`, so p25's "after" report is this phase's "before".

---

### Task 0: The guard's "before" half

- [x] p25's "after" report copied to `artifacts/test-cleanup/phase-40/generator-generation-paths/before/reports/mutation-report.json`.
- [x] `kills` on the before report for both tests: `SqlPath.cs:33:56`, `33:67`, `33:79` and `246:13` survive.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-40/coverage-before/SqlSource.Tests`: each file of `src/SqlSource/Generation/Paths/**/*.cs` is at 100%.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Paths/SqlPathTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 40's state)

- [x] **Step 1:** Add the three rows to `IsRooted_PathFromARoot_IsTrue` (F-SqlSource.Tests.Generation-2-10).
- [x] **Step 2:** Reorder the files of `FindRepeated_SeveralRepeatedPaths_AreInTheOrderOfTheSortedPaths` (F-SqlSource.Tests.Generation-2-11).
- [x] **Step 3:** Set row 40 of the epic's table to `done`.
- [x] **Step 4:** `./format.sh`, then the Paths tests pass.
- [x] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST` and nothing `NOT COMPARABLE`, and the four survivors are `Killed` in the after report.  `compare` lists `SqlPath.cs:219:13` (`Array.Clear` removed) `LOST`: in the "before" report only `FindRepeated_PathListedTwiceOrUnderTwoSpellings_IsThatPathOnce` and `FindRepeated_PathListedOnce_IsEmpty`, which this phase does not change, killed it, and a rerun on the unchanged test file loses it too, so it is a kill that did not repeat.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-40/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Paths/**/*.cs` falls.
- [x] **Step 3:** The pull request gives the changes, the compare result, the coverage figures and the run times.
