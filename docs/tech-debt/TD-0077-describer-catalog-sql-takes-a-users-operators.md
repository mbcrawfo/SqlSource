# TD-0077 - The PostgreSQL describer's catalog SQL takes a user's operator of the same name

## Problem

The catalog calls of the PostgreSQL describer, `prepare`'s read of `pg_prepared_statements`, `tables`, `relations`, `partitions`, `types` and `spellings` ([`PostgresLive`](../../src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs)), run on the describe's connection, under the user's `search_path`.  Every type and function their SQL names is written with `pg_catalog.`, but their operators are not: `=`, `<>`, `>` and `= ANY` are found by the `search_path`.  An operator of a user's on the same argument types, in a schema that stands before `pg_catalog` on the path, is taken instead of the catalog's.  Seen on PostgreSQL 18: with `shadow.= (oid, oid)` that answers false and `Search Path=shadow,pg_catalog`, `SELECT count(*) FROM pg_catalog.pg_type AS t WHERE t.oid = 25::pg_catalog.oid` is 0.

So such an operator changes the catalog's answers: `types` answers no row and the describe fails with `SQLSRC225`, or `tables` answers too few rows and a column loses its origin.

## Why it exists

The SQL was written to name its functions with `pg_catalog.`; its casts were not, until a user's domain named `text` was found to break `::text`, and they were qualified.  Writing every operator as `OPERATOR(pg_catalog.=)` makes the SQL hard to read, and the case needs a user's operator on a system type such as `oid`, `name` or `"char"`.

## Impact

Low.  It needs a user's operator on the exact argument types of a system column, in a schema placed before `pg_catalog` on the `search_path` of the describe's connection.  The describe then fails, or a column is described without its origin, and nothing says why.

## Proposed fix

Run the catalog calls under a fixed path: `SET LOCAL search_path = pg_catalog, pg_temp` inside a transaction around each, `pg_temp` named last since an unlisted one is searched first for types.  The `PREPARE`, `columns`, `explain` and `check` keep the user's path.  Or write each operator of those texts as `OPERATOR(pg_catalog.op)`.  Either needs a container case on PostgreSQL 14 and 18 with a user's `=` on `oid` before `pg_catalog`; the requests and answers of the calls do not change, so no capture is recorded again.

## Trigger

A user's report, or any other work on the SQL of the catalog calls, such as a pooler's transaction (TD-0049).
