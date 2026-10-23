# TD-0075 Tests That Depend on the Real Clock Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the four tests of TD-0075 wait for state instead of the real clock, and resolve TD-0075.

**Architecture:** Each test proves its point from a state the program or the server shows, through `Eventually` (whose `Patience` is the one limit), or from an answer that a wrong behaviour could not give.  No test reads a stopwatch or sleeps.

**Tech Stack:** xunit v3, Shouldly, Microsoft.Data.SqlClient 7, `Eventually` and `Programs` of `tests/SqlSource.Tool.Testing`.

**Spec:** `docs/tech-debt/TD-0075-tests-that-depend-on-the-real-clock.md` (Problem and Proposed fix), with the unit's instructions: item 1 must run the `SqlException` path it is named for and must not pass when the token fires first; items 1 and 2 wait for state through `Eventually`; item 3 ("keep, or widen") is fixed with waits on state, and the item is resolved.

**Where the code differs from the item:** the item as merged by td-0074 already lacks its old item 3 (ordering by `MetadataToken`, done by `ModelReflection`), so nothing of it remains.  The unit's "item 4" is the item's 3 today.

## Global Constraints

- Test names are `Method_Scenario_Outcome`; assertions are Shouldly's; every test class is `sealed`.
- A test that waits for what a program or the server does waits through `Eventually`, never a loop of `Task.Delay` (`tests/AGENTS.md`).
- A program that waits comes from `Programs.Waiter`, whichever the operating system.
- `./pre-commit-validation.sh` passes on every commit; after the change to `tests/SqlSource.Tests.SqlServer`, that project also runs on `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.
- The Windows test of `TerminationTests` runs only in `nightly.yml`; it is tried by starting `nightly.yml` by hand on the branch once it is pushed.

## Review Focus

1. Item 1 on a slow runner: the `Eventually` poll takes longer than SqlClient's 30-second command timeout, and the command ends by timeout instead of by the token — the test's command sets `CommandTimeout = 0`, so only the token ends it.
2. Item 1 when the wait is never seen: the test must cancel the command anyway (in a `finally`) so that disposing the session does not hang on a running command.
3. Item 2 on a non-English Windows: ping's text is localised — the state counts occurrences of `127.0.0.1`, which every language prints.
4. Item 2 reading a file that `cmd` holds open for writing — opened with `FileShare.ReadWrite | FileShare.Delete`; a missing file or a sharing error is "not yet".
5. Item 3's `SafetyTests` if the server ran the text: the call must fail, not pass slowly — the wait is far longer than SqlClient's command timeout, so a run of it ends in an error answer with no rows, and the call is bounded by `Eventually.WithinAsync`.

---

### Task 1: Cancel the command once the server runs it (item 1)

**Files:**
- Modify: `tests/SqlSource.Tests.SqlServer/Describer/SessionTests.cs` (`AnswerAsync_CommandCancelledWhileTheServerRunsIt_ThrowsOperationCanceled`)

- [ ] **Step 1: Rewrite the test.**  Read the session's id first (`SELECT CAST(@@SPID AS int)` on `session.Connection`).  The call runs `WAITFOR DELAY '23:59:59'` with `CommandTimeout = 0`, catches a `SqlException` into a local (`thrown`) and rethrows it.  No `CancelAfter`.  Then:

```csharp
try
{
    await Eventually.TrueAsync(
        async () => await sqlServer.ScalarAsync(RunningWaitOf(id)) is 1,
        "The server never showed the session's WAITFOR running."
    );
}
finally
{
    await cancel.CancelAsync();
}

