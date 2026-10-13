# Query generation, phase 3.2: the session and the parameters - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A PostgreSQL describer over Npgsql that opens a session and types a query's parameters through `PREPARE`, with the container test project, the recording script and the first four fixtures; registered nowhere.

**Architecture:** `src/SqlSource.Tool/Describing/Postgres/` holds the describer.  Every call to the server, `open`, `prepare`, `types` and `deallocate`, goes through the `IDescribeExchange` of sub-phase 3.1 as a plain record, so a describe is recorded and replayed.  The pure parts, the statement check, the `$n` rewrite, the grammar of a declared type, the type closure and the map from a server error to a diagnostic, use no driver and are tested in `tests/SqlSource.Tool.Tests`; the session is tested there too, over a replay the test writes, and against a real server in the new `tests/SqlSource.Tests.Postgres`.

**Tech Stack:** C# (`LangVersion` `preview`); the tool on `net8.0`, the tests on `net10.0`; Npgsql 10.0.3; Testcontainers.PostgreSql 4.16.0; xunit v3 on Microsoft.Testing.Platform with Shouldly; Docker.

**Spec:** `docs/superpowers/specs/2026-10-12-query-generation-phase-3-2-session-and-parameters-design.md`.  Read it with this plan.

**Branch:** `claude/query-generation-phase-3-2-session-and-parameters`, which exists and holds one commit, the spec as amended.  This file is untracked until Task 1 commits it.

## Where the plan settles what the spec leaves open

A spike against PostgreSQL 14.24 and 18.4 through Npgsql 10.0.3 was run for this plan.  Three of its findings change what the spec says; the rest are choices the spec leaves.

- **The server accepts a pseudo-type as a parameter's type, so one is a failure and not a throw.**  The spec says the server refuses a pseudo-type in a `PREPARE` list.  It does not: `PREPARE s (record) AS SELECT $1` succeeds, and `SELECT row_to_json(@p)` and `SELECT int4in(@p)` infer `record` and `cstring`.  A `.sql` file can cause both, so neither may throw.  A declared one is `SQLSRC228` with a third reason, `it is a pseudo-type`; an inferred one is `SQLSRC226`, with a help line that names the pseudo-type.  Both have the step `resolve type` and no `server:` line, since the server said nothing.
- **A `$n` that the SQL writes itself is typed by the server and is no error.**  `PREPARE s AS SELECT 1 FROM users WHERE id = $1` succeeds with one parameter type and no list.  So the answer to `prepare` may hold more types than the query has parameters: the describer takes the first of them, one for each parameter of the list, and throws only for fewer.  `docs/tech-debt` records it.
- **`format_type(oid, -1)` of `char` is `bpchar`.**  The spec's `character(2)` for a length cannot be made by adding `(2)` to it, so `PostgresTypeNames` writes `character(n)` itself; without a length the name is `bpchar`, as the server gives it.
- **A connection string Npgsql cannot read is `SQLSRC224` with one fixed line.**  `NpgsqlDataSource.Create` throws `ArgumentException` for `Port=x`, an unknown keyword and an empty string, and the inner exception quotes the value.  A user causes it, so it is no `SQLSRC200`.  `PostgresConnection` catches it and throws an `NpgsqlException` with the text `Npgsql cannot read the connection string` and no inner exception, which `open` answers as its `error`.
- **A `-` in a modifier stands before a digit.**  The spec allows `-` anywhere inside the parentheses, which lets `numeric(--)` start a comment in the `PREPARE`.  Nothing could be run through it, since a comment only removes text, but the grammar refuses it.
- **A position is counted in characters, as the server counts.**  `PostgresException.Position` counts code points, so the length of the `PREPARE` text before the SQL is counted in runes.
- **An OID is a `long` in a record.**  An OID is an unsigned 32-bit number, and a record of a call may hold `int` and `long`.
- **The members of a row of `types` are named as the catalog names them**: `oid`, `typname`, `nspname`, `typtype`, `typcategory`, `typelem`, `typbasetype`, `rngsubtype`, `multirangeRngsubtype`, `formatType`, `enumLabels`.
- **`types` is not asked for a query without parameters.**  There is no OID to ask for.  Sub-phase 3.3 asks it for the columns.
- **`driverVersion` is the first three parts of the Npgsql assembly's version**, `10.0.3`, as `PackageVersion.Prefix` is for the tool.
- **A `42P18` or `42P08` whose message names no `$n` of the list is `SQLSRC225`.**  The message of `SQLSRC226` and `SQLSRC227` needs a parameter to name.
- **An error positioned in the type list at an item that is `unknown` is `SQLSRC225`.**  `SQLSRC228` names a declared type.
- **A sample SQL that does not lex throws.**  The plan's parser lexed the same text, so it is a bug of the plan, as a `Parameter` lexeme outside the list is.
- **A hint after the trailing `;` is a comment.**  The server reads it as one.
- **`CA2100` is turned off for one file.**  The rule flags a command whose text is not a constant, and `PREPARE` and `DEALLOCATE` take no parameters.  `PostgresConnection.CommandAsync` is the one place that makes a command, and `.editorconfig` turns the rule off for that file with the reason, as it does for `Cli.cs`.
- **`PostgresSession.Connection` is internal**, for the container tests: `pg_prepared_statements` shows the statements of its own session alone, so a test that counts what a describe left behind asks on the session's connection.
- **The session is tested without a server too.**  `tests/SqlSource.Tool.Tests/PostgresScript.cs` builds a capture from records the test writes, and a `ReplayingExchange` over it answers the session.  A request the session does not ask as the test wrote it is a `CaptureMissException`, so these tests hold the requests as well as the results.
- **`DescribeAsync` grows over Tasks 3 to 6.**  The describer is registered nowhere, so each commit may describe less than the next: Task 3 answers a description without parameters, Task 4 refuses before that, Task 5 prepares and frees, Task 6 types.
- **Npgsql is pinned in Task 2 and referenced by the tool in Task 3.**  The container's fixture applies the schema with it.
- **The test of `SecretTests` connects to `127.0.0.1:1`**, which refuses at once and needs no Docker, so `nightly.yml` runs it on Windows and macOS.
- **`tools/record-fixtures.sh` reads the default image from `PostgresContainer.cs`**, so the tag stays in one place.
- **The collection of the container tests is `UsesPostgres`.**  `CA1711` refuses a type whose name ends in `Collection`.

## Global Constraints

- The supported floor is PostgreSQL 14.  The default image is `postgres:18`; `SQLSOURCE_TESTS_POSTGRES_IMAGE` overrides it; the tag is in one place, `PostgresContainer.DefaultImage`.
- The describer is registered nowhere: `ToolHost.Create` is unchanged, and the released tool says `SQLSRC216` for `postgres`.
- A describer returns a `DescribeFailure` for what a server or a user can cause and never throws for it.  A throw is a bug, `SQLSRC200`.
- Every call to the server goes through `OpenRequest.Exchange`.  The connection is made inside a `live` function and nowhere else: under a replay nothing of Npgsql runs.
- A request and an answer are sealed records of `string`, `int`, `long`, `bool`, nullable ones, `IReadOnlyList<T>` of them and records of the same kind.  A request holds nothing of a connection.  A call that can fail has an answer with a nullable `Error`.
- The methods are `open`, `prepare`, `types` and `deallocate`.  A method's name and a record's member names are a contract with the captures: none is renamed after Task 8 records.
- A prepared statement is named `sqlsource_<n>`, counted from 1 in each session.
- Nothing the describer returns or logs holds the connection's value or a part of it.  The driver's own message of a failed `open` is the one exception, which `docs/tech-debt/TD-0034` records.
- Nothing in the tool reads `DateTime` or `Stopwatch`: the clock is `OpenRequest.Time`.
- The ids are `SQLSRC224` to `SQLSRC228` and `SQLSRC230`, with the titles and messages of the spec.  `SQLSRC229`, `SQLSRC231` and `SQLSRC232` are sub-phase 3.3's.
- A new descriptor touches four places in one commit: the descriptor and `ToolDiagnostics.All`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`, and the tests of the descriptors.
- `README.md`, `docs/publishing.md` and `src/SqlSource.Tool/README.md` are not touched.  Nothing is expected for `docs/deferred`.
- After a change to a package reference, run `dotnet restore SqlSource.slnx` and commit every `packages.lock.json` it changed.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`, Roslynator and Sonar.  Fix what it reports in the code; never suppress, but for the two cases this plan names: `CA2000` on `PostgresSession.OpenAsync`, as `FakeDescriber.OpenAsync` has it, and `CA2100` for `PostgresConnection.cs` in `.editorconfig`.  If another check is wrong, stop and tell the user.
- `ImplicitUsings` is off: a file names every namespace it uses.
- Comments say what a thing is for, in the plain declarative style of the files around them, with two spaces after a full stop.  Types under `src/` have XML documentation; test files use `//` comments.
- Test names are `Method_Scenario_Outcome`; assertions are Shouldly's.  A test takes its token from `TestContext.Current.CancellationToken`.
- Every commit passes `./pre-commit-validation.sh`, which needs Docker running and, from Task 2, starts a PostgreSQL container.  Run `./format.sh`, then the validation, then commit in a command of its own.  Never work around the hook.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- **A connection string the driver cannot read**: `Port=x`, an unknown keyword, an empty value.  A user expects an error at the query that says the string is wrong, without the string; not `SQLSRC200` with a stack of Npgsql.  Task 3, `OpenAsync_ConnectionStringNpgsqlCannotRead_IsSqlsrc224WithoutTheDriversText`.
- **A parameter the server types as a pseudo-type**: `SELECT row_to_json(@p)`, or `-- param: @p record`.  A user expects an error that names the parameter and says what to write.  Task 6, `DescribeAsync_InferredPseudoType_IsSqlsrc226NamingIt`, `DescribeAsync_DeclaredPseudoType_IsSqlsrc228` and the container's `Describe_ParameterOfAPseudoType_IsAFailureAndNoThrow`.
- **A `$1` the user wrote beside `@name` parameters.**  The server answers one type more than the list has.  A user expects the query's own parameters typed, not a crash.  Task 6, `DescribeAsync_MoreTypesThanParameters_TypesTheListAlone` and the container's `Describe_SqlWithAPlaceholderOfItsOwn_TypesTheListedParametersAlone`.
- **A query described after one the server rejected, in one session.**  The rejected `PREPARE` left no statement, and the next must not meet `42P05` or a broken connection.  Task 5, the container's `Describe_AfterAQueryTheServerRejected_DescribesTheNextOne`.
- **A describe that is cancelled.**  A cancelled run must end as cancelled: an `OperationCanceledException`, not a `SQLSRC225` that quotes the driver's "operation was canceled".  Task 5, the container's `Describe_CancelledToken_ThrowsAndIsNoFailure`.

## File structure

Created under `src/SqlSource.Tool/Describing/Postgres/`:

| File | Is |
|----|----|
| `PostgresCalls.cs` | The request and answer records of the four calls, `PostgresServerError`, `PostgresSetting` and `PostgresTypeRow` |
| `PostgresConnection.cs` | Holds the connection's value; makes the data source, the connection and every command |
| `PostgresDescriber.cs` | `IQueryDescriber` for `SqlDialect.PostgreSql` |
| `PostgresSession.cs` | `IDescribeSession`: `open`, `DescribeAsync`, the four `live` functions |
| `DeclaredTypeText.cs` | The grammar of a declared type |
| `ParameterPlaceholders.cs` | The statement check, the `$n` rewrite, the text of the `PREPARE` and where a position falls in it |
| `PostgresErrors.cs` | A `PostgresServerError`, or a refusal of the tool, as a `DescribeFailure` |
| `PostgresFacets.cs` | A length, a precision and a scale; none in this sub-phase |
| `PostgresTypes.cs` | A `PostgresType` from the rows of the closure |
| `PostgresTypeNames.cs` | A type's `name` |
| `PostgresEvents.cs` | The three events of the log |

Created under `tests/SqlSource.Tests.Postgres/`: `SqlSource.Tests.Postgres.csproj`, `RepositoryPaths.cs`, `PostgresContainer.cs`, `UsesPostgres.cs`, `PostgresContainerTests.cs`, and in `Describer/`: `Sessions.cs`, `Requests.cs`, `SessionTests.cs`, `ParameterTests.cs`, `FixtureCasesTests.cs`.

Created under `tests/SqlSource.Tool.Tests/`: `PostgresScript.cs`, `PostgresRows.cs`; in `Describing/Postgres/`: `PostgresDescriberTests.cs`, `DeclaredTypeTextTests.cs`, `ParameterPlaceholdersTests.cs`, `PostgresErrorsTests.cs`, `PostgresSessionTests.cs`, `PostgresTypesTests.cs`, `PostgresTypeNamesTests.cs`, `PostgresEventsTests.cs`; in `Fixtures/`: `DescriberFixtureTests.cs` and `Describers/postgres/` with `schema.sql` and four cases.

Created elsewhere: `tools/record-fixtures.sh`, `docs/tech-debt/TD-0043-small-gaps-of-the-postgresql-describers-parameters.md`.

Modified: `Directory.Packages.props`, `SqlSource.slnx`, the two `.csproj` under `src/`, `.editorconfig`, `.github/workflows/build.yml`, `.github/workflows/nightly.yml`, `tools/check-package.sh`, `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`, `docs/diagnostics.md`, `CONTRIBUTING.md`, the `AGENTS.md` of `tests/`, `tools/` and `src/SqlSource.Tool/`, `docs/tech-debt/README.md`, `TD-0034` and `TD-0041`, the epic outline, and the lock files.

---

### Task 1: The plan and the outline

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (the row of 3.2 in the Phases table)
- Add to git: `docs/superpowers/plans/2026-10-13-query-generation-phase-3-2-session-and-parameters.md`

**Interfaces:**
- Produces: nothing a later task consumes.

- [ ] **Step 1: Check the branch**

Run: `git branch --show-current`
Expected: `claude/query-generation-phase-3-2-session-and-parameters`

- [ ] **Step 2: Set the row of 3.2 to In progress**

In `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, the row of the Phases table that starts `| 3.2 The session and the parameters | Not started |` becomes `| 3.2 The session and the parameters | In progress |`.  Nothing else of the row changes.

- [ ] **Step 3: Validate**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-10-07-query-generation-epic-design.md docs/superpowers/plans/2026-10-13-query-generation-phase-3-2-session-and-parameters.md
```

```bash
git commit -m "Mark sub-phase 3.2 in progress in the outline

The plan of the sub-phase comes with it.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The test project and its container

**Files:**
- Create: `tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`
- Create: `tests/SqlSource.Tests.Postgres/RepositoryPaths.cs`
- Create: `tests/SqlSource.Tests.Postgres/PostgresContainer.cs`
- Create: `tests/SqlSource.Tests.Postgres/UsesPostgres.cs`
- Create: `tests/SqlSource.Tests.Postgres/PostgresContainerTests.cs`
- Create: `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql`
- Create: `tests/SqlSource.Tests.Postgres/packages.lock.json` (by the restore)
- Modify: `Directory.Packages.props`, `SqlSource.slnx`, `src/SqlSource/SqlSource.csproj`, `src/SqlSource.Tool/SqlSource.Tool.csproj`
- Modify: `.github/workflows/build.yml`, `.github/workflows/nightly.yml`
- Modify: `CONTRIBUTING.md`, `tests/AGENTS.md`, `src/SqlSource.Tool/AGENTS.md`

**Interfaces:**
- Consumes: `FixtureCase`, `FixtureCases`, `FixtureReplay` of `tests/SqlSource.Tool.Tests/Fixtures/` and `TestSidecars` of `tests/SqlSource.Tests/Snapshot/`, compiled in by path.
- Produces, in the namespace `SqlSource.Tests.Postgres`:
  - `public sealed class PostgresContainer : IAsyncLifetime` with `const string DefaultImage = "postgres:18"`, `const string ImageVariable = "SQLSOURCE_TESTS_POSTGRES_IMAGE"`, `const string Database = "fixtures"`, `static string Image`, `string ConnectionString`
  - `public sealed class UsesPostgres : ICollectionFixture<PostgresContainer>` with `const string Name = "postgres"`
  - `internal static class RepositoryPaths` with `string Root`, `string DescriberFixtures`, `string PostgresSchema`

- [ ] **Step 1: Pin the two packages**

In `Directory.Packages.props`, add in the alphabetical place of each:

```xml
        <!--
            The driver of the PostgreSQL describer.  The tool's driver is its own: the floor of the Npgsql that a
            consumer's project may use is decided with the generated code, not here.
        -->
        <PackageVersion Include="Npgsql" Version="10.0.3" />
```

after the entry of `Microsoft.VisualStudio.SolutionPersistence`, and

```xml
        <!-- Starts the PostgreSQL that tests/SqlSource.Tests.Postgres describes against.  It needs Docker. -->
        <PackageVersion Include="Testcontainers.PostgreSql" Version="4.16.0" />
```

after the entry of `System.CommandLine`.

- [ ] **Step 2: Let the new project see the internals**

In `src/SqlSource/SqlSource.csproj`, after `<InternalsVisibleTo Include="SqlSource.Tests.RoslynFloor" />`:

```xml
        <InternalsVisibleTo Include="SqlSource.Tests.Postgres" />
```

In `src/SqlSource.Tool/SqlSource.Tool.csproj`, the item group of `InternalsVisibleTo` becomes:

```xml
    <ItemGroup>
        <InternalsVisibleTo Include="SqlSource.Tests.Postgres" />
        <InternalsVisibleTo Include="SqlSource.Tool.Tests" />
    </ItemGroup>
```

- [ ] **Step 3: Write the project file**

`tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <OutputType>Exe</OutputType>
        <IsPackable>false</IsPackable>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="GitHubActionsTestLogger" />
        <!-- The version the tool itself references: central pinning would otherwise hold it at the generator's. -->
        <PackageReference Include="Microsoft.CodeAnalysis.CSharp" VersionOverride="5.9.0" />
        <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" />
        <PackageReference Include="Npgsql" />
        <PackageReference Include="Shouldly" />
        <PackageReference Include="Testcontainers.PostgreSql" />
        <PackageReference Include="xunit.v3" />
    </ItemGroup>
    <ItemGroup>
        <!--
            The harness of a describer's fixtures, which FixtureCasesTests describes live and records with.
            tests/SqlSource.Tool.Tests owns the three files and replays the same cases with no database.
        -->
        <Compile Include="../SqlSource.Tool.Tests/Fixtures/FixtureCase.cs" Link="Fixtures/FixtureCase.cs" />
        <Compile Include="../SqlSource.Tool.Tests/Fixtures/FixtureCases.cs" Link="Fixtures/FixtureCases.cs" />
        <Compile Include="../SqlSource.Tool.Tests/Fixtures/FixtureReplay.cs" Link="Fixtures/FixtureReplay.cs" />
        <!-- The builder of a sidecar's records.  tests/SqlSource.Tests owns the file. -->
        <Compile Include="../SqlSource.Tests/Snapshot/TestSidecars.cs" Link="Snapshot/TestSidecars.cs" />
    </ItemGroup>
    <ItemGroup>
        <ProjectReference Include="../../src/SqlSource.Tool/SqlSource.Tool.csproj" />
        <ProjectReference Include="../../src/SqlSource/SqlSource.csproj" />
    </ItemGroup>
</Project>
```

In `SqlSource.slnx`, the folder `/tests/` becomes:

```xml
  <Folder Name="/tests/">
    <Project Path="tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj" />
    <Project Path="tests/SqlSource.Tests.RoslynFloor/SqlSource.Tests.RoslynFloor.csproj" />
    <Project Path="tests/SqlSource.Tests/SqlSource.Tests.csproj" />
    <Project Path="tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj" />
  </Folder>
```

- [ ] **Step 4: Write the schema**

`tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql`.  The spike applied this text to PostgreSQL 14 and 18.

```sql
-- The schema every fixture case of the PostgreSQL describer is recorded against, and every container test of
-- tests/SqlSource.Tests.Postgres runs against.  A change to it can move the OIDs of its types, which the captures
-- hold: record every case again with tools/record-fixtures.sh.

CREATE SCHEMA billing;

CREATE DOMAIN email_t AS varchar(254) CHECK (VALUE LIKE '%@%');

CREATE TYPE user_status AS ENUM ('active', 'disabled');

CREATE TABLE users (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name varchar(100) NOT NULL,
    email email_t,
    status user_status NOT NULL DEFAULT 'active',
    tags text[] NOT NULL DEFAULT '{}',
    balance numeric(18, 2) NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now(),
    deleted_at timestamptz,
    name_upper text GENERATED ALWAYS AS (upper(name)) STORED
);

CREATE TABLE orders (
    id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    user_id integer NOT NULL REFERENCES users (id),
    total numeric(18, 2) NOT NULL,
    placed_at timestamptz NOT NULL DEFAULT now(),
    note text
);

CREATE TABLE bookings (
    id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    user_id integer NOT NULL REFERENCES users (id),
    during tstzrange NOT NULL,
    slots int4multirange,
    tags text[] NOT NULL DEFAULT '{}'
);

-- One column for each base type the describer's tests name.
CREATE TABLE samples (
    id integer PRIMARY KEY,
    flag boolean NOT NULL,
    small smallint,
    big bigint,
    ratio real,
    amount double precision,
    code char(2),
    label text,
    born date,
    at_time time,
    at_local timestamp(3),
    span interval,
    token uuid,
    payload bytea,
    document jsonb
);

CREATE TABLE billing.invoices (
    id integer PRIMARY KEY,
    order_id bigint NOT NULL,
    amount numeric(18, 2) NOT NULL,
    issued_on date NOT NULL
);

CREATE VIEW active_users AS
SELECT id, name, email, status
FROM users
WHERE deleted_at IS NULL;

CREATE VIEW user_orders AS
SELECT u.id AS user_id, u.name, o.id AS order_id, o.total
FROM users AS u
LEFT JOIN orders AS o ON o.user_id = u.id;
```

In `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`, the comment above `<None Remove="Fixtures/Describers/**" />` ends `The first case comes with the first describer.`  Replace that sentence with `The engine's schema.sql is beside its cases.`

- [ ] **Step 5: Write the failing test**

`tests/SqlSource.Tests.Postgres/PostgresContainerTests.cs`:

```csharp
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres;

// The container itself: the image the variable names, the database of the fixtures, and its schema applied.
[Collection(UsesPostgres.Name)]
public sealed class PostgresContainerTests(PostgresContainer postgres)
{
    [Fact]
    public async Task ConnectionString_OfTheContainer_OpensTheFixtureDatabaseOfTheImage()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(
            "SELECT current_database(), current_setting('server_version'), "
                + "to_regclass('billing.invoices') IS NOT NULL, to_regclass('public.user_orders') IS NOT NULL"
        );

        await using var reader = await command.ExecuteReaderAsync(token);

        (await reader.ReadAsync(token)).ShouldBeTrue();
        reader.GetString(0).ShouldBe(PostgresContainer.Database);
        reader.GetBoolean(2).ShouldBeTrue();
        reader.GetBoolean(3).ShouldBeTrue();
        // "postgres:18" runs a server whose version starts "18.".  A tag that starts with no number says nothing.
        var major = new string([.. PostgresContainer.Image.Split(':')[^1].TakeWhile(char.IsAsciiDigit)]);
        if (major.Length > 0)
        {
            reader.GetString(1).ShouldStartWith(major + ".");
        }
    }

    [Fact]
    public void Image_WithoutTheVariable_IsTheDefault()
    {
        if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(PostgresContainer.ImageVariable)))
        {
            PostgresContainer.Image.ShouldBe(PostgresContainer.DefaultImage);
        }
        else
        {
            PostgresContainer.Image.ShouldNotBeEmpty();
        }
    }
}
```

- [ ] **Step 6: Run it to see it fail**

Run: `dotnet build tests/SqlSource.Tests.Postgres`
Expected: FAIL, `PostgresContainer` and `UsesPostgres` do not exist.

- [ ] **Step 7: Write the container and its helpers**

`tests/SqlSource.Tests.Postgres/RepositoryPaths.cs`:

```csharp
using System;
using System.IO;

namespace SqlSource.Tests.Postgres;

// Where the files of the repository are, found from this assembly.  A describer's fixtures are read and recorded
// in the repository itself and not in an output folder: recording writes there.
internal static class RepositoryPaths
{
    private const string Solution = "SqlSource.slnx";

    public static string Root { get; } = FindRoot();

    // The folder with one folder for each engine: its schema.sql and its cases.
    public static string DescriberFixtures { get; } =
        Path.Combine(Root, "tests", "SqlSource.Tool.Tests", "Fixtures", "Describers");

    public static string PostgresSchema { get; } = Path.Combine(DescriberFixtures, "postgres", "schema.sql");

    private static string FindRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, Solution)))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException($"No folder above '{AppContext.BaseDirectory}' holds {Solution}.");
    }
}
```

`tests/SqlSource.Tests.Postgres/PostgresContainer.cs`:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace SqlSource.Tests.Postgres;

// One PostgreSQL for every test of the collection UsesPostgres: the image, the database "fixtures", and the fixture
// schema applied to it once.  Testcontainers removes the container when the tests end, and its reaper does when
// the process is killed.
public sealed class PostgresContainer : IAsyncLifetime
{
    // The one place that names the image.  tools/record-fixtures.sh reads it from this line.
    public const string DefaultImage = "postgres:18";

    // Names another image: CI runs the tests again on the oldest PostgreSQL that is supported.
    public const string ImageVariable = "SQLSOURCE_TESTS_POSTGRES_IMAGE";

    public const string Database = "fixtures";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).WithDatabase(Database).Build();

    public static string Image =>
        Environment.GetEnvironmentVariable(ImageVariable) is { Length: > 0 } image ? image : DefaultImage;

    // Of the database "fixtures".
    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await _container.StartAsync(token);
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        await using var command = source.CreateCommand(
            await File.ReadAllTextAsync(RepositoryPaths.PostgresSchema, token)
        );
        _ = await command.ExecuteNonQueryAsync(token);
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
```

`tests/SqlSource.Tests.Postgres/UsesPostgres.cs`:

```csharp
using Xunit;

namespace SqlSource.Tests.Postgres;

// The collection of the tests that describe against the container.  Its classes share one PostgresContainer and
// run one after another, as a session describes one query at a time.
[CollectionDefinition(Name)]
public sealed class UsesPostgres : ICollectionFixture<PostgresContainer>
{
    public const string Name = "postgres";
}
```

- [ ] **Step 8: Restore, and run the tests**

Run: `dotnet restore SqlSource.slnx`
Expected: `tests/SqlSource.Tests.Postgres/packages.lock.json` is written.  Other lock files do not change in this task.

Run: `dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: PASS, 2 tests.  The first run pulls `postgres:18`.

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres --no-build`
Expected: PASS, 2 tests, on a server whose version starts `14.`.

- [ ] **Step 9: Run the floor in CI**

In `.github/workflows/build.yml`, after the step `Test` and before `Report coverage`:

```yaml
      # The Test step ran the tests of the PostgreSQL describer on the default image.  This runs them again on the
      # oldest PostgreSQL that is supported.
      - name: Test on PostgreSQL 14
        if: ${{ !cancelled() && steps.build.outcome == 'success' }}
        env:
          SQLSOURCE_TESTS_POSTGRES_IMAGE: postgres:14
        run: >-
          dotnet test --project tests/SqlSource.Tests.Postgres --configuration "$CONFIGURATION" --no-build
          --report-github
```

In `.github/workflows/nightly.yml`, the third line of the header comment, `# No check that needs Docker, and no pack: the lint images are Linux images, and build.yml has both.`, becomes:

```yaml
# No check that needs Docker, no test that needs a container, and no pack: the lint images and the PostgreSQL image
# are Linux images, and build.yml has all three.
```

and the step `Test` becomes:

```yaml
      # Each project but tests/SqlSource.Tests.Postgres, whose container these runners cannot start.  Every project
      # runs even when an earlier one failed.
      - name: Test
        run: |
          status=0
          for project in tests/SqlSource.Tests tests/SqlSource.Tests.RoslynFloor tests/SqlSource.Tool.Tests; do
              dotnet test --project "$project" --configuration "$CONFIGURATION" --no-build \
                  --results-directory "artifacts/test-results/$(basename "$project")" \
                  --report-github || status=1
          done
          exit "$status"
```

Run: `tools/actionlint.sh`
Expected: no finding.

- [ ] **Step 10: Bring the documents this makes wrong**

`CONTRIBUTING.md`:

1. The row of Docker in Required tools: the Notes cell becomes `Runs the pinned lint images used by the scripts in ``tools/``, and the PostgreSQL that ``tests/SqlSource.Tests.Postgres`` describes against` (with single backticks).
2. `The solution has two projects that ship and three test projects:` becomes `The solution has two projects that ship and four test projects:`.
3. A row at the end of the table of test projects:

```markdown
| `tests/SqlSource.Tests.Postgres` | A current version | The PostgreSQL describer against a real server, which Testcontainers starts in Docker: one container for the run, with the database `fixtures` and the schema `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql`.  It compiles the fixture harness of `tests/SqlSource.Tool.Tests/Fixtures/`, three files, and `tests/SqlSource.Tests/Snapshot/TestSidecars.cs` |
```

4. A paragraph after the one that starts `` `tests/SqlSource.Tool.Tests/Fixtures/Projects/` holds small projects ``:

