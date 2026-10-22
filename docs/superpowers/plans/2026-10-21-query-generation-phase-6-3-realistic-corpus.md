# Query generation phase 6.3, the realistic T-SQL corpus and the describer's close - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A corpus of 56 realistic T-SQL cases with a hand-written truth proves the SQL Server describer's nullability on 2017 and 2025, and the describer is then registered in `ToolHost.Create`, so the released tool describes SQL Server.

**Architecture:** What PostgreSQL's realistic suite has that is engine-neutral (the case record, the reading of `case.json` and its rules, the checks of samples against parameters, the comparison of a description with its truth, the rendering of a failure, the arithmetic of the noise budget) moves into `tests/SqlSource.Tool.Testing/Realistic/`, where both container projects compile it.  `tests/SqlSource.Tests.SqlServer/Realistic/` holds the corpus, its database `realistic` and its checks; its oracle binds each sample through `sp_executesql` at the described types, in a transaction it rolls back.  Part 2 registers `SqlServerDescriber` and documents describing SQL Server, and, since 6.4 merged first, that a user gets SQL Server models.

**Tech Stack:** C# (.NET 10 tests), Microsoft.Data.SqlClient 7.1.1, xUnit v3, Shouldly, Testcontainers (SQL Server 2025-CU9-GDR1 default, 2017-CU31-GDR19 floor).

**Spec:** `docs/superpowers/specs/2026-10-20-query-generation-phase-6-3-realistic-corpus-design.md`, with the epic's "Phase 6 - SQL Server", the spike `docs/superpowers/specs/2026-10-20-query-generation-phase-6-sql-server-spike.md`, PostgreSQL's suite `docs/superpowers/specs/2026-10-15-postgresql-realistic-query-suite-design.md`, and `/Users/michael/.claude/implementation-manager/mbcrawfo-SqlSource/query-gen-phase-6/notes.md`.

## Global Constraints

- Root `AGENTS.md` rules hold over this plan and any skill.  Every commit and every push passes `./pre-commit-validation.sh` on the exact tree.  After a change to the SQL Server tests, also run `tests/SqlSource.Tests.SqlServer` with `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`; after a change to PostgreSQL's realistic code, also run `tests/SqlSource.Tests.Postgres` with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.  Minor review findings are fixed, never deferred.
- Read `tests/AGENTS.md`, `src/SqlSource.Tool/AGENTS.md`, `docs/manual/AGENTS.md` and the `AGENTS.md` of every folder touched before editing it.
- **A truth is never written from what the describer answers.**  Each column's `nullable` and `why` are reasoned from `query.sql` and `Realistic/schema.sql`; the checks then compare.  A subagent writing cases is told this and must not run the describer to fill a truth in.  When a check disagrees with a truth, the truth is re-reasoned from the SQL, and changed only when the reasoning, not the describer, says it was wrong.
- **Queries a T-SQL developer would write.**  No query is written to make a point of PostgreSQL's; no toy query exercising one feature alone.
- A sample is a T-SQL expression for each parameter by name without `@`: `{ "status": "N'active'" }`.  `"NULL"` only for a parameter whose `-- param:` says `null`.
- `minimumMajor` and `checkedFrom` take SQL Server's majors: 14 (2017) and 17 (2025) are `SqlServerContainer.RecordedMajors`.  A case of 2022 syntax has `minimumMajor: 16`, so it runs on 17 alone.
- No expectation of the description is recorded (no `query.sql.json`, no `expected-*.txt`): 6.6 records the sidecars.  A description that differs between 2017 and 2025 is allowed.
- Every schema and seed batch runs on 2017.  What 2017 lacks (a `json` column) is made by a batch that runs only on 17 or later.
- Failures the describer gives keep their ids.  A case refused with another id than the one it expects goes to the owner (see "Questions for the owner").
- The describer is corrected only for a proven wrong `false`, by the narrowest rule keyed on the server's answer and the catalog, with a fixture of its own and a `nullableSource` value naming the rule.  One that needs an analysis of the query stops the work and goes to the owner.
- Shared files (`docs/manual/diagnostics.md`, `docs/manual/sqlserver.md`, `docs/manual/README.md`, `README.md`, `docs/architecture/`) change only in this unit's sections.
- Pull requests: 150 files or fewer each; each starts by counting its files.

## Where the code differs from the spec, and the calls this plan makes

