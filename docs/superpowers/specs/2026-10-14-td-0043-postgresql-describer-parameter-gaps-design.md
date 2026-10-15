# TD-0043, small gaps of the PostgreSQL describer's parameters - design

Date: 2026-10-14

Resolves [TD-0043](../../tech-debt/TD-0043-small-gaps-of-the-postgresql-describers-parameters.md).  The item lists nine gaps of [`PostgresSession`](../../../src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs) and its helpers.  This document says what each becomes.  The numbers 1 to 9 below are the item's bullets, in order.

## Goal

Clear the whole entry before phase 8, which sends the SQL as written and binds parameters by the sidecar's type name.  Every gap is fixed, turned into an error that tells the user what to do, or closed with the reason recorded where the code makes the choice.  One gap, describing `CALL` and data definition, becomes a deferred item: it is real work that the epic left out of scope, not a defect.

Success is:

- A user who meets any of the nine cases gets either a correct description or an error whose message and `help:` line name the cause and the fix.  No case ends in `SQLSRC200`, and no case is reported as `SQLSRC225` "server rejected the query" when the SQL is valid.
- `docs/tech-debt/TD-0043-*.md` and its row in `docs/tech-debt/README.md` are gone, and `grep -r "TD-0043" src tests docs` finds nothing outside `bin/` and `obj/`.
- A new `docs/deferred/D-0002-*.md` tracks describing `CALL` and data definition.
- `./pre-commit-validation.sh` passes, including the PostgreSQL container tests on 14 and 18.

Non-goals:

- Supporting a connection pooler in transaction mode, or Npgsql multiplexing.  The tool refuses them clearly instead (gap 8).
- Describing `CALL` or DDL (gap 5).  That is D-0002.
- A list of PostgreSQL's reserved words in the tool (gap 2).

Anti-goals, which would make this a failure even if every test passes:

- A real typo in a `SELECT`, `SELEC 1`, reported as anything other than `SQLSRC225`.
- A `models` or `codegen` setting that the user wrote, overridden by the tool without a word.
- A sidecar type name that Npgsql binds today and refuses after this change.
- A new path by which the connection string reaches an error or the log.  The fixed messages of gaps 8 and 9 quote nothing of the driver.  What existing `NpgsqlException` messages at `open` already show is [TD-0034](../../tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md)'s, and is out of scope here.

## Decisions

| Gap | Choice | Whose |
|----|----|----|
| 1, a `$n` that the SQL writes itself | Refused before anything is sent, with a new error. | Mine; you saw it in the playback without objection |
| 2, a reserved word as a type's schema | Check first what Npgsql binds.  Expected: the bare `user.status` is what Npgsql wants, so the spelling stays and a test pins it. | Mine |
| 3, a cancelled describe leaves its statement | No change.  The reason moves into the code comment. | Mine; you saw it in the playback without objection |
| 4, a failed read of the types after `PREPARE` | No change.  The reason moves into the code comment. | Mine; you saw it in the playback without objection |
| 5, `CALL` and data definition | Option A of the research: a new error, found by the statement's first word before anything is sent, with `42P14` as a backstop.  Its help says to mark the query `-- output: sql`.  The query still holds its file back. | **Yours** |
| 5, follow-up | A new deferred item, D-0002, for describing `CALL` and DDL. | **Yours** |
| 5, `SELECT … INTO` | Refused on every version, as `CREATE TABLE AS` is, so that a sidecar does not depend on the server it was described against. | Mine, after the builder check |
| 6, a server older than 14 | Refused at `open` with a new error that names the server's version and the floor. | Mine |
| 7, `int2vector` and `oidvector` | Described as base types, by OID. | Mine |
| 8, transaction poolers and `Multiplexing=true` | Refuse clearly: `Multiplexing=true` is rejected at `open`, a prepared statement that goes missing is a new error that says to use a direct connection, and the READMEs say the tool needs a session of its own. | **Yours** |
| 9, other exceptions of `open` | Enumerate them, and map what a user can cause to `SQLSRC224` with a fixed message that quotes nothing of the driver. | Mine |
| Shape of the work | One spec, one plan, one pull request. | Mine |
| Diagnostic ids | Four new ids, `SQLSRC233` to `SQLSRC236`, one for each cause a user fixes differently, which is the rule `PostgresErrors` states. | Mine |

