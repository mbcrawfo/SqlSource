# Docs overhaul phase 10, get-started: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The nine pages of `site/start/` and the finished landing page, `site/index.md`, written new in plain language, so that a junior developer with one PostgreSQL or SQL Server database reads them in order and ends with a tested application; `site/start/concepts.md` removed and every link parked for this phase retargeted.

**Architecture:** New text, written by hand page by page, on the running example of `examples/*/Repository`.  Every "what you see" is copied from a walk run first against the packed packages, on both databases, in a scratch folder outside the repository.  Each statement restates a reference owner and links it in the page's closing "Rules:" line.  `SupportedVersionsTests` gains the two restatements of the minimum server versions.  The ledger and the MP-13 worksheet record each unit.

**Tech Stack:** Markdown with docfx tabs, docfx (`tools/build-site.sh`), xUnit v3 (`tests/SqlSource.Repository.Tests`), the epic's checker (`check/check.sh`), Docker images of `DefaultImages` for the walk, a scratch .NET project against `artifacts/packages`.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-10-get-started-design.md`, under the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md` and the map `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md` (sections 1 "start/", 3 "index.md" and "Get started pages", 4 maps of `README.md`, `site/README.md`, `site/concepts.md`, 5 MP-13 and C-06, 10 "Phases 10 and 11").

**Execution:** native, in this session.  The ten pages share one example, one set of terms and one voice, so one writer keeps them consistent; the two content reviewers and `branch-review:review` are the independent checks.

## Global Constraints

