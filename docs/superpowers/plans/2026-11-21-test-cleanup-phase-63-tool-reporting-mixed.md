# Test cleanup phase 63: tool reporting mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove a test of `Reporter.Count` that two other tests already prove, and make `Report_TextWithLineBreaks_StaysOnItsLine` fail when a text that starts with a line break is written as it is.

**Architecture:** One test file, no source or helper changes.  The guard is the `tool-reporting` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Reporting/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-63-tool-reporting-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the two tests of the findings change.
- The epic's phase table: change only row 63.

Checked against the spec on `67408613`: `OneLine.Of` still tests `IndexOfAny('\r', '\n') < 0` at line 16, and its `<= 0` mutant is `Survived` in the "before" report.  `Count_AfterTwoErrors_IsTwo` still asserts 0 and then 2, and `Report_FromManyThreads_KeepsEveryErrorWholeAndCountsIt` and `Report_Sqlsrc219_IsCountedAsAnErrorAndAsADifference` still assert the count from a new reporter, so both findings hold.  Phase 62 changed this scope's tests just before this phase, and the "before" is a run of its own on the merged result.  The spec names no document to update, and none names a changed test.

## Review Focus

The new continuation line of `Report_TextWithLineBreaks_StaysOnItsLine` is the text the finding proposes, `"\nsecond"`.  Its label's colon and space come before the space the line break becomes, so the expected line has two spaces: `    help:  second`.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-63/tool-reporting/before` (Windows): 100 mutants tested, 88 killed, 12 survived, 0 timeouts, 0 errors, 7 without coverage; the tested run took 1 min 2 s.  `kills`: `Report_TextWithLineBreaks_StaysOnItsLine` 1 row, 15 kills, 0 alone, 12 survivors under it; `Count_AfterTwoErrors_IsTwo` 1 row, 3 kills, 0 alone, 0 timeouts under it, 10 survivors under it.  Its three kills, `Reporter.cs:64:13` block removal and `131:13` statement and `_count--`, are each killed by both of the tests the finding names as cover.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-63/coverage-before/SqlSource.Tool.Tests` (7,096 tests, 19 skipped, 2 min 58 s): 183 of 183 lines over the 6 files of `src/SqlSource.Tool/Reporting/`.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Reporting/ReporterTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 63's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests-4-05):** Delete `Count_AfterTwoErrors_IsTwo`.  It used no helper of its own.
- [x] **Step 2 (F-SqlSource.Tool.Tests-4-04):** `Report_TextWithLineBreaks_StaysOnItsLine` gains the continuation line `("help", "\nsecond")` and expects `    help:  second`.
- [x] **Step 3:** Set row 63 of the epic's table to `done`.
- [x] **Step 4:** `./format.sh`.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-63/tool-reporting/after` (Windows): 100 mutants tested, 89 killed, 11 survived, 0 timeouts, 0 errors, 7 without coverage; the tested run took 1 min 2 s.  `compare`: 88 detected before, 88 still detected, 0 lost, 0 not comparable.  The survivor the finding names, `OneLine.cs:16:9` `<= 0`, is now `Killed`.  `kills` after: `Report_TextWithLineBreaks_StaysOnItsLine` 1 row, 16 kills, 1 alone, 11 survivors under it.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-63/coverage-after/SqlSource.Tool.Tests` (7,095 tests, 19 skipped, 2 min 56 s, against 7,096 tests in 2 min 58 s before): 183 of 183 lines, before and after, the same figure in each of the 6 files.
- [ ] **Step 3:** The pull request gives the change, the compare result, the coverage figures, the removed test beside its cover and the run times.
