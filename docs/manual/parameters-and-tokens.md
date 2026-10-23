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
| Replaced | Never by SqlSource: it stays in the SQL, and you pass the value to the database | By the generated method, when your code asks for the SQL |
| Safe for text a user can influence | Yes | No |

<a id="query-parameters"></a>

## Query parameters

```sql
-- name: GetUser
SELECT id, name FROM users WHERE id = @id;
```

SqlSource finds the parameters of a query: `@` and a name of letters, digits and `_`, outside strings, quoted identifiers, comments and hints.  The list holds each parameter once, in the order it first appears.  The tool uses the list to ask a database what each parameter is, the query's input [model](generator.md#models) has a property for each parameter in its order, and a later release will use it to type the generated methods.

> **Coming in a later release.**  The parameters of a query will become the typed arguments of its generated method.  Today they are found and listed, and give the input model its properties.  See [what works today](README.md#what-works-today).

- **A parameter stays in the SQL exactly as written.**  The constant for `GetUser` holds `@id`.
- **Names are compared ignoring case.**  `@Id` and `@id` are one parameter.
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
SELECT id, name FROM users
WHERE created_at >= @since AND (@name IS NULL OR name = @name);
```

A `-- param:` marker inside a query declares one parameter.  The form is `@name`, then a type, then `null` or `not null`.  The type and the last part are each optional.

- **The type is written in the database's own words**, such as `decimal(18,2)` or `double precision`.  SqlSource does not check it.  When the tool describes the query, it asks the database to resolve the type and checks it against every use of the parameter: a type the database does not have is [SQLSRC228](diagnostics.md#sqlsrc228).
- **Without a type, the database decides.**  The tool takes the type that the database infers from how the parameter is used.  When no use says, as in PostgreSQL's `SELECT @p IS NULL`, the tool cannot, and the fix is a type here ([SQLSRC226](diagnostics.md#sqlsrc226)).  Each database infers by its own rules: PostgreSQL takes the type of the first use that gives one ([SQLSRC227](diagnostics.md#sqlsrc227)), and SQL Server types a parameter whose every use is like `WHERE @p IS NULL` as an `int`.  See [PostgreSQL's parameters](postgresql.md#parameters) and [SQL Server's](sqlserver.md#parameters).
- **`null` says the value can be null.**  No database reports whether a parameter can be null, so this is the only way to say it.  A parameter that does not say `null` is read as not nullable, and `not null` is accepted because it is what a column definition says; it has the same meaning as saying nothing.  The tool records the declaration in the sidecar, and `null` makes the parameter's property in the query's input model nullable: see [which properties can be null](generator.md#model-nullability).
- **A parameter that only a token brings needs a type.**  When a parameter stands only in the default of a token (see [defaults](#token-defaults)), nothing but the marker can say what it is.  The marker without a type is [SQLSRC117](diagnostics.md#sqlsrc117), and no marker at all is [SQLSRC118](diagnostics.md#sqlsrc118).  In the example under [defaults](#token-defaults), `-- param: @page int` is what makes the query valid.
- **The marker names the parameter as the SQL writes it**, `@name`, alone or followed by a space.  Anything else is [SQLSRC111](diagnostics.md#sqlsrc111).
- **A parameter may be declared twice** if the two markers say the same.  Two that differ in type or in nullability are [SQLSRC112](diagnostics.md#sqlsrc112), and `not null` counts as a difference: `@a int` and `@a int not null` conflict, though they mean the same.

The marker goes inside a query, under its `-- name:` line.  In the file header it is [SQLSRC116](diagnostics.md#sqlsrc116): see [markers in the wrong place](sql-files.md#markers-in-the-wrong-place).

A change to a `-- param:` marker, like a change to the SQL, makes the query's sidecar out of date, and so does adding or removing `not null`, since it is recorded apart from saying nothing.  Run `dotnet sqlsource describe` again: see [sidecar files](tool.md#sidecar-files).

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
| MySQL, MariaDB | A user variable, `SET @n := 1` | As a parameter, at each place it is written | Nothing yet says that a name is a variable.  It changes no generated code today |
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

`{{name}}` in a query is replaced with text that the caller supplies.  A query with a token is a static method and not a constant.  The method has one `string` parameter for each token name, in the order the names first appear, and returns the SQL with every token replaced:

```csharp
var sql = Sql.ListFrom("users", "name DESC");
// SELECT id, name FROM users ORDER BY name DESC;
```

Where the method goes is described on [the generator page](generator.md).

- **A token's name is a C# identifier that is not a reserved keyword.**  A keyword is [SQLSRC114](diagnostics.md#sqlsrc114).  Spaces and tabs inside the braces are ignored.
- **Names are case-sensitive.**  `{{Table}}` and `{{table}}` are two tokens.
- **A name that is used more than once is one token**, and every occurrence is replaced.  It is one argument.
- **A token is replaced wherever it is written**, including inside a string or a quoted identifier.
- **Braces around anything that is not a name, or a name and a default, are not a token.**  `{{table-name}}`, `{{1st}}`, `{{order by}}` and `{{name:` with no closing `}}` stay in the SQL as written, and nothing is reported.
- **Comments are removed before tokens are found**, so a `{{x}}` in a comment is not a token.  A query that keeps its comments (the `keep-comments` [generator parameter](settings.md#setting-generator-parameters)) is the exception: a kept comment is part of the SQL, and a token in it is replaced like any other.
- **A query that gains its first token changes from a constant to a method**, so the code that uses it stops compiling until it passes the arguments.

<a id="token-defaults"></a>

### Defaults

```sql
-- name: ListUsers
-- token: {{filter:AND deleted_at IS NULL}}
SELECT id, name FROM {{table:users}}
WHERE 1 = 1 {{filter}}
ORDER BY {{orderBy:name}};
```

A token can carry a default: `{{name:default}}`.  The default is everything after the first `:` up to the closing `}}`, without the white space around it, so `{{cast:x::int}}` is the token `cast` with the default `x::int`.  It may be empty, `{{extraWhere:}}`, and may run over several lines.

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
-- generator: no-token-validation
SELECT id, name FROM users {{whereClause}};
```

