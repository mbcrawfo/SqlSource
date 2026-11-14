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

A query whose [output](../settings.md#setting-output) is `methods` gets a method that takes the connection to run on, for code that manages its connections, its transactions and its timeouts itself.  Above, a `public` type `App.UserRepository` claims `UserRepository.sql`.  The method takes the connection and the query's input model, opens the connection when it is closed, sends each parameter, runs the query and reads its row into the output model, and leaves a connection that was open as it was.

<a id="method-location"></a>

## Where the methods go

A `methods` method goes where its query's [member location](members.md#setting-member-location) says, with its [visibility](members.md#setting-visibility).  Without either, it is an `internal` extension method of `DbConnection`, called on the connection as `connection.GetUser(...)`, in a generated `static partial class` named after the type with `Extensions` after it, `UserRepositoryExtensions` for `UserRepository`.

- **The extension class is in the type's namespace**, not nested in the types around the type, and is named after the type's own name alone: `Outer.Queries<T>` gives `QueriesExtensions`.  Code in another namespace calls the methods with a `using` of that namespace.
- **It is `public` when one of the type's methods in it is**, as `-- visibility: public` asks and a type that is `public` with every type around it allows, and `internal` otherwise.  Each method keeps its own visibility in it.
- **Two types whose classes have one name in one namespace share it**, as two types `Queries` nested in two types of one namespace do, when the class is `public` for both or `internal` for both.  When it is not, or when a type of your project or a model has the class's full name, the type gets none of its methods there: [SQLSRC276](../diagnostics.md#sqlsrc276).
- **A method there is not named like a member of `DbConnection` or `object`**, such as `Open` or `ToString`, which a call on a connection would find instead, nor like the class itself ([SQLSRC274](../diagnostics.md#sqlsrc274)).

Under `class`, the methods are static methods of the type itself, which take the connection as their first argument: `UserRepository.GetUser(connection, ...)`.  A method is then not named like a member of the type, the type itself or one of its type parameters, nor like a constant or token method that another query puts on the type, nor like a nested class of the type's queries ([SQLSRC274](../diagnostics.md#sqlsrc274)).

Under `nested-class`, the methods are public static methods of the nested class, which take the connection as their first argument, since a nested class cannot hold extension methods: `Sql.GetUser(connection, ...)` in the type's code, and `UserRepository.Sql.GetUser(connection, ...)` outside it when the class is `public` or `internal`.  A method is then not named like a constant or token method that another query of the type, in any of its files, puts in that class ([SQLSRC274](../diagnostics.md#sqlsrc274)), nor like the class ([SQLSRC009](../diagnostics.md#sqlsrc009)).

Two methods of one name in one class, as those of a query `Get` under `async-method-suffix` and a query `GetAsync` without it, or of a query `Get` under `method-type=both` and a query `GetAsync`, are SQLSRC274, and so are two methods of one name and one list of arguments in two extension classes of one namespace, which a call could not tell apart.  A type's methods, wherever they go, are in a file of their own among the generated files, named after the type's full name, `App.UserRepository-Methods.g.cs`.

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

The method has the name of the query, with `Async` after it under the [generator parameter](../settings.md#setting-generator-parameters) `async-method-suffix`: `ListFromAsync`.  A [synchronous method](query-methods.md#synchronous-methods) has the query's name alone, and beside one, under `method-type=both`, the asynchronous method always has `Async` after it.  Its arguments, in this order:

| Argument | What it is |
|----|----|
| `connection` | The connection to run on, after `this` for an extension method alone: see [the connection](#method-connection) |
| `parameters` | The query's input model, when it has parameters.  It is not in the method of a query without parameters |
| A `string` for each [token](tokens.md#tokens) | The text that replaces the token, named as the token, in the order the tokens first appear.  It is checked as a token method checks it: by default null, empty or blank text throws, and `token-validation=off` turns the check off |
| `transaction` | The transaction to run the query in, or null |
| `commandTimeout` | The command's timeout in seconds, or null for the driver's |
| `cancellationToken` | Cancels opening the connection, running the query and reading its rows.  What each driver throws then is on the pages of [PostgreSQL](databases/postgresql/query-methods.md#method-results) and [SQL Server](databases/sqlserver/query-methods.md#method-results) |

The method of a query with tokens raises no CA2100 ("Review SQL queries for security vulnerabilities") in your build, though it runs SQL made with their text: that text is your own code's choice, never a user's, as [tokens](tokens.md#tokens) says.  Nor does the method of a query with a [list parameter](parameters.md#list-parameters), whose SQL is the query's own with a parameter for each value.

A null `connection` or `parameters` throws `ArgumentNullException`.  A method with a list parameter throws `ArgumentException` when its query would send more parameters than its database takes, and `InvalidOperationException` when a list changed after the method counted it, as [list parameters](parameters.md#list-limit) say.  The names of the arguments a query method can have, `connection`, `parameters`, `transaction`, `commandTimeout` and `cancellationToken`, are not names a token can have ([SQLSRC275](../diagnostics.md#sqlsrc275)).  What the method returns depends on the query's result shape, as [what each shape returns](return-types.md#method-shapes) says.

<a id="method-connection"></a>

## The connection

The method opens the connection when its `State` is `Closed`, and closes it again before it returns or throws.  A method that [streams its rows](return-types.md#streaming-rows) opens it when the loop asks for the first row, and closes it again when the loop ends, and the connection is busy with the query until then, so the loop's body cannot run another command on it, on PostgreSQL or on a SQL Server connection without `MultipleActiveResultSets`.  A connection that was not closed is left as it was: it stays open, in the transaction and with the session's settings it had, unless, on SQL Server, an error that the method does not report rolls the transaction back, as [what a method reads and throws](databases/sqlserver/query-methods.md#method-results) says.  So to run several queries on one open connection, or in a transaction, open the connection yourself.

**Give the method the transaction.**  The method gives its command the `transaction` it is given, or none when it is given null.  On SQL Server, Microsoft.Data.SqlClient refuses a command on a connection that has a transaction the command was not given, and throws `InvalidOperationException` ("... requires the command to have a transaction when the connection assigned to the command is in a pending local transaction ...") before anything is sent.  Npgsql runs such a command in the connection's transaction.  A SQL Server method given a Microsoft.Data.SqlClient transaction that has completed, committed or rolled back, throws `InvalidOperationException` and runs nothing, where Microsoft.Data.SqlClient would run its command outside any transaction: see [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).  Pass the transaction to every method you call inside one, on both: see [the recipe](../recipes.md#queries-in-a-transaction).
