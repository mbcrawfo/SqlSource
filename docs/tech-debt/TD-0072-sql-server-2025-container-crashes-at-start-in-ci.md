# TD-0072 - The SQL Server 2025 container sometimes crashes as it starts in CI

## Problem

On the runners of `.github/workflows/build.yml`, the container of the default SQL Server image, `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04`, sometimes stops before it is ready.  `sqlservr` writes "This program has encountered a fatal error and cannot continue running", with `Reason: 0x00000002` and a stack in `sqlpal.dll` under `lsass.exe`, and dumps its core.  Testcontainers' wait for the container then fails in [`SqlServerContainer.InitializeAsync`](../../tests/SqlSource.Tests.SqlServer/SqlServerContainer.cs), so every test of the collection `UsesSqlServer` fails on that run: 280 tests in run 39718598004 and 367 in run 39728643901, both on the branch of phase 6.3.  The run on the floor image, 2017, passed both times, and the first commit passed when its failed jobs were run again.  It happened in two of the four runs of that branch, and in none of the runs of `main` around them.

## Why it exists

The crash is the server's own, at its start, before any statement of the tests reaches it: no code of this repository runs inside the container before it.  Its cause, the runner's kernel, its memory or the image, is not known, and it has not been seen on a developer's machine.

## Impact

A pull request that changes nothing of SQL Server fails CI at random and needs its failed jobs run again, which costs a full run each time and can be mistaken for a failure of the change.

## Proposed fix

Start the container again once in `SqlServerContainer.InitializeAsync` when it stops before it is ready and its log holds the fatal-error line, and say so in the test's output; or find the cause from the dump `sqlservr` leaves in `/var/opt/mssql/log/` and set what the image needs, such as its memory limit (`MSSQL_MEMORY_LIMIT_MB`).  Report the crash to Microsoft with its `Crash Id` if the image is at fault.

## Trigger

A third failed run of this kind, or one on `main`.
