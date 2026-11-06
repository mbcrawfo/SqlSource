# TD-0102 - SQL Server's renaming folds names ignoring case

## Problem

SQL Server's part of the renaming folds every name ignoring case, whatever the database's collation: [`SqlServerRenaming.Fold`](../../src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerRenaming.cs) is `SqlServerVocabulary.Fold`, each character upper-cased.  [`NameMapping`](../../src/SqlSource.Tool/Bundles/Renaming/NameMapping.cs) gives one replacement to every spelling of one fold, and every replacement is lower case.

The pipeline compares some names by ordinal: [`SqlServerTableMatcher`](../../src/SqlSource.Tool/Describing/SqlServer/Columns/SqlServerTableMatcher.cs) compares a column's name with its origin's, and `ColumnProofs.AreTheColumns` of [`ColumnProofs`](../../src/SqlSource.Tool/Describing/SqlServer/Proofs/ColumnProofs.cs) compares the query's names with the server's.  So a query whose names differ from the catalog's only by case, such as `EMAIL` for the column `email`, becomes one name when it is renamed.  Its answer changes, a table match of `names-differ` becoming `matched`, and the proof refuses the bundle at `expected.json`.  `RenameAsync_ANameThatDiffersFromTheCatalogsByCaseAlone_IsRefusedByTheProof` of [`SqlServerErrorRenamingTests`](../../tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.cs) holds the refusal.

A database whose collation is case-sensitive folds the same way, so two names it keeps apart, `Users` and `users`, become one in the bundle.

The owner's decision, on 2026-11-05: "agree, make it tech debt".

## Why it exists

`IEngineRenaming.Fold` is given a spelling and whether it is quoted, and no bundle, so it cannot read the `collation` that `open`'s settings hold.  And the pipeline compares names both by ordinal, as above, and ignoring case, as `SqlParameterName.Comparer` compares parameters and a CTE's name is compared, so no fold to one lower-case replacement keeps both comparisons' answers.  Folding ignoring case keeps the comparisons that ignore case, and the proof refuses a bundle whose answer the other changes, so nothing wrong is posted.

## Impact

Medium.  A query that spells a name in another case than the catalog does, where the tool compares it by ordinal, is common in SQL Server, whose default collations ignore case.  Its bundle cannot be renamed: `diagnose` refuses it with `SQLSRC294`, and it must be sent raw and privately, as the manual's [known issues of the tool](../manual/tool.md#known-issues) say.  A bundle of a case-sensitive database can merge two names; the proof refuses it when that changes the answer, and otherwise the bundle replays as the original did.

## Proposed fix

Make each replacement carry the original's case pattern, in the engine-neutral `NameMapping`: one hash for every spelling of one fold, written in a case of its own for each spelling, so that two spellings that differ by case alone stay apart by ordinal and equal ignoring case.  And give the fold the database's collation, read from `open`'s `collation` setting, so that a case-sensitive database folds by ordinal.  The second changes `IEngineRenaming`, which needs the agreement of PostgreSQL's part ([the engine's contract](../architecture/bundles.md#the-engines-contract)).  Then change the test above to expect the bundle renamed, update [SQL Server's fold](../architecture/bundles.md#sql-servers-fold) and the folder's `AGENTS.md`, and resolve this item.

## Trigger

A user's report of such a refusal, or the next change to `NameMapping`.
