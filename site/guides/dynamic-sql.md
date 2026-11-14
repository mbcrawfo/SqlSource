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
