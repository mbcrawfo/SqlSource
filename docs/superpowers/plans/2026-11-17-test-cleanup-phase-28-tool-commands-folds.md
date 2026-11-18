# Test cleanup phase 28: tool commands folds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fold the two redundant cases of `tests/SqlSource.Tool.Tests/Commands` that the test review found into the tests that cover them, with the guard showing nothing lost.

**Architecture:** One theory row dropped in `CheckFilterTests.cs`, and one fact of `DiagnoseTests.cs` folded into a theory as a row; no source change.  The guard is the `tool-root` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/*.cs`, and, since both findings' subjects lie outside that scope, each removed case beside its cover with the test project's run time before and after.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-28-tool-commands-folds-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `CheckFilterTests.Check_EntryOfAQueryThatIsGoneUnderDatabase_IsReportedWhenDescribeWouldWriteTheFile` (drop the `"postgres"` row and say in its comment that the other database's case is `Check_EntryOfAnotherDatabaseThatIsGone_AgreesWithDescribe`'s), `DiagnoseTests.Diagnose_OutIsAFileOfTheRun_IsSQLSRC218` (add `[InlineData("report.sql")]`) and `DiagnoseTests.Diagnose_OutEndsInSql_IsSQLSRC218` (removed).
- The covers keep every assertion: `CheckFilterTests.Check_EntryOfAnotherDatabaseThatIsGone_AgreesWithDescribe` and `DiagnoseTests.Diagnose_OutIsAFileOfTheRun_IsSQLSRC218`.
- `src/SqlSource.Tool/Describing/AGENTS.md:12` names `CheckFilterTests.cs` for the rule that `--check` reports what `describe` with the same filters changes; the class still holds it, so the line stays.
- The epic's phase table: change only row 28.

## Review Focus

None: the change drops one row and moves one case into a theory, and adds no behaviour.  Checked against the spec on `e7ed0e4d` (main after p24): each test reads as its finding says, and each cover exists with the assertions the finding names.

The guard's "before" mutation report is p10's "after" report (tree of `fee7e5e3`): since that commit nothing changed `src/SqlSource.Tool/*.cs` or the scope's tests (`git log fee7e5e3..origin/main` over them is empty), so the epic's step 3 lets it stand as this phase's "before".

---

### Task 0: The guard's "before" half

- [ ] Copy `test-cleanup-epic/reports/p10-tool-root-after.json` of the run's data folder to `artifacts/test-cleanup/phase-28/tool-root/before/reports/mutation-report.json`.
- [ ] `kills` on the before report for each of the three tests.
- [ ] The run time of `dotnet test --project tests/SqlSource.Tool.Tests`, and its coverage into `artifacts/test-cleanup/phase-28/coverage-before/SqlSource.Tool.Tests`, with the line figures of each file of `src/SqlSource.Tool/*.cs`.

### Task 1: The folds

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Commands/CheckFilterTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Commands/DiagnoseTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 28's state)

- [ ] **Step 1:** Drop the `"postgres"` row and update the theory's comment.
- [ ] **Step 2:** Add `[InlineData("report.sql")]` to `Diagnose_OutIsAFileOfTheRun_IsSQLSRC218` and remove `Diagnose_OutEndsInSql_IsSQLSRC218`.
- [ ] **Step 3:** Set row 28 of the epic's table to `done`.
- [ ] **Step 4:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tool.Tests`: every test passes.
- [ ] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-28/tool-root/after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows no removed or changed test made.
- [ ] **Step 2:** The run time and coverage into `artifacts/test-cleanup/phase-28/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/*.cs` falls.
- [ ] **Step 3:** The pull request lists each removed case beside its cover, the run times, the compare result and the coverage figures.
