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

Ids below 100 are about the type that carries `[SqlQueries]`.  Ids from 101 are about the contents of a `.sql` file.

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

Check the spelling, and that the path starts from the folder of this source file and not from the project.  If the file exists, check that the build sees it: a project that sets `EnableDefaultSqlSourceItems` to `false` must list each `.sql` file as an `AdditionalFiles` item.

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

In `Nested` mode the queries go in a nested class named `Sql`, and the type already has a member with that name.

```csharp
[SqlQueries]
public partial class UserRepository
{
    private string Sql { get; }
}
```

Rename the member, or set `Mode = SqlQueriesMode.Direct` so that the queries become members of the type itself.

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

## SQLSRC101

**Quote is not closed**

A string or a quoted identifier starts and never ends.  The error is at the opening quote.

```sql
SELECT 'unfinished FROM users;
```

Close the quote.  If the SQL is valid for your database, it uses a quoting form that SqlSource reads differently; see the dialect limits in the README.  Rewrite the construct in a form that SqlSource reads correctly.

## SQLSRC102

**Comment is not closed**

A block comment or a hint starts with `/*` and never ends.  The error is at the `/*`.  SqlSource nests block comments, as PostgreSQL does: each `/*` inside a comment needs its own `*/`.

```sql
/* outer /* inner */
SELECT 1;
```

Close the comment.  In a dialect that does not nest comments, remove the inner `/*`.

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

A `-- SqlSource:` marker holds a word that is not a directive.  The directives are `preserve-comments`, `token-validation`, `no-token-validation` and `token-ignore=name`.

```sql
-- SqlSource: keep-comments
```

Correct the directive.

## SQLSRC110

**Directive is missing**

A `-- SqlSource:` marker has nothing after the colon.

Add a directive, or delete the line.

## SQLSRC111

**Directive value is not valid**

`token-ignore` needs a value that is a C# identifier, as in `token-ignore=table`.  No other directive takes a value.

```sql
-- SqlSource: token-ignore
-- SqlSource: preserve-comments=true
```

Add the missing value, or remove the one that does not belong.

## SQLSRC112

**Directives conflict**

`token-validation` and `no-token-validation` both appear in one scope: both in the lines before the first `-- name:` marker, or both in one query.  The error is at the second.

Remove one of the two.  A directive in a query overrides the same directive before the first `-- name:` marker, and that is not a conflict.

## SQLSRC113

**Query has no SQL**

A query, or a whole file without `-- name:` markers, holds nothing but whitespace, comments and markers.

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
