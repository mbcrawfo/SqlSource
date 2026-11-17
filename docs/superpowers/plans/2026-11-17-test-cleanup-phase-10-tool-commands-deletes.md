# Test cleanup phase 10: tool commands deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the six tests of `ExitCodeTests` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** Six test deletions in one file, no source change.  The guard is the `tool-root` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-10-tool-commands-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but these six of `tests/SqlSource.Tool.Tests/Commands/ExitCodeTests.cs`: `Run_NothingReported_ExitsZeroWithoutTheLine`, `Run_ErrorWithoutALog_ExitsOneAndEndsWithTheLine`, `Run_DescriberThatThrows_ExitsOneAndEndsWithTheLine`, `Run_CheckThatFindsADifference_ExitsTwoWithoutTheLine`, `Run_CancelledBeforeItStarted_ExitsOneAndPrintsNothing` and `Run_NoUnit_ExitsOneWithTheLine`.
- The covers keep every assertion: `DescribeRunTests.Run_QueriesWithoutASidecar_AreDescribedAndTheSidecarIsWritten`, `DescribeRunTests.Run_OneQueryFails_TheOthersAreDescribedAndTheFileIsNotWritten`, `DescribeRunTests.Run_DescriberThatThrows_IsSqlsrc200WithoutTheConnectionAndNothingIsWritten`, `CheckTests.Check_NoSidecar_IsOneDifferenceAtTheFirstQueryAndExitsTwo`, `CliTests.Run_CancelledBeforeItStarts_PrintsNothingAndExitsWithOne`, `DescribeTests.Run_CancelledWhileMSBuildRuns_ExitsWithOneAndPrintsNothing` and `DescribeTests.Run_NoUnit_ReportsItAndExitsWithOne`.
- A helper only the deleted tests used goes with them: the field `_users` of `ExitCodeTests`, which only `Run_ErrorWithoutALog_ExitsOneAndEndsWithTheLine` reads (the constructor still adds `Users.sql`, which every other test runs on), and the `using` directives nothing else needs.  `CliRun.RunCancelledAsync` stays: `CliTests` uses it.
- The class comment of `ExitCodeTests`, "The three exit codes, and the one line …", is no longer true once the tests of exit codes 0 and 2 go, since every test left exits 1: it becomes "Which failed runs end with the one line that tells a user how to get a record of the run."
- The epic's phase table: change only row 10.

## Review Focus

None: the change removes six tests and adds no behaviour.  Checked against the spec on `d52c3ef5` (main after p09): each test reads as its finding says, and each cover exists with the assertions the finding names.  No document names a removed test or `ExitCodeTests`.

The guard's "before" mutation report is p09's "after" report (tree of `68aa2ba8`): since that commit nothing changed `src/SqlSource.Tool` or `tests/SqlSource.Tool.Tests` (the only changes are p08's, in `tests/SqlSource.Tests/Snapshot`, and documents), so the epic's step 3 lets it stand as this phase's "before".

---

### Task 0: The guard's "before" half

- [ ] Copy `test-cleanup-epic/reports/p09-tool-root-after.json` of the run's data folder to `artifacts/test-cleanup/phase-10/tool-root/before/reports/mutation-report.json`.
- [ ] `kills` on the before report for each of the six tests.
- [ ] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-10/coverage-before/SqlSource.Tool.Tests`, with the line figures of each file of `src/SqlSource.Tool/*.cs`.

### Task 1: Delete the tests

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Commands/ExitCodeTests.cs` (remove the six tests with their comments, the field `_users` and unused `using` directives)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 10's state)

- [ ] **Step 1:** Remove the six tests with their attributes and comments.
- [ ] **Step 2:** Set row 10 of the epic's table to `done`.
- [ ] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tool.Tests`: every test passes.
- [ ] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-10/tool-root/after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows no removed test made.
- [ ] **Step 2:** Coverage into `artifacts/test-cleanup/phase-10/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/*.cs` falls.
- [ ] **Step 3:** The pull request lists each removed test beside its cover, the compare result and the coverage figures.
