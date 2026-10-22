# Query generation, phase 6.5: the SQL Server end-to-end project - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `tests/SqlSource.Tests.SqlServer` becomes SQL Server's end-to-end project: the generator runs at build time over `mssql` queries of two databases, `app` and `billing`, with committed sidecars written by `tools/describe-end-to-end.sh`, proven by `--check`, a Dapper run of every model query and the error paths, on SQL Server 2025 and 2017; a second small project reads the type table on SqlClient 6.1.

**Architecture:** The project imports the package's props and targets by path, as `tests/SqlSource.Tests.Postgres` does, and `SqlServerContainer` gains `app` and `billing`, built from `EndToEnd/app/` and `EndToEnd/billing/` through the shared `EndToEndFiles`, which learns SQL Server's `GO`.  Queries live in `EndToEnd/Queries/`, claimed by types in `EndToEnd/`; their sidecars come from a SQL Server part of the script, which starts its own container and runs the tool on the project.  Tests compile against the generated models, so a missing or misshapen model fails the build.

**Tech Stack:** C# on `net10.0`, xunit v3, Shouldly, Testcontainers.MsSql 4.16.0, Microsoft.Data.SqlClient 7.1.1 and 6.1.7, Dapper 2.1.89, bash, Docker, `sqlcmd` of the image.

