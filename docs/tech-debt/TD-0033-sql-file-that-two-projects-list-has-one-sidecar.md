# TD-0033 - A `.sql` file that two projects list has one sidecar and no owner

## Problem

Two projects can list one `.sql` file, each with a type that claims it: a shared folder that both include.  The file has one sidecar beside it, and each project's generator reads that one file.  [`RunPlanner`](../../src/SqlSource.Tool/Planning/RunPlanner.cs) plans such a file once, under the first project in the run's order that claims it, and a query of it needs an entry when it does for any project that claims the file, each by its own chain of settings files.  Every setting of a query, its output included, comes from its markers and the `sqlsource.json` files of its own folder and the folders above it, which each project's build finds alike, so the projects give the file one output and need the same entries.  They differ only when one project misses a settings file that the other finds.  Two ways are known: a project whose own late target adds the file, as in [TD-0016](TD-0016-settings-file-beside-a-late-targets-file-is-missed.md), and a project that lists a settings file named in another case, `SqlSource.json`, itself, which `ListedSettingsFile.IsSettingsFile` then does not take, since it compares the name ordinally, while a project that does not list it gets it as `sqlsource.json` from the search on macOS and Windows.  Then a query can need an entry for one project and not for the other.

A solution run with `--project` keeps what the projects it leaves out need: [`RunProjects.FindAsync`](../../src/SqlSource.Tool/Projects/RunProjects.cs) evaluates them too, and the planner takes their needs of the files the run claims, without selecting the queries that only they need.  Three things are left:

- **A run that knows no other project sees the needs of its own.**  A run on a project alone, as the unit, and the build's own run, from `--manifest`, know no solution, and may write a sidecar without the entries the other project needs, or, with no filter, delete a sidecar it needs.  The other project's build then reports those entries as missing, `SQLSRC241`.  So may a solution run with `--project` when a project it leaves out cannot be evaluated, not restored or failing, which it passes over without a word.  A watch with `--project` reads the left-out projects again only when it evaluates again, after a change to a project of the run, so a change to a left-out project's claims or settings files waits for that.
- **Each project's build checks the sidecar against its own needs.**  The generator of one project sees its own settings files alone, so an entry that only the other project needs is an orphan to it: its build warns `SQLSRC245`.  A run over both projects, or with `--project`, keeps the entry, so the warning stays.

## Why it exists

The sidecar is beside the `.sql` file by the epic's decision, so that the generator pairs the two by path with no configuration.  A file with two owners was not designed for: the spec of sub-phase 2.4 plans it and records this.  The owner chose to read the other projects only where a run knows them, in a solution with `--project`: finding a solution for a project alone, or for the build of one, would evaluate every sibling project with MSBuild in each run.

## Impact

Low.  It takes a `.sql` file in two projects of one solution, and a settings file that one of the two projects misses for it, through TD-0016's late target or a settings file in another case that one project lists itself, and then a run on one of the projects alone or by its build.  When both projects find the same settings files, or the run is on the solution, with `--project` or without, the result is right.

## Proposed fix

Have a sidecar record which projects' needs it was written for, so that a run on one project keeps the entries of the others and each project's build checks only its own: a change to the sidecar's format, which needs the owner's decision.

## Trigger

A user shares a `.sql` folder between two projects that find different settings files for it, and reports a missing entry after a run on one project or its build, or an orphan entry that comes back after `describe`.
