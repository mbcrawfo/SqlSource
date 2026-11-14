# Query parameters (`@name`, `-- param:`, IN lists)

A query parameter, such as `@id` in `WHERE id = @id`, carries a value from your code to the database.  To put a piece of SQL itself in a query, such as a table name, use a token: see [token or parameter](tokens.md#token-or-parameter).

<a id="query-parameters"></a>

## Query parameters

```sql
-- name: GetUser
SELECT id, name FROM users WHERE id = @id;
```

SqlSource finds the parameters of a query: `@` and a name, outside strings, quoted identifiers, comments and hints.  A name is letters, digits and `_`.  Under `mssql` it is any character SQL Server takes in a variable's name: a combining mark, a letter number such as `Ⅰ`, a format character such as the zero-width non-joiner (but not the zero-width space, which SQL Server reads as a space and which ends the name), a connector such as `‿`, `$`, `#` and `@` among them, so `@xⅠ` and `@a$b` are one parameter each.  The list holds each parameter once, in the order it first appears.  The tool uses the list to ask a database what each parameter is, and the query's input [model](../generator.md#models) has a property for each parameter in its order.  The [method](../generator.md#query-methods) of a query whose output is `methods` or `repository` takes the input model as its argument `parameters`, and sends each parameter of the list by its name, with the value of its property: see [how a parameter is sent](../generator.md#method-parameters).

- **A parameter stays in the SQL exactly as written.**  The constant for `GetUser`, and the SQL its method sends, hold `@id`.  A [list](#list-parameters), `IN @ids`, is the one exception, and only in the SQL that a query method sends, which holds a parameter for each value.
- **Names are compared ignoring case.**  `@Id` and `@id` are one parameter.  Under `mssql`, a second spelling is a warning, [SQLSRC121](../diagnostics.md#sqlsrc121), since a SQL Server whose server collation is case-sensitive reads it as another variable.  The parameter keeps the spelling that the SQL first writes outside a token's default, so a spelling in a default or in a `-- param:` marker does not choose it.
- **Only `@` starts a parameter.**  `:name`, `$1` and `?` are not found, and stay in the SQL as written.
- **Some uses of `@` are not parameters.**  `@>`, `@@`, `@?` and `@@ROWCOUNT` are not, under every dialect, and neither is the `@` inside a word, such as `user@host`, or an `@` that no name follows, such as the one in `a <@ b`.  The `@` of `<@b` is a parameter, `b`.  Under `mssql`, an `@` after the first character of a name is part of it, so `@a@b` is one parameter, and an `@` after `$` or `#` is inside a word, as in `$@a`.
- **A name that C# cannot hold as a property is an error under `models`, `methods` and `repository`.**  A name with a format character, `$`, `#` or `@`, such as `@a$b`, gives a property name that is not a C# identifier, [SQLSRC254](../diagnostics.md#sqlsrc254): rename the parameter.  See [property names](../generator.md#property-names).  Under `sql` the query's constant is generated, with the parameter as written.
- **A parameter inside a hint is not found.**  A hint, `/*+ ... */` or `/*! ... */`, is copied whole.
- **A name that looks like a parameter is not always one to the database.**  See [names that are read as parameters](#names-read-as-parameters).

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

A `-- param:` marker inside a query declares one parameter.  The form is `@name`, then a type, then `null` or `not null`, then `->` and a C# type.  Each part after the name is optional.

- **The type is written in the database's own words**, such as `decimal(18,2)` or `double precision`.  SqlSource does not check it.  When the tool describes the query, it asks the database to resolve the type and checks it against every use of the parameter: a type the database does not have is [SQLSRC228](../diagnostics.md#sqlsrc228).
- **Without a type, the database decides.**  The tool takes the type that the database infers from how the parameter is used.  When no use says, as in PostgreSQL's `SELECT @p IS NULL`, the tool cannot, and the fix is a type here ([SQLSRC226](../diagnostics.md#sqlsrc226)).  Each database infers by its own rules: PostgreSQL takes the type of the first use that gives one ([SQLSRC227](../diagnostics.md#sqlsrc227)), and SQL Server types a parameter whose every use is like `WHERE @p IS NULL` as an `int`.  See [PostgreSQL's parameters](databases/postgresql/index.md#parameters) and [SQL Server's](databases/sqlserver/index.md#parameters).
- **`null` says the value can be null.**  No database reports whether a parameter can be null, so this is the only way to say it.  A parameter that does not say `null` is read as not nullable, and `not null` is accepted because it is what a column definition says; it has the same meaning as saying nothing.  The tool records the declaration in the sidecar, and `null` makes the parameter's property in the query's input model nullable: see [which properties can be null](../generator.md#model-nullability).
- **`->` gives the parameter a C# type of your choice**, whatever the [type override](../settings.md#setting-type-overrides) says: above, `FindUsersParams.Team` is a `TeamId`, a type of your project, which a [query method](../generator.md#method-parameters) sends through your [converters](../settings.md#converters) when the type has them.  The type is written as a type override's is, a keyword, a full name or a short name, and is never `default`, which is [SQLSRC111](../diagnostics.md#sqlsrc111).  The other parts stay optional: `-- param: @team -> TeamId` gives no type, so the database infers it from the query as for a parameter without a marker, and says nothing of null, so the parameter is not null.  A parameter's C# type is its marker's, then the type override's, then, in a query method, a library's of the [library types](../settings.md#setting-library-types), then the type map's.  A [column type](../settings.md#setting-column-types) never reaches a parameter, even one compared with the column it maps: a parameter is no column of a table, so it names its type here.
- **`-> json Type` says that the parameter holds JSON**, for a parameter of a string type, such as `text` or `nvarchar(max)`: `-- param: @metadata -> json OrderMetadata` sends the `OrderMetadata` of the property as the JSON that System.Text.Json's serializer writes.  The word is matched ignoring case.  A parameter of `json` or `jsonb` needs no word, as [a class in a JSON column](../settings.md#json-class) says.  See [the JSON opt-in](../settings.md#json-opt-in).
- **On PostgreSQL, `->` does not fix a type that the first use leaves open.**  In `(@name IS NULL OR name = @name)`, `-- param: @name -> UserName` gives no type, so the query is still [SQLSRC227](../diagnostics.md#sqlsrc227): give the type too, `-- param: @name text null -> UserName`, or turn the condition round.
- **A parameter that only a token brings needs a type.**  When a parameter stands only in the default of a token (see [defaults](tokens.md#token-defaults)), nothing but the marker can say what it is.  The marker without a type is [SQLSRC117](../diagnostics.md#sqlsrc117), a marker that gives only a C# type, `-- param: @page -> PageNumber`, among them, and no marker at all is [SQLSRC118](../diagnostics.md#sqlsrc118).  In the example under [defaults](tokens.md#token-defaults), `-- param: @page int` is what makes the query valid.
- **The marker names the parameter as the SQL writes it**, `@name`, alone or followed by a space or `->`.  Anything else is [SQLSRC111](../diagnostics.md#sqlsrc111).
- **A parameter may be declared twice** if the two markers say the same.  Two that differ in type, in nullability, in C# type or in the word `json` are [SQLSRC112](../diagnostics.md#sqlsrc112), and `not null` counts as a difference: `@a int` and `@a int not null` conflict, though they mean the same.

The marker goes inside a query, under its `-- name:` line.  In the file header it is [SQLSRC116](../diagnostics.md#sqlsrc116): see [markers in the wrong place](sql-files.md#markers-in-the-wrong-place).

A change to the type or the nullability of a `-- param:` marker, like a change to the SQL, makes the query's sidecar out of date, and so does adding or removing `not null`, since it is recorded apart from saying nothing.  Run `dotnet sqlsource describe` again: see [sidecar files](../tool.md#sidecar-files).  The C# type after `->`, with or without `json`, changes nothing that is described, so adding, changing or removing it leaves the sidecar current, and so does adding or removing a marker that gives neither a type nor a nullability, such as `-- param: @team -> TeamId` or `-- param: @team`.  A query with such a marker whose entry an earlier version of the tool wrote is out of date once: describe it again.

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

A parameter written directly after `IN` or `NOT IN`, with no parentheses, is a list of values.  Above, `ListProductsParams.Categories` is an `IEnumerable<string>?` and `Excluded` an `IEnumerable<int>`, so the [query method](../generator.md#query-methods) takes an array, a `List<T>`, a `HashSet<T>` or any other sequence, and sends one parameter for each value: with two categories, `category IN @categories` reaches the database as `category IN (@categories_0, @categories_1)`.  Each value's parameter is named after the list, with a name that no other parameter of the query has, and no variable that it [declares](#declared-variables).  `IN (@categories)` is no list: SQL reads it as a list of one value, and `@categories` is one value, as any other parameter is.

- **A list stands only after `IN` or `NOT IN`, or before `IS NULL` or `IS NOT NULL`.**  Any other use of it, as one value, `parent_category = @categories`, or in the default of a [token](tokens.md#tokens), is [SQLSRC126](../diagnostics.md#sqlsrc126), at that use.  A parameter is a list in every spelling of its name, `IN` is matched ignoring case, and a comment between `IN` and the parameter counts as a space.
- **`position(a IN @p)` is never a list.**  Directly inside the parentheses of a call to `position`, `IN` takes one value, as in PostgreSQL's `position('@' IN @email)`, so `@email` is an ordinary parameter there.  That is the one place: a parameter directly after `IN` anywhere else is a list.  Where another construct of your database takes one value after `IN`, write it in parentheses, `IN (@p)`, which SQL reads as the one value.
- **A `-- param:` marker on a list describes its values.**  `-- param: @categories text` gives each value the type `text`, `-> Category` gives each the C# type `Category`, and `null` makes the list itself nullable.  Without a type, the database works out the values' type from the use, as it would for `category IN (@categories)`: the type of `category`.  A list of `-> json` is SQLSRC126 too: a list holds values, not JSON.
- **A value of a list is never nullable.**  A list of `integer` is an `IEnumerable<int>`, never an `IEnumerable<int?>`, and no marker makes it one: a `NULL` among the values of `IN` matches nothing, and among those of `NOT IN` makes it match no row.
- **The property can be null when the query tests the list for null**, as `@categories IS NULL` does, or when its marker says `null`: see [which properties can be null](../generator.md#model-nullability).  A list that is null where its property cannot be is sent as an empty list.
- **An empty list matches no row after `IN`, and every row after `NOT IN`.**  So does a null one.  On SQL Server, under the output `methods` or `repository`, a list after a `NOT` inside an aggregate, a `GROUP BY` or an `OUTPUT` clause is [SQLSRC127](../diagnostics.md#sqlsrc127), and one inside an aggregate of your own fails when it is empty or null: see [limits](databases/sqlserver/query-methods.md#limit-list-in-an-aggregate).  On PostgreSQL, so does a list in the index predicate of `ON CONFLICT`: see [PostgreSQL's parameters](databases/postgresql/index.md#parameters).  A null list makes `@categories IS NULL` true and an empty one makes it false, so above a null `Categories` keeps every product and an empty one keeps none.
- **On PostgreSQL, a list after a `NOT`, or one whose truth is used as a value, as in `(o.id IN @ids) IS FALSE`, makes the columns of a query's joined tables possibly null** when the query holds a word such as `LEFT`, `RIGHT`, `FULL` or `OUTER`, even as a function's name, since an empty list keeps the rows an outer join filled with null: [which columns can be null](databases/postgresql/nullability.md#nullability) gives every word and every use.
- **A null value in a list of a reference type**, such as a `string`, is sent as a `NULL`.  After `IN` it matches nothing, and the other values still match.  After `NOT IN` it makes the condition match no row at all, as SQL's `id NOT IN (1, NULL)` is never true: keep nulls out of a list that `NOT IN` reads.
- **A list may be used more than once**, and every use sends the same parameters.
- **Each list is read once.**  An array, a `List<T>`, a `HashSet<T>` or any other `ICollection<T>` or `IReadOnlyCollection<T>` is counted when the method is called, and its values are read when they are sent.  Any other sequence, such as a LINQ query, is copied into an array once, when the method is called.  A collection whose count of values has changed by the time they are sent throws `InvalidOperationException`, "The list 'Excluded' changed after the query counted it.": leave a list as it is until the method's task completes, or, for a method that [streams its rows](../settings.md#streaming-rows), until the loop has its first row.
- **In the outputs `sql` and `models`, the SQL holds the list as written**, `IN @categories`, for the code that runs it, and under `models` the input model's property is the same `IEnumerable<T>`.  Whether a library runs such SQL is the library's affair: Dapper, for one, sends one parameter for each value on SQL Server, as the query methods do, but only for a list that the query does not test for null, and sends a list to PostgreSQL as one array, which `IN @categories` cannot take: see [the recipe](../recipes.md#models-through-dapper).  On PostgreSQL, write [`= ANY(@categories)`](databases/postgresql/query-methods.md#array-parameters) for a query that Dapper runs.

A change to a list's use, such as `IN (@ids)` to `IN @ids`, is a change to the SQL, so the query's sidecar is out of date until it is described again.

<a id="list-padding"></a>

### Padding

```csharp
int[] ids = [4, 8, 15];
// sent as id IN (@ids_0, @ids_1, @ids_2, @ids_3), with @ids_3 another 15
```

A query method rounds the count of each list up to 1, 2, 4, 8, 16, 32, 64, 128, 256 or 512, and above 512 to the next multiple of 512, and fills the list with its last value.  So 3 values are sent as 4, and 600 as 1,024.  A value repeated changes no result of `IN` or `NOT IN`, and the database sees a few forms of the query, one for each size, rather than one for each count, and keeps a plan of each.

- **The [generator parameter](../settings.md#setting-generator-parameters) `list-padding`** turns the padding on or off: it is on by default, and `list-padding=off` sends each list at its own count.
- **Padding never makes a method throw.**  When the padded lists would pass [the limit](#list-limit) and the lists at their own counts would not, the method sends them at their own counts.
- **An empty list or a null one** is sent as one parameter, a `NULL` of the values' type, in `(SELECT @ids_0 WHERE 1 = 0)`, which holds no row.  On SQL Server, a list that no `NOT` reaches is sent as `(@ids_0)`, which matches no row either, since SQL Server refuses the subquery inside an aggregate, a `GROUP BY` and an `OUTPUT` clause.

<a id="list-limit"></a>

### The limit of parameters

```console
System.ArgumentException: The query 'ListProducts' would send 70002 parameters; PostgreSQL accepts 65535.  Pass fewer values in 'Excluded', or send them as an array with = ANY. (Parameter 'parameters')
```

PostgreSQL takes at most 65,535 parameters in one query, and SQL Server 2,098: it takes 2,100, of which Microsoft.Data.SqlClient uses two to send a query with parameters.  Every parameter of the query counts: each one that is no list, one for each list that the query tests for null, and one for each value of each list, or one for an empty list.  A method whose query would send more throws `ArgumentException`, which names the query, the count, the limit and the property of the largest list, when it is called and before anything is sent, a method that [streams its rows](../settings.md#streaming-rows) included.  [Padding](#list-padding) alone never makes it throw.

For a list that large, send it as one parameter: on PostgreSQL as an [array](databases/postgresql/query-methods.md#array-parameters), `id = ANY(@ids)`, and on SQL Server as [one JSON array](databases/sqlserver/query-methods.md#list-parameters), read with `OPENJSON`.

<a id="declared-variables"></a>

## Variables declared with `DECLARE`

```sql mssql
-- name: FromStart
DECLARE @n int = @start;
SELECT * FROM events WHERE id > @n;
```

Under the `mssql` dialect, a name that the query declares with `DECLARE` is a local variable and not a parameter, wherever the query writes it.  The only parameter of this query is `@start`: `@n` is declared, so it is not one.

- **A declaration is a name directly after the word `DECLARE`**, or directly after a comma of the list that the word starts: `DECLARE @a int, @b int` declares both.  A name inside an initial value is a parameter, as `@start` is.
- **A list ends at a `;`** or at the word that starts the next statement, such as `SELECT`, `SET`, `INSERT` or `IF`.  T-SQL needs no `;` between statements, so SqlSource knows these words.  A comma or one of them inside parentheses is part of an expression and ends nothing.
- **If a name is taken for a variable and is a parameter**, a `-- param:` marker with its type makes it one.  The marker without a type is [SQLSRC117](../diagnostics.md#sqlsrc117).
- **Under every other dialect**, `DECLARE @n int` declares nothing for SqlSource, and `@n` is a parameter.

<a id="names-read-as-parameters"></a>

## Names that are read as parameters

SqlSource finds parameters by how the SQL is written, and not by what the database does with it.  A few names are taken for parameters that the database does not treat as one:

| Database | SQL | How SqlSource reads it | What to do |
|----|----|----|----|
| PostgreSQL | `@x`, written for the absolute value of `x` | As the parameter `x`.  Npgsql reads it the same way | Write `abs(x)`, or `@ x` with a space |
| MySQL, MariaDB | A user variable, `SET @n := 1` | As a parameter, at each place it is written | No marker says that a name is a variable.  A MySQL or MariaDB query is not described, so it has the output `sql`, and the parameter changes no generated code |
| SQL Server | The parameter of a procedure that the SQL creates, `CREATE PROCEDURE p @id int AS ...` | As a parameter of the query | Give the query `-- output: sql`: SQL Server does not describe a statement that creates a procedure ([SQLSRC225](../diagnostics.md#sqlsrc225)), and with that output the parameter changes no generated code |

Under `mssql`, the opposite can happen too: a name is taken for a variable when it is a parameter.  See [limits](#limit-parameter-taken-for-a-variable).

<a id="limits"></a>

## Limits

<a id="limit-parameter-taken-for-a-variable"></a>

### A parameter is missing from the input model after a `DECLARE`

- **Under `mssql`, a parameter can be taken for a declared variable.**  After a `DECLARE` with no `;`, a statement that starts with a word SqlSource does not know to start a statement, such as `SEND` or a label, does not end the list.  A parameter after a comma in it, outside parentheses, is then taken for a variable and left out of the parameter list.  End the `DECLARE` with a `;`, or declare the parameter with its type: `-- param: @id int`.
