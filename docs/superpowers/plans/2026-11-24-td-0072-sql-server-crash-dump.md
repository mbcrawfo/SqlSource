# TD-0072 SQL Server crash dump Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Copy the dump and log of a SQL Server container that crashed at its start out of the container before `SqlServerStart` disposes it, keep them as a CI artifact, and name the crash id in the report.

**Architecture:** `IStartingContainer` gains a read of a folder of the container as a tar archive, which `Testcontainer<T>` serves through Testcontainers' own Docker client.  `SqlServerStart` saves the archive of `/var/opt/mssql/log` and the whole console log in a folder per crash, under the folder that `SQLSOURCE_TESTS_SQLSERVER_CRASHES` names (the system's temp folder without it), before it disposes a crashed container, the first or the second, and its report names the crash id and that folder.  A failed save is reported and never stops the retry.  Each CI job that starts SQL Server containers sets the variable and uploads the folder when it holds anything.

**Tech Stack:** C#, xunit v3, Testcontainers 4.16 with Docker.DotNet.Enhanced 4.3.3, GitHub Actions.

**Spec:** `docs/tech-debt/TD-0072-sql-server-2025-container-crashes-at-start-in-ci.md`, with the unit's scope: copy the crash dump and log out of the container before `SqlServerStart` disposes it, keep them as a CI artifact, and report the crash id.  Change a memory limit only with evidence that it matters.  If the cause cannot be proved, keep the retry and rewrite the item to what is now known and how to read the artifact.  Do not move the pinned image.

## What the code and CI show (departures from the spec)

- The crash of run 39718598004 (first attempt, job 119626562644) logs `Reason: 0x00000002`, `Parameters: 0x10010`, a stack in `sqlpal.dll` under `lsasrv.dll` and `lsass.exe`, `Last errno: 11` (`Resource temporarily unavailable`), 4 processors and 16 GB, and `handle-crash.sh` leaves `core.sqlservr.<date>.<pid>.d/` with `crash.json`, `crash.txt`, `log/paldumper-debug.log` and the `.gdmp` core in `/var/opt/mssql/log`.
- Of 659 runs of `ci.yml` since 2026-10-22, none failed on the crash: the two failed SQL Server jobs (135363727353, 126270714749) hold no fatal error.  Whether the retry fired is not readable, since a job summary has no API.  So there is no evidence for a memory limit, and none is set.
- The spec's "both containers share the runner's memory while they run at once" is out of date: CI now runs the two SQL Server projects one after the other, each leg on a runner of its own.
- A crash is reproducible locally by `kill -SEGV` of the second `sqlservr` process; under emulation on Apple silicon the dump itself fails, but the folder and its `crash.json` are written.  `docker cp` of `/var/opt/mssql/log` from the exited container works.

## Global Constraints

- The pinned images do not move, and no `MSSQL_MEMORY_LIMIT_MB` is set.
- Every action is pinned to the SHA already used: `actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1`.
- TD-0072 stays open, rewritten; its README row too.  No user-visible effect, so no `site/` change.

## Review Focus

- A save that throws (Docker refuses, disk full): the retry still happens, and the report says the save failed and why.  Test: `StartAsync_SaveFails_ReportsItAndStartsAnother`.
- A crash log with no `Crash Id` line: the folder is named by the container's id.  Test: `StartAsync_NoCrashId_NamesTheFolderByTheContainer`.
- The second container crashes too: its dump is saved and reported before the exception leaves.  Test: `StartAsync_BothCrash_DisposesBothAndThrows` asserts two reports and two folders.
- A cancelled start saves nothing.  Test: `StartAsync_Cancelled_DisposesAndDoesNotRetry` asserts no archive read.
- Two crashes with the same id in one folder (a rerun of a job on a reused runner cannot happen on GitHub, but locally it can): the save overwrites, never fails.

---

### Task 1: Save the crash in the harness

**Files:**
- Modify: `tests/DatabaseTests/TestHarness/SqlServerStart.cs`
- Modify: `tests/SqlSource.Tests/TestHarness/FakeStartingContainer.cs`
- Test: `tests/SqlSource.Tests/TestHarness/SqlServerStartTests.cs`

**Interfaces:**
- `IStartingContainer` gains `string Id { get; }` and `Task<Stream> GetArchiveAsync(string path)`.
- `SqlServerStart.StartAsync<TStart>(Func<TStart> build, Func<string, Task> report, string crashFolder, CancellationToken token)`.
- `SqlServerStart.CrashesVariable = "SQLSOURCE_TESTS_SQLSERVER_CRASHES"`, `SqlServerStart.LogFolder = "/var/opt/mssql/log"`.
- Saved per crash: `<crashFolder>/<crash id or container id>/console.log` (stdout, then stderr) and `.../mssql-log.tar`.

- [ ] Tests: the existing crash tests also assert the folder holds both files with the fake's bytes and that the report names the crash id and the folder; add the Review Focus tests.
- [ ] Implement; `Testcontainer<T>.GetArchiveAsync` builds a client from `TestcontainersSettings.OS.DockerEndpointAuthConfig.GetDockerClientBuilder(ResourceReaper.DefaultSessionId)` and returns the archive's stream.
- [ ] Run `dotnet test --project tests/SqlSource.Tests -- --filter-class "*SqlServerStartTests"` and `*EngineImageTests`.
- [ ] Prove it on a real container: a scratch test (not committed) that starts the 2025 image, kills `sqlservr` with `SIGSEGV`, and saves it.
- [ ] Commit.

### Task 2: Keep it in CI and rewrite the item

**Files:**
- Modify: `.github/workflows/build.yml` (the `test` matrix and `test-sqlserver-floor`), `.github/workflows/nightly.yml` (`test-sqlserver`, `allocation-sqlserver`, `sync-async-sqlserver`), `.github/workflows/README.md` (Artifacts).
- Modify: `docs/tech-debt/TD-0072-*.md`, its row in `docs/tech-debt/README.md`, `tests/DatabaseTests/SqlServer/README.md`, `tests/README.md` (the harness row).

- [ ] Each job sets `SQLSOURCE_TESTS_SQLSERVER_CRASHES: ${{ github.workspace }}/artifacts/sqlserver-crashes` and, after its tests, uploads it as `sqlserver-crash-<job or leg>` with `if: ${{ !cancelled() }}`, `if-no-files-found: ignore` and `retention-days: 30`.
- [ ] Rewrite TD-0072 to what is known and how to read the artifact; keep the retry.
- [ ] `tools/actionlint.sh`, `./format.sh`, `./pre-commit-validation.sh`.
- [ ] Commit.
