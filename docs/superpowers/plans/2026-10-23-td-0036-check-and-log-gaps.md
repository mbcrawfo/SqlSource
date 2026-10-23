# TD-0036 Small Gaps of `--check` and the Log Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve `docs/tech-debt/TD-0036-two-small-gaps-of-check-and-the-log.md`: the log's `os` and `runtime` come through `ToolHost`; `--check --database X` reports a stale entry of another database whenever `describe --database X` would write the file; and the documentation of `run.end` says what its exit code is when the close of the log fails.

**Architecture:** Gap 1 adds two string members to `ToolHost`, filled by `ToolHost.Create` from `RuntimeInformation`, which `Cli.OpenLog` writes and the test hosts fix.  Gap 2 changes one condition of `SidecarCheck.EntriesWithoutAQuery`: the `--database` filter applies only when the file's outcome is not `FileAction.Write`.  Gap 3 is documentation and a test that pins the behaviour.

**Tech Stack:** C# / .NET 10, xUnit v3, Shouldly.

**Spec:** `docs/tech-debt/TD-0036-two-small-gaps-of-check-and-the-log.md` (Problem and Proposed fix), with the unit's instructions: all three gaps; gap 2 by its first option; gap 3 by a choice between the two options, with the reason.

## Where the code differs from the item, and the choices

- **Gap 2: what "describe would write the file" is, exactly.**  A sidecar that holds an entry without a query (a query that is gone or needs none) can never equal its file's target, since the target holds an entry only for each query that needs one.  So `describe --database X` writes such a file exactly when its outcome is not held back, and then the outcome is `Write`.  Under `--check` the outcome of the same file is decided by the same `FileOutcomes.Decide`; the only way the two modes hold a file back differently is a query whose describe failed under `--check` (it describes every selected query), and that is an error, exit `1`.  So the rule is: under `--database`, an entry without a query whose database is not named is reported when the file's outcome is `FileAction.Write`.  `SidecarCheck.Compare` already receives each file's outcome.
- **Gap 3: the second option, documentation.**  `RunLogFile.Write` already flushes the stream after every event, so the item's first option, "write `run.end`, flush the stream, and close it, and when the close fails write nothing more but report it", is what the code does today, and it still leaves a `run.end` line with the run's exit code in a file whose close then fails and makes the process exit `1`.  No order of writes can put in a file the outcome of closing that file.  So the documentation says that `run.end`'s exit code is the run's, and that a log that cannot be closed is reported as an error after it, which makes the process exit `1`.  A test pins it: a stream whose writes and flushes succeed and whose close fails.

## Global Constraints

- Keep changes to `Cli`, `ToolHost`, `SidecarCheck`, `FileOutcomes` and `SidecarStore` focused: td-0073 refactors `src/` at the same time.  `FileOutcomes` and `SidecarStore` do not change.
- User documentation (`docs/manual/`) describes effects, never internals; no tech-debt id there.
- Resolving the item, per `docs/tech-debt/AGENTS.md`: delete the item file, remove its row from `docs/tech-debt/README.md`, remove every reference to `TD-0036` (code comments, `src/SqlSource.Tool/AGENTS.md`, the epic spec), and remove its known issue from `docs/manual/tool.md`.  Old plans under `docs/superpowers/plans/` are history and keep their references.
- Every commit passes `./pre-commit-validation.sh` on the exact tree.  No describer changes, so no floor-image run.

## Review Focus

1. A stale entry of another database in a file that `--check --database X` holds back (a query of the file not selected and not current): `describe --database X` does not write the file, so the check must not report the entry.  Pinned in Task 2.
2. A stale entry with no database under `--database X`, in a file that would be written: reported.  Pinned in Task 2.
3. A file whose only difference is the stale entry: `--check --database X` exits `2`, and after `describe --database X` the same check exits `0` with the entry gone.  Pinned in Task 2.
4. A log stream whose close fails after every write succeeded: exit `1`, one `SQLSRC218`, and the file's `run.end` holds the run's own exit code `0`.  Pinned in Task 3.
5. `ToolHost.Create` gives the machine's descriptions, and the log writes the host's, not the machine's.  Pinned in Task 1.

