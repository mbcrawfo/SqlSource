# Tokens (`{{name}}`)

<a id="token-or-parameter"></a>

## Token or parameter

```sql
-- name: ListSince
-- param: @since timestamptz
SELECT id, name FROM {{table}} WHERE created_at >= @since;
```

A query takes things from the code that runs it in two ways.  A [query parameter](parameters.md#query-parameters) carries a value: `@since` above, or `@id` in `WHERE id = @id`.  A [token](#tokens) carries a piece of the SQL itself: `{{table}}` in `FROM {{table}}`.

A value cannot stand for a table name, a list of columns or a whole clause, but a token can.  A value can be made safe, but a token cannot.

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

A token is a place in a query's SQL, written `{{table}}`, that your code fills in with text when it asks for the SQL or runs the query.  A query parameter cannot stand for a table name or a clause, but a token can.

A query with a token whose output is `models` or `sql` is a static method, not a constant.  The method has one `string` parameter for each token name, in the order in which the names first appear.  It returns the SQL with every token replaced:

```csharp
var sql = Sql.ListFrom("users", "name DESC");
// SELECT id, name FROM users ORDER BY name DESC;
```

[Where the members go](member-placement.md#locations) says where the method goes.  Under the output `repository` or `methods`, the query's [method](query-methods.md#query-methods) takes the same `string` arguments, after its input model.  It fills in the tokens the same way, and runs the query: `await users.ListFrom("users", "name DESC")` for a repository, or `await connection.ListFrom("users", "name DESC")` at the output `methods`.

- **A token's name is a C# identifier that is not a reserved keyword.**  A keyword is [SQLSRC114](../diagnostics.md#sqlsrc114).
- **Spaces and tabs inside the braces are ignored.**
- **Names are case-sensitive.**  `{{Table}}` and `{{table}}` are two tokens.
- **A name that is used more than once is one token.**  Every occurrence is replaced, and the name is one argument.
- **A token is replaced wherever it is written**, inside a string or a quoted identifier too.
- **Braces around anything that is not a name, or a name and a default, are not a token.**  `{{table-name}}`, `{{1st}}`, `{{order by}}` and `{{name:` with no closing `}}` stay in the SQL as written, and nothing is reported.
- **Comments are removed before tokens are found**, so a `{{x}}` in a comment is not a token.
- **A query that keeps its comments is the exception.**  With the `keep-comments` [generator parameter](generator-parameters.md#setting-generator-parameters), a kept comment is part of the SQL, and a token in it is replaced like any other.
- **A query that gains its first token changes from a constant to a method**, or its query method gains an argument.  So the code that uses it stops compiling until it passes the arguments.

<a id="token-defaults"></a>

## Defaults

```sql
-- name: ListUsers
-- token: {{filter:AND deleted_at IS NULL}}
SELECT id, name FROM {{table:users}}
WHERE 1 = 1 {{filter}}
ORDER BY {{orderBy:name}};
```

A token can carry a default: `{{name:default}}`.  The default is everything after the first `:` up to the closing `}}`, without the white space around it.  So `{{cast:x::int}}` is the token `cast` with the default `x::int`.

- **A default can be empty**, as in `{{extraWhere:}}`.
- **A default can run over several lines.**
- **A comment must be wholly inside a token's default, or outside the token.**  Why: comments are removed before tokens are found, and kept where the query keeps its comments, so the two would read different tokens.  A comment that breaks this rule in one of the ways below is [SQLSRC120](../diagnostics.md#sqlsrc120).
- **A comment inside a default must not hold `}}`.**
- **A `--` comment runs to the end of its line.**  So `{{filter:AND a = 1 -- note}}` is one that holds `}}`.  Put the comment on a line of its own, outside the default.
- **Where a query keeps its comments, a comment must not open a default, `{{name:`, that the SQL after it closes.**
- **Where a query keeps its comments, a comment must not stand beside the name inside the braces**: between the braces and the name, or between the name and the colon or the closing braces, as in `{{f/* c */}}`.
- **Each of these rules on comments holds for a name in a [`-- token-ignore:`](#token-ignore) marker too**, since its text also runs to the next `}}`, as a token's does.

**A default is a sample of what the caller passes.**  The generated method does not use it.  The method still takes every token as a `string`, and nothing uses the default when your code runs.

The default is there for the tool.  The tool cannot send a query with `{{where}}` in it to a database, so when it describes the query, it puts the token's default there.  A token without a default, in a query that must be described, is [SQLSRC210](../diagnostics.md#sqlsrc210).  An empty default describes the query with nothing in the token's place.

A query parameter inside a default is part of the sample.  It is a query parameter of the query only if one of these is true:

- the query's SQL holds it outside a token;
- a `-- param:` marker declares it, as [the marker section](parameters.md#param-marker) says.

Here the marker declares `@page`:

```sql
-- name: ListPage
-- param: @page int
SELECT id FROM users {{paging:LIMIT 20 OFFSET @page}};
```

<a id="token-marker"></a>

### `-- token:`

In the sample of defaults, `-- token: {{filter:AND deleted_at IS NULL}}` gives the token `filter` its default.  A `-- token:` marker inside a query gives the default of a token that the query writes several times, or whose sample is long.  Its value is exactly what the SQL would hold: one token with its default.

- **A marker that is not exactly one token with a default is [SQLSRC111](../diagnostics.md#sqlsrc111).**  So is a default with a quote or a block comment that does not close inside it.
- **A name that is a keyword is [SQLSRC114](../diagnostics.md#sqlsrc114).**
- **A marker for a token that the query does not write is ignored.**
- **Two defaults for one token of a query must be the same**, whether they are two in the SQL, two markers, or one of each.  Two that differ are [SQLSRC112](../diagnostics.md#sqlsrc112).
- **Comments in a default are ignored when defaults are compared**, wherever the default is written.  So `{{f:a /* note */ b}}` in a marker and in the SQL is one default.
- **The marker goes inside a query, not in the file header.**

A change to a default makes the query's sidecar out of date, as a change to the SQL does.  A change to a comment inside a default does not.

<a id="token-ignore"></a>

## Text that looks like a token: `-- token-ignore:`

```sql
-- name: Greeting
-- token-ignore: name
SELECT 'Hello, {{name}}' AS greeting;
```

Here `{{name}}` is text for a template engine, not a token.  A `-- token-ignore: name` marker inside the query keeps every `{{name}}` of the query as it is written, so the query stays a constant.

- **A marker holds one name.**  A query can have several markers.
- **The value is one C# identifier and nothing else**, or it is [SQLSRC111](../diagnostics.md#sqlsrc111).
- **A keyword is accepted.**  So `-- token-ignore: class` keeps a `{{class}}` that would otherwise be [SQLSRC114](../diagnostics.md#sqlsrc114).
- **The marker applies to the query it stands in**, and to a `{{name:default}}` as well as to `{{name}}`.

<a id="token-validation"></a>

## Validation

```sql
-- name: ListFiltered
-- generator: token-validation=off
SELECT id, name FROM users {{whereClause}};
```

By default, the method checks each token's argument with `ArgumentException.ThrowIfNullOrWhiteSpace`, whether it is a token method or a query method.  A null argument throws `ArgumentNullException`.  An empty or blank string throws `ArgumentException`.  The check is that a value is present, not that it is safe.

An empty fragment can be what you want, for an optional clause for example.  So the `token-validation=off` generator parameter turns the check off.  [The generator parameters page](generator-parameters.md#setting-generator-parameters) says where you can write a generator parameter, and which place wins.

With validation off, the method checks nothing.  An empty argument leaves nothing where its token was.  A null argument throws `NullReferenceException`.
