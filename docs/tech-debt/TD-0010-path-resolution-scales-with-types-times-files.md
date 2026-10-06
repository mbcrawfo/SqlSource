# TD-0010 - Resolving paths costs time and memory in proportion to types times files

## Problem

[`PathResolver.FindFiles`](../../src/SqlSource/Generation/PathResolver.cs) walks every `.sql` path of the project for every attributed type, and calls `SqlPath.GetFolder` on each, which allocates a substring for each pair.  `SqlSourceGenerator.SelectFiles` walks every parsed file for every type whenever a `.sql` file is edited.

Measured in the review of phase 2, on a `Release` build:

| Types | Claimed files | Unclaimed files | First run | One `.sql` edit |
|----|----|----|----|----|
| 20 | 20 | 0 | 1.0 ms, 1.7 MB | 0.4 ms |
| 200 | 1,000 | 0 | 103 ms, 92 MB | 9.5 ms |
| 200 | 1,000 | 3,000 | 159 ms, 202 MB | 17.6 ms |
| 1,000 | 3,000 | 0 | 371 ms, 706 MB | 98 ms |

The 110 MB that 3,000 unclaimed migration scripts add in the third row is the substring for each pair: the files are never read, and still cost memory.

## Why it exists

The straightforward form was written first, and the plan's measurement used 20 types with one file each, which cannot show a cost that grows with the product.

## Impact

None for a project of ordinary size.  A project with hundreds of attributed types and thousands of `.sql` files pays on the first run in a session and whenever the list of files changes, in the IDE as well as in a build.  `src/SqlSource/AGENTS.md` makes allocation in the generator a requirement.

## Proposed fix

1. Compare a path's folder with the target in place, without a substring: the path is in the folder when it starts with the folder, ignoring case, a separator follows, and no other separator comes after it.
2. The list of paths is sorted, so the files of one folder are adjacent.  Find the first with a binary search and stop at the first that is not in the folder.
3. In `SelectFiles`, find each of the type's files with a binary search over the parsed files.
4. Add a measurement with many types and files to the pull request that makes the change, as the rule on hot paths requires.

## Trigger

A user reports a slow or memory-hungry build or IDE in a large project.  `PathResolver` is changed for another reason.
