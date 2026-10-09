# Query generation, phase 2.6: `--check` and logging - design

Date: 2026-10-09

Sub-phase 2.6 of the [query generation epic](2026-10-07-query-generation-epic-design.md), and the last of phase 2.  It delivers `describe --check`, which compares the committed sidecars with what the database says today and writes nothing, and the two ways a run says what it did: `--verbose` for the person watching, and `--log` for a file a maintainer can read.

It builds on sub-phase 2.5.  Phase 3 builds on phase 2 as a whole.

## Goal

```console
$ dotnet sqlsource describe --check
src/App/Queries/Users.sql(3,10): error SQLSRC219: The sidecar is out of date for 'GetUser': columns[2].nullable is true, and the database says false
    help: run 'dotnet sqlsource describe' and commit the sidecars
    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc219
postgres (postgres): 19 described, 1 differ, 0 failed
$ echo $?
2
```

```console
$ dotnet sqlsource describe --log sqlsource.log
$ head -c 160 sqlsource.log
{"event":"run.start","time":"2026-10-09T14:02:11.412Z","toolVersion":"0.1.0","formatVersion":1,"runtime":".NET 10.0.1","os":"Darwin 25.6.0","arguments":["de
```

Success is:

- `--check` exits `0` when the committed sidecars are what `describe --force` would write, by the comparison of the format design; `2` when they are not; and `1` when a query could not be described.  It changes no file.
- `--verbose` says, for each project, file and query, what the run decided and why.
- A log holds every decision of the run as one JSON object on a line, and never a connection's value.
- A describer can add its own events to the same log, which phase 3 does.

## Decisions

From the epic, unchanged: `--check` describes every query, as `--force` does, and compares; what it compares and what it ignores; under `--database` it compares the entries of the named databases alone; a committed file whose `formatVersion` is not the tool's is a difference and is not compared; `--verbose` writes prose to the console and nothing to disk; `--log <path>`, or `SQLSOURCE_LOG`, writes JSON Lines for the run, overwritten each run, and keeps the console quiet; what a log holds for a run and for a database, and what it never holds; a payload over 1 MiB is cut and marked; a failed run ends with one line that says how to get a log.

Settled in this spec:

| Decision | Choice | Why |
|----|----|----|
| Where `--check` builds what it compares | In memory | The epic says a temporary location.  The run of sub-phase 2.5 already gives each file's target before anything is written. |
| `--check` with `--force` | Accepted, and the same as `--check` | `--check` describes everything already. |
| A query that fails under `--check` | An error, and exit `1`, whatever else differs | "Could not check" is not "differs".  A build must tell the two apart. |
| Where a difference is reported | At the query in the `.sql` file; at the sidecar for a difference that has no query | A user fixes it from the `.sql` file.  An entry for a query that is gone has only the sidecar to point at. |
| One difference for an entry | The first, named by its path | The fix is the same for one difference or ten: run `describe`. |
| The log's option over its variable | `--log` wins over `SQLSOURCE_LOG` | As the command line wins for a connection. |
| How a describer logs | `OpenRequest` gains the run's log | Phase 3 then adds events and changes nothing here. |

## Out of scope

- A describer's own events: the SQL as sent, what a server answered, the plan, the walk.  Phase 3.
- Driver logging.  Phase 3.
- `sqlsource diagnose` and `sqlsource replay`, and any obfuscation.  Phase 10.
- A check inside a build.  The epic keeps `--check` a step of CI outside the build.

## The command line

```
sqlsource describe [<path>...] [--project <path>]... [--database <name>]...
                   [--connection <[name=]value>]... [--force] [--check]
                   [--verbose] [--log <path>]
```

## `--check`

The run of sub-phase 2.5, with three changes:

1. **Every selected query that needs an entry is described.**  None is skipped for a current entry.  A database with such a query needs a connection: `SQLSRC213` otherwise.
2. **Nothing is written or deleted.**  The step that applies the outcomes to the disk is replaced by the one that compares.
3. **Each `Ready` file's target is compared with the sidecar on disk**, as below.

A difference is `SQLSRC219`.  Its second argument says what differs.

