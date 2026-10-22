# Query generation, phase 6.7: the SQL Server examples and the close - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `examples/SqlServerExamples`, a small class library built with the solution, shows what a SQL Server user writes and gets for each output, with a Compose file that starts its database and applies its schema, sidecars written by `tools/describe-end-to-end.sh` and a README of a user's workflow; the user documentation of SQL Server is read whole; phase 6 is closed.

**Architecture:** The project copies the pattern of `examples/PostgresExamples`: the package's props and targets imported by path, the generator as an analyzer, one folder per output, each with one class that runs the generated SQL through Dapper and runs nothing.  Its Compose file has two services on the default SQL Server image: the server, and a one-shot service that, once the server is healthy, creates the database `examples` when it is missing and applies `schema.sql` to it with the image's `sqlcmd -I`.  `tools/describe-end-to-end.sh`'s SQL Server part gains the examples after the test project, as its PostgreSQL part has them.

**Tech Stack:** C# on `net10.0`, Microsoft.Data.SqlClient 7.1.1, Dapper 2.1.89, Docker Compose, bash, Markdown.

**Spec:** [`docs/superpowers/specs/2026-10-20-query-generation-phase-6-7-examples-and-close-design.md`](../specs/2026-10-20-query-generation-phase-6-7-examples-and-close-design.md), within the [query generation epic](../specs/2026-10-07-query-generation-epic-design.md), "Phase 6 - SQL Server".  It follows [sub-phase 5.5's plan](2026-10-19-query-generation-phase-5-5-examples.md).

## Global Constraints

- Every commit passes `./pre-commit-validation.sh`, run on the exact tree committed; every push needs a passing run on the exact tree pushed.  Task 2 changes `tools/describe-end-to-end.sh` and no describer or its tests, so no floor-image run is required; run `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/SqlSource.Tests.SqlServer` anyway before the first push, since the script rewrites that project's sidecars.
- One pull request, one commit per task, five commits; about 40 files, under the limit of 150.
- The example has no tests and runs nothing; it builds with the solution (`SqlSource.slnx`, folder `/examples/`).
- Every committed sidecar is written by `tools/describe-end-to-end.sh`, never by hand.
- The project file, the queries and the code are what the manual recommends (`docs/manual/recipes.md#models-through-dapper` and `#dapper-with-sql-server`); anything only the repository needs is marked so in the project file and the README.
- `SqlSourceDialect` is `mssql`; no `SqlSourceDatabase`, so the queries belong to the default logical database `mssql` and a run has one database.
- The Compose image is `DefaultImages.SqlServer`, `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04`, `platform: linux/amd64`.
- `schema.sql` names no owner, role or database.
- The README's connection holds `TrustServerCertificate=True` and says why.
- User documents describe effects, never internals; run `docs/manual/AGENTS.md`'s internals grep over every user page changed.
- A finding of the read-through that is the code's is recorded in `docs/tech-debt`, or, if small, fixed only after the implementation manager relays the owner's agreement.
- No minor review finding is deferred silently.

## Where this plan follows the merged code rather than the spec

1. **The script's variables.**  The spec says the script writes the example's sidecars "with `EXAMPLES` and its connection".  `EXAMPLES` already names `examples/PostgresExamples`, so the plan renames it `POSTGRES_EXAMPLES` and adds `SQLSERVER_EXAMPLES`; `tools/AGENTS.md`'s rule that names `EXAMPLES` changes with it.
2. **The schema service runs only on a new database.**  The spec's service creates `examples` if it does not exist and applies `schema.sql`.  Applied a second time, `schema.sql`'s `CREATE TABLE`s fail, so the service applies it only in the run that created the database, and exits 0 otherwise.  The data lives in a named volume, so `docker compose down -v` starts again from nothing, as PostgreSQL's README says.
3. **Phase 6 has no architecture placeholder left.**  6.1 to 6.3 replaced theirs (`docs/architecture/sqlserver-describer.md`, `tool.md`); `grep -rn "Not built yet" docs/architecture` names phases 7 to 10 alone.  Task 5 checks again and fills any it finds.
4. **The phase map's row of 5 is gone**, so row 6 is removed the same way, and the phase 6 finder under the table with it; what phase 7 needs from it moves into row 7.

## Review Focus

- **`docker compose up` twice.**  The second `up` must not fail on the existing tables: the schema service sees the database and exits 0 (Task 1, Step 4).
- **Waiting for the schema.**  A user who runs the tool right after `up` while the schema service has not finished gets "invalid object name"; the README's command must return only once the schema is applied (Task 1, Step 4 picks and proves it).
- **The Compose database and the script's differ.**  The Compose service applies `schema.sql` as `sa` to `examples` created by `sqlcmd`; the script does the same on its own container; the user's `--check` against the Compose database must find every entry the same (Task 2, Step 6).
- **Apple silicon.**  The image is x64 alone; the Compose file names `platform: linux/amd64` and the health check gives the emulated server time to start.
- **A user copies `TrustServerCertificate=True` to production.**  The README says it is for the container's own certificate and that a real server's connection verifies its certificate.

---

### Task 1: The project, its Compose file, its schema and its README

**Files:**
- Create: `examples/SqlServerExamples/SqlServerExamples.csproj`, `packages.lock.json` (by restore), `docker-compose.yml`, `schema.sql`, `README.md`
- Modify: `SqlSource.slnx` (the project under `/examples/`)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 6.7: `In progress`)
- Create: this plan, committed with the first step