By default the method checks each argument with `ArgumentException.ThrowIfNullOrWhiteSpace`: null throws `ArgumentNullException`, and an empty or blank string throws `ArgumentException`.  The check is that a value is present, not that it is safe.

An empty fragment can be what you want, for an optional clause for example, so the check can be turned off with the `no-token-validation` generator parameter.  Where a generator parameter can be written, and which place wins, is on [the settings page](settings.md#setting-generator-parameters).

With validation off the method checks nothing.  An empty argument leaves nothing where its token was, and a null argument throws `NullReferenceException`.

<a id="known-issues"></a>

## Known issues

- **A `--` comment inside a token's default swallows its closing braces.**  A line comment runs to the end of its line and takes the `}}` with it, so the token ends at the next `}}` further down, or not at all.  In `{{filter:AND a = 1 -- note}}` followed by a line holding `{{orderBy:name}}`, the default of `filter` runs to the braces of `orderBy`, and the token `orderBy` is gone.  Nothing is reported; the method is missing an argument, which you notice when it does not compile.  Keep line comments out of defaults, and put the comment on a line of its own.
- **A reserved word in braces in a comment can be an error where no comment is kept.**  When the project or a file's item metadata asks for `keep-comments` and the attribute of the type that claims the file turns it off, a comment such as `-- {{class}}` is still [SQLSRC114](diagnostics.md#sqlsrc114).  Take the braces out of the comment, or add a `-- token-ignore:` marker.
- **Under `mssql`, a parameter can be taken for a declared variable.**  After a `DECLARE` with no `;`, a statement that starts with a word SqlSource does not know to start a statement, such as `SEND` or a label, does not end the list.  A parameter after a comma in it, outside parentheses, is then taken for a variable and left out of the parameter list.  End the `DECLARE` with a `;`, or declare the parameter with its type: `-- param: @id int`.
- **A user variable of MySQL or MariaDB, and a parameter of a procedure that the SQL creates, are listed as parameters.**  See [names that are read as parameters](#names-read-as-parameters).