_ = await Should.ThrowAsync<OperationCanceledException>(answered);
thrown.ShouldNotBeNull("the token was cancelled before the server ran the command");
```

`RunningWaitOf(int id)` is `SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id = <id> AND wait_type = 'WAITFOR'`, formatted with the invariant culture.  The comment says the token is cancelled once the server shows the wait, so the `SqlException` is the path that runs.

- [ ] **Step 2: Prove it fails when the token fires first.**  Temporarily cancel before the call starts; run `dotnet test --project tests/SqlSource.Tests.SqlServer -- --filter-method "*CommandCancelledWhileTheServerRunsIt*"`; expected FAIL on `thrown.ShouldNotBeNull`.  Revert.

- [ ] **Step 3: Run it as written.**  Same command; expected PASS.

### Task 2: Replace the stopwatch and "at once" (item 3)

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/ProgramsTests.cs` (`RunAsync_ProgramThatOutlivesThePatience_IsKilledAndThrows`, `Sleeper`)
- Modify: `tests/SqlSource.Tests.SqlServer/Describer/SafetyTests.cs`
- Modify: `docs/architecture/sqlserver-describer.md` (the sentence on `SafetyTests.cs`: "the call answers at once")

- [ ] **Step 1: `ProgramsTests`.**  `Sleeper()` waits a day (`Programs.Waiter(86_400)`), so a program that is gone was killed.  The timeout test drops the `Stopwatch`; its `Should.ThrowAsync<TimeoutException>` is awaited through `Eventually.WithinAsync(..., "RunAsync did not give the program up at its patience.")`, and `ShouldHaveExited(seen)` is the proof.

- [ ] **Step 2: `SafetyTests`.**  Rename `ColumnsAsync_TextThatWaits_AnswersAtOnce` to `ColumnsAsync_TextThatWaits_DescribesItAndWaitsForNothing`.  The text is `WAITFOR DELAY '23:59:59'; SELECT 1 AS a`; the call goes through `Eventually.WithinAsync`; it asserts `answer.Error` is null and the visible names are `["a"]`.  The comment: had the server run the wait, SqlClient's command timeout would have ended the call with an error and no rows.  Remove `AtOnce`, the elapsed time and the `TimeProvider.System` timing from `ColumnsAsync`, which returns the answer alone.

- [ ] **Step 3: Docs.**  `docs/architecture/sqlserver-describer.md`: "the call answers at once" becomes "the call answers without waiting".

- [ ] **Step 4: Run.**  `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*ProgramsTests"` and the SQL Server project with `--filter-class "*SafetyTests"`; expected PASS.

### Task 3: The closed console waits on ping's answers (item 2)

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Commands/TerminationTests.cs`

- [ ] **Step 1: `ProjectThatWaits(string waiting, string? answers = null)`.**  On Windows the waiter's output goes to `answers` (`nul` when it is null) instead of `> nul`; the whole command is escaped with `SecurityElement.Escape`.  The Unix test's command is unchanged.

- [ ] **Step 2: The Windows test.**  Replace the wait for `waiting` and the three-second `Task.Delay` with `Eventually.TrueAsync(() => HasAnswered(answers), ...)`, then the checks it already makes (`shell.HasExited` false, one run folder).  `HasAnswered(string path)` reads the file with `FileShare.ReadWrite | FileShare.Delete` and is true when `127.0.0.1` occurs at least three times (ping's header and two answers, a second apart: the wait goes on); a missing file or an `IOException` is false.  The comment says why: a wait that failed would end the run by itself.

- [ ] **Step 3: Run on macOS.**  `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*TerminationTests"`; expected the two signal cases PASS and the Windows one SKIPPED.  The Windows case is tried through `nightly.yml` on the pushed branch.

### Task 4: Resolve TD-0075 and validate

**Files:**
- Delete: `docs/tech-debt/TD-0075-tests-that-depend-on-the-real-clock.md`
- Modify: `docs/tech-debt/README.md` (remove its row)

- [ ] **Step 1:** Delete the item and its row; `grep -rn "TD-0075" . --exclude-dir=bin --exclude-dir=obj` finds only past plans.
- [ ] **Step 2:** `./pre-commit-validation.sh` passes; the SQL Server project passes on the floor image.
- [ ] **Step 3:** Commit.
