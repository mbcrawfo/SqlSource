# Diagnostics

Every problem SqlSource finds is an error, and none can be turned off or made a warning, except two warnings about a sidecar that a project can configure, [SQLSRC240](#sqlsrc240) and [SQLSRC245](#sqlsrc245), and one message of information about the version of Npgsql, [SQLSRC248](#sqlsrc248).  The generator reports its errors and warnings in the build's output.  Its information is not in the output of `dotnet build`: an IDE that lists information shows it, and the build log has it at the detailed verbosity, as [SQLSRC248](#sqlsrc248) says.  An error in a `.sql` file is reported at its line and column in that file.  An error in how a `.sql` file is written, an id from 101 to 119, costs the file its members until it is fixed.  An error about describing a query or about its sidecar, such as [SQLSRC209](#sqlsrc209) or [SQLSRC241](#sqlsrc241), leaves them in place: the query's constant or method is still generated.  An error in a sidecar is reported at its line and column in the sidecar.  The `sqlsource` command-line tool prints its errors itself, in the format of a build error, and ends each with a `see:` line that links to its section here; the samples on this page leave that line out.

**Project and attribute**

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

**`.sql` files**

| Id | Title |
|----|----|
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

**The tool**

| Id | Title |
|----|----|
| [SQLSRC200](#sqlsrc200) | The tool failed unexpectedly |
| [SQLSRC201](#sqlsrc201) | No project or solution found |
| [SQLSRC202](#sqlsrc202) | More than one project or solution found |
| [SQLSRC203](#sqlsrc203) | Path is not a project or a solution |
| [SQLSRC204](#sqlsrc204) | Project does not use SqlSource |
| [SQLSRC205](#sqlsrc205) | Project could not be evaluated |
| [SQLSRC206](#sqlsrc206) | Project manifest cannot be read |
| [SQLSRC207](#sqlsrc207) | Project is not in the run |
| [SQLSRC208](#sqlsrc208) | Attribute argument is not a literal |
| [SQLSRC209](#sqlsrc209) | Output needs a dialect that can be described |
| [SQLSRC210](#sqlsrc210) | Token has no default |
| [SQLSRC211](#sqlsrc211) | Database has two dialects |
| [SQLSRC212](#sqlsrc212) | File is not in the run |
| [SQLSRC213](#sqlsrc213) | Database has no connection |
| [SQLSRC214](#sqlsrc214) | Two databases share a connection variable |
| [SQLSRC215](#sqlsrc215) | Connection names no database |
| [SQLSRC216](#sqlsrc216) | No describer for the dialect |
| [SQLSRC217](#sqlsrc217) | Sidecar was not written |
| [SQLSRC218](#sqlsrc218) | File could not be changed |
| [SQLSRC219](#sqlsrc219) | Sidecar is out of date |
| [SQLSRC220](#sqlsrc220) | Project was not restored |
| [SQLSRC221](#sqlsrc221) | Sidecar was written by a newer tool |
| [SQLSRC222](#sqlsrc222) | Directory cannot be read |
| [SQLSRC223](#sqlsrc223) | Solution cannot be read |

**PostgreSQL**

| Id | Title |
|----|----|
| [SQLSRC224](#sqlsrc224) | Database could not be opened |
| [SQLSRC225](#sqlsrc225) | Server rejected the query |
| [SQLSRC226](#sqlsrc226) | Parameter type could not be determined |
| [SQLSRC227](#sqlsrc227) | Parameter's first use does not fix its type |
| [SQLSRC228](#sqlsrc228) | Declared type is not valid |
| [SQLSRC229](#sqlsrc229) | Column type is not supported |
| [SQLSRC230](#sqlsrc230) | Query holds more than one statement |
| [SQLSRC231](#sqlsrc231) | Parameters as Npgsql reads them differ |
| [SQLSRC232](#sqlsrc232) | Type cannot be bound by Npgsql |
| [SQLSRC233](#sqlsrc233) | Statement cannot be described |
| [SQLSRC234](#sqlsrc234) | Query writes a positional parameter |
| [SQLSRC235](#sqlsrc235) | Server version is not supported |
| [SQLSRC236](#sqlsrc236) | Connection does not keep a session |

**The sidecar of a `.sql` file**

| Id | Title |
|----|----|
| [SQLSRC237](#sqlsrc237) | Sidecar cannot be read |
| [SQLSRC238](#sqlsrc238) | Sidecar has a newer format |
| [SQLSRC239](#sqlsrc239) | Sidecar has an older format |
| [SQLSRC240](#sqlsrc240) | Sidecar was written by another version of the tool |
| [SQLSRC241](#sqlsrc241) | Query has no entry in the sidecar |
| [SQLSRC242](#sqlsrc242) | Entry is of another database |
| [SQLSRC243](#sqlsrc243) | No type map for the dialect |
| [SQLSRC244](#sqlsrc244) | Entry is stale |
| [SQLSRC245](#sqlsrc245) | Entry names no query |

**The C# types of a PostgreSQL query**

| Id | Title |
|----|----|
| [SQLSRC246](#sqlsrc246) | Type has no C# type |
| [SQLSRC247](#sqlsrc247) | Npgsql is older than 8.0 |
| [SQLSRC248](#sqlsrc248) | Npgsql is newer than SqlSource knows |
| [SQLSRC249](#sqlsrc249) | Type needs Npgsql |

**The type override**

| Id | Title |
|----|----|
| [SQLSRC250](#sqlsrc250) | Mapped type is not found |
| [SQLSRC251](#sqlsrc251) | Mapped type is not accessible |
| [SQLSRC252](#sqlsrc252) | Mapped type cannot be a property's type |

**The models of a query**

| Id | Title |
|----|----|
| [SQLSRC253](#sqlsrc253) | Column has no name |
| [SQLSRC254](#sqlsrc254) | Name is not an identifier after PascalCase |
| [SQLSRC255](#sqlsrc255) | Two names give one property name |
| [SQLSRC256](#sqlsrc256) | Property name is taken |
| [SQLSRC259](#sqlsrc259) | Output model on a query without rows |

Ids below 100 are about the type that carries `[SqlSourceGenerate]`, or about the project.  Ids from 101 to 119 are about the contents of a `.sql` file.  Ids from 200 to 236 are the errors of the `sqlsource` tool: it prints each with a line under it that starts with `see:` and links to its section here, and exits with the code 1, or with 2 when it reported nothing but `SQLSRC219`.  The build reports two of them as well, at the same places: [SQLSRC209](#sqlsrc209) and [SQLSRC210](#sqlsrc210), for a query that must be described and cannot be.  The tool also reports `SQLSRC011`, `SQLSRC014` and the errors of a `.sql` file, the ids from 101 to 119, under the generator's ids: what is wrong is the same, and so is the fix.  It gives `SQLSRC011` and `SQLSRC014` the project file as their place, and reports them for the dialect, `SqlSourceOutput` and `SqlSourceDatabase` alone.

The ids from `SQLSRC224` to `SQLSRC236` are the errors of describing a query against PostgreSQL, which this version of the tool does for the `postgres` dialect.  It reports [SQLSRC216](#sqlsrc216) for a query of a dialect it cannot describe yet.

The ids from `SQLSRC237` to `SQLSRC245` are reported by the build, about the sidecar of a `.sql` file that a type uses.  A query whose output is `models` or `codegen` needs a current entry in its file's sidecar, which `dotnet sqlsource describe` writes; one whose output is `sql` needs none.  The default output is `codegen`, so under the `postgres` dialect every query needs an entry unless the project, the type or the file sets the output to `sql`.  A query that no entry can be written for gets [SQLSRC209](#sqlsrc209), [SQLSRC210](#sqlsrc210) or [SQLSRC243](#sqlsrc243) instead.  A sidecar that exists is checked even when no query of its file needs an entry.  While a `.sql` file has an error of its own, an id from 101 to 119, it has no queries to check, so the build checks only the sidecar itself, [SQLSRC237](#sqlsrc237) to [SQLSRC240](#sqlsrc240), and reports no entry of it as one that names no query.

The ids `SQLSRC246` and `SQLSRC249` are reported by the build, about the C# types of a query whose output is `models` or `codegen` under the `postgres` dialect and whose entry in its sidecar is current.  Each column and parameter of the entry gets the C# type that Npgsql reads it as, which can depend on the version of Npgsql that the project references, or the type the type override maps its database type to.  Each is reported at the query's name, once for a query: for its first column or parameter that has no C# type, parameters before columns.

The ids `SQLSRC247` and `SQLSRC248` are reported by the build, about the version of the `Npgsql` package that the project references, directly or through another package.  The C# types of a PostgreSQL query depend on it.  Each is reported once for the project, with no file and line, and only when a query needs an entry in its sidecar under the `postgres` dialect.

The ids from `SQLSRC250` to `SQLSRC252` are about a C# type that the type override names.  Each is reported at a query that has a column or a parameter of the database type the entry maps, and names the column or parameter, the entry and the place that wrote it.  An entry that no query uses is never an error, so a project can set its mappings before it has the queries.

The ids from `SQLSRC253` to `SQLSRC256`, and `SQLSRC259`, are about the models of a query whose entry in its sidecar is current and whose columns and parameters all have a C# type.  Each property of a model is named by the PascalCase of its column or parameter: `created_at` gives `CreatedAt`.  Each error is reported at the query's name, except `SQLSRC259`, which is at its marker, and a query gets every error it has, and no models until they are fixed.

`SQLSRC901` is not in this list because it is not a problem.  It is the id under which SqlSource turns off the compiler's warning CS0436 for the types it adds to every project, in a project that sees the internals of another one that uses SqlSource; see [Projects that share internals](generator.md#projects-that-share-internals).

## SQLSRC001

**Type must be partial**

SqlSource adds members through a second declaration of the type, which C# allows only when every declaration is `partial`.  The same holds for each type the type is nested in.

```csharp
[SqlSourceGenerate]
public class UserRepository { }
```

Add `partial` to the type named in the message.

See: [The attribute](generator.md#attribute)

## SQLSRC002

**Type is file-local**

A type declared with the `file` modifier exists only in its own source file, so generated code cannot add to it.  The same holds for a type nested in a file-local type.

```csharp
[SqlSourceGenerate]
file partial class UserRepository { }
```

Remove the `file` modifier, or move the attribute to a type that is not file-local.

See: [The attribute](generator.md#attribute)

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

See: [Path](settings.md#setting-path)

## SQLSRC005

**Folder has no SQL file**

Without `Path`, the type gets every `.sql` file in the folder of the source file that carries the attribute.  That folder has none.

Add a `.sql` file next to the source file, or set `Path` to the folder or file that holds the queries.  If a file is there, check that the build sees it, as described for [SQLSRC004](#sqlsrc004).

See: [Path](settings.md#setting-path)

## SQLSRC006

**Attribute value is not valid**

A property of `[SqlSourceGenerate]` has a value it does not take.  The message names the property and the value: a number that is no member of the property's enum, or a word in `Parameters` that is not a generator parameter.

```csharp
[SqlSourceGenerate(SqlLocation = (SqlLocation)5, Parameters = "keep-coments")]
public partial class UserRepository { }
```

Use a member of the enum, and for `Parameters` the words that [SQLSRC109](#sqlsrc109) lists.  `Output` takes `GeneratorOutput.Sql`, `GeneratorOutput.Models` or `GeneratorOutput.CodeGen`.  `InputModelType` and `OutputModelType` take `GeneratorModelType.Record`, `SealedRecord`, `Class` or `SealedClass`; `CollectionType` takes a member of `GeneratorCollectionType`; `MethodLocation` takes a member of `MethodLocation`.  `InputModelSuffix` and `OutputModelSuffix` take characters that can be part of an identifier, and `ModelNamespace` takes a namespace such as `App.Models`; a value that is empty or only white space is not set.  The type gets no members until the value is fixed.

See: [All settings](settings.md#all-settings)

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

See: [Where the members go](generator.md#locations)

## SQLSRC008

**Query name is used in two files**

Two `.sql` files of one type each have a query with the same name, and a type cannot have two members with one name.  The error is at the second query, and the message names the file that holds the first.  The file that holds the second query produces no members for that type.

Rename one of the queries.

See: [Which files belong to a type](generator.md#files-of-a-type)

## SQLSRC009

**Query is named like its containing type**

A member cannot have the name of the type that contains it.  With `SqlLocation.Nested` that type is the generated class, so no query can be named `Sql`.  With `SqlLocation.Direct` it is the attributed type, so no query can have that type's name.

```sql
-- name: Sql
SELECT 1;
```

Rename the query.

See: [Where the members go](generator.md#locations)

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

The error has no file and line, because the compiler does not tell a generator where a property or the metadata of an item was set: look in the project file, in `Directory.Build.props`, and at a `-p:` argument of the build command.  It is reported once for each wrong value, however many files have it.  A wrong property is always reported; wrong metadata is reported only for a `.sql` file that a type uses.

Correct the name, or remove the setting to get the default, `ansi`.  While a value is wrong the files it covers are read as `ansi`; a file whose own metadata is wrong does not fall back to the project's property.

The compiler hands a generator only the part of a value before the first `;` or `#`, as for [SQLSRC014](#sqlsrc014).

See: [Dialect](settings.md#setting-dialect)

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

See: [Path](settings.md#setting-path)

## SQLSRC014

**MSBuild setting is not valid**

An MSBuild property of SqlSource, or the metadata of that name on an `AdditionalFiles` item, has a value it does not take.  The message names the setting and the value.  The error has no file and line, because the compiler does not tell a generator where a property or the metadata of an item was set: look in the project file, in `Directory.Build.props` and `Directory.Build.targets`, and at a `-p:` argument of the build command.

```xml
<PropertyGroup>
    <SqlSourceGeneratorParameters>keep-coments</SqlSourceGeneratorParameters>
</PropertyGroup>
```

Correct the value, or remove it to keep the default.  While it is wrong the setting is not set.  In a list of generator parameters the other words still apply, with one exception: `default` beside another parameter makes the whole list wrong, so `default no-token-validation` is reported whole and sets no list.  The same value in several places is reported once, and the metadata of a file that no type claims is not reported.

`SqlSourceOutput` takes `sql`, `models` or `codegen`, in any case and with or without hyphens and spaces.  `SqlSourceInputModelType` and `SqlSourceOutputModelType` take `record`, `sealed record`, `class` or `sealed class`, and `SqlSourceCollectionType` takes `IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `Array`, `List`, `ImmutableArray`, `ImmutableList` or `IImmutableList`, all matched in the same way.  `SqlSourceInputModelSuffix` and `SqlSourceOutputModelSuffix` take characters that can be part of an identifier, and `SqlSourceModelNamespace` takes a namespace such as `App.Models`.  `SqlSourceDatabase` takes one word of letters, digits, `-`, `_` and `.`.  The generator does not read it, so a build does not report it; the `sqlsource` tool does.  Where a type's methods go is set by the attribute alone, so there is no property for it.

`SqlSourceTypeOverrides` takes entries `key=type`, separated by spaces or line breaks, and each entry that is not valid is reported on its own, while the others still apply.  A space or a line break inside `<>` or `[]` is part of the entry, so a `<` or a `[` that is never closed makes the rest of the value one entry, which is reported whole, and the entries after it do not apply:

```xml
<PropertyGroup>
    <SqlSourceTypeOverrides>timestamp with time zone=DateTimeOffset user_status=MyApp.UserStatus int4=int?</SqlSourceTypeOverrides>
</PropertyGroup>
```

- **No space around `=`.**  A space separates entries, so `numeric = double` is three entries, and `timestamp with time zone=...` is four.
- **The key is a type's one-word name**, `timestamptz` and not `timestamp with time zone`, `float8` and not `double precision`; every built-in type has one.  A type outside `pg_catalog` is written with its schema, `public.user_status`: a name without one must be a type SqlSource knows, so `user_status` and the misspelling `timestampz` are reported.
- **The C# type is a keyword or a full name from its namespace**, `System.DateTimeOffset` and not `DateTimeOffset`, with no `?` at the end: `int4=int` and not `int4=int?`.  Write a generic's `<` and `>` as `&lt;` and `&gt;` in the project file.

The rules are in [type overrides](settings.md#setting-type-overrides).

The compiler hands a generator only the part of a value before the first `;` or `#`.

See: [A value that is wrong](settings.md#a-value-that-is-wrong)

## SQLSRC101

**Quote is not closed**

A string or a quoted identifier starts and never ends.  The error is at the opening quote or bracket.

```sql error
SELECT 'unfinished FROM users;
```

Close the quote.  If the SQL is valid for your database, SqlSource is reading it by the rules of another one: `'it\'s'` is one string in MySQL and an unclosed one elsewhere.  Set the dialect of the file.  A few constructs are read differently from the database whatever the dialect: see [what is still read differently](dialects.md#still-read-differently).

See: [What each dialect changes](dialects.md#what-a-dialect-changes)

## SQLSRC102

**Comment is not closed**

A block comment or a hint starts with `/*` and never ends.  The error is at the `/*`.

```sql error
/* outer /* inner */
SELECT 1;
```

Close the comment.  Whether a `/*` inside a comment needs its own `*/` depends on the dialect: it does in the default dialect, in SQL Server and in PostgreSQL, and it does not in MySQL, MariaDB, SQLite and Oracle.  If the SQL is valid for your database, set the dialect of the file.

SQLite accepts a comment that is still open at the end of the file.  SqlSource does not, in any dialect, because the comment would take every later query of the file with it.

See: [What each dialect changes](dialects.md#what-a-dialect-changes)

## SQLSRC103

**Query name is not valid**

The value of a `-- name:` marker becomes the name of a C# member, so it must be a C# identifier and must not be a reserved keyword such as `class`.

```sql error
-- name: get-user
SELECT 1;
```

Rename the query, for example to `GetUser`.

The name ends at `->`, when the marker has one.

See: [Queries and names](sql-files.md#queries-and-names)

## SQLSRC104

**Query name is used twice**

Two `-- name:` markers in one file have the same name.  The error is at the second.

Rename one of the queries.

See: [Queries and names](sql-files.md#queries-and-names)

## SQLSRC105

**File name is not a valid query name**

A file without a `-- name:` marker is one query, named after the file without its extension.  This file's name cannot be the name of a C# member.

```text
001_init.sql
get-user.sql
```

Add a `-- name:` marker at the top of the file, or rename the file.  If the file is not meant to be a query of this type, move it out of the type's folder or point the type's `Path` somewhere else.

See: [A file with no `-- name:`](sql-files.md#a-file-with-no-name)

## SQLSRC106

**SQL before the first name**

In a file that has `-- name:` markers, only comments and the markers that describe the whole file may come before the first one: the table of [SQLSRC116](#sqlsrc116) has the list.  SQL there would belong to no query.

```sql error
SET search_path TO app;

-- name: GetUser
SELECT 1;
```

Move the SQL below a `-- name:` marker, or delete it.

See: [The file header](sql-files.md#the-file-header)

## SQLSRC107

**Summary before the first name**

A `-- summary:` marker describes the query it is in.  One in the file header, before the first `-- name:` line, is in no query.

```sql error
-- summary: Loads one user.
-- name: GetUser
SELECT 1;
```

Move the summary below the `-- name:` marker.

See: [Summaries](sql-files.md#summaries)

## SQLSRC108

**Marker has no SQL after it**

A marker comes before the SQL it describes.  This one stands after the last SQL of its query, where a reader would take it to belong to the next one.  That usually means it was written above the next `-- name:` marker and was meant for that query.

```sql error
-- name: GetUser
SELECT 1;
-- summary: Lists every user.
-- name: ListUsers
SELECT 2;
```

Move the marker below the `-- name:` marker of the query it describes, or delete it.

See: [Markers in the wrong place](sql-files.md#markers-in-the-wrong-place)

## SQLSRC109

**Generator parameter is not known**

A `-- generator:` marker holds a word that is not a generator parameter.  The generator parameters are `keep-comments`, `no-token-validation`, `sort-input`, `sort-output`, `no-table-models`, `async-method-suffix` and `default`.  If you used `token-validation` or `token-ignore` as generator parameters in an earlier preview: leave out the first, since validation is the default, and write the second as a marker inside the query, `-- token-ignore: name`.

```sql error
-- generator: keep-comment
```

Correct the parameter.  A dialect is not a generator parameter: it has a marker of its own, `-- dialect: name`.

See: [Generator parameters](settings.md#setting-generator-parameters)

## SQLSRC110

**Generator parameter is missing**

A `-- generator:` marker has nothing after the colon.

Add a generator parameter, or delete the line.

See: [Generator parameters](settings.md#setting-generator-parameters)

## SQLSRC111

**Marker value is not valid**

A generator parameter has a value it does not take; what follows a name in a `-- name:` marker is not a shape; a `-- dialect:` marker does not name a dialect; a marker that holds a setting, such as `-- output:`, `-- database:`, `-- input-model-type:`, `-- collection-type:` or `-- type-override:`, has a value that setting does not take; or a `-- token:`, `-- token-ignore:` or `-- param:` marker does not hold what it needs.

- No generator parameter takes a value: `keep-comments=1` is this error.
- What follows the name in a `-- name:` marker must be `->` and one of `many`, `one`, `one-optional`, `none` and `rowcount`, as in `-- name: GetUser -> one`.
- `-- dialect:` needs the name of a dialect, as in `-- dialect: postgres`, with any options after it, as in `-- dialect: mysql, ansi-quotes`.  The names and the options are those of [SQLSRC011](#sqlsrc011).  The value is the rest of the line, so nothing else may follow it.

```sql error
-- generator: keep-comments=true
-- dialect: pgsql
-- dialect: mysql keep-comments
```

- `-- output:` takes `sql`, `models` or `codegen`.  `-- database:` takes one word of letters, digits, `-`, `_` and `.`.
- `-- input-model-type:` and `-- output-model-type:` take `record`, `sealed record`, `class` or `sealed class`.  `-- collection-type:` takes `IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `Array`, `List`, `ImmutableArray`, `ImmutableList` or `IImmutableList`.
- `-- input-model-suffix:` and `-- output-model-suffix:` take characters that can be part of an identifier, and `-- model-namespace:` takes a namespace such as `App.Models`.
- `-- input-model:` and `-- output-model:` take the name of a type, or its full name with its namespace: `UserRow` or `App.Models.UserRow`.  Neither is a reserved keyword, and no generic type is named.
- `-- type-override:` takes one entry, `key = type` or `key = default`.  The key is a type's one-word name, `timestamptz` and not `timestamp with time zone`, and a type outside `pg_catalog` is written with its schema, `public.user_status`.  The type is a keyword or a full name from its namespace, `System.DateTimeOffset`, with no `?` at the end.  See [type overrides](settings.md#setting-type-overrides).

A `-- token-ignore:` marker holds one name, a C# identifier, and nothing else.

A `-- token:` marker holds exactly one token with a default, `{{name:default}}`, and nothing else; and a quote or a block comment inside the default must close there.

A `-- param:` marker starts with the parameter as the SQL writes it, `@name`, alone or followed by a space.

Add the missing value, correct the one that is wrong, or remove the one that does not belong.

See: [Marker index](sql-files.md#marker-index)

## SQLSRC112

**Settings conflict**

Two settings contradict each other.  The error is at the second.

- `default` beside another generator parameter in the file header, or inside one query: `default` is the empty list, and a list that holds something is not empty.
- Two `-- dialect:` markers name different dialects, or one dialect with different options.  A file has one dialect.
- Two defaults for one token of a query that differ: two `{{name:default}}` in its SQL, two `-- token:` markers, or one of each.
- Two `-- param:` markers for one parameter that give different types or different nullability.  `not null` counts as different from saying nothing: `-- param: @a int` and `-- param: @a int not null` conflict.  Write both the same.
- Two `-- output:` markers that give different values, or two `-- database:` markers that name different databases (`billing` and `Billing` are different), in the file header or inside one query.
- Two markers of any other setting of the models and the collection type that give different values in the file header or inside one query: two `-- input-model:` markers that name different types, two `-- model-namespace:` markers, and so on.
- Two `-- type-override:` markers that give one database type different C# types, in the file header or inside one query.  The key is compared as [type overrides](settings.md#setting-type-overrides) compares it, so `integer` and `int4` are one type, and the C# types by how they are named, so `int` and `System.Int32` are two.

```sql error
-- name: ListUsers
-- output: sql
-- output: models
SELECT id, name FROM users;
```

Remove one of the two.  A query's `-- generator:` list replaces the file header's list, and a query's `-- type-override:` for a type replaces the file header's for that type: neither is a conflict.  The same dialect given twice with the same options is not a conflict either.

See: [A value that is wrong](settings.md#a-value-that-is-wrong)

## SQLSRC113

**Query has no SQL**

A query, or a whole file without `-- name:` markers, holds nothing but whitespace, comments and markers.  A file that cannot be read is reported the same way, at its first line.

```sql error
-- name: GetUser
-- TODO: write this query
```

Write the SQL, or delete the query.

See: [Queries and names](sql-files.md#queries-and-names)

## SQLSRC114

**Token name is a keyword**

A token `{{name}}` becomes a parameter of a generated method, so its name must not be a reserved C# keyword.

```sql error
SELECT * FROM {{class}};
```

The same holds for the name in a `-- token:` marker.

Rename the token.  If the braces are literal text and not a token, add `-- token-ignore: class` to the query.

See: [Tokens](parameters-and-tokens.md#tokens)

## SQLSRC115

**Dialect marker is misplaced**

A `-- dialect:` marker sets the dialect of a whole file, and it changes how the text after it is read.  So it must stand in the file header, before the file's first `-- name:` line and before its first SQL.  This one is inside a named query, or after SQL.

```sql error
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

See: [Markers in the wrong place](sql-files.md#markers-in-the-wrong-place)

## SQLSRC116

**Marker is not allowed here**

A marker stands in the wrong part of its file.  Some markers describe one query and go inside it, after its `-- name:` line; some describe the whole file and go before the first `-- name:` line.  The message says which this one is.

```sql error
-- token: {{filter:AND deleted_at IS NULL}}

-- name: ListUsers
SELECT id FROM users WHERE 1 = 1 {{filter}};
```

Move the marker to where the message says.  A file with no `-- name:` line is one query, and takes every marker.

| Marker | File header | Inside a query |
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

The table is this error's, with two exceptions.  A `-- summary:` in the file header is [SQLSRC107](#sqlsrc107), and a `-- dialect:` inside a query or after SQL is [SQLSRC115](#sqlsrc115).

See: [Markers in the wrong place](sql-files.md#markers-in-the-wrong-place)

## SQLSRC117

**Parameter has no type**

A `-- param:` marker without a type names a parameter that does not stand as a parameter in the query's SQL: it appears only in a token's default, or, under `mssql`, only where SqlSource reads it as a variable declared with `DECLARE`, or nowhere, as when its name is misspelt.  Nothing but the marker can then say its type.

```sql error
-- name: ListUsers
-- param: @page
SELECT id FROM users {{paging:LIMIT 20 OFFSET @page}};
```

```sql error
-- dialect: mssql

-- name: FromStart
-- param: @n
DECLARE @n int = 1;
SELECT * FROM events WHERE id > @n;
```

Give the type, in the database's own words: `-- param: @page int`.  Under `mssql`, remove the marker when the name is a variable that the query declares, and give the type when it is a parameter that SqlSource took for a variable: see [variables declared with `DECLARE`](parameters-and-tokens.md#declared-variables).  Correct a misspelt name.

See: [Giving a parameter a type: `-- param:`](parameters-and-tokens.md#param-marker)

## SQLSRC118

**Parameter is not declared**

A parameter appears in the default of a token, in the SQL or in a `-- token:` marker, and nowhere else in the query.  A default is a sample, and a type taken from a sample alone would be a guess.

```sql error
-- name: ListUsers
SELECT id FROM users {{paging:LIMIT 20 OFFSET @page}};
```

Declare the parameter with its type: `-- param: @page int`.

See: [Defaults](parameters-and-tokens.md#token-defaults)

## SQLSRC119

**Query has no parameters**

An `-- input-model:` marker names the type of a query's parameters, and this query has none: no `@name` in its SQL and no `-- param:` marker.  Under the `mssql` dialect a name that the query declares with `DECLARE` is a local variable, and is not a parameter.

```sql error
-- name: CountUsers
-- input-model: CountArgs
SELECT COUNT(*) FROM users;
```

Remove the marker.

See: [Name of one query's model](settings.md#setting-model-name)

## SQLSRC200

**The tool failed unexpectedly**

`sqlsource` stopped on an exception that it has no error of its own for.  The message holds the type of the exception and its message.

```console
$ dotnet sqlsource describe
sqlsource : error SQLSRC200: sqlsource failed unexpectedly: System.IO.FileNotFoundException: Unable to find the specified file.
```

Here the current folder was deleted while a terminal was still in it.  When the message names such a condition of your machine, mend that.  Otherwise it is a bug in the tool: set the environment variable `SQLSOURCE_DEBUG` to any value, run the command again, and [report it](https://github.com/mbcrawfo/SqlSource/issues) with the lines that start with `trace:`.

See: [Environment variables](tool.md#environment-variables)

## SQLSRC201

**No project or solution found**

`sqlsource describe` was given a folder, or none and so the current one, that holds no `.sln`, `.slnx` or `.csproj` file.  The tool looks in that folder alone, not in the folders below it.

Run the command from the folder of the project or the solution, or name the one to run on:

```console
$ dotnet sqlsource describe src/App/App.csproj
```

See: [Arguments](tool.md#arguments)

## SQLSRC202

**More than one project or solution found**

`sqlsource describe` was given a folder, or none and so the current one, that holds more than one `.sln`, `.slnx` or `.csproj` file.  The tool does not choose between them, as `dotnet build` does not.

Name the one to run on:

```console
$ dotnet sqlsource describe App.slnx
```

See: [Arguments](tool.md#arguments)

## SQLSRC203

**Path is not a project or a solution**

The path given to `sqlsource describe` does not exist, or is a file that is not a `.sln`, `.slnx` or `.csproj` file.  A solution filter, `.slnf`, and a project of another language are not read.  The message holds the full path the tool looked at: a relative path is resolved against the current folder.  An empty path is this error too, which is what a variable that is not set gives in a shell.

Give the path of a solution, of a C# project, or of a folder that holds exactly one of them.

See: [Arguments](tool.md#arguments)

## SQLSRC204

**Project does not use SqlSource**

The project given to `sqlsource describe`, or named with `--project`, was restored and does not use the SqlSource package.  A project of a solution that was not named is not an error: it is left out, and nothing is said.

```console
$ dotnet sqlsource describe src/Tools/Tools.csproj
/work/src/Tools/Tools.csproj : error SQLSRC204: '/work/src/Tools/Tools.csproj' does not use SqlSource
    help: add the SqlSource package to the project
```

Add the package with `dotnet add package SqlSource --prerelease` and restore, or run the command on the project that has the queries.  A project with several target frameworks is read by the first of them: one that references SqlSource for a later framework alone is this error.

See: [Arguments](tool.md#arguments)

## SQLSRC205

**Project could not be evaluated**

`sqlsource` asks MSBuild about each project with `dotnet msbuild`: first what the project is, then which files the compiler is given.  One of those runs failed.  The lines under the error give the reason, and then the first twenty lines that MSBuild wrote.

```console
$ dotnet sqlsource describe
/work/App/App.csproj : error SQLSRC205: MSBuild could not evaluate '/work/App/App.csproj'
    reason: dotnet msbuild ended with the exit code 1
    msbuild: /work/App/App.csproj(4,5): error MSB4019: The imported project "/work/Shared.props" was not found.
```

Mend what MSBuild reports; `dotnet build` of the project reports the same.  When MSBuild says that the target `SqlSourceWriteManifest` does not exist, the SqlSource package of the project is older than the tool: update the package.  When the reason is that the project file does not exist, the solution lists a project that is not on the disk: restore the file, or take the project out of the solution.  When the reason is that `dotnet` could not be started, the .NET SDK is not installed or not on the path: the tool needs it, and not only the runtime it runs on.

See: [Requirements](tool.md#requirements)

## SQLSRC206

**Project manifest cannot be read**

For each project, `sqlsource` has MSBuild write a file that lists what the compiler is given: the project manifest, which the SqlSource package writes.  The tool could not read it.  The message ends with what is wrong.

```console
$ dotnet sqlsource describe
/work/App/App.csproj : error SQLSRC206: The project manifest of '/work/App/App.csproj' cannot be read: it has version '2' of the format and this tool reads version 1.  The SqlSource package and the sqlsource tool are out of step: update the older one
```

The SqlSource package of the project and the `sqlsource` tool are of different versions.  Update the older of the two: the package in the project file, or the tool with `dotnet tool update SqlSource.Tool --prerelease`.  The two are released together under one version number.

Any other reason, one that names a line for one, means that the package wrote a file the tool does not expect of its own version.  That is a bug in SqlSource: [report it](https://github.com/mbcrawfo/SqlSource/issues) with the message.

See: [Install the tool](tool.md#install-the-tool)

## SQLSRC207

**Project is not in the run**

A path given with `--project` is not a C# project of the solution that the run is on.  When the run is on a project, the option may name that project and no other.  The message holds the full path the tool looked at: a relative path is resolved against the current folder, not against the folder of the solution.

```console
$ dotnet sqlsource describe App.slnx --project Tools.csproj
sqlsource : error SQLSRC207: '/work/Tools.csproj' is not a project of '/work/App.slnx'
```

Give the path of a `.csproj` file that the solution lists.  `dotnet sln list` shows them.

See: [Options](tool.md#options)

## SQLSRC208

**Attribute argument is not a literal**

The `sqlsource` tool finds `[SqlSourceGenerate]` by reading the C# files of a project as text.  It does not compile them, so it cannot follow a name to its value.  `Path` and `Output` decide which queries are described, and the tool reads those two alone: `Path` must be a string literal, and `Output` a member of `GeneratorOutput` written out.

```csharp
using SqlSource;
internal static class Folders { public const string Queries = "Queries"; }
[SqlSourceGenerate(Path = Folders.Queries)]
internal static partial class Queries;
```

```console
/work/App/Queries.cs(3,27): error SQLSRC208: 'Path' of [SqlSourceGenerate] is read from the source by 'sqlsource', which needs a literal here
```

Write the value where the attribute is: `Path = "Queries"`, `Output = GeneratorOutput.Models`.  A constant, `nameof`, a concatenation, an interpolated string and a cast of a number are all this error, though the compiler accepts them.  The tool describes nothing for the type until it is mended, so the build reports [SQLSRC241](#sqlsrc241) for each of the type's queries that needs an entry in its sidecar and has none.  Under a dialect that has no database to ask, such as `ansi`, the build reports [SQLSRC209](#sqlsrc209) instead, and under `mssql` [SQLSRC243](#sqlsrc243).

See: [Known issues](generator.md#known-issues)

## SQLSRC209

**Output needs a dialect that can be described**

A query whose output is `models` or `codegen` is described by a database, and the dialect the tool can describe today is `postgres`.  The dialect `mssql` passes this check, since describing SQL Server queries comes in a later release, and its queries get [SQLSRC216](#sqlsrc216) from the tool, and the file [SQLSRC243](#sqlsrc243) in the build, until then.  This file has a query with such an output and another dialect.  The build reports this error, and so does the tool.  The default output is `codegen` and the default dialect is `ansi`, so a project that sets neither gets this error for every file.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC209: The output 'codegen' needs each query described by its database, and the dialect 'ansi' has no database to ask.  Set the dialect to 'postgres' or 'mssql', or the output to 'sql'.
```

For PostgreSQL files, set the dialect: `<SqlSourceDialect>postgres</SqlSourceDialect>` for the project, the metadata of the same name for a file, or `-- dialect: postgres` at the top of the file.  **For SQL Server files, set the output to `sql`**, though the message names `mssql`, which gives SQLSRC216 and SQLSRC243 until SQL Server queries can be described.  For any other database, and for queries that only want their SQL, set the output to `sql`: `<SqlSourceOutput>sql</SqlSourceOutput>`, `Output = GeneratorOutput.Sql` on the attribute, or `-- output: sql` in the file.  The error is reported once for a file, at the first query that needs types.  Until it is mended, no query of the file is described, and none gets an error about its entry in the sidecar; the file's constants and methods are generated as before.

See: [Database support](dialects.md#database-support)

## SQLSRC210

**Token has no default**

A query with a `{{token}}` cannot be sent to a database as it is written.  To describe it, the tool puts a sample in the token's place: the token's default.  This query's output is `models` or `codegen`, so it must be described, and the token the message names has none.  The build reports this error, and so does the tool, which prints the `help:` line under it.

```sql
-- name: FindUsers
SELECT id, name FROM users {{where}};
```

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC210: The token 'where' has no default, which a query whose output is 'codegen' needs.  Give it a default, or set the output to 'sql'.
    help: write the token with a default, {{where:default}}, or give one in a marker of the query: -- token: {{where:default}}
```

Give the token a default where it stands, `{{where:WHERE deleted_at IS NULL}}`, or once for the query with a marker, `-- token: {{where:WHERE deleted_at IS NULL}}`.  An empty default, `{{where:}}`, describes the query with nothing there.  The default is a sample for describing and nothing else: the generated method still takes the token as an argument.  A query that only wants its SQL can instead have the output `sql`, which needs no default.  The error is reported once for each token without a default, at the name of its query.  Until it is mended, the query is not described and gets no error about its entry in the sidecar; its constant or method is generated as before.

See: [Defaults](parameters-and-tokens.md#token-defaults)

## SQLSRC211

**Database has two dialects**

Each query that is described belongs to one logical database: the one its `-- database:` marker names, or else the `SqlSourceDatabase` metadata of its file, or else the property of that name, or else the name of its file's dialect.  A name is one database across the whole run, with one connection, and the dialect picks the driver of that connection.  Here two files give one name two dialects.  Names are compared ignoring case.

```console
/work/App/Queries/Reports.sql(1,10): error SQLSRC211: The database 'main' has the dialect 'mssql' here and 'postgres' in '/work/App/Queries/Users.sql'
```

Give the queries of one of the two files another database, or the dialect of the other file.  The database keeps the dialect of the first file that names it, in the order of the projects and then of the files' paths.  The error is reported once for each other file, at its first query of that database, and no query of that database in that file is described until it is mended.  A query whose output is `sql` belongs to no database and is not counted, and neither is a file that already has [SQLSRC209](#sqlsrc209).

See: [Database](settings.md#setting-database)

## SQLSRC212

**File is not in the run**

A path given to `sqlsource describe` that ends in `.sql` restricts the run to that file.  The run holds the `.sql` files that a type with `[SqlSourceGenerate]` claims, in the projects it is on, and this path is none of them.  The message holds the full path the tool looked at: a relative path is resolved against the current folder.

```console
$ dotnet sqlsource describe Queries/User.sql
sqlsource : error SQLSRC212: '/work/App/Queries/User.sql' is not a .sql file that a type of the run claims
```

Check the spelling of the path.  If the file exists, it is either not a file of the project, as when `SqlSourceIncludeFiles` is off and the project does not list it, or no type claims it: a type claims the `.sql` files in the folder of its own source file, or the ones its `Path` names.  With `--project`, the file must be claimed by a type of one of the projects named.  The rest of the run goes on.

See: [Arguments](tool.md#arguments)

## SQLSRC213

**Database has no connection**

A query that must be described belongs to a database that the run has no connection for.  A connection is given for the name of a database, and never in the project, since it holds credentials.  On the command line it is `--connection billing=<connection string>`.  In the environment it is the variable of the name: `SQLSOURCE_CONNECTION_`, then the name in upper case with every character that is not a letter or a digit written as `_`, so `billing-v2` reads `SQLSOURCE_CONNECTION_BILLING_V2`.  `SQLSOURCE_CONNECTION`, with no name, serves a run that has exactly one database that can be described: a database that is in the run only through queries with an error of their own, such as [SQLSRC209](#sqlsrc209), does not count.  The command line wins over the variable of the name, and that over `SQLSOURCE_CONNECTION`.  A variable that is empty or holds only white space is not set.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC213: No connection is given for the database 'postgres'
    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES
```

Give the connection in one of the two ways the `help:` line names, or leave the database out of the run with `--database`.  When `--connection` was given for a name that no database of the run has, the `help:` line lists those names, so that a misspelt one is seen.  The error is reported once for a database, at its first query that must be described, and those queries count as failed.  A database whose queries all have a current entry needs no connection, and its summary line says `no connection`; under `--force` every query is described, so every database of the run needs one.

See: [Connections](tool.md#connections)

## SQLSRC214

**Two databases share a connection variable**

The variable of a database's name is the name in upper case with every character that is not a letter or a digit written as `_`.  Two databases of this run have names that give one variable, as `app-v2` and `app_v2` do, so the variable cannot say which of them it is for.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC214: The databases 'app-v2' and 'app_v2' both read their connection from SQLSOURCE_CONNECTION_APP_V2
```

Give both databases their connection on the command line, `--connection app-v2=<connection string> --connection app_v2=<connection string>`, or rename one of them.  A connection on the command line for one of the two is not enough: the variable would then be read for the other alone, and a reader of the command could not tell.  The error is reported once for each of the two databases that has a query to describe, at its first such query, and those queries count as failed.

See: [Connections](tool.md#connections)

## SQLSRC215

**Connection names no database**

`SQLSOURCE_CONNECTION` is set, and it is the connection of a run with exactly one database.  This run has more, and the database of this query has no connection of its own, so nothing says that the variable is for it.  The message counts and names the databases that can be described: one that is in the run only through queries with an error of their own, such as [SQLSRC209](#sqlsrc209), needs no connection and is left out.

```console
/work/App/Queries/Reports.sql(1,10): error SQLSRC215: SQLSOURCE_CONNECTION is for a run with one database, and this run has 2: postgres, reports
    help: set SQLSOURCE_CONNECTION_POSTGRES for 'postgres' and SQLSOURCE_CONNECTION_REPORTS for 'reports', or pass --connection <name>=<connection string> for each
```

Set the variable of each database, as the `help:` line names them, or give each with `--connection`, or restrict the run to one database with `--database`.  The error stands in the place of [SQLSRC213](#sqlsrc213) and is reported as that one is: once for a database, at its first query that must be described.

See: [Connections](tool.md#connections)

## SQLSRC216

**No describer for the dialect**

The query must be described, and its dialect is `mssql`, which this version of the `sqlsource` tool cannot describe yet: it describes `postgres` alone.  It is not [SQLSRC209](#sqlsrc209), which is about a dialect the tool does not describe, every row marked No in [database support](dialects.md#database-support).

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC216: This version of sqlsource cannot describe 'mssql'
```

Set the output of the queries to `sql`, with an `-- output: sql` marker or `SqlSourceOutput` in the project file, to use their constants and methods without types; the tool then leaves them out.  Describing SQL Server queries comes in a later release, which you take by updating the `SqlSource` package and the tool together, as [SQLSRC206](#sqlsrc206) asks.  The error is reported at each query that the run would have described, and each counts as failed.

See: [SQL Server](sqlserver.md)

## SQLSRC217

**Sidecar was not written**

A sidecar is written whole or not at all: when every query of its `.sql` file that needs an entry was described in this run or already has a current one.  Here the run described a query of the file and could not write the result, because another query of the file has neither.  The lines under the message name each such query and say why: it `failed`, with an error of its own above, or it is `not in this run`, because `--database` or a `.sql` path left it out and its entry is missing or out of date.

```console
/work/App/Queries/Users.sql : error SQLSRC217: The sidecar of '/work/App/Queries/Users.sql' was not written, because not every query of the file has an entry
    query: GetInvoice: not in this run
    help: describe the whole file, or fix the queries that failed
```

Mend the queries that failed, or run `describe` without the filter that left a query out, so that every query of the file is described in one run.  The sidecar on the disk is as it was before the run.  The error is there so that a description that was not saved does not look like a success; a file that was held back with none of its queries described has only the errors of those queries.

See: [Describing again](tool.md#describing-again)

## SQLSRC218

**File could not be changed**

The tool could not write a sidecar, or could not delete one that its `.sql` file no longer needs, or could not read such a one to see that it may be deleted, or could not write the log of `--log` or `SQLSOURCE_LOG`.  The message says which, and ends with the reason the operating system gave.

```console
/work/App/Queries/Users.sql.json : error SQLSRC218: '/work/App/Queries/Users.sql.json' could not be written: Access to the path '/work/App/Queries/Users.sql.json' is denied.
```

**Fix:** give yourself the right to write in the folder of the `.sql` file, or close the program that holds the sidecar open, and run the command again.  The rest of the run goes on after an error about a sidecar.

- **A sidecar is never left half written.**  It is written to a temporary file beside it and then moved over the old one, so a run that fails or is stopped leaves the old file or the new one.
- **A sidecar is deleted only** in a run with no `--project`, `--database` or `.sql` path, in which nothing was reported as an error before the files were decided: a setting that is not valid, or a type that could not be read, can make a file look as if it needs no entry.
- **A sidecar that the tool cannot open**, for lack of permission, because it is locked or because it is a folder, is not deleted when the file beside it needs no entry, since it may be the file of a newer tool.  It is reported as one that `could not be read`.  Delete it yourself, or give yourself the right to read it.  `describe --check` reports the same, since it could not check the file.

For a log:

- **The log cannot be made or emptied as the run starts**, because its folder is not there, it is a folder, or the system will not let the tool write it.  Nothing runs and the exit code is 1.  Give a path that can be written, or run the command from another folder.
- **The path ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx`**, in any case.  The tool refuses it with the reason `a log is not written over a .sql file, a sidecar, a project or a solution`, touches nothing and runs nothing, so that a name left out before a `.sql` path does not empty that file.  Give the log a name of its own.
- **The log stops being writable while the run goes on**, as when the disk is full.  The run finishes and writes its sidecars, the error is reported once at the end, and the exit code is 1.  The log holds what was written before it stopped.

See: [Logs](tool.md#logs)

## SQLSRC219

**Sidecar is out of date**

`describe --check` asked the database to describe every query of the run and compared what it said with the sidecars that are there.  It changes no file.  The message names a query and says what differs, or names the `.sql` file when the whole sidecar does:

| The message ends with | What it means |
|----|----|
| `the sidecar does not exist` | A query of the file needs an entry, and the file has no sidecar |
| `the sidecar cannot be read`, or `the sidecar has format 2, and this tool writes 1` | The sidecar is no sidecar of this version of the tool.  A merge conflict in it is the usual cause of the first |
| `the sidecar has no entry` | The query was added, or renamed, since the sidecar was written |
| `the query has changed since it was described` | The SQL, a `-- param:` marker or the default of a token changed |
| `columns[2].nullable is true, and the database says false` | The database describes the query another way than the sidecar holds.  A change to the schema is the usual cause |
| `database is "billing", and the project says "reports"` | The query belongs to another database, or another kind of database, than the one it was described against |
| `the query is not in the file`, or `the query needs no entry` | The sidecar holds an entry for a query that was removed, or whose output is now `sql` |
| `no query of the file needs an entry` | The sidecar belongs to a file that needs none any more |

```console
/work/App/Queries/Users.sql(3,10): error SQLSRC219: The sidecar is out of date for 'GetUser': columns[2].nullable is true, and the database says false
    help: run 'dotnet sqlsource describe --force' and commit the sidecars
```

**Fix:** the `help:` line under each error names the command that mends it, and you then commit the sidecars it writes:

| The message | Run |
|----|----|
| Names a query that was added, changed or removed, another database, or a sidecar that does not exist, cannot be read or has a lower format | `dotnet sqlsource describe`, with the filters the check had |
| Names a difference that the database reports, such as `columns[2].nullable` | `dotnet sqlsource describe --force`: the tool cannot see a change to the schema, and skips a query whose SQL is as it was |
| Says the sidecar has a higher format than this tool writes | Update the `SqlSource.Tool` package: a newer tool wrote the sidecar, and `describe` leaves it as it is |

- **Columns and parameters are counted from 0**: `columns[2]` is the third column.
- **Only the first difference of a query is named.**
- **What is compared is what changes generated code**: the query as written (its SQL, its `-- param:` markers and its tokens' defaults), its database and the kind of that database, whether it returns rows and which table's rows they match, if any, and the name, position, type and nullability of each parameter and column.  The versions of the tool and of the server are not compared.
- **With `--database`**, an entry for a query that is no longer in the file, or that needs none, is reported only when its database is one of those that `--database` names: an entry of another database, or one with no database, is not.

The tool exits with the code 2 when it reported this error and no other, so that a build can tell a sidecar that is out of date from a check that could not be made.  A query that could not be described has an error of its own, and the exit code is then 1.

See: [Check in CI](tool.md#check-in-ci)

## SQLSRC220

**Project was not restored**

The tool tells that a project uses SqlSource by the package's MSBuild files, and those reach a project only through a restore.  This project either references the SqlSource package and has nothing of it, so it was not restored since the reference was added, or has no file of a restore at all, so nothing says whether it uses SqlSource.  The tool does not restore: a restore is slow and writes into the project.

```console
$ dotnet sqlsource describe
/work/src/App/App.csproj : error SQLSRC220: '/work/src/App/App.csproj' has not been restored, or not since the SqlSource package was added to it
    help: run 'dotnet restore'
```

Run `dotnet restore` on the solution or the project, then the command again.  In a solution every C# project is checked, one that has no SQL too: a run on a fresh checkout must not succeed by finding nothing.  A reference that keeps the package's MSBuild files out of the project, `ExcludeAssets="build"` for one, gives this error after a restore as well: remove that from the reference.

See: [Requirements](tool.md#requirements)

## SQLSRC221

**Sidecar was written by a newer tool**

A sidecar, the `.sql.json` file beside a `.sql` file, says which version of the format it has.  This one has a higher version than this tool writes, so a newer `sqlsource` wrote it, and this one must not write it back down: the generator of that newer version would no longer read it.

```console
/work/App/Queries/Users.sql.json : error SQLSRC221: '/work/App/Queries/Users.sql.json' has format 2, and this tool writes format 1
    help: update the SqlSource.Tool package
```

Update the tool, with `dotnet tool update SqlSource.Tool --prerelease`, to the version of the SqlSource package the project uses.  The file is left as it is, and every query of its `.sql` file that the run was asked to describe counts as failed.  A sidecar of a lower format version, of another version of the tool, or one that cannot be read, a merge conflict for one, is no error: the tool describes its queries and writes it again.  A run that may delete a sidecar, because a file needs no entry any more, does not delete one of a higher format version either.

## SQLSRC222

**Directory cannot be read**

`sqlsource describe` was given a folder, or none and so the current one, and the operating system would not list its files.  The message ends with the reason the system gave.

```console
$ dotnet sqlsource describe /srv/locked
sqlsource : error SQLSRC222: '/srv/locked' cannot be read: Access to the path '/srv/locked' is denied.
```

Give yourself the right to read the folder.

## SQLSRC223

**Solution cannot be read**

The `.sln` or `.slnx` file that `sqlsource describe` was given, or found in the folder, is not a solution that can be read.  The message ends with the reason of the library that reads it, which is the one the `dotnet` command uses.  A solution that lists one project twice is refused too.

```console
$ dotnet sqlsource describe
/work/App.sln : error SQLSRC223: '/work/App.sln' cannot be read: Not a solution file.
```

Mend the file.  `dotnet sln list` reads it the same way.

## SQLSRC224

**Database could not be opened**

The tool could not open a session with the database of a query that must be described: nothing answers at the address, the server refused the user or the password, the database does not exist, the driver cannot read the connection string, or a file that the connection string names cannot be used.  The lines that start with `server:` are what the server or the driver said, as they said it.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC224: The database 'app' could not be opened
    query: GetUser, database app, postgres
    server: 28P01: password authentication failed for user "app"
    help: check that the database is running and that the connection string names it
```

**Fix:** start the database, or correct the connection string that `--connection` or the database's variable gives.  The error is reported once, at the first query of the database that the run would have described, and each of them counts as failed.

Some `server:` lines are the tool's own, since what the driver said would quote the connection string or a path in it:

| The `server:` line | What it means |
|----|----|
| `Npgsql cannot read the connection string` | The string is not written as Npgsql wants one: `Host=localhost;Database=app;Username=app;Password=...` |
| `a file that the connection string or the environment names cannot be read` | A client certificate, a key or a password file (`SslCertificate`, `SslKey`, `Passfile`, or `PGSSLCERT`, `PGSSLKEY`, `PGPASSFILE`) is not there, its folder is not there, or the tool may not open it |
| `a file that the connection string or the environment names is not what Npgsql expects: it is not in the right form, or its password is wrong` | Such a file was read and could not be used, as when a line of a password file is no entry |
| `Npgsql does not support these settings of the connection string together` | A `Target Session Attributes` other than `Any` for one host, or a `Replication Mode` for several |
| `GSS authentication is not available on this machine` | The server asks for GSS, and the operating system's GSS library cannot be loaded |

See: [Connecting](postgresql.md#connecting)

## SQLSRC225

**Server rejected the query**

The tool asked the server to prepare the query, and the server refused it: a syntax error, a table or a column the database does not have, an operator that does not exist for the types it is given.  The message ends with what the server said, and the `server:` lines give all of it: the SQLSTATE and the message, then its `detail:` and `hint:` where it gave them.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC225: The server rejected the query 'GetUser': relation "user" does not exist
    query: GetUser, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 42P01: relation "user" does not exist
    server: at character 15
```

**Fix:** mend the query, or bring the database to the schema the query was written for.

- **`at character`** counts from the start of the query's SQL as the tool sent it: without its comments, with each token's default in the token's place and `$1` in the place of each parameter, so it is close to the place in the file and not always the same.  Run with `--log` for the SQL as it was sent.
- **An error at the step `resolve type`** has no `at character` line: there the server refused the tool's question about a type, and not the query.
- **A type that a `-- param:` marker declares** is checked against every use of the parameter, so a declared `int` compared with a text column is this error too.

A parameter with no declared type takes the type of its first use, so `placed_at > now() - @age` makes `@age` a `timestamp with time zone`, and the server then reports `operator does not exist: timestamp with time zone > interval`, an operator that the SQL does not hold.  For an error about a type or an operator, when the query has a parameter with no declared type and a use with no cast, the `help:` line says that the server may have given it the type of its first use, and names each such parameter.  The help is a guess: the server does not say whether its error is at a parameter, so the help can appear for an error that is not, such as a misspelt function.  The `at character` line says where the error is.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC225: The server rejected the query 'ListRecentOrders': operator does not exist: timestamp with time zone > interval
    query: ListRecentOrders, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 42883: operator does not exist: timestamp with time zone > interval
    server: hint: No operator matches the given name and argument types. You might need to add explicit type casts.
    server: at character 86
    help: if the error is at a use of a parameter, the server gave @age the type of its first use, which may not be the type meant: cast the parameter where it is used to the type meant, @age::<type>, or declare its type with -- param: @age <type>
```

Write the type meant in the place of `<type>`: here `@age::interval`, or `-- param: @age interval`.

A statement that the server cannot prepare, though its SQL is valid, such as `CALL` or `CREATE TABLE`, is [SQLSRC233](#sqlsrc233) and not this error.

It is also given when a table, a column or a type that the query uses was dropped while the tool described it, since another session changed the schema between two of the tool's questions.  The message says what went, as in `the table of the column 'id' no longer exists`; there is no `server:` line, since the server said nothing, and the help says to run the tool again.

It is also what a connection that the server ended while the queries were described is reported as.  The query that finds it out has what the server said last, `57P01: terminating connection due to administrator command` for a backend that was ended, or the driver's message with no SQLSTATE when the server said nothing.  Every query of that database after it has the line `the connection to the server was lost`, with no SQLSTATE: start the run again.

The step `check parameters` gives it too, for an error of the tool's comparison with Npgsql that says nothing of how Npgsql reads the SQL: a connection that was lost, or an error of the server that is not of class `42` (syntax errors and access rule violations), such as a statement that was cancelled or timed out.  The `server:` line is what the server or the driver said, and the message ends with it.  An error of class `42` there is [SQLSRC231](#sqlsrc231), since a `@` that the two read differently gives one.  Start the run again.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC225: The server rejected the query 'ListUsers': canceling statement due to statement timeout
    query: ListUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: check parameters
    server: 57014: canceling statement due to statement timeout
```

## SQLSRC226

**Parameter type could not be determined**

The server gives each parameter a type from the way the query uses it, and nothing in this query says what the parameter is: `SELECT @p IS NULL` holds for a value of any type.

```console
/work/App/Queries/Checks.sql(1,10): error SQLSRC226: The type of the parameter '@p' of the query 'IsNull' could not be determined
    query: IsNull, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 42P18: could not determine data type of parameter $1
    help: cast the parameter where it is used, @p::int, or declare its type with -- param: @p int
```

Cast the parameter where it is used, or declare its type with a `-- param:` marker.  The same error, at the step `resolve type` and with no `server:` line, says that the server did give the parameter a type and that the type is a pseudo-type, which no value has: `row_to_json(@p)` makes `@p` a `record`.  Its `help:` line names the pseudo-type, and the fix is the same.

See: [Parameters](postgresql.md#parameters)

## SQLSRC227

**Parameter's first use does not fix its type**

PostgreSQL takes a parameter's type from its first use.  Here the first use says nothing of the type and a later one does, which the server refuses.  The pattern of an optional filter does it: in `(@name IS NULL OR name = @name)` the first `@name` could be anything.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC227: The first use of the parameter '@name' of the query 'FindUsers' does not fix its type
    query: FindUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 42P08: could not determine data type of parameter $1
    server: at character 28
    help: declare the type with -- param:, or write the use that fixes the type before the one that does not: (name = @name OR @name IS NULL)
```

Declare the parameter's type with `-- param: @name text`, or turn the condition round so that the use that fixes the type comes first.

See: [Parameters](postgresql.md#parameters)

## SQLSRC228

**Declared type is not valid**

A `-- param:` marker gives a parameter a type, and the tool asks the server to resolve that type and to check it against every use of the parameter.  This type cannot be used, for the reason the message ends with:

- `it is not written as a type name`: the text is not a type name as a column definition writes one, with at most one modifier in parentheses and `[]`.  The server can give the same reason for a type it cannot use, such as `numeric(2,-3)` on PostgreSQL 14, which has no negative scale; the `server:` line then says what the server made of the type.
- `the database has no such type`: the server knows no type of that name on the session's search path.
- `it is a pseudo-type`: the server knows the name, and no value has that type, `record` and `anyelement` for two, or it is an array of such a type, `cstring[]`.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC228: The type 'user_state' declared for the parameter '@status' of the query 'ListUsers' is not valid: the database has no such type
    query: ListUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 42704: type "user_state" does not exist
    help: write a type the database has, schema-qualified if it is not on the search path
```

Write the type as the database has it: `int`, `varchar(50)`, `numeric(18,2)`, `timestamp(3) without time zone`, `public.user_status`, `"My Schema"."Status"[]`.  A type in a schema that is not on the search path of the connection needs its schema.  A marker's type runs to the end of its line, but for a `null` or `not null` that ends it, so nothing else may follow it there.

See: [Parameters](postgresql.md#parameters)

## SQLSRC229

**Column type is not supported**

A column of the query has a pseudo-type, a type that the server knows by name and that no value read from a column can be given.  `record` is the one a query meets: a row that is built in the select list and not taken apart again, `ROW(a, b)`, or the alias of a subquery selected whole.  An array of one, `record[]`, is another.  The tool cannot say what such a column holds, so the query is not described.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC229: The column 'row' of the query 'ListPairs' has the type 'record', which cannot be described
    query: ListPairs, database app, postgres 18.6, Npgsql 10.0.3
    step: resolve type
    help: cast the column to a concrete type, or give the expression an alias and a cast
```

Select the fields of the row as columns of their own, or cast the expression to a type the database has: `ROW(a, b)::pair`, with `pair` a composite type, or `to_jsonb(s)` for a whole row.  A parameter of a pseudo-type that the query returns is reported for the parameter, as [SQLSRC226](#sqlsrc226) or [SQLSRC228](#sqlsrc228).

See: [What can be described](postgresql.md#what-can-be-described)

## SQLSRC230

**Query holds more than one statement**

A query is one statement.  The tool prepares it on the server to describe it, and it found a second statement in the text.  Nothing of such a query is sent.

```console
/work/App/Queries/Cleanup.sql(1,10): error SQLSRC230: The query 'Cleanup' holds more than one statement
    query: Cleanup, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    help: one statement for each query; a ; is allowed at the end alone
```

Give each statement a `-- name:` of its own.  A `;` at the end of the statement is allowed, with white space and comments after it; a `;` inside a string, a dollar-quoted string or a comment does not count.  A `;` inside parentheses counts as the end of a statement, since the tool does not read the structure of the SQL; no statement that can be prepared holds one.

A text that the tool reads as one statement and the server as two is [SQLSRC225](#sqlsrc225) with the server's `42601: cannot insert multiple commands into a prepared statement`.  Nothing of it is run either.

See: [What can be described](postgresql.md#what-can-be-described)

## SQLSRC231

**Parameters as Npgsql reads them differ**

SqlSource finds the parameters of a query with its own reading of the SQL, and describes the query that way.  The generated query methods of a later release will send the SQL as you wrote it and let Npgsql find the `@name` parameters, so the tool also asks Npgsql to describe the SQL that way, and compares the two results: the columns, and where each parameter stands.  They are not the same, so what was described is not what would run.  The message ends with what differs: where the two texts part, the number of columns, the name or the type of one, or what the server said of the SQL as Npgsql sent it.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC231: Npgsql reads the parameters of the query 'ListUsers' differently from SqlSource: syntax error at or near "@"
    query: ListUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: check parameters
    server: 42601: syntax error at or near "@"
    help: a @ that one of the two reads as a parameter and the other as text; the server's lines say where; a table, a column or a type that another session changed during the run shows the same way, so run describe again
```

**Fix:** look for a `@` that stands where only one of the two would read a parameter: inside an unusual string or an identifier, or directly after another character of a name.  Put a space before the parameter, or write the text so that the `@` is plainly inside a string.

Npgsql's own refusal of the text is this error too.  It has no SQLSTATE, and the `server:` line is Npgsql's message: a `;` followed by more text, which Npgsql reads as the end of a statement where SqlSource read no end, is one.

A difference may leave the columns the same, and then the server accepts both readings.  Npgsql also reads `:name` as a parameter, so `tags[:id]` in a query with a parameter `@id` and a column `id` is a subscript for Npgsql, which sends `tags[$1]`, and the name of a column for SqlSource.  The query still returns the same columns, and only the comparison of the two texts sees it:

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC231: Npgsql reads the parameters of the query 'ListUsers' differently from SqlSource: the SQL with $n parts at character 45: '$1] IS NOT' for Npgsql and ':id] IS NO' for SqlSource
    query: ListUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: check parameters
    help: a @ or a : that one of the two reads as a parameter and the other as text; the message says where the two texts part
```

The query is `SELECT id FROM users WHERE id = @id AND tags[:id] IS NOT NULL`.  The position counts the characters of the SQL as the tool sent it, with `$1` in the place of each parameter and without the query's comments: it is not a position in the file.  Rename the parameter so that it is not the name of a column that a slice reads.  Where the `:` follows a bound, `tags[1:id]`, Npgsql sends `tags[1$1]` and the server answers a `42601` like the first example's.

The error also follows a schema that changed while the tool ran: a table, a column or a type that another session dropped between two of the tool's questions gives an error of class `42` at this step, which cannot be told from a misread `@`.  Run `describe` again.

One difference has no `@` in it: a parameter that is an array of a domain, `-- param: @p email_t[]`, and is returned as a column.  Npgsql sends it as an array of the domain's base type, so the column is `character varying[]` for Npgsql and `email_t[]` for SqlSource, and the help is `Npgsql sends a parameter that is an array of a domain as an array of the domain's base type, so the column it is returned as has another type; cast the column, @p::varchar[] for an array of a domain over varchar` in place of the first example's help.  Cast the column to the type you want, `@p::varchar[]`.

A pooler in transaction mode, such as PgBouncer, can also show as this error: the describe of a statement that is missing on another backend can answer no columns and no error, so the message ends "... columns for Npgsql and 0 for SqlSource", and a statement that an earlier run left on a pooled backend can give the wrong column names.  The fix is a direct connection to the server, as [SQLSRC236](#sqlsrc236) says.  An error of the check that says nothing of how Npgsql reads the SQL, such as a lost connection, is [SQLSRC225](#sqlsrc225) at the same step.  If the SQL is plain and the two still differ, report it.

See: [What can be described](postgresql.md#what-can-be-described)

## SQLSRC232

**Type cannot be bound by Npgsql**

The generated query methods of a later release will send each parameter to Npgsql by the name of its type, so the tool checks that Npgsql can send the parameter's type before it writes the sidecar.  Here Npgsql refused the type before it sent anything, or the type has no name that Npgsql could be given without ambiguity, so the query is not described.  Your build, the query's constant and your own code that runs it are not affected.  The `server:` line is Npgsql's own message, or, for a type with no name, the tool's own.  This is a known limit and not a fault of the query: Npgsql cannot send a value of these kinds of type, and the tool never rewrites your SQL to make a parameter sendable.  They are refused:

- a composite type, `public.pair`, and an array of one;
- a table's row type, and an array of one;
- `regclass`, which `WHERE c.oid = @t::regclass` gives a parameter, and the other types of the system catalogs that Npgsql does not load, such as `aclitem`, `txid_snapshot` and `pg_node_tree`;
- a type whose schema or whose name holds a `.`, which cannot be written to Npgsql without ambiguity, since `a.b.c` could name more than one type.

A type whose name or whose schema's name needs quotes (`"My Schema"."Status"`), `"char"`, the one-byte type of the system catalogs, a domain over another domain, the bit string type without a length, and an array of a type whose name is 63 characters long are bound.

```console
/work/App/Queries/Pairs.sql(1,10): error SQLSRC232: Npgsql cannot bind the type 'public.pair' of the parameter '@pair' of the query 'ListPairs'
    query: ListPairs, database app, postgres 18.6, Npgsql 10.0.3
    step: check parameters
    server: Writing is not supported for parameters having DataTypeName 'public.pair'.
    help: Npgsql cannot send a value of this type; declare the parameter as text with -- param: and cast it to the type where it is used; a later version may let a project declare a conversion for the type
```

**Fix:** declare the parameter as `text` and cast it where it is used, `-- param: @pair text` with `@pair::public.pair` in the SQL; or give the query `-- output: sql`, and the tool leaves it out.  For a type with no name the help is `the type has no name that Npgsql can be given without ambiguity; rename the schema or the type, or declare the parameter as text with -- param: and cast it to the type where it is used`.  The error is also given for a parameter that only a `-- param:` marker declares: Npgsql resolves the type of every parameter it is handed.

See: [What can be described](postgresql.md#what-can-be-described)

## SQLSRC233

**Statement cannot be described**

The tool describes a query by asking the server to prepare it, and the server prepares only `SELECT` (without `INTO`), `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `VALUES` and `TABLE`, with `WITH` around any of them.  This query starts with a statement of another kind: `CALL`, `CREATE`, `EXPLAIN`, `SHOW`, `SET` and the rest of the commands the server refuses to prepare.  The tool reads the first word of the statement before it sends anything, so the message names that word and the server is not asked.

```console
/work/App/Queries/Archive.sql(1,10): error SQLSRC233: The query 'Archive' is a CALL statement, which the tool cannot describe
    query: Archive, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    help: give the query -- output: sql; the tool describes SELECT without INTO, INSERT, UPDATE, DELETE, MERGE, VALUES and TABLE
```

Give the query `-- output: sql`: its SQL is still a string constant, or a method where the query has tokens, with no types, so the tool does not describe it.  The error still fails the query, so the file's sidecar is held back until the query is mended or given the marker: the tool does not decide on its own that such a query needs no entry, since that would override what you wrote.

`SELECT ... INTO` is refused as `SELECT INTO` on every version of the server.  PostgreSQL 15 and later prepare it and 14 does not, but it creates a table, as `CREATE TABLE AS` does, and a sidecar must not depend on the server it was described against.  Write `CREATE TABLE ... AS` with `-- output: sql`, or select the rows and insert them in a second query.  A statement that the tool's check misses and that the server names as one it cannot prepare, `42P14: utility statements cannot be prepared`, is this error too, and the message then reads "is a utility statement".

A word that the tool does not know stays [SQLSRC225](#sqlsrc225): `SELEC 1` is a typo and the server says so.

See: [What can be described](postgresql.md#what-can-be-described)

## SQLSRC234

**Query writes a positional parameter**

The tool finds a query's parameters by their `@name` and sends the SQL with `$1`, `$2` in their places.  A `$n` that the SQL writes itself would be taken for one of those, so the types the server gave would not be the parameters' own, and the query methods of a later release, which send parameters by name, could not fill it.  Nothing of such a query is sent.  The message names the `$n` as it is written.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC234: The query 'GetUser' writes the parameter '$1' itself
    query: GetUser, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    help: write the parameter as @name; generated code binds parameters by name
```

Write the parameter as `@name`.  A `$` followed by a digit that is part of a name (`a$1`), or that stands in a string, a dollar-quoted string, a quoted identifier or a comment, is text and not a parameter.

See: [What can be described](postgresql.md#what-can-be-described)

## SQLSRC235

**Server version is not supported**

The tool supports PostgreSQL 14 and later, and the server that the database's connection string reaches is older.  The version is read when the session is opened, before any query is sent, and it is the one the server reports.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC235: The database 'app' runs PostgreSQL 13.12, and the tool supports 14 and later
    query: GetUser, database app, postgres
    help: upgrade the server, or describe against a database of PostgreSQL 14 or later
```

Upgrade the server, or point the tool at a database of PostgreSQL 14 or later, a copy of the schema on a newer server for one.  The error is reported once, at the first query of the database that the run would have described, as [SQLSRC224](#sqlsrc224) is, and each of the database's queries counts as failed.  A version that does not start with a number is not refused, since the tool cannot tell what it is.

See: [Connecting](postgresql.md#connecting)

## SQLSRC236

**Connection does not keep a session**

The tool prepares a statement on the server and asks about it in later steps, which only works when every step reaches the same session.  This connection does not keep one.  There are two causes:

- The connection string sets `Multiplexing=true`, which shares physical connections between commands.  It is found when the database is opened, before the tool connects.
- A pooler in transaction mode, such as PgBouncer, sits between the tool and the server.  It hands each transaction to a backend of its own, so a later step can miss the statement the tool prepared.  The tool finds out at a query: the server does not list the statement, or answers `26000` (it does not exist) or `42P05` (it already exists).  Behind a pooler a describe can also answer no columns and no error, which the run reports as [SQLSRC231](#sqlsrc231); the fix is the same.

For `Multiplexing=true` the tool refuses before it connects, so no query has been sent and the server is not yet known; the `server:` line is the tool's own text:

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC236: The connection to the database 'app' does not keep one session
    query: GetUser, database app, postgres
    server: Multiplexing=true does not keep one session
    help: connect directly to the server, not through a pooler in transaction mode, and without Multiplexing=true
```

Behind a pooler the session has opened, so the query line names the server and the driver, and the error is at the step whose call found out.  Here the server did not list the statement it had just prepared, and the `server:` line is the tool's own text; for `26000` and `42P05` it is the server's:

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC236: The connection to the database 'app' does not keep one session
    query: GetUser, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: the server does not list the statement it prepared
    help: connect directly to the server, not through a pooler in transaction mode, and without Multiplexing=true
```

Connect directly to the server: the address of the database itself, not of the pooler, and without `Multiplexing=true`.  Hosted providers that pool connections offer a direct endpoint beside the pooled one.  Only the tool needs this: your application can still connect through the pooler.  The error for `Multiplexing=true` is reported once, at the first query of the database that the run would have described, as [SQLSRC224](#sqlsrc224) is, and each of the database's queries counts as failed; one found at a query is reported at that query.

See: [Connecting](postgresql.md#connecting)

## SQLSRC237

**Sidecar cannot be read**

The sidecar of a `.sql` file, the `.sql.json` file beside it, is not a file that `dotnet sqlsource describe` wrote.  A merge conflict in it is the usual cause, and a hand edit the next.  The message says what is wrong, and the error is at the place in the sidecar where it is.

```console
/work/App/Queries/Users.sql.json(7,1): error SQLSRC237: The sidecar cannot be read, because it is not one JSON object.  Run 'sqlsource describe' to write it again.
```

Run `dotnet sqlsource describe`, which writes the whole file again, and commit it.  After a merge conflict, take either side of the conflict or none: the tool writes the file from your queries and your database, not from what the file held.  While the sidecar cannot be read, no query of the file gets an error about its entry.

See: [Sidecar files](tool.md#sidecar-files)

## SQLSRC238

**Sidecar has a newer format**

The sidecar was written by a newer version of the `sqlsource` tool, in a format that this version of the `SqlSource` package does not read.  The message names the sidecar's format and the one this package reads.

```console
/work/App/Queries/Users.sql.json(1,1): error SQLSRC238: The sidecar has format 2, and this version of SqlSource reads format 1.  Update the SqlSource package.
```

Update the `SqlSource` package to the version of the tool that wrote the sidecar.  The package and the tool share a version, and a project should use the same one for both.  Until then, no query of the file gets an error about its entry.

See: [Sidecar files](tool.md#sidecar-files)

## SQLSRC239

**Sidecar has an older format**

The sidecar was written by an older version of the `sqlsource` tool, in a format that this version of the `SqlSource` package no longer reads.  The message names the sidecar's format and the one this package reads.

```console
/work/App/Queries/Users.sql.json(1,1): error SQLSRC239: The sidecar has format 0, and this version of SqlSource reads format 1.  Run 'sqlsource describe' to write it again.
```

Run `dotnet sqlsource describe` with the tool of the package's version, and commit the sidecar it writes.  Until then, no query of the file gets an error about its entry.

See: [Sidecar files](tool.md#sidecar-files)

## SQLSRC240

**Sidecar was written by another version of the tool**

The sidecar was written by a version of the `sqlsource` tool other than the version of the `SqlSource` package the project uses.  The package and the tool share a version, so this means that the tool and the package reference are out of step, or that the package was updated and the sidecars were not written again.  The sidecar is read and used all the same, since its format is the one this package reads.  A version's suffix, such as `-dev` in `0.1.0-dev`, is not compared.

```console
/work/App/Queries/Users.sql.json(1,1): warning SQLSRC240: The sidecar was written by sqlsource 0.1.0, and this project uses SqlSource 0.2.0.  Bring the tool and the package to one version, and run 'sqlsource describe'.
```

Update the tool or the package so that both have one version, run `dotnet sqlsource describe`, and commit the sidecars.  The tool describes every query of a file that another version wrote.

This is a warning, so that a build goes on while the sidecars are written again.  A project can list it in `NoWarn` to hide it, or in `WarningsNotAsErrors` to keep it a warning in a project that sets `TreatWarningsAsErrors`:

```xml
<PropertyGroup>
    <WarningsNotAsErrors>$(WarningsNotAsErrors);SQLSRC240</WarningsNotAsErrors>
</PropertyGroup>
```

See: [Sidecar files](tool.md#sidecar-files)

## SQLSRC241

**Query has no entry in the sidecar**

The query's output is `models` or `codegen`, which needs a description of the query in its file's sidecar, and the sidecar has none: the file has no sidecar, or its sidecar has no entry of the query's name.  A query that was added or renamed since the tool last ran is the usual cause.  Names are compared as C# compares them, so `getuser` is not `GetUser`.  The message names the query and its output.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC241: The query 'GetUser' has no entry in the sidecar of its file, and its output 'codegen' needs one.  Run 'sqlsource describe'.
```

Run `dotnet sqlsource describe` and commit the sidecar it writes.  The default output is `codegen`, so a project that only wants each query's SQL as a constant sets the output to `sql`: `<SqlSourceOutput>sql</SqlSourceOutput>` for the project, `Output = GeneratorOutput.Sql` on the attribute, or `-- output: sql` in the file.  The error is reported once for a query, however many types use its file, and the query's constant or method is still generated.

See: [Output](settings.md#setting-output)

## SQLSRC242

**Entry is of another database**

The query's entry in the sidecar was described against a kind of database that is not the dialect of its file: the file's dialect was changed since the tool last ran, or the sidecar was copied from another file.  The message names the query, the kind of database of the entry and the file's dialect.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC242: The entry of the query 'GetUser' was described for 'mssql', and the dialect of this file is 'postgres'.  Run 'sqlsource describe'.
```

Check that the file's dialect is the right one, then run `dotnet sqlsource describe` and commit the sidecar it writes.

See: [Dialect](settings.md#setting-dialect)

## SQLSRC243

**No type map for the dialect**

The file has a query whose output is `models` or `codegen`, and this version of SqlSource cannot give types for the file's dialect: it gives them for `postgres` alone.  `mssql` gets this error until SQL Server queries can be described, which comes in a later release.  The error is reported once for the file, at its first query that needs types, and no query of the file gets another error about its entry.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC243: This version of SqlSource has no type map for the dialect 'mssql', so no query of this file can have models.  Set the output to 'sql'.
```

Set the output of the file's queries to `sql`, with `-- output: sql` in the file or `SqlSourceOutput` for its item or its project.  Their constants and methods are generated as before.

See: [SQL Server](sqlserver.md)

## SQLSRC244

**Entry is stale**

The query has changed since its entry in the sidecar was written: its SQL, a `-- param:` marker or the default of one of its tokens.  A change to a comment alone does not count.  An entry that describes another version of the query could give it the wrong types, so it is not used.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC244: The query 'GetUser' has changed since its entry was written.  Run 'sqlsource describe'.
```

Run `dotnet sqlsource describe` and commit the sidecar it writes.

See: [Describing again](tool.md#describing-again)

## SQLSRC245

**Entry names no query**

The sidecar has an entry for a query that does not need one: a query that was removed or renamed, or one whose output is now `sql` for every type that uses its file.  The entry is not used.  The warning is at the entry's name in the sidecar.

```console
/work/App/Queries/Users.sql.json(15,5): warning SQLSRC245: The entry 'GetUsers' names no query of the file that needs one.  Run 'sqlsource describe', which removes it.
```

Run `dotnet sqlsource describe` and commit the sidecar it writes, which has no such entry.  When no query of the file needs an entry any more, a run of `describe` over the whole project deletes the sidecar.

A `.sql` file that two projects include is the exception.  A run of `describe` over both projects keeps the entry that the other project needs, so the warning stays.  A run over only the project that warns removes the entry, and the other project's build then reports [SQLSRC241](#sqlsrc241).  For such a file the warning clears when the query needs an entry in both projects or in neither, which is an output other than `sql` in both or `sql` in both, or when the project lists `SQLSRC245` in `NoWarn`.  See [known issues](sql-files.md#known-issues).

This is a warning, since an entry that nothing uses changes no generated code.  A project can list it in `NoWarn` to hide it, or in `WarningsNotAsErrors` to keep it a warning in a project that sets `TreatWarningsAsErrors`.

See: [Sidecar files](tool.md#sidecar-files)

## SQLSRC246

**Type has no C# type**

A column or a parameter of the query has a database type that Npgsql cannot read as a C# value, so SqlSource can give it no C# type.  The message names the column or parameter, its type, and what to do.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC246: The column 'relation' has the type 'regclass', which SqlSource cannot give a C# type.  Cast it to oid in the query.
```

What to do depends on the type:

- **`regclass`, `regproc`, `regprocedure`, `regoper`, `regoperator`, `regrole`, `regnamespace`, `regcollation` and `regdictionary`.**  Cast the column to `oid` in the query, `SELECT 'users'::regclass::oid AS relation`, which Npgsql reads as a `uint`.  Cast an array of one to `oid[]`, as the message says.
- **A composite type, which includes a table's row type, and a base type of an extension that SqlSource does not know.**  Map it with the [type override](settings.md#setting-type-overrides) to the C# type that your application reads it as, `public.address=MyApp.Address` for a type that the data source maps, or select its fields as columns of their own.
- **A range of a subtype that SqlSource does not know, and a multirange of one.**  Map it with the type override to `NpgsqlTypes.NpgsqlRange<T>` of its subtype's C# type, `public.floatrange=NpgsqlTypes.NpgsqlRange<double>`, and call `EnableUnmappedTypes()` on the data source builder, without which Npgsql cannot read it.  A project that does not reference Npgsql cannot name `NpgsqlTypes.NpgsqlRange<T>`, so the message tells it to reference Npgsql first.
- **`cube` on Npgsql 8 or 9**, which cannot read it.  Update Npgsql to 10, which reads it as `NpgsqlTypes.NpgsqlCube`, or cast the column to `text` in the query, as the message says.
- **`pg_snapshot`, `txid_snapshot`, `pg_node_tree`, and any other base type of `pg_catalog` that SqlSource does not know, such as `aclitem`.**  No version of Npgsql reads them, so a type override would build and fail when the query runs.  Cast the column to `text` in the query, and an array of one to `text[]`, as the message says.
- **A parameter with no type, `record`, `unknown`, `void`, or a kind of type that this version of SqlSource does not know.**  The `sqlsource` tool never writes these, so the sidecar was edited by hand or written by a newer tool.  Run `dotnet sqlsource describe`.

When the message says to map the type, it names the key of the type with its schema: `public.address`, or `pg_catalog.pg_class` for the row type of a catalog table, since a key without a schema must be a type that SqlSource knows.  For an array or a domain, the key is of the element or base that has no C# type, and an entry for it maps the array or domain too.  The query's constant or method is still generated.

See: [Types](postgresql.md#type-map)

## SQLSRC247

**Npgsql is older than 8.0**

The project references a version of Npgsql older than 8.0, and SqlSource gives the queries of the `postgres` dialect the C# types of Npgsql 8.0 and later.  The message names the version the project references.

```console
CSC : error SQLSRC247: This project references Npgsql 7.0.4, and SqlSource needs Npgsql 8.0 or later.  Update the package.
```

Update the `Npgsql` package to 8.0 or later.  When another package brings the older version, reference `Npgsql` from the project itself, at the version you want.  The error has no file and line, because it is about the project's references, and it is reported once for the project, only while a query's output is `models` or `codegen` under the `postgres` dialect.

See: [Which version of Npgsql](postgresql.md#which-npgsql)

## SQLSRC248

**Npgsql is newer than SqlSource knows**

The project references Npgsql 11 or later, and this version of SqlSource knows the C# types of Npgsql up to version 10.  It gives the queries of the `postgres` dialect the types that Npgsql 10 reads, which are right unless the newer driver reads one of them as another type.  The message names the version the project references.

```console
info SQLSRC248: This project references Npgsql 11.0.0, which is newer than this version of SqlSource knows.  Its models use the types of Npgsql 10.
```

Update the `SqlSource` package when a version that knows the driver is released.  This is information, not a warning, so that a project that sets `TreatWarningsAsErrors` still builds on the day it updates Npgsql.  A project can list it in `NoWarn` to hide it:

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);SQLSRC248</NoWarn>
</PropertyGroup>
```

It has no file and line, and is reported once for the project, only while a query's output is `models` or `codegen` under the `postgres` dialect.  As information, it is not in the output of `dotnet build`.  An IDE that lists information shows it with the errors and warnings.  A build log has it at the detailed verbosity, `dotnet build -v:detailed -tl:off`, as the sample shows it, since the terminal logger leaves information out at any verbosity.  A binary log, `dotnet build -bl`, has it too.

See: [Which version of Npgsql](postgresql.md#which-npgsql)

## SQLSRC249

**Type needs Npgsql**

The project does not reference Npgsql, and a column or a parameter of the query has a type whose C# type depends on it.  It is either a type that Npgsql reads as a type of its own `NpgsqlTypes` namespace, such as `point`, `tsvector` or `int4range`, or a type that Npgsql 8 and 9 read as one C# type and Npgsql 10 as another: `date`, `time`, `cidr`, `daterange` and `datemultirange`, and an array or a domain of one of them.  Without a reference, SqlSource cannot tell which Npgsql the application runs on.  The message names the column or parameter and its type.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC249: The column 'born' has the type 'date', whose C# type depends on Npgsql, and this project does not reference it.  Reference Npgsql, or map it with the type override for the Npgsql the application runs on: date=System.DateTime on Npgsql 8 and 9, date=System.DateOnly on Npgsql 10.
```

Reference the `Npgsql` package from the project, at the version the application runs on: every type then gets the C# type of that version.  A project that keeps its queries apart from the driver can map each type that differs with the [type override](settings.md#setting-type-overrides) instead, for the version the application runs on, as the message says: `<SqlSourceTypeOverrides>date=System.DateOnly time=System.TimeOnly</SqlSourceTypeOverrides>` for Npgsql 10.  An entry for `date` maps an array or a domain of `date` too.  The message offers only C# types that a project without Npgsql can name: for `cidr`, `System.Net.IPNetwork` on Npgsql 10, since Npgsql 8 and 9 read it as a type of `NpgsqlTypes`.  `daterange` and `datemultirange` are read as a type of `NpgsqlTypes` on every version, so for them, as for every other type of the `NpgsqlTypes` namespace, the message says to reference Npgsql, which they need.  Every other type has the same C# type on every version of Npgsql, and needs neither.  The query's constant or method is still generated.

See: [Which version of Npgsql](postgresql.md#which-npgsql)

## SQLSRC250

**Mapped type is not found**

The type override maps a database type to a C# type that the project cannot find: no type of that name and number of type arguments is in the project or its references, or more than one reference has one that the project can see and the project has none.  A type of a reference that the project cannot see does not count, so a name that one reference has as `public` and another as `internal` is the public one.  The message names the column or parameter, the database type, the C# type and the place that wrote the entry: the property `SqlSourceTypeOverrides`, the metadata of the file, a `-- type-override:` marker in the file header or one of the query.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC250: The type override maps 'uuid' to 'MyApp.UserKey' for column 'id', in the property SqlSourceTypeOverrides, and no type of that name is found, or more than one reference has one that this project can see.  Correct the name, with its namespace and type arguments, or for a name that two references have, map to a type of this project.
```

Write the type's full name, from its namespace, with its type arguments: `MyApp.Ids.UserKey`, or `System.Collections.Generic.List<int>`.  A nested type is written with a `.` before its name, as in C#: `MyApp.Outer.Inner`.  When two references have a type of the name that the project can see, map the database type to a type of the project's own, which is always chosen first.  The query's constant or method is still generated.

See: [Type overrides](settings.md#setting-type-overrides)

## SQLSRC251

**Mapped type is not accessible**

The type override maps a database type to a C# type that the project cannot use: the type, or one of its type arguments, is `internal` in another assembly, or is a `private` or `protected` type nested in another, in the project itself or another assembly.  The message names the column or parameter, the database type, the C# type and the place that wrote the entry.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC251: The type override maps 'uuid' to 'Shared.UserKey' for column 'id', in a -- type-override: marker of the query, and this project cannot see that type.  Make it, and each type it is nested in, public, or internal when it is this project's own, or give this project its assembly's internals with InternalsVisibleTo.
```

Make the type, and each type it is nested in, `public`.  A type of the project itself can be `internal` instead.  For a type of another assembly, you can also add `[assembly: InternalsVisibleTo("YourProject")]` to the assembly that declares it, which lets the project see its `internal` types.  The query's constant or method is still generated.

See: [Type overrides](settings.md#setting-type-overrides)

## SQLSRC252

**Mapped type cannot be a property's type**

The type override maps a database type to a C# type that no property of a model can have: a `static` class, a `ref struct` such as `System.Span<T>`, `System.Void`, or `System.Nullable<T>` as the whole type.  A type argument or an array's element that is a `static` class, a `ref struct` or `System.Void` is refused too, and so is `System.Nullable<T>` with a `?` after it, as a type argument or an array's element: `System.Nullable<int>?[]`.  The message names the column or parameter, the database type, the C# type and the place that wrote the entry.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC252: The type override maps 'int4' to 'System.Nullable<int>' for column 'id', in the metadata SqlSourceTypeOverrides of the file, and no property can have that type.  Choose another type, and for System.Nullable<T> name T, since a column that can be null makes its property nullable.
```

Map the database type to another C# type.  For `System.Nullable<T>`, map it to `T`: a column that can be null already gets a nullable property, and one that cannot gets `T`.  The query's constant or method is still generated.

See: [Type overrides](settings.md#setting-type-overrides)

## SQLSRC253

**Column has no name**

A column of the query has no name, so its property in the output model would have none.  PostgreSQL names a column `?column?` when the query gives it no alias and it is no column of a table or call of a function: an expression such as `price * quantity`, or a literal.  The message gives the column's position, from 1, and the query.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC253: The column at position 2 of the query 'GetLines' has no name.  Give it an alias.
```

```sql error
-- name: GetLines
SELECT id, price * quantity FROM order_lines;
```

Give the column an alias, which names its property, then run `dotnet sqlsource describe`:

```sql postgres
-- name: GetLines
SELECT id, price * quantity AS total FROM order_lines;
```

The query's constant or method is still generated.

## SQLSRC254

**Name is not an identifier after PascalCase**

The PascalCase of a column's or a parameter's name, which names its property, is not a C# identifier: it is empty, as for a name of underscores alone, it starts with a digit, as `_1st` gives `1st`, or it holds a character that an identifier cannot, as `unit-price` does.  The message names the column or parameter and the property name it gives.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC254: The column 'unit-price' gives the property name 'Unit-price', which is not a C# identifier.  Give it an alias.
```

Give the column an alias that is a name a C# property can have, such as `AS unit_price`, or rename the parameter, then run `dotnet sqlsource describe`.  The query's constant or method is still generated.

## SQLSRC255

**Two names give one property name**

Two columns, or two parameters, of the query give their model one property name, ignoring case: the same column selected from two tables, as `SELECT u.id, o.id`, or two spellings of a name, as `user_id` beside `userId`.  Names are compared ignoring case, since Dapper matches a column to a property ignoring case, and would fill both properties from one column.  The message names both, with their positions from 1, and the property name.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC255: The column 'id' at position 1 and the column 'id' at position 2 give one property name, 'Id', ignoring case.  Give one of them an alias.
```

```sql error
-- name: GetUserOrders
SELECT u.id, o.id FROM users u JOIN orders o ON o.user_id = u.id;
```

Give one of the columns an alias, or rename one of the parameters, then run `dotnet sqlsource describe`:

```sql postgres
-- name: GetUserOrders
SELECT u.id AS user_id, o.id AS order_id FROM users u JOIN orders o ON o.user_id = u.id;
```

The query's constant or method is still generated.

## SQLSRC256

**Property name is taken**

A column or a parameter gives a property name that a record or `object` already has a member of: `EqualityContract`, `Equals`, `GetHashCode`, `ToString`, `GetType`, `MemberwiseClone`, `Finalize`, `ReferenceEquals`, `PrintMembers`, `Deconstruct` or `Clone`.  A column named `equals` or `to_string` is one.  The message names the column or parameter and the property name.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC256: The column 'to_string' gives the property name 'ToString', which a record or object already has.  Give it an alias.
```

Give the column an alias, or rename the parameter, then run `dotnet sqlsource describe`.  The query's constant or method is still generated.

## SQLSRC259

**Output model on a query without rows**

The query has an `-- output-model:` marker, which names the type of its rows, and its statement returns none, as an `INSERT`, `UPDATE` or `DELETE` without `RETURNING` does.  The error is at the marker.

```console
/work/App/Queries/Users.sql(2,1): error SQLSRC259: The query 'DeleteUser' names an output model, and its statement returns no rows.  Remove the -- output-model: marker.
```

```sql error
-- name: DeleteUser
-- output-model: DeletedUser
DELETE FROM users WHERE id = @id;
```

Remove the marker.  To read the rows the statement changes instead, add a `RETURNING` clause and run `dotnet sqlsource describe`.  The query's constant or method is still generated.
