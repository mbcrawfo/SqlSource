# TD-0071 - The SQL Server describer answers nullable for columns that cannot be NULL

## Problem

The realistic corpus, [`tests/SqlSource.Tests.SqlServer/Realistic/`](../../tests/SqlSource.Tests.SqlServer/Realistic/), holds a truth for each column of each case, written by hand from the SQL and the schema.  Of the columns that truth says can never be `NULL`, the describer answers 15 nullable on SQL Server 2017 (major 14) and the same 15 on 2025 (17).  [`RealisticNullabilityNoiseTests`](../../tests/SqlSource.Tests.SqlServer/Realistic/Describer/RealisticNullabilityNoiseTests.cs) holds the count of each major in its `Budgets` and fails when it moves either way, listing the noisy columns.

The describer keeps the server's nullability (`sys.dm_exec_describe_first_result_set`, `nullableSource` `server`) except where [the proofs of not null](../architecture/sqlserver-describer.md#proofs-of-not-null) narrow it: from the query's text, when it is one `SELECT`, for `COUNT`, the ranking functions, `SUM`, `MAX` and `MIN` over a group or a frame that holds the current row, `COALESCE`, `GREATEST` and `LEAST`, `CAST` and `CONVERT`, arithmetic, `JSON_OBJECT` and `JSON_ARRAY`, each with the server asked about its operands, and a `PERSISTED NOT NULL` computed column read from its one table.  The 15 columns are of the kinds those proofs do not read:

- **A recursive CTE**: every column the server describes of it, even a plain one, `id` and `name`, and a cast and an arithmetic one, `path` and `depth`, in `category-tree-recursive`.  Each is a column alone, which the server has already answered over the query's rows, and only a model of the recursion would show it not null.
- **`COALESCE` over both sides of a `FULL JOIN`**: `invoices-full-join-payments` `invoice_id`, `COALESCE(iss.id, rec.invoice_id)`, where each argument is nullable alone and one of the two is not null in every row.
- **A function of arguments that are not null, which is no form of the proofs**: `EOMONTH` of a `COALESCE` that is not null in `invoices-due-this-month` `statement_on`; `AT TIME ZONE 'UTC'` of a column that is not null in `orders-in-customer-time-zone` `placed_utc`; and `LAG` with a default in `order-gaps-lag-lead` `previous_status`.
- **A `CASE` whose `ELSE` is the nullable column it guards**: `order-status-labels` `ship_to`, `CASE WHEN o.ship_city IS NULL THEN N'(none)' ELSE o.ship_city END`.
- **A computed column that is not persisted**: `categories-right-join-products` `sale_price`, read from the preserved side of a `RIGHT JOIN`, which the server judges by its expression, `list_price * (1 - ISNULL(discount_pct, 0) / 100)`.
- **A statement that is not one `SELECT`**: in an `UPDATE ... OUTPUT`, `update-orders-from-join-output` `shipped_at`, made not null by the `UPDATE`'s `WHERE`, and in `cte-feeding-update`, `note`, whose `SET` is a `CONCAT`, `due_at`, made not null by the CTE's `<`, and `outstanding`, money minus an `ISNULL` of a `SUM`, read through the CTE; and in a `UNION`, `contacts-union` `name`, whose second branch's `ship_name` is filtered by `LIKE`.

The proofs' other limits are as narrow, and no column of the corpus reaches them: a `PERSISTED NOT NULL` computed column read through a join, a derived table, a CTE or a view, or in parentheses, keeps the server's answer, since through an outer join it is `NULL` while the server still names it as the source; and a query with `FOR XML` or `FOR JSON`, or a batch of several statements, is not read at all.

## Why it exists

The rule is to rely on the server where it is accurate and to correct it only where a proof shows it wrong, and the proofs read the forms the corpus over-reported most, from the text and the server's answer about their operands, with no model of the query's rows.  Noise is safe: a column the describer cannot prove is not null is nullable, so the generated type is wider than it needs to be and a false nullable costs a check or a `!`, where a false non-nullable is a silent wrong value.  Each kind above needs a reading of its own: a function's rule of what it returns, the rows a filter or a recursion leaves, or the reader of a statement other than one `SELECT`.

## Impact

Low.  Nothing is wrong, and of the corpus's columns that cannot be `NULL`, 15 of the 193 of the cases that run on 2017 and 15 of the 209 of those that run on 2025, about one in thirteen, are answered nullable.  Each that remains is a property with a `?` that a user meets as a `!` or a check added by hand, as TD-0051 is for PostgreSQL; the manual's [SQL Server known issues](../manual/sqlserver.md#known-issues) say what a user can do.  The count grows with every case of a kind above.

## Proposed fix

The next kinds, each a form of [`ExpressionProofs`](../../src/SqlSource.Tool/Describing/SqlServer/Proofs/ExpressionProofs.cs) or a rule of [`ColumnProofs`](../../src/SqlSource.Tool/Describing/SqlServer/Proofs/ColumnProofs.cs) on the same reader, with a test of what it proves on a server and of the queries that try to break it in `ProofTests`, and the budget lowered in its commit:

- **`EOMONTH`, `AT TIME ZONE`, `DATEADD` and the like**, of operands that are not null: each a form with its own rule of when it returns `NULL` (`EOMONTH` with a month offset that is not null, `AT TIME ZONE` of a time zone that is a literal or not null, `DATEADD` with a number that is not null, under `ANSI_WARNINGS` on).  `AT TIME ZONE` is an operator, so the reader's split of an expression learns it.
- **`LAG` and `LEAD` with a default**: not null when the operand and the default both are and the offset is an integer literal; each is a leaf in the select's context.
- **A `UNION`, `INTERSECT` or `EXCEPT` whose every branch proves the column**: the statement reader takes a set operator and reads each branch as a `SELECT`, and `operands` asks for each branch's operands, its `FROM` and `WHERE` its own.
- **`OUTPUT inserted` of a computed column declared `PERSISTED NOT NULL`**, of an `INSERT` or an `UPDATE`, whose `inserted` rows are rows of the table: a reader of the `OUTPUT` list.
- **A computed column read through a join**, from the side no outer join can make `NULL`: it needs the `FROM` read as a tree of joins, and `sale_price`, which is not persisted, needs its definition (`sys.computed_columns.definition`) proved too.
- **A `CASE` whose `ELSE` is the column its one `WHEN` tests `IS NULL`**, `CASE WHEN x IS NULL THEN e ELSE x END`: not null when `e` is, as `ISNULL(x, e)` is; a form of the text alone.
- **`COALESCE` over both sides of a `FULL JOIN`**: not null when its arguments are a column of each side that the join's `ON` compares with `=`, each not null over its own side's rows.  It needs the `FROM` read as joins, and `operands` asked of each side alone.
- **A filter**: a column that a conjunct at the top level of the `WHERE`, `x IS NOT NULL`, a comparison or a `LIKE`, rejects when it is `NULL` is not null in the result's rows, and so is the optional side of a `LEFT JOIN` that such a conjunct makes inner.  The same holds of a CTE's `WHERE` for a column read through the CTE, and, with a reader of the statement, of an `UPDATE`'s `WHERE` and `SET` for its `OUTPUT inserted`.
- **A recursive CTE's columns**: not null when the column's expression is proved in the anchor, and in the recursive member with the CTE's own column taken as not null, by induction over the recursion; each member is read as a branch of a `UNION ALL`, as for a set operator above.

## Trigger

The owner asks to lower the noise again, a user reports a model wider than its query, or a case of a kind above is added to the corpus.
