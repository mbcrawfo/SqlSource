# Test cleanup phase 23: generator generation models folds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fold the two tests of `tests/SqlSource.Tests/Generation/Models` that make the same call on no groups into one test that asserts every member of the result empty, with the guard showing nothing lost.

**Architecture:** Two facts of `ModelCollectorTests` become one, no source change.  The guard is the `generator-generation-models` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Models/**/*.cs`, and, since the folded tests reach no tested mutant, the covers and the run time of `tests/SqlSource.Tests`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-23-generator-generation-models-folds-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `ModelCollectorTests.Resolve_NoGroups_GivesNoPlaces` and `ModelCollectorTests.Resolve_NoGroups_GivesNoAssignments`, which become one test, `Resolve_NoGroups_GivesAnEmptySet`.
- The new test makes the call both made, `ModelCollector.Resolve(EquatableArray<ModelGroup>.Empty, EquatableArray<TakenName>.Empty)`, once, and asserts `Models`, `Places`, `Diagnostics` and `Assignments` empty.
- The epic's phase table: change only row 23.

## Review Focus

None: the change merges two tests into one that asserts what both did and two members more.  Checked against the spec on `56299b08`: both tests still make the one call and assert one member each, `ModelSet` still has the four members `Models`, `Places`, `Diagnostics` and `Assignments`, and no document names either test or their class.

---

### Task 0: The guard's "before" half

- [x] The `generator-generation-models` mutation run into `artifacts/test-cleanup/phase-23/generator-generation-models/before` (no earlier phase of this scope).
- [x] `kills` on the before report for both folded tests.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-23/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/Models/**/*.cs`.

### Task 1: Fold the tests

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Models/ModelCollectorTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 23's state)

- [x] **Step 1:** Replace the two facts with:

```csharp
    [Fact]
    public void Resolve_NoGroups_GivesAnEmptySet()
    {
        var set = ModelCollector.Resolve(EquatableArray<ModelGroup>.Empty, EquatableArray<TakenName>.Empty);

        set.Models.ShouldBeEmpty();
        set.Places.ShouldBeEmpty();
        set.Diagnostics.ShouldBeEmpty();
        set.Assignments.ShouldBeEmpty();
    }
```

- [x] **Step 2:** Set row 23 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then the `ModelCollectorTests` tests of `tests/SqlSource.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the folded tests did not make.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-23/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Models/**/*.cs` falls.
- [x] **Step 3:** Run time of `dotnet test --project tests/SqlSource.Tests --no-build`, before and after, on one machine with the load beside each figure.
- [x] **Step 4:** The pull request lists the folded tests beside their covers, the compare result, the coverage figures and the run times.
