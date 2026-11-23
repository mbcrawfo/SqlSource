# Test cleanup phase 66: generator driver loosen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make nine driver tests of `tests/SqlSource.Tests/Generator` assert the rule each is about, and not the number of NodaTime's edges, a whole generated file or the indentation between two statements.

**Architecture:** Nine tests changed, one of them renamed, and one constant only it used removed.  No source or helper changes.  The guard is the `generator-root` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource/*.cs`, RoslynFloor, and, for the three findings mutation cannot guard, their covers and the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-66-generator-driver-loosen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the nine tests the spec names change, with `FindEmailMethods`, which only one of them used.
- The epic's phase table: change only row 66.
- Every changed file is compiled again by `tests/SqlSource.Tests.RoslynFloor`, so it uses only what Roslyn 4.14.0 has.

Checked against the spec on `df290936`: each of the nine tests still reads as its finding says, `NodaTimeConversions.Edges` is an `EquatableArray<Converter>` with a `Count`, and `FindEmailMethods` has no other reader.  No document names a test of the phase.  p65's "after" report cannot be this phase's "before", since `main` has changed the scope's tests since it ran, so the "before" is a run of its own.  `main` moved to `26d0b1fd` between the "before" run and the change, with no change under `src/SqlSource`, `tests/SqlSource.Tests` or `tests/SqlSource.Testing`.

## Review Focus

Each test still fails when the rule it names breaks, and no longer fails on a change to what its finding calls incidental.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-66/generator-root/before` (Windows): 238 mutants tested, 210 killed, 9 survived, 19 timeouts, 19 compile errors, 69 ignored; the run took 2 h 12 min.
- [x] `kills` on that report: no test of the phase kills a mutant alone.

  | Test | Kills | Alone | Timeouts under it |
  |----|----|----|----|
  | `Run_NodaTimeReferenceAdded_RunsLibraryConvertersAndConverters` | 56 | 0 | 9 |
  | `Run_UnrelatedEdit_LeavesLibraryConvertersCached` | 49 | 0 | 9 |
  | `Run_ProjectWithoutConverters_GeneratesAsPhase81ApartFromTheRowBuffer` | 60 | 0 | 8 |
  | `Run_ColumnMarkers_LeaveTheColumnNamesAlone` | 43 | 0 | 8 |
  | `Run_NestedType_RepeatsEachContainingType` | 45 | 0 | 4 |
  | `Run_ClassLocation_PutsTheConstantsOnTheType` | 42 | 0 | 4 |
  | `Run_TypeInTheGlobalNamespaceNamedWithAKeyword_HasNoNamespaceBlockAndKeepsTheEscape` | 42 | 0 | 4 |
  | `Run_SqlServerJsonColumnWithJsonOn_ReadsAndWritesThroughJsonConversions` | 68 | 0 | 12 |
  | `Run_RepositoryType_ImplementsItsGeneratedInterface` | 57 | 0 | 9 |

- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-66/coverage-before/SqlSource.Tests` (9,415 tests, 6 min 12 s): 963 of 971 lines over the 11 files of `src/SqlSource/*.cs`.
- [x] Run time of `dotnet test --project tests/SqlSource.Tests`: 9,415 tests in 5 min 50 s.
- [x] `tests/SqlSource.Tests.RoslynFloor`: 2,181 tests pass, in 5 min 38 s.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Generator/CachingConverterTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/ColumnMarkerTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/ConverterMethodTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/GeneratedSourceEscapingTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/LibraryConversionTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/RepositoryInterfaceTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 66's state)

- [x] **Step 1 (F-SqlSource.Tests.Generator-1-03, 1-04):** both caching tests compare the edges with `NodaTimeConversions.Edges.Count` in place of `14`.
- [x] **Step 2 (2-01):** `Run_ProjectWithoutConverters_GeneratesAsPhase81ApartFromTheRowBuffer` becomes `Run_ProjectWithoutConverters_ReadsAndBindsTheTypeAsTheDriverDoes`.  It asserts the typed parameter, its `TypedValue`, the column's read and that neither `FromDatabase` nor `ToDatabase` is called.  `FindEmailMethods` is removed.
- [x] **Step 3 (2-02):** `Run_ColumnMarkers_LeaveTheColumnNamesAlone` asserts the record's declaration and the two `<param>` lines' column names.
- [x] **Step 4 (3-01):** `Run_NestedType_RepeatsEachContainingType` asserts the run of declarations from `namespace App.Data` to `private static partial class Sql`, and the constant's line at its depth.
- [x] **Step 5 (3-02):** `Run_ClassLocation_PutsTheConstantsOnTheType` asserts `partial class Sample`, the public constant's line and no `class Sql`.
- [x] **Step 6 (3-03):** `Run_TypeInTheGlobalNamespaceNamedWithAKeyword_HasNoNamespaceBlockAndKeepsTheEscape` asserts the header followed by `partial struct @class`, no `namespace`, and the constant's line.
- [x] **Step 7 (4-03):** `Run_SqlServerJsonColumnWithJsonOn_ReadsAndWritesThroughJsonConversions` asserts the `SqlDbType` and `Size` statements separately.
- [x] **Step 8 (6-06):** `Run_RepositoryType_ImplementsItsGeneratedInterface` keeps the base list and asserts the interface's declaration and the member's signature.
- [x] **Step 9:** Set row 66 of the epic's table to `done`.
- [ ] **Step 10:** `./format.sh`, `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-66/generator-root/after`, then `compare` against the "before" report.
- [ ] **Step 2:** Coverage into `artifacts/test-cleanup/phase-66/coverage-after/SqlSource.Tests`, compared with the "before" figures.
- [ ] **Step 3:** `tests/SqlSource.Tests.RoslynFloor` passes.
- [ ] **Step 4:** Covers and run time for 2-01, 2-02 and 4-03.
- [ ] **Step 5:** The pull request gives the change, the compare result, the coverage figures and the run times.
