# Query generation phase 6.2, the SQL Server columns and table match - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The SQL Server describer records a query's columns (type, the server's nullability, origin, identity, computed), matches a table, reports the procedure's failures by their own ids, and forwards SqlClient's event source into the run's log.

**Architecture:** After the parameters are typed, the session asks `columns`, `sys.dm_exec_describe_first_result_set(@tsql, @params, 2)`, and builds the columns from its rows with no correction.  When every column has an origin in one table, it asks `table`, the same function over `SELECT * FROM [schema].[table]`, and `SqlServerTableMatcher` runs the format's six checks.  An `EventListener` scoped to the session forwards an allowlist of SqlClient's events.

**Tech Stack:** C# (.NET 8/10), Microsoft.Data.SqlClient 7.1.1, xUnit v3, Shouldly, Testcontainers (SQL Server 2025-CU9-GDR1 default, 2017-CU31-GDR19 floor).

**Spec:** `docs/superpowers/specs/2026-10-20-query-generation-phase-6-2-columns-and-table-match-design.md`, with the spike `docs/superpowers/specs/2026-10-20-query-generation-phase-6-sql-server-spike.md` and the epic's "Phase 6 - SQL Server".

## Global Constraints

- Root `AGENTS.md` rules hold over this plan and any skill: every commit and push passes `./pre-commit-validation.sh` on the exact tree; after a change to the describer or its tests also run `tests/SqlSource.Tests.SqlServer` with `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`; minor review findings are fixed, never deferred.
- Read `src/SqlSource.Tool/AGENTS.md`, `tests/AGENTS.md` and the `AGENTS.md` of every folder touched before editing it.
- Call names and record members are a contract with the captures (`SqlServerCalls.cs` header): add calls and nullable members, never rename one.  `CallRecordTests` holds the records to it.
- Browse mode 2, not 1.  Hidden rows (`is_hidden = 1`) are dropped before anything is built.
- The describer never narrows the server's nullability: `nullable` is `is_nullable`, `nullableSource` `server`; an `is_nullable` of null is recorded nullable.
- Failures are classified by `error_type`, never by message.
- `plan` is `skipped` on every entry with rows; an entry with no rows has neither `plan` nor `tableMatch`, as on PostgreSQL.
- The `table` call's text is the tool's: `SELECT * FROM [schema].[table]`, each name a catalog name the server gave, `]` doubled.  Never the user's text.
- Driver logging is an allowlist of events and payload members; nothing that can name the host, the database, the user or a password; only when `IRunLog.Writes`.
- Diagnostics `SQLSRC263` to `SQLSRC266` only; shared files (`ToolDiagnostics.cs`, `AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md`, `docs/architecture/`) change only in these ids' rows and sections and in the SQL Server describer's page.
- The describer stays unregistered in `ToolHost.Create` (6.3 registers it).  `docs/manual/sqlserver.md` stays a placeholder (6.3 writes it).
- One PR to `main`, about 100 files (count below), under the limit of 150.

## Where the code differs from the spec, and the calls this plan makes