| Condition | Reported at | What differs |
|----|----|----|
| The file has selected queries that need an entry, and no sidecar | The first such query | `the sidecar does not exist` |
| The sidecar cannot be read, or its `formatVersion` is not the tool's | The sidecar | `the sidecar cannot be read`, or `the sidecar has format <n>, and this tool writes <m>` |
| A selected query that needs an entry has none in the sidecar | The query | `the sidecar has no entry` |
| `SidecarComparer.FindDifference` finds a difference between the entry on disk and the one described | The query | `<path> is <committed>, and the database says <described>` |
| The sidecar has an entry for a name that no query of the file has, or for a query that needs none | The entry's line in the sidecar | `the query is not in the file`, or `the query needs no entry` |
| The file has no query that needs an entry, and a sidecar | The sidecar | `no query of the file needs an entry` |

- The file's `toolVersion` is not compared.  A sidecar whose `toolVersion` is not the tool's is compared as any other.
- **Under a filter**, the first, third and fourth rows hold for selected queries alone.  The fifth holds for an entry whose own `database` is one of `--database`, or for every entry when no `--database` is given.  The second holds for a file with a selected query.  The sixth holds only for a run with no filter at all.
- A query that failed to describe is compared with nothing.  Its error is reported and the exit code is `1`.
- A file that is not `Ready` is compared with nothing: the plan's error stands.

The summary line under `--check`:

```
<name> (<engine>): <n> described, <n> differ, <n> failed
```

A query counts under `differ` when a difference was reported at it.  Differences reported at a sidecar are in no count; they still decide the exit code.

**The exit code** is `1` when any error but `SQLSRC219` was reported, else `2` when `SQLSRC219` was, else `0`.

### Diagnostics

| Id | Title | Message | Arguments |
|----|----|----|----|
| `SQLSRC219` | Sidecar is out of date | `The sidecar is out of date for '{0}': {1}` | The query's name, or the `.sql` file's name for a difference of the whole file; what differs |

It has a `help:` line: `run 'dotnet sqlsource describe' and commit the sidecars`.  It is in `ToolDiagnostics`, with the four places it needs.

## `--verbose`

Lines of prose on standard output, each starting `sqlsource: `, in the order things happen:

- The unit, and how it was found.
- Each project: read, with its target framework and the number of `.sql` files; or left out, because it does not use SqlSource or `--project` does not name it.
- Each claimed file: its dialect, the types that claim it, and its state.  The number of files no type claims, in one line for a project.
- Each database: its dialect, where its connection came from, `--connection` or the name of a variable, and the server's version once a session is open.
- Each query that needs an entry: described, with the time it took; skipped, because its entry is current; failed; or not selected.
- Each sidecar: written, unchanged, deleted or left as it was; under `--check`, the same or different.

Without `--verbose` the run prints errors, the summary lines, and nothing else.

## The log

`--log <path>`, or `SQLSOURCE_LOG` when the option is not given.  The file is created, or emptied, when the run starts; a path that cannot be written is `SQLSRC218` and the run does not start.  It is UTF-8 with `\n` line endings, one JSON object on a line, written as the run goes so that a run that is stopped leaves what it did.

Every object has `event`, the event's name, and `time`, the moment in UTC as ISO 8601 with milliseconds.

| Event | When | Holds |
|----|----|----|
| `run.start` | First | `toolVersion`, `formatVersion`, `runtime`, `os`, and `arguments`: the command line, with the value of each `--connection` replaced |
| `project` | For each project of the unit | `path`, `usesSqlSource`, `inRun`, `targetFramework`, `sqlFiles`, `compileFiles` |
| `file` | For each claimed file | `path`, `project`, `dialect`, `state`, `claims`, `queries` |
| `database` | For each database with a query to describe | `name`, `engine`, `connectionSource`, `describer`: whether one is registered |
| `database.open` | When a session opens or fails to | `name`, `serverVersion`, `driver`, `settings`, or `error` |
| `query` | For each query that needs an entry | `file`, `name`, `database`, `hash`, `selected`, `decision`: `described`, `skipped`, `failed` or `left-out`; `durationMs` for one described; `error`, the id, for one that failed |
| `sidecar` | For each `Ready` file | `path`, `action`: `written`, `unchanged`, `deleted`, `held-back`, `same` or `differs` |
| `diagnostic` | For each error the reporter writes | `id`, `path`, `line`, `column`, `message`, and the continuation lines |
| `run.end` | Last | `exitCode`, `durationMs`, and the counts of each database |

