# TD-0036 - Small gaps of `--check` and the log

## Problem

1. **The log's `os` and `runtime` come from `RuntimeInformation`, not from `ToolHost`.**  [`Cli.RunAsync`](../../src/SqlSource.Tool/Cli.cs) writes `run.start` with `RuntimeInformation.OSDescription` and `RuntimeInformation.FrameworkDescription`.  The rule of the tool is that everything from outside comes through [`ToolHost`](../../src/SqlSource.Tool/ToolHost.cs), and a test of the log, `RunLogTests`, compares the two members with the machine's own values because it cannot fix them.
2. **`--check --database X` does not report an entry of a query that is gone when the entry's database is not one that `--database` names, though that entry by itself makes `describe --database X` rewrite the file.**  With `--database`, a sidecar that holds an entry for a query that was removed from the file, or whose output is now `sql`, is compared only for the entries of the named databases: [`SidecarCheck.EntriesWithoutAQuery`](../../src/SqlSource.Tool/Describing/SidecarCheck.cs) skips an entry of another database and one with no database.  [`FileOutcomes`](../../src/SqlSource.Tool/Describing/FileOutcomes.cs) and [`SidecarStore`](../../src/SqlSource.Tool/Describing/SidecarStore.cs) do not: every query of the file is skipped, the file's target has an entry for each query that needs one and none for a query that is gone, so the target's text differs from the file on the disk and the action is `Write`.  No other reason is needed.  So a file whose only fault is a stale entry of another database is called current by `--check --database X`, which exits `0`, and rewritten by `describe --database X`.  The claim of the epic and of the README of the tool, that `--check` reports what `describe` with the same filters would change, does not hold for that one entry.
3. **`run.end` is written before the log's file is closed.**  [`Cli.End`](../../src/SqlSource.Tool/Cli.cs) writes the event with the exit code and then disposes the log.  A failure of the close alone, when what was buffered cannot be written, is reported as `SQLSRC218` and makes the exit code `1`, but the line that is already in the file says the exit code of the run before that, so the log says exit code N for a process that exits `1`.

## Why it exists

The first is a value that the tool does not choose, so it was read where it is written and not given a member of the host.  The second is the line the plan drew: a gone query has no database in the plan, only the one its entry holds, and a run for one database was taken to say nothing of another database's entries.  The third is the order of a writer that must put the exit code in the file and learns of a failed close only by closing.

## Impact

Low.  The first makes the test of `run.start` compare with the machine it runs on and not with a value of its own.  The second leaves a stale entry of another database in a sidecar that `--check --database X` calls current, so the check can exit `0` for a file that `describe --database X` changes; a check with no `--database` reports it, and the entry is dropped by the next `describe` that writes the file.  The third matters only when the close alone fails, and then the log disagrees with the exit code of the process.

## Proposed fix

1. Add the two descriptions to `ToolHost`, as members that `ToolHost.Create` fills from `RuntimeInformation`, and let the test of the log give its own.
2. Either report the entry when `--check` has a filter and the file is one that `describe` would write, or state the bound in the epic and in the README of the tool, and take the claim out of both.
3. Write `run.end`, flush the stream, and close it, and when the close fails write nothing more but report it; or say in the documentation of `run.end` that its exit code is the run's before the file is closed.

## Trigger

The first: phase 10's bundle, which reads the log and may want the fields to be fixed by a test.  The second and the third: a report from a user.
