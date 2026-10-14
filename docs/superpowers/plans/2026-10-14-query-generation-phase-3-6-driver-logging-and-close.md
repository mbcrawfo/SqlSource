# Query generation, phase 3.6: driver logging, registration and close - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The released tool describes `postgres` queries: Npgsql's own messages reach the run's log, `ToolHost.Create` registers the PostgreSQL describer, the whole command is proven against the container, and phase 3 is closed in its documents.

**Architecture:** `NpgsqlRunLogger` is an `ILoggerFactory` over the describer's log that forwards `Npgsql.Command` and `Npgsql.Transaction` from Debug and every other category from Warning, as `postgres.driver` events.  `PostgresConnection` hands it to its `NpgsqlDataSourceBuilder` only when the log keeps anything, which `IRunLog.Writes` says through the `SessionRunLog` the describer is given.  `ToolHost.Create` gets a registry with `PostgresDescriber`.  `tests/SqlSource.Tests.Postgres` links the in-process command harness of the tool's tests and runs `Cli.RunAsync` over projects of fixture queries, and runs the built tool once as a process of its own.

**Tech Stack:** C# (`LangVersion` `preview`); the tool on `net8.0`, the tests on `net10.0`; Npgsql 10.0.3 and its `Microsoft.Extensions.Logging.Abstractions` 8.0.0, already transitive; Testcontainers.PostgreSql; xunit v3 on Microsoft.Testing.Platform with Shouldly; Docker.

**Spec:** `docs/superpowers/specs/2026-10-12-query-generation-phase-3-6-driver-logging-and-close-design.md`.  Read it with this plan.  The epic outline, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, is kept current and wins where the two disagree.

**Branch:** `claude/query-gen-driver-logging-1a3dfe`, which exists and is at `main`.  This file is untracked until Task 1 commits it.

## Where the plan settles what the spec leaves open

These are the readings the plan takes; the spec stays as written, and Task 6 records the first, the fourth and the sixth in the epic outline's phase 3 section.

- **`IRunLog` gains `bool Writes { get; }`.**  The spec wires the factory "when the log is not `NullRunLog`", but `DatabaseRuns` gives a describer `new SessionRunLog(reporter.Trace.Log, value, "postgres.")`, never `NullRunLog` itself.  `NullRunLog` says `false`, `RunLogFile` and the tests' `RecordingLog` `true`, and `SessionRunLog` says what its inner log says.
- **The logger writes the event `driver`**, and the describer's `SessionRunLog` makes it `postgres.driver`, as every other event of `PostgresEvents` is written without its prefix.  A message therefore passes through the redaction of the connection's value, as the spec's decision requires, with no code of its own.
- **The members' values.**  `category` is the logger's category name as Npgsql gives it; `level` is the `LogLevel` in lower case, `debug`, `information`, `warning`, `error` or `critical`; `eventId` is `EventId.Id` as a number; `message` is the formatter's text; `exception` is `exception.GetType().FullName + ": " + exception.Message`, or null, and always null for `Npgsql.Connection`.  `Trace` is below Debug and is never forwarded; `LogLevel.None` is never enabled.
- **`PostgresConnection` takes the log in its constructor**, `PostgresConnection(string connectionString, IRunLog log)`.  `PostgresSession.OpenAsync` passes the describer's log, and the four constructions in `ExplainTests` pass `NullRunLog.Instance`.
- **The container project links what `CliRun` needs and nothing more.**  The spec says `CliRun` brings `FakeDescriber` and its `SQLSRC999`; it does not: `CliRun` holds a `List<IQueryDescriber>` and names no fake.  The linked set is `CliRun`, `CliResult`, `Hosts`, `TempFolder`, `FakeProcessRunner`, `FakeProject`, `ProcessGate`, `TestProject`, `Plans`, `Secrets`, `LogReader`, `SidecarFiles`, `Programs` and `Fixtures/FixtureProjects`.  With no `FakeDescriber` there is no descriptor, so the project gets no `AnalyzerReleases` files.  If the build reports `RS2008` all the same, add the two files as the tool's tests have them and say so in the pull request.
- **`CliRun`'s exchange stays a `CapturingExchange` over `LiveExchange.Instance`.**  Every call still goes to the server; the capture is unused.  `CliRun` is shared and is not changed for this.
- **The fixture comparison sets three members equal, not two.**  `SidecarEntry` has `NameSpan`, the place of the query's name in the sidecar's text, which differs between `expected.json`, whose `toolVersion` is `fixture`, and a sidecar the tool writes.  The comparison sets `NameSpan`, `ServerVersion` and `Database` to one value on both entries.
- **The cases that change the schema use a table of the test's own**, `command_widgets`, created at the start of the test and dropped in a `finally`.  The container is shared with every fixture case, which `FixtureCasesTests` compares against `schema.sql` as it is.
- **The log test's host is the container's own.**  A name of the test's own does not resolve.  The user, the database and the application name are the test's: it creates the role `sqlsrc_wombat` with the password `Secrets.Marker` and the database `sqlsrc_numbat` owned by it, when they are not there yet, and names the application `sqlsrc_quokka`.  The test asserts that the host's value is nowhere in the log, beside the three words and the marker.
- **The connection that cannot be opened is the container's host on port `1`**, which refuses at once on every runner; a name under `.invalid` depends on the resolver.
- **The subprocess test runs on a copy of the fixture project `Single`**, with a type that claims its `Queries` folder written beside its `.sql` file, since `Single` has none and the run would describe nothing.  `Programs` gains the start of the tool that `TerminationTests` has privately, so both projects start it one way.
- **The tech-debt item of the forwarded levels is written in Task 2**, which introduces the choice, and the items of `docs/deferred` and the re-reading of phase 3's `docs/tech-debt` items in Task 6.