## New diagnostics

Each is an error of the tool, added to [`ToolDiagnostics`](../../../src/SqlSource/Diagnostics/ToolDiagnostics.cs) and its `All` list, to `src/SqlSource/AnalyzerReleases.Unshipped.md`, and as a section of `docs/diagnostics.md` (the test in `tests/SqlSource.Tests/Diagnostics/` fails until the sections and the descriptors agree).  The epic design's line "later phases take ids from `SQLSRC233`" (`docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, section on diagnostics) changes to `SQLSRC237`.  The titles and texts below are a starting point; keep the house style of `docs/diagnostics.md`.

| Id | Title | Message | Reported |
|----|----|----|----|
| `SQLSRC233` | Statement cannot be described | `The query '{0}' is a {1} statement, which the tool cannot describe` | At the query, step `describe parameters`, nothing sent |
| `SQLSRC234` | Query writes a positional parameter | `The query '{0}' writes the parameter '{1}' itself` | At the query, step `describe parameters`, nothing sent |
| `SQLSRC235` | Server version is not supported | `The database '{0}' runs PostgreSQL {1}, and the tool supports {2} and later` | Once for the database, as `SQLSRC224` is; each of its queries counts as failed |
| `SQLSRC236` | Connection does not keep a session | `The connection to the database '{0}' does not keep one session` | At `open` for `Multiplexing=true`, once for the database; at the query when a statement goes missing |

The help lines, roughly:

- `SQLSRC233`: `give the query -- output: sql; the tool describes SELECT without INTO, INSERT, UPDATE, DELETE, MERGE, VALUES and TABLE`.
- `SQLSRC234`: `write the parameter as @name; generated code binds parameters by name`.
- `SQLSRC235`: `upgrade the server, or describe against a database of PostgreSQL {floor} or later`.
- `SQLSRC236`: `connect directly to the server, not through a pooler in transaction mode, and without Multiplexing=true`.

`SQLSRC225`'s section loses its paragraph on `CALL` and data definition, which now points to `SQLSRC233`.

## Gap by gap

### 1. A `$n` that the SQL writes itself

Before `ParameterPlaceholders.Rewrite`, scan the lexemes of the SQL (`SqlLexer.Lex(sql, SqlDialectRules.PostgreSql)`, as `ParameterPlaceholders.Statement` already does).  In a `Text` lexeme, a `$` followed by an ASCII digit is a positional parameter unless the character before the `$` can continue an identifier (a letter, a digit, `_` or `$`), which makes it part of a name such as `a$1`.  A dollar-quote tag cannot start with a digit, and the lexer already reads dollar-quoted strings, quoted identifiers and comments as lexemes of their own, so neither is scanned.  The first match fails the describe with `SQLSRC234`, naming the `$n` as written (`$2`).

`ParameterOids` and `ArgumentCount` in `PostgresSession` keep their comments about a `$n` of the SQL only as far as they stay true.  After this change the server's count of types equals the list's, so `ParameterOids` may assert that instead of taking the first `count`; the builder chooses.

Two container tests document the gap and change: `Describe_SqlWithAPlaceholderOfItsOwn_PassesTheCheck` in `tests/SqlSource.Tests.Postgres/Describer/NpgsqlCheckTests.cs`, and `Describe_SqlWithAPlaceholderOfItsOwn_TypesTheListedParametersAlone` in `ParameterTests`.  Each becomes a test that the query is refused with `SQLSRC234`, wherever such a test fits best.  Unit tests cover: `= $2`, `SELECT @a, $1`, `a$1` (not refused), `$1` in a string, in a dollar-quoted string, in a quoted identifier and in a comment (not refused), and `$$…$$` (not refused).

### 2. A reserved word as the schema of a type

The sidecar's `name` is consumed as Npgsql's `DataTypeName` (the check binds it already, `PostgresLive.CheckAsync`), and Npgsql refuses any quoted name (`SQLSRC232`).  So quoting `user` would turn a type Npgsql may bind into one it refuses.

