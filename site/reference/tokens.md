# Tokens (`{{name}}`)

<a id="token-or-parameter"></a>

## Token or parameter

A query takes things from the code that runs it in two ways.  A [query parameter](../concepts.md#query-parameter) carries a value: `@id` in `WHERE id = @id`.  A [token](../concepts.md#token) carries a piece of the SQL itself: `{{table}}` in `FROM {{table}}`.

```sql
-- name: ListSince
-- param: @since timestamptz
SELECT id, name FROM {{table}} WHERE created_at >= @since;
```

A value cannot stand for a table name, a list of columns or a whole clause, and a token can.  A token cannot be made safe, and a value can.

| | Query parameter | Token |
|----|----|----|
| Written | `@since` | `{{table}}` |
| Carries | A value | Text of the SQL |
| Replaced | Never by SqlSource: it stays in the SQL, and its value is sent to the database beside it | By the generated method, when your code asks for the SQL or runs the query |
| Safe for text a user can influence | Yes | No |

<a id="tokens"></a>

## Tokens

```sql
-- name: ListFrom
-- summary: Lists the rows of one table.
SELECT id, name FROM {{table}} ORDER BY {{orderBy}};
```

> **Tokens are for trusted text only.**  A token is replaced by string concatenation.  Nothing is escaped, quoted or checked for safety, so a value that a user can influence is a SQL injection.  Use a token for a fragment that your own code chooses, such as a table name from a fixed list, and a query parameter for every value.

`{{name}}` in a query is replaced with text that the caller supplies.  A query with a token whose output is `sql` or `models` is a static method and not a constant.  The method has one `string` parameter for each token name, in the order the names first appear, and returns the SQL with every token replaced:

```csharp
var sql = Sql.ListFrom("users", "name DESC");
// SELECT id, name FROM users ORDER BY name DESC;
```

Where the method goes is described on [the generator page](../generator.md#token-methods).  Under the output `repository` or `methods`, the query's [method](../generator.md#query-methods) takes the same `string` arguments, after its input model, fills in the tokens the same way and runs the query: `await users.ListFrom("users", "name DESC")` for a repository, or `await connection.ListFrom("users", "name DESC")` at the output `methods`.

- **A token's name is a C# identifier that is not a reserved keyword.**  A keyword is [SQLSRC114](../diagnostics.md#sqlsrc114).  Spaces and tabs inside the braces are ignored.
- **Names are case-sensitive.**  `{{Table}}` and `{{table}}` are two tokens.
- **A name that is used more than once is one token**, and every occurrence is replaced.  It is one argument.
- **A token is replaced wherever it is written**, including inside a string or a quoted identifier.
- **Braces around anything that is not a name, or a name and a default, are not a token.**  `{{table-name}}`, `{{1st}}`, `{{order by}}` and `{{name:` with no closing `}}` stay in the SQL as written, and nothing is reported.
- **Comments are removed before tokens are found**, so a `{{x}}` in a comment is not a token.  A query that keeps its comments (the `keep-comments` [generator parameter](../settings.md#setting-generator-parameters)) is the exception: a kept comment is part of the SQL, and a token in it is replaced like any other.
- **A query that gains its first token changes from a constant to a method**, or its query method gains an argument, so the code that uses it stops compiling until it passes the arguments.

<a id="token-defaults"></a>

## Defaults

```sql
-- name: ListUsers
-- token: {{filter:AND deleted_at IS NULL}}
SELECT id, name FROM {{table:users}}
WHERE 1 = 1 {{filter}}
ORDER BY {{orderBy:name}};
```

A token can carry a default: `{{name:default}}`.  The default is everything after the first `:` up to the closing `}}`, without the white space around it, so `{{cast:x::int}}` is the token `cast` with the default `x::int`.  It may be empty, `{{extraWhere:}}`, and may run over several lines.  A comment must be wholly inside a token's default or outside the token, or it is [SQLSRC120](../diagnostics.md#sqlsrc120): comments are removed before tokens are found, and kept where the query keeps its comments, so the two would read different tokens.  So a comment inside a default must not hold `}}`, and a `--` comment runs to the end of its line, so `{{filter:AND a = 1 -- note}}` is one: put the comment on a line of its own, outside the default.  Where a query keeps its comments, a comment must not open a default, `{{name:`, that the SQL after it closes, nor stand between the braces and the name or between the name and the colon or closing braces, as in `{{f/* c */}}`.  The rule holds for a name in a [`-- token-ignore:`](#token-ignore) marker too, since its text also runs to the next `}}`, as a token's does.

**A default is a sample of what the caller will pass.**  The generated method does not use it: it still takes every token as a `string`, and nothing uses the default when your code runs.  It is there for the tool.  A query that is described cannot be sent to a database with `{{where}}` in it, so the tool puts the token's default there.  A token without a default in a query that must be described is [SQLSRC210](../diagnostics.md#sqlsrc210).  An empty default describes the query with nothing in the token's place.

A parameter inside a default is part of the sample.  It is a parameter of the query only if the query's SQL holds it outside a token or a `-- param:` marker declares it, as [the marker section](parameters.md#param-marker) says:

```sql
-- name: ListPage
-- param: @page int
SELECT id FROM users {{paging:LIMIT 20 OFFSET @page}};
```

<a id="token-marker"></a>

### `-- token:`

A `-- token:` marker inside a query gives the default of a token that the query writes several times, or whose sample is long.  Its value is exactly what the SQL would hold: one token with its default, as `{{filter:AND deleted_at IS NULL}}` is above.

- A marker that is not exactly one token with a default is [SQLSRC111](../diagnostics.md#sqlsrc111), and so is a default with a quote or a block comment that does not close inside it.  A name that is a keyword is [SQLSRC114](../diagnostics.md#sqlsrc114).
- A marker for a token the query does not write is ignored.
- Two defaults for one token of a query must be the same, whether they are two in the SQL, two markers, or one of each.  Two that differ are [SQLSRC112](../diagnostics.md#sqlsrc112).  Comments in a default are ignored when defaults are compared, wherever the default is written, so `{{f:a /* note */ b}}` in a marker and in the SQL is one default.
- The marker goes inside a query, not in the file header.

A change to a default, like a change to the SQL, makes the query's sidecar out of date.  A change to a comment inside a default does not.

<a id="token-ignore"></a>

## Text that looks like a token: `-- token-ignore:`

```sql
-- name: Greeting
-- token-ignore: name
SELECT 'Hello, {{name}}' AS greeting;
```

`{{name}}` here is text for a template engine and not a token.  A `-- token-ignore: name` marker inside the query keeps every `{{name}}` of the query as it is written: the query stays a constant.

- One name for each marker.  A query can have several markers.
- The value is one C# identifier and nothing else, or it is [SQLSRC111](../diagnostics.md#sqlsrc111).  A keyword is accepted, so `-- token-ignore: class` keeps a `{{class}}` that would otherwise be [SQLSRC114](../diagnostics.md#sqlsrc114).
- It applies to the query it stands in, and to a `{{name:default}}` as well as `{{name}}`.

<a id="token-validation"></a>

## Validation

```sql
-- name: ListFiltered
-- generator: token-validation=off
SELECT id, name FROM users {{whereClause}};
```

By default the method, a token method or a query method, checks each argument with `ArgumentException.ThrowIfNullOrWhiteSpace`: null throws `ArgumentNullException`, and an empty or blank string throws `ArgumentException`.  The check is that a value is present, not that it is safe.

An empty fragment can be what you want, for an optional clause for example, so the check can be turned off with the `token-validation=off` generator parameter.  Where a generator parameter can be written, and which place wins, is on [the settings page](../settings.md#setting-generator-parameters).

With validation off the method checks nothing.  An empty argument leaves nothing where its token was, and a null argument throws `NullReferenceException`.
