# Test cleanup phase 25: generator generation paths folds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fold the one theory of `tests/SqlSource.Tests/Generation/Paths` whose rows repeat the `Resolve` tests, keeping the one row that tests `FindFiles` alone, with the guard showing nothing lost.

**Architecture:** Five `InlineData` rows dropped from `PathResolverTests.FindFiles_SourceOfAnAttribute_GivesTheFilesItNames`; its last row, `("../../..", "")`, becomes a fact, as the proposal allows.  No source change.  The guard is the `generator-generation-paths` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Paths/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-25-generator-generation-paths-folds-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `PathResolverTests.FindFiles_SourceOfAnAttribute_GivesTheFilesItNames`.
- The covers, `Resolve_NoSource_TakesTheFileNamedForTheType`, `Resolve_SourceDot_TakesTheFilesInTheFolderOfTheSourceFileAndNotItsSubfolders`, `Resolve_FolderSource_TakesTheFilesInThatFolder` (row `"../Queries"`), `Resolve_FileSource_TakesThatFile` (row `"users.SQL"`) and `Resolve_SourceThatMatchesNothing_IsAnErrorAtTheAttribute` (row `"Missing.sql"`), keep every row and assertion.
- The epic's phase table: change only row 25.
- The theory compared a joined string to its row's text with no `if`, and the fact has no row, so TD-0089's list does not change.

## Review Focus

The kept row becomes the fact `FindFiles_SourceThatLeavesTheRootAndNamesNoSqlFile_GivesNone`: the review checks that the name says what the row tests, a source that climbs out of the root and is not a `.sql` file.  Checked against the spec on `775941a3`: `Resolve` still calls `FindFiles(type.FilePath, type.Source, type.Name, sqlPaths)`, `TestModels.Type` still gives `/app/Repo/UserRepository.cs` and `UserRepository`, and each cover still holds the row the review named.  No document names `PathResolverTests` or the folded test.

---

### Task 0: The guard's "before" half

- [x] The `generator-generation-paths` mutation run into `artifacts/test-cleanup/phase-25/generator-generation-paths/before` (no earlier phase of this scope ran).
- [x] `kills` on the before report for the folded test.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-25/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/Paths/**/*.cs`.

### Task 1: Fold the theory

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Paths/PathResolverTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 25's state)

- [x] **Step 1:** Replace the theory with the fact `FindFiles_SourceThatLeavesTheRootAndNamesNoSqlFile_GivesNone`, which calls `PathResolver.FindFiles("/app/Repo/UserRepository.cs", "../../..", "UserRepository", SqlPaths)` and asserts it is empty.
- [x] **Step 2:** Set row 25 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then the scope's tests: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the folded test did not make.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-25/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Paths/**/*.cs` falls.
- [x] **Step 3:** The pull request lists the dropped rows beside their covers, the compare result and the coverage figures.
