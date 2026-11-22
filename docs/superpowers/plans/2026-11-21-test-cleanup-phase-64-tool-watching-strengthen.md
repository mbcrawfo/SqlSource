# Test cleanup phase 64: tool watching strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `Classify_UnderBinObjOrGit_IsNone` fail when `.git` is taken out of `WatchSet`'s ignored folders, through a path below `.git` that would otherwise be new content of the project.

**Architecture:** One row of one theory and, by the owner's decision on review finding R1-F1, one assertion of another, with no source or helper changes.  The guard is the `tool-watching` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Watching/**/*.cs`, and, since the folder list is a static initialiser that mutation cannot reach, one check by hand.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-64-tool-watching-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the theory of the finding changes, and the one assertion of `Classify_NamedEnvFileUnderAnIgnoredFolder_Describes` that the owner approved beyond the spec (review finding R1-F1, "A: replace it").
- The epic's phase table: change only row 64.

Checked against the spec on `f5a9fac4`: `IgnoredFolders` is still `["bin", "obj", ".git"]` at `WatchSet.cs:31`, `EffectOf` still returns `None` for an ignored path and asks for an evaluation for new content below a project, and the row `".git/index"` is outside the project and no listed, evaluation, `.sql`, `.cs` or settings file, so it reads `None` with or without the rule.  The finding holds.  The spec names no document to update, and none names the changed test.

## Review Focus

The finding allows replacing the row or joining it.  The new row joins it: `".git/index"` stays, so no row is removed and none needs a cover.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-64/tool-watching/before` (Windows): 222 mutants tested, 185 killed, 37 survived, 0 timeouts, 0 errors, 176 without coverage; the tested run took 1 min 48 s.  `kills`: `Classify_UnderBinObjOrGit_IsNone` 7 rows, 14 kills, 0 alone, 0 timeouts under it, 14 survivors under it.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-64/coverage-before/SqlSource.Tool.Tests` (7,095 tests, 19 skipped, 4 min 12 s): 717 of 740 lines over the 8 files of `src/SqlSource.Tool/Watching/`.

### Task 1: Apply the finding

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Watching/WatchSetTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 64's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests-4-06):** `Classify_UnderBinObjOrGit_IsNone` gains the row `"App/.git/x/New.cs"`, the path the finding proposes.
- [x] **Step 2:** The check by hand: with `.git` taken out of `IgnoredFolders`, the theory fails on that row alone, 1 of 8, with `WatchEffect.Evaluate` where `None` is expected.  The source was put back.
- [x] **Step 3:** Set row 64 of the epic's table to `done`.
- [x] **Step 4:** `./format.sh`.
- [x] **Step 5 (review finding R1-F1, the owner's decision):** In `Classify_NamedEnvFileUnderAnIgnoredFolder_Describes`, the assertion that `Changed` of `.git/index` is `None`, which reads `None` with or without the rule, becomes `Created` of `App/.git/x/New.cs`.  With `.git` taken out of `IgnoredFolders`, all 5 rows of that theory now fail, where only `App/.git/sqlsource.env` failed before.  The source was put back.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-64/tool-watching/after` (Windows): 222 mutants tested, 185 killed, 37 survived, 0 timeouts, 0 errors, 176 without coverage; the tested run took 1 min 50 s.  This run includes step 5 of Task 1, on `main` with phase 60 merged, which changes nothing under `Watching`.  `compare`: 185 detected before, 185 still detected, 0 lost, 0 not comparable.  The finding names no survivor by place.  `kills`, before and after: `Classify_UnderBinObjOrGit_IsNone` 7 and 8 rows, each 14 kills, 0 alone, 0 timeouts under it, 14 survivors under it; `Classify_NamedEnvFileUnderAnIgnoredFolder_Describes` 5 rows, 34 kills, 0 alone, 0 timeouts under it, 16 survivors under it.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-64/coverage-after/SqlSource.Tool.Tests` (7,097 tests, 19 skipped, 7 min 22 s, against 7,095 tests in 4 min 12 s before; the two more are this phase's row and a test phase 60 merged): 717 of 740 lines, before and after, the same figure in each of the 8 files.  The two run times are of one machine under different loads, so their difference is not the change's.
- [ ] **Step 3:** The pull request gives the change, the owner-approved assertion beyond the spec, the compare result, the checks by hand, the coverage figures and the run times.  No test or row is removed, so none is listed beside a cover.
