# Query generation, phase 6.1: the SQL Server session and parameters - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A SQL Server describer over Microsoft.Data.SqlClient that opens a session and types every parameter of a query, with the container test project, pinned images, CI's pull step and floor run, two-engine fixture recording, and the first SQL Server fixtures.  The describer is registered nowhere.

**Architecture:** `src/SqlSource.Tool/Describing/SqlServer/` mirrors `Describing/Postgres/`: a describer, a session that asks through the exchange, a connection holder, the `live` functions with every text sent, plain call records, and builders of types, errors and events.  Parameters come from `sp_describe_undeclared_parameters`; a reused parameter is renamed into copies and unified, with an `xml`/`int` probe where copies disagree; a declared type is resolved by describing `SELECT @p AS [1]` under its declaration.  `tests/SqlSource.Tests.SqlServer` mirrors `tests/SqlSource.Tests.Postgres` on SQL Server 2025 and 2017.

**Tech Stack:** C# (`net8.0` tool, `net10.0` tests), Microsoft.Data.SqlClient 7.1.1, Testcontainers.MsSql 4.16.0, xunit v3, Shouldly, bash, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-10-20-query-generation-phase-6-1-session-and-parameters-design.md`.  Background: the spike, `docs/superpowers/specs/2026-10-20-query-generation-phase-6-sql-server-spike.md`, and the epic's "Phase 6 - SQL Server".

## Global Constraints

- Root `AGENTS.md` rules win over any skill.  Read the `AGENTS.md` of every folder you touch first (`src/SqlSource.Tool/AGENTS.md` is long and binding for the describer).
- Every commit passes `./pre-commit-validation.sh` (Docker running) on the exact tree committed.  Run `./format.sh` (it rewrites) before it.  The run takes many minutes: run it with a 10-minute Bash timeout, or in the background, and read its summary.
- Default image: `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04`, build `17.0.5015.4`.  Floor image: `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`, build `14.0.3560.2`.  Both measured on 2026-10-21; both report collation `SQL_Latin1_General_CP1_CI_AS` and edition 3.  No test, script or workflow names `-latest` or a bare year.
- Packages: `Microsoft.Data.SqlClient` `7.1.1`, `Testcontainers.MsSql` `4.16.0` (the version of `Testcontainers.PostgreSql`), pinned in `Directory.Packages.props` with a one-line comment each, as the neighbours have.
- Nothing a `.sql` file holds may make the describer run a statement: the user's text and a declaration reach the server only as parameter values of `sys.sp_describe_undeclared_parameters` or `sys.dm_exec_describe_first_result_set`, never concatenated into a batch the tool sends.
- Every type in the sidecar is one the server gave.  A copy is ignored only on the server's evidence (the probe).
- `bindName` is never written for SQL Server.
- The describer is not registered in `ToolHost.Create` (sub-phase 6.3 does that).
- New diagnostic id: `SQLSRC262` only.  Sub-phase 6.4 owns `SQLSRC267`-`SQLSRC269` and edits the same descriptor, `AnalyzerReleases.Unshipped.md` and `docs/manual/diagnostics.md` files beside us: touch only our ids' lines and sections.  Do not edit `docs/manual/sqlserver.md` or `docs/manual/README.md`.
- No `SuppressMessage`; an `Outcome` of a disposable is made with its constructor.  Test names are `Method_Scenario_Outcome`, assertions Shouldly's, test classes `sealed`.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- The PR stays under 150 files (this plan's estimate: about 95).

## Where the code differs from the spec, and the calls this plan makes

- **`declared` is asked before `parameters`, one call for each declared parameter** (the spec's "left to the builder" allows one each).  A 2715 cannot be tied to a declaration by its number in one call, and asking first means `parameters` never sees a declaration the server cannot read.  So a 2715 or 2724 from `parameters` is the query's own text (a `CAST(@p AS dbo.Nope)`), and is `SQLSRC225`, not `SQLSRC228`.
- **`@p IS NULL OR @p IS NOT NULL`**: the spec's test table says `SQLSRC226`, but its algorithm (the probe runs only when copies disagree) gives `int`, since both copies are typed `int`, as a parameter used once in `@p IS NULL` is.  The plan follows the algorithm; `ParameterTests` pins what the server gives, and the PR says so.
- **`SQLSRC226` must name a parameter, and the server's error is mapped by number alone.**  The parameter named is the first of the query's undeclared parameters, copies taken back, whose `@name` stands in one of the error's messages as a whole token, ignoring case; when none does, the first undeclared parameter of the list.  The number decides the id; the message only picks which name to print.
- **`tools/check-tool-install.sh`**: nothing in the tool loads SqlClient until 6.3 registers the describer.  The host refuses to start when an assembly of the tool's `deps.json` is missing, so the install-and-run already proves SqlClient's closure is there, as it does Npgsql's; the script's comment says so and nothing else changes.
- **The manual note beside the optional filter** (spec, "A parameter used once ..."): the spec's Documentation section says no user page changes in 6.1.  The note goes with the page that documents describing SQL Server (6.3 or 6.7); the implementation manager's notes say so.
- **`SQLSRC231`'s title** becomes "Parameters are read differently" (a title has no placeholder).

## Review Focus

1. A text with a `DECLARE`d variable that shares a copy's would-be name (`DECLARE @name_1 int; ... @name ... @name`): the copy name skips it, case-insensitively.  Test in Task 5.
2. A cancelled run: SqlClient reports a cancel as a `SqlException`, which must leave a `live` function as an `OperationCanceledException`, never as an answer's error.  Test in Task 4 (a token cancelled before the call).
3. A connection string SqlClient cannot read (`Data Source=;Foo=bar`) and one with a wrong password: `SQLSRC224`, and neither the value nor the password in any printed text, log or capture.  Tests in Task 4.
4. A declared type that is a statement (`int; DROP TABLE dbo.victim`, `int) --`): refused before anything is sent, and the canary table survives every path that sends the user's text.  Tests in Tasks 5 and 6.
5. A query whose parameters only the `-- param:` markers name, and a token with a default: described with the declared type and no `parameters` row for it.  Fixture `delete-declared-only` in Task 8.

---

### Task 1: The plan and the outline's row

**Files:**
- Create: `docs/superpowers/plans/2026-10-21-query-generation-phase-6-1-session-and-parameters.md` (this file)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row `6.1`: `Not started` → `In progress`)

- [ ] **Step 1:** Change the row's status.
- [ ] **Step 2:** `./pre-commit-validation.sh` passes.
- [ ] **Step 3:** Commit `Plan phase 6.1 and mark it in progress`.

### Task 2: The test project and its images

**Files:**
- Modify: `tests/SqlSource.Testing/DefaultImages.cs` (add `SqlServer` beside `Postgres`)
- Modify: `Directory.Packages.props` (two `PackageVersion`s)
- Modify: `SqlSource.slnx`; `src/SqlSource/SqlSource.csproj`, `src/SqlSource.Tool/SqlSource.Tool.csproj`, `tests/SqlSource.Testing/SqlSource.Testing.csproj`, `tests/SqlSource.Tool.Testing/SqlSource.Tool.Testing.csproj` (`InternalsVisibleTo Include="SqlSource.Tests.SqlServer"` beside each `SqlSource.Tests.Postgres`)
- Create: `tests/SqlSource.Tests.SqlServer/SqlSource.Tests.SqlServer.csproj` (+ its `packages.lock.json` from restore)
- Create: `tests/SqlSource.Tests.SqlServer/SqlServerContainer.cs`, `UsesSqlServer.cs`, `SqlServerScripts.cs`, `RepositoryPaths.cs`, `Recording.cs`, `SqlServerContainerTests.cs`, `SqlServerScriptsTests.cs`, `README.md`
- Create: `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/schema.sql`
- Modify: root `AGENTS.md` (rule 5), `tests/AGENTS.md`, `tests/README.md`, `CONTRIBUTING.md`

