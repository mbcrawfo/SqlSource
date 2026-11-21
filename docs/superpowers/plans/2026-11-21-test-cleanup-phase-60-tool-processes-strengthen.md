# Test cleanup phase 60: tool processes strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `ProcessRunnerTests.Run_Cancelled_LeavesNeitherTheProgramNorWhatItStartedRunning` depend on the kill of the whole tree, make `Run_ProgramThatFails_GivesItsExitCodeAndItsErrorOutput` tell the error output from the standard output, and, as the owner added to this phase, stop `Run_Cancelled_EndsOnlyWhenTheProgramHas` flaking under load.

**Architecture:** One test file, `tests/SqlSource.Tool.Tests/Processes/ProcessRunnerTests.cs`.  The flake's fix also gives the internal constructor of `src/SqlSource.Tool/Processes/ProcessRunner.cs` the length of its wait for a killed program, which `WaitAfterKill` shows; a real run still waits five seconds.  The guard is the `tool-processes` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Processes/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-60-tool-processes-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the tests of the findings and the flaky test change in what they check.  `Run_CancelledAndTheKillFails_ThrowsTheCancellation` only passes the constructor's new argument, and it and `Run_Cancelled_Throws` get the longer `Wait()` (Q4).  `WaitAfterKill_OfARealRun_IsFiveSeconds` is the one new test, which the owner approved beyond the spec (Q3).
- The flake's fix is a commit of its own.
- The epic's phase table: change only row 60.

Checked against the spec on `c1a960bb`: the child still waits 30 seconds, `Eventually.Patience` is still two minutes, `ProcessRunner.cs:125:82` still survives, and `Run_ProgramThatFails_GivesItsExitCodeAndItsErrorOutput` still asserts only a non-zero code and that the two outputs together are not empty.  So both findings hold.  No document names a changed test or its class.

## Owner decisions

