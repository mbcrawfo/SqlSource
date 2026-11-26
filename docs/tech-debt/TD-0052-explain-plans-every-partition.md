# TD-0052 - A plan that needs every partition is planned twice

## Problem

`explain` of the PostgreSQL describer ([`PostgresLive.ExplainAsync`](../../src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs)) asks for the plan with partition pruning on, and when that plan needs every partition (`PostgresLive.NeedsEveryPartition`), prepares the statement's text a second time and asks for its plan with pruning off.  A plan needs every partition when a node prints `Subplans Removed` above 0, which the executor prints when a query compares a partition key with a parameter or a stable function, as `at >= @from` does; or when an `UPDATE` or a `MERGE` reads a relation other than its target.  The describe of such a statement over a partitioned table plans every partition twice: the first plan prunes only when the executor starts, after the planner has done the work for each partition, and the second holds a scan of every partition.  On a table of thousands of partitions the describe of each such statement is slower, and its `postgres.explain` event longer, by the partitions it would never read.  A query whose conditions on the key are constants is planned once, with only the partitions they leave.

## Why it exists

The describer plans the prepared statement with a `NULL` for each parameter, under `force_generic_plan`.  The executor prunes partitions for the parameters' values when it starts, which `EXPLAIN` of an `EXECUTE` shows: a condition on the partition key and a parameter, `at >= $1`, prunes every partition for the `NULL`, and the plan prints an `Append` with no member, `Subplans Removed: 2`.  The walk tells a partitioned table's `Append` from a set operation's by its members, which must be partitions of one table (`PlanPartitions`), so an `Append` with none is read as a set operation, and every column through it keeps the heuristic.  An `UPDATE` or a `MERGE` may move the row it writes into a partition the planner pruned, so the one partition left would prove what it returns wrongly.  So such a plan is asked again without pruning, which shows every member.  The first `EXPLAIN` caches its generic plan in the prepared statement, which a setting does not end, so the second plan is of a second statement ([the plan](../architecture/postgres-nullability.md#the-plan)).  `EXPLAIN (GENERIC_PLAN)` of PostgreSQL 16 prints the plan without the executor's pruning, but 14 and 15 do not have it, and the describer does not use it for the reason [the plan](../architecture/postgres-nullability.md#the-plan) gives.

## Impact

Low.  A describe is slower for a query that compares the key of a table of many partitions with a parameter, or for an `UPDATE` or a `MERGE` that reads another relation, and only at build time.  The second `PREPARE` sends the query's text again, as the first does, and is freed before the call answers.

## Proposed fix

Read an `Append` that prints `Subplans Removed` without a second plan: the nodes above it print the partitioned table's alias, so the walk could prove a reference through every leaf partition's rows from the catalog (`pg_partition_tree`) rather than through the members, as `NullabilityWalk.Expressions.RowsOf` does through a plan's.  That drops the second statement for a read, but needs a set operation's column through such an `Append` to stay unproved.  An `UPDATE` or a `MERGE` would need the walk to refuse a scan of a partition of its target, which the plan does not tell from another relation without the catalog.  Or, on PostgreSQL 16 and later, read `EXPLAIN (GENERIC_PLAN)` of the SQL with its `$n` for the members, once the folding that [the plan](../architecture/postgres-nullability.md#the-plan) records is understood.  An `Append` that prints `Subplans Removed` above 0 must never feed the proof of a partitioned table's reference through its members' rows, nor a set operation's column through its members (`PlanNode.SubplansRemoved`), since another execution reads the rows of a member pruning removed.

## Trigger

A project on a table of hundreds of partitions or more, queried by a parameter of its key, whose describe is slow, or whose log is large, for its `explain`.
