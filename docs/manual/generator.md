# The generator

The generator is the part of the `SqlSource` package that runs in the compiler.  It reads the `.sql` files of your project and writes C# beside your own code.  For each described query it writes a type for its parameters and a type for its rows, its [models](#models), and, under the default [output](settings.md#setting-output), `codegen`, a [method](#query-methods) that runs the query.  A query whose output is `sql` or `models` gets a string constant instead, or a method that returns its SQL where it has [tokens](concepts.md#token), for your own code to run.  It needs no database, in a project that targets .NET 8 or later with C# 12 or later ([SQLSRC003](diagnostics.md#sqlsrc003), [SQLSRC012](diagnostics.md#sqlsrc012)).  It needs the package, one attribute, and one choice: a query whose output is `models` or `codegen`, the default, must be described by the [tool](tool.md), and the build reports one that has no current entry in its sidecar, while a query whose output is `sql` needs nothing more.

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
using System.Data.Common;
using SqlSource;

[SqlSourceGenerate]
public partial class UserRepository(DbConnection connection)
{
    public Task<GetUserDto?> Get(int id) =>
        connection.GetUser(new GetUserParams(id));

    public Task<ListFromDto[]> List() =>
        connection.ListFrom("users", "name DESC");
}
```

`GetUser` and `ListFrom` are the generated methods of the two queries, extension methods of `DbConnection` in the generated class `UserRepositoryExtensions`.  Each opens the connection when it is closed, sends the query's parameters, runs it and reads its rows into its output model: `GetUser` one row or null, as its [result shape](sql-files.md#result-shapes) `one-optional` says, and `ListFrom` every row, the default for a query that returns rows.  `ListFrom` takes the text of each token, and its token defaults, `users` and `name`, are only samples for [describing](concepts.md#describe) the query.  `GetUserParams`, `GetUserDto` and `ListFromDto` are the models of the two queries, generated from what the tool recorded when it described them.  A query that is renamed or removed is a compile error where it is used, and so is a column that is renamed or removed once the query is described again.

With the output `models`, the same queries give `Sql.GetUser`, a [constant](#constants) that holds the text of the first query, and `Sql.ListFrom("users", "name DESC")`, a [token method](#token-methods) that returns the text of the second with its tokens filled in, and their models, for Dapper, ADO.NET or any other library to run: see [the recipe](recipes.md#dapper-and-adonet).

<a id="attribute"></a>

## The attribute

`[SqlSourceGenerate]` marks a type that the generator adds to.  The package puts the attribute, its enums, the [`[SqlSourceConverter]`](settings.md#converters) attribute and the exceptions that a query method throws, [`UnexpectedRowCountException`](#unexpected-row-count) and [`TransactionRolledBackException`](#transaction-rolled-back), in your project, as internal types in the namespace `SqlSource`, so the code above needs `using SqlSource;` and nothing else.

```csharp
[SqlSourceGenerate(Path = "Queries", SqlLocation = SqlLocation.Direct, Parameters = "keep-comments")]
internal static partial class Reports;
```

- **It goes on a class, a struct, a record or a record struct.**  The type may be static, generic, or nested in other types.
- **The type and every type that contains it must be `partial`** ([SQLSRC001](diagnostics.md#sqlsrc001)), and none may be `file` ([SQLSRC002](diagnostics.md#sqlsrc002)).  The generator adds its members in a second declaration of each.
- **A type with an error gets no members**, and asks for no models.  The code that uses them then fails to compile too, so fix the first error the build reports.

Every property of the attribute is a [setting](settings.md), documented there with the other places it can be written and the rule for which place wins:

| Property | Sets |
|----|----|
| `Path` | Which `.sql` files belong to the type: [Path](settings.md#setting-path) |
| `SqlLocation` | Where the generated members go: [Where the SQL goes](settings.md#setting-sql-location) |
| `NestedClass` | The name of the class that holds the members under `SqlLocation.Nested`: [Name of the nested class](settings.md#setting-nested-class) |
| `Parameters` | Generator parameters for the type's queries: [Generator parameters](settings.md#setting-generator-parameters) |
| `Output` | What is generated for the type's queries: [Output](settings.md#setting-output) |
| `InputModelSuffix`, `OutputModelSuffix` | What ends the name of a model: [Suffix of a model's name](settings.md#setting-model-suffix) |
| `ModelNamespace` | The namespace of the models: [Namespace of the models](settings.md#setting-model-namespace) |
| `InputModelType`, `OutputModelType` | The shape of a model: [Shape of a model](settings.md#setting-model-type) |
| `CollectionType` | The type that holds many rows: [Collection type](settings.md#setting-collection-type) |
| `MethodLocation` | Where the methods that run the queries go: [Where the methods go](settings.md#setting-method-location) |

Each property changes what the generator writes.  `Output` also decides which queries the tool describes and the build checks against their sidecar.  `SqlLocation` and `NestedClass` place the constants and token methods of the queries whose output is `sql` or `models`, and `MethodLocation` and `CollectionType` are for the [methods](#query-methods) of the queries whose output is `codegen`.

<a id="files-of-a-type"></a>

## Which files belong to a type

```text
Repository/
    UserRepository.cs      [SqlSourceGenerate] on UserRepository
    Users.sql              belongs to UserRepository
    Orders.sql             belongs to UserRepository
    Reports/
        Monthly.sql        not searched: it is in a subfolder
