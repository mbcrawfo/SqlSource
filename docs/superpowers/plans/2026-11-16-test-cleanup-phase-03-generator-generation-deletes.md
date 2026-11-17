# Test cleanup phase 03: generator generation deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the one test of `tests/SqlSource.Tests/Generation` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** One test deletion, no source change.  The guard is the `generator-generation` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-03-generator-generation-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `SqlFileReaderTests.Read_Dialect_IsTheDialectTheFileIsParsedWith`.
- Its covers, `Read_FileParseInput_ReadsTheFileWithItsDialect` and `Read_FileWithErrors_GivesEachErrorWithItsLineAndColumnAndNoQueries`, keep every assertion.
- The epic's phase table: change only row 03.

## Review Focus

None: the change removes a test and adds no behaviour.  Checked against the spec on `e6fd7aaa`: neither `SqlFileReader.cs` nor `SqlFileReaderTests.cs` has changed since the review's commit, the finding reads as the review wrote it, and both covers exist.

---

### Task 0: The guard's "before" half

- [x] The `generator-generation` mutation run into `artifacts/test-cleanup/phase-03/generator-generation/before`.
- [x] `kills` on the before report for the removed test.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-03/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/*.cs`.

### Task 1: Delete the test

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs` (remove `Read_Dialect_IsTheDialectTheFileIsParsedWith`)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 03's state)

- [x] **Step 1:** Remove the test with its `[Fact]`, and any `using` or helper only it needed.
- [x] **Step 2:** Set row 03 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the removed test did not make.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-03/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/*.cs` falls.
- [x] **Step 3:** The pull request lists the removed test beside its covers, the compare result and the coverage figures.
