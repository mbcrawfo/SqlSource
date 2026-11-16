# Methods that take a connection (transaction, timeout)

<a id="methods-that-take-a-connection"></a>

## Methods that take a connection

```sql postgres
-- output: methods

-- name: GetUser -> one-optional
-- summary: Loads one user by id.
SELECT id, name, deleted_at
FROM users
WHERE id = @id;
```

```csharp
// Generated, shown without global::
namespace App
{
    internal static partial class UserRepositoryExtensions
    {
        internal static async Task<GetUserDto?> GetUser(
            this DbConnection connection,
            GetUserParams parameters,
            DbTransaction? transaction = null,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default)
    }
}

// Your code
GetUserDto? user = await connection.GetUser(new GetUserParams(42));
```

A query whose [output](settings.md#setting-output) is `methods` gets a method that takes the connection to run on.  The output `methods` is for your own code structures, above all a transaction or a timeout that your code controls.  Your code manages its connections, its transactions and its timeouts itself, and gives each to the method.  In the sample, a `public` type `App.UserRepository` claims `UserRepository.sql`.

The method takes the connection and the input model of the query.  Then it does these things, in this order:

1. It opens the connection, if the connection is closed.
2. It sends each parameter.
3. It runs the query.
4. It reads the row into the output model.

If the connection was open, the method leaves it as it was.

<a id="method-location"></a>

## Where the methods go

By default, the method `GetUser` of `UserRepository` is an `internal` extension method of `DbConnection`, in the class `UserRepositoryExtensions`.  You call it on the connection: `connection.GetUser(...)`.

A method that takes a connection goes where the [member location](members.md#setting-member-location) of its query says.  It has the [visibility](members.md#setting-visibility) of its query.  If the query sets neither, the method is an `internal` extension method of `DbConnection`.  It is in a generated `static partial class` with the name of the type and `Extensions` after it.

- **The extension class is in the namespace of the type.**  It is not nested in the types around the type.  Code in another namespace calls the methods with a `using` of that namespace.
- **The extension class is named after the name of the type alone.**  `Outer.Queries<T>` gives `QueriesExtensions`.
- **The extension class is `public` when one of the methods of the type in it is `public`.**  A method is `public` when `-- visibility: public` asks for it, and the type and every type around it are `public`.  Otherwise the class is `internal`.  Each method keeps its own visibility in the class.
- **Two types share one extension class when their classes have one name in one namespace.**  An example is two types `Queries`, nested in two types of one namespace.  They share it only when the class is `public` for both or `internal` for both.
- **A type gets none of its methods in an extension class that cannot be written** ([SQLSRC276](../diagnostics.md#sqlsrc276)).  This is so when two types whose classes have one name in one namespace give the class two visibilities.  It is also so when a type of your project, or a model, has the full name of the class.
- **A method in the extension class cannot have the name of a member of `DbConnection` or `object`** ([SQLSRC274](../diagnostics.md#sqlsrc274)).  A call on a connection would find a member such as `Open` or `ToString` instead of the method.
- **A method in the extension class cannot have the name of the class itself** ([SQLSRC274](../diagnostics.md#sqlsrc274)).

Under the member location `class`, each method is a static method of the type itself.  It takes the connection as its first argument: `UserRepository.GetUser(connection, ...)`.  The method cannot have the name of one of these ([SQLSRC274](../diagnostics.md#sqlsrc274)):

- a member of the type, the type itself, or one of its type parameters;
- a constant or a token method that another query puts on the type;
- a nested class of the queries of the type.

Under the member location `nested-class`, each method is a public static method of the nested class.  A nested class cannot hold extension methods, so the method takes the connection as its first argument.  In the code of the type, the call is `Sql.GetUser(connection, ...)`.  When the class is `public` or `internal`, code outside the type calls `UserRepository.Sql.GetUser(connection, ...)`.  The method cannot have the name of one of these:

- a constant or a token method that another query of the type, in any of its files, puts in that class ([SQLSRC274](../diagnostics.md#sqlsrc274));
- the class ([SQLSRC009](../diagnostics.md#sqlsrc009)).

Two methods are [SQLSRC274](../diagnostics.md#sqlsrc274) in one of these cases:

- They have one name in one class.  Examples are a query `Get` under `async-method-suffix` and a query `GetAsync` without it, or a query `Get` under `method-type=both` and a query `GetAsync`.
- They have one name and one list of arguments, in two extension classes of one namespace.  A call could not tell them apart.

Wherever the methods of a type go, they are in a generated file of their own.  The file is named after the full name of the type: `App.UserRepository-Methods.g.cs`.

<a id="method-signature"></a>

## The signature

```csharp
internal static async Task<ListFromDto[]> ListFrom(
    this DbConnection connection,
    string table,
    string orderBy,
    DbTransaction? transaction = null,
    int? commandTimeout = null,
    CancellationToken cancellationToken = default)
```

A method that takes a connection has the name of its query: `ListFrom`.  Under the [generator parameter](generator-parameters.md#setting-generator-parameters) `async-method-suffix`, the name has `Async` after it: `ListFromAsync`.  A [synchronous method](query-methods.md#synchronous-methods) has the name of the query alone.  Under `method-type=both`, the asynchronous method beside it always has `Async` after its name.  The method takes these arguments, in this order:

| Argument | What it is |
|----|----|
| `connection` | The connection to run on, after `this` for an extension method alone: see [the connection](#method-connection) |
| `parameters` | The query's input model, when it has parameters.  It is not in the method of a query without parameters |
| A `string` for each [token](tokens.md#tokens) | The text that replaces the token, named as the token, in the order the tokens first appear.  It is checked as a token method checks it: by default null, empty or blank text throws, and `token-validation=off` turns the check off |
| `transaction` | The transaction to run the query in, or null |
| `commandTimeout` | The command's timeout in seconds, or null for the driver's |
| `cancellationToken` | Cancels opening the connection, running the query and reading its rows.  What each driver throws then is on the pages of [PostgreSQL](databases/postgresql/query-methods.md#method-results) and [SQL Server](databases/sqlserver/query-methods.md#method-results) |

- **The method of a query with tokens raises no CA2100 ("Review SQL queries for security vulnerabilities") in your build.**  The method runs SQL made with the text of the tokens.  That text is the choice of your own code, never of a user, as [tokens](tokens.md#tokens) says.
- **The method of a query with a [list parameter](parameters.md#list-parameters) raises no CA2100 either.**  Its SQL is the SQL of the query, with a parameter for each value.
- **If `connection` or `parameters` is null, the method throws `ArgumentNullException`.**
- **If its query would send more parameters than its database takes, a method with a list parameter throws `ArgumentException`.**  See [list parameters](parameters.md#list-limit).
- **If a list changed after the method counted it, a method with a list parameter throws `InvalidOperationException`.**  See [list parameters](parameters.md#list-limit).
- **A token cannot have the name of an argument that a query method can have** ([SQLSRC275](../diagnostics.md#sqlsrc275)).  These names are `connection`, `parameters`, `transaction`, `commandTimeout` and `cancellationToken`.

What the method returns depends on the result shape of the query: see [what each shape returns](return-types.md#method-shapes).

<a id="method-connection"></a>

## The connection

A method that takes a connection opens the connection only when it is closed.  To run several queries on one open connection, or in a transaction, open the connection yourself.

- **If the `State` of the connection is `Closed`, the method opens the connection, and closes it again.**  It closes the connection before it returns or throws.
- **If the connection was not closed, the method leaves it as it was.**  It stays open, in its transaction and with its session settings.  On SQL Server, an error that the method does not report can still roll the transaction back: see [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).
- **A method that [streams its rows](return-types.md#streaming-rows) opens a closed connection when the loop asks for the first row.**  It closes the connection again when the loop ends.
- **While the loop of a streaming method runs, the connection is busy with the query.**  So the body of the loop cannot run another command on it.  This is so on PostgreSQL, and on a SQL Server connection without `MultipleActiveResultSets`.

**Give the method the transaction.**  Pass the transaction to every method that you call inside it, on both databases: see [the recipe](../start/transactions.md#queries-in-a-transaction).  The method gives its command the `transaction` it is given.  If it is given null, the command gets no transaction.

If a command was not given the transaction of its connection, each driver does a different thing:

- **On SQL Server, Microsoft.Data.SqlClient refuses the command.**  It throws `InvalidOperationException` ("... requires the command to have a transaction when the connection assigned to the command is in a pending local transaction ...") before anything is sent.
- **On PostgreSQL, Npgsql runs the command in the transaction of the connection.**

On SQL Server, a completed transaction is one that was committed or rolled back.  If a method is given a completed Microsoft.Data.SqlClient transaction, it throws `InvalidOperationException` and runs nothing.  Microsoft.Data.SqlClient alone would run the command outside any transaction.  See [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).
