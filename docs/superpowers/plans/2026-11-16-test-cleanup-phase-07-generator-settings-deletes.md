# Test cleanup phase 07: generator settings deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the one test of `tests/SqlSource.Tests/Settings` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** One test deletion, no source change.  The guard is the `generator-settings` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Settings/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-07-generator-settings-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `ColumnTypeMapTests.Find_HigherPlaceWins_OverASchemaBelow`.
- The cover, `ColumnTypeMapTests.Find_HeaderWins_OverASchemaInASettingsFile`, keeps its comment and every assertion.
- `TestPlaces.RootSettingsFile` stays: other tests use it.
- The epic's phase table: change only row 07.

## Review Focus

None: the change removes a test and adds no behaviour.  Checked against the spec on `65e20361`: since the review's commit only `src/SqlSource/Settings/AGENTS.md` changed under the scope's folders, the finding reads as the review wrote it, and the cover exists.  No document names the test or its class.

---

### Task 0: The guard's "before" half

- [x] The `generator-settings` mutation run into `artifacts/test-cleanup/phase-07/generator-settings/before`.
- [x] `kills` on the before report for `ColumnTypeMapTests.Find_HigherPlaceWins_OverASchemaBelow`.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-07/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Settings/**/*.cs`.

### Task 1: Delete the test

**Files:**
- Modify: `tests/SqlSource.Tests/Settings/ColumnTypes/ColumnTypeMapTests.cs` (remove `Find_HigherPlaceWins_OverASchemaBelow`)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 07's state)

- [x] **Step 1:** Remove the test with its attribute.
- [x] **Step 2:** Set row 07 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the removed test did not make.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-07/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Settings/**/*.cs` falls.
- [x] **Step 3:** The pull request lists the removed test beside its cover, the compare result and the coverage figures.
