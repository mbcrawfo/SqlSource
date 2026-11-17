# Test cleanup phase 15: tool describing SQL Server deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Delete the five tests of `tests/SqlSource.Tool.Tests/Describing/SqlServer` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** Five test deletions, no source change.  The guard is the `tool-describing-sqlserver` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Describing/SqlServer/**/*.cs`, and, for F-SqlSource.Tool.Tests.Describing.SqlServer-1-01, whose test is blind, its named cover and the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-15-tool-describing-sqlserver-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but these five:
  - `SqlServerColumnsTests.OriginOf_ExpressionWithNoType_IsAnExpression`
  - `SqlServerEventsSessionTests.DescribeAsync_BareNullableColumnOfOneTable_WritesNoOperands`
  - `SqlServerSessionProofTests.DescribeAsync_NothingToAsk_NoOperandsCall`
  - `ProofTests.Holds_NotClrBeyondTheAnswers_DoesNotHold`
  - `SqlServerEventsSessionTests.DescribeAsync_ThroughTheLogOfASession_RemovesTheSecretFromTheSqlAndTheMessageOfAnError`
- The covers keep every assertion, and `ProofTests.Holds_OperandBeyondTheAnswers_DoesNotHold` stays (only one of that pair goes).
- No helper is removed: `SqlServerSessionCases.NameColumn`, `SqlServerScript.ExpressionRow`, `SqlServerEventCases.ErrorsOf`, `SessionRunLog` and `ConnectionSecret` all have other callers.
- The epic's phase table: change only row 15.

## Review Focus

None: the change removes five tests and adds no behaviour.  Checked against the spec on `6f7c65f1`: no commit since the review's touched `src/SqlSource.Tool/Describing/SqlServer`, `tests/SqlSource.Tool.Tests/Describing/SqlServer` or `tests/SqlSource.Tool.Tests/SqlServerScript.cs`, so every finding reads as the review wrote it, and every cover exists.  No document outside `docs/superpowers/` names a removed test; `docs/architecture/sqlserver-describer.md:293` (the order of the calls in `SqlServerSessionProofTests`, which its four other tests keep) and `docs/tech-debt/TD-0071-...md:28` (the database project's `ProofTests`) stay true.

---

### Task 0: The guard's "before" half

- [x] The `tool-describing-sqlserver` mutation run into `artifacts/test-cleanup/phase-15/tool-describing-sqlserver/before`.
- [x] `kills` on the before report for each of the five tests.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-15/coverage-before/SqlSource.Tool.Tests`, with the line figures of each file of `src/SqlSource.Tool/Describing/SqlServer/**/*.cs`, and the project's run time.

### Task 1: Delete the tests

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Columns/SqlServerColumnsTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ProofTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerEventsSessionTests.cs` (two tests)
- Modify: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionProofTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 15's state)

- [x] **Step 1:** Remove each test with its attribute and the comment above it; remove a `using` only these tests used.
- [x] **Step 2:** Set row 15 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tool.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows no removed test made.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-15/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/Describing/SqlServer/**/*.cs` falls.
- [x] **Step 3:** The pull request lists each removed test beside its covers, the compare result, the coverage figures and the run times.
