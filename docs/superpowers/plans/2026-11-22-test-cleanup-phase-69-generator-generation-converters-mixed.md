# Test cleanup phase 69: generator generation converters mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove three tests of `LibraryConvertersTests` that their covers already hold, make `Of_TwiceOverEqualProbes_IsEqual` compare two separately built arrays, and stop `SourceOf_NetTopologySuite_IsTheClassOfTheFourInstances` from failing on a comment or on spacing.

**Architecture:** One test class changes, with no source or helper changes.  The guard is the `generator-generation-converters` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Converters/**/*.cs`, and, for the two findings mutation cannot guard, the covers and the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Roslyn syntax trees, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-69-generator-generation-converters-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only `LibraryConvertersTests.cs` changes among the tests.
- The epic's phase table: change only row 69.

Checked against the spec on `cb2f6bd8`: each of the five tests, the static `NodaTimeConversions.Edges`, the early returns of `Merge` and the text of `NetTopologySuiteConversions.BuildSource` still read as the findings say, so all five hold.  The spec names no document to update, and none names a test of the phase.

Where the code differs from the spec's reasoning:

- **F-SqlSource.Tests.Generation-1-04.**  The proposal's probe alone does not reach the array's equality.  `ShouldBe` compares two enumerables item by item, so with `EquatableArray.Equals` changed to compare references the test still passed.  The test therefore calls `Equals` on the two arrays.  The merged arrays are new but hold the same `Converter` instances, so what it holds is the equality of `EquatableArray`, and not that of `Converter`, which the verifier's reason also named.

## Review Focus

- The parsed assertions of the NetTopologySuite test keep the namespace, the class's name and modifiers, the four fields, the static constructor and the count of members, and give up the header, the comment, spacing and the order of the fields.
- Each deleted test's cover still holds what the finding says it does.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-69/generator-generation-converters/before` (Windows): 107 mutants tested, 73 killed, 33 survived, 1 timeout, 0 errors, 58 without coverage; 3 min 55 s.
- [x] `kills` on the "before" report: `Of_TwiceOverEqualProbes_IsEqual` 0 kills, 1 survivor under it; `SourceOf_NetTopologySuite_IsTheClassOfTheFourInstances` 0 kills; `Of_WithNodaTime_IsSortedAsTheSetSortsThem` 0 kills, 1 survivor under it; `Of_EveryEdge_ServesSqlServerAlone` 5 kills, 0 alone, 1 survivor under it; `Of_WithOneLibrary_IsThatLibrarysEdges` 5 kills, 0 alone, 2 survivors under it.
- [x] Run time of `tests/SqlSource.Tests`: 9,236 tests, 0 skipped, 6 min 25 s.
- [x] Coverage into `artifacts/test-cleanup/phase-69/coverage-before/SqlSource.Tests` (9,236 tests, 7 min 47 s): 563 of 570 lines over the 10 files of `src/SqlSource/Generation/Converters/`.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 69's state)

- [x] **Step 1 (F-SqlSource.Tests.Generation-1-10):** Delete `Of_WithNodaTime_IsSortedAsTheSetSortsThem`.
- [x] **Step 2 (F-SqlSource.Tests.Generation-1-11):** Delete `Of_EveryEdge_ServesSqlServerAlone`.
- [x] **Step 3 (F-SqlSource.Tests.Generation-1-12):** Delete `Of_WithOneLibrary_IsThatLibrarysEdges`.
- [x] **Step 4 (F-SqlSource.Tests.Generation-1-04):** `Of_TwiceOverEqualProbes_IsEqual` probes both libraries, so each call merges a new array, and calls `Equals` on the two.
- [x] **Step 5 (F-SqlSource.Tests.Generation-1-05):** `SourceOf_NetTopologySuite_IsTheClassOfTheFourInstances` keeps `source.ShouldBe(NetTopologySuiteConversions.Source)` and asserts the rest from the parsed source.
- [x] **Step 6:** Set row 69 of the epic's table to `done`.
- [x] **Step 7:** `./format.sh`.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-69/generator-generation-converters/after` (Windows): 107 mutants tested, 73 killed, 33 survived, 1 timeout, 0 errors, 58 without coverage; 4 min 45 s.  `compare`: 74 detected before, 74 still detected, 0 lost, 0 not comparable.
- [x] **Step 2:** `kills` on the "after" report: the covers still kill, `Of_WithNodaTime_HasTheFourteenEdges` 2, `Of_WithNodaTimeAndJson_IsBothSortedAsTheSetSortsThem` 20, `Of_WithJson_HasTheTenEdges` 3, `Of_EveryEdge_IsALibraryEdgeWithoutAFaultOrALocation` 1 and `ConverterTargetsTests.Serves_SqlServerEdge_DoesNotServeNpgsql10` 4.  `Of_TwiceOverEqualProbes_IsEqual` goes from 0 kills to 14.
- [x] **Step 3:** Hand check of F-SqlSource.Tests.Generation-1-04: with `EquatableArray.Equals` comparing references, the test fails on both libraries and passes on the old NodaTime probe.
- [x] **Step 4:** Hand check of F-SqlSource.Tests.Generation-1-05: the test passes with the generated comment reworded and the blank lines between the fields dropped, and fails with `IsGeography` false, with the static constructor commented out and with a fifth field added.
- [x] **Step 5:** Run time of `tests/SqlSource.Tests`: 9,233 tests, 0 skipped, 6 min 11 s, against 6 min 25 s before.
- [x] **Step 6:** Coverage into `artifacts/test-cleanup/phase-69/coverage-after/SqlSource.Tests` (9,233 tests, 6 min 19 s, against 7 min 47 s before): 563 of 570 lines, before and after, the same figure in each of the 10 files.
- [ ] **Step 7:** The pull request gives the change, the compare result, the coverage figures, the run times, and each removed test beside its cover.
