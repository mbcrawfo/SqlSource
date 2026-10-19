# Query generation, phase 5.5: the PostgreSQL examples - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `examples/PostgresExamples`, a small class library built with the solution, shows what a user writes and gets for each output, `Sql`, `Models` and `CodeGen`, with a Docker Compose file that starts its database, committed sidecars written by `tools/describe-end-to-end.sh`, a README of a user's workflow, and guidance that keeps it current.

**Architecture:** The project imports the package's props and targets by path and references the generator as an analyzer, as `tests/SqlSource.Tests.Postgres` does, with a comment saying that a user writes a `PackageReference` instead.  Each of its three folders holds `.sql` files claimed by one class whose attribute sets the folder's output; each class runs the generated SQL through Dapper and compiles, and runs nothing.  `tools/describe-end-to-end.sh` gains a third database on its container, `examples`, from the example's `schema.sql`, and a second `describe --force` run on the example's project.

**Tech Stack:** C# on `net10.0`, Npgsql 10.0.3, Dapper 2.1.89, Docker Compose, bash.

**Spec:** [`docs/superpowers/specs/2026-10-18-query-generation-phase-5-5-examples-design.md`](../specs/2026-10-18-query-generation-phase-5-5-examples-design.md), within the [query generation epic](../specs/2026-10-07-query-generation-epic-design.md).

## Global Constraints

- Every commit passes `./pre-commit-validation.sh`, run on the exact tree committed.  Every push needs a passing run on the exact tree pushed.  Task 3 changes `tools/describe-end-to-end.sh`, so the push also needs `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres`.
- One commit per step of the spec's order of work: five commits, one per task below.  The pull request keeps to 150 files or fewer; this plan counts about 25.
- The examples have no tests and run nothing; they build with the solution (`SqlSource.slnx`).
- Every committed sidecar is written by `tools/describe-end-to-end.sh`, never by hand.
- The example's project file, queries and code are what the manual recommends, so copying them is right.  Anything only the repository needs is marked as such in the project file and the README.
- `TreatWarningsAsErrors` and the repository's analyzers stay on for the project.  `SQLSRC240` is in its `WarningsNotAsErrors`.
- The Compose file's image is `postgres:18.6`, the tests' default image, and joins the list of rule 5 of `AGENTS.md`.
- Shared files that phase 5.6 also touches (`tools/describe-end-to-end.sh`, `tests/SqlSource.Tests.Postgres/README.md`) change by as few lines as the task needs.  Merge `main` before each push.
- A valid minor review finding is fixed, or the owner is asked whether to record it as tech debt.  It is never deferred silently.

## Where this plan follows the merged code rather than the spec

The spec was written before 5.3 and 5.4 merged.  Where the merged code and the manual's Dapper recipe (`docs/manual/recipes.md#models-through-dapper`) say otherwise, the examples follow them, since the spec's anti-goal forbids an example that teaches what a user should not do:

1. **A column made not null with a `-- column:` marker, not with `!`.**  The spec's `Models/` row asks for "a column made not null with `!`".  5.3 added the marker because a `!` stays in the column's name, so Dapper matches no property to it, and every query of the examples is read through Dapper.  `ListOrderTotals` writes `-- column: total not null`, and the example shows no `!`.
2. **Parameters in camelCase**, `@orderId`, since Dapper binds a property of the input model only to a parameter of its name (5.4, D-0004).
3. **The class model is the query with an array column.**  The spec asks for "one record model and one class model"; 5.4 found that Dapper cannot construct a record from a row with an array, so the class model is `GetOrderTags`, with `-- output-model-type: class` and the column `tags text[]`.
4. **`SQLSRC240` in `WarningsNotAsErrors`**, as the spec says and as `tests/SqlSource.Tests.Postgres` does.
5. **A Dapper type handler for the type override.**  The spec's project-level override `timestamptz=System.DateTimeOffset` needs, by the recipe, a handler and `DefaultTypeMap.MatchNamesWithUnderscores`.  The project holds `DapperSetup.cs` and `UtcDateTimeOffsetHandler.cs` at its root, which the spec's folder tree does not list.
6. **The logical database is the default, `postgres`.**  The project sets no `SqlSourceDatabase`, so a run has one database and the README's `SQLSOURCE_CONNECTION` works, as the spec wants.  The script connects that name to its database `examples`.
7. **The epic outline already names the examples** in the rows and scope of phases 6, 7 and 8, and in its Documentation section; Task 4 checks those and changes them only if one is missing.

