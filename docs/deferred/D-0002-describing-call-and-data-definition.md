# D-0002 - Describing CALL and data definition statements

## Planned

The [epic design](../superpowers/specs/2026-10-07-query-generation-epic-design.md) describes queries that a user keeps in `.sql` files.  It puts stored procedures out of scope, in its list of what is out of scope, and phase 8 generates a `rowcount` method for a statement without rows and always uses `CommandType.Text`, so a `CALL` could have a method.

## Delivered instead

[`SQLSRC233`](../manual/diagnostics.md#sqlsrc233), raised by [`StatementShape`](../../src/SqlSource.Tool/Describing/Postgres/Statements/StatementShape.cs) before anything is sent, and `-- output: sql` as the way to keep such a query: its SQL is still a string constant, or a method where the query has tokens, with no types, so the tool does not describe it.  The error still fails the query, so the file's sidecar is held back until the query is mended or given the marker.

## Why deferred

The tool describes a query through `PREPARE`, which cannot take `CALL`, `CREATE`, `EXPLAIN`, `SHOW`, `SET` and the rest.  The other route, a describe of the unprepared statement through Npgsql, has open problems, listed below, and the [design of the fix of the describer's parameter gaps](../superpowers/specs/2026-10-14-td-0043-postgresql-describer-parameter-gaps-design.md) was to make the refusal clear, not to build a second route.

## Remaining work

Describe through the extended protocol's Parse and Describe of the unprepared statement, through Npgsql.  `NpgsqlCommandBuilder.DeriveParameters` on the `@name` SQL gives the parameter types and keeps a domain, and a reader opened with `CommandBehavior.SchemaOnly` gives a procedure's `OUT` and `INOUT` arguments as a row.  To settle:

- A declared `-- param:` type cannot be carried through Parse, which was the epic's objection to `DeriveParameters`, so it must be refused or compared with what the server gives.
- `DeriveParameters` throws an `ArgumentException` for a type that Npgsql did not load.
- An overloaded procedure that is called with an untyped parameter resolves to `text`, without a word.
- There is no plan to walk and no table to match, so the `OUT` columns are nullable.
- The call needs a capture and fixtures of its own.

Of data definition, only `CREATE TABLE AS` and `SELECT INTO` take a parameter; `SET` and `NOTIFY` refuse one, and the rest cannot hold one.

## Trigger

Phase 8, or a user who asks for a method for a `CALL`.
