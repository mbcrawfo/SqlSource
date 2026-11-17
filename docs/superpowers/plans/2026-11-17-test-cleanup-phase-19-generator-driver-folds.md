# Test cleanup phase 19: generator driver folds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Fold the eight findings of `tests/SqlSource.Tests/Generator` that the test review found to repeat a cover, dropping rows and merging two facts into one theory, with the guard showing nothing lost.

**Architecture:** Row drops in five test files and one merge of two facts into a theory; no source change.  The guard is the `generator-root` mutation scope, the coverage of the files under its `mutate` glob (`src/SqlSource/*.cs`), RoslynFloor before and after, and, for the six findings mutation cannot guard, the covers and the run time of `tests/SqlSource.Tests`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-19-generator-driver-folds-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but the nine the spec names.  Every file keeps its first line, the `// Shared:` line `GeneratorFolderTests` checks, and uses only Roslyn 4.14.0 API and C# 12 (RoslynFloor compiles it again).
- `DialectTests.Run_File_IsReadByItsMarkerThenItsFoldersFileThenTheProjectsFile` is the cover of F-…-6-09 to 6-11 (phase 01): it keeps every row but the two the spec drops.
- A comment that names only a dropped row goes with it; a comment the drop makes untrue is reworded.  `docs/tech-debt/TD-0089` line 19 stays: the array-pool theory keeps its rows with a folder's file and its `if`.
- The epic's phase table: change only the row matched by `| 19 |`.

## Review Focus

- **The merged placement theory's name.**  Its two rows report SQLSRC123 at the method (header) or at its marker (query), so the theory is named `Run_NestedClassMadePublicByAnotherFileOfATypeReachableAsProtected_ReportsSqlsrc123AtTheMethodOrMarker`; the PR's cover table names the old fact and the theory that replaces it.  No later phase's spec names either fact.

Checked against the spec on `09fb7ee7`: no commit since the review's commit `36864a86` touched the five test files, and `src/SqlSource/Settings`, `Parsing/Markers`, `SettingsFiles`, `Generation/Models/PropertyNames.cs` and `SqlIdentifier.cs` changed only by an `AGENTS.md`, so every finding reads as the review wrote it.  Every named cover exists.  p01's generator-root report cannot serve as "before": #313 changed golden files of `tests/SqlSource.Tests/Generator` after its tree.

---

### Task 0: The guard's "before" half

- [x] The `generator-root` mutation run into `artifacts/test-cleanup/phase-19/generator-root/before`.
- [x] `kills` on the before report for each of the nine tests.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-19/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/*.cs`.
- [x] `dotnet test --project tests/SqlSource.Tests.RoslynFloor` passes.

### Task 1: Fold the rows and merge the facts

**Files:**
- Modify: `tests/SqlSource.Tests/Generator/ArrayPoolParameterTests.cs` (F-1-02: drop `(Unpooled, Other, null, null, false)`, `(Default, Other, Unpooled, null, false)`, `(null, null, Unpooled, Other, false)`; `Other` goes if nothing else uses it)
- Modify: `tests/SqlSource.Tests/Generator/DialectTests.cs` (F-2-05: drop the `"MySQL"` and `"tsql"` project rows, the comment above becomes "The project's file alone."; F-2-04: drop the three spelling rows and their two comments; F-2-06: the CockroachDB theory becomes a `[Fact]` under `cockroachdb`, its comment losing "under either of its names")
- Modify: `tests/SqlSource.Tests/Generator/ModelNameTests.cs` (F-5-03: keep `"EqualityContract"` alone; F-5-04: keep `("input-model", "file")`, `("output-model", "scoped")`, `("modelNamespace", "extension")`)
- Modify: `tests/SqlSource.Tests/Generator/NestedClassTests.cs` (F-5-05: keep `("file", false)` and `("var", true)`)
- Modify: `tests/SqlSource.Tests/Generator/PlacementDiagnosticsTests.cs` (F-5-07: one theory over the place of `-- member-location:` and the span, each row asserting the header fact's whole check)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 19's state)

- [x] **Step 1:** Apply each fold as above.
- [x] **Step 2:** Set row 19 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tests` and `tests/SqlSource.Tests.RoslynFloor`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows no changed test made.
- [ ] **Step 2:** Coverage into `artifacts/test-cleanup/phase-19/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/*.cs` falls.  RoslynFloor passes.
- [ ] **Step 3:** Run time of `dotnet test --project tests/SqlSource.Tests` before and after, interleaved on this machine with the load beside each figure.
- [ ] **Step 4:** The pull request lists each dropped row or merged test beside its covers, the compare result, the coverage figures and the run times.
