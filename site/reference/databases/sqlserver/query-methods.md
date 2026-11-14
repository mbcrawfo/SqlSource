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

Each described query whose output is `repository` or `methods` gets a method that runs it through Microsoft.Data.SqlClient: see [query methods](../../../generator.md#query-methods).  Microsoft.Data.SqlClient has no data source, so a repository's [connection source](../../../generator.md#repository-connection) is a connection string, as above, from which each call makes a `SqlConnection` that it opens and disposes, or a factory method, or a connection that the repository borrows.  A method at the output `methods` runs on the `DbConnection` it is given, such as a `SqlConnection`.  The project references Microsoft.Data.SqlClient 6.1 or later ([SQLSRC273](../../../diagnostics.md#sqlsrc273), [SQLSRC267](../../../diagnostics.md#sqlsrc267)).  Above, `placed_at` is a `datetime2(7)`, `total` a `decimal(12,2)` and `status` an `nvarchar(20)`, so the method sends `@placedAt` as a `datetime2`, with all seven digits of its time, `@total` as a `decimal(12,2)` and `@status` as an `nvarchar(20)`.  A value that fits its type never changes how it is sent; what happens to one that does not is [below](#method-parameters).

<a id="method-parameters"></a>

## How a parameter is sent

Each parameter is sent as the SQL Server type it was described with, with that type's length, precision and scale, and never as a type chosen from its value.  So a value keeps its digits, a null is a `NULL` of the parameter's type, which a column of that type takes, and the server sees one declaration of the query whatever the lengths of values that fit their types, and keeps one plan of it.  How a method names a parameter and sends a null is on [the generator page](../../../generator.md#method-parameters).  For some types:

- **A string or a `byte[]` longer than its type** is sent whole, as a `varchar(max)`, an `nvarchar(max)` or a `varbinary(max)`, and never cut.  The server refuses an `INSERT` or an `UPDATE` that would cut it.  The error is 2628, "String or binary data would be truncated".  It is 8152 on SQL Server 2017, and in a database whose compatibility level is below 150 or whose `VERBOSE_TRUNCATION_WARNINGS` is off.  A `WHERE` that compares a column with such a value finds no row that holds its start: `abcdef` finds no `char(5)` that holds `abcde`.

  Spaces at the end of a string, and zero bytes at the end of a `byte[]`, are the exception.  When a value is longer than its column only by them, the server cuts them off with no error and stores the rest.  The server also pads the shorter of two strings with spaces to compare them, so `abcde ` finds the `char(5)` that holds `abcde`.
- **`datetime2`, `time` and `datetimeoffset`** are sent with seven digits of a second, whatever the scale of their type, and the server rounds a value to the scale of the column it stores it in: `12:00:00.1235678` written to a `datetime2(3)` column is `12:00:00.124`.  A `WHERE` compares a column with all seven digits of the value, so a `datetime2(3)` that holds `12:00:00.124` is found by `12:00:00.124` and not by `12:00:00.1235678`.
- **A `decimal` or `numeric` with more digits after the point than its type** is rounded to the type's scale, half away from zero, before it is sent, as the server rounds it when it stores it: `1.23456` for a `decimal(12,4)` is sent as `1.2346`, which an `INSERT` stores and a `WHERE` finds.  One with more digits before the point than the type holds throws `ArgumentException`, "Parameter value '123456789.1000' is out of range.", and nothing is sent.
- **`json`** is sent as an `nvarchar(max)`, which the server checks and stores as `json`.  A value that is not an object or an array is refused there: see [JSON](libraries.md#json).
- **`vector`** is sent as its `SqlVector<float>`, whose length is its dimension, and a null as the null `vector` of the parameter's dimension.
- **An alias type** is sent as its base type: a parameter of `dbo.Email`, made `FROM nvarchar(320)`, as an `nvarchar(320)`.
- **An `xml` of an XML schema collection**, declared `-- param: @p xml(DOCUMENT dbo.S)`, is sent as `xml`, which has no collection, so a query that needs the collection's typing, such as `@p.value('/a', 'nvarchar(10)')` of a document, casts it in the query with `CAST(@p AS xml(DOCUMENT dbo.S))`, and is refused when it is described without the cast.
- **A parameter that has a [converter](../../../settings.md#converters)** is sent as its database type too: an `Email` whose converter returns a `string` is sent as that `nvarchar(320)`, and a converted string longer than that whole, as above.
- **A parameter of one of System.Text.Json's types, or of a class of yours in a [JSON](libraries.md#json) column,** is converted to its JSON's text and sent as its database type, as a parameter with a converter is: as an `nvarchar(max)` for a `json`, and as the column's own type, with its length, for a string column.
- **A parameter of a [NodaTime](libraries.md#nodatime) type** is converted to the C# type of [the table](types.md#type-table) and sent as its database type, as a parameter with a converter is: a `NodaTime.LocalDateTime` for a `datetime` is sent as a `datetime`, and a `NodaTime.LocalTime` as a `time`.  What the conversion does to a value that its column cannot hold is under [NodaTime](libraries.md#nodatime).
- **A parameter of a [NetTopologySuite](libraries.md#nettopologysuite) type** is sent as its `geography` or `geometry`, with the value's SRID, and a null as a `NULL` of it.
- **`hierarchyid`, `geography`, `geometry` and a CLR type of your own assembly**, given a C# type by a [type override](../../../settings.md#setting-type-overrides), such as `Microsoft.SqlServer.Types.SqlGeography`, are sent as their SQL Server type, and a null as a `NULL` of it.  A CLR type is named by its schema: `Money` of the schema `dbo` is sent as `dbo.Money`.
- **A type of a newer SQL Server**, which only a type override gives a C# type too, is sent by its value, and Microsoft.Data.SqlClient chooses its type from it.
- **A [list parameter](#list-parameters)** sends each of its values as a parameter of the values' type, as above.

<a id="list-parameters"></a>

## Lists

```sql mssql
-- name: ListOrdersByIds
SELECT id, placed_at FROM dbo.orders WHERE id IN @ids;
```

SQL Server has no arrays, so a [list parameter](../../parameters.md#list-parameters), written after `IN` with no parentheses, is how a query method sends a list of values.  Above, `ListOrdersByIdsParams.Ids` is an `IEnumerable<int>`, and each of its values is sent as an `int`, the type of `id`, as [any parameter](#method-parameters) of that type is.  A list's test for null, `@ids IS NULL`, is sent as a `bit` of the list's name.

- **A list after a `NOT` cannot stand inside an aggregate, a `GROUP BY` or an `OUTPUT` clause.**  SQL Server refuses there the form that such a list is sent in when it is empty or null, so under the output `methods` or `repository` a query such as `SELECT COUNT(IIF(category NOT IN @categories, 1, NULL)) AS others FROM dbo.products` is [SQLSRC127](../../../diagnostics.md#sqlsrc127) at build.  Test the list in a `WITH` query, and aggregate its result, as the error's page shows.  An aggregate that `OVER` follows is not one of these places, and a list that no `NOT` reaches runs empty in all of them.  A `NOT` reaches only the condition it stands in, so one in a `WHERE` leaves a list in the `HAVING` after it alone.  An aggregate of your own is a [limit](#limit-list-in-an-aggregate).
- **An empty list assumes that the connection leaves `ANSI_NULLS` on**, as Microsoft.Data.SqlClient does.  With it off, an empty list after `IN` matches the rows whose value is null: see [limits](#limit-ansi-nulls-off).
- **A query sends at most 2,098 parameters.**  SQL Server takes 2,100 parameters in one request, and Microsoft.Data.SqlClient uses two of them to send a query with parameters.  Each value of each list counts, and so does every other parameter of the query: a method whose query would send more throws `ArgumentException`, as [the limit](../../parameters.md#list-limit) says, and leaves out the [padding](../../parameters.md#list-padding) of a query that padding alone would take past it.
- **For more values than that, send them as one JSON array** in an `nvarchar(max)` parameter, and read it with `OPENJSON`, in a database whose compatibility level is 130 or later:

  ```sql mssql
  -- name: ListOrdersByIdArray
  -- param: @ids nvarchar(max)
  SELECT id, placed_at FROM dbo.orders
  WHERE id IN (SELECT CAST(value AS int) FROM OPENJSON(@ids));
  ```

  ```csharp
  var found = await orders.ListOrdersByIdArray(new ListOrdersByIdArrayParams(JsonSerializer.Serialize(ids)));
  ```

  The query sends one parameter whatever the count, and has one form, so SQL Server keeps one plan of it.  The marker is needed: SQL Server cannot tell the type of `@ids` from `OPENJSON(@ids)` alone.
- **A table-valued parameter** does the same through a table type of your database, but a query method cannot send one.  Give such a query `-- output: sql`, and send its parameter with your own code.

<a id="method-results"></a>

## What a method reads and throws

- **Give each `methods` method the transaction** of a connection that has one: Microsoft.Data.SqlClient refuses a command without it, with `InvalidOperationException`, before anything is sent, as [the connection](../../../generator.md#method-connection) says.  Its message names the call that was refused: `ExecuteReader`, `ExecuteScalar` or `ExecuteNonQuery` from a [synchronous method](../../../generator.md#synchronous-methods), and `BeginExecuteReader` or `BeginExecuteNonQuery` from an asynchronous one, so the two methods of a query under `method-type=both` throw it with messages that differ in that word.  A repository method takes no transaction, so a connection that it borrows must have none: run the queries of a transaction at the output `methods`, as [transactions and timeouts](../../../generator.md#repository-transactions) says.
- **A `methods` method refuses a transaction that has completed.**  Given a transaction that was committed or rolled back before the call, every method throws `InvalidOperationException`, "The transaction given to the query 'ListOrders' has completed; it is no longer usable.", with the query's name, and nothing runs: the connection is not opened and no command is sent.  Microsoft.Data.SqlClient would otherwise run the command outside any transaction and commit it on its own.  A loop over [streamed rows](../../../settings.md#streaming-rows) checks when the loop starts, so it throws when the loop asks for its first row, and a transaction that completes between the call and the loop is refused too.  A method knows that a transaction has completed by its `Connection`, which Microsoft.Data.SqlClient's `SqlTransaction` sets to null.  A transaction of a library that wraps Microsoft.Data.SqlClient's and keeps its `Connection` set after it completes is not refused, and its command runs outside any transaction as before: give the method the `SqlTransaction` inside.

<a id="reading-a-row"></a>

### Reading a row

- **A column is read as its property's C# type**, the one [the types](types.md#types) give it.  A type of yours that Microsoft.Data.SqlClient never reads, such as an id of your own, takes [converters](../../../settings.md#converters): the column is read as the type of [the table](types.md#type-table), an alias type as its base type, and converted.
- **A column of a [NodaTime](libraries.md#nodatime) type** is read as the type of [the table](types.md#type-table) and converted, with nothing registered on the connection or in the application.
- **A column of a [NetTopologySuite](libraries.md#nettopologysuite) type** is read with nothing registered on the connection or in the application, and without Microsoft.SqlServer.Types.  A column read as a shape, such as a `Point`, that holds another throws `InvalidCastException`.
- **A column of one of System.Text.Json's types, or of a class of yours in a [JSON](libraries.md#json) column,** is read as a `string` and parsed, with nothing registered either.  A JSON `null` in it is no null, and what it gives is under [JSON](libraries.md#json).
- **A C# enum is read only from a column of its own underlying type**: an enum over `int`, the default, from an `int`, and an enum over `byte` from a `tinyint`.  An enum over `int` throws `InvalidCastException` for a `tinyint`, a `smallint` or a `bigint`, since Microsoft.Data.SqlClient never widens or narrows a value.  Declare the enum over its column's type, `enum Level : byte` for a `tinyint`.

<a id="null-values"></a>

### Null values

- **A column whose property cannot be null** throws Microsoft.Data.SqlClient's `SqlNullValueException` when it holds a null, and a `vector` column the method's own `InvalidCastException`, "Column 'name' is null.", before any converter is called.

<a id="errors-and-rollbacks"></a>

### Errors and rollbacks

- **A query of several statements returns the rows of its first result set**, as its model has them, and its other statements still run.  An error that a statement after that result set raises, such as a `THROW`, is thrown as the error's `SqlException` by every method: one that reads rows, `one`, `one-optional` or `many`, reads the rest of the batch before it returns, so it throws the error in place of its rows, and a loop over [streamed rows](../../../settings.md#streaming-rows) throws it after the last row, unless, in a transaction, a later error of the batch then rolls the transaction back, when the method throws `TransactionRolledBackException`, as [a later bullet](#method-results) says.  The statements before the error have run, and in a transaction the error may have rolled the transaction back, depending on the error and on `XACT_ABORT`: under `SET XACT_ABORT ON` nearly every error does, and with it off `CAST('x' AS int)` does where a `THROW`, a divide by zero or `CAST(300 AS tinyint)` leaves it open.  Its `rowcount` adds up the rows its statements insert, update or delete, and a `SELECT` among them adds nothing.
- **A loop over [streamed rows](../../../settings.md#streaming-rows) that ends early throws an error of a row it left unread.**  At a `break`, or an exception in the loop's body, the rows still to come and the statements after them are read before the loop ends, and an error that SQL Server raises for one of them, such as a divide by zero, is thrown: at the `break`, or in place of the exception that the loop's body threw.  In a transaction, the error may have rolled the transaction back, as for an error after the rows.  To keep the body's exception too, see [streamed rows](../../../settings.md#streaming-rows).
- **An exception in a transaction that a later error of the batch rolled back is `TransactionRolledBackException`.**  A method that throws an exception of its own before it has read the rest of its batch, `UnexpectedRowCountException`, a null in a column whose property cannot be null, a `vector` column's `InvalidCastException` or a converter's exception, throws it in place of an error that the rest of the batch raises, and so does a method that reaches an error that SQL Server raises for a row, such as a divide by zero, or for a statement after the rows, such as `CAST(300 AS tinyint)`, which it throws as its `SqlException` in place of a later error.  The rest of the batch still runs, and in a transaction a later error there can roll the transaction back, such as a `CAST('x' AS int)` with `XACT_ABORT` off.  When it has, the method throws [`TransactionRolledBackException`](../../../generator.md#transaction-rolled-back), whose `InnerException` is the first exception, its own or the error's `SqlException`, and not the later error, so a `catch` of it tells you that the transaction you gave the method has completed: its `Connection` is null, and `Commit` throws `InvalidOperationException`, "This SqlTransaction has completed; it is no longer usable.".  An error that itself rolls the transaction back, such as a `THROW` or a duplicate key under `SET XACT_ABORT ON`, or a `CAST('x' AS int)` for a row or after the rows, is thrown as its `SqlException`, as the bullet on a query of several statements says.  Without a transaction, or with the transaction still open, the first exception is thrown as it is, and so it is for a transaction of a library that wraps Microsoft.Data.SqlClient's and keeps its `Connection` set, as the bullet on a completed transaction says.  The same holds for a loop over [streamed rows](../../../settings.md#streaming-rows): at a row, after its last row, and for an error of a row or a statement that a `break` or an exception of the loop's body leaves unread.  An error that SQL Server raises before the query's rows begin is thrown as its `SqlException`, even when the rest of the batch then rolls the transaction back: see [limits](#limit-hidden-rollback).

<a id="rowcount"></a>

### `rowcount`

- **`rowcount` is `-1` for a `SELECT`, and under `SET NOCOUNT ON`.**  A query that sets `NOCOUNT` returns `-1`.  When it has no parameters, the setting also lasts for the connection's session, so a `rowcount` method called after it on the same open connection returns `-1` too; a query with parameters, which Microsoft.Data.SqlClient runs through `sp_executesql`, sets it for itself alone.  See [the generator's known issues](../../../generator.md#known-issues).

<a id="timeout-and-cancel"></a>

### Timeouts, cancelling and the connection

- **`commandTimeout`**, of a `methods` method, that passes throws `SqlException` with `Number` `-2`, whose message starts "Execution Timeout Expired.".
- **A cancelled method throws as Microsoft.Data.SqlClient does**, and the method passes it on unchanged: `TaskCanceledException` when the token is cancelled before the call or once the server has begun to send the result, and `SqlException`, with `Number` `0` and a message that ends "Operation cancelled by user.", when it is cancelled while the command runs before that.  A `SELECT` usually begins its result before its first row, so a cancel while it waits for that row is a `TaskCanceledException`, and a cancel while an `UPDATE` waits on a lock is a `SqlException`.  A `catch (OperationCanceledException)` does not catch the second: check the token in a `catch (SqlException)` as well.
- **The connection's commands must create Microsoft.Data.SqlClient's own parameters**, as those of `SqlConnection` do.  A connection that wraps a `SqlConnection` and creates parameters of its own is a [known issue](../../../generator.md#known-issues).

<a id="collection-methods"></a>

## Collection methods

```sql mssql
-- name: AddOrder -> one
-- generator: collection-method
INSERT INTO dbo.orders (customer_id, placed_at, total, status)
OUTPUT INSERTED.id
VALUES (@customerId, @placedAt, @total, @status);
```

A query's [collection method](../../../generator.md#collection-methods) runs its items through Microsoft.Data.SqlClient, 1,000 to a round trip.  What a failed item leaves in the database depends on the transaction the method runs in, and on `XACT_ABORT`:

| The transaction | When an item fails |
|----|----|
| Its own, by `transaction=on`, the default, or an isolation level | Every item is rolled back, those of earlier round trips too, and nothing is left |
| None, by `transaction=off` | SQL Server commits each item on its own.  With `XACT_ABORT` off, an error such as a duplicate key ends its item's statement alone: the other items of its round trip, those after it included, and the round trips before it stay, and the round trips after it do not run.  Under `SET XACT_ABORT ON` the error also stops its round trip, so the items after it do not run, and so does an error that ends the batch, such as a failed conversion, `CAST('x' AS int)`, with `XACT_ABORT` off |
| The caller's, a `transaction` given at the output `methods` or an ambient one | The method leaves it to you.  With `XACT_ABORT` off, a duplicate key ends its item's statement alone, and the transaction stays open, with every other item of its round trip and of those before it, for you to commit or roll back.  An error that rolls the transaction back itself, nearly any under `SET XACT_ABORT ON`, or one that ends the batch, such as a failed conversion, `CAST('x' AS int)`, leaves nothing, and the transaction has completed: its `Connection` is null |

- **A query whose statements return more than one result set cannot run over a collection** at the shapes `one`, `one-optional` and `many`, which read one result set for each item.  A statement that returns none, such as `SET NOCOUNT ON`, a `DECLARE` or an `UPDATE` without `OUTPUT`, is fine.  A query of two result sets, or one whose `SELECT` stands in an `IF` and returns no result set for some items, throws `InvalidOperationException`, "The query 'FindOrder' returned a result set of another shape, or none, for one of the items from 0 to 1, so it cannot run over a collection: a query whose statements return more than one result set, or none for an item, cannot.", and its own transaction is rolled back.  The items it names are those of the round trip up to where the mismatch showed, since the result set too many or missing can be any of theirs.  When the other result set has as many columns as the query's rows, the method throws the same at the end of the round trip, or, before that, an item reads it as its own and throws `UnexpectedRowCountException` or Microsoft.Data.SqlClient's `InvalidCastException`; either way it never returns its rows.  The shapes `none` and `rowcount` read no rows, so their result sets do not matter.
- **Given a transaction, a method that reads rows can throw [`TransactionRolledBackException`](../../../generator.md#transaction-rolled-back)**, as a query method does [above](#method-results): when it threw an exception of its own, or reached an item's error that left the transaction open, and a later error of the round trip then rolled the transaction back.  Its `InnerException` is the first exception: the method's own, such as `UnexpectedRowCountException`, or the [`CollectionItemException`](../../../generator.md#collection-item-exception) of the item's error.  An error that rolled the transaction back itself is thrown as its `CollectionItemException`.
- **A transaction that has completed is refused**, as for a query method, before anything runs.
- **`rowcount` adds 0 for an item whose count is not reported**, under `SET NOCOUNT ON`, where a query method returns `-1`.
- **The isolation level of the method's own transaction lasts for that transaction alone**, the level that `transaction` names, or read committed under `on`.  SQL Server keeps a level on the connection after a transaction ends, and the pool keeps it on a connection that is closed and opened again, so the method sets the connection back to the level it had before the call, whether the method returns or throws, and before it closes a connection it opened.  Later statements on the connection, and on the pooled connection opened next, run at the level they would have run at without the call.
- **`transaction=snapshot` needs the database to allow it**: `ALTER DATABASE app SET ALLOW_SNAPSHOT_ISOLATION ON`.  Without it, SQL Server refuses the transaction's first item, with its error 3952.  SQL Server has all five levels, `read-uncommitted`, `read-committed`, its default, `repeatable-read`, `serializable` and `snapshot`.
- **`commandTimeout`** is that of each round trip, and **a cancelled method throws as a query method does**, often `TaskCanceledException` when an item waits, as on a lock, for the rows it returns; either way, the method's own transaction is rolled back.

<a id="limits"></a>

## Limits

<a id="limit-ansi-nulls-off"></a>

### An empty list after `IN` matches the rows whose value is null

- **A connection that turns `ANSI_NULLS` off changes what an empty list matches.**  With `ANSI_NULLS` off, SQL Server takes `= NULL` as true for a null, so an empty or a null list after `IN` matches the rows whose value is null, where it should match none, and so does a null value in a list.  Microsoft.Data.SqlClient turns `ANSI_NULLS` on when it connects: leave it on, and run no `SET ANSI_NULLS OFF` on a connection that runs your queries.

<a id="limit-hidden-rollback"></a>

### A `SqlException`, and the transaction is rolled back

- **A transaction can be rolled back by an error hidden behind an error raised before the rows.**  When SQL Server raises an error before a query's first rows, such as a divide by zero in a `DECLARE` or a failed `UPDATE` before the `SELECT`, the method throws that error's `SqlException`, and the rest of the batch still runs.  With `XACT_ABORT` off the first error alone leaves the transaction open, but an error that the rest of the batch raises can roll it back, such as a later `CAST('x' AS int)`.  The `SqlException` may not list that later error in its `Errors`, and the method cannot tell this from an error that rolled the transaction back itself, so it throws the `SqlException` as it is.  If your code catches it and goes on with the transaction, the transaction's `Connection` is null: a generated method that you then give it [refuses it](#method-results) with `InvalidOperationException`, but a command of your own, or one that Dapper runs, given it runs outside any transaction and is committed on its own, and `Commit` throws `InvalidOperationException`, "This SqlTransaction has completed; it is no longer usable.".  After catching a `SqlException`, check the transaction, and treat a `Connection` that is null as a rollback:

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

<a id="limit-list-in-an-aggregate"></a>

### `SqlException` 130 for an empty or null list after `NOT` inside an aggregate of your own

- **A list after a `NOT` inside an aggregate of your own fails when it is empty or null.**  A query such as `SELECT dbo.Median(IIF(status NOT IN @statuses, balance, NULL)) AS median FROM dbo.accounts` runs for every list that holds a value, and throws `SqlException` when the list is empty or null: "Cannot perform an aggregate function on an expression containing an aggregate or a subquery." (130).  The build and describing succeed.  So it is too where a token writes the aggregate's name, or the `GROUP BY` or `OUTPUT` that the list stands in.  Inside a built-in aggregate, a `GROUP BY` or an `OUTPUT` clause written in the query, such a list is [SQLSRC127](../../../diagnostics.md#sqlsrc127) at build, and a list that no `NOT` reaches runs empty in every one of these places.  Test the list in a `WITH` query, or a subquery in `FROM`, and aggregate its result, which runs for an empty list too:

  ```sql mssql
  -- name: CountAccountsWithoutStatuses
  WITH tested AS (
      SELECT CASE WHEN status NOT IN @statuses THEN 1 ELSE 0 END AS hit
      FROM dbo.accounts
  )
  SELECT SUM(hit) AS outside
  FROM tested;
  ```