1. **The shared parts go to `tests/SqlSource.Tool.Testing`, not `tests/SqlSource.Testing`.**  The case record converts itself to a `FixtureCase` (`AsFixture`), which is in `tests/SqlSource.Tool.Testing/Fixtures/`; it skips through `Assert.SkipUnless`, and `RenderFailure` reads the tool's `DescribeFailure`.  `tests/SqlSource.Testing` has neither the tool nor `xunit.v3.assert`, and must not gain the tool (RoslynFloor references it).  Both container projects already reference `SqlSource.Tool.Testing`.  Their tests go to `tests/SqlSource.Tool.Tests/Realistic/`, mirroring the library's folder as `Fixtures/` does.
2. **The case record gains its engine.**  `RealisticCase` takes a `RealisticEngine` (its sidecar engine name, its product name for messages, its recorded majors), so `AsFixture`, `SkipUnlessRunsOn`'s message and `checkedFrom`'s rule work for both.  PostgreSQL's members `models`, `checkedFrom` and `dapper` stay in the shared record: 6.6 uses them for SQL Server.
3. **SQL Server's case folders hold `query.sql` and `case.json` alone in 6.3.**  Its folder test checks those two files; 6.6 changes it to `RealisticCase.Files` when it adds the sidecars.  No case of 6.3 has `models`, `checkedFrom` or `dapper`; the two-columns-named-`id` case is 6.6's.
4. **The noise budget's test lands in part 1** with the budget of part 1's cases, and part 2 raises it, as "a change that adds cases" may.  The spec puts "the budget" in part 2; a check that runs in part 1 needs a number.
5. **The oracle declares sample values as variables.**  The batch is `DECLARE @sample_0 <type> = (<sample>); ...; EXEC sys.sp_executesql @statement, @declarations, @a = @sample_0, ...`, with `@statement` and `@declarations` sent as `nvarchar(max)` parameters of the command.  A sample may so be an expression (`DATEADD(day, -1, SYSUTCDATETIME())`) where `EXEC` takes only constants.  The types are `SqlServerTypes.Declared` of each described parameter.
6. **Not decided with the owner, settled as the spec recommends** (the code shows no reason not to; the PRs list them): no expectation recorded here; samples bound through `sp_executesql` at the described types; registration at the end of 6.3; the noise measured and not reduced.
7. **6.4 merged first** (notes.md), so part 2 writes the statements that a user gets SQL Server models: `docs/manual/README.md` "What works today", `README.md`, `docs/manual/generator.md` and `docs/manual/settings.md`'s Status cells, and removes the manual's advice to mssql users to set output to `sql`.  Re-check `gh pr list -R mbcrawfo/SqlSource --state merged` before each push.
8. **Pending notes from 6.1 and 6.2 go into `docs/manual/sqlserver.md`** in part 2: a parameter used once alone in `@p IS NULL` is typed `int`; only a query's first result set is described (D-0006); the phase map rows of `docs/manual/AGENTS.md` that ask for them are then done.

## File count

| PR | Group | Files |
|----|----|----|
| 1 | Plan, epic row | 2 |
| 1 | `tests/SqlSource.Tool.Testing/Realistic/` (5) and `tests/SqlSource.Tool.Tests/Realistic/` tests (4) | 9 |
| 1 | PostgreSQL realistic code changed or removed (about 17 of `Realistic/` and `EndToEnd/CheckTests.cs`) | 18 |
| 1 | SQL Server: container, paths, 10 classes of `Realistic/`, `schema.sql`, `seed.sql` | 14 |
| 1 | 28 cases, 2 files each | 56 |
| 1 | `.gitattributes`, `tests/AGENTS.md`, `tests/README.md`, both container READMEs | 5 |
| 1 | **Total** | **about 104** |
| 2 | 28 cases | 56 |
| 2 | Registration: `ToolHost.cs`, `ToolHostTests`, SQL Server's `DescribeCommandTests` and `ToolProcessTests`, a fixture project, `tools/check-tool-install.sh` | about 8 |
| 2 | User docs (tool readme, `README.md`, manual `README.md`, `tool.md`, `sqlserver.md`, `dialects.md`, `diagnostics.md`, `generator.md`, `settings.md`, manual `AGENTS.md`), architecture (`tool.md`, `sqlserver-describer.md`), epic row, test readme, budget | about 16 |
| 2 | Any correction (source, fixture case, format docs) | 0 to 8 |
| 2 | **Total** | **about 80 to 90** |

## Questions for the owner

Sent to the implementation manager (`local_4f5d8395-a9b4-483f-b8b5-cb55716ffaed`) only when they arise, with a recommended default:

- A case the describer refuses with an id other than the one the case expects.  Meanwhile the case is held out of the PR.
- A wrong `false` whose correction needs an analysis of the query.  Meanwhile the case is held back, and the work stops before registration.

---

## Part 1 (pull request 1 of 2)

### Task 1: The outline's row says In progress

**Files:** Modify `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md:87`; add this plan.

- [ ] **Step 1:** Set the 6.3 row's status to `In progress`.
- [ ] **Step 2:** `./pre-commit-validation.sh` passes; commit "Start phase 6.3: the realistic T-SQL corpus".

### Task 2: The engine-neutral parts of the realistic suite move to `SqlSource.Tool.Testing`

