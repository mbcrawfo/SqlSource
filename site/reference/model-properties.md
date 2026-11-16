# Model properties: names, column types, nullable

For the order in which SqlSource chooses the C# type of a property, see [how a C# type is chosen](type-mapping.md#property-types).

<a id="property-order"></a>

## The order of the properties

The input model has a property for each [query parameter](parameters.md#query-parameters), in the order of the query's [parameter list](parameters.md#query-parameters).  That order is the order in which the parameters first appear in the SQL, then the parameters that only a `-- param:` marker declares.  The output model has a property for each column, in the order of the select list.

The [generator parameter](generator-parameters.md#setting-generator-parameters) `sort-input` sorts the properties of the input model by name, and `sort-output` sorts the properties of the output model.  The sort compares the names character by character, by the code of each character.  So upper case comes before lower case: `BX` comes before `Ba`.

A record's parameters are in the same order as its properties.  So a sorted record does not match its columns by position and name.  A [query method](query-methods.md#reading-rows) reads a sorted model as well as any other model.  To read rows into sorted models through Dapper, make the models classes, as [the recipe](../guides/dapper.md#models-through-dapper) shows.

<a id="property-names"></a>

## Property names

| Column or parameter | Property |
|----|----|
| `created_at` | `CreatedAt` |
| `userId` | `UserId` |
| `ID` | `ID` |
| `api_v2_url` | `ApiV2Url` |
| `total!` | `Total` |

A property's name is the PascalCase of the name of its column or parameter.  To make the PascalCase, SqlSource splits the name at each `_`, makes the first character of each part upper case, and joins the parts.  The rest of each part stays as it is.

- **A column's name first loses one `!` or `?` at its end.**  The mark sets the [nullability](#model-nullability) of the column.
- **A parameter's name is the name that the SQL first writes it with, without its `@`.**  `@accountId` gives `AccountId`.

Each case below is an error at the query's name.  Until you fix the error, the query has no models, and under `repository` or `methods` it has no method.  Under `models` the generator still writes its constant or token method.  To fix the error, give the column an alias or rename the parameter.  Then describe the query again.

- **A column with no name is an error.**  PostgreSQL calls such a column `?column?`, as for an expression `price * quantity` without an alias: [SQLSRC253](../diagnostics.md#sqlsrc253).  SQL Server gives such a column no name, and the tool reports it as [SQLSRC266](../diagnostics.md#sqlsrc266) when it describes the query.
- **A name whose PascalCase is not a C# identifier is [SQLSRC254](../diagnostics.md#sqlsrc254).**  That is one of these: a name of underscores alone, a name whose PascalCase starts with a digit, or a name with a character that an identifier cannot hold, such as `unit-price`.
- **Two columns, or two parameters, whose properties have one name ignoring case are [SQLSRC255](../diagnostics.md#sqlsrc255).**  Examples are `SELECT u.id, o.id`, and `user_id` beside `userId`.  SqlSource compares the names ignoring case, since Dapper matches a column to a property ignoring case.
- **A name that a record or `object` already has a member of, or the name of the model itself, is [SQLSRC256](../diagnostics.md#sqlsrc256).**  The names of those members are `EqualityContract`, `Equals`, `GetHashCode`, `ToString`, `GetType`, `MemberwiseClone`, `Finalize`, `ReferenceEquals`, `PrintMembers`, `Deconstruct` and `Clone`.

<a id="model-nullability"></a>

## Which properties can be null

```sql postgres
-- name: GetCustomerLabels
SELECT id, initcap(name) AS "display_name!"
FROM customers;
```

A property that can be null has a C# type with `?`: `int?`, which is `System.Nullable<int>`, or `string?`, a nullable reference.  In the sample, `initcap` is never null for a value that is not null, but SqlSource treats it as a column that can be null.  So the alias `"display_name!"` gives the property `DisplayName` the type `string`, not `string?`.

- **A column's property can be null when the tool recorded that the column can be null.**  A column that the tool showed is never null gets a property that cannot be null.  Which columns can be null, for [PostgreSQL](databases/postgresql/nullability.md#nullability) and for [SQL Server](databases/sqlserver/nullability.md#nullability), says when a column can be null.
- **A column whose name ends in `!` gets a property that cannot be null, whatever the tool recorded.**
- **A column whose name ends in `?` gets a property that can be null, whatever the tool recorded.**
- **Write the mark at the end of the column's alias, in quotes.**  `"display_name!"` in the sample is such an alias.
- **A `!` is your word that the column is never null, and the build does not check it.**  If such a column is null after all, a [query method](query-methods.md#reading-rows) throws.
- **The mark stays in the name of the column that the query returns**: `display_name!`.  A query method reads the columns by position, so the name does not matter to it.
- **A library that matches the columns of a row to properties by name does not match `display_name!` to `DisplayName`.**  Dapper is such a library.  With a record model, Dapper throws when it reads the rows.  With a class model, Dapper leaves the property at its default value.  To read such rows through Dapper, use a [`-- column:` marker](#column-marker) instead.
- **A column whose `-- column:` marker says `null` or `not null` gets that nullability, whatever the tool recorded.**  The column keeps its name: see [the marker](#column-marker).
- **A parameter's property can be null only in one of these cases:**
  - its [`-- param:` marker](parameters.md#param-marker) says `null`;
  - it is a [list parameter](parameters.md#list-parameters), and the query tests the list for null, as `@ids IS NULL` does.

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

A `-- column:` marker inside a query sets the nullability of one column of the query, or gives the column a C# type of your choice.  The marker leaves the name of the column alone.  In the sample, the query returns the column `display_name`, as it does without the marker, and its property `DisplayName` has the type `string`, not `string?`.  The column `order_count`, a `bigint` that no table gives, gets the property `OrderCount` of the type `OrderCount`, a type of your project.

If a library reads the rows by the names of their columns, as Dapper does, set the nullability with the marker.  A mark at the end of an alias changes the name of the column, and the marker does not.

- **The value is the column's name, then the nullability, the C# type, or both in that order.**  The nullability is `null` or `not null`, and the C# type follows `->`.  Examples are `-- column: note null -> NoteText`, and `-- column: settings null -> json CustomerSettings`, with the word `json` before the type.
- **`null` gives a property that can be null, and `not null` gives a property that cannot be null.**
- **One marker names one column.**  Write one marker for each column.
- **Two markers for one column are fine in one of these cases:** they say the same thing, or one gives the nullability and the other gives the type.
- **Two markers for one column that disagree are [SQLSRC112](../diagnostics.md#sqlsrc112).**  They disagree when they give two nullabilities or two C# types, or one type with `json` and without it.
- **`-> json Type` says that the column holds JSON.**  Write it for a column of a string type, such as `text` or `nvarchar(max)`.  Your class in that column is then read through System.Text.Json's serializer: see [the JSON opt-in](library-types-and-json.md#json-opt-in).
- **`->` and a C# type give the column that type.**  The type is written as a type override's type is: a keyword, a full name or a short name.
- **`-> default` takes the column out of every column type.**  The type override then gives the column its type, or else a library or the type map.  Which one wins is under [column types](#setting-column-types).
- **A type does not change the nullability of the column.**  `-> OrderCount` on a column that can be null gives `OrderCount?`.
- **A query method reads the type through your [converters](converters.md#converters) when it has them.**
- **The name is the column's name as the query returns it**: its alias or its own name.  SqlSource compares the name ignoring case.
- **Write the name in double quotes when it has white space or a `.`, or when it is the word `null` or `not`.**  Double a quote inside the name: `-- column: "total due" not null`.
- **A value of any other form is [SQLSRC111](../diagnostics.md#sqlsrc111).**  The value must be one name, plain or quoted, then `null` or `not null`, a `->` and a type, or both.  So `-- column: total due not null`, without its quotes, is this error.  So is a name with a `.` outside quotes, `-- column: users.id -> UserId`, since a column's source is mapped in the file header.
- **A marker in one of these cases is [SQLSRC260](../diagnostics.md#sqlsrc260)**, and the query gets no models until you fix it:
  - the marker names no column of the query's result;
  - the query returns no rows;
  - the marker says `null` or `not null` of a column whose alias already ends in `!` or `?`, which says the nullability twice.
- **A marker that gives a type alone can name a column whose alias ends in `!` or `?`.**
- **The marker is a comment.**  If you add, change or remove a marker, or its type, the query does not change.  So you do not need to describe the query again.
- **A marker has no effect on a query whose output is `sql`**, since such a query has no models.
- **In the file header, a `-- column:` line is a column type.**  It names the column of a table or view that a value is read from, as `-- column: users.id -> UserId` does, and gives it a type in every query of the file: see [column types](#setting-column-types).

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

A column type gives one column of a table or view the C# type of your choice, in every query that reads the column.  A [type override](type-mapping.md#setting-type-overrides), by contrast, gives every column of one database type the same C# type.  So `users.id` can be a `UserId` and `posts.id` a `PostId`, though both are `bigint`.

In the sample, `ListPostsDto` gets these types:

- `Id` is a `PostId`, by the file header.
- `CreatedByUserId` is a `UserId`, by the settings file.
- `Total`, which no table gives, is a `PostCount`, by the marker of its query.

A parameter is no column of a table.  So the [`-- param:` marker](parameters.md#param-marker) gives `ListPostsParams.Author` its type.

**The key is the column that a value is read from.**  It has one of these forms:

- `table.column` matches that column of a table or view of that name, in any schema.
- `schema.table.column` matches that column of that schema's table or view alone.

The rules of a key:

- **A query's column is read from a table's column when it is that column, under an alias or not.**  `p.id AS post_id` is `posts.id`.
- **No key matches a column that is no table's column.**  `count(*)`, `p.id + 1` and a column of a `UNION` are no table's column, and nor is any other expression of a column.  Give such a column its type with a `-- column:` marker inside its query.
- **A column read from a view is the view's column.**  So the columns of a view need entries of their own: `users.id` does not reach the `id` of a view over `users`.
- **A part of a key is plain or in double quotes**, by the rules of a type override's key.  A plain part holds letters, digits, `_` and `$`, and SqlSource compares it ignoring case.  A part in quotes holds any character, with a quote inside it doubled, and SqlSource compares it exactly.  A part in quotes can be written with escapes, `U&"..."`.  So `users.id` matches `Users.ID` too, and `"Users".id` matches the table `Users` alone.
- **In a settings file, each `"` of a key is written `\"`**, as JSON requires: `"\"Users\".id"`.
- **A key serves both databases.**  An entry of a settings file maps the columns of the queries of PostgreSQL and of SQL Server alike.  A marker maps the columns of its file.

**The C# type is written as a type override's type is**: a keyword, a full name or a short name.  See [the type](type-mapping.md#setting-type-overrides).

**The places.**  A column type can be written in three places:

- `columnTypes` in the settings files of the folder of a `.sql` file and of the folders above it;
- a `-- column:` marker in the file header;
- a `-- column:` marker inside a query, which names a column of the query's result.

**In a settings file**, `columnTypes` is a JSON object with a member for each entry: `"users.id": "UserId"`.  Each member follows the rules of the type override for a settings file: see [type overrides](type-mapping.md#setting-type-overrides).  The value of a member can also be an object, `{ "type": "OrderMetadata", "json": true }`, for [the JSON opt-in](library-types-and-json.md#json-opt-in).

- **Two members of one object whose keys name one column are [SQLSRC020](../diagnostics.md#sqlsrc020)**, as `"users.id"` and `"\"users\".id"` are.  The first member applies.
- **A member that is not valid is [SQLSRC019](../diagnostics.md#sqlsrc019).**  The other members still apply.

**In the file header**, a `-- column:` line holds one entry, and the entry applies to every query of the file.  The entry has one of these forms, with or without spaces around its `->`:

- `key -> type`;
- `key -> json type`, which is [the JSON opt-in](library-types-and-json.md#json-opt-in);
- `key -> default`.

The rules of a line of the file header:

- **A line of the file header gives a type and nothing else.**  Each of these lines is [SQLSRC111](../diagnostics.md#sqlsrc111): a line without `->`, a line that says `null` or `not null`, and a line whose key has one part.
- **Two lines that give one key two C# types are [SQLSRC112](../diagnostics.md#sqlsrc112).**  SqlSource compares the types as they are written, so `UserId` and `MyApp.UserId` are two types.

**Inside a query**, a `-- column:` line names a column of the query's result, as the query returns it.  It gives the column a C# type after `->`: `-- column: total -> PostCount`.  The same marker can also set the column's nullability.  Its rules, `-> json Type` and `-> default` among them, are under [Setting a column's nullability or type with a marker](#column-marker).

**In a file with no `-- name:` line**, which is one query, a `-- column:` line can be of both kinds.  A key of two or three parts is the file header's kind, and a name of one part is the query's kind.

**Every place, merged by key.**  Of the entries that match one column, the entry of the most specific place wins.  The places, from the most specific:

1. the marker inside the query;
2. the entries of the file header;
3. the entries of the settings files, nearest first.

As for the type override, an entry for a key replaces the entries of a place below for that key alone.  `default` takes the column out of the entries of every place below.

**Within one place, the entry that matches the column best wins.**  An entry with a schema wins over an entry without one.  Then an entry spelled exactly as the column's names wins over an entry that matches it only ignoring case.

**Over the type override.**  A column type at any place wins over a type override at any place, since naming a column is more specific than naming a type.  A column type never reaches a parameter.  The whole order in which SqlSource chooses a C# type, for a column and for a parameter, is under [property types](type-mapping.md#property-types).

**A type that the project cannot find or use is an error**, from [SQLSRC250](../diagnostics.md#sqlsrc250) to [SQLSRC252](../diagnostics.md#sqlsrc252).  The message names the key, or the marker, and the place.

**The nullability stays the column's**, as [which properties can be null](#model-nullability) decides it.  So `"users.id": "UserId"` gives a column that can be null the type `UserId?`.

**What the type does.**  A type that a column type or a `-- param:` marker gives is a type you chose, as a type override's type is.  A [query method](query-methods.md#query-methods) reads and sends it through your [converters](converters.md#converters) when it has them.

- **A query method reads a `UserId` over a `bigint` column as a `long`, and gives it to the converter of `UserId`.**
- **A PostgreSQL array mapped to an array of a type of yours is converted as [converters](converters.md#converters) says of arrays.**  An example is `"posts.tag_ids": "TagId[]"`.
- **A class of yours in a column that holds JSON is read and sent through System.Text.Json's serializer**, as [a class in a JSON column](library-types-and-json.md#json-class) says.
- **A column of a string type that holds JSON takes [the JSON opt-in](library-types-and-json.md#json-opt-in).**

**Column types and `-- column:` markers change nothing that is described.**  The `sqlsource` tool does not read `columnTypes`.  If you add, change or remove a `-- column:` marker, the sidecar of the query stays current.
