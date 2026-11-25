# PostgreSQL: which columns can be null

<a id="nullability"></a>

## Which columns can be null

```sql postgres
-- name: ListOrders
SELECT o.id, o.placed_at, c.name
FROM orders o
LEFT JOIN customers c ON c.id = o.customer_id;
```

For each column of a query, SqlSource says whether the column can be null or is never null.  If `orders.id`, `orders.placed_at` and `customers.name` are all `NOT NULL`, the columns `id` and `placed_at` of the sample are never null, but `name` can be null.  The `LEFT JOIN` gives a null name to an order with no customer.

The usual reasons that a column can be null are an outer join, a query that reads a view's whole column list, or an expression such as `initcap(name)`.  A call of a function that the lists below do not name is such an expression.  The lists below give every rule.

SqlSource treats a column as one that can be null unless it can show that the column is never null.  If it treats a column that is never null as one that can be null, the cost is a null check in your code.  It never gives you a wrong value.

SqlSource can tell that a column is never null when the column is one of these:

- **A `NOT NULL` column of a table**, when no outer join of the query can fill it with null.
- **`count(*)`, or `count(x)` of anything**, or a ranking over a window: `row_number()`, `rank()`, `dense_rank()`, `percent_rank()` or `cume_dist()`.  A count is `0` for no rows, never null.
- **A column of a view that is never null by these rules in the view's own query**, when the query does not select the view's whole column list.  A `SELECT *` from a view keeps the model named after the view, whose columns can be null.
- **A value that an `INSERT` writes and `RETURNING` returns**, when the value is never null by these rules and the `RETURNING` list is not the table's whole column list.  It does not count in any of these cases:
  - The statement has `ON CONFLICT DO UPDATE`.
  - The table, or any of its partitions, has a `BEFORE INSERT` row trigger, even a disabled one.
  - The value is a query parameter.
  - The value is in a `VALUES` list of more than one row.
  - The value comes from a `UNION`, an `INTERSECT` or an `EXCEPT`.
  - The table has a `DO ALSO` rule on `INSERT`.
  - The table is partitioned, on PostgreSQL 15 or earlier.
  - On PostgreSQL 18, the query holds the word `OLD` or `NEW` in any case, or the quoted name `"old"` or `"new"`.
- **A constant** other than `NULL`, such as `1` or `'active'`.
- **A test**: `IS NULL` or `IS NOT NULL`.
- **Arithmetic or a comparison of values that are never null**, in one of these forms:
  - `+`, `-`, `*` or `/` of two numbers;
  - `=`, `<>`, `<`, `<=`, `>` or `>=` of two numbers, two texts, two booleans, or two values of one of `date`, `timestamp` and `timestamp with time zone`;
  - `||` of two texts.
- **One of these functions, over values that are never null**:
  - `lower` or `upper` of a text;
  - `length` of a `text` or a `varchar` (not of a `character(n)`);
  - `date_trunc` of a `date`, a `timestamp` or a `timestamp with time zone`, without a time zone argument;
  - `abs` of a number;
  - `ceil` or `floor` of a `double precision` or a `numeric`;
  - `round` or `trunc` of a `double precision`, or of a `numeric` with a scale or without one.
- **A cast of a value that is never null**, in one of these ways:
  - from one number type to another;
  - from one text type to another;
  - between `date`, `timestamp` and `timestamp with time zone`.
- **One of the expressions above over a system column of a table**, such as `xmax = 0` in `RETURNING (xmax = 0) AS inserted`.  SqlSource treats a system column on its own, as in `RETURNING xmax`, as one that can be null.  The exception is a column of a `UNION`, an `INTERSECT` or an `EXCEPT`, at the end of this list.  There a system column of a table counts as never null: `SELECT xmin FROM a UNION ALL SELECT xmin FROM b` is never null.
- **`COALESCE`, `GREATEST` or `LEAST` with an argument that is never null**, or **a `CASE` with an `ELSE` whose every result is never null**.
  - The other rules of this list count such a form as a value that is never null.  So arithmetic, a comparison, a function, a cast, an aggregate and a subquery's value over it can count too.  For example, `sum(coalesce(x, 0))` in a query with a `GROUP BY` is never null.
  - This holds only where the tool knows the type of the form from its arguments that are never null.  A form over arguments of different types, or of a type that the tool does not know, counts only on its own.  The form is never null, but no rule of this list counts an expression over it.
