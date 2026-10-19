# Query generation, phase 5.4: the PostgreSQL end-to-end project - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `tests/SqlSource.Tests.Postgres` becomes the end-to-end project: it runs the generator at build time over queries of two databases, `app` and `billing`, with committed sidecars written by `tools/describe-end-to-end.sh`, and proves them with `--check`, a Dapper run of every model query, and the error paths; a second small project reads `all_types` on Npgsql 8.

**Architecture:** The test project imports the package's props and targets by path and references the generator as an analyzer, as `tests/SqlSource.Tests` does.  `PostgresContainer` gains the databases `app` and `billing`, each with a `schema.sql` and a `seed.sql` under `EndToEnd/`.  Queries live in `EndToEnd/Queries/`, claimed by types in `EndToEnd/`; their sidecars are written by a script that starts its own container and runs the tool as a project.  Tests compile against the generated models, so a model that is missing or has another shape fails the build.

**Tech Stack:** C# on `net10.0`, xunit v3, Shouldly, Testcontainers, Npgsql 10.0.3 and 8.0.9, Dapper 2.1.89, bash, Docker.

**Spec:** [`docs/superpowers/specs/2026-10-18-query-generation-phase-5-4-end-to-end-design.md`](../specs/2026-10-18-query-generation-phase-5-4-end-to-end-design.md), within the [query generation epic](../specs/2026-10-07-query-generation-epic-design.md).

## Global Constraints

- Every commit passes `./pre-commit-validation.sh`, run on the exact tree committed.  Every push needs a passing run on the exact tree pushed, and a run of `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres`.
- One commit per step of the spec's order of work, below.  The pull request keeps to 150 files or fewer; this plan counts about 80.
- Every image names a major and a minor.  The default image is named in one place, `PostgresContainer.DefaultImage`; anything else that needs it reads that line, as the recording scripts do.
- Every committed sidecar is written by `tools/describe-end-to-end.sh`, never by hand.
- Every query of `EndToEnd/` writes its parameters in camelCase, `@accountId` (D-0004).
- Every Dapper test asserts the values of at least one row, `NULL`s included.  A test that writes runs in a transaction that is rolled back.
- `TreatWarningsAsErrors` stays on; `SQLSRC240` is listed in `WarningsNotAsErrors` of both projects that run the generator over sidecars.
- Test classes are `sealed`, names are `Method_Scenario_Outcome`, assertions are Shouldly's (`tests/AGENTS.md`).
- A test project compiles no `.cs` file of another project: code the two PostgreSQL projects share is in `tests/SqlSource.Testing`, whose types stay `internal`.
- User documentation describes effects, never internals: run the grep of `docs/manual/AGENTS.md` over every manual page changed.
- A valid minor review finding is fixed, or the owner is asked whether to record it as tech debt.  It is never deferred silently.

## Where this plan follows the code rather than the spec

The spec was written before 5.1 to 5.3 merged.  Where the code or a spike says otherwise, the plan follows it:

1. **`-- column:` (5.3).**  A `!` or `?` suffix stays in the column's name, so Dapper matches no property to it.  The queries cover both marks: the `-- column:` query runs through Dapper, and the suffix query is read by ordinal with plain ADO.NET into the generated record, the reader the suffix is for.
2. **`SQLSRC240`.**  The 5.1 spec, not the code, says the end-to-end project lists it in `WarningsNotAsErrors`.  Both projects here do, beside the `Directory.Build.props` list.
3. **Records with arrays.**  A spike on Npgsql 8.0.9 and 10.0.3 with Dapper 2.1.89 found that Dapper cannot construct a positional record with an array column, nullable or not.  Npgsql gives the field's type as `System.Array`, and Dapper matches a constructor by exact types.  `all_types` is therefore read into a `class` model (`-- output-model-type: class`).  The manual's Dapper recipe gains the rule and a known issue, a tech-debt item proposes a generator fix, and a test pins the failure.  The owner approved this, with no generator fix in this pull request.
4. **Dapper binds more than the spec lists.**  The same spike bound `IPAddress`, `IPNetwork`, `PhysicalAddress`, `BitArray`, `NpgsqlPoint`, `NpgsqlRange<T>`, a multirange and `hstore`'s dictionary.  It did not bind `uint`, `ulong`, `uint[]`, `NpgsqlTid`, `NpgsqlLogSequenceNumber`, a C# enum (sent as its number), or a `string` beside an enum or `jsonb`.  So the run decides which types the write side leaves out, not the spec's list, and each one is named with its reason.
5. **The write side is an `INSERT … RETURNING *`** in a rolled-back transaction, not a comparison.  `point`, `json`, `xml` and others have no `=`, and an insert gives each parameter its column's exact type.  Its rows read back into the table's own model.
6. **A type override to a type Npgsql does not box needs a Dapper type handler.**  The property-level override `timestamptz=System.DateTimeOffset` makes every `timestamptz` a `DateTimeOffset`, while Npgsql's `GetValue` gives a `DateTime`.  Dapper fills such a property only through a handler, as it does the domain's record struct.  The recipe gains this.
7. **Two enums.**  A data source that calls `MapEnum<T>()` reads that enum as `T` everywhere, so it cannot also prove an enum read as `string`.  `account_status` stays `string`, in `accounts` and `all_types`; `note_kind` is mapped to the C# enum `NoteKind`.
8. **Npgsql 8.0.9**, the latest 8.0 release, is the floor's driver at run time.  `tests/SqlSource.Testing` downloads 8.0.3 for metadata alone.
9. **The shared helper** compares the values of `NpgsqlTypes` by their invariant text.  The library it lives in, `tests/SqlSource.Testing`, cannot reference Npgsql, since RoslynFloor and the Npgsql 8 project both reference it.
10. **The `--check` test runs the tool in process with the real process runner** (`Hosts.Real`), since `CliRun` fakes MSBuild.  The error paths reuse `FixtureProjects` by linking `EndToEnd/ErrorPaths/` into the output as `Fixtures/Projects/ErrorPaths/`.

