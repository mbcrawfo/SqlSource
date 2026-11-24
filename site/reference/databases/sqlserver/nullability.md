# SQL Server: which columns can be null

<a id="nullability"></a>

## Which columns can be null

```sql mssql
-- name: ListOrders
SELECT o.id, o.placed_at, c.name
FROM dbo.orders AS o
LEFT JOIN dbo.customers AS c ON c.id = o.customer_id;
```

For each column, the tool records whether it can be null, starting from what SQL Server says.  If `orders.id`, `orders.placed_at` and `customers.name` are all `NOT NULL`, then `id` and `placed_at` above are never null.  `name` can be null: the `LEFT JOIN` gives a null name to an order with no customer.

SQL Server treats a column as one that can be null unless it can show that the column never is.  SqlSource keeps that answer unless it can show from your query that the column is never null.  A column that it treats as one that can be null, and that never is, costs a null check in your code.

**SQL Server says that a column can be null wherever a null can come from:**

- a column of a table that can hold null;
- a column of a view that the view's query can fill with null;
- the optional side of a `LEFT`, `RIGHT` or `FULL` join;
- the optional side of an `OUTER APPLY`;
- an aggregate;
- the value of a subquery;
- a window function;
- `ROLLUP`, `CUBE` and `GROUPING SETS`;
- the `inserted` columns of a `MERGE`'s `OUTPUT`, when the `MERGE` can delete, since it leaves `inserted` empty for the rows that it deletes;
- the `deleted` columns of a `MERGE`'s `OUTPUT`, when the `MERGE` can insert, since it leaves `deleted` empty for the rows that it inserts.

A `MERGE` that only updates leaves neither empty.  So its `inserted` and `deleted` columns can be null only where the columns of their table can hold null.

**SQL Server says that a column is never null** when no outer join of the query can fill it with null, and it is one of these:

- a `NOT NULL` column of a table;
- a column of a view that the view's query never fills with null;
- a constant other than `NULL`;
- `ISNULL` whose second argument is never null;
- `CONCAT`;
- a `CASE` or an `IIF` whose every result is never null.

**SqlSource also treats these as never null, where SQL Server does not**, in a query that is one `SELECT`, or several joined by `UNION`, `INTERSECT` or `EXCEPT`, with or without a `WITH` clause:

- **A count**: `COUNT` and `COUNT_BIG`, with or without `OVER (...)`.  A count is `0` for no rows, never null.
- **A subquery that is a count alone and returns one row**, such as `(SELECT COUNT(*) FROM dbo.orders AS o WHERE o.customer_id = c.id)`, with no `GROUP BY`, `HAVING`, `TOP` or `OFFSET`.
- **A rank**: `ROW_NUMBER()`, `RANK()` and `DENSE_RANK()`, and `NTILE` of a whole number, `NTILE(4)`, with `OVER (...)`.
- **`SUM`, `MAX` and `MIN` of a value that is never null, in a query with a `GROUP BY` whose every group has a row.**  A `GROUP BY` with `ROLLUP`, `CUBE` or `GROUPING SETS` counts too.
- **`SUM`, `MAX` and `MIN` of a value that is never null, with `OVER (...)` whose frame holds the current row.**  A window that gives no frame has such a frame.
- **`COALESCE` with an argument that is never null**, when every argument before it stays the same in a row, as the list below says, and no argument is of a spatial type, of `hierarchyid` or of another .NET type.
- **`GREATEST` and `LEAST` with an argument that is never null**, and no argument of a spatial type, of `hierarchyid` or of another .NET type.
- **`CAST` and `CONVERT` of a value that is never null to a built-in type**, such as `int`, `decimal(10, 2)`, `nvarchar(max)` or `datetime2`.  This holds when the type is written as SQL Server names it, and when the style of `CONVERT`, if it has one, is a whole number written in the query.  `TRY_CAST` and `TRY_CONVERT` can be null.
- **Arithmetic of values that are never null**: `+`, `-`, `*`, `/` and `%`, as in `SUM(quantity * unit_price)`.
- **`JSON_OBJECT` and `JSON_ARRAY`**, on SQL Server 2022 and later.
- **A date function of values that are never null**: `DATEADD`, `DATEDIFF`, `DATEDIFF_BIG`, `DATEPART`, `DATENAME`, `YEAR`, `MONTH`, `DAY` and `EOMONTH`.
- **`AT TIME ZONE` of a value and a time zone that are never null**, as in `placed_at AT TIME ZONE 'UTC'`.
- **`LAG` and `LEAD` with a default**, as in `LAG(status, 1, 'none') OVER (...)`, when the value and the default are never null, the offset is a whole number written in the query, and neither is of a spatial type, of `hierarchyid` or of another .NET type.  It does not count as never null inside `CAST` or `CONVERT`, as a value of the `CASE` below, or as an argument of `COALESCE`, `GREATEST` or `LEAST`, alone or in arithmetic.
- **`CASE WHEN x IS NULL THEN e ELSE x END`**, when `e` is never null, `x` is written the same both times, and `x` is a column stored in a table of the query's own database or is never null itself.  Neither may be of a spatial type, of `hierarchyid` or of another .NET type.
- **A computed column declared `PERSISTED NOT NULL`, read from its table alone**, when all of these are true:
  - the `FROM` of the query names that one table and nothing else;
  - the column is an item of its own, or the query is `SELECT *` from that table;
  - the query has no `ROLLUP`, `CUBE` or `GROUPING SETS`.

