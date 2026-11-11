# The generator

The generator is the part of the `SqlSource` package that runs in the compiler.  It reads the `.sql` files of your project and writes C# beside your own code.  For each described query it writes a type for its parameters and a type for its rows, its [models](#models), and, under the default [output](settings.md#setting-output), `repository`, a [repository method](#repository-methods): an instance method of your type that gets its own connection and runs the query.  Under the output `methods` a query gets a [method that takes the connection](#methods-that-take-a-connection) instead, for a connection, a transaction or a timeout that your own code manages.  A query whose output is `sql` or `models` gets a string constant instead, or a method that returns its SQL where it has [tokens](concepts.md#token), for your own code to run.  It needs no database, in a project that targets .NET 8 or later with C# 12 or later ([SQLSRC003](diagnostics.md#sqlsrc003), [SQLSRC012](diagnostics.md#sqlsrc012)).  It needs the package, one attribute, and one choice: a query whose output is `models`, `methods` or `repository`, the default, must be described by the [tool](tool.md), and the build reports one that has no current entry in its sidecar, while a query whose output is `sql` needs nothing more.

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

`GetUser` and `ListFrom` are the generated methods of the two queries, public instance methods of `UserRepository`.  Each opens a connection from the data source, sends the query's parameters, runs it, reads its rows into its output model and disposes the connection: `GetUser` one row or null, as its [result shape](sql-files.md#result-shapes) `one-optional` says, and `ListFrom` every row, the default for a query that returns rows.  `ListFrom` takes the text of each token, and its token defaults, `users` and `name`, are only samples for [describing](concepts.md#describe) the query.  `GetUserParams`, `GetUserDto` and `ListFromDto` are the models of the two queries, generated from what the tool recorded when it described them.  `UserRepository` also gets an interface of the two methods, `IUserRepository`, for code that takes a repository and a test that fakes one: see [the interface](#repository-interface).  A query that is renamed or removed is a compile error where it is used, and so is a column that is renamed or removed once the query is described again.