**Interfaces:**
- Produces: `DefaultImages.SqlServer` (`public const string`, one line of the shape `public const string Postgres = "...";`, which scripts read with `sed -n 's/.*const string SqlServer = "\(.*\)";.*/\1/p'`).
- Produces, in namespace `SqlSource.Tests.SqlServer`:
  - `public sealed class SqlServerContainer : IAsyncLifetime` with `const string ImageVariable = "SQLSOURCE_TESTS_SQLSERVER_IMAGE"`, `const string Database = "fixtures"`, `static string Image`, `static string TagOf(string image)` (after the last `:`), `static IReadOnlyDictionary<string, string> ImageBuilds` (image → `ProductVersion`, the two images above), `static IReadOnlyList<int> RecordedMajors = [14, 17]`, `int Major`, `string ConnectionString` (database `fixtures`), `string ConnectionStringOf(string database)`, `Task ExecuteAsync(string sql)`, `Task<object?> ScalarAsync(string sql)`, `void SkipBelow(int major, string reason)` and its static twin, as `PostgresContainer` has them.
  - `[CollectionDefinition(UsesSqlServer.Name)] public sealed class UsesSqlServer : ICollectionFixture<SqlServerContainer>` with `Name = "sqlserver"`.
  - `internal static class SqlServerScripts` with `IReadOnlyList<string> Batches(string script)` and `Task RunAsync(SqlConnection connection, string path, CancellationToken token)`.
  - `internal static class RepositoryPaths` with `DescriberFixtures` and `SqlServerSchema` (`.../Describers/mssql/schema.sql`), built on `RepositoryRoot.Folder`.
  - `internal static class Recording` with `bool IsAsked(string variable)` (as the PostgreSQL project's; a test project compiles no other project's file).

- [ ] **Step 1: Write `SqlServerScriptsTests`** (no server):
  - `Batches_LineOfGoAlone_SplitsThere`: `"CREATE SCHEMA b;\nGO\nCREATE TABLE b.t (id int);\n"` gives `["CREATE SCHEMA b;\n", "CREATE TABLE b.t (id int);\n"]` (no empty batch kept).
  - `Batches_GoInAnyCaseWithSpaces_SplitsThere`: `"  go  "` and `"Go"` lines split.
  - `Batches_GoInsideALine_DoesNotSplit`: `"SELECT 1 AS go\n"` is one batch.
  - `Batches_GoWithACount_Throws`: `"SELECT 1\nGO 5\n"` throws `InvalidOperationException` whose message says a count is not supported.