First, a container test: create a schema `"user"` with an enum `status`, and a query whose parameter has that type.  If the check passes with `user.status`, the bare spelling is right for the sidecar's one consumer.  Keep it, rewrite the remarks of [`PostgresTypeNames`](../../../src/SqlSource.Tool/Describing/Postgres/PostgresTypeNames.cs) to say why a reserved word is not quoted, and keep the test.  If Npgsql refuses it, the check already reports `SQLSRC232`, which is accurate; add the case to `SQLSRC232`'s list of known refusals in `docs/diagnostics.md` and keep the test as the proof.  Either way no reserved-word list is added.

### 3. A cancelled describe leaves its statement

No change of behaviour.  The comment on the `finally` in `PostgresSession.DescribeAsync` already says the statement dies with its session.  It gains why: a cancelled run must not wait on a server, which is the spec's rule, and the run closes the session next.

### 4. A statement whose types could not be read

No change of behaviour.  `PostgresLive.PrepareAsync` gains a comment: the one known cause is a connection that the server ended between `PREPARE` and the read, the statement is gone with its backend, and a lost connection carries no position, so nothing is counted against the `PREPARE`'s text.  If a call is ever added between the two, this needs revisiting.

Gap 8 changes the other path out of the read, a statement that is not listed; see below.

### 5. A statement that `PREPARE` does not take

Research on PostgreSQL 14 to 18 found:

- `PREPARE` takes `SELECT`, `(SELECT …)`, `TABLE t`, `VALUES`, `INSERT`, `UPDATE`, `DELETE`, `WITH …` around any of those, and `MERGE` from 15.
- It refuses `CALL`, `CREATE`, `EXPLAIN`, `SHOW`, `SET`, `COPY`, `DO`, `TRUNCATE`, `LOCK`, `REFRESH`, `LISTEN`, `NOTIFY`, `VACUUM`, `ANALYZE`, transaction control, `DECLARE`, `EXECUTE` and the rest with `42601 syntax error at or near "<word>"` at the word.
- A real typo, `SELEC 1`, gets the same `42601` at character 1, so the server's error cannot tell the two apart.
- `SELECT … INTO` prepares on 15 and later, and on 14 is `42P14 utility statements cannot be prepared` with no position.

So:

- Before anything is sent, read the statement's first word: the first `Text` word after comments, hints, white space and any leading `(`.  If it is on a list of words to refuse, fail with `SQLSRC233` naming the word in upper case.  The list holds words to refuse, not words to allow, so that a typo still reaches the server and stays `SQLSRC225`.  It covers at least: `ABORT`, `ALTER`, `ANALYZE`, `BEGIN`, `CALL`, `CHECKPOINT`, `CLOSE`, `CLUSTER`, `COMMENT`, `COMMIT`, `COPY`, `CREATE`, `DEALLOCATE`, `DECLARE`, `DISCARD`, `DO`, `DROP`, `END`, `EXECUTE`, `EXPLAIN`, `FETCH`, `GRANT`, `IMPORT`, `LISTEN`, `LOAD`, `LOCK`, `MOVE`, `NOTIFY`, `PREPARE`, `REASSIGN`, `REFRESH`, `REINDEX`, `RELEASE`, `RESET`, `REVOKE`, `ROLLBACK`, `SAVEPOINT`, `SECURITY`, `SET`, `SHOW`, `START`, `TRUNCATE`, `UNLISTEN`, `VACUUM`.  The builder checks it against the PostgreSQL 18 grammar's statement keywords.
- `SELECT … INTO` is refused on every version too, though it prepares on 15 and later: it creates a table, as `CREATE TABLE AS` does, and a sidecar must not depend on the server it was described against.  It is told from the lexemes: a word `INTO` in a `Text` lexeme at parenthesis depth 0 that does not directly follow `INSERT` or `MERGE` (with only white space and comments between).  `WITH … SELECT … INTO` is the same.  Its `{1}` is `SELECT INTO`.
- In `PostgresErrors.Prepare`, `42P14` from the server is `SQLSRC233` too, as a backstop for anything the tool's checks miss that the server names this way.  Its `{1}` there is `utility`, so the message reads "is a utility statement", and never a word such as `SELECT` that the help line says is described.
- The query still fails, so its file's sidecar is still held back, as for any failed query.  The help names the one-line fix.  The tool does not decide by itself that the query needs no entry: that would override a setting the user wrote.

