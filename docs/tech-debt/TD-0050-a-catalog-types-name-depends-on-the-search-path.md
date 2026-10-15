# TD-0050 - The name of a type of `pg_catalog` that has no modifier depends on the session's `search_path`

## Problem

The PostgreSQL describer names a type of `pg_catalog` that has no modifier by `format_type(oid, -1)`, the `FormatType` of a row of `types` ([`PostgresTypeNames`](../../src/SqlSource.Tool/Describing/Postgres/PostgresTypeNames.cs), `TypesSql` in [`PostgresLive`](../../src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs)).  `format_type` writes a type by the session's `search_path`: when a type of the same name stands before `pg_catalog` on it, the catalog's type is qualified.  Seen on PostgreSQL 18 and 14: with a domain `public.text` and `Search Path=public,pg_catalog`, `SELECT NULL::pg_catalog.text` is named `pg_catalog.text` and not `text`.

So such a name depends on the session, against the remark of `PostgresTypeNames` that the name of a type does not, and the sidecar of one query differs between two databases, or two connection strings, that differ only in a user's type named like a built-in.

A type of `pg_catalog` that takes a modifier is not affected: `format_type` special-cases every one of them and never qualifies them.  The container test `Describe_BuiltInTypeHiddenOnTheSearchPath_IsStillTheCatalogsSpelling` in [`ColumnTests`](../../tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs) guards `numeric(10,2)` under a domain named `numeric`.

## Why it exists

The rule that a type of `pg_catalog` is the server's text, and that one of any other schema is the tool's `schema.name`, was written for the modifier, and the question of a built-in hidden by a user's type was not asked until the modifier's spelling was tested against it.  The gap is older: `format_type(oid, -1)` has been the name of every type of `pg_catalog` since sub-phase 3.2.

## Impact

Low.  It needs a user type named like a built-in, `text`, `int4`, `date`, and placed before `pg_catalog` on the `search_path` of the describe's connection.  A schema that does so is rare, and the name then reads `pg_catalog.text`, which is correct but not the spelling of another database.

## Proposed fix

Spell a type of `pg_catalog` without regard to the session.  One way is to strip a leading `pg_catalog.` from `FormatType` for a row whose `nspname` is `pg_catalog`.  Another is to run the `types` query, and `spellings`, under a fixed `search_path`, `SET LOCAL search_path = pg_catalog` inside a transaction.  The first is smaller; the second is the server's own spelling of any name.  Either changes captures, which are a contract: a fixture recorded under a hiding type holds the qualified name, so the fix needs a case in the container tests for each of PostgreSQL 14 and 18 and a re-recording of any capture that holds one.

## Trigger

A user's report, or any other work on the names of types.
