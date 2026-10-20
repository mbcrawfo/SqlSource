# Hardening the describer contract before SQL Server - design

Resolves TD-0041, most of TD-0037 and most of TD-0034, so that the SQL Server describer of phase 6 is written to the contract it keeps rather than fitted to it later.  Microsoft.Data.SqlClient's errors name the server and the user ("Login failed for user 'x'") just as Npgsql's do.

## What the merged code says, where the items assumed otherwise

- TD-0034 lists six holes.  The one that names the host and the user is the PostgreSQL describer's alone: every live call maps an `NpgsqlException` through `PostgresConnection.ErrorOf`, so the fix belongs to that function, and it also keeps the host out of a capture's `open` answer.  The rest of the capture hole stays for phase 10.
- `RunEvents.Opened` (TD-0037 item 5) returns nothing, so `Guard.Call<T>` cannot wrap it as it is.  The guard gets an overload for an action.
- TD-0037 item 1 calls the `engine` arm of `SidecarCheck.FoundOf` unreachable.  It shares its arm with `database`, and the comment can say why the engine never reaches it.
- TD-0041 offers two choices.  The analyzer has no setting that names one method as a transfer of ownership, and the experiment below settles which choice works.

## 1. A describer gives the secret parts of its connection (TD-0034 a)

`IQueryDescriber` gains `IReadOnlyList<string> SecretsOf(string connection)`: the parts of the value that are secret, as the describer's driver reads the value.  It returns none for a value the driver cannot read, and it never throws, since a throw is `SQLSRC200`.  PostgreSQL's gives `Password` and `SslPassword` from `NpgsqlConnectionStringBuilder`.  A password that the driver takes from the environment (`PGPASSWORD`) or a password file is not in the value, and is not given.

`DatabaseRuns` asks for the parts through the guard, before the session is opened.  The guard at that point knows only the whole value.  The run then holds a `ConnectionSecret` (`Describing/ConnectionSecret.cs`): the whole value, and the parts it keeps.  It drops a part that is empty or white space, a part equal to the whole value, and a repeated part, and orders the rest longest first.  `Redaction`, `SessionRunLog`, `RunTrace`, `EntrySecretCheck` and the guard take a `ConnectionSecret` in place of the string.

- **The whole value** is matched wherever it stands, as it is today.
- **A part** is matched only where it stands as a token: its first character is not preceded, nor its last followed, by a letter, a digit or `_` when that edge character is one itself.  A dev password such as `test` then leaves `latest_orders` alone, while a driver message that quotes the password, `Password=hunter2;` or `'hunter2'`, still loses it.  A password equal to a whole word that the run writes, such as `postgres`, still turns that word into `***` in what is printed and logged.  That is the price of a password that equals a word, and it leaks nothing.
- **`EntrySecretCheck`** counts a part as it counts the value: it must be in the entry's text and not in the same text without the describer's part.  A dummy password `postgres` is therefore no fault, since the engine is `postgres`.  A password that equals a whole name the describer gave, a column `password` found through `SELECT *` with the password `password`, ends the run with `SQLSRC200`, and the fault's message says that a password equal to a name of the database causes it.  That case is a known issue in the manual and is left in TD-0034.  Whether entries should be searched for parts at all is a question put to the owner; until it is answered, they are, as TD-0034 proposed.

## 2. A session that cannot be opened, or that dropped (TD-0034 b)

