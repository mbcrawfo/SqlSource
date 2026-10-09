# Query generation, phase 2.5: describe - design

Date: 2026-10-09

Sub-phase 2.5 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It delivers `sqlsource describe` whole but for an engine: the interfaces a describer implements, the connections, the rules for what is described, skipped and written, and the sidecars on disk.  No describer is registered, so the released command reports every query as `SQLSRC216`; a describer of the tests proves the rest.  Phase 3 registers PostgreSQL's.

It builds on sub-phases 2.1 and 2.4.  Sub-phase 2.6 builds on it.

## Goal

```console
$ dotnet sqlsource describe --connection billing="Host=localhost;Database=billing"
src/App/Queries/Users.sql(3,10): error SQLSRC213: No connection is given for the database 'postgres'
    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES
    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc213
billing (postgres): 4 described, 12 skipped, 0 failed
postgres (postgres): 0 described, 7 skipped, 2 failed
```

Success is:

- A run with a describer of the tests writes the sidecars the format design specifies, and a second run with nothing changed describes nothing and writes nothing.
- A sidecar is written whole or not at all: never an entry from an older tool beside a new one, never half a file.
- A run recorded through the exchange replays to the same sidecars with no describer behind it.
- No value of a connection is ever printed.

## Decisions

From the epic, unchanged: a query needs an entry when its output is `Models` or `CodeGen`; one whose entry is current is skipped, and `--force` describes it again; a sidecar is written when every query of its file that needs an entry was described in this run or has a current entry, and is otherwise left as it was; a sidecar that would be empty is deleted; a connection is given for a database name on the command line or in the environment, never in the project; a database with no connection is an error for its queries and the run goes on; one summary line for each database; each token's default takes its place in the SQL that is described; a describer interface with a registry by dialect; the seam with its live, recording and replaying shapes.

Changed in the epic by the owner, and recorded in the outline:

| Decision | Choice | Why |
|----|----|----|
| The seam | Implemented here, not left as interfaces: one exchange that every call to an engine goes through, with its three shapes and the capture's model | It is the same for every engine and small.  A describer of the tests then proves a run from a capture, which is how phase 3's fixtures run. |
| A file that is not written though a query of it was described | An error, `SQLSRC217`, and exit `1` | The epic says the run names the queries that held the file back.  A description that was not saved must not look like a success. |

Settled in this spec:

| Decision | Choice | Why |
|----|----|----|
| What a describer returns for a type | The type records of sub-phase 2.1 | A second hierarchy and a mapping between the two would say the same thing twice. |
| A database with no connection, all of whose selected queries are current | Not an error | A developer with no database can run `describe` on an unchanged tree.  Under `--force` every query needs describing, and it is an error. |
| `name=` in `--connection` | Read as a name only when it is a database of the plan | A connection string holds `=` itself: `Host=localhost` must not be the database `Host`. |
| A connection with no name | Used for a database that has none of its own, and only when the run has one selected database | The epic's rule.  A second database makes it `SQLSRC215` for the database that needed it. |
| Order | Databases in the plan's order, one session each, queries one at a time | A connection is not for several threads, and a describe takes milliseconds. |
| A sidecar on disk that cannot be read | Treated as absent | It is the tool's own file.  `describe` writes it again. |
| How a file is written | To a temporary file in the same folder, then moved over the old one | A run that is stopped leaves the old file or the new one. |

## Out of scope

- A describer for any engine, and every error a server gives.  Phases 3 and 6.
- `--check`, `--verbose`, `--log`.  Sub-phase 2.6.
- The `.env` file, `--manifest`, `--online` and `--watch`.  Phase 9.
- A command-line way to record or replay.  Phase 10's `diagnose` and `replay` are that; here the exchange is chosen by the host, which only tests set.
- Describing two databases at once.
- The packed tool describing an installed project.  Phase 9 adds it to `tools/check-package-install.sh`, as the epic says.

## The command line

```
sqlsource describe [<path>...] [--project <path>]... [--database <name>]...
                   [--connection <[name=]value>]... [--force]
```

## The describer's interfaces

In `src/SqlSource.Tool/Describing/`.

