# Test cleanup phase 49: tool commands strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `VerboseTests.Run_VerboseWithoutLog_WritesNoFileButTheSidecars` assert what its name promises: that `--verbose` without `--log` adds no file to the run's folder but the two sidecars, not only that it writes no `*.log`.

**Architecture:** The test lists every file under the run's folder before the run and after it, and asserts that the files the run added are `Users.sql.json` and `Lib/Items.sql.json`.  No source or helper changes.  The guard is the `tool-root` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-49-tool-commands-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the one test changes; it keeps its name.  It is not a theory, so TD-0089 is not touched.
- The epic's phase table: change only row 49.

Checked against the spec on `d0fed261`: the test still asserts only that no `*.log` is under the run's folder, so the finding holds.  No merged phase changed `src/SqlSource.Tool/*.cs` or the scope's tests since p28's run on `38eb4c64`, so p28's "after" report is this phase's "before".

Where the code differs from the spec: the scene's fake build (`TestProject.ManifestText`) writes each project's `sqlsource.json` when the run asks for its manifest, so those two files appear during the run although no part of the tool writes them.  The verifier named only `Tools/obj/project.assets.json`, which the scene writes before the run.  The test leaves the two settings files out of the run's files by their paths, `_scene.Project.SettingsPath` and `_lib.SettingsPath`, with a comment that says why.

## Review Focus

The added files are compared as a set of full paths; a run that writes a trace, a `.txt` or any `.json` but the two sidecars fails the test.

---

### Task 0: The guard's "before" half

- [x] Mutation: p28's `reports/p28-tool-root-after.json` (tree of `38eb4c64`) copied to `artifacts/test-cleanup/phase-49/tool-root/before/reports/mutation-report.json`.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-49/coverage-before/SqlSource.Tool.Tests` (7,077 tests, 2 min 26 s).

### Task 1: Apply the finding

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Commands/VerboseTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 49's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests.Commands-2-01):** add `FilesOfTheRun()`, every file under the run's folder; the test takes it before the run and asserts that the files added, less the projects' settings files, are `_users + ".json"` and `_items + ".json"`, in any order.
- [x] **Step 2:** Set row 49 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `VerboseTests` passes (13 tests).
- [x] **Step 4:** Hand check: with the fake describer made to write `trace.txt` into the run's folder, the test fails and names `trace.txt`; the old `*.log` assertion would have passed.  Reverted.
- [x] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-49/tool-root/after`: 470 killed, 0 survived, 101 timeouts, 1 h 57 min (p28's "before": 431 killed, 140 timeouts).  `compare`: 571 detected before, 571 still detected, 0 lost, 0 not comparable.  `kills` for the changed test: before, 2 kills, 0 alone, 67 timeouts under it; after, 60 kills, 0 alone, 55 timeouts under it, 0 survivors under it.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-49/coverage-after/SqlSource.Tool.Tests` (7,077 tests, 3 min 48 s): 1,216 of 1,224 lines over the 14 files of `src/SqlSource.Tool/*.cs`, before and after, the same lines in each file.
- [x] **Step 3:** Copy the "after" report to the run's `reports/p49-tool-root-after.json`.
- [x] **Step 4:** The pull request gives the change, the compare result, the coverage figures, the hand check and the run times.
