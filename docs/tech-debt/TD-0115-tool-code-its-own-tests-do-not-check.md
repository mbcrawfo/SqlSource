# TD-0115 - Tool code its own tests do not check

## Problem

The test review of 2026-11-13 ran a mutation test of every scope of `src/SqlSource.Tool`, each part of the source with the tests of its own folder.  1,881 of its mutants belong to no test: a survivor that no test of its scope fails on, or a mutant that no test of its scope runs, less the survivors that a finding of the review asks a test to kill.  The appendix of the review, [docs/superpowers/specs/2026-11-13-test-cleanup-findings.md](../superpowers/specs/2026-11-13-test-cleanup-findings.md#gaps-that-belong-to-no-test), counts them by file.  The 873 of the thirteen files that the next sections name are settled, which leaves 1,008 in 136 files, of which 234 are in the other files of `Describing/Postgres/Walk` and `Describing/SqlServer/Proofs`.  The files with the most:

| File | Survived | No coverage | Gap |
|----|----|----|----|
| [`Describing/SqlServer/Proofs/SelectBody.cs`](../../src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs) | 44 | 4 | 41 |
| [`Describing/RunSummary.cs`](../../src/SqlSource.Tool/Describing/RunSummary.cs) | 0 | 39 | 39 |
| [`Describing/SqlServer/Proofs/ExpressionProofs.cs`](../../src/SqlSource.Tool/Describing/SqlServer/Proofs/ExpressionProofs.cs) | 34 | 2 | 36 |
| [`Describing/Postgres/PostgresConnection.cs`](../../src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs) | 7 | 25 | 32 |
| [`Describing/Postgres/Walk/NullabilityWalk.Expressions.cs`](../../src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.Expressions.cs) | 30 | 2 | 32 |
| [`Describing/SidecarStore.cs`](../../src/SqlSource.Tool/Describing/SidecarStore.cs) | 22 | 7 | 29 |
| [`Bundles/Renaming/SqlRenamer.cs`](../../src/SqlSource.Tool/Bundles/Renaming/SqlRenamer.cs) | 23 | 5 | 28 |

`SelectBody.cs` counts 41, not 48, since a finding of the review names seven of its survivors.

## Six files of the nullability proofs

[`TotalFunctions.cs`](../../src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/TotalFunctions.cs), the four `PlanExpression*.cs` beside it and [`ExpressionProofs.Calls.cs`](../../src/SqlSource.Tool/Describing/SqlServer/Proofs/ExpressionProofs.Calls.cs) have no gap that could answer a nullable column not null.  A run of their folders' tests on 2026-11-22 leaves 184 mutants undetected, each read:

- **79 are in `TotalFunctions.cs`**, in the code that builds its table once, in a static initialiser, where a mutation run cannot switch them on.  `Catalog_TotalFunctions_ArePgCatalogsOwn` in [`CatalogPinTests.cs`](../../tests/DatabaseTests/Postgres/Describer/CatalogPinTests.cs) is their guard, since only a server's catalog can say what a row must hold: it failed on each of seven such changes made by hand, to a name, a result, a C function, a row left out and the mixed integers.
- **105 are in the other five files**, and change no answer, make the reader prove less, or change only which name is asked of the server.  Those in the operand of a test are among them, since `IS NULL` is never `NULL` whatever its operand.

## Five files the command tests reach

[`WatchSession.cs`](../../src/SqlSource.Tool/Watching/WatchSession.cs), [`DiagnoseRun.cs`](../../src/SqlSource.Tool/Bundles/DiagnoseRun.cs), [`DescribeRun.cs`](../../src/SqlSource.Tool/Describing/DescribeRun.cs), [`RunEvents.cs`](../../src/SqlSource.Tool/Logging/RunEvents.cs) and [`SidecarCheck.cs`](../../src/SqlSource.Tool/Describing/SidecarCheck.cs) have no gap left.  A run of each on 2026-11-24 with its folder's tests and the command tests left 33 survivors once its timeouts were run again with an allowance of ten minutes:

- **11 broke a behaviour that no test held**, and are killed by tests of `tests/SqlSource.Tool.Tests/Commands/` and `Logging/`: `diagnose` of a query in a file after another, of a file with a parse error, of one no type claims, without a connection, beside a newer tool's sidecar, and of a bundle of an engine with no describer, a `describe --check` cancelled as its session closes, the log of a check that compared nothing, and the `--verbose` line of a file two types claim.  Five more were killed by a test of `Logging/` that the run left out.
- **The other 17 change nothing a user sees**: the message of an exception for a state that is a bug, a `Single` of a list of one, a branch that a sidecar of the current version never reaches, a text built twice where once would do, a dictionary cleared as its session ends, a describer and a capture that the steps before them already made sure of, and two that only add a true error beside the one that stops `diagnose`.

21 mutants of `WatchSession.cs` still outlast ten minutes: each stops the watch from ending, which its tests wait for.

## Two live describers

[`SqlServerLive.cs`](../../src/SqlSource.Tool/Describing/SqlServer/SqlServerLive.cs) and [`PostgresLive.cs`](../../src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs) run only against a server, so the database projects are their guard and they were read: of their 190 behaviours, 144 are held by a test of `tests/DatabaseTests/`, 24 cannot change an answer, and 8 more have a test since 2026-11-24 (a `PERSISTED` column whose expression may be `NULL`, a table name with a `.` and a `]`, a text over 4,000 characters, the edition, a `DO ALSO` rule, a partition's ancestors in order and an enum's labels in order).  SQL Server's `origins`, added since, is held by the views of geometry in `ProofTests` and its canary in `SafetyTests`.  14 have no guard, since each needs a server or a fault that the database projects do not set up:

- SQL Server: a column of a linked server, or of a database whose name differs from the session's by its case on a case-sensitive server, which would take the persisted `NOT NULL` of a local table of its name; a database of a collation other than the server's; a parameter declared of a user's CLR type, whose assembly name `declared` reads; a probe that fails with a fatal error, a timeout or a cancellation, which must not read as "does not bind"; a session that ends between calls; an error the server raises after the rows; and a flag the server answers `NULL`.
- PostgreSQL: a pooler in statement mode, whose refusal is `SQLSRC236`; a proxy that answers a read with no row, ends the transaction on an error or answers another statement's read; and a parameter's name that Npgsql refuses with an `ArgumentException`.

## Why it exists

A scope sees only its own source and the tests of its own folder, so code that only the command tests of `tests/SqlSource.Tool.Tests/Commands/` or the database projects reach, as a watch session, a diagnose run or a live describer is, shows as unchecked in the scope of its folder.  The review proposes no new test but the unit tests a demote needs, so these gaps are recorded rather than closed.  The live describers' last 14 need a linked server, a second collation, a CLR assembly, a pooler, a proxy or a fault injected into a call, none of which the containers have.

## Impact

Low today: much of this code is run by the command tests or by the database projects, which the scope of its folder does not see, so a gap here is not proof that nothing checks it.  It risks a change that the folder's own tests let through, and a failure found only by a slow command test or a container run, where a unit test would have named the fault.  A change to the live describers' clauses for a linked server or a case-sensitive server could answer a nullable column not null, and no test would fail.

## Proposed fix

For each file of the table, from the most: run its folder's scope with the command tests added to its filter, `tools/mutation-test.sh tool --tests '<the scope's filter>|FullyQualifiedName~SqlSource.Tool.Tests.Commands.' '<the file>'`, to see which gaps the command tests already close; a file that only a database project reaches is judged by reading.  The command tests are slow, so run the mutants that time out again, by their spans, with `additional-timeout` raised in `src/SqlSource.Tool/stryker-config.json` for that run, before believing a timeout.  Write a unit test in the folder's tests for the behaviour each remaining survivor or uncovered branch breaks, or record why a command or database test is the right guard.  Rewrite the table of this item, or resolve it, as the gaps close.

For the live describers, a database test for each of the 14 once the containers can stage it: a loopback linked server, a database of another collation, a pooler or a proxy in front of the server.

## Trigger

A change to one of the files of the table or to a clause of a live describer named above, or the next run of the test review, which updates this item.
