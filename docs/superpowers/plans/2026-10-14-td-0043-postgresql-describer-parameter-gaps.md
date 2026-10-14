# TD-0043, small gaps of the PostgreSQL describer's parameters - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the nine gaps of TD-0043. Each one ends as a correct description, as an error that names its cause and its fix, or with its reason recorded in the code. Then delete the item.

**Architecture:** Two checks run on the lexemes before anything is sent:
- the statement's kind (`CALL`, DDL, `SELECT … INTO`)
- a `$n` that the SQL writes itself

Three new mappings of what the driver or the server answered:
- a server older than PostgreSQL 14
- a connection that does not keep one session (`Multiplexing=true`, a statement that went missing, `26000`, `42P05`)
- the other exceptions of `open`

Smaller changes: `int2vector` and `oidvector` become base types, and a test pins the bare spelling of a schema whose name is a reserved word. All of it lives in `src/SqlSource.Tool/Describing/Postgres/` and the tool's descriptors.

**Tech Stack:** C# (.NET 10 tests, `net8.0` tool), Npgsql 10.0.3, xunit v3 with Shouldly, Testcontainers PostgreSQL 14 and 18.

**Spec:** `docs/superpowers/specs/2026-10-14-td-0043-postgresql-describer-parameter-gaps-design.md`. Read it with this plan: the spec gives the reasons, and the plan gives the names and the tests.

## Global Constraints

- New ids, exactly: `SQLSRC233` Statement cannot be described; `SQLSRC234` Query writes a positional parameter; `SQLSRC235` Server version is not supported; `SQLSRC236` Connection does not keep a session.
- The floor is PostgreSQL **14**. It is one constant.
- A query that `SQLSRC233` refuses still fails, and so still holds back its file's sidecar. The tool never decides on its own that such a query needs no entry.
- The list of first words is a list of words to **refuse**, never a list of words to allow. `SELEC 1` must stay `SQLSRC225`.
- No new message quotes the driver's text or the connection string. The new errors of gaps 8 and 9 use fixed text.
- `26000` and `42P05` are matched by SQLSTATE alone, never by their message.
- Recorded fixtures under `tests/SqlSource.Tool.Tests/Fixtures/Describers/` must not change. The version is read from the `open` answer that the captures already hold.
- Every commit passes `./pre-commit-validation.sh` (`CLAUDE.md`). Run it after the last edit and before each commit.
- Test names are `Method_Scenario_Outcome`, assertions are Shouldly's, and tests mirror the source folders (`tests/AGENTS.md`).
- Before the PR, run the version check that `CLAUDE.md` requires. The remote has no `v*` tags today, so it should find nothing to compare.

## Review Focus

These are the inputs most likely to reach a user that no single task naturally tests. Each line names the task that adds its test.

1. A refused word in lower or mixed case after a leading comment, such as `-- archive\ncall archive(@id)`, is refused like upper case. *(Task 2)*
2. A `$1` inside an escape string, `E'cost $1'`, or a Unicode string, `U&'…'`, is not a parameter. *(Task 3)*
3. The `Multiplexing` keyword in another case, or set false: `multiplexing=True` is refused and `Multiplexing=false` is not. *(Task 5)*
4. Version strings as distributions write them: `14.10 (Ubuntu 14.10-1)` passes, `9.6.24` is refused as 9, and `18beta1` passes. *(Task 4)*
5. A `26000` that `deallocate` answers stays logged and never fails a describe that otherwise succeeded. *(Task 5)*

---

### Task 1: The four descriptors and their documentation