## Review Focus

- **A user copies the project file.**  Its comments must say which lines are the repository's (the analyzer reference, the two imports) and what a user writes instead; the README shows the `PackageReference` lines with versions.
- **The Compose database and the script's database differ.**  The Compose file runs `schema.sql` as the user `examples` in the database `examples`; the script runs it as `postgres`.  `schema.sql` must not name an owner or a role, so that both give one schema and the sidecars the README's command writes match the committed ones.
- **The port is taken.**  The README says how to change the host port in one place, the Compose file, and that the connection string changes with it.
- **A schema change without the script.**  `examples/AGENTS.md` says that a change to `schema.sql` is followed by the script, and the script's header says it writes the examples' sidecars.
- **`docker compose up` on a volume of an older schema.**  The init script runs only on an empty volume; the README says `docker compose down -v` to start again.

---

### Task 1: The empty project in the solution

**Files:**
- Create: `examples/PostgresExamples/PostgresExamples.csproj`
- Create: `examples/PostgresExamples/packages.lock.json` (by `dotnet restore`)
- Modify: `SqlSource.slnx` (a `/examples/` folder with the project)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 5.5: `In progress`)
- Create: this plan, committed with the first step

**Interfaces:**
- Produces: the project, root namespace `PostgresExamples`, which Tasks 2 and 3 fill.

- [ ] **Step 1: Write the project file**

A class library: `TargetFramework` `net10.0`, `IsPackable` `false`, `SqlSourceDialect` `postgres`, `WarningsNotAsErrors` `$(WarningsNotAsErrors);SQLSRC240` (comment: raising `VersionPrefix` warns until `tools/describe-end-to-end.sh` runs), and `SqlSourceTypeOverrides` `timestamptz=System.DateTimeOffset`.  `PackageReference` to `Dapper` and `Npgsql`, with no version (central package management).  The generator as `ProjectReference` to `../../src/SqlSource/SqlSource.csproj` with `OutputItemType="Analyzer" ReferenceOutputAssembly="true"`, and `SqlSource.props` imported first and `SqlSource.targets` last, by path, as in `tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`.  A comment at the top says: the repository references the generator and imports its MSBuild files by path because no package is published; a user writes `<PackageReference Include="SqlSource" Version="..." />` instead, and gives Dapper and Npgsql a version, which here come from `Directory.Packages.props`; `Directory.Build.props` sets the repository's own analyzers and `TreatWarningsAsErrors`, which a user's project does not need.

- [ ] **Step 2: Add it to the solution and restore**

Run: `dotnet sln SqlSource.slnx add examples/PostgresExamples/PostgresExamples.csproj --solution-folder examples` (or edit the `.slnx` by hand to add `<Folder Name="/examples/">`), then `dotnet restore SqlSource.slnx`.
Expected: `examples/PostgresExamples/packages.lock.json` is written.

- [ ] **Step 3: Set the epic's row 5.5 to `In progress`**

- [ ] **Step 4: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

```bash
git add SqlSource.slnx examples/PostgresExamples docs/superpowers/specs/2026-10-07-query-generation-epic-design.md docs/superpowers/plans/2026-10-19-query-generation-phase-5-5-examples.md
git commit -m "Add the empty PostgresExamples project to the solution"
```

### Task 2: The schema and the Compose file

