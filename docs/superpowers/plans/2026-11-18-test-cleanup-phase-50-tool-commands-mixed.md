# Test cleanup phase 50: tool commands mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the six findings of phase 50 to `tests/SqlSource.Tool.Tests/Commands`: delete two replay tests whose rule the exact outputs of other tests hold, fold the four `describe --help` tests into one, fold the two replay control-character theories to their ESC row, and make `DescribeTests.Run_HelpOfTheTool_ListsTheCommand` assert the command's own entry in the help.

**Architecture:** Test-only change in `DescribeTests.cs`, `FilterTests.cs` and `ReplayTests.cs`.  No source, helper or document change but the epic's row 50.  The guard is the `tool-root` mutation scope, the coverage of `src/SqlSource.Tool/*.cs`, and covers and run time for the four findings mutation cannot guard.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-50-tool-commands-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the named tests change.  The epic's phase table: change only row 50.
- None of the touched theories is in TD-0089, and none branches on its row: TD-0089 is not touched.
- `docs/architecture/bundles.md:79` names `ReplayTests` for its fixture replays, which stay: no document changes.

Checked against the spec on `f409e747`: each test the findings name is where they say and reads as they say, and every named cover is there.  No merged phase changed `src/SqlSource.Tool/*.cs` or the scope's tests since p49's run on `f932cf50`, so p49's "after" report is this phase's "before".  `kills` on it gives each test the counts of the spec (16, 63, 48, 49, 47, 53, 36, 55, 57).

Where the code or precedent settles what the spec leaves open:

- **F-…-1-02:** the folded test is `DescribeTests.Run_Help_ShowsThePathsAndTheOptions`, which loops over the union of the four tests' texts as `DiagnoseTests.Diagnose_Help_ListsTheOptions` does, and asserts exit 0, an empty error and no process.  `Run_Help_ShowsThePath`, `Run_Help_ShowsTheProjectOption`, `Help_Describe_ListsWatch` and `FilterTests.Run_Help_ShowsThePathsAndTheOptionsOfDescribe` are removed.
- **F-…-2-04, F-…-2-05:** with one row left, the two theories become facts on ESC under their names and `Controls` is removed, as phases 29 and 31 did with a one-row theory.
- **F-…-1-01:** the help's list of commands writes `describe <path>` indented; the test asserts that a line of the output, its leading spaces trimmed, starts with `describe ` compared ordinally, so case counts and the root's description ("Describes …") no longer satisfies it.

## Review Focus

The folded help test keeps every text, the empty error and the no-process assertion of the four it replaces.

---

### Task 0: The guard's "before" half

- [x] Mutation: p49's `reports/p49-tool-root-after.json` (tree of `f932cf50`) copied to `artifacts/test-cleanup/phase-50/tool-root/before/reports/mutation-report.json`; `kills` for the nine tests saved beside it.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-50/coverage-before/SqlSource.Tool.Tests` (7,077 tests, 3 min 23 s).
- [x] Run time of `dotnet test --project tests/SqlSource.Tool.Tests`: 2 min 30 s (7,077 tests, load ~20).

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Commands/DescribeTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Commands/FilterTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 50's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests.Commands-2-02):** delete `ReplayTests.Replay_Failed_HasNoLineThatPointsToALog`.
- [x] **Step 2 (F-SqlSource.Tool.Tests.Commands-2-03):** delete `ReplayTests.Replay_LogPathWithAnEqualsSign_IsAWrongCommandLine`.
- [x] **Step 3 (F-SqlSource.Tool.Tests.Commands-2-04, -2-05):** remove `ReplayTests.Controls`; `Replay_ToolVersionWithAControlCharacter_PrintsItsEscape` and `Replay_FailureWithAControlCharacter_PrintsItsEscape` become facts on ESC, `"\u001b"` printed as `"\\u001B"`.
- [x] **Step 4 (F-SqlSource.Tool.Tests.Commands-1-02):** replace the three `DescribeTests` help tests with `Run_Help_ShowsThePathsAndTheOptions`; delete `FilterTests.Run_Help_ShowsThePathsAndTheOptionsOfDescribe`.
- [x] **Step 5 (F-SqlSource.Tool.Tests.Commands-1-01):** `Run_HelpOfTheTool_ListsTheCommand` asserts the line of the command's entry.
- [ ] **Step 6:** Set row 50 of the epic's table to `done`.
- [ ] **Step 7:** `./format.sh`, then `DescribeTests`, `FilterTests` and `ReplayTests` pass.
- [ ] **Step 8:** Hand check of F-…-1-01: with the `describe` command hidden from the help, the new test fails and the old assertion passes.  Reverted.
- [ ] **Step 9:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-50/tool-root/after`, then `compare` with the "before".
- [ ] **Step 2:** Coverage into `artifacts/test-cleanup/phase-50/coverage-after/SqlSource.Tool.Tests`, compared file by file with the "before".
- [ ] **Step 3:** Run time of `dotnet test --project tests/SqlSource.Tool.Tests`.
- [ ] **Step 4:** Copy the "after" report to the run's `reports/p50-tool-root-after.json`.
- [ ] **Step 5:** The pull request gives the changes, the compare result, the coverage figures, the hand check, the covers table and the run times.
