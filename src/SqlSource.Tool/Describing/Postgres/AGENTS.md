# AGENTS.md - src/SqlSource.Tool/Describing/Postgres

## A describe

- **`PostgresSession.DescribeAsync` holds the order of a describe**: its four checks, `prepare` first and `deallocate` in its `finally`, and `DescribePreparedAsync` the steps between them, in the order of [one describe, step by step](../../../../docs/architecture/postgres-describer.md#one-describe-step-by-step).  A call that has nothing to ask is not made.
- **A prepared statement is session state**: every call from `prepare` to `deallocate` runs in one transaction, and a new call after `prepare` goes through `PostgresLive.InDescribeAsync` ([the prepared statement and the session](../../../../docs/architecture/postgres-describer.md#the-prepared-statement-and-the-session)).
- **A relation, an attribute or a type that another session dropped between two calls is `SQLSRC225` through `PostgresErrors.SchemaChanged`, never a throw**; the throws that remain guard a bug of the describer.
- **An OID the describer names is a constant of `BuiltInTypes`.**
- **`explain` asks for a generic plan through the prepared statement**, under `SET LOCAL`s that it ends by rolling back to the describe's savepoint, and never with `ANALYZE` or `GENERIC_PLAN` ([the plan](../../../../docs/architecture/postgres-nullability.md#the-plan)).
- **`PlannerSettings` are for the tests, and its list is closed**: `ToolHost.Create` registers the describer with the constructor that takes none.

## The connection, the calls and the errors

- **The connection is made inside a `live` function and nowhere else** ([the connection](../../../../docs/architecture/postgres-describer.md#the-connection)).  `PostgresConnection` makes the data source, the connection, every command, every batch and the transaction; `PostgresLive.cs` holds the SQL sent; `PostgresSession` holds what is asked and what is made of each answer.
- **Every `live` function answers through `PostgresLive.AnswerAsync`.**  A new text that the describer throws as an `NpgsqlException` of its own goes in `PostgresConnection.OwnTexts`, or `ConnectionErrors` replaces it.
- **`NpgsqlRunLogger` forwards an allowlist of event ids**: an update of Npgsql reads the new version's messages again before it lists an id.
- **The calls and their records are a contract with the captures** ([the calls and their records](../../../../docs/architecture/postgres-describer.md#the-calls-and-their-records)).  A call of `PostgresCallNames` is added and never renamed.  A member that came later reads null as what an older capture meant.  A change to what the describer asks changes the fixtures: record them with `tools/record-fixtures.sh` and read the diff.
- **The SQL of a catalog call runs under the user's `search_path`**: write every type and function in it with `pg_catalog.`, and pair arrays with `ROWS FROM`, never `unnest(a, b)`.
- **`PostgresErrors` gives a server's error its id by the order of [errors](../../../../docs/architecture/postgres-describer.md#errors).**  A SQLSTATE is matched alone, since a message may be translated.  Which errors of `check` are a disagreement is decided in `NpgsqlCheck.Read` alone.
- **`check` is the lexer against Npgsql** ([the check against Npgsql](../../../../docs/architecture/postgres-describer.md#the-check-against-npgsql)).  The sidecar records only a `bindName` that `check` bound in this run, and the tool never rewrites a user's SQL to make a parameter bindable.  `BatchRewritesTests` and `RowDescriptionsTests` pin the members read by reflection.
- **A rule of how PostgreSQL writes a name is `PostgresIdentifiers.cs`'s.**  A new reader uses it; one that keeps a rule of its own is listed, with why, in the remarks of `PostgresIdentifiers`.
- **`tests/SqlSource.Tool.Tests/PostgresScript.cs` answers the session with no server**, so its tests hold what is asked as well as what is made of the answers.  What only a server can show is tested in `tests/SqlSource.Tests.Postgres/Describer/`.

## Nothing a `.sql` file holds can make the describer run a statement

- What holds that is how the `PREPARE` is sent, and not what the tool read of it.
- `PostgresConnection.ExecuteAsOneStatementAsync` sends it as an `NpgsqlBatch` of one command with no parameters, which Npgsql does not read and never splits: the server is given the whole text as one statement to parse, and it refuses a text that holds a second one, `42601: cannot insert multiple commands into a prepared statement`, before it runs anything.  The one statement the text can be is a `PREPARE`, which runs nothing.
- An `NpgsqlCommand` is another thing: Npgsql splits its text at each `;` that its own lexer finds and runs every part, so `PostgresConnection.CommandAsync` is for a text that holds nothing of a user's, and the `PREPARE` never goes through it.
- `check` is the second text that holds a user's SQL, and goes the same way, through `PostgresConnection.BatchOfOneAsync`, under `CommandBehavior.SchemaOnly`: with a named parameter Npgsql reads the text to place it, and refuses one with a `;` that something follows by a `NotSupportedException`, having sent nothing, which `PostgresLive.CheckAsync` answers as an error and never lets through.
- Four checks stand before anything is sent and use no server.  They are the first refusal, with an error that says what is wrong, and the guarantee does not rest on them:
  - `ParameterPlaceholders.Rewrite` refuses a text in which the lexer finds a second statement, `SQLSRC230`,
  - `StatementShape.RefusedKind` refuses a statement that `PREPARE` does not take, `SQLSRC233`,
  - `StatementShape.PositionalParameter` refuses a `$n` that the SQL writes itself, `SQLSRC234`,
  - and `DeclaredTypeText` reads each `-- param:` type against the grammar of a type name, `SQLSRC228`, since the type is put into the `PREPARE` as written.
- The tool's lexer, Npgsql's and the server's may split a text differently, so the guarantee does not rest on the lexer: it reads a character above U+007F as part of a name, as the server does, so `SELECT 1 AS §$x$; DROP TABLE t; --$x$` is two statements to all three, but a text the lexer reads as one can still be two to the server.
- The type list still ends with a line break before `AS`: whatever a type's text held, the `) AS` is not on its line, so nothing in a type can reach it.
- A change to any of them gets a case in `tests/SqlSource.Tool.Tests/Describing/Postgres/` that shows what is refused.
- A new place that puts a user's text into SQL the tool sends does it so that the driver cannot split the text, and gets a case with a canary beside `PrepareAsync_TextOfTwoStatements_AnswersTheServersErrorAndRunsNothing` in `tests/SqlSource.Tests.Postgres/Describer/ParameterTests.cs`, and for a text that goes to `check`, one beside `NpgsqlCheckTests.CheckAsync_TextNpgsqlWouldSplit_AnswersAnErrorAndRunsNothing` in `tests/SqlSource.Tests.Postgres/Describer/NpgsqlCheckTests.cs`.
- Every text the describer sends, and how, is [why a query cannot run a statement](../../../../docs/architecture/postgres-describer.md#why-a-query-cannot-run-a-statement).

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
