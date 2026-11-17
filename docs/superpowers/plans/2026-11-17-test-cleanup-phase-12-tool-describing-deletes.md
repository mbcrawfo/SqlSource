# Test cleanup phase 12: tool describing deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the one test of `tests/SqlSource.Tool.Tests/Describing` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** One test deletion, no source change.  The guard is the `tool-describing` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Describing/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-12-tool-describing-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `DatabaseRunsFaultTests.Run_DescriptionWithoutTheValue_IsDescribedAsBefore`.
- The covers keep every assertion: `DatabaseRunsFaultTests.Run_DescriptionWithAPasswordTheDescriberGaveAsAColumnsName_IsDescribed` and `DatabaseRunsTests.Run_Describer_DescribesEachQueryInOneSessionForItsDatabase`.
- No helper is removed: `FakeDescriber.ServerWith` and `FakeDescriber.NoRows` have other callers.
- The epic's phase table: change only row 12.

## Review Focus

None: the change removes a test and adds no behaviour.  Checked against the spec on `7367df0c`: no commit since the review's touched `src/SqlSource.Tool/Describing`, `DatabaseRunsFaultTests.cs` or `DatabaseRunsTests.cs`, so the finding reads as the review wrote it, and every cover exists.  No document outside `docs/superpowers/` names the test; the line of `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md` that names its class is about another test, `Run_DescriberThatThrows_IsAFaultWithoutTheConnection`, and stays true.

---

### Task 0: The guard's "before" half

- [x] The `tool-describing` mutation run into `artifacts/test-cleanup/phase-12/tool-describing/before`.
- [x] `kills` on the before report for the test.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-12/coverage-before/SqlSource.Tool.Tests`, with the line figures of each file of `src/SqlSource.Tool/Describing/*.cs`.

### Task 1: Delete the test

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsFaultTests.cs` (remove `Run_DescriptionWithoutTheValue_IsDescribedAsBefore`)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 12's state)

- [x] **Step 1:** Remove the test with its attribute.
- [x] **Step 2:** Set row 12 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tool.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the removed test did not make.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-12/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/Describing/*.cs` falls.
- [x] **Step 3:** The pull request lists the removed test beside its covers, the compare result, the coverage figures and the run times.
