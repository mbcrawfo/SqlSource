# Test cleanup phase 34: generator driver strengthening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make two driver tests of `tests/SqlSource.Tests/Generator` assert what their names promise: SQLSRC127 reported beside the missing entry's SQLSRC241 and nothing else, and every name of two clashing pairs of types hashed, which kills `HintName.cs:96:17` (`Any()` to `All()`) at the driver level.

**Architecture:** One assertion replaced and two types added to a test's source, no source change.  The guard is the `generator-root` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource/*.cs`, RoslynFloor, and, since both subjects are outside that scope, the covers and the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-34-generator-driver-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- `RunSqlServer_NegatedListInsideAnAggregateWithoutAnEntry_IsSqlsrc127BesideTheMissingEntry` asserts the whole list, SQLSRC241 for `CountOthers` and `RefusedEmpty`, with `ShouldBe(..., ignoreOrder: true)`, and keeps its assertion on the methods file.
- `Run_TypesAndNamespacesThatDifferOnlyByCase_GetFilesWhoseNamesDifferByMoreThanCase` gains `Extra` and `extra` in `namespace App`, each with `public static string Value => Sql.GetUser;`; its counts become 9 sources and 6 constants, and `App.Other.g.cs` still stays as it is.
- Neither test branches on its row, so TD-0089 does not change.  No document names either class.
- The epic's phase table: change only row 34.

## Review Focus

- Checked against the spec on `d03c0080`: `HintName.MakeUnique` still tests `ambiguous.Any(...)` (now `HintName.cs:96`), and the SQL Server run without an entry still gives SQLSRC241, so both findings hold.
- `HintName.cs:96:17` is outside `generator-root`'s `mutate` glob, so the "after" report cannot hold it.  The phase shows the kill with a run of the same command on `Generation/Emitting/HintName.cs` with `GeneratedFileNameTests` alone, before and after the change.
- The "before" mutation report is p33's "after" (tree `4aa8418f`): no file of `src/SqlSource/*.cs` and no test of the scope's filter changed between that tree and this branch's base.

---

### Task 0: The guard's "before" half

- [x] p33's report copied to `artifacts/test-cleanup/phase-34/generator-root/before/reports/mutation-report.json`.
- [x] `kills` on the before report for both tests.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-34/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/*.cs`.
- [x] The test project's run time, RoslynFloor's tests, and the run on `HintName.cs` with output `artifacts/test-cleanup/phase-34/hintname/before`.

### Task 1: Strengthen the two tests

**Files:**
- Modify: `tests/SqlSource.Tests/Generator/MethodDiagnosticsTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/GeneratedFileNameTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 34's state)

- [x] **Step 1:** Replace the assertion and add the two types.
- [x] **Step 2:** Check by hand that the MethodDiagnostics test fails when its new SQLSRC241 line is removed.
- [x] **Step 3:** Set row 34 of the epic's table to `done`.
- [x] **Step 4:** `./format.sh`, then both classes' tests pass, on the test project and on RoslynFloor.
- [x] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the changed tests did not make.
- [x] **Step 2:** The run on `HintName.cs` with output `artifacts/test-cleanup/phase-34/hintname/after`: `HintName.cs:96:17` is `Killed`.
- [x] **Step 3:** Coverage into `artifacts/test-cleanup/phase-34/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/*.cs` falls; the test project's run time; RoslynFloor passes.
- [ ] **Step 4:** The pull request gives the compare result, the kills, the coverage figures, the covers and the run times.