## Global Constraints

- Driver logging is wired only when the run has a log.  Parameter logging stays off: nothing calls `EnableParameterLogging`.  `BeginScope` is a no-op.
- `Npgsql.Command` and `Npgsql.Transaction` are forwarded at Debug and above; every other category, `Npgsql.Connection` among them, at Warning and above.
- A `postgres.driver` event's members are, in this order: `category`, `level`, `eventId`, `message`, `exception`.  An event's name and members are a contract.  No stack trace is written.
- The log never holds a host, a user, a database's own name, an application name or a password.
- The tool references no new package.
- `ToolHost.Create` gives `new DescriberRegistry([new PostgresDescriber()])`.  `Hosts.Create`, `Hosts.Real` and `CliRun` keep `DescriberRegistry.Empty` or their own list.
- `tools/check-tool-install.sh` is not changed: `describe` in an empty folder is still `SQLSRC201`.
- The repository's `README.md` and `docs/publishing.md` are not touched.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`, Roslynator and Sonar.  Fix what it reports in the code; never suppress.  If a check is wrong, stop and tell the user.
- `ImplicitUsings` is off: a file names every namespace it uses.
- Comments say what a thing is for, in the plain declarative style of the files around them, with two spaces after a full stop.  Types under `src/` have XML documentation; test files use `//` comments.
- Test names are `Method_Scenario_Outcome`; assertions are Shouldly's.  A test takes its token from `TestContext.Current.CancellationToken`.
- An `AGENTS.md` that names a file, type or folder is updated in the commit that renames, moves or adds it.
- Every commit passes `./pre-commit-validation.sh`, which needs Docker running and starts a PostgreSQL container.  Run `./format.sh`, then the validation, then commit in a command of its own.  Never work around a failure.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

How a test class is run by itself, here and in every task:

```bash
dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.NpgsqlRunLoggerTests"
dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.DescribeCommandTests"
```

## Review Focus

- **A failed open that some category other than `Npgsql.Connection` reports at Warning or above.**  Npgsql has `Npgsql.Exception` too, and its exceptions on a failed connect name the endpoint.  A user expects no host in the log whatever the driver does.  Task 4, `Describe_ConnectionRefusedWithLog_IsSqlsrc224AndNoDriverEventHoldsTheHost`, asserts it of every `postgres.driver` event, the `exception` member included.  If it fails, stop and tell the user: the spec's level table is then wrong, not the test.
- **A run with `--log` that describes against two databases at once.**  Npgsql logs from whichever thread runs the call; `RunLogFile` is written from several sessions.  A user expects whole lines.  Task 4's log test reads every line with `LogReader.Read`, which fails on a line that is not one JSON object.
- **A connection string whose password the driver repeats in a Warning.**  A user expects `***`.  Task 2, `Log_MessageThatHoldsTheConnection_IsWrittenWithoutIt`, writes through a `SessionRunLog`.
- **A run without `--log`.**  A user expects the same speed and output as before.  Task 2, `For_LogThatKeepsNothing_GivesNoFactory`, over `NullRunLog` and over a `SessionRunLog` around it.
- **A second `describe` after the first.**  A user expects nothing rewritten.  Task 4, `Describe_Again_SkipsEveryQueryAndWritesNoFile`, compares each sidecar's last write time.

