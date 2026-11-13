# Docs overhaul, phase 08: move-guides

The outline is [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md), and the content map is [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).  This brief names their rows and does not copy them.
Done by: an Opus agent.  Needs: phases 0 to 7.  Runs alone.

## Goal

This is the last move.  After it:

- `site/recipes.md` is gone, and each of its remaining recipes stands, word for word, on the guide or the `start/` page that the map names.
- Each of the 17 terms of `site/concepts.md` stands, word for word, in the reference section that defines it, and the page itself is parked at `site/start/concepts.md` until phase 10.
- The README's two scenario sections, its FAQ, "NodaTime and other libraries" and "Custom type mapping" stand on the site, and each README heading keeps one sentence that points there.
- The root of `site/` holds `index.md` and `diagnostics.md` and no other page, and the list of pages not yet moved in `SiteStructureTests` is empty.
- Every anchor of `reference/`, and every guide anchor that holds text of today, exists.  No file outside `docs/superpowers` names `site/recipes.md`, or `site/concepts.md` at the root.

No word of today's text changes in this phase, but for two corrections (C-07 and C-08) and the words that the move makes wrong.

## Where things stand

- `S/`, `G/`, `R/` and `D/` are the map's short forms of `site/start/`, `site/guides/`, `site/reference/` and `site/reference/databases/`.
- **Line numbers in this brief are those of the baseline**, `28e20e3c`.  Phases 1 to 7 changed the lines of the working tree (phase 1 took out `README.md` 7, and every phase retargeted links).  Find a unit by its row of `inventory.tsv` and its text, and read the baseline with `git show 28e20e3c:<path>`.
- `site/recipes.md` holds what phase 5 left: baseline lines 1-23, 27-121 and 258-1030.  Its links to pages that moved already name the new pages.
- `site/concepts.md` is at the root, whole, and stands in `site/start/toc.yml` as `../concepts.md`.
- These guides exist: `G/troubleshooting.md` (phase 3, with the limits' rows), `G/describe-on-save.md`, `G/ci.md`, `G/report-a-bug.md` (phase 5), `G/dapper.md` with `limits` and `limit-parameter-names` alone (phase 6), `G/folders-and-files.md` with `configure-a-folder` alone (phase 7).  `S/keep-up-to-date.md` exists with one unit.
- The twelve reference pages that take a term exist, each with the heading and anchor the term goes under.
- `README.md` holds every section of the baseline but the status paragraph, with its links retargeted by phases 3 to 7.

## In scope / Not in scope

In scope: the rows of the map's section 10 under "Phase 8, `move-guides`"; in section 4 the maps of `site/recipes.md`, `site/concepts.md` and `README.md` (lines 9, 19-29 and 132-288); in section 5 the pairs MP-10 to MP-23 as far as this phase places a text; section 6.3 for the term links; section 8 for the twins.

Not in scope:

- A change of a word of today's text, the fixed shape of a guide, or the merge of a pair.  Phase 12 does those for the guides, phases 13 to 19 for the reference pages, and phase 10 for `start/`.
- A guide section that has no text today (phase 9), and the pages `G/nullability.md` and `G/output-modes.md` (phase 9).
- The cut of the README (phase 11), and the README bullet of root `AGENTS.md` rule 4 (phase 11).
- The word "recipe" inside moved text, which names a section and not a page, and the names of the test classes `DapperRecipeTests` and `EditorRecipeTests`.

## The work

### 1. Make the pages and place the recipes and the README sections

Each page gets an h1 that is its sidebar title in the map's section 1, and an entry in its `toc.yml` under the group that section gives.  A heading of today keeps its words and its anchor.  A heading marked "new" takes the words that the map's section 3 gives the page's h2, with an `<a id>` line above it.  Under one heading the texts stand in the order shown, untouched.  The README's own headings are not carried: the README keeps them.

| Page | Made | Heading and anchor, in page order | Holds, in this order |
|----|----|----|----|
| `G/several-databases.md` | New, group Projects | `use-several-databases` | `README.md` 216; `recipes.md` 95-121 (MP-14) |
| | | `folder-with-another-dialect` | `recipes.md` 29-65 |
| `G/folders-and-files.md` | Exists | `leave-migration-scripts-out`, after `configure-a-folder` | `README.md` 220; `recipes.md` 71-89 (MP-15) |
| `G/many-rows.md` | New, Running queries | `insert-many-rows` | `recipes.md` 306-325 |
| `G/sync-and-streaming.md` | New, Running queries | `run-without-awaiting` | `recipes.md` 331-369 |
| `G/repository-connection.md` | New, Running queries | `connection-from-a-provider`; `sql-server-repository` | `recipes.md` 375-412; 418-439 |
| `G/naming-and-placement.md` | New, Running queries | `model-namespace-task` (new) | `README.md` 212 |
| `G/custom-types.md` | New, Types | `type-override` (new) | `README.md` 254-268 (MP-21) |
| | | `one-column` (new) | `README.md` 270-288 |
| | | `strongly-typed-id` | `recipes.md` 445-493 |
| `G/json.md` | New, Types | `turn-json-on` (new) | `README.md` 248 (MP-20) |
| | | `class-in-a-json-column` | `recipes.md` 499-587 |
| `G/nodatime-and-spatial.md` | New, Types | `nodatime-postgresql`, `nodatime-sql-server` (new) | `README.md` 240; 242 |
| | | `nts-postgresql`, `nts-sql-server` (new) | `README.md` 244, sentence 1; sentences 2, 3 and 4, in today's order |
| | | `ef-core-and-dapper` (new) | `README.md` 246; then `README.md` 250 as the last line of the page, its one place |
| `G/dapper.md` | Exists | `models` (new) | `README.md` 134-161 and 165 (MP-22) |
| | | `dapper-and-adonet` | `recipes.md` 593-639 |
| | | `models-through-dapper` | `README.md` 163; `recipes.md` 645-702 (MP-12) |
| | | `dapper-with-postgresql`, `dapper-with-sql-server`, each an h2 now | `recipes.md` 708-785; 791-920 |
| | | `sql-only` (new), above `limits` | `README.md` 169-180 and 183 |
| `G/dynamic-sql.md` | New, Queries | `table-name-at-run-time` | `README.md` 224; `recipes.md` 926-939 (MP-16) |
| `G/lists.md` | New, Queries | `list-after-in` (new); `filter-by-a-list` | `README.md` 228; `recipes.md` 945-961 (MP-17) |
| `G/several-projects.md` | New, Projects | `test-project-sees-internals` | `README.md` 232; `recipes.md` 967-990 (MP-18) |
| | | `share-the-exceptions` | `recipes.md` 996-1030 |
| `G/report-a-bug.md` | Exists | `report-a-bug` | `README.md` 236, above the text of `tool.md` 373-390 that phase 5 placed (MP-19) |
| `G/troubleshooting.md` | Exists | `invalidcastexception` (new), an h3 under `at-run-time` | `README.md` 197-208, whole |
| `G/other-databases.md` | New, Other ways to use your SQL | `dialect-and-output` (new) | `README.md` 181 |
| `S/transactions.md` | New | `queries-in-a-transaction` | `README.md` 193; `recipes.md` 260-300 (MP-13) |
| `S/testing.md` | New | None: the unit stands under the title | `README.md` 189 |

`recipes.md` 1-23 is navigation and goes: the 19 links are the Guides sidebar.

**Each unit is placed once.**  The line 244 of `README.md` is one paragraph of four sentences: the first is about PostgreSQL, the second about SQL Server, the third starts "On both", and the fourth is "See" with a link for each database.  Sentences 3 and 4 stand once, after sentence 2.  Line 250 stands once, on `G/nodatime-and-spatial.md`.  Line 183 stands once, at the foot of `G/dapper.md#sql-only`.  Phase 12 restates a fact where a second section needs it.

**Words that the move makes wrong.**  A recipe or a term can say "this page", "above" or "below" of text that is now on another page, and a link's words can name "the recipes page" or "the concepts page".  Find them with `grep -n -i -E "this page|above|below|recipes page|concepts page" site/guides/*.md site/start/*.md` and in the sections of step 3.  Change the fewest words that make each phrase true, and give the unit's row the note `position`.  Change no other word.  The words "step 5" and "steps 1 to 3" are not such words: see the traps.

A link in text that came from the README is absolute today.  On the site it becomes relative, as `site/AGENTS.md` rules, but for a link to a file outside `site/`, which stays `https://github.com/mbcrawfo/SqlSource/blob/main/<path>`.  The links whose target is not the old anchor on its new page are these:

| Link in | Today | Becomes |
|----|----|----|
| `recipes.md` 410 | `generator.md#known-issues`; since phase 4, the limit | `D/postgresql/query-methods.md#limit-connection-string-repository`, re-based |
| `recipes.md` 702 | `generator.md#known-issues`; since phase 6, `guides/dapper.md#limit-parameter-names` | `#limit-parameter-names`, on the same page |
| `README.md` 208 | `generator.html#known-issues`; since phase 6, `reference/query-methods.html#limit-wrapped-connection` | `R/query-methods.md#limit-wrapped-connection`, as a relative link |
| `README.md` 180 | `README.md#only-want-to-generate-models` | `#models`, on the same page |
| `README.md` 268 | The README's FAQ heading | `G/troubleshooting.md#invalidcastexception` |
| `recipes.md` 367 | `#connection-from-a-provider` | `G/repository-connection.md#connection-from-a-provider` |

### 2. Place the README's lines 9 and 19 to 29 on reference pages

The README keeps its own copy of these lines until phase 11.  Each copy on the site stands in the order of its pair's row in the map's section 5.

| Unit | Lands under | Note |
|----|----|----|
| Line 9, sentence 2 | `D/index.md#supported-versions` | MP-23.  A whole sentence, with its link to `#database-support` |
| Lines 19-24 and 27 | `R/cli.md#install-the-tool`, below the text of `tool.md` 18-29 | MP-11.  The link "step 5" of line 27 becomes `describing.md#describe` (see the calls) |
| Line 26 | `R/attributes.md#what-the-package-adds` | This phase makes the h2 and the anchor, above `limits`.  Its last sentence is corrected: C-07, below |
| Line 29, sentences 1 and 2 | `R/msbuild.md#build-requirements` | This phase makes the h2 and the anchor, after `properties` |
| Line 29, sentences 3 and 4 | `D/index.md#supported-versions` | MP-23.  Each sentence whole (see the calls) |

Line 9, sentences 1 and 3 to 5, and line 29, sentence 5, are `restated-from` rows with no location in this phase.  They stay in the README and belong to phases 10 and 11.

**Correction C-07**, in the last sentence of line 26.  "Only the code it generates, and the attributes you mark your types and converters with, their enums and the exception its methods throw, are compiled into your project." becomes "Only the code it generates, the attributes you mark your types and converters with, and the exceptions its methods throw, are compiled into your project."  The package adds no enum, and three exceptions: `src/SqlSource/Generation/AttributeSource.cs` and `src/SqlSource/Generation/Exceptions/ExceptionSource.cs`.  Make the same change in the README's own line 26, which stays until phase 11.

**The sentence of `D/other.md`.**  This phase makes `G/other-databases.md`, so it writes the sentence that phase 3 left out: one new sentence under the h1 of `D/other.md`, after its opening sentence, that sends the reader to `../../guides/other-databases.md` for the steps.  It is new text, in plain language, and no other word of that page changes.

### 3. Place the terms (MP-10)

Each term's paragraphs go word for word into the section below, directly under its heading and above the section's own first paragraph.  The `#### Term` heading and its `<a id>` are not carried: no term anchor goes to a reference page.

**Correction C-08**, in the term "Query" (line 29).  "Each query becomes one member of the generated code, named after the query." becomes "Each query becomes one or more members of the generated code, named after the query."  A query has two methods under `method-type=both`, and a collection method beside them: `settings.md` 223 and `src/SqlSource/Settings/MethodType.cs`.  Make the change in both copies, on `R/sql-files.md` and on the parked page.

| Term, baseline lines | Section |
|----|----|
| Query 29; Marker 35; File header 41 | `R/sql-files.md#queries-and-names`; `#marker`; `#the-file-header` |
| Sidecar 47 and 49 | `R/sidecars.md#sidecar-files` |
| Settings file 55 | `R/settings-file.md#settings-files` |
| Output 63; Dialect 111; Logical database 117, then the paragraph of 119 | `R/settings.md#setting-output`; `#setting-dialect`; `#setting-database` |
| Query method 69 | `R/query-methods.md#query-methods` |
| Repository 75 | `R/repositories.md#repository-methods` |
| Token method 81; Generator 127 | `R/members.md#token-methods`; `#generator` |
| Query parameter 91 | `R/parameters.md#query-parameters` |
| Generator parameter 97 | `R/generator-parameters.md#setting-generator-parameters` |
| The paragraph of 85 | `R/generator-parameters.md#three-kinds-of-parameter`, an h2 "Three kinds of parameter" that this phase makes at the foot of the page |
| Token 103 | `R/tokens.md#tokens` |
| Tool 133; Describe 139 | `R/cli.md#tool`; `R/describing.md#describe` |

- A link from one term to another takes the target that the map's section 6.3 gives the anchor.  The row that starts with `concepts.md#output` has the eight that only `concepts.md` links, and `#sidecar` and `#token` have rows of their own.
- The closing sentence of a term, "See ...", stays, and its link takes the new address of its anchor, which is often the section it now stands in.
- Four closing sentences go, as the map's section 6.4 says, since each points at a bare page that the term now stands on: those of lines 91, 103, 127 and 133.  Each is a row of the ledger.
- "Output" and "Dialect" are reworded for decisions 4 and 5 by phase 14, not here.

### 4. Park `concepts.md`

- `git mv site/concepts.md site/start/concepts.md`.  Its words, headings and 18 anchors do not change, but for the sentence of C-08.  Its links to other pages gain the path from `start/`.
- `site/start/toc.yml` names it `concepts.md`, in the place it had.  Add `transactions.md` and `testing.md` in the order of the map's section 1.
- Phase 10 removes the page.  Until then a term's text stands there and in its reference section.

### 5. Leave the pointers in the README

Take the text under each heading below out of `README.md`, and leave the heading and one pointer.  A pointer is one sentence with one absolute site URL: `See [<the sidebar title of the page>](https://mbcrawfo.github.io/SqlSource/<folder>/<page>.html#<anchor>).`  The README's link test follows each URL to a file and an anchor of `site/`.

| README heading | Pointer's URL, after `https://mbcrawfo.github.io/SqlSource/` |
|----|----|
| `## Only want to generate models?` | `guides/dapper.html#models` |
| `## Just want to keep your SQL out of code files?` | `guides/dapper.html#sql-only` |
| `### How do I mock a repository?` | `start/testing.html` |
| `### How do I run queries in a transaction?` | `start/transactions.html#queries-in-a-transaction` |
| `### I got an InvalidCastException; ...` | `guides/troubleshooting.html#invalidcastexception` |
| `### How do I change the namespace of the models?` | `guides/naming-and-placement.html#model-namespace-task` |
| `### My project uses several databases` | `guides/several-databases.html#use-several-databases` |
| `### How do I keep migration scripts out?` | `guides/folders-and-files.html#leave-migration-scripts-out` |
| `### How do I choose a table name at run time?` | `guides/dynamic-sql.html#table-name-at-run-time` |
| `### Can I pass a list to IN?` | `guides/lists.html#list-after-in` |
| `### My test project sees the internals of my main project` | `guides/several-projects.html#test-project-sees-internals` |
| `### How do I report a wrong model?` | `guides/report-a-bug.html#report-a-bug` |
| `## NodaTime and other libraries` | `guides/nodatime-and-spatial.html` |
| `## Custom type mapping` | `guides/custom-types.html#type-override` |

`## FAQ` keeps its heading and no line of its own.  That is 14 pointers under 15 headings.  The README's lines 9 and 107 link the two scenario headings, and the headings stay, so those links hold until phase 11.

### 6. Retarget every link to a recipe or a term

- **Recipes.**  A link to `recipes.md#<anchor>` takes the page of step 1 and keeps the anchor.  At the baseline they stand in `diagnostics.md` (2, in SQLSRC022 and SQLSRC038), in text that is now on `R/` and `D/` pages (from `generator.md` 7, `settings.md` 5, `parameters-and-tokens.md` 1, `postgresql.md` 1, `sqlserver.md` 2), and in the rows of `index.md` that came from `site/README.md`.  `git grep -n "recipes\.md" -- site` finds them all.  A link of `index.md` to the bare page `recipes.md` names `guides/lists.md` (see the calls).
- **The README's lines that stay.**  Line 103 becomes `start/transactions.html#queries-in-a-transaction`, and line 126 `guides/several-databases.html#use-several-databases`.
- **The 28 links to a term or to `#glossary`.**  The map's section 6.3 gives each term's target; `git grep -n "concepts\.md"` finds them.  At the baseline: `#query-parameter` 6, `#sidecar` 5 (two in the example READMEs), `#dialect` 3, `#logical-database` 3, `#glossary` 3, `#marker` 2, `#file-header` 2, `#token` 2, `#generator-parameter` 1, `#describe` 1.
- **`#glossary` and the bare page.**  The link of `index.md` to `concepts.md#glossary`, its two links to `concepts.md`, and `docs/architecture/README.md` 46 name `start/concepts.md` until phase 10 (see the calls).  The rule of `site/AGENTS.md` that names `concepts.md#glossary` takes the words of step 8.

**Interim links this phase retargets:**

| Link | Left by | Parked at | Becomes |
|----|----|----|----|
| `concepts.md` 91; 103 | Phase 3 | `reference/parameters.md`; `reference/tokens.md` | Both closing sentences go |
| `concepts.md` 133 | Phase 5 | `reference/cli.md#tool` | The closing sentence goes |
| `concepts.md` 127 | Phase 6 | `reference/members.md#generator` | The closing sentence goes |
| Each link of a reference page to a term or to a recipe | Phases 3 to 7 | `../concepts.md#<term>`, `../recipes.md#<anchor>` | The term's section, by the map's 6.3; the recipe's guide, with the same anchor |

**Interim links this phase leaves.**  The brief of phase 10 lists each one under "Interim links this phase retargets".

| Link | Now on | Parked at | The map's target | Phase |
|----|----|----|----|----|
| `site/README.md` 3, to `#glossary` | `index.md` | `start/concepts.md#glossary` | `S/how-it-works.md#words` | 10 |
| `site/README.md` 9 and 109, to the bare page | `index.md` | `start/concepts.md` | `S/how-it-works.md` | 10 |
| `docs/architecture/README.md` 46 | The same file | `site/start/concepts.md#glossary` | `site/start/how-it-works.md#words` | 10 |
| The link "step 5" of `README.md` 27 | `reference/cli.md#install-the-tool` | `describing.md#describe` | `S/first-query.md#describe` | 10 |
| `site/README.md` 118, to the bare page `recipes.md` | `index.md` | `guides/lists.md` | None: the landing page is written new | 10 |

### 7. Change the twins and the other references

The samples do not change, so no twin's code changes.  Only these words do.

| Where | Today | New words |
|----|----|----|
| `examples/AGENTS.md` 7 | `[the recipe](../site/recipes.md#models-through-dapper)` | `[the guide](../site/guides/dapper.md#models-through-dapper)` |
| `examples/PostgresExamples/DapperSetup.cs` 6 and 10-11; `examples/SqlServerExamples/DapperSetup.cs` 6 and 8 | "the manual's recipe for Dapper"; `See site/recipes.md, "Run the models through Dapper".` | "the manual's guide to Dapper"; `See site/guides/dapper.md#models-through-dapper.` |
| `examples/SqlServerExamples/DateOnlyHandler.cs` 8-9; `TypedParameter.cs` 9-10 | `See site/recipes.md, "With SQL Server".` | `See site/guides/dapper.md#dapper-with-sql-server.` |
| `examples/PostgresExamples/README.md` 17; `examples/SqlServerExamples/README.md` 16 | `[the recipe](../../site/recipes.md#dapper-with-postgresql)`, and `-sql-server` | `[the guide](../../site/guides/dapper.md#dapper-with-postgresql)`, and `-sql-server` |
| The same READMEs, 133 and 135 | `[the recipe](../../site/recipes.md#queries-in-a-transaction)` | `[running queries in a transaction](../../site/start/transactions.md#queries-in-a-transaction)` |
| The same READMEs, 88 and 93 | `../../site/concepts.md#sidecar` | `../../site/reference/sidecars.md#sidecar-files` |
| `tests/DatabaseTests/Postgres/EndToEnd/DapperRecipeTests.cs` 18-20 | "the Dapper recipe of site/recipes.md (#dapper-with-postgresql)", "the recipe" | "the Dapper guide, site/guides/dapper.md#dapper-with-postgresql,", "the guide" |
| `tests/DatabaseTests/Postgres/EndToEnd/Types/EmailHandler.cs` 6; `Types/TypedParameter.cs` 8 | "the sample of site/recipes.md (#dapper-with-postgresql)" | "the sample of site/guides/dapper.md#dapper-with-postgresql" |
| `tests/DatabaseTests/SqlServer/EndToEnd/DapperRecipeTests.cs` 17-19; `Types/DateOnlyHandler.cs` 7; `Types/TimeOnlyHandler.cs` 7; `Types/TypedParameter.cs` 8 | The same, with `#dapper-with-sql-server` | The same, with `site/guides/dapper.md#dapper-with-sql-server` |
| `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md` 42, 46, 47; `tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md` 47, 51, 52 | The heading "`all_types` and the recipe"; `site/recipes.md#dapper-with-...`; "the recipe's remedy", "that recipe", "that part of the recipe" | "`all_types` and the Dapper guide"; `site/guides/dapper.md#dapper-with-...`; "the guide's remedy", "that guide", "that part of the guide" |
| `EndToEndSources.cs` of both `EndToEnd` folders (8 and 10; 12); `tests/DatabaseTests/Postgres/README.md` 140 and 165; `tests/DatabaseTests/SqlServer/README.md` 164 and 189 | "the manual's recipe", "the manual's Dapper recipe" | "the manual's Dapper guide", with `site/guides/dapper.md#models-through-dapper` named once in each file |
| `tests/DatabaseTests/QueryMethodAllocation/Postgres/AllocationTests.cs` 18 | "the manual's recipe for your own code" | "the manual's guide for your own code, site/guides/dapper.md#dapper-and-adonet," |
| `tests/SqlSource.Tests/Generator/ExceptionReuseTests.cs` 547 | "The shared project of the manual's recipe" | "The shared project of site/guides/several-projects.md#share-the-exceptions" |

Keep each comment inside the line width of its file.  The references to the VS Code recipe (`EditorRecipeTests`, `src/SqlSource.Tool/Watching/AGENTS.md`, `tests/README.md`, `docs/tech-debt/TD-0097-...`) were phase 5's.

### 8. Tests and `AGENTS.md`

- **`SiteStructureTests`.**  Take `recipes.md` and `concepts.md` out of its list of pages not yet moved.  The list is then empty: remove the list, so that the test allows `index.md` and `diagnostics.md` at the root and nothing else.
- **`SiteTocTests`.**  `guides/toc.yml` loses its entry for `recipes.md` and gains the twelve new guides, each in the group of step 1 and in the order of the map's section 1.  The group "Other ways to use your SQL" then holds `dapper.md` and `other-databases.md`.
- **`AGENTS.md`.**  Run `git grep -n -i -E "recipe|concepts\.md|glossary|FAQ|Only want|Just want" -- '*AGENTS.md'` and give each hit its final words.  The ones known:

| Rule | Final words |
|----|----|
| `site/AGENTS.md`, "Pages not yet moved" | The bullet goes, with its clause on a recipe.  `site/guides/AGENTS.md` holds the rule since phase 3: a guide owns a sample and a procedure |
| `site/reference/AGENTS.md`, the bullet on a new page | Its second sentence, "A page not yet moved is listed there as `../<page>.md` ...", goes |
| `site/start/AGENTS.md`, the second bullet | "`toc.yml` lists `../concepts.md`" becomes "`toc.yml` lists `concepts.md`".  Phase 10 removes the bullet |
| `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`, "`SiteStructureTests.PagesNotYetMoved` only shrinks" | The bullet goes with the list |
| `site/AGENTS.md`, the Terms bullet | "**Terms.**  One term for one thing.  Say *query parameter*, *generator parameter* or *logical database* where "parameter" or "database" alone is unclear, *inside a query*, and *folder*, never "directory" in prose.  A term is defined once, in the reference section that documents the thing." |
| `site/AGENTS.md`, the table "Where a new thing goes", row "Any other feature" | "A reference section; a guide when it is a task; an example, as root rule 4 says" |
| `site/AGENTS.md`, the README bullet | True from this phase to phase 11, which gives it its last words: "**`README.md`**, beyond root rule 4: under "Only want to generate models?", "Just want to keep your SQL out of code files?", each FAQ heading, "NodaTime and other libraries" and "Custom type mapping" stands one sentence that links the page of the site that holds the text.  Add no text there." |
| The six rows of step 7 in the two `EndToEnd/AGENTS.md` and `examples/AGENTS.md` | As step 7 |

Check each file's size after the edit: 8 KB, and 80 words in a bullet.  `tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md` is 7,891 bytes at the baseline, and the words of step 7 add about 20.

### 9. Remove the old page and check

`git rm site/recipes.md`, then run the checks of "Done when".

## The ledger

`ledger/phase-08.tsv` holds a row for:

- Each unit of `recipes.md` 1-23, 27-121 and 258-1030, with the disposition of its row in the map.
- Each unit of `concepts.md` 27-139, and the two paragraphs of lines 85 and 119.  A `#### Term` heading and a group heading are units too: each is `move-split`, heading only, and stays on the parked page until phase 10.  The heading of line 83 is `move`.  The units of lines 3 to 17 are phase 10's.
- Each unit of `README.md` 132-288.  A `restated-from` row that has a location (lines 254, 268 and 288) moves there word for word, and phase 12 cuts it.  A heading of the README is a unit: its row is `move`, to `README.md`, with the note "phase 11".
- The parts of `README.md` 9 and 19-29 of step 2.  Where the checker asks for every part of a unit in one file, list the other parts with the map's disposition and the note "phase 10" or "phase 11".
- Each piece of new text, as `new`: 14 page titles, the new headings of steps 1 to 3, the 14 pointers, and the sentence of `D/other.md`.
- `README.md` 244 is `move-split`, with one part for sentence 1 and one for sentences 2 to 4.
- `corrected`: the last sentence of `README.md` 26 (C-07) and the third sentence of `concepts.md` 29 (C-08), each with both texts.
- `move`, with the note `position` and the old and the new words: each unit whose words step 1 mended.  The units that hold "step 5" or "steps 1 to 3" are `move`, with a note that names the phase which makes the words true.

Open or extend `merges/MP-<nn>.md` for MP-10 to MP-23 with each text this phase places and its new address.  The merged text is the rewrite's.

"check move 08" must leave over only these, each with its row: `recipes.md` 1, 3 and 5-23 (the title and the navigation); the four closing sentences of step 3; `README.md` 26 and `concepts.md` 29, which are corrected; `README.md` 244, which is cut; and each unit with the note `position`.

## Done when

- [ ] `ls site/*.md` prints `site/diagnostics.md` and `site/index.md`, and `ls site/start` shows `concepts.md`, `testing.md` and `transactions.md`.
- [ ] `git grep -n -E "recipes\.(md|html)" -- . ':!docs/superpowers'` finds nothing.
- [ ] `git grep -n -E "concepts\.(md|html)" -- . ':!docs/superpowers'` finds only `site/start/toc.yml`, `site/index.md` and `docs/architecture/README.md`, and each hit names `start/concepts.md`.
- [ ] `git diff -M 28e20e3c HEAD -- site/concepts.md site/start/concepts.md` shows a rename, and each changed line differs in a link target and in nothing else, but for line 29, which holds C-08.
- [ ] `git grep -n -E "their enums|becomes one member" -- README.md site` prints nothing, and `grep -c "guides/other-databases.md" site/reference/databases/other.md` prints 1.
- [ ] `sed -n '/^## Only want/,/^## Where to next/p' README.md | grep -c "https://mbcrawfo.github.io/SqlSource/"` prints 14, and the same range holds no fenced block.
- [ ] "check coverage" passes, and "check move 08" prints only the leftovers that "The ledger" lists.
- [ ] `git grep -n "#known-issues" -- site README.md` finds nothing.
- [ ] `dotnet test tests/SqlSource.Repository.Tests` passes: `InboundSiteReferenceTests`, `UserDocsLinksTests`, `SiteTocTests`, `SiteStructureTests`, `UserDocsSamplesTests`, `AgentsFileTests`.
- [ ] `tools/build-site.sh` passes with no warning.
- [ ] The content reviews, `branch-review:review` and `./pre-commit-validation.sh --full` pass, in the outline's order, on the last commit.

## For the reviewers

The no-loss reviewer, beyond the general kit:

- Reads `git show 28e20e3c:README.md` lines 132 to 288 against the site, section by section, since the checker cannot say that a paragraph stands under the right heading.
- Reads each leftover of "check move 08", and confirms that each of the 19 links of `recipes.md` 5-23 has a guide in a sidebar.
- Confirms that the four closing sentences of step 3 point only at the page the term stands on, and that the other 13 are kept.
- Confirms that sentences 3 and 4 of `README.md` 29 stand whole on `D/index.md`, and that the README still holds lines 9 and 19 to 29.
- Compares the parked page with the baseline.

The accuracy reviewer has little new text.  It checks:

- Each of the 14 pointers: the URL opens the section that holds the text that left the README.
- C-07 against `AttributeSource.cs` and `Exceptions/ExceptionSource.cs`: three attributes, no enum, three exceptions.  C-08 against `MethodType.cs` and the row of `method-type` on `R/generator-parameters.md`.
- The sentence of `D/other.md`: it promises no more than `G/other-databases.md` holds when phase 9 has written it, the steps for the output `sql` on another database.
- Each of the 28 retargeted links, read in its sentence: the section it opens defines the word that is linked.
- Each comment of step 7: the anchor it names holds the sample that the file mirrors, compared line by line with the twin.
- The final words of step 8 against the outline's "`AGENTS.md` files".

## Traps

- **A heading is a unit, and these are not carried.**  The FAQ's questions stay in the README, and the `#### Term` headings stay on the parked page.  Each has its row, as "The ledger" says.
- **The terms stand twice until phase 10**, in the reference section and on the parked page.  If "check move 08" reports a unit found twice, the reference section is the location of the ledger.
- **`recipes.md` 700 and 988 are links inside the page**, and both targets land on the page of the link.  Line 367 does not: its target is on another guide.
- **The moved text of `G/dapper.md` says "as in step 5", "the `sqlsource.json` of step 3" and "Follow steps 1 to 3".**  The steps are the README's.  The words stay, and phase 9 numbers its walkthrough so that they are true (see the calls).  The link "step 5" on `R/cli.md` stays too: phase 10 points it at step 5 of `S/first-query.md`.
- **`With PostgreSQL` and `With SQL Server` become h2**, so the bullets and fences under them keep their indent and nothing else changes.
- **`UserDocsSamplesTests` parses each `sql` block where it lands.**  Each block keeps its info string.  `recipes.md` 595 has a bare `sql` block: keep it as it is.
- **A link to `S/first-query.md`, `S/how-it-works.md` or `S/install.md` is dead until phase 10.**  The map names them as last targets; this phase uses the targets of the calls.
- **`G/other-databases.md` is one bullet under a title** until phase 9.  No pointer of the README links it in this phase.  One page does: `D/other.md`, by the sentence of step 2.
- **`start/AGENTS.md` asks each page to end with "Next:".**  The two new `start/` pages hold one unit each and no more; phase 10 writes them.
- **The README on `main` links the new pages at once.**  The outline's "Publishing" covers it: say in the pull request that the site must be published after the merge.

## Left to the agent

- The order of the commits, and whether the README's pointers land with the pages or after them.
- The words of a new heading where the map's h2 name is long, as long as the anchor is the map's.
- Where in a comment of step 7 the path stands, and how a line is wrapped.
- How `SiteStructureTests` reads once its list is gone.

## Calls made in this brief

- **`#glossary` and the bare links to `concepts.md` name the parked page.**  The map's section 6.3 sends them to `S/how-it-works.md#words`, which phase 10 makes.  Phase 10 gives them that target.
- **A bare link of `index.md` to `recipes.md` names `guides/lists.md`**, the first page of the Guides sidebar, as the README's outline does for "Guides".  If phases 4 to 7 used another rule for a bare link to a page that split, follow theirs.  Phase 10 writes the landing page and takes the link out.
- **The "step 5" link of `README.md` 27, on `R/cli.md`, names `describing.md#describe`.**  The map names `S/first-query.md#describe`, which phase 10 makes, and phase 10 retargets the link.  Phase 18 merges the pair and settles the words.
- **A part smaller than a sentence is carried with its sentence.**  Sentences 3 and 4 of `README.md` 29 go whole to `D/index.md#supported-versions`, with the half about the tool's runtime and the clause about EF Core.  Phase 19 cuts them when it merges MP-23.
- **Nothing is placed under `D/index.md#azure-sql`.**  No sentence of the README is about Azure SQL alone.
- **A term stands first in its section**, the order of MP-10's row, and its `#### ` heading is not carried, since no term anchor goes to a reference page.
- **Four closing sentences of terms go and thirteen stay.**  The map's section 6.4 drops the four links; a link that keeps an anchor is retargeted by the general rule.
- **A pointer has one URL.**  "Just want to keep your SQL out of code files?" points at `guides/dapper.html#sql-only`, where its sample is, and not at `guides/other-databases.html`, which is one bullet until phase 9.  "NodaTime and other libraries" points at one page, and the JSON paragraph is reached by the sidebar.
- **`README.md` 183, 250 and the third and fourth sentences of 244 are each placed once**: 183 at the foot of `G/dapper.md#sql-only`, 250 at the foot of `G/nodatime-and-spatial.md`, and the two sentences of 244 after its second, under `nts-sql-server`.  A move places a unit in one place.  Phase 9 writes the links of `G/other-databases.md`, and phase 12 restates what `G/json.md` and `nts-postgresql` need.
- **Two sentences are corrected in this move**, C-07 and C-08, since a correction lands in the phase that carries its sentence.  Each is corrected in both of its copies, so that no page says the false thing.
- **The sentence of `D/other.md` that links `G/other-databases.md` is written here**, by the phase that makes the page it links.
- **A plain "the manual's recipe" in a test or an example becomes "guide" and gains the path**, so that `InboundSiteReferenceTests` holds it from now on.  The test classes keep their names.
- **`S/testing.md` has no h2**, since the map gives its unit no anchor.
- **`invalidcastexception` is an h3** under the h2 `at-run-time` that phase 3 made.
- **The list of `SiteStructureTests` is removed**, not left empty.
- **The README bullet of root rule 4 waits for phase 11**, as the outline's table says, and the README bullet of `site/AGENTS.md` gets words for the time between.
- **This phase opens the worksheets** of the pairs it places a text of.
- **The dangling "step" words on `G/dapper.md` stay for one phase, and the "step 5" of `R/cli.md` for two.**  They are not words that a move mends: the outline lists "this page", "above", "below" and a page named in words.  Phase 9 numbers its walkthrough so that the first are true, and phase 10 gives the link of the second its step.