**Interfaces:**
- Produces: root namespace `SqlServerExamples`; the tables Task 2 reads:
  - `users (id int IDENTITY PRIMARY KEY, name nvarchar(100) NOT NULL, email nvarchar(200) NOT NULL, created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME())`
  - `orders (id int IDENTITY PRIMARY KEY, user_id int NOT NULL REFERENCES users (id), status varchar(20) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'shipped', 'cancelled')), placed_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), shipped_on date NULL)`
  - `order_lines (order_id int NOT NULL REFERENCES orders (id), line_number int NOT NULL, product nvarchar(100) NOT NULL, quantity int NOT NULL, unit_price decimal(10, 2) NOT NULL, PRIMARY KEY (order_id, line_number))`
  - Rows: two users, three orders (one shipped, so `shipped_on` holds a value and a `NULL`), lines for each.  No `GO` is needed; if one is, the script's `sqlcmd` and the Compose service both split on it.
- Compose: service `sqlserver` (image above, `platform: linux/amd64`, `ACCEPT_EULA: "Y"`, `MSSQL_SA_PASSWORD` a fixed password of three kinds of character, port `127.0.0.1:51433:1433`, named volume on `/var/opt/mssql`, a health check by `/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P ... -Q "SELECT 1"` with a start period for emulation); service `schema` (same image and platform, `depends_on: sqlserver: condition: service_healthy`, `restart: "no"`, `schema.sql` mounted read-only at `/schema.sql`, a bash command that, when `DB_ID(N'examples')` is null, runs `sqlcmd -C -I -b -S sqlserver -U sa -P ... -Q "CREATE DATABASE examples"` then `sqlcmd -C -I -b -S sqlserver -U sa -P ... -d examples -f 65001 -i /schema.sql`).  `$` in the command is written `$$` for Compose.
- README connection: `Server=localhost,51433;Database=examples;User ID=sa;Password=<the Compose password>;TrustServerCertificate=True`.

