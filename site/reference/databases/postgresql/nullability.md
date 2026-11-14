# PostgreSQL: which columns can be null

<a id="nullability"></a>

## Which columns can be null

```sql postgres
-- name: ListOrders
SELECT o.id, o.placed_at, c.name
FROM orders o
LEFT JOIN customers c ON c.id = o.customer_id;
```

For each column the tool records whether it can be null.  The usual reasons a column is recorded as possibly null are an outer join, a view, or an expression such as `initcap(name)` or a call of a function that the lists below do not name; the lists give every rule.  If `orders.id`, `orders.placed_at` and `customers.name` are all `NOT NULL`, `id` and `placed_at` above are never null, and `name` can be: the `LEFT JOIN` gives a null name to an order with no customer.

SqlSource treats a column as possibly null unless it can show that it never is.  A column it treats as possibly null that never is costs a null check in your code.  It never gives you a wrong value.

SqlSource can tell that a column is never null when it is:

- **A `NOT NULL` column of a table**, which no outer join of the query can fill with null.
- **`count(*)`, or `count(x)` of anything**, or a ranking over a window: `row_number()`, `rank()`, `dense_rank()`, `percent_rank()` or `cume_dist()`.  A count is `0` for no rows, never null.
- **A constant** other than `NULL`, such as `1` or `'active'`.
- **A test**: `IS NULL`, `IS NOT NULL`, `IS DISTINCT FROM` or `IS NOT DISTINCT FROM`.
- **Arithmetic or a comparison of values that are never null**: `+`, `-`, `*` or `/` of two numbers; `=`, `<>`, `<`, `<=`, `>` or `>=` of two numbers, two texts, two booleans, or two values of one of `date`, `timestamp` and `timestamp with time zone`; and `||` of two texts.
- **`lower` or `upper` of a text, `length` of a `text` or a `varchar` (not of a `character(n)`), or `date_trunc` of a `date`, a `timestamp` or a `timestamp with time zone` without a time zone argument**, over values that are never null, and **a cast** of a value that is never null from one number type to another, from one text type to another, or between `date`, `timestamp` and `timestamp with time zone`.
- **One of the expressions above over a system column of a table**, such as `xmax = 0` in `RETURNING (xmax = 0) AS inserted`.  A system column on its own, `RETURNING xmax`, is possibly null, except as a column of a `UNION`, an `INTERSECT` or an `EXCEPT` below, where a system column of a table counts as never null: `SELECT xmin FROM a UNION ALL SELECT xmin FROM b` is never null.
- **`COALESCE`, `GREATEST` or `LEAST`** with an argument that is never null, or **a `CASE` with an `ELSE`** whose every result is never null.  Arithmetic, a comparison, a function, a cast, an aggregate and a subquery's value over one are read by the rules in this list, so `sum(coalesce(x, 0))` in a query with a `GROUP BY` is never null, unless the `WHERE` sets every column of the `GROUP BY` to one value.  This holds where the tool knows the form's type from its arguments that are never null; a form over arguments of different types, or of a type the tool does not know, counts only on its own.
- **`array_agg(...)` in a query with a `GROUP BY` of one column or more**, with no `FILTER` and no `OVER`: each group has a row, and the array of a row is never null.  It can be possibly null when the `WHERE` sets every column of the `GROUP BY` to one value, as `WHERE o.user_id = @userId GROUP BY o.user_id` does.
- **`jsonb_build_object(...)` and `json_build_object(...)`**, unless the call is written with `VARIADIC`, which gives null for a null array.
- **A range's constructor** (`int4range`, `int8range`, `numrange`, `tsrange`, `tstzrange` or `daterange`, with two or three arguments), and **the intersection `*` of two ranges** that are never null.
- **`COALESCE`, `GREATEST` or `LEAST` over a column of each side of a `FULL JOIN`**, as `USING` writes the merged column: `coalesce(a.id, r.account_id)`, with a column that is never null on each side.  A row of a full join comes from one side at least, so one of the two columns holds a value.  It counts only where neither side is itself an outer join; an inner join under a side is fine.  A form above the join, or one over a cast of a column, does not count.
- **`max`, `min`, `sum` or `avg` of numbers, `max` or `min` of texts or of `date`, `timestamp` or `timestamp with time zone`, `string_agg` of two texts, or `bool_and`, `bool_or` or `every`** of values that are never null, in a query with a `GROUP BY` of one column or more, when it has no `FILTER` and no `OVER`.  Each group of such a query has a row; without `GROUP BY`, a query over no rows returns one row whose `sum` is null.  It can be possibly null when the `WHERE` sets every column of the `GROUP BY` to one value, as `WHERE o.user_id = @userId GROUP BY o.user_id` does.
- **A subquery that is the whole column, uses a column of the outer query and returns one row**, such as `(SELECT count(*) FROM orders o WHERE o.account_id = a.id)`: one with an aggregate and no `GROUP BY`, `HAVING`, `LIMIT` or `OFFSET`, whose value is `count(*)`, or arithmetic, a function or a cast of the list above over values that are never null, of a number type, a text type, or `date`, `timestamp` or `timestamp with time zone`.  The same subquery inside an expression, `(SELECT count(*) ...) + 1`, is possibly null.
- **`EXISTS (...)` whose subquery uses a column of the outer query, on PostgreSQL 17 and later.**  PostgreSQL may run an `EXISTS` whose subquery compares one of its columns with one of the outer query's as an `IN`, and such an `EXISTS` is never null only as that `IN` would be.
- **`x IN (SELECT y ...)` or `x = ANY (SELECT y ...)`, on PostgreSQL 17 and later,** when `x` and the subquery's `y` are never null by the rules above, and the comparison is one of those above.
- **`merge_action()`** in the `RETURNING` list of a `MERGE`, on PostgreSQL 17 and later.
- **A column of a `UNION`, an `INTERSECT` or an `EXCEPT`** at the top of the query, when every value it can return there is never null by the rules above: every branch of a `UNION`, either side of an `INTERSECT`, the left side of an `EXCEPT`.

