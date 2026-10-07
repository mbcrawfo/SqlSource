# Diagnostics

Every problem SqlSource finds is a build error, and none can be turned off or made a warning.  An error in a `.sql` file is reported at its line and column in that file.  A `.sql` file with an error produces no members until the error is fixed.

| Id | Title |
|----|----|
| [SQLSRC001](#sqlsrc001) | Type must be partial |
| [SQLSRC002](#sqlsrc002) | Type is file-local |
| [SQLSRC003](#sqlsrc003) | Target framework is not supported |
| [SQLSRC004](#sqlsrc004) | Path matches no SQL file |
| [SQLSRC005](#sqlsrc005) | Folder has no SQL file |
| [SQLSRC006](#sqlsrc006) | Mode is not valid |
| [SQLSRC007](#sqlsrc007) | Type has a member named Sql |
| [SQLSRC008](#sqlsrc008) | Query name is used in two files |
| [SQLSRC009](#sqlsrc009) | Query is named like its containing type |
| [SQLSRC010](#sqlsrc010) | SqlSourceTokenValidation is not valid |
| [SQLSRC011](#sqlsrc011) | SqlSourceDialect is not valid |
| [SQLSRC012](#sqlsrc012) | Language version is not supported |
| [SQLSRC013](#sqlsrc013) | SQL file paths differ only by case |
| [SQLSRC101](#sqlsrc101) | Quote is not closed |
| [SQLSRC102](#sqlsrc102) | Comment is not closed |
| [SQLSRC103](#sqlsrc103) | Query name is not valid |
| [SQLSRC104](#sqlsrc104) | Query name is used twice |
| [SQLSRC105](#sqlsrc105) | File name is not a valid query name |
| [SQLSRC106](#sqlsrc106) | SQL before the first name |
| [SQLSRC107](#sqlsrc107) | Summary before the first name |
| [SQLSRC108](#sqlsrc108) | Marker has no SQL after it |
| [SQLSRC109](#sqlsrc109) | Directive is not known |
| [SQLSRC110](#sqlsrc110) | Directive is missing |
| [SQLSRC111](#sqlsrc111) | Directive value is not valid |
| [SQLSRC112](#sqlsrc112) | Directives conflict |
| [SQLSRC113](#sqlsrc113) | Query has no SQL |
| [SQLSRC114](#sqlsrc114) | Token name is a keyword |
| [SQLSRC115](#sqlsrc115) | Dialect directive is misplaced |

Ids below 100 are about the type that carries `[SqlQueries]`, or about the project.  Ids from 101 are about the contents of a `.sql` file.

`SQLSRC901` is not in this list because it is not a problem.  It is the id under which SqlSource turns off the compiler's warning CS0436 for the two types it adds to every project, in a project that sees the internals of another one that uses SqlSource; see [Projects that share internals](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#projects-that-share-internals).

## SQLSRC001

**Type must be partial**

SqlSource adds members through a second declaration of the type, which C# allows only when every declaration is `partial`.  The same holds for each type the type is nested in.

```csharp
[SqlQueries]
public class UserRepository { }
```

Add `partial` to the type named in the message.

## SQLSRC002

**Type is file-local**

A type declared with the `file` modifier exists only in its own source file, so generated code cannot add to it.  The same holds for a type nested in a file-local type.

```csharp
[SqlQueries]
file partial class UserRepository { }
```

Remove the `file` modifier, or move the attribute to a type that is not file-local.

## SQLSRC003

**Target framework is not supported**

The generated code uses API that first appeared in .NET 8.

Target `net8.0` or later.  For a project that targets several frameworks, put the attributed types under a condition that excludes the older ones.

## SQLSRC004

**Path matches no SQL file**

`Path` is relative to the folder of the source file that carries the attribute.  A value that ends in `.sql` names one file.  Any other value names a folder, and the type gets the `.sql` files directly in it; subfolders are not searched.

```csharp
[SqlQueries(Path = "Queries/User.sql")] // The file is Queries/Users.sql.
public partial class UserRepository { }
```

Check the spelling, and that the path starts from the folder of this source file and not from the project.  If the file exists, check that the build sees it.  The package registers the `.sql` files under the project's own folder, so a file outside it, such as a linked file, must be listed as an `AdditionalFiles` item.  So must every `.sql` file of a project that sets `SqlSourceIncludeFiles` to `false`.

## SQLSRC005

**Folder has no SQL file**

Without `Path`, the type gets every `.sql` file in the folder of the source file that carries the attribute.  That folder has none.

Add a `.sql` file next to the source file, or set `Path` to the folder or file that holds the queries.  If a file is there, check that the build sees it, as described for [SQLSRC004](#sqlsrc004).

## SQLSRC006

**Mode is not valid**

`Mode` was given a value that `SqlQueriesMode` does not define.

```csharp
[SqlQueries(Mode = (SqlQueriesMode)5)]
public partial class UserRepository { }
```

Use `SqlQueriesMode.Nested` or `SqlQueriesMode.Direct`.

## SQLSRC007

**Type has a member named Sql**

In `Nested` mode the queries go in a nested class named `Sql`, and the type already has a member with that name.  The same error is reported when the type itself, or one of its type parameters, is named `Sql`: a nested class cannot share either name.

```csharp
[SqlQueries]
public partial class UserRepository
{
    private string Sql { get; }
}
```

Rename the member, the type or the type parameter, or set `Mode = SqlQueriesMode.Direct` so that the queries become members of the type itself.

## SQLSRC008

**Query name is used in two files**

Two `.sql` files of one type each have a query with the same name, and a type cannot have two members with one name.  The error is at the second query, and the message names the file that holds the first.  The file that holds the second query produces no members for that type.

Rename one of the queries.

## SQLSRC009

**Query is named like its containing type**

A member cannot have the name of the type that contains it.  In `Nested` mode that type is the generated class, so no query can be named `Sql`.  In `Direct` mode it is the attributed type, so no query can have that type's name.

```sql
-- name: Sql
SELECT 1;
```

Rename the query.

## SQLSRC010

**SqlSourceTokenValidation is not valid**

The MSBuild property `SqlSourceTokenValidation` decides whether the method of a query with tokens checks its arguments.  It accepts `true` and `false`, in any case, and the project gives it another value.  The error has no file and line, because the compiler does not tell a generator where a property was set: look in the project file, in `Directory.Build.props`, and at a `-p:` argument of the build command.

```xml
<PropertyGroup>
    <SqlSourceTokenValidation>off</SqlSourceTokenValidation>
</PropertyGroup>
```

Set it to `false` to turn validation off for the project, or remove it to keep the default, which is to validate.  While the value is wrong the generated methods validate.

The compiler hands a generator only the part of a value before the first `;` or `#`.  So `false;true` is read as `false` and is not reported, and `off;false` is reported as `off`.

## SQLSRC011

**SqlSourceDialect is not valid**

`SqlSourceDialect` says which database a project's SQL is written for, so that SqlSource finds its comments and strings by that database's rules.  It is set as an MSBuild property for the project, or as metadata on the `AdditionalFiles` item of a `.sql` file, and one of the two has a value that is not a dialect.  The message quotes the value.

The names are `ansi`, `mssql`, `postgres`, `cockroachdb`, `mysql`, `mariadb`, `sqlite` and `oracle`, in any case.  `sqlserver` and `tsql` also mean `mssql`, `postgresql` also means `postgres`, and `cockroach` also means `cockroachdb`.

After `mysql` or `mariadb` the value may name options, each after a comma, as in `mysql,ansi-quotes`.  The options are `ansi-quotes` and `no-backslash-escapes`, also written `ansi_quotes` and `no_backslash_escapes`.  An option that does not exist, an option after any other dialect, and a comma with nothing after it each make the whole value wrong.

```xml
<PropertyGroup>
    <SqlSourceDialect>pgsql</SqlSourceDialect>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Update="Reporting/**/*.sql" SqlSourceDialect="sql server" />
</ItemGroup>
```

The error has no file and line, because the compiler does not tell a generator where a property or the metadata of an item was set: look in the project file, in `Directory.Build.props`, and at a `-p:` argument of the build command.  It is reported once for each wrong value, however many files have it, and only for a `.sql` file that a type uses.

Correct the name, or remove the setting to get the default, `ansi`.  While a value is wrong the files it covers are read as `ansi`; a file whose own metadata is wrong does not fall back to the project's property.

The compiler hands a generator only the part of a value before the first `;` or `#`, as for [SQLSRC010](#sqlsrc010).

## SQLSRC012

**Language version is not supported**

The code SqlSource generates for a type is C# 12, the default language version of a project that targets .NET 8, and the project sets `LangVersion` to an older one.  The message quotes the version the project uses.

```xml
<PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>10</LangVersion>
</PropertyGroup>
```

Remove `LangVersion` to get the default of the target framework, or set it to `12` or later.  It can be set in the project file, in `Directory.Build.props`, and by a `-p:` argument of the build command.

The error is reported at each `[SqlQueries]` attribute, and no type gets members until it is fixed.  A project that targets a framework older than .NET 8 has an older language version by default; it gets [SQLSRC003](#sqlsrc003) and not this error, because targeting .NET 8 fixes both.

## SQLSRC013

**SQL file paths differ only by case**

Two `.sql` files of the project have paths that differ only by upper and lower case: `Users.sql` and `users.sql` in one folder, or a file of the same name in `Queries` and in `queries`.  SqlSource compares paths ignoring case, so that a project builds the same on every operating system, and the two are one file to it.  It would use the one the project lists first and ignore the other.  The error is at the start of the file that would be ignored, and the message names the other one.

Rename one of the files, or one of the folders.  Such a pair can exist only on a file system that tells upper case from lower, and already breaks a checkout of the repository on Windows and on macOS.

A project that lists one file twice, with spellings that differ by case, gets the same error, because SqlSource may not ask the file system whether two paths are one file.  That takes an `AdditionalFiles` item written by hand for a file that the package already includes, here `Queries/Users.sql`:

```xml
<ItemGroup>
    <AdditionalFiles Include="queries/users.sql" SqlSourceDialect="postgres" />
</ItemGroup>
```

Write `Update` in place of `Include` to change the item the package adds, and spell the path as it is on disk.

The error is reported only for a `.sql` file that a type uses.

## SQLSRC101

**Quote is not closed**

A string or a quoted identifier starts and never ends.  The error is at the opening quote or bracket.

```sql
SELECT 'unfinished FROM users;
```

Close the quote.  If the SQL is valid for your database, SqlSource is reading it by the rules of another one: `'it\'s'` is one string in MySQL and an unclosed one elsewhere.  Set the dialect of the file; see [Dialects](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#dialects) in the README, which also lists the few constructs that no dialect setting reads correctly.

## SQLSRC102

**Comment is not closed**

A block comment or a hint starts with `/*` and never ends.  The error is at the `/*`.

```sql
/* outer /* inner */
SELECT 1;
```

Close the comment.  Whether a `/*` inside a comment needs its own `*/` depends on the dialect: it does in the default dialect, in SQL Server and in PostgreSQL, and it does not in MySQL, MariaDB, SQLite and Oracle.  If the SQL is valid for your database, set the dialect of the file; see [Dialects](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#dialects) in the README.

SQLite accepts a comment that is still open at the end of the file.  SqlSource does not, in any dialect, because the comment would take every later query of the file with it.

## SQLSRC103

**Query name is not valid**

The value of a `-- name:` marker becomes the name of a C# member, so it must be a C# identifier and must not be a reserved keyword such as `class`.

```sql
-- name: get-user
SELECT 1;
```

Rename the query, for example to `GetUser`.

## SQLSRC104

**Query name is used twice**

Two `-- name:` markers in one file have the same name.  The error is at the second.

Rename one of the queries.

## SQLSRC105

**File name is not a valid query name**

A file without a `-- name:` marker is one query, named after the file without its extension.  This file's name cannot be the name of a C# member.

```text
001_init.sql
get-user.sql
```

Add a `-- name:` marker at the top of the file, or rename the file.  If the file is not meant to be a query of this type, move it out of the type's folder or point the type's `Path` somewhere else.

## SQLSRC106

**SQL before the first name**

In a file that has `-- name:` markers, only comments and `-- SqlSource:` directives may come before the first one.  SQL there would belong to no query.

```sql
SET search_path TO app;

-- name: GetUser
SELECT 1;
```

Move the SQL below a `-- name:` marker, or delete it.

## SQLSRC107

**Summary before the first name**

A `-- summary:` marker describes the query it is in.  One before the first `-- name:` marker is in no query.

```sql
-- summary: Loads one user.
-- name: GetUser
SELECT 1;
```

Move the summary below the `-- name:` marker.

## SQLSRC108

**Marker has no SQL after it**

A `-- summary:` or `-- SqlSource:` marker comes before the SQL it describes.  This one is the last thing in its query, which usually means it was written above the next `-- name:` marker and was meant for that query.

```sql
-- name: GetUser
SELECT 1;
-- summary: Lists every user.
-- name: ListUsers
SELECT 2;
```

Move the marker below the `-- name:` marker of the query it describes, or delete it.

## SQLSRC109

**Directive is not known**

A `-- SqlSource:` marker holds a word that is not a directive.  The directives are `keep-comments`, `token-validation`, `no-token-validation`, `token-ignore=name` and `dialect=name`.

```sql
-- SqlSource: keep-comment
```

Correct the directive.

## SQLSRC110

**Directive is missing**

A `-- SqlSource:` marker has nothing after the colon.

Add a directive, or delete the line.

## SQLSRC111

**Directive value is not valid**

A directive lacks a value it needs, has one it does not take, or has one that is not valid.

- `token-ignore` needs a value that is a C# identifier, as in `token-ignore=table`.
- `dialect` needs the name of a dialect, as in `dialect=postgres`, with any options after it, as in `dialect=mysql,ansi-quotes`.  The names and the options are those of [SQLSRC011](#sqlsrc011).  The value is one word: a space after a comma ends it.
- No other directive takes a value.

```sql
-- SqlSource: token-ignore
-- SqlSource: dialect=pgsql
-- SqlSource: keep-comments=true
```

Add the missing value, correct the one that is wrong, or remove the one that does not belong.

## SQLSRC112

**Directives conflict**

Two directives in one scope contradict each other.  A scope is the lines before the first `-- name:` marker, or one query.  The error is at the second directive.

- `token-validation` and `no-token-validation` both appear.
- Two `dialect` directives name different dialects, or one dialect with different options.  A file has one dialect.

Remove one of the two.  A validation directive in a query overrides the one before the first `-- name:` marker, and that is not a conflict.  The same dialect given twice with the same options is not a conflict either.

## SQLSRC113

**Query has no SQL**

A query, or a whole file without `-- name:` markers, holds nothing but whitespace, comments and markers.  A file that cannot be read is reported the same way, at its first line.

```sql
-- name: GetUser
-- TODO: write this query
```

Write the SQL, or delete the query.

## SQLSRC114

**Token name is a keyword**

A token `{{name}}` becomes a parameter of a generated method, so its name must not be a reserved C# keyword.

```sql
SELECT * FROM {{class}};
```

Rename the token.  If the braces are literal text and not a token, add `-- SqlSource: token-ignore=class` to the query.

## SQLSRC115

**Dialect directive is misplaced**

A `dialect` directive sets the dialect of a whole file, and it changes how the text after it is read.  So it must come before the file's first `-- name:` marker and before the file's first SQL.  This one is inside a named query, or after SQL.

```sql
-- name: GetUser
-- SqlSource: dialect=mysql
SELECT 1;
```

A directive after the last SQL of its query is reported twice: as this error, and as [SQLSRC108](#sqlsrc108).

Move the directive to the top of the file.  Comments may come before it, such as a licence header.  In a file with no `-- name:` marker, which is one query, put it above the query's SQL.

```sql
-- SqlSource: dialect=mysql

-- name: GetUser
SELECT 1;
```

A file cannot mix dialects.  Put the queries for another database in a file of their own.
