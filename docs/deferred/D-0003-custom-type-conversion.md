# D-0003 - Custom type conversion

## Planned

The [epic design](../superpowers/specs/2026-10-07-query-generation-epic-design.md) plans a project-level type override (phase 5), map rows that convert a value with a read and a write expression (phase 7), and binding a parameter by its type's `bindName` from the sidecar (phase 8).  It says a type the driver cannot read is a diagnostic that names the type and the override.  It plans nothing for a type the driver cannot *write*, nor a conversion that a user declares for one.

## Delivered instead

[`SQLSRC232`](../diagnostics.md#sqlsrc232), raised by [`PostgresErrors`](../../src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs) for a parameter whose type Npgsql cannot bind: a composite type, a table's row type, `regclass` and the other types of the system catalogs that Npgsql does not load, and a type whose schema or name holds a `.`.  The workaround is a `text` parameter with a cast in the SQL.  The tool never rewrites a user's SQL to make a parameter bindable.

## Why deferred

A general mechanism is a design of its own across three parts of the epic, larger than the [fix of the gaps of the check against Npgsql](../superpowers/specs/2026-10-15-td-0045-npgsql-check-gaps-design.md), which recorded the limit.

## Remaining work

A way for a project to declare a conversion for a database type, applied consistently in three places:

- **parameters**: what C# type a parameter of that database type takes, and what the driver is sent, such as text, or a type the driver can bind, possibly with a server-side cast or the driver's own mapping (`MapComposite`);
- **models**: what C# type a column of that type maps to;
- **generated query code**: how a value is bound and read.

It must work per engine, and say what the tool's `check` binds by when a conversion applies.  The types it would cover today on PostgreSQL are the ones still refused by `SQLSRC232`, listed in [`docs/diagnostics.md`](../diagnostics.md#sqlsrc232).

## Trigger

Phases 5, 7 and 8, whichever designs the override first; or a user's report of `SQLSRC232` for a composite type.
