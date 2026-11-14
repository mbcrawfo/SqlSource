# Collection methods (many items in one call)

<a id="collection-methods"></a>

## Collection methods

```sql postgres
-- name: AddUser -> one
-- generator: collection-method
INSERT INTO users (name, email)
VALUES (@name, @email)
RETURNING id;
```

```csharp
// Generated beside the query method AddUser(AddUserParams parameters, ...), shown without global::
public async Task<AddUserDto[]> AddUser(
    IEnumerable<AddUserParams> parameters,
    CancellationToken cancellationToken = default)

// Your code
var newUsers = new List<AddUserParams>
{
    new("Ada", "ada@example.com"),
    new("Grace", "grace@example.com"),
};
AddUserDto[] added = await users.AddUser(newUsers); // added[1].Id is Grace's id
```

The [generator parameter](generator-parameters.md#setting-generator-parameters) `collection-method` gives a query a second method, its collection method: an overload of its query method that takes a collection of the query's input models, runs the query once for each of them, the items, in a transaction of its own by default, and returns what every item returned, in the order of the items.  It sends the items 1,000 at a time, each 1,000 in one round trip to the database, where a loop that calls the query method takes a round trip for each.  Above, `AddUser` is a repository's.  `-- generator: collection-method` in a file header, or `"generatorParameters": ["collection-method"]` in a settings file, gives one to every query it reaches.

What it returns depends on the query's [result shape](sql-files.md#result-shapes), as a query method's does, with the rows of every item.  A synchronous collection method returns what the task holds, with no `Task`, and nothing, `void`, for `none`:

| Shape | Returns |
|----|----|
| `many` | `Task<C>`: every row of every item, in the order of the items, in the query's [collection type](return-types.md#setting-collection-type) `C`, an array by default.  For the collection type `IAsyncEnumerable`, an array, since a collection method holds its rows and never streams them |
| `one` | `Task<C>`: the row of each item, in the order of the items.  An item that returns no rows, or more than one, throws [`UnexpectedRowCountException`](exceptions.md#unexpected-row-count) |
| `one-optional` | `Task<C>`: the row of each item that returns one, in the order of the items, so it can hold fewer rows than there are items.  An item that returns more than one throws `UnexpectedRowCountException` |
| `none` | `Task` |
| `rowcount` | `Task<int>`: the counts of every item, added up.  An item whose count is not reported, as under SQL Server's `SET NOCOUNT ON`, adds 0 |

Its rows are read as a `many` method reads them, through .NET's shared array pool unless the query has `array-pool=off`.

- **A query gets one when it gets a query method and has parameters.**  `collection-method` is off by default.  A query whose output is `repository` or `methods` gets its collection method beside its query method, and one that gets no query method, as when the method has an error, gets neither.  A query without parameters gets none, and no error, so a folder that turns `collection-method` on keeps its queries without parameters as they are.  Under the output `sql` or `models` the parameter does nothing.  A [`transaction`](#collection-transactions) that names an isolation level the query's database does not have is [SQLSRC297](../diagnostics.md#sqlsrc297), which leaves the query its query method alone.
- **It is an overload of the query method**, with its name, `Async` included under `async-method-suffix`, its visibility and its place, and the same arguments in the same order, but `IEnumerable<TParams> parameters`, of the query's input model, in place of `TParams parameters`.  A `methods` method still takes the connection, `transaction`, `commandTimeout` and `cancellationToken`, and a repository's takes `cancellationToken`; the repository's interface gets it beside the query method.  A synchronous collection method takes no `cancellationToken`.  A [token](tokens.md#tokens) is one `string` argument, as in the query method, and its text is used for every item.  Once a query has one, a call of its query method that passes `new(...)`, `null` or `default` as the input model no longer compiles: either method takes it, so the call is ambiguous, the compiler's error CS0121.  Name the input model's type, as in `new AddUserParams("Ada", email)` or `default(AddUserParams)`.
- **It follows [`method-type`](query-methods.md#synchronous-methods)**, as the query method does, at either output: under `sync` it is synchronous, under `both` the query gets a synchronous collection method and an asynchronous one, each an overload of the query method of the same name, as [synchronous methods](query-methods.md#synchronous-methods) name them, and under `async`, the default, it is asynchronous.
- **The collection is read once**, as the method goes, and never held whole, so it can be a LINQ query or an iterator of your own.  A null `parameters` throws `ArgumentNullException` before anything runs.  An empty collection returns an empty collection, `0` or, for `none`, nothing, and opens no connection and begins no transaction.
- **Each item is checked as the method reads it.**  A null item throws `ArgumentNullException`, "Item 1499 of the collection is null.", with the item's position, counted from 0, and an item's [list parameter](parameters.md#list-limit) throws as the query method's does.
- **The items run 1,000 at a time.**  The method reads and checks up to 1,000 items, runs them, and only then reads the next 1,000.  So a bad item is found only after every round trip before its own has run: in the method's own transaction those items are rolled back, and under `transaction=off` or in the caller's transaction they stay.  To refuse the whole collection for one bad item there, check the items before the call.
- **`commandTimeout` is the timeout of each 1,000 items**, not of the whole method, and is otherwise the query method's.  `cancellationToken` cancels opening the connection, running the items and reading their rows, and the method's own transaction is rolled back; nothing cancels a synchronous collection method, which has no `cancellationToken`.  What each driver throws is on the pages of [PostgreSQL](databases/postgresql/query-methods.md#collection-methods) and [SQL Server](databases/sqlserver/query-methods.md#collection-methods).
- **The connection** is the query method's: a `methods` method opens a closed connection and closes it again before it returns or throws, as [the connection](connection-methods.md#method-connection) says, and a repository's gets one from its [connection source](repositories.md#repository-connection) for the call.

<a id="collection-transactions"></a>

## Transactions

```sql postgres
-- name: ImportReadings -> none
-- generator: collection-method transaction=serializable
INSERT INTO readings (sensor_id, taken_at, value)
VALUES (@sensorId, @takenAt, @value);
```

The generator parameter `transaction` says which transaction a collection method begins of its own.  It changes nothing for a query without `collection-method`.

| `transaction` | The collection method |
|----|----|
| `on`, the default | Begins a transaction at the database's default isolation level, commits it after the last item, and rolls it back when anything throws |
| `read-uncommitted`, `read-committed`, `repeatable-read`, `serializable` or `snapshot` | The same, at that isolation level.  PostgreSQL has no `read-uncommitted` and no `snapshot`, which are [SQLSRC297](../diagnostics.md#sqlsrc297) there, and SQL Server's `snapshot` needs the database to allow it: see [SQL Server](databases/sqlserver/query-methods.md#collection-methods) |
| `off` | Begins none: the items run as the database runs statements outside a transaction, so a failure leaves some of them in it, as the pages of [PostgreSQL](databases/postgresql/query-methods.md#collection-methods) and [SQL Server](databases/sqlserver/query-methods.md#collection-methods) say |

- **Its own transaction's isolation level is that transaction's alone**, under `on` as under a level: the connection's later statements run at the level they ran at before the call.
- **Its own transaction is all or nothing.**  It holds every item, and when anything throws, an item's database error, an exception of the method's own such as `UnexpectedRowCountException`, a null item or a cancellation, it is rolled back and nothing of any item is left.  When the database has already rolled it back, as SQL Server does at some errors, the method throws the first exception all the same.
- **The caller's transaction is used as it is.**  A `methods` method given a `transaction` runs every item in it, whatever `transaction` says, and neither commits it nor rolls it back: after an exception, what ran is still in it, and whether to roll it back or commit it is yours to decide.  On SQL Server, a transaction that has completed is refused, as for a query method: see [the connection](connection-methods.md#method-connection).
- **So is an ambient transaction.**  When `System.Transactions.Transaction.Current` is set as the method starts, as inside a `TransactionScope`, the method begins none, whatever `transaction` says, and runs in that one, which a connection opened inside the scope takes part in.  A connection that is not in it, such as one opened before the scope and never enlisted, or one whose connection string turns enlisting off, runs the items in no transaction at all, as under `transaction=off`.  That is the way to put a repository's collection method in a larger transaction, since it takes no `transaction`.

<a id="collection-failures"></a>

## What it throws

- **A database error at an item** is thrown as [`CollectionItemException`](exceptions.md#collection-item-exception), whose `ItemIndex` is the item's position, counted from 0, and whose `InnerException` is the driver's exception, a `PostgresException` or a `SqlException`.
- **An error that no item raised is the driver's own.**  One that the database raises after every item of a round trip has run, such as that of a PostgreSQL constraint declared `DEFERRABLE INITIALLY DEFERRED`, which is checked at the commit, is thrown as the driver's exception, as it is.
- **Every other exception is the query method's**: `UnexpectedRowCountException`, whose message and `ItemIndex` name the item, a null in a column whose property cannot be null, a converter's exception.
- **On SQL Server**, a method that reads rows and is given a transaction can throw [`TransactionRolledBackException`](exceptions.md#transaction-rolled-back), and a query whose statements return more than one result set throws `InvalidOperationException`: see [SQL Server](databases/sqlserver/query-methods.md#collection-methods).

<a id="limits"></a>

## Limits

<a id="limit-wrapped-connection"></a>

### `NotSupportedException` from a collection method, or an error that names no item, on a connection that wraps the driver's

- **A collection method works fully only on the driver's own connection.**  On a `DbConnection` that wraps the driver's, it can fail where the query method works, in three ways.  A wrapper that cannot run a batch of commands, as most cannot, makes every collection method throw `NotSupportedException`, "Specified method is not supported.", before any item runs.  A wrapper whose batch commands create or accept only parameters of their own type fails as the query method does, with `InvalidCastException`, before the items are sent.  And a wrapper whose batches hold commands of its own runs the items, but a database error at an item is thrown as the driver's own exception, a `PostgresException` or a `SqlException`, not as [`CollectionItemException`](exceptions.md#collection-item-exception), so nothing names the item.  Give the method the driver's connection that the wrapper holds, as for [the query method](query-methods.md#limit-wrapped-connection).
