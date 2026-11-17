# Test cleanup phase 14: tool describing postgres deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the sixteen tests of `tests/SqlSource.Tool.Tests/Describing/Postgres` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** Sixteen test deletions and two comments moved, no source change.  The guard is the `tool-describing-postgres` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Describing/Postgres/**/*.cs`, and, for the seven findings mutation cannot guard, each removed test beside its cover and the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-14-tool-describing-postgres-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but the sixteen the spec names; every cover keeps every assertion and row.
- No helper is removed: `ConnectionTexts`, `SessionRunLog`, `ConnectionSecret`, `CountColumn`, `WholeScript`, `Scan` and `Origin` each have other callers in their files.
- The comments of the two deleted partitioned-table facts of `ColumnNullabilityTests` move above `Of_NotNullColumnOfAPartitionedTableWithoutAForeignPartitionOrUnsaid_IsNotNullableByTheCatalog`, as one comment: "A partitioned table enforces its constraints as a table does, and a partition can never drop its parent's NOT NULL." before the theory's own sentence (F-…Postgres-2-02 and -2-05).
- The epic's phase table: change only row 14.

## Review Focus

None: the change removes tests and adds no behaviour.  Checked against the spec on `89f873f6`: no commit since the review's commit `36864a86` touched `src/SqlSource.Tool/Describing/Postgres`, `tests/SqlSource.Tool.Tests/Describing/Postgres`, `src/SqlSource.Tool/Logging`, `SessionRunLogTests.cs`, `RunLogRedactionTests.cs` or `CatalogPinTests.cs`, so every finding reads as the review wrote it, and every cover exists.  No document outside `docs/superpowers/` names a test of the phase; `src/SqlSource.Tool/Describing/Postgres/AGENTS.md:41` and `tests/AGENTS.md:55` name `NpgsqlCheckTests` of `tests/DatabaseTests` and stay true.

---

### Task 0: The guard's "before" half

- [x] The `tool-describing-postgres` mutation run into `artifacts/test-cleanup/phase-14/tool-describing-postgres/before`.
- [x] `kills` on the before report for each of the sixteen tests.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-14/coverage-before/SqlSource.Tool.Tests`, with the line figures of each file of `src/SqlSource.Tool/Describing/Postgres/**/*.cs`.
- [x] Run time of `dotnet test --project tests/SqlSource.Tool.Tests`, with the load beside it.

### Task 1: Delete the tests

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/NpgsqlCheckTests.cs` (remove `Read_Error_DiffersWithItsMessage`, `Read_ColumnsEqualInCountNamesAndTypes_IsSame`)
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs` (remove `Check_VerdictErrorOfALostConnection_IsSqlsrc225WithItsText`, `Check_TextRefusedByNpgsql_IsSqlsrc231`, `SchemaChanged_TypeThatWentAtResolveType_KeepsTheStepItWasGiven`)
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsColumnTests.cs`, `PostgresEventsRelationTests.cs`, `PostgresEventsTests.cs` (remove each `DescribeAsync_ThroughTheLogOfASession_RemovesTheSecretFrom…` test with its comment)
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/Columns/ColumnNullabilityTests.cs` (remove `Of_NotNullColumnOfATable_…`, `Of_NullableColumnOfATable_…`, `Of_NotNullColumnOfAPartitionedTable_…`, `Of_NotNullColumnOfAPartitionedTableWithPartitions_…`; move their comments as the constraints say)
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkNotNullTests.cs` (remove `Run_NotNullColumnOfATableWithChildren_…`, `Run_NotNullColumnOfAPartitionedTableWithAForeignPartition_…`)
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanNodeTests.cs` (remove `SelfAndDescendants_Leaf_IsTheNodeAlone`)
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/Expressions/CatalogOverloadsTests.cs` (remove `Overloads_Count_IsEveryOverloadOfTheNames`)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 14's state)

- [x] **Step 1:** Remove each test with its attribute and the comment that is its own; a `using` left unused goes too.
- [x] **Step 2:** Set row 14 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tool.Tests`: every test passes, sixteen fewer than before.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows no removed test made.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-14/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/Describing/Postgres/**/*.cs` falls.
- [x] **Step 3:** Run time of the test project after, on the same machine.
- [x] **Step 4:** The pull request lists each removed test beside its covers, the compare result, the coverage figures and the run times.
