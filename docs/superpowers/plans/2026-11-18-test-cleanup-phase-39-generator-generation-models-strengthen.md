# Test cleanup phase 39: generator generation models strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `ModelsByFileTests.Arrange_ModelsOfOneQueryAndRole_KeepTheSetsOrderWhenTheirPlacesAreEqual` seventeen models on one place, so that the mutant `(true?byType :left.CompareTo(right))` of `ModelsByFile.cs:186:20`, which survives today, is killed.

**Architecture:** One test changes and there is no source change.  `Array.Sort` sorts sixteen items or fewer by insertion, which keeps equal items in their order with or without the tie-break; seventeen make it partition, which reorders them.  The guard is the `generator-generation-models` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Models/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-39-generator-generation-models-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change only the body of `Arrange_ModelsOfOneQueryAndRole_KeepTheSetsOrderWhenTheirPlacesAreEqual`, keeping its name, its one place and its assertion that the slot holds the models in the set's order.
- The epic's phase table: change only row 39.

## Review Focus

None: the change widens one test's input.  Checked against the spec on `ecf6dcba`.  `PlaceOrder.Compare` still ends with `byType != 0 ? byType : left.CompareTo(right)` at line 186, and `Arrange` still sorts with `Array.Sort(ordered, 0, count, new PlaceOrder(set.Places))`, so the finding holds.  The test is a `[Fact]`, so TD-0089 is unchanged.  No merged phase changed the scope's source or tests since `013a5af9`, so p24's "after" report is this phase's "before".  `kills` on it shows the test kills 32 mutants, none alone, and has `186:20` among its 4 survivors; the widened test still runs every line it ran.

---

### Task 0: The guard's "before" half

- [x] p24's "after" report copied to `artifacts/test-cleanup/phase-39/generator-generation-models/before/reports/mutation-report.json`.
- [x] `kills` on the before report for `ModelsByFileTests.Arrange_ModelsOfOneQueryAndRole_KeepTheSetsOrderWhenTheirPlacesAreEqual`.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-39/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/Models/**/*.cs`.

### Task 1: Widen the test

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Models/ModelsByFileTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 39's state)

- [x] **Step 1:** Build seventeen models, `App.M0` to `App.M16`, all on `Place("a/A.sql", 0, ModelRole.Input)`, and assert slot 0 holds them in the set's order.  A comment says why seventeen.
- [x] **Step 2:** Apply the mutant by hand and see the test fail; revert it.
- [x] **Step 3:** Set row 39 of the epic's table to `done`.
- [x] **Step 4:** `./format.sh`, then the Models tests pass.
- [ ] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST` and nothing `NOT COMPARABLE`, and `ModelsByFile.cs:186:20 Conditional (true)` is `Killed` in the after report.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-39/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Models/**/*.cs` falls.
- [ ] **Step 3:** The pull request gives the changed input, the compare result, the coverage figures and the run times.
