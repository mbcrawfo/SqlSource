# Query generation 8.6: the examples and close of phase 8 - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make both example projects' `CodeGen/` folders show the generated methods at their simplest, with a converter over a domain or alias type; read the user documentation of phase 8 whole and fix what reads as written in pieces; close phase 8 in the phase map and the epic outline.

**Architecture:** Each example project gains a `users.email` column of a domain (`email`, PostgreSQL) or alias type (`dbo.Email`, SQL Server), a `CodeGen/Users.sql` whose header maps it to `EmailAddress` with `-- type-override:`, and an `EmailAddress` type with its converters.  The one class of `CodeGen/`, renamed `StoreQueries`, calls every method and passes the caller's `CancellationToken`.  Sidecars come from `tools/describe-end-to-end.sh`.  The reading is done page by page in the order a user meets them; its fixes are documentation alone unless a finding is the code's.

**Tech Stack:** C# on .NET 10, Npgsql 10.0.3, Microsoft.Data.SqlClient 7, Dapper (in `Models/` only), PostgreSQL 18.6 and SQL Server 2025 (the default images, which the sidecars are recorded on).

**Spec:** [`docs/superpowers/specs/2026-10-24-query-generation-phase-8-6-examples-and-close-design.md`](../specs/2026-10-24-query-generation-phase-8-6-examples-and-close-design.md), with the epic outline's phase 8 section and the precedent of 6.7's close (commit `1c1f9c33`).

## Global Constraints

- `examples/AGENTS.md`: small and representative; no tests; only what the manual recommends; one class per folder whose `[SqlSourceGenerate]` claims the folder's `.sql` files; `CodeGen/` uses the default output; sidecars written by `tools/describe-end-to-end.sh`, never by hand; `schema.sql` names no owner, role or database; README links to the manual are relative.
- The override is in the headers of `CodeGen/`'s files alone, so `Models/` (Dapper) needs no handler for the new column type.  `Models/` keeps Dapper.
- Comments in the examples say what each method shows and point at the manual's sections, and nothing more.
- User documentation (root `AGENTS.md` rule 4, `docs/manual/AGENTS.md`): effects, never internals; no phase, sub-phase, spec or deferred/tech-debt id; no "Coming in a later release" for query methods; packed readmes use absolute links and no raw HTML; `sql` samples name their dialect.
- Every commit passes `./pre-commit-validation.sh` after its last edit.  No describer or describer test changes, so no floor-image run is needed.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Where merged code differs from the spec

- **Converters already in the examples.**  8.2 added `Price` (a type override of `numeric`/`decimal`, read only) and 8.7 added `OrderId` (column types, `-- param: ... -> OrderId` and `-- column: ... -> OrderId`, read and bound).  So a column and a parameter converted by a project's converter are already shown.  `EmailAddress` over a domain or alias type is still added, as the spec says: it is the one example of a type override keyed on a domain or alias type, of a converter on a written parameter (`rowcount`), and of PostgreSQL's `-- param: @email public.email` for a parameter compared with a domain column.
- **The spec's call against a strongly-typed id** ("an override of `int4` would reach every integer") is moot since 8.7: `OrderId` is a strongly-typed id by column types.  The examples keep `OrderId`.
- **The class of `CodeGen/` is `OrderLineQueries`.**  It will claim user queries too, so it is renamed `StoreQueries` (namespace `<Project>.CodeGen`; `Models/` has its own `StoreQueries` in `<Project>.Models`, which gets no extension class).  The generated extension class becomes `StoreQueriesExtensions`.
- **Implementation is native** (in this session), with read-only subagents for the documentation reading; the branch review is the independent check.
- **Tasks 1 and 2 are one commit.**  Every commit must pass `./pre-commit-validation.sh` on its own tree, and this session has one worktree, so the two examples were validated and committed together.
- **SQL Server's `schema.sql` needs `GO` after `CREATE TYPE`**: a table can use an alias type only in a batch after the one that creates it.  Both `sqlcmd` runs (Compose and the script) split at `GO`.
- **One finding of the reading is the code's**: `SQLSRC232`'s help said "a later version may let a project declare a conversion for the type", which converters now answer otherwise; the clause is removed from `PostgresErrors.BindHelp`.  It is describer code, so `tests/SqlSource.Tests.Postgres` also runs on `postgres:14.24`.

