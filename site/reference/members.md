# What is generated

<a id="generator"></a>

## What the generator writes

```sql postgres
-- name: GetUser -> one-optional
-- summary: Loads one user by id.
SELECT id, name
FROM users
WHERE id = @id;

-- name: ListFrom
-- summary: Lists the rows of one table.
SELECT id, name FROM {{table:users}} ORDER BY {{orderBy:name}};
```

```csharp
using Npgsql;
using SqlSource;

[SqlSourceGenerate]
public partial class UserRepository(NpgsqlDataSource dataSource);

// elsewhere
var users = new UserRepository(dataSource);
GetUserDto? user = await users.GetUser(new GetUserParams(42));
ListFromDto[] rows = await users.ListFrom("users", "name DESC");
```

The generator is the part of the `SqlSource` package that runs in the compiler.  It reads the `.sql` files of your project and writes C# from them, beside your own code.  It needs no database.

For each described query, the generator writes its [models](models.md#models): a type for its parameters and a type for its rows.  The [output](settings.md#setting-output) of the query says what else it gets:

- **`repository`, the default:** a [repository method](repositories.md#repository-methods).  This is an instance method of your type that gets its own connection and runs the query.
- **`methods`:** a [method that takes the connection](connection-methods.md#methods-that-take-a-connection), for a connection, a transaction or a timeout that your own code manages.
- **`models` or `sql`:** a string constant, or a method that returns its SQL where it has [tokens](tokens.md#tokens), for your own code to run.

The generator needs the package, one attribute and one choice, the output of each query.  The [tool](describing.md#describe) must describe each query whose output is `repository`, `methods` or `models`.  The build reports such a query when its sidecar has no current entry for it.  A query whose output is `sql` needs nothing more.

The project must target .NET 8 or later and compile C# 12 or later, as [what the build needs](msbuild.md#build-requirements) says.  An older target framework is [SQLSRC003](../diagnostics.md#sqlsrc003), and an older language version is [SQLSRC012](../diagnostics.md#sqlsrc012).

In the sample, `GetUser` and `ListFrom` are the generated methods of the two queries.  They are public instance methods of `UserRepository`.  Each method opens a connection from the data source, sends the parameters of its query and runs it.  It then reads the rows into its output model, and disposes the connection.  `GetUser` reads one row or null, as its [result shape](sql-files.md#result-shapes) `one-optional` says.  `ListFrom` reads every row, the default for a query that returns rows.

`ListFrom` takes the text of each token.  Its token defaults, `users` and `name`, are only samples for [describing](describing.md#describe) the query.  `GetUserParams`, `GetUserDto` and `ListFromDto` are the models of the two queries.  The generator makes them from what the tool recorded when it described the queries.  `UserRepository` also gets `IUserRepository`, an interface of the two methods, for code that takes a repository and for a test that fakes one: see [the interface](repositories.md#repository-interface).

If a query is renamed or removed, the code that uses it does not compile.  If a column is renamed or removed, the same happens once the query is described again.

Under the output `models`, the same queries give their models and two members, for Dapper, ADO.NET or any other library to run: see [the recipe](../guides/dapper.md#dapper-and-adonet).  `Sql.GetUser` is a [constant](#constants) that holds the text of the first query.  `Sql.ListFrom("users", "name DESC")` is a [token method](#token-methods) that returns the text of the second with its tokens filled in.

[Where the members go](member-placement.md) is a page of its own: the member location, the visibility and the name of the nested class.

<a id="constants"></a>

## Constants

```csharp
public const string GetUser = "SELECT id, name\nFROM users\nWHERE id = @id;";
```

If a query has no tokens and its output is `models` or `sql`, it becomes a `const string` with the name of the query.  The constant is `public` in a nested class, and has the query's [visibility](member-placement.md#setting-visibility) on the type: see [where the members go](member-placement.md#locations).

Its value is the SQL that you wrote, with the markers and comments taken out and the line endings made `\n`.  [What reaches the generated SQL](sql-files.md#generated-sql) lists everything done to it.  A query parameter such as `@id` stays in the value as written.  You give the database its value when you run the query.

You can use the constant wherever C# needs a constant, such as in another `const` or in an attribute argument:

```csharp
public const string LoadUser = UserQueries.GetUser;

[Description(UserQueries.GetUser)]
public User Load(int id) => throw new NotImplementedException();
```

<a id="token-methods"></a>

## Token methods

```csharp
var sql = Sql.ListFrom("users", "name DESC");
// SELECT id, name FROM users ORDER BY name DESC;
```

A token method is what a query with a [token](tokens.md#tokens) becomes in place of a constant, when its output is `models` or `sql`.  It is a `static string` method with the name of the query, such as `ListFrom(string table, string orderBy)`.  It takes the text of each token, and returns the SQL with the tokens filled in.  It is as visible as a constant would be.

Under `repository` or `methods`, the query gets no token method: its [method](query-methods.md#query-methods) takes the tokens instead, and runs the query.  The parameters of a token method, and what it returns, are under [tokens](tokens.md#tokens).

A token is replaced by plain text substitution, and nothing is escaped.  **Pass only text that your own code chooses**, such as a table name from a fixed list, and a [query parameter](parameters.md#query-parameters) for every value.  By default, the method throws for a null, empty or blank argument.  The rules for tokens, their defaults and that check are under [tokens](tokens.md#tokens).

<a id="intellisense"></a>

## Documentation in IntelliSense

Every generated member has a documentation comment, so your editor shows the comment where the member is used.  For `GetUser`, the comment is:

```csharp
/// <summary>
/// Loads one user by id.
/// </summary>
/// <remarks>
/// <code>
/// SELECT id, name
/// FROM users
/// WHERE id = @id;
/// </code>
/// </remarks>
public const string GetUser = "SELECT id, name\nFROM users\nWHERE id = @id;";
```

The summary is the query's `-- summary:` ([summaries](sql-files.md#summaries)).  If the query has none, the summary is "The `GetUser` query from `UserRepository.sql`."  The remarks hold the SQL of the constant, so a reader sees what a query does without opening the `.sql` file.

A token method adds a line for each token and one for the result.  It shows its tokens as `{{table}}`, without their defaults:

```csharp
/// <summary>
/// Lists the rows of one table.
/// </summary>
/// <remarks>
/// <code>
/// SELECT id, name FROM {{table}} ORDER BY {{orderBy}};
/// </code>
/// </remarks>
/// <param name="table">The text that replaces <c>{{table}}</c>.</param>
/// <param name="orderBy">The text that replaces <c>{{orderBy}}</c>.</param>
/// <returns>The SQL with each token replaced by its argument.</returns>
public static string ListFrom(string table, string orderBy)
```

A [query method](query-methods.md#query-methods) has the same summary and remarks.  It also has these lines:

- a line for each of its arguments;
- a line for what it returns, but not for a [synchronous method](synchronous-methods.md#synchronous-methods) of the shape `none`, which returns nothing;
- for the shapes `one` and `one-optional`, a line for the [exception](exceptions.md#unexpected-row-count) that it throws;
- on SQL Server, a line for the `InvalidOperationException` that it throws when it is given a transaction that has [completed](databases/sqlserver/query-methods.md#method-results);
- on SQL Server, for the shapes `one`, `one-optional` and `many`, a line for [`TransactionRolledBackException`](exceptions.md#transaction-rolled-back).

A [collection method](collection-methods.md#collection-methods) has a line for [`CollectionItemException`](exceptions.md#collection-item-exception), and one for `UnexpectedRowCountException` under `one` and `one-optional`.  On SQL Server, a collection method also has the two SQL Server lines of a query method.  They are the line for the completed transaction's `InvalidOperationException`, and the line for `TransactionRolledBackException` under `one`, `one-optional` and `many`.  On SQL Server, a collection method that reads rows also has a line for the `InvalidOperationException` of a query that returns [more than one result set](databases/sqlserver/query-methods.md#collection-methods).

The extension class of the methods has a summary that names the type.  So does a nested class that is `public` or `protected`.

If a project treats a missing comment on a public member (CS1591) as an error, the generated code gives it none, since every generated member is documented.
