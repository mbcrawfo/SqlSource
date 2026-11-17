# Test cleanup phase 22: generator generation methods folds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fold the one theory of `tests/SqlSource.Tests/Generation/Methods` that the test review found to hold a row its other rows and `MethodHostTests` already cover, with the guard showing nothing lost.

**Architecture:** One `InlineData` row dropped from one theory, no source change.  The guard is the `generator-generation-methods` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Methods/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-22-generator-generation-methods-folds-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `MethodArgumentsTests.AppendDeclaration_TokenNamedAwait_IsVerbatim`, and in it only the row `(nameof(MemberLocation.NestedClass), "")`.
- Keep the rows `(nameof(MemberLocation.ExtensionClass), "this ")` and `(nameof(MemberLocation.Class), "")`.
- The cover, `MethodHostTests.IsExtension_EachLocation_IsTheExtensionClassAlone`, keeps every row, its `NestedClass` row included.
- The epic's phase table: change only row 22.
- The theory builds its expected text from its row by concatenation, with no `if`, so TD-0089's list does not change.

## Review Focus

None: the change removes a row and adds no behaviour.  Checked against the spec on `721cfb41`: `MethodArguments.AppendDeclaration` still passes `host.IsExtension` and nothing else of the host, the theory still holds the three rows the review named, and the cover exists with its `NestedClass` row.  The documents the spec lists, `docs/architecture/generator.md` and `tests/AGENTS.md`, name `MethodArgumentsTests` only for its allocation test, and stay.

---

### Task 0: The guard's "before" half

- [x] The `generator-generation-methods` mutation run into `artifacts/test-cleanup/phase-22/generator-generation-methods/before` (no earlier phase of this scope ran).
- [x] `kills` on the before report for the folded test.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-22/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/Methods/**/*.cs`.

### Task 1: Fold the theory

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Methods/MethodArgumentsTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 22's state)

- [x] **Step 1:** Drop the row `[InlineData(nameof(MemberLocation.NestedClass), "")]`.
- [x] **Step 2:** Set row 22 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then the scope's tests: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the folded test did not make.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-22/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Methods/**/*.cs` falls.
- [x] **Step 3:** The pull request lists the dropped row beside its covers, the compare result and the coverage figures.
