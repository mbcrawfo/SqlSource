# Test cleanup phase 05: generator generation types deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Delete the seven tests of `tests/SqlSource.Tests/Generation/Types` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** Seven test deletions, no source change.  The guard is the `generator-generation-types` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Types/**/*.cs`, and, for the four findings mutation cannot guard (1-04, 1-05, 1-06, 1-08), each removed test beside its named cover with the test project's run time before and after.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-05-generator-generation-types-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but the seven the spec names.
- The covers keep every assertion and row: `PostgresTypeMapTests.Map_EveryRowOfTheTable_GivesItsType`, `Of_EachSetOfLibraries_IsTheSameInstance`, `Map_JsonOn_GivesJsonElement`; `SqlServerTypeMapTests.Map_EveryRowOfTheTable_GivesItsType`, `Map_OverrideAndLibrary_OverrideWins`, `Map_AliasType_IsAskedOfTheOverrideFirstThenMappedByItsBase`; `MappedTypeNamesTests.Collect_EveryLevel_GivesEachTypeOnceInOrdinalOrder`.
- Both classes' `NodaTimeRows` stay, for `Map_NodaTimeOn_GivesEachRow`.
- The epic's phase table: change only row 05.

## Review Focus

None: the change removes tests and adds no behaviour.  Checked against the spec on `78f3992d`: nothing under `src/SqlSource/Generation/Types` or `tests/SqlSource.Tests/Generation/Types` has changed since the review's commit, each finding reads as the review wrote it, and every cover exists.  No helper, `using` or data member is used only by a removed test.  The document lines the spec lists (`docs/architecture/generator.md` 282 and 284, TD-0089 line 12, `src/SqlSource/Generation/Types/AGENTS.md` line 3) name only tests that stay, so they stay as they are.

---

### Task 0: The guard's "before" half

- [x] The `generator-generation-types` mutation run into `artifacts/test-cleanup/phase-05/generator-generation-types/before`.
- [x] `kills` on the before report for each of the seven removed tests.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-05/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/Types/**/*.cs`.
- [x] Run time of `dotnet test --project tests/SqlSource.Tests`, interleaved with the "after" runs (`--no-build`), with the load beside each figure.

### Task 1: Delete the tests

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs` (remove `Map_NodaTimeOff_IsTheBaseMap`, `Of_ALibrary_IsTheSameInstanceEachTime`, `Of_SameSet_IsTheSameInstance`, `PlaceOf_Json_IsTheSystemTextJsonLibrary`)
- Modify: `tests/SqlSource.Tests/Generation/Types/Maps/SqlServerTypeMapTests.cs` (remove `Map_NodaTimeOff_IsTheBaseMap`, `Map_Override_AnswersBeforeTheRow`)
- Modify: `tests/SqlSource.Tests/Generation/Types/MappedTypes/MappedTypeNamesTests.cs` (remove `Collect_FilesOfOneChain_GiveItsTypesOnce` with its comment)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 05's state)

- [x] **Step 1:** Remove each test with its attributes and the comment above it, if it has one.
- [x] **Step 2:** Set row 05 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows no removed test made.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-05/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Types/**/*.cs` falls.
- [x] **Step 3:** The pull request lists each removed test beside its cover, the compare result, the coverage figures and the run times.
