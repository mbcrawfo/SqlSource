# TD-0034 Holes in the Redaction of a Describer's Text Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close four holes of `docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md` (a value spread over an event's strings, a whole value the driver rewrote, a PostgreSQL password from the environment or a password file, a capture that holds the secret) on both describers, and narrow the item to what remains.

**Architecture:** `ConnectionSecret` gains *whole forms* of the value (the value, the value trimmed, and the value as the describer's driver writes it again), each removed wherever it stands, beside the token-matched passwords.  `IQueryDescriber.SecretsOf` takes the host's environment so that PostgreSQL's describer adds `PGPASSWORD` or the password file's passwords; a new `IQueryDescriber.RewriteOf` gives the driver's rewrite.  `SessionRunLog` also searches an event's strings joined, and `CaptureJson` gets a writer that refuses a capture holding the secret.

**Tech Stack:** C# 13 / .NET 8 tool (`src/SqlSource.Tool`), xunit v3 + Shouldly, Npgsql 10.0.3, Microsoft.Data.SqlClient 7.1.1.

**Spec:** `docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md` (Problem and Proposed fix), with the unit's instructions: close the log, changed value, environment password and capture holes; do the same for SQL Server where its driver has the hole; leave the login's name of SQL Server's messages as a recorded decision.

## Where the merged code differs from the item

- The item's Proposed fix says "have `SecretsOf` take the variables the driver reads".  Npgsql 10.0.3 (`NpgsqlConnector.GetPassword`) reads, in order: the string's `Password`; `PGPASSWORD`; the file named by the string's `Passfile`, else `PGPASSFILE`, else `$HOME/.pgpass` (Unix) or `%APPDATA%\postgresql\pgpass.conf` (Windows).  So `SecretsOf` mirrors that order, and the password file is read by the describer itself (the driver reads it the same way); the tool never knew it before.
- The item's "one more part to remove whole" does not fit `SecretsOf`'s parts, which are matched as tokens.  The rewrite gets its own interface member, `RewriteOf`, and `ConnectionSecret` keeps it with the value as a whole form.
- SqlClient reads no password from the environment, so SQL Server's describer gives none; it does give its builder's rewrite.

## Global Constraints

- Owner's decision (2026-10-20), kept: secret parts are redacted only where they stand as a whole token; parts shorter than 4 characters (`ConnectionSecret.MinimumPartLength`) are skipped; `EntrySecretCheck` searches the whole value only, never the parts.  Whole forms (value, trimmed value, rewrite) count as "the whole value".
- The login's name in SQL Server's messages is no secret, by decision: unchanged, recorded in the narrowed item.
- Everything from outside comes through `ToolHost`: environment variables only through `ToolHost.GetEnvironmentVariable`, passed to `SecretsOf`.
- `SecretsOf` and `RewriteOf` never throw (an exception is `SQLSRC200`); both are called inside `DatabaseRuns`' guard of the whole value.
- Do not touch `NpgsqlRunLogger` or `SqlClientRunListener` (td-0048 owns them).
- Every commit passes `./pre-commit-validation.sh`; after a describer change also run `tests/SqlSource.Tests.Postgres` on `postgres:14.24` and `tests/SqlSource.Tests.SqlServer` on `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.
- Docs that name the changed members change in the same commit (`src/SqlSource.Tool/AGENTS.md` rule "A connection's value goes to the describer and nowhere else", `docs/architecture/tool.md` "Where a connection's value goes").

## Review Focus

1. A password file that is missing, unreadable, a folder, or holds malformed lines: `SecretsOf` gives what it can and never throws (Task 3 tests).
2. A value spread over two members where one half also holds a token of a password: joined search must not corrupt the other strings (Task 2 test `Write_ValueSplitOverTwoMembers_...`).
3. A value with a line break logged as two items of a list: found by the `\n` join (Task 2).
4. A recorded fixture whose container password equals a run word (`postgres`): the capture check uses the same run words, so recording still works (Task 4: run `tools/record-fixtures.sh` once and restore).
5. A capture value that the JSON writer escapes (quote, backslash): searched as escaped (Task 4 test).

---

### Task 1: Whole forms of the value in `ConnectionSecret`, `Redaction` and `EntrySecretCheck`

**Files:**
- Modify: `src/SqlSource.Tool/Describing/ConnectionSecret.cs`, `src/SqlSource.Tool/Describing/Redaction.cs`, `src/SqlSource.Tool/Describing/EntrySecretCheck.cs`, `src/SqlSource.Tool/Describing/DatabaseRuns.cs:200-206`
- Test: `tests/SqlSource.Tool.Tests/Describing/ConnectionSecretTests.cs`, `RedactionTests.cs`, `EntrySecretCheckTests.cs`

**Interfaces:**
- Produces: `new ConnectionSecret(string value, IEnumerable<string?>? parts = null, IEnumerable<string>? runWords = null, IEnumerable<string?>? forms = null)`; `ImmutableArray<string> ConnectionSecret.Wholes` (the value, `value.Trim()`, and each form; empty/white-space and duplicates dropped; longest first; `Value` stays); `EntrySecretCheck.Holds(SidecarEntry entry, ConnectionSecret secret)` (replaces the `string` overload).

- [ ] **Step 1: Failing tests**
  - `ConnectionSecretTests.Wholes_ValueWithSpaceAndAForm_AreAllThreeLongestFirst`: `new ConnectionSecret(" Host=db;Password=pw ", forms: ["Host=db;Password=pw;Port=5432"]).Wholes` is `["Host=db;Password=pw;Port=5432", " Host=db;Password=pw ", "Host=db;Password=pw"]`.
  - `Wholes_FormEmptyOrEqualToTheValue_IsDropped`: forms `[null, "", " ", "v=1"]` with value `"v=1"` → `Wholes` is `["v=1"]`.
  - `IsIn_FormAnywhere_IsTrue`: form `"Host=db;Password=pw;Port=5432"` inside `"xHost=db;Password=pw;Port=5432x"` → true.
  - `RedactionTests.Of_RewrittenValueInAMessage_IsTheMark`: `Redaction.Of("bad 'Host=db;Port=5432;Password=hunter22'", new ConnectionSecret("host=db;password=hunter22", ["hunter22"], forms: ["Host=db;Port=5432;Password=hunter22"]))` is `"bad '***'"`.
  - `Of_ValueTrimmedInAMessage_IsTheMark`: value `"Host=db;Database=app \n"`, text `"cannot use Host=db;Database=app"` → `"cannot use ***"`.
  - `Of_Failure_RewriteCutOverTwoServerLines_IsTheMark`: a failure whose server lines are `["a Host=db;Port=5432", "Password=hunter22 b"]` with the form `"Host=db;Port=5432\nPassword=hunter22"` → lines `["a ***", " b"]` (mirror the existing joined-lines test).
  - `EntrySecretCheckTests.Holds_FormInTheServersVersion_IsTrue`: entry with `ServerVersion = "Host=db;Port=5432"`, secret `new ConnectionSecret("host=db", forms: ["Host=db;Port=5432"])` → true.  Update every existing `Holds(x, Secrets.Connection)` / `Holds(x, value)` call to pass `new ConnectionSecret(...)`.
  - `Holds_PasswordInAnEntry_IsFalse`: the owner's decision — `ServerVersion = "hunter22"`, secret with part `"hunter22"` → false.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~ConnectionSecretTests|FullyQualifiedName~RedactionTests|FullyQualifiedName~EntrySecretCheckTests"` → compile failure / FAIL.
- [ ] **Step 3: Implement.** `Redaction.Of(text, secret)` replaces each of `secret.Wholes` (ordinal, longest first) then each part as a token; `OfLines` normalises the line ends of every whole and part; `ConnectionSecret.IsIn` checks every whole.  `EntrySecretCheck.Holds` searches each whole as `SidecarJsonBuilder` escapes it, with the existing "and not in the text without the describer's part" rule per whole.  `DatabaseRuns.NoSecretIn` passes the secret.  Update remarks of all three types.
- [ ] **Step 4: Run** the filter of Step 2 → PASS.
- [ ] **Step 5: Validate and commit** (`./pre-commit-validation.sh`, then `git commit -m "Remove whole forms of a connection's value"`).

### Task 2: Search an event's strings joined in `SessionRunLog`

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Redaction.cs`, `src/SqlSource.Tool/Logging/SessionRunLog.cs`
- Test: `tests/SqlSource.Tool.Tests/Logging/SessionRunLogTests.cs`, `tests/SqlSource.Tool.Tests/Describing/RedactionTests.cs`

**Interfaces:**
- Consumes: `ConnectionSecret.Wholes`, `ConnectionSecret.Parts`.
- Produces: `Redaction.OfJoined(IReadOnlyList<string> texts, ConnectionSecret secret) -> string[]` — each text cleaned alone, then the texts joined with `""` and searched for each whole anywhere and each part as a token; then joined with `"\n"` and searched with the line ends of each whole and part written as `"\n"`.  Each found range writes `Mark` over the slice of each text it overlaps (several slices of one text that touch become one mark); a separator is in no text.

- [ ] **Step 1: Failing tests** (`SessionRunLogTests`, secret `new ConnectionSecret(Secrets.Connection)` unless named):
  - `Write_ValueSplitOverTwoMembers_IsTheMarkInBoth`: members `("a", "x " + first)`, `("b", second + " y")` where `first + second == Secrets.Connection` (cut in the middle) → `("a", "x ***")`, `("b", "*** y")`; a third member `("c", "plain")` is unchanged.
  - `Write_ValueSplitOverTheNameAndAMember_IsTheMarkInBoth`: event name ends with the first half, first member's name starts with the second.
  - `Write_ValueWithALineBreakOverTwoItemsOfAList_IsTheMarkInBoth`: secret `"Host=db;\nPassword=pw"`, list items `["Host=db;", "Password=pw"]` → `["***", "***"]`.
  - `Write_PasswordSplitOverTwoMembers_IsTheMarkInBoth`: secret `("Host=db;Password=hunter22", ["hunter22"])`, members `"pw hunt"`, `"er22 ok"` → `"pw ***"`, `"*** ok"`.
  - `Write_PartsOfAWordInTwoMembers_AreLeft`: same secret, members `"latest_hunt"`, `"er22x"` → unchanged (no token).
  - `RedactionTests.OfJoined_NothingFound_IsTheSameTexts`.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~SessionRunLogTests|FullyQualifiedName~RedactionTests"` → FAIL.
- [ ] **Step 3: Implement** `Redaction.OfJoined`; `SessionRunLog.Write` flattens the name, then each member's name and every string of its value in order (depth first, as written), calls `OfJoined`, and rebuilds the event from the cleaned strings in the same order (prefix before the name, as now).  Update the remarks of `SessionRunLog` (drop "each string alone").
- [ ] **Step 4: Run** Step 2's filter → PASS; also `--filter FullyQualifiedName~Redaction|FullyQualifiedName~Events` (the engines' event tests use `SessionRunLog`).
- [ ] **Step 5: Validate and commit** (`"Search a log event's strings joined for a connection's secret"`).

