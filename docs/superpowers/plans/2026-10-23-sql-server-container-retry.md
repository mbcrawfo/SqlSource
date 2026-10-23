# Retry the SQL Server 2025 Container That Crashes at Start Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A SQL Server container of the tests that stops at its start with `sqlservr`'s fatal error is disposed and replaced by a new one, once, so that the random crash of TD-0072 no longer fails every test of a project in CI.

**Architecture:** One static helper, `SqlServerStart` in `tests/SqlSource.Testing`, builds and starts a container and decides whether to retry.  Its logic runs over a small internal seam, `IStartingContainer`, which a private adapter fills from Testcontainers' `IContainer` and the tests fill with a hand-written fake, since the crash does not reproduce locally.  `SqlServerContainer` (of `tests/SqlSource.Tests.SqlServer`) and `AppContainer` (of `tests/SqlSource.Tests.SqlServer.SqlClient61`) call it in place of `_container.StartAsync`.

**Tech Stack:** C# 14 / .NET 10, xunit v3 on Microsoft.Testing.Platform, Testcontainers.MsSql 4.16.0, Shouldly.

**Spec:** `docs/tech-debt/TD-0072-sql-server-2025-container-crashes-at-start-in-ci.md` (Problem and Proposed fix), with the unit's instructions from the implementation manager.

## What the code says that the item does not

- Testcontainers 4.16 refreshes the container's state on every check of its wait strategy and throws `ContainerNotRunningException` when the container has exited ([`DockerContainer.CheckReadinessAsync`](https://github.com/testcontainers/testcontainers-dotnet/blob/4.16.0/src/Testcontainers/Containers/DockerContainer.cs)).  So after a failed `StartAsync`, `IContainer.State == TestcontainersStates.Exited` means "stopped before it was ready", and `GetLogsAsync` still reads the log, since the container is not removed until it is disposed.
- "Say so in the test's output": checked with a throwaway probe, `dotnet test` on Microsoft.Testing.Platform, as CI runs it, shows none of a fixture's console writes, xunit's diagnostic messages (even with `--xunit-diagnostics on`) or test warnings; only a test's own output helper, and only with `--output detailed` or on a failure.  xunit's own runner shows a fixture's diagnostic message with `-diagnostics`.  So the helper reports twice: as an xunit diagnostic message, and, when `GITHUB_STEP_SUMMARY` is set, as a warning appended to the job's summary on GitHub, which is where a reader of a CI run sees it.  The report carries the log from the fatal-error line on (bounded), which holds the `Crash Id` the item asks to report to Microsoft.
- `NSubstitute` has a central version but no project references it; the repository's tests use hand-written fakes (`FakeProcessRunner`).  `IContainer` has 43 members, so the seam is a 4-member internal interface instead.
- `tests/SqlSource.Testing` gains a reference to `Testcontainers.MsSql`, which brings Testcontainers and no database driver.  Every project that references the library gets Testcontainers transitively, RoslynFloor included; it brings no Roslyn, so the pinned compiler is untouched.  Their lock files change with the restore.

## Decisions

- **`MSSQL_MEMORY_LIMIT_MB` is not set.**  No evidence links the crash to memory: `Reason: 0x00000002` under `lsass.exe` in `sqlpal.dll` is not an out-of-memory report, and the floor image, which runs alone, never crashed but also never ran beside a second container.  The narrowed item keeps it as a lead.
- **The retry narrows TD-0072; it does not resolve it.**  The cause is still unknown, the retry is a workaround (which `docs/tech-debt/AGENTS.md` counts as tech debt), and two crashes in a row still fail the run.  The item is rewritten to describe what remains: the unknown cause, hidden now behind a retry that reports itself, with the dump, the memory limit and the report to Microsoft as the fix, and a new trigger.  Its impact drops to Low.
- **Placement.**  `SqlServerStart.cs` at the root of `tests/SqlSource.Testing`, beside `DefaultImages.cs`, where td-0074's `EngineImage` will also go.  The container files change only at their start, their field and their `DisposeAsync`.

