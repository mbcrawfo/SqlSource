# Docs overhaul, phase 03: skeleton

The outline is [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md), and the content map is [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).  Both win over this brief, but for the points under [Calls made in this brief](#calls-made-in-this-brief).
Done by: an Opus agent.  Needs: phases 0, 1 and 2.  Runs alone.

## Goal

Give `site/` its final shape: a landing page, three folders with a sidebar each, and the rules and the test that keep the shape.  Move the first four pages into `reference/`, so that every later move has a place to land and an example to follow.  From this phase on, feature work on other branches follows the new structure.

## Where things stand

This is what phases 0 to 2 leave.  Line numbers in this brief are those of the baseline, `28e20e3c`, as in the map; phases 1 and 2 have moved some lines since.  Check each point before you start.  Where the tree differs, follow the tree and say so in the pull request.

- **Pages.**  `site/` is flat: `README.md` (the entry page, without "What works today"), `concepts.md`, `diagnostics.md` (a title, one paragraph that links `errors.md`, then `## SQLSRC001`), `dialects.md`, `errors.md` (written by phase 2), `generator.md`, `parameters-and-tokens.md`, `postgresql.md`, `recipes.md`, `settings.md`, `sql-files.md`, `sqlserver.md`, `tool.md`.  `site/index.md` is three lines of front matter that redirect the root to `README.html`.  The sidebar is one flat `site/toc.yml`, and `site/docfx.json` takes `**/*.md` and `**/toc.yml` since phase 0.
- **Tests**, in `tests/SqlSource.Repository.Tests/Documentation/`.  Since phase 0, `UserDocs.SitePages` reads `site/` in folders, a site URL may be `folder/page.html`, `SiteTocTests` reads every `toc.yml`, `InboundSiteReferenceTests` follows every reference to a page of `site/` from outside it, and `Manual_EntryPage_ListsEveryPage` is gone.  `StatusLanguageTests` came with phase 1.  The root of the site still resolves to `site/README.md`, and `index.md` is still left out of `SitePages`: this phase changes both.
- **Rules.**  `site/AGENTS.md` is one file of about 7.8 KB, less the status rules (phase 1) and the rules on the index of `diagnostics.md` (phase 2).  It still has "When a phase lands".  Root `AGENTS.md` rule 4 has lost its callout bullets.
- **The epic's files.**  `docs/superpowers/specs/2026-11-12-docs-overhaul/` holds `content-map.md`, `inventory.tsv`, `ledger/phase-00.tsv` to `phase-02.tsv`, and `check/`.  The phase 0 brief says how the checker is run.

## In scope / Not in scope

In scope:

- The top bar and the three sidebars; `site/index.md` as a first landing page, from `site/README.md`, which goes; the moves of `sql-files.md`, `parameters-and-tokens.md`, `dialects.md` and `errors.md`, with the nine pages they create, and every link to them: in `site/`, in `README.md`, and in every other tracked file outside `docs/superpowers`.
- Root `AGENTS.md` rule 4 (two bullets), `site/AGENTS.md` whole, four new `AGENTS.md` files, and lines of five others.
- `SiteStructureTests`, the root page in `UserDocs`, `SiteTocTests` and `UserDocsReadingTests`, the path of one test of `DiagnosticsDocumentTests`, and `docs/publishing.md`, `CONTRIBUTING.md`, `tests/README.md`.

Not in scope:

- Any word of moved text.  No heading is reworded, `repository` is not put first, "Choosing a dialect" keeps its name: phase 13 rewrites these pages.
- The seven pages that wait (`concepts.md`, `settings.md`, `generator.md`, `tool.md`, `postgresql.md`, `sqlserver.md`, `recipes.md`), but for their links to the moved pages.
- The finished landing page, the rows that leave its table, and correction C-06: phase 10.  The README's words: phases 8 and 11.  The table of supported versions and the database folders: phase 4.  Running `docs.yml`: the owner starts it after the merge, and the pull request says that the run is due.

## The work

### 1. Check the start, and inventory what is new

1. Run the checks of [Where things stand](#where-things-stand), and "check coverage", which must pass for phases 0 to 2.
2. Compare each page of this phase with its baseline: `git diff 28e20e3c -- site/README.md site/sql-files.md site/parameters-and-tokens.md site/dialects.md`.  What phases 1 and 2 changed is in their ledgers.  Anything else is text that feature work added: inventory it, as the outline's "The ledger" says, and place it with the section it stands in.
3. Read `site/errors.md` and note its anchors and links.  The map expects `severity`, `where-an-error-is-reported`, `what-an-error-costs`, `who-reports`, `the-tools-errors` and `errors-by-area`.

### 2. The folders and the sidebars

`site/toc.yml` becomes the top bar, three entries in the block style of today's file: `Get started` with the href `start/`, `Guides` with `guides/`, and `Reference` with `reference/`.  The three sidebars follow.  A `# site/...` line names the file and is not part of it.

```yaml
# site/start/toc.yml, which lists no page of its own folder until phase 5
- name: Concepts
  href: ../concepts.md
# site/guides/toc.yml
- name: Recipes
  href: ../recipes.md
- name: Workflow
  items:
    - name: "Troubleshooting: it built, but something is wrong"
      href: troubleshooting.md
# site/reference/toc.yml
- name: .sql files
  items:
    - name: .sql files and markers
      href: sql-files.md
    - name: "Query parameters (@name, -- param:, IN lists)"
      href: parameters.md
    - name: "Tokens ({{name}})"
      href: tokens.md
    - name: Dialects
      href: dialects.md
- name: Settings
  items:
    - name: Settings
      href: ../settings.md
- name: Generated code
  items:
    - name: The generator
      href: ../generator.md
- name: The sqlsource tool
  items:
    - name: The tool
      href: ../tool.md
    - name: Sidecar files
      href: sidecars.md
- name: Supported databases
  items:
    - name: Supported databases and versions
      href: databases/index.md
    - name: PostgreSQL
      href: ../postgresql.md
    - name: SQL Server
      href: ../sqlserver.md
    - name: Other databases
      href: databases/other.md
- name: Errors
  items:
    - name: How errors are reported
      href: errors.md
    - name: Errors (SQLSRC)
      href: ../diagnostics.md
```

Each of the seven waiting pages stands in the sidebar it will join, under its name of today: `concepts.md` in Get started until phase 8; `recipes.md` in Guides, above the groups; `settings.md`, `generator.md`, `tool.md`, `postgresql.md` and `sqlserver.md` in Reference, each in its group.  [Step 10](#10-what-each-later-phase-changes-in-these-files) says which phase replaces each entry.

What docfx 2.81 does with this, built in a throwaway copy of the baseline site with the moves of this phase and its links retargeted by the rules of step 4 (`dotnet docfx <copy>/docfx.json --warningsAsErrors`: 0 warnings):

- The three entries of the top bar go to the first page of each sidebar: `concepts.html`, `recipes.html`, `reference/sql-files.html`.
- A root page that a folder's `toc.yml` lists as `../page.md` is built at the root and shows that folder's sidebar (`docfx:tocrel` of `concepts.html` is `start/toc.html`).  The planning found in a browser that it has no top-bar highlight and no breadcrumb.  That is accepted until the page moves, and for `diagnostics.md` for good.
- `index.md`, in no `toc.yml`, shows no sidebar and needs no front matter.  An `AGENTS.md` at any depth is left out.  A link to a missing anchor in another folder is `warning InvalidBookmark`, so `tools/build-site.sh` fails on it.

### 3. The landing page

`site/index.md` loses its front matter and takes the text of `site/README.md`, word for word, and `site/README.md` goes (`git mv -f site/README.md site/index.md` keeps the history).  The page starts with its h1, `# SqlSource manual`.

- **Kept until phase 10:** line 3 whole, with the sentence of C-06 and the link to `concepts.md#glossary`; all 49 rows of "Find what you need" (lines 9 to 57); "The pages" (lines 107 to 119), which is not extended for the new pages, since the sidebars list them.  Phase 10 writes the page of the map's section 3 in their place, with its sample pair: this first landing page has none.  The row and the bullet that phase 2 added for `errors.md` move with the page, so the table has 50 rows, and both now name `reference/errors.md`.
- **New:** `<a id="find-what-you-need"></a>` above `## Find what you need`, and `<a id="the-three-parts"></a>` above `## The pages`, which keeps its words.
- **Links.**  A link to a page that waits at the root does not change.  A link to a moved page takes its new path by the table of anchors in step 4 (lines 10, 15 to 19, 53 and 110).  Three lines need more.
- **Line 57**, eight links to `#known-issues`: the cell becomes one link, `[Troubleshooting](guides/troubleshooting.md)`.  The five anchors that live on are linked from that page: step 5.
- **Line 112**, `parameters-and-tokens.md`, becomes `reference/parameters.md`, and the bullet's last word, "tokens", becomes a link to `reference/tokens.md`.
- **Line 113**, `dialects.md`, becomes `reference/dialects.md`, and the words "which databases can be described" become a link to `reference/databases/index.md#database-support`.

### 4. The first moves

"New" is text of this phase, in plain language.  An `<a id>` line moves with its heading.

| New page | Holds, in this order |
|----|----|
| `reference/sql-files.md` | `sql-files.md` 1-14.  New: `<a id="marker"></a>` and `## What a marker is`.  Line 16, then lines 18-194.  The h1 stays `` # `.sql` files `` |
| `reference/parameters.md` | New: the h1, and a first paragraph of one or two sentences that links `tokens.md#token-or-parameter`.  `parameters-and-tokens.md` 20-156.  New: `<a id="limits"></a>`, `## Limits`, and the limit of step 5 |
| `reference/tokens.md` | New: the h1, `<a id="token-or-parameter"></a>` and `## Token or parameter`.  `parameters-and-tokens.md` 3-18, then 158-254.  "Defaults", "Text that looks like a token" and "Validation" go from h3 to h2, and `` `-- token:` `` from h4 to h3, as the map's section 3 lists them |
| `reference/dialects.md` | The h1.  New: the first paragraph, in the words of the map's section 3 ("If your database is PostgreSQL or SQL Server, set the dialect to `postgres` or `mssql` and stop reading.  ...").  `dialects.md` 3-128 |
| `reference/databases/index.md` | New: the h1.  `dialects.md` 130-143 (the anchor `database-support`, the heading, the table), then sentences 1 to 3 of line 145 |
| `reference/databases/other.md` | New: the h1; one opening sentence that links `index.md#database-support`; `<a id="not-described"></a>` and `## Databases that are not described`.  `dialects.md` 147.  New: `<a id="which-dialect"></a>`, `## Which dialect to set` and one sentence that points to `../dialects.md#choosing-a-dialect`; `<a id="duckdb"></a>` and `## DuckDB`.  Sentences 4 and 5 of line 145 |
| `reference/sidecars.md` | New: the h1; one sentence that points to `../tool.md#sidecar-files` for what a sidecar is; `<a id="limits"></a>`, `## Limits`, and the limit of step 5 |
| `reference/errors.md` | `site/errors.md`, by `git mv`.  Only its links change |
| `guides/troubleshooting.md` | New, whole: step 5 |

The h1 of a new page is its sidebar title of the map's section 1, with code font where the map has it: `` # Query parameters (`@name`, `-- param:`, IN lists) ``, `` # Tokens (`{{name}}`) ``, `# Supported databases and versions`, `# Other databases`, `# Sidecar files`, `# Troubleshooting: it built, but something is wrong`.  A `toc.yml` name has no backticks, since a sidebar shows them as they are.

**Words that the move makes wrong.**  A page that splits can leave "this page", "above" or "below" pointing at text that is now on another page, and a link's words can name a page that has moved: read the moved text of `reference/parameters.md`, `reference/tokens.md` and `reference/databases/` for each.  Change the fewest words that make the phrase true, and give the unit's `move` row the note `position`, as the outline's "How a phase is done" and the phase 0 brief say.  "The settings page", "the generator page" and "the dialects page" stay true in this phase.

**The worksheets.**  The phase that places the first text of a pair starts its worksheet, in the form of the phase 0 brief, section 7.  This phase starts three.  `merges/MP-08.md` holds `sql-files.md` 200, and phase 5 adds `tool.md` 634.  `merges/MP-23.md` holds the table of `dialects.md` 134-143, and phase 4 adds its bullets.  `merges/MP-10.md` holds the opening of each section of this phase that a term joins in phase 8: `sql-files.md` 16 under "Marker", the openings of `queries-and-names`, `the-file-header`, `query-parameters` and `tokens` under their terms, and `dialects.md` 15 under "Dialect".  Phases 5 to 8 add to it.

These go, with no new location: the heading `# Parameters and tokens` (line 1); the three headings "Known issues" with their anchors (`sql-files.md` 196-198, `parameters-and-tokens.md` 256-258, `dialects.md` 149-151); the pointer bullets `parameters-and-tokens.md` 261 and 262 and `dialects.md` 153.

**The links.**  A link whose anchor keeps its name changes only its path, by where the anchor now is:

| Anchors | New page |
|----|----|
| `marker-index`, `queries-and-names`, `result-shapes`, `summaries`, `the-file-header`, `a-file-with-no-name`, `generated-sql`, `markers-in-the-wrong-place` | `reference/sql-files.md` |
| `query-parameters`, `param-marker`, `list-parameters`, `list-padding`, `list-limit`, `declared-variables`, `names-read-as-parameters` | `reference/parameters.md` |
| `tokens`, `token-defaults`, `token-marker`, `token-ignore`, `token-validation` | `reference/tokens.md` |
| `choosing-a-dialect`, `dialect-options`, `what-a-dialect-changes`, `markers-under-mysql`, `still-read-differently` | `reference/dialects.md` |
| `database-support` | `reference/databases/index.md` |
| Every anchor of `errors.md` | `reference/errors.md` |

- From a root page, the path is `reference/<page>.md#<anchor>`.  At the baseline the root pages hold 115 links to the three pages: `diagnostics.md` 31, `generator.md` 28, `settings.md` 16, `postgresql.md` 13, `sqlserver.md` 11, `concepts.md` 7, `recipes.md` 6, `tool.md` 3.
- From a page of `reference/`, a root page is `../<page>.md` and a page of the same folder is `<page>.md`.  From `reference/databases/`, they are `../../<page>.md` and `../<page>.md`.  From `guides/`, a reference page is `../reference/<page>.md`.  So a link from moved text to a page that waits changes only its path, also where the map gives it another target in a later phase.
- A same-page link, `#x`, of a page that splits (25 at the baseline) names the new page of `x` when that is not its own.
- The links that these rules do not settle.  "Later" is the phase that changes the link again, to the target of the map's section 6:

| Link at | Today | In this phase | Later |
|----|----|----|----|
| `sql-files.md` 28 | `dialects.md` | `dialects.md#choosing-a-dialect` | |
| `sql-files.md` 47 | `settings.md` | `../settings.md#all-settings` | 7 |
| `sql-files.md` 134 | `dialects.md` | `../settings.md#setting-dialect` | 7 |
| `sql-files.md` 158 | `dialects.md` | `dialects.md#what-a-dialect-changes` | |
| `sql-files.md` 160 | `parameters-and-tokens.md` | `parameters.md#list-parameters` | |
| `sql-files.md` 194 | `diagnostics.md` | `../diagnostics.md`.  New in the same sentence, unless phase 2 added it: a link to `errors.md#what-an-error-costs`, the owner of the rule that the paragraph states | |
| `parameters-and-tokens.md` 91 | `#known-issues` | `../sqlserver.md#known-issues` | 4 |
| `parameters-and-tokens.md` 156 | `#known-issues` | `#limit-parameter-taken-for-a-variable` | |
| `parameters-and-tokens.md` 177 | `generator.md` | `../generator.md#token-methods` | 6 |
| `concepts.md` 91; 103 | `parameters-and-tokens.md` | `reference/parameters.md`; `reference/tokens.md` | Both go in 8 |
| `concepts.md` 111 | `dialects.md` | `reference/dialects.md` | |
| `diagnostics.md`, `See:` of SQLSRC243 | `dialects.md` | `reference/databases/index.md#database-support` | |
| `diagnostics.md`, SQLSRC245 | `sql-files.md#known-issues` | `reference/sidecars.md#limit-file-in-two-projects` | |
| `diagnostics.md`, the one paragraph | `errors.md` | `reference/errors.md` | |
| `generator.md` 77 | `sql-files.md` | `reference/errors.md#what-an-error-costs` | |
| `postgresql.md` 3 and `sqlserver.md` 3, two links each | `dialects.md` | The first, `settings.md#setting-dialect`; the second, `reference/dialects.md#what-a-dialect-changes` | 4, 7 |
| `settings.md` 183 | `dialects.md` | `reference/dialects.md` | |
| `tool.md` 634 | `sql-files.md#known-issues` | `reference/sidecars.md#limit-file-in-two-projects` | Goes in 5 |

**The README.**  Eleven site URLs change, and no word.  Lines 9, 183 and 298, `dialects.html#database-support`, become `reference/databases/index.html#database-support`.  Lines 58 and 297, `sql-files.html#marker-index`, and line 58, `sql-files.html#result-shapes`, take `reference/`.  Line 58, `parameters-and-tokens.html`, becomes `reference/parameters.html`.  Line 87 becomes `reference/dialects.html#choosing-a-dialect`, line 224 `reference/tokens.html#tokens`, and lines 228 and 288 `reference/parameters.html#list-parameters` and `#param-marker`.  Line 303 links the root, which is now the landing page, and does not change.  The tool's readme links no page of this phase.

### 5. Limits and `guides/troubleshooting.md`

Two limits.  Each is an h3 under `## Limits`, with its anchor on the line above and words of yours that say what the reader sees.  Under it stands the bullet, whole and still a bullet, so that the checker finds the unit.

- `reference/parameters.md#limit-parameter-taken-for-a-variable`, from `parameters-and-tokens.md` 260.  The reader sees: a parameter is missing from the input model after a `DECLARE` with no `;`.
- `reference/sidecars.md#limit-file-in-two-projects`, from `sql-files.md` 200 (MP-08; phase 5 places `tool.md` 634 beside it).  The reader sees: entries vanish from a sidecar, or SQLSRC245, when two projects include one `.sql` file.

`guides/troubleshooting.md` holds:

- The title, and one sentence that carries the search words of the map's section 1: an exception at run time, a wrong result, nothing happens.
- One sentence, for now, that links the known issues of the five pages that still have them: `../settings.md#known-issues`, `../generator.md#known-issues`, `../tool.md#known-issues`, `../postgresql.md#known-issues`, `../sqlserver.md#known-issues`.  These are the five links of line 57 of `site/README.md` that outlive this phase.
- The six h2 headings of the map, each under its anchor, in this order: An exception at run time (`at-run-time`); A model that looks wrong (`wrong-model`); A method returns something you did not expect (`wrong-result`); A query is not described, or a file is not found (`not-described`); The watch does nothing (`watch`); A secret in the output (`secrets`).
- Four entries, each one or two sentences under what the reader sees, with a link: the two limits above; a MySQL or MariaDB user variable, or a parameter of a procedure that the SQL creates, is listed as a parameter (`../reference/parameters.md#names-read-as-parameters`); SQL that is read differently from your database (`../reference/dialects.md#still-read-differently`).  A section with no entry stays empty, and the phase that moves its bullets fills it.

### 6. References from outside `site/`

`InboundSiteReferenceTests` fails on each of these until it is changed.  The list is from `git grep -n -E "site/(sql-files|parameters-and-tokens|dialects|README|errors)\.md" -- . ":!site" ":!docs/superpowers"` at the baseline; run it again.  No site URL outside the two readmes names a page of this phase.

| File | Lines | Becomes |
|----|----|----|
| `AGENTS.md` | 22 | Step 8 |
| `docs/architecture/README.md`, `examples/PostgresExamples/README.md`, `examples/SqlServerExamples/README.md` | 3 | `site/index.md`.  Line 71 of `docs/architecture/AGENTS.md` names no page since phase 1, which gave it its last words |
| `docs/architecture/generator.md` | 3, 490, 500, 512, 540, 552, 554 | `site/reference/sql-files.md` (3, 490, 512, 540), `site/reference/dialects.md` (500), `site/reference/parameters.md` (552, 554).  Four name a heading's slug: give each the explicit anchor, `#a-file-with-no-name`, `#what-a-dialect-changes`, `#generated-sql`, `#declared-variables` |
| `docs/deferred/D-0001-dialects-not-delivered.md` | 9, 17 | `site/reference/dialects.md`.  In line 17, "Database support" is now in `site/reference/databases/index.md` |
| `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md` | 18, 37 | `site/reference/dialects.md`.  In line 37, `site/parameters-and-tokens.md` becomes `site/reference/parameters.md`, and "in its known issues" becomes its section `#names-read-as-parameters` and its limits |
| `examples/PostgresExamples/README.md`; `examples/SqlServerExamples/README.md` | 13, 15; 12, 13, 14 | `#tokens` to `site/reference/tokens.md`; `#list-parameters` and `#param-marker` to `site/reference/parameters.md`; `#result-shapes` to `site/reference/sql-files.md` |
| `examples/PostgresExamples/Methods/OrderQueries.cs`, `examples/SqlServerExamples/Methods/OrderQueries.cs` | 35, a comment | `site/reference/parameters.md#list-parameters` |
| `src/SqlSource/Settings/AGENTS.md` | 16 | `site/reference/sql-files.md` |
| `src/SqlSource/Diagnostics/AGENTS.md`; `tests/README.md` | 6; 13: the words that phase 2 wrote | `site/errors.md` becomes `site/reference/errors.md` |

### 7. The tests

| Where (baseline lines) | Today | Becomes |
|----|----|----|
| `UserDocs.cs` 19-20, 34-35, 102-103, comments | The root redirects to `README.html` and drops an anchor; `index.md` only redirects | The root is `index.html`, the landing page, built from `site/index.md` |
| `UserDocs.SitePages`, 41 | Leaves out `AGENTS.md` and `index.md` | Leaves out `AGENTS.md` alone, so the landing page's links, anchors and samples are checked |
| `UserDocs.SiteLinkProblem`, 111-124 | The root with an anchor is a problem; two messages give the reason "drops the anchor" | The root is no problem, with an anchor or without.  A spelling that is not `SiteUrl` is still one, and its message gives no reason about an anchor |
| `UserDocs.Resolve`, 156 | The root is `site/README.md` | The root is `site/index.md`, with the anchor |
| `SiteTocTests` | Every page is in exactly one `toc.yml` | Every page but `site/index.md`.  Check that phase 0's test takes `start/` as a folder that holds a `toc.yml`, and reads `../concepts.md` from the folder of its `toc.yml`; if it does not, change it here |
| `UserDocsReadingTests.Resolve_LinkToThisRepository_GivesItsPathAndAnchor`, 134 | `../site/README.md#y` | `../site/index.md#y` |
| `UserDocsReadingTests.Resolve_SiteUrl_IsThePageOfSite`, 176-182 | The root and a `README.html` URL give `site/README.md` | The root gives `site/index.md`.  The `README.html` row becomes the root with an anchor, `https://mbcrawfo.github.io/SqlSource/#find-what-you-need`, which gives `site/index.md` and that anchor |
| `UserDocsReadingTests.SiteLinkProblem_NotAPageOfTheSite_IsReported`, 189-192 | The row of the root with an anchor is reported; the comment speaks of a redirect | That row moves to `SiteLinkProblem_APageOrAnotherSite_IsNone`, and the comment loses the redirect.  The row of line 196, the root without its slash, stays reported |

One test of phase 2 reads the page that moves.  The test of the table of areas in `DiagnosticsDocumentTests` reads `site/reference/errors.md` now, and a link of the table to a section is `../diagnostics.md#sqlsrcNNN`.  Change its path, the link form it expects and its comment.  No other method of that class changes.

`SiteStructureTests` is new, in `tests/SqlSource.Repository.Tests/Documentation/SiteStructureTests.cs`, over `UserDocs.SitePages`, `UserDocs.Anchors` and `UserDocs.Links`:

| It checks | What fails it |
|----|----|
| The pages at the root | A `.md` file directly in `site/`, other than `AGENTS.md`, that is not `index.md`, `diagnostics.md` or a name of the list `PagesNotYetMoved`.  A name of the list that has no file, so that a move cannot leave it there |
| The folders | A folder directly in `site/` other than `start`, `guides` and `reference` |
| Limits | A page with a `## Limits` heading that `site/guides/troubleshooting.md` does not link.  An anchor that starts with `limit-`, on any page, that no link of `site/guides/troubleshooting.md` names |

`PagesNotYetMoved` in this phase: `concepts.md`, `generator.md`, `postgresql.md`, `recipes.md`, `settings.md`, `sqlserver.md`, `tool.md`.  The field has this name in every brief.  Give the class a test that its reading finds something, as `Links_OfTheUserDocs_AreFound` does, and say in comments what fails each test.

### 8. The rules

Measured with `wc -c` on the words below, less the last sentence of the bullet on a new diagnostic, which adds 70 bytes: `site/AGENTS.md` 7,493 bytes, `site/start/AGENTS.md` 1,665, `site/guides/AGENTS.md` 1,789, `site/reference/AGENTS.md` 1,429 and `site/reference/databases/AGENTS.md` 1,154, each against 8,192.  The longest bullet is 63 words.  Root `AGENTS.md` grows by 75 bytes, from the baseline's 14,925 to 15,000, against 16,384.  Measure again after you write them: phases 1 and 2 changed two of the files.

**Root `AGENTS.md`, rule 4.**  Two bullets change: the link form of the packed readmes (line 17), and the bullet on `site/` (line 22).  The README bullet (line 12) and "or an FAQ answer" (line 11) stay until phase 11.

```markdown
     - They link the manual by its site, `https://mbcrawfo.github.io/SqlSource/<folder>/<page>.html#<anchor>`, which shows the release a reader installed, never by its pages on GitHub.  A page at the root of `site/` has no folder in its address, and the site's root is the landing page.

   - `site/` is the manual, published as the site `https://mbcrawfo.github.io/SqlSource/`.  Its root holds the landing page, `site/index.md`, which routes a reader by need, and `site/diagnostics.md`.  Every other page is in one of three folders: `site/start/`, the path of a newcomer; `site/guides/`, one task on each page; and `site/reference/`, look-up with one page for each thing.  A fact has one home, and other pages link to it.
```

**`site/AGENTS.md`**, whole: it replaces the file.  Three things come from the file as you find it.  The section "Write effects, never internals" stays as it is, with its five bullets and its grep block; it was measured in the baseline's words, less the sentence "The same holds for what is coming."  The bullets "State what SqlSource does" and "A new diagnostic" restate rules of phases 1 and 2: where the file words one of them differently, keep its words.  A rule that phase 1 or 2 added and that is not below stays, and you report it.

```markdown
# AGENTS.md - site

Rules for the user documentation, this folder and the two packed readmes, beside root rule 4.

## Where a new thing goes

| New thing | Goes in | What fails if it does not |
|----|----|----|
| Setting, as a settings-file key or a marker | A `setting-<name>` section of `settings.md`; a row under its `#all-settings` that names every place it can be written | A reviewer |
| Marker | A row in the marker index of `reference/sql-files.md` | A reviewer |
| Generator parameter | Each place that the comment on the list in `DiagnosticsDocumentTests` names | `DiagnosticsDocumentTests` |
| MSBuild property | Its section of `settings.md`; a row under `#all-settings` | A reviewer |
| Attribute, or a property of one | `generator.md#attribute` | A reviewer |
| Command, option, environment variable, exit code | The command reference of `tool.md` | A reviewer |
| Diagnostic | A `## SQLSRCnnn` section of `diagnostics.md`, in id order | `DiagnosticsDocumentTests` |
| Dialect | Its rows in `reference/dialects.md` and `reference/databases/index.md`; the section of `SQLSRC019` | A reviewer |
| Database that can be described | A page beside `postgresql.md`; a row in `reference/databases/index.md`; the README's list; an example project | A reviewer |
| Feature that meets the line for Get started | The getting-started steps of `README.md`; its rules in Reference | A reviewer |
| Any other feature | A reference section; a guide when it is a task; an example, as root rule 4 says | `SiteTocTests`, for a new page |
| Limit | `## Limits` of the page that owns it, with its own anchor; a row in `guides/troubleshooting.md` | `SiteStructureTests` |

## Structure

- **Three folders, one job each.**  `start/` is the path of a newcomer, read in order; `guides/` holds one task on each page; `reference/` is look-up, one page for each thing.  The root holds `index.md`, the landing page and the site's root, and `diagnostics.md`.
- **A fact has one home.**  A rule, a value, a default or a limit is stated in one reference section; every other page restates it in a sentence or two and links there.
- **Pages not yet moved.**  `concepts.md`, `settings.md`, `generator.md`, `tool.md`, `postgresql.md`, `sqlserver.md` and `recipes.md` wait at the root for their phase of `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`.  Change one in place, and add no page at the root.  A limit of one is a bullet of its "Known issues", and a recipe is a sample and a link to the rules.
- **A new page** gets an entry in the `toc.yml` of its folder, under its group.  Aim at 600 to 3,500 words for a page and at most nine entries for a group; a reviewer holds both.
- **`README.md`**, beyond root rule 4: the databases it works with, getting started in five steps, "Only want to generate models?", "Just want to keep your SQL out of code files?", a FAQ of five to ten items, "NodaTime and other libraries", "Custom type mapping" and "Where to next".  A FAQ answer is a few sentences that link to a recipe where one exists.
- **The site.**  docfx publishes every page here but each `AGENTS.md`.  `tools/build-site.sh` builds it and fails on any docfx warning.  The tests are in `tests/SqlSource.Repository.Tests/Documentation/` and `Diagnostics/`; none checks whether a page is true and free of internals.

## Write effects, never internals

(This section as you find it.)

## Writing

- **State what SqlSource does, as fact.**  No page says that a feature is coming, works "today" or is in development; `StatusLanguageTests` holds the phrases.  A sentence that says something is not tested, or was checked on one platform, is a fact about support and stays.
- **Plain language**, by ASD-STE100 as a guideline, for text you write or change; a reviewer holds it.  One instruction in a sentence.  At most 20 words in a step and 25 in a description.  A paragraph has one topic and at most six sentences.
- Active voice and present tense, not "will".  A condition comes first: "If the column can be null, ...".  "Must" for what is required and "can" for what is allowed.  A list for conditions and alternatives.
- **Terms.**  One term for one thing.  Use the terms of `concepts.md#glossary`, *inside a query*, and *folder*, never "directory" in prose.  Never say "parameter" or "database" alone where it is ambiguous: say *query parameter* (`@id`), *generator parameter* or *logical database*.
- **Tabs.**  A docfx tab holds a sample or a few sentences, never a heading or an `<a id>`: an anchor in a tab that is not selected is a dead link target.  A long difference between databases is two anchored sections.  A tab group that names one described database names them all.
- **Anchors.**  A heading of this folder linked from anywhere gets an `<a id>` anchor on the line above, kept when the heading is reworded.
- **Links.**  Pages of this folder link to each other relatively, and to any other file of the repository by `https://github.com/mbcrawfo/SqlSource/blob/main/<path>`: only this folder is published.
- **SQL samples** are marked by the info strings `UserDocsSamplesTests` reads; never weaken the test.  Put no fence inside a blockquote.
- **`--prerelease`** goes on every page's install and update commands while only previews are published, as root rule 4 says.

## Limits and diagnostics

- **Limits.**  A page that owns behaviour ends with `## Limits`, under `<a id="limits"></a>`: what a user can plausibly hit from the open items of `docs/tech-debt` and `docs/deferred`.  Each is an h3 under its own anchor, `limit-<name>`: what the user sees, why, and what to do.  Leave out what a user cannot see, such as a test gap or duplicated code.
- Add a limit in the PR that opens its item, with its row in `guides/troubleshooting.md`.  Remove both in the PR that closes it, found by its effect, as a limit names no id.
- **`diagnostics.md` stays at the root, and a `## SQLSRCnnn` heading never gets another anchor**: every error's help link is `diagnostics.html#sqlsrcnnn`, compiled into the packages.  Nothing but the title and one paragraph stands before `## SQLSRC001`.
- A new diagnostic gets a section whose first bold line is its descriptor's title, then its "Reported by" line, a "Database" line where one database gives the error, and, where a manual section documents the rule, a closing `See: [section](page.md#anchor)` line.  Its id joins its area's row in `reference/errors.md#errors-by-area`.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
```

**The four new files**, in one block.  Each starts at its title and ends with a blank line and the maintenance footer, the last line of `site/AGENTS.md` above, which the block leaves out.  `site/reference/databases/AGENTS.md` lands here and not with phase 4: see the calls.

```markdown
# AGENTS.md - site/start

Get started: the path of a newcomer, read in order.

- **The line for Get started.**  A topic is here only when an application with one PostgreSQL or SQL Server database and the default `repository` output needs it before its first release.  Transactions are here, and are where the `methods` output is introduced, by the need for it.  Anything else is a guide.
- **The pages are written by the `get-started` phase of `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`.**  Until then `toc.yml` lists `../concepts.md`, a page here holds only what a move placed on it, and a change to getting started goes in `README.md`.
- **The pages are read in the order of `toc.yml`**, and each ends with "Next:" and a link to the next page.
- **One running example and the `repository` output.**  The samples are the files of `examples/PostgresExamples/Repository` and `examples/SqlServerExamples/Repository`, which compile.
- **Give each step whole.**  Never send the reader to Reference in the middle of a step.  State a rule in its common form, and link its owner under "Rules:" near the end of the page.
- **A page here owns no rule**: no `## Limits`, no `setting-<name>` anchor, no table of values or defaults.
- **Put a trap of the first hour where it bites**, stated in full with its error id and the fix, such as a `.env` file at the repository's root that is not read.
- **A difference between databases is a tab** of a sample or at most three sentences; a longer one is two anchored sections.

# AGENTS.md - site/guides

Guides: one task on each page, for a reader who knows what they want to do.

- **A guide is one task, in one shape**: "Use this when", in two sentences; the whole sample, every file complete enough to copy; "What you get"; "The rules you need"; a section for each variation; and last "If it does not work", what the reader sees with a link to the limit or the error.
- **Restate each rule the task needs in a sentence or two, and link the reference section that owns it.**  The page is complete for its task without the click.
- **Show a setting in use and link it.**  Never list its values, its default or its places: a guide owns a sample and a procedure, and no value, default or precedence.
- **A guide has a `## Limits` heading only when it owns a limit**, one that no reference page can own.  Otherwise it links the limit's anchor on the page that owns it.
- **The title is in the reader's words** and carries the word a junior types in a search box, such as "bulk", "mock", "nullable", "timeout", "IN" or "enum".  The first sentence carries the other search words.
- **A long difference between databases is two anchored sections**, never tabs.
- **A new guide goes under a group of `toc.yml`**: Queries, Running queries, Types, Projects, Workflow, or Other ways to use your SQL, in that order.  Add no group for one page, and let none pass nine entries.
- **`troubleshooting.md` is for a reader with a symptom and no `SQLSRC` id.**  It has an entry for each limit of the site, under what the reader sees: one or two sentences and a link to the limit's own anchor.  `SiteStructureTests` fails for a limit that has none.

# AGENTS.md - site/reference

Reference: look-up, one page for each thing.  A reader arrives by search or by a link and does not read from the top.

- **One section defines each written name**: a settings-file key, a marker, a generator parameter, an attribute property, an MSBuild property, a command, an option, an environment variable.  The section is on the page of the thing the name changes, and every other page links it.
- **A setting's section is anchored `setting-<name>`** and holds, in this order: an example, the values, the default, the places it can be written, what is special about its precedence, and its errors.
- **A section opens with an example**, then the rules.
- **Reference states rules, not how-to.**  A procedure is a guide; link it.
- **A section stands alone.**  It names its subject, and links what it relies on, since the reader has not read the sections above it.
- **A new page gets an entry in `toc.yml`, under its group.**  A page not yet moved is listed there as `../<page>.md`, in the group it will join, until its phase of the documentation overhaul moves it.
- **`../diagnostics.md` is listed last, under Errors**, though it is at the root of `site/`.  `errors.md` holds how errors are reported and the areas with their ids, and no section of one error.

# AGENTS.md - site/reference/databases

The databases that SqlSource can describe, one folder for each, and the pages for every other database.

- **Every database folder has the same five file names**: `index.md`, `nullability.md`, `types.md`, `libraries.md` and `query-methods.md`.  The headings and anchors inside may differ, since two databases need not have the same features.  A database with no library support has a short `libraries.md` that says so.
- **A page of a database's folder states only what is true of that database.**  What is true of every database is on a page outside the folders, and the database's page links it.
- **A database that can be described gets a folder, and its row of `index.md#database-support` says Yes.**  A database that cannot is a row of that table, is covered by `other.md`, and gets no folder.
- **Until their phase of the documentation overhaul moves them here, PostgreSQL and SQL Server are `../../postgresql.md` and `../../sqlserver.md`**, and no database folder exists.
```

**Other `AGENTS.md` files.**  `src/SqlSource/Settings/AGENTS.md` is in step 6.

- `docs/deferred/AGENTS.md` and `docs/tech-debt/AGENTS.md`, lines 22, 31 and 40.  In line 31, "its known issue in `site/`" becomes "its limit in `site/`".  Lines 22 and 40 become the two lines below; in `docs/tech-debt/AGENTS.md` line 22 opens "If a user can see its effect".
- `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`.  In the first bullet, the clause of `SiteTocTests` reads "that every page of `site/` but `index.md` is in exactly one `toc.yml`", and a clause is added: "`SiteStructureTests` the folders and root pages of `site/` and that each limit has its row in `site/guides/troubleshooting.md`".  In the last bullet the form is `https://mbcrawfo.github.io/SqlSource/<folder>/<page>.html`, if phase 0 did not write it so.  The third line below is a new bullet.

```markdown
4. If a user can see what is missing, add a limit to the `site/` page that owns the behaviour, and its row to `site/guides/troubleshooting.md`, as `site/AGENTS.md` says: the effect in the user's words and what to do about it, with no id and no link to the item.
4. Remove its limit from `site/`, if it has one, with its row of `site/guides/troubleshooting.md`.  A limit names no id, so search the `## Limits` sections, and the "Known issues" of a page not yet moved, for the effect.
- **`SiteStructureTests.PagesNotYetMoved` only shrinks.**  It lists the pages that wait at the root of `site/` for their phase of the documentation overhaul.  The phase that moves one removes its name.
```

### 9. Other documents

- **`docs/publishing.md`**, "Site".  After the paragraph that ends "It is not the way to keep the site current between releases.", add this paragraph: "One exception holds until the documentation overhaul closes (`docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`).  Each of its phases 3 to 11 moves pages of the site and, in the same pull request, points the links of `README.md` and `src/SqlSource.Tool/README.md` at the new addresses.  The readmes on `main` then link to pages that the published site does not have.  So after each of those pull requests merges, and once more when the overhaul closes, run `docs.yml` on `main` by hand.  The pull request that closes the overhaul takes this paragraph out."
- **`CONTRIBUTING.md`**, line 42.  The first sentence becomes: "`site/` is the user manual, which docfx builds into the site published at `https://mbcrawfo.github.io/SqlSource/`: the landing page, `site/index.md`, the errors, `site/diagnostics.md`, and three folders, `site/start/`, `site/guides/` and `site/reference/`, each with the `toc.yml` that is its sidebar."  The second sentence stays.  **`tests/README.md`**, line 13: after "the sections of `site/diagnostics.md`," add "the folders and root pages of `site/`,".
- **`.github/workflows/README.md`** and **`tools/README.md`**: no change.  Lines 10, 12 and 30 of the first and line 18 of the second say only that `docs.yml` and `tools/build-site.sh` build `site/`, which stays true.  `pre-commit-validation.sh` and `tools/test-scopes.txt` need none either: their `site/*` patterns cross folders.
- **The map** already holds the calls below: the sentence of `D/other.md` that links `G/other-databases.md` is phase 8's, and "Find what you need" has 49 rows at the baseline.  If a call of yours changes the plan, bring the map into line in this pull request, as the outline asks.

### 10. What each later phase changes in these files

| Phase | `site/AGENTS.md` | Other rules | The sidebars | `PagesNotYetMoved` | Elsewhere |
|----|----|----|----|----|----|
| 4 | The row of a database: a folder of the five pages under `reference/databases/`; a row in `databases/index.md`; a tab in every tab group that names a database; `SiteStructureTests`.  "Pages not yet moved" loses two names | `reference/databases/AGENTS.md`: the last bullet goes; a bullet that `index.md` is the one home of the supported versions; the names of the tests | Reference: `../postgresql.md` and `../sqlserver.md` become two nested groups of five pages | Less `postgresql.md`, `sqlserver.md`.  The test gains the checks of the database folders' file names and of tab groups | `guides/troubleshooting.md`: the two links to their `#known-issues` go |
| 5 | The row of a command: `reference/cli.md`, `CliReferenceTests`.  "Write effects": `tool.md` becomes `reference/sidecars.md`.  One name less | | Reference: `../tool.md` becomes `cli.md`, `describing.md`, `connections.md`, `bundles.md` around `sidecars.md`; `msbuild.md` under Settings.  Guides: three pages under Workflow.  Get started: `keep-up-to-date.md` | Less `tool.md` | `reference/sidecars.md`: the sentence that points to `tool.md` goes.  One link less on `guides/troubleshooting.md` |
| 6 | The row of an attribute: `reference/attributes.md`.  One name less | | Reference: `../generator.md` becomes the nine pages of Generated code; `attributes.md` under Settings; a group Types after Generated code.  Guides: a group "Other ways to use your SQL" | Less `generator.md` | One link less on `guides/troubleshooting.md` |
| 7 | The rows of a setting, a marker, a generator parameter and an MSBuild property, in the outline's words, with `SettingsReferenceTests`.  One name less, and the sentence on "Known issues" goes | `docs/deferred/AGENTS.md` and `docs/tech-debt/AGENTS.md`, line 40: the clause on a page not yet moved goes | Reference: `../settings.md` becomes the pages of Settings and Types.  Guides: a group Projects | Less `settings.md` | The sentence of `guides/troubleshooting.md` that links known issues goes |
| 8 | "Pages not yet moved" goes.  "Terms" names no glossary: a term is defined once, in the reference section that documents the thing.  The README bullet follows the README | `reference/AGENTS.md`: the sentence on a page not yet moved goes.  `start/AGENTS.md`: its second bullet names `concepts.md`, not `../concepts.md` | Guides: the remaining pages, and `../recipes.md` goes.  Get started: `../concepts.md` becomes `concepts.md`, and `transactions.md` and `testing.md` join | Empty: the list and its bullet in `Documentation/AGENTS.md` go | `reference/databases/other.md` gains the sentence that sends the reader to `../../guides/other-databases.md`, which this phase makes |
| 9 | | | Guides: `nullability.md`, `output-modes.md` | | |
| 10 | The row of a Get started feature: a part of one `start/` page | `start/AGENTS.md`: its second bullet goes | Get started: the nine pages, and `concepts.md` goes | | `index.md` is written new |
| 11 | The README bullet | Root rule 4: the README bullet and "or an FAQ answer" | | | |
| 26 | A last pass | A last pass | | | `docs/publishing.md`: the exception goes |

### 11. Interim links this phase leaves

A link to an anchor that a later phase makes names the nearest place that exists, and that phase retargets it.  The brief of each phase named in the last column lists the link under "Interim links this phase retargets".  A link that keeps its anchor and only waits for its page to move is not listed: the phase that moves the page finds it by search.

| Link, by its line at the baseline | Now on | Parked at | The map's target | Phase |
|----|----|----|----|----|
| `parameters-and-tokens.md` 91 | `reference/parameters.md` | `../sqlserver.md#known-issues` | `D/sqlserver/query-methods.md#limit-list-in-an-aggregate` | 4 |
| `postgresql.md` 3 and `sqlserver.md` 3, the first link of each | The root page | `settings.md#setting-dialect` | `R/settings.md#setting-dialect` | 4 re-bases it; 7 |
| The five links of `site/README.md` 57 to a `#known-issues` | `guides/troubleshooting.md`, one sentence | The `#known-issues` of the five root pages | None: each limit has its own entry | 4, two links; 5; 6; 7, the last link and the sentence |
| The sentence that points to the sidecar's own section | `reference/sidecars.md` | `../tool.md#sidecar-files` | None: the section stands on the page | 5 |
| `parameters-and-tokens.md` 177 | `reference/tokens.md` | `../generator.md#token-methods` | `R/members.md#token-methods` | 6 |
| `sql-files.md` 47 | `reference/sql-files.md` | `../settings.md#all-settings` | `R/settings.md#all-settings` | 7 |
| `sql-files.md` 134 | `reference/sql-files.md` | `../settings.md#setting-dialect` | `R/settings.md#setting-dialect` | 7 |
| `sql-files.md` 200 | `reference/sidecars.md` | `../settings.md#known-issues` | `R/msbuild.md#limit-file-added-by-a-target` | 7 |
| `concepts.md` 91; 103 | The root page | `reference/parameters.md`; `reference/tokens.md` | None: both closing sentences go | 8 |
| `site/README.md` 3, to `concepts.md#glossary`, and lines 9 and 109, to `concepts.md` | `index.md` | The same, at the root | `S/how-it-works.md#words` | 8 re-bases them; 10 writes the page new |

## The ledger

`ledger/phase-03.tsv` holds a row for each unit of the baseline lines that step 3 and step 4 name, with the disposition of its row in the map's section 4, and a row for each piece of new text.  Where this brief adds to the map:

- `site/README.md` 57 is `move-split`: three of its links are `restated-from` `guides/troubleshooting.md`, and five move there.  Lines 112 and 113 are `move`, with a note of the link each gains.  The sentence of C-06 is `move` here; its `corrected` row is phase 10's.
- `parameters-and-tokens.md` 262 is `restated-from` the bullet of `sqlserver.md` 555-565, which phase 4 makes a limit.
- `site/errors.md` is not in the inventory: it has one `move` row, with the unit `@site/errors.md`.  So have the row and the bullet that phase 2 added to `site/README.md`, each by its line.  Text of feature work that step 1 found is `move`, named in the same way.
- A unit whose words step 4 mended is `move` with the note `position`.
- `new`: each heading, anchor and sentence that steps 3, 4 and 5 mark "New", and `guides/troubleshooting.md` whole.  The three promoted headings of `reference/tokens.md` are a note on their `move` rows.  A `toc.yml` and an `AGENTS.md` are not units.

The checker must show:

- "check coverage": passes for phases 0 to 3.  "check rewrite" does not apply.
- "check move 03": nothing left over but the units listed here.  Those are line 57 of `site/README.md` and line 145 of `dialects.md`, which are cut; the five headings and the three pointer bullets that go; line 194 of `sql-files.md`, if its new link needs new words; and each unit with the note `position`.  The checker reduces a link to its text and compares a heading without its `#` marks, so lines 112 and 113 and the promoted headings are in no list.

## Done when

- [ ] `git ls-files site` is exactly: `AGENTS.md`, `concepts.md`, `diagnostics.md`, `docfx.json`, `generator.md`, `index.md`, `postgresql.md`, `recipes.md`, `settings.md`, `sqlserver.md`, `toc.yml`, `tool.md`; `start/AGENTS.md`, `start/toc.yml`; `guides/AGENTS.md`, `guides/toc.yml`, `guides/troubleshooting.md`; `reference/AGENTS.md`, `reference/toc.yml`, `reference/dialects.md`, `reference/errors.md`, `reference/parameters.md`, `reference/sidecars.md`, `reference/sql-files.md`, `reference/tokens.md`; `reference/databases/AGENTS.md`, `reference/databases/index.md`, `reference/databases/other.md`.
- [ ] `tools/build-site.sh` ends with `0 warning(s)`.  `artifacts/site` holds `index.html` and `reference/databases/other.html`, and no `README.html` and no `agents.html`.  `head -1 site/index.md` prints `# SqlSource manual`.
- [ ] `git grep -n -E "parameters-and-tokens|site/(README|sql-files|dialects|errors)\.md|SqlSource/(README|sql-files|dialects|errors)\.html" -- . ":!docs/superpowers"` prints nothing but data of a test that phase 0 wrote.
- [ ] `git grep -n "known-issues" -- site/index.md site/guides site/reference` prints only links to the `#known-issues` of `settings.md`, `generator.md`, `tool.md`, `postgresql.md` and `sqlserver.md`.
- [ ] `git grep -n "<a id=\"limit-" -- site` prints two lines.  The tests of `tests/SqlSource.Repository.Tests` pass, `SiteStructureTests` among them.  Then prove the test: add `site/extra.md`, see it fail, and remove the file.
- [ ] `wc -c AGENTS.md site/AGENTS.md site/*/AGENTS.md site/reference/databases/AGENTS.md` shows the root under 16,384 and each other under 8,192, and `git grep -n "When a phase lands" -- site` prints nothing.
- [ ] "check coverage" and "check move 03" show what [The ledger](#the-ledger) says, and `merges/` holds `MP-08.md`, `MP-10.md` and `MP-23.md`, each with the texts of this phase.
- [ ] The reviews of the outline's "Reviews" are clean on the last commit, and `./pre-commit-validation.sh --full` passes on the tree as pushed.  The pull request says that `gh workflow run docs.yml --ref main` is due after the merge, and that the owner starts it.

## For the reviewers

The no-loss reviewer, beyond the kit:

- The two units that are cut: line 145 of `dialects.md` (three sentences on `databases/index.md`, two on `databases/other.md`) and line 57 of `site/README.md`.  Every sentence and every link is in one place, and none in two.
- The three pointer bullets that go.  Each target states what the bullet said: `names-read-as-parameters` has the rows for a user variable and for a procedure's parameter; `still-read-differently` is the list; and `parameters-and-tokens.md` 91, which linked the bullet of line 262, now links `../sqlserver.md#known-issues`, where the full text is.
- `reference/tokens.md` against lines 3-18 and 158-254: the promoted headings changed their level and no word.  The rows of the landing page's table, counted: the 49 of the baseline and the one of phase 2.

The accuracy reviewer, beyond the kit, reads every `AGENTS.md` of step 8 against the tree as this phase leaves it, so that each page, anchor and test that a rule names exists, and checks:

- Each target of the table in step 4, opened on the built site: the link says what its words promise.  `generator.md` 77 is the one whose words, "`.sql` files", no longer name its target; the map sets the target, and the words wait for their rewrite.
- The new sentences: the first paragraph of `reference/dialects.md` against decision 5; the opening of `reference/databases/other.md`, which must give "every row marked No" and "one of them" something to refer to; the four entries of `guides/troubleshooting.md` against the sections they link.
- Known and accepted: `reference/databases/index.md` is titled "Supported databases and versions" and states no version until phase 4; some sections of `guides/troubleshooting.md` are empty; `reference/sidecars.md` holds one limit and a sentence that points.

## Traps

- **A link that resolves to the wrong page.**  docfx and the tests catch a link to nothing.  Nothing catches a bare `dialects.md` that goes to `reference/dialects.md` where the table of step 4 says `settings.md#setting-dialect`.  Do the links of that table by hand.
- **One name, several things.**  `known-issues` lives on five root pages, and `limits` is on two new ones: never replace `#known-issues` across files.  `README.md` is three files, the repository's, the tool's and, until this phase, the site's, and `index.md` is two.
- **Tidying while moving.**  A reworded sentence shows up in "check move 03", and it belongs to phase 13.  **The front matter of `index.md`.**  If the three lines stay, the root still redirects to a page that is gone, and docfx says nothing.
- **YAML.**  A `toc.yml` name that holds `: `, or that starts with `{` or a quote, needs quotes, as three names of step 2 have.  **`AgentsFileTests` counts a paragraph as a rule**, so the opening line of each `AGENTS.md` is under 80 words too; a table row counts as bytes only.
- **Phases 0 to 2 may have named things otherwise** than this brief assumes: a test method, a checker command, a bullet of `site/AGENTS.md`.  The tree wins.  Do not undo their work to match this text.

## Left to the agent

- The words of every piece of new text: the opening sentences, the h3 heading of each limit, the entries of `guides/troubleshooting.md` and the section each stands in (`wrong-model` fits the two about parameters, and `not-described` the sidecar).
- How the links are rewritten.  A script of your own is fine if it stays out of the repository; the tables of step 4 are its input.
- The code of `SiteStructureTests` and of the changed tests, and the names of its test methods.  Only the name `PagesNotYetMoved` is fixed.  The order and the number of commits, as long as each passes `./pre-commit-validation.sh`.

## Calls made in this brief

- **`site/reference/databases/AGENTS.md` lands here, not in phase 4.**  The folder exists from this phase, the outline says that phase 3 lands the rules, and a file that waits is one that two briefs written at the same time can each leave to the other.  It carries one bullet that phase 4 removes.
- **The site's root may carry an anchor** in a link, and resolves to `site/index.md` with it.  The root is a page now and drops nothing.  **`index.md` has no front matter** in this phase: docfx needs none, and the layout is phase 10's.
- **The interim sidebars hold only the groups that have an entry**, in the map's order, and a later phase adds a group with its first page.  `../recipes.md` stands above the groups of Guides, since its recipes go to all of them.
- **The h1 of a new page is its sidebar title** of the map's section 1.  The map gives no h1, and the site's search shows one entry for each page, under its h1.  **The headings of `reference/tokens.md` are promoted** in the move: the map's section 3 lists them as h2, and a level is not a word.
- **Line 57 of `site/README.md`** becomes one link, as the map says, and the five `known-issues` anchors that outlive this phase are linked from one sentence of `guides/troubleshooting.md`, which each move phase shortens.  The map did not say where those five links live in between.
- **Lines 112 and 113 of `site/README.md` each gain one link**, on words they already have, since each names a page that is now two.  The map's section 6.4 lets the bare links of that page "go with it", but its text lives on in `index.md` until phase 10.  **Three other bare links get a target for now**, where the map only gives an end: `README.md` 58 goes to `reference/parameters.html`, and `concepts.md` 91 and 103 to `reference/parameters.md` and `reference/tokens.md`.
- **`reference/databases/other.md` does not yet send the reader to `guides/other-databases.md`.**  The page does not exist until phase 8 makes it, from line 181 of `README.md`.  The phase that makes the page writes the sentence, so it is phase 8's, as the map says.  The page gains one opening sentence that the map does not list, so that the moved text has its referents.  **`reference/sidecars.md` has one sentence that points to `tool.md`**, which phase 5 removes: a page of one limit with no word on its subject would puzzle a reader of the published site.
- **`SiteStructureTests` asks for each `limit-` anchor**, not only each page with `## Limits`: the outline's table says that a limit with no row fails the test.  The checks of the database folders and of tab groups wait for phase 4, which makes the first folder.
- **A limit's bullet stays a bullet** under its h3, so that a move stays word for word; the rewrite gives it the shape of a limit.  **Four links of `docs/architecture/generator.md` change from a heading's slug to its explicit anchor**: they would resolve after this phase and break in phase 13, which rewords headings.
- **The rules of `docs/deferred` and `docs/tech-debt` point to `site/AGENTS.md`** for how a limit is written, and line 31 of each changes with lines 22 and 40, which the map names alone.
- **`sql-files.md` 194 gains its link to `errors.md#what-an-error-costs` here**, if phase 2 did not add it: the map names the link and no phase.  **`site/AGENTS.md` drops two bullets with no successor**: what each page opens with, and "Recipes hold examples", which lives on as a clause of "Pages not yet moved" and in `site/guides/AGENTS.md`.  "Example first" moves to `site/reference/AGENTS.md`.
- **The table "Find what you need" has 49 rows at the baseline**, lines 9 to 57, and 50 on `index.md`, with the row of phase 2.  How many leave is phase 10's count.
- **Words that the move makes wrong are mended here**, with the note `position`, as the outline rules.  No other word of moved text changes.
