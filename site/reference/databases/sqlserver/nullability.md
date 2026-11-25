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

SQL Server treats a column as one that can be null unless it can show that the column never is.  SqlSource keeps that answer unless it can show from your query that the column is never null, but for a column of a spatial type, of `hierarchyid` or of another .NET type, which it can treat as one that can be null where SQL Server says it never is, as below.  A column that it treats as one that can be null, and that never is, costs a null check in your code.

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

**SqlSource treats a column of a spatial type, of `hierarchyid` or of another .NET type as one that can be null, though SQL Server says it is never null**, unless the column is one of these:

- a `NOT NULL` column of a table of the database the query runs in, read as the table stores it, from the table or through a subquery, a `WITH` query, a view or a function that is one `RETURN SELECT`, any of which passes it on;
- a column of a `UNION`, `INTERSECT` or `EXCEPT` of the query itself, not of a subquery, a `WITH` query or a view, whose every `SELECT` gives such a column of a table at that place.

`UNION ALL`, `UNION`, `INTERSECT`, `EXCEPT` and a `VALUES` list convert a string to the .NET type when another of their values has that type, and `geometry` and `geography` read the text `NULL`, in any case, as null.  SQL Server still says that such a column is never null, also when it is read through a subquery, a `WITH` query, a view or a function, or inside an expression.

**SqlSource also treats these as never null, where SQL Server does not**, in a query that is one `SELECT`, or several joined by `UNION`, `INTERSECT` or `EXCEPT`, with or without a `WITH` clause:

- **A count**: `COUNT` and `COUNT_BIG`, with or without `OVER (...)`.  A count is `0` for no rows, never null.
- **A subquery that is a count alone and returns one row**, such as `(SELECT COUNT(*) FROM dbo.orders AS o WHERE o.customer_id = c.id)`, with no `GROUP BY`, `HAVING`, `TOP` or `OFFSET`.
- **A rank**: `ROW_NUMBER()`, `RANK()` and `DENSE_RANK()`, and `NTILE` of a whole number, `NTILE(4)`, with `OVER (...)`.
- **`SUM`, `MAX` and `MIN` of a value that is never null, in a query with a `GROUP BY` whose every group has a row.**  A `GROUP BY` with `ROLLUP`, `CUBE` or `GROUPING SETS` counts too.
- **`SUM`, `MAX` and `MIN` of a value that is never null, with `OVER (...)` whose frame holds the current row.**  A window that gives no frame has such a frame.
- **`COALESCE` with an argument that is never null**, when every argument before it stays the same in a row, as the list below says, and no argument is of a spatial type, of `hierarchyid` or of another .NET type.
- **`ISNULL(x, y)` when `x` or `y` is never null**, and neither is of a spatial type, of `hierarchyid` or of another .NET type.
- **`COALESCE` or `ISNULL` over a column of each side of a `FULL JOIN`**, as `COALESCE(i.id, p.invoice_id)` in `FROM invoices AS i FULL JOIN payments AS p ON ...`.  Every row of the join has the left side, the right side or both.  This holds when all of these are true:
  - each column is stored in a table of the query's own database, read directly, or through a subquery in the `FROM` or a `WITH` query that returns the column itself and has no `UNION`;
  - each column is never null in the rows of its own side, without the query's `WHERE`;
  - each column is written with its table alias: one of the tables before the `FULL JOIN`, and the other the table it joins;
  - it is the last `FULL JOIN` of the `FROM`, and no `RIGHT JOIN` comes after it;
  - the joins of the `FROM` are plain joins, one after the other;
  - for `COALESCE`, each argument before the second column stays the same in a row, and so does the second column unless it is the last argument;
  - neither is of a spatial type, of `hierarchyid` or of another .NET type.