## Global Constraints

- The fatal-error line, verbatim: `This program has encountered a fatal error and cannot continue running`.
- At most one retry: the first container that failed is disposed and never started again; a new one is built from the same `MsSqlBuilder`; a second failure disposes that one and throws its own exception.
- A failure that is not this crash (still running, exited without the line, or cancelled) disposes the container and throws, with no retry.
- Types of `tests/SqlSource.Testing` stay `internal` (tests/AGENTS.md).
- Test names are `Method_Scenario_Outcome`, assertions are Shouldly's.
- Every commit passes `./pre-commit-validation.sh`; after the change to `tests/SqlSource.Tests.SqlServer`, also run that project with `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.

## Review Focus

1. A start cancelled through the token: no retry, the container is disposed, `OperationCanceledException` reaches the caller.  Test: `StartAsync_Cancelled_DisposesAndDoesNotRetry`.
2. A start that times out while the server still runs (state `Running`): no retry and the log is not read.  Test: `StartAsync_FailsWhileRunning_DisposesAndThrows`.
3. The line in the error stream rather than the output stream.  Test: theory `StartAsync_FirstCrashes_DisposesItAndStartsAnother` over both streams.
4. Reading the log of the crashed container throws: the container is still disposed and the read's exception reaches the caller.  Test: `StartAsync_LogReadFails_DisposesAndThrows`.
5. A failure of the database build after a successful start: the fixtures' existing `catch` disposes the container that started, the second one after a retry.  Covered by the fixtures' `catch` using the field the helper filled; no test (it needs the crash).

---

### Task 1: The helper and its tests

**Files:**
- Create: `tests/SqlSource.Testing/SqlServerStart.cs`
- Modify: `tests/SqlSource.Testing/SqlSource.Testing.csproj` (add `<PackageReference Include="Testcontainers.MsSql" />` with a one-line comment: for `SqlServerStart`; it brings Testcontainers and no driver)
- Modify: every `packages.lock.json` the restore changes
- Test: `tests/SqlSource.Tests.SqlServer/SqlServerStartTests.cs` (not in the collection `UsesSqlServer`)

**Interfaces:**
- Produces:
  - `internal static class SqlServerStart`
    - `public const string FatalError = "This program has encountered a fatal error and cannot continue running";`
    - `public static Task<TContainer> StartAsync<TContainer>(Func<TContainer> build, CancellationToken token) where TContainer : IContainer` — the fixtures' entry point; reports through `ReportAsync(message, Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY"))`.
    - `internal static Task<TStart> StartAsync<TStart>(Func<TStart> build, Func<string, Task> report, CancellationToken token) where TStart : IStartingContainer` — the logic, under test.
    - `internal static Task ReportAsync(string message, string? summaryPath)` — sends `message` as an xunit diagnostic message, and when `summaryPath` is set appends to that file a GitHub warning block (`> [!WARNING]`, each line of the message quoted) followed by a blank line.
  - `internal interface IStartingContainer : IAsyncDisposable` with `string Image { get; }`, `TestcontainersStates State { get; }`, `Task StartAsync(CancellationToken token)`, `Task<(string Stdout, string Stderr)> GetLogsAsync()`.  A private sealed adapter over `IContainer` passes each through (`Image.FullName`, `GetLogsAsync(timestampsEnabled: false)`).

Logic of the internal `StartAsync`: build and start; on any failure, inside a `try`/`finally` that disposes the container, decide `crashed = !token.IsCancellationRequested && State == Exited && (stdout or stderr contains FatalError)` (read the log only when exited); rethrow unless crashed; when crashed, `await report(message)` and build and start a second one, disposing it and rethrowing on its failure.  The message names the image, says the tests start a new container once (TD-0072), and ends with the log from the line holding `FatalError` on, cut to 4,000 characters.

- [ ] **Step 1: Write the failing tests**, with a private `FakeContainer : IStartingContainer` in the test file that takes the exception its start throws (or none), its state after the start and its two log streams, and counts its starts and disposals.  Tests:
  - `StartAsync_FirstStarts_ReturnsItAndReportsNothing`: one build, the returned instance is the first, started once, not disposed, no report.
  - `StartAsync_FirstCrashes_DisposesItAndStartsAnother` (theory: the line in `Stdout`, in `Stderr`): two builds; returns the second; first started once and disposed once; second started once, not disposed; one report that contains the image and `FatalError`.
  - `StartAsync_BothCrash_DisposesBothAndThrows`: `Should.ThrowAsync` gives the second's exception (same instance); two builds, never three; both disposed once; one report.
  - `StartAsync_ExitsWithoutFatalError_DisposesAndThrows`: state `Exited`, log without the line: the first's exception, one build, disposed, no report.
  - `StartAsync_FailsWhileRunning_DisposesAndThrows`: state `Running`, log holding the line: no retry, the log not read (the fake counts reads), disposed.
  - `StartAsync_Cancelled_DisposesAndDoesNotRetry`: a cancelled token and a start throwing `OperationCanceledException` with a crashed state and log: `OperationCanceledException`, one build, disposed.
  - `StartAsync_LogReadFails_DisposesAndThrows`: state `Exited`, the log read throws `InvalidOperationException`: that exception, disposed, one build.
  - `ReportAsync_SummaryPath_AppendsAWarning`: a temporary file holding `before\n`; after `ReportAsync("one\ntwo", path)` it reads `before\n> [!WARNING]\n> one\n> two\n\n`.
  - `ReportAsync_NoSummaryPath_WritesNoFile`: completes with `null`.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build tests/SqlSource.Tests.SqlServer` — Expected: fails, `SqlServerStart` not defined.

- [ ] **Step 3: Write `SqlServerStart.cs` and the package reference; `dotnet restore` to update the lock files.**

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test --project tests/SqlSource.Tests.SqlServer -- --filter-class '*SqlServerStartTests'` — Expected: all pass, no container started.

### Task 2: The fixtures call it, and the documents follow

**Files:**
- Modify: `tests/SqlSource.Tests.SqlServer/SqlServerContainer.cs` (field, `InitializeAsync`, `DisposeAsync`)
- Modify: `tests/SqlSource.Tests.SqlServer.SqlClient61/AppContainer.cs` (the same)
- Modify: `docs/tech-debt/TD-0072-sql-server-2025-container-crashes-at-start-in-ci.md` (rewrite to what remains), `docs/tech-debt/README.md` (its row: Low, the new description)
- Modify: `tests/README.md` (`SqlServerStart` in the library's row, and the two SQL Server projects' reference lines), `tests/AGENTS.md` (the `UsesSqlServer` rule: a SQL Server container of the tests starts through `SqlServerStart.StartAsync`, never `StartAsync` of the container)
- Check: `tests/SqlSource.Tests.SqlServer/README.md` and `CONTRIBUTING.md` for text the change makes wrong.

**Interfaces:**
- Consumes: `SqlServerStart.StartAsync<TContainer>(Func<TContainer>, CancellationToken)` of Task 1.

- [ ] **Step 1: Change both fixtures.**  The field becomes `private MsSqlContainer? _container;`, read through a private `Container` property that throws `InvalidOperationException` before the start; `InitializeAsync` begins `_container = await SqlServerStart.StartAsync(new MsSqlBuilder(<image>).Build, token);` and keeps its `try`/`catch` over the database build, disposing `_container`; `DisposeAsync` is `_container?.DisposeAsync() ?? ValueTask.CompletedTask`.  The comment on the `catch` stays true.
- [ ] **Step 2: Rewrite TD-0072 and its row; update `tests/README.md` and `tests/AGENTS.md`.**
- [ ] **Step 3: Validate.**  Run `./pre-commit-validation.sh` — Expected: passes.  Then `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/SqlSource.Tests.SqlServer` — Expected: passes.
- [ ] **Step 4: Commit** the plan, Task 1 and Task 2 together (`git add` the named files and lock files; message `Retry the SQL Server container that crashes at its start (TD-0072)`).
