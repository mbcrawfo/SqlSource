# Diagnostics

Every problem SqlSource finds is an error, and none can be turned off or made a warning.  The generator reports its errors in the build.  An error in a `.sql` file is reported at its line and column in that file, and a `.sql` file with an error produces no members until the error is fixed.  The `sqlsource` command-line tool prints its errors itself, in the format of a build error.

| Id | Title |
|----|----|
| [SQLSRC001](#sqlsrc001) | Type must be partial |
| [SQLSRC002](#sqlsrc002) | Type is file-local |
| [SQLSRC003](#sqlsrc003) | Target framework is not supported |
| [SQLSRC004](#sqlsrc004) | Path matches no SQL file |
| [SQLSRC005](#sqlsrc005) | Folder has no SQL file |
| [SQLSRC006](#sqlsrc006) | Attribute value is not valid |
| [SQLSRC007](#sqlsrc007) | Type has a member named Sql |
| [SQLSRC008](#sqlsrc008) | Query name is used in two files |
| [SQLSRC009](#sqlsrc009) | Query is named like its containing type |
| [SQLSRC011](#sqlsrc011) | SqlSourceDialect is not valid |
| [SQLSRC012](#sqlsrc012) | Language version is not supported |
| [SQLSRC013](#sqlsrc013) | SQL file paths differ only by case |
| [SQLSRC014](#sqlsrc014) | MSBuild setting is not valid |
| [SQLSRC101](#sqlsrc101) | Quote is not closed |
| [SQLSRC102](#sqlsrc102) | Comment is not closed |
| [SQLSRC103](#sqlsrc103) | Query name is not valid |
| [SQLSRC104](#sqlsrc104) | Query name is used twice |
| [SQLSRC105](#sqlsrc105) | File name is not a valid query name |
| [SQLSRC106](#sqlsrc106) | SQL before the first name |
| [SQLSRC107](#sqlsrc107) | Summary before the first name |
| [SQLSRC108](#sqlsrc108) | Marker has no SQL after it |
| [SQLSRC109](#sqlsrc109) | Generator parameter is not known |
| [SQLSRC110](#sqlsrc110) | Generator parameter is missing |
| [SQLSRC111](#sqlsrc111) | Marker value is not valid |
| [SQLSRC112](#sqlsrc112) | Settings conflict |
| [SQLSRC113](#sqlsrc113) | Query has no SQL |
| [SQLSRC114](#sqlsrc114) | Token name is a keyword |
| [SQLSRC115](#sqlsrc115) | Dialect marker is misplaced |
| [SQLSRC116](#sqlsrc116) | Marker is not allowed here |
| [SQLSRC117](#sqlsrc117) | Parameter has no type |
| [SQLSRC118](#sqlsrc118) | Parameter is not declared |
| [SQLSRC119](#sqlsrc119) | Query has no parameters |
| [SQLSRC200](#sqlsrc200) | The tool failed unexpectedly |

Ids below 100 are about the type that carries `[SqlSourceGenerate]`, or about the project.  Ids from 101 are about the contents of a `.sql` file.  Ids from 200 are the errors of the `sqlsource` tool: it prints each with a line under it that starts with `see:` and links to its section here, and exits with the code 1.

`SQLSRC901` is not in this list because it is not a problem.  It is the id under which SqlSource turns off the compiler's warning CS0436 for the types it adds to every project, in a project that sees the internals of another one that uses SqlSource; see [Projects that share internals](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#projects-that-share-internals).

## SQLSRC001

**Type must be partial**

SqlSource adds members through a second declaration of the type, which C# allows only when every declaration is `partial`.  The same holds for each type the type is nested in.

```csharp
[SqlSourceGenerate]
public class UserRepository { }
```

Add `partial` to the type named in the message.

## SQLSRC002

**Type is file-local**

A type declared with the `file` modifier exists only in its own source file, so generated code cannot add to it.  The same holds for a type nested in a file-local type.

```csharp
[SqlSourceGenerate]
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
[SqlSourceGenerate(Path = "Queries/User.sql")] // The file is Queries/Users.sql.
public partial class UserRepository { }
```

Check the spelling, and that the path starts from the folder of this source file and not from the project.  If the file exists, check that the build sees it.  The package registers the `.sql` files under the project's own folder, so a file outside it, such as a linked file, must be listed as an `AdditionalFiles` item.  So must every `.sql` file of a project that sets `SqlSourceIncludeFiles` to `false`.

## SQLSRC005

**Folder has no SQL file**

Without `Path`, the type gets every `.sql` file in the folder of the source file that carries the attribute.  That folder has none.

Add a `.sql` file next to the source file, or set `Path` to the folder or file that holds the queries.  If a file is there, check that the build sees it, as described for [SQLSRC004](#sqlsrc004).

## SQLSRC006

**Attribute value is not valid**

A property of `[SqlSourceGenerate]` has a value it does not take.  The message names the property and the value: a number that is no member of the property's enum, or a word in `Parameters` that is not a generator parameter.

```csharp
[SqlSourceGenerate(SqlLocation = (SqlLocation)5, Parameters = "keep-coments")]
public partial class UserRepository { }
```

Use a member of the enum, and for `Parameters` the words that [SQLSRC109](#sqlsrc109) lists.  `Output` takes `GeneratorOutput.Sql`, `GeneratorOutput.Models` or `GeneratorOutput.CodeGen`.  `InputModelType` and `OutputModelType` take `GeneratorModelType.Record`, `SealedRecord`, `Class` or `SealedClass`; `CollectionType` takes a member of `GeneratorCollectionType`; `MethodLocation` takes a member of `MethodLocation`.  `InputModelSuffix` and `OutputModelSuffix` take characters that can be part of an identifier, and `ModelNamespace` takes a namespace such as `App.Models`; a value that is empty or only white space is not set.  The type gets no members until the value is fixed.

## SQLSRC007

**Type has a member named Sql**

With `SqlLocation.Nested` the queries go in a nested class named `Sql`, and the type already has a member with that name.  The same error is reported when the type itself, or one of its type parameters, is named `Sql`: a nested class cannot share either name.

```csharp
[SqlSourceGenerate]
public partial class UserRepository
{
    private string Sql { get; }
}
```

Rename the member, the type or the type parameter, or set `SqlLocation = SqlLocation.Direct` so that the queries become members of the type itself.

## SQLSRC008

**Query name is used in two files**

Two `.sql` files of one type each have a query with the same name, and a type cannot have two members with one name.  The error is at the second query, and the message names the file that holds the first.  The file that holds the second query produces no members for that type.

Rename one of the queries.

## SQLSRC009

**Query is named like its containing type**

A member cannot have the name of the type that contains it.  With `SqlLocation.Nested` that type is the generated class, so no query can be named `Sql`.  With `SqlLocation.Direct` it is the attributed type, so no query can have that type's name.

```sql
-- name: Sql
SELECT 1;
```

Rename the query.

## SQLSRC011

**SqlSourceDialect is not valid**

`SqlSourceDialect` says which database a project's SQL is written for, so that SqlSource finds its comments and strings by that database's rules.  It is set as an MSBuild property for the project, or as metadata on the `AdditionalFiles` item of a `.sql` file, and one of the two has a value that is not a dialect.  The message quotes the value.

The names are `ansi`, `mssql`, `postgres`, `cockroachdb`, `mysql`, `mariadb`, `sqlite` and `oracle`, in any case.  `sqlserver` and `tsql` also mean `mssql`, `postgresql` also means `postgres`, and `cockroach` also means `cockroachdb`.

After `mysql` or `mariadb` the value may name options, each after a comma, as in `mysql,ansi-quotes`.  The options are `ansi-quotes` and `no-backslash-escapes`, also written `ansi_quotes` and `no_backslash_escapes`.  An option that does not exist, an option after any other dialect, and a comma with nothing before it or after it each make the whole value wrong.

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

The compiler hands a generator only the part of a value before the first `;` or `#`, as for [SQLSRC014](#sqlsrc014).

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

The error is reported at each `[SqlSourceGenerate]` attribute, and no type gets members until it is fixed.  A project that targets a framework older than .NET 8 has an older language version by default; it gets [SQLSRC003](#sqlsrc003) and not this error, because targeting .NET 8 fixes both.

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

## SQLSRC014

**MSBuild setting is not valid**

An MSBuild property of SqlSource, or the metadata of that name on an `AdditionalFiles` item, has a value it does not take.  The message names the setting and the value.  The error has no file and line, because the compiler does not tell a generator where a property or the metadata of an item was set: look in the project file, in `Directory.Build.props` and `Directory.Build.targets`, and at a `-p:` argument of the build command.

```xml
<PropertyGroup>
    <SqlSourceGeneratorParameters>keep-coments</SqlSourceGeneratorParameters>
</PropertyGroup>
```

Correct the value, or remove it to keep the default.  While it is wrong the setting is not set.  In a list of generator parameters the other words still apply, with one exception: `default` beside another parameter makes the whole list wrong, so `default no-token-validation` is reported whole and sets no list.  The same value in several places is reported once, and the metadata of a file that no type claims is not reported.

`SqlSourceOutput` takes `sql`, `models` or `codegen`, in any case and with or without hyphens and spaces.  `SqlSourceInputModelType` and `SqlSourceOutputModelType` take `record`, `sealed record`, `class` or `sealed class`, and `SqlSourceCollectionType` takes `IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `Array`, `List`, `ImmutableArray`, `ImmutableList` or `IImmutableList`, all matched in the same way.  `SqlSourceInputModelSuffix` and `SqlSourceOutputModelSuffix` take characters that can be part of an identifier, and `SqlSourceModelNamespace` takes a namespace such as `App.Models`.  `SqlSourceDatabase` is not checked by the generator.  Where a type's methods go is set by the attribute alone, so there is no property for it.

The compiler hands a generator only the part of a value before the first `;` or `#`.

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

The name ends at `->`, when the marker has one.

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

In a file that has `-- name:` markers, only comments and the markers that describe the whole file may come before the first one: the table of [SQLSRC116](#sqlsrc116) has the list.  SQL there would belong to no query.

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

A marker comes before the SQL it describes.  This one stands after the last SQL of its query, where a reader would take it to belong to the next one.  That usually means it was written above the next `-- name:` marker and was meant for that query.

```sql
-- name: GetUser
SELECT 1;
-- summary: Lists every user.
-- name: ListUsers
SELECT 2;
```

Move the marker below the `-- name:` marker of the query it describes, or delete it.

## SQLSRC109

**Generator parameter is not known**

A `-- generator:` marker holds a word that is not a generator parameter.  The generator parameters are `keep-comments`, `no-token-validation`, `sort-input`, `sort-output`, `no-table-models`, `async-method-suffix` and `default`.  `token-validation` is gone because omitting `no-token-validation` says it.  `token-ignore` was a generator parameter and is a marker now: `-- token-ignore: name`, inside the query.

```sql
-- generator: keep-comment
```

Correct the parameter.  A dialect is not a generator parameter: it has a marker of its own, `-- dialect: name`.

## SQLSRC110

**Generator parameter is missing**

A `-- generator:` marker has nothing after the colon.

Add a generator parameter, or delete the line.

## SQLSRC111

**Marker value is not valid**

A generator parameter has a value it does not take; what follows a name in a `-- name:` marker is not a shape; a `-- dialect:` marker does not name a dialect; a marker that holds a setting, such as `-- output:`, `-- database:`, `-- input-model-type:` or `-- collection-type:`, has a value that setting does not take; or a `-- token:`, `-- token-ignore:` or `-- param:` marker does not hold what it needs.

- No generator parameter takes a value: `keep-comments=1` is this error.
- What follows the name in a `-- name:` marker must be `->` and one of `many`, `one`, `one-optional`, `none` and `rowcount`, as in `-- name: GetUser -> one`.
- `-- dialect:` needs the name of a dialect, as in `-- dialect: postgres`, with any options after it, as in `-- dialect: mysql, ansi-quotes`.  The names and the options are those of [SQLSRC011](#sqlsrc011).  The value is the rest of the line, so nothing else may follow it.

```sql
-- generator: keep-comments=true
-- dialect: pgsql
-- dialect: mysql keep-comments
```

- `-- output:` takes `sql`, `models` or `codegen`.  `-- database:` takes one word of letters, digits, `-`, `_` and `.`.
- `-- input-model-type:` and `-- output-model-type:` take `record`, `sealed record`, `class` or `sealed class`.  `-- collection-type:` takes `IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `Array`, `List`, `ImmutableArray`, `ImmutableList` or `IImmutableList`.
- `-- input-model-suffix:` and `-- output-model-suffix:` take characters that can be part of an identifier, and `-- model-namespace:` takes a namespace such as `App.Models`.
- `-- input-model:` and `-- output-model:` take the name of a type, or its full name with its namespace: `UserRow` or `App.Models.UserRow`.  Neither is a reserved keyword, and no generic type is named.

A `-- token-ignore:` marker holds one name, a C# identifier, and nothing else.

A `-- token:` marker holds exactly one token with a default, `{{name:default}}`, and nothing else; and a quote or a block comment inside the default must close there.

A `-- param:` marker starts with the parameter as the SQL writes it, `@name`, alone or followed by a space.

Add the missing value, correct the one that is wrong, or remove the one that does not belong.

## SQLSRC112

**Settings conflict**

Two settings contradict each other.  The error is at the second.

- `default` beside another generator parameter in one scope: `default` is the empty list, and a list that holds something is not empty.  A scope is the lines before the first `-- name:` marker, or one query.
- Two `-- dialect:` markers name different dialects, or one dialect with different options.  A file has one dialect.
- Two defaults for one token of a query that differ: two `{{name:default}}` in its SQL, two `-- token:` markers, or one of each.
- Two `-- param:` markers for one parameter that give different types or different nullability.
- Two `-- output:` markers that give different values, or two `-- database:` markers that name different databases (`billing` and `Billing` are different) in one scope.
- Two markers of any other setting of the models and the collection type that give different values in one scope: two `-- input-model:` markers that name different types, two `-- model-namespace:` markers, and so on.

Remove one of the two.  A query's list replaces the one before the first `-- name:` marker, and that is not a conflict.  The same dialect given twice with the same options is not a conflict either.

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

The same holds for the name in a `-- token:` marker.

Rename the token.  If the braces are literal text and not a token, add `-- token-ignore: class` to the query.

## SQLSRC115

**Dialect marker is misplaced**

A `-- dialect:` marker sets the dialect of a whole file, and it changes how the text after it is read.  So it must come before the file's first `-- name:` marker and before the file's first SQL.  This one is inside a named query, or after SQL.

```sql
-- name: GetUser
-- dialect: mysql
SELECT 1;
```

A marker after the last SQL of its query is reported twice: as this error, and as [SQLSRC108](#sqlsrc108).

Move the marker to the top of the file.  Comments may come before it, such as a licence header.  In a file with no `-- name:` marker, which is one query, put it above the query's SQL.

```sql
-- dialect: mysql

-- name: GetUser
SELECT 1;
```

A file cannot mix dialects.  Put the queries for another database in a file of their own.

## SQLSRC116

**Marker is not allowed here**

A marker stands in the wrong part of its file.  Some markers describe one query and go inside it, after its `-- name:` line; some describe the whole file and go before the first `-- name:` line.  The message says which this one is.

```sql
-- token: {{filter:AND deleted_at IS NULL}}

-- name: ListUsers
SELECT id FROM users WHERE 1 = 1 {{filter}};
```

Move the marker to where the message says.  A file with no `-- name:` line is one query, and takes every marker.

| Marker | Before the first `-- name:` line | Inside a query |
|----|----|----|
| `-- name:` | no | starts one |
| `-- summary:` | no | yes |
| `-- dialect:` | yes | no |
| `-- generator:` | yes | yes |
| `-- param:` | no | yes |
| `-- token:` | no | yes |
| `-- token-ignore:` | no | yes |
| `-- database:` | yes | yes |
| `-- output:` | yes | yes |
| `-- input-model-suffix:`, `-- output-model-suffix:` | yes | no |
| `-- model-namespace:` | yes | no |
| `-- input-model-type:`, `-- output-model-type:` | yes | yes |
| `-- input-model:`, `-- output-model:` | no | yes |
| `-- collection-type:` | yes | yes |

The table is this error's, with two exceptions.  A `-- summary:` before the first `-- name:` marker is [SQLSRC107](#sqlsrc107), and a `-- dialect:` inside a query or after SQL is [SQLSRC115](#sqlsrc115).

## SQLSRC117

**Parameter has no type**

A `-- param:` marker names a parameter that is not in the SQL of its query.  Such a parameter reaches the query only inside the text of a token, so nothing but the marker can say what type it has.

```sql
-- name: ListUsers
-- param: @page
SELECT id FROM users {{paging:LIMIT 20 OFFSET @page}};
```

Give the type, in the database's own words: `-- param: @page int`.

## SQLSRC118

**Parameter is not declared**

A parameter appears in the default of a token, in the SQL or in a `-- token:` marker, and nowhere else in the query.  A default is a sample, and a type taken from a sample alone would be a guess.

```sql
-- name: ListUsers
SELECT id FROM users {{paging:LIMIT 20 OFFSET @page}};
```

Declare the parameter with its type: `-- param: @page int`.

## SQLSRC119

**Query has no parameters**

An `-- input-model:` marker names the type of a query's parameters, and this query has none: no `@name` in its SQL and no `-- param:` marker.  Under the `mssql` dialect a name that the query declares with `DECLARE` is a local variable, and is not a parameter.

```sql
-- name: CountUsers
-- input-model: CountArgs
SELECT COUNT(*) FROM users;
```

Remove the marker.

## SQLSRC200

**The tool failed unexpectedly**

`sqlsource` stopped on an exception that it has no error of its own for.  The message holds the type of the exception and its message.

```console
$ dotnet sqlsource describe /srv/locked
sqlsource : error SQLSRC200: sqlsource failed unexpectedly: System.UnauthorizedAccessException: Access to the path '/srv/locked' is denied.
```

When the message names something of your machine, as this one does, fix that.  Otherwise it is a bug in the tool: set the environment variable `SQLSOURCE_DEBUG` to any value, run the command again, and [report it](https://github.com/mbcrawfo/SqlSource/issues) with the lines that start with `trace:`.