**Files:**
- Create in `tests/SqlSource.Tool.Testing/Realistic/` (namespace `SqlSource.Tool.Testing.Realistic`): `RealisticEngine.cs`, `RealisticCase.cs`, `RealisticCaseReader.cs`, `RealisticSampleChecks.cs`, `RealisticDescriptions.cs`, `RealisticNoise.cs`.
- Create tests in `tests/SqlSource.Tool.Tests/Realistic/`: `RealisticCaseReaderTests.cs`, `RealisticCaseTests.cs`, `RealisticSampleChecksTests.cs`, `RealisticDescriptionsTests.cs`, `RealisticNoiseTests.cs`.
- Modify in `tests/SqlSource.Tests.Postgres/Realistic/`: delete `RealisticCase.cs`; `RealisticCases.cs` becomes a thin wrapper; `RealisticSamples.cs` keeps `Substitute` alone; `RealisticCasesTests.cs` keeps the corpus theories (`Cases`, `All_Cases_HoldAKnownCase`, `Folder_Case_HoldsItsFilesAlone`) and the rest moves; `RealisticSamplesTests.cs` keeps `Substitute`'s tests; `Describer/RealisticDescribe.cs` calls the shared `ColumnsOf` and `RenderFailure`; `Describer/RealisticDescribeTests.cs` moves; `Describer/RealisticNullabilityNoiseTests.cs` uses `RealisticNoise`; `using` lines in `Generation/*` and `EndToEnd/CheckTests.cs`.
- Modify `tests/SqlSource.Tool.Testing/SqlSource.Tool.Testing.csproj` only if a package is missing (`System.Text.Json` is in the framework).

**Interfaces (Produces):**
- `internal sealed record RealisticEngine(string Engine, string Product, IReadOnlyList<int> RecordedMajors)`; `Engine` is `SidecarValues.Engine.Postgres` or `.SqlServer`, `Product` "PostgreSQL" or "SQL Server".
- `TruthColumn`, `ModelRefusal`, `CheckedFrom` unchanged.  `internal sealed record RealisticCase(RealisticEngine Engine, string Name, string Folder, int? MinimumMajor, string? Refusal, IReadOnlyList<TruthColumn> Columns, IReadOnlyList<IReadOnlyDictionary<string, string>> Samples, ModelRefusal? Models = null, CheckedFrom? CheckedFrom = null, string? Dapper = null)` with every member PostgreSQL's has today; `AsFixture => new(Engine.Engine, Name, Folder)`; `SkipUnlessRunsOn` says `"{this} needs {Engine.Product} {MinimumMajor} or later, and the server is {major}."`.
- `internal static class RealisticCaseReader`: `IReadOnlyList<string> NamesIn(string root)`, `IReadOnlyList<RealisticCase> AllIn(string root, RealisticEngine engine)`, `RealisticCase Read(string folder, RealisticEngine engine)`; the rules and messages of today's `RealisticCases`, with `checkedFrom` checked against `engine.RecordedMajors`.
- `internal static class RealisticSampleChecks`: `CheckKeys(RealisticCase, IEnumerable<string>)`, `CheckNulls(RealisticCase, IReadOnlyList<SqlQueryParameter>)`, `string? Find(IReadOnlyDictionary<string, string>, string name)`.
- `internal static class RealisticDescriptions`: `IReadOnlyList<SidecarColumn> ColumnsOf(RealisticCase, Outcome<QueryDescription, DescribeFailure>)`, `string RenderFailure(DescribeFailure)`.
- `internal static class RealisticNoise`: `int CountOf(RealisticCase found, IReadOnlyList<SidecarColumn> columns)` (truth not null, described nullable or unknown); `string? Verdict(int count, int held, int major, string overHint)` returns the over-budget message (`"The noise is {count}, over the budget of {held}: " + overHint`), the under-budget message (`"...lower the budget of {major} in Budgets to {count} in this change, so the improvement is kept."`), or null.
- Each project keeps `RealisticCases` with `Engine` (its `RealisticEngine`), `Names`, `All`, `ReadNamed(name)` and `Read(folder)`, over its own `RepositoryPaths.RealisticCases`.

- [ ] **Step 1:** Move the tests first: the reader's, the record's (`RunsOn`, `Files`, `IsCheckedOn`, `IsReadThroughDapperOn`), the sample checks', `ColumnsOf`'s and `RenderFailure`'s go to `tests/SqlSource.Tool.Tests/Realistic/` against the new names, with an engine of recorded majors `[14, 18]` so the `checkedFrom` rows keep their numbers.  Add `RealisticNoiseTests`: `CountOf` counts a not-null truth described `true` or `null` and not one described `false` nor a nullable truth; `Verdict(5, 5, 14, "h")` is null, `Verdict(6, 5, 14, "h")` starts "The noise is 6, over the budget of 5: h", `Verdict(4, 5, 14, "h")` contains "lower the budget of 14 in Budgets to 4".  Add `SkipUnlessRunsOn`'s message test naming "SQL Server 16".
- [ ] **Step 2:** `dotnet test --project tests/SqlSource.Tool.Tests --filter-namespace "SqlSource.Tool.Tests.Realistic"` (or the `--filter` form the project uses) fails to compile.
- [ ] **Step 3:** Create the shared files from today's PostgreSQL code, unchanged in behaviour; make PostgreSQL's project use them.
- [ ] **Step 4:** The moved tests pass; `tests/SqlSource.Tests.Postgres` passes on the default image and on `postgres:14.24`.
- [ ] **Step 5:** `./pre-commit-validation.sh` passes; commit "Share the engine-neutral parts of the realistic suite".

