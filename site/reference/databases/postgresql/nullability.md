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

The usual reasons that a column can be null are an outer join, a view, or an expression such as `initcap(name)`.  A call of a function that the lists below do not name is such an expression.  The lists below give every rule.

SqlSource treats a column as one that can be null unless it can show that the column is never null.  If it treats a column that is never null as one that can be null, the cost is a null check in your code.  It never gives you a wrong value.

SqlSource can tell that a column is never null when the column is one of these:

- **A `NOT NULL` column of a table**, when no outer join of the query can fill it with null.
- **`count(*)`, or `count(x)` of anything**, or a ranking over a window: `row_number()`, `rank()`, `dense_rank()`, `percent_rank()` or `cume_dist()`.  A count is `0` for no rows, never null.
- **A constant** other than `NULL`, such as `1` or `'active'`.
- **A test**: `IS NULL`, `IS NOT NULL`, `IS DISTINCT FROM` or `IS NOT DISTINCT FROM`.
- **Arithmetic or a comparison of values that are never null**, in one of these forms:
  - `+`, `-`, `*` or `/` of two numbers;
  - `=`, `<>`, `<`, `<=`, `>` or `>=` of two numbers, two texts, two booleans, or two values of one of `date`, `timestamp` and `timestamp with time zone`;
  - `||` of two texts.
- **One of these functions, over values that are never null**:
  - `lower` or `upper` of a text;
  - `length` of a `text` or a `varchar` (not of a `character(n)`);
  - `date_trunc` of a `date`, a `timestamp` or a `timestamp with time zone`, without a time zone argument.
- **A cast of a value that is never null**, in one of these ways:
  - from one number type to another;
  - from one text type to another;
  - between `date`, `timestamp` and `timestamp with time zone`.
- **One of the expressions above over a system column of a table**, such as `xmax = 0` in `RETURNING (xmax = 0) AS inserted`.  SqlSource treats a system column on its own, as in `RETURNING xmax`, as one that can be null.  The exception is a column of a `UNION`, an `INTERSECT` or an `EXCEPT`, at the end of this list.  There a system column of a table counts as never null: `SELECT xmin FROM a UNION ALL SELECT xmin FROM b` is never null.
- **`COALESCE`, `GREATEST` or `LEAST` with an argument that is never null**, or **a `CASE` with an `ELSE` whose every result is never null**.
  - The other rules of this list count such a form as a value that is never null.  So arithmetic, a comparison, a function, a cast, an aggregate and a subquery's value over it can count too.  For example, `sum(coalesce(x, 0))` in a query with a `GROUP BY` is never null, unless the `WHERE` sets every column of the `GROUP BY` to one value.
  - This holds only where the tool knows the type of the form from its arguments that are never null.  A form over arguments of different types, or of a type that the tool does not know, counts only on its own.  The form is never null, but no rule of this list counts an expression over it.
- **`array_agg(...)` in a query with a `GROUP BY` of one column or more**, with no `FILTER` and no `OVER`.  Each group has a row, and the array of a row is never null.  SqlSource treats it as one that can be null when the `WHERE` sets every column of the `GROUP BY` to one value, as `WHERE o.user_id = @userId GROUP BY o.user_id` does.
- **`jsonb_build_object(...)` and `json_build_object(...)`**, unless the call is written with `VARIADIC`.  A call written with `VARIADIC` gives null for a null array.
- **A range's constructor** (`int4range`, `int8range`, `numrange`, `tsrange`, `tstzrange` or `daterange`, with two or three arguments), and **the intersection `*` of two ranges** that are never null.
- **`COALESCE`, `GREATEST` or `LEAST` over a column of each side of a `FULL JOIN`**, such as `coalesce(a.id, r.account_id)`.  `USING` writes the merged column in this form.  A row of a full join comes from one side at least, so one of the two columns holds a value.  The form counts only when all of these are true:
  - The column of each side is never null.
  - Neither side is itself an outer join.  An inner join under a side is fine.
  - The form is not above the join, and not over a cast of a column.
- **An aggregate of values that are never null, in a query with a `GROUP BY` of one column or more.**  It counts only when it has no `FILTER` and no `OVER`.  It is one of these:
  - `max`, `min`, `sum` or `avg` of numbers;
  - `max` or `min` of texts or of `date`, `timestamp` or `timestamp with time zone`;
  - `string_agg` of two texts;
  - `bool_and`, `bool_or` or `every`.

  Each group of such a query has a row.  Without `GROUP BY`, a query over no rows returns one row whose `sum` is null.  SqlSource treats the aggregate as one that can be null when the `WHERE` sets every column of the `GROUP BY` to one value, as `WHERE o.user_id = @userId GROUP BY o.user_id` does.
- **A subquery that is the whole column and returns one row**, such as `(SELECT count(*) FROM orders o WHERE o.account_id = a.id)`.  It counts only when all of these are true:
  - It uses a column of the outer query.
  - It has an aggregate, and no `GROUP BY`, `HAVING`, `LIMIT` or `OFFSET`.
  - Its value is `count(*)`, or arithmetic, a function or a cast of the list above over values that are never null.
  - Its value is of a number type, a text type, or `date`, `timestamp` or `timestamp with time zone`.

  SqlSource treats the same subquery inside an expression, as in `(SELECT count(*) ...) + 1`, as one that can be null.
