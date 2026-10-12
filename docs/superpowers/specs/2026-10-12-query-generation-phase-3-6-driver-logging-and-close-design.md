# Query generation, phase 3.6: driver logging, registration and close - design

Date: 2026-10-12

Sub-phase 3.6 of the [query generation epic](2026-10-07-query-generation-epic-design.md), and the last of phase 3.  It wires Npgsql's own logging into the run's log, registers the PostgreSQL describer in the real host so that `sqlsource describe` describes a `postgres` query, proves the whole command against a container, and closes the phase: the tool's readme, the outline, and what was deferred or left as debt.

It builds on sub-phases 3.4 and 3.5.

## Goal

```console
$ dotnet sqlsource describe --connection app="Host=localhost;Database=app;Username=app;Password=***" --log sqlsource.log
app (postgres): 12 described, 0 skipped, 0 failed
$ dotnet sqlsource describe --check --connection app="..."
app (postgres): 12 same, 0 differ, 0 failed
$ grep -c '"event":"postgres.driver"' sqlsource.log
38
```

Success is:

- A project with `postgres` queries gets its sidecars from the released tool, and `--check` passes against them.
- A log holds the driver's view of the session, enough to see the wire-level order and which statement failed, and never the connection string, the host, the user or the database's name.
- The outline says what phase 3 delivered, and nothing of its scope is undocumented.

## Decisions

From the epic, unchanged: driver logging is wired into the same file when `--log` is given, Npgsql through its logger factory with parameter logging off; the log never holds a host, a user or a database's own name; the tool's readme is the package's readme; the outline is kept current until the epic closes; the README of the repository waits for phase 5.

Settled in this spec:

| Decision | Choice | Why |
|----|----|----|
| Which of Npgsql's categories reach the log, at which level | `Npgsql.Command` and `Npgsql.Transaction` at Debug and above; every other category, `Npgsql.Connection` among them, at Warning and above | Npgsql's connection messages at Debug name the host and the database.  The command messages hold the SQL as sent, which is the tool's own, and the transaction messages the order of begin and rollback. |
| What a driver event holds | `category`, `level`, `eventId`, `message`, and for an exception its type and message | The message is the driver's text through `SessionRunLog`, so the whole connection value is removed if it is ever there.  No stack trace. |
| When the factory is wired | Only when the run has a log | A `NullRunLog` run pays nothing for formatting messages nobody reads. |
| Where the command is proven whole | `tests/SqlSource.Tests.Postgres`, running `Cli.RunAsync` with a host whose registry holds the describer, over a project of the tool's tests compiled into it by link | The tool's own tests have no database.  The helpers that make a project and a host exist in `tests/SqlSource.Tool.Tests` and are shared the way the generator's harness is shared with that project. |

## Out of scope

- Models, the type map, the end-to-end project's `.sql` files with committed sidecars, and `tools/describe-end-to-end.sh`.  Phase 5.
- The README of the repository.  Phase 5, when a user can do something with the sidecar.
- SQL Server's driver logging, through its event source.  Phase 6.
- `sqlsource diagnose` and `sqlsource replay`.  Phase 10.

## Driver logging

`NpgsqlRunLogger` in `src/SqlSource.Tool/Describing/Postgres/`: an `ILoggerFactory` whose loggers write `postgres.driver` events to the log the describer was given, with a minimum level per category as decided.  `PostgresConnection` builds its `NpgsqlDataSource` with `UseLoggerFactory(logger)` when the log is not `NullRunLog`, and with no factory otherwise.  Parameter logging stays off, Npgsql's default.  `BeginScope` is a no-op.

| Event | Members |
|----|----|
| `postgres.driver` | `category`, `level`, `eventId`, `message`, `exception`: `type: message` or null |

Npgsql depends on `Microsoft.Extensions.Logging.Abstractions`, so the tool references nothing new.

## Registration

`ToolHost.Create` gives `new DescriberRegistry([new PostgresDescriber()])`.  Everything else of the run is in place since phase 2: `SQLSRC216` is now what a `mssql` query gets until phase 6.  The tests of the tool that run with `Hosts.Create` keep their empty registry and `FakeDescriber`.

