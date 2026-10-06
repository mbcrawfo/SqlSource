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

[SqlQueries]
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

`[SqlQueries]` goes on a partial class, struct, record or record struct.  The type may be static, generic, or nested in other types, as long as it and every type that contains it is `partial`.

| Property | Default | Meaning |
|----|----|----|
| `Path` | The folder of the source file that carries the attribute | A folder or one `.sql` file, relative to that folder |
| `Mode` | `SqlQueriesMode.Nested` | Where the generated members go |

### Which files belong to a type

- Without `Path`, the type gets every `.sql` file in the folder of the source file that carries the attribute.  Subfolders are not searched.
- A `Path` that ends in `.sql` names one file: `[SqlQueries(Path = "Queries/Users.sql")]`.
- Any other `Path` names a folder: `[SqlQueries(Path = "../Queries")]`.
- `Path` is always relative to the folder of the source file, never to the project.  Both `/` and `\` separate folders, and paths are compared ignoring case, so a project builds the same on every operating system.
- Two types may use the same file.  A `.sql` file that no type uses is ignored, so a folder of migration scripts elsewhere in the project does no harm.

### Modes

| Mode | Generated members | Used as |
|----|----|----|
| `SqlQueriesMode.Nested` | Public members in a `private static class Sql` nested in the type | `Sql.GetUser`, inside the type only |
| `SqlQueriesMode.Direct` | Public members on the type itself | `UserQueries.GetUser`, wherever the type is visible |

```csharp
[SqlQueries(Mode = SqlQueriesMode.Direct)]
public static partial class UserQueries;
```

Each member is a `const string`, or a static method when the query has tokens (see Tokens, below).  Both are documented with the query's summary and its SQL.

## SQL files

### Queries

A line comment that starts its line and has the form `-- name: GetUser` begins a query.  The query runs to the next `-- name:` line or to the end of the file, and its name becomes the member's name, so it must be a C# identifier.

A file with no `-- name:` line is one query, named after the file: `CountUsers.sql` becomes `CountUsers`.

Before the first `-- name:` line a file may hold comments, such as a licence header, and `-- SqlSource:` directives that apply to every query in the file.

### Summaries

A `-- summary:` line inside a query becomes the documentation of its member.  Several are joined with a space.  A query without one is documented with its name and its file.

### What reaches the generated SQL

- The `-- name:`, `-- summary:` and `-- SqlSource:` lines are removed.
- Comments are removed: a line comment is deleted and a block comment becomes one space.  Lines left blank are removed.
- Optimizer hints, `/*+ ... */` and `/*! ... */`, are kept.
- Strings and quoted identifiers are copied exactly as written.
- Line endings are always `\n`, so the SQL does not depend on how the file was checked out.

### Directives

A `-- SqlSource:` line holds one or more directives, separated by spaces.  Inside a query it applies to that query.  Before the first `-- name:` line it applies to every query in the file.

| Directive | Effect |
|----|----|
| `preserve-comments` | Comments and blank lines stay in the SQL |
| `token-ignore=name` | `{{name}}` is literal text, not a token |
| `token-validation`, `no-token-validation` | The query's method checks its arguments, or does not, whatever the project says (see Tokens, below) |

```sql
-- name: Report
-- SqlSource: preserve-comments
SELECT /* the database logs this comment */ id FROM users;
```

### Dialect limits

SqlSource finds comments and strings with one set of rules for every database: ANSI SQL, plus the PostgreSQL and MySQL quoting forms that cannot be mistaken for anything else.  It reads the constructs below differently from the database they are written for.

| Construct | Dialect | How SqlSource reads it |
|----|----|----|
| A backslash escape in a plain string, `'a\'b'` or `"a\"b"` | MySQL | The backslash is an ordinary character, so the string ends at the escaped quote |
| A quote-operator literal, `q'[it's]'` | Oracle | An ordinary string that ends at the first quote inside it |
| A bracketed identifier that contains a quote, `--` or `/*`, such as `[a'b]` | SQL Server | Plain text, so the quote opens a string and `--` starts a comment |
| `--` with no whitespace after it, `5--3` | MySQL | A comment |
| A `#` comment | MySQL | Plain text, so the comment stays in the SQL |
| A `/*` inside a block comment | MySQL, Oracle, SQLite | A nested comment that needs its own `*/` |

