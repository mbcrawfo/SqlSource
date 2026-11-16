# Write queries

At the end of this page, your `.sql` file holds queries with parameters, an optional filter, an `IN` list, and one row or no rows.

<a id="several-queries"></a>

## Several queries in one file

Add these two queries to `UserRepository.sql`, below `GetUser`:

```sql postgres
-- name: ListUserNames
-- summary: The name of every user, in the order of their ids.
SELECT id, name
FROM users
ORDER BY id;

-- name: RenameUser -> rowcount
-- summary: Changes the name of one user.
UPDATE users
SET name = @name
WHERE id = @userId;
```

Each `-- name:` line starts a new query.  The query ends at the next `-- name:` line, or at the end of the file.

Build the project.  With the `.env` file of [Your first query](first-query.md#connection), the build describes the two new queries first.  `UserRepository` now has three methods: `GetUser`, `ListUserNames` and `RenameUser`.

<a id="result-shapes-in-short"></a>

## One row, or no rows

The word after `->` is the result shape of the query.  It says what the query's method returns:

- **No word**, for a query that returns rows: every row, in an array.  `ListUserNames` returns `ListUserNamesDto[]`.
- **`many`**: every row, as with no word.
- **`one`**: the one row.  The method throws when the query returns no row, or more than one.
- **`one-optional`**: the one row, or null when there is none.  `GetUser` returns `GetUserDto?`.  The method throws when the query returns more than one row.
- **`none`**: nothing.  The method runs the statement and returns.
- **`rowcount`**: the number of rows that the statement changed.  `RenameUser` returns an `int`.

<a id="parameters"></a>

## A parameter

A query parameter is `@` and a name, such as `@userId`.  It stays in the SQL as you wrote it.  Its value comes from a property of the query's parameters model: `GetUserParams.UserId`.  The properties are in the order that the parameters first appear in the SQL.  So you call `RenameUser` with the name first:

```csharp
int renamed = await users.RenameUser(new RenameUserParams("Ada Lovelace", 1));
```

<a id="optional-filter"></a>

## An optional filter

To filter by a value only when the caller gives one, test the parameter for null.  Add this query to `UserRepository.sql`:

# [PostgreSQL](#tab/postgresql)

```sql postgres
-- name: FindUsers
-- summary: Finds the users with a name, or every user when the name is null.
-- param: @name text null
SELECT id, name, email
FROM users
WHERE @name IS NULL OR name = @name
ORDER BY name;
```

# [SQL Server](#tab/sqlserver)

```sql mssql
-- name: FindUsers
-- summary: Finds the users with a name, or every user when the name is null.
-- param: @name nvarchar(100) null
SELECT id, name, email
FROM users
WHERE @name IS NULL OR name = @name
ORDER BY name;
```

---

The `-- param:` line gives `@name` a type, in the words of your database.  The word `null` says that the value can be null.  So `FindUsersParams.Name` is a `string?`, and `new FindUsersParams(null)` finds every user.

If your database cannot work out the type of a parameter, the tool reports [SQLSRC226](../diagnostics.md#sqlsrc226).  On PostgreSQL, `@name IS NULL` before `name = @name` gives [SQLSRC227](../diagnostics.md#sqlsrc227).  Add a `-- param:` line with the type, as above.  Until you fix it, the other new queries of the file get [SQLSRC241](../diagnostics.md#sqlsrc241) too.

<a id="lists"></a>

## A list after IN

To filter by a list of values, write the parameter directly after `IN`, with no parentheses.  Add this query to `UserRepository.sql`:

```sql postgres
-- name: ListUsersByIds
-- summary: The users whose ids are in the list.
SELECT id, name
FROM users
WHERE id IN @ids
ORDER BY id;
```

The property `ListUsersByIdsParams.Ids` is an `IEnumerable<int>`, so it takes an array or a `List<int>`:

```csharp
ListUsersByIdsDto[] found = await users.ListUsersByIds(new ListUsersByIdsParams([1, 2]));
```

The method sends one parameter for each value.  An empty list finds no rows.

<a id="not-null-columns"></a>

## A column that is never null

A property is nullable when your database cannot show that its column is never null.  The result of a function is often such a column.  If you know that the column is never null, add a `-- column:` line with `not null`.  Add this query to `UserRepository.sql`:

# [PostgreSQL](#tab/postgresql)

```sql postgres
-- name: ListDisplayNames
-- summary: The name of every user, with each word capitalized.
-- column: display_name not null
SELECT id, initcap(name) AS display_name
FROM users
ORDER BY id;
```

# [SQL Server](#tab/sqlserver)

```sql mssql
-- name: ListDisplayNames
-- summary: The name of every user, in upper case.
-- column: display_name not null
SELECT id, UPPER(name) AS display_name
FROM users
ORDER BY id;
```

---

Or end the column's alias with `!`, in double quotes: `AS "display_name!"`.  Either way, `ListDisplayNamesDto.DisplayName` is a `string`, not a `string?`.  The build does not check what you say.  If the column is null after all, the method throws when it reads the row.

<a id="summaries"></a>

## Document a query

A `-- summary:` line documents its query.  In your editor, point at a call of `GetUser`.  The editor shows the summary, `One user, or null.`, and the SQL of the query.

## If it does not work

- [SQLSRC226](../diagnostics.md#sqlsrc226) or [SQLSRC227](../diagnostics.md#sqlsrc227): your database cannot work out the type of a parameter.  Add `-- param: @name <type>`, with `null` if the value can be null.
- A property is nullable, but its column never is: add `-- column: <name> not null` to the query.
- [SQLSRC244](../diagnostics.md#sqlsrc244): a query changed after it was described.  Build with your `.env` file in place, or run `dotnet sqlsource describe`, as [The build describes for you](keep-up-to-date.md#the-build-describes) says.

## What you have now

A `.sql` file with several queries, and the forms for a parameter, an optional filter, a list and a column that is never null.

Rules: [queries and names](../reference/sql-files.md#queries-and-names), [result shapes](../reference/sql-files.md#result-shapes), [what each shape returns](../reference/return-types.md#method-shapes), [query parameters](../reference/parameters.md#query-parameters), [`-- param:`](../reference/parameters.md#param-marker), [list parameters](../reference/list-parameters.md#list-parameters), [which properties can be null](../reference/model-properties.md#model-nullability), [`-- column:`](../reference/model-properties.md#column-marker), [summaries](../reference/sql-files.md#summaries), [what your editor shows](../reference/members.md#intellisense).  Guides: [Pass a list of values (IN)](../guides/lists.md), [Fix a nullable property](../guides/nullability.md).

Next: [Call your queries](call-queries.md)
