# What is generated and where it goes

<a id="generator"></a>

## What the generator writes

The generator is the part of the `SqlSource` package that runs in the compiler.  It reads the `.sql` files of your project and writes C# beside your own code.  For each described query it writes a type for its parameters and a type for its rows, its [models](models.md#models), and, under the default [output](../settings.md#setting-output), `repository`, a [repository method](repositories.md#repository-methods): an instance method of your type that gets its own connection and runs the query.  Under the output `methods` a query gets a [method that takes the connection](connection-methods.md#methods-that-take-a-connection) instead, for a connection, a transaction or a timeout that your own code manages.  A query whose output is `sql` or `models` gets a string constant instead, or a method that returns its SQL where it has [tokens](../concepts.md#token), for your own code to run.  It needs no database, in a project that targets .NET 8 or later with C# 12 or later ([SQLSRC003](../diagnostics.md#sqlsrc003), [SQLSRC012](../diagnostics.md#sqlsrc012)).  It needs the package, one attribute, and one choice: a query whose output is `models`, `methods` or `repository`, the default, must be described by the [tool](describing.md#describe), and the build reports one that has no current entry in its sidecar, while a query whose output is `sql` needs nothing more.

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

`GetUser` and `ListFrom` are the generated methods of the two queries, public instance methods of `UserRepository`.  Each opens a connection from the data source, sends the query's parameters, runs it, reads its rows into its output model and disposes the connection: `GetUser` one row or null, as its [result shape](sql-files.md#result-shapes) `one-optional` says, and `ListFrom` every row, the default for a query that returns rows.  `ListFrom` takes the text of each token, and its token defaults, `users` and `name`, are only samples for [describing](../concepts.md#describe) the query.  `GetUserParams`, `GetUserDto` and `ListFromDto` are the models of the two queries, generated from what the tool recorded when it described them.  `UserRepository` also gets an interface of the two methods, `IUserRepository`, for code that takes a repository and a test that fakes one: see [the interface](repositories.md#repository-interface).  A query that is renamed or removed is a compile error where it is used, and so is a column that is renamed or removed once the query is described again.

