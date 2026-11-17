# Test cleanup phase 02: generator diagnostics deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the two tests of `tests/SqlSource.Tests/Diagnostics` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** Two test deletions, no source change.  The guard is the `generator-diagnostics` mutation scope, the coverage of `src/SqlSource/Diagnostics`, and, since mutation cannot guard either finding, each removed test beside its named cover with the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-02-generator-diagnostics-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but the two the findings name; the keeps F-SqlSource.Tests-1-07, -08 and -09 stay as they are.
- A test that a finding names as cover keeps every assertion and row.
- The epic's phase table: change only row 02.

## Review Focus

None: the change removes tests and adds no behaviour.  Checked against the spec: both findings still read as the review wrote them on `0ea33bd9`, and every named cover exists.

---

### Task 0: The guard's "before" half (done before this plan was written)

- [x] `tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-02/generator-diagnostics/before --tests 'FullyQualifiedName~SqlSource.Tests.Diagnostics.' 'Diagnostics/**/*.cs'`: 18 tested, 17 killed, 1 survived.
- [x] `kills` on the before report for each removed test: 0 kills, 0 timeouts under either.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-02/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Diagnostics`.
- [x] Run time of `dotnet test --project tests/SqlSource.Tests`, three runs.

### Task 1: Delete the two tests

**Files:**
- Modify: `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs` (remove `LibraryBelowTheFloor_Descriptor_IsAnErrorThatCannotBeConfigured`; its `_Message_` neighbour stays)
- Modify: `tests/SqlSource.Tests/Diagnostics/SettingsFileDiagnosticsTests.cs` (remove `Create_DefaultBesideAnotherParameter_IsNoError`)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 02's state)

- [ ] **Step 1:** Remove each test with its `[Fact]`, and any `using` only it needed.
- [ ] **Step 2:** `src/SqlSource/Diagnostics/AGENTS.md` line 5 names `SqlDiagnosticsTests` for the list of configurable ids, which stays: read it and leave it.
- [ ] **Step 3:** Set row 02 of the epic's table to `done`.
- [ ] **Step 4:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tests`: every test passes.
- [ ] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE`.
- [ ] **Step 2:** Coverage into `artifacts/test-cleanup/phase-02/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Diagnostics` falls.
- [ ] **Step 3:** Run time of `dotnet test --project tests/SqlSource.Tests`, three runs on the same machine.
- [ ] **Step 4:** The pull request lists both removed tests beside their covers, the compare result, the coverage figures and the run times.
