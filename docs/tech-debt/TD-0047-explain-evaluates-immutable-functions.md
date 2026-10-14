# TD-0047 - Planning a gated query can run a user's function

## Problem

The PostgreSQL describer asks for the generic plan of a gated query with `EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE`, without `ANALYZE`, in a transaction it rolls back ([`PostgresLive.ExplainAsync`](../../src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs)).  The statement is planned and not executed, but the planner folds a call of an `IMMUTABLE` function whose arguments are constants into its value, and to do that it runs the function.  A function that the database marks `IMMUTABLE` wrongly therefore runs during the describe, and an effect of it outside transaction control survives the rollback: a sequence that advanced, a `pg_sleep`, a file written by `COPY`, a call through `dblink`.

The adversarial review of #49 showed it through the describer on 18, and a probe of 18 shows it alone:

```sql
CREATE SEQUENCE probe_seq;
CREATE FUNCTION probe_bump(i integer) RETURNS bigint LANGUAGE sql IMMUTABLE AS $$ SELECT nextval('probe_seq') + i $$;
CREATE TABLE probe_t (id integer NOT NULL);
PREPARE s1 (integer) AS
    SELECT t.id, probe_bump(1) FROM probe_t t LEFT JOIN probe_t u ON u.id = t.id WHERE t.id = $1;
BEGIN;
SET LOCAL plan_cache_mode = force_generic_plan;
EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE s1(NULL);  -- the Output holds '2'::bigint
ROLLBACK;
SELECT last_value, is_called FROM probe_seq;      -- 1, true: the sequence advanced
```

## Why it exists

The nullability walk needs a plan, and planning folds immutable functions; nothing in `EXPLAIN` turns that off.  Sub-phase 3.5 is the first that plans: `PREPARE` parses and analyses the statement and does not plan it, and the describe of `columns` runs nothing.

## Impact

Low.  Only a function that the user's own database marks immutable wrongly can run, and only for a gated query that calls it with constant arguments.  The released tool describes `postgres` since sub-phase 3.6, and [its readme](../../src/SqlSource.Tool/README.md) says that planning a query with one of the gate's words runs such a function.

## Proposed fix

None in code that keeps the walk: the walk needs the plan, and the planner gives no plan without folding.  A user whose database has such a function with an effect can describe against a database where it is harmless, a copy of the schema for one.

## Trigger

A user's report of an effect that a describe left; a version of PostgreSQL whose `EXPLAIN` can plan a statement without folding its immutable functions.
