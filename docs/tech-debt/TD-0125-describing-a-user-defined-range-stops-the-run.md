# TD-0125: Describing a column of a user-defined range stops the run

## Problem

[`PostgresLive.ColumnsAsync`](../../src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs) reads a query's columns with `GetColumnSchemaAsync`.  Npgsql throws `NotSupportedException` there for a column of a range that it has no mapping for, unless the data source opted in with `EnableUnmappedTypes`, and the [data source the tool builds](../../src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs) does not.  The exception is not turned into a describe failure, so the whole run ends with `SQLSRC200`, against the rule of [`src/SqlSource.Tool/Describing/AGENTS.md`](../../src/SqlSource.Tool/Describing/AGENTS.md) that a describer returns a failure and never throws.

A review of TD-0114 found it and ran it.  It was then reproduced on `postgres:18.6` with the tool of that branch, whose describer is `main`'s:

```sql postgres
CREATE TYPE public.floatrange AS RANGE (subtype = float8);
CREATE TABLE r (span public.floatrange);
SELECT span FROM r;
```

`sqlsource describe` printed `error SQLSRC200: sqlsource failed unexpectedly: System.NotSupportedException: Reading and writing unmapped ranges and multiranges requires an explicit opt-in`, exited 1 and wrote no sidecar.  The same query with `span::text`, or with `lower(span)` and `upper(span)`, is described.  A multirange was not run, though Npgsql's message names it too.

## Why it exists

No test describes a user-defined range on a real server.  The tests of [`PostgresTypeMap`](../../src/SqlSource/Generation/Types/Maps/PostgresTypeMap.cs) and of `SQLSRC246` hand the generator a sidecar that holds such a type, so the path through the driver is not run.

## Impact

Medium.  A project with a column of a user-defined range cannot describe that query, and the failure stops the rest of the run.  The help of `SQLSRC246` for such a range, which says to map it to `NpgsqlRange<T>`, is out of reach through `describe`.  Casting the column to `text`, or selecting its bounds, is the workaround.

## Proposed fix

Make the describer read such a column.  Either call `EnableUnmappedTypes()` on the tool's data source builder, or read the columns from the row description without `GetColumnSchemaAsync`.  Check that no other type's description changes.  Add a range and a multirange of a user's subtype to the describer's fixtures, recorded by `tools/record-fixtures.sh postgres`, and an end-to-end test that the query reaches `SQLSRC246` and that the help's mapping then builds and reads the value.  Remove the limit from `site/reference/databases/postgresql/types.md` and its row from `site/guides/troubleshooting.md`.

## Trigger

A user reports it, or the next change to how the PostgreSQL describer connects or reads columns.
