# Test cleanup phase 32: tool logging mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete `RunLogTests.RunStart_Arguments_KeepTheOptionAndTheSeparatorOfAConnection` and drop the `("Unreadable", true, …)` row of `RunLogTests.Sidecar_EachAction_IsItsWord`, with the guard showing nothing lost.

**Architecture:** Two test changes in one file, no source change.  The deleted theory's rows `--connection=` and `--connection:` are rows of `RunLogRedactionTests.Run_ConnectionOnTheCommandLine_IsLoggedWithNeitherItsNameNorItsValue`, which asserts the same `option + "***"` token and more; `LoggedArguments.Of` cuts the token by the value's length and never reads the database's name in it.  The dropped row is a call the run never makes (`SidecarStore` passes `FileAction.Unreadable` only with `failed: false`), and `failed` picks `SidecarFailed` before the action is read, which the rows `("Write", true, …)` and `("Delete", true, …)` show.  The guard is the `tool-logging` mutation scope, the coverage of `src/SqlSource.Tool/Logging/**/*.cs`, and, for the blind fold, its cover and the project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-32-tool-logging-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but the two the spec names; the `--database` twin, `RunStart_Arguments_KeepTheOptionAndTheSeparatorOfADatabase`, stays.
- `RunLogRedactionTests.Run_ConnectionOnTheCommandLine_IsLoggedWithNeitherItsNameNorItsValue` and the other seven rows of `Sidecar_EachAction_IsItsWord` stay as they are: they are the covers.
- The epic's phase table: change only row 32.
- Neither theory branches on its row, and TD-0089 names neither, so TD-0089 does not change.  `src/SqlSource.Tool/Logging/AGENTS.md` names only the file `RunLogTests.cs`, which stays, so it stays true.

## Review Focus

None.  Checked against the spec on `e7943725`: `LoggedArguments.Of` still cuts `token[..(token.Length - value.Text.Length)]` at `value.Position - 1`; the cover still holds the rows `--connection=***` and `--connection:***`; `SidecarStore.cs:133` is still the one call with `FileAction.Unreadable`, with `failed: false`; `RunEvents.Sidecar` still reads `failed ? SidecarFailed : SidecarActionOf(action)`.

---

### Task 0: The guard's "before" half

- [x] The `tool-logging` mutation run into `artifacts/test-cleanup/phase-32/tool-logging/before` (the run takes under 4 minutes).
- [x] `kills` on the before report for both tests.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-32/coverage-before/SqlSource.Tool.Tests`, with the line figures of each file of `src/SqlSource.Tool/Logging/**/*.cs`.
- [x] The run time of `dotnet test --project tests/SqlSource.Tool.Tests`.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 32's state)

- [x] **Step 1:** Delete `RunStart_Arguments_KeepTheOptionAndTheSeparatorOfAConnection` with its attributes and rows.
- [x] **Step 2:** Delete the row `[InlineData("Unreadable", true, "failed", "could not be changed")]` of `Sidecar_EachAction_IsItsWord`.
- [x] **Step 3:** Set row 32 of the epic's table to `done`.
- [x] **Step 4:** `./format.sh`, then the `Logging` tests of `tests/SqlSource.Tool.Tests`: every test passes.
- [x] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows neither changed test made.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-32/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/Logging/**/*.cs` falls.
- [x] **Step 3:** The run time, interleaved with a "before" run on the same machine.
- [x] **Step 4:** The pull request lists the removed test and row beside their covers, the compare result, the coverage figures and the run times.
