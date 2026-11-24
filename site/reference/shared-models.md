# Table models and shared models

This page says when several queries share one [model](models.md): a model named after a table, and a model that several queries give the same name.

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
- **On SQL Server, a written-out list whose `WHERE` keeps a column that can hold null from being null gets a model of its own.**  Its column is never null there, so it no longer has the nullability of the table column.  `SELECT *` with the same `WHERE` keeps the table model.
- **The name is the PascalCase of the name of the table, with the output suffix after it.**  The PascalCase follows the rule of [property names](model-properties.md#property-names).  The PascalCase of the schema comes before it when the schema is not `public` for PostgreSQL or `dbo` for SQL Server.  So `users` gives `UsersDto`, `order_lines` gives `OrderLinesDto`, and `billing.invoices` gives `BillingInvoicesDto`.  No word is made singular.
- **A table gives no table model when its name, or the name of its schema, gives no identifier.**  The model of the query is then named after the query, with no error.  Such names include `"order-lines"`, and a name that ends in `!` or `?`.
- **`-- output-model:` wins over the name of the table.**
- **The [generator parameter](generator-parameters.md#setting-generator-parameters) `table-models=off` turns table models off**, for a query, a file, a folder or a whole project.
- **The input model is always named after the query.**
- **The namespace is that of the type that claims the query.**  So two types in two namespaces that read one table get a table model each, unless the [model namespace](models.md#setting-model-namespace) gives them one.
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
- **The error is at the later query.**  SqlSource orders the queries by the paths of their files.  It orders the queries of one file as the file writes them.  It orders one query that two types claim by the names of those types.  The error stands at the `-- input-model:` or `-- output-model:` marker of the later query, or at its name when it has none.
- **The model of the earlier query is generated.**  The later query has no model of that kind until the two agree.
- **An input model and an output model are never shared.**  So one name for both is SQLSRC257 too, and two equal suffixes give a query such a name.
- **A model shared across outputs can differ by the types of a library.**  A query whose output is `repository` or `methods` gets the types of a library where the [library types](library-types-and-json.md#setting-library-types) turn it on.  An example is NodaTime's `Instant` for a `timestamptz` on PostgreSQL.  A query whose output is `models` keeps the `System.DateTime` of the type map.
- **So a table model, or a model named by a marker, is SQLSRC257 when one of these shares it:**
  - a query with a method and a `models` query;
  - two queries with methods, where the library is on for one and off for the other.

  Name the models apart with [`-- output-model:`](models.md#setting-model-name), or turn the library on or off alike for both.
- **A PostgreSQL array parameter differs by output too.**  The input model of a query with a method, `repository` or `methods`, holds it as an `IEnumerable<T>`.  The input model of a `models` query holds it as a `T[]`.  So a model of one name that both share is SQLSRC257, which names both types.  Name the models apart, or give both queries the same output: see [array parameters](databases/postgresql/query-methods.md#array-parameters).
- **The documentation of a shared model is that of its first query.**  It takes the summary of that query, and the documentation of its columns or parameters.  This holds even when a column of another query comes from another table.
