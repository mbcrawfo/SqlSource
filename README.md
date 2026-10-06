# SqlSource

A C# source generator that turns the queries in your `.sql` files into string constants.

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

Queries become constants today.  A query that contains a `{{token}}` gets no member yet: tokens, which become the parameters of a generated method, are the next piece of work.

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
| `SqlQueriesMode.Nested` | Public constants in a `private static class Sql` nested in the type | `Sql.GetUser`, inside the type only |
| `SqlQueriesMode.Direct` | Public constants on the type itself | `UserQueries.GetUser`, wherever the type is visible |

```csharp
[SqlQueries(Mode = SqlQueriesMode.Direct)]
public static partial class UserQueries;
```

Each member is a `const string`, documented with the query's summary and its SQL.

## SQL files

### Queries

A line comment that starts its line and has the form `-- name: GetUser` begins a query.  The query runs to the next `-- name:` line or to the end of the file, and its name becomes the member's name, so it must be a C# identifier.

A file with no `-- name:` line is one query, named after the file: `CountUsers.sql` becomes `CountUsers`.

Before the first `-- name:` line a file may hold comments, such as a licence header, and `-- SqlSource:` directives that apply to every query in the file.

### Summaries

A `-- summary:` line inside a query becomes the documentation of its member.  Several are joined with a space.  A query without one is documented with its name and its file.

### What reaches the constant

- The `-- name:`, `-- summary:` and `-- SqlSource:` lines are removed.
- Comments are removed: a line comment is deleted and a block comment becomes one space.  Lines left blank are removed.
- Optimizer hints, `/*+ ... */` and `/*! ... */`, are kept.
- Strings and quoted identifiers are copied exactly as written.
- Line endings are always `\n`, so a constant does not depend on how the file was checked out.

### Directives

A `-- SqlSource:` line holds one or more directives, separated by spaces.  Inside a query it applies to that query.  Before the first `-- name:` line it applies to every query in the file.

| Directive | Effect |
|----|----|
| `preserve-comments` | Comments and blank lines stay in the SQL |
| `token-ignore=name` | `{{name}}` is literal text, not a token |
| `token-validation`, `no-token-validation` | Reserved for tokens.  They are accepted and have no effect yet. |

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

If your SQL uses one of these constructs, check the generated constant: hover over the member, or read its documentation.

## Errors

Every problem SqlSource finds is a build error with an id that starts `SQLSRC`.  An error in a `.sql` file is reported at its line and column in that file, and that file produces no members until it is fixed.  [docs/diagnostics.md](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md) explains each one.

## MSBuild

The package registers every `.sql` file of the project with the compiler as an `AdditionalFiles` item, which is how a source generator sees a file that is not C#.  The output and intermediate folders are left out.

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

## Supported environments

A project that uses SqlSource must target .NET 8 or later; the generated code relies on it, and an older target is reported as [SQLSRC003](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc003).

The generator is compiled against Roslyn 4.8.0, so it loads in the .NET 8 SDK and later and in Visual Studio 2022 17.8 and later.  Older SDKs and IDEs are not supported.

This is what a project that *uses* the generator needs.  Working on the generator itself needs more; see [Contributing](#contributing).

## Contributing

- [CONTRIBUTING.md](https://github.com/mbcrawfo/SqlSource/blob/main/CONTRIBUTING.md): development setup, building, testing and the checks to run before a commit.
- [docs/publishing.md](https://github.com/mbcrawfo/SqlSource/blob/main/docs/publishing.md): versioning and releasing.
