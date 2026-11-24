# TD-0126: An array parameter of a type named with an underscore crashes the tool

## Problem

When a query has a parameter whose type is an array of a user type whose name starts with `_`, `sqlsource describe` dies with a stack overflow in Npgsql's `DataTypeName.FromDisplayName`, reached from [`PostgresLive`](../../src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs).  A stack overflow cannot be caught, so the process ends with exit code 134 and no `SQLSRC` error, against the rule of [`src/SqlSource.Tool/Describing/AGENTS.md`](../../src/SqlSource.Tool/Describing/AGENTS.md) that a describer returns a failure and never throws.

A review of TD-0114 found it.  It was then reproduced on `postgres:18.6` with Npgsql 10.0.3 and the tool of that branch, whose describer is `main`'s, for an enum and for a composite, with no superuser:

```sql postgres
CREATE TYPE public._mood AS ENUM ('a');
CREATE TABLE t (col public._mood[]);
SELECT 1 AS one FROM t WHERE col = @p;
```

A column of that array type is described.  So is the query with `-- param: @p text` and `WHERE col = @p::public._mood[]`.

## Why it exists

No test describes a parameter of an array of a type with such a name on a real server.

## Impact

Medium.  It takes a type whose own name starts with `_` and an array parameter of it, but the whole run dies with no message of SqlSource's, and nothing names the query that caused it.  Declaring the parameter as `text` and casting it is the workaround.

## Proposed fix

Keep the tool from handing Npgsql that type's name.  Find the call in `PostgresLive` that reaches `DataTypeName.FromDisplayName`, and check the array's element name first, so that such a parameter gets the normal `SQLSRC232` for a type that Npgsql cannot bind.  Report the fault to Npgsql if a current version still has it.  Add a test on a real server for an enum and a composite named with `_`.  Remove the limit from `site/reference/databases/postgresql/types.md` and its row from `site/guides/troubleshooting.md`.

## Trigger

A user reports it, or the next change to how the PostgreSQL describer binds parameters.
