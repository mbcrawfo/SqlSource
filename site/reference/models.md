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

A model is a type that the generator writes for a described query, from the entry of the query in its [sidecar](sidecars.md#sidecar-files).  A query can get an input model for its parameters, and an output model for a row of its result.

In the sample, a `public` type in the namespace `App` claims `UserRepository.sql`.  The table `users` has the columns `id`, `name`, `email` and `deleted_at`, in that order.  `id` is an identity column, and `name` and `email` are both `NOT NULL`.  The sample shows the two models together.  Among the generated files, each model is a file of its own, named after its full name, such as `App.GetUserDto-Model.g.cs`.

The generator writes the models at every build, from the committed sidecars, with no database.  After you change a query, describe it again: see [sidecar files](sidecars.md#sidecar-files).  Until you do, its entry is out of date, and the query has no models.

<a id="which-models"></a>

## Which queries get models

A query gets models when both of these are true:

- Its [output](settings.md#setting-output) is `repository`, the default, `methods` or `models`.
- Its entry in its sidecar is current.

The entry decides which models the query gets:

| The query | Input model | Output model |
|----|----|----|
| Has parameters and returns rows | Yes | Yes |
| Returns rows and has no parameters | No | Yes |
| Has parameters and returns no rows, as an `INSERT` without `RETURNING`, or on SQL Server without `OUTPUT` | Yes | No |
| Has neither | No | No |

- **Rows of no column get an output model with no properties.**  PostgreSQL gives such rows for `SELECT FROM users`.
- **A query has no models while it has one of these errors:**
  - an entry that is missing or out of date ([SQLSRC241](../diagnostics.md#sqlsrc241), [SQLSRC244](../diagnostics.md#sqlsrc244));
  - a column or a parameter with no C# type ([SQLSRC246](../diagnostics.md#sqlsrc246), [SQLSRC249](../diagnostics.md#sqlsrc249) to [SQLSRC252](../diagnostics.md#sqlsrc252), [SQLSRC261](../diagnostics.md#sqlsrc261), [SQLSRC269](../diagnostics.md#sqlsrc269) and [SQLSRC270](../diagnostics.md#sqlsrc270));
  - a name that gives no property ([property names](model-properties.md#property-names));
  - a [`-- column:` marker](model-properties.md#column-marker) that names no column it can set ([SQLSRC260](../diagnostics.md#sqlsrc260)).
- **Under the output `repository` or `methods`, a query with such an error has no method either.**  Under the output `models`, its constant or token method is still generated.
- **A query whose output is `sql` gets no models.**
- **A type with an error asks for no models.**  When two types claim one file, a type with an error of its own asks for no models of its queries.  A type also asks for no models of a file that it leaves out ([SQLSRC008](../diagnostics.md#sqlsrc008), [SQLSRC009](../diagnostics.md#sqlsrc009), or a [placement error](members.md#locations)).  The other type still gets them.
- **`-- output-model:` on a query that returns no rows is [SQLSRC259](../diagnostics.md#sqlsrc259).**  The query has no models until you remove the marker.
- **`-- input-model:` on a query with no parameters is [SQLSRC119](../diagnostics.md#sqlsrc119).**

<a id="model-names"></a>

<a id="setting-model-suffix"></a>

<a id="setting-model-name"></a>

## Names

```sql
-- input-model-suffix: Args
-- output-model-suffix: Row

-- name: GetUser
SELECT id, name FROM users WHERE id = @id;
```

A model is named after its query, with a suffix.  Two settings change the name: the suffix, and the name of one query's model.

**The suffix.**  The suffix ends the name of the [model](#models) of the parameters of a query (input), and of the model of a row of its result (output).  In the sample, `GetUser` gets `GetUserArgs` and `GetUserRow`.

- **The value is one or more characters that a C# identifier can contain**, such as letters, digits and `_`.  A space or a period is not one.
- **The defaults are `Params` and `Dto`.**  So without the setting, `GetUser` gets `GetUserParams` for its parameters and `GetUserDto` for its rows.
- **The suffix stands in the file header only, not inside a query.**  In a settings file, it can also be `inputModelSuffix` or `outputModelSuffix`.
- **The suffix ends the name of a [table model](#table-models) too**, as `UsersRow`.
- **Two equal suffixes give the two models of a query one name.**  That is [SQLSRC257](../diagnostics.md#sqlsrc257).

**The name of one query's model.**  `-- input-model:` and `-- output-model:` name the [model](#models) of the parameters of one query, and the model of its result rows.  The marker gives the whole name, with no suffix.

```sql
-- name: GetUser
-- input-model: UserArgs
-- output-model: App.Models.UserRow
SELECT id, name FROM users WHERE id = @id;
```

In this sample, `GetUser` gets `UserArgs`, in the namespace that its models would have without the marker.  It also gets `UserRow` in the namespace `App.Models`, whatever the [model namespace](#setting-model-namespace) says.

- **The value is the name of a type, or its full name.**  A full name is identifiers joined by periods.  No identifier can be a reserved keyword, or `file`, `required`, `scoped` or `extension`, which C# does not take as the name of a type.
- **A name with a period is a full name, with its namespace.**  It ignores the model namespace.
- **The marker stands inside a query only.**  It has no key in a settings file, since a name is for one query.
- **`-- output-model:` wins over the name of a [table model](#table-models).**
- **`-- input-model:` on a query with no parameters is [SQLSRC119](../diagnostics.md#sqlsrc119).**
- **`-- output-model:` on a query whose statement returns no rows is [SQLSRC259](../diagnostics.md#sqlsrc259).**

```sql
-- name: GetUser
-- input-model: UserKey
-- output-model: App.Rows.UserRow
SELECT id, name FROM users WHERE id = @id;
```

In this sample, the input model of `GetUser` is `UserKey`, with no suffix.  `App.Rows.UserRow` is a full name, so the output model is `UserRow` in the namespace `App.Rows`.

- **A query that reads a whole table has a row model named after the table**: see [table models](#table-models).
- **Queries whose models have one full name share one model, when the models are the same.**  Two queries that give one name are [SQLSRC257](../diagnostics.md#sqlsrc257) when their models are not the same: see [shared models](#shared-models).
- **Two names that differ only by case, `UserDto` and `UserDTO`, are two models.**
- **Some names cannot be used, such as the name of a type of your project.**  Such a name is [SQLSRC258](../diagnostics.md#sqlsrc258): see [names that cannot be used](#unusable-model-names).

<a id="model-namespace"></a>

<a id="setting-model-namespace"></a>

## Namespace

```sql
-- model-namespace: App.Models

-- name: ListUsers
SELECT id, name FROM users;
```

The model namespace is the namespace that the generated [models](#models) go in.  In the sample, the models of `ListUsers` are in `App.Models`.  Models are types of a namespace, not nested in the type that claims the query.

- **The value is C# identifiers joined by periods.**  No identifier can be a reserved keyword, or `file`, `required`, `scoped` or `extension`, which C# does not take as the name of a type.
- **The default is the namespace of the type that claims the file.**  A type in the global namespace gives models in the global namespace.
- **The setting stands in the file header only.**  In a settings file, it can also be `modelNamespace`.
- **A full name ignores this setting.**  `-- input-model:` or `-- output-model:` gives a full name when the name has a period.
- **A namespace that is, or starts with, the full name of a type of your project gives models whose names cannot be used.**  That is [SQLSRC258](../diagnostics.md#sqlsrc258).
- **Two types in two namespaces get a model each, one in each namespace, in one of these cases:**
  - the two types claim one file, and each gets the models of its queries;
  - the two types read one table, and each gets a [table model](#table-models).

  This holds unless the model namespace gives the two types one namespace.

<a id="model-accessibility"></a>

## Accessibility

A `public` type nested in an `internal` type gives `internal` models.  A model is `public` when both of these are true:

- The type that claims its query is `public`.
- Every type that this type is nested in is `public`.

Otherwise the model is `internal`.  If two types share a model, the model is `public` when one of the two types would make it so.

The accessibility of a model does not follow the [visibility](members.md#setting-visibility) of the members of its query: see [where the members go](members.md#locations).

A `public` model cannot have a property whose type is not `public`.  A [type override](type-mapping.md#setting-type-overrides), a [column type](model-properties.md#setting-column-types) or a `-- param:` marker can name such a type.  That is [SQLSRC251](../diagnostics.md#sqlsrc251), at each query of the model.  The model is not generated until you do one of these:

- Make the type of the property `public`.
- Make each type that claims the query `internal`.

<a id="model-shape"></a>

<a id="setting-model-type"></a>

## Shape

```sql
-- output-model-type: sealed class

-- name: ListUsers
-- input-model-type: record
SELECT id, name FROM users WHERE id > @min;
```

The shape of a [model](#models) says whether it is a record or a class, sealed or not.  You set it apart for input models, of the parameters of a query, and for output models, of a row of its result.  In the sample, every output model of the file is a `sealed class`, and the input model of `ListUsers` is a `record`.

- **The value is `record`, `sealed record`, `class` or `sealed class`.**
- **The default is `sealed record`**, for input models and for output models.
- **The shape stands in the file header and inside a query, as two markers.**  In a settings file, it can also be `inputModelType` or `outputModelType`.
- **Two queries that share a model must give it one shape.**  If they do not, the later query is [SQLSRC257](../diagnostics.md#sqlsrc257).

To read rows through Dapper into sorted models, make the models classes: see [the recipe](../guides/dapper.md#models-through-dapper).

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

This sample is `GetUserDto` as a `class`.

- **A record is positional.**  It has a parameter for each property, in the order of the properties, as in the sample under [Models](#models).  It has value equality, `with` and deconstruction.
- **A record whose query has an array column, such as `integer[]`, also has a private constructor without parameters.**  The constructor exists for libraries that create a row and then set its properties, such as Dapper.  You cannot call it.  The equality, `with` and deconstruction of the record are as for any record.
- **A class has a property with `get` and `set` for each column or parameter**, and no constructor of its own.
- **In a class, a property of a reference type that cannot be null starts as `default!`.**  So the class compiles under nullable reference types with no warning.  A property of a value type, or a property that can be null, has no initial value.
- **The type of each property is written as its C# keyword, such as `int` or `string`, or else from `global::`**, as `global::System.DateTime`.  So a model compiles in any namespace, whatever your `using` lines and the names of your own types.  A type that you wrote by its short name is written by its full name.
- **No generated model gives a compiler warning or error**, but for the [limit](type-mapping.md#limit-nullable-type-argument) of a `?` inside a type argument of a generic type.  This includes an `[Obsolete]` or `[Experimental]` type of your choice.  `[Experimental]` on the assembly or the module of the type counts too.
- **A type of one of these kinds is refused as [SQLSRC252](../diagnostics.md#sqlsrc252):**
  - a type marked `[Obsolete]` as an error, which no code can use without an error;
  - a type marked `[Obsolete]` or `[Experimental]` under a diagnostic id that is not an identifier, whose warning a model could not turn off;
  - a generic type with a type argument that breaks a constraint of its declaration.

  The query has no models until you map another type.

<a id="model-documentation"></a>

## Documentation

Every model and every property has a documentation comment.  Your editor shows the comment where the model or the property is used, as in the sample under [Models](#models).

- **The comment of a model starts with the `-- summary:` of its query, when the query has one** ([summaries](sql-files.md#summaries)).  Then comes a line that says what the model is, and names the query and its file: "The parameters of the query `GetUser` from `UserRepository.sql`." or "A row of the query `GetUser` from `UserRepository.sql`."
- **A model that several queries share names each of them, and takes the summary of the first**: see [shared models](#shared-models).
- **A table model names its table**: see [table models](#table-models).
- **The comment of a property names these:**
  - its column or parameter, as the database names it;
  - its database type, as the tool recorded it;
  - the table column it comes from, when it has one;
  - whether that is an identity column or a computed column.
- **A record documents its properties with `<param>` tags.**  Your editor shows them on each property.
- **A class documents each property with its own summary.**

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

A query whose rows are exactly the rows of a table gets an output model named after the table, in place of one named after the query.  Every query that reads the table in this way shares the model.

The rows are the rows of the table when all of these are true:

- Each column comes from that one table, with the name and the nullability of the table column.
- The columns are all the columns of the table, in its order.

`SELECT *` gives such rows, and so does the whole list written out, as in both queries of the sample.  Each of these gives the query a model of its own: an alias, a column left out, another order, an expression or an outer join.  The tool decides this when it describes the query.

- **On PostgreSQL, a view, a materialized view and a foreign table count as a table here.**  On SQL Server, a view does.
- **On SQL Server, the columns of a table are those that `SELECT *` gives.**  So a `HIDDEN` period column of a system-versioned table is not one of them.
- **The name is the PascalCase of the name of the table, with the output suffix after it.**  The PascalCase follows the rule of [property names](model-properties.md#property-names).  The PascalCase of the schema comes before it when the schema is not `public` for PostgreSQL or `dbo` for SQL Server.  So `users` gives `UsersDto`, `order_lines` gives `OrderLinesDto`, and `billing.invoices` gives `BillingInvoicesDto`.  No word is made singular.
- **A table gives no table model when its name, or the name of its schema, gives no identifier.**  The model of the query is then named after the query, with no error.  Such names include `"order-lines"`, and a name that ends in `!` or `?`.
- **`-- output-model:` wins over the name of the table.**
- **The [generator parameter](generator-parameters.md#setting-generator-parameters) `table-models=off` turns table models off**, for a query, a file, a folder or a whole project.
- **The input model is always named after the query.**
- **The namespace is that of the type that claims the query.**  So two types in two namespaces that read one table get a table model each, unless the [model namespace](#setting-model-namespace) gives them one.
- **A table model is shared by the rule of [shared models](#shared-models).**  Two queries of one table whose models differ are SQLSRC257, as when a type override of one of them gives a column another C# type.
- **Two tables whose names give one model name share the model when their properties agree.**  For example, `a_b.c` and `a.b_c` share `ABCDto`, and its documentation names both tables.
- **A query whose own model has the name of a table model shares it too.**  An example is a query `Users` beside the table `users`.  The documentation then adds "Also a row of the query `Users` from ...".
- **A query that stops reading the whole table gets a model of its own name again.**  For example, the table gains a column, and the query lists its columns.  Once the query is described again, its rows are named after the query, and the code that used the table model no longer compiles.  A `SELECT *` query keeps the table model.

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

In the sample, `GetUser` and `FindUser` share `UserRow`.  The documentation of the model reads "A row of the queries `GetUser` from `UserRepository.sql` and `FindUser` from `UserRepository.sql`."

A model is generated once for its full name, however many queries and types ask for it.  Two queries share one model when their models of that name are the same.  The models are the same when all of these are true:

- Both are input models, or both are output models.
- They have one shape, and both are sorted or neither is.
- Their properties have the same names, C# types and nullability, in the same order.

Two types in one namespace that claim one file share the models of its queries in the same way.  The file gives both types the same settings for the models.

- **Two models of one name that differ are [SQLSRC257](../diagnostics.md#sqlsrc257)**, which names the first difference.
- **The error is at the later query.**  The queries are taken in the order of the paths of their files, then of their order in a file, then of the names of the types that claim them.  The error stands at the `-- input-model:` or `-- output-model:` marker of the later query, or at its name when it has none.
- **The model of the earlier query is generated.**  The later query has no model of that kind until the two agree.
- **An input model and an output model are never shared.**  So one name for both is SQLSRC257 too, and two equal suffixes give a query such a name.
- **A model shared across outputs can differ by the types of a library.**  A query whose output is `repository` or `methods` gets the types of a library where the [library types](library-types-and-json.md#setting-library-types) turn it on.  An example is NodaTime's `Instant` for a `timestamptz` on PostgreSQL.  A query whose output is `models` keeps the `System.DateTime` of the type map.
- **So a table model, or a model named by a marker, is SQLSRC257 when one of these shares it:**
  - a query with a method and a `models` query;
  - two queries with methods, where the library is on for one and off for the other.

  Name the models apart with [`-- output-model:`](#setting-model-name), or turn the library on or off alike for both.
- **A PostgreSQL array parameter differs by output too.**  The input model of a query with a method, `repository` or `methods`, holds it as an `IEnumerable<T>`.  The input model of a `models` query holds it as a `T[]`.  So a model of one name that both share is SQLSRC257, which names both types.  Name the models apart, or give both queries the same output: see [array parameters](databases/postgresql/query-methods.md#array-parameters).
- **The documentation of a shared model is that of its first query.**  It takes the summary of that query, and the documentation of its columns or parameters.  This holds even when a column of another query comes from another table.

<a id="unusable-model-names"></a>

## Names that cannot be used

A model is not generated when its full name could not compile, or the compiler would warn on it.  Each query that would have the model is then [SQLSRC258](../diagnostics.md#sqlsrc258).  A full name cannot be used in one of these cases:

- **A type of your project has the full name.**  The type that carries `[SqlSourceGenerate]` is among them.
- **The full name is a namespace of your project, or a part of its namespace is a type of your project.**  An example is a model namespace `App.Queries` beside a class `App.Queries`.
- **The full name is the namespace of another model, or a part of its namespace is another model.**  An example is `App.Data` beside `App.Data.Row`, which is reported for both.
- **The name is lower-case ASCII letters alone**, as `usersdto`.  The compiler warns on such a name, since it could become a keyword.

Give the query [another model name](#setting-model-name), or set another [model namespace](#setting-model-namespace).  A property that has the name of its model is [SQLSRC256](../diagnostics.md#sqlsrc256).