**Files:**
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`: four descriptors after `TypeCannotBeBound`, and added to the list at the end of the file
- Modify: `src/SqlSource/AnalyzerReleases.Unshipped.md`: four rows after `SQLSRC232`
- Modify: `docs/diagnostics.md`: four sections after `## SQLSRC232`. In `## SQLSRC225`, replace the paragraph that begins "A statement that the server cannot prepare" with one sentence pointing to `SQLSRC233`.
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`: "the later phases take ids from `SQLSRC233`" becomes `SQLSRC237`
- Test: `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs` (exists, unchanged)

**Interfaces:**
- Produces: `ToolDiagnostics.StatementCannotBeDescribed` (`SQLSRC233`, message `The query '{0}' is a {1} statement, which the tool cannot describe`), `ToolDiagnostics.PositionalParameterWritten` (`SQLSRC234`, `The query '{0}' writes the parameter '{1}' itself`), `ToolDiagnostics.ServerVersionNotSupported` (`SQLSRC235`, `The database '{0}' runs PostgreSQL {1}, and the tool supports {2} and later`), `ToolDiagnostics.SessionNotKept` (`SQLSRC236`, `The connection to the database '{0}' does not keep one session`). Each has the titles of the Global Constraints and follows the shape of `QueryHasSeveralStatements`.

- [ ] **Step 1: Add the four descriptors only, and run the document test to see it fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class "*DiagnosticsDocumentTests"`
Expected: FAIL, naming the four sections that are missing.

- [ ] **Step 2: Write the four sections, and update `SQLSRC225`, `AnalyzerReleases.Unshipped.md` and the epic's id line**

Each section follows the house style of `## SQLSRC230`: a bold title, what happened, a `console` example, and what to do. The help lines are in the spec, under "New diagnostics". The sections must also say:
- `SQLSRC233`: it is found before anything is sent; the fix is `-- output: sql`; `SELECT … INTO` is refused on every version; a `42P14` from the server is reported the same way, as a `utility` statement; and D-0002 tracks describing these statements.
- `SQLSRC235`: it is reported once for the database, as `SQLSRC224` is.
- `SQLSRC236`: its causes are `Multiplexing=true` (at open) and a pooler in transaction mode such as PgBouncer (at a query). The fix is a direct connection.

- [ ] **Step 3: Run the document test, and see it pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class "*DiagnosticsDocumentTests"`
Expected: PASS

- [ ] **Step 4: Validate and commit**

Run `./pre-commit-validation.sh`, then:
```bash
git add src/SqlSource/Diagnostics/ToolDiagnostics.cs src/SqlSource/AnalyzerReleases.Unshipped.md docs/diagnostics.md docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
git commit -m "Add the descriptors of SQLSRC233 to SQLSRC236"
```

### Task 2: Refuse a statement that `PREPARE` does not take (gap 5)

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/StatementShape.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`: `NotDescribable`, `NotDescribableHelp`, and `42P14` in `Prepare`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`: `DescribeAsync`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/StatementShapeTests.cs` (new), `PostgresSessionTests.cs`, `PostgresErrorsTests.cs`
- Test: `tests/SqlSource.Tests.Postgres/Describer/ParameterTests.cs`

**Interfaces:**
- Consumes: `ToolDiagnostics.StatementCannotBeDescribed` (Task 1).
- Produces:
  - `internal static class StatementShape`, with `public static string? RefusedKind(string sql)`. It returns the refused kind in upper case (`"CALL"`, `"CREATE"`, `"SELECT INTO"`), or null for a statement to send. It throws `InvalidOperationException` when the SQL does not lex, as `ParameterPlaceholders` does.
  - `PostgresErrors.NotDescribable(DescribeRequest query, string kind) -> DescribeFailure`: `SQLSRC233`, arguments `[query.Name, kind]`, step `DescribeParameters`, no server lines, help `PostgresErrors.NotDescribableHelp`.
  - Task 3 adds a second method to `StatementShape`.

- [ ] **Step 1: Write the failing tests of `StatementShape.RefusedKind`**

