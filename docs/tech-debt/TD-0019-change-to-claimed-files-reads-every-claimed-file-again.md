# TD-0019 - A change to the set of claimed files reads every claimed file again, and checks again the sidecars that move

## Problem

In [`SqlSourceGenerator`](../../src/SqlSource/SqlSourceGenerator.cs), the step `ClaimedPaths` is the sorted set of the files that any type claims, and it is an input of the read of every file: each file's `FileParseInput` is combined with it, to decide whether the file is claimed and so whether to read it.  When the set changes, the combined value of every file is new, and `SqlFileReader.Read` runs for every claimed file again, even though only one file entered or left the set.

The set changes when a file is added to the folder of a type that claims a folder, when a `Path` of a type is edited, and when a type is added or removed.  An edit to the text of a `.sql` file does not change it.

The same change checks the sidecars again.  The step `SidecarCheck` runs once for each claimed file, from `ParsedFiles` taken apart by a `SelectMany`, and the compiler matches the nodes a `SelectMany` gives by their place in the list, not by the file's path.  When a claimed file is added or removed, every claimed file sorted after it moves to another place, and its node's input, a `SidecarCheckInput`, is another file's.  When one is renamed, the files sorted between its old path and its new one move, and the others keep their place.  So `SidecarChecker.Check` runs again for every claimed file that moved, and its diagnostics are reported again, though nothing about those files changed.  A file that keeps its place is not checked again.  The sidecars themselves are not read again: `SidecarInput` and `SidecarFile` are steps of their own for each sidecar.  [`CachingTests`](../../tests/SqlSource.Tests/Generator/CachingTests.cs) holds this today in `Run_ClaimedSqlFileAdded_ChecksTheFilesSortedAfterIt`.

This is not [TD-0013](TD-0013-sql-edit-reads-every-attributed-type-again.md): that item is the cost of reading the attributed types again on each edit of a `.sql` file, because the attribute is added in a post-initialization step.  This one is the cost of an edit that changes which files are claimed, and it would remain if the attribute were shipped in an assembly.

## Why it exists

A generator cannot ask whether one value is in a set without taking the whole set as an input.  The step was written to keep a file that no type claims from being read at all (its errors are never reported, and a project can hold migration scripts), and the set was the simplest input that decides that.  It predates the settings work, which added more steps that combine with it: the check of invalid settings and the case-collision report.

The checks of the sidecars start from `ParsedFiles` because it is the one list that holds each claimed file once, sorted and parsed, so that a file the project lists twice is checked, and reported, once.

## Impact

Low.  Adding or removing a claimed file, or changing a `Path`, parses all the claimed files of the project once, and an ordinary edit of a query does not.  A project with thousands of `.sql` files pays that parse once for each such change, in the compiler's process.

The checks again cost, for each claimed file that moved, the hash of each of its queries that needs an entry, a search of its sidecar for each, and the report of its diagnostics.  On average that is half of the claimed files for each file added or removed, and the files between the two names for each file renamed, a few when a rename keeps a file near its old place in the order.  A project at `Sql` with no sidecars pays nothing, since a file with no sidecar and no needs is done at once.

## Proposed fix

Make the answer to "is this file claimed" a value of its own for each file, so that a file whose answer did not change is not read again: for example, join the claimed set to each file in a step that outputs the file's `FileParseInput` and a boolean, with the boolean compared by value, and read the file from the pair; or read every file in a step that does not depend on the set and filter after it, at the price of reading the files nobody claims.  Add a case to [`CachingTests`](../../tests/SqlSource.Tests/Generator/CachingTests.cs) for a file added to a claimed folder.

For the checks, give each file's check a node that stays with its path: start it from the `ParsedFile` step, whose nodes follow the compiler's list of additional texts, in which an added file does not move the others, rather than from the sorted list taken apart.  Check with a `CachingTests` case that a removed file moves no other file's node either, or key the nodes on the path some other way.  Combine each parsed file with `FileNeeds` and `Sidecars` there, as `SidecarCheckInput.Gather` does today.  A file the project lists twice then has two nodes, so the check must give nothing for every item of a path but one, for example by giving null for an item that is not the one `ParsedFiles` kept for its path and filtering the nulls out, so that its problems are still reported once.  Then change `Run_ClaimedSqlFileAdded_ChecksTheFilesSortedAfterIt` to expect only the added file to be checked.

## Trigger

A report of a slow compiler or IDE when files are added to a project with many `.sql` files or with many sidecars.
