# TD-0052 - The plan of a query is asked for without partition pruning

## Problem

`explain` of the PostgreSQL describer ([`PostgresLive.ExplainAsync`](../../src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs)) runs with `SET LOCAL enable_partition_pruning = off`, so the plan of a query over a partitioned table holds a scan of every partition, whatever its conditions.  On a table of many partitions the planner does work for each, and the plan's text grows with them: a table of thousands of partitions makes the describe of each query over it slower, and its `postgres.explain` event longer, by the partitions a query would never read.

## Why it exists

The describer plans the prepared statement with a `NULL` for each parameter, under `force_generic_plan`.  The executor prunes partitions for the parameters' values when it starts, which `EXPLAIN` of an `EXECUTE` shows: a condition on the partition key and a parameter, `at >= $1`, prunes every partition for the `NULL`, and the plan prints an `Append` with no member, `Subplans Removed: 2`.  The walk tells a partitioned table's `Append` from a set operation's by its members, which must be partitions of one table (`PlanPartitions`), so an `Append` with none is read as a set operation, and every column through it keeps the heuristic.  Without pruning every member shows, on PostgreSQL 14 and 18 alike.  `EXPLAIN (GENERIC_PLAN)` of PostgreSQL 16 prints the plan without the executor's pruning, but 14 and 15 do not have it, and the describer does not use it for the reason its `AGENTS.md` gives.

## Impact

Low.  A describe is slower for a query over a table of many partitions, and only at build time; the plan the tool reads is the same shape the server would run but for the members it would prune, and no answer is less safe for it.

## Proposed fix

Ask for the plan with pruning only when the plan without it is not needed: plan once as today, and again without pruning only when the plan holds an `Append` with `Subplans Removed` and no member.  That costs a second `EXPLAIN` for those queries and none for the rest, and changes the capture of the queries that need both.  Or, on PostgreSQL 16 and later, read `EXPLAIN (GENERIC_PLAN)` of the SQL with its `$n` for the members, once the folding that `AGENTS.md` records is understood.

## Trigger

A project on a table of hundreds of partitions or more whose describe is slow, or whose log is large, for its `explain`.
