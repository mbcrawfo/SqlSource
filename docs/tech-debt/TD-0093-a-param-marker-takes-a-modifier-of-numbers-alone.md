# TD-0093 - A `-- param:` marker on PostgreSQL takes a type modifier of numbers alone

## Problem

PostgreSQL's reader of a `-- param:` type, [`DeclaredTypeText`](../../src/SqlSource.Tool/Describing/Postgres/Statements/DeclaredTypeText.cs), takes a modifier in parentheses only when it holds digits, commas, spaces and a `-` before a digit (`TryReadModifier`).  A type whose modifier holds words, as a column definition writes PostGIS's `geometry(Point,4326)`, is refused before anything is sent, with `SQLSRC228` and the reason `it is not written as a type name`.  The manual's explanation of that reason says the text is "a type name as a column definition writes one, with at most one modifier in parentheses", which such a type is.

The spike of phase 7 found it ([question 4](../superpowers/specs/2026-10-26-query-generation-phase-7-spike.md#4-postgis)).

## Why it exists

The reader keeps the text it sends to `PREPARE` narrow, so that nothing a `.sql` file holds can make the describer run a statement; a modifier of numbers was all the base map's types needed.  A parameter carries no modifier in PostgreSQL, so `-- param: @v geometry` gives the parameter the same type, and the owner chose on 2026-10-30 to record the limit rather than widen the grammar in phase 7's spike.

## Impact

Low.  A user who copies a column's type with a modifier of words into a `-- param:` marker gets `SQLSRC228` and must write the type without its modifier.  The parameter's type is the same either way.  It reaches PostGIS's types above all, which phase 7 sub-phase 7.4 makes usable as parameters, and any other extension type whose modifier holds words.

## Proposed fix

Let `TryReadModifier` also take a modifier of identifiers and numbers separated by commas, each identifier bare (`[A-Za-z_][A-Za-z0-9_]*`), as `geometry(Point,4326)` and `geography(PointZM,4326)` are, still refusing a quote, a `;`, a comment and a nested parenthesis, so that the text sent to `PREPARE` stays a type name.  Check it against PostGIS on PostgreSQL 14 and 18 with the describer's container tests, and add cases to the PostgreSQL side's `DeclaredTypeTextTests`: a modifier of words, of words and numbers, and the refused forms.  Then remove the known issue from [`docs/manual/parameters-and-tokens.md`](../manual/parameters-and-tokens.md) and the sentence on it from `SQLSRC228` in [`docs/manual/diagnostics.md`](../manual/diagnostics.md).

## Trigger

A user's report, or the next change to PostgreSQL's `DeclaredTypeText`.