- **`EXISTS (...)` whose subquery uses a column of the outer query, on PostgreSQL 17 and later.**  PostgreSQL can run such an `EXISTS` as an `IN` when its subquery compares one of its columns with one of the outer query's.  Such an `EXISTS` is never null only when that `IN` would be.
- **`x IN (SELECT y ...)` or `x = ANY (SELECT y ...)`, on PostgreSQL 17 and later.**  It counts when `x` and the subquery's `y` are never null by the rules above, and the comparison is one of those above.
- **`merge_action()`** in the `RETURNING` list of a `MERGE`, on PostgreSQL 17 and later.
- **A column of a `UNION`, an `INTERSECT` or an `EXCEPT` at the top of the query.**  It counts when every value that it can return there is never null by the rules above.  These are the values that it can return:
  - for a `UNION`, the values of every branch;
  - for an `INTERSECT`, the values of either side, since one side is enough;
  - for an `EXCEPT`, the values of the left side.

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
  - The query holds one of the words `LEFT`, `RIGHT`, `FULL`, `OUTER`, `ROLLUP`, `CUBE`, `GROUPING`, `MERGE`, `OLD` or `NEW`.
  - The query holds a [list](../../parameters.md#list-parameters) after a `NOT` or used as a value.
  - The column is of a table that the query joins to another, on either side of the join.

  A condition such as `o.id NOT IN @ids` or `(o.id IN @ids) IS FALSE` can be on the optional side of a `LEFT JOIN`.  Then it drops the rows that the join fills with null, for every list but an empty one.  An empty list keeps those rows.  So SqlSource treats every joined column of such a query as one that can be null.  The column of a query with no join is not affected, and neither is any column of a query that holds none of the words.

  A list is after a `NOT` in `o.id NOT IN @ids`, `NOT o.id IN @ids` or `NOT (o.id IN @ids)`.  A list is used as a value in one of these cases:
  - Its truth is compared, tested, cast or passed to a function, as in `(o.id IN @ids) IS FALSE`, `(o.id IN @ids) = false` or `COALESCE(o.id IN @ids, false)`.
  - It is in the output of a subquery or a `WITH` query.

  A list is not used as a value in these places:
  - in a condition that a `WHERE`, an `ON`, a `HAVING` or a `CASE WHEN` reads, alone or with `AND` and `OR`;
  - in the query's own select list, `ORDER BY`, `GROUP BY`, `RETURNING` or `SET`.

  A word counts in any case, anywhere outside a string, a comment or a quoted name.  It counts even as the name of a function, such as `left(name, 3)` in a query whose joins are all inner.  Two quoted names count as `OLD` and `NEW`: `"old"` and `"new"`, in any case.  So does a name written `U&"..."`.
- **The column comes from a view or a materialized view.**  PostgreSQL does not record whether a view's column can be null.
- **The column comes from a foreign table.**  PostgreSQL does not enforce `NOT NULL` on its remote rows.
- **It is `NULL`, or an expression that is not in the list above.**  For example:
  - `initcap(name)`, or a call of another function;
  - `array_agg(...)` without `GROUP BY` or with `FILTER`;
  - `upper(during)` of a range;
  - `price * quantity` where either column can hold null;
  - `sum(total)` without `GROUP BY`, or with a `GROUP BY` whose every column the `WHERE` sets to one value;
  - `EXISTS (...)` before PostgreSQL 17.
- **It is a function or an operator of a name that another schema of the database holds as well.**  An example is a `count` or a `*` that an extension installs, in its own schema or in `pg_catalog`.  This holds even for a column that the list above would count as never null.  The tool cannot tell which one the query calls.
- **It is a column of a `UNION`, an `INTERSECT` or an `EXCEPT`** where a branch can give null, or where SqlSource cannot tell whether a branch gives null.
- **It is an expression computed in one of these places:**
  - in a subquery in `FROM` or a `WITH` query, such as a `count(*)` that a `WITH` query returns;
  - over a `UNION`, an `INTERSECT` or an `EXCEPT`, such as `count(*)` of a subquery that holds one.

  SqlSource can treat such a column as one that can be null even when the list above says it is never null.
- **The query has `ROLLUP`, `CUBE` or `GROUPING SETS`.**  SqlSource treats every column of such a query as one that can be null.
- **The query names `OLD` or `NEW`, and the column is a subquery, an `IN (SELECT ...)` or an `= ANY (SELECT ...)`.**  This holds even for one that the list above says is never null.
- **The search path of the connection puts another schema before `pg_catalog`, and the column is one of these.**  This holds even for one that the list above says is never null:
  - `count(*)`, `count(x)`, a ranking, `array_agg`, a JSON builder, or a range constructor or intersection;
  - arithmetic, a comparison, a function, a cast, an aggregate or a subquery;
  - an `EXISTS (...)` that PostgreSQL runs as an `IN`.  PostgreSQL can do so when the subquery of the `EXISTS` compares one of its columns with one of the outer query's, as the list above says;
  - a `COALESCE`, `GREATEST` or `LEAST` over both sides of a `FULL JOIN`.

  These are not affected: constants, tests, `COALESCE` over columns that are never null, any other `EXISTS (...)`, and `merge_action()`.  To avoid this, keep `pg_catalog` first on the search path of the connection that the tool uses.  Or leave `pg_catalog` off the path: PostgreSQL then searches it first.
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
- the rows of `VALUES`;
- a function that the rules do not list;
- an expression, when the search path puts another schema before `pg_catalog`;
- a `count(*)` or a `sum` that a `WITH` query computes;
- a `sum` or an `array_agg` in a query whose `WHERE` sets every column of the `GROUP BY` to one value;
- a `COALESCE` over a column of each side of a `FULL JOIN` that is cast or computed above the join;
- a recursive `WITH` query;
- the columns of a view.

**What to do.**  Where you know that the column is never null, do one of these:

- End its alias with `!`.
- When you read the rows with Dapper, write a `-- column:` marker that says `not null`, as [which columns can be null](#nullability) shows.
