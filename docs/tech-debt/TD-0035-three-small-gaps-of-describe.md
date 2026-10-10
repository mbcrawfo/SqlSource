# TD-0035 - Three small gaps of `describe`

## Problem

1. **A database that exists only through a file that cannot be described counts as a selected database.**  [`RunDecisions.SelectedDatabases`](../../src/SqlSource.Tool/Describing/RunDecisions.cs) takes every database of a selected query that needs an entry, whether or not the plan could describe the query.  One postgres file and one file with `SQLSRC209`, which is an `ansi` file that needs types, with `SQLSOURCE_CONNECTION` set, give `SQLSRC215`, "this run has 2: ansi, postgres", with a help line to set `SQLSOURCE_CONNECTION_ANSI`.  A user who has one database to describe is told to give a connection to a second.  This follows the spec's definition of a selected database.
2. **Deletion does not look at what it deletes.**  [`FileOutcomes`](../../src/SqlSource.Tool/Describing/FileOutcomes.cs) decides `Delete` from the plan alone, and [`SidecarStore`](../../src/SqlSource.Tool/Describing/SidecarStore.cs) deletes whatever stands at `<name>.sql.json` without reading it.  That includes a sidecar of a higher format version, which `SQLSRC221` says is left alone.
3. **The connection variables reach every `dotnet msbuild` the tool starts.**  [`ProjectEvaluator`](../../src/SqlSource.Tool/Projects/ProjectEvaluator.cs) removes a fixed list of variables from the environment of a run and passes the rest, and MSBuild reads the environment as properties.  `SQLSOURCE_CONNECTION` and `SQLSOURCE_CONNECTION_*` therefore reach MSBuild, a project's targets and the build hooks of the project, so "to the describer and to nothing else" is not literally true.

## Why it exists

Each is a corner that the review of sub-phase 2.5 found after the code and its tests were written.  The first follows the rule in the spec, the second needed a read that the decision step avoids by design, and the third is the general rule of the tool for the environment: it changes what it must and passes the rest, so that a project's build sees what the user's own `dotnet build` sees.

## Impact

Low.  The first misleads a user about what to set, and only when a file of the project cannot be described.  The second removes the sidecar of a file that needs no entry when a newer tool wrote it, and git restores it when it was committed.  The third gives a secret to processes that the run started on the machine that holds the secret.

## Proposed fix

1. For the variable with no name, count only the databases with a query that the plan did not fail.
2. In `SidecarStore`, read the file before it is deleted, and leave one of a higher format version alone, with the `SQLSRC221` that a read gives.
3. Remove `SQLSOURCE_CONNECTION` and every `SQLSOURCE_CONNECTION_*` from the environment of the runs of `dotnet msbuild`, beside the variables that `ProjectEvaluator` removes now.

## Trigger

Phase 3, the first describer.