- [ ] **Step 2:** Run them (`dotnet test --project tests/SqlSource.Tests.SqlServer -- --filter-class SqlSource.Tests.SqlServer.SqlServerScriptsTests`); they fail to build.
- [ ] **Step 3: Create the project** as `tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj` is, without the end-to-end parts (no `SqlSource.props`/`.targets` import, no dialect or output properties, no fixture project copies, no Dapper/Npgsql): `net10.0`, `Exe`, not packable, references `GitHubActionsTestLogger`, `Microsoft.CodeAnalysis.CSharp` `VersionOverride="5.9.0"` (same comment), `Microsoft.Testing.Extensions.CodeCoverage`, `Microsoft.Data.SqlClient`, `Shouldly`, `Testcontainers.MsSql`, `xunit.v3`; project references to `src/SqlSource` (as Analyzer + assembly, as Postgres's), `SqlSource.Testing`, `SqlSource.Tool.Testing`.  Add it to the solution's `/tests/` folder.  Restore without locked mode (`RestoreLockedMode=false dotnet restore SqlSource.slnx --force-evaluate`) so every lock file that the new packages reach is rewritten; commit each.
- [ ] **Step 4: Implement `SqlServerScripts`**: a batch ends at a line whose text, trimmed, equals `GO` ignoring case; a line that is `GO` followed by white space and digits throws.  `RunAsync` runs each batch as one `SqlCommand` (text), which has `QUOTED_IDENTIFIER` on.  Steps 1-2 now pass.
- [ ] **Step 5: Write `schema.sql`** from the spike's schema (`dbo.Email`, `dbo.IdList`, schema `billing`, `dbo.users` with `credit decimal(10,2) NOT NULL DEFAULT 0` added, `dbo.orders`, `billing.invoices`, `dbo.victim`, `dbo.trig` with its `AFTER INSERT` trigger, `dbo.user_orders`, `dbo.prices` system-versioned), plus `dbo.all_types` with one `NOT NULL` column per row of the spike's type table except `json` and `vector` (columns named `<type>_value`, `id int PRIMARY KEY` first), with `GO` between every statement that must start a batch (`CREATE SCHEMA`, `CREATE TRIGGER`, `CREATE VIEW`).  A header comment says what it is for and that `tests/SqlSource.Tests.SqlServer` applies it.
- [ ] **Step 6: Write `SqlServerContainerTests`** (`[Collection(UsesSqlServer.Name)]` except the static ones):
  - `ConnectionString_OfTheContainer_OpensTheFixtureDatabase`: `SELECT DB_NAME(), OBJECT_ID('billing.invoices'), OBJECT_ID('dbo.user_orders')` gives `fixtures` and two non-nulls.
  - `Image_OfTheRun_NamesItsCumulativeUpdate`: `TagOf(Image)` matches `^\d{4}-CU\d+(-GDR\d+)?-ubuntu-\d+\.\d+$`.
  - `Image_OfTheRun_IsInTheMapOfBuilds`: `ImageBuilds.ShouldContainKey(Image)`.
  - `Image_OfTheRun_IsTheVariablesValueOrTheDefault` (as PostgreSQL's).
  - `Server_OfTheImage_IsTheBuildTheMapRecords`: `SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))` equals `ImageBuilds[Image]`.
  - `ImageBuilds_EveryImage_IsPinnedAndNotLatest`: each key's tag matches the pattern above.
  - `SkipBelow_*` as PostgreSQL's three.
- [ ] **Step 7: Implement `SqlServerContainer`**: `new MsSqlBuilder(Image).Build()`; `InitializeAsync` starts it, runs `CREATE DATABASE fixtures` on `master`, then `SqlServerScripts.RunAsync` with `RepositoryPaths.SqlServerSchema` on `fixtures`, reads `Major` from `SERVERPROPERTY('ProductMajorVersion')`, and disposes the container itself when anything after the start throws (as PostgreSQL's does).  `ConnectionStringOf` sets `InitialCatalog` through `SqlConnectionStringBuilder` on the container's string (which already trusts the server's certificate).  Add `DefaultImages.SqlServer` with a comment: the scripts read the line by its shape; the image names its cumulative update; `SqlServerContainer.ImageBuilds` records its build; see `tests/SqlSource.Tests.SqlServer/README.md`.
- [ ] **Step 8:** Run the project's tests; they pass (the first start under emulation takes minutes).
- [ ] **Step 9: Documents.**  `tests/SqlSource.Tests.SqlServer/README.md`: running the project and its floor run (`SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/SqlSource.Tests.SqlServer`), the two images and their pinning rule (tag form, `ImageBuilds`, moving a tag records again), the fixtures (filled in by Task 8), that the images are x64 only and run under emulation on Apple silicon, minutes the first time while the image is pulled.  Root `AGENTS.md` rule 5 becomes both engines' (spec, "The images": a PostgreSQL image names a major and a minor; a SQL Server image names its update; moving a default image records again what was recorded on it, `tools/record-fixtures.sh <engine>`; moving a floor image changes the places that name it, which for SQL Server are `.github/workflows/build.yml`, `CONTRIBUTING.md` and `tests/SqlSource.Tests.SqlServer/README.md`, and runs the tests on it).  `tests/AGENTS.md`: six xunit projects; a bullet for `tests/SqlSource.Tests.SqlServer` (collection `UsesSqlServer`, the one place naming the image is `DefaultImages.SqlServer`, database `fixtures`, scripts applied through `SqlServerScripts`, tests that need no server are not in the collection, a change is run on both images before it is pushed).  `tests/README.md`: the project.  `CONTRIBUTING.md`: the third container project, its images and variable, emulation and the first pull's minutes.
- [ ] **Step 10:** `./format.sh`, then `./pre-commit-validation.sh` passes.  Commit `Add the SQL Server container test project`.

### Task 3: CI pulls the images and runs the floors

**Files:**
- Create: `tools/pull-test-images.sh`
- Modify: `.github/workflows/build.yml`, `.github/workflows/nightly.yml` (comment), `.github/workflows/README.md`, `tools/README.md`, `tools/AGENTS.md`, `CONTRIBUTING.md`

- [ ] **Step 1: Write `tools/pull-test-images.sh`**: usage `pull-test-images.sh [image ...]`; reads `DefaultImages.Postgres` and `DefaultImages.SqlServer` from `tests/SqlSource.Testing/DefaultImages.cs` with `sed` by their line shape, failing with a message when either is missing; then for the two defaults and every argument, `docker pull` once and print `<image>: <n> s`.  Header comment: it is a step, not a check.  No `declare -A` (macOS bash 3.2).
- [ ] **Step 2:** Run it locally with the PostgreSQL floor and the SQL Server floor; it prints four times.  `tools/shellcheck.sh` and `tools/shfmt.sh --check` pass.
- [ ] **Step 3: Edit `build.yml`'s `build` job**: job `env` gains `POSTGRES_FLOOR_IMAGE: postgres:14.24` and `SQLSERVER_FLOOR_IMAGE: mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`; a step "Pull test images" (`if: ${{ !cancelled() && steps.build.outcome == 'success' }}`) runs `tools/pull-test-images.sh "$POSTGRES_FLOOR_IMAGE" "$SQLSERVER_FLOOR_IMAGE"` before "Test"; "Test on PostgreSQL 14" reads `SQLSOURCE_TESTS_POSTGRES_IMAGE: ${{ env.POSTGRES_FLOOR_IMAGE }}`; a new "Test on SQL Server 2017" after it runs `dotnet test --project tests/SqlSource.Tests.SqlServer --configuration "$CONFIGURATION" --no-build --report-github` with `SQLSOURCE_TESTS_SQLSERVER_IMAGE: ${{ env.SQLSERVER_FLOOR_IMAGE }}`.  Raise `timeout-minutes` to 25 for now; Task 10 sets it from the PR's measured run.  `tools/actionlint.sh` passes.
- [ ] **Step 4: Documents.**  `nightly.yml`'s comment and `.github/workflows/README.md` name both container projects and the pull step.  `tools/README.md` and `tools/AGENTS.md` list the script.  `CONTRIBUTING.md`: the pull step of CI.
- [ ] **Step 5:** `./pre-commit-validation.sh` passes.  Commit `Pull the test images in a step of their own and run SQL Server 2017`.

### Task 4: The describer opens a session

**Files:**
- Modify: `src/SqlSource.Tool/SqlSource.Tool.csproj` (`PackageReference Include="Microsoft.Data.SqlClient"` with a comment naming `Describing/SqlServer/`), lock files
- Modify: `tools/check-package.sh` (`TOOL_REQUIRED` gains SqlClient's assemblies as the packed tool lays them out: list `tools/net8.0/any/Microsoft.Data.SqlClient.dll` and the `runtimes/unix/...` and `runtimes/win/...` copies that `unzip -l` of the packed tool shows, and the header comment), `tools/check-tool-install.sh` (comment only, as above)
- Create in `src/SqlSource.Tool/Describing/SqlServer/`: `SqlServerDescriber.cs`, `SqlServerSession.cs`, `SqlServerSession.Calls.cs`, `SqlServerConnection.cs`, `SqlServerLive.cs`, `SqlServerCalls.cs`, `SqlServerCallNames.cs`, `ConnectionErrors.cs`, `SqlServerErrors.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs` (`SQLSRC235` message), `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs` (its arguments), `docs/manual/diagnostics.md` (section of `SQLSRC235`), `tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs` (`Under` gains `"SqlSource.Tool.Describing.SqlServer"`)
- Create tests: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerDescriberTests.cs`, `ConnectionErrorsTests.cs`, `SqlServerSessionTests.cs` (replayed answers, no server); `tests/SqlSource.Tests.SqlServer/Describer/Sessions.cs`, `SessionTests.cs`
- Modify tests: `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`, and the tests that pin `SQLSRC235`'s text

**Interfaces:**
- Produces (namespace `SqlSource.Tool.Describing.SqlServer`):
  - `internal sealed class SqlServerDescriber : IQueryDescriber` (`Dialect => SqlDialect.SqlServer`; `SecretsOf` → `SqlServerConnection.SecretsOf`; `OpenAsync` → `SqlServerSession.OpenAsync`).
  - `internal sealed partial class SqlServerSession : IDescribeSession` with `const string DriverName = "Microsoft.Data.SqlClient"`, `const int MinimumMajorVersion = 14`, `static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(OpenRequest, IRunLog, CancellationToken)`, `ServerInfo Server`, `internal SqlServerConnection Connection`, `DescribeAsync` (Task 6 fills it; until then it returns a description of `ResultKind.None` with no parameters, which no test relies on).
  - `internal sealed class SqlServerConnection(string connectionString) : IAsyncDisposable` with `static IReadOnlyList<string> SecretsOf(string)`, `static string? DriverVersion` (SqlClient's `AssemblyInformationalVersionAttribute` cut before `+`), `Task<SqlCommand> CommandAsync(string text, CancellationToken)`, `Task<SqlCommand> ProcedureAsync(string name, CancellationToken)`; `ToString()` returns `SqlServerConnection` with no value.
  - `internal sealed class SqlServerLive(SqlServerConnection connection)` with `Task<SqlServerOpenAnswer> OpenAsync(CancellationToken)`.
  - `SqlServerCallNames`: `Open = "open"`, `Parameters = "parameters"`, `Probe = "probe"`, `Declared = "declared"`.
  - Records in `SqlServerCalls.cs`, each with a doc comment as `PostgresCalls.cs` has:
    - `SqlServerOpenRequest` (empty, `static Instance`)
    - `SqlServerOpenAnswer(string? ProductVersion, int? EngineEdition, string? DriverVersion, IReadOnlyList<SqlServerSetting>? Settings, SqlServerServerError? Error)`
    - `SqlServerSetting(string Name, string Value)`
    - `SqlServerServerError(IReadOnlyList<SqlServerErrorRow> Errors)`
    - `SqlServerErrorRow(int Number, int Class, int State, string Message)`
  - `ConnectionErrors` (SQL Server's): `static SqlServerServerError AtOpen(SqlException)`, `static SqlServerServerError Of(SqlException)`, `static SqlServerServerError Own(string text)` (number, class and state 0), and the fixed texts as `const string`s.
  - `SqlServerErrors`: `static DescribeFailure Open(SqlServerServerError error, string database)` (`SQLSRC224`, help `"check that the server is running, and that the connection string names it, a login it accepts and the database"`), `static DescribeFailure VersionNotSupported(string database, string version)` (`SQLSRC235`), `static EquatableArray<string> ServerLines(SqlServerServerError error)`.

- [ ] **Step 1: Write unit tests (no server).**
  - `SqlServerDescriberTests`: `Dialect_Is_SqlServer`; `SecretsOf_ValueWithAPassword_GivesThePassword` (`"Data Source=x;Password=Sesame123"` → `["Sesame123"]`); `SecretsOf_ValueWithoutAPassword_GivesNone`; `SecretsOf_ValueSqlClientCannotRead_GivesNoneAndDoesNotThrow` (`"Data Source=x;Nonsense=1"` and `"=;"`).
  - `SqlServerSessionTests` over a capture made in the test (a `ReplayingExchange` of a `DescribeCapture` holding one `open` answer): `OpenAsync_Server2017_OpensWithTheVersionTheDriverAndTheFourSettings` (`Server.Version` `"14.0.3560.2"`, `Driver` `"Microsoft.Data.SqlClient"`, settings named `compatibility_level`, `ANSI_NULLS`, `QUOTED_IDENTIFIER`, `collation` in that order); `OpenAsync_Server2016_IsSqlsrc235` (version `"13.0.6300.2"`, edition 3; arguments `[db, "SQL Server", "13.0.6300.2", "14 (SQL Server 2017)"]`, no server lines); `OpenAsync_AzureSqlDatabaseOfAnOlderVersion_Opens` (edition 5, version `"12.0.2000.8"`); `OpenAsync_AzureManagedInstance_Opens` (edition 8); `OpenAsync_ErrorAnswered_IsSqlsrc224WithTheFixedLine` (an answer whose error is `[18456, 14, 8, "the server refused the login"]`; the server line is `Msg 18456, Level 14, State 8: the server refused the login`).
  - `ConnectionErrorsTests`: the server-line format; `Own` lines print the text alone (number 0).  (`SqlException` has no public constructor, so `AtOpen` and `Of` are covered by the container tests.)
  - `SecretTests.Run_SqlServerDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue`, as PostgreSQL's beside it: an `mssql` query in the scene (add what the scene needs), the describer registered in the scene's run, `SQLSOURCE_CONNECTION_MSSQL` = `"Data Source=127.0.0.1,1;Connect Timeout=5;Encrypt=False;Password=" + Secrets.Marker`; `SQLSRC224` printed, the marker and `127.0.0.1` nowhere in standard error, the log or the capture.
- [ ] **Step 2:** Run; they fail to build.
- [ ] **Step 3: Implement.**
  - `SqlServerConnection`: reads the value with `SqlConnectionStringBuilder` when the first call needs the connection; an `ArgumentException` (or `FormatException`, `KeyNotFoundException`) there makes the call answer `ConnectionErrors.Own(UnreadableConnectionString)` = `"SqlClient cannot read the connection string"`, never the driver's text, which quotes the value.  Opens one `SqlConnection` for the session on first use; after a call found the connection broken (`State != Open` once opened), every later call answers `Own(LostConnection)` = `"the connection to the server was lost"` and nothing reopens it.  `SecretsOf` returns `Password` when not empty, `[]` on any exception of reading.  `.editorconfig`: add `SqlServerConnection.cs` to the section that turns `CA2100` off for `PostgresConnection.cs`, with the same reason.
  - `SqlServerLive.OpenAsync`: one text command (a `const string`):
    ```sql
    SELECT
        CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)),
        CAST(SERVERPROPERTY('EngineEdition') AS int),
        d.compatibility_level,
        CAST(SESSIONPROPERTY('ANSI_NULLS') AS int),
        CAST(SESSIONPROPERTY('QUOTED_IDENTIFIER') AS int),
        CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(128))
    FROM sys.databases AS d
    WHERE d.database_id = DB_ID()
    ```
    Settings: `compatibility_level` as its number, `ANSI_NULLS` and `QUOTED_IDENTIFIER` as `ON`/`OFF`, `collation` as given.  Every `live` function: `catch (SqlException) when (token.IsCancellationRequested)` rethrows `new OperationCanceledException(token)`; `catch (SqlException e)` answers `ConnectionErrors.AtOpen(e)` (open) or `ConnectionErrors.Of(e)` (later calls); anything else passes (a bug, `SQLSRC200`).
  - `ConnectionErrors.AtOpen` keeps each error's number, class and state and replaces every message with a fixed text: 18456 → `"the server refused the login"`; 4060 and 4064 → `"the server cannot open the database for the login"`; -2 or a `TimeoutException` in the chain → `"the connection to the server timed out"`; a `SocketException` in the chain → `"SqlClient could not reach the server: <SocketError>"`; any other → `"the server refused to open a session"`.  `Of`: number -2 → `"the server did not answer in time"`; any error of class 20 or above → `"the connection to the server was lost"` (fixed, numbers kept); otherwise every error verbatim, in order.
  - `SqlServerSession.OpenAsync` as `PostgresSession.OpenAsync` (connection disposed unless kept; `new Outcome<IDescribeSession, DescribeFailure>(session)`); the major is the `ProductVersion`'s first part; below 14 is `SQLSRC235` unless `EngineEdition` is 5 or 8.  `ServerInfo(ProductVersion, DriverName, DriverVersion, settings)`.
  - `SQLSRC235`'s message: `"The database '{0}' runs {1} {2}, and the tool supports {3} and later"`; `PostgresErrors.VersionNotSupported` passes `[database, "PostgreSQL", version, "14"]` so its printed text is unchanged.  Its section in `docs/manual/diagnostics.md` names SQL Server 2017 as SQL Server's floor and says an Azure SQL Database or Managed Instance is accepted whatever its version.
  - `SqlServerErrors.ServerLines`: one line per error, `Msg <number>, Level <class>, State <state>: <message>`, or the message alone for number 0.
- [ ] **Step 4: Write the container `SessionTests`** (`[Collection(UsesSqlServer.Name)]`, opening through `Sessions.OpenAsync`, a helper as PostgreSQL's `Describer/Sessions.cs` with `OpenAsync`, `OpenSessionAsync`, `OpenCapturedAsync`, `DescribeAsync`, `ShouldDescribeAsync`):
  - `OpenAsync_Container_GivesTheVersionTheDriverAndTheSettings`: `Server.Version == SqlServerContainer.ImageBuilds[Image]`, `Driver == "Microsoft.Data.SqlClient"`, `DriverVersion` starts with `"7.1.1"`, settings `ANSI_NULLS=ON`, `QUOTED_IDENTIFIER=ON`, `collation=SQL_Latin1_General_CP1_CI_AS`, `compatibility_level` 140 on 14 and 170 on 17 (branch on `Major`).
  - `OpenAsync_WrongPassword_IsSqlsrc224WithNoPartOfTheValue`, `OpenAsync_MissingDatabase_IsSqlsrc224WithNoPartOfTheValue`, `OpenAsync_UnreachableHost_IsSqlsrc224WithNoPartOfTheValue` (`Data Source=127.0.0.1,1;Connect Timeout=3;Encrypt=False`), `OpenAsync_UnreadableConnectionString_IsSqlsrc224WithTheFixedLine`: in each, no server line, argument or help contains the password, the host or the database's name from the value, and the capture (`OpenCapturedAsync`'s) neither.
  - `OpenAsync_TokenCancelled_ThrowsOperationCanceled`.
- [ ] **Step 5:** Pack and run `tools/check-package.sh` after adding SqlClient's paths; it passes.  Run every test touched; they pass.
- [ ] **Step 6:** `./format.sh`; `./pre-commit-validation.sh` passes.  Commit `Open a SQL Server session through SqlClient`.

### Task 5: Declared types and the copies of a reused parameter

**Files:**
- Create: `src/SqlSource.Tool/Describing/SqlServer/DeclaredTypeText.cs`, `ParameterCopies.cs`, `SqlServerTypes.cs`, `SqlServerSql.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/DeclaredTypeTextTests.cs`, `ParameterCopiesTests.cs`, `SqlServerTypesTests.cs`

**Interfaces:**
- Produces:
  - `SqlServerSql.Lex(string sql) → EquatableArray<SqlLexeme>` by `SqlDialectRules.SqlServer`, throwing `InvalidOperationException` as `PostgresSql.Lex` does.
  - `DeclaredTypeText.Read(string text) → Outcome<string, string>`: the type trimmed, or the reason it is refused.  Reasons (constants): `"it is not written as a type name"` followed by `": "` and what was found, e.g. `"a ; is not part of a type name"`, `"a comment is not part of a type name"`, `"a quote or a bracket is not closed"`, `"a type name has one or two parts"`, `"a type name is a name, then (max) or one or two numbers in parentheses"`, `"a type name is on one line"`.
  - `internal sealed record CopiedSql(string Sql, IReadOnlyList<ReusedParameter> Reused, IReadOnlyDictionary<string, string> ParameterOfCopy)` (copy name without `@` → the parameter's name as the list spells it; the dictionary ignores case).
  - `internal sealed record ReusedParameter(string Name, IReadOnlyList<string> Copies)` (copies in order of appearance).
  - `ParameterCopies.Rename(string sql, EquatableArray<SqlQueryParameter> parameters) → CopiedSql`.
  - `internal sealed record CopyAnswer(SqlServerType? Type, bool Ignored)`.
  - `ParameterCopies.Agreed(IReadOnlyList<SqlServerType?> types) → SqlServerType?`: every one non-null and equal, else null.
  - `ParameterCopies.Unify(IReadOnlyList<CopyAnswer> copies) → Outcome<SqlServerType, CopiesFailure>`; `internal sealed record CopiesFailure(SqlServerType? First, SqlServerType? Second)`: both null means no copy is left or a copy left is untyped (`SQLSRC226`); both set means two types (`SQLSRC262`, the first two distinct types in order of appearance).
  - `internal sealed record ListDifference(string Name, bool ReadBySqlSource)`.
  - `ParameterCopies.CompareLists(IEnumerable<string> undeclared, IEnumerable<string> reported, IReadOnlyDictionary<string, string> parameterOfCopy) → IReadOnlyList<ListDifference>`: `undeclared` are the list's names; `reported` are the server's names with their `@`; each copy is taken back to its parameter; compared ignoring case; differences in the list's order, then the server's.
  - `SqlServerTypes.FromParameter(SqlServerParameterRow row) → SqlServerType`, `SqlServerTypes.FromDeclared(SqlServerDeclaredRow row) → SqlServerType`, `SqlServerTypes.Spelled(SqlServerType type) → string` (`schema.name` of a user type that is not of `sys`, else `Name`; for messages).  Both builders throw `InvalidOperationException` for a row without a type name.  `userType` is set when the row names a user type's name.  `BindName` stays null.  (Records `SqlServerParameterRow` and `SqlServerDeclaredRow` are defined in `SqlServerCalls.cs` in this task; see Task 6.)

- [ ] **Step 1: Write `DeclaredTypeTextTests`**: accepted, each returning the text trimmed: `int`, `nvarchar(50)`, `varchar(max)`, `VARCHAR(MAX)`, `decimal(18,2)`, `decimal(18, 2)`, `dbo.Email`, `[billing].[Amount]`, `"dbo"."Email"`, `[a]]b]`, `"a""b"`, `sys.sysname`.  Refused, each with its reason: `int;`, `int; DROP TABLE x`, `int -- x`, `int /* x */`, `[dbo`, `"dbo`, `int) --`, `int foo`, `nvarchar(`, `nvarchar(max, 2)`, `a.b.c`, `int\nfoo`, `''`, `decimal(18,2)(3)`.
- [ ] **Step 2: Write `ParameterCopiesTests`**:
  - Rename: `@p` once is not renamed and nothing is reused; twice → `@p_1`, `@p_2`; three times → `_1`, `_2`, `_3`; two reused parameters each numbered from 1; `@P` and `@p` are one parameter (copies follow the first spelling of the list: `@p_1`, `@p_2`); a declared parameter used twice is not renamed; a text holding `@p_1` already (a `DECLARE @p_1 int`) gets `@p__1`, `@p__2`; a `DECLARE`d `@v` used twice, a `'@p'` string, a `-- @p` comment and an `[@p]` identifier are left as written; `ParameterOfCopy["P_2"]` is `p`.
  - Unify: all agree → that type; one ignored by both probes and the other typed → the other's type; one that binds as `xml` and not as `int` (so `Ignored` false) → kept; all ignored → failure with both null; two left that differ → failure with the first two types in order; a copy left untyped → failure with both null.  `Agreed`: all equal → the type; one null → null; two differ → null; equality includes the user type (`nvarchar(320)` with and without `dbo.Email` differ).
  - Lists: equal → none; a listed `id` the server lacks → `("id", true)`; a reported `@x` the list lacks → `("x", false)`; a reported `@p_2` with `ParameterOfCopy` mapping it to `p` matches the listed `p`; case is ignored.
- [ ] **Step 3: Write `SqlServerTypesTests`**: one case for each row of the spike's type table built from rows made by the test (`bigint` 8/19/0, `decimal(18,4)` 9/18/4, `time(7)` 5/16/7, `datetime2(0)` 6/19/0, `nvarchar(50)` 100/0/0, `varchar(max)` -1/0/0, `timestamp` 8/0/0, `uniqueidentifier`, `xml` -1, `sql_variant` 8016); `nvarchar(128)` with user type `sys.sysname`; `nvarchar(320)` with `dbo.Email` (`Spelled` → `dbo.Email`); `hierarchyid` 892 with `sys.hierarchyid` and its assembly-qualified name; a row without a name throws.
- [ ] **Step 4:** Run; they fail to build.
- [ ] **Step 5: Implement.**  `DeclaredTypeText` reads the spec's grammar by hand (no regex over the whole text): part = plain identifier (`[A-Za-z_#@][A-Za-z0-9_#@$]*`), `[...]` with `]]`, or `"..."` with `""`; one or two parts joined by `.` with no space around it; optional white space, then `(` `max` `)` (any case) or `(` digits [`,` digits] `)` with spaces allowed inside; nothing else but white space at the ends; no line break anywhere.  `ParameterCopies.Rename` lexes once, counts `Parameter` lexemes per listed undeclared name (ignoring case), picks the separator as `_` repeated until no `{name}{sep}{n}` of any copy equals, ignoring case, a name of any `Parameter` lexeme of the text or of another copy, and rewrites only those lexemes.
- [ ] **Step 6:** Run; they pass.  `./format.sh`; `./pre-commit-validation.sh` passes.  Commit `Read a declared T-SQL type and rename the copies of a reused parameter`.

### Task 6: Describing the parameters

**Files:**
- Modify: `src/SqlSource.Tool/Describing/SqlServer/SqlServerSession.cs`, `SqlServerSession.Calls.cs`, `SqlServerLive.cs`, `SqlServerCalls.cs`, `SqlServerErrors.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs` (`SQLSRC262`, `SQLSRC231`), `src/SqlSource/AnalyzerReleases.Unshipped.md`, `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs` (`SQLSRC231` arguments), `docs/manual/diagnostics.md` (`SQLSRC262`; `SQLSRC224`, `SQLSRC225`, `SQLSRC226`, `SQLSRC228`, `SQLSRC231` for SQL Server)
- Test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerErrorsTests.cs`, `SqlServerSessionTests.cs` (replayed describes); `tests/SqlSource.Tests.SqlServer/Describer/ParameterTests.cs`; `ToolDiagnosticsTests` and the tests that pin `SQLSRC231`'s text

**Interfaces:**
- Consumes: Task 4's session, connection, live, records and errors; Task 5's `DeclaredTypeText`, `ParameterCopies`, `SqlServerTypes`.
- Produces records (in `SqlServerCalls.cs`):
  - `SqlServerParametersRequest(string Sql, string? Params)`; `SqlServerParametersAnswer(IReadOnlyList<SqlServerParameterRow>? Rows, SqlServerServerError? Error)`; `SqlServerParameterRow(string Name, string? SuggestedSystemTypeName, int? SuggestedMaxLength, int? SuggestedPrecision, int? SuggestedScale, string? SuggestedUserTypeSchema, string? SuggestedUserTypeName, string? SuggestedAssemblyQualifiedTypeName)` (`Name` with its `@`, as the procedure gives it).
  - `SqlServerProbeRequest(string Sql, string Params)`; `SqlServerProbeAnswer(bool? Binds, SqlServerServerError? Error)`.
  - `SqlServerDeclaredRequest(string Sql, string Params)`; `SqlServerDeclaredAnswer(IReadOnlyList<SqlServerDeclaredRow>? Rows, IReadOnlyList<SqlServerDescribeError>? Errors, SqlServerServerError? Error)`; `SqlServerDeclaredRow(int ColumnOrdinal, string? SystemTypeName, int? MaxLength, int? Precision, int? Scale, string? UserTypeSchema, string? UserTypeName, string? AssemblyQualifiedTypeName)`; `SqlServerDescribeError(int Number, int Type, string Message)` (`error_number`, `error_type`, `error_message`).
- Produces in `SqlServerLive`: `ParametersAsync`, `ProbeAsync`, `DeclaredAsync` (each `(request, CancellationToken) → Task<answer>`), and `static string Declarations(IEnumerable<(string Name, string Type)>)` → `"@a int, @b nvarchar(50)"`.
- Produces in `SqlServerErrors`: `TypeRefused(DescribeRequest, SqlQueryParameter, string reason)`, `Declared(DescribeRequest, SqlQueryParameter, SqlServerDeclaredAnswer)`, `Parameters(DescribeRequest, SqlServerServerError, CopiedSql)`, `NoType(DescribeRequest, string parameter)`, `CopiesDisagree(DescribeRequest, string parameter, SqlServerType first, SqlServerType second)`, `ListsDiffer(DescribeRequest, IReadOnlyList<ListDifference>)`, `Failed(DescribeRequest, SqlServerServerError, DescribeStep)` (a connection error at a later call: `SQLSRC225` with its lines).

- [ ] **Step 1: Write `SqlServerErrorsTests`** (each row of the spec's error table, from errors made by the test):
  - `parameters` errors 11502, 11503, 11506, 11507 → `SQLSRC226` naming the parameter whose `@name` is in the message, help `"add a CAST around a use of the parameter, or declare its type with -- param:"`; a message naming a copy (`@p_2`) names `p`; a message naming none names the first undeclared parameter.
  - 11508 → `SQLSRC225` with the server's line; 207 then 11501 → `SQLSRC225`, message argument the first error's message, two `server:` lines in order; 11504 and 11505 → `SQLSRC225`; 2715 then 11501 at `parameters` → `SQLSRC225`.
  - `declared` errors: 2715 and 2724 → `SQLSRC228` with reason `"the database has no such type"`; 102 → `SQLSRC228` with `"it is not a type"`; step `resolve type`.
  - `CopiesDisagree` → `SQLSRC262`, arguments `[parameter, query, "int", "nvarchar(100)"]`, help `"declare the parameter's type with -- param:"`, step `describe parameters`.
  - `ListsDiffer` → `SQLSRC231`, arguments `["SQL Server", query, "SqlSource reads '@id' as a parameter and SQL Server does not; SQL Server reads '@x' as a parameter and SqlSource does not"]`, step `check parameters`, help `"a parameter of a CREATE PROCEDURE is no parameter of the query, and a DECLAREd variable is none either; rewrite the query so that both read it the same, or declare a parameter that SqlSource reads as a variable with -- param:"`.
  - A connection-lost error at `probe` → `SQLSRC225` with the fixed line.
- [ ] **Step 2: Write replayed `SqlServerSessionTests` of describes** (a capture made by the test answering each call; assert both the calls asked, in order, and the description):
  - two inferred parameters: one `parameters` call, `Params` null, types `inferred`.
  - the optional filter: `parameters` with `@name_1`, `@name_2`, rows `int` and `nvarchar(100)`; probes in order: `@name_1 xml, @name_2 nvarchar(100)` (binds), `@name_1 int, @name_2 nvarchar(100)` (binds), `@name_1 int, @name_2 xml` (does not); the type `nvarchar(100)`, `typeSource` `inferred-from-copies`.
  - copies agree: no probe.
  - a declared parameter: `declared` asked first with `SELECT @tenant AS [1]` and `@tenant int`, then `parameters` with `Params` `"@tenant int"`; type from the declared row, `declared`.
  - a declared type the grammar refuses: `SQLSRC228` and nothing asked.
  - the server reports a parameter the list lacks: `SQLSRC231`.
- [ ] **Step 3:** Run; they fail.
- [ ] **Step 4: Implement the live functions.**  `ParametersAsync` and `ProbeAsync` run `sys.sp_describe_undeclared_parameters` as `CommandType.StoredProcedure` with `@tsql` and `@params` as `SqlDbType.NVarChar` size -1 (`DBNull.Value` for a null `Params`).  `ParametersAsync` reads `name`, `suggested_system_type_name`, `suggested_max_length`, `suggested_precision`, `suggested_scale`, `suggested_user_type_schema`, `suggested_user_type_name`, `suggested_assembly_qualified_type_name` by name, converting the small integers to `int`.  `ProbeAsync` answers `Binds: true` when nothing is raised, `Binds: false` for a `SqlException` of class below 20 that is not a timeout, and `Error` otherwise.  `DeclaredAsync` runs the text command below with `@tsql` and `@params` as parameters, and answers `Errors` when a row has an `error_number`, `Rows` otherwise:
  ```sql
  SELECT column_ordinal, system_type_name, max_length, precision, scale, user_type_schema, user_type_name,
      assembly_qualified_type_name, error_number, error_type, error_message
  FROM sys.dm_exec_describe_first_result_set(@tsql, @params, 0)
  ORDER BY column_ordinal
  ```
- [ ] **Step 5: Implement `DescribeAsync`** in this order (every call through the exchange, each with its duration, as `PostgresSession.Calls.cs` does):
  1. Each declared parameter's type through `DeclaredTypeText.Read`; the first refused is `SqlServerErrors.TypeRefused` and nothing is sent.
  2. For each declared parameter in list order, `declared` with `Sql = "SELECT @<name> AS [1]"` and `Params = "@<name> <type>"`; `Error` → `Failed(…, ResolveType)`; `Errors` → `SqlServerErrors.Declared`; the type is `FromDeclared(rows[0])`.
  3. `ParameterCopies.Rename(request.Sql, request.Parameters)`.
  4. `parameters` with the renamed text and the declarations of every declared parameter (null when none); `Error` → `SqlServerErrors.Parameters`.
  5. For each reused parameter: the copies' types from the rows (a copy with no row is untyped); `Agreed` gives the type, else for each copy a `probe` with that copy `xml` and every other copy, every other undeclared parameter at its row's `suggested_system_type_name` and every declared parameter as declared (an untyped one left out); when it binds, a second `probe` with the copy `int`; `Ignored` = both bind.  A probe's `Error` → `Failed(…, DescribeParameters)`.  `Unify`'s failure is `NoType` or `CopiesDisagree`.  The type's source is `SidecarValues.TypeSource.InferredFromCopies` (check the constant's exact name).
  6. `ParameterCopies.CompareLists` of the list's undeclared names and the rows' names; any difference → `ListsDiffer`.
  7. A `QueryDescription(SidecarResultKind.None, parameters, null, null, null, null)` with each parameter of the list typed: declared (`SidecarValues.TypeSource.Declared`), reused (step 5), or its row (`Inferred`).
- [ ] **Step 6: Diagnostics.**  `SQLSRC262` after `SQLSRC261` in `ToolDiagnostics` (written out in full, `All` updated): title `"Copies of a parameter have different types"`, message `"The copies of the parameter '{0}' of the query '{1}' have the types '{2}' and '{3}'"`, help link `sqlsrc262`.  `SQLSRC231`: title `"Parameters are read differently"`, message `"{0} reads the parameters of the query '{1}' differently from SqlSource: {2}"`; `PostgresErrors` passes `"Npgsql"` first.  `AnalyzerReleases.Unshipped.md`: the `SQLSRC262` row and `SQLSRC231`'s new title.  `docs/manual/diagnostics.md`: the index rows and sections of `SQLSRC262` and `SQLSRC231`, and SQL Server's causes in the sections of `SQLSRC224`, `SQLSRC225`, `SQLSRC226` and `SQLSRC228` (effects only: what the user wrote, what they see, how to mend it).
- [ ] **Step 7: Write the container `ParameterTests`** (through `Sessions`, `[Collection(UsesSqlServer.Name)]`):
  - every row of the spike's parameter table that types, inferred, as one `[Theory]` of SQL and expected `name`/`maxLength`/`precision`/`scale`/user type (`TOP (@n)` `bigint`; `OFFSET`/`FETCH` `bigint`; `LIKE` `nvarchar(4000)`; `+ N'x'` `nvarchar(4000)`; `IN (@a, @b)` `int`; `email = @email` `nvarchar(320)` user type `dbo.Email`; `total > @t` `decimal(38,19)`; `rv = @rv` `timestamp`; `deleted_at < @since` `datetime2(7)`; `COLLATE` `nvarchar(100)`; `INSERT`/`UPDATE` the columns' types; `CAST(@p AS int)` and `ISNULL(@p, 0)` `int`; inside `EXISTS` and a CTE the column's type; `WHERE @p IS NULL` `int`), and `DATEADD(day, @days, …)` `int` on 14 and `bigint` on 17;
  - a declared `int`, `dbo.Email` and `varchar(max)`, `declared`;
  - a declared type that a use rejects (`-- param: @p xml` with `WHERE id = @p`) → `SQLSRC225`;
  - `SELECT @p AS x` and `SELECT COALESCE(@p, name) FROM dbo.users` → `SQLSRC226` naming `p`;
  - `-- param: @p dbo.Nope` → `SQLSRC228`;
  - the optional filter typed `nvarchar(100)`, `inferred-from-copies`, in a `SELECT`, an `UPDATE` with no `OUTPUT`, and an `IF @a = 1 SELECT id … WHERE (@n IS NULL OR name = @n) ELSE SELECT name FROM dbo.users`;
  - `UPDATE dbo.all_types SET xml_value = @d WHERE (@d IS NULL OR id = 1)` → `xml`;
  - `WHERE @p IS NULL OR @p IS NOT NULL` → whatever the server gives (expected `int`, `inferred`: see "calls" above);
  - `CREATE PROCEDURE p @id int AS SELECT 1` → `SQLSRC231`;
  - `WHERE id = @p AND name = @p` → `SQLSRC262` with `int` and `nvarchar(100)`;
  - `DECLARE @x int = 1; SELECT id FROM dbo.users WHERE id = @x` → no parameter;
  - canaries: a text of two statements with `DROP TABLE dbo.victim` described through `parameters` (a session's describe) and asked through `probe` (`SqlServerLive.ProbeAsync` on the session's connection), and `-- param: @p int; DROP TABLE dbo.victim` (`SQLSRC228`): after each, `OBJECT_ID('dbo.victim')` is not null.
- [ ] **Step 8:** Run the tool's and the container's tests; they pass.  Also on the floor: `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/SqlSource.Tests.SqlServer`.
- [ ] **Step 9:** `./format.sh`; `./pre-commit-validation.sh` passes.  Commit `Describe a SQL Server query's parameters`.

### Task 7: The events

**Files:**
- Create: `src/SqlSource.Tool/Describing/SqlServer/SqlServerEvents.cs`
- Modify: `SqlServerSession.Calls.cs` (each call writes its event)
- Test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerEventsTests.cs`, `tests/SqlSource.Tool.Tests/Describing/RedactionTests.cs`

**Interfaces:**
- Produces: `SqlServerEvents.Parameters(IRunLog, string query, SqlServerParametersRequest, SqlServerParametersAnswer, long durationMs)`, `.Probe(…)`, `.Declared(…)`, written as `parameters`, `probe`, `declared` (the log prefixes `mssql.`).  Members: `query`, `sql`, `params`, then `rows` (each row a map of every member, named as the record's JSON names them), `binds` or `rows`/`errors`, or `error` (a list of maps `number`, `class`, `state`, `message`); then `durationMs`.

- [ ] **Step 1: Write tests**: each event's members for an answer with rows and one with an error, as `PostgresEventsTests` does; `RedactionTests` gains a case for each new event whose `sql`, `params` and an error `message` hold the secret, which the session's log removes.
- [ ] **Step 2:** Run; they fail.  **Step 3:** Implement as `PostgresEvents` does; class remarks list the order a describe writes them (`declared` for each declared parameter, `parameters`, `probe` for each probe), and that `open` has no event of its own.
- [ ] **Step 4:** Run; they pass.  `./format.sh`; `./pre-commit-validation.sh` passes.  Commit `Log the calls of the SQL Server describer`.

### Task 8: The fixtures

**Files:**
- Create: `tests/SqlSource.Tests.SqlServer/Describer/FixtureCasesTests.cs`, `FixtureVersionTests.cs`
- Create: `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/<case>/query.sql` (+ recorded `capture.json`, `expected.json`) for the six cases below
- Modify: `tools/record-fixtures.sh`, `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`, `tools/README.md`, `tools/AGENTS.md`, `tests/SqlSource.Tests.SqlServer/README.md`, `CONTRIBUTING.md`

- [ ] **Step 1: Write the cases' `query.sql`** (each a complete SqlSource file of one query, LF):
  - `update-inferred`: `-- name: RenameUser` / `UPDATE dbo.users SET name = @name, email = @email WHERE id = @id;`
  - `insert-declared`: `-- name: AddUser` / `-- param: @nickname nvarchar(50)` / `-- param: @email dbo.Email` / `INSERT INTO dbo.users (name, email, nickname, credit) VALUES (@name, @email, @nickname, @credit);`
  - `delete-declared-only`: `-- name: PurgeOrders` / `-- param: @tenant int` / `DELETE FROM dbo.orders WHERE user_id = @userId {{filter:AND note IS NOT NULL}};`
  - `update-optional-filter`: `-- name: MarkByName` / `UPDATE dbo.users SET nickname = N'x' WHERE (@name IS NULL OR name = @name);`
  - `update-copies-agree`: `-- name: RetireBefore` / `UPDATE dbo.users SET deleted_at = SYSUTCDATETIME() WHERE created_at < @before OR deleted_at < @before;`
  - `update-wide-types`: `-- name: ClearNicknames` / `UPDATE TOP (@n) dbo.users SET nickname = NULL WHERE name LIKE @pattern;`
- [ ] **Step 2: Write `FixtureCasesTests` and `FixtureVersionTests`** as PostgreSQL's, for `SidecarValues.Engine.SqlServer`, describing through `new SqlServerDescriber()` and `SqlServerContainer.ConnectionString`, `RecordVariable = "SQLSOURCE_RECORD_FIXTURES"`.  `FixtureVersionTests` holds the captures' `"productVersion"` and the expected's `"serverVersion"` to `SqlServerContainer.ImageBuilds[DefaultImages.SqlServer]`.  `DescriberFixtureTests`: `DescriberOf` gains `"mssql" => new SqlServerDescriber()`; a `Cases_OfSqlServer_HoldEveryCase` listing the six; the capture-with-a-call-removed test runs for an `mssql` case too.
- [ ] **Step 3: Rewrite `tools/record-fixtures.sh`** as `record-fixtures.sh [postgres|mssql]`, both when none: one function maps an engine to its row (project, case folder, test class, image variable, `DefaultImages` constant); usage error (exit 2) for anything else; each engine prints its line as today.  No associative arrays.
- [ ] **Step 4: Record**: `tools/record-fixtures.sh mssql`.  Read every `expected.json` against the spike: `update-optional-filter`'s `name` is `nvarchar(100)` `inferred-from-copies`; `update-copies-agree`'s `before` `datetime2(7)` `inferred-from-copies`; `update-wide-types` `bigint` and `nvarchar(4000)`; `delete-declared-only`'s `tenant` `int` `declared`; `insert-declared`'s `email` `nvarchar(320)` with user type `dbo.Email` `declared`, `credit` `decimal(10,2)` `inferred`.  Check that `tools/record-fixtures.sh postgres` leaves the PostgreSQL cases unchanged (`git status`).
- [ ] **Step 5:** Run `DescriberFixtureTests` and the container's `FixtureCasesTests` and `FixtureVersionTests` (and on the floor image); they pass.  Documents: `tools/README.md`, `tools/AGENTS.md`, the project's README (its fixtures section) and `CONTRIBUTING.md` give the script's argument.
- [ ] **Step 6:** `./format.sh`; `./pre-commit-validation.sh` passes.  Commit `Record the first fixtures of the SQL Server describer`.

### Task 9: The documents, and the outline's row Done

**Files:**
- Create: `docs/architecture/sqlserver-describer.md`
- Modify: `docs/architecture/README.md`, `src/SqlSource.Tool/AGENTS.md`, `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md`, `docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md`, `docs/superpowers/specs/2026-10-07-sidecar-format-design.md` (the SQL Server `bindName` row), `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 6.1 `Done`)