- The line: a topic is on a `start/` page only when an application with one PostgreSQL or SQL Server database and the default `repository` output needs it before its first release.  Anything else is a link in the closing "Rules:" line.
- The fixed shape of a `start/` page (map section 3): opening sentence with the page's search words (map section 1); short sections in the reader's order, each "what to do, one whole sample, what you see"; tabs for differences; `## If it does not work` (no anchor); `## What you have now` (no anchor); a "Rules:" line; "Next:" as the last line.  No `## Limits`, no `setting-` anchor, no table of values or defaults.
- Anchors: exactly those of spec step 5 for each page, in that order, and for `index.md` `databases`, `find-what-you-need`, `errors`, `the-three-parts`.  `transactions.md` keeps `queries-in-a-transaction`; `keep-up-to-date.md` keeps `after-a-migration`.
- Tabs: `# [PostgreSQL](#tab/postgresql)` then `# [SQL Server](#tab/sqlserver)`, closed by `---`; a sample or at most three sentences; no heading, `<a id>` or link target inside; the text outside is true for both databases; no "as you chose".
- Language (outline "The language", `site/AGENTS.md` "Writing"): one instruction in a sentence; at most 20 words in a step and 25 in a description; active voice, present tense; condition first; "must"/"can"; a list for cases; at most six sentences a paragraph.  Terms: class, model, query parameter, the tool, describe, sidecar, folder.
- No `--prerelease` and no sentence about previews (owner, p01 Q4: it overrides spec lines 143-144 and the map's sentence for `install.md`).
- Never "choose"/"pick"/"select" before "dialect" or "output"; `first-query.md` names only `postgres` and `mssql`, never `ansi`, never the word "output", `--connection`, `--force`, `--check`, `--watch` or `SqlSourceOnline`.  "Output" appears on `transactions.md` alone.
- Azure SQL is "accepted, and not tested", never "supported".  No status words; no internals (the grep of `site/AGENTS.md`).
- Samples: the example's marker lines and SQL byte for byte, less the non-marker comment lines, the `namespace` line and `-- generator: method-type=both`; info string `sql postgres` (`sql mssql` for a SQL Server-only query).  Connection strings and paths of the baseline README.
- An error id is a link to `../diagnostics.md#sqlsrcnnn`, with the fix in full on the page; each first-hour id also stands on `errors.md#first-errors`, outside any tab.
- No word of `site/guides/` or `site/reference/` changes, but the "step 5" link target of `reference/cli.md#install-the-tool`.
- Every commit passes `./pre-commit-validation.sh`, after `./format.sh`.

## Where the tree differs from the spec

- `--prerelease`: the owner ruled it out for the whole epic (p01 Q4).  `install.md` has no flag and no sentence about previews; the walk adds the flag, or the version, only because the local feed holds a prerelease package.
- `SupportedVersionsTests.ServerMinimums()` exists since phase 4, so the new test uses it rather than a reader of its own.
- `site/start/testing.md` and `transactions.md` hold their phase 8 units, and `keep-up-to-date.md` its phase 5 unit, as the spec says; `site/index.md` holds phase 8's mends (`[Guides]`).

## Review Focus

- A step that does not work when followed to the letter on one of the two databases: the walk runs every step on both, and every console block is copied from it.
- Text outside a tab that is true for PostgreSQL alone (data source, `NpgsqlDataSource`, `text`): read each page once as each database.
- A restated rule that says more than its owner, or drops "not", "only", "never" or "unless": restate from the owner's words, and the accuracy reviewer checks owner by owner.
- The table-model trap: a reader whose `users` has exactly `id`, `name`, `email` gets `UsersDto`; the walk's table has `created_at` too, and the trap is caused once.
- A link into a tab or to a missing anchor: `UserDocsLinksTests` and the anchor grep of the done-when list.

---

### Task 1: The walk

**Files:** none in the repository; a scratch folder in the session's scratchpad.

- [ ] **Step 1:** `dotnet pack SqlSource.slnx --output artifacts/packages`.  Start `postgres:18.6` and `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04`; create `users` (`id`, `name`, `email`, `created_at`, none nullable) in a database `app` with a login `app`/`secret`.
- [ ] **Step 2:** For each database, a console project with a `nuget.config` whose only source is `artifacts/packages` and whose packages folder is its own.  Follow the drafted steps: the package and driver, the tool manifest and install, `Queries/UserRepository.sql`, `UserRepository.cs`, `sqlsource.json`, `.env`, `dotnet build`, `dotnet sqlsource describe`, the calling code, a run that returns a row.  Keep every printed line.
- [ ] **Step 3:** Cause each cheap trap and keep its output: no dialect (SQLSRC209), no sidecar (SQLSRC241), a changed query with no connection (SQLSRC244), no restore (SQLSRC220), a wrong password (SQLSRC224), the `.env` at the root, a table of exactly the query's columns (`UsersDto`), and on SQL Server EF Core 9 (SQLSRC267).
- [ ] **Step 4:** Compile the new samples on both: `FindUsers`, the list after `IN`, the not-null column, the transaction's SQL Server tab, the fake and the mock.  Write the outputs to `p10-walk.md` in the shared notes folder for the reviewers.

### Task 2: `install.md`, `first-query.md`, `how-it-works.md`

**Files:** Create `site/start/install.md`, `first-query.md`, `how-it-works.md`.  Delete `site/start/concepts.md`.  Modify `site/start/toc.yml` (the whole list of spec step 9), `docs/architecture/README.md:46` (`site/start/how-it-works.md#words`), `site/reference/cli.md` (the "step 5" target, `../start/first-query.md#describe`).

- [ ] **Step 1:** Write the three pages to spec step 5, with the walk's outputs; `how-it-works.md#picture` holds `concepts.md` 5-15 byte for byte.
- [ ] **Step 2:** `git grep -n "concepts\.md" -- . ':!docs/superpowers'` finds nothing but `index.md` and `site/start/AGENTS.md`, which Tasks 4 and 5 rewrite.

### Task 3: `write-queries.md`, `call-queries.md`, `transactions.md`, `testing.md`, `keep-up-to-date.md`, `errors.md`

**Files:** Create `site/start/write-queries.md`, `call-queries.md`, `errors.md`.  Rewrite `site/start/transactions.md`, `testing.md`, `keep-up-to-date.md`.  Modify `docs/superpowers/specs/2026-11-12-docs-overhaul/merges/MP-13.md` ("The merged text": the new section, and for each difference its owner).

- [ ] **Step 1:** Write the six pages to spec step 5.  `transactions.md` keeps the recipe's PostgreSQL sample byte for byte as its first tab; the SQL Server tab is new.  `keep-up-to-date.md`'s console block takes the summary line of `first-query.md` and the walk's numbers.  `errors.md` ends "Next: [What do you want to do?](../index.md#find-what-you-need)".
- [ ] **Step 2:** The cause of each `first-errors` row agrees with its section's bold title.

### Task 4: `index.md`

**Files:** Rewrite `site/index.md`.

- [ ] **Step 1:** The eight items of map section 3, in order: the paragraph; the sample pair; `## Does it work with my database?` (`databases`, four rows); "Start here: [Install](start/install.md)" and the line for the other readers; `## What do you want to do?` (`find-what-you-need`, the 16 rows of spec step 6); `## A build error?` (`errors`); `## The three parts` (`the-three-parts`); the two examples.  C-06 and the glossary sentence go.

### Task 5: `SupportedVersionsTests`, `AGENTS.md` files

**Files:** Modify `tests/SqlSource.Repository.Tests/Documentation/SupportedVersionsTests.cs`, `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`, `site/start/AGENTS.md`, `site/AGENTS.md` (the "Feature that meets the line for Get started" row).

- [ ] **Step 1: The failing test.**  `[Theory] [InlineData("site/index.md")] [InlineData("site/start/install.md")] Restatement_OfTheMinimumServerVersions_IsTheTables(string page)`: every "PostgreSQL *n* or later" of the page is `ServerMinimums().Postgres` and every "SQL Server *yyyy* or later" is `ServerMinimums().SqlServer`, and the page has at least one of each.  A `[Fact]` on made-up lines proves the reader: it finds `PostgreSQL 14 or later` and `SQL Server 2017 or later`, reads `PostgreSQL 13 or later` as 13, and finds nothing in a line with neither.
- [ ] **Step 2:** Run the theory before the pages exist: it fails.  After Tasks 2 and 4 it passes; change 14 to 13 on `install.md`, see it fail, change it back.
- [ ] **Step 3:** `site/start/AGENTS.md` takes the nine rules of spec step 9 in its words, with the maintenance footer; the `site/AGENTS.md` row reads "A part of one `start/` page; its rules in Reference"; the test folder's `AGENTS.md` names the restatements.

### Task 6: Ledger, checks, commit

**Files:** Create `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-10.tsv` (base `76475a62`).

- [ ] **Step 1:** The rows of the spec's "The ledger": the 43 due units of `README.md` 1-130 and `site/concepts.md` that `check coverage` lists; the units of `site/README.md` now on `index.md`; the three placed units; one `new` row per section of each page; each changed fenced block.
- [ ] **Step 2:** `check.sh coverage` passes; `check.sh rewrite 10` is read, and each lost "not"/"only"/"never"/"unless", code span, id or numeral has an owner in the ledger.
- [ ] **Step 3:** The done-when greps; `tools/build-site.sh` with no warning, both tabs and the diagram in `artifacts/site`; `dotnet test tests/SqlSource.Repository.Tests`; `wc -w` by page; the list of long sentences.
- [ ] **Step 4:** `./format.sh`; `./pre-commit-validation.sh`; commit.

### Task 7: Reviews

- [ ] **Step 1:** The no-loss and the accuracy reviewers (Opus subagents with the spec's "For the reviewers"; the accuracy reviewer repeats the walk).  Fix every finding, with `corrected` rows where a false statement of today is found.
- [ ] **Step 2:** `/branch-review:review --base origin/main` to clean; `./pre-commit-validation.sh --full`; ask to push.
