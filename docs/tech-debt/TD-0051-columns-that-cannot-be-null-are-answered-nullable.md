# TD-0051 - The PostgreSQL describer answers nullable or unknown for many columns that cannot be NULL

## Problem

The realistic corpus, [`tests/SqlSource.Tests.Postgres/Realistic/`](../../tests/SqlSource.Tests.Postgres/Realistic/), holds a truth for each column of each case, written by hand from the SQL and the schema.  Of the columns that truth says can never be `NULL`, the describer answers about half `true` or `null`: 94 on PostgreSQL 14 and 95 on 18, of about 200, when the suite landed.  [`RealisticNoiseBudgetTests`](../../tests/SqlSource.Tests.Postgres/Realistic/RealisticNoiseBudgetTests.cs) holds the count of each major version, in its `Budgets` by the major, and fails when it moves either way; the cases each kind is seen in are in their `expected-pg14.txt` and `expected-pg18.txt`.  The noise is of these kinds:

- **An expression, which has no origin in the row description**, and is `null` with `no-origin`: `count(*)` (`group-by-having`, `count-filter-statuses`), `coalesce(...)`, `CASE ... ELSE ... END` and `greatest(...)` over values that are not `NULL` (`coalesce-case-greatest`), a literal (`union-branch-literal`, `coalesce-case-greatest`), a window ranking, `row_number()`, `rank()` (`running-total-lag-rank`), `x IS NULL` (`coalesce-case-greatest`), and `jsonb_build_object(...)` (`json-build-and-agg`).
- **A set operation or a recursive CTE.**  A column of a `UNION`, an `INTERSECT`, an `EXCEPT` or a recursive CTE has no origin, so it is `null` even where every branch's column is `NOT NULL` (`union-all-activity-feed`, `intersect-accounts-with-orders-and-events`, `except-accounts-without-reservations`, `recursive-category-tree`).
- **`EXISTS (...)` as a column** (`exists-as-column`), which is never `NULL`.
- **An aggregate other than `count`** that cannot be `NULL` where it is: a `sum` or a `max` of a `NOT NULL` column over a group that has a row, `array_agg` and `string_agg` of an inner join's group (`array-and-string-agg`, `group-by-having`, `cte-chain`).
- **`VALUES`** (`values-statement`, `insert-multirow-values`), whose rows the plan does not print.
- **A strict function or operator over values that are not `NULL`**, `quantity * unit_price` (`order-report-four-tables`), `date_trunc(...)` (`aggregate-cte-joined-inner`), the range intersection `r.during * tstzrange(@from, @to)` (`reservations-in-range`), and a function that never returns `NULL` where it is called, `merge_action()` (`merge-returning`).
- **The walk's gaps**, [TD-0046](TD-0046-the-walk-leaves-some-columns-to-the-heuristic.md): a subquery the planner keeps apart (`row-number-dedupe`), a CTE used twice (`cte-used-twice`), and a partitioned table (`partition-preserved-side`, `jsonb-containment-and-key`, `jsonb-array-elements`) keep the heuristic's `true`.

A few columns of the corpus are noise of other kinds, which the design below does not take up: a column of a view is `null` with `view` (`view-joined-left`), and a `RETURNING` column of a nullable table column is the catalog's `true` where the statement writes a value that is not `NULL` (`insert-select-returning`, `update-from-aggregate-returning`, `data-modifying-cte-archive`).

## Why it exists

The describer answers a column from the catalog, through its origin, and from the plan's walk where a gate asks for one; an expression, a set operation's column and a column of a view have no origin, and the walk maps only references to relation instances.  Noise is safe: a column the describer cannot prove is not null is nullable, and the epic's rule is that a false nullable is a nuisance and a false non-nullable a silent wrong value.  The suite of realistic queries was built to measure the noise before any work lowers it.

## Impact

Medium.  Nothing is wrong, but on realistic SQL about half of the properties that could be non-nullable have a `?`, each a `!` or a check a user adds by hand; it is the most visible way the describer disappoints.  The count grows with every new case of a kind above.

## Proposed fix

[`docs/superpowers/specs/2026-10-15-postgresql-nullability-noise-design.md`](../superpowers/specs/2026-10-15-postgresql-nullability-noise-design.md): part A answers expressions from the plan's `Output` (`count`, window rankings, constants, `IS NULL`, `COALESCE`, `CASE`, `GREATEST`, `LEAST`), part B is TD-0046's own fix of the walk's gaps, and part C answers a set operation's column from its branches and a recursive CTE by induction.  Each part lowers the budgets of 14 and 18 and updates this item to hold what remains.  What the design leaves, strict functions and operators, `EXISTS` as a column, aggregates other than `count`, `VALUES`, a column of a view and a `RETURNING` column of a written value, stays here with a proposal of its own when one is found.

## Trigger

Part A of the design, which is planned after the suite; a user's report of a model with nullable properties that need not be.
