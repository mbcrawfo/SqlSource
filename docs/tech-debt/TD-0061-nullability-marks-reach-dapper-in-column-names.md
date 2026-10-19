# TD-0061 - The `!` and `?` of a column's name reach Dapper, which maps no such column

## Problem

A column whose name ends in `!` or `?` sets the nullability of its property, and the mark is left out of the property's name, by [`PropertyNames.PascalCase`](../../src/SqlSource/Generation/Models/PropertyNames.cs) and the nullability rule of [`ModelReader`](../../src/SqlSource/Generation/Models/ModelReader.cs).  The column itself keeps the mark: the query returns a column named `total!`, and the generated model has a property `Total`.

Dapper maps a column to a constructor parameter or a property by name, ignoring case and, with `DefaultTypeMap.MatchNamesWithUnderscores`, underscores, and never strips a `!` or a `?`.  So for a positional record, the default shape, Dapper finds no constructor whose parameters match the columns and throws when it reads the first row; for a class it finds no property for the column and leaves the property at its default, with no error.

The manual recommends the marks, so a user who follows it and reads the rows through Dapper meets this.  It is the known issue "A column whose name ends in `!` or `?` is not read into its model by Dapper" of [the generator page](../manual/generator.md#known-issues), and "A column whose alias ends in `!` or `?` is not read into its model by Dapper" of [the PostgreSQL page](../manual/postgresql.md#known-issues), and a note of the recipe [run the models through Dapper](../manual/recipes.md#models-through-dapper).

## Why it exists

The marks were designed for the generated query methods of phase 8, which read a row's columns by position and so do not care for a column's name.  Until those methods exist, Dapper is the way the manual gives to read rows into the models, and nothing reconciles the two.

## Impact

Medium.  A query that uses a mark fails at run time with Dapper and a record model, or gives a property its default value with a class model, which reads as a value of the row.  The build reports nothing.  The workaround is to alias the column without the mark and take the nullability the description gives, or to read the row with one's own code.

## Proposed fix

Give the Dapper user a way to read such a column.  Options: a generated `Dapper.SqlMapper.ITypeMap` or a `[Column]`-style map for each model whose columns carry a mark, registered by a generated method the user calls once; or a generated constructor attribute or a static factory that maps by position; or deliver phase 8's methods, which read by position, and state in the manual that the marks are for them.  Whichever is chosen, add a test in the end-to-end project that reads a query with a marked column through it.

## Trigger

Phase 8's methods, or a report of a Dapper read failing on a marked column.
