# Docs overhaul phase 9, write-guides: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The guide text that does not exist today is written, in plain language and the fixed shape: three whole pages (`nullability.md`, `output-modes.md`, `other-databases.md` but its moved bullet), the Dapper walkthrough, the 22 new sections of the spec's step 7, and the troubleshooting entries that are not a limit's.

**Architecture:** New text only, written by hand, page by page, into the guides that phase 8 left.  No line that an earlier phase placed changes.  Each section restates its owners' rules and links them; each sample comes from the example or test file the spec names, or is written and compiled against the built package.  Each section gets a `new` row in `ledger/phase-09.tsv`.

**Tech Stack:** Markdown with docfx tabs and alerts, docfx (`tools/build-site.sh`), xUnit v3 (`tests/SqlSource.Repository.Tests`), the epic's checker (`check/check.sh`), a scratch .NET project against `artifacts/packages` for written samples.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-09-write-guides-design.md`, under the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md` and the map `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md` (sections 1, 3 "Guide pages", 4 "The 46 known-issue bullets", 5 "Corrections" last paragraph, 10 "Phase 9").

## Global Constraints

- Do not change, move or delete a line that an earlier phase placed: `git diff --numstat d531a46a -- site` shows no deleted line.  New text goes between lines, never inside one.
- At most 11,800 words: `git diff d531a46a -- site/guides | grep '^+[^+]' | wc -w`.  Each task has a budget below; aim at 85% of it.
- The shape of spec step 1: "Use this when", "What you get", "The rules you need" are bold words opening a paragraph; `## If it does not work` is the last h2 and has no `<a id>`.  A new section inside a page of moved text: one sentence on when the reader needs it, the sample, "What you get", "The rules you need"; its limit or error entries go under `## If it does not work` at the foot of the page, made if missing.
- Each rule ends with a link to its owner's anchor; the link opens the section that states it.  No rule without a link.  A guide shows a setting in use and gives no value list, default or precedence (`output-modes.md` alone names the four outputs).
- The language of spec step 2 and `site/AGENTS.md` "Writing": "you", one instruction a sentence, ≤20 words a step and ≤25 a description, conditions first, "must"/"can", ≤6 sentences a paragraph.  A restated rule keeps each "not", "only", "never" and "unless" of its owner.
- Tabs (spec step 3): `# [PostgreSQL](#tab/postgresql)` and `# [SQL Server](#tab/sqlserver)`, a sample or ≤3 sentences each, no heading or `<a id>` inside, the group ends with a blank line then `---`.  A tab group names both databases.
- Samples (spec step 4): from the file the spec names, shortened but no kept line changed; a sample from `Repository/UserRepository.sql` drops `-- generator: method-type=both`, so the method is `GetUser`, asynchronous.  A `sql` block's info string is `sql postgres`, `sql mssql`, `sql mysql`, `sql sqlite`, `fragment` or `error`.  No fence in a blockquote or alert.
- Only these anchors are new: the map's for `guides/` (6 on `nullability.md`, 3 on `output-modes.md`, 5 on `other-databases.md`, the 22 of spec step 7).
- Link no `start/` page but `../start/transactions.md#queries-in-a-transaction`, `../start/testing.md` and `../start/keep-up-to-date.md#after-a-migration`.
- Azure SQL: "accepted, and not tested", never "supported".  No status words ("works today", "no effect yet", "coming"), no `--prerelease`, no "recipe" in new text, no internals (the grep of `site/AGENTS.md`).
- `describe-on-save.md`: add no `jsonc` block (`EditorRecipeTests`); quote the watch's two marker lines exactly as `R/describing.md#describe-on-save` does.
- Every commit passes `./pre-commit-validation.sh`, after `./format.sh`.

## Where the tree differs from the spec

