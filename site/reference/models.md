# Models

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

A model is a type that the generator writes for a described query, from the query's entry in its [sidecar](../concepts.md#sidecar): an input model for its parameters, and an output model for a row of its result.  Above, a `public` type in the namespace `App` claims `UserRepository.sql`, and the table `users` has the columns `id`, an identity column, `name` and `email`, both `NOT NULL`, and `deleted_at`, in that order.  The two models are shown together; each is a file of its own among the generated files, named after its full name, such as `App.GetUserDto-Model.g.cs`.

The models are built at every build from the committed sidecars, with no database.  After a change to a query, describe it again (see [sidecar files](sidecars.md#sidecar-files)): until then its entry is out of date, and the query has no models.

<a id="which-models"></a>

## Which queries get models

A query gets models when its [output](../settings.md#setting-output) is `models`, `methods` or `repository`, the default, and its entry in its sidecar is current.  Which models it gets depends on the entry:

| The query | Input model | Output model |
|----|----|----|
| Has parameters and returns rows | Yes | Yes |
| Returns rows and has no parameters | No | Yes |
| Has parameters and returns no rows, as an `INSERT` without `RETURNING`, or on SQL Server without `OUTPUT` | Yes | No |
| Has neither | No | No |

- **Rows of no column**, which PostgreSQL gives for `SELECT FROM users`, get an output model with no properties.
- **A query has no models while it has an error**: an entry that is missing or out of date ([SQLSRC241](../diagnostics.md#sqlsrc241), [SQLSRC244](../diagnostics.md#sqlsrc244)), a column or a parameter with no C# type ([SQLSRC246](../diagnostics.md#sqlsrc246), [SQLSRC249](../diagnostics.md#sqlsrc249) to [SQLSRC252](../diagnostics.md#sqlsrc252), [SQLSRC261](../diagnostics.md#sqlsrc261), [SQLSRC269](../diagnostics.md#sqlsrc269) and [SQLSRC270](../diagnostics.md#sqlsrc270)), a name that gives no property ([property names](model-properties.md#property-names)), or a [`-- column:` marker](model-properties.md#column-marker) that names no column it can set ([SQLSRC260](../diagnostics.md#sqlsrc260)).  Under the output `models` its constant or token method is still generated; under `methods` or `repository` it has no method either.
- **A query whose output is `sql` gets none.**
- **A type with an error asks for no models.**  When two types claim one file, a type with an error of its own asks for no models of its queries, and neither does a type for a file that it leaves out ([SQLSRC008](../diagnostics.md#sqlsrc008), [SQLSRC009](../diagnostics.md#sqlsrc009), or a [placement error](members.md#locations)); the other type still gets them.
- **`-- output-model:` on a query that returns no rows** is [SQLSRC259](../diagnostics.md#sqlsrc259), and the query has no models until the marker is removed.  `-- input-model:` on a query with no parameters is [SQLSRC119](../diagnostics.md#sqlsrc119).

<a id="model-names"></a>

## Names

```sql
-- name: GetUser
-- input-model: UserKey
-- output-model: App.Rows.UserRow
SELECT id, name FROM users WHERE id = @id;
```

A model is named after its query, with a suffix: `GetUser` gets `GetUserParams` for its parameters and `GetUserDto` for its rows.  The [suffixes](#setting-model-suffix) are settings, so `-- output-model-suffix: Row` in a file's header gives `GetUserRow`.

- **`-- input-model:` and `-- output-model:` name one query's models whole**, with no suffix: above, `UserKey`.  See [name of one query's model](#setting-model-name).
- **A name with a period is a full name**, with its namespace: `App.Rows.UserRow` is `UserRow` in the namespace `App.Rows`, whatever the [model namespace](#setting-model-namespace) says.
- **A query that reads a whole table** has a row model named after the table: see [table models](#table-models).
- **Queries whose models have one full name share one model**, when the models are the same: see [shared models](#shared-models).  Two names that differ only by case, `UserDto` and `UserDTO`, are two models.
- **Some names cannot be used**, such as the name of a type of your project: see [names that cannot be used](#unusable-model-names).

<a id="setting-model-suffix"></a>

### Suffix of a model's name

```sql
-- input-model-suffix: Args
-- output-model-suffix: Row

-- name: GetUser
SELECT id, name FROM users WHERE id = @id;
```

The suffix ends the name of the [model](#models) generated for a query's parameters (input) and for a row of its result (output).  The defaults are `Params` and `Dto`, so `GetUser` gets `GetUserParams` and `GetUserDto`; above, it gets `GetUserArgs` and `GetUserRow`.

- The value is one or more characters that a C# identifier can contain, such as letters, digits and `_`.  A space or a period is not one.
- It stands in the file header only, not inside a query.  It can also be `inputModelSuffix` or `outputModelSuffix` in a settings file.
- It ends a [table model's](#table-models) name too, `UsersRow`, and not a name that `-- input-model:` or `-- output-model:` gives.  Two equal suffixes give a query's two models one name, which is [SQLSRC257](../diagnostics.md#sqlsrc257).

The rules of the names are on [this page](#model-names).

<a id="setting-model-name"></a>

### Name of one query's model

```sql
-- name: GetUser
-- input-model: UserArgs
-- output-model: App.Models.UserRow
SELECT id, name FROM users WHERE id = @id;
```

`-- input-model:` and `-- output-model:` name the [model](#models) of one query's parameters and of its result rows, whole, with no suffix.  The value is a type's name, or its full name: identifiers joined by periods, none of them a reserved keyword or `file`, `required`, `scoped` or `extension`, which C# does not take as a type's name.  Above, `GetUser` gets `UserArgs`, in the namespace its models would have without the marker, and `UserRow` in the namespace `App.Models`, whatever the [model namespace](#setting-model-namespace) says.

- It stands inside a query only.  It has no key in a settings file, since a name is for one query.
- `-- input-model:` on a query with no parameters is [SQLSRC119](../diagnostics.md#sqlsrc119), and `-- output-model:` on a query whose statement returns no rows is [SQLSRC259](../diagnostics.md#sqlsrc259).
- `-- output-model:` wins over the name of a [table model](#table-models).
- Two queries that give one name share one model when their models are the same, and are [SQLSRC257](../diagnostics.md#sqlsrc257) otherwise: see [shared models](#shared-models).  A name that cannot be used, such as that of a type of your project, is [SQLSRC258](../diagnostics.md#sqlsrc258).

<a id="model-namespace"></a>

## Namespace

Models are types of a namespace, not nested in the type that claims the query.  They are in the namespace of that type, or in the one that the [model namespace](#setting-model-namespace) names when it is set.  A type in the global namespace gives models in the global namespace.  Two types in two namespaces that claim one file each get the models of its queries, one in each namespace, unless the model namespace gives them one.

<a id="setting-model-namespace"></a>

### Namespace of the models

```sql
-- model-namespace: App.Models

-- name: ListUsers
SELECT id, name FROM users;
```

The namespace the generated [models](#models) go in.  The default is the namespace of the type that claims the file.  Above, the models of `ListUsers` are in `App.Models`.

- The value is C# identifiers joined by periods, none of them a reserved keyword or `file`, `required`, `scoped` or `extension`, which C# does not take as a type's name.
- It stands in the file header only.  It can also be `modelNamespace` in a settings file.
- A name with a period that `-- input-model:` or `-- output-model:` gives is a full name, and ignores this setting.
- Two types in two namespaces that claim one file, or read one table, get a model each, one in each namespace, unless this setting gives them one.
- A namespace that is, or starts with, the full name of a type of your project gives models whose names cannot be used: [SQLSRC258](../diagnostics.md#sqlsrc258).

The rules are on [this page](#model-namespace).

<a id="model-accessibility"></a>

## Accessibility

A model is `public` when the type that claims its query is `public` and so is every type it is nested in, and `internal` otherwise: a `public` type nested in an `internal` one gives `internal` models.  A model that two types share is `public` when either of them would make it so.

It does not follow the [visibility](members.md#setting-visibility) of the query's members: see [where the members go](members.md#locations).

A `public` model cannot have a property whose type is not `public`, which a [type override](../settings.md#setting-type-overrides), a [column type](model-properties.md#setting-column-types) or a `-- param:` marker can name.  That is [SQLSRC251](../diagnostics.md#sqlsrc251), at each query of the model, and the model is not generated until the type is made `public`, or each type that claims the query `internal`.

<a id="model-shape"></a>

## Shape

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

A model is a record or a class, sealed or not, by the [shape of a model](#setting-model-type), set for input models and for output models apart.  Above is `GetUserDto` as a `class`.

- **A record is positional**: a parameter for each property, in the order of the properties, as in the example at the top.  It has value equality, `with` and deconstruction.  A record whose query has an array column, such as `integer[]`, also has a private constructor without parameters.  It exists for libraries that create a row and then set its properties, such as Dapper, and you cannot call it.  The record's equality, `with` and deconstruction are as for any record.
- **A class has a property with `get` and `set` for each**, and no constructor of its own.  A property of a reference type that cannot be null starts as `default!`, so the class compiles under nullable reference types with no warning; a property of a value type, or one that can be null, has no initial value.
- **Every type of a property is written as its C# keyword, such as `int` or `string`, or else from `global::`**, as `global::System.DateTime`, so a model compiles in any namespace, whatever your `using` lines and the names of your own types, and a type you wrote by its short name is written by its full name.  No generated model gives a compiler warning or error, an `[Obsolete]` or `[Experimental]` type of your choice included, but for the [known issue](../settings.md#known-issues) of a `?` inside a type argument of a generic type.  `[Experimental]` on the type's assembly or module counts too.  A type marked `[Obsolete]` as an error, which no code can use without an error, is refused as [SQLSRC252](../diagnostics.md#sqlsrc252), and so is one marked `[Obsolete]` or `[Experimental]` under a diagnostic id that is not an identifier, whose warning a model could not turn off, and a generic type with a type argument that breaks a constraint of its declaration; the query has no models until you map another type.

<a id="setting-model-type"></a>

### Shape of a model

```sql
-- output-model-type: sealed class

-- name: ListUsers
-- input-model-type: record
SELECT id, name FROM users WHERE id > @min;
```

The shape of the [model](#models) generated for a query's parameters (input) and for a row of its result (output): `record`, `sealed record`, `class` or `sealed class`.  The default is `sealed record` for both.  A record is positional; a class has a property with `get` and `set` for each column or parameter.  Above, every output model of the file is a `sealed class`, and the input model of `ListUsers` a `record`.

- It stands in the file header and inside a query, as two markers.  It can also be `inputModelType` or `outputModelType` in a settings file.
- Two queries that share a model must give it one shape, or the later is [SQLSRC257](../diagnostics.md#sqlsrc257).

What each shape looks like is on [this page](#model-shape).  To read rows through Dapper into sorted models, make them classes: see [the recipe](../recipes.md#models-through-dapper).

<a id="model-documentation"></a>

## Documentation

Every model and every property has a documentation comment, which your editor shows where it is used, as in the example at the top.

- **A model's** is the query's `-- summary:`, when it has one ([summaries](sql-files.md#summaries)), then a line that says what the model is and names the query and its file: "The parameters of the query `GetUser` from `UserRepository.sql`." or "A row of the query `GetUser` from `UserRepository.sql`."  A model that several queries share names each of them, and takes the summary of the first: see [shared models](#shared-models).  A table model names its table: see [table models](#table-models).
- **A property's** names its column or parameter as the database does, its database type as the tool recorded it, the table column it comes from when it has one, and whether that is an identity column or a computed column.
- **A record** documents its properties with `<param>` tags, which your editor shows on each property, and **a class** documents each property with its own summary.

<a id="table-models"></a>

## Table models

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

- **The name** is the PascalCase of the table's name, by the rule of [property names](model-properties.md#property-names), with the PascalCase of its schema before it when the schema is not `public` for PostgreSQL or `dbo` for SQL Server, and the output suffix after it: `users` gives `UsersDto`, `order_lines` gives `OrderLinesDto`, and `billing.invoices` gives `BillingInvoicesDto`.  No word is made singular.
- **A table whose name, or whose schema's name, gives no identifier** gives no table model, and the query's model is named after the query, with no error: a name such as `"order-lines"`, or one that ends in `!` or `?`.
- **`-- output-model:` wins over the table's name**, and the [generator parameter](../settings.md#setting-generator-parameters) `table-models=off` turns table models off, for a query, a file, a folder or a whole project.  The input model is always named after the query.
- **The namespace** is that of the type that claims the query, so two types in two namespaces that read one table get a table model each, unless the [model namespace](#setting-model-namespace) gives them one.
- **A table model is shared by the rule of [shared models](#shared-models).**  Two queries of one table whose models differ, as when a type override of one of them gives a column another C# type, are SQLSRC257.  Two tables whose names give one model name, such as `a_b.c` and `a.b_c`, share `ABCDto` when their properties agree, and its documentation names both tables.  A query whose own model has the name of a table model, a query `Users` beside the table `users`, shares it too, and the documentation adds "Also a row of the query `Users` from ...".
- **A query that stops reading the whole table gets a model of its own name again.**  When the table gains a column and the query lists its columns, the query's rows are named after the query once it is described again, and the code that used the table model no longer compiles.  A `SELECT *` query keeps the table model.

<a id="shared-models"></a>

## Shared models

```sql postgres
-- name: GetUser
-- output-model: UserRow
SELECT id, name FROM users WHERE id = @id;

-- name: FindUser
-- output-model: UserRow
SELECT id, name FROM users WHERE email = @email;
```

A model is generated once for its full name, however many queries and types ask for it.  Two queries share one model when their models of that name are the same: both input models or both output models, of one shape and sorted or not alike, with properties of the same names, C# types and nullability, in the same order.  Above, `GetUser` and `FindUser` share `UserRow`, and its documentation reads "A row of the queries `GetUser` from `UserRepository.sql` and `FindUser` from `UserRepository.sql`."  Two types in one namespace that claim one file share the models of its queries in the same way, and the file gives both the same settings for them.

- **Two models of one name that differ** are [SQLSRC257](../diagnostics.md#sqlsrc257), which names the first difference.  The queries are taken in the order of their files' paths, then of their order in a file, then of the names of the types that claim them, and the error is at the later query, at its `-- input-model:` or `-- output-model:` marker, or at its name when it has none.  The earlier query's model is generated, and the later query has none of that kind until the two agree.
- **An input model and an output model are never shared**, so one name for both, which two equal suffixes give a query, is SQLSRC257 too.
- **A model shared across outputs can differ by a library's types.**  A query whose output is `methods` or `repository` gets a library's types where the [library types](../settings.md#setting-library-types) turn it on, NodaTime's `Instant` for a `timestamptz` on PostgreSQL, while a query whose output is `models` keeps the type map's `System.DateTime`.  So a table model, or a model named by a marker, that a query with a method and a `models` query share, or two queries with methods where the library is on for one and off for the other, is SQLSRC257.  Name the models apart with [`-- output-model:`](#setting-model-name), or turn the library on or off alike for both.
- **A PostgreSQL array parameter differs by output too.**  The input model of a query with a method, `methods` or `repository`, holds it as an `IEnumerable<T>`, and a `models` query's as a `T[]`, so a model of one name that both share is SQLSRC257, which names both types.  Name the models apart, or give both queries the same output: see [array parameters](databases/postgresql/query-methods.md#array-parameters).
- **A shared model's documentation is its first query's**: that query's summary, and its columns' or parameters' documentation, though another query's column comes from another table.

<a id="unusable-model-names"></a>

## Names that cannot be used

A model is not generated, and each query that would have it is [SQLSRC258](../diagnostics.md#sqlsrc258), when its full name could not compile, or would be warned on:

- **A type of your project has the full name**, the type that carries `[SqlSourceGenerate]` among them.
- **It is a namespace of your project, or a part of its namespace is a type of your project**, as a model namespace `App.Queries` beside a class `App.Queries`.
- **It is the namespace of another model, or a part of its namespace is another model**, as `App.Data` beside `App.Data.Row`, which is reported for both.
- **Its name is lower-case ASCII letters alone**, as `usersdto`, which the compiler warns on, since such a name could become a keyword.

Give the query [another model name](#setting-model-name), or set another [model namespace](#setting-model-namespace).  A property that has the name of its model is [SQLSRC256](../diagnostics.md#sqlsrc256).
