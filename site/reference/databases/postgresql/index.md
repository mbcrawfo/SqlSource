# PostgreSQL

SqlSource reads PostgreSQL files by the `postgres` [dialect](../../../settings.md#setting-dialect), and the `sqlsource` tool can describe their queries: it asks your database what each query's parameters and columns are, and writes the answer to the query's [sidecar](../../sidecars.md#sidecar-files).  These pages are about describing, about the C# types that a described query's columns and parameters get, about the models built from them, and about the query methods that run them.  How SqlSource reads a PostgreSQL file, its comments and strings, is on [the dialects page](../../dialects.md#what-a-dialect-changes).

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

The connection is given for the query's [logical database](../../connections.md#connections), which is `postgres` when you name none, so its variable is `SQLSOURCE_CONNECTION_POSTGRES`, or `SQLSOURCE_CONNECTION` when it is the run's only database.  The summary names that database first, and its dialect in parentheses.

<a id="connecting"></a>

## Connecting

- **The connection string is Npgsql's**: `Host=localhost;Database=app;Username=app;Password=...`, with the keywords of [Npgsql's documentation](https://www.npgsql.org/doc/connection-string-parameters.html).  A string that Npgsql cannot read, or a database that cannot be opened, is [SQLSRC224](../../../diagnostics.md#sqlsrc224).
- **The supported versions of PostgreSQL** are in [Supported versions](../index.md#supported-versions).  An older server is [SQLSRC235](../../../diagnostics.md#sqlsrc235).
- **The tool needs your schema, not your data.**  A copy of the schema with no rows, such as a local database that your migrations created, is enough.
- **Connect directly to the server, or through a pooler in session or transaction mode.**  The tool keeps one session open for each database and describes each query in several steps, in one transaction.  A pooler in session or transaction mode, such as PgBouncer's, keeps that transaction on one server connection.  A pooler in statement mode refuses the transaction, and `Multiplexing=true` shares connections between commands: either is [SQLSRC236](../../../diagnostics.md#sqlsrc236).  Your application can still connect however it does.

<a id="what-describing-does"></a>

## What describing does to your database

**The tool never runs your queries.**  For each query it asks PostgreSQL to prepare the statement, which checks it and works out the types of its parameters and columns, and it reads the database's catalog to learn where each column comes from.  For some queries it also asks PostgreSQL how it would run the statement, without running it, to tell which of its columns can be null.  Each query is described in a transaction that the tool rolls back: the tool frees the statement it prepared before the end, and the session settings it changed end with the transaction.  While a query is described, a migration's `DROP TABLE` of a table it reads, or an `ALTER TABLE` that adds, drops or changes one of its columns, waits until the tool has finished with that query.

**One thing can have an effect.**  Describing a query that calls a function marked `IMMUTABLE` with constant arguments can run that function, since PostgreSQL computes such a call in advance.  A function that is marked `IMMUTABLE` and is not, such as one that advances a sequence, writes a file or calls another server, then has that effect, and rolling back does not undo it.  If your database has such a function, mark it `VOLATILE`, which is what it is, or describe against a copy of the schema where its effect does no harm.

<a id="parameters"></a>

## Parameters

```sql postgres
-- name: FindUsers
-- param: @name text
SELECT id, name FROM users
WHERE (@name IS NULL OR name = @name);
```

PostgreSQL gives each [query parameter](../../../concepts.md#query-parameter) a type from the way the query uses it: in `WHERE id = @id`, `@id` takes the type of `id`.  A [`-- param:` marker](../../parameters.md#param-marker) gives a parameter a type of your own instead, which PostgreSQL checks against every use.

- **A use that says nothing of the type** is [SQLSRC226](../../../diagnostics.md#sqlsrc226): `SELECT @p IS NULL` holds for a value of any type.  Cast the parameter where it is used, `@p::int`, or declare its type.
- **PostgreSQL takes the type from the first use.**  In the optional filter above, the first `@name` says nothing, so without the marker the query is [SQLSRC227](../../../diagnostics.md#sqlsrc227).  Declare the type, as above, or turn the condition round: `(name = @name OR @name IS NULL)`.
- **A [list parameter](../../parameters.md#list-parameters) takes its values' type from its use after `IN`**, and its test for null gives none, so the optional filter `(@ids IS NULL OR id IN @ids)` needs no marker: each value of `@ids` is of the type of `id`.
- **A list in the index predicate of `ON CONFLICT` fails when it is empty or null.**  In `INSERT ... ON CONFLICT (sku) WHERE (@kinds IS NULL OR kind IN @kinds) DO NOTHING`, the query method throws `PostgresException` with "cannot use subquery in index predicate" for an empty or a null list, though a list that holds a value runs.  A list there does nothing: the index predicate only chooses the unique index a conflict is found by, and one that holds a parameter cannot choose a partial index.  Leave the list out of it, write there the partial index's own predicate, if it has one, and test the list in the `WHERE` of `DO UPDATE` or of the `SELECT` that the `INSERT` reads.
- **A parameter compared with a column of a domain takes the domain's base type**: in `WHERE email = @email`, where `email` is a domain over `text`, `@email` is a `text`, so a [type override](../../../settings.md#setting-type-overrides) of the domain does not reach it.  Declare it as the domain, `-- param: @email public.email`, to give it the domain's C# type, or give it the C# type itself, `-- param: @email -> MyApp.Email`.
- **The first use can also give a type you did not mean.**  In `placed_at > now() - @age`, `@age` becomes a `timestamp with time zone`, and the server then reports an operator that does not exist ([SQLSRC225](../../../diagnostics.md#sqlsrc225)).  Write `@age::interval`, or declare it.
- **A declared type the database does not have** is [SQLSRC228](../../../diagnostics.md#sqlsrc228).  A type in a schema that is not on the connection's search path needs its schema: `public.user_status`.
- **`@x` for the absolute value of `x`** is read as the parameter `x`.  Write `abs(x)`: see [names that are read as parameters](../../parameters.md#names-read-as-parameters).

<a id="what-can-be-described"></a>

## What can be described

The tool describes a query that is one statement of these kinds: `SELECT` (without `INTO`), `INSERT`, `UPDATE`, `DELETE`, `MERGE` (PostgreSQL 15 and later), `VALUES` and `TABLE`, each with a `WITH` in front of it or not.  For anything else, give the query `-- output: sql`: it keeps its constant or its token method, and the tool leaves it out.

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

A described PostgreSQL query gets the [models](../../../generator.md#models) that every described query gets, by the rules of the generator page; their namespace and documentation are left out above.  What PostgreSQL decides in them:

- **Each property's type** is its column's or parameter's C# type, from [the type table](types.md#type-table) under the version of Npgsql that the project references, or [NodaTime's](libraries.md#nodatime) or [NetTopologySuite's](libraries.md#nettopologysuite) for a query whose output is `methods` or `repository` in a project that references `Npgsql.NodaTime` or `Npgsql.NetTopologySuite`, or [System.Text.Json's](libraries.md#json) for one where the library types turn `json` on, or the one you chose, by the [type override](../../../settings.md#setting-type-overrides), a [column type](../../../settings.md#setting-column-types) or a marker.  Above, `id` is a `bigint` and `placed_at` a `timestamp with time zone`, both `NOT NULL`.
- **A column's property can be null** by [which columns can be null](nullability.md#nullability): `customer_name` comes from the optional side of the `LEFT JOIN`, so its property is a `string?`.  A column's alias can say otherwise with `!` or `?`, and so can a [`-- column:` marker](../../../generator.md#column-marker).
- **A parameter's property can be null** only when its `-- param:` marker says `null`, as `@status` does, or, for a [list parameter](../../parameters.md#list-parameters), when the query tests the list for null, as `@ids IS NULL` does, since PostgreSQL does not say whether a parameter can be null.
- **An array parameter's property is an `IEnumerable<T>`** in the input model of a query whose output is `methods` or `repository`, and a `T[]` in that of a query whose output is `models`: see [array parameters](query-methods.md#array-parameters).  A column's property is a `T[]` in both.
- **A column with no name**, which PostgreSQL calls `?column?`, as an expression or a literal without an alias, is [SQLSRC253](../../../diagnostics.md#sqlsrc253), and the query gets no models until it is fixed.  Give it an alias.
- **A query whose rows are a whole table** gets a model named after the table, with the table's schema before its name unless the schema is `public`: `users` gives `UsersDto`, and `billing.invoices` gives `BillingInvoicesDto`.  See [table models](../../../generator.md#table-models).
- **Updating Npgsql from 9 to 10** changes the types of the properties for `date`, `time`, `cidr`, `daterange` and `datemultirange`, as [which version of Npgsql](types.md#which-npgsql) says, and so the models.

<a id="limits"></a>

## Limits

<a id="limit-operator-on-oid"></a>

### SQLSRC224 with `Npgsql cannot read the database's types`

- **An operator `=` of your own on `oid` or `"char"` stops the tool from reading the database.**  When a schema before `pg_catalog` on the search path of the connection the tool uses has an operator `=` defined on two `oid` or on two `"char"`, Npgsql cannot read the database's types when the tool connects, and the database's queries report [SQLSRC224](../../../diagnostics.md#sqlsrc224) with `Npgsql cannot read the database's types`.  Your application's own connections are affected the same way.  Put that schema after `pg_catalog` on the search path of the connection the tool uses, or leave it off.

<a id="limit-many-partitions"></a>

### Describing is slow on a table with thousands of partitions

- **A query over a table with many partitions is slow to describe.**  The tool looks at every partition of a partitioned table, whatever the query's conditions, when it works out which columns can be null.  On a table of thousands of partitions each such query takes longer to describe, and the [log](../../cli.md#logs) of the run grows.  Only describing is slower; nothing else is affected.
