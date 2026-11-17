# Test cleanup phase 08: generator snapshot deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the two tests of `tests/SqlSource.Tests/Snapshot` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** Two test deletions in one file, no source change.  The guard is the `generator-snapshot` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Snapshot/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-08-generator-snapshot-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `SidecarTokenizerTests.TrySkipValue_TrailingCommaInStrictMode_IsTheClosingBracket` and `SidecarTokenizerTests.Next_CommentInStrictMode_IsInvalidAtTheSlash`.
- The covers keep every row and assertion: the rows `"[1,]", 3, 1`, `"{\"a\":1,}", 7, 1` and `"[1, // c\n2]", 4, 1` of `TrySkipValue_BrokenGrammar_FailsAtTheToken`, and the row `"/* c */", 0, 1` of `Next_TextThatIsNotJson_IsInvalidAtTheCharacter`.
- F-SqlSource.Tests.Snapshot-1-04's optional extra row (`"// a\n1", 0, 1`) is not added: the spec makes it optional, and the phase writes no new test.
- The epic's phase table: change only row 08.

## Review Focus

None: the change removes tests and adds no behaviour.  Checked against the spec on `5a144cec`: nothing under `src/SqlSource/Snapshot` or `tests/SqlSource.Tests/Snapshot` changed since the review's commit, so both findings read as the review wrote them, and the covers exist.  No document names a removed test; `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md` names other tests of `SidecarTokenizerTests`, which stay, so it stays true.

---

### Task 0: The guard's "before" half

- [x] The `generator-snapshot` mutation run into `artifacts/test-cleanup/phase-08/generator-snapshot/before`.
- [x] `kills` on the before report for both tests.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-08/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Snapshot/**/*.cs`.

### Task 1: Delete the tests

**Files:**
- Modify: `tests/SqlSource.Tests/Snapshot/Reading/SidecarTokenizerTests.cs` (remove both tests)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 08's state)

- [x] **Step 1:** Remove each test with its attributes.
- [x] **Step 2:** Set row 08 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows no removed test made.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-08/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Snapshot/**/*.cs` falls.
- [x] **Step 3:** The pull request lists each removed test beside its cover, the compare result, the coverage figures and the run times.
