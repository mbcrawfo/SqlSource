# Docs overhaul phase 7, move-settings: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `site/settings.md` is gone.  What phase 6 left of it stands word for word on seven pages of `site/reference/` and on `guides/folders-and-files.md`; `reference/settings.md` has the table of the five places and the matrix of 22 settings, written new; C-01 and C-02 are made, and C-05 gets its last target; `SettingsReferenceTests` holds the matrix, the marker index, the generator parameters and the five MSBuild properties to the code.

**Architecture:** A verbatim move, done by a script in the scratchpad, adapted from phase 6's.  It cuts `site/settings.md` by `<a id>`, sets each heading to the level of spec step 2, and re-bases every relative link by resolving it to a repository path and writing it again from the new file, keyed on page and anchor together (two files are named `settings.md`).  The same resolver retargets every reference to `site/settings.md` and every `settings.html` URL.  The new text (h1s, the opening sentences, `places` with its sample and table, the matrix, `properties` with its table, the generator-parameter h3s, the limits and their troubleshooting entries), the corrections and the `position` mends are written by hand.

**Tech Stack:** Markdown, docfx (`tools/build-site.sh`), xUnit v3 (`tests/SqlSource.Repository.Tests`), the epic's checker (`check/check.sh`), Python 3 for the one-off scripts.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-07-move-settings-design.md`, under the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md` and the map `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md`.

## Global Constraints

- Moved text changes no word but C-01, C-02 and the `position` mends.  New text is plain language (ASD-STE100 as a guideline) and states facts: no status words, no `--prerelease`.
- A fenced block is byte for byte the same, with its info string; a block inside a bullet keeps the bullet's indent.
- `AGENTS.md`: at most 8,192 bytes and 80 words a rule (`AgentsFileTests`).
- Every commit passes `./pre-commit-validation.sh`; `./format.sh` first.

## Where the tree differs from the spec

- `site/settings.md` is 748 lines.  Phase 1 took out the heading "Settings that work today" and the Status column, and placed the SQLSRC232 pointer in `#setting-type-overrides`; phase 2 placed one sentence in `#converters` (it did).  Earlier phases retargeted links.  The C-01 bullet is at working line 151 and the C-01 sentence at 707.
- The guides group "Projects" holds `folders-and-files.md` alone until phase 8.  `site/guides/AGENTS.md` allows no group of one page; the owner allowed one interim exception in phase 6.  Asked as Q1; built with the exception widened to Projects.

## Review Focus

- A link `../settings.md#x` turned into `settings.md#x`: right for six anchors, dead for twelve.  `UserDocsLinksTests` and docfx fail on it.
- A reference outside `site/` to `site/settings.md` or `settings.html`: `InboundSiteReferenceTests`; the done-when greps.
- A limit with no troubleshooting entry: `SiteStructureTests`.
- The matrix against the code: `SettingsReferenceTests`, and the accuracy reviewer for the Default column.

---

### Task 1: The move

**Files:**
- Create: `site/reference/{settings,settings-file,generator-parameters,converters,library-types-and-json}.md`, `site/guides/folders-and-files.md`
- Modify: `site/reference/{type-mapping,attributes,msbuild}.md`, `site/guides/troubleshooting.md`, every page that links `settings.md`, both `toc.yml`, `README.md`, the files of spec step 7's last table, the `AGENTS.md` files of spec step 9, `tests/README.md`
- Delete: `site/settings.md`
- Test: `SiteStructureTests` (`settings.md` leaves `PagesNotYetMoved`)

- [ ] **Step 1:** Write the move script: page specs of spec step 2, the anchor map of the 18 anchors and the new ones, the `#known-issues` targets of spec step 7, and the resolver for Markdown links, bare `site/` paths and site URLs.  Run it.
- [ ] **Step 2:** By hand: the new text of spec steps 2, 3 and 4; the corrections of step 5; the six limits and entries of step 6; the `position` mends of step 7.
- [ ] **Step 3:** Sidebars, `AGENTS.md` files, `tests/README.md`, `SiteStructureTests`; `git rm site/settings.md`.  `dotnet test tests/SqlSource.Repository.Tests` passes; `tools/build-site.sh` has no warning; format; validate; commit.

### Task 2: `SettingsReferenceTests` and the shared list

**Files:**
- Create: `tests/SqlSource.Repository.Tests/Documentation/SettingsReferenceTests.cs`, the shared constant of the generator parameters
- Modify: `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs`

- [ ] **Step 1:** Write the six checks of spec step 8, each reading the code (schema, `SqlMarkerKind` with `SqlMarkerReader.WordOf`, `SqlMarkerScope`, `GeneratorParameterList.Definitions`) and failing with the name that has no row.
- [ ] **Step 2:** Run the five trials of "Done when", each undone; validate; commit.

### Task 3: Ledger and worksheets

**Files:**
- Create: `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-07.tsv`
- Modify: `merges/MP-06.md`, `MP-07.md`, `MP-10.md`

- [ ] **Step 1:** Write the ledger of spec "The ledger", base = the commit the branch started from.
- [ ] **Step 2:** `check.sh move 07` leaves over only the `move-split`, `corrected` and `position` rows; `check.sh coverage` passes.
- [ ] **Step 3:** Write the worksheets; validate; commit.

### Task 4: Done when

- [ ] Run every grep and count of the spec's "Done when"; fix what differs; `./pre-commit-validation.sh`.
