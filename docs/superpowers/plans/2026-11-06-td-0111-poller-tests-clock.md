# TD-0111 The Poller's Tests on a Still Clock Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `PollingFolderWatcherTests` gives the watcher a clock whose timer never fires, so only each test's own `Poll` lists the folder, and TD-0111 is resolved.

**Architecture:** `Scene.Start` passes `new TestClock()` of `tests/SqlSource.Tool.Testing`, whose timers fire only when a test calls `Advance`, which these tests never do.  `StillClock` is deleted and the class comment says the timer never fires.  No production code changes.

**Tech Stack:** C#, xUnit, Shouldly.

**Spec:** `docs/tech-debt/TD-0111-the-poller-tests-run-beside-a-real-timer.md`

## Global Constraints

- Test-only change to `tests/SqlSource.Tool.Tests/Watching/PollingFolderWatcherTests.cs`: the clock, `StillClock`'s removal and the class comment.  td-0110 builds its tests on this clock, so nothing else in the class changes.
- Resolve the item as `docs/tech-debt/AGENTS.md` says: delete the item file, remove its row (and only its row) from `docs/tech-debt/README.md`, and remove every reference to `TD-0111`.
- The one other reference is the sentence of `docs/tech-debt/TD-0109-…md` line 5, "The tests that depend on a timer or on the machine are TD-0111 to TD-0113.", which td-0112 and td-0113 also change: it becomes "… are TD-0112 and TD-0113.", and a sync keeps both sides' meaning.
- No manual known issue exists (the item has no user-visible effect).

## Review Focus

- A test that runs past one second: no listing may happen but the test's own `Poll`, which `TestClock` guarantees since nothing calls `Advance`.  Proven by the class passing; the race itself is not reproducible on demand.
- The constructor's first `Poll` still runs at `Start`, so the baseline listing is unchanged.

---

### Task 1: The still clock and the item's resolution

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Watching/PollingFolderWatcherTests.cs` (lines 11-12 comment, 201-203 `StillClock`, 216 the clock)
- Delete: `docs/tech-debt/TD-0111-the-poller-tests-run-beside-a-real-timer.md`
- Modify: `docs/tech-debt/README.md` (the TD-0111 row), `docs/tech-debt/TD-0109-duplicated-arrangements-and-uneven-conventions-in-tests.md` (line 5)

**Interfaces:**
- Consumes: `TestClock` (`internal sealed class TestClock : TimeProvider`, parameterless constructor) of `tests/SqlSource.Tool.Testing`, already visible to this project (`using SqlSource.Tool.Testing;` is present).
- Produces: `Scene.Start(bool recursive = true)` unchanged in signature, now built on `new TestClock()`.

- [ ] **Step 1:** In `Scene.Start`, replace `new StillClock()` with `new TestClock()`; delete `StillClock` and its comment.
- [ ] **Step 2:** Rewrite the class comment: each test lists the folder itself with `Poll`; the watcher's timer is `TestClock`'s, which fires only on `Advance`, which no test calls, so no other listing lands between two steps of a test's change.
- [ ] **Step 3:** Run `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~PollingFolderWatcherTests"`; expected: all pass.
- [ ] **Step 4:** Delete the item file, remove its README row, rewrite TD-0109 line 5 as above; `grep -rn "TD-0111" . --exclude-dir=.git` finds only this plan.
- [ ] **Step 5:** `./format.sh`, `./pre-commit-validation.sh`, commit.
