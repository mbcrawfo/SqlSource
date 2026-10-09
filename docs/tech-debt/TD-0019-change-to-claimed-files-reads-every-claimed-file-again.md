# TD-0019 - A change to the set of claimed files reads every claimed file again

## Problem

In [`SqlSourceGenerator`](../../src/SqlSource/SqlSourceGenerator.cs), the step `ClaimedPaths` is the sorted set of the files that any type claims, and it is an input of the read of every file: each file's `FileParseInput` is combined with it, to decide whether the file is claimed and so whether to read it.  When the set changes, the combined value of every file is new, and `SqlFileReader.Read` runs for every claimed file again, even though only one file entered or left the set.

The set changes when a file is added to the folder of a type that claims a folder, when a `Path` of a type is edited, and when a type is added or removed.  An edit to the text of a `.sql` file does not change it.

This is not [TD-0013](TD-0013-sql-edit-reads-every-attributed-type-again.md): that item is the cost of reading the attributed types again on each edit of a `.sql` file, because the attribute is added in a post-initialization step.  This one is the cost of an edit that changes which files are claimed, and it would remain if the attribute were shipped in an assembly.

## Why it exists

A generator cannot ask whether one value is in a set without taking the whole set as an input.  The step was written to keep a file that no type claims from being read at all (its errors are never reported, and a project can hold migration scripts), and the set was the simplest input that decides that.  It predates the settings work, which added more steps that combine with it: the check of invalid settings and the case-collision report.

## Impact

Low.  Adding or removing a claimed file, or changing a `Path`, parses all the claimed files of the project once, and an ordinary edit of a query does not.  A project with thousands of `.sql` files pays that parse once for each such change, in the compiler's process.

## Proposed fix

Make the answer to "is this file claimed" a value of its own for each file, so that a file whose answer did not change is not read again: for example, join the claimed set to each file in a step that outputs the file's `FileParseInput` and a boolean, with the boolean compared by value, and read the file from the pair; or read every file in a step that does not depend on the set and filter after it, at the price of reading the files nobody claims.  Add a case to [`CachingTests`](../../tests/SqlSource.Tests/Generator/CachingTests.cs) for a file added to a claimed folder.

## Trigger

A report of a slow compiler or IDE when files are added to a project with many `.sql` files.
