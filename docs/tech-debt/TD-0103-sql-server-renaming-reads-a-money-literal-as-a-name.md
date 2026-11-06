# TD-0103 - SQL Server's renaming reads a money literal as a name

## Problem

A money literal such as `$12` is a constant of `money` to SQL Server, but `$` starts a word by SQL Server's word rule, which [`SqlServerRenaming.IsWordStart`](../../src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerRenaming.cs) answers through `QuoteReader.IsNameCharacterOfSqlServer`.  So the engine-neutral [`SqlRenamer`](../../src/SqlSource.Tool/Bundles/Renaming/SqlRenamer.cs) reads `$12` as a word that is no engine word, and renames it to a name, `a_<hex>`.  The renamed SQL reads a column where the query held an amount.

Whether such a bundle is accepted is the proof's decision.  The capture's requests are renamed alike, so the renamed capture still answers the renamed query and the proof passes, and the bundle shows the maintainer a name in the place of the amount.  `Sql_AMoneyLiteral_IsRenamedToAName` of [`SqlServerRenamingTests`](../../tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerRenamingTests.cs) holds the renaming as it is.

## Why it exists

The word rule is the server's rule for names, the one `QuoteReader` and the describer read names by, and the SQL renamer has no rule for the literals of one engine: a number is a run the renamer skips only when it starts with a digit.  A money literal is rare in a query, which takes an amount as a parameter, so 10.3 kept the one word rule and recorded the case.

## Impact

Low.  No name or literal of the user's leaks: the amount is replaced like any word of the user's.  The renamed bundle misleads the maintainer about what the query held: `SELECT $12 AS price` reads as the selection of a column.  `diagnose` and `diagnose --scrub` write such a bundle.  The user is not told, since nothing of theirs leaks and a query rarely holds a money literal, so the manual has no known issue of it.

## Proposed fix

Read a `$` that a digit follows, then digits, an optional `.` and digits, as a number run in SQL Server's part, so that the SQL renamer keeps a money literal verbatim as it keeps `12`.  This needs the engine's contract to say where a number starts, today a digit for every engine ([the engine's contract](../architecture/bundles.md#the-engines-contract)), and the agreement of PostgreSQL's part.  Then change the test above to expect `$12` kept, update [what building SQL Server's part found](../architecture/bundles.md#what-building-sql-servers-part-found), and resolve this item.

## Trigger

A bug report whose renamed bundle shows a name where the query held an amount, or the next change to the SQL renamer's number runs.