---

## File structure

| File | Change |
|----|----|
| `src/SqlSource.Tool/Logging/IRunLog.cs`, `NullRunLog.cs`, `SessionRunLog.cs`, `RunLogFile.cs` | `Writes` |
| `src/SqlSource.Tool/Describing/Postgres/NpgsqlRunLogger.cs` | New: the factory and its logger, a private nested class |
| `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs` | Takes the log; builds the data source through `NpgsqlDataSourceBuilder` |
| `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs` | Passes the log to the connection |
| `src/SqlSource.Tool/Describing/Postgres/PostgresDescriber.cs`, `Describers/DescriberRegistry.cs`, `ToolHost.cs` | Registration, and the comments that say nothing is registered |
| `tests/SqlSource.Tool.Tests/RecordingLog.cs` | `Writes` |
| `tests/SqlSource.Tool.Tests/Describing/Postgres/NpgsqlRunLoggerTests.cs` | New |
| `tests/SqlSource.Tool.Tests/ToolHostTests.cs` | The real registry |
| `tests/SqlSource.Tool.Tests/Programs.cs`, `Commands/TerminationTests.cs` | `StartTool` moves to `Programs` |
| `tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj` | The links |
| `tests/SqlSource.Tests.Postgres/Describer/DescribeCommandTests.cs`, `ToolProcessTests.cs` | New |
| `tests/SqlSource.Tests.Postgres/Describer/ExplainTests.cs` | `NullRunLog.Instance` to the connection |
| `src/SqlSource.Tool/README.md`, `src/SqlSource.Tool/AGENTS.md`, `tests/AGENTS.md`, `CONTRIBUTING.md`, `docs/diagnostics.md`, `docs/tech-debt/`, `docs/deferred/`, the epic outline | Documents |

---

### Task 1: The plan and the outline

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md:76`
- Add: this plan

- [ ] **Step 1: Set the row of 3.6 in the table of Phases to `In progress`.**  The row is at line 76 and says `Not started`.

- [ ] **Step 2: Validate and commit**

```bash
./format.sh && ./pre-commit-validation.sh
```

```bash
git add docs/superpowers/plans/2026-10-14-query-generation-phase-3-6-driver-logging-and-close.md docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
git commit -m "Start sub-phase 3.6: driver logging, registration and close" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `NpgsqlRunLogger` and the wiring