## The command, proven

`tests/SqlSource.Tests.Postgres/Describer/DescribeCommandTests` compiles, by link, the helpers of `tests/SqlSource.Tool.Tests` that a run needs with no MSBuild, `TestProject`, `FakeProject`, `FakeProcessRunner`, `Hosts`, `CliRun`, `TempFolder` and what they depend on, and gives the host a registry with the describer and a `LiveExchange`:

| Case | Asserts |
|----|----|
| `describe` over a project with queries of each kind of this phase's fixtures | Exit `0`; the sidecars are written and `SidecarReader` reads them; each entry equals the describer's fixture of the same query |
| `describe` again | Every query skipped, no file's time changes |
| `--check` | Exit `0` |
| A column added to a table, then `--check` | Exit `2`, `SQLSRC219` for the `SELECT *` query |
| `describe --force` after the change | Exit `0`, the sidecar holds the column |
| A query the server rejects beside one it accepts, in one file | Exit `1`, `SQLSRC225` at the first and `SQLSRC217` for the file |
| `--log`, with a connection string whose host, user, database and application name are distinctive words | The file holds `postgres.driver` events and none of the four words; `SecretTests`' marker word is nowhere |
| A connection string that cannot be opened | `SQLSRC224`, exit `1` |

One test runs the packed command as a subprocess, `sqlsource describe` with `--connection`, against the container, to prove the command line end to end; it is the test the epic asks for in each end-to-end project.

## Closing phase 3

- `src/SqlSource.Tool/README.md`: "this version has no describer" goes; a section on PostgreSQL: the connection string is Npgsql's; what is described and how, in a paragraph; `-- param:` and when it is needed, with the optional-filter pattern; the override suffixes; what nullability can say and what it cannot, a view and a subquery kept by the planner among the cannots; that the tool prepares and explains and executes nothing, and leaves the session as it found it; the server versions supported.
- The epic outline: the row of phase 3 says Done; the phase 3 section records what the sub-phases settled, as phase 2's does; the table of ids gains this phase's; the notes on nullability and on the describer's route are as the spike settled them.
- `src/SqlSource.Tool/AGENTS.md` is read against the code as it ended up, and corrected.
- `CONTRIBUTING.md` says what the container project proves now.
- Anything of phase 3's scope that was not delivered has an item in `docs/deferred`; the items of `docs/tech-debt` this phase added are read once more against the code.

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing and brings its tests and the documents it makes wrong.

1. The epic outline: the row of 3.6 says In progress.
2. `NpgsqlRunLogger` and the wiring.
3. Registration in `ToolHost.Create`.
4. `DescribeCommandTests` and the subprocess test.
5. The tool's readme.
6. The documents, the outline's close, and the row set to Done.

## Testing

| Where | Cases |
|----|----|
| `NpgsqlRunLoggerTests` (tool tests) | A message of each category at each level: written or dropped as the table says; the event's members; an exception's type and message; scopes are no-ops; a `NullRunLog` run builds no factory |
| `DescribeCommandTests` (container) | The table above |
| `ToolHostTests` (tool tests) | The real host's registry finds a describer for `postgres` and none for `mssql` |
| `tools/check-tool-install.sh` | Unchanged: `describe` in an empty folder is still `SQLSRC201` |

## Documentation

- `src/SqlSource.Tool/README.md`, `src/SqlSource.Tool/AGENTS.md`, `CONTRIBUTING.md`: as under Closing phase 3.
- `docs/diagnostics.md`: `SQLSRC216` says that `postgres` is described from this version and `mssql` is not yet.
- The repository's `README.md` and `docs/publishing.md`: nothing changes.
- `docs/tech-debt`: the levels at which the driver's categories are forwarded trust Npgsql's messages at those levels not to name the host; the test holds it for the Npgsql the tool pins.
- `docs/deferred`: as under Closing phase 3.
- The epic outline: in steps 1 and 6.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