1. **Error rows are apart from columns.**  The spec lists `error_*` among a `columns` row's members.  6.1's `declared` answer already splits the function's rows into `Rows` and `Errors` (`SqlServerDescribeError`), so `columns` and `table` follow it: `SqlServerColumnsAnswer(Rows, Errors, Error)`.
2. **The session's database is asked by `open`.**  The origin rule compares `source_database` with the session's database, which nothing records today.  `open` gains a nullable member `Database`, `DB_NAME()`; every capture is recorded again in this PR anyway.
3. **`table` is asked only when the match needs it.**  The spec asks it "once for each distinct origin".  The first two checks, `no-origin` and `several-tables`, need no table, so `table` is asked once, for the one table, only when every column has an origin there.  A failing `table` call for a table the match never reads would otherwise fail the query.  The PR says so.
4. **`mssql.table-match` names its result `tableMatch`**, as the spec says (PostgreSQL's names it `result`).
5. **The spec's "Not decided with the owner" list** is settled as the spec recommends: browse mode 2; the table's columns and nullability from the server's description of `SELECT *`; an origin outside the session's database dropped; driver logging as an allowlist; a column with no name is `SQLSRC266`; `EXEC(N'...')` and `EXEC dbo.proc` recorded as described, untested.  The PR lists them.
6. **A spike row that fails under mode 2 where mode 1 answered** stops the build: the controller asks the implementation manager for the owner's decision before Task 4 goes on (Task 3, step 4).
7. **The manual's first-result-set sentence** waits for `docs/manual/sqlserver.md` (6.3): this PR adds it to the phase 6 row of `docs/manual/AGENTS.md`'s phase map, and D-0006's "Delivered instead" names the code.

## File count

| Group | Files |
|----|----|
| Source (`SqlServer/` 10, `ToolDiagnostics.cs`, `AnalyzerReleases.Unshipped.md`) | 12 |
| Tool tests (`Describing/SqlServer/` 7, `CallRecordTests`, `SecretTests`, `RedactionTests`, `EntrySecretCheckTests`) | 11 |
| Container tests (`ColumnTests`, `FailureTests`, `TableMatchTests`, `SafetyTests`, `DriverLogTests`, `SessionTests`, `ParameterTests`, `Sessions`) | 8 |
| Fixtures: 6 cases recorded again (2 each), 17 new (3 each) | 63 |
| Docs (plan, epic, spike, sidecar format, D-0006, architecture page, Tool `AGENTS.md`, `diagnostics.md`, manual `AGENTS.md`, `tests/SqlSource.Tests.SqlServer/README.md`) | 10 |
| **Total** | **about 104** |

---

### Task 1: The outline's row says In progress

**Files:** Modify `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md:86`; add this plan.

- [ ] **Step 1:** Set the 6.2 row's status to `In progress`.
- [ ] **Step 2:** `./pre-commit-validation.sh` passes; commit "Start phase 6.2: the SQL Server columns and table match".

### Task 2: The `columns` and `table` calls, the columns and the table matcher (units)

**Files:**
- Modify: `src/SqlSource.Tool/Describing/SqlServer/SqlServerCalls.cs`, `SqlServerCallNames.cs`, `SqlServerLive.cs`, `SqlServerTypes.cs`
- Create: `src/SqlSource.Tool/Describing/SqlServer/SqlServerColumns.cs`, `SqlServerTableMatcher.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerColumnsTests.cs`, `SqlServerTableMatcherTests.cs`, `SqlServerTypesTests.cs`, `tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs` (+ `CallRecordSamples.cs` if it lists samples)

**Interfaces (produces):**
- `SqlServerCallNames.Columns = "columns"`, `SqlServerCallNames.Table = "table"`.
- `SqlServerOpenAnswer` gains a last member `string? Database` (`DB_NAME()`); `OpenSql` selects it.  Update every constructor call.
- `record SqlServerColumnsRequest(string Sql, string? Params)`.
- `record SqlServerTableRequest(string Schema, string Table)`.
- `record SqlServerColumnsAnswer(IReadOnlyList<SqlServerColumnRow>? Rows, IReadOnlyList<SqlServerDescribeError>? Errors, SqlServerServerError? Error)`: `Errors` non-null only when the function answered error rows (then `Rows` null), as `SqlServerDeclaredAnswer`.  Both calls answer it.
- `record SqlServerColumnRow(bool IsHidden, int ColumnOrdinal, string? Name, bool? IsNullable, string? SystemTypeName, int? MaxLength, int? Precision, int? Scale, string? UserTypeSchema, string? UserTypeName, string? AssemblyQualifiedTypeName, string? SourceServer, string? SourceDatabase, string? SourceSchema, string? SourceTable, string? SourceColumn, bool? IsIdentityColumn, bool? IsComputedColumn)`, members named as the function's columns.
- `SqlServerLive.ColumnsAsync(SqlServerColumnsRequest, CancellationToken) : Task<SqlServerColumnsAnswer>` and `SqlServerLive.TableAsync(SqlServerTableRequest, CancellationToken) : Task<SqlServerColumnsAnswer>`: one shared reader over `sys.dm_exec_describe_first_result_set(@tsql, @params, 2) ORDER BY column_ordinal`, the user's text only as `@tsql`, `@params` DBNull for null.  `public static string TableSelect(string schema, string table)` gives `SELECT * FROM [s].[t]` with `]` doubled.
- `SqlServerTypes.FromColumn(SqlServerColumnRow row) : SqlServerType`.
- `SqlServerColumns.Build(IReadOnlyList<SqlServerColumnRow> rows, string? database) : Outcome<IReadOnlyList<BuiltColumn>, int>` where `record BuiltColumn(SidecarColumn Column, string? NoOriginReason)`; the failure is the ordinal of the first visible column with a null name (for `SQLSRC266`).  Hidden rows dropped; an empty visible list is an empty list (the session reads it as `None`).
- `SqlServerColumns.OriginOf(SqlServerColumnRow row, string? database) : (SidecarOrigin? Origin, string? Reason)`.  An origin needs `SourceSchema`, `SourceTable` and `SourceColumn` all given, `SourceServer` null, and `SourceDatabase` ordinally equal to `database` (both non-null).  Reasons, fixed strings for `mssql.column`: `"expression"` (no source given), `"another server"`, `"another database"`.
- `SqlServerTableMatcher.Match(IReadOnlyList<SidecarColumn> columns, IReadOnlyList<SqlServerColumnRow>? table) : (SidecarTable? Table, string Result)`: `table` is the visible rows of `table`'s answer, null when it was not asked (the first two checks failed).  Also `SqlServerTableMatcher.TableOf(IReadOnlyList<SidecarColumn> columns) : SidecarOrigin?`: the one table (schema and table) every column comes from, or null; the session asks `table` only for it.

- [ ] **Step 1: Failing tests.**  `SqlServerColumnsTests` from rows the test builds: `Build_HiddenRow_IsDropped` (a `ROWSTAT` hidden row among two); `Build_NoVisibleRow_IsEmpty`; `Build_Row_RecordsEachField` (name with a `!` suffix kept, ordinal, `type` by `SqlServerTypes`, `nullable` = `IsNullable`, `nullableSource` `"server"`, origin, identity, computed); `Build_NullIsNullable_IsNullable` (`IsNullable` null gives `true`); `Build_NullFlags_AreNull`; `Build_NullName_FailsWithItsOrdinal` (ordinal 1 of `SELECT 1, id`); `Build_TwoColumnsOfOneName_AreBothRecorded`; `OriginOf_OtherDatabase_IsNone` (`msdb`), `OriginOf_SystemResource_IsNone` (`mssqlsystemresource`), `OriginOf_OtherServer_IsNone`, `OriginOf_PartlyGiven_IsNone`, `OriginOf_NoSessionDatabase_IsNone`.  `SqlServerTableMatcherTests` from columns and table rows the test builds: one test per result in the format's order (`no-origin`, `several-tables`, `names-differ` with an alias, `columns-differ` with a column left out and with one twice, `order-differs`, `nullability-differs`, `matched`); `Match_TwoChecksFail_GivesTheFirst`; `Match_View_Matches`; `Match_ComputedNullableInBoth_Matches`; `Match_NamesDifferInCase_NamesDiffer` (ordinal); `Match_HiddenTableRow_IsNotAColumnOfTheTable`; `TableOf_*` for one table, two tables, a column with no origin.  `SqlServerTypesTests.FromColumn_*` for a system type, an alias type and a CLR type.  `CallRecordTests`: the new records under the rule.
- [ ] **Step 2:** Run `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~SqlServerColumns|FullyQualifiedName~SqlServerTableMatcher|FullyQualifiedName~SqlServerTypes|FullyQualifiedName~CallRecord"`: fails to compile.
- [ ] **Step 3:** Implement the interfaces above.  The matcher's checks, after `no-origin` and `several-tables` (schema and table compared ordinally): names (column names against their origins' column names, ordinal); `columns-differ` when the origins' column names, sorted ordinally, differ from the table's visible names sorted (so a column left out or there twice differs); `order-differs` when they differ in order; `nullability-differs` when a column's `Nullable` differs from its table row's `IsNullable ?? true`.  `matched` gives `new SidecarTable(schema, table)`.  Doc comments say why: the table's list and nullability are the server's reading of `SELECT *`, so a `HIDDEN` period column is out and a computed column is in, judged by its expression.
- [ ] **Step 4:** The tests of step 2 pass.
- [ ] **Step 5:** `./pre-commit-validation.sh` passes (the replayed captures lack `database` in `open`: a replay reads it as null, which nothing reads yet).  Commit "Read the columns of a SQL Server query and match a table".