**Files:**
- Modify: `src/SqlSource.Tool/Logging/IRunLog.cs`, `NullRunLog.cs`, `SessionRunLog.cs`, `RunLogFile.cs`, `tests/SqlSource.Tool.Tests/RecordingLog.cs`
- Create: `src/SqlSource.Tool/Describing/Postgres/NpgsqlRunLogger.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs`, `PostgresSession.cs:86`, `tests/SqlSource.Tests.Postgres/Describer/ExplainTests.cs:40,67,93,117`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/NpgsqlRunLoggerTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Logging/SessionRunLogTests.cs`
- Create: `docs/tech-debt/TD-0048-driver-levels-trust-npgsqls-messages.md`; modify `docs/tech-debt/README.md`
- Modify: `src/SqlSource.Tool/AGENTS.md` (the folder's file list and the log's events)

**Interfaces:**
- Produces: `IRunLog.Writes`; `internal sealed class NpgsqlRunLogger(IRunLog log) : ILoggerFactory` with `public const string EventName = "driver"` and `public static ILoggerFactory? For(IRunLog log)`; `PostgresConnection(string connectionString, IRunLog log)`.

- [ ] **Step 1: Write the failing tests**

In `SessionRunLogTests`:

```csharp
[Fact]
public void Writes_OverALog_IsWhatTheInnerLogSays()
{
    new SessionRunLog(NullRunLog.Instance, "x", "postgres.").Writes.ShouldBeFalse();
    new SessionRunLog(new RecordingLog(), "x", "postgres.").Writes.ShouldBeTrue();
}
```

`NpgsqlRunLoggerTests`, in `SqlSource.Tool.Tests.Describing.Postgres`, over a `RecordingLog`:

| Test | Asserts |
|----|----|
| `Log_CommandOrTransactionAtDebugOrAbove_IsWritten` (theory over `Npgsql.Command`, `Npgsql.Transaction` × `Debug`, `Information`, `Warning`, `Error`, `Critical`) | One event named `driver` |
| `Log_CommandOrTransactionAtTrace_IsDropped` | No event; `IsEnabled(LogLevel.Trace)` is false |
| `Log_OtherCategoryBelowWarning_IsDropped` (theory over `Npgsql.Connection`, `Npgsql.Copy`, `Npgsql.Exception`, `Npgsql.Replication` × `Trace`, `Debug`, `Information`) | No event |
| `Log_OtherCategoryAtWarningOrAbove_IsWritten` (theory over the same four × `Warning`, `Error`, `Critical`) | One event |
| `Log_AnyCategoryAtNone_IsDropped` | `IsEnabled(LogLevel.None)` is false for `Npgsql.Command` |
| `Log_Message_HasTheMembersInOrder` | Members `category` `"Npgsql.Command"`, `level` `"debug"`, `eventId` `2001`, `message` the formatted text, `exception` null, in that order |
| `Log_Exception_IsItsTypeAndMessage` | `Npgsql.Command`, `Error`, `new InvalidOperationException("boom")`: `exception` is `"System.InvalidOperationException: boom"` |
| `Log_ExceptionOfConnection_IsNotWritten` | `Npgsql.Connection`, `Error`, an exception whose message is `"Failed to connect to db.example:5432"`: `exception` is null |
| `Log_MessageThatHoldsTheConnection_IsWrittenWithoutIt` | Through `new SessionRunLog(recording, Secrets.Connection, "postgres.")`: the event is `postgres.driver` and its `message` holds `***` and not `Secrets.Connection` |
| `BeginScope_AnyState_IsANoOpThatCanBeDisposed` | Returns a non-null disposable; disposing it writes nothing |
| `For_LogThatKeepsNothing_GivesNoFactory` | `For(NullRunLog.Instance)` and `For(new SessionRunLog(NullRunLog.Instance, "x", "postgres."))` are null |
| `For_LogThatWrites_GivesAFactory` | `For(new RecordingLog())` is an `NpgsqlRunLogger` |

A message is logged with `logger.Log(level, new EventId(2001), "state", exception, (state, _) => "Executing command: SELECT 1")`, the logger from `factory.CreateLogger(category)`.  A member's value is compared as `LogValue`: `new LogMember("eventId", 2001L)`.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.NpgsqlRunLoggerTests"`
Expected: build fails, `NpgsqlRunLogger` and `Writes` do not exist.

- [ ] **Step 3: Add `bool Writes { get; }` to `IRunLog`** and to its four implementations as the first reading above says.  Its documentation: whether the log keeps what it is given, so that a step whose events cost something to make can skip them.

- [ ] **Step 4: Implement `NpgsqlRunLogger`**

`CreateLogger(string categoryName)` gives a logger of the private nested class whose minimum is `LogLevel.Debug` for the two categories and `LogLevel.Warning` otherwise, compared ordinally.  `IsEnabled(level)` is `level != LogLevel.None && level >= minimum`.  `Log` returns when not enabled, else writes `EventName` with the five members.  `AddProvider` does nothing; `Dispose` does nothing, since the log is the run's.  The class's remarks say why `Npgsql.Connection` is kept at Warning and loses its exception, and point at `TD-0048` and `TD-0034`.

- [ ] **Step 5: Wire the connection**