```csharp
internal interface IQueryDescriber
{
    SqlDialect Dialect { get; }

    Task<OpenResult> OpenAsync(OpenRequest request, CancellationToken cancellationToken);
}

internal interface IDescribeSession : IAsyncDisposable
{
    ServerInfo Server { get; }

    Task<DescribeResult> DescribeAsync(DescribeRequest request, CancellationToken cancellationToken);
}
```

| Type | Holds |
|----|----|
| `OpenRequest` | The database's name, its connection's value, and the exchange |
| `OpenResult` | A session, or a `DescribeFailure` |
| `ServerInfo` | The server's version as the sidecar records it; the driver's name and version, or null; the session's settings that change a description, as names and values |
| `DescribeRequest` | The query's name; the sample SQL; the query's parameter list, each with its declared type and nullability |
| `DescribeResult` | A `QueryDescription`, or a `DescribeFailure` |
| `QueryDescription` | `ResultKind`; `Parameters`, each a name, a `SidecarType` or null, and a type source; `Columns`, each a `SidecarColumn`, or null when there are no rows; `MatchesTable`; `Plan`; `TableMatch` |
| `DescribeFailure` | A descriptor and its arguments; the step, one of the epic's fixed list; the server's lines, verbatim; a help line or null |

- A describer never throws for what a server or a user can cause: it returns a failure.  An exception from one is a bug, and `SQLSRC200`.
- A describer gets the SQL as text and lexes it itself, with the generator's lexer, when it needs the lexemes.
- `DescriberRegistry` maps a `SqlDialect` to its describer.  `ToolHost` gains `Describers`; the real host's is empty in this sub-phase.

### The sample SQL

`SampleSql.Build(SqlQuery)` is the query's stripped segments in order: a literal as it is, and a token as its resolved default, nothing for an empty one.  A query with a token that has no default never reaches it: sub-phase 2.4 gave it a problem.

### From a description to an entry

The run builds the `SidecarEntry`:

| Member | From |
|----|----|
| `Name`, `Hash` | The planned query |
| `Engine` | `SqlDialectName.Canonical` of the file's dialect |
| `Database` | The database's name as the plan spells it |
| `ServerVersion` | The session's |
| `ResultKind`, `MatchesTable`, `Plan`, `TableMatch`, `Columns` | The description |
| `Parameters` | The query's parameter list, in its order: the name as the list has it; the ordinal, its index; the type and type source the description gives for that name, compared ignoring case, and null when it gives none; `nullable` from the query's `-- param:` marker, `true`, `false` or null |

A parameter in the description that the query's list does not hold is a bug in the describer, and `SQLSRC200`.

## The exchange

One interface that every call a describer makes to an engine goes through, so that a run can be recorded and replayed.

```csharp
internal interface IDescribeExchange
{
    Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        CancellationToken cancellationToken
    );
}
```

| Shape | Does |
|----|----|
| `LiveExchange` | Calls `live` |
| `RecordingExchange` | Calls `live`, and adds the method, the request and the answer to a `Capture` |
| `ReplayingExchange` | Never calls `live`.  Answers from a `Capture`, by the method and a hash of the request, and throws `CaptureMissException`, which names the method and the request, for a call the capture lacks |

- A request and an answer are records that `System.Text.Json` writes and reads, with one set of options that `Capture` owns.  The hash of a request is the SHA-256 of its JSON.
- An answer is a value.  A call that can fail on the server returns the failure as its answer, so that a capture holds it; phase 3 designs its methods so.
- A `Capture` is its version, `captureVersion` of `1`, the engine, and the calls in order.  `Capture.Write` and `Capture.Read` give and take the text of `capture.json`: indented, `\n` line endings.  A call asked twice with one request is answered the same both times.
- `ToolHost` gains `Exchange`, a function from a database's name to its exchange.  The real host gives `LiveExchange`.

## Connections

| Source | A named value | A value with no name |
|----|----|----|
| The command line | `--connection billing=Host=...` | `--connection Host=...` |
| The environment | `SQLSOURCE_CONNECTION_BILLING` | `SQLSOURCE_CONNECTION` |