## Names

| Thing | Name |
|----|----|
| PostgreSQL domain | `CREATE DOMAIN email AS text CHECK (VALUE LIKE '%_@_%')`; `users.email email NOT NULL` |
| SQL Server alias type | `CREATE TYPE dbo.Email FROM nvarchar(200)`; `users.email dbo.Email NOT NULL` |
| Wrapper type | `CodeGen/EmailAddress.cs`: `public readonly record struct EmailAddress(string Value)` with `[SqlSourceConverter] static EmailAddress FromDatabase(string value)` and `static string ToDatabase(EmailAddress email)` |
| Override | `-- type-override: public.email=EmailAddress` / `-- type-override: dbo.Email=EmailAddress` in `CodeGen/Users.sql`'s header |
| Queries of `CodeGen/Users.sql` | `FindUserByEmail -> one-optional` (`SELECT id, name, email FROM users WHERE email = @email`; PostgreSQL adds `-- param: @email public.email`); `ChangeUserEmail -> rowcount` (`UPDATE users SET email = @email WHERE id = @userId`); `ListUsersNewestFirst` with `-- collection-type: IReadOnlyList` (`SELECT id, name, email FROM users ORDER BY created_at DESC, id`); `ListUsersSortedBy` with a token (`SELECT id, name FROM users ORDER BY {{orderBy:name}}`) |
| Class of `CodeGen/` | `StoreQueries(DbConnection connection)`, one `...Async` method per query, each taking `CancellationToken cancellationToken = default` last and passing it as `cancellationToken:` |

## Review Focus

1. **A parameter the override does not reach**: if a `@email` is described as the base type (`text`/`nvarchar`) its property is a `string`, and the example would mislead.  Task 1/2 checks each written sidecar's parameter types and the generated `...Params` property types (the class's signatures take `EmailAddress`, so the build fails if one is `string`).
2. **The table model**: a `CodeGen` query whose columns are the whole of `users` in order would get `UsersDto`, shared with `Models/` and typed differently.  No `CodeGen` query selects every column of `users`.
3. **`Models/`'s `GetUser` (`SELECT *`)** now reads a domain or alias column: its `UsersDto.Email` must stay `string`, read by Dapper with no handler.  The build of the example shows the property type; the sidecar diff is read.
4. **Compose and script agree**: `schema.sql` with the domain or alias type must apply both from Compose and from `tools/describe-end-to-end.sh`; the script's run proves the latter, and `docker compose up` (SQL Server's `schema` service, `sqlcmd -I`) is run once for each project.
5. **A page that still says a later release for methods, or names a phase**: Task 3 runs both finders of `docs/manual/AGENTS.md` and the internals grep over every page it changes.

---