**Spec:** [`docs/superpowers/specs/2026-10-20-query-generation-phase-6-5-end-to-end-design.md`](../specs/2026-10-20-query-generation-phase-6-5-end-to-end-design.md), following [5.4's plan](2026-10-19-query-generation-phase-5-4-end-to-end.md) and the [epic](../specs/2026-10-07-query-generation-epic-design.md) ("Phase 6 - SQL Server", Testing).

## Global Constraints

- Every commit passes `./pre-commit-validation.sh` on the exact tree committed.  Every push also needs a run of `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/SqlSource.Tests.SqlServer` on that tree.
- One commit per step of the spec's order of work (the seven tasks below).  One pull request; this plan counts about 90 files, under the 150 limit.
- Images: the default `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04` is named only by `DefaultImages.SqlServer`; anything else reads that constant or, in a script, its line by `sed`.
- Every committed sidecar is written by `tools/describe-end-to-end.sh`, never by hand, and its diff is read before it is committed.
- No committed entry may differ between 2017 and 2025: a parameter the version differences touch (`DATEADD`'s count) is declared with `-- param:`.
- Every query writes its parameters in camelCase (`@accountId`, D-0004).
- Every Dapper test asserts the values of at least one row and the `NULL`s of another.  A test that writes runs in a transaction that is rolled back, and every Dapper call in it is given that `transaction` (SqlClient refuses a command without it).
- A 2025-only test skips below major 17 by `SqlServerContainer.SkipBelow(17, ...)`.
- `SQLSRC240` is in `WarningsNotAsErrors` of both projects that build over sidecars.
- A test project compiles no `.cs` file of another project; what two projects share is in `tests/SqlSource.Testing`, whose types stay `internal` and which references no driver.
- Test classes are `sealed`, names `Method_Scenario_Outcome`, assertions Shouldly's (`tests/AGENTS.md`).
- User documentation describes effects, never internals; the two packed readmes follow root `AGENTS.md` rule 4.
- A valid review finding, minor ones included, is fixed, or the owner is asked whether to record it as tech debt.

## Where this plan follows the code rather than the spec

1. **The splitter exists.**  6.3 wrote `SqlServerScripts` in `tests/SqlSource.Tests.SqlServer`, which splits a script at lines of `GO` alone and refuses `GO n`.  The SqlClient 6.1 project cannot compile it (`tests/AGENTS.md`), so its `Batches` moves into `tests/SqlSource.Testing` as the SQL Server rule of `EndToEndFiles`, and `SqlServerScripts` is deleted; its tests move with it, renamed.  `SqlSource.Testing` references no driver, so it runs a batch through `DbDataSource`; SqlClient's comes from `SqlClientFactory.Instance.CreateDataSource(connectionString)`.
2. **The project already references the tool and the generator** (`SqlSource.Tool.Testing`, generator with `ReferenceOutputAssembly="true"`), and already has `DescribeCommandTests` over the fixture project `SqlServer`.  `CheckTests` here is the real project's.
3. **Realistic cases are unclaimed.**  Importing the props makes every `.sql` of the project an `AdditionalFiles` item, `Realistic/Cases/**` and `Realistic/schema.sql` included.  An unclaimed file is skipped by the generator and the tool, so 6.6 claims them later; this plan adds no metadata for them.
4. **The map has one column.**  6.4 found no type that SqlClient 6.1.7 and 7.1.1 box differently, so the 6.1 project's models have the same types as the main project's; the project proves that 6.1 reads each one from the server.
5. **Dapper's behaviour decides the write side**, as in 5.4: the run, not a list in advance, says which types Dapper cannot send or sends lossily (a `DateTime` goes as `datetime` unless told otherwise, which rounds a `datetime2`).  Each is named with its reason in `WriteSideLeftOut`, proven by a test, and listed in the recipe.
6. **`DefaultImages.SqlServer`'s comment** says the script does not read it; Task 3 changes that.

## Calls the spec leaves open, settled as it recommends

1. The `json` and `vector` table is a 2025-only file and query, left out of 2017's `--check` by the positional filters.
2. A version difference in a parameter's type is answered by declaring the type with `-- param:`.
3. The SqlClient 6.1 project runs on the default image only, as the Npgsql 8 project does.

The pull request says so.

## Review Focus

1. **SQL Server 2017 with sidecars recorded on 2025.**  Any type or nullability that differs fails `--check` on the floor.  Every task that touches the container runs the project on both images before it commits, and Task 4's `CheckTests` runs on both.
2. **A property Dapper leaves at its default.**  Dapper fills a class model silently.  Every Dapper test compares whole rows, and `SqlServerAllTypesRows` fails on a property with no expectation and on an expectation with no property.
3. **Lossy writes.**  A value Dapper sends as another type (a `datetime2` sent as `datetime`, a `time` sent with less scale) comes back different, not as an error.  The write test compares the row read back with row 1 exactly, and a left-out column's own test proves its reason, a throw or a different value.
4. **The 6.1 project pulling SqlClient 7.**  A test asserts the loaded `Microsoft.Data.SqlClient` is 6.1 (its `AssemblyInformationalVersionAttribute` starts with `6.1.`), and its lock file holds 6.1.7 alone.
5. **A database left changed.**  Writes roll back; the error paths make and drop their table in a `finally`; `SqlServerContainerTests` checks the seed's counts after the run's other tests could have written.

---

## The files

| Path | What |
|----|----|
| `tests/SqlSource.Testing/EndToEndFiles.cs` | Two engines: folder and batch rule; `GO` splitting |
| `tests/SqlSource.Testing/EndToEnd/SqlServerAllTypesRows.cs` | The rows of `dbo.all_types` and `dbo.new_types`, shared by both SQL Server projects |
| `tests/SqlSource.Testing/DefaultImages.cs`, `SqlSource.Testing.csproj` | Comment; `InternalsVisibleTo` the 6.1 project |
| `tests/SqlSource.Tests.Postgres/PostgresContainer.cs`, `tests/SqlSource.Tests.Postgres.Npgsql8/AppContainer.cs` | Callers of `EndToEndFiles` |
| `tests/SqlSource.Tests.SqlServer/SqlSource.Tests.SqlServer.csproj`, `packages.lock.json` | Imports, settings, Dapper, `ErrorPaths` linked |
| `tests/SqlSource.Tests.SqlServer/SqlServerContainer.cs`, `RepositoryPaths.cs`, `SqlServerContainerTests.cs` | `app`, `billing`, `new-types.sql` on 17+ |
| `tests/SqlSource.Tests.SqlServer/SqlServerScripts*.cs` | Deleted; tests become `EndToEndBatchesTests.cs` |
| `tests/SqlSource.Tests.SqlServer/EndToEnd/app/`, `billing/` | `schema.sql`, `seed.sql`; `app/new-types.sql` |
| `tests/SqlSource.Tests.SqlServer/EndToEnd/Queries/` | `Accounts`, `AllTypes`, `Constants`, `Notes`, `Shapes`, `Writes`, `NewTypes`, `Billing/Invoices` `.sql` and sidecars |
| `tests/SqlSource.Tests.SqlServer/EndToEnd/*Queries.cs`, `Reporting/AccountReports.cs` | The claiming types |
| `tests/SqlSource.Tests.SqlServer/EndToEnd/Types/` | `Email`, `EmailHandler`, handlers the run needs, `TypedParameter` |
| `tests/SqlSource.Tests.SqlServer/EndToEnd/EndToEndSources.cs` | Dapper's settings and the connections |
| `tests/SqlSource.Tests.SqlServer/EndToEnd/*Tests.cs` | `ModelShapeTests`, `CheckTests`, `ErrorPathTests`, one Dapper test class per query class, `DapperRecipeTests` |
| `tests/SqlSource.Tests.SqlServer/EndToEnd/ErrorPaths/` | `App.csproj`, `Queries.cs`, `Widgets.sql` |
| `tests/SqlSource.Tests.SqlServer.SqlClient61/` | `.csproj`, `packages.lock.json`, `AllTypesQueries.cs`, `AppContainer.cs`, `AllTypesQueryTests.cs` |
| `SqlSource.slnx` | The new project |
| `tools/describe-end-to-end.sh` | A function per engine |
| Documents | Task 7 |

---

### Task 1: The project runs the generator

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 6.5: `In progress`)
- Modify: `tests/SqlSource.Tests.SqlServer/SqlSource.Tests.SqlServer.csproj`, `packages.lock.json`

**Interfaces:**
- Produces: a project whose build runs the generator with `SqlSourceDialect` `mssql`, `SqlSourceDatabase` `app`, `SqlSourceOutput` `Models`, `SqlSourceInputModelSuffix` `Args`, `SqlSourceInputModelType` `record`, `SqlSourceTypeOverrides` `date=System.DateOnly`; Dapper referenced; `EndToEnd/ErrorPaths/**` removed from `Compile`, `AdditionalFiles` and `None` and linked to the output as `Fixtures/Projects/ErrorPaths/` (the folder is made in Task 4; the items are harmless while it is empty).

- [ ] **Step 1:** Import `../../src/SqlSource/build/SqlSource.props` first and `SqlSource.targets` last, by the PostgreSQL project's comments; add the properties above, each with a one-line comment, and `<WarningsNotAsErrors>$(WarningsNotAsErrors);SQLSRC240</WarningsNotAsErrors>`; add `<PackageReference Include="Dapper" />`; add the `ErrorPaths` items.
- [ ] **Step 2:** Add the `AdditionalFiles Update="EndToEnd/Queries/Billing/**/*.sql"` item with `SqlSourceOutputModelType="sealed class"`, `SqlSourceOutputModelSuffix="Row"`, `SqlSourceTypeOverrides="date=default"` (the folder's dates are `DateTime` again).
- [ ] **Step 3:** `dotnet build tests/SqlSource.Tests.SqlServer` succeeds with no SQLSRC diagnostic (the realistic cases are unclaimed); `dotnet restore --force-evaluate` updated the lock file.
- [ ] **Step 4:** Epic row 6.5 Status `In progress`.  Run `./pre-commit-validation.sh`; commit this plan with it, `Run the generator in SQL Server's test project`.

### Task 2: `EndToEndFiles` for two engines, and the two databases

**Files:**
- Modify: `tests/SqlSource.Testing/EndToEndFiles.cs`; `tests/SqlSource.Tests.Postgres/PostgresContainer.cs`; `tests/SqlSource.Tests.Postgres.Npgsql8/AppContainer.cs`
- Create: `tests/SqlSource.Tests.SqlServer/EndToEnd/app/schema.sql`, `seed.sql`, `new-types.sql`; `EndToEnd/billing/schema.sql`, `seed.sql`
- Modify: `tests/SqlSource.Tests.SqlServer/SqlServerContainer.cs`, `RepositoryPaths.cs`, `SqlServerContainerTests.cs`, `Realistic/RealisticDatabase.cs` (and any other caller of `SqlServerScripts`)
- Delete: `tests/SqlSource.Tests.SqlServer/SqlServerScripts.cs`; rename `SqlServerScriptsTests.cs` to `EndToEndBatchesTests.cs`

**Interfaces:**
- Produces, in `SqlSource.Testing`:
  - `internal sealed record EndToEndEngine(string Folder, Func<string, IReadOnlyList<string>> Batches)` with `static EndToEndEngine Postgres` (folder `tests/SqlSource.Tests.Postgres/EndToEnd`, the whole text as one batch) and `static EndToEndEngine SqlServer` (folder `tests/SqlSource.Tests.SqlServer/EndToEnd`, `SqlServerBatches`).
  - `EndToEndFiles.Schema(EndToEndEngine engine, string database)`, `Seed(...)`, `RunAsync(DbDataSource source, string path, EndToEndEngine engine, CancellationToken)`, `CreateAsync(DbDataSource server, EndToEndEngine engine, string database, Func<string, DbDataSource> sourceOf, IReadOnlyList<string> files, CancellationToken)` and the overload without `files` (schema then seed).
  - `EndToEndFiles.SqlServerBatches(string script)`: 6.3's `Batches`, unchanged in behaviour.
- Produces, in the SQL Server project: `SqlServerContainer.AppDatabase = "app"`, `BillingDatabase = "billing"`, `EndToEndDatabases`, and `RepositoryPaths.EndToEnd`, `EndToEndProject` (the `.csproj`), `NewTypesFile`.  `SqlServerContainer` keeps applying `fixtures` and `realistic` through `EndToEndFiles.RunAsync(..., EndToEndEngine.SqlServer, ...)`.

- [ ] **Step 1: Move the splitter's tests** into `EndToEndBatchesTests` (calling `EndToEndFiles.SqlServerBatches`) and add the spec's cases:
  - `SqlServerBatches_GoInsideAString_DoesNotSplit`: `"SELECT 'a\nGO here';\n"` is one batch.
  - `SqlServerBatches_GoInALineComment_DoesNotSplit`: `"SELECT 1; -- GO\nSELECT 2;\n"` is one batch.
  - `SqlServerBatches_LineOfGoAloneInsideABlockComment_Splits`: `"/*\nGO\n*/"` gives two batches, the known limit `sqlcmd` shares; the test's comment says so.
- [ ] **Step 2:** Run them; they fail to compile.  Implement the record and the methods; delete `SqlServerScripts`; move every caller.  `dotnet test --project tests/SqlSource.Tests.SqlServer --filter-class *EndToEndBatchesTests` passes.
- [ ] **Step 3: Write the schemas and seeds.**  Every batch of `app/schema.sql` runs on 2017.  `app` (all in `dbo`):
  - `CREATE TYPE dbo.Email FROM nvarchar(320) NOT NULL`.
  - `accounts`: `id int IDENTITY PRIMARY KEY`, `name nvarchar(100) NOT NULL`, `email dbo.Email NOT NULL`, `status varchar(20) NOT NULL DEFAULT 'active'`, `opened_on date NOT NULL`, `created_at datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET()`, `closed_at datetimeoffset NULL`, `name_length AS LEN(name)`.
  - `notes`: `id int IDENTITY PRIMARY KEY`, `account_id int NOT NULL REFERENCES accounts (id)`, `kind varchar(10) NOT NULL`, `body nvarchar(max) NOT NULL`, `written_at datetime2(3) NOT NULL`, `remind_at time(0) NULL`.
  - `all_types`: `id int PRIMARY KEY` and one nullable column per row of the base map that 2017 has, named `<type>_value`: `bigint`, `int`, `smallint`, `tinyint`, `bit`, `decimal(12,4)`, `numeric(9,2)`, `money`, `smallmoney`, `float`, `real`, `date`, `datetime`, `datetime2(7)`, `smalldatetime`, `time(7)`, `datetimeoffset(7)`, `char(5)`, `varchar(20)`, `text`, `nchar(5)`, `nvarchar(20)`, `ntext`, `xml`, `binary(4)`, `varbinary(8)`, `image`, `rowversion`, `uniqueidentifier`, `sql_variant`, `sysname NULL`.  A header comment names what is left out and why (`json`, `vector`: `new-types.sql`; `hierarchyid`, `geography`, `geometry`: no C# type without Microsoft.SqlServer.Types).
  - `new-types.sql` (2025 only): `new_types (id int PRIMARY KEY, json_value json NULL, vector_value vector(3) NULL)` and its two rows.
  - `billing`: `CREATE SCHEMA billing`, then `billing.invoices (id int PRIMARY KEY, account_id int NOT NULL, number varchar(20) NOT NULL, amount decimal(12,2) NOT NULL, issued_on date NOT NULL, paid_at datetime2(0) NULL)`; seed: PostgreSQL's three invoices, the third unpaid.
  - `app/seed.sql`: PostgreSQL's accounts alice (active, 2 notes), bob (closed, `closed_at` set, 1 note), carol (suspended, no note) under `SET IDENTITY_INSERT`, with every value written out; notes with `remind_at` set on one and `NULL` on the others; `all_types` row 1 with a value in every column, chosen to show the range and exact in its type (binary fractions for `float`/`real`; a `datetime` ending `.003`; a `smalldatetime` at a whole minute; `varchar` text within code page 1252; `sql_variant` an `int`), and row 2 with `NULL` everywhere but `id` (and `rowversion`, which the server fills; the header says so).
- [ ] **Step 4: The container.**  `SqlServerContainer.InitializeAsync` creates `app` and `billing` beside the others, applies each database's schema then seed, and `new-types.sql` to `app` when `Major >= 17` (read before the end-to-end databases are built).  A comment names the order `tools/describe-end-to-end.sh` repeats.
- [ ] **Step 5: Container tests** in `SqlServerContainerTests` (collection `UsesSqlServer`): `InitializeAsync_EndToEnd_SeedsEachTable` asserts row counts `accounts` 3, `notes` 3, `all_types` 2, `billing.invoices` 3; `InitializeAsync_NewTypes_OnlyOn2025` asserts `OBJECT_ID('dbo.new_types')` is set exactly when `Major >= 17`.
- [ ] **Step 6:** Run both projects of PostgreSQL and the SQL Server project on both SQL Server images; all pass.  `./pre-commit-validation.sh`; commit `Build SQL Server's end-to-end databases through EndToEndFiles`.

### Task 3: The queries, the classes and the script

**Files:**
- Create: `EndToEnd/Queries/*.sql` (table below), their sidecars by the script; `EndToEnd/*Queries.cs`, `Reporting/AccountReports.cs`; `EndToEnd/Types/Email.cs`, `EmailHandler.cs`; `EndToEnd/ModelShapeTests.cs`
- Modify: `tools/describe-end-to-end.sh`; `tests/SqlSource.Testing/DefaultImages.cs` (comment)

**Interfaces:**
- Produces: namespace `SqlSource.Tests.SqlServer.EndToEnd`; the classes and the generated model names the Dapper tests use (`AccountsDto`, `GetAccountDto`, `GetAccountArgs`, `AccountName`, `BillingInvoicesRow`, `GetInvoiceRow`, `AllTypesDto`, `InsertAllTypesArgs`, `NewTypesDto`, ...), as the generator names them from the table.
- Produces: `tools/describe-end-to-end.sh` with functions `describe_postgres` and `describe_sqlserver`, run in that order, each starting and removing its own container.

| File | Class | Covers |
|----|----|----|
| `Accounts.sql` | `AccountQueries` (Nested, project defaults); `Reporting.AccountReports` (Direct, a second namespace) | `GetAccount` (columns in another order); `ListAccounts` (`SELECT *`, the table model `AccountsDto`, no `dbo`); `ListAccountsInTableOrder`; `ListAccountsUntabled` (`no-table-models`); `GetAccountName` and `FindAccountName` sharing `-- output-model: AccountName`, the second by `@email` of the alias type; `ListClosedBefore` with `-- param: @closedBefore datetimeoffset null`; `ListOpenedWithin` with `-- param: @days int` in `DATEADD(day, -@days, @asOf)` and `-- param: @asOf date` |
| `AllTypes.sql` | `AllTypesQueries` (Direct) | `ListAllTypes` (`SELECT *`) and `InsertAllTypes` (`INSERT ... OUTPUT INSERTED.* VALUES`, one parameter per column Dapper can send), each with `-- type-override: date = default` |
| `NewTypes.sql` | `NewTypeQueries` (Direct) | `ListNewTypes` over `dbo.new_types`; its header says it is described on 2025 alone |
| `Constants.sql` | `ConstantQueries` (Direct) | `-- output: sql`; `CountAccounts`; no sidecar |
| `Billing/Invoices.sql` | `InvoiceQueries` (Direct) | `-- database: billing`; `-- model-namespace: SqlSource.Tests.SqlServer.EndToEnd.Billing`; `ListInvoices` (`BillingInvoicesRow`); `GetInvoice` |
| `Notes.sql` | `NoteQueries` (Direct, `Output = GeneratorOutput.CodeGen`) | Header `-- type-override: dbo.Email = SqlSource.Tests.SqlServer.EndToEnd.Types.Email` and `time = System.TimeOnly`; `ListAccountNotes` (outer join, with `n.remind_at`); `ListNoteKinds` (`n.kind`, `a.email`); `CountAccountNotes` with `-- column: note_count not null`; `ListMarkedNames` with `a.id AS [id?]` and `UPPER(a.name) AS "upper_name!"`; `ListReminders` with `-- type-override: sys.time = default` |
| `Shapes.sql` | `ShapeQueries` (Direct, record output, suffix `View`, sealed class input, `ModelNamespace`, `MethodLocation.Public`, `CollectionType.List`) | PostgreSQL's four queries in T-SQL: `SearchAccounts -> many` with `sort-input`; `ListAccountColumns` with `sort-output` and class output; `GetHeadline` with a model in the namespace; `LookUpAccount` with an input model of another namespace |
| `Writes.sql` | `WriteQueries` (Direct) | `AddNote` (`INSERT ... OUTPUT INSERTED.id, INSERTED.kind`), `RenameAccount` (`UPDATE ... OUTPUT DELETED.name AS old_name, INSERTED.name AS new_name`), `MergeAccountStatus` (`MERGE ... OUTPUT $action AS change, INSERTED.id`) |

- [ ] **Step 1: The script.**  Wrap today's body in `describe_postgres`.  Add `describe_sqlserver`: read `DefaultImages.SqlServer` by `sed -n 's/.*const string SqlServer = "\(.*\)";.*/\1/p'`, failing when empty; `docker run -d --platform linux/amd64 -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=$SQLSERVER_PASSWORD" -p 127.0.0.1::1433`; remove the container on exit (one `trap` that removes both); wait up to 120 s for `sqlcmd ... -Q 'SELECT 1'`; `sqlcmd` is `/opt/mssql-tools18/bin/sqlcmd` with `-C -I -b -S localhost -U sa`; create `app` and `billing`, apply `schema.sql` then `seed.sql` of each, then `app/new-types.sql`, in `SqlServerContainer`'s order; delete `tests/SqlSource.Tests.SqlServer/EndToEnd/Queries/**/*.sql.json`; `dotnet run --project src/SqlSource.Tool -- describe --force tests/SqlSource.Tests.SqlServer/SqlSource.Tests.SqlServer.csproj --connection "app=Server=127.0.0.1,$port;User ID=sa;Password=...;TrustServerCertificate=True;Database=app" --connection "billing=..."`.  The header comment says both parts; `tools/shellcheck.sh` and `tools/shfmt.sh --check` pass.
- [ ] **Step 2:** Write the `.sql` files, the classes (each with the one-line comment of what it shows, as PostgreSQL's), `Types/Email.cs` (`internal readonly record struct Email(string Value)`) and `EmailHandler`.  Run `tools/describe-end-to-end.sh`; read every sidecar's diff (PostgreSQL's must be unchanged but for nothing; SQL Server's new); `dotnet build` passes.
- [ ] **Step 3: `ModelShapeTests`** (no server), by PostgreSQL's: the attribute is declared; `AccountsDto` is the table model with `OpenedOn` a `DateOnly` (property override), `CreatedAt` a `DateTimeOffset`, `ClosedAt` `DateTimeOffset?`, `NameLength` `int?`, and no `DboAccountsDto`, `ListAccountsDto` or `ListAccountsInTableOrderDto`; `AccountName` shared; `ListClosedBeforeArgs.ClosedBefore` is `DateTimeOffset?`; `ListOpenedWithinArgs` has `Days` `int` and `AsOf` `DateOnly`; `Reporting` has its own models; `BillingInvoicesRow` is a sealed class with `IssuedOn` a `DateTime` (metadata override) in `...EndToEnd.Billing`; `ListAccountNotesDto` has nullable note columns; `CountAccountNotesDto.NoteCount` is `int`; `ListMarkedNamesDto(int? Id, string UpperName)`; `ListNoteKindsDto.Email` is `Email` (file override); `ListAccountNotesDto.RemindAt` is `TimeOnly?` (file override) and `ListRemindersDto.RemindAt` `TimeSpan?` (`sys.time = default` in the query); `AllTypesDto`'s every property by the 6.4 map with `DateValue` `DateTime?`; `NewTypesDto.VectorValue` is `SqlVector<float>?` and `JsonValue` `string?`; `ConstantQueries.CountAccounts` exists and `Constants.sql.json` does not; the shapes of `ShapeQueries`; `WriteQueries`' models.
- [ ] **Step 4:** Run `ModelShapeTests`; pass.  Update `DefaultImages.SqlServer`'s comment.  `./pre-commit-validation.sh`; commit `Add SQL Server's end-to-end queries and their sidecars`.

### Task 4: `--check` and the error paths

**Files:**
- Create: `EndToEnd/CheckTests.cs`, `EndToEnd/ErrorPathTests.cs`, `EndToEnd/ErrorPaths/App.csproj`, `Queries.cs`, `Widgets.sql`

**Interfaces:**
- Consumes: `RepositoryPaths.EndToEnd`, `EndToEndProject`, `NewTypesFile`; `SqlServerContainer.ConnectionStringOf`; `CliRun.Real` with `ReleasedDescribers = true`, as `Describer/DescribeCommandTests` uses it; `FixtureProjects`.

- [ ] **Step 1: `CheckTests.Check_Project_EveryEntryIsTheSame`.**  Count committed entries by database from the sidecars under `EndToEnd/`.  On major 17 or later: one `describe --check <project> --connection app=... --connection billing=...`, exit 0, output exactly `"app (mssql): {app} same, 0 differ, 0 failed\nbilling (mssql): {billing} same, 0 differ, 0 failed\n"`.  Below 17: the 2025-only file left out by the positional filters, every other claimed `.sql` of `EndToEnd/Queries/` named, its counts from those files' sidecars, `NewTypes.sql`'s entries shown to be more than 0 and absent from the count.
- [ ] **Step 2: The error paths.**  `App.csproj` is PostgreSQL's with `SqlSourceDialect` `mssql` and `SqlSourceDatabase` `errors`; `Widgets.sql` is `-- name: GetWidgets` / `SELECT id, name FROM dbo.error_path_widgets;`.  `ErrorPathTests` makes `dbo.error_path_widgets (id int PRIMARY KEY, name nvarchar(50) NOT NULL)` on `fixtures` and drops it in a `finally`: `Check_QueryEditedAfterDescribe_IsOutOfDate` and `Check_ColumnAddedAfterDescribe_IsOutOfDate` exit 2 with `SQLSRC219`; `Describe_QueryTheServerRefuses_ReportsItAndWritesNoSidecar` exits 1 with `SQLSRC225` and no `Refused.sql.json`.
- [ ] **Step 3:** Run the project on both images; pass.  `./pre-commit-validation.sh`; commit `Check SQL Server's end-to-end sidecars and its error paths`.

### Task 5: The Dapper tests

**Files:**
- Create: `EndToEnd/EndToEndSources.cs`; `AccountQueryTests`, `InvoiceQueryTests`, `NoteQueryTests`, `ShapeQueryTests`, `WriteQueryTests`, `AllTypesQueryTests`, `NewTypesQueryTests`, `DapperRecipeTests`; `Types/TypedParameter.cs` and the handlers the run needs
- Create: `tests/SqlSource.Testing/EndToEnd/SqlServerAllTypesRows.cs`

**Interfaces:**
- Produces: `EndToEndSources.App(SqlServerContainer)` and `Billing(...)` returning an open-ready `SqlConnection`; its static constructor sets `DefaultTypeMap.MatchNamesWithUnderscores` and the handlers the recipe tells a project to add (`EmailHandler`, and one for `DateOnly` / `TimeOnly` if Dapper 2.1.89 does not read them from SqlClient's `DateTime` / `TimeSpan`).
- Produces: `SqlServerAllTypesRows.FullRowId = 1`, `NullRowId = 2`, `ShouldBeTheFullRow(object row, int id = 1, IReadOnlySet<string>? nullProperties = null)`, `ShouldBeTheNullRow(object row)`, `ShouldBeTheFullNewTypesRow(object row)`, `ShouldBeTheNullNewTypesRow(object row)`.  By reflection, by property name, failing on a property with no expectation and an expectation with no property; `RowversionValue` is 8 bytes, not compared by value; a `SqlVector<float>` is compared through its `Memory` (`ReadOnlyMemory<float>`), so the library needs no SqlClient.

- [ ] **Step 1:** Each query class's test by PostgreSQL's pattern: `QuerySingleAsync`/`QueryAsync` with the input model, whole rows compared, a `NULL` row asserted (carol's `closed_at`, carol's missing note, invoice 3's `paid_at`, a note's `remind_at`).  `ListMarkedNames` is read by ordinal with `GetFieldValueAsync`, with `GetName(0)` `id?` and `GetName(1)` `upper_name!`.  `ListOpenedWithin` gives the accounts of a fixed `@asOf` and `@days`.  `WriteQueryTests` runs each of the three writes in a rolled-back transaction, passes `transaction` to every call, and asserts the output row, then that the seed is as it was after the rollback.
- [ ] **Step 2: `AllTypesQueryTests`.**  `QueryAsync_ListAllTypes_ReadsBothRows`; `QuerySingleAsync_InsertAllTypes_ReturnsTheValuesWritten` (args from row 1, `id` 3, rolled back, `nullProperties` the left-out ones); `WriteSideLeftOut` with each left-out property and the reason the run found, `rowversion` among them; a theory `ExecuteAsync_LeftOutColumnAlone_FailsToWriteItsValue` proving each reason (a throw of the expected type, or the value read back differing from row 1); `InsertAllTypes_WriteSide_LeavesOutOnlyTheListedTypes`.
- [ ] **Step 3: `NewTypesQueryTests`** (`SkipBelow(17, "SQL Server before 2025 has no json or vector.")`): both rows of `new_types` read into `NewTypesDto`.
- [ ] **Step 4: `DapperRecipeTests`**: each remedy the recipe will give, proven: a parameter Dapper cannot send goes as `TypedParameter` (a `SqlMapper.ICustomQueryParameter` adding a `SqlParameter` with its `SqlDbType`), at least `vector` if Dapper refuses `SqlVector<float>`; a `datetime2` written exactly by the remedy the run found (`SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2)` or a typed parameter); a type-override type read through its handler; a `[name!]` alias not matched by Dapper.  `Types/TypedParameter.cs` is the recipe's sample, byte for byte but for the namespace.
- [ ] **Step 5:** Run the project on both images (the 2025-only tests skip on 2017); pass.  `./pre-commit-validation.sh`; commit `Read every SQL Server end-to-end query through Dapper`.

### Task 6: `tests/SqlSource.Tests.SqlServer.SqlClient61`

**Files:**
- Create: `tests/SqlSource.Tests.SqlServer.SqlClient61/SqlSource.Tests.SqlServer.SqlClient61.csproj`, `packages.lock.json`, `AllTypesQueries.cs`, `AppContainer.cs`, `AllTypesQueryTests.cs`
- Modify: `SqlSource.slnx`; `tests/SqlSource.Testing/SqlSource.Testing.csproj` (`InternalsVisibleTo`)

**Interfaces:**
- Consumes: `EndToEndFiles` with `EndToEndEngine.SqlServer`; `SqlServerAllTypesRows`; `DefaultImages.SqlServer`.

- [ ] **Step 1:** The `.csproj` by the Npgsql 8 project's: props and targets imported, `mssql`, `Models`, `SQLSRC240` not an error; `AdditionalFiles` linking `AllTypes.sql`, `NewTypes.sql` and their sidecars from `../SqlSource.Tests.SqlServer/EndToEnd/Queries/` as `Queries/`; `Microsoft.Data.SqlClient` with `VersionOverride="6.1.7"` and a comment why; Dapper, Testcontainers.MsSql, xunit, Shouldly, the logger and coverage; the generator as an analyzer with `ReferenceOutputAssembly="false"`; `SqlSource.Testing`.  Nothing that brings SqlClient 7.  `AllTypesQueries` claims both files with `Path` relative to the project.  Restore writes the lock file; it holds `Microsoft.Data.SqlClient` 6.1.7 alone.
- [ ] **Step 2:** `AppContainer` (`IAsyncLifetime`, `MsSqlBuilder(DefaultImages.SqlServer)`) creates `app` and applies its schema, its seed and `new-types.sql` through `EndToEndFiles` over `SqlClientFactory.Instance.CreateDataSource(...)`.
- [ ] **Step 3: Tests**: `SqlClient_Loaded_IsVersion61` (informational version starts with `6.1.`); `Build_AllTypes_HasTheSameTypesAsSqlClient7` (the property types of `AllTypesDto` and `NewTypesDto` as `ModelShapeTests` asserts them); `QueryAsync_ListAllTypes_ReadsBothRows`; `QueryAsync_ListNewTypes_ReadsBothRows`.
- [ ] **Step 4:** Add the project to `SqlSource.slnx` under `/tests/`.  `dotnet test --project tests/SqlSource.Tests.SqlServer.SqlClient61` passes.  `./pre-commit-validation.sh`; commit `Read the SQL Server type table on SqlClient 6.1`.

### Task 7: The documents, and the row set to Done

**Files:**
- Modify: `tests/SqlSource.Tests.SqlServer/README.md` (an "end-to-end project" section with "writing the sidecars", the databases, the files of `EndToEnd/`, the 6.1 project), `tests/README.md` (the 6.1 project beside the Npgsql 8 one; the project table), `tests/AGENTS.md` (the SQL Server end-to-end rules, mirroring PostgreSQL's bullet, and the 6.1 project's), `tools/README.md` and `tools/AGENTS.md` (the script's SQL Server part, its `sed` of `SqlServer`, the order of files), `CONTRIBUTING.md` (the 6.1 project in one sentence, the script for both engines), root `AGENTS.md` rule 5 (moving SQL Server's default image runs `tools/describe-end-to-end.sh` for the end-to-end sidecars), `docs/manual/recipes.md` (Dapper with SQL Server: what the run found, the `TypedParameter` sample for SqlClient, the parameters Dapper cannot send from `WriteSideLeftOut`, the transaction rule), `docs/manual/sqlserver.md` (only what a user must know that the run found), `docs/architecture/` pages that name `SqlServerScripts` or the script, the epic row 6.5 (`Done`, its Delivers text)

- [ ] **Step 1:** Write each change.  Every AGENTS.md that names a file keeps its maintenance footer.  Grep the docs for `SqlServerScripts`, `describe-end-to-end.sh` and `Npgsql 8` and fix every stale sentence.
- [ ] **Step 2:** `dotnet test --project tests/SqlSource.Tests --filter-class *UserDocs*` passes (links and `sql mssql` samples).
- [ ] **Step 3:** `./pre-commit-validation.sh`; commit `Document SQL Server's end-to-end project and close 6.5`.