- **`GREATEST` and `LEAST` with an argument that is never null**, and no argument of a spatial type, of `hierarchyid` or of another .NET type.
- **`CAST` and `CONVERT` of a value that is never null to a built-in type**, such as `int`, `decimal(10, 2)`, `nvarchar(max)` or `datetime2`.  This holds when the type is written as SQL Server names it, and when the style of `CONVERT`, if it has one, is a whole number written in the query.  `TRY_CAST` and `TRY_CONVERT` can be null.
- **Arithmetic of values that are never null**: `+`, `-`, `*`, `/` and `%`, as in `SUM(quantity * unit_price)`.
- **`JSON_OBJECT` and `JSON_ARRAY`**, on SQL Server 2022 and later.
- **A date function of values that are never null**: `DATEADD`, `DATEDIFF`, `DATEDIFF_BIG`, `DATEPART`, `DATENAME`, `YEAR`, `MONTH`, `DAY` and `EOMONTH`.
- **`AT TIME ZONE` of a value and a time zone that are never null**, as in `placed_at AT TIME ZONE 'UTC'`.
- **`LAG` and `LEAD` with a default**, as in `LAG(status, 1, 'none') OVER (...)`, when the value and the default are never null, the offset is a whole number written in the query, and neither is of a spatial type, of `hierarchyid` or of another .NET type.  It does not count as never null inside `CAST` or `CONVERT`, as a value of the `CASE` below, or as an argument of `COALESCE`, `GREATEST` or `LEAST`, alone or in arithmetic.
- **`CASE WHEN x IS NULL THEN e ELSE x END`**, when `e` is never null, `x` is written the same both times, and `x` is a column stored in a table of the query's own database or is never null itself.  Neither may be of a spatial type, of `hierarchyid` or of another .NET type.
- **A computed column read from a table that no join of the query can fill with null**, when all of these are true:
  - the column is declared `PERSISTED NOT NULL`, or its expression is never null by the items above, of columns of its table that are never null;
  - the query reads the table directly by its name, and not through a subquery, a `WITH` query or a view;
  - no outer join of the query can fill the table's columns with null, even one that the `WHERE` makes inner;
  - the column is an item of its own, written with its table alias when the `FROM` has more than one table, or the query is `SELECT *` from that one table, or `t.*` of that table;
  - the query is one `SELECT`, with no `ROLLUP`, `CUBE` or `GROUPING SETS`.

- **A column that the `WHERE` keeps from being null**, as `shipped_on` in `WHERE shipped_on IS NOT NULL`, when all of these are true:
  - the column is stored in a table of the query's own database;
  - the condition is `x IS NOT NULL`, `x LIKE p`, `x NOT LIKE p` or a comparison such as `x = @id` or `x > 0`, of `x` alone;
  - `AND` joins the condition to the rest of the `WHERE`, and no `OR` or `NOT` applies to it;
  - the query writes `x` the same way in the condition and where it reads it, with the same table alias.
- **A `NOT NULL` column of the optional side of a `LEFT JOIN`, when the `WHERE` makes the join inner.**  The `WHERE` must hold such a condition on a stored column of that side, written with its table alias, as `o.status` in `WHERE o.status = @status`.  The joins of the `FROM` must be plain joins, one after the other.
- **A column read from a `WITH` query whose own `WHERE` keeps it from being null**, when the `WITH` query is one `SELECT` that returns the column itself, and no outer join of the outer query can fill it with null.
- **A column of `UNION`, `UNION ALL`, `INTERSECT` or `EXCEPT`, when each `SELECT` gives a value that is never null in that place.**  Each `SELECT` is read on its own, with its own `FROM` and `WHERE`.
- **A column of a `WITH` query, made of one `SELECT` or several, a recursive one included, when each `SELECT` gives a value that is never null in that place.**  In the recursive part, a column that the `WITH` query reads from itself counts as never null when that column is one of those SqlSource shows never null.  The outer query must read the column by its name, through the `WITH` query's own name or alias, and no outer join of the outer query may fill it with null.  The `FROM` of each `SELECT` must be plain joins, one after the other.