### Task 3: The `realistic` database of SQL Server, its schema and its seed

**Files:**
- Create `tests/SqlSource.Tests.SqlServer/Realistic/schema.sql`, `Realistic/seed.sql`, `Realistic/RealisticDatabaseTests.cs`, `Realistic/RealisticDatabase.cs`.
- Modify `tests/SqlSource.Tests.SqlServer/SqlServerContainer.cs`, `RepositoryPaths.cs`, `.gitattributes` (`tests/SqlSource.Tests.SqlServer/Realistic/Cases/** text eol=lf`).

**Interfaces (Produces):**
- `SqlServerContainer.RealisticDatabase = "realistic"`, `RealisticConnectionString`, `SkipUnlessRecordedMajor(string what)` and its static form, as PostgreSQL's.  `InitializeAsync` creates `realistic` after `fixtures` and applies `schema.sql` then `seed.sql` through `SqlServerScripts.RunAsync`.
- `RepositoryPaths.Realistic`, `RealisticSchema`, `RealisticSeed`, `RealisticCases`.
- `RealisticDatabase.ScalarAsync(string connection, string sql)` and `RealisticDatabase.Major(string connection)` are not needed beyond `ScalarAsync`.

**The schema** (the builder writes it; these names and nullabilities are what the cases read):

| Object | Columns and facts |
|----|----|
| `dbo.Email` | alias type `FROM nvarchar(254) NULL` |
| `billing` | schema |
| `billing.invoice_numbers` | sequence `AS int START WITH 1000` |
| `dbo.customers` | `id int IDENTITY PK`, `name nvarchar(100) NOT NULL`, `email dbo.Email NULL`, `phone varchar(20) NULL`, `company nvarchar(100) NULL`, `status varchar(10) NOT NULL` (`active`, `suspended`, `closed`), `time_zone varchar(50) NULL`, `created_at datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME()`, `closed_at datetime2(0) NULL` |
| `dbo.categories` | `id int PK`, `parent_id int NULL` → categories, `name nvarchar(50) NOT NULL` |
| `dbo.products` | `id int IDENTITY PK`, `sku varchar(20) NOT NULL UNIQUE`, `name nvarchar(100) NOT NULL`, `category_id int NULL` → categories, `list_price decimal(10,2) NOT NULL`, `discount_pct decimal(5,2) NULL`, `sale_price AS (list_price * (1 - ISNULL(discount_pct, 0) / 100))`, `sku_prefix AS (LEFT(sku, 3)) PERSISTED`, `discontinued_at date NULL`, `attributes nvarchar(max) NULL` (JSON text) |
| `dbo.stock` | `product_id int PK` → products, `on_hand int NOT NULL`, `reorder_at int NULL`, `updated_at datetime2(0) NOT NULL` |
| `dbo.orders` | `id int IDENTITY PK`, `customer_id int NOT NULL` → customers, `placed_at datetime2(0) NOT NULL`, `status varchar(10) NOT NULL`, `ship_name nvarchar(100) NULL`, `ship_city nvarchar(50) NULL`, `shipped_at datetime2(0) NULL`, `note nvarchar(200) NULL`, `row_version rowversion NOT NULL` |
| `dbo.order_lines` | `order_id int` → orders, `line_no int`, PK both; `product_id int NOT NULL` → products, `quantity int NOT NULL`, `unit_price decimal(10,2) NOT NULL` |
| `billing.invoices` | `id int IDENTITY PK`, `number int NOT NULL DEFAULT (NEXT VALUE FOR billing.invoice_numbers)`, `order_id int NOT NULL` → orders, `issued_at date NOT NULL`, `due_at date NULL`, `amount money NOT NULL`, `tax decimal(10,2) NULL`, `voided_at datetime2(0) NULL` |
| `billing.payments` | `id int IDENTITY PK`, `invoice_id int NOT NULL` → invoices, `paid_at datetime2(0) NOT NULL`, `amount money NOT NULL`, `method varchar(10) NOT NULL`, `reference nvarchar(50) NULL` |
| `dbo.prices` | system-versioned: `product_id int PK` → products, `price decimal(10,2) NOT NULL`, `valid_from`/`valid_to datetime2 GENERATED ALWAYS ... HIDDEN`, history `dbo.prices_history` |
| `dbo.carts` | `id uniqueidentifier NOT NULL DEFAULT NEWSEQUENTIALID() PK`, `customer_id int NULL` → customers, `created_at datetime2(0) NOT NULL`, `checked_out_at datetime2(0) NULL` |
| `dbo.cart_items` | `cart_id uniqueidentifier` → carts, `product_id int` → products, PK both; `quantity int NOT NULL`, `added_at datetime2(0) NOT NULL` |
| `dbo.customer_last_orders` | view: `c.id AS customer_id, c.name, o.id AS order_id, o.placed_at, o.status` from customers `LEFT JOIN` each one's latest order |
| `dbo.product_specs` | 2025 only, made by `IF CAST(SERVERPROPERTY('ProductMajorVersion') AS int) >= 17 EXEC(N'CREATE TABLE ...')`: `product_id int PK` → products, `spec json NULL` |