- **Q1, the flaky test ("A: change src").**  Under load, 20 of 576 runs of the test's steps failed, each at 5.0 s: the late kill came after the runner's five-second wait had given up.  The suggested wait through `Eventually` would have ended the flake but left the test checking nothing about the runner's wait, which it already did not: with `WaitForExit` removed from the runner, the old test passed 21 of 21 runs.  Stryker has no mutant on that line.  So the runner's internal constructor takes the wait, the test gives it `Eventually.Patience`, holds the kill for 500 ms, asserts that the run has not ended, and then lets the kill go.
- **Q2, the named survivor ("A: accept Timeout").**  The finding's proposal makes the child outlast `Eventually.Patience`, so under the mutant the test fails only when that two-minute wait gives up, after Stryker's limit for a mutant.  The owner accepts `Timeout` for `ProcessRunner.cs:125:82`, which is `132:82` after the runner's changes, as the one exception to the guard's `Killed`, with the hand check below.
- **Q3, review finding R1-F3 ("A: add the test").**  With the wait given by the test, nothing held the five seconds of a real run: with `KillWait` at zero, every Processes test passed.  `ProcessRunner` gets `internal TimeSpan WaitAfterKill`, and `WaitAfterKill_OfARealRun_IsFiveSeconds` holds it, in the form of `Patience_OfEveryWait_IsTwoMinutes`.
- **Q4, review finding R1-F4 ("A: extend Wait()").**  A 30-second program could end by itself during a stall, and a run that a test was about to cancel would then end normally.  The shared `Wait()` now outlasts `Eventually.Patience` by 30 seconds; no assertion changes.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-60/tool-processes/before`: 26 mutants tested, 16 killed, 4 survived, 6 timeouts, 0 errors; 3 min 17 s.  `kills`: `Run_Cancelled_LeavesNeitherTheProgramNorWhatItStartedRunning` 5 kills, 4 alone, 6 timeouts and 2 survivors under it, `125:82` among them; `Run_ProgramThatFails_GivesItsExitCodeAndItsErrorOutput` 0 kills, 3 timeouts and 2 survivors under it; `Run_Cancelled_EndsOnlyWhenTheProgramHas` 1 kill, 0 alone, 5 timeouts and 1 survivor under it.
- [x] Since the runner's changes move every later line of `ProcessRunner.cs` by seven, and `compare` matches by place, a second "before" into `artifacts/test-cleanup/phase-60/tool-processes/before-seam`: the old tests, with only the constructor's new argument, on the final source.  26 mutants tested, 16 killed, 4 survived, 6 timeouts, 0 errors; 3 min 1 s.  It has the same survivors and timeouts as the first, seven lines lower.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-60/coverage-before/SqlSource.Tool.Tests` (7,085 tests, 3 min 10 s): 79 of 79 lines over the three files of `src/SqlSource.Tool/Processes/` that have lines.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Processes/ProcessRunnerTests.cs`

- [x] **Step 1 (F-SqlSource.Tool.Tests-3-04):** The child that `WaitWithAChild` starts waits 600 seconds through the same `sleep` and `ping` arguments.  The test holds the child's process, so its id cannot pass to another, and ends it if the test fails, so a failure does not leave it for ten minutes.
- [x] **Step 2 (F-SqlSource.Tool.Tests-3-05):** `Run_ProgramThatFails_GivesItsExitCodeAndItsErrorOutput` runs `cmd /c` or `sh -c` writing `sqlsource-error` to its error output alone and ending with code 3, and asserts the code, that `Error` holds the text and that `Output` is empty.
- [x] **Step 3:** `./format.sh`, `./pre-commit-validation.sh`, commit.

### Task 2: The flaky test, a commit of its own

**Files:**
- Modify: `src/SqlSource.Tool/Processes/ProcessRunner.cs`
- Modify: `tests/SqlSource.Tool.Tests/Processes/ProcessRunnerTests.cs`

- [x] **Step 1:** `ProcessRunner(Action<Process> kill, TimeSpan killWait)`; the public constructor passes the five seconds.
- [x] **Step 2:** `Run_Cancelled_EndsOnlyWhenTheProgramHas` as Q1 says; `Run_CancelledAndTheKillFails_ThrowsTheCancellation` passes `Eventually.Patience`.
- [x] **Step 3:** By hand: the test passes 10 of 10 runs, and fails 10 of 10 with `WaitForExit` removed from the runner.  A prototype of its steps under the load that reproduced the flake, 96 at once, was right in 576 of 576 runs with the wait and saw the run end early in 576 of 576 without it.
- [x] **Step 4:** `./format.sh`, `./pre-commit-validation.sh`, commit.

### Task 3: The review's findings

- [x] **Step 1 (R1-F1):** `Run_Cancelled_EndsOnlyWhenTheProgramHas` starts a program that outlasts `Eventually.Patience`.  With a 31-second stall after the kill is asked for, the old test fails and the new one passes.
- [x] **Step 2 (R1-F2):** The tests keep an open handle on each process they read after the runner released it, through `Hold`, so that a reused id is neither read nor killed in its place.  A probe on Windows showed that `GetProcessById` alone does not keep the id, and that `SafeHandle` does.
- [x] **Step 3 (R1-F3, Q3):** `WaitAfterKill` and `WaitAfterKill_OfARealRun_IsFiveSeconds`.  The test fails with `KillWait` at zero and with the public constructor passing zero.
- [x] **Step 4 (R1-F4, Q4):** `Wait()` outlasts `Eventually.Patience`.  With a 31-second stall before the cancellation, the four rows of `Run_Cancelled_Throws` and `Run_CancelledAndTheKillFails_ThrowsTheCancellation` fail with the old 30 seconds and pass with the new wait.

### Task 4: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-60/tool-processes/after`: 26 mutants tested, 16 killed, 3 survived, 7 timeouts, 0 errors; 3 min 1 s.  `compare` against `before-seam`: 22 detected before, 22 still detected, 0 lost, 0 not comparable.  `kills` after: `Run_Cancelled_LeavesNeitherTheProgramNorWhatItStartedRunning` 5 kills, 4 alone, 7 timeouts and 1 survivor under it; `Run_ProgramThatFails_GivesItsExitCodeAndItsErrorOutput` 0 kills, 3 timeouts and 2 survivors under it; `Run_Cancelled_EndsOnlyWhenTheProgramHas` 1 kill, 0 alone, 5 timeouts and 1 survivor under it; `WaitAfterKill_OfARealRun_IsFiveSeconds` no mutant, since Stryker does not mutate the value it holds.
- [x] **Step 2 (F-SqlSource.Tool.Tests-3-04):** `ProcessRunner.cs:132:82`, the named `125:82`, is `Timeout` under the owner's exception (Q2).  By hand, with `entireProcessTree: false`, the test fails after 2 min 1 s with `TimeoutException: the program that the program started is still running`.
- [x] **Step 3 (F-SqlSource.Tool.Tests-3-05):** No survivor is named.  By hand, with the error output dropped by the runner, the test fails on the assertion it adds.
- [x] **Step 4:** Coverage into `artifacts/test-cleanup/phase-60/coverage-after/SqlSource.Tool.Tests` (7,091 tests, 2 min 57 s): 80 of 80 lines, so every line of each file is covered before and after; the added line is the constructor's assignment.
- [x] **Step 5:** Run time of the coverage runs of `tests/SqlSource.Tool.Tests`: 3 min 10 s before (7,085 tests) and 2 min 57 s after (7,091 tests: the one this phase adds and five from phases merged in between).
- [x] **Step 6:** Set row 60 of the epic's table to `done`.
- [ ] **Step 7:** The pull request gives the change, the compare result, the coverage figures and the run times.