**SqlSource also treats these columns of an `OUTPUT` clause as never null, where SQL Server does not**, in an `INSERT`, `UPDATE` or `DELETE`, with or without a `WITH` clause.  `inserted` counts in an `INSERT` or an `UPDATE`, and `deleted` in an `UPDATE` or a `DELETE`.

- **A computed column declared `PERSISTED NOT NULL`**, as `inserted.total`, or a column of `inserted.*` or `deleted.*` when that is the only item.  The statement must change a table, and not a view.
- **A `deleted` column that the `WHERE` keeps from being null**, as `deleted.shipped_at` in `WHERE shipped_at IS NOT NULL`.  `deleted` holds the value that the `WHERE` tested, whatever the `SET` writes.
- **An `inserted` column of an `UPDATE` that the `WHERE` keeps from being null, when the `SET` does not write that column.**
- **An `inserted` column of an `UPDATE` whose `SET` writes a value that is never null**, by the items above, as `inserted.note` in `SET note = N'shipped'`.  If the value needs the answer of SQL Server, such as a column or `CONCAT` of one, the `UPDATE` must have a `FROM`.
- **Any other column of an `UPDATE` or a `DELETE`, by the items above**, read as a column of a `SELECT` over the statement's `FROM` and `WHERE`, such as `ISNULL(c.email, c.name)` of a table that the `FROM` joins.  Without a `FROM`, a value that needs the answer of SQL Server, such as a column, does not count.

For the `WHERE` and the `SET`, all of these must be true:

- the column is stored in a table of the query's own database, and is not a computed column, or of a spatial type, of `hierarchyid` or of another .NET type;
- the condition is one that keeps a column from being null in a `SELECT`, as above, such as `x IS NOT NULL` or `x = @id`;
- the condition writes the column as the `OUTPUT` writes it, as `x` when the statement has no `FROM` or a `FROM` of one table, or as `o.x` with the name of the table that the statement changes;
- with a `FROM`, the statement names the table that it changes by its alias in the `FROM`, as in `UPDATE o SET ... FROM dbo.orders AS o`, or by a name of one part that the `FROM` uses alone;
- for an `inserted` column, each item of the `SET` is `x = value`, `o.x = value` or `@v = value`;
- for the value that the `SET` writes, the `SET` writes the column once, with the name that the `OUTPUT` gives it.