### Task 1: The outline's row, and the PostgreSQL example

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 8.6: `Not started` -> `In progress`)
- Add: this plan
- Modify: `examples/PostgresExamples/schema.sql` (domain `email`; `users.email email NOT NULL`)
- Create: `examples/PostgresExamples/CodeGen/EmailAddress.cs`, `examples/PostgresExamples/CodeGen/Users.sql` (+ sidecar)
- Rename: `examples/PostgresExamples/CodeGen/OrderLineQueries.cs` -> `StoreQueries.cs`
- Modify: `examples/PostgresExamples/README.md`, the sidecars `tools/describe-end-to-end.sh` rewrites (`Models/Users.sql.json` gains the domain's type name)

- [ ] **Step 1:** Edit the schema, add `EmailAddress.cs` and `Users.sql` per Names, each query with a `-- summary:` and a one-line comment of what it shows; rename the class and add the four methods with `CancellationToken`, and pass it from the two existing ones.
- [ ] **Step 2:** Run `tools/describe-end-to-end.sh` (background; it needs Docker).  Read the diff: `@email` of both user queries is the domain (else add `-- param: @email public.email` to that query and run again); no other end-to-end or realistic sidecar changes except by tool version (none expected).
- [ ] **Step 3:** `dotnet build examples/PostgresExamples` passes; `docker compose up -d --wait` in the folder applies the schema, then `docker compose down -v`.
- [ ] **Step 4:** README: the `CodeGen/` row names `StoreQueries`, each shape, the collection type, the token, `EmailAddress` and the domain with links to `sql-files.md#result-shapes`, `settings.md#setting-collection-type`, `parameters-and-tokens.md#tokens`, `settings.md#setting-type-overrides`, `settings.md#converters`; the paragraph after the table and "Build, and see the generated code" name `StoreQueriesExtensions`; the workflow shows calling the methods.
- [ ] **Step 5:** `./pre-commit-validation.sh`, then commit "Show the generated methods in the PostgreSQL example".

### Task 2: The SQL Server example

Same as Task 1 in `examples/SqlServerExamples`, with `dbo.Email` (no `-- param:` needed: a parameter compared with or written to an alias-type column is described as the alias type), T-SQL `ORDER BY created_at DESC, id`, and SQL Server's Compose (`docker compose up -d --wait` then `docker compose wait schema`).  Commit "Show the generated methods in the SQL Server example".

### Task 3: The reading and its fixes

**Files:** `README.md`, `docs/manual/README.md`, `concepts.md`, `generator.md`, `sql-files.md`, `parameters-and-tokens.md`, `settings.md`, `recipes.md`, `postgresql.md`, `sqlserver.md`, `dialects.md`, `diagnostics.md`, `tool.md`, `src/SqlSource.Tool/README.md`.

- [ ] **Step 1:** Read the pages in that order (three read-only subagents, each a contiguous run of pages, report findings with file:line, the text and the proposed fix; findings are verified here before any edit).  What counts: a sentence that reads as written in pieces (two pages or sections that say one thing two ways, or contradict), a stale statement (a later release, a phase, "today" for what changed in phase 8, a name that changed such as SQLSRC messages 8.7 reworded), a missing cross-link between the methods' sections, internals, and any 8.7 feature a page should mention and does not.
- [ ] **Step 2:** Run `git grep -n -i -E "no effect yet|today|coming|arrive|later release|until|growing|(^|[^[:alnum:]_])(yet|will)([^[:alnum:]_]|$)" -- README.md src/SqlSource.Tool/README.md docs/manual` and the internals grep of `docs/manual/AGENTS.md`; read every hit that concerns methods, converters or column types.
- [ ] **Step 3:** README: the FAQ answers the `InvalidCastException` question; the status line says the methods work on both engines; the callout under "NodaTime and other libraries" speaks of the library maps alone.
- [ ] **Step 4:** Fix every finding.  A finding that is the code's: fix it here when small, else escalate (owner decides tech debt).  `./pre-commit-validation.sh`; commit "Read the user documentation of phase 8 whole and fix what it found".

### Task 4: The close

**Files:** `docs/manual/AGENTS.md`, the epic outline.

- [ ] **Step 1:** `docs/manual/AGENTS.md`: remove the phase map's row of phase 8, as 6.7 removed phase 6's; phase 7's row names what it changes on the methods' pages (the converters section of `settings.md` and the FAQ on `InvalidCastException` where a library's map makes a converter unnecessary, the methods' reading and sending of a library's types in each database page's Query methods section); reword step 2's example of a callout that covers two phases if the README's callout no longer does.
- [ ] **Step 2:** Epic outline: phase 8's Settled gains "And by each sub-phase's building and review" with one line each for 8.0 to 8.7, naming the deferred items and tech debt they recorded (D-0003 rewritten, D-0007, D-0008, TD-0078; TD-0012 resolved; TD-0006 extended) and the owner's decisions; row 8.6 `Done` with what it delivered; the paragraph under the table says phase 8 is closed.
- [ ] **Step 3:** `git grep` for any `docs/architecture` placeholder of phase 8 left; none should be.
- [ ] **Step 4:** `./pre-commit-validation.sh`; commit "Close phase 8 in the epic and the phase map".
