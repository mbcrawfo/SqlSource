# TD-0068 - A `.sql` path that climbs above the root is ignored

## Problem

[`RunFilters.Create`](../../src/SqlSource.Tool/Planning/RunFilters.cs) leaves out a `.sql` path of the command line that `SqlPath.Normalize` cannot place.  On Linux and macOS `Path.GetFullPath` keeps a backslash as part of a name, and `Normalize` reads it as a separator, so a path such as `a\..\..\..\q.sql` whose `..` climb above the root of the file system has no normalised path.  The path then selects nothing and is reported nowhere: [`RunPlanner.Select`](../../src/SqlSource.Tool/Planning/RunPlanner.cs) sees no file filter, so with no other filter every query of the run is described, and beside another `.sql` path it is as if it had not been given.  `RunFilters.NamesFiles` still counts it as a filter, so the run deletes no sidecar.

## Why it exists

The behaviour is as old as the `.sql` filters.  TD-0063's refactor kept it, since a fix changes what a user sees, and made sure that such a path still keeps the run from deleting a sidecar.

## Impact

Low.  Only a path written with backslashes that climbs above the root, on a system whose separator is `/`, reaches it.  The run describes more than was asked and deletes nothing.

## Proposed fix

Keep such a path as a `FileFilter` with no normalised path, report it as `SQLSRC212`, as a path that names no planned file is reported, and let it select no file.  Add a case beside `Run_SqlPathThatNamesNoFile_KeepsTheSidecarOfAFileThatNeedsNoEntry` in `tests/SqlSource.Tool.Tests/Commands/FilterRunTests.cs`, and remove the known issue from `docs/manual/tool.md`.

## Trigger

The next change to `RunFilters` or to how the tool reads a `.sql` path.
