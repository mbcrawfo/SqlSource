# Test cleanup phase 73: tool bundles loosen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop `BundleJsonTests.Write_Outcome_IsAnObjectWithItsKind` from failing on the indentation of `bundle.json` or the order of `id` and `step`, while an `outcome` that is not an object, or has a wrong, missing or extra member, still fails it.

**Architecture:** The test parses the written text with `JsonDocument` through one private helper of its class, and compares the members of `outcome` as name and value pairs.  No source changes.  The guard is the `tool-bundles` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Bundles/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, System.Text.Json, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-73-tool-bundles-loosen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the test of the finding and its one helper change, and the three tests of Task 2, which the owner added.
- The epic's phase table: change only row 73.

Checked against the spec on `26d0b1fd`: the test still asserts substrings with their indentation and the order of `id` and `step`, `JsonFile.Options` is still `Indented = true`, and `BundleJson.Write` still writes `outcome` as an object with `kind`, and `id` and `step` for a failure, so the finding holds.  `Write_EndsWithOneLineBreak_AndHasNoCarriageReturn` still holds the `\n` line ends.  The spec names no document to update, and none names this test.

`main` then moved to `f1a19e57`, which changed `BundleQueryFile.cs`, `BundleReplay.cs` and `DiagnoseRun.cs` of the scope.  The branch was merged with it (`a29161fe`), and the guard was run again on the merged tree: its "before" half with `main`'s `BundleJsonTests.cs` in place of the branch's.  The figures below are of that second run.

## Review Focus

- An `outcome` written as something other than an object still fails, through the helper's `ValueKind` assertion.
- A member missing from a failure still fails: the comparison keeps the exact members and their count.
- A member added to an entry's `outcome` still fails, which the old substring's closing brace held.
- A wrong value of `kind`, `id` or `step` still fails.
- `step` written before `id`, or another indent, no longer fails.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-73/tool-bundles/before` (Windows), on `a29161fe` with `main`'s `BundleJsonTests.cs`: 301 mutants tested, 260 killed, 40 survived, 1 timeout; the tested run took 2 min 30 s.
- [x] `kills` on the "before" report: `Write_Outcome_IsAnObjectWithItsKind` 1 row, 6 kills, 0 alone, 0 survivors under it; `Read_BundleVersionBelowOne_IsSqlsrc288` 2 rows, 20 kills, 2 alone, 2 survivors; `Read_NoEngine_IsSqlsrc288NamingEngine` 1 row, 26 kills, 2 alone, 7 survivors; `Read_MistypedMember_IsSqlsrc288NamingIt` 1 row, 22 kills, 2 alone, 7 survivors.  None has a timeout under it.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-73/coverage-before/SqlSource.Tool.Tests` (8,508 tests, 41 skipped, 5 min 52 s on a machine shared with other runs): 1,124 of 1,170 lines over the 19 files of `src/SqlSource.Tool/Bundles/*.cs` that have executable lines.

### Task 1: Apply the finding

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Bundles/BundleJsonTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 73's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests.Bundles-1-03):** Add `private static (string Name, string? Value)[] OutcomeMembers(string written)`, which parses the text, asserts that `outcome` is a JSON object and gives its members.
- [x] **Step 2:** The entry's members are exactly `("kind", "entry")`; the failure's are `("kind", "failure")`, `("id", "SQLSRC226")` and `("step", "describe parameters")`, with `ignoreOrder: true`.
- [x] **Step 3:** Set row 73 of the epic's table to `done`, and run `./format.sh`.

### Task 2: The owner's addition (review R1-F1, p73 Q1)

The review found that three other tests of the class built their bad input with an exact-substring `Replace` of the written text, so a change of the layout failed them too.  The owner chose to fix them in this phase ("A: fix in p73").

- [x] **Step 1:** Add `private static string WrittenWith(System.Action<JsonObject> change)`, which parses the written file as a `JsonObject`, applies the change and writes it back.
- [x] **Step 2:** `Read_BundleVersionBelowOne_IsSqlsrc288` sets `bundleVersion` to the row's value, `Read_NoEngine_IsSqlsrc288NamingEngine` removes `engine` and asserts that the member was there, and `Read_MistypedMember_IsSqlsrc288NamingIt` sets `renamed` to `"yes"`.

### Task 3: The guard's "after" half

- [x] **Step 1:** A hand check, with the source changed and put back.  On the first run, with no indentation and `step` written before `id`, `Write_Outcome_IsAnObjectWithItsKind` passes; with `step` not written, with `outcome` written as an array, and with an entry that gains an `id`, it fails.  After Task 2, the 12 tests of the class pass with `JsonFile`'s indentation turned off.
- [x] **Step 2:** The mutation run with output `artifacts/test-cleanup/phase-73/tool-bundles/after` (Windows): 301 mutants tested, 260 killed, 40 survived, 1 timeout; the tested run took 2 min 30 s.  `compare`: 261 detected before, 261 still detected, 0 lost, 0 not comparable.
- [x] **Step 3:** `kills` after, against before, none with a timeout under it:
  - `Write_Outcome_IsAnObjectWithItsKind`: 8 kills against 6.  The two more are the writes of the end of `outcome` and of the file, `BundleJson.cs:70:13` and `71:13`, which the parse now fails on.
  - `Read_BundleVersionBelowOne_IsSqlsrc288`: 19 kills against 20.  It no longer kills `BundleJson.cs:46:13`, the write of `bundleVersion` removed, since the test now sets the member itself; other tests still kill that mutant.
  - `Read_NoEngine_IsSqlsrc288NamingEngine`: 27 kills against 26; the one more is `BundleJson.cs:50:13`, the write of `engine` removed, which `ShouldBeTrue` on the removal now fails.
  - `Read_MistypedMember_IsSqlsrc288NamingIt`: the same 22 kills.
  - Alone kills and survivors under each test are unchanged.
- [x] **Step 4:** Coverage into `artifacts/test-cleanup/phase-73/coverage-after/SqlSource.Tool.Tests` (8,508 tests, 41 skipped, 3 min 13 s on the same shared machine): 1,124 of 1,170 lines, before and after, the same figure in each of the 19 files.
- [ ] **Step 5:** The pull request gives the change, the compare result, the coverage figures and the run times.  The phase removes no test or row.
