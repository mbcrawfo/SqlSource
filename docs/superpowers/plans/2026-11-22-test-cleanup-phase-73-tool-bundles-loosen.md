# Test cleanup phase 73: tool bundles loosen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop `BundleJsonTests.Write_Outcome_IsAnObjectWithItsKind` from failing on the indentation of `bundle.json` or the order of `id` and `step`, while an `outcome` that is not an object, or has a wrong, missing or extra member, still fails it.

**Architecture:** The test parses the written text with `JsonDocument` through one private helper of its class, and compares the members of `outcome` as name and value pairs.  No source changes.  The guard is the `tool-bundles` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Bundles/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, System.Text.Json, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-73-tool-bundles-loosen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the test of the finding and its one helper change.
- The epic's phase table: change only row 73.

Checked against the spec on `26d0b1fd`: the test still asserts substrings with their indentation and the order of `id` and `step`, `JsonFile.Options` is still `Indented = true`, and `BundleJson.Write` still writes `outcome` as an object with `kind`, and `id` and `step` for a failure, so the finding holds.  `Write_EndsWithOneLineBreak_AndHasNoCarriageReturn` still holds the `\n` line ends.  The spec names no document to update, and none names this test.

## Review Focus

- An `outcome` written as something other than an object still fails, through the helper's `ValueKind` assertion.
- A member missing from a failure still fails: the comparison keeps the exact members and their count.
- A member added to an entry's `outcome` still fails, which the old substring's closing brace held.
- A wrong value of `kind`, `id` or `step` still fails.
- `step` written before `id`, or another indent, no longer fails.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-73/tool-bundles/before` (Windows), on `26d0b1fd`: 301 mutants tested, 260 killed, 40 survived, 1 timeout; the tested run took 2 min 54 s.
- [x] `kills` on the "before" report for `Write_Outcome_IsAnObjectWithItsKind`: 1 row, 6 kills, 0 alone, 0 timeouts and 0 survivors under it.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-73/coverage-before/SqlSource.Tool.Tests` (7,488 tests, 19 skipped, 6 min 24 s on a machine shared with other runs): 1,082 of 1,136 lines over the 19 files of `src/SqlSource.Tool/Bundles/*.cs` that have executable lines.

### Task 1: Apply the finding

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Bundles/BundleJsonTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 73's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests.Bundles-1-03):** Add `private static (string Name, string? Value)[] OutcomeMembers(string written)`, which parses the text, asserts that `outcome` is a JSON object and gives its members.
- [x] **Step 2:** The entry's members are exactly `("kind", "entry")`; the failure's are `("kind", "failure")`, `("id", "SQLSRC226")` and `("step", "describe parameters")`, with `ignoreOrder: true`.
- [x] **Step 3:** Set row 73 of the epic's table to `done`, and run `./format.sh`.

### Task 2: The guard's "after" half

- [x] **Step 1:** A hand check of the test, with the source changed and put back.  With no indentation and `step` written before `id`, it passes.  With `step` not written, with `outcome` written as an array, and with an entry that gains an `id`, it fails.  Unchanged, it passes.
- [x] **Step 2:** The mutation run with output `artifacts/test-cleanup/phase-73/tool-bundles/after` (Windows): 301 mutants tested, 260 killed, 40 survived, 1 timeout; the tested run took 2 min 37 s.  `compare`: 261 detected before, 261 still detected, 0 lost, 0 not comparable.  `kills` after: 1 row, 8 kills, 0 alone, 0 timeouts and 0 survivors under it.  The six kills of the "before" report remain, and the two more are the writes of the end of `outcome` and of the file, `BundleJson.cs:70:13` and `71:13`, which the parse now fails on.
- [x] **Step 3:** Coverage into `artifacts/test-cleanup/phase-73/coverage-after/SqlSource.Tool.Tests` (7,488 tests, 19 skipped, 8 min 1 s on the same shared machine): 1,082 of 1,136 lines, before and after, the same figure in each of the 19 files.
- [ ] **Step 4:** The pull request gives the change, the compare result, the coverage figures and the run times.  The phase removes no test or row.
