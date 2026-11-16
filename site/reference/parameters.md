# Query parameters (`@name`, `-- param:`)

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

- **A query parameter stays in the SQL exactly as written.**  The constant for `GetUser`, and the SQL that its method sends, hold `@id`.  A [list](list-parameters.md#list-parameters), `IN @ids`, is the one exception, and only in the SQL that a query method sends: that SQL holds a parameter for each value.
- **Names are compared ignoring case.**  `@Id` and `@id` are one query parameter.
- **Under `mssql`, a second spelling of one name is a warning, [SQLSRC121](../diagnostics.md#sqlsrc121).**  A SQL Server whose server collation is case-sensitive reads the second spelling as another variable.
- The collation of the database does not change how the variables of the query are read, except in a contained database.  In a contained database, the variables ignore case, whatever the collation of the server.
- **The query parameter keeps the spelling that the SQL first writes outside a token's default.**  So a spelling in a default or in a `-- param:` marker does not choose it.
- **A parameter that only a token's default writes, and that a `-- param:` marker declares, is bound as the marker spells it.**
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
- A query parameter is no column of a table, so it names its type in this marker.
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

[List parameters](list-parameters.md), written after `IN` or `NOT IN`, are a page of their own.

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

### SQLSRC231 for a parameter after a `DECLARE`

**What you see.**  Under `mssql`, describing a query stops with [SQLSRC231](../diagnostics.md#sqlsrc231): "SQL Server reads '@id' as a parameter and SqlSource does not".  The query parameter follows a `DECLARE` with no `;`.

**Why.**  SqlSource takes the query parameter for a declared variable.  After a `DECLARE` with no `;`, a statement can start with a word that SqlSource does not know to start a statement, such as `SEND`, or with a label.  That statement does not end the list of declarations.  A query parameter after a comma in it, outside parentheses, is then taken for a variable and left out of the list of query parameters.  SQL Server reads it as a parameter, so the tool stops when it describes the query.

**What to do.**  End the `DECLARE` with a `;`, or declare the query parameter with its type: `-- param: @id int`.
