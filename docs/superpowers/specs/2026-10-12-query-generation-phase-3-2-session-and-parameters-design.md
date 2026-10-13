# Query generation, phase 3.2: the session and the parameters - design

Date: 2026-10-12

Sub-phase 3.2 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It delivers the first engine: the PostgreSQL describer over Npgsql, as far as opening a session and typing a query's parameters, and with it the project that tests a describer against a real PostgreSQL in a container, the script that records the describer's fixtures, and the first fixtures.  The describer is registered nowhere yet: the released tool keeps saying `SQLSRC216` for `postgres` until sub-phase 3.6, and until then the describer is exercised by its tests alone.

It builds on sub-phase 3.1.  Sub-phase 3.3 builds on it.

## Goal

```csharp
// tests/SqlSource.Tests.Postgres, against the container:
var description = await session.DescribeAsync(Request("SELECT id FROM users WHERE id = @id AND status = @status"), ct);
description.Parameters.ShouldBe([
    new DescribedParameter("id", Postgres("integer", internalName: "int4"), "inferred"),
    new DescribedParameter("status", Postgres("public.user_status", kind: "enum", schema: "public", internalName: "user_status", labels: ["active", "disabled"]), "inferred"),
]);
```

```console
$ tools/record-fixtures.sh
Recording 4 cases of postgres against postgres:18 ... done
```

Success is:

- Every parameter of a query gets the type the server resolved for it, a declared type included, and a declared type is checked by the server against every use of the parameter.
- A query the server rejects is a failure at the query that quotes the server, with one id for each cause a user can mend differently.
- Every call to the server goes through the exchange, so that the describer's fixtures replay in `SqlSource.Tool.Tests` with no Docker.
- The container tests run in CI on PostgreSQL 14 and 18, and locally with one command.

## Decisions

From the epic, unchanged: the describer is behind the seam of sub-phase 2.5; it returns failures and never throws; it puts no part of the connection's value in anything it returns or logs; the error format of sub-phase 2.5 with its `step:` and `server:` lines; the supported floor is PostgreSQL 14; the images are pinned by tag in one place with a variable to override, and CI runs the newest and the oldest supported; Docker is a required tool already.

Changed from the epic by the spike this phase ran, and recorded in the outline:

| Decision | Choice | Why |
|----|----|----|
| How parameters are typed | `PREPARE <name> (<declared type or unknown>, ...) AS <sql>`, then `parameter_types` of `pg_prepared_statements` | The epic planned `NpgsqlCommandBuilder.DeriveParameters`, which sends `Parse` with no types and so cannot carry a declaration.  `PREPARE` takes a declared type as written, `int`, `varchar(50)`, a domain, resolves it, checks it against every use, and infers the rest from the ones left `unknown`. |
| Where the test project lives | `tests/SqlSource.Tests.Postgres`, the per-database project | The owner's goal is one project for each database that tests its describer, its models and its generated code.  This phase creates it with the describer's tests; phase 5 adds to it.  The outline first had phase 5 create it. |
| A query of several statements | Refused before anything is sent, `SQLSRC230` | Npgsql splits a text at `;` and runs each part, and `PREPARE` would prepare the first and run the second. |

Settled in this spec:

| Decision | Choice | Why |
|----|----|----|
| The driver | Npgsql, the current release, pinned in `Directory.Packages.props` | The tool's driver is its own; the floor of Npgsql 8 is for the consumer's project, in phase 5. |
| The connection | One `NpgsqlDataSource` for each session, built from the connection's value, and one connection from it | A fresh data source loads the type catalog at its first open.  A plain `NpgsqlConnection` shares a process-wide catalog by connection string, which the spike saw go stale when the schema changed after the first open. |
| When the connection is made | Inside the `live` function of the first call, never in `OpenAsync` itself | Under replay no `live` runs, so nothing of Npgsql is touched. |
| The name of a prepared statement | `sqlsource_<n>`, counted from 1 in each session | It is part of a request, so it must be the same when recorded and replayed.  A prepared statement lives in its session, so a name never collides with another session's. |
| `DEALLOCATE` | Always, in a `finally`, outside any transaction | `PREPARE` is session state and survives `ROLLBACK`; a statement left behind is a leak until the session ends, and a second `PREPARE` of the name is `42P05`. |
| What a parameter's type carries | No facets | The protocol gives a parameter an OID and no modifier; `$1::varchar(50)` is a `varchar`. |
| What `DescribeAsync` answers in this sub-phase | A description with the parameters typed, `ResultKind.None` and no columns | The columns are sub-phase 3.3's.  The describer is registered nowhere, so no sidecar sees it; the fixtures of this sub-phase are statements without rows, which stay true. |
| A declared type the server does not know | `SQLSRC228`, from a `42704` whose position falls inside the type list of the `PREPARE` | A `42704` in the SQL itself is the server rejecting the query, `SQLSRC225`.  The position tells the two apart; a `42704` with no position is `SQLSRC225`. |
| A declared type's text | Read against the grammar of a type name before it is put into the `PREPARE`; anything else is `SQLSRC228` | The marker's type is everything to the end of the line, and the tool interpolates it into SQL it sends.  `int) AS SELECT 1; DROP TABLE users; --` must never leave the tool, and neither must anything that is not a type. |
| Every failure of `open` | `SQLSRC224`, whatever the SQLSTATE | A wrong password is `28P01` and a missing database `3D000`, and neither is the server rejecting a query. |
| Where the position of an error points | Into the SQL as it was sent, the `$n` form, which the `prepare` event logs | The rewrite changes offsets after each placeholder, and mapping back through the lexemes buys little for a position the IDE cannot use anyway. |
| `deallocate` after a `prepare` that failed | Not asked; the counter still advances | There is no statement to free.  The answer gains an `error` that is logged and never reported. |

## Out of scope

- Columns, the catalog, nullability, the table match, the walk.  Sub-phases 3.3 to 3.5.
- The lexer-versus-Npgsql check.  Sub-phase 3.3, which has the columns to compare.
- Driver logging, and registering the describer in `ToolHost.Create`.  Sub-phase 3.6.
- The two logical databases, `app` and `billing`, of the end-to-end tests.  Phase 5; this sub-phase applies `app` alone.

## The test project

`tests/SqlSource.Tests.Postgres`, in the solution's `tests` folder, built like `tests/SqlSource.Tool.Tests`: `net10.0`, xunit v3, Shouldly, project references to the tool and to the generator, both of which add it to their `InternalsVisibleTo`, and the package `Testcontainers.PostgreSql`.

| Part | Is |
|----|----|
| `PostgresContainer` | A collection fixture: starts the image, creates the database `fixtures`, applies the fixture schema to it with plain Npgsql once, and gives the connection string of that database.  Testcontainers' reaper removes the container. |
| The schema | `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql`, the one schema of this phase: the tables, two views, one plain and one over an outer join, the domain, the enum, and a table in the schema `billing`.  Every container test of phase 3 runs against it.  The end-to-end schemas of the `app` and `billing` databases are phase 5's and live in this project then. |
| `Describer/` | The tests of this phase, one class for each sub-phase's subject |
| `Describer/FixtureCasesTests` | For each case under `Fixtures/Describers/postgres/`, describes `query.sql` live and compares with `expected.json`; with `SQLSOURCE_RECORD_FIXTURES` set, writes `capture.json` and `expected.json` instead |
| `SQLSOURCE_TESTS_POSTGRES_IMAGE` | The image, `postgres:18` when not set.  The tag is in one place, `PostgresContainer` |

