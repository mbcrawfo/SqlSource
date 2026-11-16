# Query parameters (`@name`, `-- param:`, IN lists)

A query parameter, such as `@id` in `WHERE id = @id`, carries a value from your code to the database.  To put a piece of SQL itself in a query, such as a table name, use a token: see [token or parameter](tokens.md#token-or-parameter).

<a id="query-parameters"></a>

## Query parameters

```sql
-- name: GetUser
SELECT id, name FROM users WHERE id = @id;
```

A query parameter is a value that you pass when you run a query.  The SQL writes it as `@` and a name: `@id` in `WHERE id = @id`.  A [`-- param:` marker](#param-marker) can give a query parameter its database type, whether it can be null, or a C# type of your choice.

SqlSource finds the query parameters of a query: each `@` and a name, outside strings, quoted identifiers, comments and hints.  A name is letters, digits and `_`.  Under `mssql`, a name is any character that SQL Server takes in the name of a variable.  So under `mssql` a name can also hold these characters:

- a combining mark;
- a letter number, such as `Ⅰ`;
- a format character, such as the zero-width non-joiner;
- a connector, such as `‿`;
- `$`, `#` and `@`.

So `@xⅠ` and `@a$b` are one query parameter each.  The zero-width space is not part of a name: SQL Server reads it as a space, so it ends the name.

SqlSource lists each query parameter of a query once, in the order in which it first appears.  The tool uses this list to ask a database what each query parameter is.  The query's input [model](models.md#models) has a property for each query parameter, in the same order.  The [method](query-methods.md#query-methods) of a query whose output is `repository` or `methods` takes the input model as its argument `parameters`.  It sends each query parameter of the list by its name, with the value of its property: see [how a parameter is sent](query-methods.md#method-parameters).

- **A query parameter stays in the SQL exactly as written.**  The constant for `GetUser`, and the SQL that its method sends, hold `@id`.  A [list](#list-parameters), `IN @ids`, is the one exception, and only in the SQL that a query method sends: that SQL holds a parameter for each value.
- **Names are compared ignoring case.**  `@Id` and `@id` are one query parameter.
- **Under `mssql`, a second spelling of one name is a warning, [SQLSRC121](../diagnostics.md#sqlsrc121).**  A SQL Server whose server collation is case-sensitive reads the second spelling as another variable.
- **The query parameter keeps the spelling that the SQL first writes outside a token's default.**  So a spelling in a default or in a `-- param:` marker does not choose it.
- **Only `@` starts a query parameter.**  `:name`, `$1` and `?` are not found, and stay in the SQL as written.
- **Some uses of `@` are not query parameters, under every dialect.**  These are not:
  - `@>`, `@@`, `@?` and `@@ROWCOUNT`;
  - the `@` inside a word, such as `user@host`;
  - an `@` that no name follows, such as the one in `a <@ b`.  The `@` of `<@b` is a query parameter, `b`.
- **Under `mssql`, an `@` after the first character of a name is part of the name.**  So `@a@b` is one query parameter.  An `@` after `$` or `#` is inside a word, as in `$@a`.
- **Under `repository`, `methods` and `models`, a name that C# cannot hold as a property is an error.**  A name with a format character, `$`, `#` or `@`, such as `@a$b`, gives a property name that is not a C# identifier: [SQLSRC254](../diagnostics.md#sqlsrc254).  Rename the query parameter.  See [property names](model-properties.md#property-names).
- **Under `sql`, the query's constant is generated, with the query parameter as written.**
- **A query parameter inside a hint is not found.**  A hint, `/*+ ... */` or `/*! ... */`, is copied whole.
- **A name that looks like a query parameter is not always one to the database.**  See [names that are read as parameters](#names-read-as-parameters).

<a id="param-marker"></a>

## Giving a parameter a type: `-- param:`

```sql postgres
-- name: FindUsers
-- param: @since timestamptz not null
-- param: @name text null
-- param: @team -> TeamId
SELECT id, name FROM users
WHERE created_at >= @since AND (@name IS NULL OR name = @name) AND team_id = @team;
```

A `-- param:` marker inside a query declares one query parameter.  Its form is `@name`, then a type, then `null` or `not null`, then `->` and a C# type.  Each part after the name is optional.

- **The type is written in the database's own words**, such as `decimal(18,2)` or `double precision`.  SqlSource does not check it.
- **The tool checks the type when it describes the query.**  It asks the database to resolve the type, and checks the type against every use of the query parameter.  A type that the database does not have is [SQLSRC228](../diagnostics.md#sqlsrc228).
- **Without a type, the database decides.**  The tool takes the type that the database infers from how the query parameter is used.
- **If no use gives a type, the tool cannot find one**, as in PostgreSQL's `SELECT @p IS NULL`.  This is [SQLSRC226](../diagnostics.md#sqlsrc226): give the type in the marker.
- **Each database infers a type by its own rules.**  PostgreSQL takes the type from the first use.  If the first use gives none and a later use does, the query is [SQLSRC227](../diagnostics.md#sqlsrc227).  SQL Server gives the type `int` to a query parameter whose every use is like `WHERE @p IS NULL`.  See [PostgreSQL's parameters](databases/postgresql/index.md#parameters) and [SQL Server's](databases/sqlserver/index.md#parameters).
- **`null` says that the value can be null.**  No database reports whether a query parameter can be null, so this is the only way to say it.  `null` makes the property of the query parameter in the query's input model nullable: see [which properties can be null](model-properties.md#model-nullability).
- **A query parameter that does not say `null` is read as not nullable.**
- **`not null` has the same meaning as saying nothing.**  It is accepted because it is what a column definition says.
- **The tool records the declaration in the sidecar.**
- **`->` gives the query parameter a C# type of your choice**, whatever the [type override](type-mapping.md#setting-type-overrides) says.  Above, `FindUsersParams.Team` is a `TeamId`, a type of your project.  A [query method](query-methods.md#method-parameters) sends it through your [converters](converters.md#converters) when the type has them.
- **The C# type is written as a type override's is**: a keyword, a full name or a short name.  It is never `default`, which is [SQLSRC111](../diagnostics.md#sqlsrc111).
- **With `->`, the other parts stay optional.**  `-- param: @team -> TeamId` gives no type, so the database infers the type from the query, as for a query parameter without a marker.  The marker says nothing of null, so the query parameter is not null.
- **The marker's C# type wins.**  A query parameter's C# type is its marker's, then the type override's, then, in a query method, a library's of the [library types](library-types-and-json.md#setting-library-types), then the type map's: see [property types](type-mapping.md#property-types).
- **A [column type](model-properties.md#setting-column-types) never reaches a query parameter**, even one compared with the column that it maps.  A query parameter is no column of a table, so it names its type in this marker.
- **`-> json Type` says that a query parameter of a string type holds JSON**, such as one of `text` or `nvarchar(max)`.  `-- param: @metadata -> json OrderMetadata` sends the `OrderMetadata` of the property as the JSON that System.Text.Json's serializer writes.  See [the JSON opt-in](library-types-and-json.md#json-opt-in).
- **The word `json` is matched ignoring case.**
- **A query parameter of `json` or `jsonb` needs no word**, as [a class in a JSON column](library-types-and-json.md#json-class) says.
- **On PostgreSQL, `->` does not fix a type that the first use leaves open.**  In `(@name IS NULL OR name = @name)`, `-- param: @name -> UserName` gives no type, so the query is still [SQLSRC227](../diagnostics.md#sqlsrc227).  Give the type too, `-- param: @name text null -> UserName`, or turn the condition round.
- **A query parameter that only a token brings needs a type.**  If a query parameter stands only in the default of a token, nothing but the marker can say what it is: see [defaults](tokens.md#token-defaults).
- **For such a query parameter, a marker without a type is [SQLSRC117](../diagnostics.md#sqlsrc117).**  A marker that gives only a C# type, such as `-- param: @page -> PageNumber`, has no type either.
- **For such a query parameter, no marker at all is [SQLSRC118](../diagnostics.md#sqlsrc118).**  In the example under [defaults](tokens.md#token-defaults), `-- param: @page int` is what makes the query valid.
- **The marker names the query parameter as the SQL writes it**: `@name`, alone or followed by a space or `->`.  Anything else is [SQLSRC111](../diagnostics.md#sqlsrc111).
- **A query parameter can be declared twice if the two markers say the same.**  Two markers that differ in type, in nullability, in C# type or in the word `json` are [SQLSRC112](../diagnostics.md#sqlsrc112).
- **`not null` counts as a difference.**  `@a int` and `@a int not null` conflict, though they mean the same.

The marker goes inside a query, under its `-- name:` line.  In the file header it is [SQLSRC116](../diagnostics.md#sqlsrc116): see [markers in the wrong place](sql-files.md#markers-in-the-wrong-place).

A change to the database type or the nullability of a `-- param:` marker, `not null` included, makes the query's sidecar out of date, as a change to the SQL does.  The C# type after `->`, with or without `json`, does not.  Adding or removing a marker that gives no type and no nullability, such as `-- param: @team -> TeamId` or `-- param: @team`, does not either: see [describing again](sidecars.md#describing-again).  When the sidecar is out of date, run `dotnet sqlsource describe` again: see [sidecar files](sidecars.md#sidecar-files).

<a id="list-parameters"></a>

## List parameters

```sql postgres
-- name: ListProducts
SELECT id, name FROM products
WHERE (@categories IS NULL OR category IN @categories)
  AND id NOT IN @excluded;
```

```csharp
List<int> excluded = [7, 12];
var products = await catalog.ListProducts(new ListProductsParams(["books", "games"], excluded));
```

A list parameter is a query parameter written directly after `IN` or `NOT IN`, with no parentheses.  It holds a list of values.  Above, `ListProductsParams.Categories` is an `IEnumerable<string>?`, and `Excluded` is an `IEnumerable<int>`.  So the [query method](query-methods.md#query-methods) takes an array, a `List<T>`, a `HashSet<T>` or any other sequence.  It sends one parameter for each value: with two categories, `category IN @categories` reaches the database as `category IN (@categories_0, @categories_1)`.

- **Each value's parameter is named after the list.**  Its name is one that no other query parameter of the query has, and that no variable the query [declares](#declared-variables) has.
- **`IN (@categories)` is not a list.**  SQL reads it as a list of one value, and `@categories` is one value, as any other query parameter is.
- **A list stands only after `IN` or `NOT IN`, or before `IS NULL` or `IS NOT NULL`.**  Any other use of it is [SQLSRC126](../diagnostics.md#sqlsrc126), at that use: a use as one value, `parent_category = @categories`, or a use in the default of a [token](tokens.md#tokens).
- **A query parameter is a list in every spelling of its name.**
- **`IN` is matched ignoring case.**
- **A comment between `IN` and the query parameter counts as a space.**
- **`position(a IN @p)` is never a list.**  Directly inside the parentheses of a call to `position`, `IN` takes one value, as in PostgreSQL's `position('@' IN @email)`.  So `@email` is an ordinary query parameter there.
- **`position` is the one place.**  Anywhere else, a query parameter directly after `IN` is a list.
- **If another construct of your database takes one value after `IN`, write the value in parentheses**: `IN (@p)`, which SQL reads as the one value.
- **A `-- param:` marker on a list describes its values.**  `-- param: @categories text` gives each value the type `text`, and `-> Category` gives each value the C# type `Category`.
- **`null` in the marker makes the list itself nullable.**
- **Without a type, the database works out the type of the values from the use**, as it would for `category IN (@categories)`: the type of `category`.
- **A list with `-> json` is [SQLSRC126](../diagnostics.md#sqlsrc126) too.**  A list holds values, not JSON.
- **A value of a list is never nullable.**  A list of `integer` is an `IEnumerable<int>`, never an `IEnumerable<int?>`, and no marker makes it one.  Why: a `NULL` among the values of `IN` matches nothing, and a `NULL` among those of `NOT IN` makes it match no row.
- **The property of a list can be null when one of these is true**: the query tests the list for null, as `@categories IS NULL` does, or the marker of the list says `null`.  See [which properties can be null](model-properties.md#model-nullability).
- **If a list is null and its property cannot be null, the query method sends an empty list.**
- **An empty list matches no row after `IN`, and every row after `NOT IN`.**  A null list does the same.
- **`@categories IS NULL` is true for a null list and false for an empty list.**  So in the sample, a null `Categories` keeps every product, and an empty one keeps none.
- **On SQL Server, a list after a `NOT` inside an aggregate, a `GROUP BY` or an `OUTPUT` clause is [SQLSRC127](../diagnostics.md#sqlsrc127)**, under the output `repository` or `methods`.
- **On SQL Server, a list after a `NOT` inside an aggregate of your own fails when it is empty or null**, under the output `repository` or `methods`.  See [the limit](databases/sqlserver/query-methods.md#limit-list-in-an-aggregate).
- **On PostgreSQL, a list in the index predicate of `ON CONFLICT` fails when it is empty or null.**  See [PostgreSQL's parameters](databases/postgresql/index.md#parameters).
- **On PostgreSQL, a list can make the columns of a query's joined tables able to be null.**  This happens when both of these are true:
  - the list stands after a `NOT`, or its truth is used as a value, as in `(o.id IN @ids) IS FALSE`;
  - the query holds a word such as `LEFT`, `RIGHT`, `FULL` or `OUTER`, even as the name of a function.

  Why: an empty list keeps the rows that an outer join filled with null.  [Which columns can be null](databases/postgresql/nullability.md#nullability) gives every word and every use.
- **A null value in a list of a reference type, such as a `string`, is sent as a `NULL`.**  After `IN` it matches nothing, and the other values still match.
- **After `NOT IN`, a null value makes the condition match no row at all**, as SQL's `id NOT IN (1, NULL)` is never true.  Keep nulls out of a list that `NOT IN` reads.
- **A list can be used more than once.**  Every use sends the same parameters.
- **Each list is read once.**  An array, a `List<T>`, a `HashSet<T>` or any other `ICollection<T>` or `IReadOnlyCollection<T>` is counted when the method is called.  Its values are read when they are sent.
- **Any other sequence, such as a LINQ query, is copied into an array once**, when the method is called.
- **A collection must not change between its count and the send.**  If its count of values changed by the time they are sent, the method throws `InvalidOperationException`, "The list 'Excluded' changed after the query counted it.".  Leave a list as it is until the method's task completes.  For a method that [streams its rows](return-types.md#streaming-rows), leave it as it is until the loop has its first row.
- **In the outputs `models` and `sql`, the SQL holds the list as written**, `IN @categories`, for the code that runs it.  Under `models`, the property of the input model is the same `IEnumerable<T>`.  Whether a library runs such SQL is the library's affair.  Dapper, for one, sends one parameter for each value on SQL Server, as the query methods do, but only for a list that the query does not test for null.  Dapper sends a list to PostgreSQL as one array, which `IN @categories` cannot take: see [the recipe](../guides/dapper.md#models-through-dapper).  On PostgreSQL, write [`= ANY(@categories)`](databases/postgresql/query-methods.md#array-parameters) for a query that Dapper runs.

A change to a list's use, such as `IN (@ids)` to `IN @ids`, is a change to the SQL, so the query's sidecar is out of date until it is described again.

<a id="list-padding"></a>

### Padding

```csharp
int[] ids = [4, 8, 15];
// sent as id IN (@ids_0, @ids_1, @ids_2, @ids_3), with @ids_3 another 15
```

A query method rounds the count of each list up to the next size, and fills the list with its last value.  The sizes are 1, 2, 4, 8, 16, 32, 64, 128, 256 and 512, and above 512 each multiple of 512.  So 3 values are sent as 4, and 600 as 1,024.  A repeated value changes no result of `IN` or `NOT IN`.  The database sees a few forms of the query, one for each size, rather than one for each count, and keeps a plan of each.

- **The [generator parameter](generator-parameters.md#setting-generator-parameters) `list-padding` turns the padding on or off.**  It is on by default, and `list-padding=off` sends each list at its own count.
- **Padding never makes a method throw.**  If the padded lists would pass [the limit](#list-limit) and the lists at their own counts would not, the method sends them at their own counts.
- **An empty list or a null one is sent as one parameter**, a `NULL` of the values' type, in `(SELECT @ids_0 WHERE 1 = 0)`, which holds no row.
- **On SQL Server, an empty or null list that no `NOT` reaches is sent as `(@ids_0)`.**  It matches no row either.  Why: SQL Server refuses the subquery inside an aggregate, a `GROUP BY` and an `OUTPUT` clause.

<a id="list-limit"></a>

### The limit of parameters

```console
System.ArgumentException: The query 'ListProducts' would send 70002 parameters; PostgreSQL accepts 65535.  Pass fewer values in 'Excluded', or send them as an array with = ANY. (Parameter 'parameters')
```

PostgreSQL takes at most 65,535 parameters in one query.  SQL Server takes at most 2,098: it takes 2,100, of which Microsoft.Data.SqlClient uses two to send a query with parameters.  Every parameter of the query counts:

- each query parameter that is not a list;
- one for each list that the query tests for null;
- one for each value of each list, or one for an empty list.

A method whose query would send more throws `ArgumentException`.  The exception names the query, the count, the limit and the property of the largest list.  The method throws when it is called, before anything is sent, and so does a method that [streams its rows](return-types.md#streaming-rows).  [Padding](#list-padding) alone never makes it throw.

For a list that large, send it as one parameter.  On PostgreSQL, send it as an [array](databases/postgresql/query-methods.md#array-parameters), `id = ANY(@ids)`.  On SQL Server, send it as [one JSON array](databases/sqlserver/query-methods.md#list-parameters), read with `OPENJSON`.

<a id="declared-variables"></a>

## Variables declared with `DECLARE`

```sql mssql
-- name: FromStart
DECLARE @n int = @start;
SELECT * FROM events WHERE id > @n;
```

Under the `mssql` dialect, a name that the query declares with `DECLARE` is a local variable, not a query parameter, wherever the query writes it.  The only query parameter of this query is `@start`.  `@n` is declared, so it is not one.

- **A declaration is a name directly after the word `DECLARE`**, or directly after a comma of the list of declarations that the word starts.  `DECLARE @a int, @b int` declares both.
- **A name inside an initial value is a query parameter**, as `@start` is.
- **A list of declarations ends at a `;`, or at the word that starts the next statement**, such as `SELECT`, `SET`, `INSERT` or `IF`.  T-SQL needs no `;` between statements, so SqlSource knows these words.
- **Inside parentheses, a comma or one of these words is part of an expression, and ends nothing.**
- **If a name is taken for a variable and is a query parameter, a `-- param:` marker with its type makes it one.**  The marker without a type is [SQLSRC117](../diagnostics.md#sqlsrc117).
- **Under every other dialect, `DECLARE @n int` declares nothing for SqlSource**, and `@n` is a query parameter.

<a id="names-read-as-parameters"></a>

## Names that are read as query parameters

SqlSource finds query parameters by how the SQL is written, not by what the database does with it.  So it takes a few names for query parameters that the database does not treat as one:

| Database | SQL | How SqlSource reads it | What to do |
|----|----|----|----|
| PostgreSQL | `@x`, written for the absolute value of `x` | As the parameter `x`.  Npgsql reads it the same way | Write `abs(x)`, or `@ x` with a space |
| MySQL, MariaDB | A user variable, `SET @n := 1` | As a parameter, at each place it is written | No marker says that a name is a variable.  A MySQL or MariaDB query is not described, so it has the output `sql`, and the parameter changes no generated code |
| SQL Server | The parameter of a procedure that the SQL creates, `CREATE PROCEDURE p @id int AS ...` | As a parameter of the query | Give the query `-- output: sql`: SQL Server does not describe a statement that creates a procedure ([SQLSRC225](../diagnostics.md#sqlsrc225)), and with that output the parameter changes no generated code |

Under `mssql`, the opposite can happen too: SqlSource takes a name for a variable when it is a query parameter.  See [limits](#limit-parameter-taken-for-a-variable).

<a id="limits"></a>

## Limits

<a id="limit-parameter-taken-for-a-variable"></a>

### A parameter is missing from the input model after a `DECLARE`

**What you see.**  Under `mssql`, describing a query stops with [SQLSRC231](../diagnostics.md#sqlsrc231): "SQL Server reads '@id' as a parameter and SqlSource does not".  The query parameter follows a `DECLARE` with no `;`.

**Why.**  SqlSource takes the query parameter for a declared variable.  After a `DECLARE` with no `;`, a statement can start with a word that SqlSource does not know to start a statement, such as `SEND`, or with a label.  That statement does not end the list of declarations.  A query parameter after a comma in it, outside parentheses, is then taken for a variable and left out of the list of query parameters.  SQL Server reads it as a parameter, so the tool stops when it describes the query.

**What to do.**  End the `DECLARE` with a `;`, or declare the query parameter with its type: `-- param: @id int`.
