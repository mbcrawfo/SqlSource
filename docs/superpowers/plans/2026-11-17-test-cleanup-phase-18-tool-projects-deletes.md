# Test cleanup phase 18: tool projects deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the five tests of `tests/SqlSource.Tool.Tests/Projects` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** Five test deletions and three comment moves, no source change.  The guard is the `tool-projects` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Projects/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-18-tool-projects-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but the five the spec names: `TransitivePackageAssembliesTargetTests.Run_PackageListedInAnotherCase_IsNotInTheValue`, `ManifestTargetTests.WriteManifest_ProjectWithOneFramework_HoldsTheBuildsFolders`, `FixtureProjectTests.Evaluate_RestoredProjectWithoutThePackage_DoesNotUseSqlSource`, `OnlineBuildTests.Build_AutoWithADotEnvInAFolderAbove_RunsNothing` and `OnlineBuildTests.Build_TrueWithoutAConnectionSource_RunsTheTool`.
- The covers keep every assertion and row; only their comments change.
- No helper is removed: `ValueOfAsync`, `BuildFoldersOfAsync`, `Restore`, `EvaluateAsync`, `DotEnvText`, `PassesAsync` and `NoVariables` have other callers.
- The epic's phase table: change only row 18.

## Review Focus

None: the change removes five tests and adds no behaviour.  Checked against the spec on `00e70335`: no commit since the review's (`36864a86`) touched `tests/SqlSource.Tool.Tests/Projects`, `src/SqlSource.Tool/Projects` or `src/SqlSource/build`, so every finding reads as the review wrote it, and every cover exists.  No document outside `docs/superpowers/` names a test of the phase; the lines that name its classes stay true.

---

### Task 0: The guard's "before" half

- [x] The `tool-projects` mutation run into `artifacts/test-cleanup/phase-18/tool-projects/before`.
- [x] `kills` on the before report for each test.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-18/coverage-before/SqlSource.Tool.Tests`, with the line figures of each file of `src/SqlSource.Tool/Projects/**/*.cs`.

### Task 1: Delete the tests

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Projects/TransitivePackageAssembliesTargetTests.cs` (remove the test; its sentence on NuGet's case joins the comment of `Run_ReferencesOfAPackageNotListed_AreTheValue`)
- Modify: `tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs` (remove the test; the comment at line 68, in `WriteManifest_ProjectWithOneFramework_HoldsWhatTheCompilerIsGiven`, points to `WriteManifest_BuildFoldersOfAnOddPath_DecodeToTheRealFolders`, whose comment takes the deleted test's sentence on what the line holds)
- Modify: `tests/SqlSource.Tool.Tests/Projects/FixtureProjectTests.cs` (remove the test)
- Modify: `tests/SqlSource.Tool.Tests/Projects/OnlineBuildTests.cs` (remove both tests; the comment that the build looks for a `.env` in the project's folder alone moves beside the theory's row `"a .env above"`)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 18's state)

- [x] **Step 1:** Remove each test with its attribute, and move its comment as above.
- [x] **Step 2:** Set row 18 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tool.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows no removed test made.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-18/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/Projects/**/*.cs` falls.
- [x] **Step 3:** The pull request lists each removed test beside its covers, the compare result, the coverage figures and the run times.
