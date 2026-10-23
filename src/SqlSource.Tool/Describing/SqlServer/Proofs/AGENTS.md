# AGENTS.md - src/SqlSource.Tool/Describing/SqlServer/Proofs

How the SQL Server describer works: [`docs/architecture/sqlserver-describer.md`](../../../../../docs/architecture/sqlserver-describer.md).

- **The reader gives up rather than guess, and a misread must prove nothing.**  `TsqlTokens`, `SelectStatement`, `SelectBody` and `SelectItem` read a text they cannot follow as null, and a name they cannot read as a null `Name`, never as a likely answer: a column proved from a misread text is a silent wrong `false`.  A new form gets a test of what it must refuse beside what it reads.
- **Nothing here sends anything.**  The reader reads the tokens of the user's text and calls no server, no exchange and no log; what a proof asks the server goes through the session's calls, by the rule of `Describing/SqlServer/` in `src/SqlSource.Tool/AGENTS.md`.
- **Tokens come from the generator's lexer, `DescriberSql.Lex` with `SqlDialectRules.SqlServer`**, so a comment, a string and a quoted name are split as the plan splits them.  Do not read the raw text for them.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