`PostgresConnection(string connectionString, IRunLog log)`.  In `OpenAsync`, inside the existing `try`, the data source is built through `new NpgsqlDataSourceBuilder(connectionString)`, with `UseLoggerFactory(factory)` when `NpgsqlRunLogger.For(log)` gives one, then `Build()`.  The builder's constructor reads the string, so its `ArgumentException` stays inside the `catch` that gives `UnreadableConnectionString`.  The `<param name="log">` says that it is the describer's log and is given to the driver alone.  `PostgresSession.OpenAsync` passes `log`; `ExplainTests` passes `NullRunLog.Instance`.

- [ ] **Step 6: Run the tests**

Run: the filter of Step 2, then `dotnet test --project tests/SqlSource.Tool.Tests` and `dotnet test --project tests/SqlSource.Tests.Postgres`.
Expected: all pass.

- [ ] **Step 7: Write `TD-0048`** in the form of its neighbours: the levels at which the categories are forwarded trust Npgsql's messages at those levels not to name the host, the user or the database; the container test of Task 4 holds it for the Npgsql the tool pins, 10.0.3; what to do when Npgsql is updated.  Add its row to `docs/tech-debt/README.md`.  In `src/SqlSource.Tool/AGENTS.md`, name `NpgsqlRunLogger` among the folder's files and `postgres.driver` among the describer's events.

- [ ] **Step 8: Validate and commit**

```bash
./format.sh && ./pre-commit-validation.sh
```

```bash
git add -A src tests docs/tech-debt
git commit -m "Forward Npgsql's own log into the run's log" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Registration

**Files:**
- Modify: `src/SqlSource.Tool/ToolHost.cs`, `src/SqlSource.Tool/Describing/Describers/DescriberRegistry.cs`, `src/SqlSource.Tool/Describing/Postgres/PostgresDescriber.cs`
- Modify: `tests/SqlSource.Tool.Tests/ToolHostTests.cs`
- Modify: `docs/diagnostics.md` (`SQLSRC216`), `src/SqlSource.Tool/AGENTS.md` where it says the describer is registered nowhere

**Interfaces:**
- Consumes: `PostgresDescriber()`; `DescriberRegistry.Find(SqlDialect)`.

- [ ] **Step 1: Change the test.**  In `Create_RealHost_IsTheConsoleTheCurrentDirectoryAndTheEnvironment`, the line on `Describers` becomes:

```csharp
_ = host.Describers.Find(SqlDialect.PostgreSql).ShouldBeOfType<PostgresDescriber>();
host.Describers.Find(SqlDialect.SqlServer).ShouldBeNull();
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.ToolHostTests"`
Expected: FAIL, `Find` gives null for PostgreSQL.

- [ ] **Step 3: Register.**  `ToolHost.Create` gives `new DescriberRegistry([new PostgresDescriber()])`.  Its summary, `DescriberRegistry`'s and `PostgresDescriber`'s stop saying that nothing is registered: the real host has PostgreSQL's describer, and SQL Server's comes in phase 6.

- [ ] **Step 4: Run the tool's tests and the container tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests` and `dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: all pass; no test of the tool runs with `ToolHost.Create` but `ToolHostTests`.

- [ ] **Step 5: Documents.**  `docs/diagnostics.md`, `SQLSRC216`: `postgres` is described from this version and `mssql` is not yet.  The example becomes `/work/App/Queries/Users.sql(1,10): error SQLSRC216: This version of sqlsource cannot describe 'mssql'` (check the message against the descriptor), and "this version describes none" becomes "this version describes `postgres` alone".  `src/SqlSource.Tool/AGENTS.md`: every sentence that says the describer is registered nowhere, or that the released tool says `SQLSRC216` for `postgres`.

- [ ] **Step 6: Validate and commit**

```bash
./format.sh && ./pre-commit-validation.sh
```

```bash
git add -A src tests docs/diagnostics.md
git commit -m "Register the PostgreSQL describer in the real host" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The command, proven against the container

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Programs.cs`, `tests/SqlSource.Tool.Tests/Commands/TerminationTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`
- Create: `tests/SqlSource.Tests.Postgres/Describer/DescribeCommandTests.cs`, `tests/SqlSource.Tests.Postgres/Describer/ToolProcessTests.cs`
- Modify: `CONTRIBUTING.md:41` (what the project compiles), `tests/AGENTS.md` if it lists the shared files