- **`array_agg(...)` in a query with a `GROUP BY` of one column or more**, with no `FILTER` and no `OVER`.  Each group has a row, and the array of a row is never null.
- **`jsonb_build_object(...)` and `json_build_object(...)`**, unless the call is written with `VARIADIC`.  A call written with `VARIADIC` gives null for a null array.
- **A range's constructor** (`int4range`, `int8range`, `numrange`, `tsrange`, `tstzrange` or `daterange`, with two or three arguments), and **the intersection `*` of two ranges** that are never null.
- **`COALESCE`, `GREATEST` or `LEAST` over a column of each side of a `FULL JOIN`**, such as `coalesce(a.id, r.account_id)`.  `USING` writes the merged column in this form.  A row of a full join comes from one side at least, so one of the two columns holds a value.  The form counts only when all of these are true:
  - The column of each side is never null.  A cast of it counts when the cast stays within the numbers, the text types or the dates.
  - No outer join under a side can fill that column with null.  An outer join that fills only another table with null is fine.
  - The form uses the two columns themselves.  It can be above the join, as in a query that groups by both columns.
- **An aggregate of values that are never null, in a query with a `GROUP BY` of one column or more.**  It counts only when it has no `FILTER` and no `OVER`.  A rule below covers an aggregate with `OVER`.  It is one of these:
  - `max`, `min`, `sum` or `avg` of numbers;
  - `max` or `min` of texts or of `date`, `timestamp` or `timestamp with time zone`;
  - `string_agg` of two texts;
  - `bool_and`, `bool_or` or `every`.

  Each group of such a query has a row.  Without `GROUP BY`, a query over no rows returns one row whose `sum` is null.
- **One of those aggregates with `OVER`, of values that are never null, on PostgreSQL 18 and later**, such as `sum(total) OVER (PARTITION BY account_id ORDER BY placed_at)`.  Its window always holds the current row, so the aggregate has a value to read.  It counts only when all of these are true:
  - It has no `FILTER`.
  - The window has no frame clause, or each bound of its frame is `UNBOUNDED PRECEDING`, `CURRENT ROW` or `UNBOUNDED FOLLOWING`.
  - The frame has no `EXCLUDE`.
  - Its argument is a table column or an expression of this list, and not a column of a subquery or a `WITH` query.
- **`count(...)` without `OVER`, or `grouping(...)`, as the whole column of a query with `ROLLUP`, `CUBE` or `GROUPING SETS`.**  Each is a number in every group, the grand total included.
- **A column of a statement that is a `VALUES` alone**, when every row holds a literal in that column: a number with or without a `-`, a string in single quotes, `TRUE` or `FALSE`.  An `ORDER BY`, a `LIMIT`, an `OFFSET` or a `FETCH` after the rows changes nothing.  This holds only on a connection with `standard_conforming_strings` on, which is the default.
- **A subquery that is the whole column and returns one row**, such as `(SELECT count(*) FROM orders o WHERE o.account_id = a.id)`.  It counts only when all of these are true:
  - It has an aggregate, and no `GROUP BY`, `HAVING`, `LIMIT` or `OFFSET`.
  - Its value is `count(*)`, or arithmetic, a function or a cast of the list above over values that are never null.
  - Its value is of a number type, a text type, or `date`, `timestamp` or `timestamp with time zone`.

  SqlSource treats the same subquery inside an expression, as in `(SELECT count(*) ...) + 1`, as one that can be null.  On PostgreSQL 16 and earlier, in a query with query parameters, it can also treat a subquery that uses no column of the outer query as one that can be null.

  SqlSource also treats such a subquery as one that can be null in these places:

  - as the `y` of `x IN (SELECT y ...)` or `x = ANY (SELECT y ...)`;
  - as a branch of a `UNION`, an `INTERSECT` or an `EXCEPT`.

  SqlSource can treat a subquery of `min` or `max` as one that can be null when PostgreSQL reads the value from an index.  PostgreSQL can do so for an indexed column of all but the smallest tables.  It can also do so for a new or truncated table that has no size statistics, even an empty one.  So the answer can change with the table's rows and after `ANALYZE`, `VACUUM`, `TRUNCATE` or `CREATE INDEX`.