- [ ] **Step 1: `docs/architecture/sqlserver-describer.md`**, as `postgres-describer.md` is PostgreSQL's, for what exists: the files of `Describing/SqlServer/`; the connection and why it is made inside a `live` function; the calls and their records (a table: method, request, answer, when asked); one describe step by step (the seven steps of Task 6, with why `declared` comes first); the copies and the probe (with the spike's evidence in two sentences and the example of the optional filter); the lists' check; types (`name` verbatim, `sysname`, `timestamp`, no `bindName`); errors (the table of Task 6 and how `SQLSRC226` picks its name); why a query cannot run a statement (every text sent and how); events; fixtures.  Link the architecture README to it.
- [ ] **Step 2: `src/SqlSource.Tool/AGENTS.md`**: a line at the top linking the new page; rules for `Describing/SqlServer/`: the user's text and a declaration reach the server only as parameter values of the two procedures, with the canary tests in `tests/SqlSource.Tests.SqlServer/Describer/ParameterTests.cs` that prove it and that a new place sending a user's text gets one; `DeclaredTypeText` is the first refusal, not the guarantee; the copies are renamed by the lexer and a copy is ignored only when the probe binds it as `xml` and as `int`; the probe runs only when copies disagree; the calls and records are a contract with the captures (`SqlServerCallNames`, `SqlServerCalls.cs`); a cancellation that SqlClient raises as a `SqlException` leaves as `OperationCanceledException`; `SqlServerConnection.cs` is the second file `CA2100` is off for.  Keep the maintenance footer.
- [ ] **Step 3: Tech debt and design.**  `TD-0004`: the SQL Server parameter rows and the closing paragraph say the tool now catches both directions as `SQLSRC231` when it describes the query.  `TD-0034`: a row for SQL Server: a server message of a later call is kept verbatim and can name the login (error 916 for a three-part name in another database) — the redaction removes the value and the password but not a login name.  Sidecar format design: SQL Server's `bindName` row says it is never written, and why (phase 8 binds by `SqlDbType` and the facets, a CLR type by `userType`).
- [ ] **Step 4:** Row 6.1 of the outline: `Done`.
- [ ] **Step 5:** `./pre-commit-validation.sh` passes.  Commit `Document the SQL Server describer's session and parameters`.

### Task 10: CI's timeout, from a measured run

- [ ] **Step 1:** After the PR's first CI run, read the `build` job's duration (`gh run view <id> --json jobs`); set `timeout-minutes` to about 1.5 times it, rounded up to 5.  This rides in the first batch of review fixes; the PR says what was measured.
