# TD-0052: partition pruning in plans

**Goal:** `explain` of the PostgreSQL describer asks for the plan with partition pruning on, and again with it off only when that plan prints `Subplans Removed` above 0, as [TD-0052](../../tech-debt/TD-0052-explain-plans-every-partition.md) proposes.

## What the code and the server say, against the item

- Measured on 14.24 and 18.6: the first `EXPLAIN EXECUTE` caches its generic plan in the prepared statement.  A second one after `SET LOCAL enable_partition_pruning = off` reuses it and still prints `Subplans Removed`, and a rollback to the savepoint keeps it.  The owner chose (Q1, option B) a second `PREPARE` of the same text under `sqlsource_<n>_unpruned`, freed in the call, over `DISCARD PLANS`, which reaches other clients' cached plans on a pooled backend.
- Review finding R1-F1: an `UPDATE` or `MERGE` may move its row into a partition the planner pruned.  The owner chose (Q2) to plan again without pruning for any `ModifyTable` of an `UPDATE` or `MERGE` that reads a relation other than its target.
- With pruning off every partition shows, even for a constant on the key.  With it on, a constant prunes at plan time and prints no `Subplans Removed`: one partition left is a plain scan under the query's alias, and none left a `Result` with `One-Time Filter: false`.  A partition the planner prunes holds no row for any execution of the plan, since the planner prunes by immutable values alone and leaves a parameter or a stable function to the executor.
- The request does not change: the decision is made in `PostgresLive.ExplainAsync`, so an older capture, whose plan was asked without pruning, still replays, and a capture changes only where the first plan differs.

## Steps

1. `PostgresLive`: `SetTexts` sets `enable_partition_pruning = on` in place of `off`.  `ExplainAsync` plans, and when `PostgresLive.PrunedByTheExecutor(plan)` (a node of a plan `PlanNode` reads prints `Subplans Removed` above 0), sends `AgainTexts` (`SET LOCAL enable_partition_pruning = off`, `DISCARD PLANS`) and plans again, then rolls back to the savepoint as before.  Remarks rewritten.
2. Tests: `PostgresLiveTests` for `SetTexts`, `AgainTexts` and `PrunedByTheExecutor`; `ExplainTests` on a server for a parameter of the key (every member, pruning left on), a stable function of the key, and a constant of the key (the pruned plan stands); a new `Describer/PartitionPruningTests` that tries to break a proof through a pruned plan with `ProvedColumns`, over a partitioned table whose partitions differ in `NOT NULL`.
3. Keep the guard: `PlanNode.SubplansRemoved` still keeps an `Append` that prints it from proving, for an older capture and for a second plan that prints it.
4. Re-record PostgreSQL fixtures (`tools/record-fixtures.sh postgres`) and sidecars (`tools/describe-end-to-end.sh`), and read the diff; check `RealisticNullabilityNoiseTests`' `Budgets`.
5. Docs: rewrite TD-0052, its row, its limit `limit-many-partitions` and its row of `site/guides/troubleshooting.md` to what remains, a key compared with a parameter or a stable function, planned twice with every partition; `docs/architecture/postgres-nullability.md` (the plan's steps, the partitions' rows, set operations); the manual's "What describing does to your database" for `DISCARD PLANS`, by the answer to Q1.
