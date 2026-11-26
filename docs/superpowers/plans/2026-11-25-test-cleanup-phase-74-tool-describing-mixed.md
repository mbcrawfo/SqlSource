# Test cleanup phase 74: tool describing mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the five findings of `tests/SqlSource.Tool.Tests/Describing`'s root classes `ConnectionTests` and `SidecarStoreTests`: delete one test whose claim a cover makes from a later point, fold an empty value into the theory of white space, drop five rows that `Apply_FileOfAnotherName_IsLeft` holds, and loosen two `ToString` tests to assert that the secret is left out rather than the whole text.

**Architecture:** Test changes only.  The guard is the `tool-describing` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Describing/*.cs`, with covers and run time for F-1-06, whose subject, `UsageValue`, is outside the scope.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-74-tool-describing-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the tests the findings name change.
- The epic's phase table: change only row 74.

Checked against the spec on `33894dfb`: every test the findings name is still there under its name and reads as the review wrote it.  Lines have moved: `ConnectionEnvironment.ToString` is at lines 94-96 and `NullIfBlank` at 98, and the five rows of F-1-07 are at lines 564-569 of `SidecarStoreTests.cs`, each still a row of `Apply_FileOfAnotherName_IsLeft`.  The spec names no document to update, and a search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` finds none of the test names.

The branch was then merged with `main` at `1ec193d8`, which changed `DescribeOptions.cs`, `DescribeRun.cs` and `SidecarCheck.cs` of the scope.  None of them is a subject of the tests this phase changes (`ConnectionEnvironment`, `SidecarStore` and `UsageValue`), so the guard's figures below, of `33894dfb`, stand.

## Review Focus

- `For_VariableThatIsEmptyOrHoldsOnlyWhiteSpace_IsNotSet` keeps `""`, so the kill of `ConnectionEnvironment.cs:98:58` `(value!="")` stays.
- `Apply_RunOnTheWhole_LeavesAFileOfAnotherName` keeps the capital digits and the four names that the cover lacks.
- Both loosened `ToString` tests still fail when the text holds the secret.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-74/tool-describing/before` (Windows), on `33894dfb`: 628 mutants tested, 545 killed, 74 survived, 9 timeouts; the tested run took 4 min 6 s.
- [x] `kills` on the "before" report:
  - `For_VariableThatIsEmpty_IsNotSet`: 1 row, 10 kills, 0 alone.
  - `For_VariableThatHoldsOnlyWhiteSpace_IsNotSet`: 2 rows, 9 kills, 0 alone.  The one kill it lacks is `ConnectionEnvironment.cs:98:58` `(value!="")`.
  - `ToString_ConnectionEnvironment_LeavesTheValuesOut`: 1 row, 3 kills, 2 alone, `ConnectionEnvironment.cs:95:9` and `96:11`, each String `$""`.
  - `ToString_UsageValueOfAConnection_LeavesTheTextOut`: 1 row, 0 kills.
  - `Apply_RunOnTheWhole_LeavesAFileOfAnotherName`: 10 rows, 9 kills, 0 alone, 3 timeouts and 8 survivors under it.
  - `Apply_RunOnTheWholeThatWasCancelled_RemovesNothing`: 1 row, 0 kills, and 1 survivor under it, `SidecarStore.cs:108:9` Statement.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-74/coverage-before/SqlSource.Tool.Tests` (8,508 tests, 41 skipped, 3 min 26 s): 1,807 of 1,831 lines over the 28 files of `src/SqlSource.Tool/Describing/*.cs` that have executable lines.
- [x] Run time of `dotnet test --project tests/SqlSource.Tool.Tests`: 2 min 54 s (8,508 tests, 41 skipped), on a machine shared with another phase's session.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/ConnectionTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/SidecarStoreTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 74's state, and step 6 of "How a phase is done" by the owner's answer to p74 Q3)

- [x] **Step 1 (F-1-08, delete):** Remove `Apply_RunOnTheWholeThatWasCancelled_RemovesNothing`.  Its cover, row `true` of `Apply_RunThatIsCancelledAsItBegins_RemovesNothing`, stays as it is.
- [x] **Step 2 (F-1-04, fold):** Add the row `""` to the theory of white space, renamed `For_VariableThatIsEmptyOrHoldsOnlyWhiteSpace_IsNotSet`, and remove `For_VariableThatIsEmpty_IsNotSet`.
- [x] **Step 3 (F-1-07, fold):** Drop the rows `Users.sql.json.tmp`, `Users.sql.json.backup.tmp`, the 31-digit name, the name with `g` and the name ending `.tmp.txt` from `Apply_RunOnTheWhole_LeavesAFileOfAnotherName`.  Each is a row of `Apply_FileOfAnotherName_IsLeft`.
- [x] **Step 4 (F-1-05, loosen):** `ToString_ConnectionEnvironment_LeavesTheValuesOut` asserts that the text holds neither the environment's value nor the file's, `Secrets.Marker`.  Review R1-F1: the environment's value is now `Secrets.Connection` in place of `Secrets.Word`, which `Secrets.Marker` contains, so that each leak fails its own assertion.
- [x] **Step 5 (F-1-06, loosen):** `ToString_UsageValueOfAConnection_LeavesTheTextOut` asserts `ShouldNotContain(Secrets.Word)`.
- [x] **Step 6:** Set row 74 of the epic's table to `done`, and run `./format.sh`.

### Task 2: The guard's "after" half

- [x] **Step 1:** A hand check, with the source changed and put back.  With `UsageValue.ToString` printing `Text`, `ToString_UsageValueOfAConnection_LeavesTheTextOut` fails.  With `ConnectionEnvironment.ToString` printing the file's values, `ToString_ConnectionEnvironment_LeavesTheValuesOut` fails on `Secrets.Marker`; printing the environment's value, it fails on `Secrets.Connection`.
- [x] **Step 2:** The mutation run with output `artifacts/test-cleanup/phase-74/tool-describing/after` (Windows): 628 mutants tested, 540 killed, 79 survived, 9 timeouts; the tested run took 4 min 4 s.  `compare`: 554 detected before, 548 still detected, 6 lost, 0 not comparable.
  - `ConnectionEnvironment.cs:95:9` and `96:11`, each String `$""`, Killed to Survived.  These are the two kills that `ToString_ConnectionEnvironment_LeavesTheValuesOut` alone made, of the layout of the text.  F-1-05's evidence and verdict name them as the loosen's cost ("only empty the layout, and no value would leak under either").  The spec's exception did not cover them, since the test is one this phase changes, so they were put to the owner (p74 Q1).  The owner accepted them ("A: accept them") and had step 6 of the epic outline amended in this phase so that a loosen may give up the kills of the incidental form that its finding's evidence names, with the pull request naming each one (review R1-F2, p74 Q3, "1: amend step 6").
  - `JoinedRedaction.cs:90:31` Linq (Prepend to Append), `173:51` Equality (`found > 0`), `239:9` Conditional (true) and `239:9` Equality (`separator.Length != 0`), Killed to Survived.  In the "before" report only `JoinedRedactionTests` killed them, which this phase does not change, so each is a kill that did not repeat.  By hand, each mutant applied to the source passes all 27 tests of `JoinedRedactionTests`, and a control change (`Enumerable.Range(1, 0)` at line 240) fails 8, so the earlier kills were not real.  The `239:9` Conditional (false) went the other way, Survived to Killed.  Put to the owner as an outside finding (p74 Q2).
  - After review R1-F1 changed `ToString_ConnectionEnvironment_LeavesTheValuesOut`, the run was made again with output `artifacts/test-cleanup/phase-74/tool-describing/after-r1`: 628 mutants tested, 544 killed, 75 survived, 9 timeouts; 5 min 35 s.  `compare`: 554 detected before, 549 still detected, 5 lost, 0 not comparable.  The lost are the two of `ConnectionEnvironment.cs` above, and `JoinedRedaction.cs` `239:9` Conditional (true) and Equality, and `214:56` Boolean (`(whole, true)`).  `90:31` and `173:51` are Killed again, by the same `JoinedRedactionTests`, among them `Of_NothingFound_IsTheSameTexts`.  `214:56` was killed before only by `JoinedRedactionTests` too, and by hand it also passes all 27 of them.  So the `JoinedRedaction` mutants flip between runs whatever this phase does.  The owner chose a separate pull request to add the rows of `JoinedRedactionTests` that kill them ("A: separate fix PR"); this phase changes nothing for them.
- [x] **Step 3:** `kills` after:
  - `For_VariableThatIsEmptyOrHoldsOnlyWhiteSpace_IsNotSet`: 3 rows, 10 kills, 0 alone, the kills of both tests it replaces.
  - `ToString_ConnectionEnvironment_LeavesTheValuesOut`: 1 row, 1 kill, 0 alone, 2 survivors under it (the two of Step 2), in both runs.
  - `ToString_UsageValueOfAConnection_LeavesTheTextOut`: 1 row, 0 kills.
  - `Apply_RunOnTheWhole_LeavesAFileOfAnotherName`: 5 rows, 9 kills, 0 alone, 3 timeouts and 8 survivors under it, as before.
  - `Apply_FileOfAnotherName_IsLeft`: 9 rows, 10 kills, 1 alone, 3 timeouts under it.
  - `Apply_RunThatIsCancelledAsItBegins_RemovesNothing`: 2 rows, 1 kill, 2 survivors under it, `SidecarStore.cs:108:9` among them, the survivor of the deleted test.
- [x] **Step 4:** Coverage into `artifacts/test-cleanup/phase-74/coverage-after/SqlSource.Tool.Tests` (8,502 tests, 41 skipped, 3 min 56 s): 1,807 of 1,831 lines, before and after, the same figure in each of the 28 files.
- [x] **Step 5:** Run time of `dotnet test --project tests/SqlSource.Tool.Tests`: 3 min 24 s (8,502 tests, 41 skipped), on the same shared machine.  The six fewer tests take under a tenth of a second, so the difference is the machine's load.
- [ ] **Step 6:** The pull request gives the change, the compare result with each LOST mutant, the coverage figures, each removed test or row beside its cover and the run times.
