# `.sql` files

```sql
-- name: GetUser
-- summary: Loads one user by id.
SELECT id, name
FROM users
WHERE id = @id;

-- name: ListUsers
SELECT id, name FROM users ORDER BY name;
```

A `.sql` file holds one or more queries.  Each query starts with a `-- name:` line, and the markers that describe the query stand under that line.

Save the file as UTF-8, which current editors write.  A file in another encoding reads differently on different systems.  The tool reports such a file when it must describe the file's queries ([SQLSRC271](../diagnostics.md#sqlsrc271)).

<a id="marker"></a>

## What a marker is

`-- name: GetUser` and `-- summary: Loads one user.` are markers.  A marker is a line comment that starts its line and has the form `-- word: rest`, where the word is one that SqlSource knows.  SqlSource reads nothing else in a comment.  The [marker index](#marker-index) lists every marker.

- **The word is matched ignoring case, and the blanks after `--` are optional.**  So `--NAME:GetUser` is a marker too.
- **The colon follows the word directly.**  `-- name : GetUser` is a comment, not a marker.
- **A comment after SQL on the same line is not a marker.**  A comment inside a block comment or a string is not a marker either.
- **Under `mysql` and `mariadb`, a marker needs white space after the two dashes.**  There, the two dashes start a comment only when white space follows them.  See [markers under `mysql` and `mariadb`](dialects.md#markers-under-mysql).

<a id="marker-index"></a>

## Marker index

A marker stands in one of two places.  A marker that describes one query stands **inside the query**, under its `-- name:` line.  A marker that describes the whole file stands in the [file header](#the-file-header), before the first `-- name:` line.  A file with no `-- name:` line is one query, and it takes both kinds.

| Marker | Purpose | File header | Inside a query | Documented in |
|----|----|----|----|----|
| `-- name:` | Starts a query and names it, with an optional result shape | no | starts one | [Queries and names](#queries-and-names) |
| `-- summary:` | Documents the query's generated constant or method, and its models | no | yes | [Summaries](#summaries) |
| `-- dialect:` | The dialect of the file, which you set to your database: `postgres` or `mssql`.  The other names, the options and the reading rules are on [the dialects page](dialects.md#choosing-a-dialect) | yes | no | [Dialect](settings.md#setting-dialect) |
| `-- generator:` | Generator parameters | yes | yes | [Generator parameters](generator-parameters.md#setting-generator-parameters) |
| `-- param:` | Gives a query parameter its database type, whether it can be null, and, after `->`, a C# type of your choice: `-- param: @id int -> UserId`, or `-> json OrderMetadata` for a parameter of a string type that holds JSON | no | yes | [Giving a parameter a type](parameters.md#param-marker), [the JSON opt-in](library-types-and-json.md#json-opt-in) |
| `-- token:` | Gives a token a default | no | yes | [Defaults](tokens.md#token-marker) |
| `-- token-ignore:` | Keeps text that looks like a token as it is written | no | yes | [Text that looks like a token](tokens.md#token-ignore) |
| `-- database:` | The logical database a query belongs to | yes | yes | [Database](settings.md#setting-database) |
| `-- output:` | What is generated for a query: its models and an instance method of the type that gets its own connection and runs it (`repository`, the default), its models and a method that takes a connection and runs it (`methods`), its SQL and its models (`models`), or its SQL alone (`sql`).  It also decides which queries must be described, by the tool and in the build's check of the sidecars | yes | yes | [Output](settings.md#setting-output) |
| `-- input-model-suffix:`, `-- output-model-suffix:` | What ends the name of a model | yes | no | [Suffix of a model's name](models.md#setting-model-suffix) |
| `-- model-namespace:` | The namespace of the models | yes | no | [Namespace of the models](models.md#setting-model-namespace) |
| `-- input-model-type:`, `-- output-model-type:` | The shape of a model | yes | yes | [Shape of a model](models.md#setting-model-type) |
| `-- input-model:`, `-- output-model:` | The name of one query's model | no | yes | [Name of one query's model](models.md#setting-model-name) |
| `-- collection-type:` | The type that a query's method returns many rows in | yes | yes | [Collection type](return-types.md#setting-collection-type) |
| `-- member-location:` | Where a query's members go: in a class nested in the type (`nested-class`), on the type (`class`), or, for a `methods` query's method, in the extension class (`extension-class`) | yes | yes | [Where the members go](member-placement.md#setting-member-location) |
| `-- visibility:` | How visible a query's members are, or its nested class: `public`, `internal`, `protected` or `private` | yes | yes | [Visibility](member-placement.md#setting-visibility) |
| `-- nested-class:` | The name of the class nested in the type that holds a query's members | yes | yes | [Name of the nested class](member-placement.md#setting-nested-class) |
| `-- type-override:` | Gives a database type the C# type of your choice, one entry for each line | yes | yes | [Type overrides](type-mapping.md#setting-type-overrides) |
| `-- column:` | Inside a query, says whether one column of the query's result can be null, `-- column: total not null`, or gives it a C# type after `->`, `-- column: total -> PostCount`, for its property in the output model, and leaves the column's name alone.  In the file header, gives a column of a table or view a C# type in every query of the file, `-- column: users.id -> UserId`, one entry for each line.  `-> json OrderMetadata` gives a column of a string type that holds JSON a class of yours | yes | yes | [Setting a column's nullability or type with a marker](model-properties.md#column-marker), [column types](model-properties.md#setting-column-types), [the JSON opt-in](library-types-and-json.md#json-opt-in) |
| `-- library-types:` | Turns a library's types, such as NodaTime's or System.Text.Json's, on or off for the query methods, `-- library-types: nodatime=off`, one entry or more for each line | yes | yes | [Library types](library-types-and-json.md#setting-library-types) |

A marker that is a setting links to its section.  [The settings page](settings.md#all-settings) says where else you can give the setting, and which place wins.  A marker in a place whose column says "no" is an error: see [markers in the wrong place](#markers-in-the-wrong-place).

<a id="queries-and-names"></a>

## Queries and names

```sql
-- name: GetUser
SELECT id, name FROM users WHERE id = @id;

-- name: DeleteUser
DELETE FROM users WHERE id = @id;
```

A query is the SQL that follows a `-- name:` line, up to the next `-- name:` line or the end of the file.  A file with no `-- name:` line is one query: see [a file with no `-- name:`](#a-file-with-no-name).  Each query becomes one or more members of the generated code, named after the query.  So the name of a query follows these rules:

- **The name is a C# identifier that is not a reserved keyword, such as `class`.**  Any other name is [SQLSRC103](../diagnostics.md#sqlsrc103).  So `get-user` is not a name.
- **Names are case-sensitive.**  `GetUser` and `getUser` are two queries.
- **A file uses each name once.**  A name that a file uses twice is [SQLSRC104](../diagnostics.md#sqlsrc104).
- **The files of one type use each name once.**  A name that two files of one type use is [SQLSRC008](../diagnostics.md#sqlsrc008).
- **The name of a query is not the name of the class that holds its members** ([SQLSRC009](../diagnostics.md#sqlsrc009)).  That class is one of these:
  - for a query of any output, its nested class: `Sql`, unless [you name it otherwise](member-placement.md#setting-nested-class);
  - the type itself, when the constant or token method of a query whose output is `models` or `sql` goes on it.
- **A query method cannot have some other names.**  They are listed with [a repository method's signature](repositories.md#repository-signature), and with where [the `methods` methods go](connection-methods.md#method-location).  See [where the members go](member-placement.md#locations).
- **A query has SQL in it.**  A query that holds only comments and markers is [SQLSRC113](../diagnostics.md#sqlsrc113).

A `-- name:` marker can be followed by `->` and a result shape: see [result shapes](#result-shapes).  The name ends at the `->`.

<a id="result-shapes"></a>

## Result shapes

```sql
-- name: GetUser -> one-optional
SELECT id, name FROM users WHERE id = @id;

-- name: RenameUser -> none
UPDATE users SET name = @name WHERE id = @id;
```

A result shape is one of five words after `->` on a `-- name:` line.  It says what the [method](query-methods.md#query-methods) of a query whose output is `repository` or `methods` returns:

| Shape | The method returns |
|----|----|
| `many` | Every row, in the [collection type](return-types.md#setting-collection-type), an array by default |
| `one` | The one row, and throws when there are none or more than one |
| `one-optional` | The one row, or null when there is none, and throws when there are more than one |
| `none` | Nothing |
| `rowcount` | The number of rows the statement affected |

[What each shape returns](return-types.md#method-shapes) has the details.

- **Without a shape, a query that returns rows is `many`.**  A query that returns no rows is `rowcount`.
- **`one`, `one-optional` and `many` need a statement that returns rows.**  On a query whose statement returns no rows, each of them is [SQLSRC272](../diagnostics.md#sqlsrc272).  `none` and `rowcount` go on any query.
- **A shape changes nothing under the output `models` or `sql`.**
- **Adding or changing a shape does not make the query's sidecar out of date.**
- **The case of a shape is ignored.**
- **After `->`, a word that is not one of the five is [SQLSRC111](../diagnostics.md#sqlsrc111).**  So is a `->` with no word after it.
- **Text after a name with no `->` is part of the name.**  So `-- name: Get User` gives a name with a space in it, which is [SQLSRC103](../diagnostics.md#sqlsrc103).
- **A file with no `-- name:` line has no place to write a shape.**

<a id="summaries"></a>

## Summaries

```sql
-- name: GetUser
-- summary: Loads one user by id.
-- summary: Returns nothing when there is no such user.
SELECT id, name FROM users WHERE id = @id;
```

A `-- summary:` line inside a query becomes the documentation of the query's member, and of its [models](models.md#model-documentation).  IntelliSense shows the documentation of the member beside the query's SQL.

- **Several summaries are joined with a space.**
- **A summary with no text is skipped.**
- **The text is written as it is.**  So `<` and `&` appear as you typed them.
- **A query without a summary is documented with its name and its file**: "The `GetUser` query from `Users.sql`."
- **A `-- summary:` before the first `-- name:` line belongs to no query.**  It is [SQLSRC107](../diagnostics.md#sqlsrc107).

<a id="the-file-header"></a>

## The file header

```sql
-- Licensed under the MIT licence.
-- dialect: postgres
-- generator: keep-comments

-- name: GetUser
SELECT id, name FROM users WHERE id = @id;
```

The file header is the part of a `.sql` file before its first `-- name:` line.  It can hold comments and markers:

- **Comments, such as a licence.**  A comment in the header is never part of the SQL of a query, even of a query that keeps its comments.
- **The markers whose "File header" column in the [index](#marker-index) says yes.**  A marker that is a setting applies to every query in the file, unless a query gives its own.  [The settings page](settings.md#precedence) says which place wins when you give a setting in several places.

SQL does not belong in the header.  SQL before the first `-- name:` line belongs to no query, and is [SQLSRC106](../diagnostics.md#sqlsrc106).

The `-- dialect:` marker also goes before the file's first SQL.  A file has one dialect, and the marker changes how SqlSource reads the text after it: see [dialects](settings.md#setting-dialect).

<a id="a-file-with-no-name"></a>

## A file with no `-- name:`

```sql
-- summary: Counts the users.
SELECT COUNT(*) FROM users;
```

A file with no `-- name:` line is one query, named after the file: `CountUsers.sql` holds the query `CountUsers`.

- **The name is the file's name without its extension.**  It must be a name as [queries and names](#queries-and-names) describes.  `get-user.sql` and `001_init.sql` are not names, and are [SQLSRC105](../diagnostics.md#sqlsrc105).  To fix it, do one of these:
  - add a `-- name:` line;
  - rename the file;
  - keep the file out of the files that the type claims.
- **A file that has a `-- name:` line ignores its own name.**
- **The file is its own header and its own query, so it takes every marker.**  The markers stand above the SQL or between its lines.
- **A marker after the last SQL of the file is [SQLSRC108](../diagnostics.md#sqlsrc108).**

<a id="generated-sql"></a>

## What reaches the generated SQL

Hover over a generated member to see the SQL that it holds.  It is the SQL you wrote, with these changes:

- **Every marker line is removed.**
- **Comments are removed.**  A line comment is deleted, and a block comment becomes one space.
- **Blank lines are removed, and so are the blanks at the end of a line.**  Indentation and the spacing inside a line stay.
- **The `keep-comments` generator parameter keeps comments, and the blank lines between lines of SQL.**  See [generator parameters](generator-parameters.md#setting-generator-parameters).
- **Optimizer hints are kept**: `/*+ ... */` and `/*! ... */`.  Where the dialect is theirs, MariaDB's `/*M! ... */` and Oracle's `--+ ...` are kept too.
- **Strings and quoted identifiers are copied exactly as written.**  Where one starts and ends depends on the dialect: see [dialects](dialects.md#what-a-dialect-changes).
- **Every line ending becomes `\n`.**  So the SQL does not depend on how the file was checked out.
- **A query parameter such as `@id` stays as written.**
- **A token such as `{{table}}` is replaced** when your code asks for the SQL, or when the query's method runs it.
- **A [list](list-parameters.md#list-parameters), `IN @ids`, is written as one parameter for each value** when the query's method runs it.  See [list parameters](list-parameters.md#list-parameters).

<a id="markers-in-the-wrong-place"></a>

## Markers in the wrong place

```sql error
-- name: GetUser
SELECT 1;
-- summary: Lists every user.
-- name: ListUsers
SELECT 2;
```

The summary was meant for `ListUsers`, but it follows the last SQL of `GetUser`.  To a reader, a marker after the last SQL of a query looks as if it belongs to the next query, so it is refused.  Move the summary under the `-- name:` line of `ListUsers`.

A marker goes before the SQL that it describes.  These are the mistakes, and the error that each one is:

| Mistake | Error |
|----|----|
| A marker where its column in the [index](#marker-index) says "no", other than the two cases below: one for the whole file inside a query, or one for a query in the file header | [SQLSRC116](../diagnostics.md#sqlsrc116) |
| A `-- summary:` before the first `-- name:` line | [SQLSRC107](../diagnostics.md#sqlsrc107) |
| A `-- dialect:` inside a query or after SQL | [SQLSRC115](../diagnostics.md#sqlsrc115) |
| SQL before the first `-- name:` line | [SQLSRC106](../diagnostics.md#sqlsrc106) |
| A marker after the last SQL of its query | [SQLSRC108](../diagnostics.md#sqlsrc108) |
| A marker whose value its setting does not take | [SQLSRC111](../diagnostics.md#sqlsrc111) |
| The same marker twice in one place with two different values | [SQLSRC112](../diagnostics.md#sqlsrc112) |

- **The same marker twice in one place with the same value is not an error.**
- **The words of several `-- generator:` lines in one place add up.**  A query's words also add to those of its file header: see [generator parameters](generator-parameters.md#setting-generator-parameters).
- **A comment of yours can be read as a marker.**  A comment that starts with one of the words and a colon, such as `-- summary: see the ticket`, is a marker.  Reword it.

An error in how a `.sql` file is written, such as a marker in the wrong place, is reported at its line and column in that file.  Until you fix it, the file gives no members.  An error about describing a query, or about its sidecar, costs that query its models.  See [what an error costs](errors.md#what-an-error-costs), [which queries get a method](query-methods.md#which-methods) and [diagnostics](../diagnostics.md).
