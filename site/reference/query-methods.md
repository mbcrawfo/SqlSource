# Query methods

<a id="query-methods"></a>

## Query methods

```sql postgres
-- name: GetUser -> one-optional
-- summary: Loads one user by id.
SELECT id, name, deleted_at
FROM users
WHERE id = @id;
```

```csharp
// A repository, at the default output, repository
GetUserDto? user = await users.GetUser(new GetUserParams(42));

// At the output methods
GetUserDto? user = await connection.GetUser(new GetUserParams(42));
```

A query whose [output](../settings.md#setting-output) is `repository`, the default, or `methods` gets an asynchronous method that runs it, in place of its constant or token method, and a [synchronous method](#synchronous-methods) instead or beside it when you ask for one.  The two outputs differ in where the connection comes from:

| Output | The method | Called as |
|----|----|----|
| `repository`, the default | A [repository method](repositories.md#repository-methods): a `public` instance method of the type, which gets its own connection from a member of the type, and takes no connection, transaction or timeout | `users.GetUser(...)` |
| `methods` | A [method that takes a connection](connection-methods.md#methods-that-take-a-connection): by default an `internal` extension method of `DbConnection`, which takes the connection, a transaction and a timeout | `connection.GetUser(...)` |

Either method sends each parameter, runs the query and reads its rows into the output model, here one row or null, by its [result shape](sql-files.md#result-shapes).  It runs on ADO.NET's `DbConnection`, through the driver your project references, Npgsql for PostgreSQL and Microsoft.Data.SqlClient for SQL Server, and uses no reflection and no library of SqlSource's.  Its SQL is built inside the method, from the query's text, its [tokens](tokens.md#tokens) and its [lists](parameters.md#list-parameters), and your code cannot reach it: a project that runs a query's SQL itself, through Dapper or its own code, gives it the output `models`.  One type can have queries of both outputs, in one file or in several: a repository keeps the queries that run alone at `repository`, and gives those that its own code runs in a transaction the output `methods`.  What each shape returns, how a row is read and a parameter sent, and the exceptions are the same for both.  A query can also get a [collection method](collection-methods.md#collection-methods), an overload of its method that runs it over a collection of its input models.

<a id="which-methods"></a>

## Which queries get a method

A query gets its method from a type that claims its file when, for that type:

- **its output is `repository` or `methods`**;
- **it has what its models need**: a current entry in its sidecar, a C# type for each column and parameter, and every model it needs, an input model when it has parameters and an output model when it returns rows.  A query with neither needs no model;
- **its method can be written**: a result shape that its rows allow ([SQLSRC272](../diagnostics.md#sqlsrc272)), a name that nothing else takes, for each of its methods ([SQLSRC274](../diagnostics.md#sqlsrc274)), no stream under `method-type=sync` ([SQLSRC298](../diagnostics.md#sqlsrc298)), no token named as an argument a query method can have ([SQLSRC275](../diagnostics.md#sqlsrc275)), and, in the extension class, a class to hold it ([SQLSRC276](../diagnostics.md#sqlsrc276));
- **for a repository query, its type has its connection**: the type is not static ([SQLSRC034](../diagnostics.md#sqlsrc034)), and one member of it, or a pair, gives the connection of the query's database, as [the connection](repositories.md#repository-connection) says ([SQLSRC028](../diagnostics.md#sqlsrc028) to [SQLSRC033](../diagnostics.md#sqlsrc033), [SQLSRC036](../diagnostics.md#sqlsrc036)), and, for a synchronous method, a member that returns no task ([SQLSRC299](../diagnostics.md#sqlsrc299));
- **its [converters](../settings.md#converters) serve it**: a column or parameter whose type a type override, a column type or a `-- param:` marker chose, and whose type has converters, has one it can use: of the direction it needs ([SQLSRC280](../diagnostics.md#sqlsrc280)), between it and the type the driver reads when it is a type of yours that the driver never reads ([SQLSRC279](../diagnostics.md#sqlsrc279)), and the only one of that direction, with nothing wrong with it ([SQLSRC277](../diagnostics.md#sqlsrc277), [SQLSRC278](../diagnostics.md#sqlsrc278));
- **the project references the driver of its dialect** ([SQLSRC273](../diagnostics.md#sqlsrc273)), at Npgsql 8.0 or later ([SQLSRC247](../diagnostics.md#sqlsrc247)) or Microsoft.Data.SqlClient 6.1 or later ([SQLSRC267](../diagnostics.md#sqlsrc267)).

A query whose output is `repository` or `methods` that fails any of the others has an error, and gets neither its methods nor a constant until it is fixed, but for [SQLSRC299](../diagnostics.md#sqlsrc299), which costs a repository query only its synchronous methods.  Its models are still generated when it has them, and the type's other queries get their methods.  A [placement error](members.md#locations), such as a visibility that does not fit the query's member location ([SQLSRC123](../diagnostics.md#sqlsrc123)), is different: it leaves the query's whole file without members and without models for the type.  A visibility that does not fit only the models the method takes or returns, also SQLSRC123, leaves out the method alone, as the others here do.  Each type that claims a file gets its own methods from it, by its own settings, and [where the methods go](connection-methods.md#method-location) says when two such types give one `methods` method twice.  Only the queries of a dialect that can be described, `postgres` or `mssql`, have entries: a query of another dialect whose output is `repository` or `methods` is [SQLSRC209](../diagnostics.md#sqlsrc209), and wants the output `sql`.  A statement that the tool cannot describe, such as PostgreSQL's `CALL` ([SQLSRC233](../diagnostics.md#sqlsrc233)), gets no method either, so on PostgreSQL no method calls a procedure: give such a query `-- output: sql` and run its constant yourself.

<a id="synchronous-methods"></a>

## Synchronous methods

```sql postgres
-- output: methods

-- name: GetUser -> one-optional
-- generator: method-type=both
SELECT id, name
FROM users
WHERE id = @id;
```

```csharp
// Generated, shown without global::
internal static GetUserDto? GetUser(
    this DbConnection connection,
    GetUserParams parameters,
    DbTransaction? transaction = null,
    int? commandTimeout = null)

internal static async Task<GetUserDto?> GetUserAsync(
    this DbConnection connection,
    GetUserParams parameters,
    DbTransaction? transaction = null,
    int? commandTimeout = null,
    CancellationToken cancellationToken = default)

// Your code, where it does not await
GetUserDto? user = connection.GetUser(new GetUserParams(42));
```

The [generator parameter](../settings.md#setting-generator-parameters) `method-type` gives a query whose output is `methods` or `repository` a synchronous method, for code that does not await, such as a console tool or a library whose API is synchronous.  Under `sync` the query gets a synchronous method in place of its asynchronous one, and under `both` it gets the two, as `GetUser` above does.  The default, `async`, gives the asynchronous method alone.  A synchronous method runs the query on the calling thread, with the driver's synchronous calls, and never waits on a task.

- **Its name is the query's**, never with `Async` after it, whatever `async-method-suffix` says.  Under `both`, the asynchronous method's name always has `Async` after it, with or without `async-method-suffix`, since two methods that differ only by `cancellationToken`, an argument with a default, would make every call ambiguous.  Each of a query's names is checked as [where the methods go](connection-methods.md#method-location) says, and one that something else takes is [SQLSRC274](../diagnostics.md#sqlsrc274): the query gets none of its methods until it is fixed, or, for a clash with a method of another type in an extension class, none of that name.
- **Its arguments are the asynchronous method's without `cancellationToken`**: the connection, the input model, a `string` for each [token](tokens.md#tokens), `transaction` and `commandTimeout`, as [the signature](connection-methods.md#method-signature) says, and for a [repository method](repositories.md#synchronous-repository-methods) the input model and the tokens alone.  Nothing cancels it, as ADO.NET's synchronous calls take no token; `commandTimeout` still limits the command.  A token still cannot be named `cancellationToken`, under any `method-type` ([SQLSRC275](../diagnostics.md#sqlsrc275)).
- **It returns what the asynchronous method's task holds**, as [what each shape returns](return-types.md#method-shapes) says: the collection for `many`, the row for `one`, the row or null for `one-optional`, the count for `rowcount`, and nothing, `void`, for `none`.
- **It does what the asynchronous method does**: it opens a closed connection and closes it again before it returns or throws, as [the connection](connection-methods.md#method-connection) says, or a repository's gets its connection from the repository, runs in the `transaction` it is given, sends each parameter and reads each row in the same way, and throws the same exceptions, [`UnexpectedRowCountException`](exceptions.md#unexpected-row-count), the driver's exceptions and, on SQL Server, [`TransactionRolledBackException`](exceptions.md#transaction-rolled-back).  Its documentation in IntelliSense is the asynchronous method's, without `cancellationToken`, and for `none` without a line for what it returns.
- **A stream has no synchronous method.**  A `many` query whose [collection type](return-types.md#setting-collection-type) is `IAsyncEnumerable` gives each row to the caller's loop as it is read, which only an asynchronous method does.  Under `both` it gets its asynchronous method alone, named with `Async`, and, with a [collection method](collection-methods.md#collection-methods), which reads its rows into an array and never streams them, both collection methods: `ListUsers(IEnumerable<ListUsersParams> parameters, ...)` and `ListUsersAsync(IEnumerable<ListUsersParams> parameters, ...)` for a query `ListUsers`.  Under `sync` it is [SQLSRC298](../diagnostics.md#sqlsrc298), and gets no method and no collection method.  For a synchronous method, give it a collection type that is read whole, such as `List`.
- **On PostgreSQL**, a data source with `Multiplexing=true` refuses a synchronous method on a connection that has no transaction open, such as the one a repository method gets from its data source: see [what a method reads and throws](databases/postgresql/query-methods.md#method-results).

A query's [collection method](collection-methods.md#collection-methods) follows `method-type` as its method does.  Under `sync` it is synchronous, named as the query, an overload of the synchronous method by its `IEnumerable` argument, and under `both` the query gets the two, the synchronous one named as the query and the asynchronous one with `Async` after it:

```csharp
// -- name: GetUser -> one-optional, with -- generator: collection-method method-type=both,
// generated beside GetUser and GetUserAsync, shown without global::
internal static GetUserDto[] GetUser(
    this DbConnection connection,
    IEnumerable<GetUserParams> parameters,
    DbTransaction? transaction = null,
    int? commandTimeout = null)

internal static async Task<GetUserDto[]> GetUserAsync(
    this DbConnection connection,
    IEnumerable<GetUserParams> parameters,
    DbTransaction? transaction = null,
    int? commandTimeout = null,
    CancellationToken cancellationToken = default)
```

- **A synchronous collection method's arguments are the asynchronous one's without `cancellationToken`**, and it returns what the asynchronous one's task holds: the collection, the count, or nothing, `void`, for `none`, as [what it returns](collection-methods.md#collection-methods) says.
- **It does what the asynchronous one does**: it reads the collection once, runs the items 1,000 at a time, begins a transaction of its own unless it is given one or one is ambient, by the generator parameter [`transaction`](collection-methods.md#collection-transactions), and throws the same exceptions, [`CollectionItemException`](exceptions.md#collection-item-exception) with the item's `ItemIndex`, `UnexpectedRowCountException` and, on SQL Server, `TransactionRolledBackException` and the `InvalidOperationException` of a query that returns more than one result set, as [what it throws](collection-methods.md#collection-failures) says.
- **Its name, the query's, is checked as a method's is**, by [where the methods go](connection-methods.md#method-location), and one that something else takes is [SQLSRC274](../diagnostics.md#sqlsrc274).  That holds for a stream's too, which has no synchronous method of that name.

<a id="reading-rows"></a>

## How a row is read

- **Each column is read by its position** into the property the output model has for it, so its name does not matter: a `!` or `?` at the end of an alias works as well as a [`-- column:` marker](model-properties.md#column-marker).  A record is made with its constructor, a class by setting its properties, and a [sorted](model-properties.md#property-order) model as well as any other.
- **A property that can be null** is null when its column is.
- **A property that cannot be null is read with no check**, so a null in its column throws, as the driver reports it: Npgsql's `InvalidCastException` "Column 'name' is null.", and Microsoft.Data.SqlClient's `SqlNullValueException` "Data is Null.".  Microsoft.Data.SqlClient gives no error for a null `vector`, so the method checks such a column itself, and throws `InvalidCastException` "Column 'embedding' is null." with the column's name.  A `!` at the end of an alias, a `-- column:` marker that says `not null`, or a column that the tool showed is never null, is so checked the first time a null arrives, and never read as a default value.
- **A JSON `null` is not a null.**  A JSON column that holds the text `null` is read as a value: a `JsonElement` of the kind `Null`, a `JsonDocument` whose root is one, and for a `JsonNode`, a `JsonObject`, a `JsonArray` or a class of yours read through System.Text.Json's serializer, null into a property that can be null, and into one that cannot, the method's own `InvalidCastException`, "Column 'metadata' holds a JSON null.", with the column's name.  A struct of yours read through the serializer is the same: null when its property can be null, and the method's `InvalidCastException` when it cannot.  See JSON on [PostgreSQL](databases/postgresql/libraries.md#json) and [SQL Server](databases/sqlserver/libraries.md#json), and [a class in a JSON column](../settings.md#json-class).
- **Each column is read as its property's C# type**, and each parameter sent as one, unless a converter converts it.  A C# type that a [type override](../settings.md#setting-type-overrides), a [column type](model-properties.md#setting-column-types) or a `-- param:` marker names, with no converter, is given to the driver as it is, which suits a type the driver knows or was taught, and throws the driver's `InvalidCastException` for one it was never taught: see [converters](../settings.md#converters).  A class of yours in a column that holds JSON is read and sent through System.Text.Json's serializer instead: see [a class in a JSON column](../settings.md#json-class).  A type that the query methods convert themselves is not given to the driver either, whichever place chose it: on SQL Server NodaTime's, NetTopologySuite's and System.Text.Json's, and on Npgsql 8 and 9 `JsonNode`, `JsonObject` and `JsonArray`, as [SqlSource's own conversions](../settings.md#converters) says.
- **A column or parameter whose type a library gives**, by the [library types](../settings.md#setting-library-types), is read and sent on PostgreSQL by Npgsql: NodaTime's and NetTopologySuite's through their plugins, which your application registers on its data source, as [what a method reads and throws](databases/postgresql/query-methods.md#method-results) says, and without which the driver throws `InvalidCastException`, and System.Text.Json's `JsonElement` with nothing to register.  On SQL Server it is read and sent by the query method itself, with nothing to register: NodaTime's and System.Text.Json's are read as the type that Microsoft.Data.SqlClient reads and converted, and converted before they are sent, as [NodaTime on SQL Server](databases/sqlserver/libraries.md#nodatime) and [JSON on SQL Server](databases/sqlserver/libraries.md#json) say, and NetTopologySuite's is read and written as [NetTopologySuite on SQL Server](databases/sqlserver/libraries.md#nettopologysuite) says.  Either way, a [converter](../settings.md#converters) of yours of the direction it needs is used instead, but for NetTopologySuite's on SQL Server.
- **A column whose type a type override or a column type chose, and that has a [converter](../settings.md#converters)**, is read as the type the driver reads its database type as, and that value given to the converter, whose result is the property's value.  A null never reaches the converter: a property that cannot be null throws as above, before the converter is called.

<a id="method-parameters"></a>

## How a parameter is sent

- **Each parameter is sent by its name**, without its `@`, with the value of its property of the input model, in the order of the query's [parameter list](parameters.md#query-parameters).  Both drivers match a name ignoring case, as SqlSource does, so `@Id` and `@id` are one parameter, and one value fills both; under `mssql`, a second spelling is the warning [SQLSRC121](../diagnostics.md#sqlsrc121).  A parameter written in snake_case, `@user_id`, is filled from its property `UserId` like any other.
- **A null value is sent as the database's `NULL`**: that of a property that can be null, and that of a property of a reference type that cannot be, which a caller can set to null all the same.
- **A [list parameter](parameters.md#list-parameters) is sent as one parameter for each value**, each named after the list and sent as a parameter of the values' type, padded to one of a few sizes unless the query has `list-padding=off`.  A null list is sent as an empty one, and a list that the query tests for null also sends a parameter of its own name, null when the list is null.
- **A parameter whose type a type override or its `-- param:` marker chose, and that has a [converter](../settings.md#converters)**, is given to the converter, and the value it returns is sent.  A null is sent as `NULL`, without the converter.  A class of yours in a parameter that holds JSON is given to System.Text.Json's serializer in the same way, and its text sent: see [a class in a JSON column](../settings.md#json-class).
- **Each parameter is sent as the database type it was described with**, or that its [`-- param:` marker](parameters.md#param-marker) declares, and not as a type chosen from the C# type of its value.  On SQL Server it is sent with the type's length, precision and scale.  See how a parameter is sent on [PostgreSQL](databases/postgresql/query-methods.md#method-parameters) and on [SQL Server](databases/sqlserver/query-methods.md#method-parameters).

<a id="limits"></a>

## Limits

<a id="limit-wrapped-connection"></a>

### `InvalidCastException` from a method with parameters on a connection that wraps the driver's

- **A query method with parameters works only on a connection whose commands take its driver's own parameters.**  It works on the connections of the drivers, such as `NpgsqlConnection`, the one an `NpgsqlDataSource` gives, and `SqlConnection`, and so on those a repository makes from a connection string or a data source.  A `DbConnection` that wraps one of them, and whose commands create or accept only parameters of their own type, fails before the query is sent: on PostgreSQL with the wrapper's own exception, typically an `InvalidCastException`, and on SQL Server with `InvalidCastException`, "Unable to cast object of type '...' to type 'Microsoft.Data.SqlClient.SqlParameter'.".  Give the method the driver's connection that the wrapper holds, from a repository's factory method or a connection it borrows too, or give the query the output `models` and send its parameters through the wrapper yourself.