With the output `models`, the same queries give `Sql.GetUser`, a [constant](#constants) that holds the text of the first query, and `Sql.ListFrom("users", "name DESC")`, a [token method](#token-methods) that returns the text of the second with its tokens filled in, and their models, for Dapper, ADO.NET or any other library to run: see [the recipe](recipes.md#dapper-and-adonet).

<a id="attribute"></a>

## The attribute

`[SqlSourceGenerate]` marks a type that the generator adds to.  The package puts the attribute, the [`[SqlSourceConverter]`](settings.md#converters) and [`[SqlSourceConnection]`](#repository-connection) attributes and the exceptions that a query method throws, [`UnexpectedRowCountException`](#unexpected-row-count), [`TransactionRolledBackException`](#transaction-rolled-back) and [`CollectionItemException`](#collection-item-exception), in your project, as internal types in the namespace `SqlSource`, so the code above needs `using SqlSource;` and nothing else.  The exceptions are left out when your project uses another project's, and are public when it sets [`SqlSourcePublicExceptions`](settings.md#public-exceptions): see [projects that share internals](#projects-that-share-internals).  It also adds `Microsoft.CodeAnalysis.EmbeddedAttribute`, in the generated file `SqlSource-EmbeddedAttribute.g.cs`, an internal attribute that keeps every one of these types but the exceptions out of sight of other projects: see [known issues](#known-issues) if your project declares it too.

```csharp
[SqlSourceGenerate(Source = "Queries")]
internal static partial class Reports;
```

- **It goes on a class, a struct, a record or a record struct.**  The type may be static, generic, or nested in other types.  A static type has no instance, so its queries need an output other than `repository` ([SQLSRC034](diagnostics.md#sqlsrc034)): the files of `Reports` above set the output `methods`, `models` or `sql`.
- **The type and every type that contains it must be `partial`** ([SQLSRC001](diagnostics.md#sqlsrc001)), and none may be `file` ([SQLSRC002](diagnostics.md#sqlsrc002)).  The generator adds its members in a second declaration of each.
- **A type with an error gets no members**, and asks for no models.  The code that uses them then fails to compile too, so fix the first error the build reports.

The attribute has three properties:

| Property | Sets |
|----|----|
| `Source` | Which `.sql` files belong to the type: [Source](settings.md#setting-source) |
| `GenerateInterface` | Whether a repository gets its interface, over the project's choice: [repository interfaces](settings.md#repository-interfaces) |
| `InterfaceName` | The name of a repository's interface, `IUserRepository` by default: [repository interfaces](settings.md#repository-interfaces) |

Every other setting of a query, its [output](settings.md#setting-output) and [where its members go](#locations) among them, comes from its `.sql` file, through its markers and the [settings files](settings.md#settings-files) of its folder, and not from the type that claims it.

<a id="files-of-a-type"></a>

## Which files belong to a type

```text
Repository/
    UserRepository.cs      [SqlSourceGenerate] on UserRepository
    UserRepository.sql     belongs to UserRepository: the file named for it
    Orders.sql             not UserRepository's: another name
    Reports/
        Monthly.sql        not searched: it is in a subfolder
```

A type gets the file named for it, beside its C# file, or the files that its [`Source`](settings.md#setting-source) names, `Source = "."` for every `.sql` file of that folder, by the rules and with the errors given there.  What follows is what the generator does with the files.

- **A type's files are one set.**  A query name is used once across all of them: a second use is [SQLSRC008](diagnostics.md#sqlsrc008), and the file with the second one gets no members and no models for that type.
- **Two types may claim one file**, and each gets its own members from it, though two types of one namespace can give one method twice: see [where the methods go](#method-location).  They take the same settings from it, since every setting of a query comes from the file and its settings files: one output, one member location, one suffix and one shape of model for both.  Two types in one namespace share the models of the file's queries; two in two namespaces get a model each, unless the [namespace of the models](settings.md#setting-model-namespace) gives them one ([shared models](#shared-models)).  A type that needs other settings needs a file of its own.
- **A `.sql` file that no type claims is ignored**: see [Source](settings.md#setting-source).
- **A file with an error in how it is written produces no members** until it is fixed: see [`.sql` files](sql-files.md).  The other files of the type are unaffected.  An error about describing a query or about its sidecar, such as [SQLSRC241](diagnostics.md#sqlsrc241), leaves the query without models until it is fixed, and so without a [method](#which-methods) under `methods` or `repository`; under `models` it keeps its constant or token method.

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

Each query's members go where its [member location](settings.md#setting-member-location) says, with the accessibility its [visibility](settings.md#setting-visibility) gives, two settings of its `.sql` file.  Without them, as for `GetUser` and `CountUsers`, a repository method is a `public` instance method of the type, and a constant or token method is a public member of a private class `Sql` nested in the type, for the type's own code.  A query whose output is `methods` is by default an `internal` extension method of `DbConnection`, in the class `UserRepositoryExtensions`; `ListUsers` writes `class`, so it is a static method of the type, which takes the connection, `public` since the type is.  The settings' sections give their values and defaults; what follows is what to know of the code you get.

- **The models follow the type, and the members follow `visibility`.**  A model is `public` or `internal` by the type that claims its query, as [accessibility](#model-accessibility) says, whatever the visibility of the query's members.  So a `private` method of a `public` type still has public models, and a `public` extension method of a type that is not `public` is [SQLSRC123](diagnostics.md#sqlsrc123) when it takes or returns a model that is internal, as its models are unless a `public` type shares them.  One with no parameters and no rows takes no models, and compiles.
- **A nested class is `static partial`**, and is written for the members it holds alone: a class of query methods only is in the file of the type's methods, and a class of constants and methods has a part in each file.  Its members are `public`, and the class has the visibility.  A type none of whose queries can be used, such as one whose only file has an error, keeps an empty private class `Sql`, or the one that the `nestedClass` of its first file's settings files names, so that code that uses one of its members reports the member missing, and not the class.
- **In a nested class**, the type cannot have a member, a nested type or a type parameter named like the class, nor that name itself ([SQLSRC007](diagnostics.md#sqlsrc007)).  A query is not named like its class, with `Async` after a query method's name under `async-method-suffix` ([SQLSRC009](diagnostics.md#sqlsrc009)).
- **On the type**, a constant or token method is not named like the type ([SQLSRC009](diagnostics.md#sqlsrc009)), nor like a member, a nested type or a type parameter that the type declares, nor like a nested class of the type's queries ([SQLSRC125](diagnostics.md#sqlsrc125)).  So a token method does not overload a method of yours of the same name.  A query method's name is checked as [its output's section](#repository-signature) says for a repository method, and where [the methods go](#method-location) for a `methods` method.
- **A placement error leaves its file out.**  A query whose member location or visibility does not fit ([SQLSRC122](diagnostics.md#sqlsrc122), [SQLSRC123](diagnostics.md#sqlsrc123)), or whose members would take a name that is taken ([SQLSRC007](diagnostics.md#sqlsrc007), [SQLSRC125](diagnostics.md#sqlsrc125)), or whose nested class has two visibilities ([SQLSRC124](diagnostics.md#sqlsrc124)), leaves its whole file without members and without models for the type, as [a name used twice](#files-of-a-type) does; the type's other files keep theirs.  A visibility that does not fit only the models a query method takes or returns, also SQLSRC123, leaves out that method alone, as a [method's other errors](#query-methods) do.
- A query may share its name with a member that the type inherits, such as `ToString`.  The constant hides it, with no warning in the generated code.

<a id="placement-for-a-file"></a>

### A placement for a whole file or folder

A member location or a visibility written in a file header or a settings file reaches every query below it, whatever its output, and a query where it does not fit is an error that leaves its file without members and without models for the type until it is fixed, or only its method when what does not fit is the method's models.  The message says where the value was written.  Two cases catch a project out:

- **`"memberLocation": "ExtensionClass"` in a settings file** is [SQLSRC122](diagnostics.md#sqlsrc122) at every query whose output is `sql`, `models` or `repository`, in its folder and the folders below it, since only a `methods` method can go in the extension class, and so is `"memberLocation": "NestedClass"` at every repository query, whose method is always an instance method of the type.  Leave it out: each output's members go to its own default.
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

<a id="constants"></a>

## Constants

```csharp
public const string GetUser = "SELECT id, name\nFROM users\nWHERE id = @id;";
```

A query without tokens whose output is `sql` or `models` is a `const string` with the name of the query, `public` in a nested class and of the query's [visibility](settings.md#setting-visibility) on the type: see [where the members go](#locations).  Its value is the SQL you wrote, with the markers and comments taken out and line endings made `\n`: [what reaches the generated SQL](sql-files.md#generated-sql) lists everything done to it.  A query parameter such as `@id` stays in it as written; you give the database its value when you run the query.

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

A query with a token whose output is `sql` or `models` is a `static string` method with the name of the query, as visible as a constant would be, `ListFrom(string table, string orderBy)`, in place of a constant.  Under `methods` or `repository` the query's [method](#query-methods) takes the tokens instead, and runs the query.  Its parameters, and what it returns, are under [tokens](parameters-and-tokens.md#tokens).

A token is replaced by plain text substitution and nothing is escaped.  **Pass only text that your own code chooses**, such as a table name from a fixed list, and a [query parameter](concepts.md#query-parameter) for every value.  By default the method throws for a null, empty or blank argument.  The rules for tokens, their defaults and that check are on [parameters and tokens](parameters-and-tokens.md#tokens).

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

A [query method](#query-methods) has the same summary and remarks, a line for each of its arguments and one for what it returns, and, for the shapes `one` and `one-optional`, a line for the [exception](#unexpected-row-count) it throws.  On SQL Server, every method also has a line for the `InvalidOperationException` it throws when it is given a transaction that has [completed](sqlserver.md#method-results), and a method of the shapes `one`, `one-optional` and `many` one for [`TransactionRolledBackException`](#transaction-rolled-back).  A [collection method](#collection-methods) has a line for [`CollectionItemException`](#collection-item-exception), one for `UnexpectedRowCountException` under `one` and `one-optional`, and on SQL Server, when it reads rows, one for the `InvalidOperationException` of a query that returns [more than one result set](sqlserver.md#collection-methods).  The extension class of the methods has a summary that names the type, and so does a nested class that is `public` or `protected`.

Every generated member is documented, so a project that treats a missing comment on a public member (CS1591) as an error gets none from generated code.

<a id="models"></a>

## Models

```sql postgres
-- name: GetUser
-- summary: Loads one user by id.
SELECT id, name, deleted_at
FROM users
WHERE id = @id;
```

```csharp
namespace App
{
    /// <summary>
    /// Loads one user by id.
    /// The parameters of the query <c>GetUser</c> from <c>UserRepository.sql</c>.
    /// </summary>
    /// <param name="Id">The parameter <c>id</c>, <c>integer</c>.</param>
    public sealed record GetUserParams(int Id);

    /// <summary>
    /// Loads one user by id.
    /// A row of the query <c>GetUser</c> from <c>UserRepository.sql</c>.
    /// </summary>
    /// <param name="Id">The column <c>id</c>, <c>integer</c>, from <c>public.users.id</c>.  An identity column.</param>
    /// <param name="Name">The column <c>name</c>, <c>text</c>, from <c>public.users.name</c>.</param>
    /// <param name="DeletedAt">The column <c>deleted_at</c>, <c>timestamp with time zone</c>, from <c>public.users.deleted_at</c>.</param>
    public sealed record GetUserDto(int Id, string Name, global::System.DateTime? DeletedAt);
}
```

A model is a type that the generator writes for a described query, from the query's entry in its [sidecar](concepts.md#sidecar): an input model for its parameters, and an output model for a row of its result.  Above, a `public` type in the namespace `App` claims `UserRepository.sql`, and the table `users` has the columns `id`, an identity column, `name` and `email`, both `NOT NULL`, and `deleted_at`, in that order.  The two models are shown together; each is a file of its own among the generated files, named after its full name, such as `App.GetUserDto-Model.g.cs`.

The models are built at every build from the committed sidecars, with no database.  After a change to a query, describe it again (see [sidecar files](tool.md#sidecar-files)): until then its entry is out of date, and the query has no models.

<a id="which-models"></a>

### Which queries get models

A query gets models when its [output](settings.md#setting-output) is `models`, `methods` or `repository`, the default, and its entry in its sidecar is current.  Which models it gets depends on the entry:

| The query | Input model | Output model |
|----|----|----|
| Has parameters and returns rows | Yes | Yes |
| Returns rows and has no parameters | No | Yes |
| Has parameters and returns no rows, as an `INSERT` without `RETURNING`, or on SQL Server without `OUTPUT` | Yes | No |
| Has neither | No | No |

- **Rows of no column**, which PostgreSQL gives for `SELECT FROM users`, get an output model with no properties.
- **A query has no models while it has an error**: an entry that is missing or out of date ([SQLSRC241](diagnostics.md#sqlsrc241), [SQLSRC244](diagnostics.md#sqlsrc244)), a column or a parameter with no C# type ([SQLSRC246](diagnostics.md#sqlsrc246), [SQLSRC249](diagnostics.md#sqlsrc249) to [SQLSRC252](diagnostics.md#sqlsrc252), [SQLSRC261](diagnostics.md#sqlsrc261), [SQLSRC269](diagnostics.md#sqlsrc269) and [SQLSRC270](diagnostics.md#sqlsrc270)), a name that gives no property ([property names](#property-names)), or a [`-- column:` marker](#column-marker) that names no column it can set ([SQLSRC260](diagnostics.md#sqlsrc260)).  Under the output `models` its constant or token method is still generated; under `methods` or `repository` it has no method either.
- **A query whose output is `sql` gets none.**
- **A type with an error asks for no models.**  When two types claim one file, a type with an error of its own asks for no models of its queries, and neither does a type for a file that it leaves out ([SQLSRC008](diagnostics.md#sqlsrc008), [SQLSRC009](diagnostics.md#sqlsrc009), or a [placement error](#locations)); the other type still gets them.
- **`-- output-model:` on a query that returns no rows** is [SQLSRC259](diagnostics.md#sqlsrc259), and the query has no models until the marker is removed.  `-- input-model:` on a query with no parameters is [SQLSRC119](diagnostics.md#sqlsrc119).

<a id="model-names"></a>

### Names

```sql
-- name: GetUser
-- input-model: UserKey
-- output-model: App.Rows.UserRow
SELECT id, name FROM users WHERE id = @id;
```

A model is named after its query, with a suffix: `GetUser` gets `GetUserParams` for its parameters and `GetUserDto` for its rows.  The [suffixes](settings.md#setting-model-suffix) are settings, so `-- output-model-suffix: Row` in a file's header gives `GetUserRow`.

- **`-- input-model:` and `-- output-model:` name one query's models whole**, with no suffix: above, `UserKey`.  See [name of one query's model](settings.md#setting-model-name).
- **A name with a period is a full name**, with its namespace: `App.Rows.UserRow` is `UserRow` in the namespace `App.Rows`, whatever the [model namespace](settings.md#setting-model-namespace) says.
- **A query that reads a whole table** has a row model named after the table: see [table models](#table-models).
- **Queries whose models have one full name share one model**, when the models are the same: see [shared models](#shared-models).  Two names that differ only by case, `UserDto` and `UserDTO`, are two models.
- **Some names cannot be used**, such as the name of a type of your project: see [names that cannot be used](#unusable-model-names).

<a id="model-namespace"></a>

### Namespace

Models are types of a namespace, not nested in the type that claims the query.  They are in the namespace of that type, or in the one that the [model namespace](settings.md#setting-model-namespace) names when it is set.  A type in the global namespace gives models in the global namespace.  Two types in two namespaces that claim one file each get the models of its queries, one in each namespace, unless the model namespace gives them one.

<a id="model-accessibility"></a>

### Accessibility

A model is `public` when the type that claims its query is `public` and so is every type it is nested in, and `internal` otherwise: a `public` type nested in an `internal` one gives `internal` models.  A model that two types share is `public` when either of them would make it so.

It does not follow the [visibility](settings.md#setting-visibility) of the query's members: see [where the members go](#locations).

A `public` model cannot have a property whose type is not `public`, which a [type override](settings.md#setting-type-overrides), a [column type](settings.md#setting-column-types) or a `-- param:` marker can name.  That is [SQLSRC251](diagnostics.md#sqlsrc251), at each query of the model, and the model is not generated until the type is made `public`, or each type that claims the query `internal`.

<a id="model-shape"></a>

### Shape

```csharp
public class GetUserDto
{
    /// <summary>
    /// The column <c>id</c>, <c>integer</c>, from <c>public.users.id</c>.  An identity column.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The column <c>name</c>, <c>text</c>, from <c>public.users.name</c>.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The column <c>deleted_at</c>, <c>timestamp with time zone</c>, from <c>public.users.deleted_at</c>.
    /// </summary>
    public global::System.DateTime? DeletedAt { get; set; }
}
```

A model is a record or a class, sealed or not, by the [shape of a model](settings.md#setting-model-type), set for input models and for output models apart.  Above is `GetUserDto` as a `class`.

- **A record is positional**: a parameter for each property, in the order of the properties, as in the example at the top.  It has value equality, `with` and deconstruction.  A record whose query has an array column, such as `integer[]`, also has a private constructor without parameters.  It exists for libraries that create a row and then set its properties, such as Dapper, and you cannot call it.  The record's equality, `with` and deconstruction are as for any record.
- **A class has a property with `get` and `set` for each**, and no constructor of its own.  A property of a reference type that cannot be null starts as `default!`, so the class compiles under nullable reference types with no warning; a property of a value type, or one that can be null, has no initial value.
- **Every type of a property is written as its C# keyword, such as `int` or `string`, or else from `global::`**, as `global::System.DateTime`, so a model compiles in any namespace, whatever your `using` lines and the names of your own types, and a type you wrote by its short name is written by its full name.  No generated model gives a compiler warning or error, an `[Obsolete]` or `[Experimental]` type of your choice included, but for the [known issue](settings.md#known-issues) of a `?` inside a type argument of a generic type.  `[Experimental]` on the type's assembly or module counts too.  A type marked `[Obsolete]` as an error, which no code can use without an error, is refused as [SQLSRC252](diagnostics.md#sqlsrc252), and so is one marked `[Obsolete]` or `[Experimental]` under a diagnostic id that is not an identifier, whose warning a model could not turn off, and a generic type with a type argument that breaks a constraint of its declaration; the query has no models until you map another type.

<a id="property-order"></a>

### The order of the properties

The input model has a property for each [query parameter](concepts.md#query-parameter), in the order of the query's [parameter list](parameters-and-tokens.md#query-parameters): the order in which the parameters first appear in the SQL, then those that only a `-- param:` marker declares.  The output model has a property for each column, in the order of the select list.

The [generator parameters](settings.md#setting-generator-parameters) `sort-input` and `sort-output` sort the properties of the input model or of the output model by name, character by character by their codes, so that upper case comes before lower case: `BX` before `Ba`.  A record's parameters are in the same order, so a sorted record no longer matches its columns by position and name.  A [query method](#reading-rows) reads a sorted model as well as any other; to read rows into sorted models through Dapper, make them classes, as [the recipe](recipes.md#models-through-dapper) shows.

<a id="property-names"></a>

### Property names

| Column or parameter | Property |
|----|----|
| `created_at` | `CreatedAt` |
| `userId` | `UserId` |
| `ID` | `ID` |
| `api_v2_url` | `ApiV2Url` |
| `total!` | `Total` |

A property's name is the PascalCase of its column's or its parameter's name: the name is split at each `_`, the first character of each part is made upper case, the rest of it is kept as it is, and the parts are joined.  A column's name first loses one `!` or `?` at its end, which sets its [nullability](#model-nullability).  A parameter's name is the one the SQL first writes it with, without its `@`: `@accountId` gives `AccountId`.

Each of these is an error at the query's name, and the query has no models until it is fixed, nor a method under `methods` or `repository`; under `models` its constant or token method is still generated.  Give the column an alias, or rename the parameter, and describe the query again.

- **A column with no name**, which PostgreSQL calls `?column?`, as an expression `price * quantity` without an alias: [SQLSRC253](diagnostics.md#sqlsrc253).  SQL Server gives such a column no name, and the tool reports it when it describes the query, as [SQLSRC266](diagnostics.md#sqlsrc266).
- **A name whose PascalCase is not a C# identifier**: one of underscores alone, one that starts with a digit, or one with a character that an identifier cannot hold, such as `unit-price`: [SQLSRC254](diagnostics.md#sqlsrc254).
- **Two columns, or two parameters, whose properties have one name ignoring case**, as `SELECT u.id, o.id`, or `user_id` beside `userId`: [SQLSRC255](diagnostics.md#sqlsrc255).  Names are compared ignoring case, since Dapper matches a column to a property ignoring case.
- **A name that a record or `object` already has a member of**, `EqualityContract`, `Equals`, `GetHashCode`, `ToString`, `GetType`, `MemberwiseClone`, `Finalize`, `ReferenceEquals`, `PrintMembers`, `Deconstruct` or `Clone`, or the name of the model itself: [SQLSRC256](diagnostics.md#sqlsrc256).

<a id="property-types"></a>

### Property types

```sql postgres
-- column: users.id -> UserId

-- name: GetUser
-- column: nickname -> Nickname
SELECT id, nickname, created_at, born
FROM users
WHERE id = @id;
```

A property has the C# type of its column or parameter.  Above, in a project that maps `"date": "System.DateOnly"` with the type override and references `Npgsql.NodaTime`, `Id` is a `UserId` by the file header's column type, `Nickname` a `Nickname` by its query's marker, `CreatedAt`, a `timestamptz`, a `NodaTime.Instant` by NodaTime under `methods` or `repository` and a `System.DateTime` under `models`, and `Born`, a `date`, a `System.DateOnly` by the type override.  A column's C# type is the first of these that gives one:

1. its query's `-- column: name -> Type` marker: see [the `-- column:` marker](#column-marker);
2. the [column types](settings.md#setting-column-types), by the column of a table or view it is read from;
3. the [type override](settings.md#setting-type-overrides), by its database type;
4. a library of the [library types](settings.md#setting-library-types) that is on for the query, NodaTime, NetTopologySuite or System.Text.Json, for a query whose output is `methods` or `repository` alone;
5. the type map of [PostgreSQL](postgresql.md#type-map) or [SQL Server](sqlserver.md#types).

A parameter's is its [`-- param:` marker](parameters-and-tokens.md#param-marker)'s C# type after `->`, then the type override's, the library's and the type map's.  Each of the first three is a type you chose, which a query method reads and sends through your [converters](settings.md#converters) when it has them, and a class of yours in a column that holds JSON through System.Text.Json's serializer: see [a class in a JSON column](settings.md#json-class).  A library's type is read and sent on PostgreSQL by Npgsql, through its plugin for NodaTime or NetTopologySuite, and by the query method itself on SQL Server, unless your converters convert it, and the type map's is read and sent by the driver.  On SQL Server a type of NetTopologySuite is read and sent by the query method whichever place chose it, and no converter of yours replaces that: see [NetTopologySuite on SQL Server](sqlserver.md#nettopologysuite).

A PostgreSQL array parameter of a query whose output is `methods` or `repository` has the type `IEnumerable<T>` of its element, `IEnumerable<int>` for an `int4[]`, in place of `T[]`, and the query method sends any sequence: see [array parameters](postgresql.md#array-parameters).  A column's property, and a parameter's in a query whose output is `models`, stay `T[]`.

A [list parameter](parameters-and-tokens.md#list-parameters), written after `IN` with no parentheses, has the type `IEnumerable<T>` of its values' C# type under `models`, `methods` and `repository`: `IEnumerable<int>` for a list of `integer`.  Its values' type is found as a parameter's is, and is never nullable.

<a id="model-nullability"></a>

### Which properties can be null

```sql postgres
-- name: GetCustomerLabels
SELECT id, initcap(name) AS "display_name!"
FROM customers;
```

A property that can be null has its C# type with `?`: `int?`, which is `System.Nullable<int>`, or `string?`, a nullable reference.

- **A column's property can be null when the tool recorded that the column can be.**  Which columns can be null says when that is, for [PostgreSQL](postgresql.md#nullability) and for [SQL Server](sqlserver.md#nullability).  A column that the tool showed is never null gets a property that cannot be null.
- **A column whose name ends in `!` gets a property that cannot be null, and one whose name ends in `?` a property that can**, whatever the tool recorded.  Write the mark at the end of the column's alias, in quotes.  Above, `initcap` is never null for a value that is not, though SqlSource treats it as possibly null: `"display_name!"` gives the property `DisplayName` the type `string` and not `string?`.  A `!` is your word that the column is never null, and the build does not check it: a [query method](#reading-rows) throws when such a column is null after all.  The mark stays in the name of the column the query returns, `display_name!`.  A query method reads the columns by position, so the name does not matter to it, but a library that matches a row's columns to properties by name, as Dapper does, does not match it to `DisplayName`: with a record model Dapper throws when it reads the rows, and with a class model it leaves the property at its default value.  Read such rows through Dapper with a [`-- column:` marker](#column-marker) instead.
- **A column whose `-- column:` marker says `null` or `not null` gets that nullability**, whatever the tool recorded, and keeps its name: see below.
- **A parameter's property can be null only when its [`-- param:` marker](parameters-and-tokens.md#param-marker) says `null`**, or, for a [list parameter](parameters-and-tokens.md#list-parameters), when the query tests the list for null, as `@ids IS NULL` does.

<a id="column-marker"></a>

#### Setting a column's nullability or type with a marker

```sql postgres
-- name: GetCustomerLabels
-- column: display_name not null
-- column: order_count -> OrderCount
SELECT c.id, initcap(c.name) AS display_name, count(o.id) AS order_count
FROM customers AS c
LEFT JOIN orders AS o ON o.customer_id = c.id
GROUP BY c.id, c.name;
```

A `-- column:` marker inside a query sets the nullability of one of its columns, or gives it a C# type of your choice, and leaves the column's name alone.  Above, the query returns the column `display_name`, as it would without the marker, and its property `DisplayName` has the type `string` and not `string?`; `OrderCount`, a `bigint` that no table gives, has the type `OrderCount`, a type of your project.  Use the marker for the nullability where the rows are read by the names of their columns, as Dapper reads them: a mark at the end of an alias changes the column's name, and the marker does not.

- **The value is the column's name, then `null` or `not null`, `->` and a C# type, or both in that order**: `-- column: note null -> NoteText`, or `-- column: settings null -> json CustomerSettings` with the word `json` before the type.  `null` gives a property that can be null, and `not null` one that cannot.  One marker names one column: write one for each.  Two markers for one column are fine when they say the same, or when one gives the nullability and the other the type, and [SQLSRC112](diagnostics.md#sqlsrc112) when they give two nullabilities or two C# types, or one type with and without `json`.
- **`-> json Type` says that the column holds JSON**, for a column of a string type, such as `text` or `nvarchar(max)`, whose class of yours is then read through System.Text.Json's serializer: see [the JSON opt-in](settings.md#json-opt-in).
- **`->` gives the column that C# type**, and `-> default` takes the column out of every column type, so that the type override, or else a library or the type map, gives its type: which wins is in [column types](settings.md#setting-column-types).  The type is written as a type override's is, a keyword, a full name or a short name, and the column keeps its nullability: `-> OrderCount` on a column that can be null gives `OrderCount?`.  A query method reads the type through your [converters](settings.md#converters) when it has them.
- **The name is the column's as the query returns it**, its alias or its own name, compared ignoring case.  Write it in double quotes when it has white space or a `.`, or when it is the word `null` or `not`, and double a quote inside it: `-- column: "total due" not null`.  A value that is not one name, plain or quoted, followed by `null` or `not null`, a `->` and a type, or both, is [SQLSRC111](diagnostics.md#sqlsrc111): so is `-- column: total due not null`, without its quotes, and so is a name with a `.` outside quotes, `-- column: users.id -> UserId`, since a column's source is mapped in the file header.
- **A marker that names no column of the query's result is [SQLSRC260](diagnostics.md#sqlsrc260)**, and so is one on a query that returns no rows, and one that says `null` or `not null` of a column whose alias already ends in `!` or `?`, whose nullability is then said twice.  A marker that gives a type alone may name such a column.  The query gets no models until it is fixed.
- **The marker is a comment.**  Adding, changing or removing one, or its type, does not change the query, so it does not need describing again.  A query whose output is `sql` has no models, and its marker has no effect.
- **In the file header**, `-- column:` names the column of a table or view that a value is read from, `-- column: users.id -> UserId`, and gives it a type in every query of the file: see [column types](settings.md#setting-column-types).

<a id="model-documentation"></a>

### Documentation

Every model and every property has a documentation comment, which your editor shows where it is used, as in the example at the top.

- **A model's** is the query's `-- summary:`, when it has one ([summaries](sql-files.md#summaries)), then a line that says what the model is and names the query and its file: "The parameters of the query `GetUser` from `UserRepository.sql`." or "A row of the query `GetUser` from `UserRepository.sql`."  A model that several queries share names each of them, and takes the summary of the first: see [shared models](#shared-models).  A table model names its table: see [table models](#table-models).
- **A property's** names its column or parameter as the database does, its database type as the tool recorded it, the table column it comes from when it has one, and whether that is an identity column or a computed column.
- **A record** documents its properties with `<param>` tags, which your editor shows on each property, and **a class** documents each property with its own summary.

<a id="table-models"></a>

### Table models

```sql postgres
-- name: GetUser
SELECT * FROM users WHERE id = @id;

-- name: ListUsers
SELECT id, name, email, deleted_at FROM users ORDER BY name;
```

```csharp
/// <summary>
/// A row of the table <c>public.users</c>, read by the queries <c>GetUser</c> from <c>UserRepository.sql</c> and <c>ListUsers</c> from <c>UserRepository.sql</c>.
/// </summary>
/// <param name="Id">The column <c>id</c>, <c>integer</c>, from <c>public.users.id</c>.  An identity column.</param>
/// <param name="Name">The column <c>name</c>, <c>text</c>, from <c>public.users.name</c>.</param>
/// <param name="Email">The column <c>email</c>, <c>text</c>, from <c>public.users.email</c>.</param>
/// <param name="DeletedAt">The column <c>deleted_at</c>, <c>timestamp with time zone</c>, from <c>public.users.deleted_at</c>.</param>
public sealed record UsersDto(int Id, string Name, string Email, global::System.DateTime? DeletedAt);
```

A query whose rows are exactly the rows of a table gets an output model named after the table, in place of one named after the query, and every query that reads the table so shares it.  On PostgreSQL a view, a materialized view and a foreign table count as a table here, and on SQL Server a view does.  On SQL Server a table's columns are those that `SELECT *` gives, so a `HIDDEN` period column of a system-versioned table is not one of them.  The rows are the table's when each column comes from that one table with the table column's name and nullability, and the columns are all of the table's, in its order: `SELECT *`, or the whole list written out, as in both queries above.  An alias, a column left out, another order, an expression or an outer join gives the query a model of its own.  The tool decides this when it describes the query.

- **The name** is the PascalCase of the table's name, by the rule of [property names](#property-names), with the PascalCase of its schema before it when the schema is not `public` for PostgreSQL or `dbo` for SQL Server, and the output suffix after it: `users` gives `UsersDto`, `order_lines` gives `OrderLinesDto`, and `billing.invoices` gives `BillingInvoicesDto`.  No word is made singular.
- **A table whose name, or whose schema's name, gives no identifier** gives no table model, and the query's model is named after the query, with no error: a name such as `"order-lines"`, or one that ends in `!` or `?`.
- **`-- output-model:` wins over the table's name**, and the [generator parameter](settings.md#setting-generator-parameters) `table-models=off` turns table models off, for a query, a file, a folder or a whole project.  The input model is always named after the query.
- **The namespace** is that of the type that claims the query, so two types in two namespaces that read one table get a table model each, unless the [model namespace](settings.md#setting-model-namespace) gives them one.
- **A table model is shared by the rule of [shared models](#shared-models).**  Two queries of one table whose models differ, as when a type override of one of them gives a column another C# type, are SQLSRC257.  Two tables whose names give one model name, such as `a_b.c` and `a.b_c`, share `ABCDto` when their properties agree, and its documentation names both tables.  A query whose own model has the name of a table model, a query `Users` beside the table `users`, shares it too, and the documentation adds "Also a row of the query `Users` from ...".
- **A query that stops reading the whole table gets a model of its own name again.**  When the table gains a column and the query lists its columns, the query's rows are named after the query once it is described again, and the code that used the table model no longer compiles.  A `SELECT *` query keeps the table model.

<a id="shared-models"></a>

### Shared models

```sql postgres
-- name: GetUser
-- output-model: UserRow
SELECT id, name FROM users WHERE id = @id;

-- name: FindUser
-- output-model: UserRow
SELECT id, name FROM users WHERE email = @email;
```

A model is generated once for its full name, however many queries and types ask for it.  Two queries share one model when their models of that name are the same: both input models or both output models, of one shape and sorted or not alike, with properties of the same names, C# types and nullability, in the same order.  Above, `GetUser` and `FindUser` share `UserRow`, and its documentation reads "A row of the queries `GetUser` from `UserRepository.sql` and `FindUser` from `UserRepository.sql`."  Two types in one namespace that claim one file share the models of its queries in the same way, and the file gives both the same settings for them.

- **Two models of one name that differ** are [SQLSRC257](diagnostics.md#sqlsrc257), which names the first difference.  The queries are taken in the order of their files' paths, then of their order in a file, then of the names of the types that claim them, and the error is at the later query, at its `-- input-model:` or `-- output-model:` marker, or at its name when it has none.  The earlier query's model is generated, and the later query has none of that kind until the two agree.
- **An input model and an output model are never shared**, so one name for both, which two equal suffixes give a query, is SQLSRC257 too.
- **A model shared across outputs can differ by a library's types.**  A query whose output is `methods` or `repository` gets a library's types where the [library types](settings.md#setting-library-types) turn it on, NodaTime's `Instant` for a `timestamptz` on PostgreSQL, while a query whose output is `models` keeps the type map's `System.DateTime`.  So a table model, or a model named by a marker, that a query with a method and a `models` query share, or two queries with methods where the library is on for one and off for the other, is SQLSRC257.  Name the models apart with [`-- output-model:`](settings.md#setting-model-name), or turn the library on or off alike for both.
- **A PostgreSQL array parameter differs by output too.**  The input model of a query with a method, `methods` or `repository`, holds it as an `IEnumerable<T>`, and a `models` query's as a `T[]`, so a model of one name that both share is SQLSRC257, which names both types.  Name the models apart, or give both queries the same output: see [array parameters](postgresql.md#array-parameters).
- **A shared model's documentation is its first query's**: that query's summary, and its columns' or parameters' documentation, though another query's column comes from another table.

<a id="unusable-model-names"></a>

### Names that cannot be used

A model is not generated, and each query that would have it is [SQLSRC258](diagnostics.md#sqlsrc258), when its full name could not compile, or would be warned on:

- **A type of your project has the full name**, the type that carries `[SqlSourceGenerate]` among them.
- **It is a namespace of your project, or a part of its namespace is a type of your project**, as a model namespace `App.Queries` beside a class `App.Queries`.
- **It is the namespace of another model, or a part of its namespace is another model**, as `App.Data` beside `App.Data.Row`, which is reported for both.
- **Its name is lower-case ASCII letters alone**, as `usersdto`, which the compiler warns on, since such a name could become a keyword.

Give the query [another model name](settings.md#setting-model-name), or set another [model namespace](settings.md#setting-model-namespace).  A property that has the name of its model is [SQLSRC256](diagnostics.md#sqlsrc256).

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

A query whose [output](settings.md#setting-output) is `repository`, the default, or `methods` gets an asynchronous method that runs it, in place of its constant or token method.  The two outputs differ in where the connection comes from:

| Output | The method | Called as |
|----|----|----|
| `repository`, the default | A [repository method](#repository-methods): a `public` instance method of the type, which gets its own connection from a member of the type, and takes no connection, transaction or timeout | `users.GetUser(...)` |
| `methods` | A [method that takes a connection](#methods-that-take-a-connection): by default an `internal` extension method of `DbConnection`, which takes the connection, a transaction and a timeout | `connection.GetUser(...)` |

Either method sends each parameter, runs the query and reads its rows into the output model, here one row or null, by its [result shape](sql-files.md#result-shapes).  It runs on ADO.NET's `DbConnection`, through the driver your project references, Npgsql for PostgreSQL and Microsoft.Data.SqlClient for SQL Server, and uses no reflection and no library of SqlSource's.  Its SQL is built inside the method, from the query's text, its [tokens](parameters-and-tokens.md#tokens) and its [lists](parameters-and-tokens.md#list-parameters), and your code cannot reach it: a project that runs a query's SQL itself, through Dapper or its own code, gives it the output `models`.  One type can have queries of both outputs, in one file or in several: a repository keeps the queries that run alone at `repository`, and gives those that its own code runs in a transaction the output `methods`.  What each shape returns, how a row is read and a parameter sent, and the exceptions, below, are the same for both.  A query can also get a [collection method](#collection-methods), an overload of its method that runs it over a collection of its input models.

<a id="which-methods"></a>

### Which queries get a method

A query gets its method from a type that claims its file when, for that type:

- **its output is `repository` or `methods`**;
- **it has what its models need**: a current entry in its sidecar, a C# type for each column and parameter, and every model it needs, an input model when it has parameters and an output model when it returns rows.  A query with neither needs no model;
- **its method can be written**: a result shape that its rows allow ([SQLSRC272](diagnostics.md#sqlsrc272)), a name that nothing else takes ([SQLSRC274](diagnostics.md#sqlsrc274)), no token named as an argument a query method can have ([SQLSRC275](diagnostics.md#sqlsrc275)), and, in the extension class, a class to hold it ([SQLSRC276](diagnostics.md#sqlsrc276));
- **for a repository query, its type has its connection**: the type is not static ([SQLSRC034](diagnostics.md#sqlsrc034)), and one member of it gives the connection of the query's database, as [the connection](#repository-connection) says ([SQLSRC028](diagnostics.md#sqlsrc028) to [SQLSRC033](diagnostics.md#sqlsrc033), [SQLSRC036](diagnostics.md#sqlsrc036));
- **its [converters](settings.md#converters) serve it**: a column or parameter whose type a type override, a column type or a `-- param:` marker chose, and whose type has converters, has one it can use: of the direction it needs ([SQLSRC280](diagnostics.md#sqlsrc280)), between it and the type the driver reads when it is a type of yours that the driver never reads ([SQLSRC279](diagnostics.md#sqlsrc279)), and the only one of that direction, with nothing wrong with it ([SQLSRC277](diagnostics.md#sqlsrc277), [SQLSRC278](diagnostics.md#sqlsrc278));
- **the project references the driver of its dialect** ([SQLSRC273](diagnostics.md#sqlsrc273)), at Npgsql 8.0 or later ([SQLSRC247](diagnostics.md#sqlsrc247)) or Microsoft.Data.SqlClient 6.1 or later ([SQLSRC267](diagnostics.md#sqlsrc267)).

A query whose output is `repository` or `methods` that fails any of the others has an error, and gets neither its method nor a constant until it is fixed.  Its models are still generated when it has them, and the type's other queries get their methods.  A [placement error](#locations), such as a visibility that does not fit the query's member location ([SQLSRC123](diagnostics.md#sqlsrc123)), is different: it leaves the query's whole file without members and without models for the type.  A visibility that does not fit only the models the method takes or returns, also SQLSRC123, leaves out the method alone, as the others here do.  Each type that claims a file gets its own methods from it, by its own settings, and [where the methods go](#method-location) says when two such types give one `methods` method twice.  Only the queries of a dialect that can be described, `postgres` or `mssql`, have entries: a query of another dialect whose output is `repository` or `methods` is [SQLSRC209](diagnostics.md#sqlsrc209), and wants the output `sql`.  A statement that the tool cannot describe, such as PostgreSQL's `CALL` ([SQLSRC233](diagnostics.md#sqlsrc233)), gets no method either, so on PostgreSQL no method calls a procedure: give such a query `-- output: sql` and run its constant yourself.

<a id="repository-methods"></a>

### Repository methods

```sql postgres
-- name: GetUser -> one-optional
-- summary: Loads one user by id.
SELECT id, name
FROM users
WHERE id = @id;
```

```csharp
// Your code
[SqlSourceGenerate]
public partial class UserRepository(NpgsqlDataSource dataSource);

// Generated, shown without global::
namespace App
{
    partial class UserRepository : IUserRepository
    {
        public async Task<GetUserDto?> GetUser(
            GetUserParams parameters,
            CancellationToken cancellationToken = default)
    }

    public partial interface IUserRepository
    {
        Task<GetUserDto?> GetUser(
            GetUserParams parameters,
            CancellationToken cancellationToken = default);
    }
}

// Your code
var users = new UserRepository(dataSource);
GetUserDto? user = await users.GetUser(new GetUserParams(42));
```

A query whose [output](settings.md#setting-output) is `repository`, the default, gets an instance method of the type that claims it, a repository.  Above, a `public` type `App.UserRepository` claims `UserRepository.sql`.  The method gets a connection from a member of the repository, here a new one from the data source `dataSource`, sends each parameter, runs the query, reads its row into the output model, and disposes the connection before it returns or throws.  So the code that calls it passes no connection, and needs none.  The repository also gets [an interface](#repository-interface) of its public repository methods, `IUserRepository`, for the code that uses it.

<a id="repository-signature"></a>

#### The signature

```csharp
public async Task<ListFromDto[]> ListFrom(
    string table,
    string orderBy,
    CancellationToken cancellationToken = default)
```

The method has the name of the query, with `Async` after it under the [generator parameter](settings.md#setting-generator-parameters) `async-method-suffix`: `ListFromAsync`.  Its arguments, in this order:

| Argument | What it is |
|----|----|
| `parameters` | The query's input model, when it has parameters.  It is not in the method of a query without parameters |
| A `string` for each [token](parameters-and-tokens.md#tokens) | The text that replaces the token, named as the token, in the order the tokens first appear, checked as [a method that takes a connection](#method-signature) checks it |
| `cancellationToken` | Cancels getting and opening the connection, running the query and reading its rows |

It takes no connection, no transaction and no command timeout: see [transactions and timeouts](#repository-transactions).  It returns what [its shape](#method-shapes) says, as any query method does, and throws as [a method that takes a connection](#method-signature) throws, but for the checks of the connection and the transaction it does not take: `ArgumentNullException` for a null `parameters`, and for a list parameter `ArgumentException` and `InvalidOperationException`.

- **It is an instance method of the type, `public` by default**, whatever the accessibility of the type: a public method of an internal type is internal in effect.  On a type that is `protected` or `protected internal`, or inside one, a method that takes or returns a model cannot be `public`, since its models are internal, and is [SQLSRC123](diagnostics.md#sqlsrc123) until you write `-- visibility: internal` on it.  [`visibility`](settings.md#setting-visibility) gives it another, such as `protected` for the methods of an abstract base repository, which its derived types expose as they choose.  A method that is not `public` is left out of the interface.
- **It is always on the type.**  A [member location](settings.md#setting-member-location) of `class` fits, and `extension-class` or `nested-class` is [SQLSRC122](diagnostics.md#sqlsrc122).
- **Its name is not taken** by a member of the type, the type itself or one of its type parameters, a constant or token method that another query puts on the type, a nested class of the type's queries, or, for a public method, the name of the type's interface ([SQLSRC274](diagnostics.md#sqlsrc274)).  A token is not named `parameters` or `cancellationToken`, nor as any other argument a query method can have ([SQLSRC275](diagnostics.md#sqlsrc275)).
- A type's repository methods are in the file of its methods, `App.UserRepository-Methods.g.cs`, with its other query methods and its interface.

<a id="repository-connection"></a>

#### The connection

```csharp
[SqlSourceGenerate]
public partial class OrderRepository(string connectionString);

[SqlSourceGenerate]
public partial class AuditRepository(IConnectionFactory factory)
{
    [SqlSourceConnection]
    private Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
        factory.OpenAsync("audit", cancellationToken);
}
```

A repository method gets its connection from one member of the type, its connection source, which the build finds by its name or type, or which you mark `[SqlSourceConnection]`.  `OrderRepository` above has a connection string, found by its name, and `AuditRepository` a method of its own that the attribute marks.  What the method does with the connection depends on what the member gives:

| The member | Found | The method's connection |
|----|----|----|
| A `string` field, property or primary-constructor parameter named `connectionString`, `_connectionString` or `ConnectionString`, or any `string` member or method that is marked | By its name and type | A new connection of the string, an `NpgsqlConnection` or a `Microsoft.Data.SqlClient.SqlConnection`, which the method opens and disposes |
| A field, property or primary-constructor parameter of `DbDataSource` or a type derived from it, such as `NpgsqlDataSource` | By its type | A new connection that the data source opens, `OpenConnectionAsync`, which the method disposes |
| A method that returns a `DbConnection` or a type derived from it, or a `Task` or `ValueTask` of one, and that takes nothing or one `CancellationToken`, which gets the method's | When marked | The connection it returns, called for each call of the method, opened when it is closed, and disposed |
| A field, property or primary-constructor parameter of `DbConnection` or a type derived from it | When marked | Borrowed: opened when it is closed and closed again before the method returns, never disposed |

The rule to remember: **a method is a factory, and a field, a property or a parameter is held.**  What a method returns is the repository method's, which disposes it; a connection that the repository holds is borrowed, and left as the method found it.  A connection string and a data source make a new connection for each call, which the method disposes.

- **Found by its name or type.**  With no member marked, the type's one source is its one `string` named as above, or its one member of a data source type, among its own fields and properties, those of its base types that it can reach, static ones too, and its primary constructor's parameters.  A connection is never found by its type, since only you know whether the repository owns it: mark it.  None is [SQLSRC028](diagnostics.md#sqlsrc028), and two or more [SQLSRC029](diagnostics.md#sqlsrc029), which lists them.
- **Fields and properties before parameters.**  A primary-constructor parameter is found only when no field or property is.  So `partial class UserRepository(NpgsqlDataSource dataSource) { private readonly NpgsqlDataSource _dataSource = dataSource; }` uses `_dataSource`, a record's positional parameter yields to its property, and `UserRepository(string connectionString) : RepositoryBase(connectionString)` uses the base type's member when it can reach one.  A parameter that the type passes to its base type, or uses to initialize a field or property, is found only when you mark it, since the methods would keep a second copy of it, which the compiler warns of (CS9107, CS9124).
- **`[SqlSourceConnection]` marks the source**, on a field, a property, a method or a primary-constructor parameter, of any of the four kinds above.  Once one member is marked, only marked members count, and none is found by its name or type.  An override of a marked member is marked as the member it overrides.  A marked member that cannot give a connection, such as a method with other parameters, is [SQLSRC032](diagnostics.md#sqlsrc032).
- **Several databases.**  A repository whose queries use more than one [logical database](settings.md#setting-database) marks one member for each, with the database's name, which is compared ignoring case.  Nothing is found by its name or type then, since nothing says which database a member is for.

  ```csharp
  [SqlSourceGenerate(Source = ".")]
  public partial class Reports(
      [SqlSourceConnection(Database = "main")] NpgsqlDataSource main,
      [SqlSourceConnection(Database = "reports")] NpgsqlDataSource reports);
  ```

  A mark without `Database` on such a type is [SQLSRC030](diagnostics.md#sqlsrc030), and one for a database that no repository query of the type uses is the warning [SQLSRC031](diagnostics.md#sqlsrc031).  A query whose output is not `repository` needs no source.
- **The source is of its database's engine**: an Npgsql type for a SQL Server database, or a Microsoft.Data.SqlClient type for a PostgreSQL one, is [SQLSRC033](diagnostics.md#sqlsrc033).  Microsoft.Data.SqlClient has no data source, so a SQL Server repository takes a connection string, a factory method, or a connection it borrows.
- **A primary-constructor parameter named `cancellationToken`** is hidden by every method's argument of that name, and **one named `parameters`** by the input model of a method whose query has parameters, when a repository query of its database has some, [SQLSRC036](diagnostics.md#sqlsrc036): rename it.
- **A null source throws.**  A connection string, a data source or a connection that is null when the method is called, or a factory that returns null, throws `InvalidOperationException`, "The connection source 'dataSource' of 'App.UserRepository' is null.", before anything is sent.
- **A method that [streams its rows](settings.md#streaming-rows)** gets its connection when the loop asks for the first row, and disposes it, or closes a borrowed one that it opened, when the loop ends, so the connection is held for the whole loop.
- **A borrowed connection runs the command in no transaction.**  A repository method gives its command no transaction, so on SQL Server, Microsoft.Data.SqlClient refuses it on a connection that has a pending transaction, with `InvalidOperationException`; Npgsql runs it in the connection's transaction.  To run queries in a transaction, give them the output `methods`: see [transactions and timeouts](#repository-transactions).
- **A PostgreSQL connection string makes a bare connection**, which knows none of the plugins and mappings of your data source: see [known issues](#known-issues).  Give a PostgreSQL repository a data source.

The attribute is `SqlSource.SqlSourceConnectionAttribute`, generated into your project beside `[SqlSourceGenerate]`.  Its one property, `Database`, names the [logical database](settings.md#setting-database) as the `database` setting does.

<a id="repository-interface"></a>

#### The interface

```csharp
// Your code
public sealed class Greeter(IUserRepository users)
{
    public async Task<string> Greet(int id) =>
        (await users.GetUser(new GetUserParams(id)))?.Name ?? "stranger";
}

// Your code, adding a member of your own to the generated interface
public partial interface IUserRepository
{
    Task<int> CountActive(CancellationToken cancellationToken = default);
}
```

A type that claims a query whose output is `repository` gets a `partial interface`, named `I` and the type's name, `IUserRepository` for `UserRepository`, which the type implements.  Code that takes the interface can be given a fake of it in a test, with no database.

- **Its members are the type's public repository methods**, with their signatures and documentation.  A method of another output, a method that is not `public`, and a method with an error are not in it.  It is generated while the type claims a repository query, even when every repository method has an error, so that code that names it reports only those errors.
- **It is beside the type**, in its namespace, or inside the type it is nested in, with the type's type parameters.  It is `public` when the type and every type around it are, and `internal` otherwise.
- **It is `partial`**, so a `partial interface` of your own of the same name adds members, which your own part of the repository implements.
- **Its name** is [`InterfaceName`](settings.md#repository-interfaces) on the type's `[SqlSourceGenerate]` when it is given.  A name that something beside the type already has is [SQLSRC035](diagnostics.md#sqlsrc035), and one that is not a C# identifier [SQLSRC037](diagnostics.md#sqlsrc037).  Both leave the interface out, and the methods in.
- **Turning it off**: `GenerateInterface = false` on the type's `[SqlSourceGenerate]`, or `SqlSourceRepositoryInterfaces` for the project, as [repository interfaces](settings.md#repository-interfaces) says.

<a id="repository-transactions"></a>

#### Transactions and timeouts

A repository method takes no transaction and no command timeout.  It runs on the connection that its source gives, in no transaction of yours, under the driver's default timeout.  A query that must run in a transaction with others, or that needs its own timeout, takes the output `methods` instead, in its own file or with `-- output: methods` on the query, and your own code in the repository opens a connection, begins the transaction and calls its [method that takes a connection](#methods-that-take-a-connection).  See [the recipe](recipes.md#queries-in-a-transaction).

A repository's [collection method](#collection-methods) begins a transaction of its own, by the generator parameter `transaction`, and commits it before it returns, so that its items are added all together or not at all.  It begins none when an ambient transaction, such as a `TransactionScope`'s, is open, and runs in that one instead, or, on a connection that is not in it, such as a borrowed connection opened before the scope, in no transaction at all, as under `transaction=off`: see [transactions](#collection-transactions).

<a id="methods-that-take-a-connection"></a>

### Methods that take a connection

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

A query whose [output](settings.md#setting-output) is `methods` gets a method that takes the connection to run on, for code that manages its connections, its transactions and its timeouts itself.  Above, a `public` type `App.UserRepository` claims `UserRepository.sql`.  The method takes the connection and the query's input model, opens the connection when it is closed, sends each parameter, runs the query and reads its row into the output model, and leaves a connection that was open as it was.

<a id="method-location"></a>

#### Where the methods go

A `methods` method goes where its query's [member location](settings.md#setting-member-location) says, with its [visibility](settings.md#setting-visibility).  Without either, it is an `internal` extension method of `DbConnection`, called on the connection as `connection.GetUser(...)`, in a generated `static partial class` named after the type with `Extensions` after it, `UserRepositoryExtensions` for `UserRepository`.

- **The extension class is in the type's namespace**, not nested in the types around the type, and is named after the type's own name alone: `Outer.Queries<T>` gives `QueriesExtensions`.  Code in another namespace calls the methods with a `using` of that namespace.
- **It is `public` when one of the type's methods in it is**, as `-- visibility: public` asks and a type that is `public` with every type around it allows, and `internal` otherwise.  Each method keeps its own visibility in it.
- **Two types whose classes have one name in one namespace share it**, as two types `Queries` nested in two types of one namespace do, when the class is `public` for both or `internal` for both.  When it is not, or when a type of your project or a model has the class's full name, the type gets none of its methods there: [SQLSRC276](diagnostics.md#sqlsrc276).
- **A method there is not named like a member of `DbConnection` or `object`**, such as `Open` or `ToString`, which a call on a connection would find instead, nor like the class itself ([SQLSRC274](diagnostics.md#sqlsrc274)).

Under `class`, the methods are static methods of the type itself, which take the connection as their first argument: `UserRepository.GetUser(connection, ...)`.  A method is then not named like a member of the type, the type itself or one of its type parameters, nor like a constant or token method that another query puts on the type, nor like a nested class of the type's queries ([SQLSRC274](diagnostics.md#sqlsrc274)).

Under `nested-class`, the methods are public static methods of the nested class, which take the connection as their first argument, since a nested class cannot hold extension methods: `Sql.GetUser(connection, ...)` in the type's code, and `UserRepository.Sql.GetUser(connection, ...)` outside it when the class is `public` or `internal`.  A method is then not named like a constant or token method that another query of the type, in any of its files, puts in that class ([SQLSRC274](diagnostics.md#sqlsrc274)), nor like the class ([SQLSRC009](diagnostics.md#sqlsrc009)).

Two methods of one name in one class, as those of a query `Get` under `async-method-suffix` and a query `GetAsync` without it, are SQLSRC274, and so are two methods of one name and one list of arguments in two extension classes of one namespace, which a call could not tell apart.  A type's methods, wherever they go, are in a file of their own among the generated files, named after the type's full name, `App.UserRepository-Methods.g.cs`.

<a id="method-signature"></a>

#### The signature

```csharp
internal static async Task<ListFromDto[]> ListFrom(
    this DbConnection connection,
    string table,
    string orderBy,
    DbTransaction? transaction = null,
    int? commandTimeout = null,
    CancellationToken cancellationToken = default)
```

The method has the name of the query, with `Async` after it under the [generator parameter](settings.md#setting-generator-parameters) `async-method-suffix`: `ListFromAsync`.  Its arguments, in this order:

| Argument | What it is |
|----|----|
| `connection` | The connection to run on, after `this` for an extension method alone: see [the connection](#method-connection) |
| `parameters` | The query's input model, when it has parameters.  It is not in the method of a query without parameters |
| A `string` for each [token](parameters-and-tokens.md#tokens) | The text that replaces the token, named as the token, in the order the tokens first appear.  It is checked as a token method checks it: by default null, empty or blank text throws, and `token-validation=off` turns the check off |
| `transaction` | The transaction to run the query in, or null |
| `commandTimeout` | The command's timeout in seconds, or null for the driver's |
| `cancellationToken` | Cancels opening the connection, running the query and reading its rows.  What each driver throws then is on the pages of [PostgreSQL](postgresql.md#method-results) and [SQL Server](sqlserver.md#method-results) |

The method of a query with tokens raises no CA2100 ("Review SQL queries for security vulnerabilities") in your build, though it runs SQL made with their text: that text is your own code's choice, never a user's, as [tokens](parameters-and-tokens.md#tokens) says.  Nor does the method of a query with a [list parameter](parameters-and-tokens.md#list-parameters), whose SQL is the query's own with a parameter for each value.

A null `connection` or `parameters` throws `ArgumentNullException`.  A method with a list parameter throws `ArgumentException` when its query would send more parameters than its database takes, and `InvalidOperationException` when a list changed after the method counted it, as [list parameters](parameters-and-tokens.md#list-limit) say.  The names of the arguments a query method can have, `connection`, `parameters`, `transaction`, `commandTimeout` and `cancellationToken`, are not names a token can have ([SQLSRC275](diagnostics.md#sqlsrc275)).  What the method returns depends on the query's result shape, as [what each shape returns](#method-shapes) says.

<a id="method-connection"></a>

#### The connection

The method opens the connection when its `State` is `Closed`, and closes it again before it returns or throws.  A method that [streams its rows](settings.md#streaming-rows) opens it when the loop asks for the first row, and closes it again when the loop ends, and the connection is busy with the query until then, so the loop's body cannot run another command on it, on PostgreSQL or on a SQL Server connection without `MultipleActiveResultSets`.  A connection that was not closed is left as it was: it stays open, in the transaction and with the session's settings it had, unless, on SQL Server, an error that the method does not report rolls the transaction back, as [what a method reads and throws](sqlserver.md#method-results) says.  So to run several queries on one open connection, or in a transaction, open the connection yourself.

**Give the method the transaction.**  The method gives its command the `transaction` it is given, or none when it is given null.  On SQL Server, Microsoft.Data.SqlClient refuses a command on a connection that has a transaction the command was not given, and throws `InvalidOperationException` ("... requires the command to have a transaction when the connection assigned to the command is in a pending local transaction ...") before anything is sent.  Npgsql runs such a command in the connection's transaction.  A SQL Server method given a Microsoft.Data.SqlClient transaction that has completed, committed or rolled back, throws `InvalidOperationException` and runs nothing, where Microsoft.Data.SqlClient would run its command outside any transaction: see [what a method reads and throws](sqlserver.md#method-results).  Pass the transaction to every method you call inside one, on both: see [the recipe](recipes.md#queries-in-a-transaction).

<a id="method-shapes"></a>

### What each shape returns

The [result shape](sql-files.md#result-shapes) after the query's name says what the method does with the rows.  Without one, a query that returns rows is `many`, and one that returns none is `rowcount`.

What a query method returns, at either output, depends on the query's result shape:

| Shape | Returns |
|----|----|
| `many` | `Task<C>`, where `C` is the [collection type](settings.md#setting-collection-type) of the output model: an array by default, `Task<ListFromDto[]>`.  For the collection type `IAsyncEnumerable`, `IAsyncEnumerable<TDto>`, with no `Task` |
| `one` | `Task<TDto>`, the output model |
| `one-optional` | `Task<TDto?>` |
| `none` | `Task` |
| `rowcount` | `Task<int>` |

- **`many`** reads every row into the [collection type](settings.md#setting-collection-type), an array by default, and complete when the task completes, or, for the collection type `IAsyncEnumerable`, gives each row to the caller's loop as it is read: see [streaming rows](settings.md#streaming-rows).  Unless the query has [`array-pool=off`](settings.md#setting-generator-parameters), the rows of every collection type but `ImmutableList<T>`, `IImmutableList<T>` and `IAsyncEnumerable<T>`, which holds no rows, are read into buffers borrowed from .NET's shared array pool, so that, once the pool holds buffers of the sizes it needs, the method allocates no collection for up to about 1,000 rows but the one it returns.
- **`one`** returns the one row.  It throws [`UnexpectedRowCountException`](#unexpected-row-count) when the query returns no rows, or more than one: it reads a second row to see whether there is one.
- **`one-optional`** returns the row, or null when there is none, and throws the same exception for more than one.
- **`none`** runs the statement and returns nothing.
- **`rowcount`** runs the statement and returns the count the driver reports: the rows inserted, updated or deleted, those of each statement added up when the query has several, and `-1` for a `SELECT`.  The rows of a `RETURNING` or `OUTPUT` clause are counted.  On PostgreSQL a `WITH` whose last statement is a `SELECT` counts as a `SELECT`, so it is `-1` though its `WITH` updates rows.  On SQL Server, `SET NOCOUNT ON` makes it `-1`, and when a query without parameters sets it, every later count on the connection too: see [known issues](#known-issues).

`none` and `rowcount` read no rows, so they suit an `INSERT ... RETURNING` whose rows you do not want.  `one`, `one-optional` and `many` on a query that returns no rows are [SQLSRC272](diagnostics.md#sqlsrc272).  A SQL Server query of several statements returns the rows of its first result set, as its [model](sqlserver.md#what-can-be-described) has them, and its other statements still run: an error that one of them raises is thrown by every method, which reads the rest of the batch before it returns, and may have rolled back the transaction the method was given, depending on the error and on `XACT_ABORT`.  When the method throws an exception of its own first, such as `UnexpectedRowCountException`, or reaches an error that the database raises for a row or for a statement after the rows and that leaves the transaction open, such as a divide by zero at a row, and a later error then rolls the transaction back, it throws [`TransactionRolledBackException`](#transaction-rolled-back): see [what a method reads and throws](sqlserver.md#method-results).

<a id="collection-methods"></a>

### Collection methods

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

The [generator parameter](settings.md#setting-generator-parameters) `collection-method` gives a query a second method, its collection method: an overload of its query method that takes a collection of the query's input models, runs the query once for each of them, the items, in a transaction of its own by default, and returns what every item returned, in the order of the items.  It sends the items 1,000 at a time, each 1,000 in one round trip to the database, where a loop that calls the query method takes a round trip for each.  Above, `AddUser` is a repository's.  `-- generator: collection-method` in a file header, or `"generatorParameters": ["collection-method"]` in a settings file, gives one to every query it reaches.

What it returns depends on the query's [result shape](sql-files.md#result-shapes), as a query method's does, with the rows of every item:

| Shape | Returns |
|----|----|
| `many` | `Task<C>`: every row of every item, in the order of the items, in the query's [collection type](settings.md#setting-collection-type) `C`, an array by default.  For the collection type `IAsyncEnumerable`, an array, since a collection method holds its rows and never streams them |
| `one` | `Task<C>`: the row of each item, in the order of the items.  An item that returns no rows, or more than one, throws [`UnexpectedRowCountException`](#unexpected-row-count) |
| `one-optional` | `Task<C>`: the row of each item that returns one, in the order of the items, so it can hold fewer rows than there are items.  An item that returns more than one throws `UnexpectedRowCountException` |
| `none` | `Task` |
| `rowcount` | `Task<int>`: the counts of every item, added up.  An item whose count is not reported, as under SQL Server's `SET NOCOUNT ON`, adds 0 |

Its rows are read as a `many` method reads them, through .NET's shared array pool unless the query has `array-pool=off`.

- **A query gets one when it gets a query method and has parameters.**  `collection-method` is off by default.  A query whose output is `repository` or `methods` gets its collection method beside its query method, and one that gets no query method, as when the method has an error, gets neither.  A query without parameters gets none, and no error, so a folder that turns `collection-method` on keeps its queries without parameters as they are.  Under the output `sql` or `models` the parameter does nothing.  A [`transaction`](#collection-transactions) that names an isolation level the query's database does not have is [SQLSRC297](diagnostics.md#sqlsrc297), which leaves the query its query method alone.
- **It is an overload of the query method**, with its name, `Async` included under `async-method-suffix`, its visibility and its place, and the same arguments in the same order, but `IEnumerable<TParams> parameters`, of the query's input model, in place of `TParams parameters`.  A `methods` method still takes the connection, `transaction`, `commandTimeout` and `cancellationToken`, and a repository's takes `cancellationToken`; the repository's interface gets it beside the query method.  A [token](parameters-and-tokens.md#tokens) is one `string` argument, as in the query method, and its text is used for every item.  Once a query has one, a call of its query method that passes `new(...)`, `null` or `default` as the input model no longer compiles: either method takes it, so the call is ambiguous, the compiler's error CS0121.  Name the input model's type, as in `new AddUserParams("Ada", email)` or `default(AddUserParams)`.
- **The collection is read once**, as the method goes, and never held whole, so it can be a LINQ query or an iterator of your own.  A null `parameters` throws `ArgumentNullException` before anything runs.  An empty collection returns an empty collection, `0` or a completed task, and opens no connection and begins no transaction.
- **Each item is checked as the method reads it.**  A null item throws `ArgumentNullException`, "Item 1499 of the collection is null.", with the item's position, counted from 0, and an item's [list parameter](parameters-and-tokens.md#list-limit) throws as the query method's does.
- **The items run 1,000 at a time.**  The method reads and checks up to 1,000 items, runs them, and only then reads the next 1,000.  So a bad item is found only after every round trip before its own has run: in the method's own transaction those items are rolled back, and under `transaction=off` or in the caller's transaction they stay.  To refuse the whole collection for one bad item there, check the items before the call.
- **`commandTimeout` is the timeout of each 1,000 items**, not of the whole method, and is otherwise the query method's.  `cancellationToken` cancels opening the connection, running the items and reading their rows, and the method's own transaction is rolled back; what each driver throws is on the pages of [PostgreSQL](postgresql.md#collection-methods) and [SQL Server](sqlserver.md#collection-methods).
- **The connection** is the query method's: a `methods` method opens a closed connection and closes it again before it returns or throws, as [the connection](#method-connection) says, and a repository's gets one from its [connection source](#repository-connection) for the call.

<a id="collection-transactions"></a>

#### Transactions

```sql postgres
-- name: ImportReadings -> none
-- generator: collection-method transaction=serializable
INSERT INTO readings (sensor_id, taken_at, value)
VALUES (@sensorId, @takenAt, @value);
```

The generator parameter `transaction` says which transaction a collection method begins of its own.  It changes nothing for a query without `collection-method`.

| `transaction` | The collection method |
|----|----|
| `on`, the default | Begins a transaction at the database's default isolation level, commits it after the last item, and rolls it back when anything throws |
| `read-uncommitted`, `read-committed`, `repeatable-read`, `serializable` or `snapshot` | The same, at that isolation level, which holds for that transaction alone: the connection's later statements run at the level they ran at before the call.  PostgreSQL has no `read-uncommitted` and no `snapshot`, which are [SQLSRC297](diagnostics.md#sqlsrc297) there, and SQL Server's `snapshot` needs the database to allow it: see [SQL Server](sqlserver.md#collection-methods) |
| `off` | Begins none: the items run as the database runs statements outside a transaction, so a failure leaves some of them in it, as the pages of [PostgreSQL](postgresql.md#collection-methods) and [SQL Server](sqlserver.md#collection-methods) say |

- **Its own transaction is all or nothing.**  It holds every item, and when anything throws, an item's database error, an exception of the method's own such as `UnexpectedRowCountException`, a null item or a cancellation, it is rolled back and nothing of any item is left.  When the database has already rolled it back, as SQL Server does at some errors, the method throws the first exception all the same.
- **The caller's transaction is used as it is.**  A `methods` method given a `transaction` runs every item in it, whatever `transaction` says, and neither commits it nor rolls it back: after an exception, what ran is still in it, and whether to roll it back or commit it is yours to decide.  On SQL Server, a transaction that has completed is refused, as for a query method: see [the connection](#method-connection).
- **So is an ambient transaction.**  When `System.Transactions.Transaction.Current` is set as the method starts, as inside a `TransactionScope`, the method begins none, whatever `transaction` says, and runs in that one, which a connection opened inside the scope takes part in.  A connection that is not in it, such as one opened before the scope and never enlisted, or one whose connection string turns enlisting off, runs the items in no transaction at all, as under `transaction=off`.  That is the way to put a repository's collection method in a larger transaction, since it takes no `transaction`.

<a id="collection-failures"></a>

#### What it throws

- **A database error at an item** is thrown as [`CollectionItemException`](#collection-item-exception), whose `ItemIndex` is the item's position, counted from 0, and whose `InnerException` is the driver's exception, a `PostgresException` or a `SqlException`.
- **An error that no item raised is the driver's own.**  One that the database raises after every item of a round trip has run, such as that of a PostgreSQL constraint declared `DEFERRABLE INITIALLY DEFERRED`, which is checked at the commit, is thrown as the driver's exception, as it is.
- **Every other exception is the query method's**: `UnexpectedRowCountException`, whose message and `ItemIndex` name the item, a null in a column whose property cannot be null, a converter's exception.
- **On SQL Server**, a method that reads rows and is given a transaction can throw [`TransactionRolledBackException`](#transaction-rolled-back), and a query whose statements return more than one result set throws `InvalidOperationException`: see [SQL Server](sqlserver.md#collection-methods).

<a id="reading-rows"></a>

### How a row is read

- **Each column is read by its position** into the property the output model has for it, so its name does not matter: a `!` or `?` at the end of an alias works as well as a [`-- column:` marker](#column-marker).  A record is made with its constructor, a class by setting its properties, and a [sorted](#property-order) model as well as any other.
- **A property that can be null** is null when its column is.
- **A property that cannot be null is read with no check**, so a null in its column throws, as the driver reports it: Npgsql's `InvalidCastException` "Column 'name' is null.", and Microsoft.Data.SqlClient's `SqlNullValueException` "Data is Null.".  Microsoft.Data.SqlClient gives no error for a null `vector`, so the method checks such a column itself, and throws `InvalidCastException` "Column 'embedding' is null." with the column's name.  A `!` at the end of an alias, a `-- column:` marker that says `not null`, or a column that the tool showed is never null, is so checked the first time a null arrives, and never read as a default value.
- **A JSON `null` is not a null.**  A JSON column that holds the text `null` is read as a value: a `JsonElement` of the kind `Null`, a `JsonDocument` whose root is one, and for a `JsonNode`, a `JsonObject`, a `JsonArray` or a class of yours read through System.Text.Json's serializer, null into a property that can be null, and into one that cannot, the method's own `InvalidCastException`, "Column 'metadata' holds a JSON null.", with the column's name.  A struct of yours read through the serializer is the same: null when its property can be null, and the method's `InvalidCastException` when it cannot.  See JSON on [PostgreSQL](postgresql.md#json) and [SQL Server](sqlserver.md#json), and [a class in a JSON column](settings.md#json-class).
- **Each column is read as its property's C# type**, and each parameter sent as one, unless a converter converts it.  A C# type that a [type override](settings.md#setting-type-overrides), a [column type](settings.md#setting-column-types) or a `-- param:` marker names, with no converter, is given to the driver as it is, which suits a type the driver knows or was taught, and throws the driver's `InvalidCastException` for one it was never taught: see [converters](settings.md#converters).  A class of yours in a column that holds JSON is read and sent through System.Text.Json's serializer instead: see [a class in a JSON column](settings.md#json-class).  A type that the query methods convert themselves is not given to the driver either, whichever place chose it: on SQL Server NodaTime's, NetTopologySuite's and System.Text.Json's, and on Npgsql 8 and 9 `JsonNode`, `JsonObject` and `JsonArray`, as [SqlSource's own conversions](settings.md#converters) says.
- **A column or parameter whose type a library gives**, by the [library types](settings.md#setting-library-types), is read and sent on PostgreSQL by Npgsql: NodaTime's and NetTopologySuite's through their plugins, which your application registers on its data source, as [what a method reads and throws](postgresql.md#method-results) says, and without which the driver throws `InvalidCastException`, and System.Text.Json's `JsonElement` with nothing to register.  On SQL Server it is read and sent by the query method itself, with nothing to register: NodaTime's and System.Text.Json's are read as the type that Microsoft.Data.SqlClient reads and converted, and converted before they are sent, as [NodaTime on SQL Server](sqlserver.md#nodatime) and [JSON on SQL Server](sqlserver.md#json) say, and NetTopologySuite's is read and written as [NetTopologySuite on SQL Server](sqlserver.md#nettopologysuite) says.  Either way, a [converter](settings.md#converters) of yours of the direction it needs is used instead, but for NetTopologySuite's on SQL Server.
- **A column whose type a type override or a column type chose, and that has a [converter](settings.md#converters)**, is read as the type the driver reads its database type as, and that value given to the converter, whose result is the property's value.  A null never reaches the converter: a property that cannot be null throws as above, before the converter is called.

<a id="method-parameters"></a>

### How a parameter is sent

- **Each parameter is sent by its name**, without its `@`, with the value of its property of the input model, in the order of the query's [parameter list](parameters-and-tokens.md#query-parameters).  Both drivers match a name ignoring case, as SqlSource does, so `@Id` and `@id` are one parameter, and one value fills both; under `mssql`, a second spelling is the warning [SQLSRC121](diagnostics.md#sqlsrc121).  A parameter written in snake_case, `@user_id`, is filled from its property `UserId` like any other.
- **A null value is sent as the database's `NULL`**: that of a property that can be null, and that of a property of a reference type that cannot be, which a caller can set to null all the same.
- **A [list parameter](parameters-and-tokens.md#list-parameters) is sent as one parameter for each value**, each named after the list and sent as a parameter of the values' type, padded to one of a few sizes unless the query has `list-padding=off`.  A null list is sent as an empty one, and a list that the query tests for null also sends a parameter of its own name, null when the list is null.
- **A parameter whose type a type override or its `-- param:` marker chose, and that has a [converter](settings.md#converters)**, is given to the converter, and the value it returns is sent.  A null is sent as `NULL`, without the converter.  A class of yours in a parameter that holds JSON is given to System.Text.Json's serializer in the same way, and its text sent: see [a class in a JSON column](settings.md#json-class).
- **Each parameter is sent as the database type it was described with**, or that its [`-- param:` marker](parameters-and-tokens.md#param-marker) declares, and not as a type chosen from the C# type of its value.  On SQL Server it is sent with the type's length, precision and scale.  See how a parameter is sent on [PostgreSQL](postgresql.md#method-parameters) and on [SQL Server](sqlserver.md#method-parameters).

<a id="unexpected-row-count"></a>

### `UnexpectedRowCountException`

```csharp
// -- name: GetActiveUser -> one
try
{
    var user = await users.GetActiveUser(new GetActiveUserParams(email));
}
catch (UnexpectedRowCountException exception)
{
    // exception.QueryName is "GetActiveUser"
}
```

The method of a query whose shape is `one` throws `UnexpectedRowCountException` when the query returns no rows or more than one, and that of a query whose shape is `one-optional` when it returns more than one.  Its message names the query and what it returned, "The query 'GetActiveUser' returned more than one row.", and its `QueryName` the query.  A [collection method](#collection-methods) throws it for an item, and its message also names the item, "The query 'AddUser' returned no rows for item 1.", and its `ItemIndex` is the item's position, counted from 0; a query method's `ItemIndex` is null.

- **It derives from `InvalidOperationException`**, which LINQ's `Single` throws, so code that catches that catches it too.
- **It is in the namespace `SqlSource`**, generated into every project that uses SqlSource, unless the project uses the copy of the one project it references that has it: see [projects that share internals](#projects-that-share-internals).  It is `internal`, unless the project sets [`SqlSourcePublicExceptions`](settings.md#public-exceptions), so another assembly catches it by type only when it sees it, and as `InvalidOperationException` otherwise: see [known issues](#known-issues).

<a id="transaction-rolled-back"></a>

### `TransactionRolledBackException`

```csharp
// -- name: GetActiveUser -> one, at the output methods, on SQL Server
try
{
    var user = await connection.GetActiveUser(new GetActiveUserParams(email), transaction: transaction);
}
catch (TransactionRolledBackException exception)
{
    // exception.QueryName is "GetActiveUser", and exception.InnerException is the UnexpectedRowCountException.
    // The transaction has completed: begin another to go on.
}
```

A SQL Server method of the shape `one`, `one-optional` or `many` that takes a connection throws `TransactionRolledBackException` when it was given a transaction, threw an exception of its own or reached an error that the database raised for a row or for a statement after the rows, and an error that the rest of its query raised then rolled the transaction back.  The first exception is the `InnerException`: `UnexpectedRowCountException`, a null in a column whose property cannot be null, a converter's exception, or the `SqlException` of the database's error, such as a divide by zero at a row.  The later error, the one that rolled the transaction back, is not kept.  Its message names the query, "The transaction was rolled back by an error that the query 'GetActiveUser' raised after the inner exception was thrown.", and its `QueryName` the query.  Without a transaction, or with the transaction still open, the method throws the first exception as it is, and a database error that itself rolled the transaction back, such as a `THROW` under `SET XACT_ABORT ON`, is thrown as its `SqlException`.  A Microsoft.Data.SqlClient transaction that had completed before the call is refused before the query runs, with `InvalidOperationException`, so this exception says that the method's own query rolled the transaction back.  A transaction of a library that wraps Microsoft.Data.SqlClient's and keeps its `Connection` set after it completes is neither refused nor reported: see [what a method reads and throws](sqlserver.md#method-results).

- **A repository method never throws it**, since it takes no transaction.
- **A [collection method](#collection-methods) given a transaction throws it by the same rule**, when it reads rows: its `InnerException` is the method's own exception, or the [`CollectionItemException`](#collection-item-exception) of the item whose error came first.  See [SQL Server](sqlserver.md#collection-methods).
- **It derives from `InvalidOperationException`**, so code that catches that catches it too.
- **It is in the namespace `SqlSource`, and as visible as `UnexpectedRowCountException`**, beside which it is generated, so another assembly catches it by type only when it sees it, and as `InvalidOperationException` otherwise: see [known issues](#known-issues).

<a id="collection-item-exception"></a>

### `CollectionItemException`

```csharp
// -- name: AddUser -> one, with -- generator: collection-method, on SQL Server
try
{
    await users.AddUser(newUsers);
}
catch (CollectionItemException exception) when (exception.InnerException is SqlException { Number: 2627 })
{
    // exception.ItemIndex is the position of the user whose key is taken, counted from 0.
    // The method's own transaction was rolled back, so no user was added.
}
```

A [collection method](#collection-methods) throws `CollectionItemException` when the database reports an error at one of its items.  Its `InnerException` is the driver's exception, unchanged, its `ItemIndex` the item's position, counted from 0, and its `QueryName` the query.  Its message names the query and the item, then gives the database's message: "The query 'AddUser' failed at item 2: ...".  Where your code would catch the driver's exception, catch this one and test its `InnerException`, as above.

- **It derives from `System.Data.Common.DbException`**, the base of `SqlException` and `PostgresException`, so code that catches `DbException` catches it too.  Its `SqlState`, `IsTransient`, `BatchCommand` and `ErrorCode` are those of its inner exception, so a retry policy that tests `IsTransient` works unchanged.  When it comes from [another project](#projects-that-share-internals), that project targets .NET 6 or later, or has such a target among its `TargetFrameworks`: one built for `netstandard2.0` or `netstandard2.1` alone cannot pass on the first three, and your project gets [SQLSRC038](diagnostics.md#sqlsrc038).
- **It wraps only a database error at an item.**  An exception of the method's own, such as `UnexpectedRowCountException`, and an error that no item raised, are thrown as [what a collection method throws](#collection-failures) says.
- **It is in the namespace `SqlSource`, and as visible as `UnexpectedRowCountException`**, beside which it is generated, so another assembly catches it by type only when it sees it, and as `DbException` otherwise: see [known issues](#known-issues).

<a id="projects-that-share-internals"></a>

## Projects that share internals

```xml
<!-- App.csproj, which uses SqlSource -->
<ItemGroup>
    <InternalsVisibleTo Include="App.Tests" />
</ItemGroup>
```

```csharp
// App.Tests, which may use SqlSource too
try
{
    await users.GetActiveUser(new GetActiveUserParams(email));
}
catch (UnexpectedRowCountException exception)
{
    // the exception that App's method threw
}
```

A test project usually sees the internals of the project it tests, through `InternalsVisibleTo`.  Both can use SqlSource.

- **The attribute, `SqlSourceConverterAttribute` and `SqlSourceConnectionAttribute`** are each project's own, and the two copies of each do not conflict.  The same holds for `SqlSource.NodaTimeConversions`, `SqlSource.NetTopologySuiteConversions` and `SqlSource.JsonConversions`, the classes that the query methods of a project call to read and send [NodaTime's types on SQL Server](sqlserver.md#nodatime), [NetTopologySuite's](sqlserver.md#nettopologysuite), and System.Text.Json's types on [SQL Server](sqlserver.md#json) and on [Npgsql 8 and 9](postgresql.md#json), when the query methods of both projects call one.
- **The exceptions, `UnexpectedRowCountException`, `TransactionRolledBackException` and `CollectionItemException`, are the other project's.**  A project that sees them in a project it references gets none of its own, and its query methods throw the other project's.  A project referenced only under an extern alias (`Aliases` on its reference, without `global`) does not count: the project gets its own.  So the test project catches what a method of either project throws by its type, and so does a project that sees the other's internals and does not use SqlSource.
- **They are `internal`**, unless the project that has them sets [`SqlSourcePublicExceptions`](settings.md#public-exceptions) to `true`, which makes them `public`, so that any project that references it catches them by type.  A project that uses another's exceptions gets none of its own, whatever it sets.
- **A project that sees them in two projects it references** gets [SQLSRC022](diagnostics.md#sqlsrc022), as an app does that sees the internals of two data projects that both use SqlSource.  Put them in one shared project instead, which grants `InternalsVisibleTo` to every project that catches them or uses SqlSource and sees the others' internals, test projects among them: see [the recipe](recipes.md#share-the-exceptions).  A project that sees them in a project that uses another version of SqlSource gets [SQLSRC023](diagnostics.md#sqlsrc023), and a project on .NET 6 or later that sees them in a project that targets an older framework alone, such as `netstandard2.0`, gets [SQLSRC038](diagnostics.md#sqlsrc038).

The compiler gives no warning (CS0436) about the types that are each project's own, in your code or in the generated code, also when warnings are errors, so nothing has to be added to `NoWarn`.  Each project sees only its own copies, and a project that sees the other's internals and does not use SqlSource does not see them at all.

<a id="known-issues"></a>

## Known issues

- **The tool reads the attribute without compiling your code.**  `sqlsource describe` finds `[SqlSourceGenerate]` by reading your C# files as text, so `Source` must be written out where the attribute is, as a string: `Source = "Queries"`.  A constant, `nameof`, a concatenation or a cast there compiles, and the tool reports [SQLSRC208](diagnostics.md#sqlsrc208) and describes nothing for the type.  The build then reports [SQLSRC241](diagnostics.md#sqlsrc241) for each of the type's queries that needs an entry in its sidecar and has none, or, for the file, [SQLSRC209](diagnostics.md#sqlsrc209) under a dialect that has no database to ask, such as `ansi`.  Two more things follow from the same reading.  An alias of the attribute (`using Gen = SqlSource.SqlSourceGenerateAttribute;` and `[Gen]`) works in the build and is not seen by the tool, so the type's queries are not described, and the build reports SQLSRC241 for them in the same way.  An attribute inside `#if` is read with the compilation constants the tool gets from your project, which may not be those of the configuration you build.  Write the attribute under its own name, and outside `#if` where you can.
- **An input model binds through Dapper only when the query writes its parameters as the model's properties are named.**  Dapper sends a property of a parameter object only to a parameter that the SQL writes as `@` and the property's name, ignoring case but not `_`.  So the property `AccountId` binds to `@accountId`, and a query that writes `@account_id` gets no value from it.  Write the query's parameters in camelCase, or give Dapper the parameters yourself, as in `new { account_id = args.AccountId }`: see [the recipe](recipes.md#models-through-dapper).
- **`UnexpectedRowCountException`, `TransactionRolledBackException` and `CollectionItemException` are caught by their type only where they are seen.**  A project that sees another's exceptions, because the other grants it `InternalsVisibleTo` or sets [`SqlSourcePublicExceptions`](settings.md#public-exceptions), catches them by type.  A project that uses SqlSource and sees them in two projects it references gets [SQLSRC022](diagnostics.md#sqlsrc022), which [one shared project](recipes.md#share-the-exceptions) fixes; a project that does not use SqlSource and sees two copies gets the compiler's error that the name is ambiguous where it names one, which the same shared project fixes.  Anywhere else, as in an assembly that calls a `public` method of a project whose exceptions are `internal`, catch `InvalidOperationException`, from which the first two derive, and `DbException`, from which `CollectionItemException` derives.
- **A `cref` to the exceptions can fail in a project that uses another project's.**  A project that uses the `UnexpectedRowCountException`, `TransactionRolledBackException` and `CollectionItemException` of a project it references, and also references another project or package that uses SqlSource and whose exceptions it cannot see, gets a compiler warning at a `cref` in its own documentation comments that names one of the exceptions or one of its members, when it builds a documentation file: CS0419 for the exception itself, such as `<exception cref="UnexpectedRowCountException"/>`, and CS1574 for a member, such as `<see cref="UnexpectedRowCountException.QueryName"/>`.  Under warnings as errors the build fails.  Write the reference as the documentation ID of what it names, which the compiler takes as it is: `T:` and the full name for an exception, as `cref="T:SqlSource.UnexpectedRowCountException"`, `cref="T:SqlSource.TransactionRolledBackException"` or `cref="T:SqlSource.CollectionItemException"`, and `P:` for a property or `M:` for a constructor, as `cref="P:SqlSource.UnexpectedRowCountException.QueryName"`.
- **A query method with parameters works only on a connection whose commands take its driver's own parameters.**  It works on the connections of the drivers, such as `NpgsqlConnection`, the one an `NpgsqlDataSource` gives, and `SqlConnection`, and so on those a repository makes from a connection string or a data source.  A `DbConnection` that wraps one of them, and whose commands create or accept only parameters of their own type, fails before the query is sent: on PostgreSQL with the wrapper's own exception, typically an `InvalidCastException`, and on SQL Server with `InvalidCastException`, "Unable to cast object of type '...' to type 'Microsoft.Data.SqlClient.SqlParameter'.".  Give the method the driver's connection that the wrapper holds, from a repository's factory method or a connection it borrows too, or give the query the output `models` and send its parameters through the wrapper yourself.
- **A collection method works fully only on the driver's own connection.**  On a `DbConnection` that wraps the driver's, it can fail where the query method works, in three ways.  A wrapper that cannot run a batch of commands, as most cannot, makes every collection method throw `NotSupportedException`, "Specified method is not supported.", before any item runs.  A wrapper whose batch commands create or accept only parameters of their own type fails as the query method does, with `InvalidCastException`, before the items are sent.  And a wrapper whose batches hold commands of its own runs the items, but a database error at an item is thrown as the driver's own exception, a `PostgresException` or a `SqlException`, not as [`CollectionItemException`](#collection-item-exception), so nothing names the item.  Give the method the driver's connection that the wrapper holds, as for the query method above.
- **A PostgreSQL repository whose source is a connection string reads no plugin's types.**  The connection it makes, a new `NpgsqlConnection` of the string, has none of the setup of your application's data source: no plugin, such as NodaTime's or NetTopologySuite's, and none of your own mappings, `MapEnum<T>()` or `MapComposite<T>()`.  So a repository method that reads or sends a type that needs one compiles, and throws Npgsql's `InvalidCastException` when it runs, such as "Reading as 'NodaTime.Instant' is not supported".  Give the repository the data source your application builds, an `NpgsqlDataSource`, which Npgsql itself recommends over a connection string, or a factory method that opens a connection from it: see [the connection](#repository-connection).  A SQL Server repository is not affected, since its methods convert NodaTime's, NetTopologySuite's and System.Text.Json's types themselves.
- **On SQL Server, `rowcount` returns `-1` once `SET NOCOUNT ON` has run on the connection's session.**  A query without parameters, or a command of your own without them, that runs `SET NOCOUNT ON` sets it for the session, not for the statement: on a connection you opened, every later `rowcount` method returns `-1` until the connection is closed and its session reset.  A query with parameters, which Microsoft.Data.SqlClient runs through `sp_executesql`, sets it for itself alone, and only its own count is `-1`.  Run `SET NOCOUNT OFF` after it, or do not set it in a query without parameters on a connection that runs a `rowcount` method.
- **A project that declares `Microsoft.CodeAnalysis.EmbeddedAttribute` itself compiles only when its declaration can be joined to SqlSource's.**  SqlSource adds `internal sealed partial class EmbeddedAttribute : System.Attribute`, in the namespace `Microsoft.CodeAnalysis`, to every project.  A declaration of that name in your code, or in code that another package adds to your project, compiles when it is an `internal partial class` that derives from `System.Attribute` or names no base class, `sealed` or not.  Any other fails the build with the compiler's errors, which SqlSource cannot report first: CS0260 when it is not `partial`, CS0262 when it is `public`, CS0263 when it derives from another class, CS0441 when it is `static`, and CS0261 when it is a record, each with more errors after it.  Make the declaration an `internal partial class`, or remove it.
