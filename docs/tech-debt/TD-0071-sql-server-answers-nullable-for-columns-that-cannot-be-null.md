# TD-0071 - The SQL Server describer answers nullable for columns that cannot be NULL

## Problem

The realistic corpus, [`tests/SqlSource.Tests.SqlServer/Realistic/`](../../tests/SqlSource.Tests.SqlServer/Realistic/), holds a truth for each column of each case, written by hand from the SQL and the schema.  Of the columns that truth says can never be `NULL`, the describer answers 30 nullable on SQL Server 2017 (major 14) and 30 on 2025 (17), the same columns on both.  [`RealisticNullabilityNoiseTests`](../../tests/SqlSource.Tests.SqlServer/Realistic/Describer/RealisticNullabilityNoiseTests.cs) holds the count of each major in its `Budgets` and fails when it moves either way, listing the noisy columns.

The describer records the server's own nullability (`sys.dm_exec_describe_first_result_set`, `nullableSource` `server`) and never narrows it, and the server calls these forms nullable whatever their arguments.  The 30 columns are of these kinds:

- **`COUNT`**: `COUNT(*)` with no `GROUP BY` in `customer-order-counts` `order_count`; `COUNT` over a group in `big-customers-having` `order_count`, `order-skus-string-agg` `line_count`, `category-tree-recursive` `product_count` and `orders-grouping-sets` `order_count` and `shipped_count`; a scalar subquery of `COUNT(*)` in `customers-last-payment-subquery` `order_count`.
- **`SUM`, `MAX` over a group that has a row**, of a column that is not null: `big-customers-having` `total_spent` and `last_ordered_at`, `sales-by-category-rollup` `units` and `revenue`, `top-with-ties-best-sellers` `units_sold` and `revenue`, `orders-grouping-sets` `last_placed_at`.
- **A window function**: `COUNT() OVER` in `customers-first-order-value` `order_count`, `SUM() OVER` in `payments-running-total` `paid_to_date`, `RANK` and `DENSE_RANK` in `products-ranked-in-category` `rank_in_category` and `sales_tier`, and `LAG` with a default in `order-gaps-lag-lead` `previous_status`.
- **A recursive CTE**: every column the server describes, even a plain one, `id` and `name`, and a cast and an arithmetic one, `path` and `depth`, in `category-tree-recursive`.
- **`COALESCE` with an argument that is not null**: `contact-fallbacks` `display_name` and `category-parent-self-join` `breadcrumb`, and `invoices-full-join-payments` `invoice_id`, a `COALESCE` over both sides of a `FULL JOIN`.
- **A `CASE` whose `ELSE` is guarded, and a cast of one**: `order-status-labels` `ship_to` and `is_open`.
- **A computed column**: `categories-right-join-products` `sale_price`, which is not persisted.
- **A column the `WHERE` makes not null in an `UPDATE ... OUTPUT inserted`**: `update-orders-from-join-output` `shipped_at`.

The server's over-reporting kinds are in the spike's [Columns and nullability](../superpowers/specs/2026-10-20-query-generation-phase-6-sql-server-spike.md#columns-and-nullability), which also names `ROW_NUMBER()`, `NEWID()` and arithmetic; no column of the corpus is noise of those yet.

## Why it exists

The rule is to rely on the server where it is accurate and to correct it only where it is proven wrong, and the corpus was built to measure the noise before any work lowers it.  Noise is safe: a column the describer cannot prove is not null is nullable, so the generated type is wider than it needs to be and a false nullable costs a check or a `!`, where a false non-nullable is a silent wrong value.

## Impact

Low.  Nothing is wrong, but 30 properties that could be non-nullable are nullable, and `COUNT`, `SUM` and a window function are among the commonest forms of SQL.  The count grows with every case of a kind above.

## Proposed fix

A narrow correction for each kind, keyed on what the server's answer and the catalog hold, without an analysis of the query's text: a computed column whose definition the catalog proves not null (a `PERSISTED NOT NULL` one, which the server calls nullable), then `COUNT` and `COUNT_BIG` where the server's answer tells them apart.  Each correction takes a fixture that proves it, a `nullableSource` value of its own that says the answer is not the server's, and a lower budget in `Budgets` in the same commit.  A kind that only the query's text tells (an aggregate's argument, a `CASE` branch, a `COALESCE`) stays noise, and `sale_price` too, which is not persisted.

## Trigger

The owner asks to lower the noise, or a user reports a model wider than its query.
