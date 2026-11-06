# AGENTS.md - src/SqlSource.Tool/Bundles/Renaming/Postgres

How PostgreSQL's part of the renaming answers each member of `IEngineRenaming`: [renaming for PostgreSQL](../../../../../docs/architecture/bundles.md#renaming-for-postgresql).

- **A member the describer reads gets its rule in `PostgresFieldRules.cs`, and a key of a plan it reads its kind in `PostgresPlanRenamer.cs`**, in the change that makes the describer read it: the renaming refuses a member with no rule and drops a key with no kind ([why](../../../../../docs/architecture/bundles.md#renaming-for-postgresql)).
- **Nothing here keeps a word through `IRenamingScope.Keep`.**  The token check does not trust such a word, so a word of the engine's is kept by `PostgresWords.IsEngineWord` and a row of its own by `PostgresFieldRules.IsEngineRow` ([why](../../../../../docs/architecture/bundles.md#renaming-for-postgresql)).
- **A server's message is only ever written back as the tool's text**, in `PostgresOwnStrings.cs`: the server's may quote the user's names and values ([why](../../../../../docs/architecture/bundles.md#renaming-for-postgresql)).
- **`postgres-vocabulary.txt` is made by `VocabularyTests` of `tests/DatabaseTests/Postgres`, never by hand**, from the floor and default images ([how](../../../../../docs/architecture/bundles.md#renaming-for-postgresql)).
- **`PostgresRenaming.cs` only delegates** to the other files, by the field's method and path for the strings that are PostgreSQL's own; a decision goes in the file of its concern ([the parts](../../../../../docs/architecture/bundles.md#renaming-for-postgresql)).

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
