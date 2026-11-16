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

The [generator parameter](generator-parameters.md#setting-generator-parameters) `collection-method` gives a query a second method, its *collection method*.  A collection method is an overload of the query method of the query.  It takes a collection of the input models of the query, the *items*.  It runs the query once for each item, in a transaction of its own by default.  It returns what every item returned, in the order of the items.  In the sample, `AddUser` is a method of a repository.

A collection method sends the items 1,000 at a time, each 1,000 in one round trip to the database.  A loop that calls the query method takes a round trip for each item.  Each of these gives a collection method to every query that it reaches:

- `-- generator: collection-method` in a file header;
- `"generatorParameters": ["collection-method"]` in a settings file.

What a collection method returns depends on the [result shape](sql-files.md#result-shapes) of the query, as for a query method.  It holds the rows of every item.  A synchronous collection method returns what the task holds, with no `Task`.  For `none`, it returns nothing, `void`:

| Shape | Returns |
|----|----|
| `many` | `Task<C>`: every row of every item, in the order of the items, in the query's [collection type](return-types.md#setting-collection-type) `C`, an array by default.  For the collection type `IAsyncEnumerable`, an array, since a collection method holds its rows and never streams them |
| `one` | `Task<C>`: the row of each item, in the order of the items.  An item that returns no rows, or more than one, throws [`UnexpectedRowCountException`](exceptions.md#unexpected-row-count) |
| `one-optional` | `Task<C>`: the row of each item that returns one, in the order of the items, so it can hold fewer rows than there are items.  An item that returns more than one throws `UnexpectedRowCountException` |
| `none` | `Task` |
| `rowcount` | `Task<int>`: the counts of every item, added up.  An item whose count is not reported, as under SQL Server's `SET NOCOUNT ON`, adds 0 |

A collection method reads its rows as a `many` method reads them.  It reads them through the shared array pool of .NET, unless the query has [`array-pool=off`](generator-parameters.md#gp-array-pool).

- **A query gets a collection method when it gets a query method and has parameters.**  `collection-method` is off by default.
- **A query whose output is `repository` or `methods` gets its collection method beside its query method.**  Under the output `models` or `sql`, the generator parameter does nothing.
- **If a query gets no query method, it gets no collection method either.**  An example is a query whose method has an error.
- **A query without parameters gets no collection method, and no error.**  So if a folder turns `collection-method` on, its queries without parameters stay as they are.
- **If [`transaction`](#collection-transactions) names an isolation level that the database of the query does not have, the build reports [SQLSRC297](../diagnostics.md#sqlsrc297).**  The query then keeps its query method alone.
- **A collection method is an overload of the query method.**  It has the name, the visibility and the place of the query method.  Its name has `Async` under [`async-method-suffix`](generator-parameters.md#gp-async-method-suffix), as the name of the query method does.
- **It takes the arguments of the query method, in the same order, but for one.**  In place of `TParams parameters`, it takes `IEnumerable<TParams> parameters`, where `TParams` is the input model of the query.
- **At the output `methods`, a collection method takes the connection, `transaction`, `commandTimeout` and `cancellationToken`**, as the query method does.
- **A collection method of a repository takes `cancellationToken`.**  The [interface](repositories.md#repository-interface) of the repository gets the collection method beside the query method.
- **A synchronous collection method takes no `cancellationToken`.**
- **A [token](tokens.md#tokens) is one `string` argument, as in the query method.**  The collection method uses its text for every item.
- **Once a query has a collection method, a call of its query method that passes `new(...)`, `null` or `default` as the input model no longer compiles.**  Either method takes it, so the call is ambiguous: the compiler's error CS0121.  Name the type of the input model, as in `new AddUserParams("Ada", email)` or `default(AddUserParams)`.
- **A collection method follows [`method-type`](query-methods.md#synchronous-methods)**, as the query method does, at either output:
  - under `async`, the default, it is asynchronous;
  - under `sync`, it is synchronous;
  - under `both`, the query gets a synchronous collection method and an asynchronous one.  Each is an overload of the query method of the same name, as [synchronous methods](query-methods.md#synchronous-methods) name them.
- **The method reads the collection once, as it goes, and never holds it whole.**  So the collection can be a LINQ query or an iterator of your own.
- **If `parameters` is null, the method throws `ArgumentNullException` before anything runs.**
- **An empty collection opens no connection and begins no transaction.**  The method returns an empty collection, `0` or, for `none`, nothing.
- **The method checks each item as it reads it.**  If an item is null, the method throws `ArgumentNullException`, "Item 1499 of the collection is null.".  The message gives the position of the item, counted from 0.  A [list parameter](parameters.md#list-limit) of an item throws as the list parameter of the query method does.
- **The items run 1,000 at a time.**  The method reads and checks up to 1,000 items, and runs them.  Only then does it read the next 1,000.
- **The method finds a bad item only after every round trip before its own has run.**  In the method's own transaction, those items are rolled back.  Under `transaction=off`, or in the caller's transaction, they stay.  So in those two cases, to refuse the whole collection for one bad item, check the items before the call.
- **`commandTimeout` is the timeout of each 1,000 items, not of the whole method.**  Otherwise it is the `commandTimeout` of the query method.
- **`cancellationToken` cancels opening the connection, running the items and reading their rows.**  The method's own transaction is then rolled back.  What each driver throws is on the pages of [PostgreSQL](databases/postgresql/query-methods.md#collection-methods) and [SQL Server](databases/sqlserver/query-methods.md#collection-methods).
- **Nothing cancels a synchronous collection method.**  It has no `cancellationToken`.
- **A collection method gets its connection as its query method does.**  At the output `methods`, it opens a closed connection, and closes it again before it returns or throws.  If the connection was not closed, the method leaves it as it was.  See [the connection](connection-methods.md#method-connection).
- **A collection method of a repository gets a connection from its [connection source](repositories.md#repository-connection) for the call.**

<a id="collection-transactions"></a>

## Transactions

```sql postgres
-- name: ImportReadings -> none
-- generator: collection-method transaction=serializable
INSERT INTO readings (sensor_id, taken_at, value)
VALUES (@sensorId, @takenAt, @value);
```

The [generator parameter `transaction`](generator-parameters.md#gp-transaction) says which transaction a collection method begins of its own.  The sample begins one at the level `serializable`.  `transaction` changes nothing for a query without `collection-method`.

| `transaction` | The collection method |
|----|----|
| `on`, the default | Begins a transaction at the database's default isolation level, commits it after the last item, and rolls it back when anything throws |
| `read-uncommitted`, `read-committed`, `repeatable-read`, `serializable` or `snapshot` | The same, at that isolation level.  Each database has its own rules: see below |
| `off` | Begins none.  The items run as the database runs statements outside a transaction, so a failure leaves some of them in the database |

- **On PostgreSQL, `read-uncommitted` and `snapshot` are [SQLSRC297](../diagnostics.md#sqlsrc297).**  PostgreSQL has neither level.
- **On SQL Server, `snapshot` needs the database to allow it.**  See [SQL Server](databases/sqlserver/query-methods.md#collection-methods).
- **Under `off`, what a failed item leaves in the database depends on the database.**  On both, the round trips before the one of the failed item stay, and those after it do not run.  See [PostgreSQL](databases/postgresql/query-methods.md#collection-methods) and [SQL Server](databases/sqlserver/query-methods.md#collection-methods).
- **On PostgreSQL under `off`, the round trip of the failed item is rolled back.**
- **On SQL Server under `off`, each item is committed on its own.**  With `XACT_ABORT` off, an error such as a duplicate key fails its item alone: the other items of its round trip stay, those after it included.  Under `SET XACT_ABORT ON`, or at an error that ends the batch, the items before it stay, and the items after it do not run.
- **The isolation level of its own transaction is for that transaction alone.**  This is so under `on`, and under a level.  The later statements of the connection run at the level that they ran at before the call.
- **Its own transaction is all or nothing.**  It holds every item.  If anything throws, the method rolls the transaction back, and nothing of any item is left.  This holds for a database error at an item, an exception of the method's own such as `UnexpectedRowCountException`, a null item and a cancellation.
- **If the database has already rolled its own transaction back, the method throws the first exception all the same.**  SQL Server does so at some errors.
- **The caller's transaction is used as it is.**  At the output `methods`, a collection method that is given a `transaction` runs every item in it, whatever `transaction` says.  It neither commits that transaction nor rolls it back.
- **After an exception, what ran is still in the caller's transaction, unless a database error aborted the transaction or rolled it back.**  On PostgreSQL, every database error aborts it, as the next bullet says.  On SQL Server, some errors roll it back: see [SQL Server](databases/sqlserver/query-methods.md#collection-methods).  Otherwise, whether to roll the transaction back or commit it is yours to decide.
- **On PostgreSQL, a database error aborts the caller's transaction.**  PostgreSQL then takes no other command in it until you roll it back, and what ran in it is rolled back with it.  See [PostgreSQL](databases/postgresql/query-methods.md#collection-methods).
- **On SQL Server, a transaction that has completed is refused**, as for a query method.  See [the connection](connection-methods.md#method-connection).
- **An ambient transaction is used as it is too.**  If `System.Transactions.Transaction.Current` is set as the method starts, as inside a `TransactionScope`, the method begins no transaction, whatever `transaction` says.  It runs the items in the ambient transaction.  A connection opened inside the scope takes part in that transaction.
- **A connection that is not in the ambient transaction runs the items in no transaction at all**, as under `transaction=off`.  Examples are a connection opened before the scope and never enlisted, and one whose connection string turns enlisting off.
- **An ambient transaction is the way to put the collection method of a repository in a larger transaction.**  That method takes no `transaction`: see [transactions](repositories.md#repository-transactions).

<a id="collection-failures"></a>

## What it throws

A collection method throws the exceptions of its query method.  It also throws some of its own, such as one for a database error at an item.

- **A database error at an item is thrown as [`CollectionItemException`](exceptions.md#collection-item-exception).**  Its `ItemIndex` is the position of the item, counted from 0.  Its `InnerException` is the exception of the driver: a `PostgresException` or a `SqlException`.
- **An error that no item raised is thrown as the exception of the driver, as it is.**  An example is an error that the database raises after every item of a round trip has run.  A PostgreSQL constraint declared `DEFERRABLE INITIALLY DEFERRED` raises one, since PostgreSQL checks it at the commit.
- **Every other exception is the exception of the query method.**  These include:
  - `UnexpectedRowCountException`, whose message and `ItemIndex` name the item;
  - the exception for a null in a column whose property cannot be null;
  - the exception of a converter.
- **On SQL Server, a collection method that reads rows and is given a transaction can throw [`TransactionRolledBackException`](exceptions.md#transaction-rolled-back).**
- **On SQL Server, a query whose statements return more than one result set throws `InvalidOperationException`.**  See [SQL Server](databases/sqlserver/query-methods.md#collection-methods).

<a id="limits"></a>

## Limits

<a id="limit-wrapped-connection"></a>

### `NotSupportedException` from a collection method, or an error that names no item, on a connection that wraps the driver's

**What you see.**  On a `DbConnection` that wraps the connection of the driver, a collection method can fail where the query method works.  You see one of these:

- Every collection method throws `NotSupportedException`, "Specified method is not supported.", before any item runs.
- The method throws `InvalidCastException` before the items are sent, as the query method does.
- The items run, but a database error at an item is thrown as the exception of the driver, a `PostgresException` or a `SqlException`.  It is not thrown as [`CollectionItemException`](exceptions.md#collection-item-exception), so nothing names the item.

**Why.**  A collection method works fully only on the connection of the driver itself.  Each symptom comes from one kind of wrapper:

- A wrapper that cannot run a batch of commands, as most cannot, gives `NotSupportedException`.
- A wrapper whose batch commands create or accept only parameters of their own type gives `InvalidCastException`.
- A wrapper whose batches hold commands of its own gives the error that names no item.

**What to do.**  Give the method the connection of the driver that the wrapper holds, as for [the query method](query-methods.md#limit-wrapped-connection).
