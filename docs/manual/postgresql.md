# PostgreSQL

SqlSource reads PostgreSQL files by the `postgres` [dialect](dialects.md), and the `sqlsource` tool can describe their queries: it asks your database what each query's parameters and columns are, and writes the answer to the query's [sidecar](tool.md#sidecar-files).  This page is about describing, about the C# types that a described query's columns and parameters get, about the models built from them, and about the query methods that run them.  How SqlSource reads a PostgreSQL file, its comments and strings, is on [the dialects page](dialects.md).

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

The connection is given for the query's [logical database](tool.md#connections), which is `postgres` when you name none, so its variable is `SQLSOURCE_CONNECTION_POSTGRES`, or `SQLSOURCE_CONNECTION` when it is the run's only database.  The summary names that database first, and its dialect in parentheses.

<a id="connecting"></a>

## Connecting

- **The connection string is Npgsql's**: `Host=localhost;Database=app;Username=app;Password=...`, with the keywords of [Npgsql's documentation](https://www.npgsql.org/doc/connection-string-parameters.html).  A string that Npgsql cannot read, or a database that cannot be opened, is [SQLSRC224](diagnostics.md#sqlsrc224).
- **PostgreSQL 14 and later are supported**, and SqlSource is tested on every major from 14 to 18.  An older server is [SQLSRC235](diagnostics.md#sqlsrc235).
- **The tool needs your schema, not your data.**  A copy of the schema with no rows, such as a local database that your migrations created, is enough.
- **Connect directly to the server, or through a pooler in session or transaction mode.**  The tool keeps one session open for each database and describes each query in several steps, in one transaction.  A pooler in session or transaction mode, such as PgBouncer's, keeps that transaction on one server connection.  A pooler in statement mode refuses the transaction, and `Multiplexing=true` shares connections between commands: either is [SQLSRC236](diagnostics.md#sqlsrc236).  Your application can still connect however it does.

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

PostgreSQL gives each [query parameter](concepts.md#query-parameter) a type from the way the query uses it: in `WHERE id = @id`, `@id` takes the type of `id`.  A [`-- param:` marker](parameters-and-tokens.md#param-marker) gives a parameter a type of your own instead, which PostgreSQL checks against every use.

- **A use that says nothing of the type** is [SQLSRC226](diagnostics.md#sqlsrc226): `SELECT @p IS NULL` holds for a value of any type.  Cast the parameter where it is used, `@p::int`, or declare its type.
- **PostgreSQL takes the type from the first use.**  In the optional filter above, the first `@name` says nothing, so without the marker the query is [SQLSRC227](diagnostics.md#sqlsrc227).  Declare the type, as above, or turn the condition round: `(name = @name OR @name IS NULL)`.
- **A parameter compared with a column of a domain takes the domain's base type**: in `WHERE email = @email`, where `email` is a domain over `text`, `@email` is a `text`, so a [type override](settings.md#setting-type-overrides) of the domain does not reach it.  Declare it as the domain, `-- param: @email public.email`, to give it the domain's C# type, or give it the C# type itself, `-- param: @email -> MyApp.Email`.
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
| A parameter of a composite type, a table's row type, `regclass`, or an extension's type that Npgsql cannot send, such as `seg` | [SQLSRC232](diagnostics.md#sqlsrc232) | Declare it `text` and cast it where it is used: `-- param: @pair text` with `@pair::public.pair` |
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

For each column the tool records whether it can be null.  The usual reasons a column is recorded as possibly null are an outer join, a view, or an expression such as `initcap(name)` or a call of a function that the lists below do not name; the lists give every rule.  If `orders.id`, `orders.placed_at` and `customers.name` are all `NOT NULL`, `id` and `placed_at` above are never null, and `name` can be: the `LEFT JOIN` gives a null name to an order with no customer.

SqlSource treats a column as possibly null unless it can show that it never is.  A column it treats as possibly null that never is costs a null check in your code.  It never gives you a wrong value.

SqlSource can tell that a column is never null when it is:

- **A `NOT NULL` column of a table**, which no outer join of the query can fill with null.
- **`count(*)`, or `count(x)` of anything**, or a ranking over a window: `row_number()`, `rank()`, `dense_rank()`, `percent_rank()` or `cume_dist()`.  A count is `0` for no rows, never null.
- **A constant** other than `NULL`, such as `1` or `'active'`.
- **A test**: `IS NULL`, `IS NOT NULL`, `IS DISTINCT FROM` or `IS NOT DISTINCT FROM`.
- **Arithmetic or a comparison of values that are never null**: `+`, `-`, `*` or `/` of two numbers; `=`, `<>`, `<`, `<=`, `>` or `>=` of two numbers, two texts, two booleans, or two values of one of `date`, `timestamp` and `timestamp with time zone`; and `||` of two texts.
- **`lower` or `upper` of a text, `length` of a `text` or a `varchar` (not of a `character(n)`), or `date_trunc` of a `date`, a `timestamp` or a `timestamp with time zone` without a time zone argument**, over values that are never null, and **a cast** of a value that is never null from one number type to another, from one text type to another, or between `date`, `timestamp` and `timestamp with time zone`.
- **One of the expressions above over a system column of a table**, such as `xmax = 0` in `RETURNING (xmax = 0) AS inserted`.  A system column on its own, `RETURNING xmax`, is possibly null, except as a column of a `UNION`, an `INTERSECT` or an `EXCEPT` below, where a system column of a table counts as never null: `SELECT xmin FROM a UNION ALL SELECT xmin FROM b` is never null.
- **`COALESCE`, `GREATEST` or `LEAST`** with an argument that is never null, or **a `CASE` with an `ELSE`** whose every result is never null.  Arithmetic, a comparison, a function, a cast, an aggregate and a subquery's value over one are read by the rules in this list, so `sum(coalesce(x, 0))` in a query with a `GROUP BY` is never null, unless the `WHERE` sets every column of the `GROUP BY` to one value.  This holds where the tool knows the form's type from its arguments that are never null; a form over arguments of different types, or of a type the tool does not know, counts only on its own.
- **`array_agg(...)` in a query with a `GROUP BY` of one column or more**, with no `FILTER` and no `OVER`: each group has a row, and the array of a row is never null.  It can be possibly null when the `WHERE` sets every column of the `GROUP BY` to one value, as `WHERE o.user_id = @userId GROUP BY o.user_id` does.
- **`jsonb_build_object(...)` and `json_build_object(...)`**, unless the call is written with `VARIADIC`, which gives null for a null array.
- **A range's constructor** (`int4range`, `int8range`, `numrange`, `tsrange`, `tstzrange` or `daterange`, with two or three arguments), and **the intersection `*` of two ranges** that are never null.
- **`COALESCE`, `GREATEST` or `LEAST` over a column of each side of a `FULL JOIN`**, as `USING` writes the merged column: `coalesce(a.id, r.account_id)`, with a column that is never null on each side.  A row of a full join comes from one side at least, so one of the two columns holds a value.  It counts only where neither side is itself an outer join; an inner join under a side is fine.  A form above the join, or one over a cast of a column, does not count.
- **`max`, `min`, `sum` or `avg` of numbers, `max` or `min` of texts or of `date`, `timestamp` or `timestamp with time zone`, `string_agg` of two texts, or `bool_and`, `bool_or` or `every`** of values that are never null, in a query with a `GROUP BY` of one column or more, when it has no `FILTER` and no `OVER`.  Each group of such a query has a row; without `GROUP BY`, a query over no rows returns one row whose `sum` is null.  It can be possibly null when the `WHERE` sets every column of the `GROUP BY` to one value, as `WHERE o.user_id = @userId GROUP BY o.user_id` does.
- **A subquery that is the whole column, uses a column of the outer query and returns one row**, such as `(SELECT count(*) FROM orders o WHERE o.account_id = a.id)`: one with an aggregate and no `GROUP BY`, `HAVING`, `LIMIT` or `OFFSET`, whose value is `count(*)`, or arithmetic, a function or a cast of the list above over values that are never null, of a number type, a text type, or `date`, `timestamp` or `timestamp with time zone`.  The same subquery inside an expression, `(SELECT count(*) ...) + 1`, is possibly null.
- **`EXISTS (...)` whose subquery uses a column of the outer query, on PostgreSQL 17 and later.**  PostgreSQL may run an `EXISTS` whose subquery compares one of its columns with one of the outer query's as an `IN`, and such an `EXISTS` is never null only as that `IN` would be.
- **`x IN (SELECT y ...)` or `x = ANY (SELECT y ...)`, on PostgreSQL 17 and later,** when `x` and the subquery's `y` are never null by the rules above, and the comparison is one of those above.
- **`merge_action()`** in the `RETURNING` list of a `MERGE`, on PostgreSQL 17 and later.
- **A column of a `UNION`, an `INTERSECT` or an `EXCEPT`** at the top of the query, when every value it can return there is never null by the rules above: every branch of a `UNION`, either side of an `INTERSECT`, the left side of an `EXCEPT`.

`count(x)`, `array_agg`, the JSON builders and the range constructors and intersections count only when no schema of the database but `pg_catalog` holds a function or an operator of that name, and none was added to `pg_catalog`; a name that another schema holds as well, or that was added to `pg_catalog`, is possibly null, as the lists below say.

`count(*)` and the rankings, `count(x)`, `array_agg`, the JSON builders, the range constructors and intersections, constants, tests, the arithmetic, comparisons, functions, casts, aggregates and subqueries above, `merge_action()`, and `COALESCE`, `GREATEST`, `LEAST` and `CASE` each count only where an outer join cannot leave them null.

SqlSource treats a column as possibly null when:

- **The table column can hold null**: it is not `NOT NULL`, or its `NOT NULL` was added as `NOT VALID` and not validated.
- **The table can return null where its column says it cannot**: the table has or had inheritance children, it is partitioned and a foreign table is one of its partitions, or it has a `DO INSTEAD` rule on `INSERT`, `UPDATE` or `DELETE`.  Every column of such a table is possibly null.
- **An outer join can fill the column with null**: a column of the optional side of a `LEFT`, `RIGHT` or `FULL` join.
- **The column comes from a view or a materialized view.**  PostgreSQL does not record whether a view's column can be null.
- **The column comes from a foreign table.**  PostgreSQL does not enforce `NOT NULL` on its remote rows.
- **It is `NULL`**, or an expression that is not in the list above, such as `initcap(name)`, `array_agg(...)` without `GROUP BY` or with `FILTER`, `upper(during)` of a range, `price * quantity` where either column can hold null, `sum(total)` without `GROUP BY` or with a `GROUP BY` whose every column the `WHERE` sets to one value, `EXISTS (...)` before PostgreSQL 17, or a call of another function.
- **It is a function or an operator of a name that another schema of the database holds as well**, such as a `count` or a `*` that an extension installs, in its own schema or in `pg_catalog`, in a column the list above would have read.  The tool cannot tell which one the query calls.
- **It is a column of a `UNION`, an `INTERSECT` or an `EXCEPT`** where a branch can give null, or where SqlSource cannot follow a branch.
- **It is an expression that a subquery in `FROM` or a `WITH` query computes**, such as a `count(*)` that a `WITH` query returns, **or one computed over a `UNION`, an `INTERSECT` or an `EXCEPT`**, such as `count(*)` of a subquery that holds one.  Such a column can be treated as possibly null even when the list above says it is never null.
- **The query has `ROLLUP`, `CUBE` or `GROUPING SETS`.**  Every column of such a query is possibly null.
- **The query names `OLD` or `NEW`, and the column is a subquery, an `IN (SELECT ...)` or an `= ANY (SELECT ...)`**, even one the list above says is never null.
- **The connection's search path puts another schema before `pg_catalog`, and the column is `count(*)`, `count(x)`, a ranking, `array_agg`, a JSON builder, a range constructor or intersection, arithmetic, a comparison, a function, a cast, an aggregate, a subquery, or an `EXISTS (...)` that PostgreSQL runs as an `IN`**, even one the list above says is never null.  PostgreSQL may run so an `EXISTS` whose subquery compares one of its columns with one of the outer query's, as the list above says.  A `COALESCE`, `GREATEST` or `LEAST` over both sides of a `FULL JOIN` is affected too.  Constants, tests, `COALESCE` over columns that are never null, any other `EXISTS (...)` and `merge_action()` are not affected.  Keep `pg_catalog` first on the search path of the connection the tool uses, or leave it off the path, where PostgreSQL searches it first.
- **The query has an outer join, `MERGE`, `OLD` or `NEW`, and the column comes through a subquery or a `WITH` query, or from a table that an extension such as Citus or TimescaleDB manages.**  Such a column can be treated as possibly null even when its table column is `NOT NULL`.

A column that can be null gets a nullable property in the query's [output model](#models), `int?` or `string?`, and a column that is never null a property that cannot be null, `int` or `string`.  Where you know better than these rules, you decide instead: an alias that ends in `!` gives a property that cannot be null, and one that ends in `?` a property that can, or a `-- column:` marker says the same and leaves the column's name alone.

```sql postgres
-- name: GetCustomerLabels
SELECT c.id, initcap(c.name) AS "display_name!"
FROM customers c;
```

`initcap` is never null for a value that is not, though SqlSource treats it as possibly null: `"display_name!"` gives the property `DisplayName` the type `string` and not `string?`.  Write the alias in quotes, since `!` and `?` are not part of a plain name, and the mark is left out of the property's name.  A `!` is your word, and the build does not check it: a [query method](generator.md#reading-rows) throws when such a column is null after all.  The mark stays in the column's name, which Dapper matches to the property's by name, so with Dapper write a `-- column:` marker and no mark:

```sql postgres
-- name: GetCustomerLabels
-- column: display_name not null
SELECT c.id, initcap(c.name) AS display_name
FROM customers c;
```

See [which properties can be null](generator.md#model-nullability) and [the `-- column:` marker](generator.md#column-marker).

<a id="type-map"></a>

## Types

```sql postgres
-- name: GetOrder
SELECT id, placed_at, total, tags, born
FROM orders
WHERE id = @id;
```

Each column and each parameter of a described PostgreSQL query gets a C# type: the type that Npgsql reads a value of its PostgreSQL type as.  If `id` is a `bigint`, `placed_at` a `timestamp with time zone`, `total` a `numeric(12,2)`, `tags` a `text[]` and `born` a `date`, they are `long`, `System.DateTime`, `decimal`, `string[]`, and `System.DateTime` on Npgsql 8 and 9 or `System.DateOnly` on Npgsql 10; `@id` is a `long`.  Whether a column can be null is decided [above](#nullability), not by its type.

The build gives these types to each query whose output is `models` or `codegen` and whose entry in its sidecar is current, and reports an error for a column or parameter that has none, such as [SQLSRC246](diagnostics.md#sqlsrc246) for a type that Npgsql cannot read.  They are the types of the properties of the query's [models](#models), and a query with such an error has no models.  To give a type another C# type, use the [type override](settings.md#setting-type-overrides), `"timestamptz": "System.DateTimeOffset"` in a settings file's `typeOverrides`.  To give one column another, use a [column type](settings.md#setting-column-types), `"users.id": "MyApp.UserId"` in its `columnTypes`, and one parameter, its [`-- param:` marker](parameters-and-tokens.md#param-marker), `-- param: @id -> MyApp.UserId`.  A query method reads and sends that C# type as it is, so a type of yours that Npgsql does not read, such as an id or an `Email` over a domain, needs [converters](settings.md#converters) to and from the type of the table below.  A project that references `Npgsql.NodaTime` gets NodaTime's types for the date and time types of its queries whose output is `codegen`: see [NodaTime](#nodatime).  One that references `Npgsql.NetTopologySuite` gets NetTopologySuite's for PostGIS's types: see [NetTopologySuite](#nettopologysuite).  A project can read its JSON columns as System.Text.Json's types or as classes of its own: see [JSON](#json).

<a id="which-npgsql"></a>

### Which version of Npgsql

A few types are read as another C# type by Npgsql 10 than by Npgsql 8 and 9, so the types follow the `Npgsql` package that the project references, directly or through another package:

- **Npgsql 8, 9 or 10** gets the column of the table below for its version.  Updating Npgsql from 9 to 10 changes the C# types of `date`, `time`, `cidr`, `daterange` and `datemultirange`, as it changes what Npgsql reads.
- **Npgsql older than 8** is [SQLSRC247](diagnostics.md#sqlsrc247).  Update it.
- **Npgsql 11 or later** gets the types of Npgsql 10, with the information [SQLSRC248](diagnostics.md#sqlsrc248), since this version of SqlSource does not know a newer one.
- **No Npgsql**, as in a library that holds the queries and leaves the driver to the application, gets every type that Npgsql 8, 9 and 10 read alike, and models alone: give its queries the output `models`, since a query method needs the driver ([SQLSRC273](diagnostics.md#sqlsrc273)).  A type that they read differently, and a type of Npgsql's own `NpgsqlTypes` namespace, have no C# type without the driver: each is [SQLSRC249](diagnostics.md#sqlsrc249), unless the type override names it.  For the types that differ, the override says which Npgsql the application runs on: `"date": "System.DateOnly"` and `"time": "System.TimeOnly"` in a settings file's `typeOverrides` for Npgsql 10.

<a id="type-table"></a>

### The types

The names are those of PostgreSQL's catalog, which are the [type override's](settings.md#setting-type-overrides) keys, with the other spellings PostgreSQL accepts in parentheses.  A type of your own schema is written in a key with its schema, `public.user_status`, and a schema or a name created in double quotes keeps its quotes there: `public."Order Status"`.  A type's length, precision or scale does not change its C# type, but for `bit` and `varbit`.  A cell "Same" is the C# type of Npgsql 8 and 9.

| PostgreSQL type | Npgsql 8 and 9 | Npgsql 10 | Without Npgsql |
|----|----|----|----|
| `bool` (`boolean`) | `bool` | Same | Same |
| `int2` (`smallint`) | `short` | Same | Same |
| `int4` (`integer`) | `int` | Same | Same |
| `int8` (`bigint`) | `long` | Same | Same |
| `float4` (`real`) | `float` | Same | Same |
| `float8` (`double precision`) | `double` | Same | Same |
| `numeric` (`numeric(p,s)`, `decimal`), `money` | `decimal` | Same | Same |
| `text`, `varchar` (`character varying`), `bpchar` (`character`, `char(n)`), `name`, `refcursor`, `citext` | `string` | Same | Same |
| `char` (`"char"`, the one-byte type) | `char` | Same | Same |
| `bytea` | `byte[]` | Same | Same |
| `date` | `System.DateTime` | `System.DateOnly` | By the type override only |
| `time` (`time without time zone`) | `System.TimeSpan` | `System.TimeOnly` | By the type override only |
| `timetz` (`time with time zone`) | `System.DateTimeOffset` | Same | Same |
| `timestamp` (`timestamp without time zone`), `timestamptz` (`timestamp with time zone`) | `System.DateTime` | Same | Same |
| `interval` | `System.TimeSpan` | Same | Same |
| `uuid` | `System.Guid` | Same | Same |
| `json`, `jsonb`, `jsonpath`, `xml` | `string` | Same | Same |
| `inet` | `System.Net.IPAddress` | Same | Same |
| `cidr` | `NpgsqlTypes.NpgsqlCidr` | `System.Net.IPNetwork` | By the type override only |
| `macaddr`, `macaddr8` | `System.Net.NetworkInformation.PhysicalAddress` | Same | Same |
| `bit(1)`, `varbit(1)` (`bit varying(1)`) | `bool` | Same | Same |
| `bit(n)`, `varbit(n)`, and either with no length | `System.Collections.BitArray` | Same | Same |
| `point`, `line`, `lseg`, `box`, `path`, `polygon`, `circle` | `NpgsqlTypes.NpgsqlPoint`, `NpgsqlTypes.NpgsqlLine`, `NpgsqlTypes.NpgsqlLSeg`, `NpgsqlTypes.NpgsqlBox`, `NpgsqlTypes.NpgsqlPath`, `NpgsqlTypes.NpgsqlPolygon`, `NpgsqlTypes.NpgsqlCircle` | Same | SQLSRC249 |
| `tsvector`, `tsquery` | `NpgsqlTypes.NpgsqlTsVector`, `NpgsqlTypes.NpgsqlTsQuery` | Same | SQLSRC249 |
| `oid`, `xid`, `cid`, `regtype`, `regconfig` | `uint` | Same | Same |
| `xid8` | `ulong` | Same | Same |
| `tid`, `pg_lsn` | `NpgsqlTypes.NpgsqlTid`, `NpgsqlTypes.NpgsqlLogSequenceNumber` | Same | SQLSRC249 |
| `int2vector`, `oidvector` | `short[]`, `uint[]` | Same | Same |
| `hstore` | `System.Collections.Generic.Dictionary<string, string?>` | Same | Same |
| `ltree`, `lquery`, `ltxtquery` | `string` | Same | Same |
| `cube` | None: [SQLSRC246](diagnostics.md#sqlsrc246).  Update Npgsql to 10, or cast the column to `text` in the query | `NpgsqlTypes.NpgsqlCube` | SQLSRC249 |
| `int4range`, `int8range`, `numrange` | `NpgsqlTypes.NpgsqlRange<int>`, `NpgsqlTypes.NpgsqlRange<long>`, `NpgsqlTypes.NpgsqlRange<decimal>` | Same | SQLSRC249 |
| `tsrange`, `tstzrange` | `NpgsqlTypes.NpgsqlRange<System.DateTime>` | Same | SQLSRC249 |
| `daterange` | `NpgsqlTypes.NpgsqlRange<System.DateTime>` | `NpgsqlTypes.NpgsqlRange<System.DateOnly>` | SQLSRC249 |
| `int4multirange`, `int8multirange`, `nummultirange`, `tsmultirange`, `tstzmultirange`, `datemultirange` | An array of the range's type: `NpgsqlTypes.NpgsqlRange<int>[]` and so on | As the range: `NpgsqlTypes.NpgsqlRange<System.DateOnly>[]` for `datemultirange` | SQLSRC249 |
| `regclass`, `regproc`, `regprocedure`, `regoper`, `regoperator`, `regrole`, `regnamespace`, `regcollation`, `regdictionary` | None: SQLSRC246.  Cast the column to `oid` in the query | Same | Same |
| `pg_snapshot`, `txid_snapshot`, `pg_node_tree` | None: SQLSRC246.  Cast the column to `text` in the query | Same | Same |
| `geometry`, `geography`, PostGIS's types, with or without a modifier such as `(Point,4326)` | None: SQLSRC246.  In the query methods of a project that references `Npgsql.NetTopologySuite`, `NetTopologySuite.Geometries.Geometry`: see [NetTopologySuite](#nettopologysuite) | Same | Same |

`citext`, `hstore`, `ltree`, `lquery`, `ltxtquery` and `cube` are the types of extensions, and are known by their name in whatever schema the extension was created.  So are PostGIS's `geometry` and `geography`, but a type override names them with that schema, `public.geometry`.  A type that the table does not name gets its C# type by its kind:

| Kind | C# type |
|----|----|
| An array | Its element's C# type with `[]`: `int[]` for `int4[]`, `string[]` for `text[]`, `string[]` for an array of an enum |
| A domain | Its base type's C# type: a domain over `text` is a `string` |
| An enum | `string`.  Npgsql reads an enum as its label.  To read it as a C# enum, map it with the type override, `public.user_status=MyApp.UserStatus`, and call `MapEnum<MyApp.UserStatus>()` on the application's data source builder |
| A range or a multirange that the table does not name, a range of your own over `int4` included | None: SQLSRC246.  Map it with the type override to `NpgsqlTypes.NpgsqlRange<T>` of its subtype's C# type, and call `EnableUnmappedTypes()` on the data source builder, without which Npgsql cannot read it.  A project without Npgsql references it first, since that type is Npgsql's |
| A composite type, which includes a table's row type | None: SQLSRC246.  Map it with the type override to the type the application's data source maps it to, or select its fields as columns of their own |
| A base type of `pg_catalog` that the table does not name, such as `aclitem` | None: SQLSRC246.  No version of Npgsql reads it: cast the column to `text` in the query |
| Any other base type that the table does not name, such as one of an extension SqlSource does not know | None: SQLSRC246.  Map it with the type override to the type the application reads it as |

<a id="type-notes"></a>

### What to know about some types

- **`timestamptz`** is read as a `System.DateTime` whose `Kind` is `Utc`.  `timestamp`, and `date` on Npgsql 8 and 9, are read with the `Kind` `Unspecified`.  `infinity` and `-infinity` in a `timestamptz` are read as `DateTime.MaxValue` and `DateTime.MinValue`, with the `Kind` `Unspecified`.  To give a `timestamptz` the C# type `System.DateTimeOffset`, map it: `timestamptz=System.DateTimeOffset`.  A query method reads and sends it with nothing more; rows read through Dapper need a type handler: see [the recipe](recipes.md#dapper-with-postgresql).
- **`numeric`** values `NaN`, `Infinity` and `-Infinity` cannot be read as a `decimal`, and reading one throws.  Keep them out of the column, or cast it to `float8` in the query, which reads them as a `double`.
- **An `interval` with months or years** cannot be read as a `System.TimeSpan`, and reading one throws.  Map it, `interval=NpgsqlTypes.NpgsqlInterval`, or give one column that type with a [column type](settings.md#setting-column-types), and a query method reads and sends it as an `NpgsqlInterval`.
- **`inet`** is read as a `System.Net.IPAddress`, which drops the netmask.  Map it, `inet=NpgsqlTypes.NpgsqlInet`, or give one column that type with a column type, and a query method reads and sends it as an `NpgsqlInet`, which keeps the netmask.
- **`char(n)`**, `char(1)` included, is a `string`.  The `char` of the table is PostgreSQL's one-byte type, which is written `"char"` with its quotes.
- **`bit(1)`** is a `bool`, and a `bit(1)[]` column a `bool[]`.  A parameter's type has no length, so a parameter of `bit(1)` is a `System.Collections.BitArray`.
- **An array that holds a `NULL` element** needs a nullable element, `int?[]`, for an element of a value type, and the type override gives it: `int4[]=int?[]`.  Npgsql also refuses to read such an array under its default `Array Nullability Mode=Never`, so set `Array Nullability Mode=Always` or `PerInstance` in the application's connection string.  An array of a reference type, `text[]`, is read with its `NULL` elements whatever the mode; map `text[]=string?[]` to say that an element can be null.
- **A multidimensional array** is read as `int[,]` and not `int[]`, and its column's type does not say how many dimensions it has, so a column of `int4[]` that holds a two-dimensional array cannot be read into an `int[]`.  Map it, `int4[]=int[,]`, where every value of the column has two dimensions.
- **The settings of the application's data source can change what Npgsql reads**, and SqlSource cannot see them.  The table is what a data source made with `NpgsqlDataSourceBuilder`, or a plain `NpgsqlConnection`, reads.  `NpgsqlSlimDataSourceBuilder` reads fewer types until its `Enable...()` methods are called.  The AppContext switch `Npgsql.EnableLegacyTimestampBehavior`, and Npgsql 10's legacy date and time resolver, read some date and time types as other C# types; map those types with the type override to the C# types the application reads.

<a id="nodatime"></a>

### NodaTime

```sql postgres
-- name: GetShift -> one
SELECT id, starts_at, day, length, slots
FROM shifts
WHERE id = @id;
```

```csharp
public sealed record GetShiftDto(
    long Id,
    global::NodaTime.Instant StartsAt,
    global::NodaTime.LocalDate Day,
    global::NodaTime.Period Length,
    global::NpgsqlTypes.NpgsqlRange<global::NodaTime.Instant> Slots);
```

A project that references `Npgsql.NodaTime`, Npgsql's plugin for NodaTime, gets NodaTime's types for the columns and parameters of its queries whose output is `codegen`, read and sent by the plugin.  Above, `starts_at` is a `timestamptz`, `day` a `date`, `length` an `interval` and `slots` a `tstzrange`, each `NOT NULL`.  The [library types](settings.md#setting-library-types) turn NodaTime off for a project, a folder, a file or a query.  The types are the same on Npgsql 8, 9 and 10: reference the `Npgsql.NodaTime` of the same major as your Npgsql.

| PostgreSQL type | C# type with NodaTime |
|----|----|
| `timestamptz` (`timestamp with time zone`) | `NodaTime.Instant` |
| `timestamp` (`timestamp without time zone`) | `NodaTime.LocalDateTime` |
| `date` | `NodaTime.LocalDate` |
| `time` (`time without time zone`) | `NodaTime.LocalTime` |
| `timetz` (`time with time zone`) | `NodaTime.OffsetTime` |
| `interval` | `NodaTime.Period` |
| `tstzrange`, `tsrange`, `daterange` | `NpgsqlTypes.NpgsqlRange<NodaTime.Instant>`, `NpgsqlTypes.NpgsqlRange<NodaTime.LocalDateTime>`, `NpgsqlTypes.NpgsqlRange<NodaTime.LocalDate>` |
| `tstzmultirange`, `tsmultirange`, `datemultirange` | An array of the range's type: `NpgsqlTypes.NpgsqlRange<NodaTime.Instant>[]` and so on |

An array of one of these types is an array of its C# type, `NodaTime.Instant[]` for `timestamptz[]`, and a domain over one has its C# type, as [the kinds](#type-table) say.  Every other type keeps the type of [the types](#type-table).

- **Only query methods get NodaTime's types.**  A query whose output is `models` keeps the types of [the types](#type-table), and so do its models, whether rows are read through Dapper or your own code.  A model that such a query shares with a `codegen` query is [SQLSRC257](diagnostics.md#sqlsrc257): see [library types](settings.md#setting-library-types).
- **Your own choices win.**  A [type override](settings.md#setting-type-overrides), a [column type](settings.md#setting-column-types) and a `-- param:` or `-- column:` marker give their type over NodaTime's, so a project with `"timestamptz": "System.DateTimeOffset"` keeps its `DateTimeOffset` columns.  `"timestamptz": "default"` in a nearer settings file, or `-- type-override: timestamptz=default` in a file, gives `timestamptz` to NodaTime there.
- **The plugin reads other types too**, which the type override, a column type or a marker can give: a `timestamptz` as a `NodaTime.ZonedDateTime`, read in the zone UTC, a `NodaTime.OffsetDateTime`, read at offset 0, a `System.DateTimeOffset` or a `System.DateTime`; a `timestamp` as a `System.DateTime`; an `interval` as a `NodaTime.Duration`; a `tstzrange` as a `NodaTime.Interval` and a `daterange` as a `NodaTime.DateInterval`, with their multiranges as arrays of them.  A query method reads and sends each through the plugin.
- **A `ZonedDateTime` or an `OffsetDateTime` is sent in UTC alone.**  One in another zone, or at another offset, throws `ArgumentException`, the query does not run, and the connection is closed: a transaction open on it is lost, and the next command on it throws `InvalidOperationException`, so open a new connection to go on.  Npgsql 8 and 10 do the same.  Convert the value to UTC before the call, with `value.WithZone(DateTimeZone.Utc)` or `value.WithOffset(Offset.Zero)`; a value in UTC leaves the connection open.
- **An `interval` with months or years** read as a `NodaTime.Duration` throws `InvalidCastException`, which says to read it as a `NodaTime.Period`, the default.
- **`NodaTime.Interval` and `NodaTime.DateInterval` cannot hold every range**, which is why the defaults are `NpgsqlRange<T>`.  An empty range throws when it is read: `ArgumentOutOfRangeException` for a `tstzrange` as an `Interval`, and `ArgumentException` for a `daterange` as a `DateInterval`.  A `tstzrange` whose start is excluded, `(a,b]`, is read as the `Interval` `[a+1ns, b+1ns)`, since an `Interval` includes its start and excludes its end, and sent back it is stored as `[a,b)`, since PostgreSQL keeps microseconds: a read and a write move the range.  A multirange never holds an empty range, but its ranges move the same way.
- **Without the plugin**, a project that references `NodaTime` alone gets none of NodaTime's types by default.  A type override, a column type or a marker that names one, `timestamptz=NodaTime.Instant`, gives the property that type, as it would any type, and the query method asks Npgsql for it, which throws `InvalidCastException` when the query runs.  Add `Npgsql.NodaTime`, and call `UseNodaTime()` as [what a method reads and throws](#method-results) says.

<a id="nettopologysuite"></a>

### NetTopologySuite

```sql postgres
-- name: GetPlace -> one
SELECT id, outline, centre, stops
FROM places
WHERE id = @id;
```

```csharp
public sealed record GetPlaceDto(
    long Id,
    global::NetTopologySuite.Geometries.Geometry Outline,
    global::NetTopologySuite.Geometries.Geometry Centre,
    global::NetTopologySuite.Geometries.Geometry[] Stops);
```

A project that references `Npgsql.NetTopologySuite`, Npgsql's plugin for NetTopologySuite, gets NetTopologySuite's `Geometry` for PostGIS's types in the columns and parameters of its queries whose output is `codegen`, read and sent by the plugin.  Above, `outline` is a `geometry(Polygon,4326)`, `centre` a `geography(Point,4326)` and `stops` a `geometry[]`, each `NOT NULL`.  The [library types](settings.md#setting-library-types) turn NetTopologySuite off for a project, a folder, a file or a query.  Reference the `Npgsql.NetTopologySuite` of the same major as your Npgsql, with NetTopologySuite 2.5.0 or later, as [SQLSRC282](diagnostics.md#sqlsrc282) says; the application calls `UseNetTopologySuite()` on its data source, as [what a method reads and throws](#method-results) says.

| PostgreSQL type | C# type with NetTopologySuite |
|----|----|
| `geometry`, `geography`, with or without a modifier such as `(Point,4326)` | `NetTopologySuite.Geometries.Geometry` |

They are known by their name in whatever schema PostGIS was created.  An array of one is a `NetTopologySuite.Geometries.Geometry[]`, whose `NULL` element is null, and a domain over one has its C# type, as [the kinds](#type-table) say.  Every other type keeps the type of [the types](#type-table).

- **Only query methods get NetTopologySuite's types.**  A query whose output is `models` has no C# type for a PostGIS type, with the plugin or without it, and is [SQLSRC246](diagnostics.md#sqlsrc246): give the query the output `codegen`, or map the type with the [type override](settings.md#setting-type-overrides) to the C# type your code reads it as.
- **A shape is yours to choose**, with the type override, a [column type](settings.md#setting-column-types) or a `-- param:` or `-- column:` marker: `NetTopologySuite.Geometries.Point`, `Polygon` or another of `Geometry`'s subtypes, which the plugin reads and sends.  A type override names the type with its schema, `public.geometry`, and never `geometry` alone, which is SQL Server's.  Every column of `geometry` has that one key, whatever its modifier, so give one query a shape with a `-- type-override:` inside it, `-- type-override: public.geometry = NetTopologySuite.Geometries.Point`, or one column with a column type.  A value of another shape throws `InvalidCastException`, "Unable to cast object of type 'NetTopologySuite.Geometries.LineString' to type 'NetTopologySuite.Geometries.Point'.", when it is read.
- **The SRID is kept**, written and read, and so are Z and M: a point of four dimensions comes back with both.  A `geometry` with no SRID, which PostGIS stores as SRID 0, is read with the SRID -1, whether your code or the server wrote it, so compare SRIDs knowing that.  A `geography` sent with SRID 0 is stored as 4326.
- **A parameter's type** is described and checked as any other's.  A `-- param:` marker writes it without its modifier, `-- param: @outline geometry`: see [the known issue](parameters-and-tokens.md#known-issues).
- **Without the plugin**, a project that references NetTopologySuite alone gets none of its types by default.  A type override, a column type or a marker that names one gives the property that type, and the query method asks Npgsql for it, which throws `InvalidCastException` when the query runs.  Add `Npgsql.NetTopologySuite`, and call `UseNetTopologySuite()`.

<a id="json"></a>

### JSON

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
    "libraryTypes": {
        "json": "on",
    },
    "columnTypes": {
        "orders.metadata": "OrderMetadata",
        "orders.document": "System.Text.Json.JsonDocument",
    },
}
```

```sql postgres
-- name: GetOrder -> one
SELECT id, payload, history, metadata, document
FROM orders
WHERE id = @id;
```

```csharp
public sealed record GetOrderDto(
    long Id,
    global::System.Text.Json.JsonElement Payload,
    global::System.Text.Json.JsonElement[] History,
    global::MyApp.OrderMetadata Metadata,
    global::System.Text.Json.JsonDocument Document);
```

A `json` or `jsonb` column is a `string`, as [the types](#type-table) say, until the [library types](settings.md#setting-library-types) turn System.Text.Json on, named `json`.  Then the `json` and `jsonb` columns and parameters of its queries whose output is `codegen` are `System.Text.Json.JsonElement`, and an array of one, `jsonb[]`, a `JsonElement[]`.  Above, `payload` and `metadata` are `jsonb`, `document` a `json` and `history` a `jsonb[]`, each `NOT NULL`, and the column types give `metadata` a class of the project and `document` a `JsonDocument`.  `json` is off until a settings file or a marker turns it on, for a project, a folder, a file or a query.  Every .NET project has System.Text.Json, so it needs no package, and Npgsql reads and sends `JsonElement` with nothing to register on the data source.  `jsonpath` stays a `string`.

| PostgreSQL type | C# type with `json` on |
|----|----|
| `json`, `jsonb` | `System.Text.Json.JsonElement` |
| An array of one, `jsonb[]` | `System.Text.Json.JsonElement[]` |

A domain over one of these types has its C# type, as [the kinds](#type-table) say.  Every other type keeps the type of [the types](#type-table).

- **Only query methods get `JsonElement`.**  A query whose output is `models` keeps the `string` of [the types](#type-table), and so do its models.  A model that such a query shares with a `codegen` query is [SQLSRC257](diagnostics.md#sqlsrc257): see [library types](settings.md#setting-library-types).
- **Your own choices win.**  A [type override](settings.md#setting-type-overrides), a [column type](settings.md#setting-column-types) and a `-- param:` or `-- column:` marker give their type over `JsonElement`.
- **System.Text.Json's other types are yours to choose**, with the type override, a column type or a marker, with `json` on or off: `JsonDocument`, and `JsonNode`, `JsonObject` and `JsonArray` of `System.Text.Json.Nodes`, for a `json` or `jsonb` column or parameter.  Npgsql reads and sends `JsonElement` and `JsonDocument` on Npgsql 8, 9 and 10, and `JsonNode`, `JsonObject` and `JsonArray` on Npgsql 10.  Npgsql 8 and 9 read those three only after `EnableDynamicJson()` on the data source, so there the query methods convert them themselves, from and to the JSON's text, and the data source needs nothing.  There a `JsonObject` read from a JSON array, or a `JsonArray` from an object, throws `InvalidCastException`.
- **A `JsonDocument` is yours to dispose.**  Each one that a method returns, in its row's property, holds memory that is returned only when the document is disposed: dispose it, with `using` or once you have read it.  One that you send stays yours.  A `JsonElement` needs no disposal.
- **A class of your own** in a `json` or `jsonb` column, such as `OrderMetadata` above, is read and sent through System.Text.Json's serializer, with `json` on or off, and with nothing to register on the data source: no `EnableDynamicJson()`, which Npgsql's own route for a class needs.  An array of a class, `OrderMetadata[]` for a `jsonb[]`, is read and sent element by element.  A `text` or `varchar` column that holds JSON takes a class by [the JSON opt-in](settings.md#json-opt-in).  What a class is, the serializer's options, and the converters that replace it, are in [a class in a JSON column](settings.md#json-class).
- **A JSON `null` is not a SQL `NULL`.**  A column that holds the JSON `null`, `'null'::jsonb`, is read as a `JsonElement` of the kind `Null`, and as a `JsonDocument` whose root is one, whether its property can be null or not.  A `JsonNode`, a `JsonObject` and a `JsonArray` cannot hold it: into a property that can be null it is null, and into one that cannot it throws `InvalidCastException`, "Column 'node' holds a JSON null.", as a class does.  On Npgsql 8 and 9 a `JsonNode[]` throws the same for an element that is a JSON `null`, and a `JsonNode?[]` gives null.  A `JsonNode[]` that Npgsql 10 reads gives such an element as null, unchecked, as for a `NULL` element: write `JsonNode?[]` where an element can be one.  A SQL `NULL` gives its property null, or throws as any column does.
- **`jsonb` keeps the structure, not the text.**  It stores JSON in a form of its own: its white space is its own, the keys of an object are in its own order, and of two keys of one name the last alone is kept.  So a value reads back equal in structure, not in text: compare JSON read back by its structure.  `json` keeps the text as it was sent.

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

A described PostgreSQL query gets the [models](generator.md#models) that every described query gets, by the rules of the generator page; their namespace and documentation are left out above.  What PostgreSQL decides in them:

- **Each property's type** is its column's or parameter's C# type, from [the type table](#type-table) under the version of Npgsql that the project references, or [NodaTime's](#nodatime) or [NetTopologySuite's](#nettopologysuite) for a query whose output is `codegen` in a project that references `Npgsql.NodaTime` or `Npgsql.NetTopologySuite`, or [System.Text.Json's](#json) for one where the library types turn `json` on, or the one you chose, by the [type override](settings.md#setting-type-overrides), a [column type](settings.md#setting-column-types) or a marker.  Above, `id` is a `bigint` and `placed_at` a `timestamp with time zone`, both `NOT NULL`.
- **A column's property can be null** by [which columns can be null](#nullability): `customer_name` comes from the optional side of the `LEFT JOIN`, so its property is a `string?`.  A column's alias can say otherwise with `!` or `?`, and so can a [`-- column:` marker](generator.md#column-marker).
- **A parameter's property can be null** only when its `-- param:` marker says `null`, as `@status` does, since PostgreSQL does not say whether a parameter can be null.
- **A column with no name**, which PostgreSQL calls `?column?`, as an expression or a literal without an alias, is [SQLSRC253](diagnostics.md#sqlsrc253), and the query gets no models until it is fixed.  Give it an alias.
- **A query whose rows are a whole table** gets a model named after the table, with the table's schema before its name unless the schema is `public`: `users` gives `UsersDto`, and `billing.invoices` gives `BillingInvoicesDto`.  See [table models](generator.md#table-models).
- **Updating Npgsql from 9 to 10** changes the types of the properties for `date`, `time`, `cidr`, `daterange` and `datemultirange`, as [which version of Npgsql](#which-npgsql) says, and so the models.

<a id="query-methods"></a>

## Query methods

```sql postgres
-- name: AddEvent -> one
INSERT INTO events (kind, payload, happened_at)
VALUES (@kind, @payload, @happenedAt)
RETURNING id;
```

```csharp
AddEventDto added = await connection.AddEvent(
    new AddEventParams("signup", """{"plan": "free"}""", DateTime.UtcNow),
    transaction);
```

Each described query whose output is `codegen` gets a method that runs it through Npgsql, on a `DbConnection` such as `NpgsqlConnection` or the one an `NpgsqlDataSource` gives: see [query methods](generator.md#query-methods).  The project references Npgsql 8.0 or later ([SQLSRC273](diagnostics.md#sqlsrc273), [SQLSRC247](diagnostics.md#sqlsrc247)).  Above, `kind` is an enum `event_kind`, `payload` a `jsonb` and `happened_at` a `timestamptz`, so the method sends the `string` of `@kind` as an `event_kind`, that of `@payload` as a `jsonb` and the `DateTime` of `@happenedAt` as a `timestamptz`, with no cast in the SQL.

<a id="method-parameters"></a>

### How a parameter is sent

Each parameter is sent as the PostgreSQL type it was described with, or the type its [`-- param:` marker](parameters-and-tokens.md#param-marker) declares, and never as a type chosen from the C# type of its value.  A `string` for a `jsonb`, an `xml`, an `ltree` or an enum, a `uint` for an `oid`, a `decimal` for a `money` and a `DateTimeOffset` at any offset for a `timetz` each reach the server as that type.  A null is sent as a `NULL` of that type, so `@p IS NULL` works with no cast once the parameter has a type, from another use or a `-- param:` marker, as [above](#parameters).  How a method names a parameter and sends a null is on [the generator page](generator.md#method-parameters).  For some types:

- **A `timestamptz` parameter takes a `DateTimeOffset` at offset 0, or a `DateTime` whose `Kind` is `Utc`**, and a `timestamp` parameter a `DateTime` of another `Kind`.  Any other throws `ArgumentException`, the query does not run, and nothing is converted in the session's time zone.  A `DateTime` of the wrong `Kind`, an `Unspecified` one for a `timestamptz` or a `Utc` one for a `timestamp`, leaves the connection open and a transaction on it as it was.  A `DateTimeOffset` at another offset also closes the connection: a transaction open on it is lost, and the next command on it throws `InvalidOperationException`, so open a new connection to go on.  Npgsql 8 and 10 do the same, with Npgsql.NodaTime's plugin or without it.  Send `value.ToUniversalTime()` to a `timestamptz`; a value at offset 0 leaves the connection open.
- **A parameter declared as a domain is sent as the domain's base type**, so the domain's `CHECK` is not applied to it.  Compared with a column of the domain, a value that the domain refuses finds no row, and the server checks it only when it is written to such a column.
- **A `string` longer than the `varchar(n)` column it is written to is refused by the server**, with `22001`, and never cut to fit.
- **A parameter that has a [converter](settings.md#converters)** is converted to the type of [the table](#type-table), a domain's base type for a domain, and sent as its database type too.
- **A parameter of a [JSON](#json) type, or a class of yours in one,** is sent as its `json` or `jsonb`: a `JsonElement`, a `JsonDocument`, and on Npgsql 10 a `JsonNode`, a `JsonObject` or a `JsonArray`, by Npgsql, and on Npgsql 8 and 9 those three, and a class on every version, as their JSON's text.  A class by [the JSON opt-in](settings.md#json-opt-in) is sent as its JSON's text, as the parameter's own type: a `text` parameter as a `text`.

<a id="method-results"></a>

### What a method reads and throws

- **Give each method the transaction** of a connection that has one.  Npgsql runs a command in its connection's transaction even when the method is not given it, but Microsoft.Data.SqlClient does not, so code that passes it runs on both: see [the connection](generator.md#method-connection).
- **A column is read as its property's C# type**, the one [the types](#type-map) give it, so a type that Npgsql reads only once it is taught, an enum mapped to a C# enum or a plugin's type, needs the teaching on the data source, as `MapEnum<T>()`.  A type of yours that Npgsql never reads takes [converters](settings.md#converters) instead: the column is read as the type of [the table](#type-table), a domain as its base type, and converted.
- **Every type that [the table](#type-table) gives a C# type is sent and read on Npgsql 8, 9 and 10**, as the C# type of the version's column, and SqlSource is tested on Npgsql 8 and 10.  `cube`, which Npgsql 8 and 9 do not read, is sent and read on Npgsql 10.  So is each type of [NodaTime](#nodatime), on each version with its `Npgsql.NodaTime`, and of [NetTopologySuite](#nettopologysuite), with its `Npgsql.NetTopologySuite`.
- **NodaTime's types need `UseNodaTime()` on the data source.**  A method that reads or sends a [NodaTime](#nodatime) type runs only on a connection of a data source whose builder called `UseNodaTime()`, of `Npgsql.NodaTime`.  Without it, Npgsql throws `InvalidCastException`, "Reading as 'NodaTime.Instant' is not supported for fields having DataTypeName 'timestamp with time zone'", or the same for writing, when the query runs:

  ```csharp
  var builder = new NpgsqlDataSourceBuilder(connectionString);
  builder.UseNodaTime();
  await using var dataSource = builder.Build();
  ```

- **Entity Framework Core's NodaTime package turns NodaTime on.**  `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime` brings `Npgsql.NodaTime`, so the query methods of a project that references it get NodaTime's types.  `UseNodaTime()` inside EF Core's `UseNpgsql(...)` teaches the data source that EF Core builds for itself, and no other: call `UseNodaTime()` on the builder of the data source whose connections run the query methods, or turn NodaTime off with the [library types](settings.md#setting-library-types), `"nodaTime": "off"`.
- **NetTopologySuite's types need `UseNetTopologySuite()` on the data source**, of `Npgsql.NetTopologySuite`, as NodaTime's need `UseNodaTime()`.  Without it, Npgsql throws `InvalidCastException`, "Reading as 'NetTopologySuite.Geometries.Geometry' is not supported for fields having DataTypeName 'public.geometry'", or the same for writing, when the query runs.
- **Entity Framework Core's NetTopologySuite package turns NetTopologySuite on.**  `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` brings `Npgsql.NetTopologySuite`, and `UseNetTopologySuite()` inside EF Core's `UseNpgsql(...)` teaches EF Core's own data source alone, as for NodaTime: call `UseNetTopologySuite()` on the builder of the data source whose connections run the query methods, or turn NetTopologySuite off with the [library types](settings.md#setting-library-types), `"netTopologySuite": "off"`.
- **`UseNodaTime()` changes what Npgsql gives Dapper too.**  On a data source that calls it, every reader gets NodaTime's types: a `timestamptz` arrives as an `Instant`, so a query whose output is `models`, read through Dapper into its `System.DateTime` property, fails, and a `tstzrange` or a `daterange` arrives as an `Interval` or a `DateInterval`, which throws for an empty range, whatever the property's type.  That is the plugin's doing.  Give such queries NodaTime's types with the [type override](settings.md#setting-type-overrides), `"timestamptz": "NodaTime.Instant"` in a settings file of their folder, or read them on a data source without the plugin.
- **System.Text.Json's types and a class of yours in a [JSON](#json) column need nothing on the data source**, on Npgsql 8, 9 and 10: no `EnableDynamicJson()`.
- **A column whose property cannot be null** throws Npgsql's `InvalidCastException`, "Column 'name' is null.", when it holds a null, before any converter is called.  A JSON `null` in such a column is no null, and what it gives is under [JSON](#json).
- **`rowcount` is `-1` for a `SELECT`**, and for a `WITH` whose last statement is a `SELECT`, though its `WITH` updates rows: see [what each shape returns](generator.md#method-shapes).
- **`commandTimeout`** that passes throws Npgsql's `NpgsqlException`, whose inner exception is a `TimeoutException`.
- **A cancelled method throws `OperationCanceledException`**, whether the token was cancelled before the call, while the command runs or while the rows are read.
- **A loop over [streamed rows](settings.md#streaming-rows) that ends early throws the error of a row it left unread.**  At a `break`, or an exception in the loop's body, the rows still to come are read, and a `PostgresException` that PostgreSQL raises for one of them, such as `22012` for a division by zero, is thrown when the loop ends, in place of the body's exception, and aborts the transaction the query ran in.  To keep the body's exception too, see [streamed rows](settings.md#streaming-rows).
- **The connection's commands must take Npgsql's own parameters**, as those of `NpgsqlConnection` do.  A connection that wraps Npgsql's and creates parameters of its own is a [known issue](generator.md#known-issues).

<a id="known-issues"></a>

## Known issues

- **Many columns that are never null are treated as possibly null.**  The rules [above](#nullability) leave out much that a person can see: `EXISTS (...)` as a column before PostgreSQL 17, the rows of `VALUES`, a function not listed there, an expression when the search path puts another schema before `pg_catalog`, a `count(*)` or a `sum` that a `WITH` query computes, a `sum` or an `array_agg` in a query whose `WHERE` sets every column of the `GROUP BY` to one value, a `COALESCE` over a column of each side of a `FULL JOIN` that is cast or computed above the join, a recursive `WITH` query, the columns of a view.  Nothing is wrong when that happens, but the column's property in the query's model is nullable, and costs a null check in your code.  Where you know the column is never null, end its alias with `!`, or, when you read the rows with Dapper, write a `-- column:` marker that says `not null`, as [above](#nullability).
- **An operator `=` of your own on `oid` or `"char"` stops the tool from reading the database.**  When a schema before `pg_catalog` on the search path of the connection the tool uses has an operator `=` defined on two `oid` or on two `"char"`, Npgsql cannot read the database's types when the tool connects, and the database's queries report [SQLSRC224](diagnostics.md#sqlsrc224) with `Npgsql cannot read the database's types`.  Your application's own connections are affected the same way.  Put that schema after `pg_catalog` on the search path of the connection the tool uses, or leave it off.
- **A query over a table with many partitions is slow to describe.**  The tool looks at every partition of a partitioned table, whatever the query's conditions, when it works out which columns can be null.  On a table of thousands of partitions each such query takes longer to describe, and the [log](tool.md#logs) of the run grows.  Only describing is slower; nothing else is affected.
- **A function marked `IMMUTABLE` can run while a query is described.**  See [what describing does](#what-describing-does).
- **A query method fails on a connection that wraps Npgsql's and creates parameters of its own.**  Give the method the `NpgsqlConnection` inside: see [the generator's known issues](generator.md#known-issues).
