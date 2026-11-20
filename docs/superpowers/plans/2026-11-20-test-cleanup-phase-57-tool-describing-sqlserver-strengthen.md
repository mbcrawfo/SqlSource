# Test cleanup phase 57: tool describing sqlserver strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `SelectStatementTests.Read_TopDistinct_SkipsThem` fail when a `TOP` count, `PERCENT` or `WITH TIES` is left in the select list, `SelectStatementTests.Read_Groupings_AreRead` read a grouping set from the words `GROUPING SETS` alone, and `DeclaredTypeTextTests.Read_AnythingElse_IsRefusedWithWhatWasFound` refuse a dot after a type's modifier as parts.

**Architecture:** Two test files, `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/SelectStatementTests.cs` and `tests/SqlSource.Tool.Tests/Describing/SqlServer/Statements/DeclaredTypeTextTests.cs`.  No source or helper changes.  The guard is the `tool-describing-sqlserver` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Describing/SqlServer/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-57-tool-describing-sqlserver-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the three tests of the findings change, each by the assertion or row its finding proposes.
- The epic's phase table: change only row 57.

Checked against the spec on `9ca8c2e4`: `SelectBody.cs` still skips the count at line 264, `PERCENT` at 273 and `WITH TIES` at 276-278, and reads `GROUPING SETS` at 450; `DeclaredTypeText.cs:252` still gives `Parts` for a dot in `ReadEnd`.  The tests read as the findings say: `Read_TopDistinct_SkipsThem` asserts only the item's name and `HasTop`, the only `GROUPING SETS` row also holds `()`, and no `Parts` row has a dot after the parentheses.  So all three findings hold.  Phase 15 last ran this scope, and commits since have changed it, so the "before" is a run of its own.  The spec names no document to update, and none names a changed test or its class.

## Review Focus

None beyond the findings: each change adds one assertion or one row that the finding's verifier traced to fail on the named mutants.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-57/tool-describing-sqlserver/before`: 1,590 mutants tested, 1,344 killed, 217 survived, 28 timeouts, 1 error; the tested run took 7 min 36 s.  `kills`: `Read_TopDistinct_SkipsThem` 3 rows, 160 kills, 10 alone, 40 survivors under it; `Read_Groupings_AreRead` 11 rows, 126 kills, 3 alone, 21 survivors under it; `Read_AnythingElse_IsRefusedWithWhatWasFound` 62 rows, 108 kills, 19 alone, 3 survivors under it.  Each survivor the findings name is among them.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-57/coverage-before/SqlSource.Tool.Tests` (7,083 tests, 4 min 20 s): 2,625 of 2,946 lines over the 29 files of `src/SqlSource.Tool/Describing/SqlServer/` that have lines (31 files in the glob).

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/SelectStatementTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Statements/DeclaredTypeTextTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 57's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests.Describing.SqlServer-2-01):** `Read_TopDistinct_SkipsThem` reads the tokens with the statement and asserts, in every row, `Text(tokens, item.Expression).ShouldBe("a")` beside the item's name and `HasTop`.
- [x] **Step 2 (F-SqlSource.Tool.Tests.Describing.SqlServer-2-02):** `Read_Groupings_AreRead` gains the row `[InlineData("GROUP BY GROUPING SETS ((a), (b))", nameof(SelectGrouping.Sets))]`.
- [x] **Step 3 (F-SqlSource.Tool.Tests.Describing.SqlServer-2-04):** `Read_AnythingElse_IsRefusedWithWhatWasFound` gains the row `[InlineData("nvarchar(50).x", TypeNameFound.Parts)]`.
- [x] **Step 4:** Set row 57 of the epic's table to `done`.
- [x] **Step 5:** `./format.sh`, then both test classes pass (382 tests).
- [x] **Step 6:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-57/tool-describing-sqlserver/after`: 1,590 mutants tested, 1,356 killed, 205 survived, 28 timeouts, 1 error; the tested run took 5 min 57 s.  `compare`: 1,372 detected before, 1,372 still detected, 0 lost, 0 not comparable.  Twelve survivors are now `Killed`: the seven the findings name (`SelectBody.cs:264:13` Statement and PostDecrement, `273:13` Statement, `276:61` Arithmetic, `276:72` String, `450:30` and `450:99` String, `DeclaredTypeText.cs:252:16` Conditional (false)), and `SelectBody.cs:271:13` Equality (`at > end`), `271:39` String, `276:13` Equality (`at + 1 > end`) and `276:43` String.  `kills` after: `Read_TopDistinct_SkipsThem` 178 kills, 19 alone, 31 survivors under it; `Read_Groupings_AreRead` 12 rows, 128 kills, 5 alone, 19 survivors under it; `Read_AnythingElse_IsRefusedWithWhatWasFound` 63 rows, 115 kills, 20 alone, 2 survivors under it.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-57/coverage-after/SqlSource.Tool.Tests` (7,085 tests, 3 min 16 s): 2,625 of 2,946 lines, before and after, the same figure in each file.
- [x] **Step 3:** Run time of the coverage runs of `tests/SqlSource.Tool.Tests`: 4 min 20 s before (7,083 tests) and 3 min 16 s after (7,085 tests); the phase adds two rows and no test, and the difference is the machine's load.
- [x] **Step 4:** The pull request gives the change, the compare result, the coverage figures and the run times.
