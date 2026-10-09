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
reports (postgres): no connection, 5 skipped
```

Success is:

- A run with a describer of the tests writes the sidecars the format design specifies, and a second run with nothing changed describes nothing and writes nothing.
- A sidecar is written whole or not at all: never an entry from an older tool beside a new one, never half a file.
- A filter narrows what a run changes as well as what it describes: a run for one database or one file leaves every other sidecar as it was.
- No value of a connection is ever printed.

## Decisions

From the epic, unchanged: a query needs an entry when its output is `Models` or `CodeGen`; one whose entry is current is skipped, and `--force` describes it again; a sidecar is written when every query of its file that needs an entry was described in this run or has a current entry, and is otherwise left as it was; a sidecar that would be empty is deleted; a connection is given for a database name on the command line or in the environment, never in the project; one summary line for each database; each token's default takes its place in the SQL that is described; a describer interface with a registry by dialect; the seam as interfaces, with its recording and replaying shapes left to the phase that has an engine.

Changed in the epic by the owner, and recorded in the outline:

| Decision | Choice | Why |
|----|----|----|
| `--connection` | Always `name=value`.  A connection with no name is `SQLSOURCE_CONNECTION` alone | A connection string holds `=` itself, and a shell removes quotes before the tool sees them, so the tool cannot tell `billing=Host=...` from `Host=...` by looking.  The epic allowed a bare `--connection`. |
| A database with no connection, all of whose selected queries are current | Not an error.  Its summary line says `no connection` | A developer who has one of a project's two databases can run `describe`, and phase 9's build, without the other.  It is an error once a query of it must be described, and so always under `--force` and `--check`.  The epic made it an error for its queries whatever their state. |
| A file that is not written though a query of it was described | An error, `SQLSRC217`, and exit `1` | The epic says the run names the queries that held the file back.  A description that was not saved must not look like a success. |

Settled in this spec:

| Decision | Choice | Why |
|----|----|----|
| What a describer returns for a type | The type records of sub-phase 2.1 | A second hierarchy and a mapping between the two would say the same thing twice. |
| What a filter lets a run change | A sidecar is written only for a file with a selected query, and deleted only in a run with no filter | `describe --database billing` must not delete or rewrite a sidecar of another database, and a run on one project of a solution does not know what the others need of a shared file. |
| Order | Databases in the plan's order, one session each, queries one at a time | A connection is not for several threads, and a describe takes milliseconds. |
| A sidecar on disk that cannot be read, or has a lower format version | Treated as absent, and written again | It is the tool's own file.  The usual cause of one that cannot be read is a merge conflict, and `describe` is the fix. |
| A sidecar on disk with a higher format version | An error, `SQLSRC221`, and the file is left alone | A newer tool wrote it.  An older one must not write it back down. |
| How a file is written | To a temporary file in the same folder, then moved over the old one | A run that is stopped leaves the old file or the new one. |

## Out of scope

- A describer for any engine, and every error a server gives.  Phases 3 and 6.
- The recording and replaying shapes of the exchange, and the capture file.  Phase 3, where PostgreSQL's requests and answers say what a capture must hold.
- `--check`, `--verbose`, `--log`.  Sub-phase 2.6.
- The `.env` file, `--manifest`, `--online` and `--watch`.  Phase 9.
- Describing two databases at once.
- The packed tool describing an installed project.  Phase 9 adds it to `tools/check-package-install.sh`, as the epic says.

## The command line

```
sqlsource describe [<path>...] [--project <path>]... [--database <name>]...
                   [--connection <name>=<value>]... [--force]
