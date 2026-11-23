# TD-0071 - The SQL Server describer answers nullable for columns that cannot be NULL

## Problem

The realistic corpus, [`tests/DatabaseTests/SqlServer/Realistic/`](../../tests/DatabaseTests/SqlServer/Realistic/), holds a truth for each column of each case, written by hand from the SQL and the schema.  Of the columns that truth says can never be `NULL`, the describer answers 12 nullable on SQL Server 2017 (major 14) and the same 12 on 2025 (17).  [`RealisticNullabilityNoiseTests`](../../tests/DatabaseTests/SqlServer/Realistic/Describer/RealisticNullabilityNoiseTests.cs) holds the count of each major in its `Budgets` and fails when it moves either way, listing the noisy columns.

The describer keeps the server's nullability (`sys.dm_exec_describe_first_result_set`, `nullableSource` `server`) except where [the proofs of not null](../architecture/sqlserver-describer.md#proofs-of-not-null) narrow it: from the query's text, when it is one `SELECT`, for `COUNT`, the ranking functions, `SUM`, `MAX` and `MIN` over a group or a frame that holds the current row, `COALESCE`, `GREATEST` and `LEAST`, `CAST` and `CONVERT`, arithmetic, `JSON_OBJECT` and `JSON_ARRAY`, the date functions and `AT TIME ZONE`, `LAG` and `LEAD` with a default, and `CASE WHEN x IS NULL THEN e ELSE x END`, each with the server asked about its operands, and a `PERSISTED NOT NULL` computed column read from its one table.  The 12 columns are of the kinds those proofs do not read:

- **A recursive CTE**: every column the server describes of it, even a plain one, `id` and `name`, and a cast and an arithmetic one, `path` and `depth`, in `category-tree-recursive`.  Each is a column alone, which the server has already answered over the query's rows, and only a model of the recursion would show it not null.
- **`COALESCE` over both sides of a `FULL JOIN`**: `invoices-full-join-payments` `invoice_id`, `COALESCE(iss.id, rec.invoice_id)`, where each argument is nullable alone and one of the two is not null in every row.
- **`COALESCE` over a column read `FOR SYSTEM_TIME`**: `price-as-of` `effective_price`, `COALESCE(pr.price, p.list_price)`.  The server names no source column for `pr.price`, so it is not shown to give one value in a row, which an argument before the proved one must.
- **A computed column that is not persisted**: `categories-right-join-products` `sale_price`, read from the preserved side of a `RIGHT JOIN`, which the server judges by its expression, `list_price * (1 - ISNULL(discount_pct, 0) / 100)`.
- **A statement that is not one `SELECT`**: in an `UPDATE ... OUTPUT`, `update-orders-from-join-output` `shipped_at`, made not null by the `UPDATE`'s `WHERE`, and in `cte-feeding-update`, `note`, whose `SET` is a `CONCAT`, `due_at`, made not null by the CTE's `<`, and `outstanding`, money minus an `ISNULL` of a `SUM`, read through the CTE; and in a `UNION`, `contacts-union` `name`, whose second branch's `ship_name` is filtered by `LIKE`.

The proofs' other limits are as narrow, and no column of the corpus reaches them: a date function that is no form, such as `DATEFROMPARTS`, `DATETRUNC`, `DATE_BUCKET`, `SWITCHOFFSET` or `TODATETIMEOFFSET`; `LAG` or `LEAD` whose offset is not an integer literal or that names a window; a `CASE` that guards a computed column, a view's column or a derived table's expression that the server answers nullable, which is read twice and may differ between the reads; a `COALESCE` with such a value, a `CASE`, a date function or a string function before its proved argument, as in `COALESCE(SUM(CASE WHEN x > 0 THEN x END), 0)`, for the same reason; a `PERSISTED NOT NULL` computed column read through a join, a derived table, a CTE or a view, or in parentheses, keeps the server's answer, since through an outer join it is `NULL` while the server still names it as the source; and a query with `FOR XML` or `FOR JSON`, or a batch of several statements, is not read at all.

## Why it exists

The rule is to rely on the server where it is accurate and to correct it only where a proof shows it wrong, and the proofs read the forms the corpus over-reported most, from the text and the server's answer about their operands, with no model of the query's rows.  Noise is safe: a column the describer cannot prove is not null is nullable, so the generated type is wider than it needs to be and a false nullable costs a check or a `!`, where a false non-nullable is a silent wrong value.  Each kind above needs a reading of its own: a function's rule of what it returns, the rows a filter or a recursion leaves, or the reader of a statement other than one `SELECT`.

## Impact

Low.  Nothing is wrong, and of the corpus's columns that cannot be `NULL`, 12 of the 193 of the cases that run on 2017 and 12 of the 209 of those that run on 2025, about one in seventeen, are answered nullable.  Each that remains is a property with a `?` that a user meets as a `!` or a check added by hand, as TD-0051 is for PostgreSQL; the manual's [SQL Server limit](../../site/reference/databases/sqlserver/nullability.md#limit-columns-treated-as-nullable) says what a user can do.  The count grows with every case of a kind above.

## Proposed fix

The next kinds, each a form of [`ExpressionProofs`](../../src/SqlSource.Tool/Describing/SqlServer/Proofs/ExpressionProofs.cs) or a rule of [`ColumnProofs`](../../src/SqlSource.Tool/Describing/SqlServer/Proofs/ColumnProofs.cs) on the same reader, with a test of what it proves on a server and of the queries that try to break it in `ProofTests`, and the budget lowered in its commit:

- **A `UNION`, `INTERSECT` or `EXCEPT` whose every branch proves the column**: the statement reader takes a set operator and reads each branch as a `SELECT`, and `operands` asks for each branch's operands, its `FROM` and `WHERE` its own.
- **`OUTPUT inserted` of a computed column declared `PERSISTED NOT NULL`**, of an `INSERT` or an `UPDATE`, whose `inserted` rows are rows of the table: a reader of the `OUTPUT` list.
- **A computed column read through a join**, from the side no outer join can make `NULL`: it needs the `FROM` read as a tree of joins, and `sale_price`, which is not persisted, needs its definition (`sys.computed_columns.definition`) proved too.
- **`COALESCE` over both sides of a `FULL JOIN`**: not null when its arguments are a column of each side that the join's `ON` compares with `=`, each not null over its own side's rows.  It needs the `FROM` read as joins, and `operands` asked of each side alone.
- **More values that give one value in a row, for `COALESCE`** (`ExpressionProofs.OneValue`): a column read `FOR SYSTEM_TIME`, which needs the `FROM` read since the server names no source; a `CASE` of such values, which needs its conditions read; and the date and string functions that are deterministic.  Each is measured on every supported major with an operand that changes, as `ValuesThatChange` of `ProofTests` does.
- **A filter**: a column that a conjunct at the top level of the `WHERE`, `x IS NOT NULL`, a comparison or a `LIKE`, rejects when it is `NULL` is not null in the result's rows, and so is the optional side of a `LEFT JOIN` that such a conjunct makes inner.  The same holds of a CTE's `WHERE` for a column read through the CTE, and, with a reader of the statement, of an `UPDATE`'s `WHERE` and `SET` for its `OUTPUT inserted`.
- **A recursive CTE's columns**: not null when the column's expression is proved in the anchor, and in the recursive member with the CTE's own column taken as not null, by induction over the recursion; each member is read as a branch of a `UNION ALL`, as for a set operator above.

## Trigger

The owner asks to lower the noise again, a user reports a model wider than its query, or a case of a kind above is added to the corpus.
