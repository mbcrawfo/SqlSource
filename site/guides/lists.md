# Pass a list of values (IN)

<a id="list-after-in"></a>

## Pass a list after IN

Write the parameter directly after `IN`, with no parentheses, `WHERE id IN @ids`.  Its property in the input model is an `IEnumerable<int>`, which takes an array, a `List<int>` or any other sequence, and the query method sends a parameter for each value, on PostgreSQL and on SQL Server.  `(@ids IS NULL OR id IN @ids)` makes the filter optional, and a null list then keeps every row.  See [the recipe](#filter-by-a-list) and [list parameters](../reference/parameters.md#list-parameters).

<a id="filter-by-a-list"></a>

## Filter by an optional list of values

```sql postgres
-- name: ListOrdersByStatuses
SELECT id, status, placed_at
FROM orders
WHERE (@statuses IS NULL OR status IN @statuses)
ORDER BY placed_at DESC;
```

```csharp
string[] notCancelled = ["pending", "shipped"];
var shown = await orders.ListOrdersByStatuses(new ListOrdersByStatusesParams(notCancelled));
var everyOrder = await orders.ListOrdersByStatuses(new ListOrdersByStatusesParams(null));
```

`@statuses`, written after `IN` with no parentheses, is a list, and `ListOrdersByStatusesParams.Statuses` an `IEnumerable<string>?`, which takes an array, a `List<string>` or any other sequence.  The method sends a parameter for each status.  A null list returns every order, by the test `@statuses IS NULL`, and an empty one returns none.  The same query runs on SQL Server as it is.  On PostgreSQL, `status = ANY(@statuses)` sends the list as one array instead.

Rules: [list parameters](../reference/parameters.md#list-parameters), [array parameters](../reference/databases/postgresql/query-methods.md#array-parameters).

<a id="postgresql-arrays"></a>

## On PostgreSQL: one array with ANY

On PostgreSQL, you can also send the values as one array, with `= ANY(@ids)` in place of `IN @ids`.

```sql postgres
-- name: FindUsersByIds
SELECT id, name FROM users WHERE id = ANY(@ids);
```

```csharp
HashSet<int> ids = [3, 1, 2];
FindUsersByIdsDto[] found = await users.FindUsersByIds(new FindUsersByIdsParams(ids));
```

**What you get.**  `FindUsersByIdsParams.Ids` is an `IEnumerable<int>`, as the property of a list is.  The method sends the values as one parameter, a PostgreSQL array.

**The rules you need.**

- An array parameter is not a list.  `id = ANY(@ids)` sends one parameter, whatever the count of values, so no limit of parameters applies to it.  `id IN @ids` sends one parameter for each value.  See [Array parameters](../reference/databases/postgresql/query-methods.md#array-parameters).
- Use `IN @ids` if one query must run on PostgreSQL and on SQL Server, which has no arrays.  See [Array parameters](../reference/databases/postgresql/query-methods.md#array-parameters) and [Lists on SQL Server](../reference/databases/sqlserver/query-methods.md#list-parameters).
- Use `= ANY(@ids)` if Dapper runs the query.  Dapper sends a list to PostgreSQL as one array, which `IN @ids` cannot take.  See [List parameters](../reference/parameters.md#list-parameters).
- Do not write `IN (@ids)`, with parentheses.  It compares with one array.  See [Array parameters](../reference/databases/postgresql/query-methods.md#array-parameters).
- Under the output `models`, the property of an array is a `T[]`, and the property of a list an `IEnumerable<T>`.  See [Array parameters](../reference/databases/postgresql/query-methods.md#array-parameters).

<a id="large-and-empty-lists"></a>

## Large and empty lists

Read this if a list can be empty or null, or can hold thousands of values.

```sql postgres
-- name: ListUsersByIds
SELECT id, name
FROM users
WHERE id IN @ids
ORDER BY id;

-- name: ListUsersExcept
SELECT id, name
FROM users
WHERE id NOT IN @ids
ORDER BY id;
```

```csharp
int[] none = [];
ListUsersByIdsDto[] found = await users.ListUsersByIds(new ListUsersByIdsParams(none)); // no user
ListUsersExceptDto[] others = await users.ListUsersExcept(new ListUsersExceptParams(none)); // every user
```

**What you get.**  The empty list finds no user after `IN`, and every user after `NOT IN`.  Neither call throws.

**The rules you need.**

- An empty list matches no row after `IN`, and every row after `NOT IN`.  A null list does the same.  But `@ids IS NULL` is true for a null list, and false for an empty one.  See [List parameters](../reference/parameters.md#list-parameters).
- Keep nulls out of a list after `NOT IN`.  A null value there makes the condition match no row.  See [List parameters](../reference/parameters.md#list-parameters).
- The method pads each list to one of a few sizes with its last value, so it sends 3 values as 4.  The result does not change, and the database keeps one plan for each size.  To send each list at its own count, write `-- generator: list-padding=off`.  See [Padding](../reference/parameters.md#list-padding).
- Each value of a list counts toward the limit of parameters of the database, and so does every other parameter.  PostgreSQL takes 65,535, and SQL Server 2,098.  If a call would send more, the method throws `ArgumentException` before it sends anything.  Padding alone never makes it throw.  See [The limit of parameters](../reference/parameters.md#list-limit).

For more values than the limit, send them as one parameter.  On SQL Server, send a JSON array in an `nvarchar(max)` parameter, and read it with `OPENJSON`.  This needs a database whose compatibility level is 130 or later, and the `-- param:` line.  See [The limit of parameters](../reference/parameters.md#list-limit) and [Lists on SQL Server](../reference/databases/sqlserver/query-methods.md#list-parameters).

# [PostgreSQL](#tab/postgresql)

Write `id = ANY(@ids)`, as [the section above](#postgresql-arrays) shows.

# [SQL Server](#tab/sqlserver)

```sql mssql
-- name: ListOrdersByIdArray
-- param: @ids nvarchar(max)
SELECT id, placed_at FROM dbo.orders
WHERE id IN (SELECT CAST(value AS int) FROM OPENJSON(@ids));
```

```csharp
var found = await orders.ListOrdersByIdArray(new ListOrdersByIdArrayParams(JsonSerializer.Serialize(ids)));
```

---

## If it does not work

- **`ArgumentException`, "The query 'ListUsersByIds' would send ... parameters".**  The lists hold more values than the database takes.  Send the values as one parameter.  See [The limit of parameters](../reference/parameters.md#list-limit).
- **SQLSRC127 on SQL Server.**  A list after a `NOT` stands inside an aggregate, a `GROUP BY` or an `OUTPUT` clause, where SQL Server refuses an empty list.  Test the list in a `WITH` query, and use its result there.  See [SQLSRC127](../diagnostics.md#sqlsrc127).
- **`SqlException` 130 on SQL Server, for an empty or null list.**  A list after a `NOT` stands inside an aggregate of your own.  Test the list in a `WITH` query, and aggregate its result.  See [the limit](../reference/databases/sqlserver/query-methods.md#limit-list-in-an-aggregate).
- **An empty list matches the rows whose value is null, on SQL Server.**  The connection turned `ANSI_NULLS` off.  Leave it on.  See [the limit](../reference/databases/sqlserver/query-methods.md#limit-ansi-nulls-off).