### Task 3: The spike's rows under browse mode 2, through SqlClient (container)

**Files:** Create `tests/SqlSource.Tests.SqlServer/Describer/ColumnTests.cs`; modify `docs/superpowers/specs/2026-10-20-query-generation-phase-6-sql-server-spike.md` only for a row that answers otherwise.

**Interfaces (consumes):** `SqlServerLive.ColumnsAsync` and `SqlServerColumns.Build` from Task 2, on `Sessions.OpenSessionAsync(...).Connection` (the live class over the session's connection).

- [ ] **Step 1:** A theory, one case per row of the spike's "Columns and nullability" and DML tables (their SQL against `schema.sql`'s tables), each with the expected visible columns: name, `nullable`, and origin `schema.table.column` or none.  Expected values are the spike's, except that under mode 2 a view's columns have the view as origin.  Plus: `Columns_HiddenRow_IsRowstatAlone` (mode 2 adds one hidden row); `Columns_ViewStar_OriginIsTheView`; `Columns_OtherDatabase_HasNoOrigin` (`msdb.dbo.sysjobs`, `master.sys.databases`: `source_database` is not the session's); `Columns_NoResultSet_HasNoRows` (`DELETE ... OUTPUT ... INTO dbo.victim`, an `UPDATE` with no `OUTPUT`, `SELECT ... INTO #x`); `Columns_JsonAndVector_On2025` (skips below major 17: creates a table with `json` and `vector(3)` and finds `system_type_name` `json` and `vector(3)`).  The two version-dependent rows (`EXEC sp_who` is not in the spike's column tables; `DATEADD` is a parameter row) need no case.
- [ ] **Step 2:** Run on the default image: `dotnet test tests/SqlSource.Tests.SqlServer --filter FullyQualifiedName~ColumnTests`, then with `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.
- [ ] **Step 3:** A row that answers otherwise than the spike on mode 2 through SqlClient is recorded as it answers, and the spike's appendix is corrected with a sentence saying SqlClient under mode 2 answered so.
- [ ] **Step 4: Stop point.**  A row that fails (error rows) under mode 2 where the spike's mode 1 answered: do not continue.  Report it to the controller, who asks the implementation manager for the owner's decision.
- [ ] **Step 5:** `./pre-commit-validation.sh` passes, and the floor run of step 2.  Commit "Hold the server's columns under browse mode 2 to the spike".

### Task 4: The failures and `SQLSRC263` to `SQLSRC266`

**Files:**
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs` (four descriptors after `CopiesHaveDifferentTypes`, and `All`), `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerErrors.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerErrorsTests.cs`

