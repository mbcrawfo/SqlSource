# TD-0029 - A run that is ended from outside leaves its MSBuild processes and its temporary folder

## Problem

The `sqlsource` tool starts `dotnet msbuild` for each project and keeps their manifests in a temporary folder of the run.  [`Cli.Run`](../../src/SqlSource.Tool/Cli.cs) listens for Ctrl+C alone and cancels the run through its token: [`ProcessRunner`](../../src/SqlSource.Tool/Processes/ProcessRunner.cs) then kills each process with what it started, and [`ProjectEvaluator`](../../src/SqlSource.Tool/Projects/ProjectEvaluator.cs) deletes the folder.

1. **`SIGTERM` is not listened for.**  It is what `timeout`, a cancelled CI job and an editor send.  The tool ends at once with the exit code 143, the `dotnet` processes it started go on to their end, and the folder `sqlsource-<id>` stays in the temporary directory.  The review of sub-phase 2.3 ran it.
2. **A kill that fails becomes `SQLSRC200`.**  `Process.Kill` with the whole tree can throw `Win32Exception` and `AggregateException`, and `ProcessRunner` catches `InvalidOperationException` alone.  Either would stand in for the cancellation, and a Ctrl+C would be reported as an unexpected failure.  This is from reading the code; nothing made a kill fail.

## Why it exists

Sub-phase 2.2 wired Ctrl+C when the tool owned no process and no file.  Sub-phase 2.3 gave it both, and its review found these after the code was written.

## Impact

Low.  An evaluation ends by itself within seconds, and the folder holds a few small files with paths and constants.  From sub-phase 2.5 a run also holds database connections, and what is left behind grows with it.

## Proposed fix

1. Register `SIGTERM` with `PosixSignalRegistration` in `Cli.Run` and cancel the same token, so that the run ends by its own road as it does for Ctrl+C.
2. Catch the other two exceptions of the kill in `ProcessRunner`, and let the cancellation through.

## Trigger

Sub-phase 2.5, which changed the same handler: a second Ctrl+C now ends the process, in [`Cli.Interrupt`](../../src/SqlSource.Tool/Cli.cs), and a run holds database connections.
