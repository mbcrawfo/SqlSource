# Fix a nullable property

Make a nullable property, such as `string?` or `int?`, not null, and stop the null reference warning it gives.

**Use this when** a property is nullable, but its column is never null.  Use it too when a property must be nullable, and is not.

<a id="why"></a>

## Why a property is nullable

SqlSource treats a column as possibly null unless it can show that it never is.  See which columns can be null on [PostgreSQL](../reference/databases/postgresql/nullability.md#nullability) and [SQL Server](../reference/databases/sqlserver/nullability.md#nullability).  The cost is a null check in your code, never a wrong value.  You can overrule it in the `.sql` file.

**The rules you need.**

- A column that can be null gets a property that can be null, such as `int?`.  A column that is never null gets one that cannot, such as `int`.  See [Which properties can be null](../reference/model-properties.md#model-nullability).

<a id="not-null"></a>

## Say that a column is never null

If you know that a column is never null, add `-- column: <name> not null` inside its query:

# [PostgreSQL](#tab/postgresql)

```sql postgres
-- name: ListUserDisplayNames
-- column: display_name not null
SELECT
    u.id,
    initcap(u.name) AS display_name
FROM users AS u
ORDER BY u.id;
```

# [SQL Server](#tab/sqlserver)

```sql mssql
-- name: ListOrderMonths
-- column: month_start not null
SELECT
    id,
    DATEFROMPARTS(YEAR(placed_at), MONTH(placed_at), 1) AS month_start
FROM orders
ORDER BY id;
```

---

Or end the column's alias with `!`:

# [PostgreSQL](#tab/postgresql)

The alias is in double quotes: `initcap(u.name) AS "display_name!"`.

# [SQL Server](#tab/sqlserver)

The alias is in brackets: `DATEFROMPARTS(YEAR(placed_at), MONTH(placed_at), 1) AS [month_start!]`.

---

**What you get.**  `ListUserDisplayNamesDto.DisplayName` is a `string`, not a `string?`.  `ListOrderMonthsDto.MonthStart` is a `DateTime`, not a `DateTime?`.

**The rules you need.**

- The build does not check a `!` or a `not null`.  If the column is null after all, the method throws.  See [How a row is read](../reference/query-methods.md#reading-rows).
- The `-- column:` line names the column as the query returns it.  It is a comment, so the query needs no describing again.  See [Setting a column's nullability or type with a marker](../reference/model-properties.md#column-marker).
- The `!` stays in the column's name.  A change to an alias changes the query, so run `dotnet sqlsource describe`.  See [Which properties can be null](../reference/model-properties.md#model-nullability) and [Describing again](../reference/sidecars.md#describing-again).
- With Dapper, use the `-- column:` line, since Dapper matches columns to properties by name.  See [Which properties can be null](../reference/model-properties.md#model-nullability).

<a id="nullable"></a>

## Say that a column can be null

If a method throws "Column 'name' is null." or "Data is Null.", a null reached a property that cannot be null.  See [How a row is read](../reference/query-methods.md#reading-rows).  Remove your `!` or `not null`, if any.  Otherwise, add `-- column: <name> null`:

```sql postgres
-- name: ListUserNames
-- column: name null
SELECT id, name
FROM users;
```

Or end the alias with `?`: `AS "name?"` on PostgreSQL, `AS [name?]` on SQL Server.

**What you get.**  `ListUserNamesDto.Name` is a `string?`.

**The rules you need.**

- `null` or `?` gives a property that can be null, whatever SqlSource found.  See [Which properties can be null](../reference/model-properties.md#model-nullability).

<a id="parameters"></a>

## Make a query parameter nullable

If a query parameter can be null, add `null` to its `-- param:` line:

# [PostgreSQL](#tab/postgresql)

```sql postgres
-- name: FindUsers
-- param: @name text null
SELECT
    id,
    name,
    email
FROM users
WHERE @name IS NULL OR name = @name
ORDER BY name;
```

# [SQL Server](#tab/sqlserver)

```sql mssql
-- name: FindUsers
-- param: @name nvarchar(100) null
SELECT
    id,
    name,
    email
FROM users
WHERE @name IS NULL OR name = @name
ORDER BY name;
```

---

**What you get.**  `FindUsersParams.Name` is a `string?`, so `new FindUsersParams(null)` finds every user.

**The rules you need.**

- A query parameter can be null only when its `-- param:` line says `null`, or, for a list, when the query tests the list for null.  See [Which properties can be null](../reference/model-properties.md#model-nullability).
- `not null` means the same as no word, and makes nothing not null.  See [Giving a parameter a type: `-- param:`](../reference/parameters.md#param-marker).
- After a change of the type or the nullability, `not null` included, run `dotnet sqlsource describe` again.  See [Giving a parameter a type: `-- param:`](../reference/parameters.md#param-marker).

<a id="on-postgresql"></a>

## On PostgreSQL

On PostgreSQL, a column most often gets a nullable property when:

- It is on the optional side of a `LEFT`, `RIGHT` or `FULL` join.
- It comes from a view, and the query reads the view's whole column list or the full rules do not prove the column.
- It is a call of a function that the full rules do not list, such as `initcap(name)`.
- It is `sum`, `max`, `min` or `avg` with no `GROUP BY`, which is null over no rows.
- A `WITH` query or a subquery in `FROM` computes it, such as a `count(*)`, beside a column that keeps it from counting, such as a number that can be null or that the query does not read.

To fix one, [say that the column is never null](#not-null).  See [Which columns can be null](../reference/databases/postgresql/nullability.md#nullability) and [the limit](../reference/databases/postgresql/nullability.md#limit-columns-treated-as-nullable).

<a id="on-sql-server"></a>

## On SQL Server

On SQL Server, a column most often gets a nullable property when:

- It is on the optional side of an outer join or an `OUTER APPLY`.
- A filter keeps it from being null in a form such as `x IN (1, 2)` or `x BETWEEN 1 AND 2`.
- It is a function such as `DATEFROMPARTS` or `SWITCHOFFSET`, even of values that are never null.
- It is `CASE WHEN x IS NULL THEN 0 ELSE x END` of a computed column, a view's column or a subquery's expression.  Write `ISNULL(x, 0)`, which is never null.
- It is a count or a sum in an `OUTPUT` clause.

To fix one, [say that the column is never null](#not-null).  See [Which columns can be null](../reference/databases/sqlserver/nullability.md#nullability) and [the limit](../reference/databases/sqlserver/nullability.md#limit-columns-treated-as-nullable).

## If it does not work

- **"Column 'name' is null.", or on SQL Server Microsoft.Data.SqlClient's `SqlNullValueException`, "Data is Null.".**  A null reached a property that cannot be null: [say that the column can be null](#nullable).  See [How a row is read](../reference/query-methods.md#reading-rows).  On SQL Server, do not run `SET ANSI_WARNINGS OFF` on the connection: see [the limit](../reference/databases/sqlserver/nullability.md#limit-ansi-warnings-off).
- **SQLSRC260.**  The `-- column:` line names no column of the result, or the alias already ends in `!` or `?`.  Keep the mark or the line's `null` or `not null`, not both.  See [SQLSRC260](../diagnostics.md#sqlsrc260).
- **SQLSRC227 on PostgreSQL.**  `@name IS NULL` does not fix the type of `@name`.  Give the type in its `-- param:` line.  See [SQLSRC227](../diagnostics.md#sqlsrc227).