**Files:**
- Create: `examples/PostgresExamples/schema.sql`
- Create: `examples/PostgresExamples/docker-compose.yml`
- Modify: `AGENTS.md` (rule 5's list gains `examples/PostgresExamples/docker-compose.yml`)
- Modify: `tests/SqlSource.Tests.Postgres/README.md` (the image section: moving to a new minor also updates the Compose file's image)

**Interfaces:**
- Produces: the tables and columns Task 3's queries read:
  - `users (id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY, name text NOT NULL, email text NOT NULL, created_at timestamptz NOT NULL DEFAULT now())`
  - `CREATE TYPE order_status AS ENUM ('pending', 'shipped', 'cancelled')`
  - `orders (id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY, user_id integer NOT NULL REFERENCES users (id), status order_status NOT NULL DEFAULT 'pending', placed_at timestamptz NOT NULL, shipped_at timestamptz, tags text[] NOT NULL DEFAULT '{}')`
  - `order_lines (order_id integer NOT NULL REFERENCES orders (id), line_number integer NOT NULL, product text NOT NULL, quantity integer NOT NULL, unit_price numeric(10, 2) NOT NULL, PRIMARY KEY (order_id, line_number))`
  - A few rows of each: two users, three orders (one shipped, so `shipped_at` has both a value and `NULL`), and lines for each order.  Rows insert by `OVERRIDING SYSTEM VALUE` or by their identity's order; either way a reader can tell which order is which.
- Compose: service `postgres`, image `postgres:18.6`, `POSTGRES_DB`, `POSTGRES_USER` and `POSTGRES_PASSWORD` all `examples`, port `127.0.0.1:55432:5432`, `./schema.sql:/docker-entrypoint-initdb.d/schema.sql:ro`, a `pg_isready` health check.  The connection string Task 4's README gives is `Host=localhost;Port=55432;Database=examples;Username=examples;Password=examples`.

- [ ] **Step 1: Write `schema.sql`**

A header comment says the Compose file runs it on the database's first start, and `tools/describe-end-to-end.sh` on its own container, and that a change to it is followed by a run of the script.  No `OWNER`, role or database name in it.

- [ ] **Step 2: Write `docker-compose.yml`** with a comment that the credentials are for the example alone and that the image is the tests' default, which rule 5 of `AGENTS.md` keeps in step.

- [ ] **Step 3: Check that the database starts with the schema**

Run: `docker compose -f examples/PostgresExamples/docker-compose.yml up -d --wait && docker compose -f examples/PostgresExamples/docker-compose.yml exec postgres psql -U examples -d examples -c 'SELECT count(*) FROM order_lines' && docker compose -f examples/PostgresExamples/docker-compose.yml down -v`
Expected: the count of the seeded lines, then the container and its volume removed.

- [ ] **Step 4: Add the Compose file to rule 5 of `AGENTS.md`** (the sentence that lists where the default image's tag is recorded) **and to the image section of `tests/SqlSource.Tests.Postgres/README.md`** (moving to a new minor also updates the Compose file's image).  One clause each.

- [ ] **Step 5: Validate and commit**

Run: `./pre-commit-validation.sh` — every step `passed`.

```bash
git add examples/PostgresExamples AGENTS.md tests/SqlSource.Tests.Postgres/README.md
git commit -m "Give the PostgreSQL examples a schema and a Compose file"
```

### Task 3: The queries, the classes and the sidecars

**Files:**
- Create: `examples/PostgresExamples/DapperSetup.cs`, `examples/PostgresExamples/UtcDateTimeOffsetHandler.cs`
- Create: `examples/PostgresExamples/Sql/Counts.sql`, `examples/PostgresExamples/Sql/CountQueries.cs`
- Create: `examples/PostgresExamples/Models/Orders.sql`, `examples/PostgresExamples/Models/Users.sql`, `examples/PostgresExamples/Models/StoreQueries.cs`
- Create: `examples/PostgresExamples/CodeGen/OrderLines.sql`, `examples/PostgresExamples/CodeGen/OrderLineQueries.cs`
- Create (by the script): `Models/Orders.sql.json`, `Models/Users.sql.json`, `CodeGen/OrderLines.sql.json`
- Modify: `tools/describe-end-to-end.sh`

**Interfaces:**
- Consumes: Task 2's tables.
- Produces: the class and query names Task 4's README names.

The queries, each in camelCase parameters, each with a `-- summary:`:

| File | Query | Shows |
|----|----|----|
| `Sql/Counts.sql` | `CountUsers`: `SELECT count(*) FROM users;` | A constant |
| `Sql/Counts.sql` | `CountRows`: `SELECT count(*) FROM {{table}};` | A token method, with no default, which `sql` output allows |
| `Models/Orders.sql` | `GetOrder`: `o.id, o.status, o.placed_at, o.shipped_at, u.name AS customer_name` from `orders o JOIN users u`, `WHERE o.id = @orderId` | Input and output models, a sealed record; a nullable column; `status` read as `string`; `placed_at` as `DateTimeOffset` by the override |
| `Models/Orders.sql` | `ListOrderTotals`: `-- column: total not null`, `o.id, sum(l.quantity * l.unit_price) AS total`, `GROUP BY o.id ORDER BY o.id` | The marker, so `Total` is `decimal` |
| `Models/Orders.sql` | `GetOrderTags`: `-- output-model-type: class`, `o.id, o.tags`, `WHERE o.id = @orderId` | The class model, needed for the array |
| `Models/Users.sql` | `GetUser`: `SELECT * FROM users WHERE id = @userId;` | A table model, `UsersDto` |
| `CodeGen/OrderLines.sql` | `ListOrderLines -> many`: `line_number, product, quantity, unit_price` of `@orderId`, `ORDER BY line_number` | The default output and a result shape; the file's header comment says that until query methods arrive it gives what `Models` gives |

- [ ] **Step 1: Write `DapperSetup` and `UtcDateTimeOffsetHandler`**

`public static class DapperSetup` with `public static NpgsqlDataSource CreateDataSource(string connectionString)`, which, once per process (a static constructor or a `Lazy`), sets `DefaultTypeMap.MatchNamesWithUnderscores = true` and adds `UtcDateTimeOffsetHandler`, then returns `NpgsqlDataSource.Create(connectionString)`.  `UtcDateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>` is the recipe's handler, with the `DateTimeOffset` pass-through and the `InvalidCastException` arm of `tests/SqlSource.Tests.Postgres/EndToEnd/Types/UtcDateTimeOffsetHandler.cs`.  Comments say why each exists, in a sentence, and link the recipe.

- [ ] **Step 2: Write the `.sql` files** of the table above.

- [ ] **Step 3: Write the three classes**, each `public sealed partial class <Name>(DbConnection connection)` in its folder's namespace, with its attribute:
  - `CountQueries`: `[SqlSourceGenerate(Output = GeneratorOutput.Sql)]`; `Task<long> CountUsersAsync()` and `Task<long> CountRowsAsync(string table)` through `ExecuteScalarAsync<long>`.  The token's comment says to pass only a table name the code chooses.
  - `StoreQueries`: `[SqlSourceGenerate(Output = GeneratorOutput.Models)]`; `Task<GetOrderDto?> GetOrderAsync(int orderId)` with `new GetOrderParams(orderId)`, `Task<IEnumerable<ListOrderTotalsDto>> ListOrderTotalsAsync()`, `Task<GetOrderTagsDto?> GetOrderTagsAsync(int orderId)`, `Task<UsersDto?> GetUserAsync(int userId)`.
  - `OrderLineQueries`: `[SqlSourceGenerate]`, the default output; `Task<IEnumerable<ListOrderLinesDto>> ListOrderLinesAsync(int orderId)`, with a comment that phase 8's generated method will replace it.

  Methods return Dapper's task directly, with no `await`.  A type name the generator writes that differs from the above (a suffix, a table model's name) is taken from the generated code, and Task 4's README uses what was built.

- [ ] **Step 4: Extend `tools/describe-end-to-end.sh`**

After the end-to-end run: create the database `examples` on the same container, apply `examples/PostgresExamples/schema.sql` with `psql`, delete `examples/PostgresExamples/**/*.sql.json`, and run `dotnet run --project src/SqlSource.Tool -- describe --force examples/PostgresExamples/PostgresExamples.csproj --connection "postgres=Host=127.0.0.1;Port=$port;Username=postgres;Password=$PASSWORD;Database=examples"`.  The header comment names the examples among what it writes and the files whose change calls for a run.  Keep the change to a block of its own after the existing lines, so that it merges beside 5.6's.

- [ ] **Step 5: Run the script**

Run: `tools/describe-end-to-end.sh`
Expected: both runs report every query described and none failed; `git status` shows the three new sidecars under `examples/` and no change to `tests/SqlSource.Tests.Postgres/EndToEnd/Queries/` (other than none).

- [ ] **Step 6: Build and check the user's way**

Run: `dotnet build examples/PostgresExamples` — no warning, no error.
Run: `docker compose -f examples/PostgresExamples/docker-compose.yml up -d --wait`, then `SQLSOURCE_CONNECTION='Host=localhost;Port=55432;Database=examples;Username=examples;Password=examples' dotnet run --project src/SqlSource.Tool -- describe --check examples/PostgresExamples/PostgresExamples.csproj`, then `docker compose ... down -v`.
Expected: the check finds every entry the same, which proves the Compose database matches the script's.

- [ ] **Step 7: Validate and commit**, then also run `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres` (the script changed).

```bash
git add examples/PostgresExamples tools/describe-end-to-end.sh
git commit -m "Add the queries and sidecars of the PostgreSQL examples"
```

### Task 4: The README and the guidance for agents

**Files:**
- Create: `examples/PostgresExamples/README.md`
- Create: `examples/AGENTS.md`
- Modify: `AGENTS.md` (rule 4 gains an `examples/` item)
- Modify: `docs/manual/AGENTS.md` ("When a phase lands" gains a step)
- Modify: `Directory.Packages.props` (a comment on `Dapper` and `Npgsql`: the example's README lists their versions)
- Check: the epic outline's phases 6, 7 and 8 and its Documentation section name the examples

- [ ] **Step 1: Write `examples/PostgresExamples/README.md`**

Sections: what the example is and what each folder shows (the table of Task 3, by name); what a project of your own writes instead of the repository's lines (the three `PackageReference`s with versions: `SqlSource` as `--prerelease`, Dapper `2.1.89`, Npgsql `10.0.3`); start the database (`docker compose up -d`, the credentials are the example's alone, the port, `down -v` to start again); describe the queries (the user's `SQLSOURCE_CONNECTION=... dotnet sqlsource describe`, then the repository's `dotnet run --project src/SqlSource.Tool -- describe`, marked as the repository's, and why: no published tool is pinned, and another version writes another `toolVersion`; and that the committed sidecars come from `tools/describe-end-to-end.sh`); open a sidecar; build; see the generated code (the IDE's generated files, or `EmitCompilerGeneratedFiles`).  Links to the manual are relative (`../../docs/manual/...`), and to the repository's `README.md`.

- [ ] **Step 2: Write `examples/AGENTS.md`**, rules only, under 250 lines: what each example project shows and does not (no tests, nothing runs, no coverage); keep each small; one folder per output, its class's attribute setting the output; what the manual recommends and nothing else, Dapper's rules included; the project file references the generator as the tests do, with its comment, and the README's version list follows `Directory.Packages.props`; the sidecars come from `tools/describe-end-to-end.sh`, never by hand, and a change to a query or `schema.sql` is followed by it; the Compose image follows rule 5; a new database gets `<Engine>Examples` by this pattern; a phase that delivers a feature adds its example; the maintenance footer.

- [ ] **Step 3: Add the `examples/` item to rule 4 of `AGENTS.md`**: a change to what a user writes, gets or sees updates the examples that show it, in the same PR; a phase of query generation that delivers a feature adds an example of it to each example project it applies to; the examples build with the solution, and their sidecars are written by `tools/describe-end-to-end.sh`.  Add a step to "When a phase lands" in `docs/manual/AGENTS.md` that points to it.

- [ ] **Step 4: Check the epic outline** for phases 6, 7 and 8 and its Documentation section; add the examples where one is missing.

- [ ] **Step 5: Validate and commit**

```bash
git add examples AGENTS.md docs/manual/AGENTS.md Directory.Packages.props docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
git commit -m "Document the PostgreSQL examples and keep them current"
```

### Task 5: The documents, and the epic's row set to Done

**Files:**
- Modify: `README.md` (a line under the getting started's end or "other ways to use it", linking `https://github.com/mbcrawfo/SqlSource/tree/main/examples/PostgresExamples`, absolute, since it is packed)
- Modify: `CONTRIBUTING.md` (one sentence for the project; the script's sentence covers the examples)
- Modify: `tools/README.md` (the script's paragraph: also the examples' sidecars, the database `examples`)
- Modify: `tests/SqlSource.Tests.Postgres/README.md` ("Writing the sidecars": the script also writes the examples')
- Modify: `docs/publishing.md` (where it says the script writes the end-to-end sidecars, if it names them)
- Modify: `docs/manual/README.md` ("Find what you need": "See a complete project" → `../../examples/PostgresExamples/README.md`, relative, as the manual's links are)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 5.5 `Done`, its Delivers cell as built)

- [ ] **Step 1: Make the edits above.**  The README line and the manual row describe effects, never internals; run the grep of `docs/manual/AGENTS.md` over each changed user page.

- [ ] **Step 2: Validate and commit**

```bash
git add README.md CONTRIBUTING.md tools/README.md tests/SqlSource.Tests.Postgres/README.md docs
git commit -m "Link the PostgreSQL examples and close phase 5.5"
```
