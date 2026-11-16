# Docs overhaul phase 19, rewrite-postgresql: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rewrite the five pages of `site/reference/databases/postgresql/` and the two pages beside the database folders in plain language and the shape of a reference page, merge MP-23 and MP-09, and cut each rule another section owns to a restatement with a link.

**Architecture:** One page per task, in the order below.  Each task rewrites its page, adds its rows to the one ledger file, and commits.  The last task runs the checks of the whole phase and writes the word counts.

**Tech Stack:** Markdown for docfx; the epic's checker `check/check.sh` (.NET file-based app); `tools/build-site.sh`; `tests/SqlSource.Repository.Tests`.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-19-rewrite-postgresql-design.md`.  Read with it, in this order of authority: the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`, the content map `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md`, the spec, and the rewrite guide `docs/superpowers/specs/2026-11-12-docs-overhaul/rewrite-guide.md`.  The ledger, worksheet and checker forms are in sections 5 to 7 of `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-00-guard-rails-design.md`.

## Global Constraints

- `E` is `docs/superpowers/specs/2026-11-12-docs-overhaul`; `D/` is `site/reference/databases/`; `P/` is `D/postgresql/`.
- Base: `db3285d4adf7542f6e80e96d7f0b8182cd091a9f`.  The ledger `$E/ledger/phase-19.tsv` opens with `# base db3285d4adf7542f6e80e96d7f0b8182cd091a9f`, then the header `unit	disposition	to	arg	words	note`.  After a sync with `main`, the base line takes the new merge base.
- Files this phase may change: the seven pages, `$E/ledger/phase-19.tsv`, `$E/merges/MP-09.md`, `$E/merges/MP-23.md`, and this plan.  No `toc.yml`, `AGENTS.md`, test, example or other page.
- No `<a id>` line is added or removed; a fenced block stays byte-identical (it may move up to open its section); no fenced block, tab group, page or heading is added; an h1 never changes; the order and level of headings stay.
- The words that files outside `site/` quote stay letter for letter: the heading "What a method reads and throws" and the bold lead-in "**A synchronous method needs a data source without multiplexing**" on `P/query-methods.md`.  Before rewording any other heading, run `git grep -n -F '"<heading>"' -- . ':!site' ':!docs/superpowers'`.
- The `UseNodaTime()` sample of `P/query-methods.md#method-results` stays byte-identical (its twin is `examples/PostgresExamples/Methods/StoreDataSource.cs`).
- Each limit (`limit-operator-on-oid`, `limit-many-partitions`, `limit-columns-treated-as-nullable`, `limit-connection-string-repository`) has three bold-labelled parts: `**What you see.**`, `**Why.**`, `**What to do.**`; its `###` heading is not reworded.
- Terms (rewrite guide 3.3): "can be null" / "never null" (keep "nullable" for C#), "current" / "out of date", "describes", "settings file", "folder".  Outputs in the order `repository`, `methods`, `models`, `sql`, in every list.  Never "choose a dialect", never "full support"; Azure SQL is "accepted, and not tested".
- Limiter words survive: not, only, never, unless, alone, at most, at least, except, whatever.  Keep "never" in "It never gives you a wrong value" and "unless it can show" on `P/nullability.md`.
- Effects, never internals: no describer, plan, `EXPLAIN` (except text the user writes or sees), heuristic, provenance, capture, sidecar field.
- A fact whose owner (another phase's page) lacks it stays on the page, and its row's note opens `no owner: <file>#<anchor>; <fact>`.
- Every commit passes `./pre-commit-validation.sh`, run after the last edit; commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- A list made from a sentence of conditions: "all of these" against "one of these" (nullability lists, method-results bullets).
- A sentence cut in two: each diagnostic id and limiter word with its own half.
- A restatement whose owner lacks a fact: the fact stays, with a `no owner:` note.
- A merged text (MP-23, MP-09) that drops a fact of one text: every row of "The facts" stands somewhere.
- Code-span counts on `P/nullability.md`: every function and type name as often as before, or a note says why.

---

### Task 1: `D/index.md`, `D/other.md` and MP-23

**Files:** Modify `site/reference/databases/index.md`, `site/reference/databases/other.md`, `$E/merges/MP-23.md`; create `$E/ledger/phase-19.tsv`.

- [ ] Write the ledger's base and header lines.
- [ ] MP-23: fill "The facts" (one row per fact of each text: the README's three paragraphs, the two bullets, the table, `postgresql.md` 473's clause, the `#azure-sql` paragraph), "Conflicts" (settled by root `AGENTS.md` rule 5, `tests/DatabaseTests/TestHarness/DefaultImages.cs`, `.github/workflows/nightly.yml`, `SqlServerContainer.ImageBuilds`, `SqlDiagnostics.cs` SQLSRC247/248/267/268, `ToolDiagnostics.cs` SQLSRC235, the csproj files of `Postgres.Npgsql8` (8.0.9) and `SqlServer.SqlClient61` (6.1.7), `Directory.Packages.props` (Npgsql 10.0.3, SqlClient 7.1.1)), and "The merged text".
- [ ] `#supported-versions`: the table, unchanged, then a short text under it that holds every fact the loose sentences hold that the table lacks (SQLSRC235 for an older server; the methods need the driver reference, linking `../query-methods.md#which-methods`; SQLSRC247/267 for an older driver, SQLSRC248/268 for a newer one only if the text needs them, or a link to each `types.md#which-…`).  The tool's runtime part of README 29 is cut as restated-from `site/reference/cli.md#requirements`; the EF Core part as restated-from `site/reference/databases/sqlserver/types.md#limit-ef-core-sqlclient`.  No loose sentence states a version.
- [ ] `#database-support`: keep the table; reword line 36 ("`models` or `sql`"), and point at `#azure-sql` so the row does not read as "supported".
- [ ] `D/other.md`: rewrite the two old paragraphs alone (`#not-described`, `#duckdb`); `#not-described` restates "the default output is `repository`" from `../settings.md#setting-output`, keeps SQLSRC209 with "from the tool and in the build", and says the output for these databases is `sql`; `#duckdb` keeps read as `postgres`, cannot be described, needs `sql`.  New text of phase 3 and 8 stays.
- [ ] Ledger rows for every changed unit (`merge-pair` MP-23 for the pair's units; the lifted nothing: no heading is lifted in MP-23).
- [ ] Verify: `$E/check/check.sh rewrite 19` exits 0; `SupportedVersionsTests` pass (`dotnet test --project tests/SqlSource.Repository.Tests --filter SupportedVersionsTests`); `git grep -n -i -E "choos(e|ing) (a|one|the) dialect|full support" -- site/reference/databases/index.md site/reference/databases/other.md` prints nothing.
- [ ] `./pre-commit-validation.sh`, then commit.

### Task 2: `P/index.md`

**Files:** Modify `site/reference/databases/postgresql/index.md`, `$E/ledger/phase-19.tsv`.

- [ ] Rewrite `#connecting` (restates "PostgreSQL 14 and later" from `../index.md#supported-versions`, keeps "an older server is SQLSRC235"; pooler in session or transaction mode, statement mode and `Multiplexing=true` are SQLSRC236, checked against `src/SqlSource.Tool/Describing/Postgres/ConnectionErrors.cs`), `#what-describing-does` (an owner: keep whole; `IMMUTABLE` can run; check `tests/DatabaseTests/Postgres/Describer/TransactionTests.cs`, `SessionTests.cs`), `#parameters` (SQLSRC226, SQLSRC227; check `ParameterTests.cs`), `#what-can-be-described`, `#models` (SQLSRC253 from the build), and the two limits in three parts.
- [ ] Ledger rows; verify with `check.sh rewrite 19`, the anchor, fence and limit loops of rewrite guide section 10; `./pre-commit-validation.sh`; commit.

### Task 3: `P/nullability.md`

**Files:** Modify `site/reference/databases/postgresql/nullability.md`, `$E/ledger/phase-19.tsv`.

- [ ] Rewrite `#nullability` as an owner, whole: split sentences over 25 words, keep every function and type name and every bracketed exception, keep "unless it can show" and "never gives you a wrong value".  Check each case against `src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.cs` and its partial files and `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs`, `ExpressionTests.cs`, `SetOperationTests.cs`, `WalkTests.cs`.  Say what makes a column one that can be null, never how the tool finds out.
- [ ] `limit-columns-treated-as-nullable` in three parts.
- [ ] `check.sh rewrite 19` prints no code span of this page whose count fell without a note.  Ledger rows; verify; `./pre-commit-validation.sh`; commit.

### Task 4: `P/types.md`

**Files:** Modify `site/reference/databases/postgresql/types.md`, `$E/ledger/phase-19.tsv`.

- [ ] `#which-npgsql`: restate "Npgsql 8 or later" from `../index.md#supported-versions`; keep what each version changes, SQLSRC247, SQLSRC248, SQLSRC249, SQLSRC273 and what a project with no Npgsql gets (check `src/SqlSource/Generation/Types/Drivers/NpgsqlProbe.cs`, `tests/SqlSource.Tests/Generator/NpgsqlProbeTests.cs`).
- [ ] `#type-table`: an owner, every row kept, a cell reworded only if false (check `src/SqlSource/Generation/Types/Maps/PostgresTypeMap.cs`, `tests/DatabaseTests/Postgres/EndToEnd/EveryTypeTests.cs`).  `#type-map` and `#type-notes` by the language rule.
- [ ] Ledger rows; verify; `./pre-commit-validation.sh`; commit.

### Task 5: `P/libraries.md`

**Files:** Modify `site/reference/databases/postgresql/libraries.md`, `$E/ledger/phase-19.tsv`.

- [ ] In each of `#nodatime`, `#nettopologysuite`, `#json`: cut how a library is turned on or off to a restatement of `../../library-types-and-json.md#setting-library-types`; keep which types the library gives on PostgreSQL, that a reference to `Npgsql.NodaTime` (and the like) turns its types on with no setting, and what the application must call (`UseNodaTime()` and the like).
- [ ] Ledger rows; verify; `./pre-commit-validation.sh`; commit.

### Task 6: `P/query-methods.md` and MP-09

**Files:** Modify `site/reference/databases/postgresql/query-methods.md`, `$E/merges/MP-09.md`, `$E/ledger/phase-19.tsv`.

- [ ] `#method-results`: one rule per bullet; the first bullet restates "a repository method takes no transaction" from `../../repositories.md#repository-transactions` and drops what Microsoft.Data.SqlClient does (owner `../../connection-methods.md#method-connection`), keeping what Npgsql does and the advice to pass the transaction; the last bullet restates `../../query-methods.md#limit-wrapped-connection`.  Keep the heading words, the bold lead-in that the test quotes, and the `UseNodaTime()` sample byte-identical.  Check against `tests/DatabaseTests/Postgres/EndToEnd/RunTimeTests.cs`, `StreamingQueryTests.cs`.
- [ ] `#array-parameters`: an owner, whole; keep `= ANY(@ids)` with one array and that `IN (@ids)` "is not what you want" (check `src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs`); keep the last bullet telling arrays from lists.
- [ ] `#collection-methods`: restate what a collection method is and 1,000 items to a round trip from `../../collection-methods.md#collection-methods`; keep what a failed item leaves in PostgreSQL.
- [ ] MP-09: fill the worksheet's three parts; merge the two bullets of `#limit-connection-string-repository` into one limit in three parts ("What you see" is an `InvalidCastException` for a NodaTime type, a PostGIS type or an enum); settle conflicts by `src/SqlSource/Generation/Repositories/ConnectionSource.cs`, `ConnectionResolver.cs`.  The heading is not reworded; no heading is lifted.
- [ ] Verify the two quoted-words greps of the spec's "Done when" print 1 each.  Ledger rows; verify; `./pre-commit-validation.sh`; commit.

### Task 7: The checks of the phase

- [ ] Run every check of rewrite guide section 10's table over the seven pages, and `tools/build-site.sh`.
- [ ] Write a `#` comment line in the ledger with each page's words, old and new, and the old text rewritten.
- [ ] Commit; then the two content reviewers, `branch-review:review`, and `./pre-commit-validation.sh --full`, in that order.
