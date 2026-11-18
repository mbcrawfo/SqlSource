# Test cleanup phase 36: generator generation chains mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete three tests of `tests/SqlSource.Tests/Generation/Chains` whose behaviour other tests hold, and strengthen two so that the survivors `SettingsFileSet.cs:108:62` and `ListedSettingsFile.cs:47:16` are killed.

**Architecture:** Test-only change: three `[Fact]`s removed, one settings file added to a fact's set, one `InlineData` row added; no source change.  The guard is the `generator-generation-chains` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Chains/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-36-generator-generation-chains-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Delete only `ListedSettingsFileTests.Read_InvalidDatabase_IsReportedByTheToolAndNotByTheBuild`, `SettingsFileSetTests.ChainOf_FileThatIsNotJson_IsPassedThrough` and `SettingsFileSetTests.ChainOf_PathInAnotherCaseOrWithBackslashes_FindsTheChain`, and any helper only they used.
- The covers keep every row and assertion: the `Database` row of `Read_InvalidValue_IsReportedByTheSidesThatReadItsSetting`, `ChainOf_FileEditedToTextThatIsNotJson_PassesThrough`, `ChainOf_FoldersNotInAscii_AreFoundInTheOrderTheyAreSorted` and `Read_PathWithBackslashes_IsReadAtItsNormalisedFolder`.
- The epic's phase table: change only row 36.

## Review Focus

None: three deletions, one added file in a set and one added row.  Checked against the spec on `240e65fc`.  `ListedSettingsFile.IsSettingsFile` still reads `before < 0 || path[before] is '/' or '\\'` (line 47), and `SettingsFileSet.Reached` still ends a chain with `-1` at a root file (line 108); the "before" report lists both mutants `SURVIVED`, and `kills` gives the deleted tests 19, 37 and 30 kills, none alone, as the spec measured.  `NotJson` stays, used by `Reached_FileBeyondEveryRoot_IsNotReached`.  No touched theory branches on its row, so TD-0089 is unchanged.  No document names a test of the phase; the lines of `src/SqlSource/Generation/Chains/AGENTS.md` and `tests/AGENTS.md` that name `SettingsFileSetTests` stay true.  p35's "after" report does not name its tree, so this phase runs its own "before".

---

### Task 0: The guard's "before" half

- [x] The mutation run with output `artifacts/test-cleanup/phase-36/generator-generation-chains/before`.
- [x] `kills` on the before report for the five tests the phase removes or changes.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-36/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/Chains/**/*.cs`.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Chains/ListedSettingsFileTests.cs`
- Modify: `tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 36's state)

- [x] **Step 1:** Delete `Read_InvalidDatabase_IsReportedByTheToolAndNotByTheBuild` (F-SqlSource.Tests.Generation-1-07).
- [x] **Step 2:** Delete `ChainOf_FileThatIsNotJson_IsPassedThrough` (F-SqlSource.Tests.Generation-1-08) and `ChainOf_PathInAnotherCaseOrWithBackslashes_FindsTheChain` (F-SqlSource.Tests.Generation-1-09); drop a `using` only they needed.
- [x] **Step 3:** Add `("/a/sqlsource.json", "{}")` to the set of `Reached_FileBeyondEveryRoot_IsNotReached`, so the root `app` sorts after `a` (F-SqlSource.Tests.Generation-1-01).
- [x] **Step 4:** Add `[InlineData("xsqlsource.json")]` to `Read_FileNotNamedSettingsFile_IsNull` (F-SqlSource.Tests.Generation-1-03).
- [x] **Step 5:** Set row 36 of the epic's table to `done`.
- [x] **Step 6:** `./format.sh`, then the Chains tests pass.
- [x] **Step 7:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST` and nothing `NOT COMPARABLE`, and `SettingsFileSet.cs:108:62` and `ListedSettingsFile.cs:47:16` are `Killed` in the after report.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-36/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Chains/**/*.cs` falls.
- [x] **Step 3:** The pull request gives each removed test beside its cover, the added file and row, the compare result, the coverage figures and the run times.