- **The variable of a name** is the name in upper case, in the invariant culture, with every character that is not a letter or a digit replaced by `_`: `billing-v2` reads `SQLSOURCE_CONNECTION_BILLING_V2`.
- **A database's connection** is the first of: the command line's value for its name; the command line's value with no name; its variable; `SQLSOURCE_CONNECTION`.  A variable that is empty is not set.
- **A value with no name** is taken only when the run has exactly one selected database.  With more, a database that reaches it gets `SQLSRC215`, not that value.
- **`--connection`** is read as `name=value` when the text before its first `=` is the name of a database of the plan, ignoring case, and as a value with no name otherwise.  Two values for one name, or two with no name, are a wrong command line.
- **Two databases of the plan whose names give one variable** are `SQLSRC214`, once for the pair, and the queries of both fail.
- **A connection is asked for only when it is needed**: when a selected query of the database is to be described.

The tool passes a value to the describer and to nothing else.  What it may say about a connection is where it came from: `--connection`, or a variable's name.

## The run

`describe`, after the plan of sub-phase 2.4:

**1. Read what is there.**  For each `Ready` file, the sidecar beside it, if any, through `SidecarReader`.  It is usable when it was read and `Sidecar.IsWrittenBy(PackageVersion.Prefix)` holds.

**2. Decide for each query that needs an entry.**

| The query | Decision |
|----|----|
| Has a problem from the plan | Failed |
| Is selected, and `--force` is given | Describe |
| Has a current entry: the sidecar is usable and `IsCurrentFor(hash, database)` holds for the entry of its name | Skip, and keep the entry |
| Is selected | Describe |
| Otherwise | Left out: it is not selected and has no current entry |

**3. Describe**, database by database in the plan's order, for each database with a query to describe:

- No describer is registered for its dialect: `SQLSRC216` at each such query, and they fail.
- The pair error `SQLSRC214`, no connection, `SQLSRC213`, or `SQLSRC215`: reported once, at the database's first query to describe, and its queries fail.
- `OpenAsync` returns a failure: reported once, at the same place, and its queries fail.
- Otherwise each query is described in the plan's order.  A failure is reported at the query's name and the query fails; the session goes on to the next.

**4. Decide for each `Ready` file.**

| The file | Outcome |
|----|----|
| Has no query that needs an entry | Its sidecar is deleted, if there is one |
| Every query that needs an entry was skipped or described | Its target is a `Sidecar` of the tool's two versions with those entries in the `.sql` file's order.  It is written when the writer's text differs from the file on disk, compared with `\r\n` read as `\n` |
| Any other | It is left as it was.  When a query of it was described in this run, `SQLSRC217` at the file, with one continuation line for each query that held it back: its name, and `failed` or `not in this run` |

A file that is not `Ready` is not touched.  An entry for a query that no longer exists is not in the target, so writing drops it.  A sidecar with no `.sql` file beside it, or beside a file no type claims, is not looked at.

The steps are kept apart in the code: one that gives each file's outcome with its target, and one that applies the outcomes to the disk.  Sub-phase 2.6 compares where this one writes.

**5. Summarise.**  For each database that has a selected query that needs an entry, and for each name given with `--database`, one line on standard output:

```
<name> (<engine>): <n> described, <n> skipped, <n> failed
```

The counts are of selected queries that need an entry.  A database that `--database` names and no query has shows three zeros and no engine.

**6. Exit** `1` when anything was reported, and `0` otherwise.

### The format of a describer's error

```
<file>(<line>,<column>): error <id>: <message>
    query: <name>, database <database>, <engine> <server version>, <driver> <version>
    step: <step>
    server: <line>
    help: <text>
    see: <help link>
```

The parts the tool does not have, a server's version before a session opened, are left out.

### Diagnostics

In `ToolDiagnostics`, with the four places each needs.

| Id | Title | Message | Arguments |
|----|----|----|----|
| `SQLSRC213` | Database has no connection | `No connection is given for the database '{0}'` | The database |
| `SQLSRC214` | Two databases share a connection variable | `The databases '{0}' and '{1}' both read their connection from {2}` | The two names; the variable |
| `SQLSRC215` | Connection names no database | `A connection with no name is for a run with one database, and this run has {0}: {1}` | The count; the names |
| `SQLSRC216` | No describer for the dialect | `This version of sqlsource cannot describe '{0}'` | The dialect's name |
| `SQLSRC217` | Sidecar was not written | `'{0}' was not written, because not every query of '{1}' has an entry` | The sidecar; the `.sql` file |
| `SQLSRC218` | File could not be written | `'{0}' could not be written: {1}` | The file; the system's message |

