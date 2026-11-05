# TD-0102 - A list negated without NOT is trusted to filter nulls

## Problem

A list is described as one value, `o.id IN (@ids)`, which PostgreSQL's planner reads as the strict `o.id = $1`: a `WHERE` that holds it makes a `LEFT JOIN` whose optional side `o` is inner, and the plan walk proves that side's `NOT NULL` columns not null.  As the query runs, an empty list is `o.id IN (SELECT @ids_0 WHERE 1 = 0)`, which is `FALSE` for a `NULL` `o.id` where the one value gives `NULL`.  Wherever the query turns that `FALSE` into a true condition, the rows that the outer join filled with null come back.

[`SqlListUses`](../../src/SqlSource/Parsing/SqlListUses.cs) reads one such case, a `NOT` before the list, as `SqlParameterUse.NegatedList`, and for a query with one the walk trusts no join that the planner made inner, left or right (`WalkScope.DistrustsJoinReduction`).  It does not read the others: `(o.id IN @ids) IS FALSE`, `IS NOT TRUE`, `= false` or `<> true`, `CASE WHEN o.id IN @ids THEN false ELSE true END` and `COALESCE(o.id IN @ids, false) = false`.  The planner reduces the join for `IS FALSE` and `= false`, and a column of the optional side is recorded as not null although an empty list returns it `NULL`.  The generated reader then throws.

## Why it exists

`NOT` is how a query negates a list almost every time, and `SqlListUses` already reads the words around a list.  Reading every boolean context that inverts a test would need an expression parser of each dialect's precedence, which the parse does not have: the word scan sees no structure.

## Impact

Low.  It needs a PostgreSQL query with an outer join and a list negated by one of those forms, called with an empty or a null list, where the negated condition is on the optional side.  The manual's known issue says to write the test with `NOT`, or to mark the column `null` with a `-- column:` marker.

## Proposed fix

Either read the forms too, by the words after a list use (`IS FALSE`, `IS NOT TRUE`) and around the parenthesis that holds it (`=`, `<>`, `THEN`, `ELSE`, a call), and mark such a use `NegatedList`; or read every list use that is not directly a conjunct of a `WHERE`, an `ON` or a `HAVING` as negated, which over-marks but never misses.  Add `SqlListUsesTests` rows for each form, and a describer test in `tests/DatabaseTests/Postgres/Describer/WalkTests.cs` beside `DescribeAsync_LeftJoinThatANegatedListMakesInner_IsNullableThroughout`, then remove the known issue from `docs/manual/postgresql.md` and the `TD-0102` rows of `SqlListUsesTests`.

## Trigger

A report of a method that throws on an empty list in such a query, or the next change to how `SqlListUses` reads the words around a list.
