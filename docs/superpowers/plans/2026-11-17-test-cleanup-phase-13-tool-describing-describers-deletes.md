# Test cleanup phase 13: tool describing describers deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the two tests of `tests/SqlSource.Tool.Tests/Describing/Describers` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** Two test deletions, no source change.  The guard is the `tool-describing-describers` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Describing/Describers/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-13-tool-describing-describers-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `DescriberSessionTests.ServerOf_NoVersion_Throws` and `TableMatchChecksTests.OfTheTable_CheckThatFails_RunsNoCheckAfterIt`.
- The covers keep every assertion: `DescriberSessionTests.OpenAsync_AnswerWithNoVersion_ThrowsAndDisposesTheConnection`, `TableMatchChecksTests.OfTheTable_EachCheckThatFailsWithTheOnesBeforeItPassing_IsItsOwnValue` and `TableMatchChecksTests.OfTheTable_EveryCheckFails_IsNamesDiffer`.
- No helper is removed: `TestSession.ServerOfVersion` stays (the opening of `TestSession` calls it), and `Passes`, `Fails` and `MustNotRun` have other callers.
- The epic's phase table: change only row 13.

## Review Focus

None: the change removes two tests and adds no behaviour.  Checked against the spec on `ae781af6`: no commit since the review's touched `src/SqlSource.Tool/Describing/Describers` or `tests/SqlSource.Tool.Tests/Describing/Describers`, so both findings read as the review wrote them, and every cover exists.  No document outside `docs/superpowers/` names either test or its class.

---

### Task 0: The guard's "before" half

- [x] The `tool-describing-describers` mutation run into `artifacts/test-cleanup/phase-13/tool-describing-describers/before`.
- [x] `kills` on the before report for both tests.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-13/coverage-before/SqlSource.Tool.Tests`, with the line figures of each file of `src/SqlSource.Tool/Describing/Describers/**/*.cs`.

### Task 1: Delete the tests

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberSessionTests.cs` (remove `ServerOf_NoVersion_Throws`)
- Modify: `tests/SqlSource.Tool.Tests/Describing/Describers/TableMatchChecksTests.cs` (remove `OfTheTable_CheckThatFails_RunsNoCheckAfterIt`)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 13's state)

- [x] **Step 1:** Remove each test with its attribute.
- [x] **Step 2:** Set row 13 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tool.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows no removed test made.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-13/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/Describing/Describers/**/*.cs` falls.
- [x] **Step 3:** The pull request lists each removed test beside its covers, the compare result, the coverage figures and the run times.
