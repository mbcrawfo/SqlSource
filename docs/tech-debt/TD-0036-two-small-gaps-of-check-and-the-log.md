# TD-0036 - Two small gaps of `--check` and the log

## Problem

1. **The log's `os` and `runtime` come from `RuntimeInformation`, not from `ToolHost`.**  [`Cli.RunAsync`](../../src/SqlSource.Tool/Cli.cs) writes `run.start` with `RuntimeInformation.OSDescription` and `RuntimeInformation.FrameworkDescription`.  The rule of the tool is that everything from outside comes through [`ToolHost`](../../src/SqlSource.Tool/ToolHost.cs), and a test of the log, `RunLogTests`, compares the two members with the machine's own values because it cannot fix them.
2. **`--check` does not report an entry of a query that is gone when the entry's database is not one that `--database` names, though `describe` with the same filter drops it.**  With `--database`, a sidecar that holds an entry for a query that was removed from the file, or whose output is now `sql`, is compared only for the entries of the named databases: [`SidecarCheck.EntriesWithoutAQuery`](../../src/SqlSource.Tool/Describing/SidecarCheck.cs) skips an entry of another database and one with no database.  [`SidecarStore`](../../src/SqlSource.Tool/Describing/SidecarStore.cs) writes a target that holds an entry for each query that needs one and none for the others, so a `describe` that writes the file for another reason, a query of the named database that changed, drops the entry too.  The claim of the epic, that `--check` reports what `describe` with the same filters would change, does not hold for that one entry.

## Why it exists

The first is a value that the tool does not choose, so it was read where it is written and not given a member of the host.  The second is the line the plan drew: a gone query has no database in the plan, only the one its entry holds, and a run for one database was taken to say nothing of another database's entries.

## Impact

Low.  The first makes the test of `run.start` compare with the machine it runs on and not with a value of its own.  The second leaves a stale entry of another database in a sidecar that `--check` calls current; a check with no `--database` reports it, and the entry is dropped by the next `describe` that writes the file.

## Proposed fix

1. Add the two descriptions to `ToolHost`, as members that `ToolHost.Create` fills from `RuntimeInformation`, and let the test of the log give its own.
2. Either report the entry when `--check` has a filter and the file is one that `describe` would write, or state the bound in the epic and in the README of the tool, and take the claim out of both.

## Trigger

The first: phase 10's bundle, which reads the log and may want the fields to be fixed by a test.  The second: a report from a user.