Any other item of the `SET`, such as `x += 1` or `@v = x = 0`, counts as one that writes every column.  Names that differ only in case or in trailing spaces count as the same column: a `SET` that writes `Shipped_At` or `[shipped_at ]` writes `shipped_at`.  A name with any character but an ASCII letter, a digit, `_`, `@`, `#` or `$`, such as `[ß]` or `[ship date]`, counts as every column, since a collation may make it the same as another spelling.

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
- **A computed column whose expression SqlSource cannot show is never null**, unless SQL Server can show it, as for `ISNULL(quantity, 0)`.  This includes an expression of a column that can be null, of a function of your own, or with `CAST` or `CONVERT`.
- **A computed column read through a view, a subquery or a `WITH` query, or from the optional side of an outer join**, with the same exception.  This holds also for a `LEFT JOIN` that the `WHERE` makes inner, for a column written without its table alias in a `FROM` of several tables, and in a query that is not one `SELECT`.
- **A column of a `WITH` query made of several `SELECT`s, a recursive one included, that the outer query reads through an outer join**, even one that its `WHERE` makes inner, or that a `SELECT` of it reads from the `WITH` query itself through a comma, an `APPLY` or a subquery in the `FROM`.
- **`COALESCE` or `ISNULL` over a column of each side of a `FULL JOIN` in another form**, such as a `FULL JOIN` inside another join or in parentheses, `a JOIN b JOIN c ON p ON q`, a `FULL JOIN` that a `RIGHT JOIN` or another `FULL JOIN` follows, or a column of a table joined after it.
- **`COALESCE` or `ISNULL` over a column of a `FULL JOIN`'s side that only the `WHERE` keeps from being null.**  Each side is read without the query's `WHERE`.
- **`COALESCE` with an argument that can change before the one that is never null.**  This includes a `CASE`, a subquery, a window function, a date function, a computed column, a view's column and a column read `FOR SYSTEM_TIME`.  SQL Server can compute such a value again for the result, and the second value can be null.  Write `ISNULL(x, 0)`, which computes `x` once.
- **`COALESCE` with a column of another database or of a linked server before the one that is never null.**  This holds when the query reads the table by its name, a synonym, a subquery in the `FROM` or a `WITH` query.  Write `ISNULL(x, 0)`.
- **`COALESCE` with a column that a subquery or a `WITH` query combines or computes, before the one that is never null.**  This includes a column of a `UNION` or `UNION ALL`, a column of an `UNPIVOT`, and a column such as `SUM(x) AS total`.  Write `ISNULL(x, 0)`.
- **`COALESCE(AVG(x), 0)`**, unless the `AVG` is inside `CAST` or `CONVERT` to a built-in type.  Write `ISNULL(AVG(x), 0)`.
- **`CASE WHEN x IS NULL THEN 0 ELSE x END` of a computed column, a view's column or a subquery's expression that can be null.**  The `CASE` reads `x` twice, and SQL Server can compute such a value again for the second read.  Write `ISNULL(x, 0)`, which is never null.
- **A column that a filter keeps from being null in another form**, such as `x IN (1, 2)`, `x BETWEEN 1 AND 2`, `x + 1 > 0` or a condition under `OR`.
- **A computed column, a view's column or a subquery's expression that the `WHERE` tests.**  SQL Server can compute such a value again for the result, and the second value can be null.
- **The optional side of a `LEFT JOIN` whose condition in the `WHERE` names the column without its table alias**, or whose `FROM` also holds an `APPLY`, a comma, a join hint or a join in parentheses.
- **A column of a `UNION`, `INTERSECT` or `EXCEPT` with a `SELECT` that can give null in that place**, or with `*` in its select list.
- **A column of a query that the tool does not read.**  Such a query keeps the answer of SQL Server, so a count or a sum in it can be null, but for a column of a spatial type, of `hierarchyid` or of another .NET type that SqlSource treats as one that can be null, as [which columns can be null](#nullability) says.  This holds for the `OUTPUT` of a `MERGE`, for `FOR XML` or `FOR JSON`, and for several statements in one query.
- **A column of an `OUTPUT` clause in another form**, which keeps the answer of SQL Server in the same way:
  - an expression of an `inserted` or `deleted` column, such as `COALESCE(inserted.note, N'')` or `(inserted.total)`;
  - a column of an `INSERT` that is not a `PERSISTED NOT NULL` computed column;
  - a `SET` value of `DEFAULT`;
  - in an `UPDATE` or a `DELETE` without a `FROM`, a value that needs the answer of SQL Server, such as a column;
  - an `UPDATE` or a `DELETE` that changes a table through a view, a `WITH` query or a subquery, or that names it otherwise beside a `FROM`, as in `UPDATE dbo.orders ... FROM dbo.orders AS o`;
  - an `UPDATE` or a `DELETE` whose `FROM` holds an `APPLY`, a comma, a join hint, a table function, `FOR SYSTEM_TIME`, a join in parentheses or a table named `inserted` or `deleted`, or with `WHERE CURRENT OF`.
- **A conversion to `geometry`, `geography`, `hierarchyid` or another .NET type**, since SQL Server can convert the text `'NULL'` to such a type as null: `CAST(N'NULL' AS geometry)` is null.
- **`COALESCE`, `GREATEST` or `LEAST` over a value of such a type**, for the same reason: `COALESCE(shape, 'NULL')` is null.
- **A column of a query that uses `window` as a name**, an alias or a column without brackets, as in `FROM dbo.panes window`.  Such a query keeps the answer of SQL Server, but for a column of a spatial type, of `hierarchyid` or of another .NET type, as for the query above.  Write `[window]`.

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

**Why.**  SQL Server treats a column as one that can be null when it cannot show that the column never is.  SqlSource keeps its answer where it cannot show otherwise from the query, and treats a column of a spatial type, of `hierarchyid` or of another .NET type as one that can be null, though SQL Server says it never is, unless it can show that no `UNION` or `VALUES` list converted a string to it.  Among the columns that get such a property are:

- `DATEFROMPARTS`, `SWITCHOFFSET` and other functions of values that are never null;
- `COALESCE` over both sides of a `FULL JOIN` inside another join or in parentheses, or over a column that only the `WHERE` keeps from being null;
- `COALESCE` over a value that can change, such as a `CASE` or a subquery;
- `COALESCE` over a column of another database or of a linked server;
- `COALESCE` over a column of a `UNION` or an `UNPIVOT`, or one that a subquery computes;
- `COALESCE(AVG(x), 0)`;
- a computed column read through a subquery, a `WITH` query, a view or the optional side of an outer join, or whose expression holds `CAST` or `CONVERT`;
- a column that a filter keeps from being null in a form such as `x IN (1, 2)` or `x BETWEEN 1 AND 2`;
- an expression of an `inserted` or `deleted` column in an `OUTPUT` clause, or a column of an `UPDATE` or a `DELETE` without a `FROM` whose value needs the answer of SQL Server;
- a column of a spatial type, of `hierarchyid` or of another .NET type that is read from a table variable, a table of another database or of a linked server, a function with a body of several statements or a view created `WITH VIEW_METADATA`, that a `VALUES` list or an expression gives, or that a `UNION`, `INTERSECT` or `EXCEPT` inside a subquery, a `WITH` query, a view or a function gives, as [which columns can be null](#nullability) says.

**What to do.**  Do one of these:

- Give such a column a `-- column: name not null` marker, as [which columns can be null](#nullability) shows.
- End the alias of the column with `!`.
- In the place of a `COALESCE` or a `CASE` that replaces the null of `x`, write `ISNULL(x, 0)`.
- Give an `UPDATE` or a `DELETE` a `FROM` with an alias, and change the table by that alias: `UPDATE o SET ... FROM dbo.orders AS o`.

<a id="limit-ansi-warnings-off"></a>

### Null from a column whose property cannot be null, with `ANSI_WARNINGS` off

**What you see.**  A column whose property cannot be null holds a null when the query runs.  It can be a count, a sum, a cast or a conversion, `COALESCE`, `ISNULL`, `GREATEST`, `LEAST`, `LAG`, `LEAD`, a `CASE` that replaces a null, a date function, arithmetic or a computed column that SqlSource treats as never null.

**Why.**  The connection turns `ANSI_WARNINGS` off.  With `ANSI_WARNINGS` and `ARITHABORT` both off, SQL Server returns null in place of some errors: an arithmetic overflow, a value too large for its type, or a division by zero.

**What to do.**  Leave `ANSI_WARNINGS` on, as Microsoft.Data.SqlClient turns it on when it connects.  Run no `SET ANSI_WARNINGS OFF` on a connection that runs your queries.

<a id="limit-ansi-nulls-off"></a>

### Null from a filtered column whose property cannot be null, with `ANSI_NULLS` off

**What you see.**  A column whose property cannot be null holds a null when the query runs.  The `WHERE` compares a column with a query parameter, as in `WHERE x = @id` or `WHERE x <> @id`.  The null can be in that column, in any column of the optional side of a `LEFT JOIN` that the condition names, or in a column read from a `WITH` query whose `WHERE` holds the condition.

**Why.**  The connection turns `ANSI_NULLS` off.  With `ANSI_NULLS` off, `x = @id` keeps a row whose `x` is null when `@id` is null, and `x <> @id` keeps that row when `@id` is not null.

**What to do.**  Leave `ANSI_NULLS` on, as Microsoft.Data.SqlClient turns it on when it connects.  Run no `SET ANSI_NULLS OFF` on a connection that runs your queries.