- The fixture cases of the tool's tests are read from the repository, not from an output folder: `FixtureCasesTests` finds `tests/SqlSource.Tool.Tests/Fixtures/Describers/` from its own assembly's location by walking up to the folder that holds `SqlSource.slnx`, since recording writes there.
- The live comparison of a case sets `serverVersion` to one value on both entries before comparing the texts, and `expected.json` keeps the version the case was recorded against: CI runs the same cases on 14 and 18, and the default tag moves from one patch to the next.
- A test that wants a session makes a `PostgresDescriber`, an `OpenRequest` with the container's connection string, a `LiveExchange` and `TimeProvider.System`, and a `NullRunLog` or a recording one.
- `tools/record-fixtures.sh` runs `dotnet test --project tests/SqlSource.Tests.Postgres` with `SQLSOURCE_RECORD_FIXTURES=1` and xunit's `--filter-class` for `FixtureCasesTests`, after `--`, since the runner is Microsoft.Testing.Platform, and prints what it recorded.  Without Docker it fails as the tests do.  The script is not a check and is in neither `pre-commit-validation.sh` nor `build.yml`.
- `build.yml` gains one step after `Test`: `dotnet test --project tests/SqlSource.Tests.Postgres --configuration "$CONFIGURATION" --no-build` with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14`, so the floor runs on every pull request.  `nightly.yml` runs `tests/SqlSource.Tests`, `tests/SqlSource.Tests.RoslynFloor` and `tests/SqlSource.Tool.Tests` by `--project`, since its runners have no Linux containers.  `pre-commit-validation.sh` is unchanged: `dotnet test --solution` runs the new project on the default image.

## The describer

In `src/SqlSource.Tool/Describing/Postgres/`.

| Type | Is |
|----|----|
| `PostgresDescriber` | `IQueryDescriber` for `SqlDialect.PostgreSql`.  `OpenAsync` makes a `PostgresSession` and asks `open` through it |
| `PostgresSession` | `IDescribeSession`: the exchange, the log, the connection holder, the statement counter, and `DescribeAsync` |
| `PostgresConnection` | Holds the connection's value, and makes the `NpgsqlDataSource` and its connection on the first `live` call that needs them.  Disposed with the session.  Overrides `ToString` to leave the value out |
| `DeclaredTypeText` | Reads a `-- param:` type against the grammar of a type name, and gives the text to put in the list or the reason it is refused |
| `ParameterPlaceholders` | The statement check and the `$n` rewrite over the lexer's lexemes |
| `PostgresTypes` | The `types` call, and builds a `PostgresType` from the rows for an OID |
| `PostgresTypeNames` | Spells a type's `name` as the sidecar wants it |
| `PostgresErrors` | Turns a `ServerError` into a `DescribeFailure` |
| `PostgresEvents` | Builds each event of the describer's log |
| `PostgresCalls.cs` | The request and answer records of every call, one file |

### The calls of this sub-phase

| Method | Request | Answer |
|----|----|----|
| `open` | Nothing | `serverVersion`: `current_setting('server_version')` cut at its first space; `driverVersion`: the Npgsql assembly's; `settings`: `search_path`, `plan_cache_mode` and `standard_conforming_strings` as name and value; or `error` |
| `prepare` | `statement`, `sql`, `types` | `parameterTypes`: `parameter_types::oid[]` of `pg_prepared_statements` for the name, in order; or `error` |
| `types` | `oids`, sorted and distinct | `rows`, one for each type of the closure, in `oid` order |
| `deallocate` | `statement` | `error`, or null |

`ServerInfo` is built from `open`: `Driver` is `Npgsql`, `DriverVersion` and `Version` the answer's, `Settings` the three.

`ServerError` is `sqlState`, `message`, `detail`, `hint` and `position`, each null where the server gave none; a failure that is no `PostgresException`, a connection refused for one, has `message` alone.  Every `live` function catches `NpgsqlException` and returns it as the answer's `error`; a cancellation passes through.  What else a `live` throws is a bug, and `SQLSRC200`.

### `DescribeAsync`

1. **The statements.**  The sample SQL is lexed with the generator's lexer under the `postgres` rules.  A `;` in a `Text` lexeme is the end of a statement; one followed by nothing but white space and comments is the trailing one and is dropped from the text that is sent; any other is `SQLSRC230`, and nothing is sent.  Each declared type is read by `DeclaredTypeText` against the grammar of a type name: one or more words, each a bare identifier of letters, digits and `_` or a quoted identifier with `""` for a quote, joined by spaces or by `.`; then an optional modifier, `(` with digits, commas, spaces and `-` inside `)`; then more words, for `timestamp(3) without time zone`; then any number of `[]`; and nothing else, no comment, no `;`, no quote left open, no second `(`.  That reads `int`, `varchar(50)`, `numeric(2,-3)`, `interval day to second(3)`, `"My Schema"."Status"[]`, and refuses `int) AS SELECT 1`, since a `)` without its `(` is not in the grammar.  A text the grammar refuses is `SQLSRC228`, and nothing is sent.  The type list still ends with a line break before `AS`, as a second line of defence.
2. **The placeholders.**  Each `Parameter` lexeme is replaced by `$` and the one-based ordinal of its name in the request's parameter list, compared ignoring case.  A lexeme whose name is not in the list is a bug of the plan, and throws.  The type list has one item for each parameter of the list, in order: the declared type as the `-- param:` marker wrote it, or `unknown`.  A declared parameter that the SQL does not hold is in the list all the same: the server accepts a list longer than the placeholders used.
3. **`prepare`**, with the next statement name.  An `error` ends the describe with the failure `PostgresErrors` gives for it, after step 5.
4. **`types`** for the OIDs of the answer.  Each parameter gets its `PostgresType`, with `typeSource` `declared` or `inferred`.
5. **`deallocate`**, in a `finally`, when `prepare` answered without `error`, with the same cancellation token: a cancelled describe leaves the statement to die with its session.  Its own `error` is logged and never reported.

The description is the parameters, `ResultKind.None`, and null for everything else, until sub-phase 3.3.

### Types

The `types` call answers one row for each type in the closure of the OIDs asked for: the types themselves, and through `typelem` of an array, `typbasetype` of a domain, `rngsubtype` of a range, and the range of a multirange, every type they reach.  A row holds `oid`, `typname`, `nspname`, `typtype`, `typcategory`, `typelem`, `typbasetype`, `rngsubtype` for a range, the range's `rngsubtype` for a multirange, `format_type(oid, -1)`, and for an enum its labels in `enumsortorder`.  One query, with a recursive CTE that follows the four links in one term.

`PostgresTypes.Build(oid, rows, facets)` gives the sidecar's record:

| `typtype` | `kind` | And |
|----|----|----|
| `b`, `typcategory` not `A` | `base` | `length`, `precision`, `scale` from the facets, sub-phase 3.3's |
| `b`, `typcategory` `A` | `array` | `element` built for `typelem`, without facets |
| `d` | `domain` | `base` built for `typbasetype` with the facets, since the protocol applies a domain's modifier to its base |
| `e` | `enum` | `labels` |
| `r` | `range` | `subtype` built for `rngsubtype` |
| `m` | `multirange` | `subtype` built for the range's `rngsubtype` |
| `c` | `composite` | Nothing more; `fields` is a later additive key |
| `p` | None | A pseudo-type, `record`, `unknown`, `anyelement`: it cannot be described.  For a column it is sub-phase 3.3's `SQLSRC229`.  A parameter never has one: the server refuses a pseudo-type in a `PREPARE` list and infers `text` where nothing types a parameter, so one here is a bug, and throws |

`schema` is `nspname` and `internalName` is `typname`.  A row the closure lacks for an OID that was asked for is a bug, and throws.

`PostgresTypeNames.Spell(type)` gives `name`:

- A type in `pg_catalog`: `format_type(oid, -1)` from its row, `integer`, `character varying`, `timestamp with time zone`, `numrange`.  With facets, from sub-phase 3.3: `character varying(100)`, `character(2)`, `bit(8)`, `bit varying(5)`, `numeric(18,2)` with no space, `numeric(2,-3)`, `timestamp(3) without time zone` and the three other time types with the precision after their first word, `interval(6)`.
- Any other schema: the schema and the name, each written as an SQL identifier, `public.user_status`, `"My Schema"."Status"`: bare when it is lower-case letters, digits and `_` and does not start with a digit, else in double quotes with a quote doubled.  Whatever the session's `search_path`, so the value does not depend on the session.
- An array: the element's name and `[]`.

### Errors

`PostgresErrors.Failure(error, step, query)` builds the `DescribeFailure`:

| The error | Id | Help |
|----|----|----|
| `42P18` | `SQLSRC226`, the parameter named from the `$n` of the message | Cast the parameter where it is used, `@p::int`, or declare its type with `-- param: @p int` |
| `42P08` | `SQLSRC227`, the parameter named from the `$n` of the message | Declare the type with `-- param:`, or write the use that fixes the type before the one that does not: `(name = @name OR @name IS NULL)` |
| `42704` with a position inside the type list of the `PREPARE` | `SQLSRC228` with `the database has no such type`, the type and the parameter named from the position | Write a type the database has, schema-qualified if it is not on the search path |
| A declared type `DeclaredTypeText` refuses | `SQLSRC228` with `it is not written as a type name`, before anything is sent | Write the type as a column definition would: a name, a modifier in parentheses, `[]` |
| Any other, with a SQLSTATE | `SQLSRC225`, with the server's message | None: the server's lines say what is wrong |
| Any failure of `open`, with a SQLSTATE or without | `SQLSRC224` | Check that the database is running and that the connection string names it |
| No SQLSTATE, from any other call | `SQLSRC225` | None |

The `server:` lines, verbatim and in this order: `<SQLSTATE>: <message>` or `<message>` alone, then `detail: <detail>`, `hint: <hint>` and `at character <position>` for each the server gave, where the position is the one in the SQL as sent, the server's position less the length of the `PREPARE` text before the SQL, and no line when the server gave none.  The `step:` is `describe parameters` for `prepare` and `SQLSRC230`, `resolve type` for `types`, and none for `open`.

The driver's message on a failed `open` names the host or the user, `Failed to connect to tcp://host:5432`, `password authentication failed for user "app"`, and reaches the console, the log's `diagnostic` event and the capture's `open` answer.  The tool does not read a connection string and cannot take a part of it out; `docs/tech-debt/TD-0034` gains the case, and sub-phase 3.6's driver events keep it in mind.

The message of the `$n` parameter: the server names a parameter by its placeholder, `$1`, and the tool names it as the user wrote it, `@id`, through the ordinal.

### Events

Through the log the describer is given, each with `durationMs`:

| Event | Members |
|----|----|
| `postgres.prepare` | `query`, `statement`, `sql`, `types`, `parameterTypes` or `error` |
| `postgres.types` | `oids`, `rows`: each row as the answer holds it |
| `postgres.deallocate` | `statement`, `error` |

`open` has no event of its own: the run's `database.open` records what the session knows of its server.

### Diagnostics

In `ToolDiagnostics`, with the four places each needs, and ids assigned in the outline.

| Id | Title | Message | Arguments |
|----|----|----|----|
| `SQLSRC224` | Database could not be opened | `The database '{0}' could not be opened` | The database's name |
| `SQLSRC225` | Server rejected the query | `The server rejected the query '{0}': {1}` | The query; the server's message, on one line |
| `SQLSRC226` | Parameter type could not be determined | `The type of the parameter '{0}' of the query '{1}' could not be determined` | The parameter as written, with its prefix; the query |
| `SQLSRC227` | Parameter's first use does not fix its type | `The first use of the parameter '{0}' of the query '{1}' does not fix its type` | The parameter; the query |
| `SQLSRC228` | Declared type is not valid | `The type '{0}' declared for the parameter '{1}' of the query '{2}' is not valid: {3}` | The type as written; the parameter; the query; `the database has no such type` or `it is not written as a type name` |
| `SQLSRC230` | Query holds more than one statement | `The query '{0}' holds more than one statement` | The query |

`SQLSRC230` has a `help:` line: one statement for each query; a `;` is allowed at the end alone.  `SQLSRC228` has two forms of its last argument and of its help, one for a type the server does not know and one for a text the tool refuses.  `SQLSRC229`, `SQLSRC231` and `SQLSRC232` are sub-phase 3.3's.

## The first fixtures

Under `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/`, recorded by the script against the default image, each a statement without rows so that sub-phase 3.3 changes none of their entries:

| Case | Proves |
|----|----|
| `update-inferred` | `UPDATE` with two inferred parameters |
| `insert-declared` | `INSERT` with a declared `varchar(50)`, a declared domain, and an inferred enum |
| `delete-declared-only` | `DELETE` with a parameter that only a `-- param:` declares, and a token with a default |
| `update-array-and-range` | An array parameter and a range parameter |

The theory `DescriberFixtureTests.Replay_Case_GivesTheExpectedEntry` in `SqlSource.Tool.Tests` runs every case of every engine folder through `FixtureReplay` with the engine's describer and compares the text with `expected.json`.  Sub-phase 3.3 asks `columns` for every query, so these captures are re-recorded then; that is the visible fixture update the epic wants.

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing and brings its tests and the documents it makes wrong.

1. The epic outline: the row of 3.2 says In progress.
2. The test project with `PostgresContainer` and `Schema/app.sql`, the solution, `build.yml`, `nightly.yml` and `CONTRIBUTING.md`.  One test that opens a connection and reads the server's version.
3. Npgsql, `PostgresDescriber`, `PostgresSession`, `PostgresConnection`, the `open` call and `SQLSRC224`.
4. `ParameterPlaceholders`, `DeclaredTypeText`, `SQLSRC230` and the refusing form of `SQLSRC228`.
5. `prepare` and `deallocate`, with `PostgresErrors` and `SQLSRC225` to `SQLSRC228`.
6. `types`, `PostgresTypes` and `PostgresTypeNames`.
7. The events.
8. `FixtureCasesTests`, `tools/record-fixtures.sh`, the four cases, and `DescriberFixtureTests`.
9. The documents, and the outline's row set to Done.

## Testing

| Where | Cases |
|----|----|
| `ParameterPlaceholdersTests` (tool tests) | `@id` once and twice; two parameters in order of first appearance; a name in another case; `@` inside a string, a comment, a quoted identifier and a dollar-quoted string left alone; the type list with declared and `unknown`; a declared-only parameter at the end; a trailing `;` with a comment after it dropped; a `;` in a string kept; a second statement is `SQLSRC230` and nothing is sent; the line break before `AS` |
| `DeclaredTypeTextTests` (tool tests) | Each accepted form of the grammar; `;`, `'`, `/*`, `--`, `int) AS SELECT 1`, `int(`, `int(1)(2)`, `int[`, a quote left open, a word after `[]`, and a line break refused as `SQLSRC228` with its reason |
| `PostgresTypeNamesTests` (tool tests) | Each row of the spelling: every faceted form, a schema-qualified type, an identifier that needs quotes, a quote doubled, an array of each, an array of a domain |
| `PostgresTypesTests` (tool tests) | Each kind from rows made by the test; a domain over a domain; a multirange's subtype; a pseudo-type; an OID the rows lack throws |
| `PostgresErrorsTests` (tool tests) | Each row of the table; the position moved by the `PREPARE` prefix; no position; a `42704` inside and outside the type list, and with no position; each `server:` line; a parameter named from `$2`; a `28P01` from `open` is `SQLSRC224` |
| `SessionTests` (container) | `open` gives the version cut at the space, `Npgsql` as the driver and the three settings; a wrong password, a missing database and an unreachable host are `SQLSRC224` with the driver's message; the connection's whole value is in no text of the failure |
| `ParameterTests` (container) | Every type of `Schema/app.sql` inferred: base types, a `varchar`, a `numeric`, `timestamptz`, a domain, an enum with its labels, an array, a range, a multirange; a declared `int`, `varchar(50)`, `public.user_status`, a domain, an array; a declared type checked against a use that rejects it, `SQLSRC225`; `SELECT @p IS NULL` is `SQLSRC226`; `(@name IS NULL OR name = @name)` is `SQLSRC227` and with a declaration is typed; `(name = @name OR @name IS NULL)` is typed; `-- param: @p nope_t` is `SQLSRC228`; a declared-only parameter; a parameter used twice; two statements are `SQLSRC230` and `pg_prepared_statements` is empty after each test; a syntax error and an unknown table are `SQLSRC225` with the server's position in the user's SQL |
| `FixtureCasesTests` (container) | Every case's live entry equals `expected.json` |
| `DescriberFixtureTests` (tool tests) | Every case replays to `expected.json`; a capture with a call removed fails with `CaptureMissException` |
| `SecretTests` of sub-phase 2.5 | A `PostgresDescriber` given a connection string that cannot be opened, with a marker word in it: `SQLSRC224` is printed and the word is nowhere |

## Documentation

- `src/SqlSource.Tool/AGENTS.md`: `Describing/Postgres/` and the route, `PREPARE` and the prepared statement's lifecycle, why `DEALLOCATE` is outside the transaction and in a `finally`; the statement check before anything is sent; that the connection is made inside a `live` function and nowhere else; that a data source is made for each session; the names of the calls and their records are a contract with the captures; the four places a server error's id comes from.
- `CONTRIBUTING.md`: the fourth test project and what it needs: Docker for the tests, the image pulled on first run, `SQLSOURCE_TESTS_POSTGRES_IMAGE`, the two runs of CI, what nightly leaves out, and `tools/record-fixtures.sh`.
- `tools/AGENTS.md`: `record-fixtures.sh` is not a check.
- `docs/diagnostics.md`: `SQLSRC224` to `SQLSRC228` and `SQLSRC230`.
- `src/SqlSource.Tool/README.md`: nothing changes; the tool still has no describer registered.
- The repository's `README.md` and `docs/publishing.md`: nothing changes.
- `docs/tech-debt`: `TD-0034` gains the driver's messages: a failed `open` names the host or the user on the console, in the log's `diagnostic` event and in the capture, and a connection dropped mid-session is `SQLSRC225` with the same kind of message.
- `docs/deferred`: nothing is expected.
- The epic outline: in steps 1 and 9.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
