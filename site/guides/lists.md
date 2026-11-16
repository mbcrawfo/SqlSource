# Pass a list of values (IN)

Pass an array or a list to an `IN` clause, make it an optional filter, or send it as one array with `ANY` on PostgreSQL.

**Use this when** a query must find the rows whose column matches one of several values that your code holds.  Use it too when the list can be null or empty, or can hold thousands of values.

<a id="list-after-in"></a>

## Pass a list after IN

The [PostgreSQL example project](https://github.com/mbcrawfo/SqlSource/blob/main/examples/PostgresExamples/README.md) writes this query in `Methods/OrderQueries.sql`:

```sql postgres
-- name: ListOrdersByStatuses -> many
-- summary: The orders in any of the given statuses, or every order when the list is null, newest first.
-- member-location: class
-- visibility: public
SELECT id, status, placed_at
FROM orders
WHERE (@statuses IS NULL OR status IN @statuses)
ORDER BY placed_at DESC;
```

Its class, in `Methods/OrderQueries.cs`, passes an array:

```csharp
    // A method of your own that passes a list: ListOrdersByStatusesParams.Statuses is an IEnumerable<string>?, which
    // takes this array, a List<string> or any other sequence, and the method sends one parameter for each value.  See
    // site/reference/parameters.md#list-parameters.
    public static Task<ListOrdersByStatusesDto[]> ListOrdersNotCancelledAsync(
        DbConnection connection,
        CancellationToken cancellationToken = default
    )
    {
        string[] notCancelled = ["pending", "shipped"];
        return ListOrdersByStatusesAsync(
            connection,
            new ListOrdersByStatusesParams(notCancelled),
            cancellationToken: cancellationToken
        );
    }
```

**What you get.**  `ListOrdersByStatusesParams.Statuses` is an `IEnumerable<string>?`, which takes an array, a `List<string>` or any other sequence.  The query method sends a parameter for each value, on PostgreSQL and on SQL Server.  The test `@statuses IS NULL` makes the filter optional, as [Filter by an optional list of values](#filter-by-a-list) shows.  In the example, the folder sets the [output](../reference/settings.md#setting-output) `methods`, and the file header turns on [`async-method-suffix`](naming-and-placement.md#async-names).  So the method is `OrderQueries.ListOrdersByStatusesAsync`, and takes the connection first.

**The rules you need.**

- **Write the parameter directly after `IN`, with no parentheses: `WHERE id IN @ids`.**  See [list parameters](../reference/parameters.md#list-parameters).
- **The property of a list in the input model is an `IEnumerable<T>`**, such as an `IEnumerable<int>` for `int` values.  See [list parameters](../reference/parameters.md#list-parameters).

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

**What you get.**  `shown` holds the orders whose status is `pending` or `shipped`.  `everyOrder` holds every order, since the test `@statuses IS NULL` is true for a null list.  An empty list returns no order.

**The rules you need.**

- **`(@ids IS NULL OR id IN @ids)` makes the filter optional, and a null list then keeps every row.**  See [Large and empty lists](#large-and-empty-lists) and [list parameters](../reference/parameters.md#list-parameters).
- **The same query runs on SQL Server as it is.**  See [Lists on SQL Server](../reference/databases/sqlserver/query-methods.md#list-parameters).
- **To send the list as one array on PostgreSQL**, see [On PostgreSQL: one array with ANY](#postgresql-arrays) and [array parameters](../reference/databases/postgresql/query-methods.md#array-parameters).

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
- If one query must run on PostgreSQL and on SQL Server, which has no arrays, use `IN @ids`.  See [Array parameters](../reference/databases/postgresql/query-methods.md#array-parameters) and [Lists on SQL Server](../reference/databases/sqlserver/query-methods.md#list-parameters).
- If Dapper runs the query, use `= ANY(@ids)`.  Dapper sends a list to PostgreSQL as one array, which `IN @ids` cannot take.  See [List parameters](../reference/parameters.md#list-parameters).
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

Write `id = ANY(@ids)`, as [On PostgreSQL: one array with ANY](#postgresql-arrays) shows.

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
