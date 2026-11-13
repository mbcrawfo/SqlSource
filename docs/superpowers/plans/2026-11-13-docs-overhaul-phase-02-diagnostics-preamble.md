# Docs overhaul phase 02: diagnostics-preamble Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `site/diagnostics.md` becomes its title, one paragraph and the 161 sections; each fact of the preamble that nothing else states moves first to the new page `site/errors.md` or into its section, and every section gains a "Reported by" line (and the 18 describing sections a "Database" line).

**Architecture:** Documentation only, plus `DiagnosticsDocumentTests`.  New lines and paragraphs are added under each section's bold title without changing a byte of today's lines, so the epic's checker (`check.sh move 02`) proves the sections are intact.  The preamble is removed last, after its facts have homes.

**Tech Stack:** Markdown (docfx site), xUnit + Shouldly (`tests/SqlSource.Repository.Tests`), the epic's checker (`docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.sh`).

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-02-diagnostics-preamble-design.md`; outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`; map section 4, "Map: the preamble of `site/diagnostics.md`", in `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md`.

## Where the merged code differs from the spec

- Phase 1 took `--prerelease` out of three commands of `site/diagnostics.md` (baseline lines 1703, 1735, 2022).  Those three units are no longer the baseline's words; they are phase 1's, and the ledger of phase 2 names the range `347-3707` as `move` (the checker reads "old" at the phase's base, so they are not left over).
- `site/AGENTS.md` is 6,753 bytes after phase 1, not 6,870.
- No `--prerelease` and no sentence about previews anywhere (owner, p01 Q1/Q4).

## Global Constraints

- The three "Reported by" lines, word for word: `Reported by: the build.` (80), `Reported by: the tool.` (53), `Reported by: the build and the tool.` (28: `SQLSRC015` to `019`, `101` to `120`, `126`, `209`, `210`).
- The "Database" lines: `Database: PostgreSQL.` (`227`, `229`, `230`, `232`, `233`, `234`, `236`), `Database: SQL Server.` (`262` to `266`), `Database: PostgreSQL and SQL Server.` (`224`, `225`, `226`, `228`, `231`, `235`).  Confirm each against `PostgresErrors.cs`, `SqlServerErrors.cs`, `DescribeFailures.cs`.
- Each new line and each new paragraph is a paragraph of its own: a blank line before it.  Order under `**Title**`: "Reported by", "Database", the facts paragraph, then today's text.  No byte of a line of today changes.
- `site/errors.md`: about 2,000 words, plain language (outline "The language"), six h2s each with `<a id>` above it: `severity`, `where-an-error-is-reported`, `what-an-error-costs`, `who-reports`, `the-tools-errors`, `errors-by-area`.  Section links are `diagnostics.md#sqlsrcNNN`.  No internals: no descriptor, class, "generator's ids", describer.  Say "the build" and "the tool".
- The table of `#errors-by-area` links each of the 161 ids, each from exactly one row, in id order, 29 rows named by the 29 bold names.
- `site/AGENTS.md` stays under 8,192 bytes; bullets at most 80 words.
- Every commit passes `./pre-commit-validation.sh`; `./format.sh` first.

## Review Focus

- A section whose new "Reported by" fact repeats a sentence the section already has (spec trap "A fact stated twice").
- A link moved off the page that still reads `(#sqlsrcNNN)` (docfx and `UserDocsLinksTests` catch it).
- `SQLSRC019` and `database`: the tool reports it for `dialect`, `output`, `database`; the build for every key but `database`.  Never "only the tool reads".
- `SQLSRC121` and `SQLSRC020`: build alone.  The tool's parser path and `SettingsFileSides.Reports` decide; the accuracy reviewer confirms.
- A new id added on `main` while this branch is open: it needs its two lines and its row in `#errors-by-area` (the tests fail until then).

---

### Task 1: The two lines in every section, and their tests

**Files:**
- Modify: `site/diagnostics.md` (161 sections)
- Modify: `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs`

- [ ] **Step 1: Write the failing tests** `Document_EachSection_SaysWhoReportsIt` and `Document_DescribingSections_NameTheirDatabase`.
  - Reported by: for each descriptor, the first non-blank line after the title line is one of the three lines; a descriptor of `ToolDiagnostics.All` has a line naming the tool, one of `SqlDiagnostics.All` a line naming the build.
  - Database: the sections `SQLSRC224`-`236` and `262`-`266` have, as the first non-blank line after "Reported by", one of the three Database lines; no other section has a line starting `Database:`.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Repository.Tests --filter "FullyQualifiedName~DiagnosticsDocumentTests"`; both fail.
- [ ] **Step 3: Add the lines with a script** that inserts `""` + line after each `**Title**` line of a section and changes no other byte.  `git diff --numstat site/diagnostics.md` shows 358 added, 0 removed.
- [ ] **Step 4: Run the tests**; they pass.
- [ ] **Step 5: Format, validate, commit.**

### Task 2: Read each section against its range paragraph, and add the missing facts

**Files:**
- Modify: `site/diagnostics.md` (about 55 sections)
- Create (scratch, for the PR description): the reading table

- [ ] **Step 1: The reading table.**  For each row of the spec's ledger table that names sections (`020`, `021`, `024`-`037`, `127`, `241`, `246`-`261`, `267`-`270`, `272`-`284`, `297`-`299`), note which of where / how often / what it costs the section states already.  Start with the spec's certain gaps: `020`, `026`, `241`, `246`, `249`, `257`, `274`, `283`, `284`.
- [ ] **Step 2: Add one paragraph** under the two lines of each section that lacks a fact, in plain language, holding only what the section lacks.  Links on the page stay `#sqlsrcNNN`.
- [ ] **Step 3: Run** `check.sh origin site/diagnostics.md` and confirm no unit of today changed; run the tests; validate; commit.

### Task 3: `site/errors.md`, the sidebar, the entry page and the table test

**Files:**
- Create: `site/errors.md`
- Modify: `site/toc.yml` (entry before Diagnostics), `site/README.md` (a bullet under "The pages" before Diagnostics; a row under "Find what you need" after "Look up a build error or a tool error")
- Modify: `DiagnosticsDocumentTests.cs`

- [ ] **Step 1: Write the failing test** `ErrorsPage_AreaTable_LinksEveryDiagnosticOnceInIdOrder`: the links `diagnostics.md#sqlsrcNNN` of the table rows under `<a id="errors-by-area">` in `site/errors.md` are exactly the descriptors' ids, each once, in id order.
- [ ] **Step 2: Write the page** to the spec's table of headings (spec step 2), including the row of 311/11-12 in `#who-reports`, and the 29 rows of `#errors-by-area`.
- [ ] **Step 3: Run the test, `tools/build-site.sh`, and the internals grep of `site/AGENTS.md` on `site/errors.md`**; validate; commit.

### Task 4: Three sentences on other pages

**Files:**
- Modify: `site/tool.md` (section `sidecar-files`), `site/settings.md` (section `converters`)

- [ ] **Step 1:** Place 315 sentences 5 and 6 word for word as one paragraph in `sidecar-files`, links `diagnostics.md#sqlsrc126`, `#sqlsrc237`, `#sqlsrc240`.
- [ ] **Step 2:** Place 333's last sentence in `converters`, "them" made "`SQLSRC277` to `SQLSRC280`", link `diagnostics.md#sqlsrc273`.
- [ ] **Step 3:** Build the site; validate; commit.

### Task 5: The paragraph, the removal, the head and help-link tests, and the rules

**Files:**
- Modify: `site/diagnostics.md` (lines 3-345 go; one paragraph)
- Modify: `DiagnosticsDocumentTests.cs` (index test and its comment go; class comment)
- Modify: `site/AGENTS.md`, `src/SqlSource/Diagnostics/AGENTS.md` line 6, `tests/README.md` line 13, the outline's "Tests" row of `DiagnosticsDocumentTests`

- [ ] **Step 1: Write the failing tests** `Document_Head_IsTheTitleAndOneParagraph` (the file is `site/diagnostics.md`; the non-blank lines before the first `## ` are two: `# Diagnostics` and a line that is no heading, table row or list item) and `HelpLink_IsTheSitesDiagnosticsPage` (`SqlDiagnostics.HelpLinkBase == UserDocs.SiteUrl + "diagnostics.html#"`; see it fail by changing the expected string once).
- [ ] **Step 2:** Write the paragraph of four statements (spec step 5); remove baseline lines 3-345; remove `Document_Table_ListsEveryDescriptorWithItsTitleAndAnchor` and its comment.
- [ ] **Step 3:** The rules (spec step 8); measure `wc -c site/AGENTS.md`.
- [ ] **Step 4:** Run the Done-when greps of the spec; tests; build the site; validate; commit.

### Task 6: The ledger and the checker

**Files:**
- Create: `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-02.tsv`

- [ ] **Step 1:** Write the ledger from the spec's table "The ledger": `# base <merge-base with origin/main>`, header, rows.  Lines 3 and 311-345 are `move-split` with a part for each sentence, `words` the baseline's normalised words; 5-309 one `move-split` with two `*` parts; 347-3707 one `move` row; the `new` rows of the spec.
- [ ] **Step 2:** `check.sh coverage` passes; `check.sh move 02`: list 1 empty, list 2 = 238 units each with its row, unheld words of 3 and 311-345 only punctuation and joining words, list 3 as the spec says, list 4 empty.
- [ ] **Step 3:** Word counts of `site/errors.md` and of `site/diagnostics.md` before and after, for the PR description.  Validate; commit.
