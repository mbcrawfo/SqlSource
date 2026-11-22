# TD-0115 - Tool code its own tests do not check

## Problem

The test review of 2026-11-13 ran a mutation test of every scope of `src/SqlSource.Tool`, each part of the source with the tests of its own folder.  1,881 of its mutants belong to no test: a survivor that no test of its scope fails on, or a mutant that no test of its scope runs, less the survivors that a finding of the review asks a test to kill.  The appendix of the review, [docs/superpowers/specs/2026-11-13-test-cleanup-findings.md](../superpowers/specs/2026-11-13-test-cleanup-findings.md#gaps-that-belong-to-no-test), counts them by file.  The 262 of `TotalFunctions.cs`, the four `PlanExpression*.cs` and `ExpressionProofs.Calls.cs` are settled, as the next section says, which leaves 1,619, of which 234 are in the other files of `Describing/Postgres/Walk` and `Describing/SqlServer/Proofs`.  The files with the most:

| File | Survived | No coverage | Gap |
|----|----|----|----|
| [`Watching/WatchSession.cs`](../../src/SqlSource.Tool/Watching/WatchSession.cs) | 0 | 173 | 173 |
| [`Describing/SqlServer/SqlServerLive.cs`](../../src/SqlSource.Tool/Describing/SqlServer/SqlServerLive.cs) | 0 | 102 | 102 |
| [`Bundles/DiagnoseRun.cs`](../../src/SqlSource.Tool/Bundles/DiagnoseRun.cs) | 0 | 96 | 96 |
| [`Describing/Postgres/PostgresLive.cs`](../../src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs) | 1 | 77 | 78 |
| [`Describing/DescribeRun.cs`](../../src/SqlSource.Tool/Describing/DescribeRun.cs) | 0 | 65 | 65 |
| [`Logging/RunEvents.cs`](../../src/SqlSource.Tool/Logging/RunEvents.cs) | 44 | 10 | 54 |
| [`Describing/SidecarCheck.cs`](../../src/SqlSource.Tool/Describing/SidecarCheck.cs) | 0 | 43 | 43 |

## Six files of the nullability proofs

[`TotalFunctions.cs`](../../src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/TotalFunctions.cs), the four `PlanExpression*.cs` beside it and [`ExpressionProofs.Calls.cs`](../../src/SqlSource.Tool/Describing/SqlServer/Proofs/ExpressionProofs.Calls.cs) have no gap that could answer a nullable column not null.  A run of their folders' tests on 2026-11-22 leaves 184 mutants undetected, each read:

- **79 are in `TotalFunctions.cs`**, in the code that builds its table once, in a static initialiser, where a mutation run cannot switch them on.  `Catalog_TotalFunctions_ArePgCatalogsOwn` in [`CatalogPinTests.cs`](../../tests/DatabaseTests/Postgres/Describer/CatalogPinTests.cs) is their guard, since only a server's catalog can say what a row must hold: it failed on each of seven such changes made by hand, to a name, a result, a C function, a row left out and the mixed integers.
- **105 are in the other five files**, and change no answer, make the reader prove less, or change only which name is asked of the server.  Those in the operand of a test are among them, since `IS NULL` is never `NULL` whatever its operand.

## Why it exists

A scope sees only its own source and the tests of its own folder, so code that only the command tests of `tests/SqlSource.Tool.Tests/Commands/` or the database projects reach, as a watch session, a diagnose run or a live describer is, shows as unchecked in the scope of its folder.  The review proposes no new test but the unit tests a demote needs, so these gaps are recorded rather than closed.

## Impact

Low today: much of this code is run by the command tests or by the database projects, which the scope of its folder does not see, so a gap here is not proof that nothing checks it.  It risks a change that the folder's own tests let through, and a failure found only by a slow command test or a container run, where a unit test would have named the fault.

## Proposed fix

For each file of the table, from the most: run its folder's scope with the command tests added to its filter, `tools/mutation-test.sh tool --tests '<the scope's filter>|FullyQualifiedName~SqlSource.Tool.Tests.Commands.' '<the file>'`, to see which gaps the command tests already close; a file that only a database project reaches is judged by reading.  Write a unit test in the folder's tests for the behaviour each remaining survivor or uncovered branch breaks, or record why a command or database test is the right guard.  Rewrite the table of this item, or resolve it, as the gaps close.

## Trigger

A change to one of the files of the table, or the next run of the test review, which updates this item.
