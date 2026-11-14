# Docs overhaul phase 3, skeleton: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `site/` its final shape (a landing page, `start/`, `guides/` and `reference/` with a sidebar each), move `sql-files.md`, `parameters-and-tokens.md`, `dialects.md` and `errors.md` into `reference/`, and land the rules and `SiteStructureTests` that keep the shape.

**Architecture:** A verbatim move: pages are cut by line ranges of the tree at the base (which equal the baseline's for the three moved pages), links are retargeted by the tables of the spec's step 4, and every unit gets a row of `ledger/phase-03.tsv`.  The tests change only where the root page and the folders change.

**Tech Stack:** Markdown, docfx 2.81 (`tools/build-site.sh`), xUnit v3 with Shouldly (`tests/SqlSource.Repository.Tests`), the epic's checker (`check/check.sh`).

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-03-skeleton-design.md`, under the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md` and the map `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md`.

## Global Constraints

- Moved text changes no word but the links, the heading levels the spec names, and the `position` mends of step 4; every new sentence is plain language (ASD-STE100 as a guideline).
- No `--prerelease` and no sentence about previews anywhere (owner, p01 Q1/Q4): the spec's `site/AGENTS.md` bullet on `--prerelease` is replaced by today's "No `--prerelease`" rule.
- `AGENTS.md` sizes: root under 16,384 bytes, every other under 8,192; one rule per bullet of at most 80 words (`AgentsFileTests`).
- `diagnostics.html#sqlsrcNNN` at the site's root resolves at every commit.
- Every commit passes `./pre-commit-validation.sh`; `./format.sh` before validating.

## Where the tree differs from the spec

- `site/README.md`: phase 2 added a row at line 57 and a bullet at 72, and phase 1 removed "What works today".  Baseline line 57 (the known-issues row) is line 58 at the base; baseline 107-119 ("The pages") is 60-73; baseline 112 and 113 are 65 and 66.  `sql-files.md`, `parameters-and-tokens.md` and `dialects.md` are unchanged since the baseline (`git diff 28e20e3c` is empty for them), so the spec's line numbers hold for them.
- Root `AGENTS.md` is 15,003 bytes at the base (the spec measured 14,925).
- `site/AGENTS.md` holds two rules that phase 2 added that the spec's block words otherwise or lacks: the "Database" line clause ("when it is an error of describing a query against its database") and the bullet on the lists of `errors.md` (`#who-reports`, `#severity`, `#where-an-error-is-reported`).  Both are kept, in today's words, with `reference/errors.md`.

## Review Focus

- A bare link that resolves but to the wrong page (spec "Traps"): each row of the step 4 table "links these rules do not settle" is done by hand and re-read against the table.
- `site/index.md` keeping its front matter: docfx would redirect the root to a page that is gone, silently.  Task 1 checks `head -1 site/index.md`.
- A same-page link `#x` of a split page left pointing at the wrong page: docfx warns (`InvalidBookmark`), so `tools/build-site.sh` with 0 warnings pins it.
- A `toc.yml` name with `: ` unquoted: docfx or YAML parse fails; the build pins it.
- `SiteStructureTests` passing vacuously: it gets a test that its reading finds pages and limits, and is proved by adding `site/extra.md`.

---

### Task 1: Folders, sidebars, landing page and the moves

**Files:**
- Create: `site/start/toc.yml`, `site/guides/toc.yml`, `site/reference/toc.yml`, `site/reference/{sql-files,parameters,tokens,dialects,sidecars}.md`, `site/reference/databases/{index,other}.md`, `site/guides/troubleshooting.md`
- Move: `site/README.md` → `site/index.md` (`git mv -f`), `site/errors.md` → `site/reference/errors.md` (`git mv`)
- Delete: `site/sql-files.md`, `site/parameters-and-tokens.md`, `site/dialects.md`
- Modify: `site/toc.yml`; links in `site/{concepts,diagnostics,generator,postgresql,recipes,settings,sqlserver,tool}.md`; eleven site URLs of `README.md`
- Modify (tests of the root page): `tests/SqlSource.Repository.Tests/Documentation/UserDocs.cs`, `SiteTocTests.cs`, `UserDocsReadingTests.cs`; `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs` (path of the table-of-areas test)

- [ ] **Step 1:** Write the four `toc.yml` exactly as spec step 2.
- [ ] **Step 2:** Make the landing page by spec step 3: `git mv -f site/README.md site/index.md`, then the two anchors, line 58's one link, lines 65-66's extra links, and the step 4 path changes.  `head -1 site/index.md` prints `# SqlSource manual`.
- [ ] **Step 3:** Cut the three pages into the seven pages of spec step 4 by line ranges with a script kept in the scratchpad; add the new headings, anchors and sentences; promote the three tokens headings; `git mv site/errors.md site/reference/errors.md` and re-path its links (`../diagnostics.md#…`, `../tool.md#…`).
- [ ] **Step 4:** Retarget every link in `site/` and `README.md` by the anchor table and the hand table of step 4; mend the words a split makes wrong (each noted for the ledger).
- [ ] **Step 5:** Write `site/guides/troubleshooting.md` and the two limits by spec step 5.
- [ ] **Step 6:** Change the root-page tests by spec step 7's table: root is `site/index.md` (with its anchor); `SitePages` leaves out `AGENTS.md`/`CLAUDE*.md` alone; `SiteTocTests` skips `site/index.md`; the reading tests' rows; `DiagnosticsDocumentTests` reads `site/reference/errors.md` and expects `../diagnostics.md#sqlsrcNNN`.
- [ ] **Step 7:** Run `tools/build-site.sh` (expect `0 warning(s)`, `artifacts/site/index.html` and `reference/databases/other.html`, no `README.html`) and `dotnet test tests/SqlSource.Repository.Tests` (expect pass, but `InboundSiteReferenceTests`, fixed in Task 3).
- [ ] **Step 8:** Commit.

### Task 2: `SiteStructureTests`

**Files:**
- Create: `tests/SqlSource.Repository.Tests/Documentation/SiteStructureTests.cs`

**Interfaces:**
- Consumes: `UserDocs.SitePages`, `UserDocs.Anchors(file)`, `UserDocs.Links`/`Resolve` (whatever phase 0 named them).
- Produces: `SiteStructureTests.PagesNotYetMoved` (the seven names of spec step 7), the only fixed name.

- [ ] **Step 1:** Write the tests of spec step 7's table: root pages (an unknown root `.md` fails; a listed name with no file fails), folders (only `start`, `guides`, `reference`), limits (each page with a `## Limits` heading is linked from `site/guides/troubleshooting.md`; each `limit-` anchor on any page is named by one of its links), and one test that the reading finds pages, a `## Limits` page and two `limit-` anchors.  Each test's comment says what fails it.
- [ ] **Step 2:** Run them: pass.  Prove them: add `site/extra.md`, see the root-pages test fail, remove it.
- [ ] **Step 3:** Commit.

### Task 3: References from outside `site/`

**Files:** the table of spec step 6 (`docs/architecture/README.md`, `docs/architecture/generator.md`, `docs/deferred/D-0001-…`, `docs/tech-debt/TD-0004-…`, both example readmes and `Methods/OrderQueries.cs`, `src/SqlSource/Settings/AGENTS.md`, `src/SqlSource/Diagnostics/AGENTS.md`, `tests/README.md`).

- [ ] **Step 1:** Re-run `git grep -n -E "site/(sql-files|parameters-and-tokens|dialects|README|errors)\.md" -- . ":!site" ":!docs/superpowers"` and change each hit by the table (the four slugs of `generator.md` to explicit anchors).
- [ ] **Step 2:** `dotnet test tests/SqlSource.Repository.Tests`: all pass, `InboundSiteReferenceTests` among them.  Commit.

### Task 4: Rules and other documents

**Files:** root `AGENTS.md` (rule 4, two bullets), `site/AGENTS.md` (whole), `site/start/AGENTS.md`, `site/guides/AGENTS.md`, `site/reference/AGENTS.md`, `site/reference/databases/AGENTS.md`, `docs/deferred/AGENTS.md`, `docs/tech-debt/AGENTS.md`, `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`, `docs/publishing.md`, `CONTRIBUTING.md`, `tests/README.md`.

- [ ] **Step 1:** Write them by spec steps 8 and 9, with the differences listed under "Where the tree differs" above.
- [ ] **Step 2:** `wc -c` against the limits; `dotnet test tests/SqlSource.Repository.Tests --filter AgentsFileTests` passes.  Commit.

### Task 5: Ledger and worksheets

**Files:** `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-03.tsv`, `merges/MP-08.md`, `merges/MP-10.md`, `merges/MP-23.md`.

- [ ] **Step 1:** Write the ledger by spec "The ledger": `# base 819169d1…`, a row for each unit of the four pages, `@` rows for `site/errors.md` and phase 2's row and bullet, `position` notes for each mend, `new` rows.
- [ ] **Step 2:** Write the three worksheets in the phase 0 brief's form (section 7), with this phase's texts.
- [ ] **Step 3:** `check.sh coverage` passes; `check.sh move 03` exits 0 and leaves over only what the spec's ledger section lists.  Commit.

### Task 6: Done-when sweep

- [ ] Run every check of spec "Done when" (the `git ls-files site` list, the three greps, `wc -c`, the build, the checker) and fix what fails.  `./format.sh`, then `./pre-commit-validation.sh`.
