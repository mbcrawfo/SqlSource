# PostgreSQL

SqlSource reads PostgreSQL files by the `postgres` [dialect](../../settings.md#setting-dialect).  The `sqlsource` tool can describe their queries.  It asks your database what the parameters and the columns of each query are.  It writes the answer to the [sidecar](../../sidecars.md#sidecar-files) of the query.

These pages are about describing and the C# types that the columns and parameters of a described query get.  They are also about the models built from those types, and the query methods that run the queries.  [The dialects page](../../dialects.md#what-a-dialect-changes) says how SqlSource reads a PostgreSQL file, with its comments and its strings.

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
}
```

```console
$ dotnet sqlsource describe --connection postgres="Host=localhost;Database=app;Username=app;Password=secret"
postgres (postgres): 12 described, 0 skipped, 0 failed
```

The connection is for the [logical database](../../connections.md#connections) of the query.  If you name no logical database, it is `postgres`.  So the variable of the connection is `SQLSOURCE_CONNECTION_POSTGRES`, or `SQLSOURCE_CONNECTION` if `postgres` is the only database of the run.  The summary names that database first, and its dialect in parentheses.

<a id="connecting"></a>

## Connecting

- **The connection string is Npgsql's.**  For example: `Host=localhost;Database=app;Username=app;Password=...`.  Its keywords are in [Npgsql's documentation](https://www.npgsql.org/doc/connection-string-parameters.html).  A string that Npgsql cannot read, or a database that cannot be opened, is [SQLSRC224](../../../diagnostics.md#sqlsrc224).
- **The supported versions of PostgreSQL** are in [Supported versions](../index.md#supported-versions).  An older server is [SQLSRC235](../../../diagnostics.md#sqlsrc235).
- **The tool needs your schema, not your data.**  A copy of the schema with no rows, such as a local database that your migrations created, is enough.
- **Connect directly to the server, or through a pooler in session or transaction mode.**  The tool keeps one session open for each database.  It describes each query in several steps, in one transaction.  A pooler in session or transaction mode, such as PgBouncer, keeps that transaction on one server connection.  Each of these is [SQLSRC236](../../../diagnostics.md#sqlsrc236):
  - a pooler in statement mode, which refuses the transaction;
  - `Multiplexing=true` in the connection string, which shares connections between commands.

  Your application can still connect in its own way.

<a id="what-describing-does"></a>

## What describing does to your database

**The tool never runs your queries.**  For each query, the tool does these things:

- It asks PostgreSQL to prepare the statement.  PostgreSQL checks the statement and works out the types of its parameters and columns.
- It reads the catalog of the database, to learn where each column comes from.
- For some queries, it asks PostgreSQL how it would run the statement, without running it.  That tells which columns of the query can be null.

The tool describes each query in a transaction, and then rolls the transaction back.  Before the end, the tool frees the statement that it prepared.  The session settings that the tool changed end with the transaction.

While the tool describes a query, a migration waits until the tool has finished with that query, if the migration runs one of these:

- a `DROP TABLE` of a table that the query reads;
- an `ALTER TABLE` that adds, drops or changes a column of such a table.

**One thing can have an effect.**  If a query calls a function marked `IMMUTABLE` with constant arguments, describing the query can run that function.  PostgreSQL computes such a call in advance.  Some functions are marked `IMMUTABLE` and are not: for example, one that advances a sequence, writes a file or calls another server.  If describing runs such a function, the function has its effect, and the rollback does not undo it.  If your database has such a function, do one of these:

- Mark the function `VOLATILE`, which is what it is.
- Describe against a copy of the schema, where the effect of the function does no harm.

<a id="parameters"></a>

## Parameters

```sql postgres
-- name: FindUsers
-- param: @name text
SELECT id, name FROM users
WHERE (@name IS NULL OR name = @name);
```

PostgreSQL gives each [query parameter](../../parameters.md#query-parameters) a type from the way that the query uses it.  In `WHERE id = @id`, `@id` takes the type of `id`.  A [`-- param:` marker](../../parameters.md#param-marker) gives a parameter a type of your own instead, as the marker of `@name` does in the sample.  PostgreSQL checks that type against every use of the parameter.

- **If no use of a parameter says what its type is, the query is [SQLSRC226](../../../diagnostics.md#sqlsrc226).**  For example, `SELECT @p IS NULL` holds for a value of any type.  Cast the parameter where it is used, `@p::int`, or declare its type.
- **PostgreSQL takes the type from the first use.**  In the optional filter of the sample, the first `@name` says nothing of the type.  So without the marker, the query is [SQLSRC227](../../../diagnostics.md#sqlsrc227).  Declare the type, as the sample does, or put first the use that gives the type: `(name = @name OR @name IS NULL)`.
- **A [list parameter](../../list-parameters.md#list-parameters) takes the type of its values from its use after `IN`.**  Its test for null gives no type.  So the optional filter `(@ids IS NULL OR id IN @ids)` needs no marker: each value of `@ids` has the type of `id`.
- **A list in the index predicate of `ON CONFLICT` fails when it is empty or null.**  An example is `INSERT ... ON CONFLICT (sku) WHERE (@kinds IS NULL OR kind IN @kinds) DO NOTHING`.  For an empty or a null list, the query method throws `PostgresException` with "cannot use subquery in index predicate".  A list that holds a value runs.  A list there does nothing anyway.  The index predicate only chooses the unique index that finds a conflict, and a predicate that holds a parameter cannot choose a partial index.  So:
  1. Leave the list out of the index predicate.
  2. If the partial index has a predicate of its own, write that predicate there.
  3. Test the list in the `WHERE` of `DO UPDATE`, or in the `WHERE` of the `SELECT` that the `INSERT` reads.
- **A parameter compared with a column of a domain takes the base type of the domain.**  In `WHERE email = @email`, where `email` is a domain over `text`, `@email` is a `text`.  So a [type override](../../type-mapping.md#setting-type-overrides) of the domain does not reach the parameter.  To give it the C# type of the domain, declare it as the domain: `-- param: @email public.email`.  Or give it the C# type itself: `-- param: @email -> MyApp.Email`.
- **The first use can also give a type that you did not mean.**  In `placed_at > now() - @age`, `@age` becomes a `timestamp with time zone`.  The server then reports an operator that does not exist ([SQLSRC225](../../../diagnostics.md#sqlsrc225)).  Write `@age::interval`, or declare the type.
- **A declared type that the database does not have is [SQLSRC228](../../../diagnostics.md#sqlsrc228).**  If the type is in a schema that is not on the search path of the connection, write the type with its schema: `public.user_status`.
- **`@x`, for the absolute value of `x`, is read as the parameter `x`.**  Write `abs(x)` instead.  See [names that are read as parameters](../../parameters.md#names-read-as-parameters).

<a id="what-can-be-described"></a>

## What can be described

The tool describes a query that is one statement of one of these kinds, with a `WITH` in front of it or not:

- `SELECT` (without `INTO`);
- `INSERT`;
- `UPDATE`;
- `DELETE`;
- `MERGE` (PostgreSQL 15 and later);
- `VALUES`;
- `TABLE`.

For any other query, give the query `-- output: sql`.  The query then keeps its constant or its token method, and the tool does not describe it.

| The query | Reported as | What to do |
|----|----|----|
| `CALL`, `CREATE`, `SET`, `SHOW` and other commands, `SELECT ... INTO` | [SQLSRC233](../../../diagnostics.md#sqlsrc233) | Give it `-- output: sql`.  For `SELECT ... INTO`, write `CREATE TABLE ... AS` with `-- output: sql` |
| Two statements under one `-- name:` | [SQLSRC230](../../../diagnostics.md#sqlsrc230) | Give each statement a `-- name:` of its own |
| A parameter written `$1` | [SQLSRC234](../../../diagnostics.md#sqlsrc234) | Write it as `@name` |
| A parameter of a composite type, a table's row type, `regclass`, or an extension's type that Npgsql cannot send, such as `seg` | [SQLSRC232](../../../diagnostics.md#sqlsrc232) | Declare it `text` and cast it where it is used: `-- param: @pair text` with `@pair::public.pair` |
| A column of type `record`, such as `ROW(a, b)` | [SQLSRC229](../../../diagnostics.md#sqlsrc229) | Select the fields as columns of their own, or cast the row to a composite type |
| A `@` or a `:` that SqlSource and Npgsql read differently | [SQLSRC231](../../../diagnostics.md#sqlsrc231) | Put a space before the parameter, or do what the error's `help:` line says |

<a id="models"></a>

## Models

```sql postgres
-- name: FindOrders
-- param: @since timestamptz
-- param: @status text null
SELECT o.id, o.placed_at, c.name AS customer_name
FROM orders o
LEFT JOIN customers c ON c.id = o.customer_id
WHERE o.placed_at >= @since AND (@status IS NULL OR o.status = @status);
```

```csharp
public sealed record FindOrdersParams(global::System.DateTime Since, string? Status);