- [ ] **Step 1: The project file**, by `examples/PostgresExamples/PostgresExamples.csproj` line for line: the same top comment with Microsoft.Data.SqlClient for Npgsql, `SqlSourceDialect` `mssql`, `SqlSourceTypeOverrides` `date=System.DateOnly` (comment: every `date` is a `DateOnly`, which Dapper reads through `DateOnlyHandler`), `PackageReference` `Dapper` and `Microsoft.Data.SqlClient`.  Add it to `SqlSource.slnx` under `/examples/` and run `dotnet restore SqlSource.slnx`; `packages.lock.json` is written.
- [ ] **Step 2: `schema.sql`** with the header comment of PostgreSQL's (who runs it, and that a change is followed by the script), the tables and rows above, every object unqualified or `dbo.`, no `USE`, owner or role.
- [ ] **Step 3: `docker-compose.yml`** with a header comment: the password is the example's alone, the image is the tests' default by rule 5 of `AGENTS.md`, the host port and where to change it, and why the second service exists (the image has no init folder, and `schema.sql` names no database, so without `-d examples` it would land in `master`).
- [ ] **Step 4: Prove the Compose file.**  Choose the README's start command so that it returns once the schema is applied: try `docker compose up -d --wait` first (Compose v5 treats a one-shot service that exited 0 as done); if it fails on the exited service, use `docker compose up -d` then `docker compose wait schema`.  Run, from `examples/SqlServerExamples`: the chosen command; `docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P ... -d examples -Q "SELECT COUNT(*) FROM order_lines"` prints the seeded count; the chosen command again succeeds and the count is unchanged; `docker compose down -v`.
- [ ] **Step 5: `README.md`** by PostgreSQL's sections: what is in it (the table of Task 2's files and queries, by the names Task 2 fixes), your own project (`dotnet add package SqlSource --prerelease`, `Dapper --version 2.1.89`, `Microsoft.Data.SqlClient --version 7.1.1`, and the floor: SqlClient 6.1 or later, linked to `../../docs/manual/sqlserver.md`), start the database (the command of Step 4, the wait, the port, the password is the example's alone, the image runs under emulation on Apple silicon and the first start is slow, `down -v` to start again), describe the queries (`SQLSOURCE_CONNECTION` with the connection above; the paragraph on `TrustServerCertificate=True`; the repository's `dotnet run --project src/SqlSource.Tool -- describe --check examples/SqlServerExamples/SqlServerExamples.csproj` and why), read a sidecar, build and see the generated code.  Links to the manual relative.  Write the folder table now with Task 2's names; Task 2 corrects any name the generator gives otherwise.
- [ ] **Step 6: Epic row 6.7 `In progress`; validate; commit** `Add the SQL Server examples project, its database and its README`.

### Task 2: The classes, the queries and the sidecars

**Files:**
- Create in `examples/SqlServerExamples/`: `DapperSetup.cs`, `DateOnlyHandler.cs`, `TypedParameter.cs`, `Sql/Counts.sql`, `Sql/CountQueries.cs`, `Models/Orders.sql`, `Models/Users.sql`, `Models/StoreQueries.cs`, `CodeGen/OrderLines.sql`, `CodeGen/OrderLineQueries.cs`, and by the script `Models/Orders.sql.json`, `Models/Users.sql.json`, `CodeGen/OrderLines.sql.json`
- Modify: `tools/describe-end-to-end.sh`

**Interfaces:**
- Consumes: Task 1's tables.
- Produces: the names Task 1's README table uses.

| File | Query | Shows |
|----|----|----|
| `Sql/Counts.sql` | `CountUsers`: `SELECT COUNT(*) FROM users;` | A constant |
| `Sql/Counts.sql` | `CountRows`: `SELECT COUNT(*) FROM {{table}};` | A token method |
| `Models/Orders.sql` | `GetOrder`: `o.id, o.status, o.placed_at, o.shipped_on, u.name AS customer_name` of `orders AS o JOIN users AS u`, `WHERE o.id = @orderId` | Input and output models; a nullable column; `shipped_on` a `DateOnly?` by the override |
| `Models/Orders.sql` | `ListOrdersPlacedSince`: `-- param: count int`, `SELECT TOP (@count) id, placed_at FROM orders WHERE placed_at >= @since ORDER BY placed_at DESC` | A declared parameter type (the server infers `bigint` for `TOP`); a `datetime2` parameter sent with `TypedParameter(SqlDbType.DateTime2, ...)` |
| `Models/Orders.sql` | `CountShippedOrders`: `-- column: shipped not null`, `u.id, COUNT(o.shipped_on) AS shipped` of `users AS u LEFT JOIN orders AS o`, `GROUP BY u.id ORDER BY u.id` | The `-- column:` marker on a count the server calls nullable |
| `Models/Orders.sql` | `AddOrder`: `INSERT INTO orders (user_id) OUTPUT inserted.id, inserted.status, inserted.placed_at VALUES (@userId);` | Rows of an `OUTPUT` clause |
| `Models/Users.sql` | `GetUser`: `SELECT * FROM users WHERE id = @userId;` | The table model, `UsersDto`, with no `dbo` |
| `CodeGen/OrderLines.sql` | `ListOrderLines -> many`: `line_number, product, quantity, unit_price` of `@orderId`, `ORDER BY line_number` | The default output with a result shape |

