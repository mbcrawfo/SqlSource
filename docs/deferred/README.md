# Deferred work

Planned work that has not been delivered as planned.  Each active item is fully documented in its own file, linked from the ID column.  When an item is resolved, its row is removed and its file deleted in the same PR as the fix.

Next id: `D-0009`

## Active items

| ID | Status | Added | Planned in | Description |
|----|----|----|----|----|
| [D-0001](D-0001-dialects-not-delivered.md) | Open | 2026-10-06 | [SQL dialects design](https://github.com/mbcrawfo/SqlSource/blob/c4c3454e6ec16f9bb3b96aedc07f1fd1195f561b/docs/superpowers/specs/2026-10-06-sql-dialects-design.md) | Dialects for Redshift, Snowflake, BigQuery, ClickHouse, Spark and Trino, and names of their own for DuckDB, Firebird and Db2 |
| [D-0002](D-0002-describing-call-and-data-definition.md) | Open | 2026-10-14 | [Query generation epic design](../superpowers/specs/2026-10-07-query-generation-epic-design.md) | Describing `CALL` and data definition statements |
| [D-0003](D-0003-custom-type-conversion.md) | Open | 2026-10-15 | [Query generation epic design](../superpowers/specs/2026-10-07-query-generation-epic-design.md) | Sending a parameter of a type that the driver reads as no C# type, a composite, a table's row type or `regclass`, which converters cannot reach; `SQLSRC232` still refuses it |
| [D-0004](D-0004-input-models-bound-by-the-names-of-the-sql.md) | Open | 2026-10-18 | [Phase 5.3 models design](../superpowers/specs/2026-10-18-query-generation-phase-5-3-models-design.md) | A way to pass an input model to Dapper when its query writes snake_case parameters |
| [D-0005](D-0005-bracketed-type-override-keys.md) | Open | 2026-10-20 | [Query generation epic design](../superpowers/specs/2026-10-07-query-generation-epic-design.md) | Type override keys written in T-SQL's bracket form, `[dbo].[OrderStatus]` |
| [D-0006](D-0006-multiple-result-sets-on-sql-server.md) | Open | 2026-10-20 | [Phase 6 spike](../superpowers/specs/2026-10-20-query-generation-phase-6-sql-server-spike.md) | Models for a SQL Server query that returns more than one result set; only the first is described |
| [D-0007](D-0007-converters-by-convention.md) | Open | 2026-10-25 | [Converter research](../superpowers/specs/2026-10-24-query-generation-phase-8-converter-research.md) | Converters found by convention, a type's `Value` and its `From` or constructor, for Vogen's and StronglyTypedId's types; only methods marked `[SqlSourceConverter]` are converters |
| [D-0008](D-0008-converter-per-file-or-query.md) | Open | 2026-10-25 | [Converter research](../superpowers/specs/2026-10-24-query-generation-phase-8-converter-research.md) | A converter chosen per file or per query, by a suffix on the type override's entry; a type has one conversion in a project |

## Columns

| Column | Contents |
|----|----|
| ID | `D-NNNN`, linked to the item's file in this folder |
| Status | `Open` (nobody is working on it), `In progress`, or `Blocked` (waiting on something named in the item file) |
| Added | Date the item was recorded, `YYYY-MM-DD` |
| Planned in | Link to the spec, plan or doc that called for the work |
| Description | One line: what is missing |