`count(x)`, `array_agg`, the JSON builders and the range constructors and intersections count only when no schema of the database but `pg_catalog` holds a function or an operator of that name, and none was added to `pg_catalog`; a name that another schema holds as well, or that was added to `pg_catalog`, is possibly null, as the lists below say.

`count(*)` and the rankings, `count(x)`, `array_agg`, the JSON builders, the range constructors and intersections, constants, tests, the arithmetic, comparisons, functions, casts, aggregates and subqueries above, `merge_action()`, and `COALESCE`, `GREATEST`, `LEAST` and `CASE` each count only where an outer join cannot leave them null.

SqlSource treats a column as possibly null when:

- **The table column can hold null**: it is not `NOT NULL`, or its `NOT NULL` was added as `NOT VALID` and not validated.
- **The table can return null where its column says it cannot**: the table has or had inheritance children, it is partitioned and a foreign table is one of its partitions, or it has a `DO INSTEAD` rule on `INSERT`, `UPDATE` or `DELETE`.  Every column of such a table is possibly null.
- **An outer join can fill the column with null**: a column of the optional side of a `LEFT`, `RIGHT` or `FULL` join.
- **The query holds one of the words `LEFT`, `RIGHT`, `FULL`, `OUTER`, `ROLLUP`, `CUBE`, `GROUPING`, `MERGE`, `OLD` or `NEW`, and a [list](../../parameters.md#list-parameters) after a `NOT` or used as a value**, and the column is of a table that the query joins to another, on either side of the join.  A list is after a `NOT` in `o.id NOT IN @ids`, `NOT o.id IN @ids` or `NOT (o.id IN @ids)`.  It is used as a value when its truth is compared, tested, cast or passed to a function, as in `(o.id IN @ids) IS FALSE`, `(o.id IN @ids) = false` or `COALESCE(o.id IN @ids, false)`, or when it is in the output of a subquery or a `WITH` query.  A list that a `WHERE`, an `ON`, a `HAVING` or a `CASE WHEN` reads as a condition, alone or with `AND` and `OR`, is not used as a value, and neither is one in the query's own select list, `ORDER BY`, `GROUP BY`, `RETURNING` or `SET`.  A word counts anywhere outside a string or a comment, even as the name of a function such as `left(name, 3)` in a query whose joins are all inner, and outside a quoted name, but for `"old"` and `"new"`, in any case, and a name written `U&"..."`, which count as `OLD` and `NEW`.  A condition such as `o.id NOT IN @ids` or `(o.id IN @ids) IS FALSE` on the optional side of a `LEFT JOIN` drops the rows the join fills with null for every list but an empty one, which keeps them, so every joined column of such a query is possibly null.  The column of a query with no join, and every column of a query that holds none of the words, is not affected.
- **The column comes from a view or a materialized view.**  PostgreSQL does not record whether a view's column can be null.
- **The column comes from a foreign table.**  PostgreSQL does not enforce `NOT NULL` on its remote rows.
- **It is `NULL`**, or an expression that is not in the list above, such as `initcap(name)`, `array_agg(...)` without `GROUP BY` or with `FILTER`, `upper(during)` of a range, `price * quantity` where either column can hold null, `sum(total)` without `GROUP BY` or with a `GROUP BY` whose every column the `WHERE` sets to one value, `EXISTS (...)` before PostgreSQL 17, or a call of another function.
- **It is a function or an operator of a name that another schema of the database holds as well**, such as a `count` or a `*` that an extension installs, in its own schema or in `pg_catalog`, in a column the list above would have read.  The tool cannot tell which one the query calls.
- **It is a column of a `UNION`, an `INTERSECT` or an `EXCEPT`** where a branch can give null, or where SqlSource cannot follow a branch.
- **It is an expression that a subquery in `FROM` or a `WITH` query computes**, such as a `count(*)` that a `WITH` query returns, **or one computed over a `UNION`, an `INTERSECT` or an `EXCEPT`**, such as `count(*)` of a subquery that holds one.  Such a column can be treated as possibly null even when the list above says it is never null.
- **The query has `ROLLUP`, `CUBE` or `GROUPING SETS`.**  Every column of such a query is possibly null.
- **The query names `OLD` or `NEW`, and the column is a subquery, an `IN (SELECT ...)` or an `= ANY (SELECT ...)`**, even one the list above says is never null.
- **The connection's search path puts another schema before `pg_catalog`, and the column is `count(*)`, `count(x)`, a ranking, `array_agg`, a JSON builder, a range constructor or intersection, arithmetic, a comparison, a function, a cast, an aggregate, a subquery, or an `EXISTS (...)` that PostgreSQL runs as an `IN`**, even one the list above says is never null.  PostgreSQL may run so an `EXISTS` whose subquery compares one of its columns with one of the outer query's, as the list above says.  A `COALESCE`, `GREATEST` or `LEAST` over both sides of a `FULL JOIN` is affected too.  Constants, tests, `COALESCE` over columns that are never null, any other `EXISTS (...)` and `merge_action()` are not affected.  Keep `pg_catalog` first on the search path of the connection the tool uses, or leave it off the path, where PostgreSQL searches it first.
- **The query has an outer join, `MERGE`, `OLD` or `NEW`, and the column comes through a subquery or a `WITH` query, or from a table that an extension such as Citus or TimescaleDB manages.**  Such a column can be treated as possibly null even when its table column is `NOT NULL`.

A column that can be null gets a nullable property in the query's [output model](index.md#models), `int?` or `string?`, and a column that is never null a property that cannot be null, `int` or `string`.  Where you know better than these rules, you decide instead: an alias that ends in `!` gives a property that cannot be null, and one that ends in `?` a property that can, or a `-- column:` marker says the same and leaves the column's name alone.

```sql postgres
-- name: GetCustomerLabels
SELECT c.id, initcap(c.name) AS "display_name!"
FROM customers c;
```

`initcap` is never null for a value that is not, though SqlSource treats it as possibly null: `"display_name!"` gives the property `DisplayName` the type `string` and not `string?`.  Write the alias in quotes, since `!` and `?` are not part of a plain name, and the mark is left out of the property's name.  A `!` is your word, and the build does not check it: a [query method](../../../generator.md#reading-rows) throws when such a column is null after all.  The mark stays in the column's name, which Dapper matches to the property's by name, so with Dapper write a `-- column:` marker and no mark:

```sql postgres
-- name: GetCustomerLabels
-- column: display_name not null
SELECT c.id, initcap(c.name) AS display_name
FROM customers c;
```

See [which properties can be null](../../../generator.md#model-nullability) and [the `-- column:` marker](../../../generator.md#column-marker).

<a id="limits"></a>

## Limits

<a id="limit-columns-treated-as-nullable"></a>

### A property can be null though its column is never null

- **Many columns that are never null are treated as possibly null.**  The rules [above](#nullability) leave out much that a person can see: `EXISTS (...)` as a column before PostgreSQL 17, the rows of `VALUES`, a function not listed there, an expression when the search path puts another schema before `pg_catalog`, a `count(*)` or a `sum` that a `WITH` query computes, a `sum` or an `array_agg` in a query whose `WHERE` sets every column of the `GROUP BY` to one value, a `COALESCE` over a column of each side of a `FULL JOIN` that is cast or computed above the join, a recursive `WITH` query, the columns of a view.  Nothing is wrong when that happens, but the column's property in the query's model is nullable, and costs a null check in your code.  Where you know the column is never null, end its alias with `!`, or, when you read the rows with Dapper, write a `-- column:` marker that says `not null`, as [above](#nullability).
