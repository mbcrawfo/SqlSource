# Hardening the describer contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve TD-0041, most of TD-0037 and most of TD-0034, so that phase 6's SQL Server describer is built to the hardened contract.

**Architecture:** A describer gives the secret parts of its connection, `IQueryDescriber.SecretsOf`, and the run carries them with the whole value in a `ConnectionSecret` that every redaction and search takes.  The PostgreSQL describer maps every driver exception through a new `ConnectionErrors`, which keeps the SQLSTATE and puts fixed texts in place of messages that name the host or the user.  `Outcome` gains a constructor that `CA2000` treats as a transfer of ownership.

**Tech Stack:** C# 13, .NET 8 tool, Npgsql 10.0.3, xunit v3 with Shouldly, Testcontainers for PostgreSQL.

**Spec:** `docs/superpowers/specs/2026-10-20-describer-contract-hardening-design.md`

## Global Constraints

- Every commit passes `./pre-commit-validation.sh` (root `AGENTS.md`, Commits).  A task that changes the describer or the container tests also runs `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres`.
- Read the root `AGENTS.md`, `src/SqlSource.Tool/AGENTS.md` and `tests/AGENTS.md` before the first edit.  Their rules beat this plan's wording.
- Test names are `Method_Scenario_Outcome`, assertions are Shouldly's, and every test class is `sealed`.
- Comments and documents follow the repository's prose: plain declarative sentences, two spaces after a full stop.
- An AGENTS.md that names a file, type or member changes in the same commit as the rename.
- Do not touch TD-0023's text (`LoggedArguments`, `UsageCheck`, the manual's sentences about tokens typed in the wrong place).  A later unit owns it.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- A weak password, `test`, beside a name that holds it, `latest_orders`, is not redacted in printed text, and an entry with that column is no fault.  Task 4 has the tests.
- The password `postgres` in the containers is no `EntrySecretCheck` fault, since the engine is `postgres`.  Task 4 runs the container suite to show it.
- `Multiplexing=true` is still `SQLSRC236`, and the describer's own fixed texts still pass at `open`.  Task 5 has the tests.
- A query error at a later call, such as `42P01`, keeps the server's message, detail, hint and position.  Task 5 has the test.
- A connection value given with no parts, as by a describer whose driver cannot read it, is still redacted whole everywhere.  Task 4 has the test.

---

### Task 1: `CA2000` without suppressions (TD-0041)

**Files:**
- Modify: `src/SqlSource.Tool/Outcome.cs`, `.editorconfig`, `src/SqlSource.Tool/Logging/RunLogFile.cs`, `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, `tests/SqlSource.Tool.Tests/FakeDescriber.cs`
- Modify: `src/SqlSource.Tool/AGENTS.md` (the rule "A result that may fail has one shape", and the `FakeDescriber` rule), `docs/architecture/tool.md` (a short subsection under "The command line" or beside `Outcome` in Folders, saying the decision and its cost, as the spec's section 4 says)
- Delete: `docs/tech-debt/TD-0041-ca2000-is-suppressed-on-the-opener-of-the-log.md` and its row in `docs/tech-debt/README.md`
- Test: `tests/SqlSource.Tool.Tests/OutcomeTests.cs`

**Interfaces:**
- Produces: `public Outcome(TValue value)`, a success, beside `Outcome.Of`.  Rule: a value that must be disposed is wrapped with the constructor.

- [ ] **Step 1:** Add to `OutcomeTests` `Constructor_Value_IsASuccessThatHoldsIt`: `new Outcome<string, string>("v")`, so `Succeeded` is true, `Value` is `"v"` and `Failure` is null.
- [ ] **Step 2:** Add the constructor, with a doc comment saying it is how an `Outcome` of a disposable value is made: `.editorconfig` tells `CA2000` that a constructor takes ownership.  Add `dotnet_code_quality.CA2000.dispose_ownership_transfer_at_constructor = true` to the existing `[*.cs]` section of `.editorconfig`, with a comment line that gives the reason and links `docs/architecture/tool.md`.
- [ ] **Step 3:** In the three openers, remove the `[SuppressMessage]` and its now unused `using`, and use `new Outcome<…>(value)` for the value.  Build with `dotnet build SqlSource.slnx`: expected no CA2000.
- [ ] **Step 4:** Update the documents and delete TD-0041, its row and every reference to it (`grep -rn "TD-0041" src tests docs --include='*.cs' --include='*.md'`, leaving the old plans and specs under `docs/superpowers/` alone).
- [ ] **Step 5:** Run `./pre-commit-validation.sh` and commit: `Hand an Outcome's disposable over through its constructor`.

