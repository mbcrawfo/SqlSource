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

A query method is the generated method that runs a query.  It is what a described query becomes when its [output](settings.md#setting-output) is `repository`, the default, or `methods`: see [which queries get a method](#which-methods).  It is a [repository method](repositories.md#repository-methods) or a [method that takes a connection](connection-methods.md#methods-that-take-a-connection).  A [token method](members.md#token-methods) is not a query method: it returns SQL, and runs nothing.

The query method holds the SQL of the query, in place of a constant or a token method.  It sends each parameter, runs the query, and reads the rows into the output model by the [result shape](sql-files.md#result-shapes) of the query.  In the sample, the shape `one-optional` gives one row or null.

By default, a query method is asynchronous.  Under the generator parameter `method-type`, the query gets a [synchronous method](synchronous-methods.md#synchronous-methods) in place of the asynchronous one, or beside it.  The two outputs differ in where the connection comes from:

| Output | The method | Called as |
|----|----|----|
| `repository`, the default | A [repository method](repositories.md#repository-methods): a `public` instance method of the type, which gets its own connection from a member of the type, and takes no connection, transaction or timeout | `users.GetUser(...)` |
| `methods` | A [method that takes a connection](connection-methods.md#methods-that-take-a-connection): by default an `internal` extension method of `DbConnection`, which takes the connection, a transaction and a timeout | `connection.GetUser(...)` |

- **A method that takes a connection opens the connection only when it is closed.**  If the connection was open, the method leaves it as it was: see [the connection](connection-methods.md#method-connection).
- **A query method runs on the `DbConnection` of ADO.NET, through the driver that your project references.**  The driver is Npgsql for PostgreSQL, and Microsoft.Data.SqlClient for SQL Server.  The method uses no reflection, and no library of SqlSource's.
- **The method builds its SQL inside itself**, from the text of the query, its [tokens](tokens.md#tokens) and its [lists](list-parameters.md#list-parameters).  Your code cannot reach that SQL.  If your project runs the SQL of a query itself, through Dapper or its own code, give the query the output `models`.
- **One type can have queries of both outputs**, in one file or in several.  A repository keeps the queries that run alone at `repository`.  It gives the output `methods` to the queries that its own code runs in a transaction.
- **What each shape returns, how a row is read, how a parameter is sent, and the exceptions are the same for both outputs.**
- **A query can also get a [collection method](collection-methods.md#collection-methods).**  It is an overload of the query method that runs the query over a collection of input models.

<a id="which-methods"></a>

## Which queries get a method

A type that claims the file of a query gives the query its methods when all of these are true for that type:

- **The output of the query is `repository` or `methods`.**
- **The query has what its models need.**  It has a current entry in its sidecar, and a C# type for each column and parameter.  It has every model that it needs: an input model if it has parameters, and an output model if it returns rows.  A query with no parameters and no rows needs no model.
- **Each method of the query can be written.**  All of these are true:
  - The rows of the query allow its result shape ([SQLSRC272](../diagnostics.md#sqlsrc272)).
  - Nothing else takes the name of any of its methods ([SQLSRC274](../diagnostics.md#sqlsrc274)).
  - The query does not stream its rows under `method-type=sync` ([SQLSRC298](../diagnostics.md#sqlsrc298)).
  - No token has the name of an argument that a query method can have ([SQLSRC275](../diagnostics.md#sqlsrc275)).
  - For a repository query, no token has the name of the primary-constructor parameter that gives the connection ([SQLSRC275](../diagnostics.md#sqlsrc275)).  Nor does a token have the name of the repository, when the repository is not generic and a `static` member gives the connection.
  - If the method goes in the extension class, that class can be written ([SQLSRC276](../diagnostics.md#sqlsrc276)).
- **For a repository query, the type has the connection of the query.**  All of these are true:
  - The type is not static ([SQLSRC034](../diagnostics.md#sqlsrc034)).
  - One member of the type, or a pair of members, gives the connection of the database of the query, as [the connection](repository-connections.md#repository-connection) says ([SQLSRC028](../diagnostics.md#sqlsrc028) to [SQLSRC033](../diagnostics.md#sqlsrc033), [SQLSRC036](../diagnostics.md#sqlsrc036)).
  - For a synchronous method, one member that gives the connection returns no task ([SQLSRC299](../diagnostics.md#sqlsrc299)).
- **The [converters](converters.md#converters) of the query serve it.**  This is for a column or parameter whose type has converters, and whose type a type override, a column type or a `-- param:` marker chose.  It has a converter that it can use, and all of these are true:
  - The converter is of the direction that the column or parameter needs ([SQLSRC280](../diagnostics.md#sqlsrc280)).
  - If the type is a type of yours that the driver never reads, the converter converts between it and the type that the driver reads ([SQLSRC279](../diagnostics.md#sqlsrc279)).
  - It is the only converter of that direction ([SQLSRC278](../diagnostics.md#sqlsrc278)).
  - Nothing is wrong with it ([SQLSRC277](../diagnostics.md#sqlsrc277)).
- **The project references the driver of the dialect of the query** ([SQLSRC273](../diagnostics.md#sqlsrc273)).  For PostgreSQL, the driver is [Npgsql 8.0 or later](databases/index.md#supported-versions) ([SQLSRC247](../diagnostics.md#sqlsrc247)).  For SQL Server, it is [Microsoft.Data.SqlClient 6.1 or later](databases/index.md#supported-versions) ([SQLSRC267](../diagnostics.md#sqlsrc267)).

If a query whose output is `repository` or `methods` fails one of the other conditions, it has an error.  What the error costs depends on the error:

- **The query gets neither its methods nor a constant until you fix the error.**  This holds for each error of the conditions, except SQLSRC299.
- **[SQLSRC299](../diagnostics.md#sqlsrc299) costs a repository query only its synchronous methods.**
- **The models of the query are still generated, if it has them.**
- **The other queries of the type still get their methods.**
- **A [placement error](member-placement.md#locations) leaves the whole file of the query without members, and without models for the type.**  An example is a visibility that does not fit the member location of the query ([SQLSRC123](../diagnostics.md#sqlsrc123)).
- **If a visibility does not fit only the models that the method takes or returns, the query loses its method alone.**  This is also SQLSRC123, and it costs what an error of the conditions costs.

Each type that claims a file gets its own methods from the file, by its own settings.  [Where the methods go](connection-methods.md#method-location) says when two such types give one method that takes a connection twice.

Some queries whose output is `repository` or `methods` cannot get a method.  Give each of them the output `sql`:

- **A query of a dialect that cannot be described is [SQLSRC209](../diagnostics.md#sqlsrc209).**  Only the queries of `postgres` or `mssql` have entries.  See [the output setting](settings.md#setting-output) and [databases that are not described](databases/other.md#not-described).
- **A statement that the tool cannot describe gets no method either**, such as PostgreSQL's `CALL` ([SQLSRC233](../diagnostics.md#sqlsrc233)).  So on PostgreSQL, no method calls a procedure.  Give such a query `-- output: sql`, and run its constant yourself.

[Synchronous methods](synchronous-methods.md) are a page of their own.

<a id="reading-rows"></a>

## How a row is read

- **The method reads each column by its position**, into the property that the output model has for the column.  So the name of the column does not matter.  A `!` or `?` at the end of an alias works as well as a [`-- column:` marker](model-properties.md#column-marker).
- **The method makes a record with its constructor, and a class by setting its properties.**  It makes a [sorted](model-properties.md#property-order) model as well as any other.
- **If a column is null and its property can be null, the property is null.**
- **If a property cannot be null, the method reads it with no check.**  So a null in its column throws, as the driver reports it:
  - Npgsql throws `InvalidCastException`, "Column 'name' is null.".
  - Microsoft.Data.SqlClient throws `SqlNullValueException`, "Data is Null.".
  - Microsoft.Data.SqlClient gives no error for a null `vector`.  So the method checks such a column itself, and throws `InvalidCastException`, "Column 'embedding' is null.", with the name of the column.
- **A null in a column that is never null throws, and is never read as a default value.**  A column is never null when its alias ends in `!`, when a `-- column:` marker says `not null`, or when the tool showed that it is never null.  The method reads such a column with no check too, so the first null that arrives throws, as above.
- **A JSON `null` is not a null.**  A JSON column that holds the text `null` is read as a value.  What the property gets depends on its C# type:
  - A `JsonElement` is an element of the kind `Null`.
  - A `JsonDocument` is a document whose root is such an element.
  - A `JsonNode`, a `JsonObject` or a `JsonArray` is null, if its property can be null.  If its property cannot be null, the method throws its own `InvalidCastException`, "Column 'metadata' holds a JSON null.", with the name of the column.
  - A class of yours, read through the serializer of System.Text.Json, is the same as a `JsonNode`.
  - A struct of yours, read through the serializer, is the same too: null if its property can be null, and the `InvalidCastException` of the method if it cannot.

  See JSON on [PostgreSQL](databases/postgresql/libraries.md#json) and [SQL Server](databases/sqlserver/libraries.md#json), and [a class in a JSON column](library-types-and-json.md#json-class).
- **The method reads each column as the C# type of its property, and sends each parameter as one, unless a converter converts it.**
- **If a [type override](type-mapping.md#setting-type-overrides), a [column type](model-properties.md#setting-column-types) or a `-- param:` marker names a C# type with no converter, the method gives that type to the driver as it is.**  That suits a type that the driver knows, or was taught.  For a type that the driver was never taught, the driver throws `InvalidCastException`: see [converters](converters.md#converters).
- **A class of yours in a column that holds JSON is read and sent through the serializer of System.Text.Json instead.**  See [a class in a JSON column](library-types-and-json.md#json-class).
- **The method does not give the driver a type that the query methods convert themselves**, whichever place chose it.  These are the types of NodaTime, NetTopologySuite and System.Text.Json on SQL Server, and `JsonNode`, `JsonObject` and `JsonArray` on Npgsql 8 and 9.  See [SqlSource's own conversions](converters.md#converters).
- **A column or parameter whose type a [library](library-types-and-json.md#setting-library-types) gives is read and sent by Npgsql on PostgreSQL, and by the query method itself on SQL Server.**  On PostgreSQL, the types of NodaTime and NetTopologySuite need their plugins on the data source of your application.  Without them, Npgsql throws `InvalidCastException`: see [what a method reads and throws](databases/postgresql/query-methods.md#method-results).  On SQL Server, see [NodaTime](databases/sqlserver/libraries.md#nodatime), [JSON](databases/sqlserver/libraries.md#json) and [NetTopologySuite](databases/sqlserver/libraries.md#nettopologysuite).
- **A [converter](converters.md#converters) of yours of the direction that a library type needs is used in place of the reading and sending by Npgsql or by the query method**, but for the types of NetTopologySuite on SQL Server.
- **If a type override or a column type chose the type of a column, and that type has a [converter](converters.md#converters), the converter gives the value of the property.**  The method reads the column as the type that the driver reads its database type as, and gives that value to the converter.
- **A null never reaches the converter.**  If the property cannot be null, the method throws as above, before it calls the converter.

<a id="method-parameters"></a>

## How a parameter is sent

- **The method sends each parameter by its name, without its `@`.**  It sends the value of the property of the input model for the parameter, in the order of the [parameter list](parameters.md#query-parameters) of the query.
- **Both drivers match a name ignoring case, as SqlSource does.**  So `@Id` and `@id` are one parameter, and one value fills both.  Under `mssql`, a second spelling is the warning [SQLSRC121](../diagnostics.md#sqlsrc121).
- **A parameter written in snake_case, `@user_id`, is filled from its property `UserId`**, like any other.
- **The method sends a null value as the `NULL` of the database.**  This holds for a property that can be null.  It also holds for a property of a reference type that cannot be null, as a caller can set it to null all the same.
- **The method sends a [list parameter](list-parameters.md#list-parameters) as one parameter for each value.**  Each is named after the list, and sent as a parameter of the type of the values.  The list is padded to one of a few sizes, unless the query has `list-padding=off`.
- **If a list is null, the method sends it as an empty list.**  If the query tests the list for null, the method also sends a parameter with the name of the list, which is null when the list is null.
- **A parameter that has a [converter](converters.md#converters) is sent as the value that the converter returns.**  This is for a parameter whose type a type override or its `-- param:` marker chose.  The method sends a null as `NULL`, without the converter.
- **A class of yours in a parameter that holds JSON goes through the serializer of System.Text.Json in the same way.**  The method sends its text: see [a class in a JSON column](library-types-and-json.md#json-class).
- **The method sends each parameter as the database type that it was described with**, or that its [`-- param:` marker](parameters.md#param-marker) declares.  It does not send it as a type chosen from the C# type of its value.  On SQL Server, it sends the length, precision and scale of the type too.  See how a parameter is sent on [PostgreSQL](databases/postgresql/query-methods.md#method-parameters) and on [SQL Server](databases/sqlserver/query-methods.md#method-parameters).

<a id="limits"></a>

## Limits

<a id="limit-wrapped-connection"></a>

### `InvalidCastException` from a method with parameters on a connection that wraps the driver's

**What you see.**  A query method with parameters can fail on a `DbConnection` that wraps the connection of the driver.  It fails before the query is sent:

- On PostgreSQL, it throws the exception of the wrapper, typically an `InvalidCastException`.
- On SQL Server, it throws `InvalidCastException`, "Unable to cast object of type '...' to type 'Microsoft.Data.SqlClient.SqlParameter'.".

**Why.**  A query method with parameters works only on a connection whose commands take its driver's own parameters.  The commands of such a wrapper create or accept only parameters of their own type.  The method works on the connections of the drivers, such as `NpgsqlConnection`, the one that an `NpgsqlDataSource` gives, and `SqlConnection`.  So it works on the connections that a repository makes from a connection string or a data source.

**What to do.**  Do one of these:

- Give the method the connection of the driver that the wrapper holds.  For a repository, give that connection from its factory method, or as the connection that it borrows.
- Give the query the output `models`, and send its parameters through the wrapper yourself.
