# Test cleanup phase 11: tool bundles renaming deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the two tests of `tests/SqlSource.Tool.Tests/Bundles/Renaming` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** Two test deletions, no source change.  The guard is the `tool-bundles-renaming` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Bundles/Renaming/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-11-tool-bundles-renaming-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `SqlServerErrorRenamingTests.RenameAsync_AKeyThatFallsBackOnAParameter_IsRefused` and `SqlServerRenamingTests.RuleOf_AKnownAndAnUnknownField_GivesTheFieldsRuleAndNull`.
- The covers keep every assertion: `SqlServerErrorRenamingTests.RenameAsync_EveryKeyFallsBack_StopsAfterTheAttempts`, `SqlServerFieldsTests.RuleOf_AMemberOfTheRecords_IsItsRule`, `SqlServerFieldsTests.RuleOf_AnyOtherMember_IsNull`, `SqlServerErrorRenamingTests.RenameAsync_MemberWithNoRule_IsRefusedAtItsPlace` and `SqlServerErrorRenamingTests.RenameAsync_AColumnOfTheServersThroughALinkedServer_IsRenamed`.
- No helper is removed: `KeyWhere`, `ParameterInThreeSpellingsAsync`, `CutoffSpellings` and `SqlServerRenamings.RefusedAsync` have other callers.  `RefusedAsync` keeps its optional `key`, which no caller passes after the change, so that it keeps the shape of `RenameAsync` and `RenamedAsync` beside it.
- The epic's phase table: change only row 11.

## Review Focus

None: the change removes tests and adds no behaviour.  Checked against the spec on `eecf1d23`: no commit since the review's touched `src/SqlSource.Tool/Bundles/Renaming`, `tests/SqlSource.Tool.Tests/Bundles/Renaming` or `tests/SqlSource.Tool.Testing/Bundles`, so both findings read as the review wrote them, and every cover exists.  No document outside `docs/superpowers/` names either test; the lines of `docs/architecture/bundles.md` that name their classes stay true through the covers.

---

### Task 0: The guard's "before" half

- [x] The `tool-bundles-renaming` mutation run into `artifacts/test-cleanup/phase-11/tool-bundles-renaming/before`.
- [x] `kills` on the before report for both tests.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-11/coverage-before/SqlSource.Tool.Tests`, with the line figures of each file of `src/SqlSource.Tool/Bundles/Renaming/**/*.cs`.

### Task 1: Delete the tests

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.Fallback.cs` (remove `RenameAsync_AKeyThatFallsBackOnAParameter_IsRefused`)
- Modify: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerRenamingTests.cs` (remove `RuleOf_AKnownAndAnUnknownField_GivesTheFieldsRuleAndNull`)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 11's state)

- [x] **Step 1:** Remove each test with its attribute.
- [x] **Step 2:** Set row 11 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tool.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows no removed test made.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-11/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/Bundles/Renaming/**/*.cs` falls.
- [x] **Step 3:** The pull request lists each removed test beside its covers, the compare result, the coverage figures and the run times.
