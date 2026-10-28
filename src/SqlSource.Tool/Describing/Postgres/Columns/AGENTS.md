# AGENTS.md - src/SqlSource.Tool/Describing/Postgres/Columns

- **A column is built from three answers, each by a class of its own** ([building parameters and columns](../../../../../docs/architecture/postgres-describer.md#building-parameters-and-columns)).
- **A column with an origin is `nullable` `false` only when the catalog says `NOT NULL`, its origin is a table that has no inheritance child, no foreign partition and no `INSTEAD` rule, and either the query is not gated or the walk proves that no outer join null-extends its relation instance** ([nullability](../../../../../docs/architecture/postgres-nullability.md#nullability)).  A column of no origin is `false` only by the walk's `expression` or `set-operation` answer.
- **`ColumnNullability.Of` is the one place of that rule for a column with an origin**: the walk and the table match call it for a query that is not gated, so the rule of a `NOT NULL` is written once.
- **The table match is `PostgresTableMatcher`, and it runs after everything that sets a column's nullability**, since its last check reads the `nullable` each column was given.  It asks the server nothing ([matching a table](../../../../../docs/architecture/postgres-describer.md#matching-a-table)).
- **The checks are the values of the format's `tableMatch` in the one order of `TableMatchChecks`, and the first that fails is the answer**: do not reorder them, since a sidecar says which failed first.  Names are compared ordinally, as the server returned them, and the attributes as lists, not as sets.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