The order of the checks before sending: several statements (`SQLSRC230`), then the statement kind (`SQLSRC233`), then a `$n` (`SQLSRC234`), then the declared types (`SQLSRC228`).  The builder may reorder if there is a reason, and says why.

Unit tests cover each listed word, a word after a line comment, a block comment and a hint, `(SELECT 1)` (not refused), `WITH … INSERT INTO` (not refused), `INSERT INTO` and `MERGE INTO` (not refused), `TABLE t` (not refused), `SELEC 1` (not refused), `SELECT … INTO t FROM` and `WITH … SELECT … INTO` (refused), and `INTO` inside a string, a comment or parentheses (not refused).  A container test covers `CALL` and `SELECT … INTO` on both servers, each refused with `SQLSRC233` before anything is sent, and a replayed `42P14` answer covers the backstop.

### 5, the follow-up: D-0002

Add `docs/deferred/D-0002-describing-call-and-data-definition.md`, take the id from `Next id` in `docs/deferred/README.md`, and add its row, following `docs/deferred/AGENTS.md`.  Its content, from the research:

- **Planned**: the epic design describes queries a user keeps in `.sql` files.  It puts stored procedures out of scope (`2026-10-07-query-generation-epic-design.md`, the list of what is out of scope), and phase 8 generates a `rowcount` method for a statement without rows and always uses `CommandType.Text`, so a `CALL` could have a method.
- **Delivered instead**: `SQLSRC233`, and `-- output: sql` as the way to keep such a query.
- **Why deferred**: `PREPARE` cannot take them, and the other route has open problems.
- **Remaining work**: describe through extended-protocol Parse/Describe of the unprepared statement through Npgsql.  `NpgsqlCommandBuilder.DeriveParameters` on the `@name` SQL gives the parameter types, keeping a domain, and a SchemaOnly reader gives a procedure's OUT and INOUT arguments as a row.  To settle: a declared `-- param:` type cannot be carried through Parse (the epic's objection to `DeriveParameters`), so it must be refused or compared; `DeriveParameters` throws an `ArgumentException` for a type Npgsql did not load; an overloaded procedure called with an untyped parameter resolves to `text` without a word; there is no walk or table match, so OUT columns are nullable; and the call needs a capture and fixtures of its own.  Of DDL, only `CREATE TABLE AS` and `SELECT INTO` take a parameter; `SET` and `NOTIFY` refuse one, and the rest cannot hold one.
- **Trigger**: phase 8, or a user who asks for a method for a `CALL`.

### 6. A server older than PostgreSQL 14

After `open` answers, read the major version from the version the session already records (`ServerInfo.Version`, from `PostgresSession.VersionOf`): the digits before the first character that is not a digit, so `13.12` is 13 and `18beta1` is 18.  A version with no leading digits is not refused: the tool cannot tell, and the server's own errors stay what they are today.  Below 14, the session fails with `SQLSRC235`, whose arguments are the database, the version as recorded and the floor.  The floor is one constant in the describer.  The version comes from the answer the captures already hold, so no recorded fixture changes.

A unit test opens through a replayed `open` answer with `13.12`.  No container test, since CI has no 13 and none is added.

### 7. `int2vector` and `oidvector`

In [`PostgresTypes`](../../../src/SqlSource.Tool/Describing/Postgres/PostgresTypes.cs), `IsArray` is false for OIDs 22 (`int2vector`) and 30 (`oidvector`), so they are described as kind `base` with the name `format_type` gives, `int2vector` and `oidvector`.  The closure query may still fetch their element's row; that is harmless.  A container test describes a parameter compared with `pg_index.indkey` and one with `pg_proc.proargtypes`, and the check must pass, which proves Npgsql binds the names.  If Npgsql refuses either, that is `SQLSRC232` and goes on its list of known refusals.

### 8. A proxy that pools by transaction

Refuse it clearly; do not support it.