- [ ] **Step 1: `DapperSetup`, `DateOnlyHandler`, `TypedParameter`.**  `DateOnlyHandler` and `TypedParameter` are the recipe's samples (`tests/SqlSource.Tests.SqlServer/EndToEnd/Types/`), namespace `SqlServerExamples`, with a one-line comment linking the recipe.  `public static class DapperSetup` with a static constructor setting `DefaultTypeMap.MatchNamesWithUnderscores = true` and adding `DateOnlyHandler`, and `public static SqlConnection CreateConnection(string connectionString)`.
- [ ] **Step 2: The `.sql` files of the table**, parameters in camelCase, each query with `-- summary:`, each file with a header comment as PostgreSQL's.  `OrderLines.sql`'s comment says codegen gives what models gives until query methods arrive.
- [ ] **Step 3: The three classes**, `public sealed partial class <Name>(DbConnection connection)`, by PostgreSQL's: `CountQueries` (`Output = GeneratorOutput.Sql, NestedClass = "Counts"`, `ExecuteScalarAsync<int>`), `StoreQueries` (`Output = GeneratorOutput.Models`; `GetOrderAsync(int)`, `ListOrdersPlacedSinceAsync(int count, DateTime since)` passing `new { count, since = new TypedParameter(SqlDbType.DateTime2, since) }`, `CountShippedOrdersAsync()`, `AddOrderAsync(int userId)` by `QuerySingleAsync`, `GetUserAsync(int)`), `OrderLineQueries` (`[SqlSourceGenerate]`).  A comment on each method says what its model shows, as PostgreSQL's do; a comment that states the tool's answer is checked against the sidecar.
- [ ] **Step 4: The script.**  Rename `EXAMPLES` to `POSTGRES_EXAMPLES`, keep `EXAMPLES_DATABASE` for both engines, and add `SQLSERVER_EXAMPLES='examples/SqlServerExamples'`.  In `describe_sqlserver`, after the test project's run: create `[examples]`, apply `schema.sql` with `"${SQLCMD[@]}" -d examples -f 65001 -i /dev/stdin`, delete `$SQLSERVER_EXAMPLES/**/*.sql.json`, and run `describe --force "$SQLSERVER_EXAMPLES/SqlServerExamples.csproj" --connection "mssql=Server=127.0.0.1,$port;User ID=sa;Password=$SQLSERVER_PASSWORD;TrustServerCertificate=True;Database=examples"`.  The header comment's SQL Server bullet names the examples.
- [ ] **Step 5: Run `tools/describe-end-to-end.sh`.**  Expected: every query described, none failed; `git status` shows the three new sidecars and no change to the test projects' sidecars.
- [ ] **Step 6: Build, and check the user's way.**  `dotnet build examples/SqlServerExamples` has no warning.  Start the Compose database by Task 1's command, run `SQLSOURCE_CONNECTION='<README connection>' dotnet run --project src/SqlSource.Tool -- describe --check examples/SqlServerExamples/SqlServerExamples.csproj`: every entry the same.  `docker compose down -v`.
- [ ] **Step 7: Correct the README's table** to the names built; validate; commit `Add the queries and sidecars of the SQL Server examples`.

### Task 3: The rules for agents and the developer documents

**Files:**
- Modify: `examples/AGENTS.md` (rules 3 to 6 of "Keeping them current" per engine; "Adding a database" names both projects as patterns)
- Modify: `AGENTS.md` (rule 5: `examples/SqlServerExamples/docker-compose.yml` repeats `DefaultImages.SqlServer`'s tag, and the examples' sidecars are among what moving it records again)
- Modify: `tools/AGENTS.md` (the `EXAMPLES` rule names both projects and their logical databases, `postgres` and `mssql`)
- Modify: `tests/SqlSource.Testing/DefaultImages.cs` (the comment on `SqlServer`: the Compose file repeats the tag)
- Modify: `tests/SqlSource.Tests.SqlServer/README.md` (the image section and "Writing the sidecars": the Compose file and the examples)
- Modify: `tools/README.md` (the script's paragraph), `CONTRIBUTING.md` (a sentence for the project, and the SQL Server readme's sentence names the examples), `Directory.Packages.props` (the comment that the examples' READMEs list Dapper's and SqlClient's versions, by PostgreSQL's comment)

