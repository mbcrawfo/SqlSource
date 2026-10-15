# A suite of realistic queries for the PostgreSQL describer - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A corpus of at least 40 realistic, hand-written queries, described live on PostgreSQL 14 and 18, each checked against a hand-written truth and against seeded data, with its description pinned and a noise budget over the whole corpus; and a help line for SQLSRC225 when the server rejects a query over a type it inferred for a parameter.

**Architecture:** A second database, `realistic`, made in the container the collection `UsesPostgres` already starts, from its own `schema.sql` and `seed.sql`.  Each case is a folder under `tests/SqlSource.Tests.Postgres/Realistic/Cases/` with a SqlSource `query.sql`, a hand-written `case.json` (the truth, the sample values, a refusal or a minimum version) and recorded `expected-pg14.txt` and `expected-pg18.txt`.  A small harness in `tests/SqlSource.Tests.Postgres/Realistic/` plans each `query.sql` as a run does (through `FixtureReplay.Plan`), describes it live, executes it in a rolled-back transaction for the oracle, and renders the description as text for the expectation.

**Tech Stack:** C# on .NET 10, xunit v3, Shouldly, Npgsql, Testcontainers, System.Text.Json, bash for the recording script.

**Spec:** `docs/superpowers/specs/2026-10-15-postgresql-realistic-query-suite-design.md`, with its appendix `docs/superpowers/specs/2026-10-15-postgresql-realistic-query-suite-spike.md` (the spike's seed, 44 queries and output).

## Global Constraints

- Every commit passes `./pre-commit-validation.sh`, run after the last edit and before the commit (`AGENTS.md`).  It needs Docker running.
- The database `fixtures` and `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql` are not changed: other container tests assert row counts and OIDs there.
- `tests/SqlSource.Tests.Postgres/PostgresContainer.cs` stays the one place that names the image.
- The truth in `case.json` is written by reasoning about the SQL and the schema, never copied from the describer's output; where unsure, the column is `"nullable": true` with a `"why"`.
- Recording never writes `case.json`.
- A parameter's truth: it can be `NULL` only when the case's `-- param:` marker says `null`; saying nothing means `not null`.
- A bug the suite finds, a column recorded `nullable: false` that can be `NULL` or a describe that throws, is fixed in this work with the case that found it as its test.  A fix that turns out large stops the work: ask the user before going on.
- Tests are named `Method_Scenario_Outcome`, assert with Shouldly, and the container tests are in the collection `UsesPostgres`.
- Any `AGENTS.md` that names a file this plan renames or moves is updated in the same commit, and every new `AGENTS.md`-listed file keeps the maintenance footer.
- Before opening the pull request, compare `VersionPrefix` in `Directory.Build.props` with `git tag --list 'v*' --sort=-v:refname | head -n 1` after `git fetch --tags origin`; when it is not greater, ask the user for the new version.  Turn on Auto-fix for the pull request.

## Review Focus

1. **Two columns with one name** (`SELECT a.id, b.id`): the truth is matched to the description by position, and the names are compared only as a check.  Task 3 pins it.
2. **`@name` inside a string, a comment or a quoted identifier** is no parameter, and substitution must leave it alone (`'@x'`, `-- @x`).  Task 3 pins it.
3. **A case folder that is wrong** (no `case.json`, a `case.json` that does not parse, a truth with more or fewer columns than the result, a sample that names no parameter of the query): the test fails naming the case and the fault, never with a bare exception.  Task 3 pins the loader's faults; Task 4's truth theory pins the column count.
4. **A query that writes** must leave the seed as it was after the oracle, or later cases read changed data.  Task 3 pins it with an `INSERT ... RETURNING` run twice.
5. **Recording on a server other than 14 or 18** must fail, not write an `expected-pg16.txt` nothing reads.  Task 4 pins it.

---

### Task 1: SQLSRC225's help for a parameter typed by its first use

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs` (constants near line 85, `Prepare` before its last `return Rejected(...)`, `Rejected` at line 523)
- Modify: `docs/diagnostics.md`, section `## SQLSRC225`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs`

**Interfaces:**
- Produces: `PostgresErrors.InferredParameterHelp(IReadOnlyList<SqlQueryParameter> undeclared) -> string`, public static, for the tests.

- [ ] **Step 1: Write the failing tests**, with the file's existing helpers `Error`, `Query`, `Inferred`, `Declared` and `Prepared`:

```csharp
[Theory]
[InlineData("42883", "operator does not exist: timestamp with time zone > interval")]
[InlineData("42804", "argument of WHERE must be type boolean, not type integer")]
[InlineData("42725", "operator is not unique: unknown || unknown")]
public void Prepare_TypeErrorWithAParameterOfNoDeclaredType_IsSqlsrc225WithTheFirstUseHelp(string state, string message)
{
    var failure = PostgresErrors.Prepare(
        Error(state, message, 47), Query(Inferred("age"), Declared("n", "int"), Inferred("q")), Prepared("sqlsource_1", "unknown", "int", "unknown"), "app");

    failure.Descriptor.Id.ShouldBe("SQLSRC225");
    failure.Arguments.ShouldBe(["Q", message]);
    failure.Help.ShouldBe(
        "the server gave each of @age, @q the type of its first use, which may not be the type meant: cast the "
            + "parameter where it is used, @age::int, or declare its type with -- param: @age int");
}

[Fact]
public void Prepare_TypeErrorWithOneParameterOfNoDeclaredType_NamesItAlone() =>
    PostgresErrors.Prepare(Error("42883", "operator does not exist: integer = text", 40), Query(Inferred("x")), Prepared("sqlsource_1", "unknown"), "app")
        .Help.ShouldBe(
            "the server gave @x the type of its first use, which may not be the type meant: cast the parameter "
                + "where it is used, @x::int, or declare its type with -- param: @x int");

[Fact]
public void Prepare_TypeErrorWithEveryParameterDeclared_IsSqlsrc225WithNoHelp() =>
    PostgresErrors.Prepare(Error("42883", "operator does not exist: integer = text", 40), Query(Declared("x", "text")), Prepared("sqlsource_1", "text"), "app")
        .Help.ShouldBeNull();

[Fact]
public void Prepare_OtherErrorWithAParameterOfNoDeclaredType_IsSqlsrc225WithNoHelp() =>
    PostgresErrors.Prepare(Error("42P01", "relation \"nope\" does not exist", 40), Query(Inferred("x")), Prepared("sqlsource_1", "unknown"), "app")
        .Help.ShouldBeNull();
```

Adjust only the argument shapes of `Prepared` and `Query` to what the helpers take; the assertions are the spec's.  A parameter with a `-- param:` marker that gives no type, `Type is null`, counts as one of no declared type.

- [ ] **Step 2: Run them and see the first three fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*PostgresErrorsTests"`
Expected: FAIL, `Help` is null.

- [ ] **Step 3: Implement.**  Add `private const string UndefinedFunction = "42883";`, `DatatypeMismatch = "42804";`, `AmbiguousFunction = "42725";`.  In `Prepare`, the last `return` becomes a `Rejected` whose help is `InferredParameterHelp(undeclared)` when `error.SqlState` is one of the three and `query.Parameters.Where(p => p.Type is null)` is not empty, in the list's order, each written by `Written(...)`; otherwise null.  `Rejected` takes the help as a parameter; every other caller passes `null`.  `InferredParameterHelp` writes `@a` for one parameter and `each of @a, @b` for several, and its example is always the first of them with `int`.

- [ ] **Step 4: Run them and see them pass**, same command.  Expected: PASS.

- [ ] **Step 5: Document it.**  In `docs/diagnostics.md`, in `## SQLSRC225`, after the paragraph that ends "a declared `int` compared with a text column is this error too.", add a paragraph: a parameter with no declared type takes the type of its first use, so `placed_at > now() - @age` makes `@age` a `timestamp with time zone` and the server then reports `operator does not exist: timestamp with time zone > interval`, an operator the SQL does not hold; for `42883`, `42804` and `42725` with such a parameter the help names each one and says to cast it, `@age::interval`, or declare it, `-- param: @age interval`.  Run `dotnet test --project tests/SqlSource.Tests -- --filter-class "*DiagnosticsDocumentTests"`; expected PASS.

- [ ] **Step 6: Validate and commit**

Run: `./pre-commit-validation.sh`, expected exit 0.

```bash
git add src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs docs/diagnostics.md
git commit -m "Say what an inferred parameter may have done when the server rejects a query over a type"
```

---

### Task 2: The database `realistic`

**Files:**
- Create: `tests/SqlSource.Tests.Postgres/Realistic/schema.sql`
- Create: `tests/SqlSource.Tests.Postgres/Realistic/seed.sql`
- Modify: `tests/SqlSource.Tests.Postgres/RepositoryPaths.cs`
- Modify: `tests/SqlSource.Tests.Postgres/PostgresContainer.cs`
- Test: `tests/SqlSource.Tests.Postgres/Realistic/RealisticDatabaseTests.cs`

**Interfaces:**
- Produces: `RepositoryPaths.Realistic` (the folder), `RepositoryPaths.RealisticSchema`, `RepositoryPaths.RealisticSeed`, `RepositoryPaths.RealisticCases` (`Realistic/Cases`); `PostgresContainer.RealisticDatabase = "realistic"` and `PostgresContainer.RealisticConnectionString` (the container's connection string with `Database = realistic`, built with `NpgsqlConnectionStringBuilder`).

- [ ] **Step 1: Write the failing test** `RealisticDatabaseTests.Database_AfterStart_HasEveryTableAndTheSeed`, in the collection `UsesPostgres`: on `RealisticConnectionString`, `SELECT count(*) FROM accounts` is greater than 0, `SELECT count(*) FROM pg_catalog.pg_inherits WHERE inhparent = 'events'::regclass` is 2, and `SELECT count(*) FROM orders` on the `fixtures` connection is 0 (the shared database is untouched).

- [ ] **Step 2: Run it and see it fail.**  Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "*RealisticDatabaseTests"`.  Expected: FAIL, no member `RealisticConnectionString`.

- [ ] **Step 3: Write `schema.sql`** with a header comment like `schema.sql`'s that says what reads it.  These objects, which the corpus's queries are written against; add a column only when a case needs it:

| Object | Columns and constraints |
|----|----|
| schema `billing` | |
| domain `email_t` | `text CHECK (VALUE LIKE '%@%')` |
| domain `positive_amount` | `numeric(12,2) NOT NULL CHECK (VALUE > 0)`, a domain with its own `NOT NULL` |
| enum `account_status` | `'active', 'suspended', 'closed'` |
| `accounts` | `id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY`, `name varchar(100) NOT NULL`, `email email_t`, `status account_status NOT NULL DEFAULT 'active'`, `tags text[] NOT NULL DEFAULT '{}'`, `settings jsonb`, `created_at timestamptz NOT NULL DEFAULT now()`, `closed_at timestamptz` |
| `categories` | `id integer PRIMARY KEY`, `parent_id integer REFERENCES categories (id)`, `name text NOT NULL`, `slug text NOT NULL UNIQUE` |
| `products` | `id integer PRIMARY KEY`, `category_id integer REFERENCES categories (id)`, `sku text NOT NULL UNIQUE`, `name text NOT NULL`, `price numeric(12,2) NOT NULL`, `discontinued_on date` |
| `orders` | `id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY`, `account_id integer NOT NULL REFERENCES accounts (id)`, `placed_at timestamptz NOT NULL`, `shipped_at timestamptz`, `note text`, `coupon text` |
| `order_lines` | `order_id bigint NOT NULL REFERENCES orders (id)`, `line_no integer NOT NULL`, `product_id integer NOT NULL REFERENCES products (id)`, `quantity integer NOT NULL`, `unit_price numeric(12,2) NOT NULL`, `PRIMARY KEY (order_id, line_no)` |
| `billing.invoices` | `id integer PRIMARY KEY`, `order_id bigint NOT NULL REFERENCES orders (id)`, `amount positive_amount`, `issued_on date NOT NULL`, `paid_on date` |
| `events`, partitioned `BY RANGE (at)` | `id bigint NOT NULL`, `account_id integer NOT NULL`, `at timestamptz NOT NULL`, `kind text NOT NULL`, `payload jsonb`; partitions `events_2024` (`MINVALUE` to `'2025-01-01'`) and `events_2025` (`'2025-01-01'` to `MAXVALUE`) |
| `reservations` | `id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY`, `account_id integer NOT NULL REFERENCES accounts (id)`, `during tstzrange NOT NULL` |
| view `account_orders` | `SELECT a.id AS account_id, a.name, o.id AS order_id, o.placed_at FROM accounts a LEFT JOIN orders o ON o.account_id = a.id` |

- [ ] **Step 4: Write `seed.sql`.**  Fixed values, no `now()`, so every run sees the same rows.  It must hold at least: an account with no orders, no events and no reservations; one whose `email` and `settings` are `NULL` and one where they are not; a closed account; a category tree three deep with a root whose `parent_id` is `NULL`; a product with no category and one discontinued; an order with no lines, one with no invoice, one whose `note`, `coupon` and `shipped_at` are `NULL` and one where they are not; an invoice with `paid_on` `NULL`; events in both partitions, one account's events in one partition alone, and a `payload` missing a key another has; a reservation.  Cases added in Task 5 may add rows; they never remove these.

- [ ] **Step 5: Make the database.**  `PostgresContainer.InitializeAsync`, after the fixture schema: `CREATE DATABASE realistic` on the `fixtures` connection, then on `RealisticConnectionString` run `schema.sql` and then `seed.sql`, each read from `RepositoryPaths`.  The `catch` that disposes the container covers these too.  Add the four paths to `RepositoryPaths`.

- [ ] **Step 6: Run the test and see it pass**, same command.  Expected: PASS.

- [ ] **Step 7: Validate and commit.**  `./pre-commit-validation.sh`, exit 0.

```bash
git add tests/SqlSource.Tests.Postgres
git commit -m "Make a database of realistic shape for the describer's container tests"
```

---

### Task 3: The case harness

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Fixtures/FixtureReplay.cs` (make `Plan` public)
- Create: `tests/SqlSource.Tests.Postgres/Realistic/RealisticCase.cs`
- Create: `tests/SqlSource.Tests.Postgres/Realistic/RealisticCases.cs`
- Create: `tests/SqlSource.Tests.Postgres/Realistic/RealisticRun.cs`
- Create: `tests/SqlSource.Tests.Postgres/Realistic/RealisticOracle.cs`
- Create: `tests/SqlSource.Tests.Postgres/Realistic/Cases/aggregate-cte-left-joined/query.sql` and `case.json`, the first case
- Test: `tests/SqlSource.Tests.Postgres/Realistic/RealisticHarnessTests.cs`

**Interfaces:**
- Consumes: `PostgresContainer.RealisticConnectionString`, `RepositoryPaths.RealisticCases` (Task 2); `FixtureReplay.Plan(FixtureCase, CancellationToken) -> (PlannedQuery Query, SqlDialect Dialect)`, made `public` and otherwise unchanged, and `SampleSql.Build(SqlQuery)`.
- Produces:

```csharp
internal sealed record TruthColumn(string Name, bool Nullable, string? Why, string? Exempt);

internal sealed record RealisticCase(
    string Name, string Folder, int? MinimumMajor, string? Refusal,
    IReadOnlyList<TruthColumn> Columns, IReadOnlyList<IReadOnlyDictionary<string, string>> Samples)
{
    public const string QueryFile = "query.sql";
    public const string CaseFile = "case.json";
    public string ExpectedPath(int major);           // Folder/expected-pg{major}.txt
    public FixtureCase AsFixture { get; }            // new("postgres", Name, Folder)
    public override string ToString();               // "realistic/{Name}"
}

internal static class RealisticCases
{
    public static IReadOnlyList<RealisticCase> All { get; }          // every folder of Cases that holds a query.sql, by name
    public static RealisticCase Read(string folder);                 // throws InvalidOperationException naming the case and the fault
}

internal static class RealisticRun
{
    public static DescribeRequest RequestOf(RealisticCase found);    // as a run plans it: query.Name, SampleSql.Build(query), query.Parameters
    public static Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(RealisticCase found, string connection);
    public static string Render(Outcome<QueryDescription, DescribeFailure> described);
    public static Task<int> ServerMajorAsync(string connection);
}

internal sealed record OracleRun(IReadOnlyList<string> Names, IReadOnlyList<bool> SawNull, int Rows);

internal static class RealisticOracle
{
    public static string Substitute(string sql, IReadOnlyDictionary<string, string> sample, IReadOnlyDictionary<string, string> types);
    public static Task<OracleRun> RunAsync(string connection, string sql);   // in a transaction that is rolled back
}
```

`case.json` is, with every member optional but `columns` (absent for a refusal), and `samples` defaulting to one empty set:

```json
{
  "minimumMajor": 17,
  "refusal": "SQLSRC229",
  "columns": [
    { "name": "id", "nullable": false },
    { "name": "email", "nullable": true, "why": "accounts.email is nullable" },
    { "name": "spent", "nullable": true, "why": "...", "exempt": "the seed has no account whose ..." }
  ],
  "samples": [ { "limit": "10", "offset": "0" } ]
}
```

A sample's values are SQL literals as text, `"10"`, `"'%a%'"`, `"'{1,2}'"`, `"NULL"`, keyed by the parameter's name without `@`, compared ignoring case.  `Substitute` replaces each `Parameter` lexeme of `SqlLexer.Lex(sql, SqlDialectRules.PostgreSql)` with `({value})::{type}`, from the end of the text backwards; a parameter that the sample does not name is an `InvalidOperationException` naming it.

`Render` writes one line per fact, `\n` endings, a final line feed, and nothing else:

```
kind: rows
plan: walked
table-match: several-tables
parameter @limit: bigint (inferred)
column 0 id: integer | nullable false | catalog | public.accounts.id
column 3 spent: numeric | nullable null | no-origin | -
```

and for a refusal `failure: SQLSRC229`, one `argument: ...` line per argument and one `server: ...` line per server line.  `kind`, `plan` and `table-match` are written as the sidecar spells them; a missing value is `-`.

- [ ] **Step 1: Write the failing tests** in `RealisticHarnessTests`, in the collection `UsesPostgres`:
  - `Substitute_ParameterInAStringACommentOrAQuotedName_IsLeftAlone`: `Substitute("SELECT '@x', \"@x\", @x -- @x", {x: "1"}, {x: "integer"})` is `"SELECT '@x', \"@x\", (1)::integer -- @x"`.
  - `Substitute_TwoParameters_ReplacesEachWithItsCastLiteral`: `"WHERE a = @a AND b = @B"` with `{a: "'x'", b: "NULL"}` and types `{a: "text", b: "bigint"}` is `"WHERE a = ('x')::text AND b = (NULL)::bigint"`.
  - `Substitute_ParameterNotInTheSample_ThrowsNamingIt`: message contains `@c`.
  - `Read_FolderWithoutCaseJson_ThrowsNamingTheCase` and `Read_CaseJsonThatDoesNotParse_ThrowsNamingTheCase`, over folders made in a `TempFolder`.
  - `RunAsync_InsertReturning_LeavesTheTableAsItWas`: `INSERT INTO reservations (account_id, during) SELECT id, '[2024-01-01,2024-01-02)' FROM accounts RETURNING id` run twice through `RunAsync` on `RealisticConnectionString`; the count of `reservations` after equals the count before, and `Rows` equals the count of `accounts`.
  - `DescribeAsync_FirstCase_IsDescribedAndRendered`: the case `aggregate-cte-left-joined` describes with no failure, and `Render` of it starts with `"kind: rows\n"`.

- [ ] **Step 2: Run them and see them fail.**  Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "*RealisticHarnessTests"`.  Expected: FAIL to compile, the types do not exist.

- [ ] **Step 3: Write the first case.**  `aggregate-cte-left-joined/query.sql`: a `-- name:` marker and the spike's query 02 over this schema: a CTE of `orders` grouped by `account_id` with `sum` of its lines and `count(*)`, left-joined to `accounts`, filtered to accounts not closed, ordered by the sum, with `LIMIT @limit OFFSET @offset`.  Its `case.json` holds the truth of each column, with a `why` for each `true`, and one sample set.

- [ ] **Step 4: Implement the four types** with the signatures above.  `RealisticCases.Read` parses `case.json` with `System.Text.Json` (`JsonSerializer.Deserialize` into private DTOs, `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`) and fails for a missing `query.sql`, a missing or unreadable `case.json`, both `refusal` and `columns`, or neither.  `DescribeAsync` opens `new PostgresDescriber()` with `new OpenRequest("realistic", connection, LiveExchange.Instance, TimeProvider.System)`, describes `RequestOf(found)` and disposes the session.  `RunAsync` reads every row with `IsDBNull` per column, and rolls the transaction back in a `finally`.

- [ ] **Step 5: Run the tests and see them pass**, same command.  Expected: PASS.

- [ ] **Step 6: Validate and commit.**  `./pre-commit-validation.sh`, exit 0.

```bash
git add tests/SqlSource.Tool.Tests/Fixtures/FixtureReplay.cs tests/SqlSource.Tests.Postgres/Realistic
git commit -m "Describe, run and render a realistic case of the PostgreSQL describer"
```

---

### Task 4: The checks and the recording

**Files:**
- Create: `tests/SqlSource.Tests.Postgres/Realistic/RealisticQueryTests.cs`
- Create: `tools/record-realistic.sh`
- Create: `tests/SqlSource.Tests.Postgres/Realistic/Cases/aggregate-cte-left-joined/expected-pg18.txt` and `expected-pg14.txt`, recorded

**Interfaces:**
- Consumes: everything Task 3 produces.
- Produces: `RealisticQueryTests.RecordVariable = "SQLSOURCE_RECORD_REALISTIC"`; `RealisticQueryTests.Cases`, a `TheoryData<string>` of the case names; and the private helper `SkipUnlessTheServerRunsIt(RealisticCase, int major)`, which calls `Assert.Skip` with `"The case needs PostgreSQL {n} or later."` when `MinimumMajor > major`.

Five theories over `Cases`, each its own test so that a failure says which check failed.  Each describes the case afresh.

- [ ] **Step 1: Write the theories.**
  - `Describe_Case_IsRefusedOnlyAsItsCaseSays`: a case with a `refusal` fails with that id; one without describes with no failure, and the names of its columns equal the truth's in order, and their count is the truth's.
  - `Describe_Case_IsNotNullOnlyWhereTheTruthIs` (check 1): for each column `i`, when the truth at `i` is `nullable: true`, the description at `i` is not `Nullable == false`.  The message names the case, the column and its ordinal.
  - `Execute_Case_ReturnsNoNullWhereEitherSaysNotNull` (checks 2 and 3): for each sample set, `RunAsync(Substitute(request.Sql, sample, types))`, where `types` maps each described parameter's name to `Type.Name`.  `Names` equals the described names.  Over all the runs: a column described `false` saw no `NULL`; a column whose truth is `false` saw no `NULL`; a column whose truth is `true` and has no `exempt` saw a `NULL`.
  - `Describe_Case_IsItsExpectation` (check 4): on a major other than 14 and 18, `Assert.Skip("The expectations are recorded on PostgreSQL 14 and 18 alone.")`.  With `SQLSOURCE_RECORD_REALISTIC` set, write `Render(...)` to `ExpectedPath(major)` and return; on another major the recording fails with `"Record on PostgreSQL 14 or 18."`.  Otherwise `Render(...)` equals the file's text, and a missing file fails with the path and the script's name.
  - Every theory calls `SkipUnlessTheServerRunsIt` first; the refusal, safety and oracle theories skip a refused case where they have nothing to read.

- [ ] **Step 2: Write `tools/record-realistic.sh`** after `tools/record-fixtures.sh`: no arguments, the image from `PostgresContainer.cs` or `SQLSOURCE_TESTS_POSTGRES_IMAGE`, `SQLSOURCE_RECORD_REALISTIC=1 dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-method "*Describe_Case_IsItsExpectation"`, and the same messages.  Recording both versions is two runs, the second with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14`.

- [ ] **Step 3: Record the first case on both versions and read the two files.**  Run `tools/record-realistic.sh`, then `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 tools/record-realistic.sh`.  Expected: `done` twice, and `expected-pg18.txt` and `expected-pg14.txt` in the case's folder.

- [ ] **Step 4: Run the suite on both versions and see it pass.**  `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "*RealisticQueryTests"`, then the same with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14`.  Expected: PASS.  Then break the truth of one `false` column of the first case to `true` with no `exempt`, see `Execute_Case_ReturnsNoNullWhereEitherSaysNotNull` fail naming it, and put it back.

- [ ] **Step 5: Validate and commit.**  `./pre-commit-validation.sh`, exit 0; it runs shellcheck and shfmt over the new script.

```bash
git add tests/SqlSource.Tests.Postgres/Realistic tools/record-realistic.sh
git commit -m "Check each realistic case against its truth, the seed and its recording"
```

---

### Task 5: The corpus

**Files:**
- Create: one folder per case under `tests/SqlSource.Tests.Postgres/Realistic/Cases/`, each with `query.sql`, `case.json`, `expected-pg18.txt`, `expected-pg14.txt` (not for a case whose `minimumMajor` is above 14)
- Modify: `tests/SqlSource.Tests.Postgres/Realistic/seed.sql`, where a case needs rows
- Modify: `src/SqlSource.Tool/Describing/Postgres/*` only for a bug a case finds (Global Constraints)

**Interfaces:**
- Consumes: Task 3's `case.json` format and Task 4's theories and script.

- [ ] **Step 1: Write the cases.**  At least 40 with the first one, each a query a person might have in an application, written fresh against this schema and not copied from the existing fixtures.  Every query of the spike appendix is in it in some form.  These names, at least, one case each; a name says the shape, and the spec's table says what each row covers:

| Area | Cases |
|----|----|
| Unions | `union-all-activity-feed`, `union-distinct-search-ordered-limited`, `intersect-accounts-with-orders-and-events`, `except-accounts-without-reservations`, `union-mixed-types` (`integer` with `bigint`, `varchar(100)` with `text`), `union-in-cte-joined`, `union-branch-literal` |
| CTEs | `recursive-category-tree`, `cte-used-twice`, `cte-materialized`, `cte-not-materialized`, `cte-chain`, `aggregate-cte-joined-inner`, `data-modifying-cte-archive` |
| Subqueries | `scalar-subquery-last-order`, `in-subquery-buyers`, `exists-as-column`, `distinct-derived-table`, `row-number-dedupe` |
| `LATERAL` | `lateral-latest-order`, `left-lateral-latest-order`, `unnest-tags-with-ordinality`, `jsonb-array-elements` |
| Joins | `order-report-four-tables`, `right-join-orders-accounts`, `full-join-accounts-reservations`, `category-parent-self-join`, `view-joined-left`, `partition-preserved-side`, `partition-nullable-side` |
| Grouping | `group-by-having`, `count-filter-statuses`, `rollup-by-status`, `distinct-on-latest-per-account` |
| Windows | `running-total-lag-rank` |
| Expressions | `coalesce-case-greatest`, `json-build-and-agg`, `array-and-string-agg` |
| Writes | `upsert-product-returning`, `insert-select-returning`, `insert-multirow-values`, `insert-from-unnest-arrays`, `update-from-aggregate-returning`, `delete-using-returning`, `merge-returning` (`"minimumMajor": 17`) |
| Parameters | `any-ids-and-enum-array`, `search-ilike-optional-filter-paged` (the optional filter's parameter declared `-- param: @status account_status null`, with a sample set where it is `NULL`), `reservations-in-range`, `parameter-in-cte`, `jsonb-containment-and-key` |
| Statements | `table-statement`, `values-statement` |
| Refusals | `array-agg-of-row` (`SQLSRC229`), `interval-without-cast` (`SQLSRC225` with Task 1's help in its expectation), `parameter-of-two-types` (`SQLSRC225`) |

Each `case.json`'s truth is reasoned from the SQL and `schema.sql`, with a `why` on each `true`.  Where the seed cannot show a `NULL` that the truth says can happen, add rows to `seed.sql` first; an `exempt` is for what no seed can show, and says why.

- [ ] **Step 2: Record and read.**  Run `tools/record-realistic.sh` and the same with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14`.  Read every `expected-pg*.txt` against its `case.json`.

- [ ] **Step 3: Run the suite on both versions.**  `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "*RealisticQueryTests"`, and with `postgres:14`.  Expected: PASS.  A failure of the refusal, safety or oracle theory is either a truth written wrong, which is mended in `case.json` with its `why`, or a bug of the describer, which is fixed here by the test-first cycle of `superpowers:test-driven-development` with the case as its test, or, if large, reported to the user before going on.

- [ ] **Step 4: Validate and commit**, in as many commits as there are coherent groups of cases, and one commit per bug fixed with the case that found it.  `./pre-commit-validation.sh` before each.

```bash
git add tests/SqlSource.Tests.Postgres/Realistic
git commit -m "Add the realistic cases of <area>"
```

---

### Task 6: The noise budget, the documents and the pull request

**Files:**
- Create: `tests/SqlSource.Tests.Postgres/Realistic/RealisticNoiseBudgetTests.cs`
- Modify: `tests/AGENTS.md`, `CONTRIBUTING.md`
- Test: the new class

**Interfaces:**
- Consumes: `RealisticCases.All`, `RealisticRun` (Task 3).

- [ ] **Step 1: Write the test** `Noise_OverTheCorpus_IsTheBudget`, in the collection `UsesPostgres`.  It describes every case the server runs that is not a refusal, and counts the columns whose truth is `nullable: false` and whose description is `true` or `null`.  It holds `private const int Budget14` and `private const int Budget18`, skips on any other major with `"The budget is held for PostgreSQL 14 and 18 alone."`, and asserts the count equals the version's budget.  Above it, the message is `"The noise is {count}, over the budget of {budget}: a change made the describer answer nullable or unknown where it answered not null; the cases that changed are in their expected-pg{major}.txt."`  Below it, `"The noise is {count}, under the budget of {budget}: lower Budget{major} to {count} in this change, so the improvement is kept."`  A comment says the number is raised only in a change that adds cases, and by how much and why in its commit.

- [ ] **Step 2: Run it on both versions** with both budgets at 0 and read the counts from the failures.  Expected: FAIL, "over the budget of 0", with the corpus's counts, about 40 if the spike is a guide.

- [ ] **Step 3: Set the two budgets** to those counts, run on both versions, and see it pass.

- [ ] **Step 4: Document it.**  `tests/AGENTS.md`: a bullet that `tests/SqlSource.Tests.Postgres/Realistic/` holds the realistic corpus in its own database `realistic`; that each `case.json` is the truth, written by hand from the SQL and the schema and never from the describer's output; that `tools/record-realistic.sh` writes only `expected-pg*.txt`; that the noise budget is lowered whenever the describer improves and raised only with cases; and that a case's `exempt` needs a reason.  `CONTRIBUTING.md`: in the row of `tests/SqlSource.Tests.Postgres`, the second database and the corpus; beside the paragraph on `tools/record-fixtures.sh`, how to record the corpus on 18 and 14 and when.

- [ ] **Step 5: Validate and commit.**  `./pre-commit-validation.sh`, exit 0.

```bash
git add tests/SqlSource.Tests.Postgres/Realistic tests/AGENTS.md CONTRIBUTING.md
git commit -m "Hold the realistic corpus's noise to a budget, and say how to keep it"
```

- [ ] **Step 6: Open the pull request.**  Check the version as the Global Constraints say.  Push the branch, open the pull request against `main` with a description that lists the corpus's areas, the budgets, any bug found and fixed, and the specs; end it with the attribution line the session gives.  Turn on Auto-fix for it.