### Task 2: The guard, the log's names and the host's files (TD-0037 items 5, 7, 8)

**Files:**
- Modify: `src/SqlSource.Tool/Describing/DatabaseRuns.cs` (`Guard`), `src/SqlSource.Tool/Logging/RunLogFile.cs`, `src/SqlSource.Tool/ToolHost.cs`, `src/SqlSource.Tool/Cli.cs` (`OpenLog`), `tests/SqlSource.Tool.Testing/Hosts.cs`, `tests/SqlSource.Tool.Testing/CliRun.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs`, `tests/SqlSource.Tool.Tests/Logging/RunLogFileTests.cs`, `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs`, `tests/SqlSource.Tool.Tests/ToolHostTests.cs`

**Interfaces:**
- Produces: `Guard.Call(Action call)`; `ToolHost.CreateFile: Func<string, Stream>` as the last positional member; `RunLogFile.Open(string given, string workingDirectory, Func<string, Stream> createFile, TimeProvider time)`; `CliRun.CreateFile: Func<string, Stream>?` (null means the host's own).

- [ ] **Step 1, item 5:** Add `DatabaseRunsTests.Run_ServerWithASettingThatHasNoName_IsOneSqlsrc200AndClosesTheSession`.  `FakeDescriber.Server` has a `ServerSetting(null!, "x")`, so the run reports one `SQLSRC200`, and the describer's `Closed` is 1.  This test may pass before the change, since the guard adds only redaction to an exception that holds no value; say so in the commit.  Add `Guard.Call(Action)` and call `RunEvents.Opened` through it.
- [ ] **Step 2, item 8:** Add `RunLogFileTests.Write_MemberNamedEventBesideOneNamedUnderscoreEvent_WritesEachUnderADistinctKey`.  Members `event`=1 and `_event`=2 give the line `{"event":"e","time":"…","__event":1,"_event":2}`.  Add a second test with `event`, `_event` and `__event`, whose `event` becomes `___event`.  Make `Unreserved` prepend `_` until no member of the event has the name.  Update the class comment and `docs/architecture/tool.md`'s "The log", which says one underscore.
- [ ] **Step 3, item 7:** Add `ToolHost.CreateFile` (`ToolHost.Create`: `static path => new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)`; `Hosts` gives the same), and pass it to `RunLogFile.Open` from `Cli.OpenLog`.  Replace `RunLogTests.Run_LogThatStopsBeingWritable_FinishesTheRunAndIsOneSqlsrc218` with a test of the same name and assertions that sets `_scene.Run.CreateFile` to a stream whose `Write` and `Flush` throw `IOException("No space left on device")`, with no skip, and with `--log run.log`.  Add a `ToolHostTests` fact that `Create().CreateFile` makes and empties a file.  Update `src/SqlSource.Tool/AGENTS.md`'s "Everything from outside comes through `ToolHost`" and `docs/architecture/tool.md`'s "The host".
- [ ] **Step 4:** Run `./pre-commit-validation.sh` and commit: `Guard the open event, number the log's reserved names, give the host its files`.

### Task 3: The tests TD-0037 lists (items 1 to 4), and TD-0037 rewritten

**Files:**
- Test: `tests/SqlSource.Tool.Tests/Commands/CheckTests.cs`, `tests/SqlSource.Tool.Tests/Commands/CheckFilterTests.cs`, `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs`, `tests/SqlSource.Tool.Tests/Commands/VerboseTests.cs`
- Modify: `src/SqlSource.Tool/Describing/SidecarCheck.cs` (the comment of `FoundOf` only), `docs/tech-debt/TD-0037-*.md` and its row

- [ ] **Step 1, item 1 (`CheckTests`):** Add three tests.  A file that is not `Ready`, one with a parse error, gets no difference under `--check`.  Two queries of one file that both differ are reported in the file's order.  A sidecar file the system will not open, `chmod 000` on Unix, skipped on Windows with `Assert.SkipWhen(OperatingSystem.IsWindows(), …)` and also skipped when running as root, is `SQLSRC218` and no difference.  Change `FoundOf`'s comment to say that the hash covers the engine, so a changed engine is reported as a changed query and the `engine` arm is the `database` arm's.
- [ ] **Step 2, item 2 (`CheckFilterTests`):** These tests cover `EntriesWithoutAQuery`'s `--database` guard for an entry of a query that is gone.  With `--database BILLING` against an entry of `billing`, the entry is reported.  An entry with no database is not reported under `--database billing`.  With a `.sql` path, or `--project` and no `--database`, it is reported.
- [ ] **Step 3, item 3 (`RunLogTests`):** Assert the log's members for `usesSqlSource` null and false, a `state` other than `ready`, a `decision` of `left-out`, `selected` false, and a sidecar delete that fails.  Read `RunEvents` for the exact member names and values.
- [ ] **Step 4, item 4 (`VerboseTests`):** Assert the lines `has parse errors`, `cannot be described`, `could not be read`, `deleted`, `could not be changed` and `no session`, from `RunEvents`.
- [ ] **Step 5:** Rewrite TD-0037 to describe item 6 alone.  Rename its file to `TD-0037-logging-and-the-steps-of-a-run-refer-to-each-other.md`, and update its row in `docs/tech-debt/README.md` and every reference (`grep -rn "TD-0037" src tests docs`, leaving `docs/superpowers/` alone).
- [ ] **Step 6:** Run `./pre-commit-validation.sh` and commit: `Add the tests TD-0037 lists, leave it the circle of namespaces`.

### Task 4: The secret parts of a connection, and the formatted message (TD-0034 a and c)

**Files:**
- Create: `src/SqlSource.Tool/Describing/ConnectionSecret.cs`
- Modify: `src/SqlSource.Tool/Describing/Redaction.cs`, `src/SqlSource.Tool/Logging/SessionRunLog.cs`, `src/SqlSource.Tool/Logging/RunTrace.cs`, `src/SqlSource.Tool/Describing/EntrySecretCheck.cs`, `src/SqlSource.Tool/Describing/DatabaseRuns.cs`, `src/SqlSource.Tool/Describing/Describers/IQueryDescriber.cs`, `src/SqlSource.Tool/Describing/Postgres/PostgresDescriber.cs`, `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs`, `tests/SqlSource.Tool.Tests/FakeDescriber.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/ConnectionSecretTests.cs` (new), `RedactionTests.cs`, `EntrySecretCheckTests.cs`, `DatabaseRunsTests.cs`, `tests/SqlSource.Tool.Tests/Logging/SessionRunLogTests.cs`, `RunTraceTests.cs`, `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresDescriberTests.cs`, and the `new SessionRunLog(…, "users", …)` call sites of the `PostgresEvents*Tests`

**Interfaces:**
- Produces:
  - `sealed class ConnectionSecret(string value, IEnumerable<string?>? parts = null)` with `string Value`, `ImmutableArray<string> Parts`, and `bool IsIn(string text)`.  `Parts` keeps a part that is not null, empty or white space, not equal to `Value` and not repeated, longest first.  `IsIn` is true when `Value` or a part as a token is in the text.
  - `Redaction.Of(string text, ConnectionSecret secret)`, `Redaction.Of(DescribeFailure, ConnectionSecret)`, `Redaction.Of(ToolDiagnostic, ConnectionSecret)`, and `Redaction.HoldsToken(string text, string part)`, the token rule.
  - `SessionRunLog(IRunLog inner, ConnectionSecret secret, string? prefix = null)`, `RunTrace.Without(ConnectionSecret secret)`, `EntrySecretCheck.Holds(SidecarEntry entry, ConnectionSecret secret)`.
  - `IQueryDescriber.SecretsOf(string connection)` returning `IReadOnlyList<string>`.  `PostgresConnection.SecretsOf(string connection)` is static and backs `PostgresDescriber.SecretsOf`.  `FakeDescriber.Secrets`, an `IReadOnlyList<string>` that defaults to empty and is settable, so that `SecretsOf` returns it, and `FakeDescriber.SecretsException`, an `Exception?` that `SecretsOf` throws when it is set.
- Token rule, `HoldsToken`: an occurrence counts when the part's first character, if it is a letter, digit or `_`, is not preceded by one, and its last character, if it is one, is not followed by one.  `Redaction.Of(string, ConnectionSecret)` first replaces the whole value anywhere, then each part where it counts, with `***`.

- [ ] **Step 1:** Write `ConnectionSecretTests`.  `Parts` drops blank, null, repeated parts and the value itself, and orders the rest longest first.  `IsIn` holds for the whole value inside a word and for a part as a token, and does not hold for `test` inside `latest_orders`.  `IsIn` holds for `hunter2` in `Password=hunter2;`, for `p@ss!` in `'p@ss!'`, and not for `p@ss!` in `xp@ss!`.  Run: they fail to compile.
- [ ] **Step 2:** Implement `ConnectionSecret` and the token rule, and move `Redaction`, `SessionRunLog`, `RunTrace`, `EntrySecretCheck` and `Guard` to it.  `Redaction.OfLines` searches each part as it searches the value, with its line breaks as `\n`.  `EntrySecretCheck` searches each part JSON-escaped (`SidecarJsonBuilder.Quote(part)[1..^1]`) with the token rule and the same "not in the text without the describer's part" exemption.  Update the existing tests to pass `new ConnectionSecret(…)`.
- [ ] **Step 3:** Add `RedactionTests`/`SessionRunLogTests`/`RunTraceTests` cases: a password part in a log member, a `--verbose` line and a describe failure's server line is `***`, and `test` beside `latest_orders` is left.  Add `EntrySecretCheckTests`: a password part as a column's whole name is a fault, the same part inside `is_admin_x` is not, and a part `postgres` is not since the engine holds it.
- [ ] **Step 4:** Add `SecretsOf` to `IQueryDescriber`, with doc comments.  In `DatabaseRuns.RunDatabaseAsync`, call it through a guard of the whole value and build the `ConnectionSecret` before the trace, the log, the open and the guard that follows.  Implement `PostgresConnection.SecretsOf`: `Password` and `SslPassword` of `new NpgsqlConnectionStringBuilder(connection)`, and none when that throws `ArgumentException`.  `PostgresDescriberTests`: `SecretsOf("Host=h;Password=pw;SSL Password=kp")` is `["pw", "kp"]` in some order, and `SecretsOf("Nonsense=1")` is empty.  `DatabaseRunsTests`: a `FakeDescriber` with `Secrets = ["pw"]` whose open failure's server line holds `pw` prints `***`.  A describer whose `SecretsOf` throws an exception whose message holds the whole value is one `SQLSRC200` without the value.
- [ ] **Step 5, the formatted message:** Add `RedactionTests.Of_ValueThatTheFormatAndAnArgumentMakeTogether_IsRemovedFromTheMessage`.  A descriptor with format `"half: Pass{0}"` and argument `"word=x"`, under the secret `"Password=x"`, yields a diagnostic whose formatted message holds `***` and not `Password=x`.  A second test does the same over two arguments.  Implement it in `Redaction.Of(ToolDiagnostic, ConnectionSecret)` as the spec's section 3 says.
- [ ] **Step 6:** Make `DatabaseRuns.NoSecretIn`'s message say: `The description of the query '{name}' holds the connection's value or a password of it.  The describer put it there, or the password equals a name of the database.`
- [ ] **Step 7:** Run `./pre-commit-validation.sh`, then the container tests on `postgres:14.24`, and commit: `Redact and search the secret parts of a connection`.

### Task 5: Fixed texts for a session that could not open or that dropped (TD-0034 b)

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/ConnectionErrors.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs` (remove `ErrorOf`; its own texts stay), `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs` (the `open` call uses `ConnectionErrors.AtOpen`, every other call `ConnectionErrors.Of`)
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/ConnectionErrorsTests.cs` (new), and in `tests/SqlSource.Tests.Postgres` the tests of a refused connection, a wrong password and a backend that was terminated: `Describer/DescribeCommandTests.cs`, and whatever `grep -rn "terminating connection\|Failed to connect\|password authentication" tests/SqlSource.Tests.Postgres` finds

**Interfaces:**
- Produces: `static PostgresServerError ConnectionErrors.AtOpen(NpgsqlException exception)` and `static PostgresServerError ConnectionErrors.Of(NpgsqlException exception)`, with the texts of the spec's section 2 table as `public const string`s.  `PostgresConnection.LostConnection` is reused.

- [ ] **Step 1:** Write `ConnectionErrorsTests`, one theory row per row of the spec's table.  Build a `PostgresException` through its public constructor (`new PostgresException(messageText, severity, invariantSeverity, sqlState, detail, hint, position…)`; read Npgsql 10.0.3's signature).  Build a driver failure as `new NpgsqlException("Failed to connect to 10.0.0.5:5432", new SocketException((int)SocketError.ConnectionRefused))`.  Assert the SQLSTATE, the exact message, and that `Detail`, `Hint` and `Position` are null.  Add: each of `PostgresConnection`'s own texts passes unchanged at `open`, and a `42P01` at a later call keeps its message, detail, hint and position.
- [ ] **Step 2:** Implement `ConnectionErrors`.  An exception of the describer's own is an `NpgsqlException`, not a `PostgresException`, with no inner exception, whose message is one of `PostgresConnection`'s constants.  Npgsql's no-password case is matched by its message starting with `No password has been provided`, ordinal.  Wire it into `PostgresLive`.
- [ ] **Step 3:** Update the container tests.  The refused connection's `diagnostic` lines hold `Npgsql could not reach the server: ConnectionRefused` and not `127.0.0.1`.  The wrong password's hold `28P01: the server refused the password` and not the role's name.  The terminated backend's hold `57P01: the server ended the session`.  Each also asserts the absence of the host and the user anywhere in the log.  Update the TD-0034 comment at `DescribeCommandTests.cs:248`.
- [ ] **Step 4:** Run `./pre-commit-validation.sh`, then `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres`.  No fixture capture holds an `open` error or a session-ending error.  If one does, record it again with `tools/record-fixtures.sh` and read the diff.  Commit: `Keep the host and the user out of a session that did not open or dropped`.

### Task 6: The documents, and TD-0034 narrowed

**Files:**
- Modify: `docs/architecture/tool.md` ("Where a connection's value goes", "Describing"), `docs/architecture/postgres-describer.md` (the describer contract: `SecretsOf`; the connection and errors: `ConnectionErrors`; line 37's exception for a driver's message in an `error`), `src/SqlSource.Tool/AGENTS.md` (the rules of a request and an answer, of `PostgresConnection`'s `live` functions and of a connection's value), `docs/manual/tool.md` (Logs: "Read a log before you share it" and its list item about a database that could not be opened; the TD-0034 half of the known issue), `docs/manual/postgresql.md` (if it repeats that a log names the host), `docs/manual/diagnostics.md` (`SQLSRC224`'s sample becomes `server: 28P01: the server refused the password`, and its table gains the new texts; `SQLSRC225`'s paragraph on a connection the server ended), `docs/tech-debt/TD-0034-*.md`, `docs/tech-debt/TD-0048-*.md` (its first bullet), `docs/tech-debt/README.md`

- [ ] **Step 1:** Rewrite the documents to describe the code as it now is.  The manual describes effects only: a password is written as `***` where it stands apart from other letters and digits, and an error of a database that cannot be opened names neither its host nor its user.  Add a known issue: a database password that equals the whole name of a column, table or type of a described query makes `describe` stop with SQLSRC200, so use a password that is no name in the database.
- [ ] **Step 2:** Rewrite TD-0034 to what remains: a capture is not searched for the value or its parts; a changed whole value other than its parts; a value spread over two members of one event; a password the driver reads from the environment or a file; a value equal to a word of the describer's part, and a password equal to a name the describer gave.  Rename its file if its title changes, and update its row and references.
- [ ] **Step 3:** Run `./pre-commit-validation.sh` and commit: `Document the hardened describer contract`.

### Task 7: Whole-branch check

- [ ] Run `./pre-commit-validation.sh` and the container tests on `postgres:14.24` on the final tree.  Confirm with `grep -rn "TD-0041" . --include='*.cs' --include='*.md' | grep -v docs/superpowers` that nothing names the deleted item.