**Interfaces (produces):**
- `ToolDiagnostics.ConflictingResults` `SQLSRC263`, title "Query returns rows of different shapes", message "The query '{0}' returns rows of different shapes on different paths".
- `ToolDiagnostics.DynamicSql` `SQLSRC264`, title "Query runs SQL the server cannot read", message "The query '{0}' runs SQL that the server cannot read until it runs".
- `ToolDiagnostics.TemporaryTableInBatch` `SQLSRC265`, title "Query uses a temporary table in a batch", message "The query '{0}' uses a temporary table in a batch of several statements".
- `ToolDiagnostics.ColumnHasNoName` `SQLSRC266`, title "Column has no name", message "Column {0} of the query '{1}' has no name".
- All four as the others: Error, `NotConfigurable`, help link `sqlsrc26x`.
- `SqlServerErrors.TemporaryTableHelp = "make the query one statement, or use a table variable in place of the temporary table"`; `SqlServerErrors.ColumnNameHelp = "give the column a name with AS"`.
- `SqlServerErrors.Columns(DescribeRequest query, IReadOnlyList<SqlServerDescribeError> errors, DescribeStep step) : DescribeFailure`: the first row whose `Type` is 3, 4 or 10 gives `SQLSRC263`, `SQLSRC264` or `SQLSRC265` (with its help); else the first row of type 8 gives `SQLSRC225`; else `SQLSRC225`, its message argument the first row's message.  Every row is a `server:` line, `Msg {number}: {message}`, in the server's order (the format `Declared` uses).  `step` is `DescribeColumns` for `columns`, `TableMatch` for `table`.
- `SqlServerErrors.AfterRefusal(DescribeRequest query, SqlServerServerError parameters, CopiedSql copied, SqlServerColumnsAnswer columns) : DescribeFailure`: when `columns` answered error rows and one has type 3, 4 or 10, that row's id as `Columns` gives it; otherwise `Parameters(query, parameters, copied)` as today.
- `SqlServerErrors.NoColumnName(DescribeRequest query, int ordinal) : DescribeFailure` (`SQLSRC266`, arguments `[ordinal, query.Name]`, step `DescribeColumns`, no server line, `ColumnNameHelp`).
- `SqlServerErrors.RefusedBatch = 11501` (the number the session tests `parameters`' error for).

- [ ] **Step 1: Failing tests.**  `Columns_ConflictingResults_Is263`, `Columns_DynamicSql_Is264`, `Columns_TemporaryTable_Is265WithHelp`, `Columns_UndeclaredParameter_Is225`, `Columns_OtherType_Is225WithEveryLineInOrder` (334 then 11501; 207 then 11501; 102 then 11501; 2812 then 11529), `Columns_TableStep_IsTableMatch`, `AfterRefusal_TemporaryTable_Is265`, `AfterRefusal_OtherRows_IsParametersFailure`, `AfterRefusal_ColumnsAnswered_IsParametersFailure`, `NoColumnName_Is266`.  `tests/SqlSource.Tests/Diagnostics/` tests fail until `diagnostics.md` has the four sections.
- [ ] **Step 2:** Run `dotnet test tests/SqlSource.Tool.Tests --filter FullyQualifiedName~SqlServerErrorsTests` and `dotnet test tests/SqlSource.Tests --filter FullyQualifiedName~Diagnostics`: fail.
- [ ] **Step 3:** Implement.  In `diagnostics.md`: the four index rows beside `SQLSRC262`; a section each (meaning, cause, fix, a `sql error` sample where one helps; `SQLSRC264`'s fix: write the SQL in the query, or `EXEC` a literal string; `SQLSRC263`'s: make every path return the same columns, or split the query); `SQLSRC225`'s section gains SQL Server's causes (an `OUTPUT` without `INTO` on a table with a trigger, an unknown column, a `GO` inside the query, a word read as a procedure); the paragraph on the ids of describing says "and `SQLSRC262` to `SQLSRC266`".  Rows in `AnalyzerReleases.Unshipped.md` between `SQLSRC262` and `SQLSRC267`.
- [ ] **Step 4:** Step 2's runs pass.
- [ ] **Step 5:** `./pre-commit-validation.sh` passes.  Commit "Report the failures of describing a SQL Server query's columns".

### Task 5: The session asks `columns` and `table`; the events; the fixtures recorded again

**Files:**
- Modify: `src/SqlSource.Tool/Describing/SqlServer/SqlServerSession.cs`, `SqlServerSession.Calls.cs`, `SqlServerEvents.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionTests.cs`, `SqlServerEventsTests.cs`; `tests/SqlSource.Tests.SqlServer/Describer/SessionTests.cs`, `ParameterTests.cs`; every `capture.json` and `expected.json` under `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/`

**Interfaces (consumes):** Tasks 2 and 4.  **Produces:** `SqlServerSession` keeps `Database` from `open` (a private field); `ColumnsAsync`/`TableAsync` call wrappers in `SqlServerSession.Calls.cs`; events `SqlServerEvents.Columns(log, query, SqlServerColumnsRequest, SqlServerColumnsAnswer, durationMs)`, `Table(log, query, SqlServerTableRequest, SqlServerColumnsAnswer, durationMs)`, `Column(log, query, SidecarColumn, string? noOriginReason)`, `TableMatch(log, query, SidecarTable?, string tableMatch)`.

The order of a describe, after today's (declared, parameters, list comparison, probes):

1. `columns` with `request.Sql` as written and `params` every parameter of the list in order: a declared one at its declaration as `DeclaredTypeText` read it, any other at `SqlServerTypes.Spelled` of its described type.
2. `Error` is `SQLSRC225` at `describe columns` (`SqlServerErrors.Failed`); error rows are `SqlServerErrors.Columns(..., DescribeColumns)`.
3. `SqlServerColumns.Build`: a null name is `NoColumnName`; no column is `SidecarResultKind.None` (columns, match and plan null); else one `mssql.column` per column, in order.
4. `SqlServerTableMatcher.TableOf`; when it gives a table, `table` for it (`Error` is `SQLSRC225` at `table match`, error rows `Columns(..., TableMatch)`); then `Match`; `mssql.table-match`.
5. `QueryDescription(Rows, parameters, columns, matchesTable, SidecarValues.Plan.Skipped, result)`.

When `parameters` answers an error whose rows include `11501`: ask `columns` with `request.Sql` and `params` the declarations alone (null when there are none), then return `SqlServerErrors.AfterRefusal(...)`.  Any other `parameters` error is reported as today, with no `columns`.

Events: `mssql.columns` and `mssql.table` with `query`, `sql` and `params` (`table`: `schema` and `table`), then `error`, `errors` or `rows` as `declared` writes them, each row a map of every member in camelCase, then `durationMs`; `mssql.column` with `query`, `ordinal`, `name`, `type`, `origin` (map of `schema`, `table`, `column`, or null), `noOrigin` (the reason, or null), `nullable`, `nullableSource`, `identity`, `computed`; `mssql.table-match` with `query`, `tableMatch` and `table` (map of `schema` and `table`, or null).  The class's remarks list the new order of events.

- [ ] **Step 1: Failing tests.**  `SqlServerSessionTests` (replayed calls the test builds, as the file's existing tests do): `DescribeAsync_Rows_RecordsColumnsAndMatch`; `DescribeAsync_NoRows_IsNone`; `DescribeAsync_ColumnsParams_DeclareEveryParameter` (a declared, an inferred and a reused parameter: the request's `params`); `DescribeAsync_OneTable_AsksTableOnce`; `DescribeAsync_TwoTables_AsksNoTable`; `DescribeAsync_NoOrigin_AsksNoTable`; `DescribeAsync_TableFails_Is225AtTableMatch`; `DescribeAsync_Refused11501_AsksColumnsWithDeclarationsAlone`; `DescribeAsync_OtherParametersError_AsksNoColumns`; `DescribeAsync_NullName_Is266`.  `SqlServerEventsTests`: one per new event, members in order, and the order of a describe's events.
- [ ] **Step 2:** Run the two classes: fail.
- [ ] **Step 3:** Implement.  Update the remarks of `SqlServerSession` (rows are described now) and `SqlServerEvents`.
- [ ] **Step 4:** Step 2 passes.  Record again: `tools/record-fixtures.sh mssql`.  Check each `expected.json` diff: the six cases have `resultKind` `none` still (statements without rows); every `capture.json` gains `database` and a `columns` call.  Update the container `SessionTests`/`ParameterTests` that asserted `ResultKind.None` for a statement that now has rows (none expected: 6.1's cases are statements without rows).
- [ ] **Step 5:** `./pre-commit-validation.sh` passes, and the floor run of `tests/SqlSource.Tests.SqlServer`.  Commit "Describe a SQL Server query's columns and match its table".

### Task 6: SqlClient's event source into the run's log

**Files:**
- Create: `src/SqlSource.Tool/Describing/SqlServer/SqlClientRunListener.cs`
- Modify: `SqlServerSession.cs` (create in `OpenAsync` before the connection is opened when `log.Writes`; dispose in `DisposeAsync` and when `open` fails)
- Test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlClientRunListenerTests.cs`; `tests/SqlSource.Tests.SqlServer/Describer/DriverLogTests.cs`; `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`, `Describing/RedactionTests.cs`, `EntrySecretCheckTests.cs` where they list the describer's events

**Interfaces (produces):**
- `internal sealed class SqlClientRunListener : EventListener` with `public const string EventName = "driver"`, `public const string SourceName = "Microsoft.Data.SqlClient.EventSource"`, `public static SqlClientRunListener? For(IRunLog log)` (null when `!log.Writes`), and `internal void Forward(string eventName, EventLevel level, int eventId, IReadOnlyList<string> payloadNames, IReadOnlyList<object?> payload)`, which `OnEventWritten` calls and the unit tests call directly.
- The allowlist, a static map of event name to payload members: `BeginExecute` → `objectId`, `commandText`; `EndExecute` → `objectId`, `compositeState`, `sqlExceptionNumber`.  Enabled for the keyword `ExecutionTrace` (1) at `Informational`.  Builder's job: confirm on a container run, by a listener that records every event and payload name of keywords 1 under the session's calls, that these are the names SqlClient 7.1.1 writes; adjust the list (never to a member that can hold a host, database, user, password or a free-form message: `dataSource`, `database` and `message` are out) and record what was found in the class's remarks.
- An event written: `mssql.driver` through the describer's log with `category` (the event's name), `level` (lower-case `EventLevel`), `eventId`, each listed member by its name, and `exception` null.

- [ ] **Step 1: Failing tests.**  `SqlClientRunListenerTests`: `For_LogThatKeepsNothing_IsNull`; `Forward_ListedEvent_WritesListedMembersAlone` (a `BeginExecute` with `dataSource` and `database` in its payload: neither is written); `Forward_UnlistedEvent_WritesNothing` (`Trace`, a free-form string); `Forward_StringsPassTheLogsRedaction` (through a log that redacts).  `DriverLogTests` (container): `Describe_WithLog_WritesDriverEvents` and none of their strings holds the container's host, `sa`, the database's name or the password; `Open_RefusedConnection_WritesNoSecret` and `Open_RejectedLogin_WritesNoSecret` (same check, and the listener is disposed); `Describe_WithoutLog_EnablesNoListener` (`For` gave null: no event source has a listener of this type).
- [ ] **Step 2:** Run both classes: fail.
- [ ] **Step 3:** Implement.  `OnEventSourceCreated` enables only `SourceName`.  The listener writes from any thread through `IRunLog.Write`; check `IRunLog`'s implementations are safe to call concurrently, and lock in the listener if not.
- [ ] **Step 4:** Step 2 passes.  If an allowlisted member could still name something, add `docs/tech-debt/TD-0034`'s note; otherwise leave it.
- [ ] **Step 5:** `./pre-commit-validation.sh` passes, and the floor run.  Commit "Forward SqlClient's commands into the run's log".

### Task 7: The new cases and the container tests of failures, matches and safety

**Files:**
- Create: 17 cases under `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/` (each `query.sql`, then `capture.json` and `expected.json` by `tools/record-fixtures.sh mssql`): `select-left-join`, `select-view-star`, `select-table-star`, `select-hidden-period`, `select-temporal-all`, `select-aggregates`, `select-alias-and-clr`, `select-computed-not-null`, `select-other-database`, `insert-output`, `merge-output`, `select-first-result-set`, `select-names-differ`, `select-columns-differ`, `select-order-differs`, `select-nullability-differs`, `select-several-tables`.
- Create: `tests/SqlSource.Tests.SqlServer/Describer/FailureTests.cs`, `TableMatchTests.cs`, `SafetyTests.cs`
- Modify `schema.sql` only if a case needs a table the schema lacks (for `select-computed-not-null`, `dbo.users.name_len_p` is the column; for `select-table-star`, `dbo.users` has a computed column and a `rowversion`).

Each case's `expected.json` is checked against the spec's "Proves" column before it is committed: `select-view-star` `matchesTable` `dbo.user_orders`; `select-table-star` and `select-computed-not-null` `matched`; `select-hidden-period` `matched` to `dbo.prices` with `id, amount`; `select-temporal-all` and `select-other-database` `no-origin`; `insert-output` `matched` to its table (`OUTPUT inserted.*`); `merge-output` every `inserted`/`deleted` column nullable; `select-first-result-set` the first statement's columns alone; each `select-<check>` case its check.

- [ ] **Step 1:** Write the 17 `query.sql` files (`-- name:` and the query, SQL left to the builder within the table) and the three container classes: `FailureTests` — a temporary table in two statements is `SQLSRC265` (its `server:` lines say the procedure's rows), `EXEC(@s)` of a variable is `SQLSRC264`, an `IF` of two shapes is `SQLSRC263`, `INSERT dbo.trig ... OUTPUT inserted.id` is `SQLSRC225` naming 334; `TableMatchTests` — each of the seven results against `schema.sql`; `SafetyTests` — `columns` of `SELECT 1 AS a; DROP TABLE dbo.victim`, of `WAITFOR DELAY '00:00:30'; SELECT 1 AS a` (answers in under 10 s), and of `SHUTDOWN WITH NOWAIT`: afterwards `dbo.victim` exists and the server answers `SELECT 1`.
- [ ] **Step 2:** Run them on the default image: the container classes fail where the code has a gap; `FixtureCasesTests` fails for the new cases until recorded.
- [ ] **Step 3:** `tools/record-fixtures.sh mssql`; check each `expected.json` against the list above.  Fix any code gap found, with a unit test for it.
- [ ] **Step 4:** All pass on the default image and on the floor image (`FixtureCasesTests` compares with one server version).
- [ ] **Step 5:** `./pre-commit-validation.sh` passes.  Commit "Prove the SQL Server columns and table match against the server".

### Task 8: The documents, and the outline's row set to Done

**Files:** `docs/architecture/sqlserver-describer.md` ("Columns and the table match" placeholder replaced: the `columns` call, browse mode 2 and why, hidden rows, origins and the session's database, the failures by `error_type` and the 11501 path, `table` only when the match needs it and its text from catalog names, the matcher's six checks, `plan: skipped`, driver logging's allowlist and scope); `src/SqlSource.Tool/AGENTS.md` (rules: hidden rows dropped, browse mode 2, origins of another database dropped, `table`'s text from catalog names alone, no walk and `plan: skipped`, never narrow the server's nullability, the listener's allowlist and scope; linking the architecture page; names in the maintenance footer); `docs/superpowers/specs/2026-10-07-sidecar-format-design.md` §2.3 `name` row (SqlClient 6.1 and later receive `json` and `vector(n)`, so the tool records them so; `varchar(max)` is what an older client sees); `docs/deferred/D-0006-multiple-result-sets-on-sql-server.md` "Delivered instead" (names `SqlServerSession`'s `columns` call, which describes the first result set, and says the manual's sentence comes with 6.3's page); `docs/manual/AGENTS.md` phase map row 6 (the SQL Server page says only a query's first result set is described and read into its model); `tests/SqlSource.Tests.SqlServer/README.md` if its procedures name the test classes; the epic row of 6.2 `Done`.

- [ ] **Step 1:** Write them.  Check user documentation names no internals (root rule 4).
- [ ] **Step 2:** `./pre-commit-validation.sh` passes.  Commit "Document the SQL Server columns and table match".

## Review Focus

1. **A query whose parameter is declared with a type the session cannot spell back** (an inferred CLR type, `hierarchyid`; an alias type `dbo.Email`): `columns`' `params` must bind.  Task 5 step 1 adds `DescribeAsync_ColumnsParams_DeclareEveryParameter` with an alias-typed and a CLR-typed inferred parameter; Task 7 adds `select-alias-and-clr` with a parameter on `email`.
2. **A query with no result set whose statement is an `OUTPUT ... INTO` or a `SELECT ... INTO`**: `none`, not a failure.  Task 3's `Columns_NoResultSet_HasNoRows` and Task 5's `DescribeAsync_NoRows_IsNone`.
3. **A view or table name holding `]`** in the `table` call's text.  Task 2 adds `TableSelect_NameWithBracket_DoublesIt`.
4. **A database whose collation is case-insensitive and a query that writes a table's name in another case**: origins come from the catalog, so the match still holds.  Task 7's `TableMatchTests` adds `Match_QueryNamesTableInOtherCase_Matches` (`SELECT * FROM DBO.USERS`).
5. **A run with a log whose describe fails at open** must not leave the listener enabled for the next database.  Task 6's `Open_RefusedConnection_WritesNoSecret` also asserts the listener is disposed.
