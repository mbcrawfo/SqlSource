# TD-0072 - The SQL Server 2025 container sometimes crashes as it starts in CI, and the tests start another

## Problem

On the runners of `.github/workflows/build.yml`, the container of the default SQL Server image, `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04`, sometimes stops before it is ready.  It happened in two of the four runs of the branch of phase 6.3 (runs 39718598004 and 39728643901), each time failing every test of the collection `UsesSqlServer`, and in none of the runs of `main` around them.  The run on the floor image, 2017, passed both times.

What the first crash logged is all that is known of it:

- `sqlservr` wrote "This program has encountered a fatal error and cannot continue running" with `Reason: 0x00000002`, `Parameters: 0x5802db7f0` with `[0] 0x10010`, a stack in `sqlpal.dll` under `lsasrv.dll` and `lsass.exe`, the SQLPAL's own LSA, and `Last errno: 11` (`Resource temporarily unavailable`).
- The runner had 4 processors and 16 GB.  At the time all the test projects ran at once on one runner; CI now runs the two SQL Server projects one after the other, on a runner of their own.
- `handle-crash.sh` captured a dump, `core.sqlservr.<date>.<pid>`, and moved it with what it captured into `/var/opt/mssql/log/core.sqlservr.<date>.<pid>.d/`.  The dump was lost with the container.

Of the 659 runs of `ci.yml` from 2026-10-22 to 2026-11-24, none failed on the crash.  How often the retry below fired in them is not known: it reported only in the job's summary, which GitHub gives no API to read.

[`SqlServerStart`](../../tests/DatabaseTests/TestHarness/SqlServerStart.cs) works around it.  The harness starts every SQL Server container through it, and when the container has exited before it was ready with the fatal error in its log, it saves the crash, disposes the container and starts a new one, once.  A second crash is saved too, and fails every test of the project.  Each crash is reported, with its `Crash Id`, where it was saved and its log from the fatal error on, as a warning in the summary of the job on GitHub and as an xunit diagnostic message.

## Why it exists

The crash is the server's own, at its start, before any statement of the tests reaches it, and it has not been seen on a developer's machine, so it could not be studied; a retry was the fix within reach.  The cause cannot be found without a dump of a crash on a runner, which no crash since has given.

## Impact

A crashed start costs the time of a second start, about half a minute.  The retry hides the crash from a run's result: only the warning in the job's summary and the artifact below show it.  Two crashes in a row still fail the run and need its failed jobs run again.

## Reading a crash

`SqlServerStart` saves each crash in a folder of its own, named by its `Crash Id` (or by the container's id when the log has none), under the folder that `SQLSOURCE_TESTS_SQLSERVER_CRASHES` names, or `sqlsource-sqlserver-crashes` in the system's temporary folder without it.  The folder holds `console.log`, the container's whole stdout and stderr, and `mssql-log.tar`, the archive of the container's `/var/opt/mssql/log`.  CI sets the variable to `artifacts/sqlserver-crashes` of the workspace, and each job that starts SQL Server containers uploads that folder, when it holds anything, as the artifact `sqlserver-crash-<job>`, kept 30 days.

```bash
gh api --paginate 'repos/mbcrawfo/SqlSource/actions/artifacts?name=sqlserver-crash-sqlserver&per_page=100' --jq '.artifacts[] | [.workflow_run.id, .created_at] | @tsv'
```

```bash
gh run download <run-id> --repo mbcrawfo/SqlSource --pattern 'sqlserver-crash-*'
```

The first lists the runs whose SQL Server leg of `build.yml` crashed, which counts the crashes for the trigger below; the floor job's artifact is `sqlserver-crash-floor`, and the nightly's are `sqlserver-crash-2019`, `-2022`, `-allocation` and `-sync-async`.  Extract `mssql-log.tar` with `tar -xf`: in `log/core.sqlservr.<date>.<pid>.d/`, `crash.txt` and `crash.json` hold the crash's report, `log/paldumper-debug.log` how the dump was taken, and the `.gdmp` file the dump itself; `log/errorlog` is the server's error log up to the crash.

## Proposed fix

Read the dump of the next crash.  If it shows a limit of the runner, such as the threads or memory that `Last errno: 11` suggests, set what the image needs on its containers in `SqlServerContainer`: its memory limit, `MSSQL_MEMORY_LIMIT_MB`, is the first thing to try.  If the image is at fault, report the crash to Microsoft with its `Crash Id`, `crash.json` and the dump, and move the default image to a build that fixes it.  Then remove `SqlServerStart` and this item.

## Trigger

A `sqlserver-crash-*` artifact in more than one run in ten, a run that fails after the retry, or a new default image of SQL Server.