`SQLSRC213` and `SQLSRC215` have a `help:` line that names the option and the variable.  `SQLSRC217` has one: describe the whole file, or fix the queries that failed.  `SQLSRC218` is also what a sidecar that cannot be deleted reports.

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing and brings its tests and the documents it makes wrong.

1. The epic outline: the row of 2.5 says In progress.
2. The interfaces and their records, `DescriberRegistry`, `SampleSql`, and the describer of the tests.
3. The exchange: its three shapes and `Capture`.
4. Connections, with `SQLSRC213` to `SQLSRC215`.
5. The decisions of step 2 of the run, and describing, with `SQLSRC216` and the format of a describer's error.
6. A file's outcome and its target.
7. Applying the outcomes: writing, deleting, `SQLSRC217` and `SQLSRC218`.
8. `--force`, the summary and the exit code.
9. The documents, and the outline's row set to Done.

## Testing

The describer of the tests, `FakeDescriber`, is given a description or a failure for each query's name, makes every call through the exchange, and records what it was asked.  Tests run `Cli.RunAsync` over a `TestProject` of sub-phase 2.4 with a host that holds it.

| Where | Cases |
|----|----|
| `SampleSqlTests` | No token; a default; an empty default; one token twice; a default from a `-- token:` marker |
| `EntryBuilderTests` | Each row of the table of an entry.  A parameter the description lacks; one it gives in another case; a declared-only parameter; `nullable` for `null`, `not null` and neither.  A parameter the list lacks throws |
| `ExchangeTests` | Live calls through.  Recording gives the same answer and a call in the capture.  Replaying answers without calling, and throws for a call that is not there, naming it.  One request twice.  A capture written, read and equal.  Two requests that differ in one member have two hashes |
| `ConnectionTests` | Each of the four sources, and each winning over the ones below it.  The variable of a name with `-`, `.`, `_` and a letter outside ASCII.  `name=` for a database of the plan, in another case, and for a word that is none.  A value that holds `=`.  A value with no name and one database, and two.  Two names with one variable.  Two values for one name.  An empty variable |
| `DescribeRunTests` | Each row of the table of decisions.  A second run describes and writes nothing.  A changed query, a changed `-- param:`, a changed default, a changed database: described again.  A sidecar of another tool version, of another format version, and one that cannot be read: every query described.  `--force`.  A failure in one query: the others are described, the file is not written, `SQLSRC217` names it.  A failure to open: once for the database.  No connection with every query current: no error; with one stale: `SQLSRC213` once |
| `SidecarStoreTests` | A file is written, unchanged, and deleted.  An entry of a deleted query is dropped.  A file with `\r\n` and the same content is not written again.  A folder that cannot be written gives `SQLSRC218` and leaves no temporary file.  A file that is not `Ready` is not touched |
| `FilterRunTests` | `--database` with the other database's entries current: a mixed file is written.  With one of them stale: not written, `SQLSRC217` with `not in this run`.  A file filter.  `--project` |
| `SummaryTests` | The line for each database; the counts; a `--database` name that no query has |
| `ReplayTests` | A run recorded with `FakeDescriber` behind `RecordingExchange`, then run again with `ReplayingExchange` and a describer whose live calls throw: the same sidecars, byte for byte |
| `SecretTests` | A run whose connection value holds a marker word, with errors of every kind of this spec: neither writer holds the word |

## Documentation

- `src/SqlSource.Tool/AGENTS.md`: a describer returns failures and never throws; every call to an engine goes through the exchange, with a request and an answer that can be written as JSON; a connection's value goes to the describer and nowhere else, and `SecretTests` holds the tool to that; a sidecar is written whole, through the store; the two steps of the run stay apart.
- `docs/diagnostics.md`: `SQLSRC213` to `SQLSRC218`.
- `README.md`, `CONTRIBUTING.md` and `docs/publishing.md`: nothing in them changes.
- `docs/tech-debt` and `docs/deferred`: nothing is expected.
- The epic outline: in steps 1 and 9.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