````markdown
`tests/SqlSource.Tests.Postgres` needs Docker running, and its first run pulls the image.  The image is `postgres:18`, named in one place, `PostgresContainer.DefaultImage`; the variable `SQLSOURCE_TESTS_POSTGRES_IMAGE` names another, and PostgreSQL 14 is the oldest that is supported:

```bash
SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres
```

`dotnet test --solution SqlSource.slnx`, and so `pre-commit-validation.sh`, runs it on the default image.
````

5. In the table of workflows, the Does cell of `build.yml` becomes `Every check in ``pre-commit-validation.sh``, a ``Release`` build, the tests with coverage, the tests of ``tests/SqlSource.Tests.Postgres`` again on PostgreSQL 14, ``dotnet pack``, and the three package checks`, and that of `nightly.yml` becomes `Restores, builds in ``Release`` and runs the tests that need no container on ``windows-latest`` and ``macos-latest``` (with single backticks).
6. In the paragraph that starts `` `ci.yml` runs on Linux alone. ``, the sentence `It runs no check that needs Docker and packs nothing, and it is no required check` becomes `It runs no check that needs Docker, not ``tests/SqlSource.Tests.Postgres``, whose container its runners cannot start, and packs nothing, and it is no required check`.

`tests/AGENTS.md`:

1. `Three xunit projects.` becomes `Four xunit projects.`
2. A bullet after the one that starts `` **`tests/SqlSource.Tests/Snapshot/TestSidecars.cs` is compiled into ``:

```markdown
- **`tests/SqlSource.Tests.Postgres` compiles four files of the other test projects**, by exact path in its `.csproj`: `FixtureCase.cs`, `FixtureCases.cs` and `FixtureReplay.cs` of `tests/SqlSource.Tool.Tests/Fixtures/`, and `tests/SqlSource.Tests/Snapshot/TestSidecars.cs`.  A move or rename of one changes that file, and a helper one of them needs goes into the file itself.  Its tests are in the collection `UsesPostgres`, which starts one container for the run through `tests/SqlSource.Tests.Postgres/PostgresContainer.cs`, the one place that names the image.  It reads `tests/SqlSource.Tool.Tests/Fixtures/Describers/` from the repository, found by walking up to `SqlSource.slnx`, and not from an output folder.  `nightly.yml` leaves the project out.
```

`src/SqlSource.Tool/AGENTS.md`: in the bullet that starts `**The fixtures of a describer are in**`, the last sentence, `The folder does not exist until the first describer brings its cases.`, becomes `` `postgres/schema.sql` is the schema that `tests/SqlSource.Tests.Postgres` applies to its container; a change to it can move the OIDs that the captures hold, and every case is recorded again. ``

- [ ] **Step 11: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`
Expected: every step `passed`.

```bash
git add Directory.Packages.props SqlSource.slnx src/SqlSource/SqlSource.csproj src/SqlSource.Tool/SqlSource.Tool.csproj tests/SqlSource.Tests.Postgres tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj .github/workflows/build.yml .github/workflows/nightly.yml CONTRIBUTING.md tests/AGENTS.md src/SqlSource.Tool/AGENTS.md
```

```bash
git commit -m "Add the test project that describes against PostgreSQL in a container

One container for the run, with the fixture schema, on postgres:18 and in
CI on postgres:14 as well.  The nightly run leaves the project out.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The describer, its session and `open`

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`
- Create: `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs`
- Create: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`
- Create: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`
- Create: `src/SqlSource.Tool/Describing/Postgres/PostgresDescriber.cs`
- Create: `tests/SqlSource.Tool.Tests/PostgresScript.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresDescriberTests.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs`
- Create: `tests/SqlSource.Tests.Postgres/Describer/Sessions.cs`
- Create: `tests/SqlSource.Tests.Postgres/Describer/SessionTests.cs`
- Modify: `src/SqlSource.Tool/SqlSource.Tool.csproj`, `.editorconfig`, `tools/check-package.sh`, `CONTRIBUTING.md`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Modify: `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`
- Modify: the `packages.lock.json` of `src/SqlSource.Tool`, `tests/SqlSource.Tool.Tests` and `tests/SqlSource.Tests.Postgres`

**Interfaces:**
- Consumes: `IQueryDescriber`, `IDescribeSession`, `IDescribeExchange`, `OpenRequest`, `DescribeRequest`, `DescribeFailure`, `QueryDescription`, `ServerInfo`, `ServerSetting` of `Describing/Describers/`; `Outcome<TValue, TFailure>`; `DescribeCapture`, `CaptureCall`, `CaptureJson`, `ReplayingExchange` of `Describing/Capture/`.
- Produces, in the namespace `SqlSource.Tool.Describing.Postgres`:
  - `sealed record PostgresOpenRequest` with `static PostgresOpenRequest Instance`
  - `sealed record PostgresOpenAnswer(string? ServerVersion, string? DriverVersion, IReadOnlyList<PostgresSetting>? Settings, PostgresServerError? Error)`
  - `sealed record PostgresSetting(string Name, string Value)`
  - `sealed record PostgresServerError(string? SqlState, string Message, string? Detail, string? Hint, int? Position)`
  - `sealed class PostgresConnection(string connectionString) : IAsyncDisposable` with `const string UnreadableConnectionString`, `static string? DriverVersion`, `Task<NpgsqlConnection> OpenAsync(CancellationToken)`, `Task<NpgsqlCommand> CommandAsync(string sql, CancellationToken)`, `static PostgresServerError ErrorOf(NpgsqlException)`
  - `static class PostgresErrors` with `const string OpenHelp`, `DescribeFailure Open(PostgresServerError error, string database)`, `EquatableArray<string> ServerLines(PostgresServerError error, int head)`
  - `sealed class PostgresSession : IDescribeSession` with `const string DriverName = "Npgsql"`, `static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(OpenRequest, CancellationToken)`, `static string VersionOf(string serverVersion)`, `ServerInfo Server`, `internal PostgresConnection Connection`
  - `sealed class PostgresDescriber : IQueryDescriber`
  - `ToolDiagnostics.DatabaseCannotBeOpened`, `SQLSRC224`
- Produces, in the tests: `SqlSource.Tool.Tests.PostgresScript` with `Answer`, `Open`, `Play`, `OpenAsync`, `SessionAsync`; `SqlSource.Tests.Postgres.Describer.Sessions` with `OpenAsync` and `TextsOf`.

- [ ] **Step 1: Reference Npgsql from the tool, and say so where the package is checked**

In `src/SqlSource.Tool/SqlSource.Tool.csproj`, the package references become:

```xml
        <PackageReference Include="Microsoft.CodeAnalysis.CSharp" VersionOverride="5.9.0" />
        <PackageReference Include="Microsoft.VisualStudio.SolutionPersistence" />
        <!-- The driver of the PostgreSQL describer, Describing/Postgres/. -->
        <PackageReference Include="Npgsql" />
        <PackageReference Include="System.CommandLine" />
```

Run: `dotnet restore SqlSource.slnx`
Expected: the lock files of `src/SqlSource.Tool`, `tests/SqlSource.Tool.Tests` and `tests/SqlSource.Tests.Postgres` change.

In `tools/check-package.sh`, the header comment's `the tool with the generator's` / `assembly, Roslyn and the library that reads a solution beside it` becomes `the tool with the generator's` / `assembly, Roslyn, the library that reads a solution and Npgsql beside it`, and `TOOL_REQUIRED` gains a line after the one of `Microsoft.VisualStudio.SolutionPersistence.dll`:

```bash
    'tools/net8.0/any/Npgsql.dll'
```

In `CONTRIBUTING.md`, under Package, `with the generator's assembly, Roslyn and the library that reads a solution beside it` becomes `with the generator's assembly, Roslyn, the library that reads a solution and Npgsql beside it`.

In `.editorconfig`, after the section `[src/SqlSource.Tool/Cli.cs]`:

```ini
[src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs]
# CA2100 (review SQL for user input): the one place that makes a command of the PostgreSQL describer.  PREPARE and
# DEALLOCATE take no parameters, and what the describer puts into them is a counter, a type that DeclaredTypeText
# read, and the user's own SQL, which is what is to be described.
dotnet_diagnostic.CA2100.severity = none
```

- [ ] **Step 2: Write the failing tests of the tool**

`tests/SqlSource.Tool.Tests/PostgresScript.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests;

// What a PostgreSQL would answer, written by a test: each call the describer is to make, with its answer.  Play
// gives the exchange that answers from it, with no database.  A request the describer asks otherwise than the test
// wrote it has no answer and is a CaptureMissException, so a test of this kind holds the requests too.
internal sealed class PostgresScript
{
    public const string Database = "app";

    // No connection string: under a replay nothing reads it.
    public const string Connection = "replay";

    private readonly DescribeCapture _capture = new(SidecarValues.Engine.Postgres);

    // What "open" answers for a server that opened.
    public static PostgresOpenAnswer Opened { get; } =
        new(
            "18.4",
            "10.0.3",
            [
                new PostgresSetting("search_path", "\"$user\", public"),
                new PostgresSetting("plan_cache_mode", "auto"),
                new PostgresSetting("standard_conforming_strings", "on"),
            ],
            null
        );

    public PostgresScript Answer<TRequest, TAnswer>(string method, TRequest request, TAnswer answer)
    {
        _capture.Add(new CaptureCall(method, CaptureJson.ToElement(request), CaptureJson.ToElement(answer)));
        return this;
    }

    public PostgresScript Open() => Answer("open", PostgresOpenRequest.Instance, Opened);

    // Made when the script is whole.
    public ReplayingExchange Play() => new(_capture);

    public static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        IDescribeExchange exchange,
        IRunLog? log = null,
        string connection = Connection
    ) =>
        new PostgresDescriber().OpenAsync(
            new OpenRequest(Database, connection, exchange, TimeProvider.System),
            log ?? NullRunLog.Instance,
            TestContext.Current.CancellationToken
        );

    // The session of a script whose "open" answered a server.
    public static async Task<IDescribeSession> SessionAsync(IDescribeExchange exchange, IRunLog? log = null) =>
        (await OpenAsync(exchange, log)).Value.ShouldNotBeNull();
}
```

`tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs`:

```csharp
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// A server's error, or a refusal of the tool, as the failure the run reports at the query.
public class PostgresErrorsTests
{
    [Fact]
    public void Open_ErrorWithASqlState_IsSqlsrc224AtNoStepWithTheServersLine()
    {
        var error = new PostgresServerError(
            "28P01",
            "password authentication failed for user \"app\"",
            null,
            null,
            null
        );

        var failure = PostgresErrors.Open(error, "billing");

        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.Arguments.ShouldBe(["billing"]);
        failure.Step.ShouldBeNull();
        failure.ServerLines.ShouldBe(["28P01: password authentication failed for user \"app\""]);
        failure.Help.ShouldBe(PostgresErrors.OpenHelp);
    }

    [Fact]
    public void Open_ErrorWithoutASqlState_HasTheMessageAlone() =>
        PostgresErrors
            .Open(new PostgresServerError(null, "Failed to connect to 127.0.0.1:1", null, null, null), "app")
            .ServerLines.ShouldBe(["Failed to connect to 127.0.0.1:1"]);

    // Each line the server gave, in the order the epic fixes, and a position counted from where the SQL starts.
    [Fact]
    public void ServerLines_ErrorWithEveryPart_GivesEachInOrder() =>
        PostgresErrors
            .ServerLines(new PostgresServerError("42883", "operator does not exist", "a detail", "a hint", 63), 31)
            .ShouldBe(["42883: operator does not exist", "detail: a detail", "hint: a hint", "at character 32"]);

    [Theory]
    [InlineData(null)]
    [InlineData(31)]
    [InlineData(7)]
    public void ServerLines_NoPositionOrOneBeforeTheSql_HasNoPositionLine(int? position) =>
        PostgresErrors
            .ServerLines(new PostgresServerError("42601", "syntax error", null, null, position), 31)
            .ShouldBe(["42601: syntax error"]);
}
```

`tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresDescriberTests.cs`:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The describer of PostgreSQL where no server is needed: what "open" answered becomes the session's server or the
// failure, and a connection string the driver cannot read is a failure too.
public class PostgresDescriberTests
{
    [Fact]
    public void Dialect_IsPostgreSql() => new PostgresDescriber().Dialect.ShouldBe(SqlDialect.PostgreSql);

    // The connection of a script is the word "replay", which no driver could open: nothing of Npgsql runs.
    [Fact]
    public async Task OpenAsync_ReplayedOpen_GivesTheServerOfTheAnswerAndTouchesNoConnection()
    {
        var exchange = new PostgresScript().Open().Play();

        await using var session = await PostgresScript.SessionAsync(exchange);

        session.Server.ShouldBe(
            new ServerInfo(
                "18.4",
                "Npgsql",
                "10.0.3",
                new EquatableArray<ServerSetting>([
                    new ServerSetting("search_path", "\"$user\", public"),
                    new ServerSetting("plan_cache_mode", "auto"),
                    new ServerSetting("standard_conforming_strings", "on"),
                ])
            )
        );
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task OpenAsync_ReplayedError_IsSqlsrc224ForTheDatabase()
    {
        var error = new PostgresServerError("3D000", "database \"nope\" does not exist", null, null, null);
        var exchange = new PostgresScript()
            .Answer("open", PostgresOpenRequest.Instance, new PostgresOpenAnswer(null, null, null, error))
            .Play();

        var opened = await PostgresScript.OpenAsync(exchange);

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.Arguments.ShouldBe([PostgresScript.Database]);
        failure.ServerLines.ShouldBe(["3D000: database \"nope\" does not exist"]);
    }

    // A user causes each of these, so none is an exception, and the driver's own text, whose inner exception
    // quotes the value, is not passed on.
    [Theory]
    [InlineData("Host=127.0.0.1;Port=" + Secrets.Marker)]
    [InlineData("Host=127.0.0.1;Bogus=" + Secrets.Marker)]
    [InlineData(Secrets.Marker)]
    [InlineData("Password=" + Secrets.Marker)]
    public async Task OpenAsync_ConnectionStringNpgsqlCannotRead_IsSqlsrc224WithoutTheDriversText(string connection)
    {
        var opened = await PostgresScript.OpenAsync(LiveExchange.Instance, connection: connection);

        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.ServerLines.ShouldBe([PostgresConnection.UnreadableConnectionString]);
        failure
            .Arguments.Concat(failure.ServerLines)
            .ShouldAllBe(text => !text.Contains(Secrets.Marker, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("18.4 (Debian 18.4-1.pgdg13+1)", "18.4")]
    [InlineData("14.24", "14.24")]
    [InlineData("19beta1 (Ubuntu 19~beta1-1)", "19beta1")]
    public void VersionOf_ServerVersion_IsCutAtItsFirstSpace(string reported, string expected) =>
        PostgresSession.VersionOf(reported).ShouldBe(expected);
}
```

In `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`, add `using SqlSource.Tool.Describing.Postgres;` and, after `Run_ValueOnTheCommandLineAndEveryErrorWithLog_IsNeverLogged`:

```csharp
    // The describer of PostgreSQL itself, given a connection that nothing answers: the port 1 of this machine
    // refuses at once.  What the driver says is printed, and the value is nowhere: not printed, not in the log, not
    // in the capture.
    [Fact]
    public async Task Run_PostgresDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue()
    {
        _scene.Run.Describers.Clear();
        _scene.Run.Describers.Add(new PostgresDescriber());
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] =
            "Host=127.0.0.1;Port=1;Timeout=5;Password=" + Secrets.Marker;

        var result = await _scene.DescribeAsync("--log", "run.log");

        ShouldHold(result, "224");
        result.Error.ShouldContain("    server: Failed to connect to 127.0.0.1:1");
        var log = await File.ReadAllTextAsync(
            _scene.Run.Folder.PathOf("run.log"),
            TestContext.Current.CancellationToken
        );
        log.ShouldNotContain(Secrets.Marker);
        log.ShouldContain("\"id\":\"SQLSRC224\"");
        CaptureJson.Write(_scene.Run.Capture).ShouldNotContain(Secrets.Marker);
    }
```

In `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, at the end of the class:

```csharp
    [Fact]
    public void DatabaseCannotBeOpened_Message_HoldsTheDatabase() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.DatabaseCannotBeOpened.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "app"
            )
            .ShouldBe("The database 'app' could not be opened");
```

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet build tests/SqlSource.Tool.Tests`
Expected: FAIL, the namespace `SqlSource.Tool.Describing.Postgres` does not exist.

- [ ] **Step 4: Add the descriptor in its four places**

`src/SqlSource/Diagnostics/ToolDiagnostics.cs`, after `SolutionCannotBeRead`:

