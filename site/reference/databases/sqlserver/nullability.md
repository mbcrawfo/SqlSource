# SQL Server: which columns can be null

<a id="nullability"></a>

## Which columns can be null

```sql mssql
-- name: ListOrders
SELECT o.id, o.placed_at, c.name
FROM dbo.orders AS o
LEFT JOIN dbo.customers AS c ON c.id = o.customer_id;
```

For each column the tool records whether it can be null, starting from what SQL Server says.  If `orders.id`, `orders.placed_at` and `customers.name` are all `NOT NULL`, `id` and `placed_at` above are never null, and `name` can be: the `LEFT JOIN` gives a null name to an order with no customer.

SQL Server treats a column as possibly null unless it can show that it never is, and SqlSource keeps its answer unless it can show from your query that the column never is.  A column it treats as possibly null that never is costs a null check in your code.  SQL Server says a column can be null wherever a null can come from: a column of a table that can hold null, a column of a view that the view's query can fill with null, the optional side of a `LEFT`, `RIGHT` or `FULL` join or of an `OUTER APPLY`, an aggregate, a subquery's value, a window function, `ROLLUP`, `CUBE` and `GROUPING SETS`, and the `inserted` and `deleted` columns of a `MERGE`'s `OUTPUT` when one of its actions leaves them empty: a `MERGE` that can delete leaves `inserted` empty for the rows it deletes, and one that can insert leaves `deleted` empty for the rows it inserts.  A `MERGE` that only updates leaves neither empty, so its `inserted` and `deleted` columns are possibly null only where their table's columns can hold null.

SQL Server says that a column is never null when no outer join of the query can fill it with null and it is a `NOT NULL` column of a table, a column of a view that the view's query never fills with null, a constant other than `NULL`, `ISNULL` whose second argument is never null, `CONCAT`, or a `CASE` or an `IIF` whose every result is never null.  In a query that is one `SELECT`, with or without a `WITH` clause, SqlSource also treats these as never null, where SQL Server does not.  A query with `GROUP BY ALL`, which returns a group with no row, gets none of these, and nor does a select list that mixes `*` with other items, such as `SELECT *, COUNT(*) OVER () AS total`:

- **A count**: `COUNT` and `COUNT_BIG`, with or without `OVER (...)`, and a subquery that is a count alone and returns one row, `(SELECT COUNT(*) FROM dbo.orders AS o WHERE o.customer_id = c.id)`, with no `GROUP BY`, `HAVING`, `TOP` or `OFFSET`.  A count is `0` for no rows, never null.
- **A rank**: `ROW_NUMBER()`, `RANK()` and `DENSE_RANK()`, and `NTILE` of a whole number, `NTILE(4)`, with `OVER (...)`.
- **`SUM`, `MAX` and `MIN` of a value that is never null**, in a query with a `GROUP BY`, `ROLLUP`, `CUBE` and `GROUPING SETS` included, whose every group has a row; or with `OVER (...)` whose frame holds the current row, as the frame does when the window gives none.
- **`COALESCE`, `GREATEST` and `LEAST`** with an argument that is never null, and none of a spatial, `hierarchyid` or other .NET type.
- **`CAST` and `CONVERT` of a value that is never null to a built-in type**, such as `int`, `decimal(10, 2)`, `nvarchar(max)` or `datetime2`, written as SQL Server names it, `CONVERT`'s style, when it has one, a whole number written in the query.  `TRY_CAST` and `TRY_CONVERT` are possibly null.
- **Arithmetic of values that are never null**: `+`, `-`, `*`, `/` and `%`, as in `SUM(quantity * unit_price)`.
- **`JSON_OBJECT` and `JSON_ARRAY`**, on SQL Server 2022 and later.
- **A computed column declared `PERSISTED NOT NULL`**, read from its table alone: the query's `FROM` names that one table and nothing else, the column is an item of its own or the query is `SELECT *` from that table, and there is no `ROLLUP`, `CUBE` or `GROUPING SETS`.

