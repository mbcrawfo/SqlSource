# AGENTS.md - src/SqlSource.Tool/Describing

## The steps of a run

- **`Describing/DescribeRun.cs` calls the steps in order, and they stay apart** ([a describe run](../../../docs/architecture/tool.md#a-describe-run)).  The step that decides never writes, the step that writes never decides, and the step that compares does neither.
- **`--check` shares the run and replaces only the step that writes**, `SidecarStore`, with `Describing/SidecarCheck.cs`, which returns its differences as data.  What it says of a sidecar that would be deleted it takes from `FileOutcomes`.
- **A sidecar is written whole or not at all, through `SidecarStore`**, whose remarks say which leftover temporary files a run removes.
- **An entry from an older tool never stands beside a new one.**  A file's target holds an entry for every query that needs one, each described in this run or current (`SidecarEntry.IsCurrentFor` and `Sidecar.IsWrittenBy`); a file with a query that has neither is left as it was.

## Filters

- **A filter narrows what a run may read and change, as well as what it describes** ([writing or checking](../../../docs/architecture/tool.md#writing-or-checking)).  `tests/SqlSource.Tool.Tests/Commands/FilterRunTests.cs` holds it, and `CheckFilterTests.cs` beside it holds that `--check` reports what `describe` with the same filters would change.
- **A sidecar is deleted only in a run with no `--project`, no `--database` and no `.sql` path, in which the reporter holds nothing when `DescribeRun` decides the outcomes.**  One of a higher format version, or one that the system will not let the tool open, is never deleted.

## Databases and connections

- **A database needs a describer and a connection only when a query of it must be described.**  Every selected database's connection is looked up, for the summary, and a missing one is an error only in `DatabaseRuns`.
- **`SQLSOURCE_CONNECTION` is for a run with one database in `RunDecisions.DescribableDatabases`**: a selected database whose every selected query the plan failed is none of them, and `SQLSRC215` neither counts nor names it ([connections](../../../docs/architecture/tool.md#connections)).
- **A query that the plan could not describe, and every query of a file that is not `Ready`, counts as failed and gets no further error.**
- **A described entry or a capture that holds the secret is refused, not cleaned** ([where a connection's value goes](../../../docs/architecture/tool.md#where-a-connections-value-goes)).  An entry is searched by `Describing/EntrySecretCheck.cs`, inside the guard of `Describing/DatabaseRuns.cs`, for the whole forms alone; a finding is `SQLSRC200`, and no sidecar is written.
- **A member of an entry that a describer gives must be left out in `EntrySecretCheck.WithoutTheDescribersPart`**, or a value in it is never found, and gets a fact in `tests/SqlSource.Tool.Tests/Describing/EntrySecretCheckTests.cs`.
- **A capture is written through `CaptureJson.Write(DescribeCapture, ConnectionSecret)`**, which refuses one that holds a whole form or a part: `CapturingExchange` records answers as given.  The overload without the secret is for a capture known to hold no connection.

## Describers

- **`Describing/Describers/` is the contract a describer implements** ([the describer contract](../../../docs/architecture/postgres-describer.md#the-describer-contract)).  It uses only `Outcome`, `Reporting/`, `Logging/` and the generator's types, so a describer depends on no step of the run.  A describer's clock is `OpenRequest.Time`: it reads no `DateTime` and no `Stopwatch`.
- **A describer returns a failure and never throws** ([the describer contract](../../../docs/architecture/postgres-describer.md#the-describer-contract)).  What a server or a user can cause is a `DescribeFailure`, which the run reports at the query in the format of `Describing/DescribeErrors.cs`.  An exception from one is a bug of the describer and ends the run with `SQLSRC200`.
- **A describer is registered in the `DescriberRegistry` of `ToolHost.Create`** ([the host](../../../docs/architecture/tool.md#the-host)), and a query that must be described in a dialect with none registered is `SQLSRC216`.
- **Every call a describer makes to an engine goes through `IDescribeExchange`**, the one it is given in its `OpenRequest`, so that a run can be recorded and replayed ([the exchange](../../../docs/architecture/postgres-describer.md#the-exchange)).
- **The fixtures of a describer are in `tests/SqlSource.Tool.Tests/Fixtures/Describers/<engine>/<case>/`**, beside the engine's `schema.sql`; [PostgreSQL's](../../../tests/SqlSource.Tests.Postgres/README.md#the-describers-fixtures) and [SQL Server's](../../../tests/SqlSource.Tests.SqlServer/README.md#the-describers-fixtures) readmes say what a case holds and how one is recorded.  A case's `query.sql` is a complete SqlSource file with exactly one query.
- **The fixtures are compared byte for byte**: their folder is `text eol=lf` in `.gitattributes` and has a section in `.editorconfig`.
- **A change to an engine's `schema.sql` records every case of that engine again.**  `postgres/schema.sql` is what `tests/SqlSource.Tests.Postgres` applies to its container, and a change to it can move the OIDs that the captures hold.
- **`tests/SqlSource.Tool.Tests/FakeDescriber.cs` is a describer written by these rules** ([test describers](../../../docs/architecture/postgres-describer.md#fixtures-and-test-describers)).  Its `OpenAsync` makes its `Outcome` with the constructor, as every opener does.

## Requests, answers and captures

- **A request and an answer are plain records, and nothing of the seam crosses the exchange** ([requests and answers](../../../docs/architecture/postgres-describer.md#requests-and-answers)).  A record of a call is sealed and holds strings, integers, booleans, nullable ones, `IReadOnlyList<T>` of them and records of the same kind.  It never holds a driver type, a `DateTime`, an `Exception`, an enum, an abstract base, a dictionary, anything polymorphic, an `Outcome`, a `DescribeFailure`, a `QueryDescription` or an `EquatableArray`.
- **A describer maps between its records and the seam's types on its own side.**  A call that can fail on the server has an answer whose `error` is null on success.
- **A request holds everything that decides the answer, and nothing of a connection**: not the value, a part of it, the host or the user's name.  A name that a describer makes up and sends is a counter within its session and never random.
- **An `error` of `open`, of a session that the server ended, or of a connection that was lost or timed out holds the engine's code of the error**, PostgreSQL's SQLSTATE, where there is one, and a fixed text of the describer's own, never the driver's or the server's message, which would name them.  A driver's refusal of a query's text or of a parameter's type keeps the driver's message.
- **`tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs` holds every record whose name ends in `Request` or `Answer`**, in the namespace of an engine's describer or under it and in the tests', to these rules.  A describer in a new namespace adds it to `Under` there.
- **A capture outlives the version that wrote it** ([capture and replay](../../../docs/architecture/postgres-describer.md#capture-and-replay)), so what a describer asks and is answered is a contract with the captures that exist.  The name of a method never changes.
- **A request's record may gain a member only when it is nullable and its null means what the request meant before** ([how a record may change](../../../docs/architecture/postgres-describer.md#how-a-record-may-change)).  A member that is renamed, removed or moved, a member of a value type, and a new member where it holds a value each make a question that no older capture answers.
- **An answer's record may gain a member, which must be nullable**, one of a value type too.  `tests/SqlSource.Tool.Tests/Describing/Capture/ReplayingExchangeTests.cs` holds these rules of a record.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
