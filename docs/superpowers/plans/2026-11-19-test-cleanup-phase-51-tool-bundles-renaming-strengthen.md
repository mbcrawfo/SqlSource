# Test cleanup phase 51: tool bundles renaming strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the two strengthen findings of phase 51 to `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres`: make `PostgresDerivationsTests.Derive_PrepareErrorBeforeTheList_IsKept` a theory over both edges of "before the list", and give the root's unread key of `PostgresPlanRenamerTests.WordsOf_KeyNotRead_IsGivenWhole` a string.

**Architecture:** Test-only change in `PostgresDerivationsTests.cs` and `PostgresPlanRenamerTests.cs`.  No source, helper or document change but the epic's row 51.  The guard is the `tool-bundles-renaming` mutation scope and the coverage of `src/SqlSource.Tool/Bundles/Renaming/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-51-tool-bundles-renaming-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the two named tests change.  The epic's phase table: change only row 51.
- The guard runs need the CRLF fix of `tools/mutation-test.sh` (b903f59d, its own pull request), cherry-picked with `-x`; it drops out once `main` has it.
- No document names a test of the phase (the spec's search); `docs/architecture/bundles.md` already promises what F-…-1-03 now asserts, so no document changes.

Checked against the spec on `bf5da819`: each test reads as the findings say (`Derive_PrepareErrorBeforeTheList_IsKept` asks position 3 alone; `WordsOf_KeyNotRead_IsGivenWhole` has `"Extra": 1` at the root), and the source lines the survivors name are unchanged: `PostgresDerivations.cs:122-125` (`position <= opening`, then `position >= 1 ? position : null`) and `PostgresPlanRenamer.Words.cs:55` (`AllStrings(member.Value, words)`).  The scope's last phase was 11; its report is not in the run's folder, and later merges touched the scope, so this phase runs its own "before".

Where the code or precedent settles what the spec leaves open:

- **F-…-1-02:** `InlineData` cannot hold the computed 21, so the theory's row is the distance before the opening's end, as the file's `Derive_PrepareErrorAfterTheLastItem_StaysInIt` counts `beforeTheSql`: rows 20, 18 and 0 give positions 1, 3 and 21 of `PREPARE sqlsource_1 (`, the end taken from `ParameterPlaceholders.Characters(ParameterPlaceholders.Opening(Statement))`.

## Review Focus

Each row of the theory asserts the position kept unchanged and no refusal.

---

### Task 0: The guard's "before" half

- [x] Mutation: `tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-51/tool-bundles-renaming/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Bundles.Renaming.' 'Bundles/Renaming/**/*.cs'`, with the CRLF fix of `tools/mutation-test.sh` (b903f59d, cherry-picked): 1,008 killed, 157 survived, 18 timeouts, 43 no coverage, 11 min.  `kills` for the two tests saved beside it: `Derive_PrepareErrorBeforeTheList_IsKept` 19 kills, 3 alone, with `PostgresDerivations.cs` 122:42 `position < opening`, 124:20 `position > 1` and 124:20 Conditional (true) surviving under it; `WordsOf_KeyNotRead_IsGivenWhole` 18 kills, 6 alone, with `PostgresPlanRenamer.Words.cs:55:21` Statement its one survivor.  Both as the spec measured.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-51/coverage-before/SqlSource.Tool.Tests` (7,082 tests, 3 min 22 s).

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresDerivationsTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresPlanRenamerTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 51's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests.Bundles.Renaming-1-02):** `Derive_PrepareErrorBeforeTheList_IsKept` becomes a theory over `beforeTheList` 20, 18 and 0, each position kept and no refusal.
- [x] **Step 2 (F-SqlSource.Tool.Tests.Bundles.Renaming-1-03):** the root's unread key becomes `"Extra": "other"`; expect `["Future Key", "secret", "Extra", "other"]`.
- [x] **Step 3:** Set row 51 of the epic's table to `done`.
- [x] **Step 4:** `./format.sh`, then `PostgresDerivationsTests` and `PostgresPlanRenamerTests` pass (66 tests).
- [x] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-51/tool-bundles-renaming/after`: 1,011 killed, 154 survived, 18 timeouts, 11 min.  `compare`: 1,026 detected before, 1,026 still detected, 0 lost, 0 not comparable.  `PostgresDerivations.cs:124:20` `position > 1` and `PostgresPlanRenamer.Words.cs:55:21` Statement are `Killed`, and so is `122:42` `position < opening`.  `kills` after: `Derive_PrepareErrorBeforeTheList_IsKept` 3 rows, 21 kills, 5 alone; `WordsOf_KeyNotRead_IsGivenWhole` 16 kills, 7 alone, no survivor under it.  `124:20` Conditional (true), `(true?position :null)`, still survives: only a position below 1 tells it apart, which is refused, not kept, and the finding's proposal gives no such row.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-51/coverage-after/SqlSource.Tool.Tests` (7,084 tests, 3 min 37 s): 3,611 of 3,714 lines over the 38 files of `src/SqlSource.Tool/Bundles/Renaming/`, before and after, the same lines in each file.
- [ ] **Step 3:** The pull request gives the changes, the compare result and the coverage figures.