**The seed** only grows.  It has, for every nullable column, a row where it is `NULL`; a customer with no order, an order with no invoice, an invoice with no payment, a product never ordered, a category with no product and a product with no category, a root category, a cart with no customer and one with no items, a product with no stock row; prices updated once after insert so `prices_history` has rows; `product_specs` rows (one `NULL` spec) when the table exists.

- [ ] **Step 1:** Write `RealisticDatabaseTests.Database_AfterStart_HasRowsInEveryTableAndLeavesTheFixturesAlone`: every user table of `dbo` and `billing` in `realistic` (from `sys.tables`, history included) has rows, `billing.invoices` among them, and `fixtures` has no `dbo.customers` (`OBJECT_ID(N'dbo.customers') IS NULL`).
- [ ] **Step 2:** It fails (no database `realistic`).
- [ ] **Step 3:** Write the schema, the seed and the container's setup.
- [ ] **Step 4:** It passes on the default image and on 2017; the rest of `tests/SqlSource.Tests.SqlServer` still passes.
- [ ] **Step 5:** `./pre-commit-validation.sh`; commit "Add the realistic database of SQL Server".

### Task 4: SQL Server's cases, their samples and the oracle's batch

**Files:**
- Create in `tests/SqlSource.Tests.SqlServer/Realistic/`: `RealisticCases.cs`, `RealisticCasesTests.cs`, `RealisticSamples.cs`, `RealisticSamplesTests.cs`.

**Interfaces:**
- Consumes: Task 2's `RealisticCaseReader`, `RealisticEngine`, `RealisticSampleChecks.Find`; `SqlServerTypes.Declared(SqlServerType)`.
- Produces: `RealisticCases.Engine = new(SidecarValues.Engine.SqlServer, "SQL Server", SqlServerContainer.RecordedMajors)`, `Names`, `All`, `ReadNamed`, `Read`.  `internal sealed record SampleBatch(string Text, string Statement, string Declarations)` and `RealisticSamples.Batch(string sql, IReadOnlyDictionary<string, string> sample, IReadOnlyList<DescribedParameter> parameters) : SampleBatch`.

`Batch` gives, for parameters `a` and `b` described as `int` and `[dbo].[Email]`:
```
Text:          "DECLARE @sample_0 int = (1);\nDECLARE @sample_1 [dbo].[Email] = (N'x');\nEXEC sys.sp_executesql @statement, @declarations, @a = @sample_0, @b = @sample_1;"
Declarations:  "@a int, @b [dbo].[Email]"
Statement:     the SQL unchanged
```
and for no parameters `Text` is `"EXEC sys.sp_executesql @statement;"` with `Declarations` empty.  A parameter with no type, or no sample value, throws naming it.

- [ ] **Step 1:** Tests: `RealisticCasesTests` with `Cases` (TheoryData of `RealisticCases.Names`), `Folder_Case_HoldsItsQueryAndItsTruthAlone` (files are `query.sql` and `case.json`, `.DS_Store` ignored; comment: 6.6 adds the sidecar), `All_Cases_HoldAKnownCase` (a part-1 case name); `RealisticSamplesTests`: the two shapes above, a sample key differing in case is found, a missing value and a missing type each throw naming `@name`.
- [ ] **Step 2:** They fail to compile.
- [ ] **Step 3:** Implement.  The test classes need no server and are not in `UsesSqlServer`.
- [ ] **Step 4:** They pass (the known case is added in Task 6; until then `All_Cases_HoldAKnownCase` is written in Task 6).
- [ ] **Step 5:** `./pre-commit-validation.sh`; commit "Read SQL Server's realistic cases and bind their samples".

### Task 5: The describe, the oracle and the checks

**Files:**
- Create in `tests/SqlSource.Tests.SqlServer/Realistic/Describer/`: `RealisticDescribe.cs`, `RealisticNullabilityOracle.cs`, `RealisticNullabilityOracleTests.cs`, `RealisticDescriptionTests.cs`, `RealisticNullabilitySafetyTests.cs`, `RealisticNullabilityNoiseTests.cs`.

**Interfaces:**
- `RealisticDescribe`: `Start(string name, int major)`, `DescribedAsync(RealisticCase, string connection)` → `(IReadOnlyList<SidecarColumn> Columns, IReadOnlyList<DescribedParameter> Parameters)`, `DescribeOnceAsync`, `RequestOf(RealisticCase)`, as PostgreSQL's, opening through `Sessions.OpenAsync(connection, database: SqlServerContainer.RealisticDatabase)`.
- `RealisticNullabilityOracle.RunAsync(string connection, SampleBatch batch) : Task<NullabilityRun>` with `NullabilityRun(IReadOnlyList<string> Names, IReadOnlyList<bool> SawNull, int Rows)`: opens a `SqlConnection`, begins a transaction, runs `batch.Text` with `@statement` and `@declarations` as `SqlDbType.NVarChar, -1` parameters (`@declarations` omitted when empty), reads the first result set's rows, rolls back with `CancellationToken.None`; a failed query rolls back what it can and rethrows its own error.

