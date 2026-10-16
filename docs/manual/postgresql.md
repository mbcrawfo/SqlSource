# PostgreSQL

SqlSource reads PostgreSQL files by the `postgres` [dialect](dialects.md), and the `sqlsource` tool can describe their queries: it asks your database what each query's parameters and columns are, and writes the answer to the query's [sidecar](tool.md#sidecar-files).  This page is about describing.  How SqlSource reads a PostgreSQL file, its comments and strings, is on [the dialects page](dialects.md).

```xml
<PropertyGroup>
    <SqlSourceDialect>postgres</SqlSourceDialect>
</PropertyGroup>
```

```console
$ dotnet sqlsource describe --connection postgres="Host=localhost;Database=app;Username=app;Password=secret"
postgres (postgres): 12 described, 0 skipped, 0 failed
```

The connection is given for the query's [logical database](tool.md#connections), which is `postgres` when you name none.

<a id="connecting"></a>

## Connecting

- **The connection string is Npgsql's**: `Host=localhost;Database=app;Username=app;Password=...`, with the keywords of [Npgsql's documentation](https://www.npgsql.org/doc/connection-string-parameters.html).  A string that Npgsql cannot read, or a database that cannot be opened, is [SQLSRC224](diagnostics.md#sqlsrc224).
- **PostgreSQL 14 and later are supported**, and SqlSource is tested on 14 and 18.  An older server is [SQLSRC235](diagnostics.md#sqlsrc235).
- **The tool needs your schema, not your data.**  A copy of the schema with no rows, such as a local database that your migrations created, is enough.
- **Connect directly to the server.**  The tool keeps one session open for each database and asks about each query in several steps, which must all reach that session.  A pooler in transaction mode, such as PgBouncer's, hands each step to a server connection of its own, and `Multiplexing=true` shares connections between commands: either is [SQLSRC236](diagnostics.md#sqlsrc236).  Hosted providers that pool connections offer a direct endpoint beside the pooled one; use that one for the tool.  Your application can still connect through the pooler.

<a id="what-describing-does"></a>

## What describing does to your database

**The tool never runs your queries.**  For each query it asks PostgreSQL to prepare the statement, which checks it and works out the types of its parameters and columns, and it reads the database's catalog to learn where each column comes from.  For some queries it also asks PostgreSQL how it would run the statement, without running it, to tell which of its columns can be null.  That step happens in a transaction that the tool rolls back.  When it is done, the tool frees every statement it prepared, and the session settings it changed ended with the transaction.

**One thing can have an effect.**  Describing a query that calls a function marked `IMMUTABLE` with constant arguments can run that function, since PostgreSQL computes such a call in advance.  A function that is marked `IMMUTABLE` and is not, such as one that advances a sequence, writes a file or calls another server, then has that effect, and rolling back does not undo it.  If your database has such a function, mark it `VOLATILE`, which is what it is, or describe against a copy of the schema where its effect does no harm.

<a id="parameters"></a>

## Parameters

```sql postgres
-- name: FindUsers
-- param: @name text
SELECT id, name FROM users
WHERE (@name IS NULL OR name = @name);
```

PostgreSQL gives each [query parameter](concepts.md#query-parameter) a type from the way the query uses it: in `WHERE id = @id`, `@id` takes the type of `id`.  A [`-- param:` marker](parameters-and-tokens.md#param-marker) gives a parameter a type of your own instead, which PostgreSQL checks against every use.

- **A use that says nothing of the type** is [SQLSRC226](diagnostics.md#sqlsrc226): `SELECT @p IS NULL` holds for a value of any type.  Cast the parameter where it is used, `@p::int`, or declare its type.
- **PostgreSQL takes the type from the first use.**  In the optional filter above, the first `@name` says nothing, so without the marker the query is [SQLSRC227](diagnostics.md#sqlsrc227).  Declare the type, as above, or turn the condition round: `(name = @name OR @name IS NULL)`.
- **The first use can also give a type you did not mean.**  In `placed_at > now() - @age`, `@age` becomes a `timestamp with time zone`, and the server then reports an operator that does not exist ([SQLSRC225](diagnostics.md#sqlsrc225)).  Write `@age::interval`, or declare it.
- **A declared type the database does not have** is [SQLSRC228](diagnostics.md#sqlsrc228).  A type in a schema that is not on the connection's search path needs its schema: `public.user_status`.
- **`@x` for the absolute value of `x`** is read as the parameter `x`.  Write `abs(x)`: see [names that are read as parameters](parameters-and-tokens.md#names-read-as-parameters).

<a id="what-can-be-described"></a>

## What can be described

The tool describes a query that is one statement of these kinds: `SELECT` (without `INTO`), `INSERT`, `UPDATE`, `DELETE`, `MERGE` (PostgreSQL 15 and later), `VALUES` and `TABLE`, each with a `WITH` in front of it or not.  For anything else, give the query `-- output: sql`: it keeps its constant or its token method, and the tool leaves it out.

| The query | Reported as | What to do |
|----|----|----|
| `CALL`, `CREATE`, `SET`, `SHOW` and other commands, `SELECT ... INTO` | [SQLSRC233](diagnostics.md#sqlsrc233) | Give it `-- output: sql`.  For `SELECT ... INTO`, write `CREATE TABLE ... AS` with `-- output: sql` |
| Two statements under one `-- name:` | [SQLSRC230](diagnostics.md#sqlsrc230) | Give each statement a `-- name:` of its own |
| A parameter written `$1` | [SQLSRC234](diagnostics.md#sqlsrc234) | Write it as `@name` |
| A parameter of a composite type, a table's row type or `regclass`, which Npgsql cannot send | [SQLSRC232](diagnostics.md#sqlsrc232) | Declare it `text` and cast it where it is used: `-- param: @pair text` with `@pair::public.pair` |
| A column of type `record`, such as `ROW(a, b)` | [SQLSRC229](diagnostics.md#sqlsrc229) | Select the fields as columns of their own, or cast the row to a composite type |
| A `@` or a `:` that SqlSource and Npgsql read differently | [SQLSRC231](diagnostics.md#sqlsrc231) | Put a space before the parameter, or do what the error's `help:` line says |

<a id="nullability"></a>

## Which columns can be null

```sql postgres
-- name: ListOrders
SELECT o.id, o.placed_at, c.name
FROM orders o
LEFT JOIN customers c ON c.id = o.customer_id;
```

For each column the tool records whether it can be null.  If `orders.id`, `orders.placed_at` and `customers.name` are all `NOT NULL`, `id` and `placed_at` above are never null, and `name` can be: the `LEFT JOIN` gives a null name to an order with no customer.

SqlSource treats a column as possibly null unless it can show that it never is.  A column it treats as possibly null that never is costs a null check in your code; it never gives you a wrong value.

SqlSource can tell that a column is never null when it is:

- **A `NOT NULL` column of a table**, which no outer join of the query can fill with null.
- **`count(*)`**, or a ranking over a window: `row_number()`, `rank()`, `dense_rank()`, `percent_rank()` or `cume_dist()`.
- **A constant** other than `NULL`, such as `1` or `'active'`.
- **A test**: `IS NULL`, `IS NOT NULL`, `IS DISTINCT FROM` or `IS NOT DISTINCT FROM`.
- **`COALESCE`, `GREATEST` or `LEAST`** with an argument that is never null, or **a `CASE` with an `ELSE`** whose every result is never null.
- **A column of a `UNION`, an `INTERSECT` or an `EXCEPT`** at the top of the query, when every value it can return there is never null by the rules above: every branch of a `UNION`, either side of an `INTERSECT`, the left side of an `EXCEPT`.

`count(*)` and the rankings, constants, tests, and `COALESCE`, `GREATEST`, `LEAST` and `CASE` each count only where an outer join cannot leave them null.

SqlSource treats a column as possibly null when:

- **The table column can hold null**: it is not `NOT NULL`, or its `NOT NULL` was added as `NOT VALID` and not validated.
- **The table can return null where its column says it cannot**: the table has or had inheritance children, it is partitioned and a foreign table is one of its partitions, or it has a `DO INSTEAD` rule on `INSERT`, `UPDATE` or `DELETE`.  Every column of such a table is possibly null.
- **An outer join can fill the column with null**: a column of the optional side of a `LEFT`, `RIGHT` or `FULL` join.
- **The column comes from a view or a materialized view.**  PostgreSQL does not record whether a view's column can be null.
- **The column comes from a foreign table.**  PostgreSQL does not enforce `NOT NULL` on its remote rows.
- **It is `NULL`**, or an expression that is not in the list above, such as `price * quantity`, `sum(total)`, `count(note)`, `EXISTS (...)` or a function call.
- **It is a column of a `UNION`, an `INTERSECT` or an `EXCEPT`** where a branch can give null, or where SqlSource cannot follow a branch.
- **The query has `ROLLUP`, `CUBE` or `GROUPING SETS`.**  Every column of such a query is possibly null.
- **The query has an outer join, `MERGE`, `OLD` or `NEW`, and the column comes through a subquery or a `WITH` query, or from an extension's own scan such as Citus's or TimescaleDB's.**  Such a column can be treated as possibly null even when its table column is `NOT NULL`.

> **Coming in a later release.**  Generated models will make a property nullable for each column that can be null, and non-nullable for each that never is.  See [what works today](README.md#what-works-today).

<a id="type-map"></a>

## Types

> **Coming in a later release.**  A table of the C# type that each PostgreSQL type becomes in a generated model or method.  See [what works today](README.md#what-works-today).

<a id="models"></a>

## Models

> **Coming in a later release.**  The generator will write a type for the parameters and a type for the result rows of each described PostgreSQL query.  See [what works today](README.md#what-works-today).

<a id="query-methods"></a>

## Query methods

> **Coming in a later release.**  The generator will write a method that runs each described PostgreSQL query through Npgsql and returns its rows.  See [what works today](README.md#what-works-today).

<a id="known-issues"></a>

## Known issues

- **Many columns that are never null are treated as possibly null.**  The rules [above](#nullability) leave out much that a person can see: `sum` or `max` of a `NOT NULL` column in a `GROUP BY` query, `count(x)`, `EXISTS (...)` as a column, the rows of `VALUES`, arithmetic on columns that are never null, a recursive `WITH` query, the columns of a view.  Nothing is wrong when that happens, and no generated code depends on it today.
- **Describing through a pooler can give the wrong error, or a wrong answer.**  Behind a pooler in transaction mode, most queries fail with [SQLSRC236](diagnostics.md#sqlsrc236), but some are reported as [SQLSRC231](diagnostics.md#sqlsrc231) with a message about the number of columns, and in rare cases a column can be recorded with the nullability of another statement that an earlier run left on the server.  Connect directly to the server, as [connecting](#connecting) says, and describe again with `--force`.
- **A type of your own named like a built-in one changes the names of built-in types.**  When the connection's search path puts a schema that has a type named like a built-in, such as a domain `public.text`, before `pg_catalog`, the tool records the built-in type as `pg_catalog.text` and not `text`.  Two databases that differ only in this describe the same query differently, and `--check` against one reports the sidecars written against the other as out of date.  Rename the type, or keep `pg_catalog` first on the search path of the connection the tool uses, the same on every machine.
- **A query over a table with many partitions is slow to describe.**  The tool looks at every partition of a partitioned table, whatever the query's conditions, when it works out which columns can be null.  On a table of thousands of partitions each such query takes longer to describe, and the [log](tool.md#logs) of the run grows.  Only describing is slower; nothing else is affected.
- **A function marked `IMMUTABLE` can run while a query is described.**  See [what describing does](#what-describing-does).
