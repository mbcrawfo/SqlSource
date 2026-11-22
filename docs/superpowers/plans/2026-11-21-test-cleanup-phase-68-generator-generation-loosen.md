# Test cleanup phase 68: generator generation loosen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop `Text_GenerateAttribute_HasTheInterfaceProperties` from failing when the attribute's properties are declared in another order, while a property added or removed still fails it.

**Architecture:** One test changes, with no source or helper changes.  The guard is the `generator-generation` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-68-generator-generation-loosen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the test of the finding changes.
- The epic's phase table: change only row 68.

Checked against the spec on `07a5f134`: the test still compares the properties from `GetMembers()` with an ordered `ShouldBe`, and `AttributeSource.Text` still declares `Source`, `GenerateInterface` and `InterfaceName`, which a user sets as named arguments, so the finding holds.  The spec names no document to update, and none names the test.

## Review Focus

`ignoreOrder: true` keeps the exact set, so the change gives up only the order of declaration.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-68/generator-generation/before` (Windows): 44 mutants tested, 37 killed, 5 survived, 2 timeouts, 0 errors, 69 without coverage; the tested run took 2 min 56 s.  `kills`: `Text_GenerateAttribute_HasTheInterfaceProperties` 1 row, 0 kills, 0 alone, 0 timeouts under it, 0 survivors under it.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-68/coverage-before/SqlSource.Tests` (9,236 tests, 0 skipped, 10 min 7 s): 364 of 366 lines over the 17 files of `src/SqlSource/Generation/*.cs` that have executable lines.

### Task 1: Apply the finding

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/AttributeSourceTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 68's state)

- [x] **Step 1 (F-SqlSource.Tests.Generation-1-06):** `Text_GenerateAttribute_HasTheInterfaceProperties` compares the three properties with `ignoreOrder: true`.
- [x] **Step 2:** Set row 68 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-68/generator-generation/after` (Windows): 44 mutants tested, 37 killed, 5 survived, 2 timeouts, 0 errors, 69 without coverage; the tested run took 2 min 10 s.  `compare`: 39 detected before, 39 still detected, 0 lost, 0 not comparable.  `kills` after: the test has 1 row, 0 kills, 0 alone, 0 timeouts under it, 0 survivors under it.
- [x] **Step 2:** A hand check, since no mutant reaches the test: with `Source` and `InterfaceName` swapped in `AttributeSource.Text` the test passes, with `GenerateInterface` removed it fails, and with the source restored it passes.
- [x] **Step 3:** Coverage into `artifacts/test-cleanup/phase-68/coverage-after/SqlSource.Tests` (9,236 tests, 0 skipped, 6 min 33 s, against 10 min 7 s before): 364 of 366 lines, before and after, the same figure in each of the 17 files.
- [ ] **Step 4:** The pull request gives the change, the compare result, the coverage figures and the run times.  The phase removes no test or row.
