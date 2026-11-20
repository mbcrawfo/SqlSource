# Test cleanup phase 53: tool describing capture strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `ReplayingExchangeTests.AskAsync_AnswerWithANullWhereTheRecordHasNone_ThrowsNamingTheMember` check the index it names for a null in a later element of a list, not only in the first, where `index++` and `index--` both give 0.

**Architecture:** One new row of the theory: `settings` of `[{"name":"a","value":"b"}, null]`, naming `server.settings[1]`.  No source or helper changes.  The guard is the `tool-describing-capture` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Describing/Capture/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-53-tool-describing-capture-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the one theory changes, by one row; it keeps its name and its other rows.
- The epic's phase table: change only row 53.

Checked against the spec on `bf5da819`: `AnswerNulls.Check` still writes `{index++}` into the path, and the theory's only row with an index still puts the null in the first element, so the finding holds.  No earlier phase ran this scope, so the "before" is a run of its own.

Where the run differs from the spec: on Windows `tools/mutation-test.sh` read the static run's `exclusions.txt`, which the report tool writes with CRLF, with the carriage return still on each line, so Stryker missed the last span of each line and the second run tested three static mutants and rejected its own report.  The owner chose a separate pull request for the fix; this branch carries its commit, `b903f59d`, cherry-picked with `-x`, so the guard could run, and it drops out when `main` has the fix.

## Review Focus

The new row's first element is a whole setting, with `name` and `value`, so the check reaches the second element; a null there must be named `server.settings[1]`.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-53/tool-describing-capture/before`, with `b903f59d` on the tree: 89 mutants tested, 82 killed, 7 survived, 0 timeouts, 2 min 50 s.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-53/coverage-before/SqlSource.Tool.Tests` (7,082 tests, 5 min 14 s): 258 of 264 lines over the 9 files of `src/SqlSource.Tool/Describing/Capture/`.
- [x] Run time of `dotnet test --project tests/SqlSource.Tool.Tests`: 3 min 40 s (7,082 tests).

### Task 1: Apply the finding

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/Capture/ReplayingExchangeTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 53's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests.Describing-1-01):** add the row `settings` of `[{"name":"a","value":"b"}, null]` naming `server.settings[1]`.
- [x] **Step 2:** Set row 53 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then the theory passes (5 rows).
- [x] **Step 4:** Hand check: with `{index++}` made `{index--}` in `AnswerNulls.Check`, the new row fails, naming `server.settings[-1]`, and the other four pass.  Reverted.
- [x] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-53/tool-describing-capture/after`: 89 mutants tested, 83 killed, 0 timeouts, 3 min 27 s.  `compare`: 82 detected before, 82 still detected, 0 lost, 0 not comparable.  `kills` for the changed test: before, 4 rows, 21 kills, 8 alone, 3 survivors under it, `AnswerNulls.cs:62:80` among them; after, 5 rows, 22 kills, 9 alone, 2 survivors under it, and `src/SqlSource.Tool/Describing/Capture/AnswerNulls.cs:62:80` is `Killed`, by this test alone.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-53/coverage-after/SqlSource.Tool.Tests` (7,083 tests, 4 min 55 s): 258 of 264 lines over the 9 files of `src/SqlSource.Tool/Describing/Capture/`, before and after, the same lines in each file.
- [x] **Step 3:** Run time of `dotnet test --project tests/SqlSource.Tool.Tests` after the change: 3 min 19 s (7,083 tests), against 3 min 40 s before; the one row adds nothing measurable.
- [x] **Step 4:** The pull request gives the change, the compare result, the coverage figures, the hand check and the run times.