---

### Task 1: The log's `os` and `runtime` come through `ToolHost`

**Files:**
- Modify: `src/SqlSource.Tool/ToolHost.cs` (two members, appended last; `Create` fills them)
- Modify: `src/SqlSource.Tool/Cli.cs` (`OpenLog` writes `host.Runtime` and `host.OperatingSystem`; drop `using System.Runtime.InteropServices` only if nothing else needs it — `PosixSignalRegistration` does, so it stays)
- Modify: `tests/SqlSource.Tool.Testing/Hosts.cs` (both hosts give fixed values)
- Modify: `tests/SqlSource.Tool.Tests/ToolHostTests.cs`, `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs`
- Modify: `src/SqlSource.Tool/AGENTS.md` (line 11: drop the sentence about the one thing that does not come through `ToolHost`; name the two members), `docs/architecture/tool.md` (the host section lists them)

**Interfaces:**
- Produces: `ToolHost.Runtime` (`string`, the .NET description) and `ToolHost.OperatingSystem` (`string`, the system's description), positional parameters after `CreateFile`.  `Hosts.Runtime = ".NET of the test"` and `Hosts.OperatingSystem = "the system of the test"`, public constants of `Hosts`.

Naming: a property named `OperatingSystem` hides `System.OperatingSystem` inside `ToolHost` only; nothing there uses that type.  If the analyzers object, use `OperatingSystemDescription` and `RuntimeDescription` instead, everywhere in this task.

- [ ] **Step 1: Tests.**  In `ToolHostTests.Create_RealHost_IsTheConsoleTheCurrentDirectoryAndTheEnvironment` add `host.Runtime.ShouldBe(RuntimeInformation.FrameworkDescription)` and `host.OperatingSystem.ShouldBe(RuntimeInformation.OSDescription)`.  In `RunLogTests.Run_WithLog_StartsWithWhatRanAndEndsWithHowItEnded` expect `Hosts.Runtime` and `Hosts.OperatingSystem`, and drop its `using System.Runtime.InteropServices` if unused.
- [ ] **Step 2: Build fails** (`dotnet build tests/SqlSource.Tool.Tests`): the members do not exist.
- [ ] **Step 3: Implement** the members, `Create`, `Cli.OpenLog`, and `Hosts.Create`/`Hosts.Real` (both give the constants).  Document each parameter in the record's `<param>` list.
- [ ] **Step 4: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~ToolHostTests|FullyQualifiedName~RunLogTests"`: pass.
- [ ] **Step 5: Docs** as listed under Files.
- [ ] **Step 6: Validate and commit**: `./pre-commit-validation.sh`, then commit "Take the log's os and runtime from ToolHost".

### Task 2: `--check --database` reports a stale entry of a file that `describe` would write

**Files:**
- Modify: `src/SqlSource.Tool/Describing/SidecarCheck.cs` (`EntriesWithoutAQuery` takes the `FileOutcome`; the `databases` `<param>` of `Compare` says the new rule)
- Modify: `tests/SqlSource.Tool.Tests/Commands/CheckFilterTests.cs`
- Modify: `docs/manual/tool.md` (line 174: drop the sentence about the gap and "A check with no filter has no such gap"; line 320: delete the known issue), `docs/architecture/tool.md` (line 170: the new condition), `src/SqlSource.Tool/AGENTS.md` (line 69: drop "with one exception … (TD-0036)"), `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (line 577: drop the ", but for an entry … (TD-0036)" clause)

**Interfaces:**
- Consumes: `FileOutcome.Action`, `FileAction.Write` (unchanged).

- [ ] **Step 1: Tests** in `CheckFilterTests` (Users.sql holds `GetUser` of `postgres` and `GetInvoice` of `billing`; `AddAnEntryOfAQueryThatIsGoneAsync(database)` adds an entry `Gone`):
  - `Check_EntryOfAQueryThatIsGoneUnderDatabase_IsReportedForItsOwnDatabaseAlone` becomes `Check_EntryOfAQueryThatIsGoneUnderDatabase_IsReportedWhenDescribeWouldWriteTheFile`: `[InlineData("billing")] [InlineData("BILLING")] [InlineData("postgres")]`, each `ExitCode.ShouldBe(2)` and `ReportsGone(result).ShouldBeTrue()`.
  - `Check_EntryOfAQueryThatIsGoneWithNoDatabase_IsNotReportedUnderDatabase` becomes `…_IsReportedUnderDatabase`: exit `2`, reported; comment without the TD reference.
  - New `Check_EntryOfAnotherDatabaseInAHeldBackFile_IsNotReported`: after the first describe, add `Gone` of `billing`, then edit Users.sql with `SqlTexts.WithSecondQueryEdited` (GetInvoice's hash changes, so under `--database postgres` it is left out and the file is held back).  `--check --database postgres`: exit `0`, `ReportsGone` false.  Then `describe --database postgres` exits `0` (GetUser is current and skipped, so nothing is described and there is no `SQLSRC217`) and leaves the sidecar's text as it was (read before and after).
  - New `Check_StaleEntryOfAnotherDatabase_AgreesWithDescribe`: after the first describe add `Gone` of `billing`; `--check --database postgres` exits `2` and reports it; `describe --database postgres` exits `0`; `--check --database postgres` then exits `0` with an empty error.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tool.Tests --filter FullyQualifiedName~CheckFilterTests`: the changed and new reporting tests fail.
- [ ] **Step 3: Implement**: pass `outcome` to `EntriesWithoutAQuery`, and skip an entry for the database filter only when `outcome.Action != FileAction.Write`.  Comment, one line: the entry by itself makes the file's text differ from its target, so a file that describe writes drops it whatever its database.
- [ ] **Step 4: Run** the filter again: pass.  Run `dotnet test tests/SqlSource.Tool.Tests`: pass.
- [ ] **Step 5: Docs** as listed under Files.
- [ ] **Step 6: Validate and commit** "Report a stale entry of another database when describe would write its file".

### Task 3: `run.end` and a log that cannot be closed; resolve the item

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs` (new test and a stream class beside `FullStream`)
- Modify: `docs/manual/tool.md` (the list of what a log holds, last bullet "each error, and how the run ended": add that a log that cannot be finished is reported as an error once the run has ended, which makes the exit code `1` while the log's last line holds the code the run had before), `docs/architecture/tool.md` (line 232: `Cli.End` writes `run.end` with the run's exit code and then closes the file; a failed close is reported after it, so `run.end` holds the exit code before the close), `src/SqlSource.Tool/Cli.cs` (comment of `End`: the same, in one sentence)
- Delete: `docs/tech-debt/TD-0036-two-small-gaps-of-check-and-the-log.md`; remove its row from `docs/tech-debt/README.md`
- Check: `grep -rn "TD-0036" . --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj` lists only `docs/superpowers/plans/` files.

- [ ] **Step 1: Test** `Run_LogWhoseCloseFails_IsOneSqlsrc218AndItsRunEndHoldsTheRunsExitCode`: `_scene.Run.CreateFile = path => new UnclosableStream(path)` where `UnclosableStream` is a `FileStream` subclass (`FileMode.Create`, `FileAccess.Write`, `FileShare.Read`) whose `Dispose(bool disposing)` calls `base.Dispose(disposing)` and then throws `new IOException("the file could not be closed")` once when disposing.  Assert exit `1`; the error starts with `$"{Log} : error SQLSRC218: '{Log}' could not be written: the file could not be closed"` and holds one `error SQLSRC`; `LogReader.Read(Log)[^1]` is `run.end` with `exitCode` `0`.
- [ ] **Step 2: Run** it: it passes on the current code (it pins documented behaviour).  If it does not, stop and report.
- [ ] **Step 3: Docs and comment** as listed.
- [ ] **Step 4: Resolve the item** as listed, and run the grep.
- [ ] **Step 5: Validate and commit** "Say what run.end holds when the log cannot be closed, and resolve TD-0036".