```

A type gets the files that its [`Path`](settings.md#setting-path) names, by the rules and with the errors given there.  What follows is what the generator does with the files.

- **A type's files are one set.**  A query name is used once across all of them: a second use is [SQLSRC008](diagnostics.md#sqlsrc008), and the file with the second one gets no members and no models for that type.
- **Two types may claim one file**, and each gets its own members from it, though two types of one namespace can give one method twice: see [where the methods go](#method-location).  They can ask for different things: each type's attribute applies to its own members only, and to the models it asks for, which two types share when they are the same ([shared models](#shared-models)).
- **A `.sql` file that no type claims is ignored**: see [Path](settings.md#setting-path).
- **A file with an error in how it is written produces no members** until it is fixed: see [`.sql` files](sql-files.md).  The other files of the type are unaffected.  An error about describing a query or about its sidecar, such as [SQLSRC241](diagnostics.md#sqlsrc241), leaves the query without models until it is fixed, and so without a [method](#which-methods) under `codegen`; under `models` it keeps its constant or token method.

<a id="locations"></a>

## Where the members go

```csharp
[SqlSourceGenerate(Output = GeneratorOutput.Models)]    // Nested, the default
public partial class UserRepository
{
    public string Get() => Sql.GetUser;
}

[SqlSourceGenerate(Output = GeneratorOutput.Models, SqlLocation = SqlLocation.Direct)]
public static partial class UserQueries;

// elsewhere
var sql = UserQueries.GetUser;
```

This section is about the constants and token methods of the queries whose output is `sql` or `models`.  A query whose output is `codegen` has a method in their place, which `SqlLocation` does not move: see [where the methods go](#method-location).

[`SqlLocation`](settings.md#setting-sql-location) chooses between a private class `Sql` nested in the type (`Nested`, the default) and the type itself (`Direct`).  Its section says what each means.  [The nested class](settings.md#setting-nested-class) can have another name, in a settings file for the types of a folder, or on the attribute for one type.  What to know when you choose:

- Use `Nested` for queries that one class runs, and `Direct` for a type whose job is to hold queries that other code uses.
- With `Nested`, the type cannot have a member or a type parameter named like the nested class, and cannot have that name itself ([SQLSRC007](diagnostics.md#sqlsrc007)).  Rename it, give the nested class another name, or use `Direct`.  The nested class holds the queries whose output is `sql` or `models`, and is not written when every query of the type's usable files is `codegen`.  A type with no queries at all keeps it, empty.
- A query whose output is `sql` or `models` is not named like the class that holds the members: the nested class under `Nested`, `Sql` unless it is named otherwise, and the type's own name under `Direct` ([SQLSRC009](diagnostics.md#sqlsrc009)).  A query whose output is `codegen` has no member there, and may have that name when [its method](#method-location) can.
- A query may share its name with a member that the type inherits, such as `ToString`.  The constant hides it, with no warning in the generated code.

<a id="constants"></a>

## Constants

```csharp
public const string GetUser = "SELECT id, name\nFROM users\nWHERE id = @id;";
```

A query without tokens whose output is `sql` or `models` is a `public const string` with the name of the query.  Its value is the SQL you wrote, with the markers and comments taken out and line endings made `\n`: [what reaches the generated SQL](sql-files.md#generated-sql) lists everything done to it.  A query parameter such as `@id` stays in it as written; you give the database its value when you run the query.

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

A query with a token whose output is `sql` or `models` is a `public static string` method with the name of the query, `ListFrom(string table, string orderBy)`, in place of a constant.  Under `codegen` the query's [method](#query-methods) takes the tokens instead, and runs the query.  Its parameters, and what it returns, are under [tokens](parameters-and-tokens.md#tokens).

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

The summary is the query's `-- summary:` ([summaries](sql-files.md#summaries)), or "The `GetUser` query from `Users.sql`." when it has none.  The remarks hold the SQL of the constant, so a reader sees what a query does without opening the `.sql` file.

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

A [query method](#query-methods) has the same summary and remarks, a line for each of its arguments and one for what it returns, and, for the shapes `one` and `one-optional`, a line for the [exception](#unexpected-row-count) it throws.  On SQL Server, every method also has a line for the `InvalidOperationException` it throws when it is given a transaction that has [completed](sqlserver.md#method-results), and a method of the shapes `one`, `one-optional` and `many` one for [`TransactionRolledBackException`](#transaction-rolled-back).  The extension class of the methods has a summary that names the type.

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
    /// The parameters of the query <c>GetUser</c> from <c>Users.sql</c>.
    /// </summary>
    /// <param name="Id">The parameter <c>id</c>, <c>integer</c>.</param>
    public sealed record GetUserParams(int Id);

    /// <summary>
    /// Loads one user by id.
    /// A row of the query <c>GetUser</c> from <c>Users.sql</c>.
    /// </summary>
    /// <param name="Id">The column <c>id</c>, <c>integer</c>, from <c>public.users.id</c>.  An identity column.</param>
    /// <param name="Name">The column <c>name</c>, <c>text</c>, from <c>public.users.name</c>.</param>
    /// <param name="DeletedAt">The column <c>deleted_at</c>, <c>timestamp with time zone</c>, from <c>public.users.deleted_at</c>.</param>
    public sealed record GetUserDto(int Id, string Name, global::System.DateTime? DeletedAt);
}
```

