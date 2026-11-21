# Test cleanup phase 56: tool describing postgres mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Drop four rows of `NullabilityWalkTests` that other tests already hold, and make three of its tests assert what their names promise: the ungated query whose reference names another column is `unavailable`, and an unmapped reference, and a view's column printed under the base table's name, pass the check with no reason.

**Architecture:** One test file, `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs`, no source or helper changes.  The guard is the `tool-describing-postgres` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Describing/Postgres/**/*.cs`, with covers and run time for the three strengthens, whose subject lines do not compile under mutation.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-56-tool-describing-postgres-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the four tests of the findings change, each by its rows or an assertion.
- The epic's phase table: change only row 56.

Checked against the spec on `2d035cdb`: the ungated `!fits` return of `Unusable` (`NullabilityWalk.cs:159-162`), `OutputFits` (`NullabilityWalk.cs:239-264`) with its `ColumnNullability.IsTable` guard, and `ColumnOf`'s reading of an entry through `OutputReference.Parse` read as the findings say; the four tests are unchanged since the review, and the covers, `OutputReferenceTests.Parse_Expression_IsNoReference` (rows `$1`, `COALESCE(u.id, p.id)`, `(u.id)`) and `NullabilityWalkNameTests.Run_BareNameUnderAJoin_IsUnmappable`, still hold their rows, so every finding holds.  Phase 55, of the same scope, left an "after" report, but it ran before its review's row in `PlanExpressionTests` (`0a3b167b`), so the scope's tests changed after it and this phase's "before" is a run of its own.  The spec names no document to update, and none names a test of the phase or its class.

## Review Focus

- F-1-08 drops `(a.id)`, `COALESCE(a.id, b.id)`, `$1` and `id`, and keeps `a.id + 1` (an entry that parses to no reference) and `old.id` (an alias of no instance, the `TryFind` miss).
- The three strengthens each add one assertion; each is shown, by a hand edit of the source line it guards, to fail without that line and to pass on the code.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-56/tool-describing-postgres/before`: 2,745 mutants tested, 2,318 killed, 374 survived, 51 timeouts, 2 errors; the tested run took 24 min 0 s.  `kills`:
  - `Run_UngatedQueryWhoseReferenceNamesAnotherColumn_IsUnavailableAndKeepsTheCatalogsAnswer`: 1 row, 56 kills, 0 alone, 0 timeouts and 4 survivors under it.
  - `Run_UnmappedReferenceAndExpression_PassTheCheck`: 1 row, 24 kills, 0 alone, 0 timeouts and 5 survivors under it.
  - `Run_ViewsColumnPrintedUnderTheBaseTablesName_IsWalked`: 1 row, 31 kills, 0 alone, 0 timeouts and 5 survivors under it.
  - `Run_ColumnOfATableWhoseEntryCannotBeMapped_KeepsTheHeuristic`: 6 rows, 34 kills, 0 alone, 0 timeouts and 6 survivors under it.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-56/coverage-before/SqlSource.Tool.Tests` (7,091 tests): 5,031 of 5,645 lines over the 56 files of `src/SqlSource.Tool/Describing/Postgres/` that have a coverable line.
- [x] Run time of `dotnet test --project tests/SqlSource.Tool.Tests`: 7,091 tests, 2 min 42 s.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 56's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-08):** `Run_ColumnOfATableWhoseEntryCannotBeMapped_KeepsTheHeuristic` drops the rows `"(a.id)"`, `"COALESCE(a.id, b.id)"`, `"$1"` and `"id"`.
- [x] **Step 2 (F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-01):** `Run_UngatedQueryWhoseReferenceNamesAnotherColumn_IsUnavailableAndKeepsTheCatalogsAnswer` asserts `outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable)`.
- [x] **Step 3 (F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-02):** `Run_UnmappedReferenceAndExpression_PassTheCheck` asserts `outcome.Reason.ShouldBeNull()` beside its `Plan` assertion.
- [x] **Step 4 (F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-03):** `Run_ViewsColumnPrintedUnderTheBaseTablesName_IsWalked` asserts `outcome.Reason.ShouldBeNull()`.
- [x] **Step 5:** Each strengthen is checked by hand: with the source line it guards changed, the test fails on its new assertion alone, and on the code the class passes (16 tests).
  - F-1-01: `if (!fits && !scope.Gated)` at `NullabilityWalk.cs:159` made never true (`&& fits` added, since `if (false)` does not compile here): `outcome.Plan` is `walked`.
  - F-1-02: `OutputFits` counting an alias of no instance (`!instances.TryFind(...) || ...` at `NullabilityWalk.cs:253`): `outcome.Reason` is `output-differs`.
  - F-1-03: `&& ColumnNullability.IsTable(origin.Table)` at `NullabilityWalk.cs:249` removed: `outcome.Reason` is `output-differs`.
- [x] **Step 6:** Set row 56 of the epic's table to `done`.
- [x] **Step 7:** `./format.sh`, then `NullabilityWalkTests` passes; `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-56/tool-describing-postgres/after`: 2,745 mutants tested, 2,318 killed, 374 survived, 51 timeouts, 2 errors; the tested run took 23 min 32 s.  `compare`: 2,369 detected before, 2,369 still detected, 0 lost, 0 not comparable.  `kills` on the "after" report:
  - `Run_UngatedQueryWhoseReferenceNamesAnotherColumn_IsUnavailableAndKeepsTheCatalogsAnswer`: 1 row, 59 kills, 0 alone.
  - `Run_UnmappedReferenceAndExpression_PassTheCheck`: 1 row, 30 kills, 0 alone.
  - `Run_ViewsColumnPrintedUnderTheBaseTablesName_IsWalked`: 1 row, 35 kills, 0 alone.
  - `Run_ColumnOfATableWhoseEntryCannotBeMapped_KeepsTheHeuristic`: 2 rows, 30 kills, 0 alone.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-56/coverage-after/SqlSource.Tool.Tests` (7,087 tests): 5,031 of 5,645 lines over the 56 files, before and after, the same figure in each file.
- [x] **Step 3:** Run time of `dotnet test --project tests/SqlSource.Tool.Tests`: 2 min 42 s before (7,091 tests), 2 min 35 s after (7,087 tests).
- [x] **Step 4:** The pull request gives the change, the compare result, the coverage figures, each removed row beside its cover, and the run times.