- [ ] **Step 1:** `RealisticNullabilityOracleTests` (in `UsesSqlServer`): an `INSERT INTO dbo.stock ... OUTPUT inserted.product_id SELECT ...` run twice leaves the count as it was and returns the rows each time; `SELECT id, email FROM dbo.customers ORDER BY id` sees `[false, true]`; a batch with a parameter `@status varchar(10)` bound to `'active'` returns only active customers; a query that fails (`SELECT 1/0 AS x`) throws `SqlException` number 8134.
- [ ] **Step 2:** They fail.
- [ ] **Step 3:** Implement `RealisticDescribe` and the oracle.
- [ ] **Step 4:** The checks, each a theory over `RealisticCasesTests.Cases` (they skip until cases exist; xunit needs at least one row, so this task's commit lands with Task 6's first case if the theory data is empty):
  - `RealisticDescriptionTests.DescribeAsync_Case_IsRefusedOnlyAsItsCaseSays` — as PostgreSQL's.
  - `RealisticNullabilitySafetyTests.DescribeAsync_Case_IsNotNullOnlyWhereTheTruthIs` and `Execute_Case_ReturnsNoNullWhereEitherSaysNotNull`: the oracle over every sample, `CheckKeys` and `CheckNulls` first, the names compared, then the three failures of PostgreSQL's (described not null held `NULL`; truth not null held `NULL`, "the truth is wrong"; nullable, not exempt, no `NULL`).
  - `RealisticNullabilityNoiseTests`: `Budgets` `{ [14] = n, [17] = m }`, `Budgets_RecordedMajors_EachHoldOneAndNoOtherDoes`, and `Noise_OverTheCorpus_IsTheBudget` through `RealisticNoise`, its over-budget hint naming the cases' truths and `RealisticNullabilitySafetyTests` (there are no sidecars).
- [ ] **Step 5:** Commit together with Task 6's first batch (below).

### Task 6: The first half of the cases (28)

**Files:** `tests/SqlSource.Tests.SqlServer/Realistic/Cases/<name>/query.sql` and `case.json`; `seed.sql` grows where a case's nullable column shows no `NULL`; the budget of Task 5.

