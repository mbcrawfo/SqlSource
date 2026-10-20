# PostgreSQL

SqlSource reads PostgreSQL files by the `postgres` [dialect](dialects.md), and the `sqlsource` tool can describe their queries: it asks your database what each query's parameters and columns are, and writes the answer to the query's [sidecar](tool.md#sidecar-files).  This page is about describing, about the C# types that a described query's columns and parameters get, and about the models built from them.  How SqlSource reads a PostgreSQL file, its comments and strings, is on [the dialects page](dialects.md).

```xml
<PropertyGroup>
    <SqlSourceDialect>postgres</SqlSourceDialect>
</PropertyGroup>
```

```console
$ dotnet sqlsource describe --connection postgres="Host=localhost;Database=app;Username=app;Password=secret"
postgres (postgres): 12 described, 0 skipped, 0 failed
```

The connection is given for the query's [logical database](tool.md#connections), which is `postgres` when you name none.  The summary names that database first, and its dialect in parentheses.

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

For each column the tool records whether it can be null.  The usual reasons a column is recorded as possibly null are an outer join, a view, or an expression such as `count(note)` or a call of a function that the lists below do not name; the lists give every rule.  If `orders.id`, `orders.placed_at` and `customers.name` are all `NOT NULL`, `id` and `placed_at` above are never null, and `name` can be: the `LEFT JOIN` gives a null name to an order with no customer.

SqlSource treats a column as possibly null unless it can show that it never is.  A column it treats as possibly null that never is costs a null check in your code.  When the tool connects directly to the server, it never gives you a wrong value; see [known issues](#known-issues) for a pooler.

SqlSource can tell that a column is never null when it is:

- **A `NOT NULL` column of a table**, which no outer join of the query can fill with null.
- **`count(*)`**, or a ranking over a window: `row_number()`, `rank()`, `dense_rank()`, `percent_rank()` or `cume_dist()`.
- **A constant** other than `NULL`, such as `1` or `'active'`.
- **A test**: `IS NULL`, `IS NOT NULL`, `IS DISTINCT FROM` or `IS NOT DISTINCT FROM`.
- **Arithmetic or a comparison of values that are never null**: `+`, `-`, `*` or `/` of two numbers; `=`, `<>`, `<`, `<=`, `>` or `>=` of two numbers, two texts, two booleans, or two values of one of `date`, `timestamp` and `timestamp with time zone`; and `||` of two texts.
- **`lower`, `upper` or `length` of a text, or `date_trunc` of a `date`, a `timestamp` or a `timestamp with time zone` without a time zone argument**, over values that are never null, and **a cast** of a value that is never null from one number type to another, from one text type to another, or between `date`, `timestamp` and `timestamp with time zone`.
- **One of the expressions above over a system column of a table**, such as `xmax = 0` in `RETURNING (xmax = 0) AS inserted`.  A system column on its own, `RETURNING xmax`, is possibly null.
- **`COALESCE`, `GREATEST` or `LEAST`** with an argument that is never null, or **a `CASE` with an `ELSE`** whose every result is never null.  Each counts only on its own: arithmetic, a comparison, a function, a cast, an aggregate or a subquery's value computed from one, such as `sum(coalesce(x, 0))`, is possibly null.
- **`max`, `min`, `sum` or `avg` of numbers, `max` or `min` of texts or of `date`, `timestamp` or `timestamp with time zone`, `string_agg` of two texts, or `bool_and`, `bool_or` or `every`** of values that are never null, in a query with a `GROUP BY` of one column or more, when it has no `FILTER` and no `OVER`.  Each group of such a query has a row; without `GROUP BY`, a query over no rows returns one row whose `sum` is null.
- **A subquery that is the whole column, uses a column of the outer query and returns one row**, such as `(SELECT count(*) FROM orders o WHERE o.account_id = a.id)`: one with an aggregate and no `GROUP BY`, `HAVING`, `LIMIT` or `OFFSET`, whose value is `count(*)`, or arithmetic, a function or a cast of the list above over values that are never null, of a number type, a text type, or `date`, `timestamp` or `timestamp with time zone`.  The same subquery inside an expression, `(SELECT count(*) ...) + 1`, is possibly null.
- **`EXISTS (...)` whose subquery uses a column of the outer query, on PostgreSQL 18.**  PostgreSQL may run an `EXISTS` whose subquery compares one of its columns with one of the outer query's as an `IN`, and such an `EXISTS` is never null only as that `IN` would be.
- **`x IN (SELECT y ...)` or `x = ANY (SELECT y ...)`, on PostgreSQL 18,** when `x` and the subquery's `y` are never null by the rules above, and the comparison is one of those above.
- **`merge_action()`** in the `RETURNING` list of a `MERGE`, on PostgreSQL 18.
- **A column of a `UNION`, an `INTERSECT` or an `EXCEPT`** at the top of the query, when every value it can return there is never null by the rules above: every branch of a `UNION`, either side of an `INTERSECT`, the left side of an `EXCEPT`.

`count(*)` and the rankings, constants, tests, the arithmetic, comparisons, functions, casts, aggregates and subqueries above, `merge_action()`, and `COALESCE`, `GREATEST`, `LEAST` and `CASE` each count only where an outer join cannot leave them null.

SqlSource treats a column as possibly null when:

- **The table column can hold null**: it is not `NOT NULL`, or its `NOT NULL` was added as `NOT VALID` and not validated.
- **The table can return null where its column says it cannot**: the table has or had inheritance children, it is partitioned and a foreign table is one of its partitions, or it has a `DO INSTEAD` rule on `INSERT`, `UPDATE` or `DELETE`.  Every column of such a table is possibly null.
- **An outer join can fill the column with null**: a column of the optional side of a `LEFT`, `RIGHT` or `FULL` join.
- **The column comes from a view or a materialized view.**  PostgreSQL does not record whether a view's column can be null.
- **The column comes from a foreign table.**  PostgreSQL does not enforce `NOT NULL` on its remote rows.
- **It is `NULL`**, or an expression that is not in the list above, such as `count(note)`, `array_agg(...)`, `upper(during)` of a range, `price * quantity` where either column can hold null, `sum(total)` without `GROUP BY`, `sum(coalesce(x, 0))`, `EXISTS (...)` before PostgreSQL 18, or a call of another function.
- **It is a column of a `UNION`, an `INTERSECT` or an `EXCEPT`** where a branch can give null, or where SqlSource cannot follow a branch.
- **It is an expression that a subquery in `FROM` or a `WITH` query computes**, such as a `count(*)` that a `WITH` query returns, **or one computed over a `UNION`, an `INTERSECT` or an `EXCEPT`**, such as `count(*)` of a subquery that holds one.  Such a column can be treated as possibly null even when the list above says it is never null.
- **The query has `ROLLUP`, `CUBE` or `GROUPING SETS`.**  Every column of such a query is possibly null.
- **The query names `OLD` or `NEW`, and the column is a subquery, an `IN (SELECT ...)` or an `= ANY (SELECT ...)`**, even one the list above says is never null.
- **The connection's search path puts another schema before `pg_catalog`, and the column is `count(*)`, a ranking, arithmetic, a comparison, a function, a cast, an aggregate or a subquery**, even one the list above says is never null.  Constants, tests, `COALESCE` over columns that are never null, `EXISTS (...)` and `merge_action()` are not affected.  Keep `pg_catalog` first on the search path of the connection the tool uses, or leave it off the path, where PostgreSQL searches it first.
- **The query has an outer join, `MERGE`, `OLD` or `NEW`, and the column comes through a subquery or a `WITH` query, or from a table that an extension such as Citus or TimescaleDB manages.**  Such a column can be treated as possibly null even when its table column is `NOT NULL`.

A column that can be null gets a nullable property in the query's [output model](#models), `int?` or `string?`, and a column that is never null a property that cannot be null, `int` or `string`.  Where you know better than these rules, you decide instead: an alias that ends in `!` gives a property that cannot be null, and one that ends in `?` a property that can, or a `-- column:` marker says the same and leaves the column's name alone.

```sql postgres
-- name: GetCustomerNotes
SELECT c.id, c.name, count(o.note) AS "note_count!"
FROM customers c
JOIN orders o ON o.customer_id = c.id
GROUP BY c.id, c.name;
```

A count is never null, though SqlSource treats `count(o.note)` as possibly null: `"note_count!"` gives the property `NoteCount` the type `long` and not `long?`.  Write the alias in quotes, since `!` and `?` are not part of a plain name, and the mark is left out of the property's name.  A `!` is your word, and nothing checks it.  The mark stays in the column's name, which Dapper matches to the property's by name, so with Dapper write a `-- column:` marker and no mark:

```sql postgres
-- name: GetCustomerNotes
-- column: note_count not null
SELECT c.id, c.name, count(o.note) AS note_count
FROM customers c
JOIN orders o ON o.customer_id = c.id
GROUP BY c.id, c.name;
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

Each column and each parameter of a described query gets a C# type: the type that Npgsql reads a value of its PostgreSQL type as.  If `id` is a `bigint`, `placed_at` a `timestamp with time zone`, `total` a `numeric(12,2)`, `tags` a `text[]` and `born` a `date`, they are `long`, `System.DateTime`, `decimal`, `string[]`, and `System.DateTime` on Npgsql 8 and 9 or `System.DateOnly` on Npgsql 10; `@id` is a `long`.  Whether a column can be null is decided [above](#nullability), not by its type.

The build gives these types to each query whose output is `models` or `codegen` and whose entry in its sidecar is current, and reports an error for a column or parameter that has none, such as [SQLSRC246](diagnostics.md#sqlsrc246) for a type that Npgsql cannot read.  They are the types of the properties of the query's [models](#models), and a query with such an error has no models.  To give a type another C# type, use the [type override](settings.md#setting-type-overrides): `timestamptz=System.DateTimeOffset`.

<a id="which-npgsql"></a>

### Which version of Npgsql

A few types are read as another C# type by Npgsql 10 than by Npgsql 8 and 9, so the types follow the `Npgsql` package that the project references, directly or through another package:

- **Npgsql 8, 9 or 10** gets the column of the table below for its version.  Updating Npgsql from 9 to 10 changes the C# types of `date`, `time`, `cidr`, `daterange` and `datemultirange`, as it changes what Npgsql reads.
- **Npgsql older than 8** is [SQLSRC247](diagnostics.md#sqlsrc247).  Update it.
- **Npgsql 11 or later** gets the types of Npgsql 10, with the information [SQLSRC248](diagnostics.md#sqlsrc248), since this version of SqlSource does not know a newer one.
- **No Npgsql**, as in a library that holds the queries and leaves the driver to the application, gets every type that Npgsql 8, 9 and 10 read alike.  A type that they read differently, and a type of Npgsql's own `NpgsqlTypes` namespace, have no C# type without the driver: each is [SQLSRC249](diagnostics.md#sqlsrc249), unless the type override names it.  For the types that differ, the override says which Npgsql the application runs on: `date=System.DateOnly time=System.TimeOnly` for Npgsql 10.

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

`citext`, `hstore`, `ltree`, `lquery`, `ltxtquery` and `cube` are the types of extensions, and are known by their name in whatever schema the extension was created.  A type that the table does not name gets its C# type by its kind:

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

- **`timestamptz`** is read as a `System.DateTime` whose `Kind` is `Utc`.  `timestamp`, and `date` on Npgsql 8 and 9, are read with the `Kind` `Unspecified`.  `infinity` and `-infinity` in a `timestamptz` are read as `DateTime.MaxValue` and `DateTime.MinValue`, with the `Kind` `Unspecified`.  To give a `timestamptz` the C# type `System.DateTimeOffset`, map it: `timestamptz=System.DateTimeOffset`.  Rows read through Dapper then also need a type handler: see [the recipe](recipes.md#models-through-dapper).
- **`numeric`** values `NaN`, `Infinity` and `-Infinity` cannot be read as a `decimal`, and reading one throws.  Keep them out of the column, or cast it to `float8` in the query, which reads them as a `double`.
- **An `interval` with months or years** cannot be read as a `System.TimeSpan`, and reading one throws.  Your own ADO.NET code can read it as an `NpgsqlTypes.NpgsqlInterval`, with `reader.GetFieldValue<NpgsqlInterval>(i)`.
- **`inet`** is read as a `System.Net.IPAddress`, which drops the netmask.  Your own ADO.NET code can read it as an `NpgsqlTypes.NpgsqlInet`, which keeps it, with `reader.GetFieldValue<NpgsqlInet>(i)`.
- **`char(n)`**, `char(1)` included, is a `string`.  The `char` of the table is PostgreSQL's one-byte type, which is written `"char"` with its quotes.
- **`bit(1)`** is a `bool`, and a `bit(1)[]` column a `bool[]`.  A parameter's type has no length, so a parameter of `bit(1)` is a `System.Collections.BitArray`.
- **An array that holds a `NULL` element** needs a nullable element, `int?[]`, for an element of a value type, and the type override gives it: `int4[]=int?[]`.  Npgsql also refuses to read such an array under its default `Array Nullability Mode=Never`, so set `Array Nullability Mode=Always` or `PerInstance` in the application's connection string.  An array of a reference type, `text[]`, is read with its `NULL` elements whatever the mode; map `text[]=string?[]` to say that an element can be null.
- **A multidimensional array** is read as `int[,]` and not `int[]`, and its column's type does not say how many dimensions it has, so a column of `int4[]` that holds a two-dimensional array cannot be read into an `int[]`.  Map it, `int4[]=int[,]`, where every value of the column has two dimensions.
- **The settings of the application's data source can change what Npgsql reads**, and SqlSource cannot see them.  The table is what a data source made with `NpgsqlDataSourceBuilder`, or a plain `NpgsqlConnection`, reads.  `NpgsqlSlimDataSourceBuilder` reads fewer types until its `Enable...()` methods are called.  The AppContext switch `Npgsql.EnableLegacyTimestampBehavior`, and Npgsql 10's legacy date and time resolver, read some date and time types as other C# types; map those types with the type override to the C# types the application reads.

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

- **Each property's type** is its column's or parameter's C# type, from [the type table](#type-table) under the version of Npgsql that the project references, or from the [type override](settings.md#setting-type-overrides).  Above, `id` is a `bigint` and `placed_at` a `timestamp with time zone`, both `NOT NULL`.
- **A column's property can be null** by [which columns can be null](#nullability): `customer_name` comes from the optional side of the `LEFT JOIN`, so its property is a `string?`.  A column's alias can say otherwise with `!` or `?`, and so can a [`-- column:` marker](generator.md#column-marker).
- **A parameter's property can be null** only when its `-- param:` marker says `null`, as `@status` does, since PostgreSQL does not say whether a parameter can be null.
- **A column with no name**, which PostgreSQL calls `?column?`, as an expression or a literal without an alias, is [SQLSRC253](diagnostics.md#sqlsrc253), and the query gets no models until it is fixed.  Give it an alias.
- **A query whose rows are a whole table** gets a model named after the table, with the table's schema before its name unless the schema is `public`: `users` gives `UsersDto`, and `billing.invoices` gives `BillingInvoicesDto`.  See [table models](generator.md#table-models).
- **Updating Npgsql from 9 to 10** changes the types of the properties for `date`, `time`, `cidr`, `daterange` and `datemultirange`, as [which version of Npgsql](#which-npgsql) says, and so the models.

<a id="query-methods"></a>

## Query methods

> **Coming in a later release.**  The generator will write a method that runs each described PostgreSQL query through Npgsql and returns its rows.  See [what works today](README.md#what-works-today).

<a id="known-issues"></a>

## Known issues

- **Many columns that are never null are treated as possibly null.**  The rules [above](#nullability) leave out much that a person can see: `count(x)`, `array_agg(...)`, `sum(coalesce(x, 0))`, `EXISTS (...)` as a column before PostgreSQL 18, the rows of `VALUES`, a function not listed there, an expression when the search path puts another schema before `pg_catalog`, a `count(*)` or a `sum` that a `WITH` query computes, a recursive `WITH` query, the columns of a view.  Nothing is wrong when that happens, but the column's property in the query's model is nullable, and costs a null check in your code.  Where you know the column is never null, end its alias with `!`, or, when you read the rows with Dapper, write a `-- column:` marker that says `not null`, as [above](#nullability).
- **Describing through a pooler can give the wrong error, or a wrong answer.**  Behind a pooler in transaction mode, most queries fail with [SQLSRC236](diagnostics.md#sqlsrc236), but some are reported as [SQLSRC231](diagnostics.md#sqlsrc231) with a message about the number of columns, and in rare cases a column can be recorded with the nullability of another statement that an earlier run left on the server.  Connect directly to the server, as [connecting](#connecting) says, and describe again with `--force`.
- **A type of your own named like a built-in one changes the names of built-in types.**  When the connection's search path puts a schema that has a type named like a built-in, such as a domain `public.text`, before `pg_catalog`, the tool records the built-in type as `pg_catalog.text` and not `text`.  Two databases that differ only in this describe the same query differently, and `--check` against one reports the sidecars written against the other as out of date.  Rename the type, or keep `pg_catalog` first on the search path of the connection the tool uses, the same on every machine.
- **A query over a table with many partitions is slow to describe.**  The tool looks at every partition of a partitioned table, whatever the query's conditions, when it works out which columns can be null.  On a table of thousands of partitions each such query takes longer to describe, and the [log](tool.md#logs) of the run grows.  Only describing is slower; nothing else is affected.
- **A type override to a generic type with a type argument that breaks the type's constraints is a compiler error in the model.**  SqlSource does not check a generic type's constraints, so an override such as `int4=System.Collections.Generic.List<System.Nullable<string>>`, whose `Nullable<T>` takes only a value type, is accepted, and the compiler reports an error such as CS0453 in the generated model, not an SQLSRC error at the query.  Check the type arguments of a generic type you map against its constraints.
- **A function marked `IMMUTABLE` can run while a query is described.**  See [what describing does](#what-describing-does).
