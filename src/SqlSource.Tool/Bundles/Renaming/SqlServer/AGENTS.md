# AGENTS.md - src/SqlSource.Tool/Bundles/Renaming/SqlServer

How SQL Server's part answers each member of the contract: [renaming for SQL Server](../../../../../docs/architecture/bundles.md#renaming-for-sql-server).

- **Every replacement comes through the scope**, `IRenamingScope.Parameter` and the rest, and `SqlServerMessages` writes no name of its own making: the token check trusts only the mapping's replacements and the engine's words.
- **A string member added to a record of `Describing/SqlServer/SqlServerCalls.cs`, or to the entry's SQL Server type, gets its rule in `SqlServerFields.cs`**, and never `FieldRule.Drop` while the pipeline reads it ([the fields](../../../../../docs/architecture/bundles.md#sql-servers-fields)).  A member with no rule is refused in every bundle that holds it.
- **`SqlServerVocabulary`'s lists are the union of what the floor and the default images know.**  When `VocabularyTests` or `CorpusWordsTests` of `tests/DatabaseTests/SqlServer/Renaming/` fail, add each engine word they name to its list, and each word of the user's to `CorpusUserWords`; never trim a list to make a test pass ([the vocabulary](../../../../../docs/architecture/bundles.md#sql-servers-vocabulary)).
- **Never put a word of the user's in the vocabulary**: an engine word is kept verbatim, and the proof cannot tell, since a renamed engine word still replays.
- **`SqlServerVocabulary.Fold` is the one fold**, of `Fold`, the vocabulary and the engine's rows, upper-invariant by character.  It ignores case whatever the collation, which is [TD-0102](../../../../../docs/tech-debt/TD-0102-sql-server-renaming-folds-ignoring-case.md).
- **`CopyOf` gives the suffix `_<n>` whatever the raw copy's separator**, since it is what `ParameterCopies` makes from the renamed query ([the copies](../../../../../docs/architecture/bundles.md#the-copies-of-a-reused-parameter)).  A copy it cannot read back, cut or of an ambiguous separator, is [TD-0104](../../../../../docs/tech-debt/TD-0104-sql-server-renaming-cannot-read-back-some-copies.md).
- **A message keeps only the `@name`s that `SqlServerErrors.NamedIn` reads**, a copy as its parameter, since no decision reads more of it ([the messages](../../../../../docs/architecture/bundles.md#sql-servers-messages)).

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