- None found that changes the work.  Every owner anchor the spec names exists at `d531a46a`; the 36 limits already have their troubleshooting entries (the done-when grep prints 36 before this phase).
- `G/other-databases.md`'s step 4 heading, "Set the dialect of your database and the output `sql`", is phase 8's line and has no number.  So no heading of the page carries a number: the page's opening gives the five steps as a numbered list that links each section, and each section's first words name its step ("Step 1."), so the steps stay numbered without changing phase 8's line.
- The Dapper walkthrough has no anchors of its own (the map names none): its six steps are a numbered list under `## Models for Dapper, start to end`, above the moved paragraph, with each step's fenced blocks indented inside its item.

## The ledger

`docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-09.tsv`, first line `# base d531a46aa8c916711965c1021c5c81e85cd1c5f4`, then the header `unit	disposition	to	arg	words	note`.  One row for each section or page of spec steps 5 and 7, each troubleshooting entry, and the two sidebar entries:

`-	new	site/guides/<page>.md#<anchor>	<owners restated, comma-separated, as site/... paths with #anchor>	<wc -w of the section's added lines>	<sample source, or "written, to compile", or "no sample">`

## Review Focus

- A rule that drops "not", "only", "never" or "unless" of its owner, or states more than the owner does: the accuracy reviewer, owner by owner.
- A written sample that does not compile or a SQLite sample that does not run: the implementer compiles each in a scratch project against `artifacts/packages` before committing, and names it in the ledger.
- A tab group that reads right for one database only: read it once as each.
- A moved line changed by an edit that touched its neighbour: `git diff --numstat d531a46a -- site` after each task.
- The word budget overrun by `<a id>` lines and samples: count after each task.

---

### Task 1: `G/nullability.md`

**Files:** Create `site/guides/nullability.md`.  Modify `site/guides/toc.yml` (Queries, after `lists.md`), the ledger.

- [ ] **Step 1:** Read the owners of spec step 5 and the facts of spec step 6 on `!`, `?`, `-- column:` and `-- param:`.  Write the page: title "Fix a nullable property", the h2 anchors `why`, `not-null`, `nullable`, `parameters`, `on-postgresql`, `on-sql-server` in that order, then `## If it does not work` (the two run-time messages, SQLSRC260, SQLSRC227).  The alias mark of `not-null` is a tab group (`AS "total!"`, `AS [total!]`).  Five causes for each database, each from its `nullability.md` list.  Budget 1,000.
- [ ] **Step 2:** Sidebar entry; ledger rows; `dotnet test tests/SqlSource.Repository.Tests`; `tools/build-site.sh` with no warning; the numstat and word checks.
- [ ] **Step 3:** `./format.sh`; `./pre-commit-validation.sh`; commit.

### Task 2: `G/output-modes.md`, `G/other-databases.md`, the Dapper walkthrough

**Files:** Create `site/guides/output-modes.md`.  Modify `site/guides/other-databases.md`, `site/guides/dapper.md` (between the `models` heading and its first paragraph), `site/guides/toc.yml` (Other ways to use your SQL, `output-modes.md` first), the ledger.

