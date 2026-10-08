# SqlSource

A C# source generator that turns the queries in your `.sql` files into string constants, and into methods where a query has replacement tokens.

Keep SQL in `.sql` files, where your editor highlights it and your tools can run it:

```sql
-- name: GetUser
-- summary: Loads one user by id.
SELECT id, name
FROM users
WHERE id = @id;

-- name: ListUsers
SELECT id, name FROM users ORDER BY name;
```

Mark a partial type in the same folder, and use the queries by name:

```csharp
using SqlSource;

[SqlSourceGenerate]
public partial class UserRepository(IDbConnection connection)
{
    public Task<User> Get(int id) => connection.QuerySingleAsync<User>(Sql.GetUser, new { id });
}
```

A query that is renamed or removed is a compile error where it is used, and IntelliSense shows each query's summary and its SQL.

## Status

SqlSource is in early development, and no version has been published to nuget.org.  It will be published as the `SqlSource` package.

## Installation

```bash
dotnet add package SqlSource
```

The package is a development dependency.  It adds nothing to your application's output and nothing to the dependencies of a package you build.

## The attribute

`[SqlSourceGenerate]` goes on a partial class, struct, record or record struct.  The type may be static, generic, or nested in other types, as long as it and every type that contains it is `partial`.

| Property | Default | Meaning |
|----|----|----|
| `Path` | The folder of the source file that carries the attribute | A folder or one `.sql` file, relative to that folder |
| `SqlLocation` | `SqlLocation.Nested` | Where the generated members go |

### Which files belong to a type

