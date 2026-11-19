# Test cleanup phase 48: generator snapshot mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete two tests of `tests/SqlSource.Tests/Snapshot` that their covers already prove, and strengthen four so that each survivor its finding names is killed.

**Architecture:** Test-only change: two facts removed, rows added to two theories and one fact made a theory, and a second entry added to one fact's file.  The guard is the `generator-snapshot` mutation scope and the line coverage of `src/SqlSource/Snapshot/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-48-generator-snapshot-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the six tests the findings name change; no source change.
- The covers keep every assertion that makes them so: the `"h", "app", true` row of `IsCurrentFor_HashAndDatabase_HoldsWhenBothAreTheEntrys`, `Read_UsersExample_ReadsAnEntryWithoutRows` and `Read_OrdersExample_ReadsTheShapeOfSqlServerTypes`.
- None of the changed theories branches on its row (the new `Read_BindNameThatIsNotAString_IsWrongTypeAtTheValue` takes the type's text as a row value), so TD-0089 does not change.
- The epic's phase table: change only row 48.

## Review Focus

- Each new row's `at` must be the first place it is found in the whole file's text, since `ShouldBeMalformed` takes the first `IndexOf`.
- The comparer rows must each change two keys of one parameter or type, and expect the earlier key's path, so that swapping either side of the `??` chain is seen.

Checked against the spec on `d71714e6`.  Every finding still reads as the review wrote it: `SidecarComparer.cs` lines 72-74 and 144 and `SidecarTypeReader.cs` lines 98, 228, 231, 237, 240, 271, 295 and 320 are the lines the findings name.  No merged phase changed `src/SqlSource/Snapshot` or `tests/SqlSource.Tests/Snapshot` since p08 (`git log 3c6c6f4b..origin/main` on both is empty), so p08's "after" report was taken as this phase's "before".  In it every survivor of the scope, the named ones included, was a `Timeout` (0 survived, 83 timeouts), so `compare` against it listed 38 survivors this phase did not touch as `LOST`; the phase ran its own "before" on this tree with the old tests instead, and keeps p08's report beside it as `before-p08`.

No document names a test of the phase, and the lines that name its classes (`docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md`, `src/SqlSource/Snapshot/AGENTS.md`) describe nothing the phase removes, so no document changes.

---

### Task 0: The guard's "before" half

- [x] Copy `reports/p08-generator-snapshot-after.json` (tree of `3c6c6f4b`) to `artifacts/test-cleanup/phase-48/generator-snapshot/before-p08/reports/mutation-report.json`.
- [x] Run the scope on this tree with the two test files of `d71714e6` into `artifacts/test-cleanup/phase-48/generator-snapshot/before`: 849 killed, 32 survived, 42 timeouts, 3 errors, 1 h 4 min, load 27 rising to 165.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-48/coverage-before/SqlSource.Tests`.
- [x] `kills` on the before report for each of the six tests: the two deleted ones kill nothing alone (`IsCurrentFor_SameHashDatabaseAndVersion_IsTrue` 0 kills; `Read_EntryWithoutRows_HasNoColumns` 153 kills, 0 alone, 11 timeouts under it, on p08's report).

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Snapshot/SidecarComparerTests.cs`
- Modify: `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 48's state)

- [x] **Step 1 (1-01):** delete `SidecarComparerTests.IsCurrentFor_SameHashDatabaseAndVersion_IsTrue`.
- [x] **Step 2 (1-05):** delete `SidecarReaderTests.Read_EntryWithoutRows_HasNoColumns`.
- [x] **Step 3 (1-06):** `FindDifference_TwoDifferences_IsTheFirstInTheOrder` gains, before its `parameters[1].nullable` row, four rows on the `status` parameter: name and ordinal (`parameters[1].name`, kills 72:17), ordinal and type (`parameters[1].ordinal`, 73:20), type and nullable (`parameters[1].type.name`, 74:20), and a bind name with the kind (`parameters[1].type.bindName`, 144:16).
- [x] **Step 4 (1-07):** `Read_NullUnderEveryKeyAReaderCanDoWithout_ReadsNull`'s file gains a SQL Server entry `R` with two columns: one whose type has `'userType': null`, one whose user type has `'schema'`, `'name'` and `'assemblyQualifiedName'` null; the test asserts both types (271:63, 295:76).
- [x] **Step 5 (1-08):** `Read_TypeWithAValueOfAnotherJsonType_IsWrongTypeAtTheValue` gains `mssql` rows with `'name': null`, `'precision': null` and `'scale': null`, each `WrongType` at the null (228:59, 237:63, 240:59).
- [x] **Step 6 (1-09):** `Read_BindNameThatIsNotAString_IsWrongTypeAtTheValue` becomes a theory over `(engine, type, at, length)`: the existing PostgreSQL `3`, and `null` under `postgres`, `mssql` and `sqlite` parameter types (98:63, 231:63, 320:63).
- [x] **Step 7:** Row 48 of the epic's table is `done`.
- [x] **Step 8:** `./format.sh`, then the Snapshot tests pass: `dotnet test --project tests/SqlSource.Tests -- --filter-namespace 'SqlSource.Tests.Snapshot*'`.  The "after" mutation run is the check that each changed test fails on its mutant.
- [x] **Step 9:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] `tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-48/generator-snapshot/after --tests 'FullyQualifiedName~SqlSource.Tests.Snapshot.' 'Snapshot/**/*.cs'`: 862 killed, 27 survived, 34 timeouts, 3 errors, 1 h 2 min, load 6 rising to 76.  Each of the twelve named survivors is `Killed`.  `compare` lists two `LOST`, `SidecarTypeReader.cs:255:16` Conditional (true) and Logical, each `Timeout` before and `Survived` after, and nothing `NOT COMPARABLE`.  Both are equivalent: the reader keeps no model once `cursor.Error` is set, and `name` is set whenever it is not.  By hand, on the old tests, each mutant passes all 476 Snapshot tests in under 20 s, while its Conditional (false) twin fails 8, as the report's 8 killers say; their twins at 329:16 of `ReadOther` are `Timeout` with no killer in both runs.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-48/coverage-after/SqlSource.Tests`: 9236 tests before, 9240 after; the 22 files of `src/SqlSource/Snapshot` cover 1578 of 1600 lines before and after, each file unchanged.
- [x] Copy the "after" report to `reports/p48-generator-snapshot-after.json` and write `p48-notes.md` in the shared notes folder.