**Interfaces:**
- Produces: `Programs.ToolPath` (`string`, `SqlSource.Tool.dll` in `AppContext.BaseDirectory`) and `Programs.StartTool(string workingDirectory, string temp, params string[] arguments)` returning the started `Process`, with standard output and error redirected and standard input closed; `Programs.SetToolEnvironment(ProcessStartInfo start, string temp)`.
- Consumes: `CliRun`, `Plans.Postgres`, `TestProject.AddSql`, `SidecarFiles.Read`, `LogReader.Read`, `FixtureCases.Of(string, string)`, `RepositoryPaths.DescriberFixtures`, `PostgresContainer.ConnectionString`.

- [ ] **Step 1: Move the start of the tool.**  `ToolPath`, `StartTool` and `SetEnvironment` leave `TerminationTests` for `Programs`, unchanged but for the working directory, which becomes a parameter; `TerminationTests` passes `_fixtures.Root` and its Windows test calls `Programs.SetToolEnvironment`.  Run `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Commands.TerminationTests"`: PASS.

- [ ] **Step 2: Link the harness.**  In the container project's file, beside the existing links and with a comment that says what they are for and that `tests/SqlSource.Tool.Tests` owns them: the files of the reading on the linked set, as `<Compile Include="../SqlSource.Tool.Tests/X.cs" Link="Tool/X.cs" />`, and `Fixtures/FixtureProjects.cs` with the link `Fixtures/FixtureProjects.cs`.  For the subprocess test, the fixture project and the package's MSBuild files go to the output where `FixtureProjects` reads them:

```xml
<None
    Include="../SqlSource.Tool.Tests/Fixtures/Projects/Single/**"
    Link="Fixtures/Projects/Single/%(RecursiveDir)%(Filename)%(Extension)"
    CopyToOutputDirectory="PreserveNewest"
/>
<None Include="../../src/SqlSource/build/SqlSource.props" Link="Fixtures/build/SqlSource.props" CopyToOutputDirectory="PreserveNewest" />
<None Include="../../src/SqlSource/build/SqlSource.targets" Link="Fixtures/build/SqlSource.targets" CopyToOutputDirectory="PreserveNewest" />
<None Include="../../global.json" Link="Fixtures/global.json" CopyToOutputDirectory="PreserveNewest" />
```

Build: `dotnet build tests/SqlSource.Tests.Postgres`.  Expected: success, and `Fixtures/Projects/Single/App.csproj` in the output.

- [ ] **Step 3: Write `DescribeCommandTests`**, `[Collection(UsesPostgres.Name)]`, `(PostgresContainer postgres)`, `IDisposable` over a `CliRun`.  Every run gives `--connection app=<postgres.ConnectionString>` unless the test says otherwise; every `.sql` file is added with `("SqlSourceDatabase", "app")` to a project from `Plans.Postgres(run.Folder, directory: "")` answered by `run.Processes`.

The cases are every case of `FixtureCases.Of("postgres", RepositoryPaths.DescriberFixtures)` that has no `explain-settings.txt`, each case's `query.sql` written as `<case>.sql`.

