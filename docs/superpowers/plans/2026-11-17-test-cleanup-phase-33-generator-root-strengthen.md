# Test cleanup phase 33: generator root strengthening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kill the two survivors the review measured in the root tests of `tests/SqlSource.Tests`: `EquatableArray.Equals(object)` held to false for different items, and `TextLines.LineSpanOf` held to SourceText's span for a text that ends in a bare `\r`.

**Architecture:** One assertion and two `InlineData` rows, no source change.  The guard is the `generator-root` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-33-generator-root-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- `Equals_DifferentItems_ReturnsFalse` gains `Of("a", "b").Equals((object)Of("a", "c")).ShouldBeFalse();` and keeps its three assertions.
- `LineSpanOf_ASpanOfText_IsTheSourceTextsLineSpan` gains the rows `("a\r", 0, 2)` and `("a\r", 2, 0)` and keeps its 21 rows; its comment names the new case.
- Neither test branches on its row, so TD-0089 does not change.  No document names either class.
- The epic's phase table: change only row 33.

## Review Focus

None: the change only adds.  Checked against the spec on `fd29ad32`: `EquatableArray.cs:31` is still `obj is EquatableArray<T> other && Equals(other)` and `TextLines.cs:43` still guards the `\n` after a `\r` with `end < text.Length`, so both findings hold.  p19's "after" report is not a valid "before" (PR #331 changed the scope's source and tests since), so the "before" run is this phase's own.

---

### Task 0: The guard's "before" half

- [x] The mutation run with output `artifacts/test-cleanup/phase-33/generator-root/before`.
- [x] `kills` on the before report for both tests.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-33/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/*.cs`.

### Task 1: Strengthen the two tests

**Files:**
- Modify: `tests/SqlSource.Tests/EquatableArrayTests.cs`
- Modify: `tests/SqlSource.Tests/TextLinesTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 33's state)

- [x] **Step 1:** Add the assertion and the two rows.
- [x] **Step 2:** Set row 33 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then both classes' tests pass.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the changed tests did not make; `EquatableArray.cs:31:49` and `TextLines.cs:43:40` are `Killed`.
- [ ] **Step 2:** Coverage into `artifacts/test-cleanup/phase-33/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/*.cs` falls.
- [ ] **Step 3:** The pull request gives the compare result, the two kills, the coverage figures and the run times.