- [ ] **Step 1: Make the edits**, each a clause or a sentence beside PostgreSQL's.  `examples/AGENTS.md` rule 3: each project sets no `SqlSourceDatabase`, its queries belong to its dialect's default database, `postgres` or `mssql`.  Rule 4: the Compose file and the script both apply `schema.sql` as their engine's admin in a database of their own.  Rule 5: each Compose file's image is its engine's default image.  Rule 6: each README lists Dapper's and its driver's versions.
- [ ] **Step 2: Validate; commit** `Keep the SQL Server examples current through the rules`.

### Task 4: The read-through of the user documentation

**Files:**
- Modify: whichever of `README.md`, `src/SqlSource.Tool/README.md`, `docs/manual/*.md` the reading finds wrong.
- Possibly create: `docs/tech-debt/TD-00nn-*.md` for a finding that is the code's.

- [ ] **Step 1: Run the finders** and keep their output: the phase 6 finder (`git grep -n -i -E "SQLSRC216|cannot describe|can be described|PostgreSQL" -- README.md src/SqlSource.Tool/README.md docs/manual`), the "When a phase lands" finder of step 3, and `git grep -n "Coming in a later release"`.  Every hit that says PostgreSQL alone where SQL Server holds too is fixed; every callout left is for phases 7 to 10.
- [ ] **Step 2: Read whole** the pages the phase map's row 6 names: `sqlserver.md` against `postgresql.md` (sections, known issues, the `WHERE @p IS NULL` → `int` note, first result set only), `dialects.md`'s support table, `README.md` (status line, supported environments, step 5, the "Use SQL Server, or another database" row, "Where to next"), `docs/manual/README.md` ("What works today", "Find what you need", the new example row), `settings.md`'s Status cells, `tool.md` ("Which queries are described"), `src/SqlSource.Tool/README.md`, `recipes.md` (the two Dapper sections, "Read one folder with another dialect"), `generator.md`, `concepts.md`, `diagnostics.md` (`SQLSRC209`, `SQLSRC216`, the paragraph on the ids of describing, `SQLSRC262` to `SQLSRC269`).  Split the reading across read-only subagents, each with a list of pages, the rules of `docs/manual/AGENTS.md` and the finders' hits for its pages, returning findings with line, quote and the proposed fix; check each before applying.
- [ ] **Step 3: Link the example**: `README.md`'s "Where to next" row and `docs/manual/README.md`'s "See a whole project" row name both examples (absolute links in the README, relative in the manual), and `sqlserver.md` links the example where `postgresql.md` links its own.
- [ ] **Step 4: Apply the document findings; record any of the code's** in `docs/tech-debt` (or ask the implementation manager for the owner's agreement to fix a small one here, and wait).  Run the internals grep of `docs/manual/AGENTS.md` over every page changed.
- [ ] **Step 5: Validate; commit** `Read the SQL Server user documentation whole and fix what it found`.

### Task 5: The close of phase 6

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 6.7 `Done` with its Delivers as built; the Phase 6 section's Settled gains a bullet per sub-phase, as phase 5's has, from the merged units recorded in the implementation manager's notes and each sub-phase's PR)
- Modify: `docs/manual/AGENTS.md` (the phase map's row 6 and the phase 6 finder removed; row 7 names what phase 7 changes on both databases' pages: each Types section, and `sqlserver.md`'s help for `hierarchyid`, `geography` and `geometry` and `json` where a library map replaces the override)
- Check: `grep -rn "Not built yet" docs/architecture` lists no phase 6 placeholder.

- [ ] **Step 1: The Settled bullets**, one per sub-phase 6.1 to 6.7, each what it settled that the outline recommended or left open, in the style of phase 5's (6.5's `-- param:` for a version difference, 6.6's `date=default` metadata and the one `dapper` case, 6.7's second Compose service).  The table's rows of 6.1 to 6.6 already say `Done`; check each Delivers cell against what merged.
- [ ] **Step 2: The phase map and the architecture check.**
- [ ] **Step 3: Row 6.7 `Done`; validate; commit** `Close phase 6 in the epic and the phase map`.

### After the tasks

- [ ] Whole-branch review (superpowers:requesting-code-review), fixes in one commit.
- [ ] `git fetch --tags origin`; compare `VersionPrefix` with the highest `v*` tag.
- [ ] Bring the branch up to date with `main`, validate, clear with the CodeRabbit manager, push, open the PR with the file count and the read-through's findings, turn on Auto-fix.
