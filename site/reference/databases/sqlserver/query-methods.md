# SQL Server: running queries

<a id="query-methods"></a>

## Query methods

```sql mssql
-- name: AddOrder -> one
INSERT INTO dbo.orders (customer_id, placed_at, total, status)
OUTPUT INSERTED.id
VALUES (@customerId, @placedAt, @total, @status);
```

```csharp
[SqlSourceGenerate]
public partial class OrderRepository(string connectionString);

// elsewhere
AddOrderDto order = await orders.AddOrder(
    new AddOrderParams(customerId, placedAt, 1234.5m, "placed"));
```

A described query whose output is `repository` or `methods` gets a method that runs it through Microsoft.Data.SqlClient: see [query methods](../../query-methods.md#query-methods).  The project must reference Microsoft.Data.SqlClient 6.1 or later ([SQLSRC273](../../../diagnostics.md#sqlsrc273), [SQLSRC267](../../../diagnostics.md#sqlsrc267)).

- **A repository's [connection source](../../repository-connections.md#repository-connection) is a connection string, a method that returns a connection, or a connection that the repository borrows.**  Microsoft.Data.SqlClient has no data source.  From a connection string, as above, each call makes a `SqlConnection`, opens it and disposes it.
- **A `methods` method runs on the `DbConnection` that you give it**, such as a `SqlConnection`.
- **A value that fits its type never changes how it is sent.**  Above, `placed_at` is a `datetime2(7)`, `total` a `decimal(12,2)` and `status` an `nvarchar(20)`.  So the method sends `@placedAt` as a `datetime2`, with all seven digits of its time, `@total` as a `decimal(12,2)` and `@status` as an `nvarchar(20)`.  What happens to a value that does not fit is in [how a parameter is sent](#method-parameters).

<a id="method-parameters"></a>

## How a parameter is sent

A query method sends each parameter as the SQL Server type it was described with, with that type's length, precision and scale.  It never chooses a type from the value.  So:

- a value keeps its digits;
- a null is a `NULL` of the parameter's type, which a column of that type takes;
- the server sees one declaration of the query, whatever the lengths of the values that fit their types, and keeps one plan of it.

How a method names a parameter and sends a null is on [the Query methods page](../../query-methods.md#method-parameters).  For some types:

- **A string or a `byte[]` longer than its type is sent whole**, as a `varchar(max)`, an `nvarchar(max)` or a `varbinary(max)`.  The method never cuts it.  The server refuses an `INSERT` or an `UPDATE` that would cut it, with error 2628, "String or binary data would be truncated".  The error is 8152 on SQL Server 2017, and in a database whose compatibility level is below 150 or whose `VERBOSE_TRUNCATION_WARNINGS` is off.  A `WHERE` that compares a column with such a value finds no row that holds its start: `abcdef` finds no `char(5)` that holds `abcde`.

  Spaces at the end of a string, and zero bytes at the end of a `byte[]`, are the exception.  If a value is longer than its column only by them, the server cuts them off with no error and stores the rest.  To compare two strings, the server pads the shorter one with spaces, so `abcde ` finds the `char(5)` that holds `abcde`.
- **`datetime2`, `time` and `datetimeoffset` are sent with seven digits of a second**, whatever the scale of their type.  The server rounds a value to the scale of the column that stores it: `12:00:00.1235678` written to a `datetime2(3)` column is `12:00:00.124`.  A `WHERE` compares a column with all seven digits of the value.  So `12:00:00.124` finds a `datetime2(3)` that holds `12:00:00.124`, and `12:00:00.1235678` does not.
- **A `decimal` or `numeric` with more digits after the point than its type is rounded before it is sent.**  It is rounded to the scale of the type, half away from zero, as the server rounds it when it stores it.  `1.23456` for a `decimal(12,4)` is sent as `1.2346`, which an `INSERT` stores and a `WHERE` finds.
- **A `decimal` or `numeric` with more digits before the point than its type holds throws `ArgumentException`**, "Parameter value '123456789.1000' is out of range.", and nothing is sent.
- **`json` is sent as an `nvarchar(max)`**, which the server checks and stores as `json`.  The server refuses a value that is not an object or an array: see [JSON](libraries.md#json).
- **`vector` is sent as its `SqlVector<float>`**, whose length is its dimension.  A null is sent as the null `vector` of the parameter's dimension.
- **An alias type is sent as its base type**: a parameter of `dbo.Email`, made `FROM nvarchar(320)`, as an `nvarchar(320)`.
- **An `xml` of an XML schema collection is sent as `xml`, which has no collection.**  Such a parameter is declared `-- param: @p xml(DOCUMENT dbo.S)`.  If the query needs the typing of the collection, as `@p.value('/a', 'nvarchar(10)')` of a document does, cast the parameter in the query: `CAST(@p AS xml(DOCUMENT dbo.S))`.  Without the cast, SQL Server refuses the query when the tool describes it.
- **A parameter that has a [converter](../../converters.md#converters) is sent as its database type too.**  An `Email` whose converter returns a `string` is sent as that `nvarchar(320)`.  A converted string longer than that is sent whole, as above.
- **A parameter of one of System.Text.Json's types, or of a class of yours in a [JSON](libraries.md#json) column, is sent as its database type.**  The method converts it to the text of its JSON, as it converts a parameter that has a converter.  It sends the text as an `nvarchar(max)` for a `json`, and as the column's own type, with its length, for a string column.
- **A parameter of a [NodaTime](libraries.md#nodatime) type is sent as its database type.**  The method converts it to the C# type of [the table](types.md#type-table), as it converts a parameter that has a converter.  A `NodaTime.LocalDateTime` for a `datetime` is sent as a `datetime`, and a `NodaTime.LocalTime` as a `time`.  What the conversion does to a value that its column cannot hold is under [NodaTime](libraries.md#nodatime).
- **A parameter of a [NetTopologySuite](libraries.md#nettopologysuite) type is sent as its `geography` or `geometry`**, with the SRID of the value.  A null is sent as a `NULL` of that type.
- **`hierarchyid`, `geography`, `geometry` and a CLR type of your own assembly are sent as their SQL Server type**, when a [type override](../../type-mapping.md#setting-type-overrides) gives them a C# type, such as `Microsoft.SqlServer.Types.SqlGeography`.  A null is sent as a `NULL` of that type.  A CLR type is named by its schema: `Money` of the schema `dbo` is sent as `dbo.Money`.
- **A type of a newer SQL Server is sent by its value**, and Microsoft.Data.SqlClient chooses its type from the value.  Only a type override gives such a type a C# type.
- **A [list parameter](#list-parameters) sends each of its values as a parameter of the values' type**, as above.

<a id="list-parameters"></a>

## Lists

```sql mssql
-- name: ListOrdersByIds
SELECT id, placed_at FROM dbo.orders WHERE id IN @ids;
```

SQL Server has no arrays, so a query method sends a list of values as a [list parameter](../../list-parameters.md#list-parameters).  Above, `ListOrdersByIdsParams.Ids` is an `IEnumerable<int>`.  Each of its values is sent as an `int`, the type of `id`, as [any parameter](#method-parameters) of that type is.  The test of a list for null, `@ids IS NULL`, is sent as a `bit` that has the name of the list.

- **A list after a `NOT` cannot stand inside an aggregate, a `GROUP BY` or an `OUTPUT` clause**, under the output `repository` or `methods`.  SQL Server refuses there the form in which such a list is sent when it is empty or null.  So a query such as `SELECT COUNT(IIF(category NOT IN @categories, 1, NULL)) AS others FROM dbo.products` is [SQLSRC127](../../../diagnostics.md#sqlsrc127) at build.  Test the list in a `WITH` query, and aggregate its result, as the error's page shows.
- **An aggregate that `OVER` follows is not one of these places.**
- **A list that no `NOT` reaches runs empty in all of these places.**
- **A `NOT` reaches only the condition that it stands in.**  So a `NOT` in a `WHERE` leaves a list in the `HAVING` after it alone.
- **Inside an aggregate of your own, a list after a `NOT` fails when the query runs**, if the list is empty or null: see [the limit](#limit-list-in-an-aggregate).
- **An empty list assumes that the connection leaves `ANSI_NULLS` on**, as Microsoft.Data.SqlClient does: see [the limit](#limit-ansi-nulls-off).
- **A query sends at most 2,098 parameters.**  SQL Server takes 2,100 parameters in one request, and Microsoft.Data.SqlClient uses two of them to send a query with parameters.  What counts, and what a method does past it, is in [the limit of parameters](../../list-parameters.md#list-limit).
- **For more values than that, send them as one JSON array** in an `nvarchar(max)` parameter, and read it with `OPENJSON`.  This needs a database whose compatibility level is 130 or later:

  ```sql mssql
  -- name: ListOrdersByIdArray
  -- param: @ids nvarchar(max)
  SELECT id, placed_at FROM dbo.orders
  WHERE id IN (SELECT CAST(value AS int) FROM OPENJSON(@ids));
  ```

  ```csharp
  var found = await orders.ListOrdersByIdArray(new ListOrdersByIdArrayParams(JsonSerializer.Serialize(ids)));
  ```

  The query sends one parameter whatever the count, and has one form, so SQL Server keeps one plan of it.  Write the marker: SQL Server cannot tell the type of `@ids` from `OPENJSON(@ids)` alone.
- **A table-valued parameter does the same through a table type of your database**, but a query method cannot send one.  Give such a query `-- output: sql`, and send its parameter with your own code.

<a id="method-results"></a>

## What a method reads and throws

- **Give each `methods` method the transaction of a connection that has one.**  Without it, Microsoft.Data.SqlClient refuses the command with `InvalidOperationException` before anything is sent, as [the connection](../../connection-methods.md#method-connection) says.  The message names the call that was refused: `ExecuteReader`, `ExecuteScalar` or `ExecuteNonQuery` from a [synchronous method](../../synchronous-methods.md#synchronous-methods), and `BeginExecuteReader` or `BeginExecuteNonQuery` from an asynchronous one.  So under `method-type=both`, the two methods of a query throw messages that differ in that word.
- **A connection that a repository borrows must have no transaction that your code began on it with `BeginTransaction`.**  Otherwise Microsoft.Data.SqlClient refuses the command with `InvalidOperationException`, as a repository method takes no transaction: see [transactions and timeouts](../../repositories.md#repository-transactions).
- **A `methods` method refuses a transaction that has completed.**  If the transaction was committed or rolled back before the call, every method throws `InvalidOperationException`, "The transaction given to the query 'ListOrders' has completed; it is no longer usable.", with the query's name.  Nothing runs: the connection is not opened, and no command is sent.  Without this, Microsoft.Data.SqlClient would run the command outside any transaction and commit it on its own.
- **A loop over [streamed rows](../../return-types.md#streaming-rows) checks the transaction when the loop starts.**  So it throws when the loop asks for its first row, and it also refuses a transaction that completes between the call and the loop.
- **A completed transaction of a library that wraps Microsoft.Data.SqlClient's can be missed.**  A method knows that a transaction has completed by its `Connection`, which Microsoft.Data.SqlClient's `SqlTransaction` sets to null.  If a wrapper keeps its `Connection` set after it completes, the method does not refuse it, and the command runs outside any transaction.  Give the method the `SqlTransaction` inside the wrapper.

<a id="reading-a-row"></a>

### Reading a row

- **A column is read as the C# type of its property**: see [how a row is read](../../query-methods.md#reading-rows).  If a [converter](../../converters.md#converters) converts it, the method first reads the column as the type of [the table](types.md#type-table), and an alias type as its base type.
- **NodaTime's, NetTopologySuite's and System.Text.Json's types, and a class of yours in a JSON column, need nothing registered**, on the connection or in the application.  The method reads them itself, as [NodaTime](libraries.md#nodatime), [NetTopologySuite](libraries.md#nettopologysuite) and [JSON](libraries.md#json) say.
- **A C# enum is read only from a column of its own underlying type.**  An enum over `int`, the default, is read from an `int`, and an enum over `byte` from a `tinyint`.  An enum over `int` throws `InvalidCastException` for a `tinyint`, a `smallint` or a `bigint`, since Microsoft.Data.SqlClient never widens or narrows a value.  Declare the enum over the type of its column: `enum Level : byte` for a `tinyint`.

<a id="null-values"></a>

### Null values

- **A null in a column whose property cannot be null throws before any converter is called.**  The method throws Microsoft.Data.SqlClient's `SqlNullValueException`, and for a `vector` column its own `InvalidCastException`, "Column 'name' is null.".

<a id="errors-and-rollbacks"></a>

### Errors and rollbacks

- **A query of several statements returns the rows of its first result set**, as its model has them.  Its other statements still run.
- **Every method throws an error that a statement after the first result set raises**, such as a `THROW`, as the error's `SqlException`.  The statements before the error have run.  In a transaction, if a later error of the batch then rolls the transaction back, the method throws `TransactionRolledBackException` instead, as the bullets below say.
- **A method that reads rows throws the error in place of its rows.**  A method of the shape `one`, `one-optional` or `many` reads the rest of the batch before it returns.
- **A loop over [streamed rows](../../return-types.md#streaming-rows) throws the error after the last row.**
- **In a transaction, the error may have rolled the transaction back.**  This depends on the error and on `XACT_ABORT`:
  - Under `SET XACT_ABORT ON`, nearly every error rolls it back.
  - With `XACT_ABORT` off, `CAST('x' AS int)` rolls it back.  A `THROW`, a divide by zero or `CAST(300 AS tinyint)` leaves it open.
- **`rowcount` adds up the rows that the statements insert, update or delete.**  A `SELECT` among them adds nothing.
- **A loop over streamed rows that ends early throws the error of a row or a statement that it left unread.**  It throws it at the `break`, or in place of the exception of the loop's body.  In a transaction, that error may have rolled the transaction back, as for an error after the rows.  See [streamed rows](../../return-types.md#streaming-rows), which also says how to keep the exception of the body.
- **The first exception wins over a later error of the batch.**  A method throws the first exception that it meets, and not an error that the rest of the batch raises after it.  The first exception is one of these:
  - an exception of the method's own, thrown before it has read the rest of its batch.  It is `UnexpectedRowCountException`, a null in a column whose property cannot be null, the `InvalidCastException` of a `vector` column, or a converter's exception;
  - an error that SQL Server raises for a row, such as a divide by zero, thrown as its `SqlException`;
  - an error that SQL Server raises for a statement after the rows, such as `CAST(300 AS tinyint)`, thrown as its `SqlException`.
- **In a transaction that a later error rolled back, the method throws [`TransactionRolledBackException`](../../exceptions.md#transaction-rolled-back) in place of the first exception.**  The rest of the batch still runs after the first exception.  A later error there can roll the transaction back, such as a `CAST('x' AS int)` with `XACT_ABORT` off.  The `InnerException` is the first exception, and not the later error.
- **A `catch` of `TransactionRolledBackException` tells you that your transaction has completed.**  Its `Connection` is null, and `Commit` throws `InvalidOperationException`, "This SqlTransaction has completed; it is no longer usable.".
- **An error that itself rolls the transaction back is thrown as its `SqlException`**, as the bullet on an error after the first result set says.  Examples are a `THROW` or a duplicate key under `SET XACT_ABORT ON`, and a `CAST('x' AS int)` for a row or after the rows.
- **The first exception is thrown as it is in these cases:**
  - without a transaction;
  - with the transaction still open;
  - for a transaction of a library that wraps Microsoft.Data.SqlClient's and keeps its `Connection` set, as the bullet on a completed transaction says.
- **The same rules hold for a loop over [streamed rows](../../return-types.md#streaming-rows).**  They hold at a row and after its last row.  They also hold for the error of a row or a statement that a `break`, or an exception of the body, leaves unread.
- **An error that SQL Server raises before the rows begin is thrown as its `SqlException`**, even when the rest of the batch then rolls the transaction back: see [the limit](#limit-hidden-rollback).

<a id="rowcount"></a>

### `rowcount`

- **`rowcount` is `-1` under `SET NOCOUNT ON`.**  A query that sets `NOCOUNT` returns `-1`.  If the query has no parameters, the setting also lasts for the session of the connection, so a later `rowcount` method on the same open connection returns `-1` too.  A query with parameters sets it for itself alone.  See [the limit](#limit-rowcount-after-nocount).

<a id="timeout-and-cancel"></a>

### Timeouts, cancelling and the connection

- **A `commandTimeout` that passes throws `SqlException` with `Number` `-2`**, whose message starts "Execution Timeout Expired.".  This is the `commandTimeout` of a `methods` method.
- **A cancelled method throws what Microsoft.Data.SqlClient throws**, unchanged.  That is one of two exceptions:
  - `TaskCanceledException`, if the token is cancelled before the call, or once the server has begun to send the result;
  - `SqlException`, with `Number` `0` and a message that ends "Operation cancelled by user.", if the token is cancelled while the command runs, before that.
- **A `SELECT` usually begins its result before its first row.**  So a cancel while it waits for that row is a `TaskCanceledException`, and a cancel while an `UPDATE` waits on a lock is a `SqlException`.
- **A `catch (OperationCanceledException)` does not catch the `SqlException`.**  Check the token in a `catch (SqlException)` as well.
- **A method with parameters fails on a connection that wraps a `SqlConnection` and creates parameters of its own**: see [the limit](../../query-methods.md#limit-wrapped-connection).

<a id="collection-methods"></a>

## Collection methods

```sql mssql
-- name: AddOrder -> one
-- generator: collection-method
INSERT INTO dbo.orders (customer_id, placed_at, total, status)
OUTPUT INSERTED.id
VALUES (@customerId, @placedAt, @total, @status);
```

A query's [collection method](../../collection-methods.md#collection-methods) runs its items through Microsoft.Data.SqlClient.  What a failed item leaves in the database depends on the transaction that the method runs in, and on `XACT_ABORT`:

| The transaction | When an item fails |
|----|----|
| Its own, by `transaction=on`, the default, or an isolation level | Every item is rolled back, those of earlier round trips too, and nothing is left |
| None, by `transaction=off` | SQL Server commits each item on its own.  With `XACT_ABORT` off, an error such as a duplicate key ends its item's statement alone: the other items of its round trip, those after it included, and the round trips before it stay, and the round trips after it do not run.  Under `SET XACT_ABORT ON` the error also stops its round trip, so the items after it do not run, and so does an error that ends the batch, such as a failed conversion, `CAST('x' AS int)`, with `XACT_ABORT` off |
| The caller's, a `transaction` given at the output `methods` or an ambient one | The method leaves it to you.  With `XACT_ABORT` off, a duplicate key ends its item's statement alone, and the transaction stays open, with every other item of its round trip and of those before it, for you to commit or roll back.  An error that rolls the transaction back itself, nearly any under `SET XACT_ABORT ON`, or one that ends the batch, such as a failed conversion, `CAST('x' AS int)`, leaves nothing, and the transaction has completed: its `Connection` is null |

- **A query whose statements return more than one result set cannot run over a collection** at the shapes `one`, `one-optional` and `many`, which read one result set for each item.  The shapes `none` and `rowcount` read no rows, so their result sets do not matter.
- **A statement that returns no result set is fine**, such as `SET NOCOUNT ON`, a `DECLARE` or an `UPDATE` without `OUTPUT`.
- **A second result set, or none for an item, throws `InvalidOperationException`**, and the method's own transaction is rolled back.  This happens to a query of two result sets, and to one whose `SELECT` stands in an `IF` and returns no result set for some items.  The message is "The query 'FindOrder' returned a result set of another shape, or none, for one of the items from 0 to 1, so it cannot run over a collection: a query whose statements return more than one result set, or none for an item, cannot.".  The items it names are those of the round trip up to where the mismatch showed, since the result set too many or missing can be any of theirs.
- **A second result set with as many columns as the query's rows can fail in another way.**  The method throws the same exception at the end of the round trip.  Or, before that, an item reads the result set as its own and throws `UnexpectedRowCountException` or Microsoft.Data.SqlClient's `InvalidCastException`.  Either way, the method never returns its rows.
- **Given a transaction, a method that reads rows can throw [`TransactionRolledBackException`](../../exceptions.md#transaction-rolled-back)**, as a query method does [above](#method-results).  It throws it when both of these are true:
  - it threw an exception of its own, or reached an item's error that left the transaction open;
  - a later error of the round trip then rolled the transaction back.

  Its `InnerException` is the method's own exception, such as `UnexpectedRowCountException`, or the [`CollectionItemException`](../../exceptions.md#collection-item-exception) of the item's error.
- **An error that rolled the transaction back itself is thrown as its `CollectionItemException`.**
- **A transaction that has completed is refused** before anything runs, as for a query method: see [transactions](../../collection-methods.md#collection-transactions).
- **`rowcount` adds 0 for an item under `SET NOCOUNT ON`**, where a query method returns `-1`.
- **The isolation level of the method's own transaction lasts for that transaction alone**, as [transactions](../../collection-methods.md#collection-transactions) says.  It is the level that `transaction` names, or read committed under `on`.  SQL Server keeps a level on the connection after a transaction ends, and the pool keeps it on a connection that is closed and opened again.  So the method sets the connection back to its level before the call, whether the method returns or throws, and before it closes a connection that it opened.  The pooled connection opened next runs at the level it would have run at without the call.
- **`transaction=snapshot` needs the database to allow it**: `ALTER DATABASE app SET ALLOW_SNAPSHOT_ISOLATION ON`.  Without it, SQL Server refuses the first item of the transaction with its error 3952.
- **SQL Server has all five isolation levels**: `read-uncommitted`, `read-committed`, which is its default, `repeatable-read`, `serializable` and `snapshot`.
- **`commandTimeout` is that of each round trip, and a cancelled method throws as a query method does**: see [timeouts and cancelling](#timeout-and-cancel).  It often throws `TaskCanceledException` when an item waits for the rows it returns, as on a lock.  Either way, the method's own transaction is rolled back.

<a id="limits"></a>

## Limits

<a id="limit-ansi-nulls-off"></a>

### An empty list after `IN` matches the rows whose value is null

**What you see.**  An empty or a null list after `IN` matches the rows whose value is null, where it should match none.  A null value in a list matches them too.

**Why.**  The connection turns `ANSI_NULLS` off.  With `ANSI_NULLS` off, SQL Server takes `= NULL` as true for a null.

**What to do.**  Leave `ANSI_NULLS` on, as Microsoft.Data.SqlClient turns it on when it connects.  Run no `SET ANSI_NULLS OFF` on a connection that runs your queries.

<a id="limit-hidden-rollback"></a>

### A `SqlException`, and the transaction is rolled back

**What you see.**  A method throws the `SqlException` of an error raised before the query's first rows, and the transaction that you gave it has been rolled back.  If your code catches the `SqlException` and goes on, the transaction's `Connection` is null:

- A generated method that you give the transaction [refuses it](#method-results) with `InvalidOperationException`.
- A command of your own, or one that Dapper runs, given the transaction runs outside any transaction, and is committed on its own.
- `Commit` throws `InvalidOperationException`, "This SqlTransaction has completed; it is no longer usable.".

**Why.**  An error before the first rows does not stop the batch, and the rest of it still runs.  Examples are a divide by zero in a `DECLARE`, and a failed `UPDATE` before the `SELECT`.  With `XACT_ABORT` off, the first error alone leaves the transaction open, but a later error of the batch can roll it back, such as a later `CAST('x' AS int)`.  The `SqlException` may not list that later error in its `Errors`.  So the method cannot tell this from an error that rolled the transaction back itself, and it throws the `SqlException` as it is.

**What to do.**  After you catch a `SqlException`, check the transaction, and treat a `Connection` that is null as a rollback:

```csharp
try
{
    var orders = await connection.ListOrders(new ListOrdersArgs(customerId), transaction: transaction, cancellationToken: token);
}
catch (SqlException exception) when (transaction.Connection is null)
{
    throw new InvalidOperationException("The transaction was rolled back by an error the query raised.", exception);
}
```

<a id="limit-rowcount-after-nocount"></a>

### A `rowcount` method returns `-1` after `SET NOCOUNT ON`

**What you see.**  On SQL Server, a `rowcount` method returns `-1` once `SET NOCOUNT ON` has run on the session of the connection.

**Why.**  A query without parameters, or a command of your own without them, that runs `SET NOCOUNT ON` sets it for the session, not for the statement.  On a connection that you opened, every later `rowcount` method returns `-1`, until the connection is closed and its session reset.  A query with parameters, which Microsoft.Data.SqlClient runs through `sp_executesql`, sets it for itself alone, and only its own count is `-1`.

**What to do.**  Do one of these:

- Run `SET NOCOUNT OFF` after it.
- Do not set it in a query without parameters on a connection that runs a `rowcount` method.

<a id="limit-list-in-an-aggregate"></a>

### `SqlException` 130 for an empty or null list after `NOT` inside an aggregate of your own

**What you see.**  A query such as `SELECT dbo.Median(IIF(status NOT IN @statuses, balance, NULL)) AS median FROM dbo.accounts` throws `SqlException` when the list is empty or null: "Cannot perform an aggregate function on an expression containing an aggregate or a subquery." (130).  It runs for every list that holds a value, and the build and describing succeed.

**Why.**  SQL Server refuses, inside an aggregate, the form in which an empty or null list after a `NOT` is sent.  A list after a `NOT` inside an aggregate of your own fails when the query runs.  So does such a list where a token writes the aggregate's name, or the `GROUP BY` or `OUTPUT` that the list stands in.  Inside a built-in aggregate, a `GROUP BY` or an `OUTPUT` clause written in the query, such a list is [SQLSRC127](../../../diagnostics.md#sqlsrc127) at build: see [lists](#list-parameters).  A list that no `NOT` reaches runs empty in every one of these places.

**What to do.**  Test the list in a `WITH` query, or a subquery in `FROM`, and aggregate its result.  This runs for an empty list too:

```sql mssql
-- name: CountAccountsWithoutStatuses
WITH tested AS (
    SELECT CASE WHEN status NOT IN @statuses THEN 1 ELSE 0 END AS hit
    FROM dbo.accounts
)
SELECT SUM(hit) AS outside
FROM tested;
```
