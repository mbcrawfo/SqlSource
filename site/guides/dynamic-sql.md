# Choose a table or a clause at run time

<a id="table-name-at-run-time"></a>

## Choose a table name at run time

Write a token with a default, `{{table:orders}}`, where the name goes.  The query becomes a method that takes the text to put there.  The default is only a sample that the tool describes the query with; a query whose output is `sql` needs none.  Nothing is escaped, so pass only text that your own code chooses, never a value a user typed.  See [the recipe](#table-name-at-run-time) and [tokens](../reference/tokens.md#tokens).

```sql postgres
-- name: CountRows -> one
SELECT COUNT(*) AS total FROM {{table:orders}};
```

```csharp
var table = kind == Kind.Archived ? "orders_archive" : "orders";
var counted = await reports.CountRows(table);
// runs SELECT COUNT(*) AS total FROM orders_archive; counted.Total is the count
```

The query's method takes the text of the token, and runs the query with the token replaced.  Under the output `sql` or `models`, the query is instead a method that returns the SQL, `Sql.CountRows(table)`, for your own code to run.  The default after `:` is only a sample that the tool describes the query with, and the method still takes the table name; a query whose output is `sql` needs no default.  Pass only text that your own code chooses, as above, and never a value that a user typed: nothing is escaped.

Rules: [tokens](../reference/tokens.md#tokens), [the signature](../reference/repositories.md#repository-signature), [token methods](../reference/members.md#token-methods), [defaults](../reference/tokens.md#token-defaults).

<a id="clause"></a>

## Choose a clause at run time, such as ORDER BY

Use a token when your code chooses a whole clause at run time, such as the order of the rows.

```sql postgres
-- name: ListUsersSortedBy
SELECT id, name
FROM users
ORDER BY {{orderBy:name}};
```

```csharp
string orderBy = newestFirst ? "created_at DESC" : "name";
ListUsersSortedByDto[] sorted = await users.ListUsersSortedBy(orderBy);
```

**What you get.**  `ListUsersSortedBy` takes the text of `{{orderBy}}` as a `string`, and runs the query with that text in the place of the token.

**The rules you need.**

- The method takes one `string` for each token, after the input model when the query has one.  See [Tokens](../reference/tokens.md#tokens).
- The default after the `:`, here `name`, is only a sample, which the tool describes the query with.  The method never uses it, so your code always passes the text.  See [Defaults](../reference/tokens.md#token-defaults).
- If a token is written several times, or its sample is long, give its default once.  Write it in a line such as `-- token: {{orderBy:name}}` inside the query.  See [`-- token:`](../reference/tokens.md#token-marker).
- A change to a default makes the query's sidecar out of date.  Run `dotnet sqlsource describe` again.  See [`-- token:`](../reference/tokens.md#token-marker).
- Under the output `sql` or `models`, the query is a method that returns its SQL, `Sql.ListUsersSortedBy(orderBy)`.  See [Token methods](../reference/members.md#token-methods).

<a id="safety"></a>

## Keep it safe from SQL injection

Read this before your code passes any text to a token.

> [!WARNING]
> A token is never safe for text that a user can change.  The method puts the text in the SQL as it is, and escapes, quotes and checks nothing.  So text from a user is a SQL injection.  Pass only text that your own code chooses, such as a column name from a fixed list.  Put every value in a query parameter, such as `@name`, which is safe.  By default the method checks only that the text is not null, empty or blank, and that check is not a check of safety.  See [Tokens](../reference/tokens.md#tokens) and [Validation](../reference/tokens.md#token-validation).

## If it does not work

- **SQLSRC210.**  A token has no default, and the query must be described.  Give it a default, as `{{orderBy:name}}` has.  See [SQLSRC210](../diagnostics.md#sqlsrc210).
- **`ArgumentNullException` or `ArgumentException` from the method.**  The text of a token is null, empty or blank.  For an optional clause that can be empty, write `-- generator: token-validation=off`.  See [Validation](../reference/tokens.md#token-validation).
