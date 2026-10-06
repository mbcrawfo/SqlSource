# TD-0007 - Two `.sql` files whose paths differ only by case are treated as one

## Problem

The generator compares paths ignoring case, so that a project builds the same on every operating system; see [`SqlPath`](../../src/SqlSource/Generation/SqlPath.cs).  On a case-sensitive file system a folder can hold both `Users.sql` and `users.sql`.  [`SqlSourceGenerator`](../../src/SqlSource/SqlSourceGenerator.cs) removes duplicates from the list of paths with the same comparer, so the two count as one file.  Both are read and parsed, and the result of the second is dropped.  Nothing is reported.

## Why it exists

Removing duplicates is needed: a project that lists a `.sql` file as an `AdditionalFiles` item, as well as getting it from the package's default, hands the generator the same file twice.  The generator cannot tell that case from two real files, because it may not ask the file system.

## Impact

The queries of the second file are silently missing.  It needs a case-sensitive file system and two file names that differ only by case in one folder, which is rare and already breaks a checkout on Windows and macOS.

## Proposed fix

Remove duplicates with an ordinal comparison, so that two paths that differ by case are two files.  Then report an error, located at the second file, when two files of one type have paths that are equal ignoring case.  That needs a new diagnostic.

## Trigger

A user reports missing queries on Linux.  Path handling in `SqlPath` is changed for another reason.
