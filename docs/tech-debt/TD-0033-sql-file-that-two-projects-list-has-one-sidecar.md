# TD-0033 - A `.sql` file that two projects list has one sidecar and no owner

## Problem

Two projects can list one `.sql` file, each with a type that claims it: a shared folder that both include.  The file has one sidecar beside it, and each project's generator will read that one file.  [`RunPlanner`](../../src/SqlSource.Tool/Planning/RunPlanner.cs) plans such a file once, under the first project in the run's order that claims it, and two things follow:

- **The second project's dialect and database are not used.**  The file is parsed, hashed and given its database with the settings of the first project.  A second project that gives the file another dialect gets a sidecar whose hashes, from phase 5, its build calls stale.
- **A run that does not hold both projects sees the needs of one.**  A query needs an entry when any claim of any project of the run says so.  A run on one project, as the unit or by `--project`, does not read the other's claims, and may write a sidecar without the entries the other needs, or, in a run with no filter, delete a sidecar the other needs.

## Why it exists

The sidecar is beside the `.sql` file by the epic's decision, so that the generator pairs the two by path with no configuration.  A file with two owners was not designed for: the spec of sub-phase 2.4 plans it and records this.

## Impact

Low.  It takes a `.sql` file in two projects of one solution.  When both give it the same settings and the run is on the solution, the result is right.

## Proposed fix

Report a shared file whose projects resolve it to different dialects or databases as an error of its own, since no sidecar can serve both.  For the run on one project: have a sidecar record which projects' needs it was written for, or refuse to write or delete the sidecar of a file that a project outside the run also lists, which needs the solution's other manifests.

## Trigger

A user shares a `.sql` folder between two projects and reports a stale or missing entry.  Or phase 5, when the generator reports both.
