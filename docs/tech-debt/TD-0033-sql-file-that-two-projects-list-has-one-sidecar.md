# TD-0033 - A `.sql` file that two projects list has one sidecar and no owner

## Problem

Two projects can list one `.sql` file, each with a type that claims it: a shared folder that both include.  The file has one sidecar beside it, and each project's generator reads that one file.  [`RunPlanner`](../../src/SqlSource.Tool/Planning/RunPlanner.cs) plans such a file once, under the first project in the run's order that claims it, and three things follow:

- **The second project's dialect and database are not used.**  The file is parsed, hashed and given its database with the settings of the first project.  When the first project's dialect cannot be described and the second's can, the file is never described, and the second project's build reports each query that needs an entry as having none, `SQLSRC241`.  The generator does not read the database, so the second project's build cannot tell that the entries describe the first project's database.
- **A run that does not hold both projects sees the needs of one.**  A query needs an entry when any claim of any project of the run says so.  A run on one project, as the unit or by `--project`, does not read the other's claims, and may write a sidecar without the entries the other needs, or, in a run with no filter, delete a sidecar the other needs.  The other project's build then reports those entries as missing, `SQLSRC241`.
- **Each project's build checks the sidecar against its own needs.**  The generator of one project sees the claims of that project alone, so an entry that only the other project needs is an orphan to it: its build warns `SQLSRC245`.  A run over both projects writes the entry again, so the warning stays; a run over this project alone removes the entry, at the cost to the other project that the bullet above describes.

## Why it exists

The sidecar is beside the `.sql` file by the epic's decision, so that the generator pairs the two by path with no configuration.  A file with two owners was not designed for: the spec of sub-phase 2.4 plans it and records this.

## Impact

Low.  It takes a `.sql` file in two projects of one solution.  When both give it the same settings and the run is on the solution, the result is right.

## Proposed fix

Report a shared file whose projects resolve it to different dialects or databases as an error of its own, since no sidecar can serve both.  For the run on one project: have a sidecar record which projects' needs it was written for, or refuse to write or delete the sidecar of a file that a project outside the run also lists, which needs the solution's other manifests.

## Trigger

A user shares a `.sql` folder between two projects and reports a missing entry, or an orphan entry that comes back after `describe`.