A value is never null here when it is one of the above, or when SQL Server says it is never null where the query reads it, such as a `NOT NULL` column that no outer join reaches; an expression that holds an aggregate, `OVER` or a function of your own counts only when it is one of the above.  Under `ROLLUP`, `CUBE` and `GROUPING SETS`, a column of the `GROUP BY` is null in the rows of a total, so outside an aggregate SqlSource takes no column of such a query as a value that is never null, whatever its table says.  A call that names a window of the `WINDOW` clause, `OVER w`, and a function of your own, called with its schema, `dbo.f(x)`, are never one of the above.  These answers assume that the connection that runs the query leaves `ANSI_WARNINGS` on, as Microsoft.Data.SqlClient does: see [limits](#limit-ansi-warnings-off).

SqlSource treats these as possibly null, though some of them never are:

- **`LAG(...)` and `LEAD(...)` with a default.**
- **Other functions of values that are never null**, such as `EOMONTH`, `DATEADD`, `AT TIME ZONE` and `NEWID()`.
- **A computed column** that is not persisted, or declared `PERSISTED NOT NULL` and read through a join, a view, a subquery or a `WITH` query, unless SQL Server can show from its expression that it is never null, as for `ISNULL(quantity, 0)`.
- **A column of a recursive `WITH` query.**
- **`COALESCE` over a column of each side of a `FULL JOIN`**, as in `COALESCE(i.id, p.invoice_id)`.
- **A `CASE` whose `ELSE` is the column it tests for null**, `CASE WHEN x IS NULL THEN 0 ELSE x END`.  Write `ISNULL(x, 0)`, which is never null.
- **A column that a filter keeps from being null**, such as a column that can hold null under `WHERE x IS NOT NULL`, or the optional side of a `LEFT JOIN` that the `WHERE` makes inner.
- **A column of a query that is not one `SELECT`**: the `OUTPUT` of an `INSERT`, `UPDATE`, `DELETE` or `MERGE`, a `UNION`, `INTERSECT` or `EXCEPT`, `FOR XML` or `FOR JSON`, and several statements in one query keep SQL Server's answer, so a count or a sum there is possibly null.
- **A conversion to `geometry`, `geography`, `hierarchyid` or another .NET type, or `COALESCE`, `GREATEST` or `LEAST` over a value of one**, since SQL Server can convert the text `'NULL'` to such a type as null: `CAST(N'NULL' AS geometry)` and `COALESCE(shape, 'NULL')` are null.
- **A column of a query that uses `window` as a name**, an alias or a column without brackets, as in `FROM dbo.panes window`, keeps SQL Server's answer.  Write `[window]`.

A column that can be null gets a nullable property in the query's [output model](index.md#models), `int?` or `string?`, and a column that is never null a property that cannot be null, `int` or `string`.  Where you know better, you decide instead: a `-- column:` marker says that a column is never null, or can be, and leaves its name alone, and so does an alias that ends in `!` or `?`.

```sql mssql
-- name: ListOrderMonths
-- column: month_end not null
SELECT id, EOMONTH(placed_at) AS month_end
FROM dbo.orders;
```

If `placed_at` is `NOT NULL`, the last day of its month is never null, though SQL Server treats `EOMONTH` as possibly null: the marker gives the property `MonthEnd` the type `System.DateTime` and not `System.DateTime?`.  The build does not check the marker: a [query method](../../../generator.md#reading-rows) throws when such a column is null after all.  An alias `AS [month_end!]` does the same without the marker, and the mark is left out of the property's name; it stays in the column's name, which Dapper matches to the property's by name, so with Dapper write the marker.  See [which properties can be null](../../../generator.md#model-nullability) and [the `-- column:` marker](../../../generator.md#column-marker).

<a id="limits"></a>

## Limits

<a id="limit-columns-treated-as-nullable"></a>

### A property can be null though its column is never null

- **Some columns that are never null get a property that can be null.**  SQL Server treats a column as possibly null when it cannot show that it never is, and SqlSource keeps its answer where it cannot show otherwise from the query, so `EOMONTH`, `AT TIME ZONE` and other functions of values that are never null, `LAG` and `LEAD` with a default, the columns of a recursive `WITH` query, `COALESCE` over both sides of a `FULL JOIN`, a computed column that is not persisted, a column that a filter keeps from being null, and a count or a sum in an `OUTPUT` clause or a `UNION`, among others, get a property such as `int?` where `int` would do.  No value is wrong, but your code checks for a null that never comes.  Give such a column a `-- column: name not null` marker, or end its alias with `!`, as [which columns can be null](#nullability) shows, or write `ISNULL(x, 0)` in the place of a `CASE` that tests `x` for null.

<a id="limit-ansi-warnings-off"></a>

### Null from a column whose property cannot be null, with `ANSI_WARNINGS` off

- **A connection that turns `ANSI_WARNINGS` off can read null from a column whose property cannot be null.**  With `ANSI_WARNINGS` and `ARITHABORT` both off, SQL Server returns null where an arithmetic overflow, a value too large for its type or a division by zero would otherwise be an error, so a count, a sum, a cast or a conversion, `COALESCE`, `GREATEST`, `LEAST` or arithmetic that SqlSource treats as never null can be null.  Microsoft.Data.SqlClient turns `ANSI_WARNINGS` on when it connects: leave it on, and run no `SET ANSI_WARNINGS OFF` on a connection that runs your queries.
