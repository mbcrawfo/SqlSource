# Test cleanup phase 30: tool describing postgres folds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Drop the row `"empty-list"` of `PostgresSessionPlanTests.DescribeAsync_GatedQueryWithoutAPlanTheWalkCanUse_IsDescribedByTheHeuristicAndUnavailable` and the rows `"Values Scan"` and `"WorkTable Scan"` of `NullabilityWalkNameTests.Run_AliasOfANodeThatIsNoScanOfARelation_IsUnmappable`, with the guard showing nothing lost.

**Architecture:** Three rows go, no source change.  The guard is the `tool-describing-postgres` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Describing/Postgres/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-30-tool-describing-postgres-folds-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Drop only the three named rows; `"text-that-is-no-json"` and the other rows of the session theory, and the `Subquery Scan`, `CTE Scan` and `Function Scan` rows of the walk theory, stay.
- The covers, `PlanNodeTests.Read_TextThatIsNotAnArrayOfOnePlan_IsNull` and `PlanInstancesTests.Find_NodeThatIsNoScanOfARelation_NamesNone`, keep every row and assertion.
- The epic's phase table: change only row 30.

## Review Focus

None: the change drops rows the covers assert.  Checked against the spec on `6d8c1c01`: both theories read as the findings say.  No phase since p14 changed the scope's source or tests (`git log aa101522..origin/main` over `src/SqlSource.Tool/Describing/Postgres` and `tests/SqlSource.Tool.Tests/Describing/Postgres` names none), so p14's "after" report is this phase's "before".  Neither theory branches on its row (the session theory's `error is null ? null : …` only derives a value passed to the same call), so TD-0089 does not change.

---

### Task 0: The guard's "before" half

- [x] p14's "after" report (tree of aa101522) copied to `artifacts/test-cleanup/phase-30/tool-describing-postgres/before/reports/mutation-report.json`.
- [x] `kills` on the before report for both tests.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-30/coverage-before/SqlSource.Tool.Tests`, with the line figures of each file of `src/SqlSource.Tool/Describing/Postgres/**/*.cs`.

### Task 1: Fold the rows

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionPlanTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkNameTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 30's state)

- [x] **Step 1:** Drop the three rows.
- [x] **Step 2:** Set row 30 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then the Describing/Postgres tests pass.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** Coverage into `artifacts/test-cleanup/phase-30/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/Describing/Postgres/**/*.cs` falls.
- [x] **Step 2:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the changed theories did not make.
- [x] **Step 3:** The pull request lists the dropped rows beside their cover, the compare result, the coverage figures and the run times.