- **`EXISTS (...)` whose subquery uses a column of the outer query, on PostgreSQL 17 and later.**  PostgreSQL can run such an `EXISTS` as an `IN` when its subquery compares one of its columns with one of the outer query's.  Such an `EXISTS` is never null only when that `IN` would be.
- **`x IN (SELECT y ...)` or `x = ANY (SELECT y ...)`, on PostgreSQL 17 and later.**  It counts when `x` and the subquery's `y` are never null by the rules above, and the comparison is one of those above.
- **`merge_action()`** in the `RETURNING` list of a `MERGE`, on PostgreSQL 17 and later.
- **A column of a `UNION`, an `INTERSECT` or an `EXCEPT` at the top of the query.**  It counts when every value that it can return there is never null by the rules above.  These are the values that it can return:
  - for a `UNION`, the values of every branch;
  - for an `INTERSECT`, the values of either side, since one side is enough;
  - for an `EXCEPT`, the values of the left side.
- **A column that a subquery in `FROM` or a `WITH` query computes**, such as a `count(*)`.  It counts when all of these are true:
  - An outer join cannot fill it with null.
  - One column of the subquery at least is never null by the rules above and is of the same kind of type.  The kinds are the numbers (`smallint`, `integer`, `bigint`, `numeric`, `real` and `double precision`), the text types (`text`, `varchar` and `char`), and the dates (`date`, `timestamp` and `timestamp with time zone`).
  - Every other column of the subquery is never null and of that kind, or is of one of the other two kinds.  A column of any other type, such as `boolean`, `time`, `interval`, `money` or a domain, keeps the column from counting.  For a column of a text type, every other column must be a text type that is never null.
  - SqlSource knows the kind of a table column, a cast, and a function or an aggregate that this page lists.  Any other expression, such as `initcap(name)`, keeps the column from counting.
  - An expression in the subquery's `ORDER BY` that is not in its select list counts as one of its columns.
  - A column of the same kind that the outer query does not read counts as one that can be null, unless it is in the subquery's `GROUP BY`, the `WITH` query is `MATERIALIZED`, or the query reads the `WITH` query more than once.
  - The subquery has no `UNION`, `INTERSECT` or `EXCEPT`, and is not recursive.
  - No cast was added between two of these kinds of type.

`count(x)`, `array_agg`, the JSON builders and the range constructors and intersections count only when both of these are true:

- No schema of the database but `pg_catalog` holds a function or an operator of that name.
- No function or operator of that name was added to `pg_catalog`.

If another schema holds the name as well, or the name was added to `pg_catalog`, SqlSource treats the column as one that can be null.  The list below says so too.

Each of these counts only where an outer join cannot leave it null:

- `count(*)` and the rankings, and `count(x)`;
- `array_agg`, the JSON builders, and the range constructors and intersections;
- constants and tests;
- the arithmetic, comparisons, functions, casts, aggregates and subqueries above;
- `merge_action()`;
- `COALESCE`, `GREATEST`, `LEAST` and `CASE`.

SqlSource treats a column as one that can be null when one of these is true:

- **The table column can hold null.**  The column is not `NOT NULL`, or its `NOT NULL` was added as `NOT VALID` and was not validated.
- **The table can return null where its column says it cannot.**  SqlSource then treats every column of the table as one that can be null.  A table can do so when one of these is true of it:
  - It has or had inheritance children.
  - It is partitioned, and a foreign table is one of its partitions.
  - It has a `DO INSTEAD` rule on `INSERT`, `UPDATE` or `DELETE`.
