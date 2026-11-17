# Test cleanup phase 09: tool root deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the two tests at the root of `tests/SqlSource.Tool.Tests` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** Two test deletions, no source change.  The guard is the `tool-root` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/*.cs`, and, for the test of the test library's `TestProject`, which mutation cannot guard, its named cover and the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-09-tool-root-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `UsageValueTests.NamedAsPath_PrintableToken_IsTheFullPathInQuotes` and `TestProjectTests.Manifest_ProjectWithSettingsAndASettingsFileOfAFolder_ListsTheProjectsFileFirst`.
- The covers, `UsageValueTests.NamedAsPath_PrintableTokenInAFolderWithAnEqualsSign_IsTheFullPathInQuotes` and `TestProjectTests.Manifest_ProjectWithFilesAndSettings_IsReadByTheToolsReader`, keep every assertion.  The deleted test's comment, "As the build lists the files of the farther folders first", explains the order the cover's `SettingsFiles` assertion checks, so it moves onto that assertion.
- F-SqlSource.Tool.Tests-1-02 takes the proposal's first option, deleting the test; the cover keeps its order assertion.
- The epic's phase table: change only row 09.

## Review Focus

None: the change removes two tests and adds no behaviour.  Checked against the spec on `7cc756ac` (main after p06): since the review's commit nothing changed in `UsageValueTests.cs`, `TestProjectTests.cs`, `src/SqlSource.Tool/UsageValue.cs` or `tests/SqlSource.Tool.Testing/TestProject.cs`, so both findings read as the review wrote them, and both covers exist.  No document names either test or its class.

---

### Task 0: The guard's "before" half

- [x] The `tool-root` mutation run into `artifacts/test-cleanup/phase-09/tool-root/before`.
- [x] `kills` on the before report for `UsageValueTests.NamedAsPath_PrintableToken_IsTheFullPathInQuotes`.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-09/coverage-before/SqlSource.Tool.Tests`, with the line figures of each file of `src/SqlSource.Tool/*.cs`.
- [x] Run time of `dotnet test --project tests/SqlSource.Tool.Tests`, before, interleaved with the "after" runs on the same machine.

### Task 1: Delete the tests

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/UsageValueTests.cs` (remove `NamedAsPath_PrintableToken_IsTheFullPathInQuotes`)
- Modify: `tests/SqlSource.Tool.Tests/TestProjectTests.cs` (remove `Manifest_ProjectWithSettingsAndASettingsFileOfAFolder_ListsTheProjectsFileFirst` with its comment; carry the comment onto the cover's `SettingsFiles` assertion)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 09's state)

- [x] **Step 1:** Remove both tests with their attributes.
- [x] **Step 2:** Set row 09 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tool.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the removed test did not make.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-09/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/*.cs` falls.
- [x] **Step 3:** The pull request lists each removed test beside its cover, the compare result, the coverage figures and the run times.