`StatementShapeTests`:
- `RefusedKind_RefusedFirstWord_IsThatWord` is a theory over every word of the spec's list. Each case is `"<word> x"` and expects the word in upper case. Add the cases `"call p(@a)"` → `"CALL"`, `"-- archive\ncall archive(@id)"` → `"CALL"`, `"/* c */ CREATE TEMP TABLE t (a int)"` → `"CREATE"` and `"/*+ hint */ TRUNCATE t"` → `"TRUNCATE"`.
- `RefusedKind_StatementPrepareTakes_IsNull` is a theory over `SELECT 1`, `(SELECT 1)`, `((SELECT 1) UNION (SELECT 2))`, `TABLE users`, `VALUES (1)`, `INSERT INTO t VALUES (1)`, `UPDATE t SET a = 1`, `DELETE FROM t`, `MERGE INTO t USING s ON true WHEN MATCHED THEN DELETE`, `WITH d AS (DELETE FROM t RETURNING *) INSERT INTO u SELECT * FROM d`, `SELEC 1`, `SELECT 'INTO' FROM t`, `SELECT 1 -- INTO t`, and `SELECT (SELECT 1 INTO t)`.
- `RefusedKind_SelectInto_IsSelectInto` is a theory over `SELECT * INTO t FROM users`, `select a into t from users`, and `WITH x AS (SELECT 1) SELECT * INTO t FROM x`. Each expects `"SELECT INTO"`.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "*StatementShapeTests"`
Expected: FAIL, because `StatementShape` does not exist.

- [ ] **Step 2: Implement `StatementShape.RefusedKind`**

Lex with `SqlLexer.Lex(sql, SqlDialectRules.PostgreSql)`. Words are read from `Text` lexemes only. A word is a run of letters, digits, `_` and `$`; compare words ignoring case.
- **The first word.** Skip comment, block comment and hint lexemes, white space, and `(`. If the first thing left is not a word, return null.
- **The list.** Hold the refused words in a `FrozenSet<string>` with `StringComparer.OrdinalIgnoreCase`. Before you merge, check the spec's list against the statement keywords of PostgreSQL 18's grammar (`gram.y`, `stmt:`), and list the words you added in the class's remarks.
- **`SELECT … INTO`.** Track the parenthesis depth across `Text` lexemes. A word `INTO` at depth 0 whose previous word is neither `INSERT` nor `MERGE` makes the kind `"SELECT INTO"`. Comments may stand between the two words.

- [ ] **Step 3: Run the tests, and see them pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "*StatementShapeTests"`
Expected: PASS

- [ ] **Step 4: Write the failing session and errors tests**

In `PostgresSessionTests`:
- `DescribeAsync_CallStatement_IsSqlsrc233AndAsksNothing`: the script is `Open()` alone. The SQL `CALL archive(@id)` is refused with `SQLSRC233`, and the arguments are `["Q", "CALL"]`.
- `DescribeAsync_SeveralStatementsThatStartWithCall_IsSqlsrc230`: the SQL `CALL a(); CALL b()` is refused with `SQLSRC230`. It pins the order: statements are counted before the kind is read.

In `PostgresErrorsTests`:
- `Prepare_UtilityStatementsCannotBePrepared_IsSqlsrc233`: the error `new PostgresServerError("42P14", "utility statements cannot be prepared", null, null, null)` gives `SQLSRC233` with arguments `[query.Name, "utility"]`. Its server lines are `["42P14: utility statements cannot be prepared"]`.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "*PostgresSessionTests" --filter-class "*PostgresErrorsTests"`
Expected: FAIL

- [ ] **Step 5: Implement**

- In `PostgresSession.DescribeAsync`, call `StatementShape.RefusedKind(request.Sql)` after the `Rewrite` check and before `TypeList`. On a kind, `return Failed(PostgresErrors.NotDescribable(request, kind))`.
- In `PostgresErrors.Prepare`, before `DeclaredAt`, map `42P14` to `SQLSRC233`. The kind is `"utility"`, and the lines are `ServerLines(error, head)`.
- Update `Prepare`'s summary to name `SQLSRC233`.

- [ ] **Step 6: Write the container tests**

In `ParameterTests`, `Describe_StatementPrepareDoesNotTake_IsSqlsrc233AndSendsNothing` is a theory over `CALL p(@a)`, `CREATE TEMP TABLE t AS SELECT @a::int` and `SELECT @a::int AS a INTO TEMP t`. Each is refused with `SQLSRC233`, and `MethodsOf(capture)` is `["open"]`. Use the pattern of `Describe_TwoStatements_IsSqlsrc230AndSendsNothing`.

Run, on both images:
- `dotnet test --project tests/SqlSource.Tests.Postgres --filter-class "*ParameterTests"`
- `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres --filter-class "*ParameterTests"`

Expected: PASS on both.

- [ ] **Step 7: Validate and commit**

```bash
git add src/SqlSource.Tool/Describing/Postgres tests/SqlSource.Tool.Tests/Describing/Postgres tests/SqlSource.Tests.Postgres/Describer/ParameterTests.cs
git commit -m "Refuse a statement that PREPARE does not take as SQLSRC233"
```

### Task 3: Refuse a `$n` that the SQL writes itself (gap 1)

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/StatementShape.cs`, `PostgresErrors.cs`, `PostgresSession.cs` (`DescribeAsync`; the comments of `ParameterOids` and `ArgumentCount`)
- Test: `StatementShapeTests.cs`, `PostgresSessionTests.cs`
- Test: `tests/SqlSource.Tests.Postgres/Describer/ParameterTests.cs` and `NpgsqlCheckTests.cs`: rewrite `Describe_SqlWithAPlaceholderOfItsOwn_*` in both

**Interfaces:**
- Produces:
  - `StatementShape.PositionalParameter(string sql) -> string?`: the first `$n` as written (`"$2"`), or null.
  - `PostgresErrors.PositionalParameter(DescribeRequest query, string written) -> DescribeFailure`: `SQLSRC234`, arguments `[query.Name, written]`, step `DescribeParameters`, help `PostgresErrors.PositionalParameterHelp`.

- [ ] **Step 1: Write the failing tests**

In `StatementShapeTests`:
- `PositionalParameter_DollarAndDigits_IsItAsWritten` covers `SELECT 1 FROM users WHERE id = @id AND name = $2` → `"$2"`, `SELECT @a, $1` → `"$1"`, and `SELECT $12` → `"$12"`.
- `PositionalParameter_NotAParameter_IsNull` covers `SELECT a$1 FROM t`, `SELECT '$1'`, `SELECT E'cost $1'`, `SELECT U&'$1'`, `SELECT $$ $1 $$`, `SELECT $tag$ $1 $tag$`, `SELECT "a$1" FROM t`, `SELECT 1 -- $1`, `SELECT 1 /* $1 */` and `SELECT $ 1`.

In `PostgresSessionTests`:
- `DescribeAsync_PositionalParameterInTheSql_IsSqlsrc234AndAsksNothing`: the script is `Open()` alone, and the arguments are `["Q", "$2"]`.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "*StatementShapeTests" --filter-class "*PostgresSessionTests"`
Expected: FAIL

- [ ] **Step 2: Implement**

- In a `Text` lexeme, a `$` followed by an ASCII digit is a parameter, unless the character before it in the same lexeme is a letter, a digit, `_` or `$`. A `$` at the start of a lexeme is never part of a name.
- `DescribeAsync` calls `PositionalParameter` right after `RefusedKind`, and returns `PostgresErrors.PositionalParameter` on a match.
- Rewrite the comments of `ParameterOids` and `ArgumentCount`: the SQL can no longer hold a `$n` of its own. Whether `ParameterOids` now asserts equal counts is your choice (the spec leaves it open).

- [ ] **Step 3: Rewrite the two container tests**

- In `ParameterTests`, `Describe_SqlWithAPlaceholderOfItsOwn_TypesTheListedParametersAlone` becomes `Describe_SqlWithAPlaceholderOfItsOwn_IsSqlsrc234AndSendsNothing`. Same SQL; it expects `SQLSRC234` with arguments `[Requests.Query, "$2"]` and methods `["open"]`.
- In `NpgsqlCheckTests`, delete `Describe_SqlWithAPlaceholderOfItsOwn_PassesTheCheck` and its comment, which names TD-0043. The `ParameterTests` case now covers it.

Run: `dotnet test --project tests/SqlSource.Tests.Postgres --filter-class "*ParameterTests" --filter-class "*NpgsqlCheckTests"`
Expected: PASS

- [ ] **Step 4: Validate and commit**

```bash
git add src/SqlSource.Tool/Describing/Postgres tests/SqlSource.Tool.Tests/Describing/Postgres tests/SqlSource.Tests.Postgres/Describer
git commit -m "Refuse a positional parameter that the SQL writes itself as SQLSRC234"
```

### Task 4: Refuse a server older than PostgreSQL 14 (gap 6)

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`: `OpenAsync`; `MajorVersionOf` and `MinimumMajorVersion`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`: `VersionNotSupported`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`

**Interfaces:**
- Produces:
  - `PostgresSession.MinimumMajorVersion` (`public const int`, value `14`).
  - `PostgresSession.MajorVersionOf(string version) -> int?`: the leading ASCII digits, or null when there are none.
  - `PostgresErrors.VersionNotSupported(string database, string version) -> DescribeFailure`: `SQLSRC235`, arguments `[database, version, "14"]` (from the constant), step null, help `PostgresErrors.VersionNotSupportedHelp`.

- [ ] **Step 1: Write the failing tests**

- `MajorVersionOf_VersionAsRecorded_IsItsLeadingDigits` covers `"18.4"` → 18, `"13.12"` → 13, `"9.6.24"` → 9, `"18beta1"` → 18 and `"devel"` → null.
- `OpenAsync_ServerOlderThanTheFloor_IsSqlsrc235` is a theory over `"13.12"` and `"9.6.24"`. The script answers `open` with `PostgresScript.Opened with { ServerVersion = v }`; expect `SQLSRC235` with arguments `[PostgresScript.Database, v, "14"]` and step null.
- `OpenAsync_ServerAtOrAboveTheFloor_Opens` is a theory over `"14.0"`, `"18beta1"` and `"devel"`.

A version string from the server such as `14.10 (Ubuntu 14.10-1)` is already cut to `14.10` by `VersionOf`, so `VersionOfTests` (or the existing `VersionOf` test) gains that case if it lacks it.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "*PostgresSessionTests"`
Expected: FAIL

- [ ] **Step 2: Implement**

In `OpenAsync`, after the error check, build `ServerOf(answer)`. If `MajorVersionOf(server.Version) is { } major && major < MinimumMajorVersion`, return the failure. The `finally` already disposes the connection when `kept` is false.

- [ ] **Step 3: Run the tests, see them pass, validate and commit**

```bash
git add src/SqlSource.Tool/Describing/Postgres tests/SqlSource.Tool.Tests/Describing/Postgres
git commit -m "Refuse a PostgreSQL older than 14 as SQLSRC235"
```

### Task 5: Refuse a connection that does not keep one session (gap 8)

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs`: `MultiplexingRefused`, and the check in `BuildSource`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs`: `PrepareAsync` answers `StatementNotListed` instead of throwing
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`: `SessionNotKept`; `Open`, `Prepare` and `Columns` map to it
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`: a `_database` field from `OpenRequest.Database`; `WalkAsync` returns a failure for `26000`
- Modify: `src/SqlSource.Tool/README.md`: the `## PostgreSQL` section
- Test: `PostgresConnectionTests.cs`, `PostgresErrorsTests.cs`, `PostgresSessionTests.cs`

**Interfaces:**
- Produces:
  - `PostgresConnection.MultiplexingRefused` (`public const string`, `"Multiplexing=true does not keep one session"`).
  - `PostgresLive.StatementNotListed` (`public const string`, `"the server does not list the statement it prepared"`).
  - `PostgresErrors.SessionNotKept(string database, DescribeStep? step, EquatableArray<string> serverLines) -> DescribeFailure`: `SQLSRC236`, arguments `[database]`, help `PostgresErrors.SessionNotKeptHelp`.
  - New signatures: `PostgresErrors.Prepare(PostgresServerError error, DescribeRequest query, PostgresPrepareRequest prepare, string database)` and `PostgresErrors.Columns(PostgresServerError error, DescribeRequest query, string database)`.
  - `PostgresSession.WalkAsync` returns `Task<Outcome<WalkOutcome, DescribeFailure>>`.

- [ ] **Step 1: Write the failing tests**

In `PostgresConnectionTests`:
- `OpenAsync_Multiplexing_ThrowsRefusedBeforeConnecting` is a theory over `"Host=127.0.0.1;Port=1;Multiplexing=true"` and `"host=127.0.0.1;port=1;multiplexing=True"`. Each throws `NpgsqlException` whose `Message` is `PostgresConnection.MultiplexingRefused`. Port 1 proves that nothing connected: a connect would fail with another message.
- `OpenAsync_MultiplexingFalse_IsNotRefused`: `"Host=127.0.0.1;Port=1;Multiplexing=false"` throws an `NpgsqlException` whose message is not `MultiplexingRefused`.

In `PostgresErrorsTests`:
- `Open_MultiplexingRefused_IsSqlsrc236`: `new PostgresServerError(null, PostgresConnection.MultiplexingRefused, null, null, null)` gives `SQLSRC236` with arguments `["app"]` and step null.
- `Prepare_StatementNotListedOrAlreadyExists_IsSqlsrc236` is a theory over the error with message `StatementNotListed` and no state, and `42P05`. Step `DescribeParameters`.
- `Columns_PreparedStatementDoesNotExist_IsSqlsrc236`: `26000` at step `DescribeColumns`.

In `PostgresSessionTests`:
- `DescribeAsync_ExplainAnswers26000_IsSqlsrc236`: a gated query whose `explain` answers `26000`.
- `DescribeAsync_DeallocateAnswers26000_IsDescribed`: the describe succeeds, and the `deallocate` event is still logged (Review Focus 5).

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "*PostgresConnectionTests" --filter-class "*PostgresErrorsTests" --filter-class "*PostgresSessionTests"`
Expected: FAIL

- [ ] **Step 2: Implement**

- **`BuildSource`.** Throw `new NpgsqlException(MultiplexingRefused)` when `builder.ConnectionStringBuilder.Multiplexing` is true, before `Build()`. An unreadable string still throws its `ArgumentException` from the builder's constructor first.
- **`PrepareAsync`.** When the read finds no row, return `new PostgresPrepareAnswer(null, new PostgresServerError(null, StatementNotListed, null, null, null))`. Add the comment for gap 4: the one known cause of a read that fails is a lost connection, which has no position, and this needs revisiting if a call is ever added between the two.
- **The mapping.** `Open` maps the refused message with no SQLSTATE. `Prepare` checks `StatementNotListed` (no SQLSTATE) and `42P05` before anything else. `Columns` maps `26000`. Each passes `ServerLines`, without the position.
- **The walk.** `DescribePreparedAsync` returns the walk's failure. Any other `explain` error keeps today's behaviour: logged, and the columns keep the heuristic.
- **The tool's README.** In `## PostgreSQL`, after the paragraph on the connection string, add: the tool needs a session of its own; use a direct connection, not a pooler in transaction mode such as PgBouncer's, and not `Multiplexing=true`; hosted providers offer a direct endpoint; otherwise the run reports `SQLSRC236` (absolute link, like the others in the file).

- [ ] **Step 3: Run the tests, and see them pass**

Same command as Step 1. Expected: PASS

- [ ] **Step 4: Try it once through PgBouncer**

Run a throwaway PostgreSQL 18 container with PgBouncer in transaction mode in front of it, for example `edoburu/pgbouncer` with `POOL_MODE=transaction`. Point `dotnet run --project src/SqlSource.Tool -- describe` at a scratch copy of `tests/SqlSource.Tool.Tests/Fixtures/Projects/Single/` through it, and note which error appears first. Remove the containers afterwards. Put the result in a note for the PR description. No file of the repository changes in this step.

- [ ] **Step 5: Validate and commit**

```bash
git add src/SqlSource.Tool tests/SqlSource.Tool.Tests/Describing/Postgres
git commit -m "Refuse a connection that does not keep one session as SQLSRC236"
```

### Task 6: Types: `int2vector`, `oidvector` and a reserved-word schema (gaps 7 and 2)

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresTypes.cs`: `IsArray`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresTypeNames.cs`: the remarks
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresTypesTests.cs`
- Test: `tests/SqlSource.Tests.Postgres/Describer/ParameterTests.cs` (describe), `NpgsqlCheckTests.cs` (bind)
- Modify (only if Npgsql refuses a name): `docs/diagnostics.md`, the list of known refusals in `## SQLSRC232`

- [ ] **Step 1: Write the failing unit test**

`Build_Int2vectorAndOidvector_AreBaseTypes` is a theory over `(22, "int2vector", elem 21)` and `(30, "oidvector", elem 26)`. Each row has `Typtype "b"` and `Typcategory "A"`, built with the helpers of `PostgresRows.cs`. The result has kind `base`, its name is the row's `format_type`, and there is no element.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "*PostgresTypesTests"`
Expected: FAIL (today the kind is `array`).

- [ ] **Step 2: Implement**

`IsArray` is false for OIDs 22 and 30. Name the two OIDs as constants with a comment: they are of the category `A` with an element, and they are not arrays.

- [ ] **Step 3: Write the container tests**

- `Describe_ParameterComparedWithInt2vectorOrOidvector_IsABaseType` covers `SELECT 1 FROM pg_index WHERE indkey = @k` → `int2vector`, and `SELECT 1 FROM pg_proc WHERE proargtypes = @a` → `oidvector`. Each expects kind `base`, and the check passes (the describe succeeds).
- `Describe_TypeOfASchemaNamedByAReservedWord_IsSpelledBare` creates `SCHEMA "user"` and `TYPE "user".status AS ENUM ('a')` in a `try`/`finally` that drops the schema `CASCADE`, using the pattern of `Canary`. It describes `SELECT 1 WHERE @s = 'a'::"user".status` and expects the parameter's name `user.status`.

Run on both images: `dotnet test --project tests/SqlSource.Tests.Postgres --filter-class "*ParameterTests"` (and again with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14`).

- **If both pass:** in `PostgresTypeNames`'s remarks, replace "a reserved word is not quoted, which TD-0043 records" with the reason. The name is what Npgsql binds, Npgsql takes `user.status` bare and refuses any quoted name, and the test pins it.
- **If Npgsql refuses either name:** the describe is `SQLSRC232`. Change the test to expect it, and add the type to `## SQLSRC232`'s list. The remarks still say why no name is quoted for a reserved word.

- [ ] **Step 4: Validate and commit**

```bash
git add src/SqlSource.Tool/Describing/Postgres tests docs/diagnostics.md
git commit -m "Describe int2vector and oidvector as base types, and pin the bare spelling of a reserved schema"
```

### Task 7: Exceptions of `open` that are no `NpgsqlException` (gap 9)

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs`: `OpenAsync`, its catches, its remarks, and one constant for each message
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresConnectionTests.cs`, or `tests/SqlSource.Tests.Postgres/Describer/SessionTests.cs` for a case that needs a server
- Modify (if a line a user may see is added): `docs/diagnostics.md`, `## SQLSRC224`

- [ ] **Step 1: Enumerate**

Read Npgsql 10.0.3's source, the tag `v10.0.3` of `npgsql/npgsql`, read only. Find what `NpgsqlDataSourceBuilder.Build`, `NpgsqlDataSource.OpenConnectionAsync` and the connector's TLS and authentication code throw for these keywords: `SslCertificate`, `SslKey`, `SslPassword`, `RootCertificate`, `Passfile`, `KerberosServiceName`, any other keyword that names a file or a service, and GSS on a platform without it. For each, record the exception type and whether Npgsql wraps it in an `NpgsqlException`. That list goes into the remarks of `OpenAsync`.

- [ ] **Step 2: Write the failing tests**

There is one test per unwrapped type that a user can cause, named `OpenAsync_<Cause>_ThrowsTheFixedMessage`. It asserts that `NpgsqlException.Message` equals the new constant and does not contain the path or the value from the connection string.

Use a unit test where the exception is thrown before a server answers. Otherwise use a container test through `Sessions.OpenAsync` that expects `SQLSRC224` with the fixed line. If a cause can only be reached with a TLS server, which the container does not have, the reading in Step 1 is the proof, and the remarks say so.

- [ ] **Step 3: Implement**

Catch each type in `OpenAsync` next to the `ArgumentException`, and throw `new NpgsqlException(<constant>)` without keeping the exception, as the `ArgumentException` catch does. Messages name the kind of problem, such as `"a file that the connection string names cannot be read"`.

An `NpgsqlException` that quotes a path passes as today. TD-0034 covers it, and no change here.

- [ ] **Step 4: Run, validate and commit**

```bash
git add src/SqlSource.Tool/Describing/Postgres tests docs/diagnostics.md
git commit -m "Report what a user can cause at open as SQLSRC224"
```

### Task 8: Close the item

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`: the comment on the `finally` of `DescribeAsync` (gap 3)
- Create: `docs/deferred/D-0002-describing-call-and-data-definition.md`
- Modify: `docs/deferred/README.md` (`Next id` → `D-0003`, plus a row), `docs/tech-debt/README.md` (remove the TD-0043 row), `src/SqlSource.Tool/AGENTS.md` (the line that names TD-0043 beside TD-0044 keeps TD-0044 alone), `src/SqlSource.Tool/README.md` (`-- output: sql` for a `CALL`)
- Delete: `docs/tech-debt/TD-0043-small-gaps-of-the-postgresql-describers-parameters.md`

- [ ] **Step 1: Gap 3's comment**

The comment on the `finally` says three things:
- a cancelled describe leaves its statement to die with the session;
- a cancelled run must not wait on a server, which is the spec's rule;
- the run closes the session next.

- [ ] **Step 2: D-0002**

Write it with the sections of `docs/deferred/AGENTS.md`. Its content is the spec's section "5, the follow-up: D-0002". Its row: `Open`, `2026-10-14`, planned in the epic design, "Describing `CALL` and data definition statements". Link it from `## SQLSRC233` in `docs/diagnostics.md`.

- [ ] **Step 3: The tool's README**

In `## PostgreSQL`, add one sentence: a `CALL` or a statement of data definition in a `.sql` file is `SQLSRC233` unless the query has `-- output: sql`.

- [ ] **Step 4: Delete TD-0043 and every reference**

Run: `grep -rn "TD-0043" --exclude-dir=bin --exclude-dir=obj . | grep -v "docs/superpowers/"`
Expected: no output. The spec and this plan keep their mentions, since they are the record of the work.

- [ ] **Step 5: Validate on both images, and commit**

Run `./pre-commit-validation.sh` and `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres`. Both must pass.

```bash
git add -A docs src/SqlSource.Tool
git commit -m "Close TD-0043 and defer describing CALL and data definition as D-0002"
```

- [ ] **Step 6: Version check before the PR**

Run `git fetch --tags origin && git tag --list 'v*' --sort=-v:refname | head -n 1`. If a tag exists and `VersionPrefix` in `Directory.Build.props` is not greater than it, ask the user for the new version.
