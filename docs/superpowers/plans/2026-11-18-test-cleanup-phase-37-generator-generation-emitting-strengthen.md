# Test cleanup phase 37: generator generation emitting strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Strengthen three tests of `tests/SqlSource.Tests/Generation/Emitting` so that the five mutants their findings name, which survive today, are killed.

**Architecture:** Each finding adds input to one test, and there is no source change.  The guard is the `generator-generation-emitting` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Emitting/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-37-generator-generation-emitting-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Add only the input each finding's proposal names, and keep every existing row and assertion.
- The epic's phase table: change only row 37.

## Review Focus

None: the change adds input to three tests.  Checked against the spec on `ac01c74d`.  `HintName.MakeUnique` still tests `ambiguous.Any(...)`, `PostgresParameterBinder.CanonicalName` still tests `IndexOf('.') >= 0` on the schema and the name, and `SqlServerParameterBinder.TryReadDimension` still stops its loop at `end < name.Length` and tests the same before parsing.  Every finding holds.  The two theories touched do not branch on their row, and the HintName test is a fact, so TD-0089 is unchanged.

No change to `src/SqlSource/Generation/Emitting` or its tests since p21's "after" report (tree of `96711e64`) but a comment in `TypeEmitterTests.cs`, so that report is this phase's "before".

---

### Task 0: The guard's "before" half

- [x] p21's "after" report copied to `artifacts/test-cleanup/phase-37/generator-generation-emitting/before/reports/mutation-report.json`.
- [x] `kills` on the before report for the three tests: each survivor the spec names is `SURVIVED`.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-37/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/Emitting/**/*.cs`.

### Task 1: Add the input

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Emitting/HintNameTests.cs`
- Modify: `tests/SqlSource.Tests/Generation/Emitting/PostgresParameterBinderTests.cs`
- Modify: `tests/SqlSource.Tests/Generation/Emitting/SqlServerParameterBinderTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 37's state)

- [ ] **Step 1:** In `MakeUnique_AmbiguousName_GainsAHashOfItsExactSpelling`, the ambiguous list becomes `TestSidecars.Of("App.Other.g.cs", "App.other.g.cs", "App.Sample.g.cs", "App.sample.g.cs")`; the expected names stay.
- [ ] **Step 2:** Add `[InlineData(".schema", "t")]` and `[InlineData("public", ".b")]` to `Append_DotInTheName_BindsItsBindName`.
- [ ] **Step 3:** Add `[InlineData("vector(3", 20, 0, 0)]` to `Append_TypeWithoutABinding_BindsByValue`.
- [ ] **Step 4:** Set row 37 of the epic's table to `done`.
- [ ] **Step 5:** `./format.sh`, then the Emitting tests pass.
- [ ] **Step 6:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST` and nothing `NOT COMPARABLE`, and each of `HintName.cs:96:17`, `PostgresParameterBinder.cs:144:9`, `PostgresParameterBinder.cs:144:42`, `SqlServerParameterBinder.cs:213:16` and `SqlServerParameterBinder.cs:218:16` (the `end <= name.Length` mutants) is `Killed` in the after report.
- [ ] **Step 2:** Coverage into `artifacts/test-cleanup/phase-37/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Emitting/**/*.cs` falls.
- [ ] **Step 3:** The pull request gives the added input, the compare result, the coverage figures and the run times.
