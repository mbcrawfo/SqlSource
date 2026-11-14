# TD-0115 - Tool code its own tests do not check

## Problem

The test review of 2026-11-13 ran a mutation test of every scope of `src/SqlSource.Tool`, each part of the source with the tests of its own folder.  1,881 of its mutants belong to no test: a survivor that no test of its scope fails on, or a mutant that no test of its scope runs, less the survivors that a finding of the review asks a test to kill.  The appendix of the review, [docs/superpowers/specs/2026-11-13-test-cleanup-findings.md](../superpowers/specs/2026-11-13-test-cleanup-findings.md#gaps-that-belong-to-no-test), counts them by file.  The files with the most:

| File | Survived | No coverage | Gap |
|----|----|----|----|
| [`Watching/WatchSession.cs`](../../src/SqlSource.Tool/Watching/WatchSession.cs) | 0 | 173 | 173 |
| [`Describing/SqlServer/SqlServerLive.cs`](../../src/SqlSource.Tool/Describing/SqlServer/SqlServerLive.cs) | 0 | 102 | 102 |
| [`Bundles/DiagnoseRun.cs`](../../src/SqlSource.Tool/Bundles/DiagnoseRun.cs) | 0 | 96 | 96 |
| [`Describing/Postgres/Walk/Expressions/TotalFunctions.cs`](../../src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/TotalFunctions.cs) | 78 | 1 | 79 |
| [`Describing/Postgres/PostgresLive.cs`](../../src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs) | 1 | 77 | 78 |
| [`Describing/Postgres/Walk/Expressions/PlanExpression.cs`](../../src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanExpression.cs) | 63 | 8 | 70 |
| [`Describing/DescribeRun.cs`](../../src/SqlSource.Tool/Describing/DescribeRun.cs) | 0 | 65 | 65 |
| [`Logging/RunEvents.cs`](../../src/SqlSource.Tool/Logging/RunEvents.cs) | 44 | 10 | 54 |
| [`Describing/SqlServer/Proofs/ExpressionProofs.Calls.cs`](../../src/SqlSource.Tool/Describing/SqlServer/Proofs/ExpressionProofs.Calls.cs) | 46 | 1 | 46 |
| [`Describing/SidecarCheck.cs`](../../src/SqlSource.Tool/Describing/SidecarCheck.cs) | 0 | 43 | 43 |

`PlanExpression.cs` and `ExpressionProofs.Calls.cs` each have one survivor that a finding of the review names, so their gap is one less than the sum.

## Why it exists

A scope sees only its own source and the tests of its own folder, so code that only the command tests of `tests/SqlSource.Tool.Tests/Commands/` or the database projects reach, as a watch session, a diagnose run or a live describer is, shows as unchecked in the scope of its folder.  The review proposes no new test but the unit tests a demote needs, so these gaps are recorded rather than closed.

## Impact

Low today: much of this code is run by the command tests or by the database projects, which the scope of its folder does not see, so a gap here is not proof that nothing checks it.  It risks a change that the folder's own tests let through, and a failure found only by a slow command test or a container run, where a unit test would have named the fault.

## Proposed fix

For each file of the table, from the most: run its folder's scope with the command tests added to its filter, `tools/mutation-test.sh tool --tests '<the scope's filter>|FullyQualifiedName~SqlSource.Tool.Tests.Commands.' '<the file>'`, to see which gaps the command tests already close; a file that only a database project reaches is judged by reading.  Write a unit test in the folder's tests for the behaviour each remaining survivor or uncovered branch breaks, or record why a command or database test is the right guard.  Rewrite the table of this item, or resolve it, as the gaps close.

## Trigger

A change to one of the files of the table, or the next run of the test review, which updates this item.
