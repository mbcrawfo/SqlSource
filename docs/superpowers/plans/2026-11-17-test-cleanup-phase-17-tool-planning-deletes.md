# Test cleanup phase 17: tool planning deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the three tests of `tests/SqlSource.Tool.Tests/Planning` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** Three test deletions, no source change.  The guard is the `tool-planning` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Planning/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-17-tool-planning-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `FileTextTests.Read_FileThatIsThere_GivesItsText`, `AttributeReaderTests.Read_AttributeInsideIfOfAFramework_IsReadWithTheConstantsOfTheManifest` and `RunPlannerTests.Plan_FileClaimedByTwoProjects_TakesEachProjectsChainForItsOutput`.
- The covers keep every assertion and row: `FileTextTests.Read_Utf8WithoutMark_IsTheTextAndTheCompilerDoesNotFallBack`, `AttributeReaderTests.Read_AttributeInsideIf_IsReadWhenTheManifestHasTheConstant` and `RunPlannerTests.Plan_FileThatTwoProjectsClaim_IsPlannedOnceUnderTheFirstAndNeedsWhatEitherNeeds`; `Plan_FileClaimedByTwoProjects_TakesTheChainOfAProjectThatIsNotTheOwner` stays.
- No helper is removed: `TempFolder.WriteFile` (the `SqlFileText` test), the `Read` helper of `AttributeReaderTests` with its constants (the `#if` theory), `Plans.Postgres(_folder, …)` and `TestProject.Settings` (`RunPlannerSettingsFileTests`, `RunPlannerErrorTests`) all have other callers.
- The comment above the deleted `#if` test (how a framework's constants reach the manifest) goes with it: `FixtureProjectTests` and `ManifestReaderTests` prove that.  The comment above the deleted planner test goes with it: the cover's comment states the same rule.
- The epic's phase table: change only row 17.

## Review Focus

None: the change removes three tests and adds no behaviour.  Checked against the spec on `7a40ab05`: no commit since the review's commit touched `src/SqlSource.Tool/Planning` or `tests/SqlSource.Tool.Tests/Planning`, so the three findings read as the review wrote them, and every cover exists.  No document outside `docs/superpowers/` names a test of the phase; `docs/tech-debt/TD-0089` line 22 names `Plan_OutputOfTheChain_DecidesWhetherAQueryNeedsAnEntry`, which stays.

---

### Task 0: The guard's "before" half

- [x] The `tool-planning` mutation run into `artifacts/test-cleanup/phase-17/tool-planning/before`.
- [x] `kills` on the before report for the three tests.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-17/coverage-before/SqlSource.Tool.Tests`, with the line figures of each file of `src/SqlSource.Tool/Planning/**/*.cs`.

### Task 1: Delete the tests

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Planning/FileTextTests.cs` (remove `Read_FileThatIsThere_GivesItsText`)
- Modify: `tests/SqlSource.Tool.Tests/Planning/AttributeReaderTests.cs` (remove `Read_AttributeInsideIfOfAFramework_IsReadWithTheConstantsOfTheManifest` and its comment)
- Modify: `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs` (remove `Plan_FileClaimedByTwoProjects_TakesEachProjectsChainForItsOutput` and its comment)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 17's state)

- [x] **Step 1:** Remove each test with its attribute and comment.
- [x] **Step 2:** Set row 17 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tool.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows no removed test made.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-17/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/Planning/**/*.cs` falls.
- [x] **Step 3:** The pull request lists each removed test beside its covers, the compare result, the coverage figures and the run times.
