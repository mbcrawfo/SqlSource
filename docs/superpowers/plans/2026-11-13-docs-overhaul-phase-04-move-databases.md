# Docs overhaul phase 4, move-databases: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `site/postgresql.md` and `site/sqlserver.md` become ten pages under `site/reference/databases/`, five for each database, with no word changed; `databases/index.md` gains the one table of supported versions; the 18 known-issue bullets become 14 limits and 4 pointers go; `SupportedVersionsTests` holds the table to the code, and `SiteStructureTests` holds the two folders to the same five file names.

**Architecture:** A verbatim move: a script kept in the scratchpad cuts the two pages by baseline line ranges (the tree at the base differs from the baseline only by retargeted links, so the line numbers hold), re-bases every relative link, sends each same-page anchor to the new page that holds it (keyed on page and anchor), and retargets every link to the two pages in `site/`, `README.md` and the rest of the repository.  New text is the eight h1 headings, the limit headings, the five h3 of SQL Server's `#method-results`, the table of `#supported-versions`, `#azure-sql`, the two bullets of `#connecting` and the troubleshooting entries.  Every unit gets a row of `ledger/phase-04.tsv`.

**Tech Stack:** Markdown, docfx (`tools/build-site.sh`), xUnit v3 with Shouldly (`tests/SqlSource.Repository.Tests`), the epic's checker (`check/check.sh`).

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-04-move-databases-design.md`, under the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md` and the map `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md`.

## Global Constraints

- Moved text changes no word but the links, the 20 heading levels of spec step 2 and the `position` mends of step 7; new text is plain language (ASD-STE100 as a guideline).
- No `--prerelease` and no sentence about previews (owner, p01 Q1/Q4).
- `AGENTS.md` sizes: under 8,192 bytes, one rule per bullet of at most 80 words (`AgentsFileTests`); `src/SqlSource.Tool/AGENTS.md` ends at 8,147 bytes.
- The bold words of pg 490, "A synchronous method needs a data source without multiplexing", stay byte-identical: `tests/DatabaseTests/Postgres.SyncAsync/KnownDifferences.cs` quotes them.
- `SupportedVersionsTests` pulls no container and adds no project reference: it reads `DefaultImages.cs`, `SqlServerContainer.cs` and `nightly.yml` as text.
- Every commit passes `./pre-commit-validation.sh`; `./format.sh` before validating.

## Where the tree differs from the spec

- `git diff 28e20e3c -- site/postgresql.md site/sqlserver.md` holds only links that phase 3 retargeted (pg 3, 48, 52, 57, 119, 163, 401, 433, 440, 465; ms 3, 20, 48, 84, 157, 390, 449, 453).  No feature work, so no `@` rows for these pages.
- `guides/troubleshooting.md`'s sentence of known issues also links settings, the generator and the tool; only its PostgreSQL and SQL Server links go, and the sentence's list closes on "and [the tool]".

## Review Focus

- A same-page anchor shared by both databases and `generator.md` (`models`, `query-methods`, `method-parameters`, `collection-methods`, `nullability`, `type-table`, `json`, …) sent to the wrong database: the script keys on page and anchor; docfx's `InvalidBookmark` and `UserDocsLinksTests` pin a dead target, not a wrong database, so the script's table is re-read against spec step 7.
- A README site URL three folders deep refused by `UserDocsLinksTests`: Task 1 runs the test.
- `SupportedVersionsTests` passing vacuously (no row or no cell found): it gets a test that the reading finds both rows and every column, and is proved by changing a number by hand.
- The tab-group check passing vacuously because no page has a database tab yet: it gets a test of made-up lines.
- The five h3 of `#method-results` taken as limits by `SiteStructureTests.UnanchoredLimits`: they stand under `## What a method reads and throws`, not `## Limits`, so the test does not read them; the test run pins it.

---

### Task 1: The ten pages, the index, the sidebar and every link

**Files:**
- Create: `site/reference/databases/{postgresql,sqlserver}/{index,nullability,types,libraries,query-methods}.md`
- Modify: `site/reference/databases/index.md`, `site/reference/toc.yml`, `site/guides/troubleshooting.md`; the links of `site/{diagnostics,generator,index,recipes,settings,tool}.md` and `site/reference/parameters.md`; 19 URLs of `README.md`; the outside references of spec step 9
- Delete: `site/postgresql.md`, `site/sqlserver.md`

