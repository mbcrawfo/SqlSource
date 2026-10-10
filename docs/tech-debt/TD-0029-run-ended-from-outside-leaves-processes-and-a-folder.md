# TD-0029 - A run that is ended from outside leaves its MSBuild processes, its temporary folder and a temporary sidecar

## Problem

The `sqlsource` tool starts `dotnet msbuild` for each project and keeps their manifests in a temporary folder of the run.  [`Cli.Run`](../../src/SqlSource.Tool/Cli.cs) listens for Ctrl+C alone and cancels the run through its token: [`ProcessRunner`](../../src/SqlSource.Tool/Processes/ProcessRunner.cs) then kills each process with what it started, and [`ProjectEvaluator`](../../src/SqlSource.Tool/Projects/ProjectEvaluator.cs) deletes the folder.

1. **`SIGTERM` is not listened for.**  It is what `timeout`, a cancelled CI job and an editor send.  The tool ends at once with the exit code 143, the `dotnet` processes it started go on to their end, and the folder `sqlsource-<id>` stays in the temporary directory.  The review of sub-phase 2.3 ran it.
2. **A kill that fails becomes `SQLSRC200`.**  `Process.Kill` with the whole tree can throw `Win32Exception` and `AggregateException`, and `ProcessRunner` catches `InvalidOperationException` alone.  Either would stand in for the cancellation, and a Ctrl+C would be reported as an unexpected failure.  This is from reading the code; nothing made a kill fail.
3. **A run killed while a sidecar is written leaves a temporary file.**  [`SidecarStore`](../../src/SqlSource.Tool/Describing/SidecarStore.cs) writes `<name>.sql.json.<guid>.tmp` beside the `.sql` file and moves it over the sidecar.  A run that is killed between the two, by `SIGTERM` or by a second Ctrl+C, leaves the `.tmp` file in the project, and nothing removes it.

## Why it exists

Sub-phase 2.2 wired Ctrl+C when the tool owned no process and no file.  Sub-phase 2.3 gave it both, and its review found the first two after the code was written.  Sub-phase 2.5 wrote the sidecars and added the third.  The released tool has no describer, so a run still holds no database connection.

## Impact

Low.  An evaluation ends by itself within seconds, and the folder holds a few small files with paths and constants.  From phase 3, when the first describer is registered, a run also holds database connections, and what is left behind grows with it.  A `.tmp` file beside a `.sql` file can be committed by mistake.

## Proposed fix

1. Register `SIGTERM` with `PosixSignalRegistration` in `Cli.Run` and cancel the same token, so that the run ends by its own road as it does for Ctrl+C.
2. Catch the other two exceptions of the kill in `ProcessRunner`, and let the cancellation through.
3. Have `SidecarStore` remove its `.tmp` file when a run ends by `SIGTERM`, and have a run delete the `<name>.sql.json.<guid>.tmp` files that an earlier run left beside the `.sql` files it reads.

## Trigger

Phase 3, the first describer, when a run first holds a database connection.  Sub-phase 2.5 changed the same handler: a second Ctrl+C now ends the process, in [`Cli.Interrupt`](../../src/SqlSource.Tool/Cli.cs).
