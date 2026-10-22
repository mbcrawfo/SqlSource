# D-0003 - Custom type conversion

## Planned

The [epic design](../superpowers/specs/2026-10-07-query-generation-epic-design.md) plans a project-level type override (phase 5), map rows that convert a value with a read and a write expression (phase 7), and binding a parameter by its type's `bindName` from the sidecar (phase 8).  It says a type the driver cannot read is a diagnostic that names the type and the override.  It plans nothing for a type the driver cannot *write*, nor a conversion that a user declares for one.

## Delivered instead

[`SQLSRC232`](../manual/diagnostics.md#sqlsrc232), raised by [`PostgresErrors`](../../src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs) for a parameter whose type Npgsql cannot bind: a composite type, a table's row type, `regclass` and the other types of the system catalogs that Npgsql does not load, and a type whose schema or name holds a `.`.  The workaround is a `text` parameter with a cast in the SQL.  The tool never rewrites a user's SQL to make a parameter bindable.

Sub-phase 5.2 delivered the [type override](../manual/settings.md#setting-type-overrides), `SqlSourceTypeOverrides` and `-- type-override:`, which gives any database type, a composite included, the C# type of a project's choice for the models of sub-phase 5.3.  It is read by [`TypeOverrideGrammar`](../../src/SqlSource/Settings/TypeOverrides/TypeOverrideGrammar.cs) and applied by [`QueryTyper`](../../src/SqlSource/Generation/Types/QueryTyper.cs).  It names a C# type and nothing else: no conversion, and no change to what is sent, so a parameter that `SQLSRC232` refuses is still refused.

## Why deferred

A general mechanism is a design of its own across three parts of the epic, larger than the [fix of the gaps of the check against Npgsql](../superpowers/specs/2026-10-15-td-0045-npgsql-check-gaps-design.md), which recorded the limit.

## Remaining work

A way for a project to declare a conversion for a database type, applied consistently in three places:

- **parameters**: what C# type a parameter of that database type takes, and what the driver is sent, such as text, or a type the driver can bind, possibly with a server-side cast or the driver's own mapping (`MapComposite`);
- **models**: how a column's value is converted to the C# type that the type override gives it, when the driver cannot read it as that type;
- **generated query code**: how a value is bound and read.

What is left is the conversion: a read and a write expression, or a type the driver binds in place of the database type, which the type override's grammar leaves room for after an entry.  It must work per engine, and say what the tool's `check` binds by when a conversion applies.  The types it would cover today on PostgreSQL are the ones still refused by `SQLSRC232`, listed in [`docs/manual/diagnostics.md`](../manual/diagnostics.md#sqlsrc232).

## Trigger

Phase 7, whose converted types add read and write expressions to the map, or phase 8, which binds parameters; or a user's report of `SQLSRC232` for a composite type.