```

## Three sets of databases

A query belongs to a database only when it needs an entry; sub-phase 2.4 gives the plan its databases by that rule.  This spec uses three sets:

| Set | The databases that have |
|----|----|
| Of the plan | A query that needs an entry |
| Selected | A selected query that needs an entry |
| To describe | A query that the run decided to describe, by step 2 of the run |

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

### The exchange

One interface that every call a describer makes to an engine goes through, so that phase 3 can record a run and replay it.

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

This sub-phase delivers the interface and `LiveExchange`, which calls `live`.  `ToolHost` gains `Exchange`, a function from a database's name to its exchange, and the real host gives `LiveExchange`.  The describer of the tests makes its calls through it, so that the tests show a describer written against the interface.

Phase 3 adds the two other shapes and the capture, and meets what they need: a request and an answer of a real describer are plain data that can be written as JSON, and a call that can fail on the server returns the failure as its answer.

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

## Connections

| Source | A named value | A value with no name |
|----|----|----|
| The command line | `--connection billing=Host=...` | None |
| The environment | `SQLSOURCE_CONNECTION_BILLING` | `SQLSOURCE_CONNECTION` |

- **`--connection`** is a name, `=`, and a value: the name is the text before the first `=` and must be a database name by the rule of the `-- database:` marker, and the value is the rest and must not be empty.  Anything else is a wrong command line.  So is a second value for one name, ignoring case.  The message of each names the option and never what was given.
- **The variable of a name** is the name in upper case, in the invariant culture, with every character that is not a letter or a digit replaced by `_`: `billing-v2` reads `SQLSOURCE_CONNECTION_BILLING_V2`.  A variable that is empty is not set.
- **A database's connection** is the first of: the command line's value for its name, ignoring case; its variable; and `SQLSOURCE_CONNECTION`, when the run has exactly one selected database.
- **A name on the command line that is no selected database** is not an error: a script may give every connection it has.  The `help:` line of `SQLSRC213` lists such names, so that a misspelt one is seen.
- **Two selected databases whose names give one variable**, where either has no value on the command line, are `SQLSRC214`.
- **`SQLSOURCE_CONNECTION` with more than one selected database** is not used.  A database to describe that has no other connection gets `SQLSRC215` in place of `SQLSRC213`.

The connection of every selected database is looked up, since the summary says which have none.  A missing one is an error only for a database to describe.

The tool passes a value to the describer and to nothing else.  What it may say about a connection is where it came from: `--connection`, or a variable's name.

## The run

`describe`, after the plan of sub-phase 2.4:

**1. Read what is there.**  For each `Ready` file with a query that needs an entry, the sidecar beside it, if any, through `SidecarReader`.

| The sidecar | Is |
|----|----|
| Read, and `Sidecar.IsWrittenBy(PackageVersion.Prefix)` holds | Usable |
| Of a higher format version | `SQLSRC221` at the sidecar.  The file is left alone, and every selected query of its `.sql` file that needs an entry fails |
| Anything else: absent, malformed, of a lower format version, or of another tool version | Absent |

**2. Decide for each query that needs an entry.**

| The query | Decision |
|----|----|
| Has a problem from the plan, or failed in step 1 | Failed |
| Is selected, and `--force` is given | Describe |
| Has a current entry: the sidecar is usable and `IsCurrentFor(hash, database)` holds for the entry of its name | Skip, and keep the entry |
| Is selected | Describe |
| Otherwise | Left out: it is not selected and has no current entry |

**3. Describe**, database by database in the plan's order, for each database to describe.  The first of these that holds is reported once, at the database's first query to describe, and its queries to describe fail:

- No describer is registered for its dialect, `SQLSRC216`.  This one is reported at each such query, as the epic says.
- `SQLSRC214`, for the second of the pair in the plan's order as well as the first.
- No connection: `SQLSRC215` when `SQLSOURCE_CONNECTION` is set and was not used, and `SQLSRC213` otherwise.
- `OpenAsync` returns a failure.

Otherwise each query is described in the plan's order.  A failure is reported at the query's name and the query fails; the session goes on to the next.

**4. Decide for each `Ready` file.**

| The file | Outcome |
|----|----|
| Has a selected query that needs an entry, and every query of it that needs one was skipped or described | Its target is a `Sidecar` of the tool's two versions with those entries in the `.sql` file's order.  It is written when the writer's text differs from the file on disk, compared with `\r\n` read as `\n` |
| Has a selected query that needs an entry, and a query that failed or was left out | It is left as it was.  When a query of it was described in this run, `SQLSRC217` at the `.sql` file, with one continuation line for each query that held it back: its name, and `failed` or `not in this run` |
| Has no query that needs an entry, in a run with no filter: no `--project`, no `--database` and no `.sql` path | Its sidecar is deleted, if there is one |
| Any other | It is not touched |

A file that is not `Ready` is not touched.  An entry for a query that no longer exists is not in a target, so writing drops it.  A sidecar with no `.sql` file beside it, or beside a file no type claims, is not looked at.

The steps are kept apart in the code: one that gives each file's outcome with its target, and one that applies the outcomes to the disk.  Sub-phase 2.6 compares where this one writes.

**5. Summarise.**  For each selected database, and for each name given with `--database`, one line on standard output:

```
<name> (<engine>): <n> described, <n> skipped, <n> failed
<name> (<engine>): no connection, <n> skipped
```

The counts are of selected queries that need an entry.  The second form is for a database with no connection and nothing to describe.  A database that `--database` names and no query has shows three zeros and no engine.

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
| `SQLSRC215` | Connection names no database | `SQLSOURCE_CONNECTION is for a run with one database, and this run has {0}: {1}` | The count; the names |
| `SQLSRC216` | No describer for the dialect | `This version of sqlsource cannot describe '{0}'` | The dialect's name |
| `SQLSRC217` | Sidecar was not written | `The sidecar of '{0}' was not written, because not every query of the file has an entry` | The `.sql` file's name |
| `SQLSRC218` | File could not be changed | `'{0}' could not be {1}: {2}` | The file; `written` or `deleted`; the system's message |
| `SQLSRC221` | Sidecar was written by a newer tool | `'{0}' has format {1}, and this tool writes format {2}` | The sidecar; its version; the tool's |

`SQLSRC213` has a `help:` line that names the option and the variable, and the names `--connection` gave that are no selected database.  `SQLSRC215` has one that names the variable of each database.  `SQLSRC217` has one: describe the whole file, or fix the queries that failed.  `SQLSRC221` has one: update the `SqlSource.Tool` package.  `SQLSRC221` is out of the order of the others because it was added after the ids up to `SQLSRC220` were given out.

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing and brings its tests and the documents it makes wrong.

1. The epic outline: the row of 2.5 says In progress.
2. The interfaces and their records, `DescriberRegistry`, `IDescribeExchange` with `LiveExchange`, `SampleSql`, and the describer of the tests.
3. Connections, with `SQLSRC213` to `SQLSRC215`.
4. Steps 1 and 2 of the run, with `SQLSRC221`.
5. Describing, with `SQLSRC216` and the format of a describer's error.
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
| `ConnectionTests` | Each of the three sources, and each winning over the ones below it.  The variable of a name with `-`, `.`, `_` and a letter outside ASCII.  A name in another case.  A value that holds `=`.  `--connection` with no `=`, with an empty name, with a name that is no name, with an empty value, and twice for one name: each a wrong command line whose message does not hold what was given.  A name that is no selected database: no error, and it is in the help of `SQLSRC213`.  `SQLSOURCE_CONNECTION` with one selected database, and with two.  Two names with one variable: both from the environment, one on the command line, and both on the command line.  An empty variable |
| `DescribeRunTests` | Each row of the table of decisions.  A second run describes and writes nothing.  A changed query, a changed `-- param:`, a changed default, a changed database: described again.  A sidecar of another tool version, of a lower format version, and one that cannot be read: every query described.  One of a higher format version: `SQLSRC221`, the file is byte for byte as it was, and its queries are failed.  `--force`.  A failure in one query: the others are described, the file is not written, `SQLSRC217` names it.  A failure to open: once for the database.  No connection with every query current: no error, and the summary says so; with one stale: `SQLSRC213` once |
| `SidecarStoreTests` | A file is written, unchanged, and deleted.  An entry of a deleted query is dropped.  A file with `\r\n` and the same content is not written again.  A folder that cannot be written gives `SQLSRC218` and leaves no temporary file.  A file that is not `Ready` is not touched |
| `FilterRunTests` | `--database` with the other database's entries current: a mixed file is written.  With one of them stale: not written, `SQLSRC217` with `not in this run`.  A file whose queries all belong to the other database is not read, written or deleted.  A file that needs no entry keeps its sidecar under each of the three filters, and loses it without one.  A file filter.  `--project` |
| `SummaryTests` | Each form of the line; the counts; a `--database` name that no query has |
| `SecretTests` | A run whose connection value holds a marker word, from the command line and from each variable, with errors of every kind of this spec: neither writer holds the word.  The same for `--connection=`, `--connection:`, and a misspelt `--conection` followed by the value, which sub-phase 2.2's rule for an unknown option stops |

## Documentation

- `src/SqlSource.Tool/AGENTS.md`: a describer returns failures and never throws; every call to an engine goes through the exchange; a connection's value goes to the describer and nowhere else, and `SecretTests` holds the tool to that; a sidecar is written whole, through the store; the two steps of the run stay apart; a filter narrows what a run may change.
- `docs/diagnostics.md`: `SQLSRC213` to `SQLSRC218`, and `SQLSRC221`.
- `README.md`, `CONTRIBUTING.md` and `docs/publishing.md`: nothing in them changes.
- `docs/tech-debt` and `docs/deferred`: nothing is expected.
- The epic outline: in steps 1 and 9.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