```csharp
    public static readonly DiagnosticDescriptor DatabaseCannotBeOpened = new(
        id: "SQLSRC224",
        title: "Database could not be opened",
        messageFormat: "The database '{0}' could not be opened",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc224",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

and `DatabaseCannotBeOpened` after `SolutionCannotBeRead` in `All`.

`src/SqlSource/AnalyzerReleases.Unshipped.md`, at the end:

```
SQLSRC224 | SqlSource | Error | Database could not be opened
```

`docs/diagnostics.md`: a row at the end of the table,

```markdown
| [SQLSRC224](#sqlsrc224) | Database could not be opened |
```

a sentence at the end of the paragraph that starts `Ids below 100 are about the type`,

```markdown
The ids from `SQLSRC224` on are the errors of describing a query against PostgreSQL, which this version of the tool does not do yet: it reports [SQLSRC216](#sqlsrc216) for every such query.
```

and a section at the end of the file:

````markdown
## SQLSRC224

**Database could not be opened**

The tool could not open a session with the database of a query that must be described: nothing answers at the address, the server refused the user or the password, the database does not exist, or the driver cannot read the connection string.  The lines that start with `server:` are what the server or the driver said, as they said it.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC224: The database 'app' could not be opened
    query: GetUser, database app, postgres
    server: 28P01: password authentication failed for user "app"
    help: check that the database is running and that the connection string names it
```

Start the database, or correct the connection string that `--connection` or the database's variable gives.  When the line is `Npgsql cannot read the connection string`, the string is not written as Npgsql wants one, `Host=localhost;Database=app;Username=app;Password=...`: the tool does not repeat what the driver said of it, since that can quote the string.  The error is reported once, at the first query of the database that the run would have described, and each of them counts as failed.
````

- [ ] **Step 5: Write the records of `open`**

`src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`:

```csharp
using System.Collections.Generic;

namespace SqlSource.Tool.Describing.Postgres;

// What the describer of PostgreSQL passes through the exchange, in one file: plain records that a capture can hold.
// A method's name and a member's name are a contract with the captures that exist: a member is added, nullable,
// and none is renamed.  tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs holds each to the rule.

/// <summary>
/// The request of <c>open</c>.  It holds nothing: nothing of a connection may reach a capture.  Its one member is
/// static, so it is written as <c>{}</c>.
/// </summary>
internal sealed record PostgresOpenRequest
{
    public static PostgresOpenRequest Instance { get; } = new();
}

/// <summary>What a session knows of its server, or why none was opened.</summary>
/// <param name="ServerVersion">The server's <c>server_version</c>, cut at its first space.</param>
/// <param name="DriverVersion">The version of the Npgsql that asked.</param>
/// <param name="Settings">The settings of the session that change a description.</param>
/// <param name="Error">Why no session was opened, or null.</param>
internal sealed record PostgresOpenAnswer(
    string? ServerVersion,
    string? DriverVersion,
    IReadOnlyList<PostgresSetting>? Settings,
    PostgresServerError? Error
);

/// <summary>A setting of the session, as <c>current_setting</c> gives it.</summary>
internal sealed record PostgresSetting(string Name, string Value);

/// <summary>
/// What the server said of a call it refused, each part null where the server gave none.  A failure that is not
/// the server's, a connection that was refused for one, has the driver's message alone.
/// </summary>
/// <param name="SqlState">The five characters of the SQLSTATE.</param>
/// <param name="Message">The primary message.</param>
/// <param name="Detail">The detail.</param>
/// <param name="Hint">The hint.</param>
/// <param name="Position">The character of the text as sent that the error is at, counted from one.</param>
internal sealed record PostgresServerError(string? SqlState, string Message, string? Detail, string? Hint, int? Position);
```

- [ ] **Step 6: Write the connection**

`src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The one thing of the describer that holds the connection's value, and the one place that makes a connection and
/// a command.  Nothing here runs until a <c>live</c> function asks for it, so a replayed session touches no
/// driver.
/// </summary>
/// <remarks>
/// <para>
/// A session has a data source of its own.  A fresh one loads the type catalog when it first opens; a plain
/// <see cref="NpgsqlConnection" /> shares a catalog by connection string for the life of the process, which goes
/// stale when the schema changes.
/// </para>
/// <para>
/// A command's text is not a constant here, which is what <c>CA2100</c> flags: <c>.editorconfig</c> turns the rule
/// off for this file and says why.
/// </para>
/// </remarks>
/// <param name="connectionString">The connection's value.  It goes to the driver and to nothing else.</param>
internal sealed class PostgresConnection(string connectionString) : IAsyncDisposable
{
    /// <summary>
    /// What is said of a connection string the driver cannot read.  The driver's own text is not passed on: its
    /// inner exception quotes the value.
    /// </summary>
    public const string UnreadableConnectionString = "Npgsql cannot read the connection string";

    private NpgsqlDataSource? _source;
    private NpgsqlConnection? _connection;

    /// <summary>The version of the driver, as <c>major.minor.patch</c>.</summary>
    public static string? DriverVersion => typeof(NpgsqlConnection).Assembly.GetName().Version?.ToString(3);

    /// <summary>The session's connection, opened by the first call.</summary>
    /// <exception cref="NpgsqlException">
    /// The server could not be reached or refused, or the driver cannot read the connection string.
    /// </exception>
    public async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        try
        {
            _source ??= NpgsqlDataSource.Create(connectionString);
            _connection = await _source.OpenConnectionAsync(cancellationToken);
            return _connection;
        }
        catch (ArgumentException)
        {
            // The driver's word for a string it cannot read: a keyword it does not know, a value that is no number,
            // no host.  A user's mistake, and so an answer of "open" like any other.  The exception is not kept.
            throw new NpgsqlException(UnreadableConnectionString);
        }
    }

    /// <summary>A command on the session's connection.  Its caller disposes it.</summary>
    public async Task<NpgsqlCommand> CommandAsync(string sql, CancellationToken cancellationToken) =>
        new(sql, await OpenAsync(cancellationToken));

    /// <summary>What the driver threw, as the answer of a call holds it.</summary>
    public static PostgresServerError ErrorOf(NpgsqlException exception) =>
        exception is PostgresException server
            ? new PostgresServerError(
                server.SqlState,
                server.MessageText,
                server.Detail,
                server.Hint,
                server.Position > 0 ? server.Position : null
            )
            : new PostgresServerError(null, exception.Message, null, null, null);

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        if (_source is not null)
        {
            await _source.DisposeAsync();
        }
    }

    /// <summary>The connection without its value.</summary>
    public override string ToString() => nameof(PostgresConnection);
}
```

- [ ] **Step 7: Write the errors of `open`**

`src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using SqlSource.Diagnostics;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// What the server said, or what the tool refuses before it asks, as the failure a run reports at the query.  One
/// id for each cause a user mends differently.
/// </summary>
internal static class PostgresErrors
{
    public const string OpenHelp = "check that the database is running and that the connection string names it";

    /// <summary>
    /// <c>SQLSRC224</c>: no session.  Every failure of <c>open</c> is this one, whatever its SQLSTATE: a wrong
    /// password and a missing database are not the server rejecting a query.
    /// </summary>
    public static DescribeFailure Open(PostgresServerError error, string database) =>
        new(
            ToolDiagnostics.DatabaseCannotBeOpened,
            new EquatableArray<string>([database]),
            null,
            ServerLines(error, 0),
            OpenHelp
        );

    /// <summary>
    /// The <c>server:</c> lines, verbatim and in this order: the SQLSTATE and the message, the detail, the hint, and
    /// the character the error is at.
    /// </summary>
    /// <param name="error">What the server said.</param>
    /// <param name="head">
    /// How many characters of the text that was sent stand before the user's SQL.  The position line counts from
    /// the SQL, and there is none for a position that is not in it.
    /// </param>
    public static EquatableArray<string> ServerLines(PostgresServerError error, int head)
    {
        var lines = new List<string> { error.SqlState is { } state ? $"{state}: {error.Message}" : error.Message };
        if (error.Detail is { } detail)
        {
            lines.Add($"detail: {detail}");
        }

        if (error.Hint is { } hint)
        {
            lines.Add($"hint: {hint}");
        }

        if (error.Position is { } position && position > head)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"at character {position - head}"));
        }

        return new EquatableArray<string>([.. lines]);
    }
}
```

- [ ] **Step 8: Write the session and the describer**

`src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`.  `DescribeAsync` describes nothing yet: Tasks 4 to 6 give it its steps, and no run reaches it until sub-phase 3.6.

```csharp
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// A session with one PostgreSQL database.  Every call it makes to the server goes through the exchange, and the
/// connection is made inside a <c>live</c> function and nowhere else.
/// </summary>
internal sealed class PostgresSession : IDescribeSession
{
    public const string DriverName = "Npgsql";

    // The settings of a session that change a description, in the order "open" answers them.
    private static readonly string[] SettingNames = ["search_path", "plan_cache_mode", "standard_conforming_strings"];

    private const string OpenSql =
        "SELECT pg_catalog.current_setting('server_version'), pg_catalog.current_setting('search_path'), "
        + "pg_catalog.current_setting('plan_cache_mode'), pg_catalog.current_setting('standard_conforming_strings')";

    private PostgresSession(PostgresConnection connection, ServerInfo server)
    {
        Connection = connection;
        Server = server;
    }

    public ServerInfo Server { get; }

    /// <summary>
    /// The session's connection.  The container tests ask on it what a describe left behind:
    /// <c>pg_prepared_statements</c> shows the statements of its own session alone.
    /// </summary>
    internal PostgresConnection Connection { get; }

    /// <summary>Asks <c>open</c>, and gives the session of the server that answered.</summary>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The session owns the connection and is its caller's to close: the run closes what it "
            + "opened.  The analyzer follows a disposable that is returned itself and not one that is returned in "
            + "an Outcome.  docs/tech-debt/TD-0041."
    )]
    public static async Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        CancellationToken cancellationToken
    )
    {
        var connection = new PostgresConnection(request.Connection);
        var kept = false;
        try
        {
            var answer = await request.Exchange.AskAsync(
                "open",
                PostgresOpenRequest.Instance,
                (_, token) => OpenLiveAsync(connection, token),
                cancellationToken
            );
            if (answer.Error is { } error)
            {
                return Outcome<IDescribeSession, DescribeFailure>.Failed(PostgresErrors.Open(error, request.Database));
            }

            var session = new PostgresSession(connection, ServerOf(answer));
            kept = true;
            return Outcome<IDescribeSession, DescribeFailure>.Of(session);
        }
        finally
        {
            if (!kept)
            {
                await connection.DisposeAsync();
            }
        }
    }

    /// <summary><c>server_version</c> as a sidecar records it: <c>18.4 (Debian 18.4-1.pgdg13+1)</c> is <c>18.4</c>.</summary>
    public static string VersionOf(string serverVersion)
    {
        var space = serverVersion.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? serverVersion : serverVersion[..space];
    }

    // The steps of a describe arrive with the calls they make.  Until then a query has no typed parameter.
    public Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        DescribeRequest request,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult(
            Outcome<QueryDescription, DescribeFailure>.Of(
                new QueryDescription(
                    SidecarResultKind.None,
                    EquatableArray<DescribedParameter>.Empty,
                    null,
                    null,
                    null,
                    null
                )
            )
        );

    public ValueTask DisposeAsync() => Connection.DisposeAsync();

    private static ServerInfo ServerOf(PostgresOpenAnswer answer) =>
        new(
            answer.ServerVersion
                ?? throw new InvalidOperationException("The answer to 'open' holds no version and no error."),
            DriverName,
            answer.DriverVersion,
            new EquatableArray<ServerSetting>([
                .. (answer.Settings ?? []).Select(static setting => new ServerSetting(setting.Name, setting.Value)),
            ])
        );

    // What the server can cause is the answer's error.  A cancellation passes through, and anything else is a bug.
    private static async Task<PostgresOpenAnswer> OpenLiveAsync(PostgresConnection connection, CancellationToken token)
    {
        try
        {
            await using var command = await connection.CommandAsync(OpenSql, token);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                throw new InvalidOperationException("The server answered 'open' with no row.");
            }

            return new PostgresOpenAnswer(
                VersionOf(reader.GetString(0)),
                PostgresConnection.DriverVersion,
                [.. SettingNames.Select((name, index) => new PostgresSetting(name, reader.GetString(index + 1)))],
                null
            );
        }
        catch (NpgsqlException exception)
        {
            return new PostgresOpenAnswer(null, null, null, PostgresConnection.ErrorOf(exception));
        }
    }
}
```

`src/SqlSource.Tool/Describing/Postgres/PostgresDescriber.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Asks a PostgreSQL to describe queries, through Npgsql.  It is registered nowhere until the describer is whole:
/// the released tool has none for <c>postgres</c>.
/// </summary>
internal sealed class PostgresDescriber : IQueryDescriber
{
    public SqlDialect Dialect => SqlDialect.PostgreSql;

    public Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    ) => PostgresSession.OpenAsync(request, cancellationToken);
}
```

The describer does not use `log` yet: the session takes it in Task 7, with the events.

- [ ] **Step 9: Run the tool's tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS.  The theories of `CallRecordTests` now list `PostgresOpenRequest` and `PostgresOpenAnswer` and pass for both.

Run: `dotnet test --project tests/SqlSource.Tests`
Expected: PASS, with `DiagnosticsDocumentTests` and `ToolDiagnosticsTests` among them.

- [ ] **Step 10: Write the container tests**

`tests/SqlSource.Tests.Postgres/Describer/Sessions.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// A session of the PostgreSQL describer as a run opens one, for a test: the live exchange unless the test brings
// one, the machine's clock, and no log unless the test brings one.
internal static class Sessions
{
    public static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        string connection,
        IDescribeExchange? exchange = null,
        IRunLog? log = null
    ) =>
        new PostgresDescriber().OpenAsync(
            new OpenRequest(PostgresContainer.Database, connection, exchange ?? LiveExchange.Instance, TimeProvider.System),
            log ?? NullRunLog.Instance,
            TestContext.Current.CancellationToken
        );

    // Every text of a failure that a run prints or logs.
    public static IReadOnlyList<string> TextsOf(DescribeFailure failure) =>
        [
            failure.Descriptor.Id,
            failure.Descriptor.Title.ToString(CultureInfo.InvariantCulture),
            failure.Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            .. failure.Arguments,
            .. failure.ServerLines,
            failure.Help ?? string.Empty,
        ];
}
```

`tests/SqlSource.Tests.Postgres/Describer/SessionTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// "open" against a real server: what a session knows of it, and each way a session is not opened.
[Collection(UsesPostgres.Name)]
public sealed class SessionTests(PostgresContainer postgres)
{
    private const string Marker = "s3cret-marker";

    [Fact]
    public async Task OpenAsync_Container_GivesTheVersionTheDriverAndTheThreeSettings()
    {
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);

        await using var session = opened.Value.ShouldNotBeNull();
        var server = session.Server;
        server.Version.ShouldNotContain(" ");
        char.IsAsciiDigit(server.Version[0]).ShouldBeTrue(server.Version);
        server.Driver.ShouldBe("Npgsql");
        server.DriverVersion.ShouldBe(typeof(NpgsqlConnection).Assembly.GetName().Version!.ToString(3));
        server.Settings.ShouldBe([
            new ServerSetting("search_path", "\"$user\", public"),
            new ServerSetting("plan_cache_mode", "auto"),
            new ServerSetting("standard_conforming_strings", "on"),
        ]);
    }

    [Fact]
    public async Task OpenAsync_ThroughACapturingExchange_KeepsAnEmptyRequestAndNothingOfTheConnection()
    {
        var capture = new DescribeCapture("postgres");

        var opened = await Sessions.OpenAsync(
            postgres.ConnectionString,
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        await using var session = opened.Value.ShouldNotBeNull();
        var call = capture.Calls.ShouldHaveSingleItem();
        call.Method.ShouldBe("open");
        CaptureJson.TextOf(call.Request).ShouldBe("{}");
        CaptureJson.Write(capture).ShouldNotContain(postgres.ConnectionString);
    }

    [Fact]
    public async Task OpenAsync_WrongPassword_IsSqlsrc224WithTheServersMessage()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Password = Marker };

        var failure = await ShouldNotOpenAsync(connection.ConnectionString);

        failure.ServerLines[0].ShouldStartWith("28P01: password authentication failed");
    }

    [Fact]
    public async Task OpenAsync_MissingDatabase_IsSqlsrc224WithTheServersMessage()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = "nope" };

        var failure = await ShouldNotOpenAsync(connection.ConnectionString);

        failure.ServerLines.ShouldBe(["3D000: database \"nope\" does not exist"]);
    }

    // The port 1 of this machine refuses at once.
    [Fact]
    public async Task OpenAsync_UnreachableHost_IsSqlsrc224WithTheDriversMessage()
    {
        var failure = await ShouldNotOpenAsync("Host=127.0.0.1;Port=1;Timeout=5;Username=app;Password=" + Marker);

        failure.ServerLines.ShouldBe(["Failed to connect to 127.0.0.1:1"]);
    }

    // No session, SQLSRC224 at no step for the database, and the connection's whole value in no text of it.
    private static async Task<DescribeFailure> ShouldNotOpenAsync(string connection)
    {
        var opened = await Sessions.OpenAsync(connection);

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.Arguments.ShouldBe([PostgresContainer.Database]);
        failure.Step.ShouldBeNull();
        Sessions
            .TextsOf(failure)
            .ShouldAllBe(text =>
                !text.Contains(connection, StringComparison.Ordinal) && !text.Contains(Marker, StringComparison.Ordinal)
            );
        return failure;
    }
}
```

- [ ] **Step 11: Run the container tests**

Run: `dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: PASS, 7 tests.

- [ ] **Step 12: Check the package**

Run: `tools/check-package.sh`
Expected: it ends with a line that lists `tools/net8.0/any/Npgsql.dll` among what the tool's package holds.

- [ ] **Step 13: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`
Expected: every step `passed`.

```bash
git add src/SqlSource.Tool src/SqlSource/Diagnostics/ToolDiagnostics.cs src/SqlSource/AnalyzerReleases.Unshipped.md docs/diagnostics.md .editorconfig tools/check-package.sh CONTRIBUTING.md tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs tests/SqlSource.Tool.Tests tests/SqlSource.Tests.Postgres
```

```bash
git commit -m "Add the PostgreSQL describer as far as opening a session

Npgsql, a data source for each session, the open call through the
exchange, and SQLSRC224 for every way a session is not opened.  The
describer is registered nowhere.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The statement check, the placeholders and a declared type's text

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/DeclaredTypeText.cs`
- Create: `src/SqlSource.Tool/Describing/Postgres/ParameterPlaceholders.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/DeclaredTypeTextTests.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/ParameterPlaceholdersTests.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`
- Create: `tests/SqlSource.Tests.Postgres/Describer/Requests.cs`
- Create: `tests/SqlSource.Tests.Postgres/Describer/ParameterTests.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`, `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Modify: `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs`, `tests/SqlSource.Tests.Postgres/Describer/Sessions.cs`

**Interfaces:**
- Consumes: `SqlLexer.Lex(string, SqlDialectRules)`, `SqlDialectRules.PostgreSql`, `SqlLexemeKind`, `SqlQueryParameter(string Name, string? Type, bool? Nullable, bool IsDeclared)` of `SqlSource.Parsing`; `DescribeStep.DescribeParameters`; the session and `PostgresScript` of Task 3.
- Produces, in `SqlSource.Tool.Describing.Postgres`:
  - `static class DeclaredTypeText` with `bool TryRead(string text, [NotNullWhen(true)] out string? type)`
  - `static class ParameterPlaceholders` with `const string Unknown = "unknown"`, `string? Rewrite(string sql, EquatableArray<SqlQueryParameter> parameters)`, `Outcome<IReadOnlyList<string>, SqlQueryParameter> TypeList(EquatableArray<SqlQueryParameter> parameters)`, `string PrepareText(string statement, IReadOnlyList<string> types, string sql)`, `int HeadLength(string statement, IReadOnlyList<string> types)`, `int TypeAt(string statement, IReadOnlyList<string> types, int position)`
  - `PostgresErrors.SeveralStatements(DescribeRequest query)`, `PostgresErrors.TypeRefused(DescribeRequest query, SqlQueryParameter parameter)`, `PostgresErrors.Written(SqlQueryParameter parameter)`, and the constants `NotATypeName`, `NotATypeNameHelp`, `SeveralStatementsHelp`
  - `ToolDiagnostics.DeclaredTypeNotValid`, `SQLSRC228`; `ToolDiagnostics.QueryHasSeveralStatements`, `SQLSRC230`
- Produces, in the container tests: `Requests.Of(string sql, params (string Name, string Type)[] declared)`, `Requests.Query`, `Sessions.PreparedStatementsAsync(PostgresSession session)`.

- [ ] **Step 1: Write the failing tests of the grammar**

`tests/SqlSource.Tool.Tests/Describing/Postgres/DeclaredTypeTextTests.cs`:

```csharp
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The text of a -- param: type is put into SQL the tool sends, so it is read against the grammar of a type name
// first: a name, PostgreSQL's own type words, one modifier, and array marks.  Anything else never leaves the tool.
public class DeclaredTypeTextTests
{
    [Theory]
    [InlineData("int")]
    [InlineData("varchar(50)")]
    [InlineData("numeric(2,-3)")]
    [InlineData("numeric(18, 2)")]
    [InlineData("character varying(50)")]
    [InlineData("national character varying(10)")]
    [InlineData("bit varying(5)")]
    [InlineData("double precision")]
    [InlineData("timestamp(3) without time zone")]
    [InlineData("timestamp with time zone")]
    [InlineData("time(3) with time zone")]
    [InlineData("interval day to second(3)")]
    [InlineData("interval year to month")]
    [InlineData("INTERVAL HOUR TO MINUTE")]
    [InlineData("public.user_status")]
    [InlineData("public . user_status")]
    [InlineData("\"My Schema\".\"Status\"[]")]
    [InlineData("\"a\"\"b\"")]
    [InlineData("text[]")]
    [InlineData("int[][]")]
    [InlineData("email_t")]
    public void TryRead_TypeName_IsAccepted(string text)
    {
        DeclaredTypeText.TryRead(text, out var type).ShouldBeTrue(text);

        type.ShouldBe(text);
    }

    [Fact]
    public void TryRead_SpaceAroundTheName_IsLeftOut()
    {
        DeclaredTypeText.TryRead("  varchar(50) \t", out var type).ShouldBeTrue();

        type.ShouldBe("varchar(50)");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(";")]
    [InlineData("int;")]
    [InlineData("'")]
    [InlineData("int'")]
    [InlineData("int /* x */")]
    [InlineData("int -- x")]
    [InlineData("int) AS SELECT 1")]
    [InlineData("int) AS SELECT 1; DROP TABLE users; --")]
    [InlineData("int AS SELECT foo")]
    [InlineData("int foo")]
    [InlineData("int(")]
    [InlineData("int(1)(2)")]
    [InlineData("int(a)")]
    [InlineData("int(--)")]
    [InlineData("int(1-)")]
    [InlineData("(1)")]
    [InlineData("int[")]
    [InlineData("int[3]")]
    [InlineData("[]")]
    [InlineData("int[] foo")]
    [InlineData("\"open")]
    [InlineData("\"a\nb\"")]
    [InlineData("int\"x\"")]
    [InlineData("a.b.c")]
    [InlineData(".t")]
    [InlineData("t.")]
    [InlineData("int\nfoo")]
    [InlineData("int\n")]
    public void TryRead_AnythingElse_IsRefused(string text)
    {
        DeclaredTypeText.TryRead(text, out var type).ShouldBeFalse(text);

        type.ShouldBeNull();
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build tests/SqlSource.Tool.Tests`
Expected: FAIL, `DeclaredTypeText` does not exist.

- [ ] **Step 3: Write the grammar**

`src/SqlSource.Tool/Describing/Postgres/DeclaredTypeText.cs`.  The spike ran this code over every case of Step 1.

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Reads the type of a <c>-- param:</c> marker against the grammar of a type name, before it is put into a
/// <c>PREPARE</c>.
/// </summary>
/// <remarks>
/// <para>
/// A marker's type is everything to the end of its line, and the tool interpolates it into SQL it sends.  So
/// nothing leaves the tool that is not written as a type: a name, one bare identifier or one quoted one, or two
/// joined by a dot; then any of PostgreSQL's own type words, with one modifier in parentheses before, among or
/// after them; then any number of <c>[]</c>.  A comment, a <c>;</c>, a quote left open, a <c>)</c> without its
/// <c>(</c> and any other bare word are refused.
/// </para>
/// <para>
/// It does not say whether the database has the type: the server does, and checks it against every use.
/// </para>
/// </remarks>
internal static class DeclaredTypeText
{
    // The words of PostgreSQL's multi-word type names, compared ignoring case.
    private static readonly HashSet<string> TypeWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "varying",
        "precision",
        "national",
        "character",
        "with",
        "without",
        "time",
        "zone",
        "year",
        "month",
        "day",
        "hour",
        "minute",
        "second",
        "to",
    };

    /// <summary>The text to put into the type list, or false for a text that is not written as a type name.</summary>
    public static bool TryRead(string text, [NotNullWhen(true)] out string? type)
    {
        type = null;
        var position = 0;
        if (!TryReadIdentifier(text, ref position))
        {
            return false;
        }

        SkipSpaces(text, ref position);
        if (position < text.Length && text[position] == '.')
        {
            position++;
            if (!TryReadIdentifier(text, ref position))
            {
                return false;
            }
        }

        if (!TryReadWordsAndModifier(text, ref position) || !TryReadArrayMarks(text, ref position))
        {
            return false;
        }

        type = text.Trim(' ', '\t');
        return true;
    }

    // Type words and at most one modifier, in any order, up to the first array mark or the end.
    private static bool TryReadWordsAndModifier(string text, ref int position)
    {
        var hasModifier = false;
        while (true)
        {
            SkipSpaces(text, ref position);
            if (position == text.Length || text[position] == '[')
            {
                return true;
            }

            if (text[position] == '(')
            {
                if (hasModifier || !TryReadModifier(text, ref position))
                {
                    return false;
                }

                hasModifier = true;
            }
            else
            {
                var start = position;
                while (position < text.Length && IsBare(text[position]))
                {
                    position++;
                }

                if (position == start || !TypeWords.Contains(text[start..position]))
                {
                    return false;
                }
            }
        }
    }

    // "(" with digits, commas and spaces up to ")".  A "-" stands before a digit, so that two cannot start a comment.
    private static bool TryReadModifier(string text, ref int position)
    {
        for (var index = position + 1; index < text.Length; index++)
        {
            var value = text[index];
            if (value == ')')
            {
                position = index + 1;
                return true;
            }

            var isSign = value == '-' && index + 1 < text.Length && char.IsAsciiDigit(text[index + 1]);
            if (!char.IsAsciiDigit(value) && value is not (',' or ' ') && !isSign)
            {
                return false;
            }
        }

        return false;
    }

    // Any number of "[]", and then the end.
    private static bool TryReadArrayMarks(string text, ref int position)
    {
        while (position < text.Length)
        {
            if (text[position] != '[' || position + 1 == text.Length || text[position + 1] != ']')
            {
                return false;
            }

            position += 2;
            SkipSpaces(text, ref position);
        }

        return true;
    }

    private static bool TryReadIdentifier(string text, ref int position)
    {
        SkipSpaces(text, ref position);
        if (position < text.Length && text[position] == '"')
        {
            return TryReadQuoted(text, ref position);
        }

        var start = position;
        while (position < text.Length && IsBare(text[position]))
        {
            position++;
        }

        return position > start;
    }

    // A quoted identifier, with "" for a quote.  It ends on its line.
    private static bool TryReadQuoted(string text, ref int position)
    {
        for (var index = position + 1; index < text.Length; index++)
        {
            var value = text[index];
            if (value is '\r' or '\n' or '\0')
            {
                return false;
            }

            if (value != '"')
            {
                continue;
            }

            if (index + 1 < text.Length && text[index + 1] == '"')
            {
                index++;
                continue;
            }

            position = index + 1;
            return true;
        }

        return false;
    }

    private static bool IsBare(char value) => char.IsLetterOrDigit(value) || value == '_';

    private static void SkipSpaces(string text, ref int position)
    {
        while (position < text.Length && text[position] is ' ' or '\t')
        {
            position++;
        }
    }
}
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "SqlSource.Tool.Tests.Describing.Postgres.DeclaredTypeTextTests"`
Expected: PASS.

- [ ] **Step 4: Write the failing tests of the placeholders**

`tests/SqlSource.Tool.Tests/Describing/Postgres/ParameterPlaceholdersTests.cs`:

```csharp
using System;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What is sent for a query: its one statement with $n where the lexer finds a parameter, and the type list of the
// PREPARE.  A text of several statements is never sent.
public class ParameterPlaceholdersTests
{
    private static EquatableArray<SqlQueryParameter> List(params SqlQueryParameter[] parameters) => new([.. parameters]);

    private static SqlQueryParameter Inferred(string name) => new(name, null, null, false);

    private static SqlQueryParameter Declared(string name, string type) => new(name, type, null, true);

    [Fact]
    public void Rewrite_ParameterOnceAndTwice_HasOneOrdinal() =>
        ParameterPlaceholders
            .Rewrite("SELECT @id, @id + 1 FROM users WHERE id = @id", List(Inferred("id")))
            .ShouldBe("SELECT $1, $1 + 1 FROM users WHERE id = $1");

    [Fact]
    public void Rewrite_TwoParameters_AreNumberedByTheList() =>
        ParameterPlaceholders
            .Rewrite("SELECT 1 WHERE @b = 1 AND @a = 2", List(Inferred("b"), Inferred("a"), Declared("c", "int")))
            .ShouldBe("SELECT 1 WHERE $1 = 1 AND $2 = 2");

    [Fact]
    public void Rewrite_NameInAnotherCase_IsTheSameParameter() =>
        ParameterPlaceholders.Rewrite("SELECT @ID, @Id", List(Inferred("id"))).ShouldBe("SELECT $1, $1");

    [Theory]
    [InlineData("SELECT '@id'")]
    [InlineData("SELECT 1 -- @id\nFROM users")]
    [InlineData("SELECT /* @id */ 1")]
    [InlineData("SELECT \"@id\" FROM users")]
    [InlineData("SELECT $q$ @id $q$")]
    public void Rewrite_AtSignThatIsNoParameter_IsLeftAlone(string sql) =>
        ParameterPlaceholders.Rewrite(sql, List()).ShouldBe(sql);

    [Fact]
    public void Rewrite_TrailingSemicolonWithSpaceAndCommentsAfterIt_IsDroppedWithThem() =>
        ParameterPlaceholders
            .Rewrite("DELETE FROM users WHERE id = @id ; -- done\n/* end */ ", List(Inferred("id")))
            .ShouldBe("DELETE FROM users WHERE id = $1 ");

    [Fact]
    public void Rewrite_SemicolonInAStringOrADollarQuote_IsKept() =>
        ParameterPlaceholders.Rewrite("SELECT ';', $x$;$x$", List()).ShouldBe("SELECT ';', $x$;$x$");

    [Theory]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("SELECT 1; SELECT 2;")]
    [InlineData("SELECT 1;;")]
    [InlineData("SELECT 1; -- one\nDROP TABLE users")]
    public void Rewrite_SecondStatement_IsNull(string sql) => ParameterPlaceholders.Rewrite(sql, List()).ShouldBeNull();

    // The plan's parser made the list from the same SQL, so a parameter outside it is a bug of the plan.
    [Fact]
    public void Rewrite_ParameterTheListDoesNotHold_Throws() =>
        Should
            .Throw<InvalidOperationException>(() => ParameterPlaceholders.Rewrite("SELECT @id", List()))
            .Message.ShouldContain("'id'");

    [Fact]
    public void TypeList_DeclaredAndInferred_HasTheTypeAsWrittenOrUnknownInTheListsOrder()
    {
        var types = ParameterPlaceholders.TypeList(
            List(Inferred("id"), Declared("name", " varchar(50) "), Declared("tenant", "public.tenant_id"))
        );

        types.Value.ShouldBe(["unknown", "varchar(50)", "public.tenant_id"]);
    }

    [Fact]
    public void TypeList_TypeThatIsNoTypeName_GivesItsParameter()
    {
        var refused = Declared("b", "int) AS SELECT 1");

        var types = ParameterPlaceholders.TypeList(List(Declared("a", "int"), refused, Declared("c", "nope nope")));

        types.Value.ShouldBeNull();
        types.Failure.ShouldBe(refused);
    }

    // The list ends on a line of its own: whatever a type's text held, the ") AS" is not on its line.
    [Fact]
    public void PrepareText_WithTypes_EndsTheListWithALineBreakBeforeAs() =>
        ParameterPlaceholders
            .PrepareText("sqlsource_1", ["unknown", "int"], "SELECT $1, $2")
            .ShouldBe("PREPARE sqlsource_1 (unknown, int\n) AS SELECT $1, $2");

    // "PREPARE s () AS" is a syntax error.
    [Fact]
    public void PrepareText_WithoutTypes_HasNoList() =>
        ParameterPlaceholders.PrepareText("sqlsource_1", [], "SELECT 1").ShouldBe("PREPARE sqlsource_1 AS SELECT 1");

    [Fact]
    public void HeadLength_OfAPrepare_IsWhatStandsBeforeTheSql()
    {
        ParameterPlaceholders.HeadLength("sqlsource_6", ["unknown"]).ShouldBe(34);
        ParameterPlaceholders.HeadLength("sqlsource_4", []).ShouldBe(23);
    }

    // The server counts characters, and one outside the basic plane is two chars of a string.
    [Fact]
    public void HeadLength_TypeWithACharacterOutsideTheBasicPlane_CountsItOnce() =>
        ParameterPlaceholders
            .HeadLength("sqlsource_1", ["\"\U0001F600\""])
            .ShouldBe(ParameterPlaceholders.HeadLength("sqlsource_1", ["\"x\""]));

    // "PREPARE sqlsource_10 (unknown, nope_t" and then a line break and ")": unknown is the characters 23 to 29 and
    // nope_t 32 to 37.  A position between two items, or after the last, belongs to the item before it.
    [Theory]
    [InlineData(1, 0)]
    [InlineData(23, 0)]
    [InlineData(29, 0)]
    [InlineData(31, 0)]
    [InlineData(32, 1)]
    [InlineData(37, 1)]
    [InlineData(39, 1)]
    public void TypeAt_Position_IsTheItemItFallsInOrFollows(int position, int expected) =>
        ParameterPlaceholders.TypeAt("sqlsource_10", ["unknown", "nope_t"], position).ShouldBe(expected);
}
```

- [ ] **Step 5: Run them to see them fail**

Run: `dotnet build tests/SqlSource.Tool.Tests`
Expected: FAIL, `ParameterPlaceholders` does not exist.

- [ ] **Step 6: Write the placeholders**

`src/SqlSource.Tool/Describing/Postgres/ParameterPlaceholders.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The text a query is prepared with: its one statement with <c>$n</c> in the place of each parameter, the type
/// list, and the <c>PREPARE</c> around both.
/// </summary>
/// <remarks>
/// The lexer of the generator says what is a parameter and what is a string, a comment or a quoted identifier, under
/// the rules of <c>postgres</c>, so the tool and the generator read a query one way.  Npgsql splits a text at
/// <c>;</c> and runs each part, and <c>PREPARE</c> would prepare the first and run the second: a text of several
/// statements is never sent.
/// </remarks>
internal static class ParameterPlaceholders
{
    /// <summary>The item of the type list for a parameter with no declared type: the server infers it.</summary>
    public const string Unknown = "unknown";

    private const string Separator = ", ";

    /// <summary>
    /// The SQL with <c>$</c> and the one-based ordinal of its name in the list for each parameter, and without its
    /// trailing <c>;</c> and what follows it.  Null when a <c>;</c> is followed by anything but white space and
    /// comments: the query holds more than one statement.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex, or holds a parameter that the list does not: a bug of the plan, whose parser read the
    /// same text.
    /// </exception>
    public static string? Rewrite(string sql, EquatableArray<SqlQueryParameter> parameters)
    {
        var lexed = SqlLexer.Lex(sql, SqlDialectRules.PostgreSql);
        if (lexed.Error is not null)
        {
            throw new InvalidOperationException("The sample SQL of a query does not lex, and the plan's parser read it.");
        }

        var rewritten = new StringBuilder(sql.Length);
        for (var index = 0; index < lexed.Lexemes.Count; index++)
        {
            var lexeme = lexed.Lexemes[index];
            var text = sql.AsSpan(lexeme.Span.Start, lexeme.Span.Length);
            if (lexeme.Kind == SqlLexemeKind.Parameter)
            {
                _ = rewritten.Append('$').Append(OrdinalOf(text[1..], parameters));
                continue;
            }

            var end = lexeme.Kind == SqlLexemeKind.Text ? text.IndexOf(';') : -1;
            if (end < 0)
            {
                _ = rewritten.Append(text);
                continue;
            }

            // The first ";" of the SQL.  It is the trailing one when nothing but white space and comments follows.
            return text[(end + 1)..].IsWhiteSpace() && OnlyCommentsFollow(sql, lexed.Lexemes, index + 1)
                ? rewritten.Append(text[..end]).ToString()
                : null;
        }

        return rewritten.ToString();
    }

    /// <summary>
    /// One item for each parameter of the list, in order: the declared type as the marker wrote it, or
    /// <see cref="Unknown" />.  A declared parameter that the SQL does not hold is in the list all the same: the
    /// server accepts a list longer than the placeholders used.  The failure is the first parameter whose type is
    /// not written as a type name.
    /// </summary>
    public static Outcome<IReadOnlyList<string>, SqlQueryParameter> TypeList(
        EquatableArray<SqlQueryParameter> parameters
    )
    {
        var types = new List<string>(parameters.Count);
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            if (parameter.Type is null)
            {
                types.Add(Unknown);
            }
            else if (DeclaredTypeText.TryRead(parameter.Type, out var type))
            {
                types.Add(type);
            }
            else
            {
                return Outcome<IReadOnlyList<string>, SqlQueryParameter>.Failed(parameter);
            }
        }

        return Outcome<IReadOnlyList<string>, SqlQueryParameter>.Of(types);
    }

    /// <summary>
    /// The statement that is sent.  The type list ends with a line break before <c>AS</c>: whatever a type's text
    /// held, the rest of the statement is not on its line.  A query without parameters has no list.
    /// </summary>
    public static string PrepareText(string statement, IReadOnlyList<string> types, string sql) =>
        Head(statement, types) + sql;

    /// <summary>
    /// How many characters of <see cref="PrepareText" /> stand before the SQL, counted as the server counts a
    /// position: a character outside the basic plane once.
    /// </summary>
    public static int HeadLength(string statement, IReadOnlyList<string> types) => Characters(Head(statement, types));

    /// <summary>
    /// The index of the item of the type list that a position of the server, counted from one, falls in.  A position
    /// between two items, or after the last, belongs to the item before it.
    /// </summary>
    public static int TypeAt(string statement, IReadOnlyList<string> types, int position)
    {
        var start = Characters(Opening(statement));
        var found = 0;
        for (var index = 0; index < types.Count; index++)
        {
            if (position > start)
            {
                found = index;
            }

            start += Characters(types[index]) + Separator.Length;
        }

        return found;
    }

    private static string Head(string statement, IReadOnlyList<string> types) =>
        types.Count == 0
            ? $"PREPARE {statement} AS "
            : $"{Opening(statement)}{string.Join(Separator, types)}\n) AS ";

    private static string Opening(string statement) => $"PREPARE {statement} (";

    private static int Characters(string text) => text.EnumerateRunes().Count();

    private static string OrdinalOf(ReadOnlySpan<char> name, EquatableArray<SqlQueryParameter> parameters)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            if (name.Equals(parameters[index].Name, StringComparison.OrdinalIgnoreCase))
            {
                return (index + 1).ToString(CultureInfo.InvariantCulture);
            }
        }

        throw new InvalidOperationException(
            $"The SQL holds the parameter '{name.ToString()}', which the query's list does not."
        );
    }

    // A hint is a comment to the server.
    private static bool OnlyCommentsFollow(string sql, EquatableArray<SqlLexeme> lexemes, int from)
    {
        for (var index = from; index < lexemes.Count; index++)
        {
            var lexeme = lexemes[index];
            var isComment =
                lexeme.Kind is SqlLexemeKind.LineComment or SqlLexemeKind.BlockComment or SqlLexemeKind.Hint;
            var isSpace =
                lexeme.Kind == SqlLexemeKind.Text && sql.AsSpan(lexeme.Span.Start, lexeme.Span.Length).IsWhiteSpace();
            if (!isComment && !isSpace)
            {
                return false;
            }
        }

        return true;
    }
}
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "SqlSource.Tool.Tests.Describing.Postgres.ParameterPlaceholdersTests"`
Expected: PASS.

- [ ] **Step 7: Write the failing tests of the two refusals**

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs`, add `using SqlSource.Parsing;` and `using SqlSource.Tool.Describing.Describers;`, and in the class:

```csharp
    private static DescribeRequest Query(params SqlQueryParameter[] parameters) =>
        new("Q", "SELECT 1", new EquatableArray<SqlQueryParameter>([.. parameters]));

    private static SqlQueryParameter Inferred(string name) => new(name, null, null, false);

    private static SqlQueryParameter Declared(string name, string type) => new(name, type, null, true);

    [Fact]
    public void SeveralStatements_Query_IsSqlsrc230WithItsHelp()
    {
        var failure = PostgresErrors.SeveralStatements(Query());

        failure.Descriptor.Id.ShouldBe("SQLSRC230");
        failure.Arguments.ShouldBe(["Q"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.SeveralStatementsHelp);
    }

    // The type as the marker wrote it, and the parameter as the SQL writes one, with its prefix.
    [Fact]
    public void TypeRefused_Parameter_IsSqlsrc228ThatSaysItIsNoTypeName()
    {
        var parameter = Declared("tenant", "int) AS SELECT 1");

        var failure = PostgresErrors.TypeRefused(Query(parameter), parameter);

        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["int) AS SELECT 1", "@tenant", "Q", "it is not written as a type name"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.NotATypeNameHelp);
    }
```

`tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`:

```csharp
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The session over a replay the test writes: what it asks, in which order, and what it makes of each answer.  A
// call the script does not hold is a CaptureMissException, so a describe that asks more than its test wrote fails.
public class PostgresSessionTests
{
    private static DescribeRequest Request(string sql, params SqlQueryParameter[] parameters) =>
        new("Q", sql, new EquatableArray<SqlQueryParameter>([.. parameters]));

    private static SqlQueryParameter Inferred(string name) => new(name, null, null, false);

    private static SqlQueryParameter Declared(string name, string type) => new(name, type, null, true);

    // The script holds "open" alone: nothing may be asked for a text of two statements.
    [Theory]
    [InlineData("DELETE FROM users WHERE id = @id; DELETE FROM orders")]
    [InlineData("SELECT 1;;")]
    public async Task DescribeAsync_SeveralStatements_IsSqlsrc230AndAsksNothing(string sql)
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(
            Request(sql, Inferred("id")),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldBeNull();
        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC230");
    }

    [Fact]
    public async Task DescribeAsync_DeclaredTypeThatIsNoTypeName_IsSqlsrc228AndAsksNothing()
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(
            Request(
                "SELECT 1 FROM users WHERE id = @id",
                Declared("id", "int) AS SELECT 1; DROP TABLE users; --")
            ),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments[1].ShouldBe("@id");
        failure.Arguments[3].ShouldBe(PostgresErrors.NotATypeName);
    }

    // The statements are counted before the types are read: the first thing wrong is the first reported.
    [Fact]
    public async Task DescribeAsync_SeveralStatementsAndABadType_IsSqlsrc230()
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(
            Request("SELECT @id; SELECT 2", Declared("id", "int foo")),
            TestContext.Current.CancellationToken
        );

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC230");
    }
}
```

In `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, at the end of the class:

```csharp
    [Fact]
    public void DeclaredTypeNotValid_Message_HoldsTheTypeTheParameterTheQueryAndTheReason() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.DeclaredTypeNotValid.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "nope_t",
                "@p",
                "GetUser",
                "the database has no such type"
            )
            .ShouldBe(
                "The type 'nope_t' declared for the parameter '@p' of the query 'GetUser' is not valid: the database "
                    + "has no such type"
            );

    [Fact]
    public void QueryHasSeveralStatements_Message_HoldsTheQuery() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.QueryHasSeveralStatements.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "Cleanup"
            )
            .ShouldBe("The query 'Cleanup' holds more than one statement");
```

- [ ] **Step 8: Add the two descriptors in their four places**

`src/SqlSource/Diagnostics/ToolDiagnostics.cs`, after `DatabaseCannotBeOpened`:

```csharp
    public static readonly DiagnosticDescriptor DeclaredTypeNotValid = new(
        id: "SQLSRC228",
        title: "Declared type is not valid",
        messageFormat: "The type '{0}' declared for the parameter '{1}' of the query '{2}' is not valid: {3}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc228",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor QueryHasSeveralStatements = new(
        id: "SQLSRC230",
        title: "Query holds more than one statement",
        messageFormat: "The query '{0}' holds more than one statement",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc230",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

and `DeclaredTypeNotValid, QueryHasSeveralStatements` after `DatabaseCannotBeOpened` in `All`.

`src/SqlSource/AnalyzerReleases.Unshipped.md`, at the end:

```
SQLSRC228 | SqlSource | Error | Declared type is not valid
SQLSRC230 | SqlSource | Error | Query holds more than one statement
```

`docs/diagnostics.md`: two rows at the end of the table,

```markdown
| [SQLSRC228](#sqlsrc228) | Declared type is not valid |
| [SQLSRC230](#sqlsrc230) | Query holds more than one statement |
```

and two sections at the end of the file:

````markdown
## SQLSRC228

**Declared type is not valid**

A `-- param:` marker gives a parameter a type, and the tool asks the server to resolve that type and to check it against every use of the parameter.  This type cannot be used, for the reason the message ends with:

- `it is not written as a type name`: the text is not a type as a column definition writes one.  The tool puts a declared type into the statement it sends, so it reads the text first and sends nothing that is not a name, PostgreSQL's own type words such as `varying` or `without time zone`, one modifier in parentheses, and `[]`.
- `the database has no such type`: the server knows no type of that name on the session's search path.
- `it is a pseudo-type`: the server knows the name, and no value has that type, `record` and `anyelement` for two.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC228: The type 'user_state' declared for the parameter '@status' of the query 'ListUsers' is not valid: the database has no such type
    query: ListUsers, database app, postgres 18.4, Npgsql 10.0.3
    step: describe parameters
    server: 42704: type "user_state" does not exist
    help: write a type the database has, schema-qualified if it is not on the search path
```

Write the type as the database has it: `int`, `varchar(50)`, `numeric(18,2)`, `timestamp(3) without time zone`, `public.user_status`, `"My Schema"."Status"[]`.  A type in a schema that is not on the search path of the connection needs its schema.  A marker's type runs to the end of its line, but for a `null` or `not null` that ends it, so nothing else may follow it there.

## SQLSRC230

**Query holds more than one statement**

A query is one statement.  The tool prepares it on the server to describe it, and a text with a second statement would have the first prepared and the second run.  Nothing of such a query is sent.

```console
/work/App/Queries/Cleanup.sql(1,10): error SQLSRC230: The query 'Cleanup' holds more than one statement
    query: Cleanup, database app, postgres 18.4, Npgsql 10.0.3
    step: describe parameters
    help: one statement for each query; a ; is allowed at the end alone
```

Give each statement a `-- name:` of its own.  A `;` at the end of the statement is allowed, with white space and comments after it; a `;` inside a string, a dollar-quoted string or a comment does not count.
````

- [ ] **Step 9: Write the two refusals, and refuse in the session**

In `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`, add `using SqlSource.Parsing;` and, in the class:

```csharp
    public const string NotATypeName = "it is not written as a type name";

    public const string NotATypeNameHelp =
        "write the type as a column definition would: a name, a modifier in parentheses, []";

    public const string SeveralStatementsHelp = "one statement for each query; a ; is allowed at the end alone";

    /// <summary><c>SQLSRC230</c>: the text holds a second statement.  Nothing of it is sent.</summary>
    public static DescribeFailure SeveralStatements(DescribeRequest query) =>
        new(
            ToolDiagnostics.QueryHasSeveralStatements,
            new EquatableArray<string>([query.Name]),
            DescribeStep.DescribeParameters,
            EquatableArray<string>.Empty,
            SeveralStatementsHelp
        );

    /// <summary>
    /// <c>SQLSRC228</c> for a type that <see cref="DeclaredTypeText" /> refuses.  Nothing of the query is sent.
    /// </summary>
    public static DescribeFailure TypeRefused(DescribeRequest query, SqlQueryParameter parameter) =>
        DeclaredType(
            query,
            parameter,
            NotATypeName,
            DescribeStep.DescribeParameters,
            EquatableArray<string>.Empty,
            NotATypeNameHelp
        );

    /// <summary>A parameter as the SQL writes one: the server names it <c>$1</c>, and the user wrote <c>@id</c>.</summary>
    public static string Written(SqlQueryParameter parameter) =>
        SqlDialectRules.PostgreSql.ParameterPrefix + parameter.Name;

    // SQLSRC228, whose last argument is the reason the type cannot be used.
    private static DescribeFailure DeclaredType(
        DescribeRequest query,
        SqlQueryParameter parameter,
        string reason,
        DescribeStep step,
        EquatableArray<string> serverLines,
        string help
    ) =>
        new(
            ToolDiagnostics.DeclaredTypeNotValid,
            new EquatableArray<string>([parameter.Type ?? string.Empty, Written(parameter), query.Name, reason]),
            step,
            serverLines,
            help
        );
```

In `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, `DescribeAsync` becomes:

```csharp
    // The calls arrive in the next steps.  Until then a query that passes the two checks has no typed parameter.
    public Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        DescribeRequest request,
        CancellationToken cancellationToken
    )
    {
        if (ParameterPlaceholders.Rewrite(request.Sql, request.Parameters) is null)
        {
            return Task.FromResult(Failed(PostgresErrors.SeveralStatements(request)));
        }

        var types = ParameterPlaceholders.TypeList(request.Parameters);
        return Task.FromResult(
            types.Succeeded
                ? Outcome<QueryDescription, DescribeFailure>.Of(
                    new QueryDescription(
                        SidecarResultKind.None,
                        EquatableArray<DescribedParameter>.Empty,
                        null,
                        null,
                        null,
                        null
                    )
                )
                : Failed(PostgresErrors.TypeRefused(request, types.Failure))
        );
    }
```

and the class gains:

```csharp
    private static Outcome<QueryDescription, DescribeFailure> Failed(DescribeFailure failure) =>
        Outcome<QueryDescription, DescribeFailure>.Failed(failure);
```

- [ ] **Step 10: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests`, then `dotnet test --project tests/SqlSource.Tests`
Expected: PASS.

- [ ] **Step 11: Write the container tests**

In `tests/SqlSource.Tests.Postgres/Describer/Sessions.cs`, add to the class:

```csharp
    // How many prepared statements the session holds.  The view shows those of its own session alone, so this asks
    // on the session's connection.
    public static async Task<long> PreparedStatementsAsync(PostgresSession session)
    {
        var token = TestContext.Current.CancellationToken;
        await using var command = await session.Connection.CommandAsync(
            "SELECT count(*) FROM pg_catalog.pg_prepared_statements",
            token
        );
        return (long)(await command.ExecuteScalarAsync(token) ?? 0L);
    }
```

`tests/SqlSource.Tests.Postgres/Describer/Requests.cs`:

```csharp
using System;
using System.Collections.Generic;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tests.Postgres.Describer;

// A request as the plan of a run makes one, from SQL a test writes: the parameters of the SQL in the order they
// first appear, then the declared ones that the SQL does not hold, as SqlParameterList orders a query's list.
internal static class Requests
{
    public const string Query = "Q";

    public static DescribeRequest Of(string sql, params (string Name, string Type)[] declared)
    {
        var parameters = new List<SqlQueryParameter>();
        foreach (var lexeme in SqlLexer.Lex(sql, SqlDialectRules.PostgreSql).Lexemes)
        {
            if (lexeme.Kind == SqlLexemeKind.Parameter)
            {
                Add(parameters, sql.Substring(lexeme.Span.Start + 1, lexeme.Span.Length - 1), declared);
            }
        }

        foreach (var (name, _) in declared)
        {
            Add(parameters, name, declared);
        }

        return new DescribeRequest(Query, sql, new EquatableArray<SqlQueryParameter>([.. parameters]));
    }

    private static void Add(List<SqlQueryParameter> parameters, string name, (string Name, string Type)[] declared)
    {
        if (parameters.Exists(parameter => Same(parameter.Name, name)))
        {
            return;
        }

        var index = Array.FindIndex(declared, found => Same(found.Name, name));
        parameters.Add(
            index < 0
                ? new SqlQueryParameter(name, null, null, false)
                : new SqlQueryParameter(name, declared[index].Type, null, true)
        );
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
```

`tests/SqlSource.Tests.Postgres/Describer/ParameterTests.cs`:

```csharp
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// A query's parameters against a real server.  Every describe here runs in a session of its own and ends with no
// prepared statement left in it.
[Collection(UsesPostgres.Name)]
public sealed class ParameterTests(PostgresContainer postgres)
{
    private async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        DescribeRequest request,
        IDescribeExchange? exchange = null
    )
    {
        var opened = await Sessions.OpenAsync(postgres.ConnectionString, exchange);
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();

        var described = await session.DescribeAsync(request, TestContext.Current.CancellationToken);

        (await Sessions.PreparedStatementsAsync(session)).ShouldBe(0);
        return described;
    }

    private static string[] MethodsOf(DescribeCapture capture) => [.. capture.Calls.Select(call => call.Method)];

    [Fact]
    public async Task Describe_TwoStatements_IsSqlsrc230AndSendsNothing()
    {
        var capture = new DescribeCapture("postgres");

        var described = await DescribeAsync(
            Requests.Of("DELETE FROM orders WHERE user_id = @id; DELETE FROM users WHERE id = @id"),
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC230");
        MethodsOf(capture).ShouldBe(["open"]);
    }

    [Fact]
    public async Task Describe_DeclaredTypeThatIsNoTypeName_IsSqlsrc228AndSendsNothing()
    {
        var capture = new DescribeCapture("postgres");

        var described = await DescribeAsync(
            Requests.Of("SELECT 1 FROM users WHERE id = @id", ("id", "int) AS SELECT 1; DROP TABLE users; --")),
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe([
            "int) AS SELECT 1; DROP TABLE users; --",
            "@id",
            Requests.Query,
            PostgresErrors.NotATypeName,
        ]);
        MethodsOf(capture).ShouldBe(["open"]);
    }
}
```

Run: `dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: PASS, 9 tests.

- [ ] **Step 12: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`
Expected: every step `passed`.

```bash
git add src/SqlSource.Tool/Describing/Postgres src/SqlSource/Diagnostics/ToolDiagnostics.cs src/SqlSource/AnalyzerReleases.Unshipped.md docs/diagnostics.md tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs tests/SqlSource.Tool.Tests/Describing/Postgres tests/SqlSource.Tests.Postgres/Describer
```

```bash
git commit -m "Refuse several statements and a type that is no type name before anything is sent

The statement check and the placeholders over the generator's lexer, the
grammar of a declared type, SQLSRC230 and the refusing form of SQLSRC228.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `prepare`, `deallocate` and the server's errors

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`, `PostgresErrors.cs`, `PostgresSession.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Modify: `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs`, `PostgresSessionTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/ParameterTests.cs`

**Interfaces:**
- Consumes: `ParameterPlaceholders.Rewrite`, `TypeList`, `PrepareText`, `HeadLength`, `TypeAt`; `PostgresErrors.ServerLines`, `Written`, `DeclaredType`; `PostgresConnection.CommandAsync`, `ErrorOf`.
- Produces, in `SqlSource.Tool.Describing.Postgres`:
  - `sealed record PostgresPrepareRequest(string Statement, string Sql, IReadOnlyList<string> Types)`
  - `sealed record PostgresPrepareAnswer(IReadOnlyList<long>? ParameterTypes, PostgresServerError? Error)`
  - `sealed record PostgresDeallocateRequest(string Statement)`
  - `sealed record PostgresDeallocateAnswer(PostgresServerError? Error)`
  - `PostgresErrors.Prepare(PostgresServerError error, DescribeRequest query, PostgresPrepareRequest prepare)`, and the constants `NoSuchType`, `NoSuchTypeHelp`, `FirstUseHelp`
  - `ToolDiagnostics.ServerRejectedQuery` (`SQLSRC225`), `ParameterTypeNotDetermined` (`SQLSRC226`), `ParameterFirstUseDoesNotFixType` (`SQLSRC227`)
  - `PostgresSession` asks `prepare` and `deallocate`; a statement's name is `sqlsource_<n>`.

- [ ] **Step 1: Write the failing tests of the errors**

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs`, add to the class.  The positions are the ones the spike saw from the server.

```csharp
    private static PostgresPrepareRequest Prepared(string statement, params string[] types) =>
        new(statement, "SELECT 1", types);

    private static PostgresServerError Error(string state, string message, int? position = null, string? hint = null) =>
        new(state, message, null, hint, position);

    // 42P18 has no position.  The server names the parameter $2, and the user wrote @b.
    [Fact]
    public void Prepare_42P18_IsSqlsrc226NamingTheParameterOfTheMessage()
    {
        var failure = PostgresErrors.Prepare(
            Error("42P18", "could not determine data type of parameter $2"),
            Query(Inferred("a"), Inferred("b")),
            Prepared("sqlsource_1", "unknown", "unknown")
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC226");
        failure.Arguments.ShouldBe(["@b", "Q"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe(["42P18: could not determine data type of parameter $2"]);
        failure.Help.ShouldBe(
            "cast the parameter where it is used, @b::int, or declare its type with -- param: @b int"
        );
    }

    // "PREPARE sqlsource_7 (unknown" and a line break and ") AS " are 34 characters, so 62 is the 28th of the SQL.
    [Fact]
    public void Prepare_42P08_IsSqlsrc227WithThePositionInTheUsersSql()
    {
        var failure = PostgresErrors.Prepare(
            Error("42P08", "could not determine data type of parameter $1", 62),
            Query(Inferred("name")),
            Prepared("sqlsource_7", "unknown")
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC227");
        failure.Arguments.ShouldBe(["@name", "Q"]);
        failure.ServerLines.ShouldBe(["42P08: could not determine data type of parameter $1", "at character 28"]);
        failure.Help.ShouldBe(PostgresErrors.FirstUseHelp);
    }

    [Theory]
    [InlineData("42P18", "could not determine data type of parameter $7")]
    [InlineData("42P08", "could not determine data type of a parameter")]
    public void Prepare_ParameterErrorThatNamesNoParameterOfTheList_IsSqlsrc225(string state, string message) =>
        PostgresErrors
            .Prepare(Error(state, message), Query(Inferred("a")), Prepared("sqlsource_1", "unknown"))
            .Descriptor.Id.ShouldBe("SQLSRC225");

    // The character 32 of "PREPARE sqlsource_10 (unknown, nope_t" is the n of nope_t.
    [Fact]
    public void Prepare_42704InsideTheTypeList_IsSqlsrc228NamingTheTypeAndTheParameter()
    {
        var failure = PostgresErrors.Prepare(
            Error("42704", "type \"nope_t\" does not exist", 32),
            Query(Inferred("id"), Declared("tenant", "nope_t")),
            Prepared("sqlsource_10", "unknown", "nope_t")
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["nope_t", "@tenant", "Q", "the database has no such type"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe(["42704: type \"nope_t\" does not exist"]);
        failure.Help.ShouldBe(PostgresErrors.NoSuchTypeHelp);
    }

    // The same SQLSTATE at a cast in the SQL is the server rejecting the query: 47 less the 35 before the SQL.
    [Fact]
    public void Prepare_42704OutsideTheTypeList_IsSqlsrc225WithThePosition()
    {
        var failure = PostgresErrors.Prepare(
            Error("42704", "type \"nope_t\" does not exist", 47),
            Query(Inferred("p")),
            Prepared("sqlsource_11", "unknown")
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["Q", "type \"nope_t\" does not exist"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe(["42704: type \"nope_t\" does not exist", "at character 12"]);
        failure.Help.ShouldBeNull();
    }

    [Fact]
    public void Prepare_42704WithNoPosition_IsSqlsrc225() =>
        PostgresErrors
            .Prepare(
                Error("42704", "type \"nope_t\" does not exist"),
                Query(Declared("p", "nope_t")),
                Prepared("sqlsource_1", "nope_t")
            )
            .Descriptor.Id.ShouldBe("SQLSRC225");

    // Any other error the server positions inside the type list is the type's too: PostgreSQL 14 has no negative
    // scale, and says so at the type.
    [Theory]
    [InlineData("22023", "NUMERIC scale -3 must be between 0 and precision 2")]
    [InlineData("42601", "syntax error at or near \"numeric\"")]
    public void Prepare_OtherErrorInsideTheTypeList_IsSqlsrc228ThatSaysItIsNoTypeName(string state, string message)
    {
        var failure = PostgresErrors.Prepare(
            Error(state, message, 23),
            Query(Declared("p", "numeric(2,-3)")),
            Prepared("sqlsource_35", "numeric(2,-3)")
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["numeric(2,-3)", "@p", "Q", "it is not written as a type name"]);
        failure.ServerLines.ShouldBe([$"{state}: {message}"]);
        failure.Help.ShouldBe(PostgresErrors.NotATypeNameHelp);
    }

    // SQLSRC228 names a declared type, and the item at the position is the server's own "unknown".
    [Fact]
    public void Prepare_ErrorInsideTheTypeListAtAnInferredParameter_IsSqlsrc225() =>
        PostgresErrors
            .Prepare(Error("42601", "syntax error", 23), Query(Inferred("p")), Prepared("sqlsource_1", "unknown"))
            .Descriptor.Id.ShouldBe("SQLSRC225");

    [Fact]
    public void Prepare_AnyOtherError_IsSqlsrc225WithEveryLineOfTheServer()
    {
        var failure = PostgresErrors.Prepare(
            Error("42883", "operator does not exist: character varying = integer", 63, "You might need to add casts."),
            Query(Declared("p", "int")),
            Prepared("sqlsource_12", "int")
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["Q", "operator does not exist: character varying = integer"]);
        failure.ServerLines.ShouldBe([
            "42883: operator does not exist: character varying = integer",
            "hint: You might need to add casts.",
            "at character 32",
        ]);
        failure.Help.ShouldBeNull();
    }

    // A connection that dropped: the driver's message, and no SQLSTATE.
    [Fact]
    public void Prepare_ErrorWithoutASqlState_IsSqlsrc225WithTheMessageAlone()
    {
        var failure = PostgresErrors.Prepare(
            new PostgresServerError(null, "Exception while reading from stream", null, null, null),
            Query(),
            Prepared("sqlsource_1")
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.ServerLines.ShouldBe(["Exception while reading from stream"]);
    }

    // The server counts a character outside the basic plane once, and so does the tool: the fifth character of the
    // SQL, whatever the type list holds.
    [Fact]
    public void Prepare_TypeListWithACharacterOutsideTheBasicPlane_StillCountsFromTheSql()
    {
        var prepare = Prepared("sqlsource_1", "\"\U0001F600\"");
        var head = ParameterPlaceholders.HeadLength(prepare.Statement, prepare.Types);

        var failure = PostgresErrors.Prepare(
            Error("42601", "syntax error", head + 5),
            Query(Declared("p", "\"\U0001F600\"")),
            prepare
        );

        failure.ServerLines.ShouldBe(["42601: syntax error", "at character 5"]);
    }
```

In `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, at the end of the class:

```csharp
    [Fact]
    public void ServerRejectedQuery_Message_HoldsTheQueryAndTheServersMessage() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.ServerRejectedQuery.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "GetUser",
                "relation \"nope\" does not exist"
            )
            .ShouldBe("The server rejected the query 'GetUser': relation \"nope\" does not exist");

    [Fact]
    public void ParameterTypeNotDetermined_Message_HoldsTheParameterAndTheQuery() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.ParameterTypeNotDetermined.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "@p",
                "IsNull"
            )
            .ShouldBe("The type of the parameter '@p' of the query 'IsNull' could not be determined");

    [Fact]
    public void ParameterFirstUseDoesNotFixType_Message_HoldsTheParameterAndTheQuery() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.ParameterFirstUseDoesNotFixType.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "@name",
                "FindUsers"
            )
            .ShouldBe("The first use of the parameter '@name' of the query 'FindUsers' does not fix its type");
```

- [ ] **Step 2: Write the failing tests of the session**

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`, add to the class:

```csharp
    private static readonly PostgresDeallocateAnswer Freed = new(null);

    private static PostgresScript Prepares(
        PostgresScript script,
        int number,
        string sql,
        string[] types,
        PostgresPrepareAnswer answer
    ) => script.Answer("prepare", new PostgresPrepareRequest($"sqlsource_{number}", sql, types), answer);

    private static PostgresScript Frees(PostgresScript script, int number, PostgresDeallocateAnswer? answer = null) =>
        script.Answer("deallocate", new PostgresDeallocateRequest($"sqlsource_{number}"), answer ?? Freed);

    // The trailing ";" is not sent, the parameter is $1, and the statement is freed when the describe is done.
    [Fact]
    public async Task DescribeAsync_Query_AsksPrepareThenDeallocateForTheStatement()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "DELETE FROM samples WHERE flag", [], new PostgresPrepareAnswer([], null));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("DELETE FROM samples WHERE flag;"),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldNotBeNull();
        exchange.Unused.ShouldBeEmpty();
    }

    // A name is a counter within its session, so that a capture is the same each time it is recorded.
    [Fact]
    public async Task DescribeAsync_SecondQuery_TakesTheNextName()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "DELETE FROM samples", [], new PostgresPrepareAnswer([], null));
        _ = Frees(script, 1);
        _ = Prepares(script, 2, "DELETE FROM orders", [], new PostgresPrepareAnswer([], null));
        var exchange = Frees(script, 2).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        _ = await session.DescribeAsync(Request("DELETE FROM samples"), TestContext.Current.CancellationToken);
        var second = await session.DescribeAsync(Request("DELETE FROM orders"), TestContext.Current.CancellationToken);

        second.Value.ShouldNotBeNull();
        exchange.Unused.ShouldBeEmpty();
    }

    // There is no statement to free, so "deallocate" is not asked: the script holds none for the first name.  The
    // counter advances all the same.
    [Fact]
    public async Task DescribeAsync_PrepareThatFails_IsTheFailureFreesNothingAndTheNextTakesTheNextName()
    {
        // "PREPARE sqlsource_1 AS " is 23 characters, and nope is the 13th of the SQL.
        var rejected = new PostgresServerError("42P01", "relation \"nope\" does not exist", null, null, 36);
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "DELETE FROM nope", [], new PostgresPrepareAnswer(null, rejected));
        _ = Prepares(script, 2, "DELETE FROM orders", [], new PostgresPrepareAnswer([], null));
        var exchange = Frees(script, 2).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var first = await session.DescribeAsync(Request("DELETE FROM nope"), TestContext.Current.CancellationToken);
        var second = await session.DescribeAsync(Request("DELETE FROM orders"), TestContext.Current.CancellationToken);

        var failure = first.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["Q", "relation \"nope\" does not exist"]);
        failure.ServerLines.ShouldBe(["42P01: relation \"nope\" does not exist", "at character 13"]);
        second.Value.ShouldNotBeNull();
        exchange.Unused.ShouldBeEmpty();
    }

    // The error of "deallocate" is logged and never reported.
    [Fact]
    public async Task DescribeAsync_DeallocateThatFails_IsStillDescribed()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "DELETE FROM samples", [], new PostgresPrepareAnswer([], null));
        var gone = new PostgresServerError("26000", "prepared statement \"sqlsource_1\" does not exist", null, null, null);
        var exchange = Frees(script, 1, new PostgresDeallocateAnswer(gone)).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Request("DELETE FROM samples"), TestContext.Current.CancellationToken);

        described.Value.ShouldNotBeNull();
        described.Failure.ShouldBeNull();
    }

    // The declared type goes into the list as written, and the rest is "unknown".
    [Fact]
    public async Task DescribeAsync_DeclaredAndInferredParameters_AskWithTheTypeListOfTheQuery()
    {
        var rejected = new PostgresServerError("42704", "type \"varchar\" does not exist", null, null, 31);
        var script = new PostgresScript().Open();
        var exchange = Prepares(
                script,
                1,
                "SELECT 1 FROM users WHERE id = $1 AND name = $2",
                ["unknown", "varchar(50)", "nope_t"],
                new PostgresPrepareAnswer(null, rejected)
            )
            .Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request(
                "SELECT 1 FROM users WHERE id = @id AND name = @name",
                Inferred("id"),
                Declared("name", "varchar(50)"),
                Declared("tenant", "nope_t")
            ),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["varchar(50)", "@name", "Q", PostgresErrors.NoSuchType]);
        exchange.Unused.ShouldBeEmpty();
    }
```

The last test's position, 31, is the `v` of `varchar(50)` in `PREPARE sqlsource_1 (unknown, varchar(50), nope_t`: the failure names the type the server pointed at, which is the second item and not the one the message quotes.

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet build tests/SqlSource.Tool.Tests`
Expected: FAIL, `PostgresPrepareRequest` and `PostgresErrors.Prepare` do not exist.

- [ ] **Step 4: Add the three descriptors in their four places**

`src/SqlSource/Diagnostics/ToolDiagnostics.cs`, between `DatabaseCannotBeOpened` and `DeclaredTypeNotValid`:

```csharp
    public static readonly DiagnosticDescriptor ServerRejectedQuery = new(
        id: "SQLSRC225",
        title: "Server rejected the query",
        messageFormat: "The server rejected the query '{0}': {1}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc225",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ParameterTypeNotDetermined = new(
        id: "SQLSRC226",
        title: "Parameter type could not be determined",
        messageFormat: "The type of the parameter '{0}' of the query '{1}' could not be determined",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc226",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ParameterFirstUseDoesNotFixType = new(
        id: "SQLSRC227",
        title: "Parameter's first use does not fix its type",
        messageFormat: "The first use of the parameter '{0}' of the query '{1}' does not fix its type",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc227",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

and `ServerRejectedQuery, ParameterTypeNotDetermined, ParameterFirstUseDoesNotFixType` between `DatabaseCannotBeOpened` and `DeclaredTypeNotValid` in `All`.

`src/SqlSource/AnalyzerReleases.Unshipped.md`: between the rows of `SQLSRC224` and `SQLSRC228`,

```
SQLSRC225 | SqlSource | Error | Server rejected the query
SQLSRC226 | SqlSource | Error | Parameter type could not be determined
SQLSRC227 | SqlSource | Error | Parameter's first use does not fix its type
```

`docs/diagnostics.md`: three rows between those of `SQLSRC224` and `SQLSRC228`,

```markdown
| [SQLSRC225](#sqlsrc225) | Server rejected the query |
| [SQLSRC226](#sqlsrc226) | Parameter type could not be determined |
| [SQLSRC227](#sqlsrc227) | Parameter's first use does not fix its type |
```

and three sections between those of `SQLSRC224` and `SQLSRC228`:

````markdown
## SQLSRC225

**Server rejected the query**

The tool asked the server to prepare the query, and the server refused it: a syntax error, a table or a column the database does not have, an operator that does not exist for the types it is given.  The message ends with what the server said, and the `server:` lines give all of it: the SQLSTATE and the message, then its `detail:` and `hint:` where it gave them.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC225: The server rejected the query 'GetUser': relation "user" does not exist
    query: GetUser, database app, postgres 18.4, Npgsql 10.0.3
    step: describe parameters
    server: 42P01: relation "user" does not exist
    server: at character 15
```

Mend the query, or bring the database to the schema the query was written for.  `at character` counts from the start of the query's SQL as the tool sent it: without its comments, with each token's default in the token's place and `$1` in the place of each parameter, so it is close to the place in the file and not always the same.  Run with `--log` for the SQL as it was sent.  A type that a `-- param:` marker declares is checked against every use of the parameter, so a declared `int` compared with a text column is this error too.  It is also what a connection that dropped while a query was described is reported as, with the driver's message and no SQLSTATE.

## SQLSRC226

**Parameter type could not be determined**

The server gives each parameter a type from the way the query uses it, and nothing in this query says what the parameter is: `SELECT @p IS NULL` holds for a value of any type.

```console
/work/App/Queries/Checks.sql(1,10): error SQLSRC226: The type of the parameter '@p' of the query 'IsNull' could not be determined
    query: IsNull, database app, postgres 18.4, Npgsql 10.0.3
    step: describe parameters
    server: 42P18: could not determine data type of parameter $1
    help: cast the parameter where it is used, @p::int, or declare its type with -- param: @p int
```

Cast the parameter where it is used, or declare its type with a `-- param:` marker.  The same error, at the step `resolve type` and with no `server:` line, says that the server did give the parameter a type and that the type is a pseudo-type, which no value has: `row_to_json(@p)` makes `@p` a `record`.  Its `help:` line names the pseudo-type, and the fix is the same.

## SQLSRC227

**Parameter's first use does not fix its type**

PostgreSQL takes a parameter's type from its first use.  Here the first use says nothing of the type and a later one does, which the server refuses.  The pattern of an optional filter does it: in `(@name IS NULL OR name = @name)` the first `@name` could be anything.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC227: The first use of the parameter '@name' of the query 'FindUsers' does not fix its type
    query: FindUsers, database app, postgres 18.4, Npgsql 10.0.3
    step: describe parameters
    server: 42P08: could not determine data type of parameter $1
    server: at character 28
    help: declare the type with -- param:, or write the use that fixes the type before the one that does not: (name = @name OR @name IS NULL)
```

Declare the parameter's type with `-- param: @name text`, or turn the condition round so that the use that fixes the type comes first.
````

- [ ] **Step 5: Add the records of the two calls**

At the end of `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`:

```csharp
/// <summary>Everything that decides the answer of <c>prepare</c>.</summary>
/// <param name="Statement">The name of the prepared statement: a counter within its session, never random.</param>
/// <param name="Sql">The query's one statement, with <c>$n</c> in the place of each parameter.</param>
/// <param name="Types">The type list: a declared type as written or <c>unknown</c>, for each parameter in order.</param>
internal sealed record PostgresPrepareRequest(string Statement, string Sql, IReadOnlyList<string> Types);

/// <summary>The types the server resolved, or why it prepared nothing.</summary>
/// <param name="ParameterTypes">
/// <c>parameter_types</c> of <c>pg_prepared_statements</c> for the statement, as OIDs, in order.
/// </param>
/// <param name="Error">What the server said, or null.</param>
internal sealed record PostgresPrepareAnswer(IReadOnlyList<long>? ParameterTypes, PostgresServerError? Error);

/// <summary>The request of <c>deallocate</c>: the statement to free.</summary>
internal sealed record PostgresDeallocateRequest(string Statement);

/// <summary>Null when the statement was freed.  An error here is logged and never reported.</summary>
internal sealed record PostgresDeallocateAnswer(PostgresServerError? Error);
```

- [ ] **Step 6: Write the map of a `prepare` that failed**

In `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`, add `using System;`, and in the class:

```csharp
    public const string NoSuchType = "the database has no such type";

    public const string NoSuchTypeHelp =
        "write a type the database has, schema-qualified if it is not on the search path";

    public const string FirstUseHelp =
        "declare the type with -- param:, or write the use that fixes the type before the one that does not: "
        + "(name = @name OR @name IS NULL)";

    private const string UndefinedObject = "42704";
    private const string IndeterminateDatatype = "42P18";
    private const string AmbiguousParameter = "42P08";

    /// <summary>
    /// What a failed <c>prepare</c> is reported as.  An error the server positions inside the type list is the
    /// declared type's, <c>SQLSRC228</c>; <c>42P18</c> and <c>42P08</c> name a parameter, <c>SQLSRC226</c> and
    /// <c>SQLSRC227</c>; anything else is the server rejecting the query, <c>SQLSRC225</c>.
    /// </summary>
    public static DescribeFailure Prepare(
        PostgresServerError error,
        DescribeRequest query,
        PostgresPrepareRequest prepare
    )
    {
        var head = ParameterPlaceholders.HeadLength(prepare.Statement, prepare.Types);
        var lines = ServerLines(error, head);
        if (DeclaredAt(error, query, prepare, head) is { } declared)
        {
            return error.SqlState == UndefinedObject
                ? DeclaredType(query, declared, NoSuchType, DescribeStep.DescribeParameters, lines, NoSuchTypeHelp)
                : DeclaredType(query, declared, NotATypeName, DescribeStep.DescribeParameters, lines, NotATypeNameHelp);
        }

        if (error.SqlState is IndeterminateDatatype or AmbiguousParameter && NamedIn(error.Message, query) is { } named)
        {
            var written = Written(named);
            return error.SqlState == IndeterminateDatatype
                ? new DescribeFailure(
                    ToolDiagnostics.ParameterTypeNotDetermined,
                    new EquatableArray<string>([written, query.Name]),
                    DescribeStep.DescribeParameters,
                    lines,
                    $"cast the parameter where it is used, {written}::int, or declare its type with "
                        + $"-- param: {written} int"
                )
                : new DescribeFailure(
                    ToolDiagnostics.ParameterFirstUseDoesNotFixType,
                    new EquatableArray<string>([written, query.Name]),
                    DescribeStep.DescribeParameters,
                    lines,
                    FirstUseHelp
                );
        }

        return Rejected(error, query, DescribeStep.DescribeParameters, lines);
    }

    // SQLSRC225.  The server's lines say what is wrong, so there is no help.
    private static DescribeFailure Rejected(
        PostgresServerError error,
        DescribeRequest query,
        DescribeStep step,
        EquatableArray<string> serverLines
    ) =>
        new(
            ToolDiagnostics.ServerRejectedQuery,
            new EquatableArray<string>([query.Name, error.Message]),
            step,
            serverLines,
            null
        );

    // The parameter whose declared type the server's position falls in, or null: for no position, one in the SQL,
    // and one at an item that is the tool's own "unknown".
    private static SqlQueryParameter? DeclaredAt(
        PostgresServerError error,
        DescribeRequest query,
        PostgresPrepareRequest prepare,
        int head
    )
    {
        if (error.Position is not { } position || position > head || prepare.Types.Count == 0)
        {
            return null;
        }

        var parameter = query.Parameters[ParameterPlaceholders.TypeAt(prepare.Statement, prepare.Types, position)];
        return parameter.Type is null ? null : parameter;
    }

    // The parameter of the first "$n" of a message, or null when it has none or n is no ordinal of the list.
    private static SqlQueryParameter? NamedIn(string message, DescribeRequest query)
    {
        var dollar = message.IndexOf('$', StringComparison.Ordinal);
        while (dollar >= 0)
        {
            var end = dollar + 1;
            while (end < message.Length && char.IsAsciiDigit(message[end]))
            {
                end++;
            }

            if (
                int.TryParse(
                    message.AsSpan(dollar + 1, end - dollar - 1),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var ordinal
                )
            )
            {
                return ordinal >= 1 && ordinal <= query.Parameters.Count ? query.Parameters[ordinal - 1] : null;
            }

            dollar = message.IndexOf('$', end);
        }

        return null;
    }
```

- [ ] **Step 7: Prepare and free in the session**

`src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs` becomes the following.  What changes: the session keeps the exchange and counts its statements; `DescribeAsync` asks `prepare` and, when it answered without an error, `deallocate` in a `finally`; two `live` functions.  A query that is prepared still has no typed parameter: Task 6 types them.

```csharp
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// A session with one PostgreSQL database.  Every call it makes to the server goes through the exchange, and the
/// connection is made inside a <c>live</c> function and nowhere else.
/// </summary>
/// <remarks>
/// A query is described through one prepared statement: <c>PREPARE</c> takes a declared type as written, resolves
/// it, checks it against every use of its parameter, and infers the rest.  A prepared statement is session state
/// and survives a rollback, so it is freed in a <c>finally</c>, outside any transaction; one left behind would be
/// a leak until the session ends.  Its name is a counter, since the name is part of a request and a capture must be
/// the same each time it is recorded.
/// </remarks>
internal sealed class PostgresSession : IDescribeSession
{
    public const string DriverName = "Npgsql";

    // The settings of a session that change a description, in the order "open" answers them.
    private static readonly string[] SettingNames = ["search_path", "plan_cache_mode", "standard_conforming_strings"];

    private const string OpenSql =
        "SELECT pg_catalog.current_setting('server_version'), pg_catalog.current_setting('search_path'), "
        + "pg_catalog.current_setting('plan_cache_mode'), pg_catalog.current_setting('standard_conforming_strings')";

    private const string ParameterTypesSql =
        "SELECT parameter_types::oid[] FROM pg_catalog.pg_prepared_statements WHERE name = @name";

    private readonly IDescribeExchange _exchange;
    private int _statements;

    private PostgresSession(IDescribeExchange exchange, PostgresConnection connection, ServerInfo server)
    {
        _exchange = exchange;
        Connection = connection;
        Server = server;
    }

    public ServerInfo Server { get; }

    /// <summary>
    /// The session's connection.  The container tests ask on it what a describe left behind:
    /// <c>pg_prepared_statements</c> shows the statements of its own session alone.
    /// </summary>
    internal PostgresConnection Connection { get; }

    /// <summary>Asks <c>open</c>, and gives the session of the server that answered.</summary>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The session owns the connection and is its caller's to close: the run closes what it "
            + "opened.  The analyzer follows a disposable that is returned itself and not one that is returned in "
            + "an Outcome.  docs/tech-debt/TD-0041."
    )]
    public static async Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        CancellationToken cancellationToken
    )
    {
        var connection = new PostgresConnection(request.Connection);
        var kept = false;
        try
        {
            var answer = await request.Exchange.AskAsync(
                "open",
                PostgresOpenRequest.Instance,
                (_, token) => OpenLiveAsync(connection, token),
                cancellationToken
            );
            if (answer.Error is { } error)
            {
                return Outcome<IDescribeSession, DescribeFailure>.Failed(PostgresErrors.Open(error, request.Database));
            }

            var session = new PostgresSession(request.Exchange, connection, ServerOf(answer));
            kept = true;
            return Outcome<IDescribeSession, DescribeFailure>.Of(session);
        }
        finally
        {
            if (!kept)
            {
                await connection.DisposeAsync();
            }
        }
    }

    /// <summary><c>server_version</c> as a sidecar records it: <c>18.4 (Debian 18.4-1.pgdg13+1)</c> is <c>18.4</c>.</summary>
    public static string VersionOf(string serverVersion)
    {
        var space = serverVersion.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? serverVersion : serverVersion[..space];
    }

    public async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        DescribeRequest request,
        CancellationToken cancellationToken
    )
    {
        // Before anything is sent: one statement, and every declared type written as a type name.
        if (ParameterPlaceholders.Rewrite(request.Sql, request.Parameters) is not { } sql)
        {
            return Failed(PostgresErrors.SeveralStatements(request));
        }

        var types = ParameterPlaceholders.TypeList(request.Parameters);
        if (!types.Succeeded)
        {
            return Failed(PostgresErrors.TypeRefused(request, types.Failure));
        }

        // The counter advances for a prepare that fails too: a name is used once.
        _statements++;
        var prepare = new PostgresPrepareRequest(
            string.Create(CultureInfo.InvariantCulture, $"sqlsource_{_statements}"),
            sql,
            types.Value
        );
        var prepared = await _exchange.AskAsync("prepare", prepare, PrepareLiveAsync, cancellationToken);
        if (prepared.Error is { } error)
        {
            // There is no statement to free.
            return Failed(PostgresErrors.Prepare(error, request, prepare));
        }

        try
        {
            return Outcome<QueryDescription, DescribeFailure>.Of(
                new QueryDescription(
                    SidecarResultKind.None,
                    EquatableArray<DescribedParameter>.Empty,
                    null,
                    null,
                    null,
                    null
                )
            );
        }
        finally
        {
            // With the describe's own token: a cancelled describe leaves the statement to die with its session.
            // The error of the call is never reported.
            _ = await _exchange.AskAsync(
                "deallocate",
                new PostgresDeallocateRequest(prepare.Statement),
                DeallocateLiveAsync,
                cancellationToken
            );
        }
    }

    public ValueTask DisposeAsync() => Connection.DisposeAsync();

    private static Outcome<QueryDescription, DescribeFailure> Failed(DescribeFailure failure) =>
        Outcome<QueryDescription, DescribeFailure>.Failed(failure);

    private static ServerInfo ServerOf(PostgresOpenAnswer answer) =>
        new(
            answer.ServerVersion
                ?? throw new InvalidOperationException("The answer to 'open' holds no version and no error."),
            DriverName,
            answer.DriverVersion,
            new EquatableArray<ServerSetting>([
                .. (answer.Settings ?? []).Select(static setting => new ServerSetting(setting.Name, setting.Value)),
            ])
        );

    // In each "live" function, what the server can cause is the answer's error.  A cancellation passes through, and
    // anything else is a bug.
    private static async Task<PostgresOpenAnswer> OpenLiveAsync(PostgresConnection connection, CancellationToken token)
    {
        try
        {
            await using var command = await connection.CommandAsync(OpenSql, token);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                throw new InvalidOperationException("The server answered 'open' with no row.");
            }

            return new PostgresOpenAnswer(
                VersionOf(reader.GetString(0)),
                PostgresConnection.DriverVersion,
                [.. SettingNames.Select((name, index) => new PostgresSetting(name, reader.GetString(index + 1)))],
                null
            );
        }
        catch (NpgsqlException exception)
        {
            return new PostgresOpenAnswer(null, null, null, PostgresConnection.ErrorOf(exception));
        }
    }

    private async Task<PostgresPrepareAnswer> PrepareLiveAsync(PostgresPrepareRequest asked, CancellationToken token)
    {
        try
        {
            var text = ParameterPlaceholders.PrepareText(asked.Statement, asked.Types, asked.Sql);
            await using (var prepare = await Connection.CommandAsync(text, token))
            {
                _ = await prepare.ExecuteNonQueryAsync(token);
            }

            await using var read = await Connection.CommandAsync(ParameterTypesSql, token);
            _ = read.Parameters.AddWithValue("name", asked.Statement);
            var types =
                await read.ExecuteScalarAsync(token) as uint[]
                ?? throw new InvalidOperationException($"The server lists no prepared statement '{asked.Statement}'.");
            return new PostgresPrepareAnswer([.. types.Select(static oid => (long)oid)], null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresPrepareAnswer(null, PostgresConnection.ErrorOf(exception));
        }
    }

    private async Task<PostgresDeallocateAnswer> DeallocateLiveAsync(
        PostgresDeallocateRequest asked,
        CancellationToken token
    )
    {
        try
        {
            await using var command = await Connection.CommandAsync($"DEALLOCATE {asked.Statement}", token);
            _ = await command.ExecuteNonQueryAsync(token);
            return new PostgresDeallocateAnswer(null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresDeallocateAnswer(PostgresConnection.ErrorOf(exception));
        }
    }
}
```

- [ ] **Step 8: Run the tool's tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests`, then `dotnet test --project tests/SqlSource.Tests`
Expected: PASS.  `CallRecordTests` now holds the four new records.

- [ ] **Step 9: Write the container tests**

In `tests/SqlSource.Tests.Postgres/Describer/ParameterTests.cs`, add `using System;` and `using System.Threading;`, and in the class:

```csharp
    private async Task<DescribeFailure> ShouldFailAsync(DescribeRequest request, string id)
    {
        var described = await DescribeAsync(request);

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe(id);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        return failure;
    }

    // The server checks a declared type against every use of its parameter.  The hint's words differ by version.
    [Fact]
    public async Task Describe_DeclaredTypeThatAUseRejects_IsSqlsrc225WithTheServersLines()
    {
        var failure = await ShouldFailAsync(
            Requests.Of("SELECT 1 FROM users WHERE name = @p", ("p", "int")),
            "SQLSRC225"
        );

        failure.Arguments.ShouldBe([Requests.Query, "operator does not exist: character varying = integer"]);
        failure.ServerLines.Count.ShouldBe(3);
        failure.ServerLines[0].ShouldBe("42883: operator does not exist: character varying = integer");
        failure.ServerLines[1].ShouldStartWith("hint: ");
        failure.ServerLines[2].ShouldBe("at character 32");
        failure.Help.ShouldBeNull();
    }

    [Fact]
    public async Task Describe_ParameterThatNoUseTypes_IsSqlsrc226()
    {
        var failure = await ShouldFailAsync(Requests.Of("SELECT @p IS NULL"), "SQLSRC226");

        failure.Arguments.ShouldBe(["@p", Requests.Query]);
        failure.ServerLines.ShouldBe(["42P18: could not determine data type of parameter $1"]);
    }

    [Fact]
    public async Task Describe_FirstUseThatDoesNotFixTheType_IsSqlsrc227AtTheUse()
    {
        var failure = await ShouldFailAsync(
            Requests.Of("SELECT 1 FROM users WHERE (@name IS NULL OR name = @name)"),
            "SQLSRC227"
        );

        failure.Arguments.ShouldBe(["@name", Requests.Query]);
        failure.ServerLines.ShouldBe(["42P08: could not determine data type of parameter $1", "at character 28"]);
    }

    [Theory]
    [InlineData("SELECT 1 FROM users WHERE (name = @name OR @name IS NULL)", null)]
    [InlineData("SELECT 1 FROM users WHERE (@name IS NULL OR name = @name)", "varchar")]
    public async Task Describe_OptionalFilterWithTheTypingUseFirstOrADeclaration_IsDescribed(string sql, string? type)
    {
        var request = type is null ? Requests.Of(sql) : Requests.Of(sql, ("name", type));

        (await DescribeAsync(request)).Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task Describe_DeclaredTypeTheDatabaseLacks_IsSqlsrc228NamingItAndItsParameter()
    {
        var failure = await ShouldFailAsync(
            Requests.Of("SELECT 1 FROM users WHERE id = @id", ("p", "nope_t")),
            "SQLSRC228"
        );

        failure.Arguments.ShouldBe(["nope_t", "@p", Requests.Query, PostgresErrors.NoSuchType]);
        failure.ServerLines.ShouldBe(["42704: type \"nope_t\" does not exist"]);
    }

    // The position is in the user's SQL: the PREPARE around it is not counted.
    [Theory]
    [InlineData("SELEC 1 WHERE @p = 1", "42601: syntax error at or near \"SELEC\"", "at character 1")]
    [InlineData("SELECT 1 FROM nope WHERE id = @id", "42P01: relation \"nope\" does not exist", "at character 15")]
    [InlineData("SELECT 'hé\U0001F600' FROM nope WHERE id = @id", "42P01: relation \"nope\" does not exist", "at character 19")]
    public async Task Describe_QueryTheServerRejects_IsSqlsrc225AtTheCharacterOfTheUsersSql(
        string sql,
        string message,
        string position
    )
    {
        var failure = await ShouldFailAsync(Requests.Of(sql), "SQLSRC225");

        failure.ServerLines.ShouldBe([message, position]);
    }

    // A rejected PREPARE leaves no statement, and the session goes on with the next name.
    [Fact]
    public async Task Describe_AfterAQueryTheServerRejected_DescribesTheNextOne()
    {
        var capture = new DescribeCapture("postgres");
        var opened = await Sessions.OpenAsync(
            postgres.ConnectionString,
            new CapturingExchange(LiveExchange.Instance, capture)
        );
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        var token = TestContext.Current.CancellationToken;

        var first = await session.DescribeAsync(Requests.Of("DELETE FROM nope WHERE id = @id"), token);
        var second = await session.DescribeAsync(Requests.Of("DELETE FROM users WHERE id = @id"), token);

        first.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC225");
        second.Value.ShouldNotBeNull();
        MethodsOf(capture).Where(method => method is "prepare" or "deallocate").ShouldBe(["prepare", "prepare", "deallocate"]);
        CaptureJson.Write(capture).ShouldContain("\"statement\": \"sqlsource_2\"");
        (await Sessions.PreparedStatementsAsync(session)).ShouldBe(0);
    }

    // A run that is cancelled ends as cancelled, and never as a failure that quotes the driver.
    [Fact]
    public async Task Describe_CancelledToken_ThrowsAndIsNoFailure()
    {
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);
        await using var session = opened.Value.ShouldNotBeNull();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            session.DescribeAsync(Requests.Of("DELETE FROM users WHERE id = @id"), cancelled.Token)
        );
    }

    [Fact]
    public async Task Describe_StatementWithATrailingSemicolonAndNoParameter_IsDescribed() =>
        (await DescribeAsync(Requests.Of("DELETE FROM samples WHERE flag; -- all of them"))).Value.ShouldNotBeNull();
```

`MethodsOf` filtered: until Task 6 the list has no `types`, and the test must still hold after it.

- [ ] **Step 10: Run the container tests on both servers**

Run: `dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: PASS, 21 tests.

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres --no-build`
Expected: PASS, 21 tests.

- [ ] **Step 11: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`
Expected: every step `passed`.

```bash
git add src/SqlSource.Tool/Describing/Postgres src/SqlSource/Diagnostics/ToolDiagnostics.cs src/SqlSource/AnalyzerReleases.Unshipped.md docs/diagnostics.md tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs tests/SqlSource.Tool.Tests/Describing/Postgres tests/SqlSource.Tests.Postgres/Describer
```

```bash
git commit -m "Prepare a query on the server, free it, and report what the server refuses

One prepared statement for each query, named by a counter and freed in a
finally, and SQLSRC225 to SQLSRC228 from the server's SQLSTATE and the
place of its error.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `types`, and the parameters typed

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/PostgresFacets.cs`
- Create: `src/SqlSource.Tool/Describing/Postgres/PostgresTypeNames.cs`
- Create: `src/SqlSource.Tool/Describing/Postgres/PostgresTypes.cs`
- Create: `tests/SqlSource.Tool.Tests/PostgresRows.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresTypeNamesTests.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresTypesTests.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`, `PostgresErrors.cs`, `PostgresSession.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs`, `PostgresSessionTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/ParameterTests.cs`

**Interfaces:**
- Consumes: `PostgresType`, `SidecarValues.Kind`, `SidecarValues.TypeSource` of `SqlSource.Snapshot`; `TestSidecars.Postgres(...)`; `PostgresErrors.Rejected`, `DeclaredType`, `Written`; the session of Task 5.
- Produces, in `SqlSource.Tool.Describing.Postgres`:
  - `sealed record PostgresTypesRequest(IReadOnlyList<long> Oids)`
  - `sealed record PostgresTypesAnswer(IReadOnlyList<PostgresTypeRow>? Rows, PostgresServerError? Error)`
  - `sealed record PostgresTypeRow(long Oid, string Typname, string Nspname, string Typtype, string Typcategory, long Typelem, long Typbasetype, long? Rngsubtype, long? MultirangeRngsubtype, string FormatType, IReadOnlyList<string>? EnumLabels)`
  - `sealed record PostgresFacets(int? Length, int? Precision, int? Scale)` with `static PostgresFacets None`
  - `static class PostgresTypeNames` with `string Spell(PostgresTypeRow row, PostgresFacets facets)`, `string ArrayOf(string element)`, `string Identifier(string name)`
  - `static class PostgresTypes` with `PostgresType Build(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows, PostgresFacets facets)`, `bool IsPseudo(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows)`
  - `PostgresErrors.Types(PostgresServerError error, DescribeRequest query)`, `PostgresErrors.PseudoType(DescribeRequest query, SqlQueryParameter parameter, string typeName)`, and the constants `PseudoTypeReason`, `PseudoTypeHelp`
  - `PostgresSession.DescribeAsync` answers every parameter of the query's list with its `PostgresType` and `declared` or `inferred`.
- Produces, in the tool's tests: `PostgresRows` with the rows `Int4`, `Text`, `Varchar`, `Numeric`, `Timestamptz`, `TextArray`, `EmailT`, `UserStatus`, `Tstzrange`, `Int4range`, `Int4multirange`, `Record`, the makers `BaseOf`, `ArrayOf`, `DomainOf`, `EnumOf`, `RangeOf`, `MultirangeOf`, `PseudoOf`, and `Of(params PostgresTypeRow[])`.

- [ ] **Step 1: Add the records of `types`**

At the end of `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`:

```csharp
/// <summary>The request of <c>types</c>.</summary>
/// <param name="Oids">The types to resolve, sorted and each once.</param>
internal sealed record PostgresTypesRequest(IReadOnlyList<long> Oids);

/// <summary>The rows of the closure, or why the catalog could not be read.</summary>
/// <param name="Rows">
/// One row for each type asked for and for every type they reach: the element of an array, the base of a domain,
/// the subtype of a range, the range of a multirange.  In <c>oid</c> order.
/// </param>
/// <param name="Error">What the server said, or null.</param>
internal sealed record PostgresTypesAnswer(IReadOnlyList<PostgresTypeRow>? Rows, PostgresServerError? Error);

/// <summary>What the catalog says of one type.  Each member is named as the catalog names it.</summary>
/// <param name="Oid">The type.</param>
/// <param name="Typname">Its name in <c>pg_type</c>: <c>int4</c>, <c>_text</c>.</param>
/// <param name="Nspname">Its schema.</param>
/// <param name="Typtype"><c>b</c>, <c>c</c>, <c>d</c>, <c>e</c>, <c>m</c>, <c>p</c> or <c>r</c>.</param>
/// <param name="Typcategory">The category: <c>A</c> for an array.</param>
/// <param name="Typelem">The element of an array, or zero.</param>
/// <param name="Typbasetype">The base of a domain, or zero.</param>
/// <param name="Rngsubtype">The subtype of a range, or null for a type that is none.</param>
/// <param name="MultirangeRngsubtype">The subtype of a multirange's range, or null for a type that is none.</param>
/// <param name="FormatType"><c>format_type(oid, -1)</c>: the SQL spelling without a modifier.</param>
/// <param name="EnumLabels">The labels of an enum in their sort order, or null for a type that is none.</param>
internal sealed record PostgresTypeRow(
    long Oid,
    string Typname,
    string Nspname,
    string Typtype,
    string Typcategory,
    long Typelem,
    long Typbasetype,
    long? Rngsubtype,
    long? MultirangeRngsubtype,
    string FormatType,
    IReadOnlyList<string>? EnumLabels
);
```

- [ ] **Step 2: Write the rows a test makes**

`tests/SqlSource.Tool.Tests/PostgresRows.cs`.  The values are the ones the spike read from PostgreSQL 18 with the fixture schema.

```csharp
using System.Collections.Generic;
using System.Linq;
using SqlSource.Tool.Describing.Postgres;

namespace SqlSource.Tool.Tests;

// Rows of "types" as a server answers them, for a test that has no server.  A row is declared after every row it
// is made from.
internal static class PostgresRows
{
    public static PostgresTypeRow Int4 { get; } = BaseOf(23, "int4", "integer", "N");

    public static PostgresTypeRow Text { get; } = BaseOf(25, "text", "text", "S");

    public static PostgresTypeRow Varchar { get; } = BaseOf(1043, "varchar", "character varying", "S");

    public static PostgresTypeRow Numeric { get; } = BaseOf(1700, "numeric", "numeric", "N");

    public static PostgresTypeRow Timestamptz { get; } = BaseOf(1184, "timestamptz", "timestamp with time zone", "D");

    public static PostgresTypeRow TextArray { get; } = ArrayOf(1009, "_text", Text);

    public static PostgresTypeRow EmailT { get; } = DomainOf(16387, "email_t", Varchar);

    public static PostgresTypeRow UserStatus { get; } = EnumOf(16390, "user_status", "active", "disabled");

    public static PostgresTypeRow Tstzrange { get; } = RangeOf(3910, "tstzrange", Timestamptz);

    public static PostgresTypeRow Int4range { get; } = RangeOf(3904, "int4range", Int4);

    public static PostgresTypeRow Int4multirange { get; } = MultirangeOf(4451, "int4multirange", Int4range);

    public static PostgresTypeRow Record { get; } = PseudoOf(2249, "record");

    public static PostgresTypeRow BaseOf(
        long oid,
        string typname,
        string formatType,
        string category,
        string schema = "pg_catalog"
    ) => new(oid, typname, schema, "b", category, 0, 0, null, null, formatType, null);

    public static PostgresTypeRow ArrayOf(long oid, string typname, PostgresTypeRow element) =>
        new(oid, typname, element.Nspname, "b", "A", element.Oid, 0, null, null, element.FormatType + "[]", null);

    // A domain has the category of its base.
    public static PostgresTypeRow DomainOf(long oid, string typname, PostgresTypeRow baseType, string schema = "public") =>
        new(oid, typname, schema, "d", baseType.Typcategory, 0, baseType.Oid, null, null, typname, null);

    public static PostgresTypeRow EnumOf(long oid, string typname, params string[] labels) =>
        new(oid, typname, "public", "e", "E", 0, 0, null, null, typname, labels);

    public static PostgresTypeRow RangeOf(long oid, string typname, PostgresTypeRow subtype) =>
        new(oid, typname, "pg_catalog", "r", "R", 0, 0, subtype.Oid, null, typname, null);

    public static PostgresTypeRow MultirangeOf(long oid, string typname, PostgresTypeRow range) =>
        new(oid, typname, "pg_catalog", "m", "R", 0, 0, null, range.Rngsubtype, typname, null);

    public static PostgresTypeRow PseudoOf(long oid, string typname) =>
        new(oid, typname, "pg_catalog", "p", "P", 0, 0, null, null, typname, null);

    public static IReadOnlyDictionary<long, PostgresTypeRow> Of(params PostgresTypeRow[] rows) =>
        rows.ToDictionary(static row => row.Oid);
}
```

- [ ] **Step 3: Write the failing tests of the names**

`tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresTypeNamesTests.cs`:

```csharp
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// A type's name as the sidecar records it: as PostgreSQL spells a type of its catalog, with its facets, and the
// schema and the name as identifiers for any other.
public class PostgresTypeNamesTests
{
    // format_type(oid, -1) of char is "bpchar", and of a char with a length "character(n)".
    [Theory]
    [InlineData("int4", "integer", null, null, null, "integer")]
    [InlineData("varchar", "character varying", null, null, null, "character varying")]
    [InlineData("varchar", "character varying", 100, null, null, "character varying(100)")]
    [InlineData("bpchar", "bpchar", null, null, null, "bpchar")]
    [InlineData("bpchar", "bpchar", 2, null, null, "character(2)")]
    [InlineData("bit", "bit", 8, null, null, "bit(8)")]
    [InlineData("varbit", "bit varying", 5, null, null, "bit varying(5)")]
    [InlineData("numeric", "numeric", null, null, null, "numeric")]
    [InlineData("numeric", "numeric", null, 18, 2, "numeric(18,2)")]
    [InlineData("numeric", "numeric", null, 2, -3, "numeric(2,-3)")]
    [InlineData("numeric", "numeric", null, 10, null, "numeric(10)")]
    [InlineData("timestamp", "timestamp without time zone", null, 3, null, "timestamp(3) without time zone")]
    [InlineData("timestamptz", "timestamp with time zone", null, 3, null, "timestamp(3) with time zone")]
    [InlineData("time", "time without time zone", null, 0, null, "time(0) without time zone")]
    [InlineData("timetz", "time with time zone", null, 6, null, "time(6) with time zone")]
    [InlineData("interval", "interval", null, 6, null, "interval(6)")]
    [InlineData("text", "text", 5, 5, 5, "text")]
    [InlineData("numrange", "numrange", null, null, null, "numrange")]
    public void Spell_TypeOfTheCatalog_IsItsSqlSpellingWithTheFacetsItCarries(
        string typname,
        string formatType,
        int? length,
        int? precision,
        int? scale,
        string expected
    ) =>
        PostgresTypeNames
            .Spell(PostgresRows.BaseOf(1, typname, formatType, "X"), new PostgresFacets(length, precision, scale))
            .ShouldBe(expected);

    // Whatever the session's search_path: the name does not depend on the session.  A facet is not written, since
    // the tool knows how to spell only the modifier of a type of the catalog.
    [Theory]
    [InlineData("public", "user_status", "public.user_status")]
    [InlineData("billing", "money_2", "billing.money_2")]
    [InlineData("My Schema", "Status", "\"My Schema\".\"Status\"")]
    [InlineData("we\"ird", "t\"\"", "\"we\"\"ird\".\"t\"\"\"\"\"")]
    [InlineData("public", "1st", "public.\"1st\"")]
    [InlineData("public", "café", "public.\"café\"")]
    public void Spell_TypeOfAnotherSchema_IsItsSchemaAndNameAsIdentifiers(string schema, string typname, string expected) =>
        PostgresTypeNames
            .Spell(PostgresRows.BaseOf(1, typname, typname, "U", schema), new PostgresFacets(9, 9, 9))
            .ShouldBe(expected);

    [Theory]
    [InlineData("users", "users")]
    [InlineData("_t1", "_t1")]
    [InlineData("Users", "\"Users\"")]
    [InlineData("user name", "\"user name\"")]
    [InlineData("", "\"\"")]
    public void Identifier_Name_IsBareOnlyWhenItIsLowerCaseLettersDigitsAndUnderscores(string name, string expected) =>
        PostgresTypeNames.Identifier(name).ShouldBe(expected);

    [Fact]
    public void ArrayOf_ElementName_IsTheNameAndTheMark() =>
        PostgresTypeNames.ArrayOf("public.email_t").ShouldBe("public.email_t[]");
}
```

`tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresTypesTests.cs`:

```csharp
using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tests.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The sidecar's record of a type, from the rows of the closure.
public class PostgresTypesTests
{
    private static readonly PostgresType Integer = TestSidecars.Postgres();

    private static readonly PostgresType CharacterVarying = TestSidecars.Postgres(
        "character varying",
        internalName: "varchar"
    );

    private static readonly PostgresType EmailT = TestSidecars.Postgres(
        "public.email_t",
        SidecarValues.Kind.Domain,
        "public",
        "email_t",
        baseType: CharacterVarying
    );

    [Fact]
    public void Build_BaseType_HasItsNameSchemaAndInternalName() =>
        PostgresTypes.Build(23, PostgresRows.Of(PostgresRows.Int4), PostgresFacets.None).ShouldBe(Integer);

    [Fact]
    public void Build_BaseTypeWithFacets_CarriesThemAndSpellsThem() =>
        PostgresTypes
            .Build(1700, PostgresRows.Of(PostgresRows.Numeric), new PostgresFacets(null, 18, 2))
            .ShouldBe(TestSidecars.Postgres("numeric(18,2)", internalName: "numeric", precision: 18, scale: 2));

    // An element is built without facets, and the array has none.
    [Fact]
    public void Build_Array_HasItsElementAndTheElementsNameWithTheMark() =>
        PostgresTypes
            .Build(1009, PostgresRows.Of(PostgresRows.Text, PostgresRows.TextArray), new PostgresFacets(5, null, null))
            .ShouldBe(
                TestSidecars.Postgres(
                    "text[]",
                    SidecarValues.Kind.Array,
                    internalName: "_text",
                    element: TestSidecars.Postgres("text", internalName: "text")
                )
            );

    [Fact]
    public void Build_Domain_HasItsBase() =>
        PostgresTypes
            .Build(16387, PostgresRows.Of(PostgresRows.Varchar, PostgresRows.EmailT), PostgresFacets.None)
            .ShouldBe(EmailT);

    // The protocol applies a domain's modifier to its base, so the facets land there.
    [Fact]
    public void Build_DomainWithFacets_PutsThemOnItsBase() =>
        PostgresTypes
            .Build(16387, PostgresRows.Of(PostgresRows.Varchar, PostgresRows.EmailT), new PostgresFacets(254, null, null))
            .ShouldBe(
                EmailT with
                {
                    Base = TestSidecars.Postgres("character varying(254)", internalName: "varchar", length: 254),
                }
            );

    [Fact]
    public void Build_DomainOverADomain_HasTheChainOfBases()
    {
        var work = PostgresRows.DomainOf(16400, "work_email", PostgresRows.EmailT, "billing");

        PostgresTypes
            .Build(16400, PostgresRows.Of(PostgresRows.Varchar, PostgresRows.EmailT, work), PostgresFacets.None)
            .ShouldBe(
                TestSidecars.Postgres(
                    "billing.work_email",
                    SidecarValues.Kind.Domain,
                    "billing",
                    "work_email",
                    baseType: EmailT
                )
            );
    }

    [Fact]
    public void Build_ArrayOfADomain_IsNamedByTheDomain()
    {
        var emails = PostgresRows.ArrayOf(16386, "_email_t", PostgresRows.EmailT);

        var built = PostgresTypes.Build(
            16386,
            PostgresRows.Of(PostgresRows.Varchar, PostgresRows.EmailT, emails),
            PostgresFacets.None
        );

        built.Name.ShouldBe("public.email_t[]");
        built.Kind.ShouldBe(SidecarValues.Kind.Array);
        built.Schema.ShouldBe("public");
        built.Element.ShouldBe(EmailT);
    }

    [Fact]
    public void Build_Enum_HasItsLabelsInOrder() =>
        PostgresTypes
            .Build(16390, PostgresRows.Of(PostgresRows.UserStatus), PostgresFacets.None)
            .ShouldBe(
                TestSidecars.Postgres(
                    "public.user_status",
                    SidecarValues.Kind.Enum,
                    "public",
                    "user_status",
                    labels: ["active", "disabled"]
                )
            );

    [Fact]
    public void Build_Range_HasItsSubtype() =>
        PostgresTypes
            .Build(3910, PostgresRows.Of(PostgresRows.Timestamptz, PostgresRows.Tstzrange), PostgresFacets.None)
            .ShouldBe(
                TestSidecars.Postgres(
                    "tstzrange",
                    SidecarValues.Kind.Range,
                    internalName: "tstzrange",
                    subtype: TestSidecars.Postgres("timestamp with time zone", internalName: "timestamptz")
                )
            );

    // The subtype of a multirange is its range's.
    [Fact]
    public void Build_Multirange_HasTheSubtypeOfItsRange() =>
        PostgresTypes
            .Build(
                4451,
                PostgresRows.Of(PostgresRows.Int4, PostgresRows.Int4range, PostgresRows.Int4multirange),
                PostgresFacets.None
            )
            .ShouldBe(
                TestSidecars.Postgres(
                    "int4multirange",
                    SidecarValues.Kind.Multirange,
                    internalName: "int4multirange",
                    subtype: Integer
                )
            );

    [Fact]
    public void Build_Composite_HasItsKindAndNothingMore()
    {
        var address = new PostgresTypeRow(16500, "address", "public", "c", "C", 0, 0, null, null, "address", null);

        PostgresTypes
            .Build(16500, PostgresRows.Of(address), PostgresFacets.None)
            .ShouldBe(TestSidecars.Postgres("public.address", SidecarValues.Kind.Composite, "public", "address"));
    }

    [Fact]
    public void IsPseudo_Type_IsWhetherItsTyptypeIsP()
    {
        var rows = PostgresRows.Of(PostgresRows.Record, PostgresRows.Int4);

        PostgresTypes.IsPseudo(2249, rows).ShouldBeTrue();
        PostgresTypes.IsPseudo(23, rows).ShouldBeFalse();
    }

    // The session asks IsPseudo first, so a pseudo-type here is a bug.
    [Fact]
    public void Build_PseudoType_Throws() =>
        Should
            .Throw<InvalidOperationException>(() =>
                PostgresTypes.Build(2249, PostgresRows.Of(PostgresRows.Record), PostgresFacets.None)
            )
            .Message.ShouldContain("record");

    [Fact]
    public void Build_OidTheRowsLack_ThrowsNamingIt()
    {
        Should
            .Throw<InvalidOperationException>(() =>
                PostgresTypes.Build(99, PostgresRows.Of(PostgresRows.Int4), PostgresFacets.None)
            )
            .Message.ShouldContain("99");
        // The element of an array the closure should have reached.
        Should
            .Throw<InvalidOperationException>(() =>
                PostgresTypes.Build(1009, PostgresRows.Of(PostgresRows.TextArray), PostgresFacets.None)
            )
            .Message.ShouldContain("25");
    }
}
```

- [ ] **Step 4: Run them to see them fail**

Run: `dotnet build tests/SqlSource.Tool.Tests`
Expected: FAIL, `PostgresFacets`, `PostgresTypeNames` and `PostgresTypes` do not exist.

- [ ] **Step 5: Write the facets, the names and the types**

`src/SqlSource.Tool/Describing/Postgres/PostgresFacets.cs`:

```csharp
namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// What a type's modifier sets.  A parameter has none: the protocol gives a parameter an OID and no modifier.  A
/// column's come with the columns.
/// </summary>
/// <param name="Length">The length of a <c>varchar</c>, a <c>char</c>, a <c>bit</c> or a <c>varbit</c>.</param>
/// <param name="Precision">The precision of a <c>numeric</c>, a time type or an <c>interval</c>.</param>
/// <param name="Scale">The scale of a <c>numeric</c>.  It may be negative.</param>
internal sealed record PostgresFacets(int? Length, int? Precision, int? Scale)
{
    public static PostgresFacets None { get; } = new(null, null, null);
}
```

`src/SqlSource.Tool/Describing/Postgres/PostgresTypeNames.cs`:

```csharp
using System;
using System.Globalization;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Spells a type's <c>name</c> as the sidecar wants it, and as <c>format_type</c> spells a type with its modifier.
/// </summary>
/// <remarks>
/// A type of <c>pg_catalog</c> is its SQL spelling, <c>character varying(100)</c>.  A type of any other schema is the
/// schema and the name, whatever the session's <c>search_path</c>, so that the value does not depend on the
/// session.  An identifier is quoted unless it is lower-case letters, digits and <c>_</c>: a reserved word is not
/// quoted, which <c>docs/tech-debt/TD-0043</c> records.
/// </remarks>
internal static class PostgresTypeNames
{
    private const string Catalog = "pg_catalog";

    /// <summary>The name of a type that is no array.  Facets that the type does not carry are left out.</summary>
    public static string Spell(PostgresTypeRow row, PostgresFacets facets) =>
        row.Nspname == Catalog ? InCatalog(row, facets) : $"{Identifier(row.Nspname)}.{Identifier(row.Typname)}";

    /// <summary>The name of an array: its element's and <c>[]</c>.</summary>
    public static string ArrayOf(string element) => element + "[]";

    /// <summary>
    /// A name as an SQL identifier: bare when it is lower-case letters, digits and <c>_</c> and does not start with
    /// a digit, else in double quotes with a quote doubled.
    /// </summary>
    public static string Identifier(string name) =>
        IsBare(name) ? name : $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    // format_type(oid, -1) of char is "bpchar": only a char with a length is "character".
    private static string InCatalog(PostgresTypeRow row, PostgresFacets facets) =>
        row.Typname switch
        {
            "bpchar" when facets.Length is { } length => Invariant($"character({length})"),
            "varchar" or "bit" or "varbit" when facets.Length is { } length => Invariant(
                $"{row.FormatType}({length})"
            ),
            "numeric" when facets is { Precision: { } precision, Scale: { } scale } => Invariant(
                $"numeric({precision},{scale})"
            ),
            "numeric" when facets.Precision is { } precision => Invariant($"numeric({precision})"),
            "time" or "timetz" or "timestamp" or "timestamptz" when facets.Precision is { } precision =>
                AfterFirstWord(row.FormatType, precision),
            "interval" when facets.Precision is { } precision => Invariant($"interval({precision})"),
            _ => row.FormatType,
        };

    // "timestamp(3) without time zone": the precision stands after the first word.
    private static string AfterFirstWord(string name, int precision)
    {
        var space = name.IndexOf(' ', StringComparison.Ordinal);
        return space < 0
            ? Invariant($"{name}({precision})")
            : Invariant($"{name[..space]}({precision}){name[space..]}");
    }

    private static bool IsBare(string name)
    {
        if (name.Length == 0 || char.IsAsciiDigit(name[0]))
        {
            return false;
        }

        foreach (var value in name)
        {
            if (!char.IsAsciiLetterLower(value) && !char.IsAsciiDigit(value) && value != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
```

`src/SqlSource.Tool/Describing/Postgres/PostgresTypes.cs`:

```csharp
using System;
using System.Collections.Generic;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The sidecar's record of a type, from the rows that <c>types</c> answered.
/// </summary>
/// <remarks>
/// The rows are the closure of the types asked for: every type reached through the element of an array, the base of
/// a domain, the subtype of a range and the range of a multirange.  A row that is missing for a type that is
/// reached is a bug of the query, and throws.
/// </remarks>
internal static class PostgresTypes
{
    /// <summary>
    /// Whether the type is a pseudo-type, <c>record</c>, <c>cstring</c>, <c>anyelement</c>: one that no value has
    /// and that cannot be described.  The server accepts one as a parameter's type, declared or inferred.
    /// </summary>
    public static bool IsPseudo(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows) =>
        Row(oid, rows).Typtype == "p";

    /// <summary>The record of a type.</summary>
    /// <param name="oid">The type.</param>
    /// <param name="rows">The rows of the closure, by OID.</param>
    /// <param name="facets">
    /// What the type's modifier sets.  They land on a base type, and on the base of a domain, since the protocol
    /// applies a domain's modifier to its base.  The element of an array and a subtype have none.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The rows lack a type that is reached, or the type is a pseudo-type, or of a kind this version does not know.
    /// </exception>
    public static PostgresType Build(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows, PostgresFacets facets)
    {
        var row = Row(oid, rows);
        return row.Typtype switch
        {
            "b" when row.Typcategory == "A" => Array(row, rows),
            "b" => Plain(row, SidecarValues.Kind.Base, facets),
            "d" => Plain(row, SidecarValues.Kind.Domain, PostgresFacets.None) with
            {
                Base = Build(row.Typbasetype, rows, facets),
            },
            "e" => Plain(row, SidecarValues.Kind.Enum, PostgresFacets.None) with
            {
                Labels = new EquatableArray<string>([.. row.EnumLabels ?? throw Lacks(row, "labels")]),
            },
            "r" => Plain(row, SidecarValues.Kind.Range, PostgresFacets.None) with
            {
                Subtype = Build(row.Rngsubtype ?? throw Lacks(row, "subtype"), rows, PostgresFacets.None),
            },
            "m" => Plain(row, SidecarValues.Kind.Multirange, PostgresFacets.None) with
            {
                Subtype = Build(row.MultirangeRngsubtype ?? throw Lacks(row, "subtype"), rows, PostgresFacets.None),
            },
            "c" => Plain(row, SidecarValues.Kind.Composite, PostgresFacets.None),
            _ => throw new InvalidOperationException(
                $"The type '{row.FormatType}' has the typtype '{row.Typtype}', which cannot be described."
            ),
        };
    }

    private static PostgresType Array(PostgresTypeRow row, IReadOnlyDictionary<long, PostgresTypeRow> rows)
    {
        var element = Build(row.Typelem, rows, PostgresFacets.None);
        return new PostgresType(
            PostgresTypeNames.ArrayOf(element.Name),
            SidecarValues.Kind.Array,
            row.Nspname,
            row.Typname,
            null,
            null,
            null,
            element,
            null,
            null,
            null
        );
    }

    private static PostgresType Plain(PostgresTypeRow row, string kind, PostgresFacets facets) =>
        new(
            PostgresTypeNames.Spell(row, facets),
            kind,
            row.Nspname,
            row.Typname,
            facets.Length,
            facets.Precision,
            facets.Scale,
            null,
            null,
            null,
            null
        );

    private static PostgresTypeRow Row(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows) =>
        rows.TryGetValue(oid, out var row)
            ? row
            : throw new InvalidOperationException($"The answer to 'types' holds no row for the type {oid}.");

    private static InvalidOperationException Lacks(PostgresTypeRow row, string what) =>
        new($"The row of the type '{row.FormatType}' holds no {what}.");
}
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresTypesTests"`, then the same for `PostgresTypeNamesTests`
Expected: PASS.

- [ ] **Step 6: Write the failing tests of the errors and the session**

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs`, add to the class:

```csharp
    [Fact]
    public void Types_Error_IsSqlsrc225AtResolveType()
    {
        var failure = PostgresErrors.Types(
            new PostgresServerError(null, "Exception while reading from stream", null, null, null),
            Query()
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["Q", "Exception while reading from stream"]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBe(["Exception while reading from stream"]);
    }

    // The server said nothing: it accepted the type.  So there is no server line, and the help says what it gave.
    [Fact]
    public void PseudoType_InferredParameter_IsSqlsrc226ThatNamesThePseudoType()
    {
        var parameter = Inferred("p");

        var failure = PostgresErrors.PseudoType(Query(parameter), parameter, "record");

        failure.Descriptor.Id.ShouldBe("SQLSRC226");
        failure.Arguments.ShouldBe(["@p", "Q"]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(
            "the server gave it the pseudo-type 'record', which no value has: cast the parameter where it is used, "
                + "or declare its type with -- param: @p <type>"
        );
    }

    [Fact]
    public void PseudoType_DeclaredParameter_IsSqlsrc228ThatSaysItIsAPseudoType()
    {
        var parameter = Declared("p", "anyelement");

        var failure = PostgresErrors.PseudoType(Query(parameter), parameter, "anyelement");

        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["anyelement", "@p", "Q", "it is a pseudo-type"]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.PseudoTypeHelp);
    }
```

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`, add `using System;`, `using SqlSource.Snapshot;` and `using SqlSource.Tests.Snapshot;`, and in the class:

```csharp
    private static PostgresScript Resolves(PostgresScript script, long[] oids, params PostgresTypeRow[] rows) =>
        script.Answer("types", new PostgresTypesRequest(oids), new PostgresTypesAnswer(rows, null));

    [Fact]
    public async Task DescribeAsync_InferredAndDeclaredParameters_AreTypedInTheListsOrder()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(
            script,
            1,
            "SELECT id FROM users WHERE id = $1 AND name = $2",
            ["unknown", "varchar(50)"],
            new PostgresPrepareAnswer([23, 1043], null)
        );
        _ = Resolves(script, [23, 1043], PostgresRows.Int4, PostgresRows.Varchar);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request(
                "SELECT id FROM users WHERE id = @id AND name = @name;",
                Inferred("id"),
                Declared("name", "varchar(50)")
            ),
            TestContext.Current.CancellationToken
        );

        described
            .Value.ShouldNotBeNull()
            .ShouldBe(
                new QueryDescription(
                    SidecarResultKind.None,
                    TestSidecars.Of(
                        new DescribedParameter("id", TestSidecars.Postgres(), SidecarValues.TypeSource.Inferred),
                        new DescribedParameter(
                            "name",
                            TestSidecars.Postgres("character varying", internalName: "varchar"),
                            SidecarValues.TypeSource.Declared
                        )
                    ),
                    null,
                    null,
                    null,
                    null
                )
            );
        exchange.Unused.ShouldBeEmpty();
    }

    // The request of "types" is sorted and holds each OID once, so that it is the same however the query orders
    // its parameters.  A declared parameter that the SQL does not hold is typed as it was declared.
    [Fact]
    public async Task DescribeAsync_ParametersOfOneTypeAndADeclaredOnlyOne_AskForEachOidOnce()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(
            script,
            1,
            "UPDATE users SET name = $1 WHERE id = $2",
            ["unknown", "unknown", "int"],
            new PostgresPrepareAnswer([1043, 23, 23], null)
        );
        _ = Resolves(script, [23, 1043], PostgresRows.Int4, PostgresRows.Varchar);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request(
                "UPDATE users SET name = @name WHERE id = @id",
                Inferred("name"),
                Inferred("id"),
                Declared("tenant", "int")
            ),
            TestContext.Current.CancellationToken
        );

        var parameters = described.Value.ShouldNotBeNull().Parameters;
        parameters.Select(parameter => parameter.Name).ShouldBe(["name", "id", "tenant"]);
        parameters.Select(parameter => parameter.TypeSource).ShouldBe(["inferred", "inferred", "declared"]);
        parameters[2].Type.ShouldBe(TestSidecars.Postgres());
        exchange.Unused.ShouldBeEmpty();
    }

    // A "$2" that the SQL writes itself is typed by the server after the query's own: it is no parameter of the
    // query, and "types" is not asked for it.
    [Fact]
    public async Task DescribeAsync_MoreTypesThanParameters_TypesTheListAlone()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(
            script,
            1,
            "SELECT 1 FROM users WHERE id = $1 AND name = $2",
            ["unknown"],
            new PostgresPrepareAnswer([23, 25], null)
        );
        _ = Resolves(script, [23], PostgresRows.Int4);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT 1 FROM users WHERE id = @id AND name = $2", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldNotBeNull().Parameters.ShouldHaveSingleItem().Name.ShouldBe("id");
        exchange.Unused.ShouldBeEmpty();
    }

    // A bug of the describer or of the server, and the statement is freed all the same.
    [Fact]
    public async Task DescribeAsync_FewerTypesThanParameters_ThrowsAndFreesTheStatement()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "SELECT $1, $2", ["unknown", "unknown"], new PostgresPrepareAnswer([23], null));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        _ = await Should.ThrowAsync<InvalidOperationException>(() =>
            session.DescribeAsync(
                Request("SELECT @a, @b", Inferred("a"), Inferred("b")),
                TestContext.Current.CancellationToken
            )
        );

        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_InferredPseudoType_IsSqlsrc226NamingIt()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "SELECT row_to_json($1)", ["unknown"], new PostgresPrepareAnswer([2249], null));
        _ = Resolves(script, [2249], PostgresRows.Record);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT row_to_json(@p)", Inferred("p")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC226");
        failure.Arguments.ShouldBe(["@p", "Q"]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.Help.ShouldNotBeNull().ShouldContain("'record'");
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_DeclaredPseudoType_IsSqlsrc228()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "SELECT $1", ["record"], new PostgresPrepareAnswer([2249], null));
        _ = Resolves(script, [2249], PostgresRows.Record);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT @p", Declared("p", "record")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["record", "@p", "Q", PostgresErrors.PseudoTypeReason]);
        exchange.Unused.ShouldBeEmpty();
    }

    // A connection that dropped between the two calls.  The statement is still asked to be freed.
    [Fact]
    public async Task DescribeAsync_TypesThatFails_IsSqlsrc225AtResolveType()
    {
        var dropped = new PostgresServerError(null, "Exception while reading from stream", null, null, null);
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "SELECT $1::int", ["unknown"], new PostgresPrepareAnswer([23], null));
        _ = script.Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer(null, dropped));
        var exchange = Frees(script, 1, new PostgresDeallocateAnswer(dropped)).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT @p::int", Inferred("p")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        exchange.Unused.ShouldBeEmpty();
    }
```

Add `using System.Linq;` to the file as well.

- [ ] **Step 7: Run them to see them fail**

Run: `dotnet build tests/SqlSource.Tool.Tests`
Expected: FAIL, `PostgresErrors.Types` and `PostgresErrors.PseudoType` do not exist.

- [ ] **Step 8: Write the two failures**

In `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`, in the class:

```csharp
    public const string PseudoTypeReason = "it is a pseudo-type";

    public const string PseudoTypeHelp =
        "declare a type that a value can have: a pseudo-type such as record or anyelement stands for no value";

    /// <summary><c>SQLSRC225</c> at the step <c>resolve type</c>: the catalog could not be read.</summary>
    public static DescribeFailure Types(PostgresServerError error, DescribeRequest query) =>
        Rejected(error, query, DescribeStep.ResolveType, ServerLines(error, 0));

    /// <summary>
    /// A parameter whose type is a pseudo-type.  The server accepts one and says nothing, so there is no server
    /// line.  A declared one is <c>SQLSRC228</c>; an inferred one is <c>SQLSRC226</c>, whose help says what the
    /// server gave.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="typeName">The pseudo-type, as <c>format_type</c> spells it.</param>
    public static DescribeFailure PseudoType(DescribeRequest query, SqlQueryParameter parameter, string typeName)
    {
        if (parameter.Type is not null)
        {
            return DeclaredType(
                query,
                parameter,
                PseudoTypeReason,
                DescribeStep.ResolveType,
                EquatableArray<string>.Empty,
                PseudoTypeHelp
            );
        }

        var written = Written(parameter);
        return new DescribeFailure(
            ToolDiagnostics.ParameterTypeNotDetermined,
            new EquatableArray<string>([written, query.Name]),
            DescribeStep.ResolveType,
            EquatableArray<string>.Empty,
            $"the server gave it the pseudo-type '{typeName}', which no value has: cast the parameter where it is "
                + $"used, or declare its type with -- param: {written} <type>"
        );
    }
```

- [ ] **Step 9: Type the parameters in the session**

In `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`:

Add `using System.Collections.Generic;` and `using NpgsqlTypes;`.

Add the constant after `ParameterTypesSql`.  The spike ran this query on PostgreSQL 14 and 18.

```csharp
    // The closure of the types asked for, in one query: a recursive term that follows the element of an array, the
    // base of a domain, the subtype of a range and the range of a multirange.  A domain has the category of its
    // base, so an array is a base type of the category A.
    private const string TypesSql = """
        WITH RECURSIVE closure (oid) AS (
            SELECT t.oid FROM pg_catalog.pg_type AS t WHERE t.oid = ANY (@oids)
            UNION
            SELECT link.oid
            FROM closure AS c
            JOIN pg_catalog.pg_type AS t ON t.oid = c.oid
            LEFT JOIN pg_catalog.pg_range AS r ON r.rngtypid = t.oid
            LEFT JOIN pg_catalog.pg_range AS m ON m.rngmultitypid = t.oid
            CROSS JOIN LATERAL (
                VALUES
                    (CASE WHEN t.typtype = 'b' AND t.typcategory = 'A' THEN t.typelem END),
                    (CASE WHEN t.typtype = 'd' THEN t.typbasetype END),
                    (r.rngsubtype),
                    (m.rngtypid)
            ) AS link (oid)
            WHERE link.oid IS NOT NULL AND link.oid <> 0
        )
        SELECT
            t.oid,
            t.typname::text,
            n.nspname::text,
            t.typtype::text,
            t.typcategory::text,
            t.typelem,
            t.typbasetype,
            r.rngsubtype,
            mr.rngsubtype,
            pg_catalog.format_type(t.oid, -1),
            (
                SELECT pg_catalog.array_agg(e.enumlabel::text ORDER BY e.enumsortorder)
                FROM pg_catalog.pg_enum AS e
                WHERE e.enumtypid = t.oid
            )
        FROM closure AS c
        JOIN pg_catalog.pg_type AS t ON t.oid = c.oid
        JOIN pg_catalog.pg_namespace AS n ON n.oid = t.typnamespace
        LEFT JOIN pg_catalog.pg_range AS r ON r.rngtypid = t.oid
        LEFT JOIN pg_catalog.pg_range AS m ON m.rngmultitypid = t.oid
        LEFT JOIN pg_catalog.pg_range AS mr ON mr.rngtypid = m.rngtypid
        ORDER BY t.oid
        """;
```

In `DescribeAsync`, the body of the `try` becomes:

```csharp
            return await TypeParametersAsync(request, prepared, cancellationToken);
```

Add to the class, after `DescribeAsync`:

```csharp
    // Each parameter of the query's list gets the type the server resolved for it.
    private async Task<Outcome<QueryDescription, DescribeFailure>> TypeParametersAsync(
        DescribeRequest request,
        PostgresPrepareAnswer prepared,
        CancellationToken cancellationToken
    )
    {
        var count = request.Parameters.Count;
        var resolved =
            prepared.ParameterTypes
            ?? throw new InvalidOperationException("The answer to 'prepare' holds no types and no error.");
        if (resolved.Count < count)
        {
            throw new InvalidOperationException(
                $"The server resolved {resolved.Count} types for the {count} parameters of '{request.Name}'."
            );
        }

        if (count == 0)
        {
            return Described([]);
        }

        // The server lists a type for a "$n" that the SQL writes itself, after the query's own.  It is no parameter
        // of the query: the list's are the first.
        var oids = resolved.Take(count).ToList();
        var answer = await _exchange.AskAsync(
            "types",
            new PostgresTypesRequest([.. oids.Distinct().Order()]),
            TypesLiveAsync,
            cancellationToken
        );
        if (answer.Error is { } error)
        {
            return Failed(PostgresErrors.Types(error, request));
        }

        var rows = (
            answer.Rows ?? throw new InvalidOperationException("The answer to 'types' holds no rows and no error.")
        ).ToDictionary(static row => row.Oid);
        var parameters = new List<DescribedParameter>(count);
        for (var index = 0; index < count; index++)
        {
            var parameter = request.Parameters[index];
            if (PostgresTypes.IsPseudo(oids[index], rows))
            {
                // The server accepts a pseudo-type, declared or inferred, and no value has one.
                return Failed(PostgresErrors.PseudoType(request, parameter, rows[oids[index]].FormatType));
            }

            // A parameter has no facets: the protocol gives it an OID and no modifier.
            parameters.Add(
                new DescribedParameter(
                    parameter.Name,
                    PostgresTypes.Build(oids[index], rows, PostgresFacets.None),
                    parameter.Type is null ? SidecarValues.TypeSource.Inferred : SidecarValues.TypeSource.Declared
                )
            );
        }

        return Described(parameters);
    }

    // The parameters, no rows and nothing else: the columns come with the next sub-phase.
    private static Outcome<QueryDescription, DescribeFailure> Described(List<DescribedParameter> parameters) =>
        Outcome<QueryDescription, DescribeFailure>.Of(
            new QueryDescription(
                SidecarResultKind.None,
                new EquatableArray<DescribedParameter>([.. parameters]),
                null,
                null,
                null,
                null
            )
        );
```

and, after `PrepareLiveAsync`:

```csharp
    private async Task<PostgresTypesAnswer> TypesLiveAsync(PostgresTypesRequest asked, CancellationToken token)
    {
        try
        {
            await using var command = await Connection.CommandAsync(TypesSql, token);
            _ = command.Parameters.Add(
                new NpgsqlParameter("oids", NpgsqlDbType.Array | NpgsqlDbType.Oid)
                {
                    Value = asked.Oids.Select(static oid => (uint)oid).ToArray(),
                }
            );
            await using var reader = await command.ExecuteReaderAsync(token);
            var rows = new List<PostgresTypeRow>();
            while (await reader.ReadAsync(token))
            {
                rows.Add(
                    new PostgresTypeRow(
                        reader.GetFieldValue<uint>(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        reader.GetFieldValue<uint>(5),
                        reader.GetFieldValue<uint>(6),
                        await reader.IsDBNullAsync(7, token) ? null : reader.GetFieldValue<uint>(7),
                        await reader.IsDBNullAsync(8, token) ? null : reader.GetFieldValue<uint>(8),
                        reader.GetString(9),
                        await reader.IsDBNullAsync(10, token) ? null : reader.GetFieldValue<string[]>(10)
                    )
                );
            }

            return new PostgresTypesAnswer(rows, null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresTypesAnswer(null, PostgresConnection.ErrorOf(exception));
        }
    }
```

- [ ] **Step 10: Run the tool's tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS.  The tests of Task 5 whose queries have no parameter still pass: `types` is not asked for them.

- [ ] **Step 11: Write the container tests**

In `tests/SqlSource.Tests.Postgres/Describer/ParameterTests.cs`, add `using SqlSource.Snapshot;` and `using SqlSource.Tests.Snapshot;`, and in the class.  The expected types are what the spike saw the server infer.

```csharp
    private static readonly PostgresType CharacterVarying = TestSidecars.Postgres(
        "character varying",
        internalName: "varchar"
    );

    private static readonly PostgresType EmailT = TestSidecars.Postgres(
        "public.email_t",
        SidecarValues.Kind.Domain,
        "public",
        "email_t",
        baseType: CharacterVarying
    );

    private static readonly PostgresType UserStatus = TestSidecars.Postgres(
        "public.user_status",
        SidecarValues.Kind.Enum,
        "public",
        "user_status",
        labels: ["active", "disabled"]
    );

    private static readonly PostgresType TextArray = TestSidecars.Postgres(
        "text[]",
        SidecarValues.Kind.Array,
        internalName: "_text",
        element: TestSidecars.Postgres("text", internalName: "text")
    );

    private async Task<EquatableArray<DescribedParameter>> ShouldDescribeAsync(DescribeRequest request)
    {
        var described = await DescribeAsync(request);

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.None);
        description.Columns.ShouldBeNull();
        return description.Parameters;
    }

    private static DescribedParameter Inferred(string name, PostgresType type) =>
        new(name, type, SidecarValues.TypeSource.Inferred);

    private static DescribedParameter Declared(string name, PostgresType type) =>
        new(name, type, SidecarValues.TypeSource.Declared);

    // The goal of the sub-phase.
    [Fact]
    public async Task Describe_ParametersComparedWithColumns_AreInferredFromThem()
    {
        var parameters = await ShouldDescribeAsync(
            Requests.Of("SELECT id FROM users WHERE id = @id AND status = @status")
        );

        parameters.ShouldBe([Inferred("id", TestSidecars.Postgres()), Inferred("status", UserStatus)]);
    }

    // An assignment gives a parameter the column's own type, so every base type of the schema is reached through
    // one.  A parameter has no facets: numeric(18,2) is numeric and char(2) is bpchar.
    [Theory]
    [InlineData("samples", "id", "integer", "int4")]
    [InlineData("samples", "flag", "boolean", "bool")]
    [InlineData("samples", "small", "smallint", "int2")]
    [InlineData("samples", "big", "bigint", "int8")]
    [InlineData("samples", "ratio", "real", "float4")]
    [InlineData("samples", "amount", "double precision", "float8")]
    [InlineData("samples", "code", "bpchar", "bpchar")]
    [InlineData("samples", "label", "text", "text")]
    [InlineData("samples", "born", "date", "date")]
    [InlineData("samples", "at_time", "time without time zone", "time")]
    [InlineData("samples", "at_local", "timestamp without time zone", "timestamp")]
    [InlineData("samples", "span", "interval", "interval")]
    [InlineData("samples", "token", "uuid", "uuid")]
    [InlineData("samples", "payload", "bytea", "bytea")]
    [InlineData("samples", "document", "jsonb", "jsonb")]
    [InlineData("users", "name", "character varying", "varchar")]
    [InlineData("users", "balance", "numeric", "numeric")]
    [InlineData("users", "created_at", "timestamp with time zone", "timestamptz")]
    public async Task Describe_BaseTypeInferredFromAnAssignment_IsSpelledAsTheServerSpellsIt(
        string table,
        string column,
        string name,
        string internalName
    )
    {
        var parameters = await ShouldDescribeAsync(Requests.Of($"UPDATE {table} SET {column} = @p"));

        parameters.ShouldBe([Inferred("p", TestSidecars.Postgres(name, internalName: internalName))]);
    }

    // A parameter keeps its domain; a column, in the next sub-phase, is flattened to its base by the protocol.
    [Fact]
    public async Task Describe_DomainEnumArrayRangeAndMultirangeInferred_HaveTheirParts()
    {
        var users = await ShouldDescribeAsync(
            Requests.Of("UPDATE users SET email = @email, status = @status, tags = @tags")
        );
        var bookings = await ShouldDescribeAsync(Requests.Of("UPDATE bookings SET during = @during, slots = @slots"));

        users.ShouldBe([Inferred("email", EmailT), Inferred("status", UserStatus), Inferred("tags", TextArray)]);
        bookings.ShouldBe([
            Inferred(
                "during",
                TestSidecars.Postgres(
                    "tstzrange",
                    SidecarValues.Kind.Range,
                    internalName: "tstzrange",
                    subtype: TestSidecars.Postgres("timestamp with time zone", internalName: "timestamptz")
                )
            ),
            Inferred(
                "slots",
                TestSidecars.Postgres(
                    "int4multirange",
                    SidecarValues.Kind.Multirange,
                    internalName: "int4multirange",
                    subtype: TestSidecars.Postgres()
                )
            ),
        ]);
    }

    // A comparison with a varchar or a domain column infers text: the operator is text's.  The last is the
    // optional filter with the use that fixes the type first.
    [Theory]
    [InlineData("SELECT 1 FROM users WHERE name = @p")]
    [InlineData("SELECT 1 FROM users WHERE email = @p")]
    [InlineData("SELECT 1 FROM users WHERE (name = @p OR @p IS NULL)")]
    public async Task Describe_ParameterComparedWithATextualColumn_IsText(string sql) =>
        (await ShouldDescribeAsync(Requests.Of(sql))).ShouldBe([
            Inferred("p", TestSidecars.Postgres("text", internalName: "text")),
        ]);

    // The server parses each declared type: int is integer, varchar(50) loses its length, a domain stays a domain.
    [Fact]
    public async Task Describe_DeclaredTypes_AreResolvedByTheServer()
    {
        var parameters = await ShouldDescribeAsync(
            Requests.Of(
                "SELECT @a, @b, @c, @d, @e",
                ("a", "int"),
                ("b", "varchar(50)"),
                ("c", "public.user_status"),
                ("d", "email_t"),
                ("e", "text[]")
            )
        );

        parameters.ShouldBe([
            Declared("a", TestSidecars.Postgres()),
            Declared("b", CharacterVarying),
            Declared("c", UserStatus),
            Declared("d", EmailT),
            Declared("e", TextArray),
        ]);
    }

    [Fact]
    public async Task Describe_OptionalFilterWithADeclaration_IsTypedAsDeclared() =>
        (
            await ShouldDescribeAsync(
                Requests.Of("SELECT 1 FROM users WHERE (@name IS NULL OR name = @name)", ("name", "varchar"))
            )
        ).ShouldBe([Declared("name", CharacterVarying)]);

    // The declared parameter is not in the SQL: it stands at the end of the list, and the server accepts a list
    // longer than the placeholders used.
    [Fact]
    public async Task Describe_ParameterThatOnlyAMarkerDeclares_IsTypedAsDeclared() =>
        (await ShouldDescribeAsync(Requests.Of("DELETE FROM users WHERE status = @status", ("tenant", "int")))).ShouldBe(
            [Inferred("status", UserStatus), Declared("tenant", TestSidecars.Postgres())]
        );

    [Fact]
    public async Task Describe_ParameterUsedTwice_IsOneParameter() =>
        (await ShouldDescribeAsync(Requests.Of("SELECT 1 FROM users WHERE id = @id OR id = @ID + 1"))).ShouldBe([
            Inferred("id", TestSidecars.Postgres()),
        ]);

    // The server accepts a pseudo-type, declared or inferred.  A .sql file can cause either, so neither throws.
    [Theory]
    [InlineData("SELECT row_to_json(@p)", null, "SQLSRC226")]
    [InlineData("SELECT int4in(@p)", null, "SQLSRC226")]
    [InlineData("SELECT @p", "record", "SQLSRC228")]
    [InlineData("SELECT @p", "anyelement", "SQLSRC228")]
    public async Task Describe_ParameterOfAPseudoType_IsAFailureAndNoThrow(string sql, string? declared, string id)
    {
        var request = declared is null ? Requests.Of(sql) : Requests.Of(sql, ("p", declared));

        var described = await DescribeAsync(request);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe(id);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBeEmpty();
    }

    // "$2" is no parameter of SqlSource.  The server types it after the query's own, and the query's own are what
    // is described.
    [Fact]
    public async Task Describe_SqlWithAPlaceholderOfItsOwn_TypesTheListedParametersAlone() =>
        (await ShouldDescribeAsync(Requests.Of("SELECT 1 FROM users WHERE id = @id AND name = $2"))).ShouldBe([
            Inferred("id", TestSidecars.Postgres()),
        ]);
```

`EquatableArray<>` needs no `using`: it is of the namespace `SqlSource`, which this file's namespace is inside.

- [ ] **Step 12: Run the container tests on both servers**

Run: `dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: PASS.

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres --no-build`
Expected: PASS.  PostgreSQL 14 has multiranges, so every case holds there.

- [ ] **Step 13: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`
Expected: every step `passed`.

```bash
git add src/SqlSource.Tool/Describing/Postgres tests/SqlSource.Tool.Tests/PostgresRows.cs tests/SqlSource.Tool.Tests/Describing/Postgres tests/SqlSource.Tests.Postgres/Describer
```

```bash
git commit -m "Type a query's parameters from the server's catalog

The closure of the types a prepare resolved, in one query, as the
sidecar's records with their spelled names.  A pseudo-type, which the
server accepts, is a failure at the query.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: The events

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, `PostgresDescriber.cs`

**Interfaces:**
- Consumes: `IRunLog.Write(string name, params LogMember[] members)`, `LogMember(string Name, LogValue Value)`, `LogValue` with its implicit conversions from `string`, `bool` and `long`, `LogValue.Of(long?)`, `LogValue.ListOf`, `LogValue.ListOfTexts`, `LogValue.MapOf`, `LogValue.Null`; `SessionRunLog(IRunLog inner, string secret, string? prefix)`; `OpenRequest.Time`; `RecordingLog` of the tool's tests.
- Produces:
  - `static class PostgresEvents` with `Prepare(IRunLog log, string query, PostgresPrepareRequest request, PostgresPrepareAnswer answer, long durationMs)`, `Types(IRunLog log, PostgresTypesRequest request, PostgresTypesAnswer answer, long durationMs)`, `Deallocate(IRunLog log, PostgresDeallocateRequest request, PostgresDeallocateAnswer answer, long durationMs)`
  - `PostgresSession.OpenAsync(OpenRequest request, IRunLog log, CancellationToken cancellationToken)`: the signature gains the log.
  - The events `prepare`, `types` and `deallocate`, which the run's `SessionRunLog` writes as `postgres.prepare`, `postgres.types` and `postgres.deallocate`.

- [ ] **Step 1: Write the failing tests**

`tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`:

```csharp
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the describer says of each call in the run's log: what it asked and what the server answered.
public class PostgresEventsTests
{
    private static readonly DescribeRequest Query = new(
        "GetUser",
        "SELECT 1 FROM users WHERE id = @id",
        new EquatableArray<SqlQueryParameter>([new SqlQueryParameter("id", null, null, false)])
    );

    private static readonly PostgresPrepareRequest Prepare = new(
        "sqlsource_1",
        "SELECT 1 FROM users WHERE id = $1",
        ["unknown"]
    );

    private static readonly PostgresDeallocateRequest Deallocate = new("sqlsource_1");

    private static LogValue Member(LogMember[] members, string name) =>
        members.Single(member => member.Name == name).Value;

    private static string[] NamesOf(LogMember[] members) => [.. members.Select(member => member.Name)];

    private static async Task DescribeAsync(PostgresScript script, IRunLog log)
    {
        await using var session = await PostgresScript.SessionAsync(script.Play(), log);
        _ = await session.DescribeAsync(Query, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Describe_QueryWithAParameter_WritesPrepareTypesAndDeallocateInOrder()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Answer("prepare", Prepare, new PostgresPrepareAnswer([23], null))
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

        await DescribeAsync(script, log);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "types", "deallocate"]);

        var prepare = log.Events[0].Members;
        NamesOf(prepare).ShouldBe(["query", "statement", "sql", "types", "parameterTypes", "durationMs"]);
        Member(prepare, "query").ShouldBe((LogValue)"GetUser");
        Member(prepare, "statement").ShouldBe((LogValue)"sqlsource_1");
        Member(prepare, "sql").ShouldBe((LogValue)"SELECT 1 FROM users WHERE id = $1");
        Member(prepare, "types").ShouldBe(LogValue.ListOfTexts(["unknown"]));
        Member(prepare, "parameterTypes").ShouldBe(LogValue.ListOf([23L]));
        _ = Member(prepare, "durationMs").ShouldBeOfType<LogValue.Number>();

        var types = log.Events[1].Members;
        NamesOf(types).ShouldBe(["oids", "rows", "durationMs"]);
        Member(types, "oids").ShouldBe(LogValue.ListOf([23L]));
        Member(types, "rows")
            .ShouldBe(
                LogValue.ListOf([
                    LogValue.MapOf(
                        new("oid", 23L),
                        new("typname", "int4"),
                        new("nspname", "pg_catalog"),
                        new("typtype", "b"),
                        new("typcategory", "N"),
                        new("typelem", 0L),
                        new("typbasetype", 0L),
                        new("rngsubtype", LogValue.Null),
                        new("multirangeRngsubtype", LogValue.Null),
                        new("formatType", "integer"),
                        new("enumLabels", LogValue.Null)
                    ),
                ])
            );

        var deallocate = log.Events[2].Members;
        NamesOf(deallocate).ShouldBe(["statement", "error", "durationMs"]);
        Member(deallocate, "statement").ShouldBe((LogValue)"sqlsource_1");
        Member(deallocate, "error").ShouldBe(LogValue.Null);
    }

    // A prepare that failed has its error in the place of the types, and nothing else is asked or written.
    [Fact]
    public async Task Describe_PrepareThatFails_WritesItsErrorAndNoOtherEvent()
    {
        var log = new RecordingLog();
        var rejected = new PostgresServerError("42P01", "relation \"users\" does not exist", "a detail", null, 49);
        var script = new PostgresScript().Open().Answer("prepare", Prepare, new PostgresPrepareAnswer(null, rejected));

        await DescribeAsync(script, log);

        var prepare = log.Events.ShouldHaveSingleItem();
        prepare.Name.ShouldBe("prepare");
        NamesOf(prepare.Members).ShouldBe(["query", "statement", "sql", "types", "error", "durationMs"]);
        Member(prepare.Members, "error")
            .ShouldBe(
                LogValue.MapOf(
                    new("sqlState", "42P01"),
                    new("message", "relation \"users\" does not exist"),
                    new("detail", "a detail"),
                    new("hint", LogValue.Null),
                    new("position", 49L)
                )
            );
    }

    // The error of "deallocate" is nowhere but here.
    [Fact]
    public async Task Describe_DeallocateThatFails_WritesTheErrorThatIsNeverReported()
    {
        var log = new RecordingLog();
        var gone = new PostgresServerError("26000", "prepared statement \"sqlsource_1\" does not exist", null, null, null);
        var script = new PostgresScript()
            .Open()
            .Answer("prepare", Prepare, new PostgresPrepareAnswer([23], null))
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(gone));

        await DescribeAsync(script, log);

        var error = Member(log.Events[2].Members, "error").ShouldBeOfType<LogValue.Members>();
        error.Values[0].ShouldBe(new LogMember("sqlState", "26000"));
    }

    // As a run gives the describer its log: the engine before each name, and the connection's value written as ***
    // wherever a describer put it, the SQL of a query included.
    [Fact]
    public async Task Describe_ThroughTheLogOfASession_HasTheEngineBeforeEachNameAndNothingOfTheSecret()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Answer("prepare", Prepare, new PostgresPrepareAnswer([23], null))
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

        await DescribeAsync(script, new SessionRunLog(log, "users", "postgres."));

        log.Events.Select(written => written.Name)
            .ShouldBe(["postgres.prepare", "postgres.types", "postgres.deallocate"]);
        Member(log.Events[0].Members, "sql").ShouldBe((LogValue)"SELECT 1 FROM *** WHERE id = $1");
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresEventsTests"`
Expected: FAIL: the log holds no event.

- [ ] **Step 3: Write the events**

`src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs`:

```csharp
using System.Linq;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The events of the PostgreSQL describer, each built in one place: what a call asked and what the server answered,
/// as the capture holds it.
/// </summary>
/// <remarks>
/// The names are written without the engine: the log a describer is given puts <c>postgres.</c> before each, and
/// removes the connection's value from every string.  A member is added and never renamed.  <c>open</c> has no
/// event of its own: the run's <c>database.open</c> records what the session knows of its server.
/// </remarks>
internal static class PostgresEvents
{
    private const string Error = "error";
    private const string Duration = "durationMs";

    /// <summary><c>postgres.prepare</c>: the SQL as it was sent, and the types the server resolved or its error.</summary>
    public static void Prepare(
        IRunLog log,
        string query,
        PostgresPrepareRequest request,
        PostgresPrepareAnswer answer,
        long durationMs
    ) =>
        log.Write(
            "prepare",
            new("query", query),
            new("statement", request.Statement),
            new("sql", request.Sql),
            new("types", LogValue.ListOfTexts(request.Types)),
            answer.Error is { } error
                ? new LogMember(Error, ErrorOf(error))
                : new LogMember(
                    "parameterTypes",
                    LogValue.ListOf((answer.ParameterTypes ?? []).Select(static oid => (LogValue)oid))
                ),
            new(Duration, durationMs)
        );

    /// <summary><c>postgres.types</c>: each row as the answer holds it, or the error.</summary>
    public static void Types(IRunLog log, PostgresTypesRequest request, PostgresTypesAnswer answer, long durationMs) =>
        log.Write(
            "types",
            new("oids", LogValue.ListOf(request.Oids.Select(static oid => (LogValue)oid))),
            answer.Error is { } error
                ? new LogMember(Error, ErrorOf(error))
                : new LogMember("rows", LogValue.ListOf((answer.Rows ?? []).Select(RowOf))),
            new(Duration, durationMs)
        );

    /// <summary><c>postgres.deallocate</c>: the one place that says a statement could not be freed.</summary>
    public static void Deallocate(
        IRunLog log,
        PostgresDeallocateRequest request,
        PostgresDeallocateAnswer answer,
        long durationMs
    ) =>
        log.Write(
            "deallocate",
            new("statement", request.Statement),
            new(Error, answer.Error is { } error ? ErrorOf(error) : LogValue.Null),
            new(Duration, durationMs)
        );

    private static LogValue ErrorOf(PostgresServerError error) =>
        LogValue.MapOf(
            new("sqlState", error.SqlState),
            new("message", error.Message),
            new("detail", error.Detail),
            new("hint", error.Hint),
            new("position", LogValue.Of(error.Position))
        );

    private static LogValue RowOf(PostgresTypeRow row) =>
        LogValue.MapOf(
            new("oid", row.Oid),
            new("typname", row.Typname),
            new("nspname", row.Nspname),
            new("typtype", row.Typtype),
            new("typcategory", row.Typcategory),
            new("typelem", row.Typelem),
            new("typbasetype", row.Typbasetype),
            new("rngsubtype", LogValue.Of(row.Rngsubtype)),
            new("multirangeRngsubtype", LogValue.Of(row.MultirangeRngsubtype)),
            new("formatType", row.FormatType),
            new("enumLabels", row.EnumLabels is { } labels ? LogValue.ListOfTexts(labels) : LogValue.Null)
        );
}
```

- [ ] **Step 4: Give the session the log and the clock**

In `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`:

Add `using SqlSource.Tool.Logging;`.

The fields and the constructor become:

```csharp
    private readonly IDescribeExchange _exchange;
    private readonly IRunLog _log;
    private readonly TimeProvider _time;
    private int _statements;

    private PostgresSession(OpenRequest request, IRunLog log, PostgresConnection connection, ServerInfo server)
    {
        _exchange = request.Exchange;
        _time = request.Time;
        _log = log;
        Connection = connection;
        Server = server;
    }
```

`OpenAsync` takes the log after the request, `OpenRequest request, IRunLog log, CancellationToken cancellationToken`, with a `<param>` for it, `Where the session's events go.`, and makes the session with `new PostgresSession(request, log, connection, ServerOf(answer))`.

In `DescribeAsync`, `await _exchange.AskAsync("prepare", prepare, PrepareLiveAsync, cancellationToken)` becomes `await PrepareAsync(request.Name, prepare, cancellationToken)`, and the statement of the `finally` becomes:

```csharp
            await DeallocateAsync(new PostgresDeallocateRequest(prepare.Statement), cancellationToken);
```

In `TypeParametersAsync`, the call of `types` becomes:

```csharp
        var answer = await TypesAsync(new PostgresTypesRequest([.. oids.Distinct().Order()]), cancellationToken);
```

Add to the class, before `OpenLiveAsync`:

```csharp
    // Each call is asked through the exchange and written to the log where it happens, with how long it took by the
    // run's clock.  Under a replay the event is written all the same.
    private async Task<PostgresPrepareAnswer> PrepareAsync(
        string query,
        PostgresPrepareRequest request,
        CancellationToken cancellationToken
    )
    {
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("prepare", request, PrepareLiveAsync, cancellationToken);
        PostgresEvents.Prepare(_log, query, request, answer, ElapsedSince(started));
        return answer;
    }

    private async Task<PostgresTypesAnswer> TypesAsync(PostgresTypesRequest request, CancellationToken cancellationToken)
    {
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("types", request, TypesLiveAsync, cancellationToken);
        PostgresEvents.Types(_log, request, answer, ElapsedSince(started));
        return answer;
    }

    // The error of the call is logged and never reported.
    private async Task DeallocateAsync(PostgresDeallocateRequest request, CancellationToken cancellationToken)
    {
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("deallocate", request, DeallocateLiveAsync, cancellationToken);
        PostgresEvents.Deallocate(_log, request, answer, ElapsedSince(started));
    }

    private long ElapsedSince(long started) => (long)_time.GetElapsedTime(started).TotalMilliseconds;
```

In `src/SqlSource.Tool/Describing/Postgres/PostgresDescriber.cs`, `OpenAsync` passes the log on:

```csharp
    ) => PostgresSession.OpenAsync(request, log, cancellationToken);
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests`, then `dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: PASS.

- [ ] **Step 6: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`
Expected: every step `passed`.

```bash
git add src/SqlSource.Tool/Describing/Postgres tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs
```

```bash
git commit -m "Log what the PostgreSQL describer asked and what the server answered

postgres.prepare, postgres.types and postgres.deallocate, each with how
long the call took by the run's clock.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: The fixtures, recorded and replayed

**Files:**
- Create: `tests/SqlSource.Tests.Postgres/Describer/FixtureCasesTests.cs`
- Create: `tools/record-fixtures.sh`
- Create: `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`
- Create: `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/update-inferred/query.sql`, and `capture.json` and `expected.json` by the script
- Create: the same three files under `insert-declared/`, `delete-declared-only/` and `update-array-and-range/`
- Modify: `CONTRIBUTING.md`, `tools/AGENTS.md`

**Interfaces:**
- Consumes: `FixtureCase(string Engine, string Name, string Folder)` with `QueryFile`, `CaptureFile`, `ExpectedFile`, `Query`, `Capture`, `Expected`; `FixtureCases.Root`, `FixtureCases.Of(string engine)`, `FixtureCases.Of(string engine, string root)`; `FixtureReplay.DescribeAsync(FixtureCase, Func<FixtureCase, IQueryDescriber>)`, `FixtureReplay.DescribeThroughAsync(FixtureCase, Func<FixtureCase, IQueryDescriber>, string connection, IDescribeExchange)`; `CaptureJson.Read`, `CaptureJson.Write`; `CapturingExchange`; `CaptureMissException`; `RepositoryPaths.DescriberFixtures`; `PostgresContainer`; `TempFolder` of the tool's tests.
- Produces: `FixtureCasesTests.RecordVariable = "SQLSOURCE_RECORD_FIXTURES"`; the four cases; `tools/record-fixtures.sh`.

- [ ] **Step 1: Write the four queries**

Each is a complete SqlSource file with one query, a statement without rows, so that sub-phase 3.3 changes none of its entry.

`tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/update-inferred/query.sql`:

```sql
-- name: RenameUser
UPDATE users SET name = @name WHERE id = @id;
```

`tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/insert-declared/query.sql`:

```sql
-- name: AddUser
-- param: @name varchar(50)
-- param: @email email_t
INSERT INTO users (name, email, status) VALUES (@name, @email, @status);
```

`tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/delete-declared-only/query.sql`:

```sql
-- name: PurgeUsers
-- param: @tenant int
DELETE FROM users WHERE status = @status {{filter:AND deleted_at IS NOT NULL}};
```

`tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/update-array-and-range/query.sql`:

```sql
-- name: MoveBooking
UPDATE bookings SET during = @during, tags = @tags WHERE id = @id;
```

- [ ] **Step 2: Write the failing replay test**

`tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// Every case of every engine's fixtures, replayed with the engine's describer and no database: the text of the
// sidecar is the case's expected.json, byte for byte.  tests/SqlSource.Tests.Postgres describes the same cases
// against a server, and tools/record-fixtures.sh records them.
public sealed class DescriberFixtureTests : IDisposable
{
    private static readonly IReadOnlyList<FixtureCase> Found = FindAll();

    private readonly TempFolder _folder = new();

    // Each case as "engine/name".
    public static TheoryData<string> Cases => [with(Found.Select(static found => found.ToString()))];

    public void Dispose() => _folder.Dispose();

    // A folder under the root is an engine.
    private static List<FixtureCase> FindAll()
    {
        if (!Directory.Exists(FixtureCases.Root))
        {
            return [];
        }

        return
        [
            .. new DirectoryInfo(FixtureCases.Root)
                .GetDirectories()
                .Select(static folder => folder.Name)
                .Order(StringComparer.Ordinal)
                .SelectMany(static engine => FixtureCases.Of(engine)),
        ];
    }

    private static IQueryDescriber DescriberOf(FixtureCase fixture) =>
        fixture.Engine switch
        {
            "postgres" => new PostgresDescriber(),
            _ => throw new InvalidOperationException($"No describer is known for the engine of the case {fixture}."),
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Replay_Case_GivesTheExpectedEntry(string name)
    {
        var fixture = Found.Single(found => found.ToString() == name);

        var replayed = await FixtureReplay.DescribeAsync(fixture, DescriberOf);

        replayed.ShouldBe(fixture.Expected);
    }

    // The cases of the first describer.  A build that copied none would leave the theory with nothing to run.
    [Fact]
    public void Cases_OfPostgres_HoldTheFirstFour()
    {
        var names = Found.Select(static found => found.ToString()).ToList();

        foreach (
            var name in new[]
            {
                "postgres/delete-declared-only",
                "postgres/insert-declared",
                "postgres/update-array-and-range",
                "postgres/update-inferred",
            }
        )
        {
            names.ShouldContain(name);
        }
    }

    // A describer whose calls changed fails its fixtures, and never passes silently.
    [Theory]
    [InlineData("prepare")]
    [InlineData("types")]
    [InlineData("deallocate")]
    public async Task Replay_CaptureWithACallRemoved_ThrowsACaptureMiss(string method)
    {
        var source = Found.Single(static found => found.ToString() == "postgres/update-inferred");
        var capture = CaptureJson.Read(source.Capture).Value.ShouldNotBeNull();
        var cut = new DescribeCapture(capture.Engine);
        foreach (var call in capture.Calls.Where(call => call.Method != method))
        {
            cut.Add(call);
        }

        _ = _folder.WriteFile($"postgres/cut/{FixtureCase.QueryFile}", source.Query);
        _ = _folder.WriteFile($"postgres/cut/{FixtureCase.CaptureFile}", CaptureJson.Write(cut));
        var fixture = new FixtureCase("postgres", "cut", _folder.PathOf("postgres/cut"));

        _ = await Should.ThrowAsync<CaptureMissException>(() => FixtureReplay.DescribeAsync(fixture, DescriberOf));
    }
}
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "SqlSource.Tool.Tests.Fixtures.DescriberFixtureTests"`
Expected: FAIL: the four cases have no `capture.json`.

- [ ] **Step 3: Write the test that describes the cases live, and records them**

`tests/SqlSource.Tests.Postgres/Describer/FixtureCasesTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Tests.Fixtures;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// The fixture cases of the PostgreSQL describer against a real server: describing a case's query.sql gives the
// entry of its expected.json.  With SQLSOURCE_RECORD_FIXTURES set it writes the case's capture.json and
// expected.json and compares nothing: tools/record-fixtures.sh runs it so.  The cases are in the repository, in
// tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres, where the tool's tests replay them with no database.
[Collection(UsesPostgres.Name)]
public sealed class FixtureCasesTests(PostgresContainer postgres)
{
    public const string RecordVariable = "SQLSOURCE_RECORD_FIXTURES";

    private const string Engine = "postgres";

    // As SidecarWriter writes the key.
    private const string VersionKey = "\"serverVersion\": \"";

    private static readonly IReadOnlyList<FixtureCase> Found = FixtureCases.Of(
        Engine,
        RepositoryPaths.DescriberFixtures
    );

    public static TheoryData<string> Cases => [with(Found.Select(static found => found.Name))];

    private static bool Recording => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RecordVariable));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Describe_Case_GivesTheExpectedEntry(string name)
    {
        var fixture = Found.Single(found => found.Name == name);
        if (Recording)
        {
            await RecordAsync(fixture);
            return;
        }

        var live = await FixtureReplay.DescribeThroughAsync(
            fixture,
            static _ => new PostgresDescriber(),
            postgres.ConnectionString,
            LiveExchange.Instance
        );

        WithOneServerVersion(live).ShouldBe(WithOneServerVersion(fixture.Expected));
    }

    private async Task RecordAsync(FixtureCase fixture)
    {
        var token = TestContext.Current.CancellationToken;
        var capture = new DescribeCapture(Engine);

        var expected = await FixtureReplay.DescribeThroughAsync(
            fixture,
            static _ => new PostgresDescriber(),
            postgres.ConnectionString,
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        await File.WriteAllTextAsync(
            Path.Combine(fixture.Folder, FixtureCase.CaptureFile),
            CaptureJson.Write(capture),
            token
        );
        await File.WriteAllTextAsync(Path.Combine(fixture.Folder, FixtureCase.ExpectedFile), expected, token);
    }

    // An expected.json keeps the version it was recorded against.  CI describes the same cases on two servers, and
    // the default tag moves from one patch to the next, so the two texts are compared with one value there.
    private static string WithOneServerVersion(string sidecar)
    {
        var key = sidecar.IndexOf(VersionKey, StringComparison.Ordinal);
        key.ShouldBeGreaterThanOrEqualTo(0, "the entry has no serverVersion");
        var value = key + VersionKey.Length;
        var end = sidecar.IndexOf('"', value);
        return string.Concat(sidecar.AsSpan(0, value), "*", sidecar.AsSpan(end));
    }
}
```

- [ ] **Step 4: Write the script**

`tools/record-fixtures.sh`:

```bash
#!/usr/bin/env bash
# Records the fixture cases of the PostgreSQL describer: describes the query.sql of each case under
# tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres against a PostgreSQL in a container, and writes the case's
# capture.json and expected.json.  It is no check: a person runs it, after a change to what the describer asks or
# answers or to schema.sql, and reads the diff of what it wrote before committing it.
# It needs Docker.  The image is the one of the tests, which SQLSOURCE_TESTS_POSTGRES_IMAGE overrides.
# Usage: record-fixtures.sh
set -euo pipefail

PROJECT='tests/SqlSource.Tests.Postgres'
CASES='tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres'
TESTS='SqlSource.Tests.Postgres.Describer.FixtureCasesTests'

if [[ $# -ne 0 ]]; then
    echo 'Usage: record-fixtures.sh' >&2
    exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

# The default image is named in one place, which is read here and not repeated.
default_image="$(sed -n 's/.*DefaultImage = "\(.*\)";.*/\1/p' "$PROJECT/PostgresContainer.cs")"
image="${SQLSOURCE_TESTS_POSTGRES_IMAGE:-$default_image}"
count="$(find "$CASES" -mindepth 1 -maxdepth 1 -type d | wc -l | tr -d ' ')"

printf 'Recording %s cases of postgres against %s ... ' "$count" "$image"
if ! output="$(SQLSOURCE_RECORD_FIXTURES=1 dotnet test --project "$PROJECT" -- --filter-class "$TESTS" 2>&1)"; then
    echo 'failed'
    echo "$output" >&2
    exit 1
fi
echo 'done'
```

Run: `chmod +x tools/record-fixtures.sh`, then `tools/shfmt.sh`, then `tools/shellcheck.sh`
Expected: `shfmt` leaves the script as it is or lays it out; ShellCheck has no finding.

- [ ] **Step 5: Record**

Run: `tools/record-fixtures.sh`
Expected: `Recording 4 cases of postgres against postgres:18 ... done`, and each of the four folders now holds `capture.json` and `expected.json`.

- [ ] **Step 6: Read what was recorded**

For each case, `capture.json` holds `"captureVersion": 1`, `"engine": "postgres"` and the calls `open`, `prepare`, `types`, `deallocate` in that order; the request of `open` is `{}`; the `sql` of `prepare` has `$1` for each parameter and no `;`.  Check each of these against the files:

| Case | `types` of `prepare` | `parameterTypes` | Parameters of `expected.json`, in order |
|----|----|----|----|
| `update-inferred` | `unknown`, `unknown` | `1043`, `23` | `name`: `character varying`, `inferred`; `id`: `integer`, `inferred` |
| `insert-declared` | `varchar(50)`, `email_t`, `unknown` | `1043`, the OID of `email_t`, the OID of `user_status` | `name`: `character varying`, `declared`; `email`: `public.email_t`, kind `domain`, with the base `character varying`, `declared`; `status`: `public.user_status`, kind `enum`, with the labels `active` and `disabled`, `inferred` |
| `delete-declared-only` | `unknown`, `int` | the OID of `user_status`, `23` | `status`: `public.user_status`, `inferred`; `tenant`: `integer`, `declared`.  The `sql` ends `AND deleted_at IS NOT NULL` |
| `update-array-and-range` | `unknown`, `unknown`, `unknown` | `3910`, `1009`, `20` | `during`: `tstzrange`, kind `range`, with the subtype `timestamp with time zone`; `tags`: `text[]`, kind `array`, with the element `text`; `id`: `bigint`; all `inferred` |

Every `expected.json` has `"toolVersion": "fixture"`, `"resultKind": "none"` and no columns.

Run: `grep -rlE 'Host=|Password|127\.0\.0\.1' tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres`
Expected: no file: nothing of the connection is in a capture.

If a file is not as the table says, the describer is wrong, not the file: mend the describer and record again.  Never edit a recorded file by hand.

- [ ] **Step 7: Run the three kinds of test of the cases**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "SqlSource.Tool.Tests.Fixtures.DescriberFixtureTests"`
Expected: PASS, 8 tests: the build copied the cases to the output.

Run: `dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: PASS, with `FixtureCasesTests` among them, 4 tests.

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres --no-build`
Expected: PASS: the entries recorded against 18 are what 14 gives, but for the version.

Run: `git add tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres`, then `tools/record-fixtures.sh`, then `git diff --stat tests/SqlSource.Tool.Tests/Fixtures/Describers`
Expected: no line.  A recording is the same each time: a capture holds no time, no port and no name that is not a counter.

- [ ] **Step 8: Bring the documents this makes wrong**

`CONTRIBUTING.md`: after the paragraph that Task 2 added and that ends `runs it on the default image.`:

````markdown
The describer's fixtures are in `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/`: a folder for each case with its `query.sql`, the `capture.json` of describing it and the `expected.json` of its sidecar.  `tests/SqlSource.Tool.Tests` replays each from its capture with no database, and `tests/SqlSource.Tests.Postgres` describes each against the container and compares the entry, so a describer that asks something else fails the first and one that answers something else fails the second.  After a change to what the describer asks or answers, or to `schema.sql`, record them again and read the diff before it is committed:

```bash
tools/record-fixtures.sh
```

To add a case, add its folder with a `query.sql` and run the script.  A recorded file is never edited by hand.
````

`tools/AGENTS.md`: the first sentence, `The check scripts that ``pre-commit-validation.sh`` and ``.github/workflows/build.yml`` run, and the consumer project that one of them installs the package into.`, gains a clause at its end, before the full stop: `, and one script that is no check`.  And a bullet at the end of the list:

```markdown
- **`record-fixtures.sh` is not a check.**  It rewrites the `capture.json` and the `expected.json` of every case under `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/`, so it is in neither `pre-commit-validation.sh` nor `build.yml`, and nothing runs it but a person.  It reads the default image from `tests/SqlSource.Tests.Postgres/PostgresContainer.cs`, by the line that declares `DefaultImage`: a change to that line's shape changes the `sed` of the script.
```

- [ ] **Step 9: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`
Expected: every step `passed`.  `editorconfig-checker` reads the recorded files by the section of `.editorconfig` for the fixture folder.

```bash
git add tools/record-fixtures.sh tools/AGENTS.md CONTRIBUTING.md tests/SqlSource.Tests.Postgres/Describer/FixtureCasesTests.cs tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres
```

```bash
git commit -m "Record the first fixtures of the PostgreSQL describer, and replay them

Four statements without rows, recorded by tools/record-fixtures.sh
against the container, described live there and replayed with no
database in the tool's tests.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: The documents, and the outline

**Files:**
- Create: `docs/tech-debt/TD-0043-small-gaps-of-the-postgresql-describers-parameters.md`
- Modify: `docs/tech-debt/README.md`, `docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md`, `docs/tech-debt/TD-0041-ca2000-is-suppressed-on-the-opener-of-the-log.md`
- Modify: `src/SqlSource.Tool/AGENTS.md`, `tests/AGENTS.md`
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`

**Interfaces:**
- Consumes: everything above, by name.
- Produces: nothing a later task consumes.

- [ ] **Step 1: Record what was found and not mended**

`docs/tech-debt/TD-0043-small-gaps-of-the-postgresql-describers-parameters.md`.  If `Next id` in `docs/tech-debt/README.md` is no longer `TD-0043`, take the id it gives, and use it in the file's name, in its title, in `PostgresTypeNames.cs` and in the bullets of Step 3.

```markdown
# TD-0043 - Small gaps of the PostgreSQL describer's parameters

## Problem

Three things that [`PostgresSession`](../../src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs) does with a query's parameters are not whole:

- **A `$n` that the SQL writes itself.**  SqlSource's parameter is `@name`, and the lexer reads `$1` as text.  The server reads it as a parameter: `SELECT 1 FROM users WHERE id = @id AND name = $2` prepares with two types, and `SELECT @a, $1` has one parameter that the SQL names twice.  The describer types the parameters of the query's list, the first of the types the server resolved, and says nothing of the rest: the query is described as if the `$n` were not a parameter, and what the generated code then sends is the user's to get right.
- **A reserved word as the schema of a type.**  [`PostgresTypeNames`](../../src/SqlSource.Tool/Describing/Postgres/PostgresTypeNames.cs) writes a schema and a name bare when each is lower-case letters, digits and `_`.  A schema named `user` or `order` needs quotes in SQL, and its types are spelled `user.status`.  The sidecar's `name` is what phase 8's generated code binds with.
- **A cancelled describe leaves its statement.**  `deallocate` is asked with the describe's own token, so a run that is cancelled between `prepare` and `deallocate` frees nothing.  The statement dies with its session, which the run closes next.

## Why it exists

The first: refusing a `$n` needs the tool to tell one from a dollar-quoted string's tag and from a money literal, which the lexer does for a quote and not for a placeholder, and sub-phase 3.3's check of the lexer against Npgsql is the place that sees a query whose parameters the two read differently.  The second: the tool has no list of PostgreSQL's reserved words, which differs by version.  The third is the spec's choice: a cancelled run must not wait on a server.

## Impact

Low.  The first two need a query or a schema that few projects have, and the describer is registered nowhere until sub-phase 3.6.  The third costs nothing: the session ends.

## Proposed fix

For the first, report a `$` followed by a digit in a `Text` lexeme, outside an identifier, as an error of its own at the query, or let sub-phase 3.3's check report it.  For the second, quote a name that `pg_get_keywords()` lists as reserved, asked once for a session with `open`, or always quote a name of a schema outside `pg_catalog` and `public`.  The third needs none.

## Trigger

Sub-phase 3.3, whose check of the lexer against Npgsql meets the first; phase 8, whose generated code binds a parameter by the sidecar's `name`, for the second.
```

In `docs/tech-debt/README.md`: `Next id` becomes the id after the one taken, and a row at the end of the table, with today's date in the place of `<date>`:

```markdown
| [TD-0043](TD-0043-small-gaps-of-the-postgresql-describers-parameters.md) | Open | <date> | Low | The PostgreSQL describer types the parameters of a query's list and says nothing of a `$n` that the SQL writes itself; it spells a type of a schema whose name is a reserved word without quotes; and a describe that is cancelled leaves its prepared statement to die with the session |
```

- [ ] **Step 2: Bring the two items this sub-phase changes**

`docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md`:

1. `Five things are outside all of them:` becomes `Six things are outside all of them:`.
2. A bullet at the end of that list:

```markdown
- **The driver's own message of a session that could not be opened, or that dropped.**  The PostgreSQL describer answers `open` with what Npgsql or the server said, and that names a part of the connection that is no secret and is still the user's: `Failed to connect to 10.0.0.5:5432` holds the host and the port, and `password authentication failed for user "app"` the user's name.  It is printed under `SQLSRC224`, written to the log's `diagnostic` event, and kept in the `open` answer of a capture.  A connection that drops while a query is described is `SQLSRC225` with a message of the same kind.  What Npgsql says of a connection string it cannot read is the one text the describer does not pass on: [`PostgresConnection`](../../src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs) answers `Npgsql cannot read the connection string` in its place, since the driver's exception quotes the value.
```

3. In Why it exists, `and no describer exists yet whose texts could be studied` becomes `and the first describer, PostgreSQL's, is registered nowhere yet, so its driver's texts have been met only in its tests`.
4. In Impact, `Low today: the released tool has no describer.  From phase 3 a describer's rule is` becomes `Low today: the released tool has no describer registered.  A describer's rule is`.
5. In Proposed fix, `With the first real describer: have it hand the tool` becomes `When the PostgreSQL describer is registered, in sub-phase 3.6: have it hand the tool`.
6. Trigger becomes `Sub-phase 3.6, which registers the PostgreSQL describer and routes its driver's events to the log; for a capture, phase 10's ``sqlsource diagnose``, which writes one for a user.` (with single backticks).

In `docs/tech-debt/README.md`, the row of `TD-0034` gains at the end of its description: `; and the driver's own message of a session that could not be opened names the host or the user`.

`docs/tech-debt/TD-0041-ca2000-is-suppressed-on-the-opener-of-the-log.md`: a paragraph at the end of Problem:

```markdown
[`PostgresSession.OpenAsync`](../../src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs) has the same attribute for the same reason: it returns the session in an `Outcome`, and the session owns the `PostgresConnection` the method makes.  The method disposes the connection itself on every path that does not return the session, in a `finally`, and an edit that breaks that is not reported.
```

In `docs/tech-debt/README.md`, the row of `TD-0041`: `the describer of the tests has the same on its ``OpenAsync``, and the first real describer will` becomes `the describer of the tests has the same on its ``OpenAsync``, and so has ``PostgresSession.OpenAsync`` of the PostgreSQL describer` (with single backticks).

- [ ] **Step 3: Say in the tool's AGENTS.md what the describer is**

In `src/SqlSource.Tool/AGENTS.md`:

1. The first paragraph gains a sentence at its end: `` The PostgreSQL describer is also tested against a server, in `tests/SqlSource.Tests.Postgres`. ``
2. After the bullet that starts `**The fixtures of a describer are in**`, these bullets:

```markdown
- **`Describing/Postgres/` is the PostgreSQL describer, and it is registered nowhere.**  `ToolHost.Create` still gives `DescriberRegistry.Empty`, and its tests alone reach it, until the describer is whole.  It describes a query through one prepared statement: `PostgresSession.DescribeAsync` rewrites `@name` to `$n` (`ParameterPlaceholders.Rewrite`), asks `prepare`, which runs `PREPARE <name> (<declared type or unknown>, ...) AS <sql>` and reads `parameter_types` of `pg_prepared_statements`, asks `types` for the closure of those OIDs, and asks `deallocate`.  `PREPARE` is the route because it takes a declared type as written, `int`, `varchar(50)`, a domain, resolves it and checks it against every use of the parameter; Npgsql's own describe sends no types.  A description has its parameters, `ResultKind.None` and nothing else: the columns are sub-phase 3.3's.
- **A prepared statement is session state.**  It survives a rollback, so `deallocate` is asked in a `finally`, outside any transaction, whenever `prepare` answered without an error, and not after one that failed: there is nothing to free, and the counter of the names advances all the same.  A name is `sqlsource_<n>`, counted within the session: it is part of a request, so it is the same when a capture is recorded and when it is replayed.  The error of `deallocate` is written to the log and never reported.
- **Nothing a `.sql` file holds can make the describer run a statement.**  Two checks stand before anything is sent, and both use no server.  `ParameterPlaceholders.Rewrite` refuses a text with a second statement, `SQLSRC230`: Npgsql splits at `;`, and `PREPARE` would prepare the first part and run the second.  `DeclaredTypeText` reads each `-- param:` type against the grammar of a type name, `SQLSRC228`: the type is put into the `PREPARE` as written.  The type list still ends with a line break before `AS`.  A change to either check, or a new place that puts a user's text into SQL the tool sends, gets a case in `tests/SqlSource.Tool.Tests/Describing/Postgres/` that shows what is refused.
- **The connection is made inside a `live` function and nowhere else.**  `PostgresConnection` holds the connection's value, makes one `NpgsqlDataSource` for the session and one connection from it, on the first `live` call that asks, and makes every command; under a replay none of that runs.  A data source for each session, because a plain `NpgsqlConnection` shares a type catalog by connection string for the life of the process.  It is the one file for which `.editorconfig` turns `CA2100` off.  Each `live` function catches `NpgsqlException` and returns it as its answer's `error`; a cancellation passes through; anything else is a bug.  A connection string that Npgsql cannot read is an `NpgsqlException` with a fixed text and no inner exception, so that it is an answer too and the driver's quote of the value is dropped.
- **The calls and their records are a contract with the captures.**  The methods are `open`, `prepare`, `types` and `deallocate`, and their records are in `Describing/Postgres/PostgresCalls.cs`, with the rule of a record above.  An OID is a `long`.  A row of `types` names its members as the catalog does.  A change to what the describer asks changes the fixtures: record them with `tools/record-fixtures.sh` and read the diff.
- **A server's error gets its id from four things, in this order** (`PostgresErrors`): the call, since every failure of `open` is `SQLSRC224`; a position inside the type list of the `PREPARE`, which makes it the declared type's, `SQLSRC228`, with `42704` as "no such type" and anything else as "not a type name"; the SQLSTATE `42P18` or `42P08` with a `$n` of the list in the message, `SQLSRC226` and `SQLSRC227`; and anything else is `SQLSRC225` with the server's message.  The server counts a position in characters of the text as sent, so the `at character` line is the position less `ParameterPlaceholders.HeadLength`, counted in runes, and points into the `$n` form of the SQL, which the `prepare` event logs.  The server accepts a pseudo-type as a parameter's type, declared or inferred, and says nothing: `PostgresTypes.IsPseudo` finds it after `types`, and it is `SQLSRC228` or `SQLSRC226` at the step `resolve type`.
- **A type's `name` is the tool's own spelling** (`PostgresTypeNames`): `format_type(oid, -1)` for a type of `pg_catalog`, with the facets written as PostgreSQL writes a modifier; the schema and the name as identifiers for any other, whatever the `search_path`; an element's name and `[]` for an array.  `format_type` of `char` is `bpchar`, and with a length the tool writes `character(n)`.  A parameter has no facets, since the protocol gives it an OID and no modifier; `PostgresFacets` is there for the columns.  What the spelling and the parameters leave open is `docs/tech-debt/TD-0043-small-gaps-of-the-postgresql-describers-parameters.md`.
- **`tests/SqlSource.Tool.Tests/PostgresScript.cs` answers the session with no server.**  A test writes each call the describer is to make, with its answer, and a `ReplayingExchange` plays them: a request the session asks otherwise has no answer and is a `CaptureMissException`, so these tests hold what is asked as well as what is made of the answers.  `tests/SqlSource.Tool.Tests/PostgresRows.cs` has rows of `types` as a server gives them.  What only a server can show, the types it infers, its errors and their positions, is in `tests/SqlSource.Tests.Postgres/Describer/`.
```

3. In the bullet that starts `**A describer returns a failure and never throws.**`, the last sentence, `The released tool has no describer: ``ToolHost.Create`` gives ``DescriberRegistry.Empty``, and every query that must be described is ``SQLSRC216``, until phase 3 registers PostgreSQL's.`, becomes `The released tool has no describer: ``ToolHost.Create`` gives ``DescriberRegistry.Empty``, and every query that must be described is ``SQLSRC216``, until sub-phase 3.6 registers PostgreSQL's, which is in ``Describing/Postgres/``.` (with single backticks).

In `tests/AGENTS.md`, in the bullet that starts `**Tests mirror the source folders.**`, after `the tests of ``ToolHost``, of ``Outcome`` and of a helper.`: `` `PostgresScript.cs` and `PostgresRows.cs` are among the helpers there, for the tests of `Describing/Postgres/`. ``

- [ ] **Step 4: Bring the outline**

In `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`:

1. The row of the Phases table that starts `| 3.2 The session and the parameters | In progress |` becomes `| 3.2 The session and the parameters | Done |`.
2. In the section of phase 3, under Settled, after the bullet that starts `**A declared type's text is read against the grammar of a type name**`:

```markdown
- **The server accepts a pseudo-type as a parameter's type.**  Sub-phase 3.2 found that `PREPARE s (record) AS SELECT $1` succeeds, and that `row_to_json($1)` and `int4in($1)` infer `record` and `cstring`.  So a parameter of a pseudo-type is a failure at the query and no bug of the describer: `SQLSRC228` for a declared one, with the reason `it is a pseudo-type`, and `SQLSRC226` for an inferred one, both at the step `resolve type` and with no line of the server, which said nothing.
- **A `$n` that the SQL writes itself is typed by the server.**  `PREPARE s AS SELECT 1 FROM users WHERE id = $1` succeeds with one type and no list.  The describer types the parameters of the query's list, the first of the server's, and `docs/tech-debt` records the rest.
- **`format_type(oid, -1)` of `char` is `bpchar`.**  A parameter of a `char(2)` column is recorded as `bpchar`, and a column with a length, in 3.3, as `character(2)`, which the tool writes itself.
- **A connection string Npgsql cannot read is `SQLSRC224`** with one fixed line and nothing of the driver's text, whose inner exception quotes the value.
```

3. In the table of the diagnostics there, the About cell of the row of 3.2, `a declared type that is not valid, one the database lacks or one not written as a type name;`, becomes `a declared type that is not valid, one the database lacks, one not written as a type name or a pseudo-type;`.

- [ ] **Step 5: Check the documents against the code**

Run: `grep -n "SQLSRC22[4-8]\|SQLSRC230" docs/diagnostics.md | head -20`
Expected: six rows of the table and six headings, in the order of the ids.

Run: `grep -rn "Describing/Postgres/\|tests/SqlSource.Tests.Postgres" src/SqlSource.Tool/AGENTS.md tests/AGENTS.md tools/AGENTS.md CONTRIBUTING.md | wc -l`
Expected: more than zero for each file, and every file and type a bullet names exists: open each path the new bullets name.

`README.md`, `docs/publishing.md` and `src/SqlSource.Tool/README.md` are unchanged: `git diff --stat main -- README.md docs/publishing.md src/SqlSource.Tool/README.md` shows nothing.  `docs/deferred` is unchanged: nothing of the spec was left out.

- [ ] **Step 6: Check the version**

Run: `git fetch --tags origin`, then `git tag --list 'v*' --sort=-v:refname | head -n 1`
Expected: no line.  With no `v*` tag there is nothing to compare `VersionPrefix` with.  If a tag is there and `VersionPrefix` in `Directory.Build.props` is not greater, stop and ask the user for the new version.

- [ ] **Step 7: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

```bash
git add docs/tech-debt src/SqlSource.Tool/AGENTS.md tests/AGENTS.md docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
```

```bash
git commit -m "Document the PostgreSQL describer's session and parameters, and close sub-phase 3.2

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
