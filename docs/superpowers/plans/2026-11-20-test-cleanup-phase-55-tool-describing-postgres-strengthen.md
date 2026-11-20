# Test cleanup phase 55: tool describing postgres strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make four tests of `tests/SqlSource.Tool.Tests/Describing/Postgres` fail on the mutants that survive them: the session's own refusal of fewer types than parameters, a member alias with nothing or more than a number after its last `_`, an operator that is no parameter beside another column's name, and two references to a full join's inner side.

**Architecture:** Three test files, no source or helper changes.  The guard is the `tool-describing-postgres` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Describing/Postgres/**/*.cs`.  Every finding is a strengthen, so no test or row is removed.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-55-tool-describing-postgres-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the four tests of the findings change, each by an assertion or a row.
- The epic's phase table: change only row 55.

Checked against the spec on `ff1da495`: the guard of `ParameterOids` (`PostgresSession.Builders.cs:38-42`), `PrefixOf` (`PlanPartitions.cs:116-120`), `MayNameColumn` (`PlanExpression.cs:184-205`) and `FullJoinSides` (`PlanExpression.Nullability.cs:90-115`) read as the findings say, and so do the four tests, so every finding holds.  Phases 14 and 30 ran this scope before; neither left an "after" report this phase can take, so the "before" is a run of its own.  The spec names no document to update, and none names a test of the phase or its class.

## Review Focus

- F-2-01 asserts the whole message, which names the counts and the query, so the replay's own `CaptureMissException` (also an `InvalidOperationException`) no longer passes, and its comment names "columns" as the call the replay would miss, as the verifier's condition asks.
- F-1-04 adds the two rows beside the existing ones rather than replacing `e` and `e_x`, so that no kill of those rows is lost.
- F-1-04's location, `PlanPartitions.cs:119:16`, holds two survivors in the "before" report: the Logical mutant the finding reasons about, and the Equality mutant `cut >= 0`, which the finding's rows do not kill.  The owner's answer to p55 Q1, "Yes, add the row", adds a third row, `member-with-nothing-before-the-underscore` (aliases `_1` and `_2`), which pins that an empty prefix is no prefix.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-55/tool-describing-postgres/before`: 2,745 mutants tested, 2,312 killed, 383 survived, 48 timeouts, 2 errors; the tested run took 22 min 2 s.  `kills`:
  - `DescribeAsync_FewerTypesThanParameters_ThrowsAndFreesTheStatement`: 1 row, 32 kills, 0 alone, 4 timeouts and 5 survivors under it, `PostgresSession.Builders.cs:38:16` and `40:17` among them.
  - `MembersOf_AppendOfAnotherShape_IsEmpty`: 7 rows, 38 kills, 4 alone, 0 timeouts and 7 survivors under it, `PlanPartitions.cs:119:16` (Logical and Equality), `119:27`, `119:33` and `119:53` among them.
  - `MayNameColumn_Entry_IsWhetherItNamesTheColumnOrHoldsAParameter`: 6 rows, 66 kills, 2 alone, 9 timeouts and 10 survivors under it, `PlanExpression.cs:197:21` among them.
  - `NullableOf_CoalesceOverBothSidesOfAFullJoin_IsNotNull`: 11 rows, 145 kills, 4 alone, 11 timeouts and 76 survivors under it, `PlanExpression.Nullability.cs:109:21` among them.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-55/coverage-before/SqlSource.Tool.Tests` (7,084 tests, 3 min 0 s): 5,031 of 5,645 lines over the 56 files of `src/SqlSource.Tool/Describing/Postgres/` that have a coverable line (57 files in all).

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionParameterTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanPartitionsTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/Expressions/PlanExpressionTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 55's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests.Describing.Postgres-2-01):** `DescribeAsync_FewerTypesThanParameters_ThrowsAndFreesTheStatement` asserts the exception is exactly `InvalidOperationException` with the message `The server resolved 1 types for the 2 parameters of 'Q'.`, and its comment says that without the check the session would go on to ask "columns", which the script does not answer.
- [x] **Step 2 (F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-04):** `MembersOf_AppendOfAnotherShape_IsEmpty` gains the rows `member-with-nothing-after-the-underscore` (alias `e_`) and `member-with-a-number-then-a-word` (alias `e_1x`), and, by the owner's answer to p55 Q1, `member-with-nothing-before-the-underscore` (aliases `_1` and `_2`).
- [x] **Step 3 (F-SqlSource.Tool.Tests.Describing.Postgres.Walk.Expressions-1-01):** `MayNameColumn_Entry_IsWhetherItNamesTheColumnOrHoldsAParameter` gains the row `("(u.name || 'x'::text)", "id", false)`.
- [x] **Step 4 (F-SqlSource.Tool.Tests.Describing.Postgres.Walk.Expressions-1-02):** `NullableOf_CoalesceOverBothSidesOfAFullJoin_IsNotNull` gains the row `("COALESCE(a.id, r.account_id, r.account_id)", false, BuiltInTypes.Int4)`.
- [x] **Step 5:** Set row 55 of the epic's table to `done`.
- [x] **Step 6:** `./format.sh`, then the three classes pass (487 tests).
- [ ] **Step 7:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-55/tool-describing-postgres/after`, `compare` against the "before", and each survivor the findings name `Killed`.
- [ ] **Step 2:** Coverage into `artifacts/test-cleanup/phase-55/coverage-after/SqlSource.Tool.Tests`, compared file by file.
- [ ] **Step 3:** The pull request gives the change, the compare result and the coverage figures.
