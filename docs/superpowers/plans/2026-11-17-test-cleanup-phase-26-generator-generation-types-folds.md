# Test cleanup phase 26: generator generation types folds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fold the two tests of `FileTypesInputTests` that keep one engine's type override into one theory over the engine, with the guard showing nothing lost.

**Architecture:** Two facts become one theory, no source change.  The guard is the `generator-generation-types` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Types/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-26-generator-generation-types-folds-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `FileTypesInputTests.TypeSettingsOf_LevelWithOnlyASqlServerTypeOverride_KeepsIt` and `TypeSettingsOf_LevelWithOnlyAPostgreSqlTypeOverride_KeepsIt`.
- The theory is `TypeSettingsOf_LevelWithOnlyOneEnginesTypeOverride_KeepsIt`, with a row for each `DatabaseEngine`, so a new engine gets its row.  It keeps the two tests' assertions: the result is not `SettingsLevel.None`, the engine's slot holds the entry, and every other engine's slot is null.
- `DatabaseEngine` is `internal` and a test method is `public`, so the theory takes the engine's name, rows from `Enum.GetNames<DatabaseEngine>()`, as other theories of the project take an engine as a string.  It does not branch on its row (TD-0089): every other slot being null is stated as the whole `TypeOverrides` equalling `PerEngine.Only` of the engine and the entry.
- The comment that states the rule stays, above the theory.
- The epic's phase table: change only row 26.

## Review Focus

None: the change merges facts and drops no row.  Checked against the spec on `110d42b8`: `TypeSettingsOf` still compares the whole `PerEngine` with `default` (`src/SqlSource/Generation/Types/FileTypesInput.cs:96`), and the two tests are still one body with the engines swapped.  No earlier report of this scope is saved (p05 kept none), so the phase runs its own "before".

---

### Task 0: The guard's "before" half

- [x] The `generator-generation-types` mutation run into `artifacts/test-cleanup/phase-26/generator-generation-types/before`.
- [x] `kills` on the before report for each of the two tests: 5 kills each, 0 alone, 0 timeouts under it.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-26/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/Types/**/*.cs`.

### Task 1: Fold the tests

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Types/FileTypesInputTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 26's state)

- [x] **Step 1:** Replace the two tests with the theory, as the constraints say.
- [x] **Step 2:** Set row 26 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the folded tests did not make.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-26/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Types/**/*.cs` falls.
- [x] **Step 3:** The pull request lists the folded tests beside their cover, the compare result, the coverage figures and the run times.
