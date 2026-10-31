# Query generation phase 7.5: the examples and close - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each example project's `CodeGen` folder shows each library its database supports, the user documentation of phase 7 is read whole and fixed, and phase 7 is closed in the epic.

**Architecture:** No generator code changes.  Each example gains a `deliveries` table, a `CodeGen/Deliveries.sql` of two queries, a `DeliveryDetails` record for a JSON column, and methods in `StoreQueries` that call them.  The PostgreSQL example's Compose file builds PostgreSQL 18.6 with PostGIS from a Dockerfile of its own, and `tools/describe-end-to-end.sh` describes the PostgreSQL example on the PostGIS container it already builds.

**Tech stack:** C# 14 on net10.0, Npgsql 10.0.3 with `Npgsql.NodaTime` and `Npgsql.NetTopologySuite` 10.0.3, Microsoft.Data.SqlClient, NodaTime 3.3.7, NetTopologySuite 2.6.0, `NetTopologySuite.IO.SqlServerBytes` 2.1.0, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-10-26-query-generation-phase-7-library-maps-design.md`, sections "7.5 The examples and close" and "Documentation".  Read with `examples/AGENTS.md`, `docs/manual/AGENTS.md` and root `AGENTS.md`.

## Where merged code differs from the spec

- 7.3 already removed every "Coming in a later release" callout of phase 7 (README, `settings.md`), flipped the manual's "What works today" row, and removed the architecture's `## Not built yet / ### Library maps` placeholder.  7.5 checks they are gone and that nothing of phase 7 is still described as coming; it does not redo them.
- The README's "NodaTime and other libraries" section names each plugin's `Use...()` call but not the two traps the spec asks for (EF Core's plugin turning the library on, Dapper on a data source that calls `UseNodaTime()`).  Both are in `docs/manual/postgresql.md`; Task 3 adds them to the README.
- The spec lists, for SQL Server, "NodaTime and the `json` library turned on, and a class in `json`", while its rule is "each library its database supports", and SQL Server supports NetTopologySuite since 7.4.  This plan includes NetTopologySuite on SQL Server (one `geography` column).  It is an owner question sent to the manager ("blocks merge: yes"); if the answer is no, Task 2's `location` column, its parameter and the `NetTopologySuite.IO.SqlServerBytes` reference come out.

## Global constraints

- Root `AGENTS.md` rule 5: every test image pinned; the PostgreSQL default image `postgres:18.6`; the PostGIS package `3.6.4+dfsg-2.pgdg13+1`.
- `examples/AGENTS.md`: small and representative; nothing runs; only what the manual recommends; `CodeGen/` uses the default output; sidecars are written by `tools/describe-end-to-end.sh`, never by hand; `schema.sql` names no owner, role or database; READMEs link to the manual relatively.
- Spec anti-goal: the generated code of every existing example query stays byte for byte the same.
- `docs/manual/AGENTS.md`: write effects, never internals (run its grep on every page changed); the README is a packed readme with absolute links and no raw HTML.
- Every commit passes `./pre-commit-validation.sh`; run `./format.sh` first.

## Review focus

1. **The existing examples' generated code changes.**  Turning NodaTime and JSON on, or referencing a plugin, must not change any existing query's models or methods.  Each example task builds with `EmitCompilerGeneratedFiles` before and after and diffs the generated folder: only new files and the `StoreQueries` methods file may differ, and in that file only the new methods.
2. **Compose from nothing.**  `docker compose down -v && docker compose up -d --wait` in `examples/PostgresExamples` builds the PostGIS image and applies `schema.sql`, `CREATE EXTENSION postgis` included, as the user `examples`.  Task 1 runs it once.
3. **JSON property names.**  System.Text.Json's default options match property names case-sensitively, so the seed's JSON keys are `Carrier` and `TrackingNumber`, spelled as the record's properties, or a read gives nulls into non-nullable properties.
4. **Dapper and the plugins on one data source.**  `DapperSetup.CreateDataSource` keeps calling neither `UseNodaTime()` nor `UseNetTopologySuite()`, and the methods' data source calls both; each says why in a comment.
5. **The PostGIS version in two Dockerfiles.**  A test keeps the example's Dockerfile on `DefaultImages.Postgres` and `PostgisContainer.Package`, so moving either fails until both move.

---

### Task 1: PostgreSQL example

**Files:**
- Create: `examples/PostgresExamples/Postgis/Dockerfile`, `examples/PostgresExamples/CodeGen/Deliveries.sql`, `examples/PostgresExamples/CodeGen/DeliveryDetails.cs`, sidecar `CodeGen/Deliveries.sql.json` (by the script)
- Modify: `examples/PostgresExamples/{docker-compose.yml, schema.sql, PostgresExamples.csproj, packages.lock.json, CodeGen/StoreQueries.cs, DapperSetup.cs, README.md}`, `tools/describe-end-to-end.sh`, `tools/README.md`, `tests/DatabaseTests/TestHarness/DefaultImages.cs` (comment), `AGENTS.md` (rule 5), `tests/SqlSource.Tests/TestHarness/PostgisContainerTests.cs`, any of `tests/DatabaseTests/Postgres/README.md` and `CONTRIBUTING.md` that describe the script's examples part or the example's image

**Interfaces:**
- Produces: table `deliveries`, record `PostgresExamples.CodeGen.DeliveryDetails(string Carrier, string TrackingNumber)`, `StoreQueries.CreateDataSource(string connectionString)`, `StoreQueries.RecordDeliveryAsync`, `StoreQueries.FindDeliveryAsync`.  Task 2 mirrors these names.

- [ ] **Step 1: Capture the generated code before.**  `dotnet build examples/PostgresExamples -p:EmitCompilerGeneratedFiles=true`, copy `obj/Debug/net10.0/generated/` to the scratchpad.

- [ ] **Step 2: Failing test.**  In `PostgisContainerTests`, add:

```csharp
[Fact]
public void ExampleDockerfile_ImageAndPackage_AreTheTests() =>
    File.ReadAllLines(RepositoryRoot.Path("examples", "PostgresExamples", "Postgis", "Dockerfile"))
        .ShouldSatisfyAllConditions(
            lines => lines.ShouldContain($"FROM {DefaultImages.Postgres}"),
            lines => lines.ShouldContain($"ARG POSTGIS={PostgisContainer.Package}"));
```

Run `dotnet test tests/SqlSource.Tests --filter PostgisContainerTests`; expect FAIL (file missing).

- [ ] **Step 3: The image.**  `Postgis/Dockerfile` is the tests' `tests/DatabaseTests/TestHarness/Postgis/Dockerfile` with `FROM postgres:18.6` in place of `ARG PG` and `FROM postgres:${PG}`, and its header comment rewritten for a user: PostgreSQL 18.6 with PostGIS at a pinned package version, built by `docker-compose.yml`; the repository keeps both versions on its tests' by a test.  `docker-compose.yml`'s service takes `build: ./Postgis` in place of `image:`, and its header says the first `up` builds the image and that `up --build` builds it again after a change to the Dockerfile.  Run the test of Step 2; expect PASS.

- [ ] **Step 4: The schema.**  In `schema.sql`, after the existing tables and before the inserts, with comments in the file's style:

```sql
CREATE EXTENSION postgis;

CREATE TABLE deliveries (
    order_id integer PRIMARY KEY REFERENCES orders (id),
    delivered_at timestamptz NOT NULL,
    location geography(Point, 4326) NOT NULL,
    details jsonb NOT NULL
);
```

and one row for order 1, delivered `2026-03-02 16:00:00+00` at `SRID=4326;POINT(-0.1276 51.5072)` with `{"Carrier": "Parcelforce", "TrackingNumber": "PF1234567"}`.

- [ ] **Step 5: The packages.**  `PostgresExamples.csproj` references `Npgsql.NodaTime` and `Npgsql.NetTopologySuite` (no version; central), with a comment that referencing them gives the query methods NodaTime's and NetTopologySuite's types.  `dotnet restore examples/PostgresExamples --force-evaluate` updates `packages.lock.json`.

- [ ] **Step 6: The queries.**  `CodeGen/Deliveries.sql`, in the style of `Users.sql` (a file comment, a `summary:` and a comment on each query):
  - File header `-- type-override: timestamptz=default`, its comment saying the project maps every `timestamptz` to `DateTimeOffset` for Dapper, which would win over NodaTime, so the file gives it back and its `timestamptz` is NodaTime's `Instant`; `location` is NetTopologySuite's `Geometry`, as the project references both plugins; `details` is a class of the project, read and sent through System.Text.Json.
  - `RecordDelivery -> rowcount`: `INSERT INTO deliveries (order_id, delivered_at, location, details) VALUES (@orderId, @deliveredAt, @location, @details);` with `-- param: @details -> DeliveryDetails`, since no column type reaches a parameter.
  - `FindDelivery -> one-optional`: `SELECT delivered_at, location, details FROM deliveries WHERE order_id = @orderId;` with `-- column: details -> DeliveryDetails`.  It leaves out `order_id` so that the rows are not the table's model.
  - `DeliveryDetails.cs`: `public sealed record DeliveryDetails(string Carrier, string TrackingNumber);` with a comment that System.Text.Json's default options match the JSON's keys to these names, case and all.

- [ ] **Step 7: The methods and the data source.**  In `CodeGen/StoreQueries.cs`, with comments in the file's style:
  - `public static NpgsqlDataSource CreateDataSource(string connectionString)`: an `NpgsqlDataSourceBuilder` that calls `UseNodaTime()` and `UseNetTopologySuite()`, then `Build()`.  The comment: the project references both plugins, so the methods read and send their types, which Npgsql reads only on a data source taught them; `DapperSetup`'s data source calls neither, since after `UseNodaTime()` Npgsql gives Dapper an `Instant` for every `timestamptz`.
  - `RecordDeliveryAsync(int orderId, Instant deliveredAt, Geometry location, DeliveryDetails details, CancellationToken cancellationToken = default)` returning `Task<int>`, and `FindDeliveryAsync(int orderId, CancellationToken cancellationToken = default)` returning `Task<FindDeliveryDto?>`, each passing the token on; construct the generated params records with named arguments.
  - `DapperSetup.cs`: its comment adds why its data source calls no plugin.

- [ ] **Step 8: The script.**  `tools/describe-end-to-end.sh` describes `examples/PostgresExamples` on the PostGIS container, which is built from the default image's tag, before that container is removed: the database `examples` made there from the example's `schema.sql`, as today on the plain container.  Update the script's header comment, `tools/README.md`'s paragraph on the script, and any README of `tests/` that says where the examples are described.

- [ ] **Step 9: Rule 5 and the image comment.**  Root `AGENTS.md` rule 5: the PostgreSQL bullet names `examples/PostgresExamples/Postgis/Dockerfile` where it names `examples/PostgresExamples/docker-compose.yml` for the tag; the PostGIS bullet adds that dockerfile to the places that name the package, and the sidecars of `examples/PostgresExamples` to what is recorded on the image.  `DefaultImages.cs`'s comment names the Dockerfile in place of the Compose file.  Keep the file under 16 KB and each bullet under 80 words (`AgentsFileTests`); split a bullet if needed.

- [ ] **Step 10: Sidecars.**  Run `tools/describe-end-to-end.sh` (Docker).  If it would take longer than 10 minutes, stop and say so.  `git diff --stat` must show only `Deliveries.sql.json` added among the examples' sidecars; any other sidecar change is reported, not committed.

- [ ] **Step 11: The generated code after.**  Build as in Step 1 and diff against the copy: only a new `Deliveries`-related model files and the new methods in the `StoreQueries` methods file may differ.

- [ ] **Step 12: Compose from nothing.**  In `examples/PostgresExamples`: `docker compose down -v`, `docker compose up -d --wait`, then `docker compose exec postgres psql -U examples -d examples -c "SELECT ST_AsText(location::geometry), details FROM deliveries"` shows the row; `docker compose down -v`.

- [ ] **Step 13: README.**  `examples/PostgresExamples/README.md`: a row for `Postgis/Dockerfile`; the `CodeGen/` row describes `Deliveries.sql` (the `Instant` given back by the header's `timestamptz=default`, the `Geometry`, the class in `jsonb` by the two markers, each linked to its manual section: `postgresql.md#nodatime`, `#nettopologysuite`, `#json`, `settings.md#setting-library-types`); "Your own project" adds `dotnet add package Npgsql.NodaTime --version 10.0.3` and `Npgsql.NetTopologySuite --version 10.0.3`, and says the packages turn the libraries on for the methods; "Start the database" says the first `up` builds PostgreSQL with PostGIS; "Call the methods" uses `StoreQueries.CreateDataSource(connectionString)` and says why Dapper's data source is another.

- [ ] **Step 14: Validate and commit.**  `./format.sh`, `./pre-commit-validation.sh`, then commit: `feat(examples): show NodaTime, NetTopologySuite and a class in jsonb in the PostgreSQL example`.

### Task 2: SQL Server example

**Files:**
- Create: `examples/SqlServerExamples/CodeGen/Deliveries.sql`, `examples/SqlServerExamples/CodeGen/DeliveryDetails.cs`, sidecar `CodeGen/Deliveries.sql.json` (by the script)
- Modify: `examples/SqlServerExamples/{schema.sql, sqlsource.json, SqlServerExamples.csproj, packages.lock.json, CodeGen/StoreQueries.cs, README.md}`

**Interfaces:**
- Consumes: Task 1's names and the `describe-end-to-end.sh` it leaves.

- [ ] **Step 1: Capture the generated code before**, as Task 1 Step 1.

- [ ] **Step 2: The schema.**  After the existing tables, a comment saying `json` is a type of SQL Server 2025, the version `docker-compose.yml` starts:

```sql
CREATE TABLE deliveries (
    order_id int PRIMARY KEY REFERENCES orders (id),
    delivered_at datetimeoffset NOT NULL,
    location geography NOT NULL,
    details json NOT NULL,
    events json NOT NULL
);
```

and one row for order 1: `'2026-03-02T16:00:00+00:00'`, `geography::Point(51.5072, -0.1276, 4326)`, `N'{"Carrier": "Parcelforce", "TrackingNumber": "PF1234567"}'`, `N'[{"Status": "Delivered"}]'`.

- [ ] **Step 3: The switch and the packages.**  `sqlsource.json` gains `"libraryTypes": { "nodaTime": "on", "netTopologySuite": "on", "json": "on" }`, with a comment that SQL Server turns a library on only when asked, and that the project's `date=System.DateOnly` override still wins over NodaTime for every `date`.  `SqlServerExamples.csproj` references `NodaTime` and `NetTopologySuite.IO.SqlServerBytes`; restore with `--force-evaluate`.

- [ ] **Step 4: The queries.**  `CodeGen/Deliveries.sql`, in the style of `Users.sql`, its file comment saying `datetimeoffset` is NodaTime's `OffsetDateTime`, `geography` NetTopologySuite's `Geometry` and `json` a `JsonElement`, by the project's library types, and `details` a class of the project, all converted by the methods themselves with nothing to register on the connection:
  - `RecordDelivery -> rowcount`: insert of all five columns from `@orderId`, `@deliveredAt`, `@location`, `@details`, `@events`, with `-- param: @details -> DeliveryDetails`.
  - `FindDelivery -> one-optional`: `SELECT delivered_at, location, details, events FROM deliveries WHERE order_id = @orderId;` with `-- column: details -> DeliveryDetails`.
  - `DeliveryDetails.cs` as Task 1's, in namespace `SqlServerExamples.CodeGen`.

- [ ] **Step 5: The methods.**  `StoreQueries.RecordDeliveryAsync(int orderId, OffsetDateTime deliveredAt, Geometry location, DeliveryDetails details, JsonElement events, CancellationToken cancellationToken = default)` returning `Task<int>`, and `FindDeliveryAsync(int orderId, CancellationToken cancellationToken = default)` returning `Task<FindDeliveryDto?>`, commented in the file's style.

- [ ] **Step 6: Sidecars**, as Task 1 Step 10: only `examples/SqlServerExamples/CodeGen/Deliveries.sql.json` added.

- [ ] **Step 7: The generated code after**, as Task 1 Step 11: besides the new methods and models, only the new `NodaTimeConversions`, `NetTopologySuiteConversions` and `JsonConversions` files appear, if the new methods use them.

- [ ] **Step 8: README.**  `examples/SqlServerExamples/README.md`: the `CodeGen/` row describes `Deliveries.sql` and the library types of `sqlsource.json`, linked to `sqlserver.md#nodatime`, `#nettopologysuite`, `#json` and `settings.md#setting-library-types`; "Your own project" adds `dotnet add package NodaTime --version 3.3.7` and `NetTopologySuite.IO.SqlServerBytes --version 2.1.0`, and says nothing is registered on the connection.

- [ ] **Step 9: Validate and commit.**  `./format.sh`, `./pre-commit-validation.sh`, then commit: `feat(examples): show NodaTime, NetTopologySuite and JSON in the SQL Server example`.

### Task 3: The user documentation of phase 7, read whole

**Files:**
- Modify: `README.md`, and whatever page the checks below find wrong among `src/SqlSource.Tool/README.md` and `docs/manual/*.md`

- [ ] **Step 1: README's traps.**  In "NodaTime and other libraries", after the PostgreSQL paragraphs, add the two traps in a few sentences, linking `docs/manual/postgresql.md#known-issues` with an absolute URL: EF Core's `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime` and `.NetTopologySuite` bring the plugins, so the library is on, and `UseNodaTime()` inside `UseNpgsql(...)` teaches EF Core's own data source alone; and on a data source that calls `UseNodaTime()`, Npgsql gives Dapper an `Instant` for a `timestamptz`, so read Dapper's queries on another data source or give them NodaTime's types by type override.  Add the examples' link, as the section's last sentence, if the README links examples elsewhere; otherwise leave it.
- [ ] **Step 2: Callouts and "coming".**  `git grep -n "Coming in a later release" -- README.md src/SqlSource.Tool/README.md docs/manual` and the phase map's grep of `docs/manual/AGENTS.md`: no hit may describe NodaTime, NetTopologySuite, System.Text.Json, PostGIS, spatial or JSON types, or the library types as missing or still to come.  Rewrite each that does.
- [ ] **Step 3: Read the phase's pages whole.**  `README.md` (library section, FAQ on `InvalidCastException`, "Custom type mapping"), `docs/manual/README.md` ("What works today", "Find what you need"), `settings.md` (library types, converters, column types' JSON opt-in), `postgresql.md` and `sqlserver.md` (Types, the library sections, Query methods, known issues), `generator.md` (property types, query methods), `recipes.md` (class in a JSON column), `diagnostics.md` (`SQLSRC246`, `SQLSRC281` to `SQLSRC283`).  Fix what is untrue, contradictory between pages, internal (the manual's grep), or a broken relative link.  Report each change in the hand-back.
- [ ] **Step 4: Validate and commit.**  `./pre-commit-validation.sh` (the documentation tests), commit: `docs: read phase 7's user documentation whole`.

### Task 4: The close

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, `docs/manual/AGENTS.md`, `docs/architecture/generator.md` only if Step 3 finds a phase 7 placeholder

- [ ] **Step 1: The epic.**  Row 7.5 `Done`, with what it delivered in the style of row 8.6.  The paragraph under the table says phase 7 is closed, as it says of phases 5, 6 and 8.  Phase 7's section gains `### Settled`, after `### Decided`, in the shape of phase 8's: what the design settled, then "And by each sub-phase's building and review", one bullet each for 7.0 to 7.5 from the manager's notes and the sub-phases' hand-over notes: ids taken (`SQLSRC281` 7.1, `SQLSRC282` 7.2 and 7.4, `SQLSRC283` 7.3), the owner's decisions with their dates (range defaults; `default` in the switch; the switch's names per sub-phase; `SQLSRC282`'s split; `SQLSRC283` at the query; R1-F4 the unfiltered check; R1-F3 TD-0042's budget), the tech debt left open (`TD-0093`, `TD-0042` rewritten), and 7.5's own (the examples, the PostGIS image of the PostgreSQL example, and the owner's answer on NetTopologySuite in the SQL Server example).
- [ ] **Step 2: The phase map.**  `docs/manual/AGENTS.md`'s table loses its row of phase 7, as phases 6 and 8 lost theirs.
- [ ] **Step 3: The architecture.**  `git grep -n -i -E "not built|phase 7|library maps|later release" -- docs/architecture`: no placeholder of phase 7 may remain; `docs/architecture/generator.md` describes the library map, the probe, the `Library` origin, the serializer and the spatial path, which 7.1 to 7.4 wrote.  Fix a stale sentence if one is found.
- [ ] **Step 4: Validate and commit.**  `./pre-commit-validation.sh`, commit: `docs: close phase 7 in the epic and the phase map`.

### After the tasks

`./format.sh`, `./pre-commit-validation.sh --full` on the whole branch, the version check of root `AGENTS.md`'s "Pull requests", then `/branch-review:review --base origin/main`.