| Test | Asserts |
|----|----|
| `Describe_FixtureQueries_WritesTheFixturesEntries` | Exit `0`; `run.Out` is `app (postgres): <n> described, 0 skipped, 0 failed\n`; for each case, `SidecarFiles.Read` of its file has one entry, equal to the one entry of `SidecarReader.Read(case.Expected)` after `entry with { NameSpan = default, ServerVersion = "*", Database = "*" }` on both |
| `Describe_Again_SkipsEveryQueryAndWritesNoFile` | After a first run, each sidecar's `File.SetLastWriteTimeUtc(path, SidecarFiles.Old)`; the second run: exit `0`, `0 described, <n> skipped`, every time still `SidecarFiles.Old` |
| `Check_AfterDescribe_IsTheSame` | Exit `0`; `app (postgres): <n> same, 0 differ, 0 failed\n` |
| `Check_ColumnAddedAfterDescribe_DiffersUntilDescribedWithForce` | `CREATE TABLE command_widgets (id integer PRIMARY KEY, name text NOT NULL)`; one file `widgets.sql`, `-- name: GetWidgets\nSELECT * FROM command_widgets;\n`; `describe` exit `0`; `ALTER TABLE command_widgets ADD COLUMN note text`; `--check`: exit `2`, the error holds `SQLSRC219` and `GetWidgets`; `describe --force`: exit `0`, the entry's columns are `id`, `name`, `note`; `DROP TABLE IF EXISTS command_widgets` in a `finally` |
| `Describe_RejectedQueryBesideAnAcceptedOne_IsSqlsrc225AndSqlsrc217` | One file, `-- name: Broken\nSELECT no_such_column FROM users;\n\n-- name: Fine\nSELECT id FROM users;\n`: exit `1`; the error holds `SQLSRC225` on the line of `Broken` and `SQLSRC217` for the file; no sidecar |
| `Describe_WithLog_HoldsDriverEventsAndNoConnectionWord` | The role, the database and the application of the reading; one file, `-- name: One\nSELECT 1 AS one;\n`; the connection is `postgres.ConnectionString` through `NpgsqlConnectionStringBuilder` with `Username = "sqlsrc_wombat"`, `Password = Secrets.Marker`, `Database = "sqlsrc_numbat"`, `ApplicationName = "sqlsrc_quokka"`; `--log` a file in `run.Folder`: exit `0`; `LogReader.Read` has at least one event named `postgres.driver` with `category` `Npgsql.Command`; the file's text holds none of `sqlsrc_wombat`, `sqlsrc_numbat`, `sqlsrc_quokka`, the builder's `Host`, and `Secrets.Marker`; `run.Out` and `run.Error` hold no `Secrets.Marker` |
| `Describe_ConnectionRefusedWithLog_IsSqlsrc224AndNoDriverEventHoldsTheHost` | The connection is the builder's `Host` with `Port = 1`; `--log`: exit `1`; the error holds `SQLSRC224`; the log has a `diagnostic` event with `id` `SQLSRC224` and a non-empty `lines`; no `postgres.driver` event's text holds the host |

The role and the database are made on a connection of the test's own to `postgres.ConnectionString`, after a look in `pg_roles` and `pg_database`, since `CREATE ROLE` and `CREATE DATABASE` have no `IF NOT EXISTS`.  `CREATE DATABASE` runs outside a transaction.

- [ ] **Step 4: Run them**

Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.DescribeCommandTests"`
Expected: PASS.  A failure of the fixture comparison names the case: compare with `FixtureCasesTests` before changing anything.  A host found in a driver event is the first item of Review Focus: stop and tell the user.

- [ ] **Step 5: Write `ToolProcessTests`**, `[Collection(UsesPostgres.Name)]`, one test:

`Describe_BuiltToolAgainstTheContainer_WritesTheSidecar`: copy `Single` with `FixtureProjects`; write `Single/Queries/Queries.cs` with `Plans.Type`; start the tool with `Programs.StartTool(fixtures.Root, temp, "describe", project, "--connection", "billing=" + postgres.ConnectionString)`; read both streams and wait for exit.  Asserts: exit `0`; standard error empty; standard output `billing (postgres): 1 described, 0 skipped, 0 failed\n`, with the line ending of the platform; `Single/Queries/Users.sql.json` exists and `SidecarReader` reads one entry named `GetUser` with `engine` `postgres`.  Kill the process tree in a `finally` when it has not exited.

- [ ] **Step 6: Run it**

Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.ToolProcessTests"`
Expected: PASS.

- [ ] **Step 7: Documents.**  `CONTRIBUTING.md`, the row of `tests/SqlSource.Tests.Postgres`: what it compiles now, the command harness of the tool's tests and the fixture project `Single` among it.  `tests/AGENTS.md`: the files of `tests/SqlSource.Tool.Tests` that the container project links, so that a change to one is built in both.

- [ ] **Step 8: Validate and commit**

```bash
./format.sh && ./pre-commit-validation.sh
```