public sealed record FindOrdersDto(long Id, global::System.DateTime PlacedAt, string? CustomerName);
```

A described PostgreSQL query gets the same [models](../../models.md#models) as every described query, by the rules of the Models page.  The sample leaves out their namespace and their documentation.  PostgreSQL decides these things in them:

- **Each property has the C# type of its column or parameter.**  That type comes from one of these:
  - [the type table](types.md#type-table), under the version of Npgsql that the project references;
  - the types of [NodaTime](libraries.md#nodatime) or of [NetTopologySuite](libraries.md#nettopologysuite), for a query whose output is `repository` or `methods`, in a project that references `Npgsql.NodaTime` or `Npgsql.NetTopologySuite`;
  - the types of [System.Text.Json](libraries.md#json), for a query whose output is `repository` or `methods`, where the library types turn `json` on;
  - the type that you chose, with the [type override](../../type-mapping.md#setting-type-overrides), a [column type](../../model-properties.md#setting-column-types) or a marker.

  In the sample, `id` is a `bigint` and `placed_at` a `timestamp with time zone`, both `NOT NULL`.
- **A column's property can be null by the rules of [which columns can be null](nullability.md#nullability).**  In the sample, `customer_name` comes from the optional side of the `LEFT JOIN`, so its property is a `string?`.  The alias of a column can say otherwise, with `!` or `?`.  So can a [`-- column:` marker](../../model-properties.md#column-marker).
- **A parameter's property can be null only in one of these cases**, since PostgreSQL does not say whether a parameter can be null:
  - its `-- param:` marker says `null`, as the marker of `@status` does;
  - it is a [list parameter](../../list-parameters.md#list-parameters), and the query tests the list for null, as `@ids IS NULL` does.
- **An array parameter's property is an `IEnumerable<T>`** in the input model of a query whose output is `repository` or `methods`.  In the input model of a query whose output is `models`, it is a `T[]`.  See [array parameters](query-methods.md#array-parameters).  A column's property is a `T[]` in both cases.
- **A column with no name is [SQLSRC253](../../../diagnostics.md#sqlsrc253).**  PostgreSQL calls such a column `?column?`: an expression or a literal without an alias.  The build reports the error, and the query gets no models until you fix it.  Give the column an alias.
- **A query whose rows are a whole table gets a model named after the table.**  The name has the schema of the table before it, unless the schema is `public`: `users` gives `UsersDto`, and `billing.invoices` gives `BillingInvoicesDto`.  See [table models](../../shared-models.md#table-models).
- **Updating Npgsql from 9 to 10 changes the models.**  It changes the types of the properties for `date`, `time`, `cidr`, `daterange` and `datemultirange`, as [which version of Npgsql](types.md#which-npgsql) says.

<a id="limits"></a>

## Limits

<a id="limit-operator-on-oid"></a>

### SQLSRC224 with `Npgsql cannot read the database's types`

**What you see.**  The queries of a database report [SQLSRC224](../../../diagnostics.md#sqlsrc224) with `Npgsql cannot read the database's types`, when the tool connects.

**Why.**  An operator `=` of your own on `oid` or `"char"` stops the tool from reading the database.  This happens when all of these are true:

- A schema on the search path of the connection that the tool uses has an operator `=` defined on two `oid` or on two `"char"`.
- That schema is before `pg_catalog` on the search path.

Npgsql then cannot read the types of the database when it connects.  The connections of your application are affected in the same way.

**What to do.**  On the search path of the connection that the tool uses, put that schema after `pg_catalog`, or leave it off.

<a id="limit-many-partitions"></a>

### Describing is slow on a table with thousands of partitions

**What you see.**  A query over a table with many partitions is slow to describe.  On a table of thousands of partitions, each such query takes longer to describe, and the [log](../../cli.md#logs) of the run grows.

**Why.**  To work out which columns can be null, the tool looks at every partition of a partitioned table, whatever the conditions of the query.

**What to do.**  Nothing needs to change.  Only describing is slower; nothing else is affected.