- [ ] **Step 1:** Script the cut of spec step 2's table, with the 20 heading levels, the eight h1 of step 2, the five h3 of step 3, and the `## Limits` sections of step 4 (each bullet under an h3 with its `limit-` anchor, in today's order; the four pointers and both "Known issues" headings go).
- [ ] **Step 2:** In the same script, re-base the links of moved text and map each same-page anchor by the anchor table of step 7; the `#known-issues` links by step 7's table (pg 493, ms 312, 485, 488 stay at `../../../generator.md#known-issues`).
- [ ] **Step 3:** Write the two `#connecting` bullets of step 6 and fill `databases/index.md` by step 5 (order: Supported versions, Database support, Azure SQL).
- [ ] **Step 4:** Retarget every other link to the two pages in `site/` (each by the page of its anchor; no anchor → `index.md`), `generator.md` 702 and `recipes.md` 410 to `#limit-connection-string-repository`, `reference/parameters.md`'s link to `#limit-list-in-an-aggregate`; README URLs by step 8; outside references by step 9 (C-05 in `TD-0067`, `MethodShapeTests.cs` 538, `src/SqlSource.Tool/AGENTS.md` 15, `./format.sh` for `KnownDifferences.cs`).
- [ ] **Step 5:** The sidebar of step 10; the 15 entries of `guides/troubleshooting.md` and its sentence; `git rm` both pages.
- [ ] **Step 6:** Mend the words the split makes wrong: `grep -n -E "This page|above|below|later bullet" site/reference/databases/*/*.md`, each hit read and the fewest words changed; record each for the ledger.
- [ ] **Step 7:** `tools/build-site.sh` (0 warnings); `dotnet test tests/SqlSource.Repository.Tests` passes but `SiteStructureTests.RootPages_…` (fixed in Task 2).  Commit.

### Task 2: `SiteStructureTests` and `SupportedVersionsTests`

**Files:**
- Modify: `tests/SqlSource.Repository.Tests/Documentation/SiteStructureTests.cs`
- Create: `tests/SqlSource.Repository.Tests/Documentation/SupportedVersionsTests.cs`

**Interfaces:**
- Consumes: `UserDocs.SitePages`, `UserDocs.Read(string[]).TabList` (`(int Line, string Id)` of each tab heading), `RepositoryRoot.Path(...)`, `PostgresSession.MinimumMajorVersion` (`SqlSource.Tool.Describing.Postgres`), `SqlServerErrors.MinimumRelease` (`"SQL Server 2017"`), `SqlDiagnostics.NpgsqlTooOld.Title` / `SqlClientTooOld.Title`.
- Produces: `SupportedVersionsTests.ServerMinimums()` → `(int Postgres, int SqlServer)` read from the table, for phases 10 and 11.

- [ ] **Step 1:** `PagesNotYetMoved` loses `postgresql.md` and `sqlserver.md`.
- [ ] **Step 2:** New `DatabaseFolders_EachHoldsTheFivePages`: every folder directly under `site/reference/databases/` holds exactly `index.md`, `nullability.md`, `types.md`, `libraries.md`, `query-methods.md` (`AGENTS.md` and `CLAUDE*.md` aside), and at least two folders are found.
- [ ] **Step 3:** New `TabGroups_ThatNameADatabase_NameEveryDatabase`, over a helper `TabGroupsMissingADatabase(page, lines, databases)`: a tab group (consecutive tab headings with no other heading between, at one heading level) in which one tab id is a database folder's name has a tab for every database folder; plus a test of the helper on made-up lines (a group with `postgresql` alone is reported; one with both, and one with neither, are not).
- [ ] **Step 4:** `SupportedVersionsTests`: read the first table after `<a id="supported-versions"></a>` of `site/reference/databases/index.md`; find a row by its first cell and a cell by its header; first number of a "supported" cell, every number of a "tested" cell.  Facts: PostgreSQL supported = `MinimumMajorVersion` (14); SQL Server supported = the year in `MinimumRelease` (2017); PostgreSQL tested = majors of `PostgresFloor`, `Postgres` (from `DefaultImages.cs` as text) and each `image: postgres:` line of `.github/workflows/nightly.yml` (14, 15, 16, 17, 18); SQL Server tested = years in the keys of `ImageBuilds` (from `SqlServerContainer.cs` as text: 2017, 2019, 2022, 2025); drivers supported = the version ending each descriptor title (8, 6.1); the least tested equals the supported minimum.  Plus `Reading_OfTheTable_FindsBothRowsAndEveryColumn`.  A comment on each test says what fails it.
- [ ] **Step 5:** Run; prove: change "14 or later" to "13 or later" → fails; rename `site/reference/databases/sqlserver/types.md` → the folder test fails; restore.  Commit.

### Task 3: Rules and documents

**Files:** `site/reference/databases/AGENTS.md`, `site/AGENTS.md`, `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`, `tests/README.md` line 13.

- [ ] **Step 1:** Write them by spec step 12; `grep -n -E "postgresql|sqlserver" site/AGENTS.md site/*/AGENTS.md` and rewrite any other hit.
- [ ] **Step 2:** `wc -c` each; `dotnet test tests/SqlSource.Repository.Tests --filter AgentsFileTests` passes.  Commit.

### Task 4: Ledger and worksheets

**Files:** `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-04.tsv`, `merges/MP-09.md`, `merges/MP-23.md`.

- [ ] **Step 1:** `# base <sha of the base>`; a row for each unit of the two pages, with rows 1-10 of the spec's "The ledger".
- [ ] **Step 2:** Open `MP-09.md` with pg 528; add pg 25, ms 28 and the clause of pg 473 to `MP-23.md`.
- [ ] **Step 3:** `check.sh coverage` passes; `check.sh move 04` leaves over only rows 2, 4, 9 and 10.  Commit.

### Task 5: Done-when sweep

- [ ] Every check of the spec's "Done when"; `./format.sh`; `./pre-commit-validation.sh`; the SQL Server project on its floor image (`MethodShapeTests.cs` changed).  Then the two content reviews, `branch-review:review`, `./pre-commit-validation.sh --full`.