## Review Focus

1. **A Windows checkout.**  `nightly.yml` builds the solution on Windows, where `* text=auto` gives a `.sql` file CRLF.  If the hash counts line ends, every end-to-end query is stale there.  Task 3 checks this by building after converting a query to CRLF, and adds `.gitattributes` lines only if the build fails.
2. **PostgreSQL 14.**  The sidecars are recorded on 18.6, and `--check` compares nullability on 14.24 too.  Every task that touches the container runs the project on both images before it commits.
3. **A property Dapper leaves at its default.**  A class model whose column Dapper does not match is filled silently.  Every Dapper test compares whole rows, and the `all_types` helper fails on a property that has no expectation and on an expectation that has no property.
4. **The Npgsql 8 project pulling Npgsql 10 in.**  A transitive reference would make it a second Npgsql 10 run.  A test asserts that the loaded `Npgsql` assembly's major version is 8, and another that the model's driver-dependent properties have Npgsql 8's types.
5. **A failed recording.**  `tools/describe-end-to-end.sh` removes its container on every exit, fails when the tool fails, and leaves no partial state that a test would read as current.  `--check` and the build catch what it got wrong.

---

## The files

| Path | What |
|----|----|
| `tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj` | Imports, analyzer reference, Dapper, the settings below, `ErrorPaths` removed and linked |
| `tests/SqlSource.Tests.Postgres/PostgresContainer.cs`, `RepositoryPaths.cs` | The databases `app` and `billing` |
| `tests/SqlSource.Tests.Postgres/EndToEnd/app/schema.sql`, `seed.sql` | The `public` tables, the two enums, the domain, three extensions, `all_types` |
| `tests/SqlSource.Tests.Postgres/EndToEnd/billing/schema.sql`, `seed.sql` | The schema `billing` with `billing.invoices` |
| `tests/SqlSource.Tests.Postgres/EndToEnd/Queries/*.sql`, `*.sql.json` | The queries and their sidecars (table under Task 3) |
| `tests/SqlSource.Tests.Postgres/EndToEnd/*Queries.cs`, `Reporting/AccountReports.cs` | The claiming types |
| `tests/SqlSource.Tests.Postgres/EndToEnd/Types/` | `NoteKind`, `Email`, `EmailHandler`, `UtcDateTimeOffsetHandler` |
| `tests/SqlSource.Tests.Postgres/EndToEnd/EndToEndTool.cs` | Runs the tool in process with the real process runner and the PostgreSQL describer |
| `tests/SqlSource.Tests.Postgres/EndToEnd/EndToEndSources.cs` | Dapper's settings and the data sources of the Dapper tests |
| `tests/SqlSource.Tests.Postgres/EndToEnd/*Tests.cs` | `ModelShapeTests`, `CheckTests`, `ErrorPathTests`, `AccountQueryTests`, `NoteQueryTests`, `ShapeQueryTests`, `InvoiceQueryTests`, `AllTypesQueryTests` |
| `tests/SqlSource.Tests.Postgres/EndToEnd/ErrorPaths/` | `App.csproj`, `Queries.cs`, `Widgets.sql`: the scratch project |
| `tests/SqlSource.Testing/EndToEnd/AllTypesRows.cs` | The two rows of `all_types` and their assertions, shared by both projects |
| `tests/SqlSource.Tests.Postgres.Npgsql8/` | `SqlSource.Tests.Postgres.Npgsql8.csproj`, `packages.lock.json`, `AllTypesQueries.cs`, `AppContainer.cs`, `AllTypesQueryTests.cs` |
| `tools/describe-end-to-end.sh` | Writes every sidecar of the project |
| Documents | Listed under Task 7 |

---

### Task 0: The plan

- [ ] **Step 1:** Commit this plan alone: `git add docs/superpowers/plans/2026-10-19-query-generation-phase-5-4-end-to-end.md`, message `Plan phase 5.4 of query generation`.  Run `./pre-commit-validation.sh` first; it validates the tree.

### Task 1: The project runs the generator, with no claimed file

**Files:**
- Modify: `tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`, `tests/SqlSource.Tests.Postgres/packages.lock.json`
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (the 5.4 row: `In progress`)
- Create: `tests/SqlSource.Tests.Postgres/EndToEnd/ModelShapeTests.cs`

**Interfaces:**
- Produces: the project's MSBuild settings, which later tasks rely on: `SqlSourceDialect` `postgres`, `SqlSourceDatabase` `app`, `SqlSourceOutput` `Models`, `SqlSourceInputModelSuffix` `Args`, `SqlSourceInputModelType` `record`, `SqlSourceTypeOverrides` `timestamptz=System.DateTimeOffset`, and `WarningsNotAsErrors` `$(WarningsNotAsErrors);SQLSRC240`.

