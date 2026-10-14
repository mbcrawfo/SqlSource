# TD-0046 - The walk leaves some columns to the heuristic

## Problem

The nullability walk of the PostgreSQL describer ([`NullabilityWalk`](../../src/SqlSource.Tool/Describing/Postgres/NullabilityWalk.cs), [`PlanInstances`](../../src/SqlSource.Tool/Describing/Postgres/PlanInstances.cs)) gives a column of a gated query the catalog's answer only when the column's entry of the plan's `Output` names a relation instance.  Six kinds of column never do, and keep the heuristic's answer where the server can never return `NULL` for them:

- **A column through a `Subquery Scan` or a `CTE Scan`.**  A subquery with a window function, a `LIMIT` the planner keeps apart, a `MATERIALIZED` CTE or one that is referenced twice is planned by itself, and the outer plan prints `s.total`, where `s` is the scan and no relation.  The fixture cases `walk-subquery-kept` and `walk-cte-materialized` show it.
- **A column of a partitioned table or of an inheritance parent.**  It scans as an `Append` of `Member` children, whose aliases are `p_1` and `p_2` while the `Output` above prints `p.id`.  A qual on a constant prunes at plan time, and the one partition left scans under the alias the query gave the table, a `Seq Scan on public.pt1 p` with alias `p` under the join on 14 and 18, so the walk answers.  A qual on a parameter prunes at run time, so the generic plan keeps the `Append`.
- **A column of a foreign table**, whose `Foreign Scan` is no scan of the seven kinds: it stays `null` with `view` where a join extends it, and is not `outer-join`.
- **A column of an extension's `Custom Scan`**, Citus and TimescaleDB for two.
- **A bare name in the `RETURNING` list of an `UPDATE` or a `DELETE` with any join under its `ModifyTable`**, a `WHERE id IN (...)` that plans as a semi join for one.  A bare name there may be a column of a relation the planner proved empty under an outer join, so the walk takes it for the target's only with no join under the `ModifyTable`.  Only a join that null-extends can hide such a relation, and the rule could narrow to those.
- **A `RETURNING` column of any table but the target in a statement that names `OLD` or `NEW`.**  PostgreSQL 18 prints its old and new row under a name an alias of an inner scope can share in the plan, so `OuterJoinGate.NamesOldOrNew` makes the walk map only the target's alias at a root `ModifyTable`: a joined table's column keeps the heuristic, and so does every column of a table merely aliased `old`.  PostgreSQL 18 resolves the quoted `"old"` and `"new"` too, so a quoted identifier of either name counts, in any case, and so does any `U&"..."` identifier, whose escapes the tool does not decode: a table aliased `"Old"`, or any statement with a Unicode-escaped identifier, keeps the heuristic as well.  The fix is to tell the old and the new row from an alias: before 18 the words name no row, and on 18 the names in use are `old` and `new` or those `WITH (OLD AS ..., NEW AS ...)` gives, read from the SQL and the server version, so that any other alias maps.  Reading the SQL must read quoted identifiers too, a doubled quote and a `U&"..."` with its `UESCAPE` decoded, and compare the name as the server does, case and all.

Two more things are not whole:

- **A plan is asked for when it cannot change anything.**  The gate reads words, so `left(name, 3)` gates a query with no join, and a gated query none of whose columns has an origin is asked for its plan all the same.  Each costs one `EXPLAIN` in a transaction.
- **`postgres.column` holds the heuristic's answer for a gated query.**  The event is written when the column is built, before the plan is asked for; the answer the sidecar records is in `postgres.walk.column`.

## Why it exists

The epic planned to map a subquery scan's outputs to the child plan's by position.  The spike found that the scan's `Output` lists the subquery's columns by name, and the child plan's `Output` is positional expressions, `o.total`: the names of the subquery's columns are nowhere in the plan.  Matching by name is unsafe, since `SELECT o.note AS total, o.total AS note` would be read crossed, and a `false` that is wrong is the one thing the walk must never give.  A partitioned table was left because an `Append` is also what a `UNION` plans as, and telling the two apart needs the `Relation Name` of every member and the catalog's `pg_inherits`.  The plan is asked for every gated query because the spec says so, and the cost is one round trip.

## Impact

Low.  A column that is `true` with `heuristic` is a nullable property in a generated model where a non-nullable one would do: never wrong, sometimes a `!` or a check the user did not need.  A project whose tables are all partitioned gets nothing from the walk for the columns of the table an outer join keeps.

## Proposed fix

For a subquery scan: the child plan of a `Subquery Scan` is its one `Subquery` child, and of a `CTE Scan` the `InitPlan` whose `Subplan Name` is `CTE <CTE Name>`, both of which `PlanNode` already reads.  The scan's `Output` and the child's are in the same order when the scan projects nothing away, which a test must establish for each shape before any code trusts it; where it holds, the `n`th entry of the scan maps to the `n`th of the child, which is walked in turn with the joins above the scan added to its path.  Where the order cannot be proved the column stays unknown.  For a partitioned table: an `Append` whose every `Member` is a scan of a relation that `pg_inherits` says is a child of one parent is an instance of that parent under the parent's alias, the members' alias with its `_n` cut.  That needs one more catalog call.  For a foreign table: add `Foreign Scan` to `PlanInstances.RelationScans`, with a container test against `postgres_fdw`.  For the plan nobody needs: skip `explain` when no column has an origin in a table or a view, and record `plan` as `not-needed`.  `postgres.column` is left as it is, since a member is never changed once a reader has it, and the walk's answer stays in `postgres.walk.column`.

## Trigger

A user's report of a column they expected to be non-nullable, with a bundle of phase 10 that shows a `Subquery Scan` or an `Append`; phase 5, when the models make the cost of a nullable property visible; a project on a partitioned schema.