Each `query.sql` is `-- name: <PascalName>` then `-- param:` lines where a parameter needs a type or `null`, then the query.  Each `case.json` has `columns` (every column, `why` on each nullable one, written as PostgreSQL's are: the cause and the seed row that shows it) and `samples` when the query has parameters; a write's samples choose rows the seed has.

| # | Case | What it is |
|----|----|----|
| 1 | `customers-search-paged` | `LIKE @q`, the optional filter `(@status IS NULL OR c.status = @status)`, `OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY` |
| 2 | `top-with-ties-best-sellers` | `TOP (@n) WITH TIES` products by quantity sold, category name by `LEFT JOIN` |
| 3 | `orders-keyset-page` | keyset paging on `(placed_at, id)` after `@after_placed`, `@after_id` |
| 4 | `orders-with-invoice` | orders `LEFT JOIN billing.invoices` |
| 5 | `categories-right-join-products` | `RIGHT JOIN` |
| 6 | `invoices-full-join-payments` | `FULL JOIN` |
| 7 | `customers-cross-apply-latest-order` | `CROSS APPLY (SELECT TOP 1 ...)` |
| 8 | `invoices-outer-apply-latest-payment` | `OUTER APPLY (SELECT TOP 1 ...)` |
| 9 | `last-orders-view-joined` | a join to `dbo.customer_last_orders` |
| 10 | `category-parent-self-join` | categories `LEFT JOIN` their parent |
| 11 | `big-customers-having` | `GROUP BY` with `HAVING SUM(...) > @min_total` |
| 12 | `customer-order-counts` | `COUNT(*)`, `MAX(placed_at)` with no `GROUP BY`, for `@customer_id` |
| 13 | `order-skus-string-agg` | `STRING_AGG(... ) WITHIN GROUP (ORDER BY ...)` |
| 14 | `sales-by-category-rollup` | `ROLLUP` with `GROUPING()` |
| 15 | `orders-grouping-sets` | `GROUPING SETS` of status and city |
| 16 | `customers-last-payment-subquery` | a scalar subquery of `MAX` |
| 17 | `latest-order-per-customer` | `ROW_NUMBER()` in a CTE, `rn = 1` |
| 18 | `products-ranked-in-category` | `RANK()` and `DENSE_RANK()` |
| 19 | `order-gaps-lag-lead` | `LAG` and `LEAD` over a customer's orders |
| 20 | `payments-running-total` | `SUM(...) OVER (... ROWS UNBOUNDED PRECEDING)` |
| 21 | `customers-first-order-value` | `FIRST_VALUE` |
| 22 | `category-tree-recursive` | a recursive CTE with the path |
| 23 | `insert-customer-output` | `INSERT ... OUTPUT inserted.*` |
| 24 | `merge-stock-upsert` | `MERGE dbo.stock ... OUTPUT $action, inserted.*, deleted.*` |
| 25 | `update-orders-from-join-output` | `UPDATE o ... OUTPUT deleted.x, inserted.x FROM ... JOIN` |
| 26 | `delete-cart-items-output` | `DELETE ... OUTPUT deleted.*` |
| 27 | `contact-fallbacks` | `ISNULL`, `COALESCE`, `NULLIF`, `IIF` over the contact fields |
| 28 | `order-status-labels` | `CASE` with and without `ELSE` |

- [ ] **Step 1:** Write cases 1 to 14 (a subagent may; it reasons each truth from the SQL and the schema, never from the describer, and writes the `why` with the seed row).
- [ ] **Step 2:** Run `RealisticCasesTests`, `RealisticDescriptionTests` and `RealisticNullabilitySafetyTests` on the default image.  A safety failure against the truth or the oracle is a wrong `false`: stop and follow "A wrong `false`" (the case is held back to part 2 with its fix).  A truth failure is re-reasoned; a coverage failure adds a seed row.  A refusal is not expected here.
- [ ] **Step 3:** Write cases 15 to 28; run as in step 2.
- [ ] **Step 4:** Run the noise test, set `Budgets` from the counts on 17 and on 14 (2017 image), so it passes on both.
- [ ] **Step 5:** All of `tests/SqlSource.Tests.SqlServer` passes on both images; `./pre-commit-validation.sh`; commit "Prove the SQL Server describer over the first half of the realistic corpus" (Task 5's files with the cases, or Task 5 alone first if its theories run with an empty corpus).

### Task 7: The procedures of part 1, and its pull request

**Files:** `tests/SqlSource.Tests.SqlServer/README.md` ("The realistic corpus": layout, the database, adding a case, writing a truth, the samples as T-SQL expressions bound through `sp_executesql`, setting the budget); `tests/SqlSource.Tests.Postgres/README.md` (the shared parts now in `tests/SqlSource.Tool.Testing/Realistic/`); `tests/README.md` (the SQL Server project's row; `SqlSource.Tool.Testing` holds `Realistic/`); `tests/AGENTS.md` (the SQL Server corpus's rules: truths never from the describer, folders hold `query.sql` and `case.json` alone until 6.6, the budget changed as the readme says, the database `realistic`; PostgreSQL's bullet names the shared files).

- [ ] **Step 1:** Write them; read each whole.
- [ ] **Step 2:** Count the PR's files (`git diff --stat origin/main | tail -1`), under 150.
- [ ] **Step 3:** `./pre-commit-validation.sh`; commit "Document the realistic corpus of SQL Server".
- [ ] **Step 4:** The version check (root `AGENTS.md`); bring the branch up to date with `main`; re-read the CodeRabbit brief, ask the CodeRabbit manager for clearance, push and open "(part 1 of 2)" with the spec's open calls settled, the deviations above, any small correction, and the budget; turn on Auto-fix.

---

## Part 2 (pull request 2 of 2), after part 1 merges

### Task 8: The second half of the cases (28) and the budget

| # | Case | What it is |
|----|----|----|
| 29 | `cte-feeding-update` | `WITH due AS (...) UPDATE ... OUTPUT` |
| 30 | `insert-invoice-next-value` | `INSERT billing.invoices` with `NEXT VALUE FOR`, `OUTPUT` |
| 31 | `payment-reference-try-convert` | `TRY_CONVERT(int, reference)` |
| 32 | `shipping-label-concat` | `CONCAT` and `+` over nullable strings |
| 33 | `invoices-due-this-month` | `EOMONTH`, `DATEFROMPARTS`, `DATEADD(day, @days, ...)` |
| 34 | `orders-in-customer-time-zone` | `AT TIME ZONE 'UTC' AT TIME ZONE c.time_zone` |
| 35 | `activity-feed-union-all` | orders and payments, `UNION ALL` |
| 36 | `contacts-union` | `UNION` of customers' and ship names |
| 37 | `customers-with-orders-and-carts` | `INTERSECT` |
| 38 | `products-never-ordered` | `EXCEPT` |
| 39 | `product-attributes-openjson` | `CROSS APPLY OPENJSON(p.attributes) WITH (...)` |
| 40 | `product-color-json-value` | `JSON_VALUE` |
| 41 | `orders-with-lines-for-json` | a column of `(SELECT ... FOR JSON PATH)` |
| 42 | `price-as-of` | `FOR SYSTEM_TIME AS OF @at` |
| 43 | `price-history-all` | `FOR SYSTEM_TIME ALL` |
| 44 | `orders-optional-filters` | several optional filters |
| 45 | `orders-in-statuses` | `IN` lists |
| 46 | `customers-exists-flags` | `EXISTS` and `NOT EXISTS`, and `CASE WHEN EXISTS` as a column |
| 47 | `products-like-escape` | `LIKE @pattern ESCAPE '\'` |
| 48 | `cart-by-id` | a `uniqueidentifier` key, its items by `LEFT JOIN` |
| 49 | `order-note-optimistic-update` | `UPDATE ... OUTPUT inserted.row_version WHERE row_version = @version` |
| 50 | `price-bounds-greatest` | `GREATEST`, `LEAST`; `minimumMajor` 16 |
| 51 | `customers-distinct-from` | `IS DISTINCT FROM`; `minimumMajor` 16 |
| 52 | `customer-json-object` | `JSON_OBJECT`; `minimumMajor` 16 |
| 53 | `product-specs-json-column` | the `json` column; `minimumMajor` 17 |
| 54 | `report-dynamic-exec` | `EXEC(@s)` of a built string; `refusal` `SQLSRC264` |
| 55 | `staging-temp-table` | `CREATE TABLE #t` then a `SELECT` from it; `refusal` `SQLSRC265` |
| 56 | `report-label-parameter` | a parameter whose type cannot be deduced; `refusal` `SQLSRC226` |

- [ ] Steps as Task 6's, with the refusals checked by `RealisticDescriptionTests`; a refusal with another id goes to the owner.  Raise the budgets to the new counts and say why in the commit.  Commit "Prove the SQL Server describer over the second half of the realistic corpus".

### Task 9: A wrong `false`, if any check found one

- [ ] Only if Task 6 or 8 found one: a fixture case `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/<name>/` recorded by `tools/record-fixtures.sh mssql`, a unit test of the rule, the narrowest rule in the session's column build keyed on the server's answer and the catalog, and a new `nullableSource` value added by the format's rules for an additive change (`docs/superpowers/specs/2026-10-07-sidecar-format-design.md`, the schema, `SidecarValues`).  One that needs an analysis of the query goes to the owner instead.

### Task 10: The describer registered

**Files:** `src/SqlSource.Tool/ToolHost.cs` (`new DescriberRegistry([new PostgresDescriber(), new SqlServerDescriber()])`, its summary); `tests/SqlSource.Tool.Tests/ToolHostTests.cs`; `tests/SqlSource.Tests.SqlServer/Describer/DescribeCommandTests.cs` and `ToolProcessTests.cs` by PostgreSQL's pattern; a fixture project with an `mssql` file if none fits; `tools/check-tool-install.sh`'s comment.

- [ ] **Step 1:** `ToolHostTests` expects both dialects; `DescribeCommandTests.Describe_ProjectWithAnMssqlFile_WritesItsSidecar` runs `Cli.RunAsync` against the container over a copy of the fixture project and asserts the entry's columns; `ToolProcessTests` runs the built tool once over it.  They fail.
- [ ] **Step 2:** Register; they pass; `tools/check-tool-install.sh` passes (it now loads SqlClient).
- [ ] **Step 3:** `./pre-commit-validation.sh`; commit "Register the SQL Server describer".

### Task 11: The documents, and the outline's row set to Done

- [ ] `src/SqlSource.Tool/README.md`'s first paragraph and `docs/manual/tool.md`'s "Which queries are described": SQL Server is described.
- [ ] `docs/manual/sqlserver.md`: what describing needs (versions tested 2017 and 2025, `TrustServerCertificate` for a local container, only the first result set, the parameter types the server widens and the `-- param:` that narrows them, `@p IS NULL` alone typed `int`); its callouts for describing and, since 6.4 merged, for models replaced.
- [ ] `docs/manual/dialects.md` support table and callout; `README.md` status line and supported environments; `docs/manual/README.md` "What works today"; `docs/manual/generator.md`; `docs/manual/settings.md` Status cells; the advice to set output to `sql` for mssql removed; every sentence of the phase map that says PostgreSQL alone is described.
- [ ] `docs/manual/diagnostics.md`: `SQLSRC216` no longer lists `mssql`.
- [ ] `docs/manual/AGENTS.md`'s phase map rows done by this unit.
- [ ] `docs/architecture/tool.md`'s "The SQL Server describer" placeholder; `docs/architecture/sqlserver-describer.md`: the corpus as the describer's proof, and any correction.
- [ ] The epic's 6.3 row: Done.
- [ ] `./pre-commit-validation.sh`; commit "Document describing SQL Server and close phase 6.3".
- [ ] Version check; up to date with `main`; check whether 6.4's statements still apply; CodeRabbit clearance; push and open "(part 2 of 2)"; Auto-fix.

## Review Focus

1. **A sample of an alias-typed parameter** (`dbo.Email`): the oracle declares `[dbo].[Email]`; `RealisticSamplesTests` pins the bracketed declaration, and a case compares `c.email = @email` (case 1 or 27 takes `@email`).
2. **A sample given as an expression** (`DATEADD(day, -30, SYSUTCDATETIME())`) where `EXEC` would refuse it: case 3 or 42 uses one, so the `DECLARE` form is exercised.
3. **A write whose `OUTPUT` holds `NULL` only for rows the seed has**: the oracle reads `OUTPUT` inside the rolled-back transaction, so a case's second sample sees the seed unchanged; `RealisticNullabilityOracleTests` runs the insert twice.
4. **A case of 2022 syntax on 2017**: `minimumMajor` 16 skips it there, and the noise budget of 14 leaves it out; the reader rejects a `checkedFrom` major that is not 14 or 17 (shared reader test with SQL Server's majors).
5. **A database other than `realistic`**: the cases describe on `realistic`, and the `fixtures` database is left untouched (Task 3's test).
