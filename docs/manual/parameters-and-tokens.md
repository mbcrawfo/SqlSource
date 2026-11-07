# Parameters and tokens

A query takes things from the code that runs it in two ways.  A [query parameter](concepts.md#query-parameter) carries a value: `@id` in `WHERE id = @id`.  A [token](concepts.md#token) carries a piece of the SQL itself: `{{table}}` in `FROM {{table}}`.

```sql
-- name: ListSince
-- param: @since timestamptz
SELECT id, name FROM {{table}} WHERE created_at >= @since;
```

A value cannot stand for a table name, a list of columns or a whole clause, and a token can.  A token cannot be made safe, and a value can.

| | Query parameter | Token |
|----|----|----|
| Written | `@since` | `{{table}}` |
| Carries | A value | Text of the SQL |
| Replaced | Never by SqlSource: it stays in the SQL, and its value is sent to the database beside it | By the generated method, when your code asks for the SQL or runs the query |
| Safe for text a user can influence | Yes | No |

<a id="query-parameters"></a>

## Query parameters

```sql
-- name: GetUser
SELECT id, name FROM users WHERE id = @id;
```

SqlSource finds the parameters of a query: `@` and a name of letters, digits and `_`, outside strings, quoted identifiers, comments and hints.  The list holds each parameter once, in the order it first appears.  The tool uses the list to ask a database what each parameter is, and the query's input [model](generator.md#models) has a property for each parameter in its order.  The [method](generator.md#query-methods) of a query whose output is `codegen` takes the input model as its argument `parameters`, and sends each parameter of the list by its name, with the value of its property: see [how a parameter is sent](generator.md#method-parameters).

- **A parameter stays in the SQL exactly as written.**  The constant for `GetUser`, and the SQL its method sends, hold `@id`.  A [list](#list-parameters), `IN @ids`, is the one exception, and only in the SQL that a query method sends, which holds a parameter for each value.
- **Names are compared ignoring case.**  `@Id` and `@id` are one parameter.  Under `mssql`, a second spelling is a warning, [SQLSRC121](diagnostics.md#sqlsrc121), since a SQL Server whose server collation is case-sensitive reads it as another variable.  The parameter keeps the spelling that the SQL first writes outside a token's default, so a spelling in a default or in a `-- param:` marker does not choose it.
- **Only `@` starts a parameter.**  `:name`, `$1` and `?` are not found, and stay in the SQL as written.
- **Some uses of `@` are not parameters.**  `@>`, `@@`, `@?` and `@@ROWCOUNT` are not, and neither is the `@` inside a word, such as `user@host`, or an `@` that no name follows, such as the one in `a <@ b`.  The `@` of `<@b` is a parameter, `b`.
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

- **The type is written in the database's own words**, such as `decimal(18,2)` or `double precision`.  SqlSource does not check it.  When the tool describes the query, it asks the database to resolve the type and checks it against every use of the parameter: a type the database does not have is [SQLSRC228](diagnostics.md#sqlsrc228).
- **Without a type, the database decides.**  The tool takes the type that the database infers from how the parameter is used.  When no use says, as in PostgreSQL's `SELECT @p IS NULL`, the tool cannot, and the fix is a type here ([SQLSRC226](diagnostics.md#sqlsrc226)).  Each database infers by its own rules: PostgreSQL takes the type of the first use that gives one ([SQLSRC227](diagnostics.md#sqlsrc227)), and SQL Server types a parameter whose every use is like `WHERE @p IS NULL` as an `int`.  See [PostgreSQL's parameters](postgresql.md#parameters) and [SQL Server's](sqlserver.md#parameters).
- **`null` says the value can be null.**  No database reports whether a parameter can be null, so this is the only way to say it.  A parameter that does not say `null` is read as not nullable, and `not null` is accepted because it is what a column definition says; it has the same meaning as saying nothing.  The tool records the declaration in the sidecar, and `null` makes the parameter's property in the query's input model nullable: see [which properties can be null](generator.md#model-nullability).
- **`->` gives the parameter a C# type of your choice**, whatever the [type override](settings.md#setting-type-overrides) says: above, `FindUsersParams.Team` is a `TeamId`, a type of your project, which a [query method](generator.md#method-parameters) sends through your [converters](settings.md#converters) when the type has them.  The type is written as a type override's is, a keyword, a full name or a short name, and is never `default`, which is [SQLSRC111](diagnostics.md#sqlsrc111).  The other parts stay optional: `-- param: @team -> TeamId` gives no type, so the database infers it from the query as for a parameter without a marker, and says nothing of null, so the parameter is not null.  A parameter's C# type is its marker's, then the type override's, then, in a query method, a library's of the [library types](settings.md#setting-library-types), then the type map's.  A [column type](settings.md#setting-column-types) never reaches a parameter, even one compared with the column it maps: a parameter is no column of a table, so it names its type here.
- **`-> json Type` says that the parameter holds JSON**, for a parameter of a string type, such as `text` or `nvarchar(max)`: `-- param: @metadata -> json OrderMetadata` sends the `OrderMetadata` of the property as the JSON that System.Text.Json's serializer writes.  The word is matched ignoring case.  A parameter of `json` or `jsonb` needs no word, as [a class in a JSON column](settings.md#json-class) says.  See [the JSON opt-in](settings.md#json-opt-in).
- **On PostgreSQL, `->` does not fix a type that the first use leaves open.**  In `(@name IS NULL OR name = @name)`, `-- param: @name -> UserName` gives no type, so the query is still [SQLSRC227](diagnostics.md#sqlsrc227): give the type too, `-- param: @name text null -> UserName`, or turn the condition round.
- **A parameter that only a token brings needs a type.**  When a parameter stands only in the default of a token (see [defaults](#token-defaults)), nothing but the marker can say what it is.  The marker without a type is [SQLSRC117](diagnostics.md#sqlsrc117), a marker that gives only a C# type, `-- param: @page -> PageNumber`, among them, and no marker at all is [SQLSRC118](diagnostics.md#sqlsrc118).  In the example under [defaults](#token-defaults), `-- param: @page int` is what makes the query valid.
- **The marker names the parameter as the SQL writes it**, `@name`, alone or followed by a space or `->`.  Anything else is [SQLSRC111](diagnostics.md#sqlsrc111).
- **A parameter may be declared twice** if the two markers say the same.  Two that differ in type, in nullability, in C# type or in the word `json` are [SQLSRC112](diagnostics.md#sqlsrc112), and `not null` counts as a difference: `@a int` and `@a int not null` conflict, though they mean the same.

The marker goes inside a query, under its `-- name:` line.  In the file header it is [SQLSRC116](diagnostics.md#sqlsrc116): see [markers in the wrong place](sql-files.md#markers-in-the-wrong-place).

A change to the type or the nullability of a `-- param:` marker, like a change to the SQL, makes the query's sidecar out of date, and so does adding or removing `not null`, since it is recorded apart from saying nothing.  Run `dotnet sqlsource describe` again: see [sidecar files](tool.md#sidecar-files).  The C# type after `->`, with or without `json`, changes nothing that is described, so adding, changing or removing it leaves the sidecar current, and so does adding or removing a marker that gives neither a type nor a nullability, such as `-- param: @team -> TeamId` or `-- param: @team`.  A query with such a marker whose entry an earlier version of the tool wrote is out of date once: describe it again.

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
var products = await connection.ListProducts(new ListProductsParams(["books", "games"], excluded));
```

A parameter written directly after `IN` or `NOT IN`, with no parentheses, is a list of values.  Above, `ListProductsParams.Categories` is an `IEnumerable<string>?` and `Excluded` an `IEnumerable<int>`, so the [query method](generator.md#query-methods) takes an array, a `List<T>`, a `HashSet<T>` or any other sequence, and sends one parameter for each value: with two categories, `category IN @categories` reaches the database as `category IN (@categories_0, @categories_1)`.  Each value's parameter is named after the list, with a name that no other parameter of the query has, and no variable that it [declares](#declared-variables).  `IN (@categories)` is no list: SQL reads it as a list of one value, and `@categories` is one value, as any other parameter is.

- **A list stands only after `IN` or `NOT IN`, or before `IS NULL` or `IS NOT NULL`.**  Any other use of it, as one value, `parent_category = @categories`, or in the default of a [token](#tokens), is [SQLSRC126](diagnostics.md#sqlsrc126), at that use.  A parameter is a list in every spelling of its name, `IN` is matched ignoring case, and a comment between `IN` and the parameter counts as a space.
- **`position(a IN @p)` is never a list.**  Directly inside the parentheses of a call to `position`, `IN` takes one value, as in PostgreSQL's `position('@' IN @email)`, so `@email` is an ordinary parameter there.  That is the one place: a parameter directly after `IN` anywhere else is a list.  Where another construct of your database takes one value after `IN`, write it in parentheses, `IN (@p)`, which SQL reads as the one value.
- **A `-- param:` marker on a list describes its values.**  `-- param: @categories text` gives each value the type `text`, `-> Category` gives each the C# type `Category`, and `null` makes the list itself nullable.  Without a type, the database works out the values' type from the use, as it would for `category IN (@categories)`: the type of `category`.  A list of `-> json` is SQLSRC126 too: a list holds values, not JSON.
- **A value of a list is never nullable.**  A list of `integer` is an `IEnumerable<int>`, never an `IEnumerable<int?>`, and no marker makes it one: a `NULL` among the values of `IN` matches nothing, and among those of `NOT IN` makes it match no row.
- **The property can be null when the query tests the list for null**, as `@categories IS NULL` does, or when its marker says `null`: see [which properties can be null](generator.md#model-nullability).  A list that is null where its property cannot be is sent as an empty list.
- **An empty list matches no row after `IN`, and every row after `NOT IN`.**  So does a null one.  On SQL Server, a list inside an aggregate, a `GROUP BY` expression or an `OUTPUT` clause fails when it is empty or null: see [known issues](#known-issues).  On PostgreSQL, so does a list in the index predicate of `ON CONFLICT`: see [PostgreSQL's parameters](postgresql.md#parameters).  A null list makes `@categories IS NULL` true and an empty one makes it false, so above a null `Categories` keeps every product and an empty one keeps none.
- **On PostgreSQL, a list after a `NOT`, or one whose truth is used as a value, as in `(o.id IN @ids) IS FALSE`, makes the columns of a query's joined tables possibly null** when the query holds a word such as `LEFT`, `RIGHT`, `FULL` or `OUTER`, even as a function's name, since an empty list keeps the rows an outer join filled with null: [which columns can be null](postgresql.md#nullability) gives every word and every use.
- **A null value in a list of a reference type**, such as a `string`, is sent as a `NULL`.  After `IN` it matches nothing, and the other values still match.  After `NOT IN` it makes the condition match no row at all, as SQL's `id NOT IN (1, NULL)` is never true: keep nulls out of a list that `NOT IN` reads.
- **A list may be used more than once**, and every use sends the same parameters.
- **Each list is read once.**  An array, a `List<T>`, a `HashSet<T>` or any other `ICollection<T>` or `IReadOnlyCollection<T>` is counted when the method is called, and its values are read when they are sent.  Any other sequence, such as a LINQ query, is copied into an array once, when the method is called.  A collection whose count of values has changed by the time they are sent throws `InvalidOperationException`, "The list 'Excluded' changed after the query counted it.": leave a list as it is until the method's task completes, or, for a method that [streams its rows](settings.md#streaming-rows), until the loop has its first row.
- **In the outputs `sql` and `models`, the SQL holds the list as written**, `IN @categories`, for the code that runs it, and under `models` the input model's property is the same `IEnumerable<T>`.  Whether a library runs such SQL is the library's affair: Dapper, for one, sends one parameter for each value on SQL Server, as the query methods do, but only for a list that the query does not test for null, and sends a list to PostgreSQL as one array, which `IN @categories` cannot take: see [the recipe](recipes.md#models-through-dapper).  On PostgreSQL, write [`= ANY(@categories)`](postgresql.md#array-parameters) for a query that Dapper runs.

A change to a list's use, such as `IN (@ids)` to `IN @ids`, is a change to the SQL, so the query's sidecar is out of date until it is described again.

<a id="list-padding"></a>

### Padding

```csharp
int[] ids = [4, 8, 15];
// sent as id IN (@ids_0, @ids_1, @ids_2, @ids_3), with @ids_3 another 15
```

A query method rounds the count of each list up to 1, 2, 4, 8, 16, 32, 64, 128, 256 or 512, and above 512 to the next multiple of 512, and fills the list with its last value.  So 3 values are sent as 4, and 600 as 1,024.  A value repeated changes no result of `IN` or `NOT IN`, and the database sees a few forms of the query, one for each size, rather than one for each count, and keeps a plan of each.

- **The [generator parameter](settings.md#setting-generator-parameters) `list-padding`** turns the padding on or off: it is on by default, and `list-padding=off` sends each list at its own count.
- **Padding never makes a method throw.**  When the padded lists would pass [the limit](#list-limit) and the lists at their own counts would not, the method sends them at their own counts.
- **An empty list or a null one** is sent as one parameter, a `NULL` of the values' type, in `(SELECT @ids_0 WHERE 1 = 0)`, which holds no row.

<a id="list-limit"></a>

### The limit of parameters

```console
System.ArgumentException: The query 'ListProducts' would send 70002 parameters; PostgreSQL accepts 65535.  Pass fewer values in 'Excluded', or send them as an array with = ANY. (Parameter 'parameters')
```

PostgreSQL takes at most 65,535 parameters in one query, and SQL Server 2,098: it takes 2,100, of which Microsoft.Data.SqlClient uses two to send a query with parameters.  Every parameter of the query counts: each one that is no list, one for each list that the query tests for null, and one for each value of each list, or one for an empty list.  A method whose query would send more throws `ArgumentException`, which names the query, the count, the limit and the property of the largest list, when it is called and before anything is sent, a method that [streams its rows](settings.md#streaming-rows) included.  [Padding](#list-padding) alone never makes it throw.

For a list that large, send it as one parameter: on PostgreSQL as an [array](postgresql.md#array-parameters), `id = ANY(@ids)`, and on SQL Server as [one JSON array](sqlserver.md#list-parameters), read with `OPENJSON`.

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
- **If a name is taken for a variable and is a parameter**, a `-- param:` marker with its type makes it one.  The marker without a type is [SQLSRC117](diagnostics.md#sqlsrc117).
- **Under every other dialect**, `DECLARE @n int` declares nothing for SqlSource, and `@n` is a parameter.

<a id="names-read-as-parameters"></a>

## Names that are read as parameters

SqlSource finds parameters by how the SQL is written, and not by what the database does with it.  A few names are taken for parameters that the database does not treat as one:

| Database | SQL | How SqlSource reads it | What to do |
|----|----|----|----|
| PostgreSQL | `@x`, written for the absolute value of `x` | As the parameter `x`.  Npgsql reads it the same way | Write `abs(x)`, or `@ x` with a space |
| MySQL, MariaDB | A user variable, `SET @n := 1` | As a parameter, at each place it is written | No marker says that a name is a variable.  A MySQL or MariaDB query is not described, so it has the output `sql`, and the parameter changes no generated code |
| SQL Server | The parameter of a procedure that the SQL creates, `CREATE PROCEDURE p @id int AS ...` | As a parameter of the query | Give the query `-- output: sql`: SQL Server does not describe a statement that creates a procedure ([SQLSRC225](diagnostics.md#sqlsrc225)), and with that output the parameter changes no generated code |

Under `mssql`, the opposite can happen too: a name is taken for a variable when it is a parameter.  See [known issues](#known-issues).

<a id="tokens"></a>

## Tokens

```sql
-- name: ListFrom
-- summary: Lists the rows of one table.
SELECT id, name FROM {{table}} ORDER BY {{orderBy}};
```

> **Tokens are for trusted text only.**  A token is replaced by string concatenation.  Nothing is escaped, quoted or checked for safety, so a value that a user can influence is a SQL injection.  Use a token for a fragment that your own code chooses, such as a table name from a fixed list, and a query parameter for every value.

`{{name}}` in a query is replaced with text that the caller supplies.  A query with a token whose output is `sql` or `models` is a static method and not a constant.  The method has one `string` parameter for each token name, in the order the names first appear, and returns the SQL with every token replaced:

```csharp
var sql = Sql.ListFrom("users", "name DESC");
// SELECT id, name FROM users ORDER BY name DESC;
```

Where the method goes is described on [the generator page](generator.md).  Under the output `codegen`, the query's [method](generator.md#method-signature) takes the same `string` arguments, after its input model, fills in the tokens the same way and runs the query: `await connection.ListFrom("users", "name DESC")`.

- **A token's name is a C# identifier that is not a reserved keyword.**  A keyword is [SQLSRC114](diagnostics.md#sqlsrc114).  Spaces and tabs inside the braces are ignored.
- **Names are case-sensitive.**  `{{Table}}` and `{{table}}` are two tokens.
- **A name that is used more than once is one token**, and every occurrence is replaced.  It is one argument.
- **A token is replaced wherever it is written**, including inside a string or a quoted identifier.
- **Braces around anything that is not a name, or a name and a default, are not a token.**  `{{table-name}}`, `{{1st}}`, `{{order by}}` and `{{name:` with no closing `}}` stay in the SQL as written, and nothing is reported.
- **Comments are removed before tokens are found**, so a `{{x}}` in a comment is not a token.  A query that keeps its comments (the `keep-comments` [generator parameter](settings.md#setting-generator-parameters)) is the exception: a kept comment is part of the SQL, and a token in it is replaced like any other.
- **A query that gains its first token changes from a constant to a method**, or its query method gains an argument, so the code that uses it stops compiling until it passes the arguments.

<a id="token-defaults"></a>

### Defaults

```sql
-- name: ListUsers
-- token: {{filter:AND deleted_at IS NULL}}
SELECT id, name FROM {{table:users}}
WHERE 1 = 1 {{filter}}
ORDER BY {{orderBy:name}};
```

A token can carry a default: `{{name:default}}`.  The default is everything after the first `:` up to the closing `}}`, without the white space around it, so `{{cast:x::int}}` is the token `cast` with the default `x::int`.  It may be empty, `{{extraWhere:}}`, and may run over several lines.  A comment must be wholly inside a token's default or outside the token, or it is [SQLSRC120](diagnostics.md#sqlsrc120): comments are removed before tokens are found, and kept where the query keeps its comments, so the two would read different tokens.  So a comment inside a default must not hold `}}`, and a `--` comment runs to the end of its line, so `{{filter:AND a = 1 -- note}}` is one: put the comment on a line of its own, outside the default.  Where a query keeps its comments, a comment must not open a default, `{{name:`, that the SQL after it closes, nor stand between the braces and the name or between the name and the colon or closing braces, as in `{{f/* c */}}`.  The rule holds for a name in a [`-- token-ignore:`](#token-ignore) marker too, since its text also runs to the next `}}`, as a token's does.

**A default is a sample of what the caller will pass.**  The generated method does not use it: it still takes every token as a `string`, and nothing uses the default when your code runs.  It is there for the tool.  A query that is described cannot be sent to a database with `{{where}}` in it, so the tool puts the token's default there.  A token without a default in a query that must be described is [SQLSRC210](diagnostics.md#sqlsrc210).  An empty default describes the query with nothing in the token's place.

A parameter inside a default is part of the sample.  It is a parameter of the query only if the query's SQL holds it outside a token or a `-- param:` marker declares it, as [the marker section](#param-marker) says:

```sql
-- name: ListPage
-- param: @page int
SELECT id FROM users {{paging:LIMIT 20 OFFSET @page}};
```

<a id="token-marker"></a>

#### `-- token:`

A `-- token:` marker inside a query gives the default of a token that the query writes several times, or whose sample is long.  Its value is exactly what the SQL would hold: one token with its default, as `{{filter:AND deleted_at IS NULL}}` is above.

- A marker that is not exactly one token with a default is [SQLSRC111](diagnostics.md#sqlsrc111), and so is a default with a quote or a block comment that does not close inside it.  A name that is a keyword is [SQLSRC114](diagnostics.md#sqlsrc114).
- A marker for a token the query does not write is ignored.
- Two defaults for one token of a query must be the same, whether they are two in the SQL, two markers, or one of each.  Two that differ are [SQLSRC112](diagnostics.md#sqlsrc112).  Comments in a default are ignored when defaults are compared, wherever the default is written, so `{{f:a /* note */ b}}` in a marker and in the SQL is one default.
- The marker goes inside a query, not in the file header.

A change to a default, like a change to the SQL, makes the query's sidecar out of date.  A change to a comment inside a default does not.

<a id="token-ignore"></a>

### Text that looks like a token: `-- token-ignore:`

```sql
-- name: Greeting
-- token-ignore: name
SELECT 'Hello, {{name}}' AS greeting;
```

`{{name}}` here is text for a template engine and not a token.  A `-- token-ignore: name` marker inside the query keeps every `{{name}}` of the query as it is written: the query stays a constant.

- One name for each marker.  A query can have several markers.
- The value is one C# identifier and nothing else, or it is [SQLSRC111](diagnostics.md#sqlsrc111).  A keyword is accepted, so `-- token-ignore: class` keeps a `{{class}}` that would otherwise be [SQLSRC114](diagnostics.md#sqlsrc114).
- It applies to the query it stands in, and to a `{{name:default}}` as well as `{{name}}`.

<a id="token-validation"></a>

### Validation

```sql
-- name: ListFiltered
-- generator: token-validation=off
SELECT id, name FROM users {{whereClause}};
```

By default the method, a token method or a query method, checks each argument with `ArgumentException.ThrowIfNullOrWhiteSpace`: null throws `ArgumentNullException`, and an empty or blank string throws `ArgumentException`.  The check is that a value is present, not that it is safe.

An empty fragment can be what you want, for an optional clause for example, so the check can be turned off with the `token-validation=off` generator parameter.  Where a generator parameter can be written, and which place wins, is on [the settings page](settings.md#setting-generator-parameters).

With validation off the method checks nothing.  An empty argument leaves nothing where its token was, and a null argument throws `NullReferenceException`.

<a id="known-issues"></a>

## Known issues

- **Under `mssql`, a parameter can be taken for a declared variable.**  After a `DECLARE` with no `;`, a statement that starts with a word SqlSource does not know to start a statement, such as `SEND` or a label, does not end the list.  A parameter after a comma in it, outside parentheses, is then taken for a variable and left out of the parameter list.  End the `DECLARE` with a `;`, or declare the parameter with its type: `-- param: @id int`.
- **A user variable of MySQL or MariaDB, and a parameter of a procedure that the SQL creates, are listed as parameters.**  See [names that are read as parameters](#names-read-as-parameters).
- **On SQL Server, a list inside an aggregate, a `GROUP BY` expression or an `OUTPUT` clause fails when it is empty or null.**  The query method throws `SqlException`, though every list that holds a value runs.  Test the list in a `WITH` query and aggregate its result: see [SQL Server's known issues](sqlserver.md#known-issues).
