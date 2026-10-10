# TD-0035 - Three small gaps of `describe`

## Problem

1. **A database that exists only through a file that cannot be described counts as a selected database.**  [`RunDecisions.SelectedDatabases`](../../src/SqlSource.Tool/Describing/RunDecisions.cs) takes every database of a selected query that needs an entry, whether or not the plan could describe the query.  One postgres file and one file with `SQLSRC209`, which is an `ansi` file that needs types, with `SQLSOURCE_CONNECTION` set, give `SQLSRC215`, "this run has 2: ansi, postgres", with a help line to set `SQLSOURCE_CONNECTION_ANSI`.  A user who has one database to describe is told to give a connection to a second.  This follows the spec's definition of a selected database.
2. **The connection variables reach every `dotnet msbuild` the tool starts.**  [`ProjectEvaluator`](../../src/SqlSource.Tool/Projects/ProjectEvaluator.cs) removes a fixed list of variables from the environment of a run and passes the rest, and MSBuild reads the environment as properties.  `SQLSOURCE_CONNECTION` and `SQLSOURCE_CONNECTION_*` therefore reach MSBuild, a project's targets and the build hooks of the project, so "to the describer and to nothing else" is not literally true.
3. **A sidecar that the tool cannot open is neither deleted nor mentioned.**  When a file has no query that needs an entry, a run with no filter and no error deletes its sidecar, after [`FileOutcomes`](../../src/SqlSource.Tool/Describing/FileOutcomes.cs) has read it to leave one of a higher format version alone.  A sidecar that the system will not let the tool open, for lack of permission, because it is locked or because it is a directory, cannot be read, so it is left as it is with nothing said.  Before that check it was deleted, or reported as `SQLSRC218` when the delete failed.  The stale file stays, and every later run leaves it again.

## Why it exists

Each is a corner that the review of sub-phase 2.5 found after the code and its tests were written.  The first follows the rule in the spec, the second is the general rule of the tool for the environment: it changes what it must and passes the rest, so that a project's build sees what the user's own `dotnet build` sees.  The third came with the check that keeps a sidecar of a newer tool: a file that cannot be opened cannot be told from one of a newer format, so it is left.

## Impact

Low.  The first misleads a user about what to set, and only when a file of the project cannot be described.  The second gives a secret to processes that the run started on the machine that holds the secret.  The third leaves a file in the project that the run was meant to remove, with no word of why, and only where the file system already refuses the tool access to it.

## Proposed fix

1. For the variable with no name, count only the databases with a query that the plan did not fail.
2. Remove `SQLSOURCE_CONNECTION` and every `SQLSOURCE_CONNECTION_*` from the environment of the runs of `dotnet msbuild`, beside the variables that `ProjectEvaluator` removes now.
3. Have `SidecarOnDisk` tell a file that could not be opened from one that is absent, and report the first under `SQLSRC218`, with the reason the system gave, when the file beside it needs no entry, so that the user is told which file stays.

## Trigger

Phase 3, the first describer.
