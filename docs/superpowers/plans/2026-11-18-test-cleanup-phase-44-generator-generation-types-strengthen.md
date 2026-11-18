# Test cleanup phase 44: generator generation types strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `ShortNameIndexTests.MetadataNamesOf_ReferenceUnchanged_IndexesOnce` tell an index read again from one built again, so that the survivor `src/SqlSource/Generation/Types/MappedTypes/ShortNameIndex.cs:80:9` (Block removal of `{ return index; }`) is killed.

**Architecture:** `ShortNameIndex` counts the indexes it builds on the calling thread, beside the existing `IndexOf` hook "for tests", and the test asserts that the second compilation's ask builds none.  The count is thread-static because tests run in parallel and other tests build indexes of their own references; `MetadataNamesOf` is synchronous, so every build it makes is on the caller's thread.  Both compilations share the harness's static runtime references (`GeneratorHarness.RuntimeReferences`) and the one library reference, so the second ask has nothing to build.  The guard is the `generator-generation-types` mutation scope and the coverage of `src/SqlSource/Generation/Types/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-44-generator-generation-types-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- `ShortNameIndex` gains `[field: ThreadStatic] internal static int BuildsOnThisThread { get; private set; }` (IDE0032 rejects a property over a private field), incremented in `IndexFor` right after `ReferenceIndex.Build`, documented as the number of indexes built on the calling thread, kept or not, "For tests.", as `IndexOf` is.
- The test reads `BuildsOnThisThread` before and after the first ask and asserts it rose, so the count is shown to count, asserts it unchanged after the second, and keeps `IndexOf(library).ShouldBeSameAs(index)` and both `ShouldBe(["Lib.UserId"])`.
- The epic's phase table: change only row 44.

## Review Focus

- A count shared by every thread would race with other tests that build indexes in parallel and make this test flaky; the count is thread-static.
- The count adds production state to the generator for a test; it is one increment on a path taken once per reference, beside an existing test hook.

Checked against the spec on `d57296e2`.  `IndexFor` still returns the table's index at `ShortNameIndex.cs:79-82` and builds then `GetValue`s at 95-96; the finding holds.  No phase since p26 (#339, tree of `c05102e5`) changed the scope's source or tests, but this phase changes `ShortNameIndex.cs`, and `compare` matches mutants by place, so it needs no change to `src/` between its two runs: p26's report cannot be the "before".  The "before" runs on the tree with the count added and the test as it was; the "after" adds only the test's change.  This differs from the brief's default, which assumed a test-only change.

---

### Task 0: The guard's "before" half

- [x] Coverage of `tests/SqlSource.Tests`, on the tree of `d57296e2`, into `artifacts/test-cleanup/phase-44/coverage-before/SqlSource.Tests` (9217 tests, 5 min 38 s): `ShortNameIndex.cs` 87/92 lines, 2396/2481 over the scope's 26 files.
- [x] Add `BuildsOnThisThread` to `ShortNameIndex` as the constraints say; the generator builds.
- [x] Mutation run into `artifacts/test-cleanup/phase-44/generator-generation-types/before` on that tree; `ShortNameIndex.cs` block removal of `{ return index; }` Survived.  485 killed, 185 survived, 5 timeouts, 4 min 19 s.
- [x] `kills` on the before report for `ShortNameIndexTests.MetadataNamesOf_ReferenceUnchanged_IndexesOnce`: 17 kills; the block removal at 87:9 and the count's own two mutants at 103:9 (statement removal, `++` to `--`) survive under it.

### Task 1: Apply the finding

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Types/MappedTypes/ShortNameIndexTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 44's state)

- [x] **Step 1:** In the test, `var buildsBefore = ShortNameIndex.BuildsOnThisThread;` before the first ask; after `var index = …`, `var builds = ShortNameIndex.BuildsOnThisThread;` and `builds.ShouldBeGreaterThan(buildsBefore);`; after the second ask, `ShortNameIndex.BuildsOnThisThread.ShouldBe(builds);` (F-SqlSource.Tests.Generation.Types-1-01).  The first after run, without `ShouldBeGreaterThan`, left the count's own mutants surviving, which would leave the new assertion vacuous; that check was added and the after run repeated.
- [x] **Step 2:** Check the test fails with the block `{ return index; }` emptied by hand, then restore it.
- [x] **Step 3:** Set row 44 of the epic's table to `done`.
- [x] **Step 4:** `./format.sh`, then the `ShortNameIndexTests` pass.
- [x] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare`: nothing `LOST` and nothing `NOT COMPARABLE`, and the block removal of `{ return index; }` (`ShortNameIndex.cs:80:9` on the review's commit, line 87 now) is `Killed` in the after report.  488 killed, 182 survived, 5 timeouts, 4 min 33 s; `compare`: 490 detected before, 490 still detected, 0 lost, 0 not comparable.  Changed: 87:9 block removal and both 103:9 mutants of the count, Survived to Killed.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-44/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Types/**/*.cs` falls (9217 tests, 4 min 55 s): `ShortNameIndex.cs` 88/93, every other file unchanged, 2397/2482 over the 26 files.
- [x] **Step 3:** The pull request gives the change, the compare result, the coverage figures and the run times.
