# Test cleanup phase 54: tool describing describers mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove `DescriberTests.DescribeAsync_QueryGivenAFailure_ReturnsItAndDoesNotThrow`, whose round trip of a failure `DescribeAsync_FailureGivenToTheDescriber_ComesBackWithEveryPart` proves part by part, and make `DescriberTests.Constructor_TwoDescribersOfOneDialect_Throws` tell the registry's own refusal from the one of the dictionary it builds.

**Architecture:** One test file, `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberTests.cs`.  No source or helper changes: the two-argument `FakeDescriber.Failure` the deleted test used has many other callers.  The guard is the `tool-describing-describers` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Describing/Describers/**/*.cs`, and, for the delete, its named cover and the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-54-tool-describing-describers-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the two tests of the findings change; the cover keeps every assertion.
- The epic's phase table: change only row 54.

Checked against the spec on `5d285c81`: the deleted test and its cover read as the finding says, the strengthened test asserts only `Should.Throw<ArgumentException>`, and `DescriberRegistry`'s guard and `ToDictionary` are as the finding reads them, so both findings hold.  No earlier phase ran this scope since phase 13, so the "before" is a run of its own.  The spec names no document to update, and none names either test or `DescriberTests`.

## Review Focus

The strengthened test asserts the whole message, with the runtime's ` (Parameter 'describers')` suffix, as the finding proposes, and `ParamName`: `ToDictionary`'s own `ArgumentException` for the duplicate key has neither.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-54/tool-describing-describers/before`: 76 mutants tested, 73 killed, 3 survived, 0 timeouts; the tested run took 1 min 12 s.  `kills`: `Constructor_TwoDescribersOfOneDialect_Throws` 1 row, 0 kills, 3 survivors under it (`DescriberRegistry.cs:18:91`, `21:13`, `21:41`); `DescribeAsync_QueryGivenAFailure_ReturnsItAndDoesNotThrow` 1 row, 0 kills, 0 survivors under it.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-54/coverage-before/SqlSource.Tool.Tests` (7,085 tests, 3 min 1 s): 198 of 212 lines over the 19 files of `src/SqlSource.Tool/Describing/Describers/`.
- [x] Run time of `dotnet test --project tests/SqlSource.Tool.Tests`: 2 min 34 s (7,085 tests).

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 54's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests.Describing.Describers-1-02):** delete `DescribeAsync_QueryGivenAFailure_ReturnsItAndDoesNotThrow`.
- [x] **Step 2 (F-SqlSource.Tool.Tests.Describing.Describers-1-01):** `Constructor_TwoDescribersOfOneDialect_Throws` keeps the exception and asserts `ParamName` `describers` and the message `Two describers are given for the dialect PostgreSql. (Parameter 'describers')`.
- [x] **Step 3:** Set row 54 of the epic's table to `done`.
- [x] **Step 4:** `./format.sh`, then `DescriberTests` passes (19 tests).
- [x] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-54/tool-describing-describers/after`: 76 mutants tested, 76 killed, 0 survived, 0 timeouts; the tested run took 1 min 8 s.  `compare`: 73 detected before, 73 still detected, 0 lost, 0 not comparable.  `kills` for the changed test: 1 row, 4 kills, 3 alone, 0 survivors under it; `DescriberRegistry.cs:18:91`, `21:13` and `21:41` are `Killed`, each by this test alone.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-54/coverage-after/SqlSource.Tool.Tests` (7,084 tests, 3 min 0 s): 198 of 212 lines over the 19 files of `src/SqlSource.Tool/Describing/Describers/`, before and after, the same figure in each file.
- [x] **Step 3:** Run time of `dotnet test --project tests/SqlSource.Tool.Tests` after the change: 2 min 34 s (7,084 tests), against 2 min 34 s before.
- [x] **Step 4:** The pull request gives the change, the compare result, the coverage figures, the deleted test beside its cover and the run times.
