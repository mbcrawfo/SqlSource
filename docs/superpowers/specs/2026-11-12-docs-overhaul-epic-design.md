# User documentation overhaul - epic outline

This is the outline of an epic that rebuilds the user documentation as a website.  It records what the owner decided, the structure of the new site, the rules that keep the structure, and the phases that deliver it.  Each phase has a document of its own, named in [Phases](#phases), which is the brief for the agent that does that phase.  The appendix [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md) says where every part of today's documentation goes.

This outline is kept current until the epic closes.  A phase that changes the plan updates it in the same pull request.

## Intent

The manual was written as Markdown files to be read in the repository.  It is now a GitHub Pages site built with docfx, but its shape did not change: twelve flat pages, four of them over 1,000 lines, an entry page that is a 49-row table, and a walkthrough that lives in the repository's README and not on the site.  Much of the text also speaks of a library in development.

The owner wants the documentation rethought as a site, for a junior developer who works this library out alone.  That reader must be able to start at the landing page, install the package, run a first query, and then find the answer to each later question by its own words, without reading a 20,000-word page.

## Goals

- The site has three groups, each of several pages: **Get started**, **Guides** and **Reference**.
- The site owns getting started.  `README.md` and `src/SqlSource.Tool/README.md` are a brief introduction, setup and how to use, and link to the site.
- Every fact of today's documentation is kept and has one home.  Nothing is dropped by accident, and each intended removal is recorded.
- The text states what SqlSource does, as fact.  No page says that a feature is coming, works "today" or is in development.
- The language aims at ASD-STE100 Simplified Technical English, as a guideline.
- `AGENTS.md` rules and tests keep the structure true after the epic closes.
- Each phase is one pull request that a person can review.

## Non-goals

- No change to the library, the tool, a diagnostic's id or title, or a setting.
- No versioned site, no API reference, no custom domain, no theme work beyond what docfx's modern template gives.
- No new toolchain: docfx and .NET only.
- No change to `docs/architecture/`, `CONTRIBUTING.md` or the example projects beyond the links and names the move makes wrong.
- No glossary page.

## Anti-goals

- A build error's help link that leads nowhere.  `diagnostics.html#sqlsrcNNN` at the site's root must resolve at every commit.
- A fact lost in a move or changed in a rewrite without anyone deciding it.
- A Get started path that sends the reader into Reference in the middle of a step.
- Rules that only ask.  Where a test can hold a rule cheaply, a test holds it.
- A test so brittle that an agent learns to weaken it.

## Constraints

- docfx 2.81, pinned in `dotnet-tools.json`.  `tools/build-site.sh` builds the site and fails on any docfx warning.  `pre-commit-validation.sh` and CI run it.
- Every diagnostic's help link is `https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrcNNN`.  It is compiled into the packages.
- The two readmes are packed into NuGet packages.  nuget.org shows them with HTML off, so they hold no raw HTML, and every link in them is absolute.
- Root `AGENTS.md` caps itself at 16 KB and every other `AGENTS.md` at 8 KB, with bullets of at most 80 words.  `site/AGENTS.md` is 7.8 KB today, and `src/SqlSource.Tool/AGENTS.md` is 33 bytes under its limit and names four site pages.
- 274 references in 78 files outside `site/` name a site page, 51 of them in C# comments.  No test checks them today.
- Five test files under `tests/DatabaseTests/*/EndToEnd/` mirror samples of `site/recipes.md`, and `EditorRecipeTests` reads the VS Code task from it.
- No `v*` tag exists.  The only published package is a beta, whose links into the manual already broke when the manual moved to `site/`.
- Feature work continues on other branches while the epic runs.
- `pre-commit-validation.sh` does not know the kind `.tsv` (`is_known_kind`), so a commit that touches one makes a plain run full.  Phase 0 makes the epic's `.tsv` files a known kind, and phase 26 takes the pattern out.
- `.editorconfig` holds every file but `.md` to 120 columns, and `tools/editorconfig-checker.sh` checks every tracked file.  Phase 0 frees the epic's `.tsv` files of the limit, and phase 26 takes the section out.

What docfx does, checked in a throwaway site:

- A root `toc.yml` of three folder entries is the top bar, and each folder's `toc.yml` is its sidebar.
- A page at the root can be listed in a folder's sidebar as `../diagnostics.md`.  It shows that sidebar, but no top-bar highlight and no breadcrumb.
- docfx checks anchors across folders, so the build fails on a bad anchor after a move.
- An anchor inside a tab that is not selected is a dead link target.  The choice of tab does not carry from page to page.  Text in a tab that is not selected is hidden from the browser's find.
- Search has one entry for each page, not for each section.

## Approaches

Three agents each proposed a structure from one angle, and three reviewers attacked all three: one as a junior developer looking for answers, one checking the content maps against the pages and the source, and one testing whether each plan can be built, reviewed and kept.  Each reviewer ranked a different proposal first.  The proposals agreed on the folders, the landing page, a folder for each database, and `diagnostics.md` at the root.  They differed in where the rules live.

| Approach | Trades off |
|----|----|
| **Combined (chosen)** | Get started is a path read in order.  Guides are whole task pages.  Reference is one page for each thing, and each setting is documented beside what it changes, with an index of every setting and place.  A guide restates the rules its task needs, so a guide can drift from its reference owner, and only a reviewer catches that. |
| One settings home | Every setting lives on the settings pages and all other pages link there.  The rule for a new setting is exact.  It separates a setting from the behaviour it changes; six sections of today's text have no line to split on, so the move becomes a rewrite. |
| Thin pages over a complete reference | Get started and the middle group show a sample and link; every rule is in Reference.  Nothing is stated twice.  The junior-reader review ranked it last: most answers are two clicks away and the task pages are thin. |

The owner chose the combined structure: it is the one that each reviewer's "take from each" list pointed to.

## Decisions made with the owner

1. **Three groups.**  Overview, setup and the most common use cases; advanced topics; reference.
2. **The site owns getting started.**  The README keeps a brief introduction, setup and how to use, for a reader who arrives through the repository, and links to the site.
3. **Keep it, move it.**  Every fact survives, moved to the page that owns it and rewritten in plain language.  Two things are removed on purpose: the language of a library in development, and the preamble of `diagnostics.md`.
4. **Output modes.**  `repository` is the default.  `methods` is for a reader who uses the queries in code structures of their own, above all to control transactions.  `models` and `sql` are fallbacks, mainly for Dapper users.
5. **Dialect is not a choice.**  A reader sets the dialect to their database, `postgres` or `mssql`.  Another dialect matters only for the `sql` output.
6. **Supported databases.**  PostgreSQL and SQL Server are grouped under one heading that takes more databases later.
7. **Diagnostics.**  The page is the list of error sections and nothing else.  The 161 sections are rewritten in plain language too.
8. **The reader and the language.**  A junior developer alone with the library.  ASD-STE100 is the target, as a guideline.
9. **The combined structure**, and **no glossary page**: nearly every term is known to a developer with basic .NET and SQL, and the few that are SqlSource's own are defined where they are documented.
10. **Roles.**  Fable agents proposed and reviewed the plans.  Opus agents do each phase and write the pages.
11. **Content reviewers.**  Besides the repository's normal reviews, each phase has content reviewers who confirm that the new text is accurate and that every drop against the old documentation is intended.
12. **The site is published by hand after each phase that moves a page**, so that the README on `main` never links for long to a page the site lacks.

Calls of mine, which the owner has not ruled on, are in [My calls](#my-calls).

## The design

### The site

```text
site/
  index.md            landing page
  diagnostics.md      at the root; in the Reference sidebar as "Errors (SQLSRC)"
  toc.yml             top bar: Get started, Guides, Reference
  start/              Get started, read in order
    install.md  first-query.md  how-it-works.md  write-queries.md  call-queries.md
    transactions.md  testing.md  keep-up-to-date.md  errors.md
  guides/             one task on each page; the groups are in toc.yml only
    Queries:              lists.md  nullability.md  dynamic-sql.md
    Running queries:      many-rows.md  sync-and-streaming.md  repository-connection.md
                          naming-and-placement.md
    Types:                custom-types.md  json.md  nodatime-and-spatial.md
    Projects:             several-databases.md  folders-and-files.md  several-projects.md
    Workflow:             describe-on-save.md  ci.md  report-a-bug.md  troubleshooting.md
    Other ways to use your SQL:  output-modes.md  dapper.md  other-databases.md
  reference/          look-up; one page for each thing
    .sql files:           sql-files.md  parameters.md  tokens.md  dialects.md
    Settings:             settings.md  settings-file.md  generator-parameters.md
                          attributes.md  msbuild.md
    Generated code:       members.md  models.md  model-properties.md  query-methods.md
                          return-types.md  repositories.md  connection-methods.md
                          collection-methods.md  exceptions.md
    Types:                type-mapping.md  library-types-and-json.md  converters.md
    The sqlsource tool:   cli.md  describing.md  connections.md  sidecars.md  bundles.md
    Supported databases:  databases/index.md
                          databases/postgresql/{index,nullability,types,libraries,query-methods}.md
                          databases/sqlserver/{index,nullability,types,libraries,query-methods}.md
                          databases/other.md
    Errors:               errors.md  ../diagnostics.md
```

That is 70 pages in place of 12: 2 at the root, 9 in `start/`, 20 in `guides/` and 39 in `reference/`.

The content map is the authority on sidebar titles, each page's headings and anchors, and where each part of today's text goes.  It gives the reason for each page that splits one of today's.  Its sizes are counted at the density of today's text.  Plain language makes pages longer, so each rewrite phase recounts its pages and reports the count, and the `resize` phase splits a page that is then over 3,500 words.

**The landing page** holds one paragraph on what SqlSource does, one sample pair (a `.sql` file and the C# that calls it), a table that answers "does it work with my database?", one link to start, and about sixteen "I want to" rows.  It links to the errors page, to the Dapper guide and to the guide for other databases.

**Get started** is the path of a newcomer.  A topic is there only when an application with one PostgreSQL or SQL Server database and the default `repository` output needs it before its first release.  Transactions are there, and are where the `methods` output is introduced, by the need for it.  Each page:

- teaches with one running example and the `repository` output;
- gives each step whole, and never sends the reader to Reference in the middle of one;
- states a rule in its common form and links to its owner;
- holds no `## Limits` section and no setting's definition;
- puts a trap of the first hour where it bites, such as a `.env` file at the repository's root that is not read;
- ends with "Next:" and the next page.

**Guides** are the owner's "advanced topics".  The group is named for what its pages are: one task each.  A guide has a fixed shape: the situation, a whole sample, what you get, the rules the task needs, and last the limits that bear on the task, as links to the pages that own them.  A guide has a `## Limits` heading of its own only when it owns a limit.  Each rule it restates links to the reference section that owns it.  A title is in the reader's words and carries the word a junior types in a search box: "bulk", "mock", "nullable", "timeout", "IN", "enum".

**Reference** is look-up.  A reader arrives by search or by a link and does not read top to bottom.  A section opens with an example, then the rules.  Each written name (a settings-file key, a marker, a generator parameter, an attribute property, an MSBuild property, a command, an option, an environment variable) is defined in one section, on the page of the thing it changes.

A sidebar group aims at no more than nine entries, and a page at 600 to 3,500 words; the content map flags each page outside that and says why it is allowed.  Both are rules for a reviewer, and no test holds them.

### Where rules live

- **A setting** is documented in full on the reference page of the thing it changes, under an anchor `setting-<name>`, which keeps its name from today.
- **`reference/settings.md`** holds: the table of the places a setting can be written (the place, how it is written there, what it applies to); the matrix of every setting, with one column for each place; which place wins; and the output, database and dialect settings.  `reference/settings-file.md` holds the settings file, `sqlsource.json`.
- **"Setting" has one meaning**: a named choice that changes what SqlSource generates or does, written in one of five places.  The places are a marker inside a query, a marker in the file header, a key of a settings file, a property of an attribute, and an MSBuild property of the project.  Today's text lists properties of the attribute and of the project among settings and then says they are not settings.  The content map lists the 22 rows of the matrix.
- **A guide** is whole for its task.  It does not list a setting's values, default or places; it shows the setting in use and links.
- **A term** is defined once, in the reference section that documents the thing.  The words that look alike (three kinds of parameter; dialect and logical database) are told apart on the reference page of the thing, and `site/AGENTS.md` keeps the rule for authors.

### How the owner's reframings land

- **Output modes.**  Get started uses `repository` only and does not ask the reader to choose.  `start/transactions.md` introduces `methods`.  `guides/output-modes.md` compares the four in the owner's terms.  `guides/dapper.md` is the one page for `models` and `sql` with Dapper or ADO.NET, with a short whole walkthrough for both databases.  `reference/settings.md` holds the output setting's rules.
- **Dialect.**  `start/first-query.md` has one step, "name your database": set the dialect to `postgres` or `mssql`.  No page is titled "Choosing a dialect".  `reference/dialects.md` opens by saying that a reader with PostgreSQL or SQL Server sets it and reads no further, and then holds today's tables whole.  `guides/other-databases.md` is the path for the `sql` output on any other database.
- **Supported databases.**  Each described database is a folder of the same five pages: `index.md`, `nullability.md`, `types.md`, `libraries.md` and `query-methods.md`.  `databases/index.md` is the one home of the supported versions; the landing page, `start/install.md` and the README restate the two minimum versions and link to it.  The parity is of file names only: PostgreSQL's array parameters and SQL Server's lists are different features and are not forced under one heading.  A difference that is long is two anchored sections; a tab holds a sample or a few sentences, and never a heading or an anchor.
- **Statements of fact.**  The "What works today" table, the Status column of the settings table and the "Coming in a later release" callout go, with the rules that made them.  A sentence that says something is not tested or was checked on one platform is a fact about support and stays.  `--prerelease` stays on every install command, since the packages are previews, and the sentence beside it says only that.

### Diagnostics

- `site/diagnostics.md` stays at the root.  One short paragraph stands between the title and `## SQLSRC001`, and it links to `reference/errors.md`, "How errors are reported".
- The preamble's index tables and range paragraphs go.  Facts that are stated nowhere else move first, to `reference/errors.md` or into the sections: which diagnostics can be made warnings and that no other can; which ids are the tool's; when the build checks only some ids; that a section which names no database is PostgreSQL's.  The last is not moved but mended: each of those 18 sections gains a "Database" line.  Every section gains a "Reported by" line that says what reports it, the build or the tool.  Phase 2 adds both lines, so that no section depends on a sentence that is gone.
- Each section keeps its `## SQLSRCnnn` heading, its anchor and its bold title line, which a test compares with the descriptor.
- The rewrite gives each section one shape: the title, the "Reported by" line and the "Database" line where it has one, what happened, why, the fix as numbered steps, a sample where one helps, and a closing `See:` line.

### The readmes

`README.md`: what SqlSource does and why, with one sample pair; the databases it works with; install; use it in a few short steps, each linking to its Get started page; "Other ways to use it", three bullets for `methods`, `models` and `sql`; where the documentation is; a link to `CONTRIBUTING.md`.  It holds no FAQ and no scenario.  `src/SqlSource.Tool/README.md`: what the tool is, install, the one command, and a link to the site.  The content map gives both outlines and says where each of today's README sections goes.

### Limits

A "Known issues" entry becomes an entry of `## Limits` on the page that owns the behaviour, with an anchor of its own, written as what the reader sees, why, and what to do.  `guides/troubleshooting.md` lists every limit by what the reader sees.  The rule of today on when to add and remove one, from the open items of `docs/tech-debt` and `docs/deferred`, stays.

### The language

ASD-STE100 as a guideline.  The rule for authors, which a reviewer applies and no test does:

- One instruction in a sentence.  At most 20 words in a step and 25 in a description.
- Active voice and present tense.  Not "will".
- One term for one thing.
- A condition comes first: "If the column can be null, ...".
- "Must" for what is required and "can" for what is allowed.
- A list for conditions and alternatives, not a long sentence.
- A paragraph has one topic and at most six sentences.

## Rules that keep the structure

Phase 3 lands these rules with the skeleton, so that feature work on other branches follows the new structure from then on.  A rule or a row that names a page or a test lands in the phase that makes the page or the test, so that no `AGENTS.md` names a thing that does not exist yet; until then the row names the page of today.  Each phase updates the `AGENTS.md` lines it makes wrong, in the same commit, and checks the byte limits.

### Where a new thing goes

This table opens `site/AGENTS.md`.

| New thing | Goes in | What fails if it does not |
|----|----|----|
| Setting, as a settings-file key or a marker | A `setting-<name>` section on the reference page of what it changes; a row in the matrix of `reference/settings.md` | `SettingsReferenceTests` |
| Marker | A row in the marker index of `reference/sql-files.md` | `SettingsReferenceTests` |
| Generator parameter | `reference/generator-parameters.md`; the section of `SQLSRC109` | `SettingsReferenceTests`, `DiagnosticsDocumentTests` |
| MSBuild property | A section of `reference/msbuild.md`; a row in the matrix | `SettingsReferenceTests` |
| Attribute, or a property of one | `reference/attributes.md` | A reviewer |
| Command, option, environment variable, exit code | `reference/cli.md` | `CliReferenceTests` |
| Diagnostic | A `## SQLSRCnnn` section of `diagnostics.md`, in id order | `DiagnosticsDocumentTests` |
| Dialect | Its rows in `reference/dialects.md`; the section of `SQLSRC019` | A reviewer |
| Database that can be described | A folder of the five pages under `reference/databases/`; a row in `databases/index.md`; a tab in every tab group that names a database; the README's list; an example project | `SiteStructureTests` |
| Feature that meets the line for Get started | A part of one `start/` page; its rules in Reference | A reviewer |
| Any other feature | A reference section; a guide when it is a task; an example, as root rule 4 says | `SiteTocTests`, for a new page |
| Limit | `## Limits` of the page that owns it, with its own anchor; a row in `guides/troubleshooting.md` | `SiteStructureTests` |

### `AGENTS.md` files

`site/AGENTS.md` is nearly full, so the rules are pushed down, as the root guidelines ask.  `docfx.json` already keeps every `AGENTS.md` off the site.

- **Root `AGENTS.md`, rule 4.**  User documentation states what SqlSource does, as fact.  `README.md` is a brief introduction that links to the site and holds no FAQ, scenario or option.  `site/` has three folders and two root pages.  The link form of the packed readmes names the folder: `<folder>/<page>.html#<anchor>`.  The callout bullet and the "What works today" wording go.  The bullets on `--prerelease`, SQL info strings, internals and `site/diagnostics.md` stay.
- **`site/AGENTS.md`.**  The table above.  A fact has one home.  Never move `diagnostics.md` or change a `## SQLSRCnnn` heading.  State what SqlSource does; no status.  The language rule.  The terms: say *query parameter*, *generator parameter* or *logical database* where "parameter" or "database" alone is unclear.  No heading or anchor inside a tab.  "Write effects, never internals", example first, anchors, links and SQL samples stay as they are.
- **`site/start/AGENTS.md`.**  The line for Get started.  The pages are read in order and each ends with "Next:".  One running example and the `repository` output.  A rule in its common form with a link to its owner; no `## Limits`, no setting's definition.
- **`site/guides/AGENTS.md`.**  A guide is one task, in the fixed shape.  Show a setting in use and link it; never list its values, default or places.  A title in the reader's words.  A new guide goes under a group of `toc.yml`; no group of one page, and none over nine.
- **`site/reference/AGENTS.md`.**  One section defines each written name, on the page of what it changes.  A section opens with an example.  Reference states rules, not how-to.
- **`site/reference/databases/AGENTS.md`.**  Every database folder has the same five file names.  A page states only what is true of its database.  What is true of every database is on a page outside the folders.

The phase documents carry the final wording.  "When a phase lands", with its table of the query-generation phases, goes from `site/AGENTS.md`: the table above replaces it.

### Tests

Each test reads the code or the file system, and fails only for a real mistake.

| Test | What fails it | Lands in |
|----|----|----|
| `InboundSiteReferenceTests` (new) | A reference to `site/<path>.md[#anchor]` or to a site URL, in any tracked text file, that names no page or anchor.  It leaves out `docs/superpowers`, the user documents, whose links `UserDocsLinksTests` follows, and four named exceptions of test data | Phase 0 |
| `UserDocs`, `UserDocsReadingTests`, `UserDocsLinksTests` (changed) | They read `site/` in folders, read a tab heading and front matter correctly, and accept `folder/page.html` in a site URL.  `Manual_EntryPage_ListsEveryPage` goes | Phase 0, and phase 3 for the root page |
| `SiteTocTests` (changed) | A page that is not in exactly one `toc.yml`; an entry that names no page | Phase 0 |
| `DiagnosticsDocumentTests` (changed) | `diagnostics.md` is not at the root; more than the title and one paragraph stands before `## SQLSRC001`; `SqlDiagnostics.HelpLinkBase` is not the site's address and `diagnostics.html#`.  The test of the index tables goes | Phase 2, and phase 3 for the path of `errors.md` |
| `StatusLanguageTests` (new) | One of a short list of exact phrases in a user document: "Coming in a later release", "works today", "no effect yet", "early development".  Not "still to come", which today's text uses properly of rows | Phase 1 |
| `SiteStructureTests` (new) | A page at the root other than `index.md`, `diagnostics.md` and a list in the test of the pages not yet moved, which each move phase shortens and phase 8 empties; a folder other than the three; database folders with different file names; a tab group that names one described database and not all; a page with `## Limits` that `guides/troubleshooting.md` does not link | Phase 3, growing in phases 4 and 8 |
| `SupportedVersionsTests` (new) | The table of `reference/databases/index.md` disagrees with the pinned floor and default images or with the versions the descriptors enforce; a restatement on the landing page, `start/install.md` or the README disagrees with the table | Phase 4, growing in phases 10 and 11 |
| `SettingsReferenceTests` (new) | A key of `schemas/settings-v1.schema.json`, a `SqlMarkerKind`, a generator parameter or one of the five user MSBuild properties with no row; a `setting-` anchor with no row or with two | Phase 7 |
| `CliReferenceTests` (new) | A command or option of the tool that is not hidden, or a `SQLSOURCE_` variable, with no row in `reference/cli.md` | Phase 5 |
| `ReadmeTests` (new) | A readme over its size; an install or update command of a readme that is not on `start/install.md` | Phase 11 |

Left out on purpose, as brittle or of little value: a test of page sizes; a test that a guide does not repeat a setting; a test that every `See:` line goes to Reference; a classification of every MSBuild property the build reads; a test of single words such as "later" or "yet", which have hundreds of proper uses.

## Delivery

### How a phase is done

Every phase is one pull request, done by an Opus agent from its phase document.  A phase leaves the site building with no docfx warning, every test passing, every `AGENTS.md` true, and no page saying something false.

**Today's text is moved or rewritten, never both in one pull request.**  A move is verbatim: the words do not change, only the page, the anchors where the map says so, and the links.  Where two of today's sections become one, the move places them side by side on the new page, each untouched under its own anchor, and the rewrite of that area merges them.  This gives a move a mechanical check, and lets the rewrites run in parallel once the moves are in.

Five things a move phase does besides moving, each listed in its ledger:

- **It adds the text that the map marks `new`** for its pages: a heading, an anchor, an index table, a sentence that points.  New text is written in plain language and gets the accuracy review.
- **It makes every destination page and anchor that its units need**, also a page that a later phase fills.  A part of `tool.md` that belongs on `reference/msbuild.md` makes that page in phase 5, and phase 7 adds to it.  A link to an anchor that a later phase makes names the nearest place that exists, and that later phase retargets it.  After phase 10 every anchor of the content map exists, so a rewrite adds and removes none.
- **It places each known-issue bullet of its pages** under `## Limits` of the page that owns it, with the bullet's own anchor, and adds its row to `guides/troubleshooting.md`, which phase 3 makes.  No `known-issues` anchor outlives its page.
- **It makes each correction** that the map lists for a sentence it carries, so that a false sentence is not moved.
- **It mends the words that a move makes wrong**: "this page", "above", "below", or a page of today named in words, such as "the generator page".  It changes the fewest words that make the phrase true, and lists each in the ledger with the note `position`.

**A heading that a rewrite merges from two keeps both anchors**, as two `<a id>` lines above it, so that a rewrite changes no link in another page.

**A rewrite pull request holds at most about 12,000 words**, the most a reviewer can read old against new.  A page that is written new, in phases 2, 9 and 10, is in plain language from the start and has no rewrite.

### The ledger

A diff cannot show that a move lost nothing: a paragraph is one line, and two thirds of the prose is on lines whose links change.  So the epic keeps its own record, in `docs/superpowers/specs/2026-11-12-docs-overhaul/`, beside the content map: `inventory.tsv`, a `ledger/` folder with one file for each phase, a `merges/` folder with one worksheet for each merge pair, and the checker in `check/`.  The last phase removes all four; the content map stays, as the record of the plan.

- **A baseline.**  The baseline is commit `28e20e3c`, whose line numbers the content map uses.  Phase 0 records it in the inventory and makes no tag, since `git show 28e20e3c:<path>` reads a baseline page without one.  It makes an inventory of every unit of the user documents at that commit: each paragraph, bullet, table row and fenced block, with its file, line and a hash.
- **The ledger.**  Each unit has a disposition, in the words of the content map: `move`, `move-split`, `merge-pair`, `restated-from`, `removed-status`, `removed-preamble`, `corrected`, and `new` for text that has no unit.  A unit that does not move whole names its reason or its owner.  A checker fails on a unit that has no disposition.  "Every drop is intended" is then a file that a reviewer reads.  The ledger is one file for each phase, so that phases which run together do not edit the same file.
- **The check of a move.**  The checker strips link targets and `<a id>` lines and matches units exactly.  A true move leaves nothing over.  What is left over is the reviewer's reading list.
- **The check of a rewrite.**  Old and new are read side by side, unit by unit.  The checker compares, for each page, the code spans, the diagnostic ids, the numerals and the rows of each table, as counted sets, and lists each sentence that lost "not", "only", "never" or "unless".  A fenced block is byte-identical unless the phase lists it.
- **A merge worksheet** for each pair of near-duplicate sections: both texts, the merged text, and for each conflict the source file that settles it.
- **Text added after the baseline** by feature work is inventoried by the phase that moves its page.

The inventory, the ledger, the worksheets and the checker are files of the epic, run and read by the phase's agent and by its reviewers.  The brief of phase 0 is their specification.  They are not tests of the repository, so phase 26 removes all four, `inventory.tsv`, `ledger/`, `merges/` and `check/`, without touching a test project.  They use no toolchain but .NET and the shell.

Two more files of the folder outlive the epic with the content map: `rewrite-guide.md`, the writer's guide of every rewrite, and `diagnostics-guide.md`, that of a diagnostic section.

### Reviews

Each phase passes, in this order:

1. **Content review**, by two Opus agents that did not write the phase and are given the reviewer's kit: the baseline pages, the phase's ledger, the checker's output, and the merge worksheets.
   - **The no-loss reviewer** confirms that each unit of the phase is where the ledger says, that each disposition is right, and that nothing left over is a lost fact.
   - **The accuracy reviewer** checks each statement that is new, merged, corrected or reworded against the source, the tests and the example projects.  A sample on a page that is written new comes from an example project or from a test where one has it; where none has, the reviewer compiles it in a scratch project against the built package and says so.  Text in tabs is checked once for each database.
2. `branch-review:review`, repeated until it comes back clean, as the root `AGENTS.md` requires.
3. `./pre-commit-validation.sh --full`, on the tree exactly as it is pushed.

A fix after any step runs the later steps again, and the content reviewers read again what a fix changed.  The clean branch review and the passing validation are both of the last commit.

A finding is fixed in the phase, however small.  A statement of today's documentation that a reviewer finds to be false is corrected, with a `corrected` row in the ledger.  The content map lists eight that are known, C-01 to C-08.  One example: `settings.md` says that only the tool reads the database setting, while `generator.md` and `MethodPlanner` show that the build reads it too.

### Phases

Each phase document is `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-<nn>-<name>-design.md`, with `<nn>` in two digits.  A phase document may settle what this outline and the map leave open, under "Calls made in this brief"; where one disagrees with this outline, the outline wins.

| # | Phase | What it does | Size | Needs |
|----|----|----|----|----|
| 0 | `guard-rails` | The changed and new tests that let the site have folders, tabs and a landing page; the inbound-reference test; the docfx globs; the inventory at the baseline, the ledger and the checker.  No page changes | About 400 lines of C# | The spec |
| 1 | `status-language` | "What works today", the Status column, the status sentences of the pages and the readmes, the callout and status rules of both `AGENTS.md`; `StatusLanguageTests` | About 150 lines | 0 |
| 2 | `diagnostics-preamble` | The preamble's unique facts written to `errors.md`, which is at the root until phase 3; a "Reported by" line in each section, and a "Database" line in the 18 that need one; the preamble removed; the changed diagnostics test | 346 lines out, about 300 in | 1 |
| 3 | `skeleton` | The three folders, their `toc.yml` and their `AGENTS.md`; `index.md` as a first landing page in place of `site/README.md`; `sql-files`, `parameters-and-tokens`, `dialects` and `errors` into `reference/`; `guides/troubleshooting.md`; pages not yet moved are listed in the sidebar they will join, and `concepts.md` in that of Get started; the new `AGENTS.md` rules; `SiteStructureTests`; `docs/publishing.md` | About 85 KB moved | 2 |
| 4 | `move-databases` | `postgresql.md` and `sqlserver.md` into `reference/databases/`; the table of supported versions; `SupportedVersionsTests` | About 146 KB moved | 3 |
| 5 | `move-tool` | `tool.md` and the recipes of the tool; `EditorRecipeTests`; `src/SqlSource.Tool/AGENTS.md` trimmed; the issue template; `CliReferenceTests` | About 90 KB moved | 4 |
| 6 | `move-generated-code` | `generator.md`, with the sections of `settings.md` on models, members, collections, interfaces and exceptions | About 165 KB moved | 5 |
| 7 | `move-settings` | The rest of `settings.md`: settings, the settings file, types and the build; the matrix and the table of places; `SettingsReferenceTests` | About 100 KB moved | 6 |
| 8 | `move-guides` | The remaining recipes; each term of `concepts.md` placed in the reference section that defines it, and the page parked at `start/concepts.md`; the README's FAQ, scenarios, library and type-mapping sections move to guides, and the README sections become pointers | About 8,500 words moved | 7 |
| 9 | `write-guides` | The guide pages and sections that have no text today, written new: `nullability.md`, `output-modes.md`, `other-databases.md`, the walkthrough of `dapper.md`, and each section the map marks `new` in a guide | At most 11,800 words written | 8 |
| 10 | `get-started` | The nine Get started pages and the finished landing page, written new, taking in the few units that phases 5 and 8 placed on `start/` pages; `start/concepts.md` goes | About 9,000 words written | 9 |
| 11 | `readmes` | Both readmes cut to their outlines; root rule 4's README bullet; `ReadmeTests` | About 250 lines out | 10 |
| 12 | `rewrite-guides` | The moved text of the guides, in plain language and the fixed shape | At most 12,000 words each, phases 12 to 20 | 11 |
| 13 | `rewrite-sql-files` | `.sql` files, query parameters, tokens, dialects | | 11 |
| 14, 15 | `rewrite-settings-1`, `-2` | The Settings group, then the Types group; their merges | | 11 |
| 16, 17 | `rewrite-generated-code-1`, `-2` | The Generated code group; its merges | | 11 |
| 18 | `rewrite-tool` | The group of the tool | | 11 |
| 19 | `rewrite-postgresql` | PostgreSQL's folder, `databases/index.md` and `databases/other.md` | | 11 |
| 20 | `rewrite-sqlserver` | SQL Server's folder | | 11 |
| 21 to 24 | `rewrite-diagnostics-1` to `-4` | The 161 sections, by range of ids, in the one shape | About 11,000 words each | 11 |
| 25 | `resize` | Each page recounted; a page over 3,500 words split where it has a clean cut, with its anchors, its `toc.yml` entry and its inbound links.  A group may then hold a tenth entry.  A fact that a rewrite reported as being on a page whose owner lacks it is placed with its owner.  If nothing needs either, there is no pull request, and phase 26 records the counts | Unknown until the rewrites are in | 12 to 24 |
| 26 | `close` | The inventory, the ledger, the worksheets and the checker removed; the rule on the shape of a diagnostic section into `site/AGENTS.md`; a last pass over every `AGENTS.md`, `CONTRIBUTING.md`, `docs/publishing.md`, `tests/README.md`, `tools/README.md` and `.github/workflows/README.md`; this outline closed | About 200 lines | All |

Phases 0 to 11 run one after another: the moves share their source pages, each one rewrites links in the pages that remain, and phases 1 to 3 all edit `site/AGENTS.md`.  Phases 12 to 24 touch separate files, or separate ranges of `diagnostics.md`, and change no anchor and no link in another page, so they can run at the same time.  That is 27 pull requests, or 26 if no page needs a split.

A branch of feature work that edits a page after its move ports its text by hand.  To keep that rare, phases 3 to 11 run back to back, and each move can be run again from the map after a rebase.

### Publishing

The site is published by a release or by hand, never by a merge.  Between phases 3 and 11, the README on `main` links to pages that the published site does not have until the site is published again.  So the site is published by hand, `gh workflow run docs.yml --ref main`, after each of phases 3 to 11 merges, and once more at the close.  The owner starts each run.  Between phases 3 and 10 the published site is true but uneven: old pages beside new, and Get started holding a few moved fragments in place of its path.  `docs/publishing.md` says today that a run by hand is not the way to keep the site current between releases; phase 3 adds the exception for this epic, and the close takes it out.

## Left to the builder

- The names of test methods, and how each test is written.
- Whether the checker is a shell script or a C# file run with `dotnet run`.
- The grain of the inventory where a table or a list is better kept as one unit.
- The words of each page.  The content map fixes the headings, the anchors and the sources; the phase's agent writes the text.
- The ranges of ids of the four diagnostics phases.  The content map gives a measured cut of about 11,000 words each; each phase recounts after phase 2 adds its lines.
- How `index.md` is laid out within docfx's modern template.

## My calls

The owner has not ruled on these.  Each is what I chose and why.

- **The middle group is named "Guides"**, not "Advanced topics": a page such as "Check the sidecars in CI" is a task and not advanced.
- **No redirect is kept for a page that moves.**  No stable release exists, so no link to the site is compiled into a release but the help links, which do not move.
- **The folders of the databases are `postgresql/` and `sqlserver/`**, the names of today's pages, not the dialect names.
- **Each database folder has five pages, and the settings file has a page of its own.**  The content map found both `types.md` pages and `reference/settings.md` over 3,500 words otherwise.
- **The term anchors of `concepts.md` are retired**, and their 28 links point at the section that now defines each term.
- **One limit lives on a guide**: a model that binds through Dapper only by name, on `guides/dapper.md`, since no reference page covers Dapper.
- **Content reviewers are Opus agents**, separate from the writer, since the owner gave the work to Opus.
- **The ledger and the checker are files of the epic, not tests**, so the close removes them cleanly.
- **No table of the 161 error titles is kept.**  The preamble's index goes with it, as the owner asked; `reference/errors.md` keeps the names of the areas with their ids.  With the index gone, an error is found by its title only with the browser's find, since the site's search has one entry for the whole page.
- **The headings of `diagnostics.md` stay bare ids.**  A heading that carried its title would make the right-hand list readable, at the cost of a second anchor for each section and a change to the test.  The reader arrives by a help link, so the list matters little.
- **The sentence beside `--prerelease`** says that the packages are previews.  That is a fact about the packages, not a statement that the library is unfinished.
- **A page keeps today's sample code** in a move and in a rewrite, unless the phase lists the change, since tests mirror some samples.
- **The example projects do not grow in this epic.**  A new page takes its sample from an example or a test where one has it, and otherwise the accuracy reviewer compiles it.  Root rule 4 asks for an example when a feature is delivered, and this epic delivers none.
- **27 pull requests.**  The reviewer of this outline found text that no phase wrote, the new guides, and pages that may need a split after the rewrite; each is a phase of its own.
- **The baseline is commit `28e20e3c`**, the head of `main` when the content map was made.