- Without `Path`, the type gets every `.sql` file in the folder of the source file that carries the attribute.  Subfolders are not searched.
- A `Path` that ends in `.sql` names one file: `[SqlSourceGenerate(Path = "Queries/Users.sql")]`.
- Any other `Path` names a folder: `[SqlSourceGenerate(Path = "../Queries")]`.
- `Path` is always relative to the folder of the source file, never to the project.  Both `/` and `\` separate folders, and paths are compared ignoring case, so a project builds the same on every operating system.  Two `.sql` files of a type whose paths differ only by case are therefore an error, [SQLSRC013](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc013).
- Two types may use the same file.  A `.sql` file that no type uses is ignored, so a folder of migration scripts elsewhere in the project does no harm.

### Locations

| `SqlLocation` | Generated members | Used as |
|----|----|----|
| `SqlLocation.Nested` | Public members in a `private static class Sql` nested in the type | `Sql.GetUser`, inside the type only |
| `SqlLocation.Direct` | Public members on the type itself | `UserQueries.GetUser`, wherever the type is visible |

```csharp
[SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
public static partial class UserQueries;
```

Each member is a `const string`, or a static method when the query has tokens (see Tokens, below).  Both are documented with the query's summary and its SQL.

### Projects that share internals

SqlSource adds the attribute and `SqlLocation` to each project that uses it, as internal types.  A project that sees the internals of another one, as a test project does through `InternalsVisibleTo`, sees both types twice when both projects use SqlSource.  Each project uses its own copy.  The compiler warns about such a conflict (CS0436), and SqlSource turns that warning off for these two types only, as suppression `SQLSRC901`: nothing has to be added to `NoWarn`, and a conflict between two types of your own is still reported.

## SQL files

A line comment that starts its line and has the form `-- word: rest`, where the word is one SqlSource knows, is a marker.  Nothing else in a comment is read.  The markers are `-- name:`, `-- summary:`, `-- generator:`, `-- dialect:` and `-- token:`, written in any case, and each has its own form for the rest of the line.

### Queries

A line comment that starts its line and has the form `-- name: GetUser` begins a query.  The query runs to the next `-- name:` line or to the end of the file, and its name becomes the member's name, so it must be a C# identifier.

A file with no `-- name:` line is one query, named after the file: `CountUsers.sql` becomes `CountUsers`.

Before the first `-- name:` line a file may hold comments, such as a licence header, `-- generator:` lines that apply to every query in the file, and a `-- dialect:` marker (see Dialects, below).

### Summaries

A `-- summary:` line inside a query becomes the documentation of its member.  Several are joined with a space.  A query without one is documented with its name and its file.

### What reaches the generated SQL

- Every marker line is removed: `-- name:`, `-- summary:`, `-- generator:`, `-- dialect:`, `-- token:`, `-- token-ignore:` and `-- param:`.
- Comments are removed: a line comment is deleted and a block comment becomes one space.  Lines left blank are removed.
- Optimizer hints, `/*+ ... */` and `/*! ... */`, are kept.  So are MariaDB's `/*M! ... */` and Oracle's `--+ ...` when the dialect is theirs.
- Strings and quoted identifiers are copied exactly as written.  Where one starts and ends depends on the dialect (see Dialects, below).
- Line endings are always `\n`, so the SQL does not depend on how the file was checked out.

### Generator parameters

A `-- generator:` line holds one or more generator parameters, separated by spaces.  Inside a query it applies to that query.  Before the first `-- name:` line it applies to every query in the file.

| Parameter | Effect |
|----|----|
| `keep-comments` | Comments and blank lines stay in the SQL |
| `token-validation`, `no-token-validation` | The query's method checks its arguments, or does not, whatever the project says (see Tokens, below) |

```sql
-- name: Report
-- generator: keep-comments
SELECT /* the database logs this comment */ id FROM users;
```

## Dialects

Databases disagree about where a comment or a string ends.  `'it\'s'` is one string in MySQL and an unclosed one in PostgreSQL; `#` starts a comment in MySQL and names a temporary table in SQL Server.  SqlSource removes comments, so it has to know which rules your SQL follows.  Tell it the dialect.

| Dialect | Also accepted | Use it for |
|----|----|----|
| `ansi` | | The default.  Any database without a dialect of its own, such as Db2. |
| `mssql` | `sqlserver`, `tsql` | SQL Server, Azure SQL |
| `postgres` | `postgresql` | PostgreSQL, DuckDB |
| `cockroachdb` | `cockroach` | CockroachDB |
| `mysql` | | MySQL |
| `mariadb` | | MariaDB |
| `sqlite` | | SQLite |
| `oracle` | | Oracle, Firebird |

Names are not case-sensitive.

### Setting the dialect

For the project, with an MSBuild property:

```xml
<PropertyGroup>
    <SqlSourceDialect>postgres</SqlSourceDialect>
</PropertyGroup>
```

For some of its files, with metadata on their items.  Where two lines match a file, the later one wins:

```xml
<ItemGroup>
    <AdditionalFiles Update="Reporting/**/*.sql" SqlSourceDialect="mssql" />
</ItemGroup>
```

For one file, with a marker in the file:

```sql
-- dialect: mysql

-- name: FindByNote
SELECT id FROM notes WHERE body = 'it\'s here'; # MySQL reads this as a comment
```

The marker wins over the metadata, and the metadata over the property.  A file has one dialect:

- The marker goes before the file's first `-- name:` line and before its first SQL.  Anywhere else it is the error [SQLSRC115](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc115).
- Its value is the rest of the line: a dialect name, then any options, separated by commas, with any spaces around them.  Nothing else may follow it on the line.
- It takes effect on the line after it.  Comments above it, such as a licence header, are read by the dialect that the metadata or the property gives.
- A name that is not a dialect is an error: [SQLSRC011](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc011) in the property or the metadata, [SQLSRC111](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc111) in the marker.

### Options of a dialect

MySQL and MariaDB have SQL modes that change how a string is read.  If your server runs with one, name it after the dialect, with a comma:

| Option | Also accepted | SQL mode | What it changes |
|----|----|----|----|
| `ansi-quotes` | `ansi_quotes` | `ANSI_QUOTES` | `"..."` is a quoted identifier, and a backslash does not escape in it |
| `no-backslash-escapes` | `no_backslash_escapes` | `NO_BACKSLASH_ESCAPES` | A backslash does not escape in `'...'` or in `"..."` |

```xml
<PropertyGroup>
    <SqlSourceDialect>mysql,ansi-quotes</SqlSourceDialect>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Update="Legacy/**/*.sql" SqlSourceDialect="mariadb,ansi-quotes,no-backslash-escapes" />
</ItemGroup>
```

```sql
-- dialect: mysql, no-backslash-escapes

-- name: GetPath
SELECT 'C:\temp\' AS path;
```

- Options are not case-sensitive and come in any order.
- Only `mysql` and `mariadb` have options.  An option of another dialect, or one that does not exist, is the same error as a name that is not a dialect.
- A value replaces the one it wins over whole.  A file with `-- dialect: mysql` in a project that sets `mysql,ansi-quotes` is read as plain `mysql`.

### What a dialect changes

| | `ansi` | `mssql` | `postgres` | `cockroachdb` | `mysql` | `mariadb` | `sqlite` | `oracle` |
|----|----|----|----|----|----|----|----|----|
| A backslash escapes in `'...'` and `"..."` | No | No | No | No | Yes | Yes | No | No |
| A backslash escapes in `E'...'` | Yes | No | Yes | Yes | Yes | Yes | No | No |
| `` `...` `` is a quoted identifier | Yes | No | No | No | Yes | Yes | Yes | No |
| `[...]` is a quoted identifier | No | Yes | No | No | No | No | Yes | No |
| `$tag$...$tag$` is a string | Yes | No | Yes | Yes | Yes | No | No | No |
| `q'[...]'` is a string | No | No | No | No | No | No | No | Yes |
| A `/*` inside a block comment needs its own `*/` | Yes | Yes | Yes | Yes | No | No | No | No |
| `--` is a comment with no whitespace after it | Yes | Yes | Yes | Yes | No | No | Yes | Yes |
| `#` starts a comment | No | No | No | No | Yes | Yes | No | No |
| Kept as hints, besides `/*+ ... */` and `/*! ... */` | | | | | | `/*M! ... */` | | `--+ ...` |

Four details:

- In `mssql` a `]]` inside brackets stands for one `]`.  In `sqlite` the first `]` ends the identifier.
- In `mysql` and `mariadb` an option changes the first row (see Options of a dialect, above).
- In `postgres` and `cockroachdb` an `E'...'` string that is continued on the next line is one string, and each later part takes backslash escapes too.  In `postgres`, `--` comments may stand between the parts, as PostgreSQL allows, and they are removed like any other comment.  In `cockroachdb` only whitespace may.
- In `cockroachdb` a bytes literal, `b'...'`, takes backslash escapes as an `E'...'` string does.

Under `mysql` and `mariadb` a marker needs the space that those databases need: `-- name: GetUser` is a marker, and `--name: GetUser` is not a comment at all.

### What is still read differently

SqlSource reads these differently from the database, whatever the dialect:

| Construct | Database | How SqlSource reads it |
|----|----|----|
| A versioned comment whose body holds a string that contains `*/`, such as `/*!50700 SELECT '*/' */` | MySQL, MariaDB | The comment ends at the first `*/`.  Where the server ends it depends on the server's version. |
| A block comment that is still open at the end of the file | SQLite | The error [SQLSRC102](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc102) |
| A file whose lines end with a carriage return alone, with no line feed | MySQL, SQLite, CockroachDB | A line ends there, as in every dialect.  These databases end a `--` comment only at a line feed. |
| A client command that is not SQL: `DELIMITER`, `GO`, SQL*Plus `PROMPT` and `REM`, a psql `\` command | All | As SQL, so a quote in it can open a string |

And `ansi` reads the SQL of every database by one set of rules, so it misreads each construct in the table above that it says No to and your database says Yes to.  The fix for those is to set the dialect.

A misread has one of two results:

- **A build error** that reports an unclosed quote or comment ([SQLSRC101](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc101), [SQLSRC102](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc102)) in SQL that is valid for your database.  `keep-comments` does not help.
- **Missing SQL.**  The misread quotes happen to balance, and the SQL after them is taken for a comment and removed: under `ansi`, `SELECT 'a\'b -- c', 2` becomes `SELECT 'a\'b`.  Nothing is reported.  `keep-comments` on the query prevents the removal.

If your SQL uses one of these constructs, check the generated SQL: hover over the member, or read its documentation.

## Parameters

SqlSource finds the parameters of a query: `@` and a name of letters, digits and `_`, outside strings, quoted identifiers, comments and hints.  Names are compared ignoring case.  `@>`, `<@`, `@@`, `@?` and `@@ROWCOUNT` are not parameters, and neither is the `@` inside a word such as `user@host`.

Nothing is generated from a query's parameters yet, and a parameter stays in the SQL exactly as written.  The list is what a later release types.

A `-- param:` marker inside a query declares one parameter:

```sql
-- name: FindUsers
-- param: @since timestamptz not null
-- param: @name text null
-- param: @page int
SELECT id, name FROM users
WHERE created_at >= @since AND (@name IS NULL OR name = @name) {{paging:LIMIT 20 OFFSET @page}};
```

- The form is `@name`, then a type, then `null` or `not null`; the type and the last part are each optional.  The type is in the database's own words and is not checked here: `decimal(18,2)`, `double precision`.
- `not null` says what saying nothing says.  It is accepted because it is what a column definition says.
- A parameter that the query's SQL does not hold, because it comes only with a token, must be declared with its type: without one it is the error [SQLSRC117](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc117).  A parameter that appears only in a token's default and is not declared is [SQLSRC118](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc118).
- A T-SQL local variable or a MySQL user variable is read as a parameter, and so is `@x` written for PostgreSQL's absolute value of `x`: write `abs(x)`.

## Tokens

A query parameter such as `@id` cannot stand for a table name, a list of columns or a whole clause.  A token can: `{{name}}` in a query is replaced with text that the caller supplies.

```sql
-- name: ListFrom
-- summary: Lists the rows of one table.
SELECT id, name FROM {{table}} ORDER BY {{orderBy}};
```

A query with a token is a static method, not a constant.  The method has one `string` parameter for each token name, in the order the names first appear, and returns the SQL with every token replaced:

```csharp
var sql = Sql.ListFrom("users", "name DESC");
// SELECT id, name FROM users ORDER BY name DESC;
```

- A token's name is a C# identifier that is not a reserved keyword; a keyword is the error [SQLSRC114](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc114).  Spaces and tabs inside the braces are ignored.
- Names are case-sensitive: `{{Table}}` and `{{table}}` are two parameters.
- A name that is used more than once is one parameter, and every occurrence is replaced.
- A token is replaced wherever it is written, including inside a string or a quoted identifier.
- Braces around anything that is not a name, or a name and a default, such as `{{table-name}}`, `{{1st}}` or `{{order by}}`, are not a token.  The text stays in the SQL as written, and nothing is reported.
- Text that has the form of a token and is not meant as one stays in the SQL when a `-- token-ignore: name` marker inside the query lists its name.  One name for each marker; a query can have several.
- After its first call, the method allocates the string it returns and nothing else.
- A query that gains its first token changes from a constant to a method, so the code that uses it stops compiling until it passes the argument.

### Defaults

A token can carry a default: `{{name:default}}`.  The default is everything after the first `:` up to the closing `}}`, without the white space around it, so `{{cast:x::int}}` is the token `cast` with the default `x::int`.  It may be empty, `{{extraWhere:}}`, and may run over several lines.

```sql
-- name: ListUsers
-- token: {{filter:AND deleted_at IS NULL}}
SELECT id, name FROM {{table:users}}
WHERE 1 = 1 {{filter}}
ORDER BY {{orderBy:name}};
```

A default is a sample of what the caller will pass.  It changes nothing that is generated today: the method still takes each token as a `string`, and nothing uses the default at run time.  It is there for the tool that will describe a query to its database, which needs SQL it can send.

- A `-- token:` marker inside a query gives the default for a token that the query writes several times, or whose sample is long.  Its value is exactly what the SQL would hold: one token with its default.
- Two defaults for one token of a query must be the same.  Two that differ are the error [SQLSRC112](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc112).
- `{{a:b}}` in SQL that does not mean a token, the text of a template for example, is kept as written by a `-- token-ignore: a` marker.

**Tokens are for trusted text only.**  A token is replaced by string concatenation.  Nothing is escaped, quoted or checked for safety, so a value that a user can influence is a SQL injection.  Use a token for a fragment that your own code chooses, such as a table name from a fixed list, and a query parameter for every value.

### Validation

By default the method checks each argument with `ArgumentException.ThrowIfNullOrWhiteSpace`: null throws `ArgumentNullException`, and an empty or blank string throws `ArgumentException`.  The check is that a value is present, not that it is safe.

An empty fragment can be what you want, for an optional clause for example, so the check can be turned off.  Three switches decide, and the first one that applies wins:

1. A `-- generator: token-validation` or `-- generator: no-token-validation` line inside the query.
2. The same line before the first `-- name:` line, which covers every query in the file.
3. The MSBuild property `SqlSourceTokenValidation`, which covers the project.  It accepts `true` and `false`; any other value is the error [SQLSRC010](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc010).

```xml
<PropertyGroup>
    <SqlSourceTokenValidation>false</SqlSourceTokenValidation>
</PropertyGroup>
```

```sql
-- name: ListFiltered
-- generator: no-token-validation
SELECT id, name FROM users {{whereClause}};
```

With validation off the method checks nothing.  An empty argument leaves nothing where its token was, and a null argument throws `NullReferenceException`.

## Errors

Every problem SqlSource finds is a build error with an id that starts `SQLSRC`.  An error in a `.sql` file is reported at its line and column in that file, and that file produces no members until it is fixed.  [docs/diagnostics.md](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md) explains each one.

## MSBuild

The package registers every `.sql` file under the project's folder with the compiler as an `AdditionalFiles` item, which is how a source generator sees a file that is not C#.  The output and intermediate folders are left out.  A `.sql` file outside the project's folder is not registered; list it yourself.

To leave some files out:

```xml
<ItemGroup>
    <AdditionalFiles Remove="Migrations/**/*.sql" />
</ItemGroup>
```

To turn the default off and list the files yourself:

```xml
<PropertyGroup>
    <SqlSourceIncludeFiles>false</SqlSourceIncludeFiles>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Include="Queries/**/*.sql" />
</ItemGroup>
```

### Token validation

`SqlSourceTokenValidation` turns the argument checks of the generated methods off for a project when it is `false`.  See Tokens, above.

### Dialect

`SqlSourceDialect` names the database whose rules the `.sql` files are read by.  It is a property for the project and metadata of an `AdditionalFiles` item for some of its files.  See Dialects, above.

A project that lists its own files can give the metadata where it lists them:

```xml
<ItemGroup>
    <AdditionalFiles Include="Queries/**/*.sql" SqlSourceDialect="postgres" />
</ItemGroup>
```

## Supported environments

A project that uses SqlSource must target .NET 8 or later; the generated code relies on it, and an older target is reported as [SQLSRC003](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc003).

The generated code is C# 12, the default language version of a project that targets .NET 8.  A project that sets `LangVersion` below 12 gets [SQLSRC012](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc012).

The generator is compiled against Roslyn 4.8.0, so it loads in the .NET 8 SDK and later and in Visual Studio 2022 17.8 and later.  Older SDKs and IDEs are not supported.

This is what a project that *uses* the generator needs.  Working on the generator itself needs more; see Contributing, below.

## Contributing

- [CONTRIBUTING.md](https://github.com/mbcrawfo/SqlSource/blob/main/CONTRIBUTING.md): development setup, building, testing and the checks to run before a commit.
- [docs/publishing.md](https://github.com/mbcrawfo/SqlSource/blob/main/docs/publishing.md): versioning and releasing.
