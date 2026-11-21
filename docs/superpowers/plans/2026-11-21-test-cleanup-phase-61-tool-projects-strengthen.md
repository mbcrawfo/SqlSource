# Test cleanup phase 61: tool projects strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make three tests of `tests/SqlSource.Tool.Tests/Projects` fail on the survivors their findings name: a value that starts with a character MSBuild reads, a run with no projects that makes its folder, and an unreadable directory named in quotes that loses the system's message.

**Architecture:** Three test files, no source or helper changes.  The guard is the `tool-projects` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Projects/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-61-tool-projects-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the three tests of the findings change.
- The epic's phase table: change only row 61.

Checked against the spec on `3d38e426`: `MSBuildProperty.Escape` still takes its short cut on `IndexOfAny('%', ';', ',') < 0` and no row's value starts with one of the three; `ProjectEvaluator.EvaluateAsync` still returns before it makes the run's folder and deletes that folder in its `finally`, so the test's empty `_temp` holds either way; and `RunUnitFinder.FindIn` still gives the fixed text only for an argument that may not be printed as a path.  So the three findings hold.  No phase has run this scope before, so the "before" is a run of its own.  The spec names no document to update, and none names a changed test.

**Where the code differs from the spec's assumption:** `Find_DirectoryThatCannotBeRead_IsSqlsrc222` skips on Windows and for a privileged user, so on this Windows machine `RunUnitFinder.cs:74:17` is `NoCoverage` and the scope's run cannot show it `Killed`.  That survivor is guarded by a second run of `Projects/RunUnitFinder.cs` with the filter `FullyQualifiedName~SqlSource.Tool.Tests.Projects.RunUnitTests`, on Linux as a non-root user, in the container `mcr.microsoft.com/dotnet/sdk:10.0.401`, from a `git archive` of the commit.

## Review Focus

Each change is one its finding traced to one named mutant.  The temporary folder of `Evaluate_NoProjects_StartsNothingAndMakesNoFolder` is a path below a file, which makes `Directory.CreateDirectory` throw on every platform, so the test's name is now what it checks.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-61/tool-projects/before` (Windows): 222 mutants tested, 210 killed, 11 survived, 1 timeout, 0 errors, 19 without coverage; the tested run took 14 min 5 s.  `MSBuildProperty.cs:25:9` Equality `<= 0` and `ProjectEvaluator.cs:89:9` Block removal survive; `RunUnitFinder.cs:74:17` is `NoCoverage`.  `kills`: `Escape_Value_IsWhatMSBuildReadsBackWhole` 7 rows, 8 kills, 0 alone, 2 survivors under it; `Evaluate_NoProjects_StartsNothingAndMakesNoFolder` 1 row, 0 kills, 1 survivor under it; `Find_DirectoryThatCannotBeRead_IsSqlsrc222` 0 kills, skipped.
- [x] `RunUnitFinder.cs` on Linux as a non-root user: 23 mutants tested, 22 killed, 1 survived, the Conditional (true) at `74:17`.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-61/coverage-before/SqlSource.Tool.Tests` (7,089 tests, 19 skipped, 3 min 9 s): 578 of 589 lines over the 9 files of `src/SqlSource.Tool/Projects/` that hold lines.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Projects/MSBuildPropertyTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Projects/ProjectEvaluatorTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Projects/RunUnitTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 61's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests.Projects-1-01):** `Escape_Value_IsWhatMSBuildReadsBackWhole` gains the row `(";a,b", "%3Ba%2Cb")`.
- [x] **Step 2 (F-SqlSource.Tool.Tests.Projects-1-02):** `Evaluate_NoProjects_StartsNothingAndMakesNoFolder` sets `_temp` to a path below a file, keeps its assertions that the result is empty and nothing is started, and drops the listing of `_temp`, which no longer exists.
- [x] **Step 3 (F-SqlSource.Tool.Tests.Projects-1-03):** `Find_DirectoryThatCannotBeRead_IsSqlsrc222` asserts `ShouldNotContain("the system would not list its files")`.
- [x] **Step 4:** Set row 61 of the epic's table to `done`.
- [x] **Step 5:** `./format.sh`, then the three changed classes pass (102 tests, 2 of them skipped on Windows).
- [ ] **Step 6:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-61/tool-projects/after`, and `compare` lists nothing `LOST` or `NOT COMPARABLE`.
- [ ] **Step 2:** `RunUnitFinder.cs` on Linux as a non-root user: the Conditional (true) at `74:17` is `Killed`.
- [ ] **Step 3:** Coverage into `artifacts/test-cleanup/phase-61/coverage-after/SqlSource.Tool.Tests` does not fall in any file.
- [ ] **Step 4:** The pull request gives the change, the compare result, the coverage figures and the run times.