- **A column that the `WHERE` keeps from being null**, as `shipped_on` in `WHERE shipped_on IS NOT NULL`, when all of these are true:
  - the column is stored in a table of the query's own database;
  - the condition is `x IS NOT NULL`, `x LIKE p`, `x NOT LIKE p` or a comparison such as `x = @id` or `x > 0`, of `x` alone;
  - `AND` joins the condition to the rest of the `WHERE`, and no `OR` or `NOT` applies to it;
  - the query writes `x` the same way in the condition and where it reads it, with the same table alias.
- **A `NOT NULL` column of the optional side of a `LEFT JOIN`, when the `WHERE` makes the join inner.**  The `WHERE` must hold such a condition on a column of that side, written with its table alias, as `o.status` in `WHERE o.status = @status`.  The joins of the `FROM` must be plain joins, one after the other.
- **A column read from a `WITH` query whose own `WHERE` keeps it from being null**, when the `WITH` query is one `SELECT` that returns the column itself, and no outer join of the outer query can fill it with null.
- **A column of `UNION`, `UNION ALL`, `INTERSECT` or `EXCEPT`, when each `SELECT` gives a value that is never null in that place.**  Each `SELECT` is read on its own, with its own `FROM` and `WHERE`.

**SqlSource treats none of these items as never null in two kinds of query:**

- a query with `GROUP BY ALL`, which returns a group with no row;
- a query whose select list mixes `*` with other items, such as `SELECT *, COUNT(*) OVER () AS total`.

**What counts as a value that is never null** in the items above:

- **One of the items above.**
- **A value that SQL Server says is never null where the query reads it**, such as a `NOT NULL` column that no outer join reaches.
- **An expression that holds an aggregate, `OVER` or a function of your own counts only when it is one of the items above.**
- **Under `ROLLUP`, `CUBE` and `GROUPING SETS`, no column outside an aggregate is a value that is never null**, whatever its table says.  A column of the `GROUP BY` is null in the rows of a total.
- **A call that names a window of the `WINDOW` clause, `OVER w`, is never one of the items above.**
- **A function of your own, called with its schema, `dbo.f(x)`, is never one of the items above.**

**What stays the same in a row.**  SQL Server can compute an argument of `COALESCE` twice: once to test it and once for the result.  So each argument before the one that is never null must be one of these:

- a constant, `NULL` or a query parameter;
- a column stored in a table of the query's own database, read directly, or through a subquery in the `FROM` or a `WITH` query that returns the column itself and has no `UNION`;
- arithmetic of these, or `CAST`, `CONVERT` to a built-in type, `COALESCE`, `ISNULL` or `NULLIF` of these;
- `SUM`, `MAX` or `MIN` of these, without `OVER (...)`;
- `AVG` of these, without `OVER (...)`, inside `CAST` or `CONVERT` to a built-in type.

