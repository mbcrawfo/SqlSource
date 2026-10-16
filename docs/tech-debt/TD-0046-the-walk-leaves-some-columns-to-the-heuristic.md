# TD-0046 - The walk leaves some columns to the heuristic

## Problem

The nullability walk of the PostgreSQL describer ([`NullabilityWalk`](../../src/SqlSource.Tool/Describing/Postgres/NullabilityWalk.cs), [`PlanInstances`](../../src/SqlSource.Tool/Describing/Postgres/PlanInstances.cs)) gives a column of a gated query the catalog's answer only when the column's entry of the plan's `Output` names a relation instance: a scan of a relation or of a foreign table, a partitioned table's `Append`, or a `Subquery Scan` or `CTE Scan` through which the column's origin is found in the other plan.  Two kinds of column never do, and keep the heuristic's answer where the server can never return `NULL` for them:

- **A column of an extension's `Custom Scan`**, Citus and TimescaleDB for two.  An expression a node computes from what such a scan returns is read as any other: a join inside the scan, which the plan does not show, is not seen, as it is not for a `Foreign Scan` of a join pushed down to the remote server, which `postgres_fdw` does only for a join that needs no value computed inside it.
- **A `RETURNING` column of any table but the target in a statement that names `OLD` or `NEW`.**  PostgreSQL 18 prints its old and new row under a name an alias of an inner scope can share in the plan, so `OuterJoinGate.NamesOldOrNew` makes the walk map only the target's alias at a root `ModifyTable`: a joined table's column keeps the heuristic, and so does every column of a table merely aliased `old`.  PostgreSQL 18 resolves the quoted `"old"` and `"new"` too, so a quoted identifier of either name counts, in any case, and so does any `U&"..."` identifier, whose escapes the tool does not decode: a table aliased `"Old"`, or any statement with a Unicode-escaped identifier, keeps the heuristic as well.  The fix is to tell the old and the new row from an alias: before 18 the words name no row, and on 18 the names in use are `old` and `new` or those `WITH (OLD AS ..., NEW AS ...)` gives, read from the SQL and the server version, so that any other alias maps.  Reading the SQL must read quoted identifiers too, a doubled quote and a `U&"..."` with its `UESCAPE` decoded, and compare the name as the server does, case and all.

Two more things are not whole:

- **A plan is asked for when it cannot change anything.**  The gate reads words, so `left(name, 3)` gates a query with no join and costs one `EXPLAIN` in a transaction, and a `partitions` call when the plan scans a partitioned table.  A query with a column of no origin is planned whether or not it is gated, since the plan may answer that column as an expression.
- **`postgres.column` holds the heuristic's answer for a gated query.**  The event is written when the column is built, before the plan is asked for; the answer the sidecar records is in `postgres.walk.column`.

## Why it exists

A `Custom Scan` is an extension's, and what it scans and how it joins is the extension's to say: the plan prints what the extension chooses.  The old and the new row of PostgreSQL 18 print under a name an alias of an inner scope can share, and telling them apart needs the SQL read as the server reads it.  The plan is asked for every gated query because the gate reads words, and the cost is one round trip.

## Impact

Low.  A column that is `true` with `heuristic` is a nullable property in a generated model where a non-nullable one would do: never wrong, sometimes a `!` or a check the user did not need.  A project on Citus or TimescaleDB gets nothing from the walk for the columns its extension scans.

## Proposed fix

For a `Custom Scan`: a scan of one relation names it in `Relation Name` with an `Alias`, as a `Seq Scan` does, and could join the relation scans once a container test against an extension shows it; a scan that joins, as a `Foreign Scan` of a join pushed down does, has neither and stays unmapped.  For `OLD` and `NEW`: before 18 the words name no row, and on 18 the names in use are `old` and `new` or those `WITH (OLD AS ..., NEW AS ...)` gives, read from the SQL and the server version, so that any other alias maps.  Reading the SQL must read quoted identifiers too, a doubled quote and a `U&"..."` with its `UESCAPE` decoded, and compare the name as the server does, case and all.  For the plan nobody needs: only a query whose every column has an origin can skip it, and then only when no word of the gate can null-extend anything.  The gate could tell a function's name from a join's word by the `(` that follows it, `left(name, 3)` against `LEFT JOIN`, and skip `explain` for such a query, recording `plan` as `not-needed`.  `postgres.column` is left as it is, since a member is never changed once a reader has it, and the walk's answer stays in `postgres.walk.column`.

## Trigger

A user's report of a column they expected to be non-nullable, with a bundle of phase 10 that shows a `Custom Scan` or a `RETURNING` of `OLD` or `NEW`; a project on an extension that plans its own scans.