- [ ] **Step 1: Write the failing test** `ModelShapeTests.Build_Project_DeclaresTheGeneratorsAttribute`: `typeof(ModelShapeTests).Assembly.GetType("SqlSource.SqlSourceGenerateAttribute").ShouldNotBeNull()`.  The class is not in `UsesPostgres`: it needs no server.
- [ ] **Step 2: Run it**, `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "*ModelShapeTests"`.  Expected: FAIL, the type is null.
- [ ] **Step 3: Change the project.**  Follow `tests/SqlSource.Tests/SqlSource.Tests.csproj`: `<Import Project="../../src/SqlSource/build/SqlSource.props" />` first, `SqlSource.targets` last, and the reference to `src/SqlSource` becomes `OutputItemType="Analyzer" ReferenceOutputAssembly="true"` (one reference gives both).  Add `<PackageReference Include="Dapper" />`.  Set the settings above, each with a one-line comment on what it covers.  The comment on `SQLSRC240` says that a pull request that raises `VersionPrefix` warns until it runs `tools/describe-end-to-end.sh`.  Run `dotnet restore tests/SqlSource.Tests.Postgres` without locked mode to update the lock file.
- [ ] **Step 4: Run the test.**  Expected: PASS.  `dotnet build SqlSource.slnx` has no warning, and the existing tests still pass.  The `.sql` files of `Realistic/` and the fixture schema become `AdditionalFiles` but are claimed by nothing.
- [ ] **Step 5:** The epic's 5.4 row says `In progress`.
- [ ] **Step 6: Validate and commit**, message `Run the generator in the PostgreSQL tests`.

### Task 2: The two databases

**Files:**
- Create: `tests/SqlSource.Tests.Postgres/EndToEnd/app/schema.sql`, `app/seed.sql`, `billing/schema.sql`, `billing/seed.sql`
- Modify: `PostgresContainer.cs`, `RepositoryPaths.cs`, `PostgresContainerTests.cs`, `tests/SqlSource.Tests.Postgres/README.md` (the databases the container has, in Running)

**Interfaces:**
- Produces, on `PostgresContainer`: `public const string AppDatabase = "app"`, `public const string BillingDatabase = "billing"`, `public static IReadOnlyList<string> EndToEndDatabases { get; } = [AppDatabase, BillingDatabase]`, `public string ConnectionStringOf(string database)`.
- Produces, on `RepositoryPaths`: `EndToEnd` (the folder), `EndToEndProject` (the `.csproj`), `EndToEndSchema(string database)`, `EndToEndSeed(string database)`.  `tools/describe-end-to-end.sh` uses the same layout, `EndToEnd/<database>/schema.sql` then `seed.sql`.

`app`, in `public`:
- The extensions `citext`, `hstore` and `ltree` (`cube` is left out, below).
- `CREATE TYPE account_status AS ENUM ('active', 'suspended', 'closed')`, read as `string` everywhere.  `CREATE TYPE note_kind AS ENUM ('call', 'email', 'visit')`, mapped to `NoteKind` in Task 3.
- `CREATE DOMAIN email AS text CHECK (VALUE LIKE '%@%')`.
- `accounts`: `id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY`, `name text NOT NULL`, `email email NOT NULL`, `status account_status NOT NULL DEFAULT 'active'`, `created_at timestamptz NOT NULL DEFAULT now()`, `closed_at timestamptz`, `name_length integer GENERATED ALWAYS AS (length(name)) STORED`.  This covers nullable, not null, an identity, a generated column and a default.
- `notes`: `id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY`, `account_id integer NOT NULL REFERENCES accounts (id)`, `kind note_kind NOT NULL`, `body text NOT NULL`, `written_at timestamptz NOT NULL`.  An account with no note gives the `LEFT JOIN` its `NULL`s.
- `all_types`: `id integer PRIMARY KEY`, then one nullable column named `<type>_value` for each type that the base map (`src/SqlSource/Generation/Types/PostgresTypeMap.cs`) gives a C# type on both Npgsql 8 and 10 and that PostgreSQL 14 has.  That is `bool` to `numeric(12,4)` and `money`; `text`, `varchar(20)`, `char(5)` (`bpchar`), `name`, `"char"`, `citext`, `bytea`; `date`, `time`, `timetz`, `timestamp`, `timestamptz`, `interval`; `uuid`, `json`, `jsonb`, `jsonpath`, `xml`; `inet`, `cidr`, `macaddr`, `macaddr8`; `bit(1)`, `bit(8)`, `varbit`; the seven geometric types; `tsvector`, `tsquery`; `oid`, `xid`, `cid`, `regtype`, `regconfig`, `xid8`, `tid`, `pg_lsn`, `int2vector`, `oidvector`, `refcursor`; `hstore`, `ltree`, `lquery`, `ltxtquery`; the six built-in ranges and six multiranges.  Add the arrays `integer[]`, `text[]`, `date[]` and `account_status[]`, an `email` column (domain) and an `account_status` column (enum).  `cube` is left out, with a comment: Npgsql 8 cannot read it, and 5.2's generator tests prove its row.  A type PostgreSQL refuses as a column, if `int2vector` or `oidvector` is one, is left out with the server's reason in a comment.
- Its seed has two rows.  Row 1 has a value in every column, chosen to exercise the type's range: integer extremes, a non-ASCII string, timestamps with microseconds, an open and a closed bound in the ranges.  It avoids what Npgsql cannot read: `numeric` NaN or infinity, an `interval` with months, an array with a `NULL` element.  Row 2 has `NULL` in every column but `id`.

`billing`: `CREATE SCHEMA billing`; `billing.invoices`: `id integer PRIMARY KEY`, `account_id integer NOT NULL`, `number text NOT NULL`, `amount numeric(12,2) NOT NULL`, `issued_on date NOT NULL`, `paid_at timestamptz`.  Three rows, one unpaid.

Seeds give every value explicitly, `created_at` included, so a test can name it.  `accounts` has three rows, one of them closed and one with no note; `notes` has three rows over the other two accounts.