- **A connection's value never reaches the log.**  The log's writer is never handed one, and `arguments` is built by a function that replaces what follows `--connection`, in both of its forms, `--connection x` and `--connection=x`: a named value keeps its name, `billing=***`, and one with no name is `***`.
- **Never written**: a host, a user or a database's own name; the environment.
- **A string over 1 MiB**, counted in UTF-8, is cut to that size, and its object gains `"truncated": true`.  Nothing in phase 2 is that long; the rule is here because the writer is.
- A reader ignores an event and a key it does not know.  The log is for people and carries no version.

`IRunLog` is the interface: one method that takes an event's name and its members.  `NullRunLog` is what a run without `--log` has.  `OpenRequest` gains `Log`, so that a describer writes into the same file; an event of a describer has a name that starts with its engine, `postgres.`.

A run that exits `1`, without `--log`, ends with one line on standard error:

```
sqlsource: run again with --log <file> for a record of what the run did
```

## Closing phase 2

The last step of this sub-phase closes the phase:

- The outline's rows for 2.1 to 2.6 say Done, and its section on phase 2 says what was delivered where it differs from what these specs planned.
- `src/SqlSource.Tool/AGENTS.md` is read against the code as it ended up, and corrected.
- Anything of phase 2's scope that was not delivered has an item in `docs/deferred`, as `AGENTS.md` requires.

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing and brings its tests and the documents it makes wrong.

1. The epic outline: the row of 2.6 says In progress.
2. `--check`: every query described, nothing written, and the exit code's three values.
3. The comparison and `SQLSRC219`, without filters.
4. The comparison under filters, and the summary line.
5. `IRunLog`, the writer of the file, `--log` and `SQLSOURCE_LOG`, with `run.start`, `run.end` and `diagnostic`.
6. The other events, and `Log` on `OpenRequest`.
7. `--verbose`.
8. The line that ends a failed run.
9. The documents, and the close of phase 2.

## Testing

| Where | Cases |
|----|----|
| `CheckTests` | Sidecars written by `describe`, then `--check`: exit `0`, nothing reported.  Each row of the table of differences, with the id, the position and the text.  A changed `toolVersion` alone: exit `0`.  Each member that the comparison ignores, changed in the file: exit `0`.  A query that fails: exit `1`, and exit `1` with a difference beside it.  No connection: `SQLSRC213` and exit `1`.  With `--force`: the same.  No file's time or content changes in any case, and no sidecar is made or deleted |
| `CheckFilterTests` | `--database` with a difference in the other database's entry: exit `0`.  An entry of a deleted query whose `database` is named, and one whose is not.  A file filter.  The sixth row only without a filter |
| `RunLogTests` | Each event of the table, with its members, for a run that describes, skips and fails.  The order.  Every line is one JSON object.  `--log` over the variable; the variable alone; neither.  A path that cannot be written.  The file is emptied by a second run.  A string of 2 MiB is cut and marked |
| `RedactionTests` | `--connection name=value`, `--connection value`, `--connection=name=value` and `--connection=value`: `arguments` holds no value.  The `SecretTests` of sub-phase 2.5, with `--verbose` and `--log`: neither writer nor the file holds the marker word |
| `VerboseTests` | A line for each thing the list names, for a run over two projects and two databases.  Without the option: errors and summaries alone.  With `--log` and not `--verbose`: the console is as without either |
| `ExitCodeTests` | Each of `0`, `1` and `2`, and the line that ends a failed run: there for `1` without `--log`, not for `2`, not with `--log` |
| A describer of the tests that logs | Its event is in the file, in order, between the `database.open` and the `query` it belongs to |

## Documentation

- `docs/diagnostics.md`: `SQLSRC219`.
- `src/SqlSource.Tool/AGENTS.md`: `--check` shares the run and replaces only its last step; the exit code's three values and which errors give which; an event's name and members are read by people and by phase 10's bundle, so a member is added and not renamed; nothing hands the log a connection's value, and `RedactionTests` holds the tool to that.
- `CONTRIBUTING.md`: nothing changes, unless the tool's tests gained a step a developer must know.
- `README.md` and `docs/publishing.md`: nothing in them changes.  The tool's commands reach the readme with phase 5.
- `docs/deferred`: as under Closing phase 2.
- The epic outline: in steps 1 and 9.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
