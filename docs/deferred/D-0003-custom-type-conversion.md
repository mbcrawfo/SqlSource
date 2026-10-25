# D-0003 - Sending the types the driver reads as no C# type

## Planned

The [epic design](../superpowers/specs/2026-10-07-query-generation-epic-design.md) plans a project-level type override (phase 5), map rows that convert a value with a read and a write expression (phase 7), and binding a parameter by its type's `bindName` from the sidecar (phase 8).  The [fix of the gaps of the check against Npgsql](../superpowers/specs/2026-10-15-td-0045-npgsql-check-gaps-design.md) recorded that nothing plans a conversion a project declares for a type the driver cannot write.  [Sub-phase 8.2](../superpowers/specs/2026-10-24-query-generation-phase-8-2-converters-design.md) delivered that conversion for every type the base map gives a C# type, and left out, as a non-goal, making a composite, a table's row type or another type that `SQLSRC232` refuses bindable.

## Delivered instead

Converters, sub-phase 8.2: a static method marked `[SqlSourceConverter]`, read by [`ConverterReader`](../../src/SqlSource/Generation/Converters/ConverterReader.cs) and chosen by [`ConversionPlanner`](../../src/SqlSource/Generation/Methods/ConversionPlanner.cs), converts a type override's C# type to and from the type the base map reads the database type as, for the columns and parameters of the generated query methods.  Its database side is the base map's type exactly, so it serves every database type the base map gives a C# type, and the manual documents it under [converters](../manual/settings.md#converters).

The database types the base map gives none have nothing to convert from:

- **A parameter** of a composite type, a table's row type, `regclass` and the other types of the system catalogs that Npgsql does not load, or a type whose schema or name holds a `.`, is still [`SQLSRC232`](../manual/diagnostics.md#sqlsrc232), raised by [`PostgresErrors`](../../src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs) when the tool describes the query.  The workaround is a `text` parameter with a cast in the SQL, and the tool never rewrites a user's SQL to make a parameter bindable.
- **A column** of such a type, mapped by the type override to a type that has converters, is [`SQLSRC279`](../manual/diagnostics.md#sqlsrc279) at its query, in the form that says the database type has no C# type to convert from.  Mapped to a type with no converter, it is read with `GetFieldValue<T>` of the override's type, which works when the application's data source maps the type, as with `MapComposite<T>()`.

## Why deferred

A converter's database side is the type the driver reads, by the 8.2 spec's decision, so that the read stays `GetFieldValue<B>` and the binding the base map's own.  A composite has no such type: binding one needs either the driver's own mapping, which the generator and the tool's check cannot see, or a type sent in its place with a cast in the SQL.  Either is a design of its own, across the generator, the binders of 8.3 and the tool's check, which 8.2 did not take on.

## Remaining work

A way for a query method to send a parameter of a type that the base map reads as no C# type, on PostgreSQL:

- **What is sent**: the override's type, trusting the data source's `MapComposite<T>()` as the methods trust `MapEnum<T>()` for a column, with the parameter's `DataTypeName` from the sidecar; or a converter to a type the driver binds, `string` for one, with the cast to the database type that the SQL needs.
- **The tool's check**: it binds each parameter by its type's name, and refuses these types as `SQLSRC232` before it writes the sidecar.  It must learn which parameters it may describe anyway, and what it binds them by.
- **The diagnostics and the manual**: `SQLSRC232` and the second form of `SQLSRC279` narrowed to what stays refused, and the types listed in [`docs/manual/diagnostics.md`](../manual/diagnostics.md#sqlsrc232) changed to match.

## Trigger

A user's report of `SQLSRC232` for a composite type, or the binding by `bindName` of sub-phase 8.3, if it gives a way to send a composite by its name.
