# TD-0043 - Small gaps of the PostgreSQL describer's parameters

## Problem

Four things that [`PostgresSession`](../../src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs) does with a query's parameters are not whole:

- **A `$n` that the SQL writes itself.**  SqlSource's parameter is `@name`, and the lexer reads `$1` as text.  The server reads it as a parameter: `SELECT 1 FROM users WHERE id = @id AND name = $2` prepares with two types, and `SELECT @a, $1` has one parameter that the SQL names twice.  The describer types the parameters of the query's list, the first of the types the server resolved, and says nothing of the rest: the query is described as if the `$n` were not a parameter, and what the generated code then sends is the user's to get right.
- **A reserved word as the schema of a type.**  [`PostgresTypeNames`](../../src/SqlSource.Tool/Describing/Postgres/PostgresTypeNames.cs) writes a schema and a name bare when each is lower-case letters, digits and `_`.  A schema named `user` or `order` needs quotes in SQL, and its types are spelled `user.status`.  The sidecar's `name` is what phase 8's generated code binds with.
- **A cancelled describe leaves its statement.**  `deallocate` is asked with the describe's own token, so a run that is cancelled between `prepare` and `deallocate` frees nothing.  The statement dies with its session, which the run closes next.
- **A statement whose types could not be read.**  `prepare` runs `PREPARE` and then reads `parameter_types` of `pg_prepared_statements`.  When the `PREPARE` succeeds and the read fails, a connection that drops between the two for one, `prepare` answers the error, so `deallocate` is not asked and the statement stays until the session ends; and the error is mapped as one of the `PREPARE`, so a position in it would be counted against the `PREPARE`'s text.

## Why it exists

The first: refusing a `$n` needs the tool to tell one from a dollar-quoted string's tag and from a money literal, which the lexer does for a quote and not for a placeholder, and sub-phase 3.3's check of the lexer against Npgsql is the place that sees a query whose parameters the two read differently.  The second: the tool has no list of PostgreSQL's reserved words, which differs by version.  The third is the spec's choice: a cancelled run must not wait on a server.  The fourth: an answer has a value or an error and nothing between.

## Impact

Low.  The first two need a query or a schema that few projects have, and the describer is registered nowhere until sub-phase 3.6.  The third costs nothing: the session ends.  The fourth costs nothing in practice, since a connection that fails there has usually ended.

## Proposed fix

For the first, report a `$` followed by a digit in a `Text` lexeme, outside an identifier, as an error of its own at the query, or let sub-phase 3.3's check report it.  For the second, quote a name that `pg_get_keywords()` lists as reserved, asked once for a session with `open`, or always quote a name of a schema outside `pg_catalog` and `public`.  The third needs none, and the fourth none until a call is added between the `PREPARE` and the read.

## Trigger

Sub-phase 3.3, whose check of the lexer against Npgsql meets the first; phase 8, whose generated code binds a parameter by the sidecar's `name`, for the second.
