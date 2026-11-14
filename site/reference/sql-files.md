# `.sql` files

A `.sql` file holds one or more queries.  Save it as UTF-8, which current editors write: a file in another encoding reads differently on different systems, and the tool reports one whose queries it must describe ([SQLSRC271](../diagnostics.md#sqlsrc271)).  Each query starts with a `-- name:` line, and the markers that describe it stand under that line:

```sql
-- name: GetUser
-- summary: Loads one user by id.
SELECT id, name
FROM users
WHERE id = @id;

-- name: ListUsers
SELECT id, name FROM users ORDER BY name;
```

<a id="marker"></a>

## What a marker is

A [marker](../concepts.md#marker) is a line comment that starts its line and has the form `-- word: rest`, where the word is one SqlSource knows.  Nothing else in a comment is read.  The words are matched ignoring case, the blanks after `--` are optional, and the colon follows the word directly: `-- name: GetUser` and `--NAME:GetUser` are markers, `-- name : GetUser` is a comment.  A comment after SQL on the same line, or inside a block comment or a string, is not a marker either.  Under `mysql` and `mariadb` the two dashes need a space after them to start a comment at all, so there a marker needs one too: see [markers under `mysql` and `mariadb`](dialects.md#markers-under-mysql).

<a id="marker-index"></a>

## Marker index

A marker stands in one of two places.  A marker that describes one query stands **inside the query**, under its `-- name:` line.  A marker that describes the whole file stands in the [file header](../concepts.md#file-header), before the first `-- name:` line.  A file with no `-- name:` line is one query and takes both kinds.

| Marker | Purpose | File header | Inside a query | Documented in |
|----|----|----|----|----|
| `-- name:` | Starts a query and names it, with an optional result shape | no | starts one | [Queries and names](#queries-and-names) |
| `-- summary:` | Documents the query's generated constant or method, and its models | no | yes | [Summaries](#summaries) |
| `-- dialect:` | The dialect the file is read by, and its options.  The names and the reading rules are on [the dialects page](dialects.md#choosing-a-dialect) | yes | no | [Dialect](../settings.md#setting-dialect) |
| `-- generator:` | Generator parameters | yes | yes | [Generator parameters](../settings.md#setting-generator-parameters) |
| `-- param:` | Gives a query parameter its database type, whether it can be null, and, after `->`, a C# type of your choice: `-- param: @id int -> UserId`, or `-> json OrderMetadata` for a parameter of a string type that holds JSON | no | yes | [Giving a parameter a type](parameters.md#param-marker), [the JSON opt-in](../settings.md#json-opt-in) |
| `-- token:` | Gives a token a default | no | yes | [Defaults](tokens.md#token-marker) |
| `-- token-ignore:` | Keeps text that looks like a token as it is written | no | yes | [Text that looks like a token](tokens.md#token-ignore) |
| `-- database:` | The logical database a query belongs to | yes | yes | [Database](../settings.md#setting-database) |
| `-- output:` | What is generated for a query: its SQL alone (`sql`), its SQL and its models (`models`), its models and a method that takes a connection and runs it (`methods`), or its models and an instance method of the type that gets its own connection and runs it (`repository`, the default).  It also decides which queries must be described, by the tool and in the build's check of the sidecars | yes | yes | [Output](../settings.md#setting-output) |
| `-- input-model-suffix:`, `-- output-model-suffix:` | What ends the name of a model | yes | no | [Suffix of a model's name](../settings.md#setting-model-suffix) |
| `-- model-namespace:` | The namespace of the models | yes | no | [Namespace of the models](../settings.md#setting-model-namespace) |
| `-- input-model-type:`, `-- output-model-type:` | The shape of a model | yes | yes | [Shape of a model](../settings.md#setting-model-type) |
| `-- input-model:`, `-- output-model:` | The name of one query's model | no | yes | [Name of one query's model](../settings.md#setting-model-name) |
| `-- collection-type:` | The type that a query's method returns many rows in | yes | yes | [Collection type](../settings.md#setting-collection-type) |
| `-- member-location:` | Where a query's members go: in a class nested in the type (`nested-class`), on the type (`class`), or, for a `methods` query's method, in the extension class (`extension-class`) | yes | yes | [Where the members go](../settings.md#setting-member-location) |
| `-- visibility:` | How visible a query's members are, or its nested class: `public`, `internal`, `protected` or `private` | yes | yes | [Visibility](../settings.md#setting-visibility) |
| `-- nested-class:` | The name of the class nested in the type that holds a query's members | yes | yes | [Name of the nested class](../settings.md#setting-nested-class) |
| `-- type-override:` | Gives a database type the C# type of your choice, one entry for each line | yes | yes | [Type overrides](../settings.md#setting-type-overrides) |
| `-- column:` | Inside a query, says whether one column of the query's result can be null, `-- column: total not null`, or gives it a C# type after `->`, `-- column: total -> PostCount`, for its property in the output model, and leaves the column's name alone.  In the file header, gives a column of a table or view a C# type in every query of the file, `-- column: users.id -> UserId`, one entry for each line.  `-> json OrderMetadata` gives a column of a string type that holds JSON a class of yours | yes | yes | [Setting a column's nullability or type with a marker](../generator.md#column-marker), [column types](../settings.md#setting-column-types), [the JSON opt-in](../settings.md#json-opt-in) |
| `-- library-types:` | Turns a library's types, such as NodaTime's or System.Text.Json's, on or off for the query methods, `-- library-types: nodatime=off`, one entry or more for each line | yes | yes | [Library types](../settings.md#setting-library-types) |

A marker that is a setting links to its section of [the settings page](../settings.md#all-settings), which also says where else the setting can be given and which place wins.  A marker where its column says "no" is an error: see [markers in the wrong place](#markers-in-the-wrong-place).

<a id="queries-and-names"></a>

## Queries and names

```sql
-- name: GetUser
SELECT id, name FROM users WHERE id = @id;

-- name: DeleteUser
DELETE FROM users WHERE id = @id;
```

A `-- name:` line begins a query.  The query runs to the next `-- name:` line or to the end of the file.  Its name becomes the name of its member in the generated code, so:

- The name is a C# identifier, and not a reserved keyword such as `class`.  Anything else is [SQLSRC103](../diagnostics.md#sqlsrc103).  `get-user` is not a name.
- Names are case-sensitive: `GetUser` and `getUser` are two queries.
- A name is used once in a file ([SQLSRC104](../diagnostics.md#sqlsrc104)), and once among all the files of one type ([SQLSRC008](../diagnostics.md#sqlsrc008)).
- The name of a query is not the name of the class its members go in: its nested class, `Sql` unless [it is named otherwise](../settings.md#setting-nested-class), for a query of any output, or the type itself when the constant or token method of a query whose output is `sql` or `models` goes on it ([SQLSRC009](../diagnostics.md#sqlsrc009)).  The other names a query method cannot have are with [a repository method's signature](../generator.md#repository-signature) and where [the `methods` methods go](../generator.md#method-location).  See [the generator](../generator.md#locations).
- The query has SQL in it.  A query that holds only comments and markers is [SQLSRC113](../diagnostics.md#sqlsrc113).

The marker may be followed by `->` and a result shape: see [result shapes](#result-shapes).  The name ends at the `->`.

<a id="result-shapes"></a>

## Result shapes

```sql
-- name: GetUser -> one-optional
SELECT id, name FROM users WHERE id = @id;

-- name: RenameUser -> none
UPDATE users SET name = @name WHERE id = @id;
```

A name can be followed by `->` and one of five shapes, which say what the [method](../generator.md#query-methods) of a query whose output is `methods` or `repository` returns:

| Shape | The method returns |
|----|----|
| `many` | Every row, in the [collection type](../settings.md#setting-collection-type), an array by default |
| `one` | The one row, and throws when there are none or more than one |
| `one-optional` | The one row, or null when there is none, and throws when there are more than one |
| `none` | Nothing |
| `rowcount` | The number of rows the statement affected |

Without a shape, a query that returns rows is `many`, and one that returns none is `rowcount`.  `one`, `one-optional` and `many` on a query whose statement returns no rows are [SQLSRC272](../diagnostics.md#sqlsrc272); `none` and `rowcount` go on any query.  [What each shape returns](../generator.md#method-shapes) has the details.  A shape changes nothing under the output `sql` or `models`, and adding or changing one does not make the query's sidecar out of date.

The case of a shape is ignored.  A word after `->` that is not one of the five, or no word at all, is [SQLSRC111](../diagnostics.md#sqlsrc111).  Text after a name with no `->`, as in `-- name: Get User`, is read as part of the name and is [SQLSRC103](../diagnostics.md#sqlsrc103).  A file with no `-- name:` line has no place to write a shape.

<a id="summaries"></a>

## Summaries

```sql
-- name: GetUser
-- summary: Loads one user by id.
-- summary: Returns nothing when there is no such user.
SELECT id, name FROM users WHERE id = @id;
```

A `-- summary:` line inside a query becomes the documentation of its member, which IntelliSense shows beside the query's SQL, and of its [models](../generator.md#model-documentation).  Several are joined with a space.  A summary with no text is skipped, and the text is written as it is, so `<` and `&` appear as typed.

A query without a summary is documented with its name and its file: "The `GetUser` query from `Users.sql`."

A `-- summary:` before the first `-- name:` line belongs to no query and is [SQLSRC107](../diagnostics.md#sqlsrc107).

<a id="the-file-header"></a>

## The file header

```sql
-- Licensed under the MIT licence.
-- dialect: postgres
-- generator: keep-comments

-- name: GetUser
SELECT id, name FROM users WHERE id = @id;
```

The [file header](../concepts.md#file-header) is the part of a file before its first `-- name:` line.  It may hold:

- Comments, such as a licence.  Comments in the header are never part of any query's SQL, even for a query that keeps its comments.
- The markers whose "File header" column in the [index](#marker-index) says yes.  Those that are settings apply to every query in the file, unless a query gives its own.  [The settings page](../settings.md#precedence) says which place wins when a setting is given in several.

SQL does not belong there: SQL before the first `-- name:` line belongs to no query and is [SQLSRC106](../diagnostics.md#sqlsrc106).

The `-- dialect:` marker also goes before the file's first SQL.  A file has one dialect, and the marker affects how the text after it is read: see [dialects](../settings.md#setting-dialect).

<a id="a-file-with-no-name"></a>

## A file with no `-- name:`

```sql
-- summary: Counts the users.
SELECT COUNT(*) FROM users;
```

A file with no `-- name:` line is one query, and it is named after the file: `CountUsers.sql` holds the query `CountUsers`.  The name is the file's name without its extension, and it must be a name as [queries and names](#queries-and-names) describes.  `get-user.sql` and `001_init.sql` are not, and are [SQLSRC105](../diagnostics.md#sqlsrc105): add a `-- name:` line, rename the file, or keep the file out of the type's files.  A file that has a `-- name:` line ignores its own name.

Such a file is its own header and its own query, so it takes every marker.  The markers stand above the SQL or between its lines.  One after the last SQL of the file is [SQLSRC108](../diagnostics.md#sqlsrc108).

<a id="generated-sql"></a>

## What reaches the generated SQL

Hover over a generated member to see the SQL it holds.  It is the SQL you wrote, with this done to it:

- Every marker line is removed.
- Comments are removed.  A line comment is deleted, and a block comment becomes one space.  Blank lines are removed, and so are the blanks at the end of a line.  Indentation and the spacing inside a line stay.  The `keep-comments` generator parameter keeps comments and the blank lines between lines of SQL: see [generator parameters](../settings.md#setting-generator-parameters).
- Optimizer hints are kept: `/*+ ... */` and `/*! ... */`, and where the dialect is theirs, MariaDB's `/*M! ... */` and Oracle's `--+ ...`.
- Strings and quoted identifiers are copied exactly as written.  Where one starts and ends depends on the dialect: see [dialects](dialects.md#what-a-dialect-changes).
- Every line ending becomes `\n`, so the SQL does not depend on how the file was checked out.
- A query parameter such as `@id` stays as written.  A token such as `{{table}}` is replaced when your code asks for the SQL, or when the query's method runs it, and a [list](parameters.md#list-parameters), `IN @ids`, is written as one parameter for each value when the query's method runs it: see [parameters and tokens](parameters.md#list-parameters).

<a id="markers-in-the-wrong-place"></a>

## Markers in the wrong place

A marker goes before the SQL it describes.  These are the mistakes, and what each one is reported as:

| Mistake | Error |
|----|----|
| A marker where its column in the [index](#marker-index) says "no", other than the two cases below: one for the whole file inside a query, or one for a query in the file header | [SQLSRC116](../diagnostics.md#sqlsrc116) |
| A `-- summary:` before the first `-- name:` line | [SQLSRC107](../diagnostics.md#sqlsrc107) |
| A `-- dialect:` inside a query or after SQL | [SQLSRC115](../diagnostics.md#sqlsrc115) |
| SQL before the first `-- name:` line | [SQLSRC106](../diagnostics.md#sqlsrc106) |
| A marker after the last SQL of its query | [SQLSRC108](../diagnostics.md#sqlsrc108) |
| A marker whose value its setting does not take | [SQLSRC111](../diagnostics.md#sqlsrc111) |
| The same marker twice in one place with two different values | [SQLSRC112](../diagnostics.md#sqlsrc112) |

A marker that follows the last SQL of a query looks to a reader as if it belongs to the next query, so it is refused:

```sql error
-- name: GetUser
SELECT 1;
-- summary: Lists every user.
-- name: ListUsers
SELECT 2;
```

The summary was meant for `ListUsers`.  Move it under that query's `-- name:` line.

Writing a marker twice in one place is fine when the value is the same.  The words of several `-- generator:` lines in one place add up, and a query's add to its file header's: see [generator parameters](../settings.md#setting-generator-parameters).

A comment of yours that happens to start with one of the words and a colon, such as `-- summary: see the ticket`, is read as a marker.  Reword it.

An error in how a `.sql` file is written, such as a marker in the wrong place, is reported at its line and column in that file, and the file produces no members until it is fixed.  An error about describing a query or about its sidecar leaves the query without models: under the output `models` it keeps its constant or token method, and under `methods` or `repository` it gets no method until it is fixed.  See [which queries get a method](../generator.md#which-methods), [what an error costs](errors.md#what-an-error-costs) and [diagnostics](../diagnostics.md).
