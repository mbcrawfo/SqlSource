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

- [ ] Mutation into `artifacts/test-cleanup/phase-57/tool-describing-sqlserver/before`, with `kills` for each of the three tests.
- [ ] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-57/coverage-before/SqlSource.Tool.Tests`, with the line figures of `src/SqlSource.Tool/Describing/SqlServer/**/*.cs`, and the project's run time.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/SelectStatementTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Statements/DeclaredTypeTextTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 57's state)

- [ ] **Step 1 (F-SqlSource.Tool.Tests.Describing.SqlServer-2-01):** `Read_TopDistinct_SkipsThem` reads the tokens with the statement and asserts, in every row, `Text(tokens, item.Expression).ShouldBe("a")` beside the item's name and `HasTop`.
- [ ] **Step 2 (F-SqlSource.Tool.Tests.Describing.SqlServer-2-02):** `Read_Groupings_AreRead` gains the row `[InlineData("GROUP BY GROUPING SETS ((a), (b))", nameof(SelectGrouping.Sets))]`.
- [ ] **Step 3 (F-SqlSource.Tool.Tests.Describing.SqlServer-2-04):** `Read_AnythingElse_IsRefusedWithWhatWasFound` gains the row `[InlineData("nvarchar(50).x", TypeNameFound.Parts)]`.
- [ ] **Step 4:** Set row 57 of the epic's table to `done`.
- [ ] **Step 5:** `./format.sh`, then both test classes pass.
- [ ] **Step 6:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-57/tool-describing-sqlserver/after`, then `compare`: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` on the "before" report shows no changed test made.  Each survivor the findings name is `Killed`.
- [ ] **Step 2:** Coverage into `artifacts/test-cleanup/phase-57/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/Describing/SqlServer/**/*.cs` falls.
- [ ] **Step 3:** Run time of `dotnet test --project tests/SqlSource.Tool.Tests` after the change.
- [ ] **Step 4:** The pull request gives the change, the compare result, the coverage figures and the run times.