- [ ] **Step 1:** `output-modes.md`: h2 `the-four-outputs` (one table, `repository`, `methods`, `models`, `sql`, columns "You get" and "Use it when", decision 4's frame, and a sentence that most readers keep the default), `set-the-output` (query, file, folder samples from the four example folders; "the nearest place wins" with a link to `#precedence`), `which-need-a-database` (the two cases of `sql` in a described project, spec step 6); closing links to `S/transactions.md#queries-in-a-transaction`, `dapper.md`, `other-databases.md`.  Budget 800.
- [ ] **Step 2:** `other-databases.md`: the opening and a numbered list of the five steps; new h2 `install` (the package alone), `write-a-query`, `mark-a-class` before the moved `dialect-and-output`, which gains text after the bullet only; `run-the-constant` after it, then `what-you-do-not-get` (no models, methods or schema check; SQLSRC209; DuckDB as `postgres` with output `sql`).  Never offer `models`, `methods` or `repository`.  The sample is SQLite with `Microsoft.Data.Sqlite`, modelled on `examples/PostgresExamples/Sql/CountQueries.{sql,cs}`; compile and run it in a scratch project against `artifacts/packages`.  Budget 650.
- [ ] **Step 3:** The Dapper walkthrough, about 400 words: "Before you start" (the install commands of `R/cli.md#install-the-tool` and the package, `dotnet add package Dapper`, the driver in a tab), then steps 1 to 6 as the spec numbers them, from `Models/Users.sql`, `Models/sqlsource.json`, `Models/StoreQueries.cs`, `DapperSetup.cs`, `.env.example` of both examples; step 3 ends with a link to `#sql-only`.  The moved "step 5", "step 3", "steps 1 to 3" are then true.
- [ ] **Step 4:** As Task 1 steps 2 and 3.

### Task 3: Queries and running queries

**Files:** Modify `site/guides/lists.md`, `dynamic-sql.md`, `many-rows.md`, `sync-and-streaming.md`, `repository-connection.md`, the ledger.

- [ ] **Step 1:** The sections of spec step 7 for these pages, each by its row: `postgresql-arrays`, `large-and-empty-lists`; `clause`, `safety` (a docfx alert, no fence inside); `transaction`, `failures`; `stream-rows`; `mark-the-member`.  Each after the moved sections, in the map's h2 order, with entries under `## If it does not work` where a rule has a limit or an error.  Budget 3,450.
- [ ] **Step 2:** As Task 1 steps 2 and 3, and compile each written sample.

### Task 4: Naming, types and several databases

**Files:** Modify `site/guides/naming-and-placement.md` (`async-names`, `model-names` before the moved `model-namespace-task`; `record-or-class`, `collection-type-task`, `placement` after), `custom-types.md` (`vogen` as numbered steps, `enum` with the two databases as tabs or two parts), `json.md` (`json-in-text`), `several-databases.md` (`repository-on-two` between the two moved sections), the ledger.

- [ ] **Step 1:** The sections of spec step 7 for these pages, each by its row.  Budget 3,150.
- [ ] **Step 2:** As Task 1 steps 2 and 3, and compile each written sample.

### Task 5: Projects, workflow, troubleshooting

**Files:** Modify `site/guides/folders-and-files.md` (`class-for-a-folder`), `several-projects.md` (`one-file-two-projects`), `describe-on-save.md` (`start-the-watch`, first h2), `ci.md` (`exit-code`), `report-a-bug.md` (`what-is-safe`, between the two moved sections), `troubleshooting.md`, the ledger.

- [ ] **Step 1:** The sections of spec step 7 for these pages, each by its row.
- [ ] **Step 2:** `troubleshooting.md`: at most six entries that are not a limit's, each one or two sentences and one link, under the h2 that fits: the two null messages to `nullability.md#nullable`; a nullable property to `nullability.md`; a wrong model that is SqlSource's mistake to `report-a-bug.md#report-a-bug`; SQLSRC209 on another database to `other-databases.md`; SQLSRC241 or SQLSRC244 after a pull to `../reference/sidecars.md#describing-again`.  Confirm the 36 limits and 3 pointers each have an entry.  Budget 2,350 with the sections.
- [ ] **Step 3:** As Task 1 steps 2 and 3.

### Task 6: Checks, reviews, push

- [ ] **Step 1:** Every done-when line of the spec: 20 guides, the 36 anchors declared once, no deleted line, ≤11,800 words, 36 limits, the Azure grep, the internals grep, the `start/` link grep, `check coverage` passes and `check move 03`…`08` print what they printed at `d531a46a`, repository tests, `tools/build-site.sh`.
- [ ] **Step 2:** The two Opus content reviews (no-loss, accuracy), fixes; `/branch-review:review --base origin/main` to clean; `./pre-commit-validation.sh --full`.  A fix reruns the later steps.
- [ ] **Step 3:** Word counts by page (`wc -w`) into the PR description; `p09-notes.md` in the shared notes folder.
