# Test cleanup phase 27: generator settings folds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Drop the three rows of `TypeKeyTextTests.WritePart_Text_IsQuotedWhenItNeedsIt` that `WritePart_WhiteSpaceThatIsNoControlCharacter_IsKeptInTheQuotes` already proves with its read-back, with the guard showing nothing lost.

**Architecture:** Three `InlineData` rows go, no source change.  The guard is the `generator-settings` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Settings/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-27-generator-settings-folds-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Drop only the rows `("a  b", "\"a  b\"")`, `("a\u00A0b", "\"a\u00A0b\"")` and `("x\u2028y", "\"x\u2028y\"")` of `WritePart_Text_IsQuotedWhenItNeedsIt`, the cover's three texts; `("my  schema", "\"my  schema\"")` and the plain-space row `("a b", "\"a b\"")` stay.
- `WritePart_WhiteSpaceThatIsNoControlCharacter_IsKeptInTheQuotes`, the cover, keeps every row and assertion.
- The epic's phase table: change only row 27.

## Review Focus

None: the change drops rows the cover asserts in full.  Checked against the spec on `9a1e0bc9`: the finding's rows 86 `a b` and 87 `x y` are, at the reviewed commit and today, `"a\u00A0b"` and `"x\u2028y"` (the finding printed the characters, not their escapes), the cover's second and third rows.  The theory's last row, a plain space `("a b", "\"a b\"")`, is not one of the cover's texts and stays.  No phase since p07 changed the scope's source or tests (`git log 2de98d92..origin/main` names none), so p07's "after" report is this phase's "before".

---

### Task 0: The guard's "before" half

- [x] p07's "after" report (tree of 2de98d92) copied to `artifacts/test-cleanup/phase-27/generator-settings/before/reports/mutation-report.json`.
- [x] `kills` on the before report for both tests.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-27/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Settings/**/*.cs`.

### Task 1: Fold the rows

**Files:**
- Modify: `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeyTextTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 27's state)

- [x] **Step 1:** Drop the three rows.
- [x] **Step 2:** Set row 27 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then the Settings tests pass.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the changed theory did not make.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-27/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Settings/**/*.cs` falls.
- [x] **Step 3:** The pull request lists the dropped rows beside their cover, the compare result, the coverage figures and the run times.
