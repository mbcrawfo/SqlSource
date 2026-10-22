# TD-0071 - The SQL Server describer answers nullable for columns that cannot be NULL

## Problem

The realistic corpus, [`tests/SqlSource.Tests.SqlServer/Realistic/`](../../tests/SqlSource.Tests.SqlServer/Realistic/), holds a truth for each column of each case, written by hand from the SQL and the schema.  Of the columns that truth says can never be `NULL`, the describer answers 39 nullable on SQL Server 2017 (major 14) and 44 on 2025 (17).  The 39 are the same columns on both; the 5 more of 2025 are of cases of 2022's syntax, which run on 2025 alone.  [`RealisticNullabilityNoiseTests`](../../tests/SqlSource.Tests.SqlServer/Realistic/Describer/RealisticNullabilityNoiseTests.cs) holds the count of each major in its `Budgets` and fails when it moves either way, listing the noisy columns.

The describer records the server's own nullability (`sys.dm_exec_describe_first_result_set`, `nullableSource` `server`) and never narrows it, and the server calls these forms nullable whatever their arguments.  The 44 columns are of these kinds:

- **`COUNT`**: `COUNT(*)` with no `GROUP BY` in `customer-order-counts` `order_count`; `COUNT` over a group in `big-customers-having` `order_count`, `order-skus-string-agg` `line_count`, `category-tree-recursive` `product_count` and `orders-grouping-sets` `order_count` and `shipped_count`; a scalar subquery of `COUNT(*)` in `customers-last-payment-subquery` `order_count`.
- **`SUM`, `MAX` over a group that has a row**, of a column that is not null: `big-customers-having` `total_spent` and `last_ordered_at`, `sales-by-category-rollup` `units` and `revenue`, `top-with-ties-best-sellers` `units_sold` and `revenue`, `orders-grouping-sets` `last_placed_at`.
- **A window function**: `COUNT() OVER` in `customers-first-order-value` `order_count`, `SUM() OVER` in `payments-running-total` `paid_to_date`, `RANK` and `DENSE_RANK` in `products-ranked-in-category` `rank_in_category` and `sales_tier`, and `LAG` with a default in `order-gaps-lag-lead` `previous_status`.
- **A recursive CTE**: every column the server describes, even a plain one, `id` and `name`, and a cast and an arithmetic one, `path` and `depth`, in `category-tree-recursive`.
- **`COALESCE` with an argument that is not null**: `contact-fallbacks` `display_name`, `category-parent-self-join` `breadcrumb`, `invoices-due-this-month` `due_on` and `price-as-of` `effective_price`, and `invoices-full-join-payments` `invoice_id`, a `COALESCE` over both sides of a `FULL JOIN`.
- **A function or an operator of arguments that are not null**: `EOMONTH` of a `COALESCE` that is not null in `invoices-due-this-month` `statement_on`; `AT TIME ZONE 'UTC'` of a column that is not null in `orders-in-customer-time-zone` `placed_utc`; and money minus an `ISNULL` of a `SUM` in `cte-feeding-update` `outstanding`.
- **`GREATEST` and `LEAST` with an argument that is not null**, which ignore a `NULL` argument (2025 alone): `price-bounds-greatest` `lowest_price` and `highest_price`.
- **`JSON_OBJECT` and `JSON_ARRAY`**, which are never `NULL`, whatever their values (2025 alone): `customer-json-object` `contact`, `reachable_by` and `channels`.
- **A `CASE` whose `ELSE` is the nullable column it guards**: `order-status-labels` `ship_to`, `CASE WHEN o.ship_city IS NULL THEN N'(none)' ELSE o.ship_city END`.
- **A `CAST`, which the server calls nullable whatever its argument**: `order-status-labels` `is_open` and `customers-exists-flags` `has_open_cart`, each a `CAST` to `bit` of a `CASE` that is never `NULL`.
- **A computed column**: `categories-right-join-products` `sale_price`, which is not persisted.
- **A nullable column that a filter or a write makes not null**: in an `UPDATE ... OUTPUT inserted`, `update-orders-from-join-output` `shipped_at`, by its `WHERE`, and `cte-feeding-update` `note`, an `UPDATE ... OUTPUT inserted` column whose `SET` is never `NULL`, a `CONCAT`; through a CTE, `cte-feeding-update` `due_at`, by the CTE's `<`; and in a `UNION`, `contacts-union` `name`, whose second branch's `ship_name` is filtered by `LIKE`.

The server's over-reporting kinds are in the spike's [Columns and nullability](../superpowers/specs/2026-10-20-query-generation-phase-6-sql-server-spike.md#columns-and-nullability), which also names `ROW_NUMBER()` and `NEWID()`; no column of the corpus is either (the `ROW_NUMBER()` of `latest-order-per-customer` is filtered on, never returned).  Casts and arithmetic, which the spike names too, appear inside the kinds above: `path` and `depth` of the recursive CTE are a cast and a sum, and `revenue` of `sales-by-category-rollup` is a `SUM` of a product.

## Why it exists

The rule is to rely on the server where it is accurate and to correct it only where it is proven wrong, and the corpus was built to measure the noise before any work lowers it.  Noise is safe: a column the describer cannot prove is not null is nullable, so the generated type is wider than it needs to be and a false nullable costs a check or a `!`, where a false non-nullable is a silent wrong value.

## Impact

Medium.  Nothing is wrong, but of the corpus's columns that cannot be `NULL`, 39 of the 193 of the cases that run on 2017 and 44 of the 209 of those that run on 2025, about a fifth, are answered nullable, and each is a property with a `?` that a user meets as a `!` or a check added by hand, as TD-0051 is for PostgreSQL; the manual's [SQL Server known issues](../manual/sqlserver.md#known-issues) say what a user can do.  `COUNT`, `SUM` and a window function are among the commonest forms of SQL, and the count grows with every case of a kind above.

## Proposed fix

A narrow correction keyed on what the server's answer and the catalog hold, without an analysis of the query's text: a column whose origin (`source_schema`, `source_table` and `source_column` of `sys.dm_exec_describe_first_result_set`) is a computed column that `sys.computed_columns` marks `is_persisted` and `sys.columns` marks not nullable, a `PERSISTED NOT NULL` one, which the server calls nullable, is answered not null.  It takes a fixture that proves it and a `nullableSource` value of its own that says the answer is not the server's.  No column of the corpus is of that kind, so the budget stays as it is; a correction that reaches a column of the corpus lowers its budget in `Budgets` in the same commit.

Every other kind is one that only the query's text tells, and stays noise until the describer reads it: `COUNT` and `COUNT_BIG`, which the server answers as an `int` or a `bigint` column with no origin, like any expression of those types; an aggregate's argument and whether its group has a row; a window function; a `CASE` branch; a `COALESCE`; a `CAST`; a function or an operator whose arguments are not null, `EOMONTH`, `AT TIME ZONE` and arithmetic among them; `GREATEST` and `LEAST`; `JSON_OBJECT` and `JSON_ARRAY`; a recursive CTE's columns; a filter, the `WHERE` of an `UPDATE`, of a CTE or of a branch of a `UNION`; and the `SET` of an `UPDATE` whose `OUTPUT inserted` reads the column it sets.  `sale_price` stays noise too, since it is not persisted.

## Trigger

The owner asks to lower the noise, or a user reports a model wider than its query.
