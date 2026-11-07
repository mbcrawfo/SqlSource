# TD-0114 - An empty list fails in a PostgreSQL index predicate

## Problem

A query method sends an empty or a null list as one parameter in a subquery that holds no row, `IN (SELECT @kinds_0 WHERE 1 = 0)` ([`ListSqlWriter`](../../src/SqlSource/Generation/Emitting/QueryMethods/ListSqlWriter.cs), `EmptyOf`).  PostgreSQL refuses a subquery in the index predicate of `INSERT ... ON CONFLICT (...) WHERE`, "cannot use subquery in index predicate" (SQLSTATE `0A000`), on 14 and 18.  So a query such as `INSERT ... ON CONFLICT (sku) WHERE (@kinds IS NULL OR kind IN @kinds) DO NOTHING`, with or without the null test, builds and describes, and its method throws `PostgresException` for an empty or a null list, a null one on a property that cannot be null included, which is sent as empty, while every list that holds a value runs.

## Why it exists

The empty form must be a subquery wherever a list stands, since no scalar form keeps `NOT IN` true for every row ([TD-0106](TD-0106-an-empty-list-fails-in-a-sql-server-aggregate.md) has the same cause on SQL Server).  Writing another form in an index predicate needs the parser to tell the generator that a use stands there, and the owner chose to record the limit and document the workaround instead.  A list there does nothing useful: a predicate that holds a parameter cannot be proved to imply a partial index's own predicate, so it never lets PostgreSQL infer a partial unique index ("there is no unique or exclusion constraint matching the ON CONFLICT specification"), and with an index that is not partial the predicate is not needed.

## Impact

Low.  It needs a PostgreSQL `codegen` query with a list in the index predicate of `ON CONFLICT`, called with an empty or a null list.  The manual's known issue gives the workaround: leave the list out of the index predicate, and write there the partial index's own predicate, if it has one.

## Proposed fix

Mark a list use that stands in the index predicate of `ON CONFLICT (...) WHERE`, from the words before it in `SqlListUses`, as `SampleSql.IndexPredicates` finds the predicate in the tool, and have the method write its empty form there without a subquery, such as `IN (NULL)`: the predicate chooses the index a conflict is found by, and selects no row.  Or report such a use at parse time with a diagnostic that names the workaround.  Then add an end-to-end query that runs with an empty and a null list, and remove the known issues from `docs/manual/postgresql.md` and `docs/manual/parameters-and-tokens.md`.

## Trigger

A report of the `PostgresException`, or the next change to how the parser reads where a list stands.