A new `Describing/Postgres/ConnectionErrors.cs` replaces `PostgresConnection.ErrorOf` with two functions.  `ConnectionErrors.AtOpen(NpgsqlException)` serves the `open` call and `ConnectionErrors.Of(NpgsqlException)` every other call.  Each keeps the SQLSTATE and puts a fixed text of the describer's own in place of the driver's or the server's message, with no detail, no hint and no position.  The describer's own texts (`PostgresConnection`'s constants) pass as they are.

| Where | What was thrown | `Message` |
|----|----|----|
| `open` | a server error `28P01` | `the server refused the password` |
| `open` | another of class `28` | `the server refused to authenticate the user` |
| `open` | `3D000` | `the server has no such database` |
| `open` | `42501` | `the user may not connect to the database` |
| `open` | `53300` | `the server has too many connections` |
| `open` | `57P03` | `the server is not accepting connections now` |
| `open` | class `08` | `the server refused the connection` |
| `open` | any other SQLSTATE | `the server refused to open a session` |
| `open` | no SQLSTATE, Npgsql's "No password has been provided" | `the server asks for a password and the connection string gives none` |
| `open` | no SQLSTATE, a `TimeoutException` in the chain | `the connection to the server timed out` |
| `open` | no SQLSTATE, a `SocketException` in the chain | `Npgsql could not reach the server: <SocketError>`, e.g. `ConnectionRefused` |
| `open` | any other with no SQLSTATE | `Npgsql could not open a connection to the server (<type of the innermost exception>)` |
| a later call | class `08`, `57P01`, `57P02`, `57P04`, `57P05`, `25P03` | `the server ended the session` |
| a later call | no SQLSTATE, a `TimeoutException` in the chain | `the server did not answer in time` |
| a later call | any other with no SQLSTATE | `the connection to the server was lost` (`PostgresConnection.LostConnection`) |
| a later call | any other SQLSTATE | the server's error as today |

So `SQLSRC224` and `SQLSRC225` print, log and capture no host, port, user or database name.  `SQLSRC236`'s refusal of `Multiplexing=true` is one of the describer's own texts and is unchanged.  A SocketError's name and an exception's type name hold nothing of a connection.

## 3. A failure's message is searched once it is formatted (TD-0034 c)

`Redaction.Of(ToolDiagnostic, ConnectionSecret)` formats the message as `Reporter` does, from the raw arguments, after it has cleaned each argument.  When the secret is in that text, the diagnostic gets a copy of its descriptor whose message format is `{0}`, with the cleaned message as its one argument.  The value that a format and an argument, or two arguments, make together is then removed.

## 4. `CA2000` and an `Outcome` of a disposable (TD-0041)

The experiment: with no attribute, `RunLogFile.Open` reports `CA2000` for the `RunLogFile` and not for the `FileStream` given to its constructor.  The analyzer already treats a disposable passed to the constructor of a disposable type as handed over.  With `dotnet_code_quality.CA2000.dispose_ownership_transfer_at_constructor = true` and `new Outcome<RunLogFile, ToolDiagnostic>(log)`, it reports nothing.

**Decision:** `Outcome` gains a public constructor that takes the value, and `.editorconfig` sets that option for `[*.cs]`.  A method that returns an `Outcome` of a value that must be disposed makes it with the constructor, never `Outcome.Of`, and carries no `SuppressMessage`.  `RunLogFile.Open`, `PostgresSession.OpenAsync`, `FakeDescriber.OpenAsync` and, in phase 6, SQL Server's opener follow it.

**What it costs:** the analyzer no longer follows a disposable passed to the constructor of a type that is not disposable, such as a record that holds one without owning it.  Such code is rare here, and the `CA2213` and `CA1001` analyzers still check a type's own disposable fields.

**Not taken:** a disposable result type of its own.  Each caller would then dispose the result, about twenty call sites in the tests.  `DatabaseRuns` closes a session through its guard, or quietly after a failure, so an `await using` of a result would close the session outside the guard.

## 5. TD-0037

- **Item 5:** `Guard` gains `void Call(Action)`, and `DescribeAllAsync` calls `RunEvents.Opened` through it.
- **Item 8:** `RunLogFile` writes a reserved name with as many underscores before it as it takes to reach a name that no member of the event has: `event` beside `_event` is written `__event`.
- **Item 7:** `ToolHost` gains `Func<string, Stream> CreateFile`, which makes or empties a file for writing.  `RunLogFile.Open` takes it.  `ToolHost.Create` gives a `FileStream` with `FileMode.Create`, `FileAccess.Write` and `FileShare.Read`, and `CliRun` lets a test give its own.  The `/dev/full` test becomes a test that runs on every system, with a stream whose writes fail.
- **Items 1 to 4:** the missing tests, each in the class the item names.  For item 1, the comment of `SidecarCheck.FoundOf` says why the engine never reaches its arm.
- **Item 6** (the namespaces that refer to each other) is not small: it moves the events of `RunEvents` or changes what they take, and `Logging/` would still use `Redaction`.  TD-0037 is rewritten to describe it alone.

## Documents

- `docs/architecture/tool.md`: where a connection's value goes, `Outcome` and `CA2000`, the host's `CreateFile`, and the log's reserved names.
- `docs/architecture/postgres-describer.md`: `SecretsOf` in the contract, and `ConnectionErrors` under the connection and errors.
- `src/SqlSource.Tool/AGENTS.md`.
- The manual: `tool.md` (the logs warning and its known issue; the part of that known issue that is TD-0023's stays for cli-secrets), `postgresql.md` and `diagnostics.md` (`SQLSRC224`'s sample and table, `SQLSRC225`'s dropped connection).
- TD-0041 is deleted, and TD-0037 and TD-0034 are rewritten to what remains, with TD-0048's sentence on TD-0034 updated.

## Testing

Unit tests in `tests/SqlSource.Tool.Tests` for each part.  The container tests in `tests/SqlSource.Tests.Postgres` of a refused connection, a wrong password and a terminated backend change to assert the fixed texts and that the host and the user are absent.  They run on the default image and on `postgres:14.24`.