- [ ] **Step 1: Write the failing test** `PostgresContainerTests.InitializeAsync_EndToEndDatabases_AreSeeded`: on `ConnectionStringOf(AppDatabase)`, `SELECT count(*) FROM all_types` is `2` and `SELECT count(*) FROM accounts` is `3`.  On `ConnectionStringOf(BillingDatabase)`, `SELECT count(*) FROM billing.invoices` is `3`.
- [ ] **Step 2: Run it.**  Expected: FAIL, database `app` does not exist.
- [ ] **Step 3: Write the four scripts** and extend `InitializeAsync`.  For each of `EndToEndDatabases`: `CREATE DATABASE`, then apply the schema and the seed with plain ADO.NET, as the realistic database is applied.  Each script opens with a comment that says which database it builds, that `PostgresContainer` and `tools/describe-end-to-end.sh` apply it, and that a change to it is followed by a run of the script.
- [ ] **Step 4: Run** the project on `postgres:18.6` and on `postgres:14.24`.  Expected: PASS on both.
- [ ] **Step 5: Validate and commit**, message `Give the PostgreSQL tests the databases app and billing`.

### Task 3: The queries, the claiming types and the script

**Files:**
- Create: `tools/describe-end-to-end.sh`
- Create: `EndToEnd/Queries/Accounts.sql`, `Notes.sql`, `Shapes.sql`, `Constants.sql`, `AllTypes.sql`, `Billing/Invoices.sql`, and each one's `.sql.json` but `Constants.sql`'s
- Create: `EndToEnd/AccountQueries.cs`, `Reporting/AccountReports.cs`, `NoteQueries.cs`, `ShapeQueries.cs`, `ConstantQueries.cs`, `InvoiceQueries.cs`, `AllTypesQueries.cs`, `Types/NoteKind.cs`, `Types/Email.cs`
- Modify: `SqlSource.Tests.Postgres.csproj` (metadata of `EndToEnd/Queries/Billing/**`), `ModelShapeTests.cs`, `PostgresContainer.cs` (its comment names the third reader of `DefaultImage`), `.editorconfig` (`[*.sql.json]`: `max_line_length = off`, "sidecars in the layout the tool writes")

**Interfaces:**
- Consumes: Task 1's settings and Task 2's schemas.
- Produces: the generated types below, in namespace `SqlSource.Tests.Postgres.EndToEnd` unless named otherwise.  `NoteKind` is `enum NoteKind { Call, Email, Visit }`; `Email` is `readonly record struct Email(string Value)`.

**The claims and the settings they cover.**  Every `-- param:` and `@` name is camelCase.

