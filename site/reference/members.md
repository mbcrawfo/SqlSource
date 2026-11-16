# What is generated and where it goes

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

<a id="locations"></a>

<a id="setting-member-location"></a>

## Where the members go

```sql postgres
-- output: methods

-- name: GetUser -> one-optional
-- member-location: class
-- visibility: public
SELECT id, name FROM users WHERE id = @id;

-- name: CountUsers
-- output: sql
SELECT count(*) FROM users;
```

```csharp
[SqlSourceGenerate]
public static partial class UserQueries
{
    public static string CountText => Sql.CountUsers;
}

// elsewhere
GetUserDto? user = await UserQueries.GetUser(connection, new GetUserParams(42));
```

The member location of a query says where its generated members go.  Those are its [query method](query-methods.md#query-methods), for the output `repository` or `methods`, and its constant or token method, for `models` or `sql`.  The [visibility](#setting-visibility) of the query says how visible the members are.  Both are settings of the query's `.sql` file.

Above, `GetUser` is a public static method of `UserQueries` that takes the connection.  `CountUsers` writes no member location, so it is a constant of the private class `Sql` nested in `UserQueries`.

| Member location | The members are | Used as |
|----|----|----|
| `class`, the default for `repository` | Members of the type itself: a [repository method](repositories.md#repository-methods) is an instance method, which gets its own connection, and any other member is static, a `methods` method taking the connection as its first argument | `users.GetUser(...)` for a repository method, `UserQueries.GetUser`, or `UserQueries.GetUser(connection, ...)` for a `methods` method |
| `extension-class`, the default for `methods` | Extension methods of `DbConnection`, in a static class beside the type, named after it with `Extensions` after it.  Only a `methods` method can go there | `connection.GetUser(...)` |
| `nested-class`, the default for `models` and `sql` | Public static members of a static class nested in the type, `Sql` unless [the nested class](#setting-nested-class) names it.  A `methods` method there takes the connection as its first argument | `Sql.GetUser`, or `Sql.GetUser(connection, ...)` for a `methods` method |

Without a member location, the members of each output go to its default, as the table shows.  The setting is written `-- member-location:`, in the file header or inside a query.  In a settings file it is `memberLocation`, as `"memberLocation": "class"`.

- **Each query has its own.**  A file can put the members of some queries on the type, some in a nested class and some in the extension class.  Two files of one type can place theirs differently.
- **A value in the file header or in a settings file reaches every query below it, whatever its output.**  [A placement for a whole file or folder](#placement-for-a-file) says what to do where it does not fit.
- **A repository method is always on the type.**  `class` fits a query whose output is `repository`.  On such a query, `extension-class` or `nested-class` is [SQLSRC122](../diagnostics.md#sqlsrc122).
- **Only a `methods` method can go in the extension class.**  On a query whose output is `repository`, `models` or `sql`, `extension-class` is [SQLSRC122](../diagnostics.md#sqlsrc122).  A constant extends nothing, and a repository method is always on the type.

```sql postgres
-- name: GetUser -> one-optional
SELECT id, name FROM users WHERE id = @id;

-- name: CountUsers
-- output: sql
SELECT count(*) FROM users;

-- name: ListUsers
-- output: methods
-- member-location: class
SELECT id, name FROM users ORDER BY name;
```

```csharp
[SqlSourceGenerate]
public partial class UserRepository(NpgsqlDataSource dataSource)
{
    public string CountText => Sql.CountUsers;
}

// elsewhere
GetUserDto? user = await users.GetUser(new GetUserParams(42));
ListUsersDto[] all = await UserRepository.ListUsers(connection);
```

In this sample, `GetUser` and `CountUsers` write no member location and no visibility, so they take the defaults.  `GetUser` is a repository method, a `public` instance method of the type.  `CountUsers` is a constant, and a constant or token method is a public member of a private class `Sql` nested in the type, for the type's own code.  A query whose output is `methods` is by default an `internal` extension method of `DbConnection`, in the class `UserRepositoryExtensions`.  `ListUsers` writes `class`, so it is a static method of the type that takes the connection.  It is `public`, since the type is.

- **The models follow the type, and the members follow `visibility`.**  A model is `public` or `internal` by the type that claims its query, whatever the visibility of the query's members: see [accessibility](models.md#model-accessibility).  So a `private` method of a `public` type still has public models.
- **A `public` extension method of a type that is not `public` is [SQLSRC123](../diagnostics.md#sqlsrc123) when it takes or returns a model that is internal.**  The models of such a type are internal, unless a `public` type shares them.  A method with no parameters and no rows takes no models, and compiles.
- **A nested class is `static partial`, and is written for the members that it holds alone.**  A class that holds only query methods is in the file of the type's methods.  A class that holds constants and methods has a part in each file.
- **The members of a nested class are `public`, and the class has the visibility.**
- **A type with no query that it can use keeps an empty private class `Sql`.**  An example is a type whose only file has an error.  If the settings files of its first file set a `nestedClass`, the empty class has that name.  So code that uses one of its members reports the member missing, and not the class.
- **In a nested class, the type cannot have a member, a nested type or a type parameter with the name of the class.**  The type cannot have that name itself either.  Each of these is [SQLSRC007](../diagnostics.md#sqlsrc007).
- **A query cannot have the name of its nested class** ([SQLSRC009](../diagnostics.md#sqlsrc009)).  Under `async-method-suffix`, this holds for the name of a query method with `Async` after it.
- **On the type, a constant or token method cannot have the name of the type** ([SQLSRC009](../diagnostics.md#sqlsrc009)).
- **On the type, a constant or token method cannot have the name of anything that the type declares, or of a nested class of its queries** ([SQLSRC125](../diagnostics.md#sqlsrc125)).  What the type declares is a member, a nested type or a type parameter.  So a token method does not overload a method of yours of the same name.
- **The name of a query method is checked where its output says.**  For a repository method, see [its output's section](repositories.md#repository-signature).  For a `methods` method, see [where the methods go](connection-methods.md#method-location).
- **A placement error leaves its file out.**  The file then has no members and no models for the type, as with [a name used twice](attributes.md#files-of-a-type), and the type's other files keep theirs.  A placement error is one of these:
  - a member location or a visibility that does not fit the query ([SQLSRC122](../diagnostics.md#sqlsrc122), [SQLSRC123](../diagnostics.md#sqlsrc123));
  - a member that would take a name that is taken ([SQLSRC007](../diagnostics.md#sqlsrc007), [SQLSRC125](../diagnostics.md#sqlsrc125));
  - a nested class with two visibilities ([SQLSRC124](../diagnostics.md#sqlsrc124)).
- **A visibility that does not fit only the models of a query method leaves out that method alone.**  This is also SQLSRC123, and it acts as the [other errors of a method](query-methods.md#query-methods) do.
- **A query can have the name of a member that the type inherits, such as `ToString`.**  The constant hides the inherited member, and the generated code gives no warning.

<a id="placement-for-a-file"></a>

### A placement for a whole file or folder

A member location or a visibility in a file header or a settings file reaches every query below it, whatever its output.  If the value does not fit a query, that is an error.  The error leaves the file without members and without models for the type until it is fixed.  If only the models of the query's method do not fit, the error leaves out only that method.  The message says where the value was written.  Two cases catch a project out:

- **`"memberLocation": "ExtensionClass"` in a settings file is [SQLSRC122](../diagnostics.md#sqlsrc122) at every query whose output is `repository`, `models` or `sql`.**  This holds in its folder and the folders below it, since only a `methods` method can go in the extension class.  In the same way, `"memberLocation": "NestedClass"` is [SQLSRC122](../diagnostics.md#sqlsrc122) at every repository query, whose method is always an instance method of the type.  Write `-- member-location:` on the queries that the value does not fit, or leave it out: each output's members then go to its own default.
- **A visibility in the header of a file that mixes outputs reaches its query methods too:**

```sql postgres
-- visibility: private
-- output: methods

-- name: CountUsers
-- output: sql
SELECT count(*) FROM users;

-- name: GetUser -> one-optional
SELECT id, name FROM users WHERE id = @id;
```

```text
error SQLSRC123: The query 'GetUser' of 'App.UserRepository' cannot be private, which the file header sets, at the member location extension-class, because an extension method is public or internal.  Write -- visibility: or -- member-location: on the query.
```

`CountUsers` would be a constant of a private nested class, its default anyway.  `GetUser` cannot be private, since its method goes to the extension class.  To fix the file, do one of these:

- Write the visibility on the query that needs another.  It wins over the header.
- Give the query a member location where the visibility fits.

The file below takes the first way:

```sql postgres
-- visibility: private
-- output: methods

-- name: CountUsers
-- output: sql
SELECT count(*) FROM users;

-- name: GetUser -> one-optional
-- visibility: internal
SELECT id, name FROM users WHERE id = @id;
```

<a id="setting-visibility"></a>

## Visibility

```sql postgres
-- visibility: internal

-- name: GetUser -> one-optional
SELECT id, name FROM users WHERE id = @id;
```

The visibility of a query says how visible its members are at their member location.  Above, `GetUser` is an `internal` repository method of the type, and its interface leaves it out.

The values are `public`, `internal`, `protected` and `private`.  What a value sets, and the default, depend on the member location:

| Member location | `visibility` sets | Default |
|----|----|----|
| `class` | Each constant, token method and query method on the type | `public` for a repository method.  Otherwise `public` when the type and every type around it are `public`, and `internal` otherwise |
| `nested-class` | The nested class's own accessibility.  Its members are always `public` inside it | `private` |
| `extension-class` | Each query method's accessibility.  The class is `public` when one of the type's methods in it is, and `internal` otherwise | `internal` |

Without a visibility, a repository method is `public` whatever the accessibility of the type, unless `public` does not fit its models, as below.  Any other query takes the default of its member location.  The setting is written `-- visibility:`, in the file header or inside a query, and `visibility` in a settings file.

- **A value in the file header or in a settings file reaches every query below it**, as a member location does.  Where the value does not fit a query, it is [SQLSRC123](../diagnostics.md#sqlsrc123), and [A placement for a whole file or folder](#placement-for-a-file) says what the error leaves out.
- **`-- visibility: private` in the header of a file of `methods` and `sql` queries is an error at its query methods.**  It gives the constants a private nested class, but the query methods go to the extension class.  Write `-- visibility:` or `-- member-location:` on those queries, as [a placement for a whole file or folder](#placement-for-a-file) shows.
- **A nested class has one visibility.**  If a query writes no visibility for its class, it takes the one that another query of the type writes for the class.  That query can be in any of the type's files.
- **Two queries that write two different visibilities for one nested class are [SQLSRC124](../diagnostics.md#sqlsrc124).**  Every file with a query in that class then gets no members and no models for the type until it is fixed.
- **A query method that writes no visibility for its nested class can still be [SQLSRC123](../diagnostics.md#sqlsrc123).**  This happens when the visibility that it takes does not fit the models that it takes or returns, and it leaves out that method alone.
- **The models do not follow the visibility.**  The models of a query are `public` or `internal` by the type that claims it, whatever the visibility of its members: see [accessibility](models.md#model-accessibility).

**What does not fit.**  A visibility does not fit when one of these is true:

- **`private` or `protected` in the extension class.**  A class that is not nested cannot be private or protected.
- **`public` in the extension class, for a query method, when all of these are true:**
  - The method takes or returns a model that is internal.
  - The type, or a type around it, is not `public`.

  The models of such a type are internal, and the method cannot take or return them.
- **`public` or `protected` for a query method, when all of these are true:**
  - The visibility is for a method on the type, or for the nested class that holds the method.
  - The method takes or returns a model that is internal.
  - The type, or a type around it, is `protected` or `protected internal`.
  - Neither the type nor any type around it is `internal`, `private` or `private protected`.

  A class in another assembly can derive from the type and call the method, but the models are internal.
- **`protected` where C# forbids it.**  That is one of these:
  - on the type, when it is a static class, a struct or a record struct;
  - for the nested class of a struct or a record struct.

**What fits, with no diagnostic.**  Each of these fits, and the build reports nothing, unless a case of "What does not fit" above applies to it:

- **`public` for the members on a type that is `internal`.**  They are then internal in effect, as a repository method is by default.
- **`protected` for the repository methods of an abstract base repository.**  Its derived types call them.
- **`public` or `internal` for a nested class.**  Code outside the type then uses it as `UserRepository.Sql.GetUser`.
- **`protected` on a sealed class or record.**  It compiles with no warning from the generated code.
- **`protected` for a nested class in a static class.**
- **`internal` or `private` for a query method or a nested class on a type that is `protected` or `protected internal`, or inside one.**
- **Any visibility for constants and token methods on such a type.**  They take no models.

A query method with no parameters and no rows takes no models either, so it fits wherever the visibility alone does.  So does a query method whose models a `public` type shares, which makes them public.

**The default of a repository method does not always fit.**  The default `public` does not fit a repository method that takes or returns a model when all of these are true:

- The type, or a type around it, is `protected` or `protected internal`.
- Neither the type nor any type around it is `internal`, `private` or `private protected`.

This is [SQLSRC123](../diagnostics.md#sqlsrc123), which leaves out that method alone.  Write `-- visibility: internal` on the query, or in the file header for all of its queries.

<a id="setting-nested-class"></a>

## Name of the nested class

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "nestedClass": "Queries",
}
```

```sql postgres
-- nested-class: Statements
-- output: models

-- name: GetUser
SELECT id, name FROM users WHERE id = @id;
```

```csharp
// UserRepository.cs, beside the UserRepository.sql above
[SqlSourceGenerate]
public partial class UserRepository
{
    public string Get() => Statements.GetUser;
}
```

The nested class names the class, nested in the type, that holds the members of a query whose [member location](#setting-member-location) is `nested-class`.  Above, the queries of the project are in a class `Queries`, and those of `UserRepository.sql` in `Statements`.

- **The value must be one C# identifier that is not a keyword.**  `Queries` is valid, and `class`, `My-Sql` or `App.Sql` is not.
- **`file`, `required`, `scoped`, `extension` and `var` are not valid either.**  C# does not take the first four as the name of a class.  With `var`, the query methods of the type would not compile.
- **A name in lower case, such as `queries`, or `record`, is valid.**  It gives no compiler warning in the generated code.

Without a value, the class is `Sql`.  The setting is written `-- nested-class:`, in the file header or inside a query, and `nestedClass` in a settings file.

- **Each query has its own.**  So one file can fill two classes, as `Users.Get` and `Orders.List`, and two files of one type can fill one.  A class is written only when a query of the type puts a member in it.
- **It is a setting of the `.sql` file.**  It comes from the settings files of the folder of the `.sql` file and the folders above it, as every other setting does.  The folder of the type's C# file counts only when it is the folder of the `.sql` file or a folder above it.  For a type that reads the file named for it beside its C# file, as it does without [`Source`](attributes.md#setting-source), the two are the same folder.
- **It is ignored where the member location is not `nested-class`.**  So a `nestedClass` for a whole project, beside a file whose queries go on the type, is no error.
- **A value that is not valid is [SQLSRC111](../diagnostics.md#sqlsrc111) in a marker, and [SQLSRC019](../diagnostics.md#sqlsrc019) in a settings file.**  After SQLSRC019, a farther settings file or the default gives the name.
- **A type that already has the name is [SQLSRC007](../diagnostics.md#sqlsrc007).**  The name can be a member, a nested type or a type parameter of the type, or its own name.  Every file with a query in that class then gets no members and no models for the type until it is fixed.  The other names that its members cannot have are under [Where the members go](#locations).

<a id="constants"></a>

## Constants

```csharp
public const string GetUser = "SELECT id, name\nFROM users\nWHERE id = @id;";
```

If a query has no tokens and its output is `models` or `sql`, it becomes a `const string` with the name of the query.  The constant is `public` in a nested class, and has the query's [visibility](#setting-visibility) on the type: see [where the members go](#locations).

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
- a line for what it returns, but not for a [synchronous method](query-methods.md#synchronous-methods) of the shape `none`, which returns nothing;
- for the shapes `one` and `one-optional`, a line for the [exception](exceptions.md#unexpected-row-count) that it throws;
- on SQL Server, a line for the `InvalidOperationException` that it throws when it is given a transaction that has [completed](databases/sqlserver/query-methods.md#method-results);
- on SQL Server, for the shapes `one`, `one-optional` and `many`, a line for [`TransactionRolledBackException`](exceptions.md#transaction-rolled-back).

A [collection method](collection-methods.md#collection-methods) has a line for [`CollectionItemException`](exceptions.md#collection-item-exception), and one for `UnexpectedRowCountException` under `one` and `one-optional`.  On SQL Server, a collection method that reads rows also has a line for the `InvalidOperationException` of a query that returns [more than one result set](databases/sqlserver/query-methods.md#collection-methods).

The extension class of the methods has a summary that names the type.  So does a nested class that is `public` or `protected`.

If a project treats a missing comment on a public member (CS1591) as an error, the generated code gives it none, since every generated member is documented.