```bash
git add -A tests CONTRIBUTING.md
git commit -m "Prove the describe command against the container" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The tool's readme

**Files:**
- Modify: `src/SqlSource.Tool/README.md`

The readme is the package's readme on nuget.org: every link is absolute.

- [ ] **Step 1: Remove** the paragraph that says this version has no describer, and the sentence of the first example that implies it.

- [ ] **Step 2: Add a section `## PostgreSQL`** after the paragraph on `--check` and the exit codes, holding, each in a short paragraph or table in the readme's voice:
  - The connection string is Npgsql's, with a link to its documentation of the keywords.
  - What is described and how: a prepared statement per query, its parameters' types from the server, its columns from a description of the statement, the catalog for origins, identity, computed columns and nullability, and the plan for a query with an outer join or a grouping set.
  - `-- param: @name type`: when the server cannot type a parameter, `SQLSRC226`, and the optional filter `(@name IS NULL OR name = @name)`, `SQLSRC227`, with the declaration or the reversed order as the fix.
  - The override suffixes, as `docs/diagnostics.md` and the sidecar format name them.
  - What `nullable` can say and what it cannot: `true` and `false` from the catalog or the walk; unknown, `null`, for a column of a view, of a foreign table, of a grouping set, and of a subquery the planner keeps; `plan: unavailable` means the walk had no plan it could use and the column took the heuristic's answer.
  - The tool prepares and explains, executes nothing, and leaves the session as it found it: every prepared statement is freed, every setting dies with its transaction; with the exception of an immutable function that the planner evaluates, which `TD-0047` records, said in one sentence.
  - The server versions supported: PostgreSQL 14 and later, tested on 14 and 18.

Read each statement against the code and `docs/diagnostics.md` before writing it; the readme says nothing the tool does not do.

- [ ] **Step 3: Add a sentence to the paragraph on `--log`**: with a log, the driver's own messages are written too, the SQL it sends and its transactions, and never its connection messages, which name the host.

- [ ] **Step 4: Validate and commit**

```bash
./format.sh && ./pre-commit-validation.sh
```

```bash
git add src/SqlSource.Tool/README.md
git commit -m "Say in the tool's readme what the PostgreSQL describer does" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: The close of phase 3, and the pull request

**Files:**
- Modify: the epic outline, `src/SqlSource.Tool/AGENTS.md`, `CONTRIBUTING.md`, `docs/deferred/`, `docs/tech-debt/` items `TD-0043` to `TD-0048`

- [ ] **Step 1: The outline.**  The row of 3.6 says `Done`; every other row of phase 3 already does.  Phase 3's `### Settled` gains bullets for this sub-phase: `IRunLog.Writes`, the event written through the describer's log, `NameSpan` in the comparison, and no `AnalyzerReleases` in the container project.  The table of ids gains no row: 3.6 adds none, said in one line under it.  Read the bullets on nullability and on the describer's route against the code as it ended up, and correct what is not so.

- [ ] **Step 2: `src/SqlSource.Tool/AGENTS.md`.**  Read it whole against the code and correct every file, type, event and rule that is no longer so.  It is past the 250 lines that the root `AGENTS.md` names as ideal; say in the pull request how long it is and leave a split to a maintainability review.

- [ ] **Step 3: `CONTRIBUTING.md`.**  What the container project proves now: the describer, the fixture cases, and the whole `describe` command in process and as the built tool.

- [ ] **Step 4: `docs/deferred`.**  Compare phase 3's scope in the outline, item by item, with what is in the code.  Each part not delivered gets an item, `D-0002` onward, in the form of `D-0001` and with a row in `docs/deferred/README.md`.  If every part is delivered, say so in the pull request and add none.

- [ ] **Step 5: `docs/tech-debt`.**  Read `TD-0043` to `TD-0048` against the code: a step that is done is removed, a file or name that moved is corrected, an item that is wholly resolved is deleted with its row.

- [ ] **Step 6: The version.**  `git fetch --tags origin`, then `git tag --list 'v*' --sort=-v:refname | head -n 1`.  If a tag exists and `VersionPrefix` in `Directory.Build.props` is not greater, ask the user for the version and set it in this commit.

- [ ] **Step 7: Validate and commit**

```bash
./format.sh && ./pre-commit-validation.sh
```

```bash
git add -A docs src/SqlSource.Tool/AGENTS.md CONTRIBUTING.md Directory.Build.props
git commit -m "Close phase 3" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 8: Push and open the pull request** against `main`, after `./pre-commit-validation.sh` passed on the tree as pushed.  The description says what each commit does, the readings of this plan, and the length of `src/SqlSource.Tool/AGENTS.md`, and ends with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.  Turn on Auto-fix for it.
