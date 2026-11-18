# Test cleanup phase 31: tool logging folds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Drop the `"tmp"` row of `RunLogDestinationTests.Run_LogThatCannotBeWritten_IsSqlsrc218AndTheRunDoesNotStart`, keeping `"no-such-folder/run.log"` to prove that a log that cannot be written is SQLSRC218 and the run does not start, with the guard showing nothing lost.

**Architecture:** One theory becomes a fact, no source change.  Both rows reach the one `catch` of `RunLogFile.Open`, whose filter `FileErrors.IsPathError` takes every path error alike, and the test's assertions stop at the system's reason, so the row's reason never matters.  `RunLogFileTests.Open_PathThatCannotBeWritten_GivesNoLogAndTheErrorWithTheReason` keeps the directory (`"folder"`) and the missing folder at unit level.  With one row left the theory is a fact, as phases 19 and 29 did, and its comment, which only says why `tmp` is a directory, goes.  The guard is the `tool-logging` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Logging/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-31-tool-logging-folds-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but the one the spec names; its name stays, since it still says what it proves.
- The fact keeps the theory's call and assertions, with a local constant `Given`, `"no-such-folder/run.log"`, in place of the parameter `given`.
- `RunLogFileTests.Open_PathThatCannotBeWritten_GivesNoLogAndTheErrorWithTheReason` stays as it is: it is the cover.
- The epic's phase table: change only row 31.
- The theory does not branch on its row, so TD-0089 does not change; its entry for `RunLogDestinationTests` names another test, and `src/SqlSource.Tool/Logging/AGENTS.md` names only the file, so both stay true.

## Review Focus

None: the theory keeps its second row as a fact.  Checked against the spec on `d168504e`: `RunLogFile.Open` still has one `catch`, filtered by `FileErrors.IsPathError`, which takes `IOException`, `UnauthorizedAccessException`, `ArgumentException` and `NotSupportedException` alike, and the cover still holds `"folder"` and `"no-such-folder/run.log"`.

---

### Task 0: The guard's "before" half

- [x] The `tool-logging` mutation run into `artifacts/test-cleanup/phase-31/tool-logging/before` (p16's "after" report is in an archived worktree, and the run takes under 4 minutes).
- [x] `kills` on the before report for the test.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-31/coverage-before/SqlSource.Tool.Tests`, with the line figures of each file of `src/SqlSource.Tool/Logging/**/*.cs`.

### Task 1: Fold the test

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Logging/RunLogDestinationTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 31's state)

- [x] **Step 1:** Replace the theory, its comment and its rows with:

```csharp
    [Fact]
    public async Task Run_LogThatCannotBeWritten_IsSqlsrc218AndTheRunDoesNotStart()
    {
        const string Given = "no-such-folder/run.log";
        var path = _scene.Run.Folder.PathOf(Given);

        var result = await _scene.DescribeAsync("--log", Given);

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith($"{path} : error SQLSRC218: '{path}' could not be written: ");
        _scene.Describer.Opened.ShouldBeEmpty();
    }
```

- [x] **Step 2:** Set row 31 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then the `Logging` tests of `tests/SqlSource.Tool.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the folded test did not make.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-31/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/Logging/**/*.cs` falls.
- [x] **Step 3:** The pull request lists the dropped row beside its cover, the compare result, the coverage figures and the run times.
