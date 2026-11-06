# TD-0111 - The poller's tests run beside a real timer

## Problem

Found by the maintainability review of 2026-11-06.  [`PollingFolderWatcherTests`](../../tests/SqlSource.Tool.Tests/Watching/PollingFolderWatcherTests.cs) gives the watcher `StillClock`, a `TimeProvider` that overrides nothing, so its `CreateTimer` is the system's.  [`PollingFolderWatcher`](../../src/SqlSource.Tool/Watching/PollingFolderWatcher.cs)'s constructor starts that timer, which calls `Poll` every `PollingFolderWatcher.Interval`, one second, on a thread of the pool, beside each test's own `Poll`.  `tests/AGENTS.md` says a test holds on CI's busy runner and that no timer decides an order.

A test that runs past one second can see a timer's poll between two steps of one change, and six tests can then fail:

- `Poll_FileCreated_RaisesCreated`, `Poll_ProjectAssetsCreatedBelowObj_RaisesCreated` and `Poll_NotRecursive_RaisesNothingForAFileBelow`: a poll between the create and the write of `File.WriteAllText` raises `Created`, and the test's poll then `Changed`.
- `Poll_FileRewritten_RaisesChanged` and `Poll_ProjectAssetsWritten_RaisesChanged`: a poll between `File.WriteAllText` and `File.SetLastWriteTimeUtc` raises `Changed` twice.
- `Poll_FolderGone_RaisesOneFailureUntilItIsBack`: a poll between its two `Directory.Delete` calls raises `Deleted` for the inner folder, so `Seen.ShouldBeEmpty()` fails.

The class's other five tests make one change or none, so an early poll gives them the same result.

## Why it exists

`StillClock` was written to keep the timer from firing and does not; its comment said it did until this review corrected it.

## Impact

Low: a rare failure on a stalled runner, which a rerun passes.

## Proposed fix

Pass `new TestClock()` of `tests/SqlSource.Tool.Testing` in the class's `Scene.Start`, as `Commands/WatchFolderTests.cs` already does with this watcher, delete `StillClock`, and say again in the class comment that the timer never fires.

## Trigger

A failure of one of these tests, or the next change to the class.