A model is a type that the generator writes for a described query, from the query's entry in its [sidecar](concepts.md#sidecar): an input model for its parameters, and an output model for a row of its result.  Above, a `public` type in the namespace `App` claims `Users.sql`, and the table `users` has the columns `id`, an identity column, `name` and `email`, both `NOT NULL`, and `deleted_at`, in that order.  The two models are shown together; each is a file of its own among the generated files, named after its full name, such as `App.GetUserDto-Model.g.cs`.

The models are built at every build from the committed sidecars, with no database.  After a change to a query, describe it again (see [sidecar files](tool.md#sidecar-files)): until then its entry is out of date, and the query has no models.

<a id="which-models"></a>

### Which queries get models

A query gets models when its [output](settings.md#setting-output) is `models` or `codegen`, the default, and its entry in its sidecar is current.  Which models it gets depends on the entry:

| The query | Input model | Output model |
|----|----|----|
| Has parameters and returns rows | Yes | Yes |
| Returns rows and has no parameters | No | Yes |
| Has parameters and returns no rows, as an `INSERT` without `RETURNING`, or on SQL Server without `OUTPUT` | Yes | No |
| Has neither | No | No |

- **Rows of no column**, which PostgreSQL gives for `SELECT FROM users`, get an output model with no properties.
- **A query has no models while it has an error**: an entry that is missing or out of date ([SQLSRC241](diagnostics.md#sqlsrc241), [SQLSRC244](diagnostics.md#sqlsrc244)), a column or a parameter with no C# type ([SQLSRC246](diagnostics.md#sqlsrc246), [SQLSRC249](diagnostics.md#sqlsrc249) to [SQLSRC252](diagnostics.md#sqlsrc252), [SQLSRC261](diagnostics.md#sqlsrc261), [SQLSRC269](diagnostics.md#sqlsrc269) and [SQLSRC270](diagnostics.md#sqlsrc270)), a name that gives no property ([property names](#property-names)), or a [`-- column:` marker](#column-marker) that names no column it can set ([SQLSRC260](diagnostics.md#sqlsrc260)).  Under the output `models` its constant or token method is still generated; under `codegen` it has no method either.
- **A query whose output is `sql` gets none.**  When two types claim its file, a type whose output for the query is `sql` asks for no models, and neither does a type with an error of its own, or a type for a file that it leaves out ([SQLSRC008](diagnostics.md#sqlsrc008), [SQLSRC009](diagnostics.md#sqlsrc009)); the other type still gets them.
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

Each of these is an error at the query's name, and the query has no models until it is fixed, nor a method under `codegen`; under `models` its constant or token method is still generated.  Give the column an alias, or rename the parameter, and describe the query again.

- **A column with no name**, which PostgreSQL calls `?column?`, as an expression `price * quantity` without an alias: [SQLSRC253](diagnostics.md#sqlsrc253).  SQL Server gives such a column no name, and the tool reports it when it describes the query, as [SQLSRC266](diagnostics.md#sqlsrc266).
- **A name whose PascalCase is not a C# identifier**: one of underscores alone, one that starts with a digit, or one with a character that an identifier cannot hold, such as `unit-price`: [SQLSRC254](diagnostics.md#sqlsrc254).
- **Two columns, or two parameters, whose properties have one name ignoring case**, as `SELECT u.id, o.id`, or `user_id` beside `userId`: [SQLSRC255](diagnostics.md#sqlsrc255).  Names are compared ignoring case, since Dapper matches a column to a property ignoring case.
- **A name that a record or `object` already has a member of**, `EqualityContract`, `Equals`, `GetHashCode`, `ToString`, `GetType`, `MemberwiseClone`, `Finalize`, `ReferenceEquals`, `PrintMembers`, `Deconstruct` or `Clone`, or the name of the model itself: [SQLSRC256](diagnostics.md#sqlsrc256).

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
- **A parameter's property can be null only when its [`-- param:` marker](parameters-and-tokens.md#param-marker) says `null`.**

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

- **The value is the column's name, then `null` or `not null`, `->` and a C# type, or both in that order**: `-- column: note null -> NoteText`.  `null` gives a property that can be null, and `not null` one that cannot.  One marker names one column: write one for each.  Two markers for one column are fine when they say the same, or when one gives the nullability and the other the type, and [SQLSRC112](diagnostics.md#sqlsrc112) when they give two nullabilities or two C# types.
- **`->` gives the column that C# type**, and `-> default` takes the column out of every column type, so that the type override, or else the type map, gives its type: which wins is in [column types](settings.md#setting-column-types).  The type is written as a type override's is, a keyword, a full name or a short name, and the column keeps its nullability: `-> OrderCount` on a column that can be null gives `OrderCount?`.  A query method reads the type through your [converters](settings.md#converters) when it has them.
- **The name is the column's as the query returns it**, its alias or its own name, compared ignoring case.  Write it in double quotes when it has white space or a `.`, or when it is the word `null` or `not`, and double a quote inside it: `-- column: "total due" not null`.  A value that is not one name, plain or quoted, followed by `null` or `not null`, a `->` and a type, or both, is [SQLSRC111](diagnostics.md#sqlsrc111): so is `-- column: total due not null`, without its quotes, and so is a name with a `.` outside quotes, `-- column: users.id -> UserId`, since a column's source is mapped in the file header.
- **A marker that names no column of the query's result is [SQLSRC260](diagnostics.md#sqlsrc260)**, and so is one on a query that returns no rows, and one that says `null` or `not null` of a column whose alias already ends in `!` or `?`, whose nullability is then said twice.  A marker that gives a type alone may name such a column.  The query gets no models until it is fixed.
- **The marker is a comment.**  Adding, changing or removing one, or its type, does not change the query, so it does not need describing again.  A query whose output is `sql` has no models, and its marker has no effect.
- **In the file header**, `-- column:` names the column of a table or view that a value is read from, `-- column: users.id -> UserId`, and gives it a type in every query of the file: see [column types](settings.md#setting-column-types).

<a id="model-documentation"></a>

### Documentation

Every model and every property has a documentation comment, which your editor shows where it is used, as in the example at the top.

- **A model's** is the query's `-- summary:`, when it has one ([summaries](sql-files.md#summaries)), then a line that says what the model is and names the query and its file: "The parameters of the query `GetUser` from `Users.sql`." or "A row of the query `GetUser` from `Users.sql`."  A model that several queries share names each of them, and takes the summary of the first: see [shared models](#shared-models).  A table model names its table: see [table models](#table-models).
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
/// A row of the table <c>public.users</c>, read by the queries <c>GetUser</c> from <c>Users.sql</c> and <c>ListUsers</c> from <c>Users.sql</c>.
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
- **`-- output-model:` wins over the table's name**, and the [generator parameter](settings.md#setting-generator-parameters) `no-table-models` turns table models off, for a query, a file, a type or a whole project.  The input model is always named after the query.
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

A model is generated once for its full name, however many queries and types ask for it.  Two queries share one model when their models of that name are the same: both input models or both output models, of one shape and sorted or not alike, with properties of the same names, C# types and nullability, in the same order.  Above, `GetUser` and `FindUser` share `UserRow`, and its documentation reads "A row of the queries `GetUser` from `Users.sql` and `FindUser` from `Users.sql`."  Two types in one namespace that claim one file share the models of its queries in the same way, so their settings for models must agree.

- **Two models of one name that differ** are [SQLSRC257](diagnostics.md#sqlsrc257), which names the first difference.  The queries are taken in the order of their files' paths, then of their order in a file, then of the names of the types that claim them, and the error is at the later query, at its `-- input-model:` or `-- output-model:` marker, or at its name when it has none.  The earlier query's model is generated, and the later query has none of that kind until the two agree.
- **An input model and an output model are never shared**, so one name for both, which two equal suffixes give a query, is SQLSRC257 too.
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
// Generated, shown without global::
namespace App
{
    public static partial class UserRepositoryExtensions
    {
        public static async Task<GetUserDto?> GetUser(
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

A query whose [output](settings.md#setting-output) is `codegen`, the default, gets an asynchronous method that runs it, in place of its constant or token method.  Above, a `public` type `App.UserRepository` claims `Users.sql`.  The method takes the connection and the query's input model, opens the connection when it is closed, sends each parameter, runs the query and reads its row into the output model, here one row or null, by its [result shape](sql-files.md#result-shapes).  It runs on ADO.NET's `DbConnection`, through the driver your project references, Npgsql for PostgreSQL and Microsoft.Data.SqlClient for SQL Server, and uses no reflection and no library of SqlSource's.  Its SQL is a constant inside the method, which your code cannot reach: a project that runs a query's SQL itself, through Dapper or its own code, gives it the output `models`.

<a id="which-methods"></a>

### Which queries get a method

A query gets its method from a type that claims its file when, for that type:

- **its output is `codegen`**;
- **it has what its models need**: a current entry in its sidecar, a C# type for each column and parameter, and every model it needs, an input model when it has parameters and an output model when it returns rows.  A query with neither needs no model;
- **its method can be written**: a result shape that its rows allow ([SQLSRC272](diagnostics.md#sqlsrc272)), a name that nothing else takes ([SQLSRC274](diagnostics.md#sqlsrc274)), no token named as one of the method's own arguments ([SQLSRC275](diagnostics.md#sqlsrc275)), and, under the default `MethodLocation`, a class to hold it ([SQLSRC276](diagnostics.md#sqlsrc276));
- **its [converters](settings.md#converters) serve it**: a column or parameter whose type a type override, a column type or a `-- param:` marker chose, and whose type has converters, has one it can use: of the direction it needs ([SQLSRC280](diagnostics.md#sqlsrc280)), between it and the type the driver reads when it is a type of yours that the driver never reads ([SQLSRC279](diagnostics.md#sqlsrc279)), and the only one of that direction, with nothing wrong with it ([SQLSRC277](diagnostics.md#sqlsrc277), [SQLSRC278](diagnostics.md#sqlsrc278));
- **the project references the driver of its dialect** ([SQLSRC273](diagnostics.md#sqlsrc273)), at Npgsql 8.0 or later ([SQLSRC247](diagnostics.md#sqlsrc247)) or Microsoft.Data.SqlClient 6.1 or later ([SQLSRC267](diagnostics.md#sqlsrc267)).

A query whose output is `codegen` that fails any of the others has an error, and gets neither its method nor a constant until it is fixed.  Its models are still generated when it has them, and the type's other queries get their methods.  Each type that claims a file gets its own methods from it, by its own settings, and [where the methods go](#method-location) says when two such types give one method twice.  Only the queries of a dialect that can be described, `postgres` or `mssql`, have entries: a query of another dialect whose output is `codegen` is [SQLSRC209](diagnostics.md#sqlsrc209), and wants the output `sql`.  A statement that the tool cannot describe, such as PostgreSQL's `CALL` ([SQLSRC233](diagnostics.md#sqlsrc233)), gets no method either, so on PostgreSQL no method calls a procedure: give such a query `-- output: sql` and run its constant yourself.

<a id="method-location"></a>

### Where the methods go

Under the default [`MethodLocation`](settings.md#setting-method-location), `ExtensionClass`, the methods are extension methods of `DbConnection`, called on the connection as `connection.GetUser(...)`.  They are in a generated `static partial class` named after the type with `Extensions` after it, `UserRepositoryExtensions` for `UserRepository`.

- **The class is in the type's namespace**, not nested in the types around the type, and is named after the type's own name alone: `Outer.Queries<T>` gives `QueriesExtensions`.  Code in another namespace calls the methods with a `using` of that namespace.
- **It is `public` when the type and every type around it are `public`**, and `internal` otherwise, as a [model](#model-accessibility) is.
- **Two types whose classes have one name in one namespace share it**, as two types `Queries` nested in two types of one namespace do, when the class has one accessibility for both.  When it does not, or when a type of your project or a model has the class's full name, the type gets none of its methods: [SQLSRC276](diagnostics.md#sqlsrc276).
- **A method is not named like a member of `DbConnection` or `object`**, such as `Open` or `ToString`, which a call on a connection would find instead, nor like the class itself ([SQLSRC274](diagnostics.md#sqlsrc274)).

Under `Public`, `Internal` or `Private`, the methods are static methods of the type itself, of that accessibility, which take the connection as their first argument: `UserRepository.GetUser(connection, ...)`.  A method is then not named like a member of the type, the type itself or one of its type parameters, nor like the constant, token method or nested class that the type's other queries get ([SQLSRC274](diagnostics.md#sqlsrc274)).

Two methods of one type with one name, as those of a query `Get` under `async-method-suffix` and a query `GetAsync` without it, are SQLSRC274, and so are two methods of one name and one list of arguments in two extension classes of one namespace, which a call could not tell apart.  [`SqlLocation`](settings.md#setting-sql-location) does not move a method: it places only the constants and token methods.  A type's methods are in a file of their own among the generated files, named after the type's full name, `App.UserRepository-Methods.g.cs`.

<a id="method-signature"></a>

### The signature

```csharp
public static async Task<ListFromDto[]> ListFrom(
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
| `connection` | The connection to run on: see [the connection](#method-connection) |
| `parameters` | The query's input model, when it has parameters.  It is not in the method of a query without parameters |
| A `string` for each [token](parameters-and-tokens.md#tokens) | The text that replaces the token, named as the token, in the order the tokens first appear.  It is checked as a token method checks it: by default null, empty or blank text throws, and `no-token-validation` turns the check off |
| `transaction` | The transaction to run the query in, or null |
| `commandTimeout` | The command's timeout in seconds, or null for the driver's |
| `cancellationToken` | Cancels opening the connection, running the query and reading its rows.  What each driver throws then is on the pages of [PostgreSQL](postgresql.md#method-results) and [SQL Server](sqlserver.md#method-results) |

The method of a query with tokens raises no CA2100 ("Review SQL queries for security vulnerabilities") in your build, though it runs SQL made with their text: that text is your own code's choice, never a user's, as [tokens](parameters-and-tokens.md#tokens) says.

A null `connection` or `parameters` throws `ArgumentNullException`.  The names of the arguments that every method has, `connection`, `parameters`, `transaction`, `commandTimeout` and `cancellationToken`, are not names a token can have ([SQLSRC275](diagnostics.md#sqlsrc275)).  What the method returns depends on the query's result shape:

| Shape | Returns |
|----|----|
| `many` | `Task<C>`, where `C` is the [collection type](settings.md#setting-collection-type) of the output model: an array by default, `Task<ListFromDto[]>`.  For the collection type `IAsyncEnumerable`, `IAsyncEnumerable<TDto>`, with no `Task` |
| `one` | `Task<TDto>`, the output model |
| `one-optional` | `Task<TDto?>` |
| `none` | `Task` |
| `rowcount` | `Task<int>` |

<a id="method-connection"></a>

### The connection

The method opens the connection when its `State` is `Closed`, and closes it again before it returns or throws.  A method that [streams its rows](settings.md#streaming-rows) opens it when the loop asks for the first row, and closes it again when the loop ends, and the connection is busy with the query until then, so the loop's body cannot run another command on it, on PostgreSQL or on a SQL Server connection without `MultipleActiveResultSets`.  A connection that was not closed is left as it was: it stays open, in the transaction and with the session's settings it had, unless, on SQL Server, an error that the method does not report rolls the transaction back, as [what a method reads and throws](sqlserver.md#method-results) says.  So to run several queries on one open connection, or in a transaction, open the connection yourself.

**Give the method the transaction.**  The method gives its command the `transaction` it is given, or none when it is given null.  On SQL Server, Microsoft.Data.SqlClient refuses a command on a connection that has a transaction the command was not given, and throws `InvalidOperationException` ("... requires the command to have a transaction when the connection assigned to the command is in a pending local transaction ...") before anything is sent.  Npgsql runs such a command in the connection's transaction.  A SQL Server method given a Microsoft.Data.SqlClient transaction that has completed, committed or rolled back, throws `InvalidOperationException` and runs nothing, where Microsoft.Data.SqlClient would run its command outside any transaction: see [what a method reads and throws](sqlserver.md#method-results).  Pass the transaction to every method you call inside one, on both: see [the recipe](recipes.md#queries-in-a-transaction).

<a id="method-shapes"></a>

### What each shape returns

The [result shape](sql-files.md#result-shapes) after the query's name says what the method does with the rows.  Without one, a query that returns rows is `many`, and one that returns none is `rowcount`.

- **`many`** reads every row into the [collection type](settings.md#setting-collection-type), an array by default, and complete when the task completes, or, for the collection type `IAsyncEnumerable`, gives each row to the caller's loop as it is read: see [streaming rows](settings.md#streaming-rows).  Unless the query has [`no-array-pool`](settings.md#setting-generator-parameters), the rows of every collection type but `ImmutableList<T>`, `IImmutableList<T>` and `IAsyncEnumerable<T>`, which holds no rows, are read into buffers borrowed from .NET's shared array pool, so that, once the pool holds buffers of the sizes it needs, the method allocates no collection for up to about 1,000 rows but the one it returns.
- **`one`** returns the one row.  It throws [`UnexpectedRowCountException`](#unexpected-row-count) when the query returns no rows, or more than one: it reads a second row to see whether there is one.
- **`one-optional`** returns the row, or null when there is none, and throws the same exception for more than one.
- **`none`** runs the statement and returns nothing.
- **`rowcount`** runs the statement and returns the count the driver reports: the rows inserted, updated or deleted, those of each statement added up when the query has several, and `-1` for a `SELECT`.  The rows of a `RETURNING` or `OUTPUT` clause are counted.  On PostgreSQL a `WITH` whose last statement is a `SELECT` counts as a `SELECT`, so it is `-1` though its `WITH` updates rows.  On SQL Server, `SET NOCOUNT ON` makes it `-1`, and when a query without parameters sets it, every later count on the connection too: see [known issues](#known-issues).

`none` and `rowcount` read no rows, so they suit an `INSERT ... RETURNING` whose rows you do not want.  `one`, `one-optional` and `many` on a query that returns no rows are [SQLSRC272](diagnostics.md#sqlsrc272).  A SQL Server query of several statements returns the rows of its first result set, as its [model](sqlserver.md#what-can-be-described) has them, and its other statements still run: an error that one of them raises is thrown by every method, which reads the rest of the batch before it returns, and may have rolled back the transaction the method was given, depending on the error and on `XACT_ABORT`.  When the method throws an exception of its own first, such as `UnexpectedRowCountException`, and such an error then rolls the transaction back, it throws [`TransactionRolledBackException`](#transaction-rolled-back): see [what a method reads and throws](sqlserver.md#method-results).

<a id="reading-rows"></a>

### How a row is read

- **Each column is read by its position** into the property the output model has for it, so its name does not matter: a `!` or `?` at the end of an alias works as well as a [`-- column:` marker](#column-marker).  A record is made with its constructor, a class by setting its properties, and a [sorted](#property-order) model as well as any other.
- **A property that can be null** is null when its column is.
- **A property that cannot be null is read with no check**, so a null in its column throws, as the driver reports it: Npgsql's `InvalidCastException` "Column 'name' is null.", and Microsoft.Data.SqlClient's `SqlNullValueException` "Data is Null.".  Microsoft.Data.SqlClient gives no error for a null `vector`, so the method checks such a column itself, and throws `InvalidCastException` "Column 'embedding' is null." with the column's name.  A `!` at the end of an alias, a `-- column:` marker that says `not null`, or a column that the tool showed is never null, is so checked the first time a null arrives, and never read as a default value.
- **Each column is read as its property's C# type**, and each parameter sent as one, unless a converter converts it.  A C# type that a [type override](settings.md#setting-type-overrides), a [column type](settings.md#setting-column-types) or a `-- param:` marker names, with no converter, is given to the driver as it is, which suits a type the driver knows or was taught, and throws the driver's `InvalidCastException` for one it was never taught: see [converters](settings.md#converters).
- **A column whose type a type override or a column type chose, and that has a [converter](settings.md#converters)**, is read as the type the driver reads its database type as, and that value given to the converter, whose result is the property's value.  A null never reaches the converter: a property that cannot be null throws as above, before the converter is called.

<a id="method-parameters"></a>

### How a parameter is sent

- **Each parameter is sent by its name**, without its `@`, with the value of its property of the input model, in the order of the query's [parameter list](parameters-and-tokens.md#query-parameters).  Both drivers match a name ignoring case, as SqlSource does, so `@Id` and `@id` are one parameter, and one value fills both.  A parameter written in snake_case, `@user_id`, is filled from its property `UserId` like any other.
- **A null value is sent as the database's `NULL`**: that of a property that can be null, and that of a property of a reference type that cannot be, which a caller can set to null all the same.
- **A parameter whose type a type override or its `-- param:` marker chose, and that has a [converter](settings.md#converters)**, is given to the converter, and the value it returns is sent.  A null is sent as `NULL`, without the converter.
- **Each parameter is sent as the database type it was described with**, or that its [`-- param:` marker](parameters-and-tokens.md#param-marker) declares, and not as a type chosen from the C# type of its value.  On SQL Server it is sent with the type's length, precision and scale.  See how a parameter is sent on [PostgreSQL](postgresql.md#method-parameters) and on [SQL Server](sqlserver.md#method-parameters).

<a id="unexpected-row-count"></a>

### `UnexpectedRowCountException`

```csharp
// -- name: GetActiveUser -> one
try
{
    var user = await connection.GetActiveUser(new GetActiveUserParams(email));
}
catch (UnexpectedRowCountException exception)
{
    // exception.QueryName is "GetActiveUser"
}
```

The method of a query whose shape is `one` throws `UnexpectedRowCountException` when the query returns no rows or more than one, and that of a query whose shape is `one-optional` when it returns more than one.  Its message names the query and what it returned, "The query 'GetActiveUser' returned more than one row.", and its `QueryName` the query.

- **It derives from `InvalidOperationException`**, which LINQ's `Single` throws, so code that catches that catches it too.
- **It is `internal`, in the namespace `SqlSource`**, generated into every project that uses SqlSource, beside the attribute, so another assembly catches it as `InvalidOperationException`: see [known issues](#known-issues).

<a id="transaction-rolled-back"></a>

### `TransactionRolledBackException`

```csharp
// -- name: GetActiveUser -> one, on SQL Server
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

A SQL Server method of the shape `one`, `one-optional` or `many` throws `TransactionRolledBackException` when it was given a transaction, threw an exception of its own, and an error that the rest of its query raised then rolled the transaction back.  Its own exception is the `InnerException`: `UnexpectedRowCountException`, a null in a column whose property cannot be null, or a converter's exception.  Its message names the query, "The transaction was rolled back by an error that the query 'GetActiveUser' raised after the inner exception was thrown.", and its `QueryName` the query.  Without a transaction, or with the transaction still open, the method throws its own exception as it is.  A Microsoft.Data.SqlClient transaction that had completed before the call is refused before the query runs, with `InvalidOperationException`, so this exception says that the method's own query rolled the transaction back.  A transaction of a library that wraps Microsoft.Data.SqlClient's and keeps its `Connection` set after it completes is neither refused nor reported: see [what a method reads and throws](sqlserver.md#method-results).

- **It derives from `InvalidOperationException`**, so code that catches that catches it too.
- **It is `internal`, in the namespace `SqlSource`**, generated into every project that uses SqlSource, as `UnexpectedRowCountException` is, so another assembly catches it as `InvalidOperationException`: see [known issues](#known-issues).

<a id="projects-that-share-internals"></a>

## Projects that share internals

A test project usually sees the internals of the project it tests, through `InternalsVisibleTo`.  If both use SqlSource, each has its own copy of the attribute, its enums (`SqlLocation`, `GeneratorOutput`, `GeneratorModelType`, `GeneratorCollectionType` and `MethodLocation`), `UnexpectedRowCountException`, `TransactionRolledBackException` and `SqlSourceConverterAttribute`, and the test project sees two of each.  It uses its own.  So a `catch (UnexpectedRowCountException)` in the test project does not catch the exception that a method of the other project throws, which is the other project's copy, and nor does a `catch (TransactionRolledBackException)`: catch `InvalidOperationException` there.  The compiler warns about such a conflict (CS0436) wherever the test project's code names one of them, for example at its `[SqlSourceGenerate]`.

SqlSource turns that warning off for these types and for nothing else, as the suppression `SQLSRC901`.  Nothing has to be added to `NoWarn`.  The suppression holds when warnings are errors.  A conflict between two types of your own is still reported, even when one of them is named like a SqlSource type: an `internal class SqlLocation` that you declare in both projects, in a namespace of your own, still gets CS0436.

`SQLSRC901` is not an error and is not in the [diagnostics list](diagnostics.md).

<a id="known-issues"></a>

## Known issues

- **The tool reads the attribute without compiling your code.**  `sqlsource describe` finds `[SqlSourceGenerate]` by reading your C# files as text, so `Path` and `Output` must be written out where the attribute is: `Path = "Queries"` and `Output = GeneratorOutput.Models`.  A constant, `nameof`, a concatenation or a cast there compiles, and the tool reports [SQLSRC208](diagnostics.md#sqlsrc208) and describes nothing for the type.  The build then reports [SQLSRC241](diagnostics.md#sqlsrc241) for each of the type's queries that needs an entry in its sidecar and has none, or, for the file, [SQLSRC209](diagnostics.md#sqlsrc209) under a dialect that has no database to ask, such as `ansi`.  Two more things follow from the same reading.  An alias of the attribute (`using Gen = SqlSource.SqlSourceGenerateAttribute;` and `[Gen]`) works in the build and is not seen by the tool, so the type's queries are not described, and the build reports SQLSRC241 for them in the same way.  An attribute inside `#if` is read with the compilation constants the tool gets from your project, which may not be those of the configuration you build.  Write the attribute under its own name, and outside `#if` where you can.
- **An input model binds through Dapper only when the query writes its parameters as the model's properties are named.**  Dapper sends a property of a parameter object only to a parameter that the SQL writes as `@` and the property's name, ignoring case but not `_`.  So the property `AccountId` binds to `@accountId`, and a query that writes `@account_id` gets no value from it.  Write the query's parameters in camelCase, or give Dapper the parameters yourself, as in `new { account_id = args.AccountId }`: see [the recipe](recipes.md#models-through-dapper).
- **Under `Direct`, a query named like a member that the type declares is a compiler error.**  A query `Get` whose output is `sql` or `models`, in a type that declares a method `Get`, gives the compiler's CS0102, in the generated code, and no SQLSRC error.  Rename the query, or use `Nested`.  A query method on the type is checked instead, as [SQLSRC274](diagnostics.md#sqlsrc274).
- **`UnexpectedRowCountException` and `TransactionRolledBackException` cannot be caught by their type in another assembly.**  They are `internal`, so an assembly that calls a `public` method of another cannot name them, and two projects that share internals each have their own copy of each, so a `catch (UnexpectedRowCountException)` in one does not catch the other's, nor a `catch (TransactionRolledBackException)`.  Catch `InvalidOperationException`, from which both derive.
- **A query method with parameters works only on a connection whose commands take its driver's own parameters.**  It works on the connections of the drivers, such as `NpgsqlConnection`, the one an `NpgsqlDataSource` gives, and `SqlConnection`.  A `DbConnection` that wraps one of them, and whose commands create or accept only parameters of their own type, fails before the query is sent: on PostgreSQL with the wrapper's own exception, typically an `InvalidCastException`, and on SQL Server with `InvalidCastException`, "Unable to cast object of type '...' to type 'Microsoft.Data.SqlClient.SqlParameter'.".  Give the method the driver's connection that the wrapper holds, or give the query the output `models` and send its parameters through the wrapper yourself.
- **On SQL Server, `rowcount` returns `-1` once `SET NOCOUNT ON` has run on the connection's session.**  A query without parameters, or a command of your own without them, that runs `SET NOCOUNT ON` sets it for the session, not for the statement: on a connection you opened, every later `rowcount` method returns `-1` until the connection is closed and its session reset.  A query with parameters, which Microsoft.Data.SqlClient runs through `sp_executesql`, sets it for itself alone, and only its own count is `-1`.  Run `SET NOCOUNT OFF` after it, or do not set it in a query without parameters on a connection that runs a `rowcount` method.
- **Where the suppression of CS0436 does not run, the warning is shown.**  The suppression is part of the package's analyzers.  A compiler or an editor that does not run it still reports CS0436 in a project that sees the internals of another one that uses SqlSource, and a project that treats warnings as errors then fails to build there.  SqlSource has not been checked in the live analysis of Visual Studio or Rider.  If you see the warning, add `<NoWarn>$(NoWarn);CS0436</NoWarn>` to the project that sees the other's internals.  That also hides a conflict between two types of your own.
