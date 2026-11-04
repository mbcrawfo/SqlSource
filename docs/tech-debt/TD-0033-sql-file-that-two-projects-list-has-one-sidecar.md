# TD-0033 - A `.sql` file that two projects list has one sidecar and no owner

## Problem

Two projects can list one `.sql` file, each with a type that claims it: a shared folder that both include.  The file has one sidecar beside it, and each project's generator reads that one file.  [`RunPlanner`](../../src/SqlSource.Tool/Planning/RunPlanner.cs) plans such a file once, under the first project in the run's order that claims it, and a query of it needs an entry when it does for any project of the run that claims the file, each by its own chain of settings files.  Every setting of a query, its output included, comes from its markers and the `sqlsource.json` files of its own folder and the folders above it, which each project's build finds alike, so the projects give the file one output and need the same entries.  They differ only when one project misses a settings file that the other finds, as a project whose own late target adds the file does in [TD-0016](TD-0016-settings-file-beside-a-late-targets-file-is-missed.md).  Then a query can need an entry for one project and not for the other, and two things follow:

- **A run that does not hold both projects sees the needs of one.**  A run on one project, as the unit or by `--project`, does not read the other's settings files, and may write a sidecar without the entries the other needs, or, in a run with no filter, delete a sidecar the other needs.  The other project's build then reports those entries as missing, `SQLSRC241`.
- **Each project's build checks the sidecar against its own needs.**  The generator of one project sees its own settings files alone, so an entry that only the other project needs is an orphan to it: its build warns `SQLSRC245`.  A run over both projects writes the entry again, so the warning stays; a run over this project alone removes the entry, at the cost to the other project that the bullet above describes.

## Why it exists

The sidecar is beside the `.sql` file by the epic's decision, so that the generator pairs the two by path with no configuration.  A file with two owners was not designed for: the spec of sub-phase 2.4 plans it and records this.

## Impact

Low.  It takes a `.sql` file in two projects of one solution, and a settings file that one of the two projects misses for it, which TD-0016's late target is the known way to cause.  When both projects find the same settings files and the run is on the solution, the result is right.

## Proposed fix

Have a sidecar record which projects' needs it was written for, so that a run on one project keeps the entries of the others, or refuse to write or delete the sidecar of a file that a project outside the run also lists, which needs the solution's other manifests.

## Trigger

A user shares a `.sql` folder between two projects, one of which adds files from a late target, and reports a missing entry, or an orphan entry that comes back after `describe`.  Fixing TD-0016 leaves this item no case.
