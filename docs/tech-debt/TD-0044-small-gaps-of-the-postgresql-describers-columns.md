# TD-0044 - Small gaps of the PostgreSQL describer's columns

## Problem

Seven things that the PostgreSQL describer records of a column are less than the server knows, or more than it promises:

- **The modifier of a type outside `pg_catalog`.**  [`ColumnFacets`](../../src/SqlSource.Tool/Describing/Postgres/ColumnFacets.cs) reads a modifier for the ten built-in types that carry one, by internal name.  A type of an extension has its own: PostGIS's `geometry(Point,4326)` is recorded as `public.geometry`, with no facet.
- **The facets of an array's element.**  A column of `character varying(10)[]` is recorded as `character varying[]`, and its `element` has no `length`.  Npgsql gives the array the numbers of its element, and the describer drops them.
- **The fields of an interval.**  `interval year to month` is recorded as `interval`, and `interval day to second(3)` as `interval(3)`.  Npgsql gives an interval's precision and nothing of its fields.
- **A foreign table says `view`.**  PostgreSQL does not enforce the constraints of a foreign table, whose `attnotnull` is a declaration about the remote, so [`ColumnNullability`](../../src/SqlSource.Tool/Describing/Postgres/ColumnNullability.cs) answers `null` for its columns, with the source `view` for want of a word of its own.
- **A `NOT NULL` that is `NOT VALID`.**  PostgreSQL 18 lets a `NOT NULL` constraint be added `NOT VALID`.  It sets `attnotnull`, and rows that were there before may hold `NULL`.  The describer says `nullable` `false` with `catalog`.
- **A table that goes between `columns` and `tables`.**  [`ColumnOrigin.Find`](../../src/SqlSource.Tool/Describing/Postgres/ColumnOrigin.cs) throws when the answer of `tables` lacks a table or an attribute that `columns` named, as a bug of the describer.  Another session that drops the table, or the column, between the two calls causes it, and a throw ends the run as `SQLSRC200`.
- **A domain's own modifier, for a parameter.**  A column of `email_t`, a domain over `varchar(254)`, is recorded with a `base` of `character varying(254)`, and a parameter of `email_t` with a `base` of `character varying`, with no length.  A column's facets come from the row description, which gives a domain column the domain's modifier; a parameter has no row description, and `types` does not read `pg_type.typtypmod`.

## Why it exists

The first three: Npgsql does not expose a column's raw type modifier, only `ColumnSize`, `NumericPrecision` and `NumericScale`, which are the modifier decoded for the types Npgsql knows.  The spec of sub-phase 3.3 chose them over a second describe with `KeyInfo`, which costs a query of `pg_attribute` for each query described.  The fourth: the sidecar format's provenance values have none for a foreign table, and a new value is a change of the format.  The fifth was found while the catalog query was written, on a server that few projects run yet.  The sixth: the plan chose a throw, since the two answers come from one server within one describe, and a bug of the describer is what a mismatch would mean.  The seventh: a parameter has no facets, since the protocol gives it an OID and no modifier, and `types` reads no `typtypmod`; a column has the domain's modifier only because Npgsql's numbers come from its row description.

## Impact

Low.  The describer is registered nowhere until sub-phase 3.6.  The first three lose detail that the generated code does not need to read a value: a `geometry` is read as one whatever its modifier, and a C# type has no length.  They matter to a tool that would compare a sidecar with a schema.  The fourth gives a foreign table's column the right answer, unknown, under a word that is not quite true.  The fifth is the one that can be wrong: a column recorded as not nullable that holds `NULL` gives a generated model that throws when it reads the row.  The sixth needs a schema change in the same moment as a run: it is rare, and it ends the run with `SQLSRC200`, which says the tool has a bug, where `SQLSRC225` would say what happened.  The seventh loses the length that a domain gives a parameter, which a generated model does not read; it makes a parameter and a column of one domain differ in the sidecar.

## Proposed fix

For the first three, read each column's modifier from the server: `tables` already reads `pg_attribute`, and `atttypmod` with `format_type(atttypid, atttypmod)` gives the modifier and its spelling for every column that has an origin; a column without one keeps Npgsql's numbers.  For the fourth, add a provenance value, `foreign-table`, to the format's list, which a reader keeps when it does not know it.  For the fifth, read `pg_constraint.convalidated` for the `NOT NULL` constraints of the tables asked for, on PostgreSQL 18 and later, and answer a column whose constraint is not validated as `true` with `catalog`.  For the sixth, answer it as `SQLSRC225` at the step `catalog`, which needs `ColumnOrigin.Find` to return an `Outcome`.  For the seventh, read `typtypmod` in `types` and give a domain's base its facets from it wherever the domain is built, a parameter included.

## Trigger

A user who needs the modifier of an extension's type, an array's element or a domain's parameter in the sidecar; phase 5, whose models read `nullable`, for the fifth; sub-phase 3.6, which gives the describer to users, for the sixth.
