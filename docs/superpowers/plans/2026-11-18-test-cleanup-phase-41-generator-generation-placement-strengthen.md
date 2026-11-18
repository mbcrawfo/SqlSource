# Test cleanup phase 41: generator generation placement strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Make `TypePlacementTests.Of_Protected_IsAnErrorOnlyWhereCSharpForbidsIt` expect the location as a marker writes it, and give the nested class of `TypePlacementTests.Of_NestedClassNamedLikeAMemberOfTheType_ReportsSqlsrc007OnceAndDropsItsFiles` a third query, so that the two survivors the findings name are killed.

**Architecture:** One theory's rows give their location as marker text and its expected diagnostic takes that literal in place of `PlacementChecks.TextOf`; one fact gains a query.  There is no source change.  The guard is the `generator-generation-placement` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Placement/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-41-generator-generation-placement-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- `Of_Protected_IsAnErrorOnlyWhereCSharpForbidsIt`: each row's first value is its location as a marker writes it, `"class"` or `"nested-class"`, in place of the enum's name, and the expected SQLSRC123 takes that value in place of `PlacementChecks.TextOf(location)`.  The test maps the text to `MemberLocation` with a switch expression of its own.  The rows, their order and their other values stay.
- `Of_NestedClassNamedLikeAMemberOfTheType_ReportsSqlsrc007OnceAndDropsItsFiles`: a third query, `CountUsers`, in the nested class `Queries` of `Users.sql`; the one expected SQLSRC007 and `Usable` `[false, true]` stay.
- The epic's phase table: change only row 41.

## Review Focus

- F-2-12: the spec says "give each row its location as a marker writes it".  The row's location is given once, as marker text, rather than as a second parameter beside the enum's name that could disagree with it; the switch that maps it to `MemberLocation` only derives the value passed to the query, so by the `td0089` sorting rule it adds no branch, and TD-0089's entry for this theory (list 1, on `reason`) stays as it is.

Checked against the spec on `d4035cc1`.  `PlacementChecks.TextOf` still gives `"nested-class"` at `PlacementChecks.cs:118`, and `CheckTakenClassNames` still skips on `!taken.Add(name)` at `TypePlacement.cs:252`; both findings hold.  No earlier phase of this run changed the scope, so this phase runs its own "before".

---

### Task 0: The guard's "before" half

- [x] Mutation run into `artifacts/test-cleanup/phase-41/generator-generation-placement/before`: 192 mutants, 178 killed, 14 survived, 0 timeouts, 1 min 32 s.
- [x] `kills` on the before report: `PlacementChecks.cs:118:43` survives under `Of_Protected_IsAnErrorOnlyWhereCSharpForbidsIt`, `TypePlacement.cs:252:24` under `Of_NestedClassNamedLikeAMemberOfTheType_ReportsSqlsrc007OnceAndDropsItsFiles`.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-41/coverage-before/SqlSource.Tests` (9223 tests, 11 min 5 s under load): `PlaceText.cs` 6/7, `PlacementChecks.cs` 53/59, `TypePlacement.cs` 353/356 lines.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Placement/TypePlacementTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 41's state)

- [x] **Step 1:** `Of_Protected_IsAnErrorOnlyWhereCSharpForbidsIt` takes `locationText` (F-SqlSource.Tests.Generation-2-12).
- [x] **Step 2:** `Of_NestedClassNamedLikeAMemberOfTheType_ReportsSqlsrc007OnceAndDropsItsFiles` gains `CountUsers` (F-SqlSource.Tests.Generation-2-13).
- [x] **Step 3:** Set row 41 of the epic's table to `done`.
- [x] **Step 4:** `./format.sh`, then the Placement tests pass.
- [x] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST` and nothing `NOT COMPARABLE`, and both survivors are `Killed` in the after report.  180 killed, 12 survived, 0 timeouts, 58 s; `compare`: 178 detected before, 178 still detected, 0 lost, 0 not comparable.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-41/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Placement/**/*.cs` falls (9223 tests, 4 min 53 s; the same lines as before in each file).
- [x] **Step 3:** The pull request gives the changes, the compare result, the coverage figures and the run times.
