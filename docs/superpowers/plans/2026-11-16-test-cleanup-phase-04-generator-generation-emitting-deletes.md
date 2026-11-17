# Test cleanup phase 04: generator generation emitting deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the six tests of `tests/SqlSource.Tests/Generation/Emitting` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** Six test deletions and one comment moved, no source change.  The guard is the `generator-generation-emitting` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Emitting/**/*.cs`, and, for the blind `GeneratedTextTests.IndentOf_ThreeLevels_IsTwelveSpaces`, its named cover and the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-04-generator-generation-emitting-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but the six the spec names; every named cover keeps every assertion and row.
- `CallSpellingTests.AppendCallStart_EveryCall_HasAnAsynchronousAndASynchronousSpelling` is a keep and stays as it is.
- The epic's phase table: change only row 04.

## Review Focus

None: the change removes tests and adds no behaviour.  Checked against the spec on `641f2a57`: each removed test and each cover reads as the review wrote it, `GeneratedText.Deeper` and the indent cache are as the findings describe, and no helper is used only by a removed test (`Write`, `ToLong`, `ListUsers` and `Pooled` all have other users).

---

### Task 0: The guard's "before" half

- [x] The `generator-generation-emitting` mutation run into `artifacts/test-cleanup/phase-04/generator-generation-emitting/before`.
- [x] `kills` on the before report for each of the six removed tests.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-04/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/Emitting/**/*.cs`.
- [x] The run time of `dotnet test --project tests/SqlSource.Tests`, with the load average beside it.

### Task 1: Delete the tests

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Emitting/RowBufferTests.cs` (remove `IsUsedBy_ManyOfIAsyncEnumerable_IsFalse`; its reason, that a streaming method has nothing to buffer, moves onto `Pooled`)
- Modify: `tests/SqlSource.Tests/Generation/Emitting/ParameterBinderTests.cs` (remove `Append_ArrayConvertedByElement_SendsTheConvertedArray`)
- Modify: `tests/SqlSource.Tests/Generation/Emitting/GeneratedTextTests.cs` (remove `IndentOf_ThreeLevels_IsTwelveSpaces` and `Deeper_IndentNotFromTheCache_AddsOneLevel`)
- Modify: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/CallSpellingTests.cs` (remove `AppendCallStart_EveryCall_IsTheAwaitTheTargetAndTheMember` and `WholeCall_WithItsToken_IsTheAwaitedCallOrTheCall`)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 04's state)

- [x] **Step 1:** Remove each test with its attributes and comment, and any `using` or helper only it needed.
- [x] **Step 2:** Set row 04 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows no removed test made.
- [ ] **Step 2:** Coverage into `artifacts/test-cleanup/phase-04/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Emitting/**/*.cs` falls.
- [ ] **Step 3:** The run time of `dotnet test --project tests/SqlSource.Tests` after the change, interleaved with a before run, with the load average beside each.
- [ ] **Step 4:** The pull request lists each removed test beside its covers, the compare result, the coverage figures and the run times.
