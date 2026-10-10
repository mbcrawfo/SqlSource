# TD-0030 - A path of `--project` that goes through a symbolic link is not found

## Problem

[`RunProjects`](../../src/SqlSource.Tool/Projects/RunProjects.cs) compares a path that `--project` names with the projects of the unit as full paths, as text.  The unit is found from the working directory as the system gives it, which is the real folder.  A path that the user gives in full, through a symbolic link to that folder, keeps the spelling of the link:

```console
$ cd /work/link          # a link to /work/real
$ dotnet sqlsource describe --project "$PWD/App/App.csproj"
sqlsource : error SQLSRC207: '/work/link/App/App.csproj' is not a project of '/work/real/App.slnx'
```

The review of sub-phase 2.3 ran it.  A relative path is found, and so is a full path when the unit is given by the same spelling.

## Why it exists

The spec of the sub-phase says that paths are compared as full paths, ignoring case.  No path of the tool is resolved through links, the unit's included.

## Impact

Low.  It takes a working directory behind a link and a full path on the command line.  The message holds both spellings, so the cause can be seen.

## Proposed fix

Resolve links on both sides before the comparison, with `File.ResolveLinkTarget` for the file and each folder above it, or by comparing the two files' identities.  Do the same in sub-phase 2.4, which compares the path of a `.sql` filter in the same way.

## Trigger

A user reports `SQLSRC207` for a project that the solution lists.  Or sub-phase 2.4, when the `.sql` filter meets the same comparison.