| File | Claimed by | Settings | Queries, and the models they give |
|----|----|----|----|
| `Accounts.sql` | `AccountQueries` (`internal sealed partial class`, `Path = "Queries/Accounts.sql"`, `Nested`), and `Reporting.AccountReports` (`Path = "../Queries/Accounts.sql"`, `Direct`, namespace `SqlSource.Tests.Postgres.EndToEnd.Reporting`) | Output `Models` and database `app` by property.  Output models are the default `sealed record`.  Input models are `record` with suffix `Args` by property.  A second namespace claims the file | `GetAccount` (`WHERE id = @accountId`, a list in another order: `GetAccountArgs`, `GetAccountDto`).  `ListAccounts` (`SELECT * … ORDER BY id`: table model `AccountsDto`).  `ListAccountsInTableOrder` (the full list in table order: `AccountsDto`, shared).  `ListAccountsUntabled` (`-- generator: no-table-models`: `ListAccountsUntabledDto`).  `GetAccountName` (`id = @accountId`) and `FindAccountName` (`email = @email`), both `-- output-model: AccountName`.  `ListClosedBefore` (`-- param: @closedBefore timestamptz null`, `WHERE @closedBefore IS NULL OR closed_at < @closedBefore`).  Each model exists once in each namespace |
| `Notes.sql` | `NoteQueries` (`Direct`, `Output = GeneratorOutput.CodeGen`) | Header `-- type-override: public.note_kind = SqlSource.Tests.Postgres.EndToEnd.NoteKind` and `-- type-override: public.email = SqlSource.Tests.Postgres.EndToEnd.Email` | `ListAccountNotes` (`accounts LEFT JOIN notes`: the notes' columns are nullable).  `CountAccountNotes` (`count(n.id) AS note_count`, `-- column: note_count not null`).  `ListMarkedNames` (`a.id AS "id?"`, `upper(a.name) AS "upper_name!"`).  `ListNoteKinds` (`n.kind`: `NoteKind`; `a.email`: `Email`) |
| `Shapes.sql` | `ShapeQueries` (`Direct`, `OutputModelType = GeneratorModelType.Record`, `OutputModelSuffix = "View"`, `InputModelType = GeneratorModelType.SealedClass`, `ModelNamespace = "SqlSource.Tests.Postgres.EndToEnd.Shapes"`, `MethodLocation = MethodLocation.Public`, `CollectionType = GeneratorCollectionType.List`) | Header `-- input-model-suffix: Filter`, `-- collection-type: IReadOnlyList` | `SearchAccounts -> many` (`-- generator: sort-input`, parameters `@name` then `@accountId`: `SearchAccountsFilter` with `AccountId` before `Name`).  `ListAccountColumns` (`-- generator: sort-output`, `-- output-model-type: class`).  `GetHeadline` (`-- output-model: AccountHeadline`, in `Shapes`).  `LookUpAccount` (`-- input-model: SqlSource.Tests.Postgres.EndToEnd.Shared.AccountLookup`, `-- input-model-type: class`) |
| `Constants.sql` | `ConstantQueries` (`Direct`) | Header `-- output: sql`: no sidecar | `CountAccounts` |
| `Billing/Invoices.sql` | `InvoiceQueries` (`Path = "Queries/Billing"`) | Header `-- database: billing` and `-- model-namespace: SqlSource.Tests.Postgres.EndToEnd.Billing`.  Metadata on `EndToEnd/Queries/Billing/**`: `SqlSourceOutputModelType` `sealed class`, `SqlSourceOutputModelSuffix` `Row`, `SqlSourceTypeOverrides` `timestamptz=System.DateTime` | `ListInvoices` (`SELECT *`: `BillingInvoicesRow`).  `GetInvoice` (`WHERE id = @invoiceId`) |
| `AllTypes.sql` | `AllTypesQueries` (`Direct`) | Header `-- output-model-type: class` (finding 3) | `ListAllTypes` (`SELECT * FROM all_types ORDER BY id`, `-- type-override: timestamptz = default`: `AllTypesDto`).  `InsertAllTypes` (`INSERT INTO all_types (…) VALUES (@id, @boolValue, …) RETURNING *`, the same marker), its column list fixed in Task 5 |

`Nested` puts the constants in a private class, so a test takes a query's SQL from `AccountReports`, the `Direct` claimer of the same file.

So every output, both locations, the four model types (property `record`, metadata `sealed class`, attribute `record` and `sealed class`, marker `class`) and the four suffix levels (property, metadata, attribute, header marker) are covered.  So are both model markers with and without a period, the namespace by default, attribute, marker and a second claiming namespace, both sorts, every table-model case, a shared name, each nullability rule, and the override at every level but the attribute, which has none.  The phase 8 settings are on `ShapeQueries` and `Shapes.sql`.

- [ ] **Step 1: Write the script.**  It follows `tools/record-fixtures.sh`: `set -euo pipefail`, no arguments (exit 2 with a usage line), `cd` to the root.  It reads the image with the same `sed` from `PostgresContainer.cs` and ignores `SQLSOURCE_TESTS_POSTGRES_IMAGE`, since the sidecars are recorded on the default image.  Then:
  - `docker run -d -e POSTGRES_PASSWORD=… -p 127.0.0.1::5432 "$image"`, with `trap` removing the container on every exit;
  - wait until `docker exec "$container" pg_isready -h 127.0.0.1 -U postgres` succeeds, at most 60 seconds (the entrypoint's first server listens on the socket alone);
  - for each of `app billing`: `CREATE DATABASE`, then `psql -v ON_ERROR_STOP=1` with `schema.sql` then `seed.sql` on standard input;
  - delete every `*.sql.json` under `tests/SqlSource.Tests.Postgres/EndToEnd/Queries` (from nothing);
  - run `dotnet run --project src/SqlSource.Tool -- describe --force tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`, with `--connection <db>=Host=127.0.0.1;Port=<docker port>;Username=postgres;Password=…;Database=<db>` for each database, and fail when it fails.

  The header comment says what it writes, when to run it (after a query, schema or seed change, and in the pull request that raises `VersionPrefix`), and why it starts its own container: the project does not build until its sidecars are current.  `tools/shellcheck.sh` and `tools/shfmt.sh --check` pass.
- [ ] **Step 2: Write the failing tests** in `ModelShapeTests`, one for each row of the table.  Each asserts by reflection what the table says the generator gives:
  - the type exists with that full name;
  - it is sealed or not;
  - it is a record or not (a record has a `<Clone>$` method);
  - its public properties, in order, with their types and their nullability by `NullabilityInfoContext`.

  Name them for the setting, such as `Models_OutputTypeByMetadata_IsASealedClass`, `Models_SortInput_OrdersTheInputByName`, `Models_SecondNamespace_GetsItsOwnModels`, `Models_ColumnMarker_MakesTheCountNotNull`, `Models_SuffixMarks_SetTheNullability`, `Models_LeftJoin_MakesTheOptionalSideNullable`, `Models_PropertyOverride_MakesTimestamptzADateTimeOffset`, `Models_QueryOverride_ReturnsTimestamptzToDateTime`, `Models_FolderOverride_MakesTimestamptzADateTime`, `Models_AllTypes_HasNpgsql10sTypes`.  The last asserts `date` `DateOnly?`, `time` `TimeOnly?`, `cidr` `IPNetwork?`, `daterange` `NpgsqlRange<DateOnly>?` and `datemultirange` `NpgsqlRange<DateOnly>[]?`.  Also add `ConstantQueries_SqlOutput_HasTheConstantAndNoSidecar`.  These tests compile only once the models exist, so the failing run is the build's: `CS0246` for each model.
- [ ] **Step 3: Write the queries, the claiming types and `Types/`**, then run `tools/describe-end-to-end.sh` and read every sidecar it wrote.  Check two things in each: that the nullability the table expects is what was described, and that `ListAccounts`, `ListAccountsInTableOrder`, `ListAllTypes` and `ListInvoices` have `matchesTable` `true`.  A query whose description differs from the table is rewritten until it covers its row; a sidecar is never edited.
- [ ] **Step 4: Build and run** `ModelShapeTests`.  Expected: PASS.  If `InsertAllTypes`' `RETURNING *` is not described as matching the table, it gets a model of its own and Task 5 reads that one.
- [ ] **Step 5: Check line ends.**  Convert `Accounts.sql` to CRLF in the working tree, build, and look for `SQLSRC244`.  If it is reported, add `tests/SqlSource.Tests.Postgres/EndToEnd/** text eol=lf` to `.gitattributes` with a comment.  Since a user's Windows checkout would hit the same, also record a tech-debt item and ask the owner whether to fix the hash instead.  Restore the file.
- [ ] **Step 6: Validate on both images and commit**, message `Add the end-to-end queries with their sidecars`.

### Task 4: `--check` and the error paths

**Files:**
- Create: `EndToEnd/EndToEndTool.cs`, `EndToEnd/CheckTests.cs`, `EndToEnd/ErrorPathTests.cs`, `EndToEnd/ErrorPaths/App.csproj`, `EndToEnd/ErrorPaths/Queries.cs`, `EndToEnd/ErrorPaths/Widgets.sql`
- Modify: `SqlSource.Tests.Postgres.csproj`

**Interfaces:**
- Consumes: `Hosts.Real(string workingDirectory, string tempDirectory)`, `DescriberRegistry(IEnumerable<IQueryDescriber>)`, `PostgresDescriber`, `Cli.RunAsync(string[] args, ToolHost host, CancellationToken)`, `CliResult(int ExitCode, string Out, string Error)`, `FixtureProjects.Copy(string fixture)`, `TempFolder`.
- Produces: `internal static class EndToEndTool { public static Task<CliResult> RunAsync(string workingDirectory, params string[] args); }`.  It builds `Hosts.Real(...) with { Out, Error, Describers = new DescriberRegistry([new PostgresDescriber()]) }` over `StringWriter`s with `NewLine = "\n"`, and a temporary folder it deletes.

`EndToEnd/ErrorPaths/` is a project of its own, never built.  `App.csproj` follows `tests/SqlSource.Tool.Tests/Fixtures/Projects/Single/App.csproj`: it imports `../build/SqlSource.props` and `.targets`, with `SqlSourceDialect` `postgres` and `SqlSourceDatabase` `errors`.  `Queries.cs` is `[SqlSource.SqlSourceGenerate] internal static partial class Queries;`.  `Widgets.sql` is `-- name: GetWidgets` over `error_path_widgets (id integer PRIMARY KEY, name text NOT NULL)`, a table each test creates on the database `fixtures` through `PostgresContainer.ExecuteAsync` and drops in a `finally`.  The test project removes the folder from `Compile`, `AdditionalFiles` and `None`, after the props' import.  It adds it as `None` with `Link="Fixtures/Projects/ErrorPaths/%(RecursiveDir)%(Filename)%(Extension)"` and `CopyToOutputDirectory="PreserveNewest"`, so `FixtureProjects.Copy("ErrorPaths")` copies it from the output, as `Single` is copied.

- [ ] **Step 1: Write the failing tests.**  In `CheckTests` (`UsesPostgres`): `Check_Project_EveryEntryIsTheSame` runs `EndToEndTool.RunAsync(RepositoryPaths.Root, "describe", "--check", RepositoryPaths.EndToEndProject, "--connection", "app=…", "--connection", "billing=…")`.  It asserts `ExitCode` `0` with `Error` as the message, and `Out` `"app (postgres): <n> same, 0 differ, 0 failed\nbilling (postgres): <m> same, 0 differ, 0 failed\n"`, with `n` and `m` the counts of Task 3's queries.  In `ErrorPathTests` (`UsesPostgres`):
  - `Check_QueryEditedAfterDescribe_IsOutOfDate`: describe the copy (exit `0`, the sidecar exists), add a column to `Widgets.sql`'s select list, then `--check` exits `2` with `SQLSRC219` in `Error`.
  - `Check_ColumnAddedAfterDescribe_IsOutOfDate`: describe, `ALTER TABLE error_path_widgets ADD COLUMN note text`, then `--check` exits `2` with `SQLSRC219`.
  - `Describe_QueryTheServerRefuses_ReportsItAndWritesNoSidecar`: write `Refused.sql` over a table that does not exist, then `describe` exits `1` with `SQLSRC225` in `Error` and no `Refused.sql.json`.
- [ ] **Step 2: Run them.**  Expected: FAIL to compile (`EndToEndTool` missing).
- [ ] **Step 3: Implement `EndToEndTool`, the scratch project and the project's items.**
- [ ] **Step 4: Run** on 18.6 and 14.24.  Expected: PASS on both.  If `--check` differs on 14, read the difference.  Its cause decides the fix: a query that covers a row without depending on the planner, or a question to the owner when the describer is at fault.
- [ ] **Step 5: Validate and commit**, message `Check the end-to-end sidecars and the tool's error paths`.

### Task 5: The Dapper tests

**Files:**
- Create: `EndToEnd/EndToEndSources.cs`, `EndToEnd/Types/EmailHandler.cs`, `EndToEnd/Types/UtcDateTimeOffsetHandler.cs`, `EndToEnd/AccountQueryTests.cs`, `NoteQueryTests.cs`, `ShapeQueryTests.cs`, `InvoiceQueryTests.cs`, `AllTypesQueryTests.cs`
- Create: `tests/SqlSource.Testing/EndToEnd/AllTypesRows.cs`
- Modify: `EndToEnd/Queries/AllTypes.sql` (the column list of `InsertAllTypes`), its sidecar by the script

**Interfaces:**
- Produces: `internal static class EndToEndSources { public static NpgsqlDataSource App(PostgresContainer postgres); public static NpgsqlDataSource Billing(PostgresContainer postgres); }`.  Its static constructor sets `DefaultTypeMap.MatchNamesWithUnderscores = true` and adds `EmailHandler` and `UtcDateTimeOffsetHandler`, once for the process.  `App` maps `MapEnum<NoteKind>()` and nothing else: the recipe's settings and no more.
- Produces, in `tests/SqlSource.Testing/EndToEnd/AllTypesRows.cs`: `internal static class AllTypesRows`, with:
  - `public const int FullRowId = 1;` and `public const int NullRowId = 2;`
  - `public static void ShouldBeTheFullRow(object row, int id = FullRowId, IReadOnlySet<string>? nullProperties = null)`
  - `public static void ShouldBeTheNullRow(object row)`

  It reads the row's public properties by reflection.  It holds one expectation for each property, by name, as a `Func<Type, object?>` of the property's type, so that one expectation serves both drivers: `date` is `DateOnly` when the property is `DateOnly?` and `DateTime` when it is `DateTime?`.  It fails on a property with no expectation and on an expectation with no property.  It compares:
  - an array element by element;
  - a `BitArray` bit by bit, and a dictionary entry by entry;
  - a `DateTime`'s `Kind` with its value: `Utc` for `timestamptz`, `Unspecified` for `timestamp`;
  - a type of the namespace `NpgsqlTypes`, and `cidr` on both drivers, by its text under `CultureInfo.InvariantCulture`, since the library cannot reference Npgsql (finding 9).

`ShouldBeTheNullRow` asserts the id and a `null` in every other property.  `tests/SqlSource.Testing/SqlSource.Testing.csproj` grants `InternalsVisibleTo` to `SqlSource.Tests.Postgres.Npgsql8` in Task 6.

- [ ] **Step 1: Write the failing tests**, each in `UsesPostgres`, each opening a connection of its own `EndToEndSources` source.  Each passes the generated input model to Dapper as the parameters and compares whole rows with the seed's values, built as the generated models, `NULL`s included.  Record models compare with `ShouldBe`, classes with `ShouldBeEquivalentTo`.
  - `AccountQueryTests`:
    - `Query_GetAccount_ReadsTheAccount`;
    - `Query_ListAccounts_ReadsEveryAccountIntoTheTableModel`, which asserts the generated and the default columns of the three rows;
    - `Query_ListAccountsInTableOrder_ReadsTheSameRows`;
    - `Query_ListAccountsUntabled_ReadsEveryAccount`;
    - `Query_SharedModel_ReadsBothQueries`;
    - `Query_ListClosedBefore_NullParameter_ReadsEveryAccount` and `Query_ListClosedBefore_Date_ReadsTheClosedAccount`;
    - `Query_SecondNamespace_ReadsTheSameAccount`, through `Reporting`'s models.
  - `NoteQueryTests`:
    - `Query_ListAccountNotes_AccountWithNoNote_HasNulls`;
    - `Query_CountAccountNotes_ReadsTheCounts`;
    - `Query_ListNoteKinds_ReadsTheEnumAndTheDomain`;
    - `ExecuteReader_ListMarkedNames_ReadsByOrdinalIntoTheRecord`, which reads with `NpgsqlDataReader.GetFieldValue<T>` by ordinal into the record's constructor, the reader the suffix marks are for.
  - `ShapeQueryTests`: one test per query of `Shapes.sql`.
  - `InvoiceQueryTests`: `Query_ListInvoices_ReadsTheTableModel` and `Query_GetInvoice_ReadsTheUnpaidInvoice`, on `billing`.
  - `AllTypesQueryTests`:
    - `Query_ListAllTypes_ReadsBothRows`, through `AllTypesRows`;
    - `Query_InsertAllTypes_ReturnsTheValuesWritten`.  In a transaction rolled back in a `finally`, it builds the input model from row 1's values, property by property by name, with `Id` `3`.  It inserts, then asserts with `AllTypesRows.ShouldBeTheFullRow(row, 3, nullProperties: WriteSideLeftOut.Keys)`.
    - `InsertAllTypes_WriteSide_LeavesOutOnlyTheListedTypes`.  `WriteSideLeftOut` is a `Dictionary<string, string>` from property name to the one-line reason Dapper or Npgsql gave.  The test asserts that the input model's properties and `WriteSideLeftOut`'s keys together are `AllTypesDto`'s properties, with no overlap.
    - `Query_RecordWithAnArray_DapperCannotConstructIt`.  It reads `SELECT int4_array_value FROM all_types` into a test-local `sealed record` and asserts Dapper's `InvalidOperationException`.  This pins finding 3 and the tech-debt item that Task 7 records, and is removed with the fix.
- [ ] **Step 2: Run them.**  Expected: FAIL (the helpers are missing).
- [ ] **Step 3: Implement** `EndToEndSources`, the two handlers and `AllTypesRows`.  `UtcDateTimeOffsetHandler` parses a `DateTime` as `new DateTimeOffset(value)` and passes a `DateTimeOffset` through.  It writes `value.UtcDateTime`, since Npgsql writes only offset 0.  `EmailHandler` reads `new Email((string)value)` and writes `value.Value`.
- [ ] **Step 4: Fix `InsertAllTypes`' column list.**  Start with every column but `id` and run the test.  Move each column whose parameter fails into `WriteSideLeftOut`, with the reason from the exception, rerun, and repeat until it passes.  Rewrite the query to insert only the columns that bind.  Never leave out a column that binds.  Run the script, read the diff of `AllTypes.sql.json`, and run again.
- [ ] **Step 5: Run** on 18.6 and 14.24.  Expected: PASS on both.  A failure that is not a binding failure is a finding of the map or the generator: fix it in this pull request, or ask the owner about recording it as tech debt.
- [ ] **Step 6: Validate and commit**, message `Read every end-to-end query through Dapper into its models`.

### Task 6: `tests/SqlSource.Tests.Postgres.Npgsql8`

**Files:**
- Create: `tests/SqlSource.Tests.Postgres.Npgsql8/SqlSource.Tests.Postgres.Npgsql8.csproj`, `packages.lock.json`, `AllTypesQueries.cs`, `AppContainer.cs`, `AllTypesQueryTests.cs`
- Modify: `SqlSource.slnx` (`/tests/`, in order), `tests/SqlSource.Testing/SqlSource.Testing.csproj` (`InternalsVisibleTo`), `.github/workflows/nightly.yml` (the comment above Test names both projects that need a container), `tools/AGENTS.md` (the readers of `DefaultImage`)

**Interfaces:**
- Consumes: `AllTypesRows`, `RepositoryRoot.Folder`, `tests/SqlSource.Tests.Postgres/EndToEnd/app/schema.sql` and `seed.sql`, `AllTypes.sql` and its sidecar.

The project: `net10.0`, `Exe`, not packable.  It imports the props and targets by path and references `src/SqlSource` with `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`, and `tests/SqlSource.Testing`.  Packages: `Npgsql` with `VersionOverride="8.0.9"`, a comment saying why (the floor's latest 8.0 release; the end-to-end project gets 10 through the tool), `Dapper`, `Shouldly`, `Testcontainers.PostgreSql`, `xunit.v3`, `GitHubActionsTestLogger`, `Microsoft.Testing.Extensions.CodeCoverage`.  No reference to the tool or to `tests/SqlSource.Tool.Testing`.  Settings: `SqlSourceDialect` `postgres`, `SqlSourceOutput` `Models`, `WarningsNotAsErrors` with `SQLSRC240`.  It links `../SqlSource.Tests.Postgres/EndToEnd/Queries/AllTypes.sql` and its `.sql.json` as `AdditionalFiles` with `Link="Queries/%(Filename)%(Extension)"`.  `AllTypesQueries` is `[SqlSourceGenerate(Path = "../SqlSource.Tests.Postgres/EndToEnd/Queries/AllTypes.sql", SqlLocation = SqlLocation.Direct)] internal static partial class AllTypesQueries;`.

`AppContainer : IAsyncLifetime` starts the default image, read from `tests/SqlSource.Tests.Postgres/PostgresContainer.cs` by the line `tools/record-fixtures.sh` reads, so the image is still named once.  It creates the database `app` and applies its schema and seed.  It is a class fixture of `AllTypesQueryTests`.

- [ ] **Step 1: Write the failing tests** in `AllTypesQueryTests`:
  - `Npgsql_Loaded_IsVersion8`: `typeof(NpgsqlConnection).Assembly.GetName().Version!.Major` is `8`.
  - `Models_AllTypes_HasNpgsql8sTypes`: `date` `DateTime?`, `time` `TimeSpan?`, `cidr` `NpgsqlCidr?`, `daterange` `NpgsqlRange<DateTime>?`, `datemultirange` `NpgsqlRange<DateTime>[]?`.
  - `Query_ListAllTypes_ReadsBothRows`, with `DefaultTypeMap.MatchNamesWithUnderscores` set, through `AllTypesRows`.
- [ ] **Step 2: Run them.**  Expected: FAIL to build until the project exists; then FAIL on the missing link.
- [ ] **Step 3: Create the project**, add it to the solution, restore without locked mode to write its lock file, and grant the `InternalsVisibleTo`.
- [ ] **Step 4: Run** `dotnet test --project tests/SqlSource.Tests.Postgres.Npgsql8`.  Expected: PASS.  `dotnet list tests/SqlSource.Tests.Postgres.Npgsql8 package --include-transitive` shows no Npgsql 10.
- [ ] **Step 5: Validate and commit**, message `Read every mapped type on Npgsql 8`.

### Task 7: The documents, and the epic's row set to Done

**Files:**
- Modify: `tests/SqlSource.Tests.Postgres/README.md`: the intro's list of what it proves gains the end-to-end project.  A section, "The end-to-end project", covers the databases, the queries and what covers which setting, the script and when to run it, that camelCase parameters are the rule, and that a new `all_types` column needs an expectation in `AllTypesRows`.  The image section adds the end-to-end sidecars to what a new minor records again.
- Modify: `tests/README.md`: five test projects; the table's row and a short section for the Npgsql 8 project; the graph; nightly leaves out both projects that need a container.
- Modify: `tests/AGENTS.md`: five projects.  Rules for `EndToEnd/`: sidecars are written by the script alone, parameters are camelCase, a Dapper test reads and a write is rolled back, `ErrorPaths/` is never compiled, and `all_types` and `AllTypesRows` change together.  The Npgsql 8 project holds the `all_types` read alone and references nothing that brings Npgsql 10.
- Modify: `tools/README.md` and `tools/AGENTS.md`: the third recording script, which stays out of `pre-commit-validation.sh` and `build.yml`.
- Modify: `CONTRIBUTING.md`: the projects' count and the Npgsql 8 project's sentence; the script beside the other two.
- Modify: `docs/publishing.md`: the release step that raises `VersionPrefix` runs `tools/describe-end-to-end.sh` and commits the sidecars it rewrites.
- Modify: `AGENTS.md`, rule 5: the end-to-end sidecars, by `tools/describe-end-to-end.sh`, are among what a new minor of the default image records again.
- Modify: `.github/workflows/README.md`: what `build.yml` and `nightly.yml` run.
- Modify: `docs/manual/recipes.md` (`#models-through-dapper`), as the tests found.  A model with an array column is a class.  A type override to a type the driver does not box needs `SqlMapper.AddTypeHandler`, with the `DateTimeOffset` handler as the sample.  A PostgreSQL enum mapped to a C# enum needs `MapEnum<T>()`.  For a parameter Dapper cannot send, the types of `WriteSideLeftOut`, give an `NpgsqlParameter` with `DataTypeName`, or cast in the SQL.
- Modify: `docs/manual/generator.md`, its known issues: a model with an array column read through Dapper fails unless it is a class.
- Create: `docs/tech-debt/TD-<next>-record-models-with-arrays-and-dapper.md`, and its row and `Next id` in `docs/tech-debt/README.md`.
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`: the 5.4 row `Done`, and "Dapper and sorting" under Models gains the array rule.

- [ ] **Step 1: Write the documents.**  Run the grep of `docs/manual/AGENTS.md` over the manual pages changed and rewrite each hit.
- [ ] **Step 2: Check the version** (`git fetch --tags origin`; no `v*` tag, nothing to compare) and the file count (`git diff --stat origin/main | tail -n 1`, at most 150).
- [ ] **Step 3: Validate** with `./pre-commit-validation.sh`, and the PostgreSQL project on `postgres:14.24`.  Commit, message `Document the end-to-end project and close phase 5.4`.
