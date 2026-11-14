# Model properties: names, column types, nullable

For the order in which SqlSource chooses the C# type of a property, see [how a C# type is chosen](type-mapping.md#property-types).

<a id="property-order"></a>

## The order of the properties

The input model has a property for each [query parameter](../concepts.md#query-parameter), in the order of the query's [parameter list](parameters.md#query-parameters): the order in which the parameters first appear in the SQL, then those that only a `-- param:` marker declares.  The output model has a property for each column, in the order of the select list.

The [generator parameters](generator-parameters.md#setting-generator-parameters) `sort-input` and `sort-output` sort the properties of the input model or of the output model by name, character by character by their codes, so that upper case comes before lower case: `BX` before `Ba`.  A record's parameters are in the same order, so a sorted record no longer matches its columns by position and name.  A [query method](query-methods.md#reading-rows) reads a sorted model as well as any other; to read rows into sorted models through Dapper, make them classes, as [the recipe](../recipes.md#models-through-dapper) shows.

<a id="property-names"></a>

## Property names

| Column or parameter | Property |
|----|----|
| `created_at` | `CreatedAt` |
| `userId` | `UserId` |
| `ID` | `ID` |
| `api_v2_url` | `ApiV2Url` |
| `total!` | `Total` |

A property's name is the PascalCase of its column's or its parameter's name: the name is split at each `_`, the first character of each part is made upper case, the rest of it is kept as it is, and the parts are joined.  A column's name first loses one `!` or `?` at its end, which sets its [nullability](#model-nullability).  A parameter's name is the one the SQL first writes it with, without its `@`: `@accountId` gives `AccountId`.

Each of these is an error at the query's name, and the query has no models until it is fixed, nor a method under `methods` or `repository`; under `models` its constant or token method is still generated.  Give the column an alias, or rename the parameter, and describe the query again.

- **A column with no name**, which PostgreSQL calls `?column?`, as an expression `price * quantity` without an alias: [SQLSRC253](../diagnostics.md#sqlsrc253).  SQL Server gives such a column no name, and the tool reports it when it describes the query, as [SQLSRC266](../diagnostics.md#sqlsrc266).
- **A name whose PascalCase is not a C# identifier**: one of underscores alone, one that starts with a digit, or one with a character that an identifier cannot hold, such as `unit-price`: [SQLSRC254](../diagnostics.md#sqlsrc254).
- **Two columns, or two parameters, whose properties have one name ignoring case**, as `SELECT u.id, o.id`, or `user_id` beside `userId`: [SQLSRC255](../diagnostics.md#sqlsrc255).  Names are compared ignoring case, since Dapper matches a column to a property ignoring case.
- **A name that a record or `object` already has a member of**, `EqualityContract`, `Equals`, `GetHashCode`, `ToString`, `GetType`, `MemberwiseClone`, `Finalize`, `ReferenceEquals`, `PrintMembers`, `Deconstruct` or `Clone`, or the name of the model itself: [SQLSRC256](../diagnostics.md#sqlsrc256).

<a id="model-nullability"></a>

## Which properties can be null

```sql postgres
-- name: GetCustomerLabels
SELECT id, initcap(name) AS "display_name!"
FROM customers;
```

A property that can be null has its C# type with `?`: `int?`, which is `System.Nullable<int>`, or `string?`, a nullable reference.

- **A column's property can be null when the tool recorded that the column can be.**  Which columns can be null says when that is, for [PostgreSQL](databases/postgresql/nullability.md#nullability) and for [SQL Server](databases/sqlserver/nullability.md#nullability).  A column that the tool showed is never null gets a property that cannot be null.
- **A column whose name ends in `!` gets a property that cannot be null, and one whose name ends in `?` a property that can**, whatever the tool recorded.  Write the mark at the end of the column's alias, in quotes.  Above, `initcap` is never null for a value that is not, though SqlSource treats it as possibly null: `"display_name!"` gives the property `DisplayName` the type `string` and not `string?`.  A `!` is your word that the column is never null, and the build does not check it: a [query method](query-methods.md#reading-rows) throws when such a column is null after all.  The mark stays in the name of the column the query returns, `display_name!`.  A query method reads the columns by position, so the name does not matter to it, but a library that matches a row's columns to properties by name, as Dapper does, does not match it to `DisplayName`: with a record model Dapper throws when it reads the rows, and with a class model it leaves the property at its default value.  Read such rows through Dapper with a [`-- column:` marker](#column-marker) instead.
- **A column whose `-- column:` marker says `null` or `not null` gets that nullability**, whatever the tool recorded, and keeps its name: see below.
- **A parameter's property can be null only when its [`-- param:` marker](parameters.md#param-marker) says `null`**, or, for a [list parameter](parameters.md#list-parameters), when the query tests the list for null, as `@ids IS NULL` does.

<a id="column-marker"></a>

### Setting a column's nullability or type with a marker

```sql postgres
-- name: GetCustomerLabels
-- column: display_name not null
-- column: order_count -> OrderCount
SELECT c.id, initcap(c.name) AS display_name, count(o.id) AS order_count
FROM customers AS c
LEFT JOIN orders AS o ON o.customer_id = c.id
GROUP BY c.id, c.name;
```

A `-- column:` marker inside a query sets the nullability of one of its columns, or gives it a C# type of your choice, and leaves the column's name alone.  Above, the query returns the column `display_name`, as it would without the marker, and its property `DisplayName` has the type `string` and not `string?`; `OrderCount`, a `bigint` that no table gives, has the type `OrderCount`, a type of your project.  Use the marker for the nullability where the rows are read by the names of their columns, as Dapper reads them: a mark at the end of an alias changes the column's name, and the marker does not.

- **The value is the column's name, then `null` or `not null`, `->` and a C# type, or both in that order**: `-- column: note null -> NoteText`, or `-- column: settings null -> json CustomerSettings` with the word `json` before the type.  `null` gives a property that can be null, and `not null` one that cannot.  One marker names one column: write one for each.  Two markers for one column are fine when they say the same, or when one gives the nullability and the other the type, and [SQLSRC112](../diagnostics.md#sqlsrc112) when they give two nullabilities or two C# types, or one type with and without `json`.
- **`-> json Type` says that the column holds JSON**, for a column of a string type, such as `text` or `nvarchar(max)`, whose class of yours is then read through System.Text.Json's serializer: see [the JSON opt-in](library-types-and-json.md#json-opt-in).
- **`->` gives the column that C# type**, and `-> default` takes the column out of every column type, so that the type override, or else a library or the type map, gives its type: which wins is in [column types](#setting-column-types).  The type is written as a type override's is, a keyword, a full name or a short name, and the column keeps its nullability: `-> OrderCount` on a column that can be null gives `OrderCount?`.  A query method reads the type through your [converters](converters.md#converters) when it has them.
- **The name is the column's as the query returns it**, its alias or its own name, compared ignoring case.  Write it in double quotes when it has white space or a `.`, or when it is the word `null` or `not`, and double a quote inside it: `-- column: "total due" not null`.  A value that is not one name, plain or quoted, followed by `null` or `not null`, a `->` and a type, or both, is [SQLSRC111](../diagnostics.md#sqlsrc111): so is `-- column: total due not null`, without its quotes, and so is a name with a `.` outside quotes, `-- column: users.id -> UserId`, since a column's source is mapped in the file header.
- **A marker that names no column of the query's result is [SQLSRC260](../diagnostics.md#sqlsrc260)**, and so is one on a query that returns no rows, and one that says `null` or `not null` of a column whose alias already ends in `!` or `?`, whose nullability is then said twice.  A marker that gives a type alone may name such a column.  The query gets no models until it is fixed.
- **The marker is a comment.**  Adding, changing or removing one, or its type, does not change the query, so it does not need describing again.  A query whose output is `sql` has no models, and its marker has no effect.
- **In the file header**, `-- column:` names the column of a table or view that a value is read from, `-- column: users.id -> UserId`, and gives it a type in every query of the file: see [column types](#setting-column-types).

<a id="setting-column-types"></a>

## Column types

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "columnTypes": {
        "users.id": "UserId",
        "posts.created_by_user_id": "UserId",
        "billing.invoices.id": "MyApp.Billing.InvoiceId",
    },
}
```

```sql postgres
-- column: posts.id -> PostId

-- name: ListPosts
-- column: total -> PostCount
-- param: @author -> UserId
SELECT p.id, p.created_by_user_id, count(*) AS total
FROM posts AS p
WHERE p.created_by_user_id = @author
GROUP BY p.id;
```

A column type gives one column of a table or view the C# type of your choice, in every query that reads it, where a [type override](type-mapping.md#setting-type-overrides) gives every column of one database type the same C# type.  So `users.id` can be a `UserId` and `posts.id` a `PostId`, though both are `bigint`.  Above, `ListPostsDto` has `Id` a `PostId`, by the file header, `CreatedByUserId` a `UserId`, by the settings file, and `Total`, which no table gives, a `PostCount`, by its query's marker.  A parameter is no column of a table, so its [`-- param:` marker](parameters.md#param-marker) gives `ListPostsParams.Author` its type.  The C# type is written as a type override's is, a keyword, a full name or a short name: see [the type](type-mapping.md#setting-type-overrides).

**The key is the column a value is read from**: `table.column`, which matches that column of a table or view of that name in any schema, or `schema.table.column`, which matches that schema's alone.  A query's column is read from a table's column when it is that column, under an alias or not, and not when it is an expression of it: `p.id AS post_id` is `posts.id`, and `count(*)`, `p.id + 1` and a column of a `UNION` are no table's column, so no key matches them.  Give such a column its type with a `-- column:` marker inside its query.  A column read from a view is the view's, so a view's columns need entries of their own: `users.id` does not reach the `id` of a view over `users`.

- **A part of a key is plain or in double quotes**, by the rules of a type override's key: a plain part holds letters, digits, `_` and `$` and is compared ignoring case, and a part in quotes holds any character, with a quote inside it doubled, is compared exactly, and may be written with escapes, `U&"..."`.  In a settings file each `"` of a key is written `\"`, as JSON requires: `"\"Users\".id"`.  So `users.id` matches `Users.ID` too, and `"Users".id` the table `Users` alone.
- **A key serves both databases.**  A settings file's entry maps the columns of PostgreSQL's queries and SQL Server's alike, and a marker those of its file.

**Every place, merged by key.**  The column types are `columnTypes` in the settings files of a `.sql` file's folder and of the folders above it, a `-- column:` marker in the file header, and a `-- column:` marker inside a query, which names a column of the query's result.  For each column the query's marker wins, then the file header's entries, then those of the settings files, nearest first, and as for the type override, an entry for a key replaces the entries of a place below for that key alone, and `default` takes the column out of the entries of every place below.  Of the entries that match one column, the one of the most specific place wins; within one place, one with a schema wins over one without, and then one spelled exactly as the column's names over one that matches it only ignoring case.

**Over the type override.**  A column's C# type is, in order: the column types, its query's `-- column: name -> type` first and then those of the other places by the column it is read from; then the type override, by its database type; then, in a query method, a library of the [library types](library-types-and-json.md#setting-library-types) that is on; then the type map.  So a column type at any place wins over a type override at any place: naming a column is more specific than naming a type.  `-- column: name -> default` frees one column of a query for the type override.  A column type never reaches a parameter: a parameter's C# type is its [`-- param:` marker](parameters.md#param-marker)'s, then the type override's, then a library's, then the type map's.

**The nullability stays the column's**, as [which properties can be null](#model-nullability) decides it: `"users.id": "UserId"` gives a column that can be null the type `UserId?`.

**What the type does.**  A type that a column type or a `-- param:` marker gives is one you chose, as a type override's is.  A [query method](query-methods.md#query-methods) reads and sends it through your [converters](converters.md#converters) when it has them, so a `UserId` over a `bigint` column is read as a `long` and given to `UserId`'s converter, and a PostgreSQL array mapped to an array of a type of yours, `"posts.tag_ids": "TagId[]"`, is converted as [converters](converters.md#converters) says of arrays.  A class of yours in a column that holds JSON is read and sent through System.Text.Json's serializer, as [a class in a JSON column](library-types-and-json.md#json-class) says, and a column of a string type that holds JSON takes [the JSON opt-in](library-types-and-json.md#json-opt-in).  A type that the project cannot find or use is [SQLSRC250](../diagnostics.md#sqlsrc250) to [SQLSRC252](../diagnostics.md#sqlsrc252), whose message names the key, or the marker, and the place.

**In a settings file**, `columnTypes` is a JSON object with a member for each entry, `"users.id": "UserId"`, written by the type override's rules for a settings file: see [type overrides](type-mapping.md#setting-type-overrides).  A member's value may also be an object, `{ "type": "OrderMetadata", "json": true }`, for [the JSON opt-in](library-types-and-json.md#json-opt-in).  Two members of one object whose keys are one column's, `"users.id"` and `"\"users\".id"`, are [SQLSRC020](../diagnostics.md#sqlsrc020), and the first applies.  A member that is not valid is [SQLSRC019](../diagnostics.md#sqlsrc019), and the other members still apply.

**In the file header**, a `-- column:` line holds one entry, `key -> type`, `key -> json type`, which is [the JSON opt-in](library-types-and-json.md#json-opt-in), or `key -> default`, with or without spaces around its `->`, for every query of the file.  It gives a type and nothing else: a line without `->`, one that says `null` or `not null`, and one whose key has one part are [SQLSRC111](../diagnostics.md#sqlsrc111).  Two lines that give one key two C# types are [SQLSRC112](../diagnostics.md#sqlsrc112); the types are compared by how they are written, so `UserId` and `MyApp.UserId` are two.

**Inside a query**, a `-- column:` line names a column of the query's result, as the query returns it, and gives it a C# type after `->`: `-- column: total -> PostCount`, or `-> json Type` for a column of a string type that holds JSON, [the JSON opt-in](library-types-and-json.md#json-opt-in).  It wins over the column types of the other places and over the type override, and `-> default` takes the column out of the column types, so that the type override, or else a library or the type map, gives its type.  The same marker sets the column's nullability, before the `->` or alone: `-- column: note null -> NoteText`.  Its name is one part, so a name with a `.` outside quotes is [SQLSRC111](../diagnostics.md#sqlsrc111): a column's source is mapped in the file header.  The rules of its name, of its nullability and of two markers for one column are under [Setting a column's nullability or type with a marker](#column-marker).  In a file with no `-- name:` line, which is one query and takes both kinds, a key of two or three parts is the file header's kind, and a name of one part the query's.

The `sqlsource` tool does not read `columnTypes`, and a `-- column:` marker changes nothing that is described: adding, changing or removing one leaves the query's sidecar current.