A misread has one of two results:

- **A build error** that reports an unclosed quote or comment ([SQLSRC101](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc101), [SQLSRC102](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc102)) in SQL that is valid for your database.  The construct has to be rewritten; `preserve-comments` does not help.
- **Missing SQL.**  The misread quotes happen to balance, and the SQL after them is taken for a comment and removed: `SELECT 'a\'b -- c', 2` becomes `SELECT 'a\'b`.  Nothing is reported.  `preserve-comments` on the query prevents the removal.

If your SQL uses one of these constructs, check the generated SQL: hover over the member, or read its documentation.

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
- Braces around anything that is not a name, such as `{{table-name}}`, `{{1st}}` or `{{order by}}`, are not a token.  The text stays in the SQL as written, and nothing is reported.
- Text that has the form of a token and is not meant as one stays in the SQL when a `token-ignore=name` directive lists its name.
- After its first call, the method allocates the string it returns and nothing else.
- A query that gains its first token changes from a constant to a method, so the code that uses it stops compiling until it passes the argument.

**Tokens are for trusted text only.**  A token is replaced by string concatenation.  Nothing is escaped, quoted or checked for safety, so a value that a user can influence is a SQL injection.  Use a token for a fragment that your own code chooses, such as a table name from a fixed list, and a query parameter for every value.

### Validation

By default the method checks each argument with `ArgumentException.ThrowIfNullOrWhiteSpace`: null throws `ArgumentNullException`, and an empty or blank string throws `ArgumentException`.  The check is that a value is present, not that it is safe.

An empty fragment can be what you want, for an optional clause for example, so the check can be turned off.  Three switches decide, and the first one that applies wins:

1. A `-- SqlSource: token-validation` or `-- SqlSource: no-token-validation` directive inside the query.
2. The same directive before the first `-- name:` line, which covers every query in the file.
3. The MSBuild property `SqlSourceTokenValidation`, which covers the project.  It accepts `true` and `false`; any other value is the error [SQLSRC010](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc010).

```xml
<PropertyGroup>
    <SqlSourceTokenValidation>false</SqlSourceTokenValidation>
</PropertyGroup>
```

```sql
-- name: ListFiltered
-- SqlSource: no-token-validation
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
    <EnableDefaultSqlSourceItems>false</EnableDefaultSqlSourceItems>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Include="Queries/**/*.sql" />
</ItemGroup>
```

### Token validation

`SqlSourceTokenValidation` turns the argument checks of the generated methods off for a project when it is `false`.  See Tokens, above.

### Deleting or renaming a file

Editing a `.sql` file is always picked up by the next build.  Deleting, renaming or moving one is not: an incremental `dotnet build` can succeed with the old members still in place, because MSBuild does not notice that the list of files changed.  Run `dotnet build --no-incremental` afterwards.  A clean build, such as a CI build, is not affected.

## Supported environments

A project that uses SqlSource must target .NET 8 or later; the generated code relies on it, and an older target is reported as [SQLSRC003](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc003).

The generated code is C# 12, the default language version of a project that targets .NET 8.  The language version is not checked: a project that sets `LangVersion` below 12 can get compiler errors inside generated code.

The generator is compiled against Roslyn 4.8.0, so it loads in the .NET 8 SDK and later and in Visual Studio 2022 17.8 and later.  Older SDKs and IDEs are not supported.

This is what a project that *uses* the generator needs.  Working on the generator itself needs more; see Contributing, below.

## Contributing

- [CONTRIBUTING.md](https://github.com/mbcrawfo/SqlSource/blob/main/CONTRIBUTING.md): development setup, building, testing and the checks to run before a commit.
- [docs/publishing.md](https://github.com/mbcrawfo/SqlSource/blob/main/docs/publishing.md): versioning and releasing.
