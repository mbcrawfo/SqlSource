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