### Task 3: The environment's password and the driver's rewrite from each describer

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Describers/IQueryDescriber.cs`, `src/SqlSource.Tool/Describing/Postgres/PostgresDescriber.cs`, `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs:110-130`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerDescriber.cs`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerConnection.cs:40-45`, `src/SqlSource.Tool/Describing/DatabaseRuns.cs:98-106`
- Create: `src/SqlSource.Tool/Describing/Postgres/PasswordFile.cs`
- Modify (test describers): `tests/SqlSource.Tool.Tests/FakeDescriber.cs`, `tests/SqlSource.Tool.Tests/Fixtures/FixtureReplayTests.cs:200`, `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs:838`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresDescriberTests.cs`, `tests/SqlSource.Tool.Tests/Describing/Postgres/PasswordFileTests.cs` (new), `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerDescriberTests.cs`, `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`

**Interfaces:**
- Consumes: Task 1's `forms` parameter.
- Produces:
  - `IReadOnlyList<string> IQueryDescriber.SecretsOf(string connection, Func<string, string?> getEnvironmentVariable)`
  - `string? IQueryDescriber.RewriteOf(string connection)` — the value as the driver writes it again (`NpgsqlConnectionStringBuilder.ConnectionString`, `SqlConnectionStringBuilder.ConnectionString`), or null when the driver cannot read it; never throws.
  - `PasswordFile.Passwords(string text) -> IReadOnlyList<string>` — every entry's password, parsed as Npgsql's `PgPassFile` does (trimmed lines; skip empty and `#`; split at unescaped `:`; `\:` and `\\` unescape, any other `\x` kept; exactly five fields else the line is skipped; a `*` password is none).
  - `PasswordFile.PathsOf(string? passfile, Func<string, string?> getEnvironmentVariable) -> IReadOnlyList<string>` — `[passfile]` when set, else `[PGPASSFILE]` when set, else `$HOME/.pgpass` and `$APPDATA/postgresql/pgpass.conf` for whichever variable is set (both: no OS check, the other is only more to remove).
  - PostgreSQL `SecretsOf`: the string's `Password` and `SSL Password`; when it has no `Password`, `PGPASSWORD` when set; when neither, every password of each path of `PathsOf(builder.Passfile, ...)`, read with `File.ReadAllText`, a path that cannot be read (any `IOException`, `UnauthorizedAccessException`, `ArgumentException`, `NotSupportedException`) giving none.  A string the driver cannot read still gives none.
  - `DatabaseRuns`: under the first guard, `SecretsOf(value, host.GetEnvironmentVariable)` and `RewriteOf(value)`; the secret is `new ConnectionSecret(value, parts, [database.Name, engine], [rewrite])`.

