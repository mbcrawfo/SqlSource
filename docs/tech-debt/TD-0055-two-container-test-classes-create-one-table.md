# TD-0055 - Two container test classes create one table

## Problem

A maintainability review of phase 3 on 2026-10-16 found that [`WalkTests`](../../tests/SqlSource.Tests.Postgres/Describer/WalkTests.cs) and [`ExpressionTests`](../../tests/SqlSource.Tests.Postgres/Describer/ExpressionTests.cs) each hold the same `CREATE TABLE IF NOT EXISTS walk_parts ...` with its two partitions, against the one server of the `UsesPostgres` collection.  Whichever class runs first defines the table; the other's text is never run.  The same partitioned shape is made three more times under other names, `plan_parts` in `PlanOutputTests`, `scan_parts` in `ScanTests` and `setop_parts` in `SetOperationTests`, and `ExpressionTests` reads `PlanOutputTests.ShadowCount`.

## Why it exists

`ExpressionTests` was written after `WalkTests` and copied its table, and the two are the same today.

## Impact

Low today.  An edit to one copy, a column added for a new test, passes or fails by the order the classes run in, which is not fixed, so the failure comes and goes.

## Proposed fix

One `ScratchSchema` class in `tests/SqlSource.Tests.Postgres/Describer/` holding the scratch tables' text as constants, one name per table, each made by the class that needs it from that constant; `ShadowCount` moved there too.

## Trigger

The first change to `walk_parts` in either class.
