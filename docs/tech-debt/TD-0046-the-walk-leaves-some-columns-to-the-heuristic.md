# TD-0046 - The walk leaves some columns to the heuristic

## Problem

The nullability walk of the PostgreSQL describer ([`NullabilityWalk`](../../src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.cs), [`PlanInstances`](../../src/SqlSource.Tool/Describing/Postgres/Walk/PlanInstances.cs)) gives a column of a gated query the catalog's answer only when the column's entry of the plan's `Output` names a relation instance: a scan of a relation or of a foreign table, a partitioned table's `Append`, or a `Subquery Scan` or `CTE Scan` through which the column's origin is found in the other plan.  Some columns never get that answer, and keep the heuristic's where the server can never return `NULL` for them:

- **A column of an extension's `Custom Scan`**, Citus and TimescaleDB for two.  An expression a node computes from what such a scan returns is read as any other: a join inside the scan, which the plan does not show, is not seen, as it is not for a `Foreign Scan` of a join pushed down to the remote server, which `postgres_fdw` does only for a join that needs no value computed inside it.
- **On PostgreSQL 18, a `RETURNING` column whose alias may be the old or the new row's name.**  [`OldAndNewNames`](../../src/SqlSource.Tool/Describing/Postgres/Statements/OldAndNewNames.cs) reads the names: `old`, `new` and every name a `RETURNING WITH (...)` gives, for every list of the statement together.  A table aliased with one of them keeps the heuristic, the target included, since a view's query can give the target the old row's name.  When a name cannot be read, a non-ASCII one included, every qualified reference keeps it.  Four cases of this are rows of the statement's own tables, which the server never confuses with the old or the new row:
  - a target aliased `old` or `new` in the statement itself, `UPDATE t AS old ... RETURNING old.v`, which the server gives the name, while a view's query does not;
  - a table of the top level aliased `old` or `new`, which takes the name from the row;
  - a table aliased `old` in a list that renamed the old row;
  - a table of one list aliased with a name that only another list gives.
- **On PostgreSQL 18, an expression over a bare name at a root `ModifyTable`** in a statement that names either row, `RETURNING id + 1` of a single-target `UPDATE`.  The plan prints the old and the new row qualified, so a bare name is the target's.  The expression keeps the stricter rule it had before the names were read.

Two more things are not whole:

- **A plan is asked for when it cannot change anything.**  A query with a column of no origin or of a view is planned whether or not it is gated, since the plan may answer that column as an expression.  So is any statement that holds the word `INSERT` and a nullable column of a table, though it writes nothing the walk can read.  A word of the gate that is no function's name still gates a query with no join, `GROUPING` beside a plain `GROUP BY` for one, and costs one `EXPLAIN` in a transaction, and a `partitions` call when the plan scans a partitioned table.
- **`postgres.column` holds the heuristic's answer for a gated query.**  The event is written when the column is built, before the plan is asked for; the answer the sidecar records is in `postgres.walk.column`.

## Why it exists

A `Custom Scan` is an extension's, and what it scans and how it joins is the extension's to say: the plan prints what the extension chooses.  Telling a top-level table from an inner scope's, or a list's names from another's, needs the statement parsed as the server parses it, which the tool does not do; the names it reads are the words of the SQL.  The plan is asked for every gated query because the gate reads words, and the cost is one round trip.

## Impact

Low.  A column that is `true` with `heuristic` is a nullable property in a generated model where a non-nullable one would do: never wrong, sometimes a `!` or a check the user did not need.  A project on Citus or TimescaleDB gets nothing from the walk for the columns its extension scans.

## Proposed fix

For a `Custom Scan`: a scan of one relation names it in `Relation Name` with an `Alias`, as a `Seq Scan` does, and could join the relation scans once a container test against an extension shows it; a scan that joins, as a `Foreign Scan` of a join pushed down does, has neither and stays unmapped.  For the old and the new row: read which tables are the statement's top level, and whether the written target is a table or a view, and which list each `RETURNING WITH` belongs to, so a top-level table or a table target aliased `old` maps and a list's names apply to its own `ModifyTable`; and read a bare name in an expression as the target's on 18, with a test of a single-target `UPDATE` and `DELETE` that names either row.  For the plan nobody needs: tell a statement that writes from one that merely holds the word `INSERT`, by its first word or a data-modifying `WITH`.  `postgres.column` is left as it is, since a member is never changed once a reader has it, and the walk's answer stays in `postgres.walk.column`.

## Trigger

A user's report of a column they expected to be non-nullable, with a bundle of `sqlsource diagnose` that shows a `Custom Scan` or a `RETURNING` of a table aliased as the old or the new row; a project on an extension that plans its own scans.
