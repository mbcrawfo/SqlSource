# Test cleanup phase 20: generator generation folds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fold the one theory of `tests/SqlSource.Tests/Generation` that the test review found to hold rows testing the settings and not the step, with the guard showing nothing lost.

**Architecture:** Four `InlineData` rows dropped from one theory, no source change.  The guard is the `generator-generation` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/*.cs`, and, since the dropped rows reach no tested mutant, the covers and the run time of `tests/SqlSource.Tests`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-20-generator-generation-folds-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `FileParseInputTests.Resolve_CommentsWanted_IsTheChainsStateOfKeepComments`, and in it only the rows `"sort-input keep-comments"`, `"keep-comments=on default"`, `"sort-input"` and `"default"`, with the comment that heads only `"default"`.
- Keep the rows `keep-comments`, `keep-comments=off` and `null`.
- The covers, `QuerySettingsTests.KeepsComments_OfALevel_IsTheLevelsResolvedKeepComments`, `GeneratorParameterListTests.ReadElements_DefaultBesideOtherWords_ResetsThenAppliesThem`, `GeneratorParameterListTests.ReadElements_Words_NameTheirStates` and `GeneratorParameterChoicesTests.Reset_Resolves_ToTheDefaults`, keep every assertion and row.
- The epic's phase table: change only row 20.

## Review Focus

None: the change removes rows and adds no behaviour.  Checked against the spec on `9ac0129e`: `FileParseInput.Resolve` still takes `chain.Level.KeepsComments` alone, the theory still holds the rows the review named, and every cover exists.

---

### Task 0: The guard's "before" half

- [x] The `generator-generation` mutation run into `artifacts/test-cleanup/phase-20/generator-generation/before` (p03's "after" report went with its worktree, and #331 changed the scope since).
- [x] `kills` on the before report for the folded test.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-20/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/*.cs`.

### Task 1: Fold the theory

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/FileParseInputTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 20's state)

- [x] **Step 1:** Drop the four rows, and the comment `// Every default, which leaves comments out.`, which headed only `"default"`.
- [x] **Step 2:** Set row 20 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the folded test did not make.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-20/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/*.cs` falls.
- [x] **Step 3:** Run time of `dotnet test --project tests/SqlSource.Tests --no-build`, before and after, interleaved on one machine with the load beside each figure.
- [x] **Step 4:** The pull request lists the dropped rows beside their covers, the compare result, the coverage figures and the run times.
