# Test cleanup phase 43: generator generation sidecars strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Give `SidecarInputTests.Create_SidecarOfAClaimedFile_HoldsTheFileAndThePathOfItsSqlFileAsClaimed` a row for the first claimed path, and give `SidecarNeedsTests.Merge_Files_AreSortedByPathIgnoringCase` and `SidecarNeedsTests.Merge_NeedsOfOneType_AreInOrdinalOrderOfTheirNames` names whose ordinal order differs from their order ignoring case, so that each test proves the order its name promises.

**Architecture:** One theory gains a row and takes its expected SQL path as an argument; two facts change their input and expectation.  There is no source change.  The guard is the `generator-generation-sidecars` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Sidecars/**/*.cs`, and, for F-3-11 and F-3-12, a hand check and the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-43-generator-generation-sidecars-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- `Create_SidecarOfAClaimedFile_HoldsTheFileAndThePathOfItsSqlFileAsClaimed`: rows `("/app/Orders.sql.json", "app/Orders.sql")`, `("/app/Users.sql.json", "app/Users.sql")`, `("/app/users.SQL.json", "app/Users.sql")`; the SQL path asserted is the row's second argument.  The theory does not branch on its row, so TD-0089 is not touched.
- `Merge_Files_AreSortedByPathIgnoringCase`: input `app/B.sql` then `app/a.sql`, expecting `["app/a.sql", "app/B.sql"]`.  The input stays in the reverse of the expected order, so the test still kills `OrderBy` to `OrderByDescending` and also fails an unsorted result.
- `Merge_NeedsOfOneType_AreInOrdinalOrderOfTheirNames`: one file's needs of its own, queries `a` then `B`, expecting `["B", "a"]`.  The shared `Second` stays, since the other tests use it.
- The epic's phase table: change only row 43.

Checked against the spec on `6489ed65`.  `SidecarInput.Create` still returns null for `index < 0` (`SidecarInput.cs:30`), `Merge` still sorts by `SqlPath.Comparer` (`SidecarNeeds.cs:57`) and `Union` still keys by `StringComparer.Ordinal` (`SidecarNeeds.cs:63`); the three findings hold.  No earlier phase of this run changed the scope, so this phase runs its own "before".

---

### Task 0: The guard's "before" half

- [x] Mutation run into `artifacts/test-cleanup/phase-43/generator-generation-sidecars/before`: 39 mutants tested, 34 killed, 5 survived, 0 timeouts, 2 min 30 s.
- [x] `kills` on the before report: `SidecarInput.cs:30:16` (`index <= 0`) survives under `Create_SidecarOfAClaimedFile_HoldsTheFileAndThePathOfItsSqlFileAsClaimed`; `Merge_Files_AreSortedByPathIgnoringCase` kills `SidecarNeeds.cs:54:13` alone; `Merge_NeedsOfOneType_AreInOrdinalOrderOfTheirNames` kills `68:21` and `69:17`, both also killed by `Merge_NeedsOfTwoTypes_AreTheUnionByNameWithTheGreatestOutput`.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-43/coverage-before/SqlSource.Tests` (9223 tests): `SidecarInput.cs` 8/8, `SidecarNeeds.cs` 47/47, `SidecarChecker.cs` 142/151, `SidecarFileChecks.cs` 63/64, every other file of the scope full.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Sidecars/SidecarInputTests.cs`
- Modify: `tests/SqlSource.Tests/Generation/Sidecars/SidecarNeedsTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 43's state)

- [x] **Step 1:** The `Orders.sql` row and the `sqlPath` argument (F-SqlSource.Tests.Generation-3-10).
- [x] **Step 2:** `app/B.sql` and `app/a.sql` (F-SqlSource.Tests.Generation-3-11).
- [x] **Step 3:** Queries `a` and `B` (F-SqlSource.Tests.Generation-3-12).
- [x] **Step 4:** Set row 43 of the epic's table to `done`.
- [x] **Step 5:** `./format.sh`, then the Sidecars tests pass (9).
- [x] **Step 6:** Hand checks: with `Merge` sorting by `StringComparer.Ordinal`, `Merge_Files_AreSortedByPathIgnoringCase` fails; with `Union` keyed by `StringComparer.OrdinalIgnoreCase`, `Merge_NeedsOfOneType_AreInOrdinalOrderOfTheirNames` fails.  Each change reverted.
- [x] **Step 7:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `compare`: 35 killed, 4 survived, 0 timeouts, 1 min; `compare`: 34 detected before, 34 still detected, 0 lost, 0 not comparable.  `SidecarInput.cs:30:16` (`index <= 0`) is killed, by the theory alone.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-43/coverage-after/SqlSource.Tests` (9224 tests): the same lines as before in each file of the scope.
- [x] **Step 3:** Run time of `dotnet test --project tests/SqlSource.Tests --no-build`, after then before, on one machine: after 4 min 42 s (9224 tests, load average 3 at the end), before 6 min 44 s (9223 tests, load average 32 at the end).  The difference is the machine's load, not the change, which adds one fast row.
- [x] **Step 4:** The pull request gives the changes, the compare result, the coverage figures, the hand checks and the run times.
