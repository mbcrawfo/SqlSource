# AGENTS.md - src/SqlSource.Tool/Describing/SqlServer/Columns

- **A column's `nullable` is the server's `is_nullable`, narrowed only by a proof of `SqlServer/Proofs/` (`nullableSource` `expression`) or by a `PERSISTED NOT NULL` computed column read from its one table (`catalog`), and never widened** ([columns](../../../../../docs/architecture/sqlserver-describer.md#columns), [the proofs of not null](../../../../../docs/architecture/sqlserver-describer.md#proofs-of-not-null)).
- **A column has an origin only when its source is a table or a view of the session's database, the `DB_NAME()` that `open` reads** (`SqlServerColumns.OriginOf`, [origins](../../../../../docs/architecture/sqlserver-describer.md#origins)).  The sidecar's origin names no database, so a column of another database, a linked server or the system resource must not match a table of the same name in the session's.
- **`SqlServerTableMatcher` runs the format's checks through `TableMatchChecks`, the one order both matchers share**, and the server is asked for `table` only when its first two checks pass ([the table match](../../../../../docs/architecture/sqlserver-describer.md#the-table-match)).
- **The matcher reads the table's columns and their nullability from `table`'s answer, never from `sys.columns`**, so that a hidden period column is out of the list, and compares a query with those rows corrected by `ColumnProofs.TableRows`.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