- **An outer join can fill the column with null.**  This is a column of the optional side of a `LEFT`, `RIGHT` or `FULL` join.
- **A list after a `NOT` or used as a value can make a joined column one that can be null.**  This is so when all of these are true:
  - The query holds a [list](../../list-parameters.md#list-parameters) after a `NOT` or used as a value.
  - The column is of a table that the query joins to another, on either side of the join, or of a view whose query joins tables, or an expression over such a column.
  - For a table's own column, the query holds one of the words `LEFT`, `RIGHT`, `FULL`, `OUTER`, `ROLLUP`, `CUBE`, `GROUPING`, `MERGE`, `OLD` or `NEW`.

  A condition such as `o.id NOT IN @ids` or `(o.id IN @ids) IS FALSE` can be on the optional side of a `LEFT JOIN`.  Then it drops the rows that the join fills with null, for every list but an empty one.  An empty list keeps those rows.  So SqlSource treats every joined column of such a query as one that can be null.  The column of a query with no join is not affected, and neither is any column of a query that holds none of the words.

  A list is after a `NOT` in `o.id NOT IN @ids`, `NOT o.id IN @ids` or `NOT (o.id IN @ids)`.  A list is used as a value in one of these cases:
  - Its truth is compared, tested, cast or passed to a function, as in `(o.id IN @ids) IS FALSE`, `(o.id IN @ids) = false` or `COALESCE(o.id IN @ids, false)`.
  - It is in the output of a subquery or a `WITH` query.

  A list is not used as a value in these places:
  - in a condition that a `WHERE`, an `ON`, a `HAVING` or a `CASE WHEN` reads, alone or with `AND` and `OR`;
  - in the query's own select list, `ORDER BY`, `GROUP BY`, `RETURNING` or `SET`.

  A word counts in any case, anywhere outside a string, a comment or a quoted name.  `LEFT`, `RIGHT`, `FULL` and `OUTER` do not count where `(` follows them, as the name of a function such as `left(name, 3)`.  Two quoted names count as `OLD` and `NEW`: `"old"` and `"new"`, in lower case, as PostgreSQL compares a quoted name.  A name written with escapes, `U&"..."`, counts when it is one of the two.
- **The column comes from a materialized view, or from a view that the rules above do not prove it of.**  PostgreSQL does not record whether a view's column can be null.  A query of a view's whole column list counts as such a query.
- **The column comes from a foreign table.**  PostgreSQL does not enforce `NOT NULL` on its remote rows.
- **It is `NULL`, or an expression that is not in the list above.**  For example:
  - `initcap(name)`, or a call of another function;
  - `array_agg(...)` without `GROUP BY` or with `FILTER`;
  - `upper(during)` of a range;
  - `price * quantity` where either column can hold null;
  - `sum(total)` without `GROUP BY`;
  - `sum(total) OVER (...)` before PostgreSQL 18, over a column of a subquery or a `WITH` query, or with a `FILTER`, an `EXCLUDE` or a frame bound such as `1 PRECEDING`;
  - a column of `VALUES` where a row holds `NULL`, a parameter, a cast or another expression;
  - `EXISTS (...)` before PostgreSQL 17.
- **It is a function or an operator of a name that another schema of the database holds as well.**  An example is a `count` or a `*` that an extension installs, in its own schema or in `pg_catalog`.  This holds even for a column that the list above would count as never null.  The tool cannot tell which one the query calls.
- **It is a column of a `UNION`, an `INTERSECT` or an `EXCEPT` where a branch can give null.**  The same holds where SqlSource cannot tell whether a branch gives null.
- **It is an expression computed in one of these places:**
  - in a subquery in `FROM` or a `WITH` query that does not meet the conditions in the list above, such as a `count(*)` beside a number that can be null or that the outer query does not read;
  - in a recursive `WITH` query;
  - over a `UNION`, an `INTERSECT` or an `EXCEPT`, such as `count(*)` of a subquery that holds one.

  SqlSource can treat such a column as one that can be null even when the list above says it is never null.
- **The query has `ROLLUP`, `CUBE` or `GROUPING SETS`.**  SqlSource treats every column of such a query as one that can be null, except a `count(...)` without `OVER` or a `grouping(...)` that is the whole column.
- **On PostgreSQL 18, the query names `OLD` or `NEW`, and the column is a subquery, an `IN (SELECT ...)`, an `= ANY (SELECT ...)`, or an expression that a subquery in `FROM` or a `WITH` query computes.**  This holds even for one that the list above says is never null.
- **On PostgreSQL 18, the query names `OLD` or `NEW`, and a column of its `RETURNING` list is one of these:**
  - a column of a table aliased `old` or `new`, or by a name that `RETURNING WITH (OLD AS ..., NEW AS ...)` gives, unless the table is the one the statement changes;
  - an expression over a column written without its table's name, in an `UPDATE` or a `DELETE` of one table.

  SqlSource does not tell such a column from one of the old or the new row, which is null where the row does not exist.
- **The search path of the connection puts another schema before `pg_catalog`, and the column is one of these.**  This holds even for one that the list above says is never null:
  - `count(*)`, `count(x)`, a ranking, `array_agg`, a JSON builder, or a range constructor or intersection;
  - arithmetic, a comparison, a function, a cast, an aggregate or a subquery;
  - an `EXISTS (...)` that PostgreSQL runs as an `IN`.  PostgreSQL can do so when the subquery of the `EXISTS` compares one of its columns with one of the outer query's.  The list above says so too;
  - a `COALESCE`, `GREATEST` or `LEAST` over both sides of a `FULL JOIN`.

  These are not affected: constants, tests, `COALESCE` over columns that are never null, any other `EXISTS (...)`, `merge_action()`, `grouping(...)` and a column of literals of a `VALUES` statement.  To avoid this, keep `pg_catalog` first on the search path of the connection that the tool uses.  Or leave `pg_catalog` off the path: PostgreSQL then searches it first.
- **The query has an outer join, `MERGE`, `OLD` or `NEW`, and the column comes from one of these places:**
  - through a subquery or a `WITH` query;
  - from a table that an extension such as Citus or TimescaleDB manages.

  SqlSource can treat such a column as one that can be null even when its table column is `NOT NULL`.

A column that can be null gets a nullable property in the query's [output model](index.md#models), such as `int?` or `string?`.  A column that is never null gets a property that cannot be null, such as `int` or `string`.  Where you know better than these rules, you decide instead, in one of these ways:

- An alias that ends in `!` gives a property that cannot be null.  An alias that ends in `?` gives a property that can be null.
- A `-- column:` marker says the same, and leaves the name of the column alone.

```sql postgres
-- name: GetCustomerLabels
SELECT c.id, initcap(c.name) AS "display_name!"
FROM customers c;
```

`initcap` of a value that is not null is never null, but SqlSource treats it as one that can be null.  In the sample, `"display_name!"` gives the property `DisplayName` the type `string`, and not `string?`.  Write the alias in quotes, since `!` and `?` are not part of a plain name.  SqlSource leaves the mark out of the name of the property.

A `!` is your word, and the build does not check it.  If such a column is null after all, a [query method](../../query-methods.md#reading-rows) throws.

The mark stays in the name of the column, and Dapper matches the name of the column to the name of the property.  So with Dapper, write a `-- column:` marker and no mark:

```sql postgres
-- name: GetCustomerLabels
-- column: display_name not null
SELECT c.id, initcap(c.name) AS display_name
FROM customers c;
```

See [which properties can be null](../../model-properties.md#model-nullability) and [the `-- column:` marker](../../model-properties.md#column-marker).

<a id="limits"></a>

## Limits

<a id="limit-columns-treated-as-nullable"></a>

### A property can be null though its column is never null

**What you see.**  A column is never null, but its property in the query's model is nullable.  The property costs a null check in your code.

**Why.**  The rules of [which columns can be null](#nullability) leave out many columns that a person can see are never null.  SqlSource treats each of them as one that can be null.  Nothing is wrong when that happens.  These are some of the columns that the rules leave out:

- `EXISTS (...)` as a column before PostgreSQL 17;
- a column of `VALUES` where a row holds anything but a literal;
- a window aggregate before PostgreSQL 18, or one over a column of a subquery or a `WITH` query;
- a function that the rules do not list;
- `IS DISTINCT FROM` and `IS NOT DISTINCT FROM`, which are null when a user's `=` operator returns null;
- an expression, when the search path puts another schema before `pg_catalog`;
- a `count(*)` or a `sum` that a `WITH` query computes beside a column that keeps it from counting, such as a number that can be null or that the outer query does not read, or an `initcap(name)`;
- a subquery that uses no column of the outer query, in a query with query parameters, on PostgreSQL 16 and earlier;
- a subquery of `min` or `max` that PostgreSQL reads from an index, such as `coalesce(max(id), 0)`;
- a subquery as the `y` of an `IN` or an `= ANY`, or as a branch of a `UNION`, an `INTERSECT` or an `EXCEPT`;
- a recursive `WITH` query;
- a view's whole column list, a `GROUP BY` column of a view, and a column of a view over a `UNION`;
- a value that an `INSERT` takes from a `VALUES` list of more than one row, or from a `UNION`, an `INTERSECT` or an `EXCEPT`;
- a table's whole column list in `RETURNING`, and a value that an `UPDATE`, a `DELETE` or a `MERGE` writes.

**What to do.**  Where you know that the column is never null, do one of these:

- End its alias with `!`.
- When you read the rows with Dapper, write a `-- column:` marker that says `not null`, as [which columns can be null](#nullability) shows.
