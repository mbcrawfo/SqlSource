# The SQL Server lists of the phase 10 spike

The two lists that [the phase 10 spike](../2026-11-05-query-generation-phase-10-spike.md#3-the-vocabulary) counted and its prototype reads, fetched from Microsoft's documentation on 2026-11-04.

- `mssql-keywords.tsv` (`word`, `list`): the reserved, ODBC and future keywords of [Reserved keywords (Transact-SQL)](https://learn.microsoft.com/en-us/sql/t-sql/language-elements/reserved-keywords-transact-sql).  The note `INT` and the Azure Synapse Analytics keyword `LABEL` were left out of the reserved list, and `WITHIN GROUP` was split to `WITHIN`.  The future list as fetched lacks `REGR_R2`, the one word of the page with a digit, which the extraction lost: it holds 272 of the page's 273, and is kept as the prototype read it.
- `mssql-functions.tsv` (`name`, `category`): the node "Functions" of the documentation's table of contents, <https://learn.microsoft.com/en-us/sql/toc.json>, since the category pages under [Functions](https://learn.microsoft.com/en-us/sql/t-sql/functions/functions) are partly rendered by script.  The `@@` variables, `$PARTITION`, `UPDATE()`, `NEXT VALUE FOR` and the entries of models were left out; the rowset functions (`OPENQUERY`, `OPENROWSET`, `OPENXML`, `OPENJSON`, `OPENDATASOURCE`, `CONTAINSTABLE`, `FREETEXTTABLE` and the `SEMANTIC*TABLE` functions) and `COALESCE`, `NULLIF`, `CAST`, `CONVERT` and `ISNULL` were added from the rowset and language pages.

They are the lists as fetched, kept so that the spike's counts can be checked again; the lists a later sub-phase commits for the renaming are its own.
