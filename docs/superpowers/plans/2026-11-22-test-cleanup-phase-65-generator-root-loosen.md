# Test cleanup phase 65: generator root loosen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop `SqlServerNamesTests` from holding the order of `SqlServerNames.ClrSystemTypes`, which reaches no output, while it still holds the three names.

**Architecture:** One test changed and renamed, no source or helper changes.  The guard is the `generator-root` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource/*.cs`, and, since mutation is blind to this finding, its cover and the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-65-generator-root-loosen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only `ClrSystemTypes_OfSys_AreTheThreeInOrder` changes.
- The epic's phase table: change only row 65.

Checked against the spec on `6bb145f2`: `ClrSystemTypes` has the same readers, the `Contains` of `SqlServerNames.IsClrSystemType` and the loop of `SqlServerParameterBinder.BuildRows`, which adds each name to a dictionary, and a doc comment of `SqlServerTypes`.  `docs/architecture/sqlserver-describer.md` names the list and not its order, so the finding holds.  No document names the test.  No phase of this run has handed on a report of this scope, so the "before" is a run of its own.  `main` moved to `07a5f134` between the two halves, with no change under `src/SqlSource` or `tests/SqlSource.Tests`.

## Review Focus

The test still fails when a name is missing, added or changed, and no longer fails when the list is reordered.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-65/generator-root/before` (Windows): 238 mutants tested, 210 killed, 13 survived, 15 timeouts, 0 errors; the run took 2 h 23 min.  `kills`: `ClrSystemTypes_OfSys_AreTheThreeInOrder` 1 row, 0 kills, 0 alone, 0 timeouts and 0 survivors under it.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-65/coverage-before/SqlSource.Tests` (9,236 tests, 6 min 37 s): 963 of 971 lines over the 11 files of `src/SqlSource/*.cs`, with `SqlServerNames.cs` at 6 of 6.
- [x] Run time of `dotnet test --project tests/SqlSource.Tests`: 9,236 tests in 7 min 38 s.

### Task 1: Apply the finding

**Files:**
- Modify: `tests/SqlSource.Tests/SqlServerNamesTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 65's state)

- [x] **Step 1 (F-SqlSource.Tests-1-05):** `ClrSystemTypes_OfSys_AreTheThreeInOrder` becomes `ClrSystemTypes_OfSys_AreTheThree` and asserts the three names with `ignoreOrder: true`.  Checked by hand: with the list reordered the test passes, and with `Geometry` dropped from it the test fails.
- [x] **Step 2:** Set row 65 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-65/generator-root/after` (Windows): 238 mutants tested, 209 killed, 12 survived, 17 timeouts, 0 errors; the run took 2 h 12 min.  `compare`: 225 detected before, 224 still detected, 0 lost, 1 not comparable.
- [x] **Step 2:** The one not comparable is `SortedSearch.cs:68:23`, Arithmetic mutation to `middle - 1`, `Killed` before and `Timeout` after: a kill that did not repeat, which the epic's procedure allows, since `kills` on the "before" report shows the changed test made no kill and had no timeout under it.  The mutant stays detected.  One other status moved, in the safe direction: `SqlSourceGenerator.cs:1297:27`, Linq method mutation to `group.FirstOrDefault`, `Survived` before and `Timeout` after.
- [x] **Step 3:** Coverage into `artifacts/test-cleanup/phase-65/coverage-after/SqlSource.Tests` (9,236 tests, 7 min 25 s): 963 of 971 lines over the 11 files, and every one of the 290 files of `SqlSource` has the same figure before and after.
- [x] **Step 4:** Cover: none is needed, since no test or row is removed and the test keeps all three names.  Run time: 9,236 tests in 6 min 7 s, against 7 min 38 s before, on the same machine with other work running, and the change is one assertion.
- [ ] **Step 5:** The pull request gives the change, the compare result, the coverage figures and the run times.