These answers assume that the connection that runs the query leaves `ANSI_WARNINGS` and `ANSI_NULLS` on, as Microsoft.Data.SqlClient does: see [`ANSI_WARNINGS` off](#limit-ansi-warnings-off) and [`ANSI_NULLS` off](#limit-ansi-nulls-off).

**SqlSource treats these as columns that can be null, though some of them never are:**

- **Other functions of values that are never null**, such as `DATEFROMPARTS`, `SWITCHOFFSET` and `NEWID()`.
- **A computed column that is not persisted**, unless SQL Server can show from its expression that it is never null, as for `ISNULL(quantity, 0)`.
- **A computed column declared `PERSISTED NOT NULL` and read through a join, a view, a subquery or a `WITH` query**, with the same exception.
- **A column of a recursive `WITH` query.**
- **`COALESCE` over a column of each side of a `FULL JOIN`**, as in `COALESCE(i.id, p.invoice_id)`.
- **`COALESCE` with an argument that can change before the one that is never null.**  This includes a `CASE`, a subquery, a window function, a date function, a computed column, a view's column and a column read `FOR SYSTEM_TIME`.  SQL Server can compute such a value again for the result, and the second value can be null.  Write `ISNULL(x, 0)`, which computes `x` once.
- **`COALESCE` with a column of another database or of a linked server before the one that is never null.**  This holds when the query reads the table by its name, a synonym, a subquery in the `FROM` or a `WITH` query.  Write `ISNULL(x, 0)`.
- **`COALESCE` with a column that a subquery or a `WITH` query combines or computes, before the one that is never null.**  This includes a column of a `UNION` or `UNION ALL`, a column of an `UNPIVOT`, and a column such as `SUM(x) AS total`.  Write `ISNULL(x, 0)`.
- **`COALESCE(AVG(x), 0)`**, unless the `AVG` is inside `CAST` or `CONVERT` to a built-in type.  Write `ISNULL(AVG(x), 0)`.
- **`CASE WHEN x IS NULL THEN 0 ELSE x END` of a computed column, a view's column or a subquery's expression that can be null.**  The `CASE` reads `x` twice, and SQL Server can compute such a value again for the second read.  Write `ISNULL(x, 0)`, which is never null.
- **A column that a filter keeps from being null in another form**, such as `x IN (1, 2)`, `x BETWEEN 1 AND 2`, `x + 1 > 0` or a condition under `OR`.
- **A computed column, a view's column or a subquery's expression that the `WHERE` tests.**  SQL Server can compute such a value again for the result, and the second value can be null.
- **The optional side of a `LEFT JOIN` whose condition in the `WHERE` names the column without its table alias**, or whose `FROM` also holds an `APPLY`, a comma, a join hint or a join in parentheses.
- **A column of a `UNION`, `INTERSECT` or `EXCEPT` with a `SELECT` that can give null in that place**, or with `*` in its select list.
- **A column of a query that is not a `SELECT`, or that the tool does not read.**  Such a query keeps the answer of SQL Server, so a count or a sum in it can be null.  This holds for the `OUTPUT` of an `INSERT`, `UPDATE`, `DELETE` or `MERGE`, for `FOR XML` or `FOR JSON`, and for several statements in one query.
- **A conversion to `geometry`, `geography`, `hierarchyid` or another .NET type**, since SQL Server can convert the text `'NULL'` to such a type as null: `CAST(N'NULL' AS geometry)` is null.
- **`COALESCE`, `GREATEST` or `LEAST` over a value of such a type**, for the same reason: `COALESCE(shape, 'NULL')` is null.
- **A column of a query that uses `window` as a name**, an alias or a column without brackets, as in `FROM dbo.panes window`.  Such a query keeps the answer of SQL Server.  Write `[window]`.

A column that can be null gets a nullable property in the query's [output model](index.md#models), `int?` or `string?`.  A column that is never null gets a property that cannot be null, `int` or `string`.  Where you know better, you decide instead: a `-- column:` marker says that a column is never null, or can be null, and leaves its name alone.  An alias that ends in `!` or `?` does the same.

```sql mssql
-- name: ListOrderMonths
-- column: month_start not null
SELECT id, DATEFROMPARTS(YEAR(placed_at), MONTH(placed_at), 1) AS month_start
FROM dbo.orders;
```

If `placed_at` is `NOT NULL`, the first day of its month is never null, though SQL Server treats `DATEFROMPARTS` as a value that can be null.  So the marker gives the property `MonthStart` the type `System.DateTime`, and not `System.DateTime?`.

- **The build does not check the marker.**  A [query method](../../query-methods.md#reading-rows) throws if such a column is null after all.
- **An alias `AS [month_start!]` does the same without the marker.**  The mark is left out of the name of the property.  It stays in the name of the column, which Dapper matches to the name of the property, so with Dapper write the marker.

See [which properties can be null](../../model-properties.md#model-nullability) and [the `-- column:` marker](../../model-properties.md#column-marker).

<a id="limits"></a>

## Limits

<a id="limit-columns-treated-as-nullable"></a>

### A property can be null though its column is never null

**What you see.**  A column that is never null gets a property that can be null, such as `int?` where `int` would do.  No value is wrong, but your code checks for a null that never comes.

**Why.**  SQL Server treats a column as one that can be null when it cannot show that the column never is.  SqlSource keeps its answer where it cannot show otherwise from the query.  Among the columns that get such a property are:

- `DATEFROMPARTS`, `SWITCHOFFSET` and other functions of values that are never null;
- the columns of a recursive `WITH` query;
- `COALESCE` over both sides of a `FULL JOIN`;
- `COALESCE` over a value that can change, such as a `CASE` or a subquery;
- `COALESCE` over a column of another database or of a linked server;
- `COALESCE` over a column of a `UNION` or an `UNPIVOT`, or one that a subquery computes;
- `COALESCE(AVG(x), 0)`;
- a computed column that is not persisted;
- a column that a filter keeps from being null in a form such as `x IN (1, 2)` or `x BETWEEN 1 AND 2`;
- a count or a sum in an `OUTPUT` clause.

**What to do.**  Do one of these:

- Give such a column a `-- column: name not null` marker, as [which columns can be null](#nullability) shows.
- End the alias of the column with `!`.
- In the place of a `COALESCE` or a `CASE` that replaces the null of `x`, write `ISNULL(x, 0)`.

<a id="limit-ansi-warnings-off"></a>

### Null from a column whose property cannot be null, with `ANSI_WARNINGS` off

**What you see.**  A column whose property cannot be null holds a null when the query runs.  It can be a count, a sum, a cast or a conversion, `COALESCE`, `GREATEST`, `LEAST`, `LAG`, `LEAD`, a `CASE` that replaces a null, a date function or arithmetic that SqlSource treats as never null.

**Why.**  The connection turns `ANSI_WARNINGS` off.  With `ANSI_WARNINGS` and `ARITHABORT` both off, SQL Server returns null in place of some errors: an arithmetic overflow, a value too large for its type, or a division by zero.

**What to do.**  Leave `ANSI_WARNINGS` on, as Microsoft.Data.SqlClient turns it on when it connects.  Run no `SET ANSI_WARNINGS OFF` on a connection that runs your queries.

<a id="limit-ansi-nulls-off"></a>

### Null from a filtered column whose property cannot be null, with `ANSI_NULLS` off

**What you see.**  A column whose property cannot be null holds a null when the query runs.  The `WHERE` compares the column with a query parameter, as in `WHERE x = @id` or `WHERE x <> @id`.

**Why.**  The connection turns `ANSI_NULLS` off.  With `ANSI_NULLS` off, `x = @id` keeps a row whose `x` is null when `@id` is null, and `x <> @id` keeps that row when `@id` is not null.

**What to do.**  Leave `ANSI_NULLS` on, as Microsoft.Data.SqlClient turns it on when it connects.  Run no `SET ANSI_NULLS OFF` on a connection that runs your queries.