- **At `open`**: when the connection string sets `Multiplexing=true`, fail the session with `SQLSRC236` before connecting.  Read it with `NpgsqlConnectionStringBuilder`, where `PostgresConnection` already builds its data source, so an unreadable string stays `SQLSRC224`.
- **A statement that goes missing**: `PostgresLive.PrepareAsync` throws today when `pg_prepared_statements` does not list the statement it just prepared.  It answers an error instead, with a fixed message that `PostgresErrors.Prepare` maps to `SQLSRC236`, in the way `PostgresConnection.LostConnection` is a fixed message.
- **The other symptoms**: a later call on another backend gets `26000 prepared statement "sqlsource_n" does not exist` (`columns`, `explain`), and a backend that a past run left a statement on gets `42P05 prepared statement "sqlsource_n" already exists` at `prepare`.  Both are mapped to `SQLSRC236` by their SQLSTATE alone, not by reading the message, which `lc_messages` may translate.  That is safe because at those calls the text the tool sends holds only its own statement's name, and a user's SQL that could cause them, an `EXECUTE` or a `PREPARE`, is refused before sending by gap 5.  `deallocate`'s error stays logged and never reported.
- **Docs**: `src/SqlSource.Tool/README.md`, section on PostgreSQL, and the root `README.md` if it says where the tool connects, say that the tool needs a session of its own: a direct connection, not a pooler in transaction mode such as PgBouncer's, and not `Multiplexing=true`.  It names the reason and that hosted providers offer a direct endpoint.

Proving it: a replay test for each mapped answer (a missing statement, `26000`, `42P05`), and a unit test for `Multiplexing=true`.  The builder tries the describer once through PgBouncer in transaction mode in a throwaway container to confirm which symptom appears first, and records the result in the pull request.  No PgBouncer container joins CI.

### 9. An exception of `open` that is none of the two the describer knows

Read Npgsql 10's source for what `NpgsqlDataSource.OpenConnectionAsync` and the data source builder throw for each connection-string keyword that names a file or a service: `SslCertificate`, `SslKey`, `SslPassword`, `RootCertificate`, `Passfile`, `KerberosServiceName` and any others found, and for GSS on a platform without it.  For each exception type that a user can cause and that is not an `NpgsqlException`, `PostgresConnection.OpenAsync` catches it and throws an `NpgsqlException` with a fixed message, as it does for the `ArgumentException` of an unreadable string today.  The message names the kind of problem, such as "a file the connection string names cannot be read", and never repeats the driver's text, which may quote the value.  The list found, with where each comes from, goes into the remarks of `PostgresConnection.OpenAsync`.

A unit test for each mapped case that can be made without a server, a missing certificate file at the least.  If Npgsql wraps one of these causes in an `NpgsqlException` whose message quotes a path, that message passes as today: it is the case TD-0034 already records (a driver's message that quotes one keyword of the value), and this work does not widen it.

## Docs and references

In the same pull request:

- Delete `docs/tech-debt/TD-0043-small-gaps-of-the-postgresql-describers-parameters.md` and its row in `docs/tech-debt/README.md`.
- Remove every reference to TD-0043: the remarks of `PostgresTypeNames`, the comment in `NpgsqlCheckTests`, and the line in `src/SqlSource.Tool/AGENTS.md` that names TD-0043 beside TD-0044 (keep TD-0044).
- `docs/diagnostics.md`: four new sections, `SQLSRC225`'s paragraph on `CALL`, `SQLSRC224` if gap 9 adds a line a user may see, and `SQLSRC232`'s list if gap 2 or 7 adds a refusal.
- `src/SqlSource.Tool/README.md`: the direct connection (gap 8) and `-- output: sql` for a `CALL` (gap 5).
- `docs/deferred/D-0002-*.md` and its row.
- The epic design's line on the next free diagnostic id.
- Before opening the pull request, the version check that `CLAUDE.md` requires.

## Left to the builder

- Where the classifiers live (gap 1 and gap 5 may share one pass over the lexemes), and their names.
- The exact wording of messages and help lines, in the house style.
- Whether `ParameterOids` asserts the counts are equal (gap 1).
- The final list of refused words (gap 5), checked against the grammar.
- The exceptions found in gap 9, and their messages.
- Which tests are unit, replay or container tests, within what each section asks for.
