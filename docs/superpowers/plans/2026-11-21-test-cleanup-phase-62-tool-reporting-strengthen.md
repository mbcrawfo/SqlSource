# Test cleanup phase 62: tool reporting strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make three tests of `NearNameTests` fail on what their findings name: a length gate that turns away a difference of 2, a swap of neighbours that stops counting as one edit, a first row of distances left at zero, a given name's last character that is dropped, and a comparison that ignores case and is never reached.

**Architecture:** One test file, no source or helper changes.  The guard is the `tool-reporting` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Reporting/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-62-tool-reporting-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the three tests of the findings change.
- The epic's phase table: change only row 62.

Checked against the spec on `a77d0352`: `NearName.IsNear` still has the length gate at line 23, the first row at 33-36, the loop over the given name at 38 and the guard of the swap at 46-49, and `Same` still ignores case at 65.  The six survivors the findings name are `Survived` in the "before" report, so the three findings hold.  No phase has run this scope before, so the "before" is a run of its own.  The spec names no document to update, and none names a changed test.

## Review Focus

Each new row is the one its finding proposes.  `IsNear_UnrelatedName_IsFalseWhenCaseIsIgnored` keeps its assertion and changes its names to two of equal length, so the comparison runs.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-62/tool-reporting/before` (Windows): 100 mutants tested, 81 killed, 19 survived, 0 timeouts, 0 errors, 7 without coverage; the tested run took 1 min 0 s.  `kills`: `IsNear_NameWithinTheDistance_IsTrue` 6 rows, 35 kills, 3 alone, 7 survivors under it; `IsNear_NameBeyondTheDistance_IsFalse` 5 rows, 29 kills, 1 alone, 7 survivors under it; `IsNear_UnrelatedName_IsFalseWhenCaseIsIgnored` 1 row, 1 kill, 0 alone, 1 survivor under it.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-62/coverage-before/SqlSource.Tool.Tests` (7,090 tests, 19 skipped, 2 min 54 s): 183 of 183 lines over the 6 files of `src/SqlSource.Tool/Reporting/`.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Reporting/NearNameTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 62's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests-4-01):** `IsNear_NameWithinTheDistance_IsTrue` gains the rows `("--cnonectoin", "--connection")` and `("--for", "--force")`.
- [x] **Step 2 (F-SqlSource.Tool.Tests-4-02):** `IsNear_NameBeyondTheDistance_IsFalse` gains the rows `("-x", "-abc")` and `("--forxxx", "--force")`.
- [x] **Step 3 (F-SqlSource.Tool.Tests-4-03):** `IsNear_UnrelatedName_IsFalseWhenCaseIsIgnored` compares `"Host"` with `"user"`.  Checked by hand with `Same` changed to `left == right || ignoreCase`: the test fails with the new names and passes with the old ones.
- [x] **Step 4:** Set row 62 of the epic's table to `done`.
- [x] **Step 5:** `./format.sh`, `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-62/tool-reporting/after` (Windows): 100 mutants tested, 88 killed, 12 survived, 0 timeouts, 0 errors, 7 without coverage; the tested run took 1 min 0 s.  `compare`: 81 detected before, 81 still detected, 0 lost, 0 not comparable.  All seven survivors at the six places the findings name are now `Killed`: `NearName.cs:23:13` `>=`, `33:25` `>` and `<`, `34:9` block removal, `38:25` `<`, `46:21` `i < 1` and `47:24` `j < 1`.  `kills` after: `IsNear_NameWithinTheDistance_IsTrue` 8 rows, 39 kills, 6 alone; `IsNear_NameBeyondTheDistance_IsFalse` 7 rows, 34 kills, 5 alone; `IsNear_UnrelatedName_IsFalseWhenCaseIsIgnored` 1 row, 29 kills, 0 alone; none has a survivor under it.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-62/coverage-after/SqlSource.Tool.Tests` (7,094 tests, 19 skipped, 3 min 36 s, against 7,090 tests in 2 min 54 s before): 183 of 183 lines, before and after, the same figure in each of the 6 files.
- [ ] **Step 3:** The pull request gives the change, the compare result, the coverage figures and the run times.
