# Test cleanup phase 58: tool describing sqlserver mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete three SQL Server describer tests whose every assertion a named cover makes, make the test that a failed `declared` call is reported at resolve type run that decision through the session, and give CONVERT's style a row that starts with an integer literal but is not one.

**Architecture:** Three test files: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerErrorsTests.cs`, `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionParameterTests.cs` and `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ExpressionProofsTests.cs`.  No source or helper changes.  The guard is the `tool-describing-sqlserver` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Describing/SqlServer/**/*.cs`, and, for F-SqlSource.Tool.Tests.Describing.SqlServer-1-02, covers and run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-58-tool-describing-sqlserver-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the tests of the findings change, each as its finding proposes; the covers keep every assertion and row that makes them covers.
- The epic's phase table: change only row 58.

Checked against the spec on `05e73c80`: `SqlServerSession.cs:384` still fails a `declared` error with `SqlServerErrors.Failed(request, error, DescribeStep.ResolveType)`, and no session test answers `declared` with an error.  `ExpressionProofs.Calls.cs:248` still reads the style as one integer token, and the "before" report lists `248:34` Logical as surviving under `Of_Form_IsProvedAsExpected` alone.  The deleted tests read as the findings say, and their covers still make each assertion; `SqlServerSessionTableTests.DescribeAsync_TableFails_Is225AtTableMatch` has since also gained a lost-connection case beside the refused rows, and still asserts `SQLSRC225`, `TableMatch` and the line for `table`'s error rows.  So all five findings hold.  Phase 57, of the same scope, merged just before, and nothing has changed the scope's source or tests since its "after" run on `33e7f6aa`, so its "after" report is this phase's "before".  The spec names no document to update, and none names a changed test or its class.

## Review Focus

- The new session test of F-SqlSource.Tool.Tests.Describing.SqlServer-1-02 stands where the proposal puts it, in `SqlServerSessionParameterTests`, and fails when the `ResolveType` argument at `SqlServerSession.cs:384` is changed, checked once by hand.

---

### Task 0: The guard's "before" half

- [x] Mutation: p57's "after" report (`33e7f6aa`), copied into `artifacts/test-cleanup/phase-58/tool-describing-sqlserver/before`: 1,590 mutants tested, 1,356 killed, 205 survived, 28 timeouts, 1 error.  `kills`: `Failed_ErrorAtDeclared_IsAtResolveType` 0 kills, 2 survivors under it; `Columns_TableStep_IsTableMatch` 3 kills, 0 alone, 2 survivors under it; `Of_Form_IsProvedAsExpected` 171 rows, 560 kills, 164 alone, 10 timeouts and 135 survivors under it, `ExpressionProofs.Calls.cs:248:34` Logical among them; `Of_ProvedFromTheTextAlone_AsksNothing` 154 kills, 0 alone, 5 timeouts under it; `Of_FormOfOperands_AsksEachOperand` 196 kills, 0 alone, 5 timeouts under it.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-58/coverage-before/SqlSource.Tool.Tests` (7,085 tests, 4 min 33 s).
- [x] Run time of `dotnet test --project tests/SqlSource.Tool.Tests`: 2 min 53 s (7,085 tests).

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerErrorsTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionParameterTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ExpressionProofsTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 58's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests.Describing.SqlServer-1-03):** Delete `SqlServerErrorsTests.Columns_TableStep_IsTableMatch`.
- [x] **Step 2 (F-SqlSource.Tool.Tests.Describing.SqlServer-1-02):** Delete `SqlServerErrorsTests.Failed_ErrorAtDeclared_IsAtResolveType` and add `SqlServerSessionParameterTests.DescribeAsync_DeclaredAnswersAnError_IsSqlsrc225AtResolveType`: a declared parameter whose `declared` answers `Lost`, asserting only the `declared` call was asked, and `SQLSRC225` at `DescribeStep.ResolveType` with the fixed line.  Check once by hand that it fails with another step at `SqlServerSession.cs:384`.
- [x] **Step 3 (F-SqlSource.Tool.Tests.Describing.SqlServer-2-05):** Delete `ExpressionProofsTests.Of_ProvedFromTheTextAlone_AsksNothing`.
- [x] **Step 4 (F-SqlSource.Tool.Tests.Describing.SqlServer-2-06):** Delete `ExpressionProofsTests.Of_FormOfOperands_AsksEachOperand`.
- [x] **Step 5 (F-SqlSource.Tool.Tests.Describing.SqlServer-2-03):** Add the row `[InlineData("CONVERT(varchar(10), t.d, 23 + @p)", "", false, false, "")]` to `Of_Form_IsProvedAsExpected`, among the CONVERT style rows.
- [x] **Step 6:** Set row 58 of the epic's table to `done`.
- [x] **Step 7:** `./format.sh`, then the changed test classes pass.
- [x] **Step 8:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-58/tool-describing-sqlserver/after`, then `compare` against the "before": nothing `LOST` or `NOT COMPARABLE` but a kill that did not repeat; `ExpressionProofs.Calls.cs:248:34` is `Killed`.
- [ ] **Step 2:** Coverage into `artifacts/test-cleanup/phase-58/coverage-after/SqlSource.Tool.Tests`; the files of `src/SqlSource.Tool/Describing/SqlServer/` keep their line coverage.
- [ ] **Step 3:** Run time of `dotnet test --project tests/SqlSource.Tool.Tests` after.
- [ ] **Step 4:** The pull request gives the change, the compare result, the coverage figures, each removed test beside its cover and the run times.
