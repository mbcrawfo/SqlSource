# TD-0104 - A list's null test in an index predicate cannot be described

## Problem

On PostgreSQL, [`SampleSql`](../../src/SqlSource.Tool/Describing/SampleSql.cs) describes a list's null test, `@ids IS NULL`, as `(SELECT NULL) IS NULL`, so that the planner cannot fold the condition and reduce an outer join that the running query keeps.  PostgreSQL refuses a subquery in two places: a `CALL` argument ("cannot use subquery in CALL argument") and the index predicate of `INSERT ... ON CONFLICT (...) WHERE` ("cannot use subquery in index predicate"), on 14, 15 and 18.  A `CALL` is never described, since the tool refuses it before it asks the server ([SQLSRC233](../manual/diagnostics.md#sqlsrc233)), so only the index predicate is affected: such a query fails to describe with the server's error, though the running query, whose null test is a `boolean` parameter, would run.

## Why it exists

The stand-in is what keeps an outer join's nullability right for the common optional filter, and a list tested for null in an index predicate is rare: the predicate names the partial index that the conflict is inferred from.  A retry of the describe with `NULL` in place of the stand-in, when the server refuses the subquery, would need the describer to know of lists, which it does not, or the run to describe twice.

## Impact

Low.  It needs a PostgreSQL query that tests a list for null in `ON CONFLICT (...) WHERE`.  The manual's known issue says to test the list elsewhere, or to give the query `-- output: sql`.

## Proposed fix

When the describe of a query whose sample holds a stand-in fails with SQLSTATE `0A000` and the message of a subquery in an index predicate, describe it once more with `NULL` as the stand-in, in `DatabaseRuns` and `BundleReplay` alike, which build the sample; or have `SampleSql` write `NULL` for a null test that stands inside the parentheses after `ON CONFLICT (...) WHERE`, from the words before it, where no join can be reduced.  Add a describer test of such a query, and remove the known issues from `docs/manual/postgresql.md` and `docs/manual/parameters-and-tokens.md`.

## Trigger

A report of such a query, or the next change to how the sample is built.