With the output `models`, the same queries give `Sql.GetUser`, a [constant](#constants) that holds the text of the first query, and `Sql.ListFrom("users", "name DESC")`, a [token method](#token-methods) that returns the text of the second with its tokens filled in, and their models, for Dapper, ADO.NET or any other library to run: see [the recipe](../recipes.md#dapper-and-adonet).

<a id="locations"></a>

## Where the members go

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

Each query's members go where its [member location](#setting-member-location) says, with the accessibility its [visibility](#setting-visibility) gives, two settings of its `.sql` file.  Without them, as for `GetUser` and `CountUsers`, a repository method is a `public` instance method of the type, and a constant or token method is a public member of a private class `Sql` nested in the type, for the type's own code.  A query whose output is `methods` is by default an `internal` extension method of `DbConnection`, in the class `UserRepositoryExtensions`; `ListUsers` writes `class`, so it is a static method of the type, which takes the connection, `public` since the type is.  The settings' sections give their values and defaults; what follows is what to know of the code you get.

- **The models follow the type, and the members follow `visibility`.**  A model is `public` or `internal` by the type that claims its query, as [accessibility](models.md#model-accessibility) says, whatever the visibility of the query's members.  So a `private` method of a `public` type still has public models, and a `public` extension method of a type that is not `public` is [SQLSRC123](../diagnostics.md#sqlsrc123) when it takes or returns a model that is internal, as its models are unless a `public` type shares them.  One with no parameters and no rows takes no models, and compiles.
- **A nested class is `static partial`**, and is written for the members it holds alone: a class of query methods only is in the file of the type's methods, and a class of constants and methods has a part in each file.  Its members are `public`, and the class has the visibility.  A type none of whose queries can be used, such as one whose only file has an error, keeps an empty private class `Sql`, or the one that the `nestedClass` of its first file's settings files names, so that code that uses one of its members reports the member missing, and not the class.
- **In a nested class**, the type cannot have a member, a nested type or a type parameter named like the class, nor that name itself ([SQLSRC007](../diagnostics.md#sqlsrc007)).  A query is not named like its class, with `Async` after a query method's name under `async-method-suffix` ([SQLSRC009](../diagnostics.md#sqlsrc009)).
- **On the type**, a constant or token method is not named like the type ([SQLSRC009](../diagnostics.md#sqlsrc009)), nor like a member, a nested type or a type parameter that the type declares, nor like a nested class of the type's queries ([SQLSRC125](../diagnostics.md#sqlsrc125)).  So a token method does not overload a method of yours of the same name.  A query method's name is checked as [its output's section](repositories.md#repository-signature) says for a repository method, and where [the methods go](connection-methods.md#method-location) for a `methods` method.
- **A placement error leaves its file out.**  A query whose member location or visibility does not fit ([SQLSRC122](../diagnostics.md#sqlsrc122), [SQLSRC123](../diagnostics.md#sqlsrc123)), or whose members would take a name that is taken ([SQLSRC007](../diagnostics.md#sqlsrc007), [SQLSRC125](../diagnostics.md#sqlsrc125)), or whose nested class has two visibilities ([SQLSRC124](../diagnostics.md#sqlsrc124)), leaves its whole file without members and without models for the type, as [a name used twice](attributes.md#files-of-a-type) does; the type's other files keep theirs.  A visibility that does not fit only the models a query method takes or returns, also SQLSRC123, leaves out that method alone, as a [method's other errors](query-methods.md#query-methods) do.
- A query may share its name with a member that the type inherits, such as `ToString`.  The constant hides it, with no warning in the generated code.

<a id="placement-for-a-file"></a>

### A placement for a whole file or folder

A member location or a visibility written in a file header or a settings file reaches every query below it, whatever its output, and a query where it does not fit is an error that leaves its file without members and without models for the type until it is fixed, or only its method when what does not fit is the method's models.  The message says where the value was written.  Two cases catch a project out:

- **`"memberLocation": "ExtensionClass"` in a settings file** is [SQLSRC122](../diagnostics.md#sqlsrc122) at every query whose output is `sql`, `models` or `repository`, in its folder and the folders below it, since only a `methods` method can go in the extension class, and so is `"memberLocation": "NestedClass"` at every repository query, whose method is always an instance method of the type.  Leave it out: each output's members go to its own default.
- **A visibility in the header of a file that mixes outputs** reaches its query methods too:

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

`CountUsers` would be a constant of a private nested class, its default anyway, and `GetUser`, whose method goes to the extension class, cannot be private.  Write the visibility on the query that needs another, which wins over the header, or give it a member location where it fits:

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

<a id="setting-member-location"></a>

### Where the members go

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

The member location of a query says where its generated members go: its constant or token method, for the output `sql` or `models`, and its [query method](query-methods.md#query-methods), for `methods` or `repository`.  Above, `GetUser` is a public static method of `UserQueries` that takes the connection, and `CountUsers`, which writes no member location, is a constant of the private class `Sql` nested in it.

| Member location | The members are | Used as |
|----|----|----|
| `nested-class`, the default for `sql` and `models` | Public static members of a static class nested in the type, `Sql` unless [the nested class](#setting-nested-class) names it.  A `methods` method there takes the connection as its first argument | `Sql.GetUser`, or `Sql.GetUser(connection, ...)` for a `methods` method |
| `class`, the default for `repository` | Members of the type itself: a [repository method](repositories.md#repository-methods) is an instance method, which gets its own connection, and any other member is static, a `methods` method taking the connection as its first argument | `users.GetUser(...)` for a repository method, `UserQueries.GetUser`, or `UserQueries.GetUser(connection, ...)` for a `methods` method |
| `extension-class`, the default for `methods` | Extension methods of `DbConnection`, in a static class beside the type, named after it with `Extensions` after it.  Only a `methods` method can go there | `connection.GetUser(...)` |

It is written `-- member-location:`, in the file header or inside a query, and `memberLocation` in a settings file, as `"memberLocation": "class"`.

- **Each query has its own.**  A file may put some of its queries' members on the type, some in a nested class and some in the extension class, and two files of one type may place theirs differently.
- **A repository method is always on the type.**  `class` fits a query whose output is `repository`, and `extension-class` or `nested-class` is [SQLSRC122](../diagnostics.md#sqlsrc122).
- **A value written in the file header or in a settings file reaches every query below it, whatever its output.**  `extension-class` on a query whose output is `sql`, `models` or `repository`, or `nested-class` on one whose output is `repository`, is [SQLSRC122](../diagnostics.md#sqlsrc122), and its file gets no members and no models for the type until it is fixed, since a constant extends nothing and a repository method is the type's.  So `"memberLocation": "ExtensionClass"` in a settings file is an error at every such query of its folder and the folders below it.  Write `-- member-location:` on the queries it does not fit, or leave it out: each output's members go to its own default.
- **The names a member cannot have**, and how the generated code is laid out, are on [this page](#locations).

<a id="setting-visibility"></a>

## Visibility

```sql postgres
-- visibility: internal

-- name: GetUser -> one-optional
SELECT id, name FROM users WHERE id = @id;
```

The visibility says how visible the members of a query's member location are: `public`, `internal`, `protected` or `private`.  Above, `GetUser` is an `internal` repository method of the type, which its interface leaves out.  It is written `-- visibility:`, in the file header or inside a query, and `visibility` in a settings file.  Without one, a repository method is `public`, whatever the accessibility of the type, unless that does not fit its models, as below, and any other query takes the default of its member location:

| Member location | `visibility` sets | Default |
|----|----|----|
| `class` | Each constant, token method and query method on the type | `public` for a repository method.  Otherwise `public` when the type and every type around it are `public`, and `internal` otherwise |
| `nested-class` | The nested class's own accessibility.  Its members are always `public` inside it | `private` |
| `extension-class` | Each query method's accessibility.  The class is `public` when one of the type's methods in it is, and `internal` otherwise | `internal` |

- **A value written in the file header or in a settings file reaches every query below it**, as a member location does, and is [SQLSRC123](../diagnostics.md#sqlsrc123) where it does not fit, which leaves its file without members and without models for the type until it is fixed, or only the query's method when what does not fit is the models the method takes or returns.  A file of `sql` and `methods` queries with `-- visibility: private` in its header gives its constants a private nested class, and its query methods, which go to the extension class, the error: write `-- visibility:` or `-- member-location:` on those queries.  [This page](#placement-for-a-file) shows such a file.
- **What does not fit**: `private` or `protected` in the extension class, which a class that is not nested cannot be; `public` in the extension class of a type that is not `public` with every type around it, for a query method that takes or returns a model, since the type's models are internal and such a method cannot take or return them; `public` or `protected` for a query method that takes or returns a model that is internal, on the type or for its nested class, when the type, or a type around it, is `protected` or `protected internal`, and none of them is `internal`, `private` or `private protected`, since a class derived from it in another assembly could call the method, and its models are internal; and `protected` where C# forbids it, on the type when it is a static class, a struct or a record struct, and for the nested class of a struct or a record struct.
- **What fits, with no diagnostic**: `public` for members on a type that is `internal`, which are then internal in effect, as a repository method is by default; `protected` for the repository methods of an abstract base repository, whose derived types call them; `public` or `internal` for a nested class, which code outside the type then uses as `UserRepository.Sql.GetUser`; `protected` on a sealed class or record, which compiles with no warning from the generated code; a protected nested class in a static class; and, on a type that is `protected` or `protected internal`, or inside one, an `internal` or `private` query method or nested class, and constants and token methods of any visibility, which take no models.  A query method with no parameters and no rows takes no models either, so it fits wherever the visibility alone does, and so does one whose models a `public` type shares, which makes them public.
- **A nested class has one visibility.**  A query that writes none for its class takes the one that another query of the type writes for it, in any of the type's files.  Two queries that write two different ones are [SQLSRC124](../diagnostics.md#sqlsrc124), and every file with a query in that class gets no members and no models for the type until it is fixed.  A query method that writes none is [SQLSRC123](../diagnostics.md#sqlsrc123) when the visibility it takes does not fit the models it takes or returns, which leaves out that method alone.
- **A repository method's default can not fit either.**  On a type that is `protected` or `protected internal`, or inside one, with none of them `internal`, `private` or `private protected`, the default `public` does not fit a repository method that takes or returns a model, and is [SQLSRC123](../diagnostics.md#sqlsrc123), which leaves out that method alone: write `-- visibility: internal` on the query, or in the file header for all of them.
- **The models do not follow it.**  A query's models are `public` or `internal` by the type that claims it, whatever the visibility of its members: see [accessibility](models.md#model-accessibility).

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

This names the class nested in the type that holds a query's members when its [member location](#setting-member-location) is `nested-class`.  It is written `-- nested-class:`, in the file header or inside a query, and `nestedClass` in a settings file, and is `Sql` without one.  Above, the queries of the project are in a class `Queries`, and those of `UserRepository.sql` in `Statements`.

- **Each query has its own**, so one file can fill two classes, as `Users.Get` and `Orders.List`, and two files of one type can fill one.  A class is written only when a query of the type puts a member in it.
- **It is a setting of the `.sql` file**, taken from the settings files of its folder and the folders above it, as every other setting is, and not from those of the type's C# file.  For a type that reads the file named for it beside its C# file, as it does without [`Source`](../settings.md#setting-source), the two are the same folder.
- **It is ignored where the member location is not `nested-class`**, so a `nestedClass` for a whole project beside a file whose queries go on the type is no error.
- **The value is one C# identifier that is not a keyword**: `Queries` is fine, and `class`, `My-Sql` or `App.Sql` is not.  Nor is `file`, `required`, `scoped` or `extension`, which C# does not take as a class's name, or `var`, with which the query methods of the type would not compile.  A name in lower case, such as `queries`, or `record`, is fine, and gives no compiler warning in the generated code.  A value that is not valid is [SQLSRC111](../diagnostics.md#sqlsrc111) in a marker, and [SQLSRC019](../diagnostics.md#sqlsrc019) in a settings file, where a farther settings file or the default then gives the name.
- **A type that already has the name**, as a member, a nested type or a type parameter, or as its own name, is [SQLSRC007](../diagnostics.md#sqlsrc007), and every file with a query in that class gets no members and no models for it until it is fixed.  The other names its members cannot have are on [this page](#locations).

<a id="constants"></a>

## Constants

```csharp
public const string GetUser = "SELECT id, name\nFROM users\nWHERE id = @id;";
```

A query without tokens whose output is `sql` or `models` is a `const string` with the name of the query, `public` in a nested class and of the query's [visibility](#setting-visibility) on the type: see [where the members go](#locations).  Its value is the SQL you wrote, with the markers and comments taken out and line endings made `\n`: [what reaches the generated SQL](sql-files.md#generated-sql) lists everything done to it.  A query parameter such as `@id` stays in it as written; you give the database its value when you run the query.

Because it is a constant, it can be used wherever C# wants one, such as another `const` or an attribute argument:

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

A query with a token whose output is `sql` or `models` is a `static string` method with the name of the query, as visible as a constant would be, `ListFrom(string table, string orderBy)`, in place of a constant.  Under `methods` or `repository` the query's [method](query-methods.md#query-methods) takes the tokens instead, and runs the query.  Its parameters, and what it returns, are under [tokens](tokens.md#tokens).

A token is replaced by plain text substitution and nothing is escaped.  **Pass only text that your own code chooses**, such as a table name from a fixed list, and a [query parameter](../concepts.md#query-parameter) for every value.  By default the method throws for a null, empty or blank argument.  The rules for tokens, their defaults and that check are on [tokens](tokens.md#tokens).

<a id="intellisense"></a>

## Documentation in IntelliSense

Every generated member has a documentation comment, so your editor shows it where the member is used.  For `GetUser`, the comment is:

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

The summary is the query's `-- summary:` ([summaries](sql-files.md#summaries)), or "The `GetUser` query from `UserRepository.sql`." when it has none.  The remarks hold the SQL of the constant, so a reader sees what a query does without opening the `.sql` file.

A token method adds a line for each token and one for the result, and shows its tokens as `{{table}}`, without their defaults:

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

A [query method](query-methods.md#query-methods) has the same summary and remarks, a line for each of its arguments and one for what it returns, but for a [synchronous method](query-methods.md#synchronous-methods) of the shape `none`, which returns nothing, and, for the shapes `one` and `one-optional`, a line for the [exception](exceptions.md#unexpected-row-count) it throws.  On SQL Server, every method also has a line for the `InvalidOperationException` it throws when it is given a transaction that has [completed](databases/sqlserver/query-methods.md#method-results), and a method of the shapes `one`, `one-optional` and `many` one for [`TransactionRolledBackException`](exceptions.md#transaction-rolled-back).  A [collection method](collection-methods.md#collection-methods) has a line for [`CollectionItemException`](exceptions.md#collection-item-exception), one for `UnexpectedRowCountException` under `one` and `one-optional`, and on SQL Server, when it reads rows, one for the `InvalidOperationException` of a query that returns [more than one result set](databases/sqlserver/query-methods.md#collection-methods).  The extension class of the methods has a summary that names the type, and so does a nested class that is `public` or `protected`.

Every generated member is documented, so a project that treats a missing comment on a public member (CS1591) as an error gets none from generated code.
