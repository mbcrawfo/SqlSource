# TD-0072 - The SQL Server 2025 container sometimes crashes as it starts in CI, and the tests start another

## Problem

On the runners of `.github/workflows/build.yml`, the container of the default SQL Server image, `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04`, sometimes stops before it is ready.  `sqlservr` writes "This program has encountered a fatal error and cannot continue running", with `Reason: 0x00000002` and a stack in `sqlpal.dll` under `lsass.exe`, and dumps its core.  It happened in two of the four runs of the branch of phase 6.3 (runs 39718598004 and 39728643901), each time failing every test of the collection `UsesSqlServer`, and in none of the runs of `main` around them.  The run on the floor image, 2017, passed both times.

[`SqlServerStart`](../../tests/SqlSource.Testing/SqlServerStart.cs) works around it: [`SqlServerContainer`](../../tests/SqlSource.Tests.SqlServer/SqlServerContainer.cs) and [`AppContainer`](../../tests/SqlSource.Tests.SqlServer.SqlClient61/AppContainer.cs) start their container through it, and when the container has exited before it was ready with that line in its log, it disposes the container and starts a new one, once.  It reports the retry, with the log from the fatal error on, as a warning in the summary of the job on GitHub and as an xunit diagnostic message.  The crash itself is not understood: its cause, the runner's kernel, its memory or the image, is not known, and two crashes in a row still fail every test of the project.

## Why it exists

The crash is the server's own, at its start, before any statement of the tests reaches it, and it has not been seen on a developer's machine, so it could not be studied; a retry was the fix within reach.

## Impact

A crashed start now costs the time of a second start, about half a minute.  The retry hides the crash from a run's result: only the warning in the job's summary shows how often it happens.  Two crashes in a row still fail the run and need its failed jobs run again.

## Proposed fix

Find the cause from the dump `sqlservr` leaves in `/var/opt/mssql/log/`, kept by copying it out of the container before `SqlServerStart` disposes it, and set what the image needs on both containers, which share the runner's memory while they run at once: their memory limit (`MSSQL_MEMORY_LIMIT_MB`) is the first thing to try, though nothing yet links the crash to memory.  If the image is at fault, report the crash to Microsoft with the `Crash Id` from the warning's log, and move the default image to a build that fixes it.  Then remove `SqlServerStart` and this item.

## Trigger

The warning of `SqlServerStart` in the summary of more than one run in ten, a run that fails after the retry, or a new default image of SQL Server.