- [ ] **Step 1: Failing tests**
  - `PasswordFileTests`: `Passwords_EntriesWithCommentsAndBlankLines_AreEachPassword` (`"# c\n\nh:5432:db:u:pw1\n*:*:*:*:pw2\n"` → `["pw1", "pw2"]`); `Passwords_EscapedColonAndBackslash_AreUnescaped` (`@"h:*:*:u:a\:b\\c\d"` → `[@"a:b\c\d"]`); `Passwords_LineWithoutFiveFields_IsSkipped`; `Passwords_WildcardPassword_IsNone`; `PathsOf_PassfileGiven_IsItAlone`; `PathsOf_PgPassFileSet_IsIt`; `PathsOf_NeitherSet_IsTheDefaultsOfHomeAndAppData`.
  - `PostgresDescriberTests`: existing calls pass `static _ => null`; add `SecretsOf_NoPasswordAndPgPasswordSet_IsPgPassword` (`"Host=h"`, env `PGPASSWORD=envpass1`) → `["envpass1"]`; `SecretsOf_PasswordInTheStringAndPgPasswordSet_IsTheStringsAlone`; `SecretsOf_NoPasswordAndAPasswordFile_IsItsPasswords` (temp file named by `PGPASSFILE`); `SecretsOf_PassfileInTheString_WinsOverPgPassFile`; `SecretsOf_PasswordFileMissingOrAFolder_IsNoneAndDoesNotThrow`; `RewriteOf_ConnectionString_IsTheBuildersText` (`"host=h;password=pw"` → `new NpgsqlConnectionStringBuilder("host=h;password=pw").ConnectionString`); `RewriteOf_ConnectionTheDriverCannotRead_IsNull`.
  - `SqlServerDescriberTests`: existing calls pass `static _ => null`; add `SecretsOf_PasswordVariablesSet_GivesOnlyTheStrings` (env answering every name with `"envpass1"` → `["Sesame123"]`); `RewriteOf_ValueWithAPassword_IsTheBuildersText`; `RewriteOf_ValueSqlClientCannotRead_IsNull` (reuse the existing theory's data).
  - `SecretTests` (through `Cli`, with `FakeDescriber`): `Run_DescribersPasswordFromTheEnvironment_IsTheMarkInAFailure` — the fake's `SecretsOf` returns what its environment function gave for `"FAKE_PASSWORD"`, the host's environment has it, and a failure quoting it prints `***`; `Run_DescribersRewriteInAFailure_IsTheMark` — the fake's `RewriteOf` gives a reordered value and a failure quoting it prints `***`.  Give `FakeDescriber` a `Rewrite` property (default null) and a `SecretsFrom` function (default: `Secrets`).
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~PasswordFileTests|FullyQualifiedName~DescriberTests|FullyQualifiedName~SecretTests"` → compile failure / FAIL.
- [ ] **Step 3: Implement** the interface, both describers, `PasswordFile`, `DatabaseRuns` and the three test describers.  `IQueryDescriber`'s remarks: the environment is the host's, a password the driver takes from it is given, and the rewrite is removed whole.  `PostgresConnection.SecretsOf`'s remarks name Npgsql 10.0.3's order (`NpgsqlConnector.GetPassword`).
- [ ] **Step 4: Run** Step 2's filter → PASS, then the whole of `tests/SqlSource.Tool.Tests`.
- [ ] **Step 5: Validate, floor-image runs, commit.**  `./pre-commit-validation.sh`; `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test tests/SqlSource.Tests.Postgres`; `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test tests/SqlSource.Tests.SqlServer` (background).  Commit `"Remove a password from the environment and the driver's rewrite of a connection"`.

### Task 4: Refuse to write a capture that holds the secret

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Capture/CaptureJson.cs`, `tests/SqlSource.Tool.Testing/Fixtures/FixtureCaseRuns.cs`, `tests/SqlSource.Tool.Testing/Fixtures/FixtureCaseFiles.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/Capture/CaptureJsonTests.cs`

**Interfaces:**
- Consumes: `ConnectionSecret.Wholes`, `.Parts`, `Redaction.HoldsToken`.
- Produces: `CaptureJson.Write(DescribeCapture capture, ConnectionSecret secret) -> Outcome<string, string>` — the file's text, or the failure `"The capture holds the connection's value or a password of it."`.  The text is searched for each whole anywhere and each part as a token, each written as `Options`' encoder writes it inside a string (`JsonSerializer.Serialize(s, Options)[1..^1]`).  `FixtureCaseFiles.WriteRecordingAsync` takes the secret and fails the test with that sentence; `FixtureCaseRuns.ShouldDescribeOrRecordAsync` builds the secret as `DatabaseRuns` does: `new ConnectionSecret(connection, describer.SecretsOf(connection, Environment.GetEnvironmentVariable), [database, engine], [describer.RewriteOf(connection)])`, with the case's planned database.

- [ ] **Step 1: Failing tests** (`CaptureJsonTests`): `Write_WithASecretNotInTheCapture_IsTheFile` (equals `Write(capture)`); `Write_ValueInAnAnswer_IsRefused`; `Write_RewriteInAnAnswer_IsRefused`; `Write_PasswordAsATokenInAnError_IsRefused` (`"password hunter22 is wrong"`); `Write_PasswordInsideAWord_IsTheFile` (`"hunter22x"`); `Write_ValueWithAQuoteAndABackslash_IsFoundAsEscaped` (value `Password="a\b"`).
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tool.Tests --filter FullyQualifiedName~CaptureJsonTests` → FAIL.
- [ ] **Step 3: Implement** the overload and the recorder's use of it.
- [ ] **Step 4: Run** Step 2 → PASS.  Then record both engines once (`tools/record-fixtures.sh`, background) to see the check accept the real captures, and restore the fixtures' files with `git checkout -- tests/SqlSource.Tool.Tests/Fixtures/Describers` when only server versions moved.
- [ ] **Step 5: Validate and commit** (`"Refuse to write a capture that holds a connection's secret"`).

### Task 5: Narrow TD-0034 and update the docs

**Files:**
- Modify: `docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md`, `docs/tech-debt/README.md` (its row), `docs/manual/tool.md` (lines ~81, ~294, ~319), `docs/architecture/tool.md` ("Where a connection's value goes"), `docs/architecture/postgres-describer.md:9`, `docs/architecture/sqlserver-describer.md:11`, `src/SqlSource.Tool/AGENTS.md` (rule at line 67)

- [ ] **Step 1: Rewrite TD-0034** to what remains: a part shorter than four characters; a part equal to a run word; the login's name in SQL Server's messages (owner's decision, no secret); the entry searched for whole forms alone (owner's decision); a value the sidecar holds without the describer; a value that equals a word of the describer's part of an entry; a password equal to a whole word that is printed; a capture's server message (what an `error` may keep, decided with phase 10's first real capture — the writer now refuses one that holds the secret); a password file read whole (every entry's password is removed, not only the one Npgsql would pick); a password the driver takes from somewhere the describer does not know (`ProvidePasswordCallback`, a periodic provider; none is used by the tool).  Keep Impact Low; update Trigger.
- [ ] **Step 2: Update** the README row, the manual's paragraphs and known issue (drop "a value changed" and "split over two fields"; add that `PGPASSWORD` and the password file are hidden), the architecture section (whole forms, `RewriteOf`, the environment, joined log, the capture writer), the two describer pages and the AGENTS.md rule.
- [ ] **Step 3: Run** `./pre-commit-validation.sh` (the documentation tests check links) → PASS.
- [ ] **Step 4: Commit** (`"Narrow TD-0034 to what the redaction still does not cover"`).
