# Test cleanup phase 35: generator generation chains strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `BuildFoldersTests.Of_OtherPercentSign_IsKept` a row whose `%` stands just before the entry's last separator, so that the two mutants of `BuildFolders.cs:135:36` that survive today are killed.

**Architecture:** One `InlineData` row is added and there is no source change.  The guard is the `generator-generation-chains` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Chains/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-35-generator-generation-chains-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Add only the row `[InlineData("/repo/a%/", "repo/a%")]` to `Of_OtherPercentSign_IsKept`, and keep its other rows.
- The epic's phase table: change only row 35.

## Review Focus

None: the change adds one row.  Checked against the spec on `8cb887d0`.  `BuildFolders.Unescape` still reads `entry[i + 1]` and `entry[i + 2]` only when `i + 2 < entry.Length`, and `FolderOf` still unescapes only an entry that is rooted and ends with a separator.  The finding holds.  The theory does not branch on its row, so TD-0089 is unchanged.  No earlier phase ran the `generator-generation-chains` scope, so this phase runs its own "before".

---

### Task 0: The guard's "before" half

- [x] The mutation run with output `artifacts/test-cleanup/phase-35/generator-generation-chains/before`.
- [x] `kills` on the before report for `BuildFoldersTests.Of_OtherPercentSign_IsKept`.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-35/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/Chains/**/*.cs`.

### Task 1: Add the row

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Chains/BuildFoldersTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 35's state)

- [x] **Step 1:** Add `[InlineData("/repo/a%/", "repo/a%")]` to `Of_OtherPercentSign_IsKept`.
- [x] **Step 2:** Set row 35 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then the Chains tests pass.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST` and nothing `NOT COMPARABLE`, and `BuildFolders.cs:135:36` is `Killed` in the after report.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-35/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Chains/**/*.cs` falls.
- [x] **Step 3:** The pull request gives the added row, the compare result, the coverage figures and the run times.
