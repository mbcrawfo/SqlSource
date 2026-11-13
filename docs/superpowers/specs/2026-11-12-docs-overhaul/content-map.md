# Docs overhaul: the content map

This appendix says where every unit of today's user documentation goes, and in which phase.  It belongs to the epic spec of the documentation overhaul and is the input of the link rewrite and of each move phase.  The outline of the epic, [`../2026-11-12-docs-overhaul-epic-design.md`](../2026-11-12-docs-overhaul-epic-design.md), wins wherever the two differ.

**Check.**  A script was run over `heading-inventory.txt` against this file on 2026-11-12, and again on the same day after the map was brought into line with the revised outline (section 9 lists what changed).  Every h2 and h3 of the 13 current documents has a row of its own in the map of its document, at its line and with its text: 217 of 217 (121 h2 and 96 h3).  The 13 h1 and the 30 h4 headings have rows too.  The three lines of `tool.md` that the inventory lists and that are comments inside code blocks are left out.  Further checks, each run by a script:

- Each of the 481 rows of section 4 has one of the eight dispositions of the outline.
- Each of the 46 known-issue bullets has a row, and each of the 36 limits has its phase.
- Each of the 211 explicit `<a id>` anchors of today's pages is named on a page of section 3 (184) or retired in section 6 (27: the 18 of `concepts.md`, the 8 named `known-issues`, and `what-works-today`).
- Every anchor that the map names on a new page is declared in section 3: 876 references in the whole file, 769 of them in the rows of sections 4 to 10, to 292 anchors.
- The tree has 70 pages, and each has an entry in section 3.
- No row says that an anchor is kept only up to a merge, and none refers to a note kept for a rewrite.

The `diagnostics.md` preamble is mapped by sentence, not by heading, and is outside the count.

The counts above are those of the two runs.  The map was then brought into line with the briefs of the phases, and section 9 lists what changed: two corrections, one anchor (`gp-default`) and several rows.

How to read it:

- `S/` is `site/start/`, `G/` is `site/guides/`, `R/` is `site/reference/`, `D/` is `site/reference/databases/`.  `index.md` and `diagnostics.md` with no prefix are at the root of `site/`.
- Line numbers are those of the files at commit `28e20e3c`.  A range `33-58` is the lines 33 to 58.  A heading's range ends before the next heading of any level, so a parent's row does not include its children.
- Word counts are counted with `wc -w` rules over today's text, code blocks included.  The plain-language rewrite changes them, so every size below is an estimate.  Each rewrite phase recounts its pages and reports the count, and phase 25, `resize`, splits a page that is then over 3,500 words.  No test holds a size.
- A disposition is one of the eight of the outline's ledger: `move` (the text goes whole to the new place, word for word, but for the words that the move makes wrong, such as "this page", "above" or a page of today named in words, which the move mends in the fewest words and lists with the note `position`), `move-split` (the unit is cut and each part has its own row), `merge-pair <id>` (the text is placed whole beside its near-duplicate under one heading, and the rewrite phase of that page merges the two: section 5), `removed-status` (decision 3, the language of a library in development: phase 1 removes it), `removed-preamble` (decision 3, the preamble of `diagnostics.md`: phase 2 removes it), `restated-from <owner>` (the unit's facts are owned by the named section, so the unit is rewritten as a short restatement that links there, or goes when it only points), `corrected` (the text is wrong today, and the phase that carries it corrects it: section 5), `new` (text that does not exist today: section 3 says which phase writes it).
- Today's text is moved or rewritten, never both in one phase.  A `restated-from` unit that has a new location moves there word for word, and the rewrite phase of that page cuts it to the restatement.  One that has no location goes in the phase that moves its page.
- An explicit `<a id>` keeps its name unless a row says otherwise.  So `settings.md#setting-output` becomes `R/settings.md#setting-output`, and only the page part of a link changes.
- "Decision" with a number is one of the twelve under "Decisions made with the owner" in the outline.  "Phase" with a number is a row of the outline's table of phases.
- A unit moves in the phase that moves its page.  Section 10 gives the pages and line ranges of each phase.  A row or a column names a phase only where that rule gives no answer: a new page that gets its text in more than one phase, a limit, a correction, new text.

## 1. The tree

Top bar (`site/toc.yml`): **Get started**, **Guides**, **Reference**.  Each folder has one `toc.yml`, which is its sidebar.  The sidebar groups of `guides/` and `reference/` exist only in the `toc.yml`.  Sizes are words at today's density.

### Root

| File | Sidebar title | Group | Words |
|----|----|----|----|
| `index.md` | (no sidebar: the landing page) | none | 550 |
| `diagnostics.md` | Errors (SQLSRC) | Reference, group Errors | 43,400 |
| `toc.yml` | | | |

### `start/`: Get started (read in order)

| File | Sidebar title | Search words for the first sentence | Words |
|----|----|----|----|
| `install.md` | Install | NuGet, package, tool, .NET version | 700 |
| `first-query.md` | Your first query | tutorial, quick start, PostgreSQL, SQL Server | 1,500 |
| `how-it-works.md` | How SqlSource works | sidecar, describe, generator, source generator | 700 |
| `write-queries.md` | Write queries | parameter, optional filter, IN list, one row, no rows | 1,200 |
| `call-queries.md` | Call your queries | repository, dependency injection, cancellation token, model, DTO | 1,300 |
| `transactions.md` | Run queries in a transaction (or with a timeout) | transaction, timeout, commit, rollback | 800 |
| `testing.md` | Test code that uses a repository (mock or fake) | mock, fake, unit test, interface | 600 |
| `keep-up-to-date.md` | Keep your queries up to date | migration, schema change, git, merge conflict, CI | 900 |
| `errors.md` | When something goes wrong | build error, SQLSRC, exception | 800 |

Nine entries, no groups.

### `guides/`: Guides (one task for each page)

| Group | File | Sidebar title | Search words for the first sentence | Words |
|----|----|----|----|----|
| Queries | `lists.md` | Pass a list of values (IN) | IN clause, array, ANY, optional list | 1,100 |
| Queries | `nullability.md` | Fix a nullable property | nullable, not null, `?`, null reference warning | 1,000 |
| Queries | `dynamic-sql.md` | Choose a table or a clause at run time | dynamic SQL, table name, ORDER BY, SQL injection | 700 |
| Running queries | `many-rows.md` | Insert or update many rows in one call (bulk) | bulk insert, batch, many rows | 800 |
| Running queries | `sync-and-streaming.md` | Call a query without await, or stream rows | synchronous, sync, IAsyncEnumerable, large result | 1,000 |
| Running queries | `repository-connection.md` | Give a repository its connection | connection string, data source, connection factory, dependency injection | 1,000 |
| Running queries | `naming-and-placement.md` | Change names, namespaces and visibility | Async suffix, Dto, record or class, internal, `List<T>` | 1,100 |
| Types | `custom-types.md` | Use your own types: ids, enums, value objects | enum, strongly typed id, Vogen, converter, InvalidCastException | 1,400 |
| Types | `json.md` | Read and write JSON columns | json, jsonb, System.Text.Json, JsonElement | 1,100 |
| Types | `nodatime-and-spatial.md` | Use NodaTime and NetTopologySuite | Instant, LocalDate, PostGIS, geometry, geography | 900 |
| Projects | `several-databases.md` | Use several databases | second database, two connection strings | 900 |
| Projects | `folders-and-files.md` | Give a folder its own settings, or leave files out | migration scripts, folder, one class for a folder | 900 |
| Projects | `several-projects.md` | Use SqlSource in several projects | InternalsVisibleTo, test project, catch exception, class library | 800 |
| Workflow | `describe-on-save.md` | Describe queries as you save (watch) | watch, VS Code task, Rider, Visual Studio | 1,000 |
| Workflow | `ci.md` | Check the sidecars in CI | GitHub Actions, build server, pipeline | 800 |
| Workflow | `report-a-bug.md` | Report a wrong model | bug report, wrong type, wrong nullability | 1,300 |
| Workflow | `troubleshooting.md` | Troubleshooting: it built, but something is wrong | exception at run time, wrong result, nothing happens | 1,900 |
| Other ways to use your SQL | `output-modes.md` | Choose what is generated (output modes) | output, repository, methods, models, sql | 800 |
| Other ways to use your SQL | `dapper.md` | Use Dapper or your own ADO.NET code | Dapper, ADO.NET, models only, SQL constants | 2,300 |
| Other ways to use your SQL | `other-databases.md` | Use MySQL, SQLite, Oracle or another database | MySQL, MariaDB, SQLite, Oracle, CockroachDB, DuckDB | 700 |

Twenty entries in six groups of three or four.

### `reference/`: Reference (look-up, one page for each thing)

| Group | File | Sidebar title | Words |
|----|----|----|----|
| .sql files | `sql-files.md` | .sql files and markers | 2,500 |
| .sql files | `parameters.md` | Query parameters (`@name`, `-- param:`, IN lists) | 3,350 |
| .sql files | `tokens.md` | Tokens (`{{name}}`) | 1,450 |
| .sql files | `dialects.md` | Dialects | 1,450 |
| Settings | `settings.md` | Settings: every setting, and where you write it | 2,600 |
| Settings | `settings-file.md` | sqlsource.json | 1,500 |
| Settings | `generator-parameters.md` | Generator parameters | 1,500 |
| Settings | `attributes.md` | Attributes | 1,600 |
| Settings | `msbuild.md` | MSBuild properties and the build | 3,200 |
| Generated code | `members.md` | What is generated and where it goes | 3,850 |
| Generated code | `models.md` | Models | 3,350 |
| Generated code | `model-properties.md` | Model properties: names, column types, nullable | 2,700 |
| Generated code | `query-methods.md` | Query methods | 3,000 |
| Generated code | `return-types.md` | What a method returns (arrays, lists, streaming) | 1,750 |
| Generated code | `repositories.md` | Repositories | 3,150 |
| Generated code | `connection-methods.md` | Methods that take a connection (transaction, timeout) | 1,250 |
| Generated code | `collection-methods.md` | Collection methods (many items in one call) | 1,800 |
| Generated code | `exceptions.md` | Exceptions | 1,950 |
| Types | `type-mapping.md` | How a C# type is chosen, and type overrides | 3,400 |
| Types | `library-types-and-json.md` | Library types and JSON | 2,500 |
| Types | `converters.md` | Converters | 2,050 |
| The sqlsource tool | `cli.md` | The sqlsource command | 2,600 |
| The sqlsource tool | `describing.md` | What describe does (watch, check, force) | 2,450 |
| The sqlsource tool | `connections.md` | Connections and the .env file | 1,900 |
| The sqlsource tool | `sidecars.md` | Sidecar files | 1,750 |
| The sqlsource tool | `bundles.md` | Bug-report bundles (diagnose, replay) | 1,650 |
| Supported databases | `databases/index.md` | Supported databases and versions | 700 |
| Supported databases, PostgreSQL | `databases/postgresql/index.md` | Connect and describe | 1,900 |
| Supported databases, PostgreSQL | `databases/postgresql/nullability.md` | Which columns can be null | 2,450 |
| Supported databases, PostgreSQL | `databases/postgresql/types.md` | Types | 1,900 |
| Supported databases, PostgreSQL | `databases/postgresql/libraries.md` | NodaTime, NetTopologySuite and JSON | 2,000 |
| Supported databases, PostgreSQL | `databases/postgresql/query-methods.md` | Running queries | 2,800 |
| Supported databases, SQL Server | `databases/sqlserver/index.md` | Connect and describe | 2,100 |
| Supported databases, SQL Server | `databases/sqlserver/nullability.md` | Which columns can be null | 1,700 |
| Supported databases, SQL Server | `databases/sqlserver/types.md` | Types | 1,150 |
| Supported databases, SQL Server | `databases/sqlserver/libraries.md` | NodaTime, NetTopologySuite and JSON | 2,400 |
| Supported databases, SQL Server | `databases/sqlserver/query-methods.md` | Running queries | 4,450 |
| Supported databases | `databases/other.md` | Other databases | 600 |
| Errors | `errors.md` | How errors are reported | 2,000 |
| Errors | `../diagnostics.md` | Errors (SQLSRC) | (root) |

Group sizes: .sql files 4, Settings 5, Generated code 9, Types 3, The sqlsource tool 5, Supported databases 4 (the overview, two nested database groups of 5 each, and Other databases), Errors 2.  No group has more than nine entries.

Page count: 2 at the root, 9 in `start/`, 20 in `guides/`, 39 in `reference/` (12 of them under `databases/`).  70 pages replace 12.

### Pages over 3,500 words or under 600

| Page | Words | Why it is allowed, or how it splits |
|----|----|----|
| `diagnostics.md` | 43,400 | Decision 7, and the outline's constraint on help links: `diagnostics.html#sqlsrcNNN` is compiled into the packages, so it stays one page |
| `R/members.md` | 3,850 | It holds merge pair MP-01 (two sections named "Where the members go", about 500 words in common) and the overview of `generator.md`, which the rewrite of phase 16 shortens.  If phase 16 reports it over 3,500, phase 25, `resize`, splits the placement sections (`#locations`, `#placement-for-a-file`, `#setting-member-location`, `#setting-visibility`, `#setting-nested-class`) to `R/member-placement.md`, with their anchors, a `toc.yml` entry and the inbound links.  The group then holds a tenth entry, which the outline allows in that phase |
| `D/sqlserver/query-methods.md` | 4,450 | The one page of a database folder that is still over the limit with five file names.  "What a method reads and throws" is 1,395 words, and the page's four limits are about 820.  A rewrite adds no anchor, so the move makes the anchors and the rewrite uses them.  Phase 4, which moves the page, cuts "What a method reads and throws" with five new h3 headings and anchors (section 3), so that each limit links the part it belongs to.  The rewrite of this page, in phase 20, must do two things.  1. Cut each sentence that restates a rule owned by `R/query-methods.md`, `R/return-types.md`, `R/collection-methods.md`, `R/parameters.md#list-parameters` or `R/exceptions.md#transaction-rolled-back`, and link the owner in its place, so that the page holds only what SQL Server decides.  2. Write each of the four limits as what you see, why, what to do.  The target is under 3,500 words.  No fact about SQL Server is cut to reach it, and no sixth file name is added: if the page is still over, phase 20 reports its count, phase 25 does not split it, and the page stays the one listed exception |
| `index.md` | 550 | A landing page routes and states nothing of its own |
| `S/testing.md` | 600 | It has a page of its own because "mock" and "test" are the reader's search words and no other title carries them |
| `D/other.md` | 600 | It is the one door of the database group for a reader whose database is not described, and it sends that reader to `G/other-databases.md` |

Pages close to the limit, whose count the rewrite phase of each reports, and which phase 25 splits if they pass it: `R/type-mapping.md` (3,400), `R/parameters.md` (3,350), `R/models.md` (3,350), `R/msbuild.md` (3,200), `R/repositories.md` (3,150).

## 2. Changes to the tree

Each change is to the tree the owner chose.  The three folders and the two root files are as given.  The first two rows were settled on the first draft of this map.  Both are calls of the outline ("My calls"), which the owner has not ruled on (section 9).

| Change | Reason |
|----|----|
| New `R/settings-file.md`, with the sidebar title "sqlsource.json", in the Settings group.  It takes "The settings file" (`#settings-files`) and its limit | A call of the outline.  `R/settings.md` with it would be 4,050 words, and a reader with the file in hand finds a page named for it.  "Where rules live" in the outline says the same: `R/settings.md` keeps the places table, the matrix, precedence, and the output, database and dialect settings |
| Every database folder has five file names, not four: `index.md`, `nullability.md`, `types.md`, `libraries.md`, `query-methods.md`.  `libraries.md` takes the NodaTime, NetTopologySuite and JSON sections | A call of the outline.  With four files both `types.md` were over 3,500 words (3,900 and 3,550).  A database with no library support has a short `libraries.md` that says so |
| `R/parameters-and-tokens.md` is two pages, `R/parameters.md` and `R/tokens.md` | Size: the one page would be 4,600 words.  The two halves answer two questions (a value, or a piece of SQL), and the comparison table that opens today's page moves to `R/tokens.md#token-or-parameter`, which `R/parameters.md` links in its first paragraph |
| New `R/model-properties.md` beside `R/models.md` | Size: `generator.md#models` with the four model settings is 4,770 words.  The cut is by the reader's question.  `R/models.md` answers "which models does a query get, and what are they called".  `R/model-properties.md` answers "why has this property this name, this type and this nullability, and how do I change one column", and so it also holds the column types setting (`#setting-column-types`) and the `-- column:` marker inside a query (`#column-marker`), which today are in two files |
| New `R/return-types.md` beside `R/query-methods.md` | Size: the one page would be 4,650 words.  `R/return-types.md` holds what each shape returns (`#method-shapes`), the collection type (`#setting-collection-type`) and streaming rows (`#streaming-rows`): one question, "what does the method give back" |
| New `R/describing.md` in the group "The sqlsource tool" | It is the home the owner asked for: "which queries are described" (`#which-queries`) lives here, with the rules of `--force`, `--watch` and `--check`.  `R/cli.md` with these sections would be 4,350 words.  `R/cli.md` keeps the command line itself: arguments, options, variables, exit codes, logs, the summary |
| Running in the build lives on `R/msbuild.md`, not in the tool's group | The two sections that describe it today (`tool.md#running-in-the-build`, `settings.md#online-build`) are merge pair MP-06 and must land on one page.  `SqlSourceOnline` and `SqlSourceEnvFile` change the build, so by the outline's rule that a setting is documented on the page of the thing it changes, their sections are on the page of the build.  `R/cli.md` and `R/describing.md` each link it in their opening.  Phase 5 creates the page, for `tool.md` 210-246, and phase 7 fills it |
| New `R/errors.md`, "How errors are reported", a page and not a section | Its facts belong to no one page: they cover the generator, the build and the tool together, so `R/cli.md` cannot own them.  It is about 2,000 words: the unique facts of the `diagnostics.md` preamble and the table of the 29 areas, each with its ids as links.  It is the link target of the one paragraph that stays above `## SQLSRC001`, and of `S/errors.md`.  The preamble goes in phase 2, when the folders do not exist yet, so that phase writes the page as `site/errors.md`, at the root of `site/`, and phase 3 moves it to `site/reference/errors.md` |
| The `## Models` section of each database page goes to that database's `index.md`, not to `types.md` or `query-methods.md` | Size, and fit: the section is a list of what the database decides in a model, each item a pointer to the nullability, types or query-methods page.  On `index.md` it works as the overview of the folder.  On `D/sqlserver/query-methods.md` it would add to the one page that is already over 3,500 |
| `G/dapper.md` holds a `## Limits` section | One limit (an input model binds through Dapper only by name) is about a library that no reference page covers.  The guide is the only page that owns the behaviour, and the only guide with the heading.  It is a call of the outline ("My calls") |
| `tool.md#install-the-tool` goes to `R/cli.md#install-the-tool`, and `S/install.md` restates it | A Get started page restates a rule and links its owner.  The section is also the `See:` target of a diagnostic, which must be a reference section |
| No glossary page (decision 9), so `concepts.md` has no successor page in `reference/` | Each of its 17 terms is defined in the reference section that documents the thing: merge pair MP-10 lists them |

The parity rule for the two database folders is at file-name level only: both folders have `index.md`, `nullability.md`, `types.md`, `libraries.md` and `query-methods.md`.  Nothing forces the same headings or anchors.  `D/postgresql/query-methods.md#array-parameters` and `D/sqlserver/query-methods.md#list-parameters` are different features and keep their names, as do `#type-map` and `#types`, and `#which-npgsql` and `#which-sqlclient`.

## 3. Per page

An anchor in code font is an explicit `<a id>` that exists today and must be kept.  "(new)" marks an anchor that does not exist today.  A heading may be reworded; its anchor may not.  A move does not change words, so the rewrite phase of a page rewords a heading of today, and the phase that makes a new heading words it.

**Written in.**  New text has a phase, as moved text has:

- Phase 2 writes `R/errors.md` and the one paragraph of `diagnostics.md`.
- A move phase, 3 to 8, writes the new text of a reference page: a heading, an anchor, an index table, a sentence that points.  It also writes a new heading or anchor of a guide when moved text stands under it, and each `## Limits` heading, each limit's h3 and each row of `G/troubleshooting.md` that its bullets need.
- Phase 9, `write-guides`, writes each guide page and each guide section that has no text today.
- Phase 10, `get-started`, writes the nine `start/` pages and the finished landing page.

New text is in plain language from the start and has no rewrite.  After phase 8 every anchor of `reference/`, and every guide anchor that holds text of today, exists, so a rewrite adds and removes none.  A "Written in" line or table below names the phase wherever these rules leave a choice.

### `index.md`, the landing page

Who arrives: a reader from nuget.org, the repository, or a search, who asks "what is this, and does it work with my database".  It has no sidebar.  It states no fact that another page does not own.  Full outline:

1. `# SqlSource`.  One paragraph: what SqlSource does and why SQL in a file of its own is better.  From `README.md` line 5.  No status sentence.
2. One sample pair: a five-line `.sql` file with one query, and at most three lines of C# that call its method.  The query is `GetUser` of `examples/PostgresExamples/Repository/UserRepository.sql`, lines 5, 6 and 12 to 14: the example's line 11, `-- generator: method-type=both`, is left out, so the method is asynchronous and is named `GetUser`.  No example file compiles that form, so the accuracy reviewer of phase 10 compiles it.  The README shows the same pair, byte for byte (section 7).
3. `## Does it work with my database?` (new anchor `databases`).  Four rows, each a link: PostgreSQL 14 or later, typed models and methods (`D/postgresql/index.md`).  SQL Server 2017 or later, typed models and methods (`D/sqlserver/index.md`).  Azure SQL, accepted and not tested (`D/index.md#azure-sql`).  Any other database, your SQL as constants (`G/other-databases.md`).  The two minimum versions are a tested restatement of `D/index.md#supported-versions` (section 5).
4. One button: **Start here: Install** (`S/install.md`).  Under it, one line for the two other readers: "You use Dapper: start with [Use Dapper](guides/dapper.md).  Your database is not PostgreSQL or SQL Server: start with [another database](guides/other-databases.md)."
5. `## What do you want to do?` (new anchor `find-what-you-need`).  About 16 rows in the reader's words, each to one page: run queries in a transaction, or with a timeout (`S/transactions.md`); pass a list to `IN` (`G/lists.md`); stop a property being nullable (`G/nullability.md`); mock a repository in a test (`S/testing.md`); insert many rows in one call (`G/many-rows.md`); use an enum or an id type of my own (`G/custom-types.md`); read a JSON column (`G/json.md`); use a second database (`G/several-databases.md`); keep the sidecars right in CI (`G/ci.md`); describe as I save (`G/describe-on-save.md`); choose a table name at run time (`G/dynamic-sql.md`); use Dapper (`G/dapper.md`); look up a setting and where to write it (`R/settings.md#all-settings`); look up a `-- word:` line (`R/sql-files.md#marker-index`); look up a command or an option (`R/cli.md#command-reference`); it built, but something is wrong (`G/troubleshooting.md`).
6. `## A build error?` (new anchor `errors`).  Two links: `diagnostics.md`, where the help link of an error opens its section, and `S/errors.md`.
7. `## The three parts` (new anchor `the-three-parts`).  One sentence for each folder and its first page.
8. Links to the two example projects.

Sources: `README.md` 5; `site/README.md` 1-3, 5-57, 107-119.

Written in: two phases.  Phase 3 makes a first landing page: it moves what phase 1 leaves of `site/README.md` (lines 1-57 and 107-119) to `index.md`, word for word, with the anchor `find-what-you-need`, and removes `site/README.md`.  Phase 10 writes the outline above new.  The table has 49 rows at the baseline, and phase 2 adds one for `errors.md`.  19 rows stand behind the 16 rows of the outline above.  The other 30, the row of phase 2 and correction C-06 leave in phase 10, when the sidebars and Get started can take their place.

### Get started pages

Fixed shape of every `start/` page:

1. One sentence that says what the reader has at the end of the page, with the page's search words.
2. Short sections in the order the reader does the work.  Each section: what to do, one whole sample, what you see.
3. A difference between databases is a docfx tab that holds a sample or at most three sentences.  No heading and no `<a id>` is inside a tab.  A long difference is two anchored sections.
4. "If it does not work": the traps that bite on this page, each one sentence with its error id and the fix, stated in full on the page.
5. "What you have now".
6. "Rules:" with links to the reference owners.  No step sends the reader into reference.
7. "Next:" with the next page.  It is the last line of the page.
8. No `## Limits`, no `setting-<name>` anchor, no table of values or defaults.

Every statement of a `start/` page is `restated-from` a reference owner, except the samples, the diagram, and the command that adds the package (`S/install.md#add-the-package`), which no reference page states.

Written in: phase 10 writes all nine pages new, and no rewrite phase covers `start/`.  Three of the pages exist before phase 10, because a move phase makes each page that one of its units needs.  Each holds one unit of today under its anchor, word for word, and stands in the Get started sidebar:

| Page | Made in | Holds until phase 10 |
|----|----|----|
| `S/keep-up-to-date.md` | Phase 5 | `recipes.md` 185-196, under `after-a-migration` |
| `S/transactions.md` | Phase 8 | `recipes.md` 258-300 and `README.md` 191-193 (MP-13), under `queries-in-a-transaction` |
| `S/testing.md` | Phase 8 | `README.md` 187-189 |

Phase 10 writes each of the three around its unit, rewrites the unit in plain language, and merges MP-13.  `site/start/concepts.md` stands beside them from phase 8 to phase 10 (the map of `site/concepts.md` in section 4).  A sample or a diagram that a row moves to another `start/` page is placed by phase 10.

First-hour traps go where they bite.  Each is stated in full on the Get started page, in the step or the tab where the reader meets it, and has its reference home:

| Trap | Get started page | Reference home |
|----|----|----|
| On SQL Server, EF Core 8 or 9 brings Microsoft.Data.SqlClient 5.1: SQLSRC267 | `S/install.md#add-the-package`, in the SQL Server tab, with the `PackageReference` that fixes it; `S/errors.md#first-errors` | `D/sqlserver/types.md#limit-ef-core-sqlclient` |
| A `.env` file at the root of the repository is not read | `S/first-query.md#connection` | `R/connections.md#env-file`; `R/msbuild.md#setting-env-file` |
| No dialect is set: SQLSRC209 for every file | `S/first-query.md#name-your-database`; `S/errors.md#first-errors` | `R/settings.md#setting-dialect`; `D/other.md#not-described` |
| A query is not described yet (SQLSRC241), or changed since it was (SQLSRC244) | `S/first-query.md#describe`; `S/keep-up-to-date.md#the-build-describes`; `S/errors.md#first-errors` | `R/settings.md#setting-output`; `R/sidecars.md#describing-again` |
| The .NET SDK is older than 9.0.300, or Visual Studio is before 17.14: SQLSRC021 | `S/install.md#requirements` | `R/msbuild.md#build-requirements` |
| The tool and the package have different versions: SQLSRC206 | `S/install.md#install-the-tool-step` | `R/cli.md#install-the-tool` |
| SQL Server in a local container needs `TrustServerCertificate=True`; the tool cannot sign in with Microsoft Entra ID | `S/first-query.md#connection`, in the SQL Server tab | `D/sqlserver/index.md#connecting` |
| The projects are not restored (SQLSRC220); the database cannot be opened (SQLSRC224) | `S/first-query.md#describe` | `R/cli.md#requirements`; each database's `index.md#connecting` |
| The database cannot work out a parameter's type: SQLSRC226, SQLSRC227 | `S/write-queries.md#optional-filter` | `R/parameters.md#param-marker` |
| A property is nullable though the column never is | `S/write-queries.md#not-null-columns` | `R/model-properties.md#model-nullability`; the task is `G/nullability.md` |
| A build with no connection, and every build in CI, does not describe | `S/keep-up-to-date.md#in-ci` | `R/msbuild.md#running-in-the-build` |

**`S/install.md`**.  Reader: has decided to try it.  First line: the fork for the two other readers (Dapper: `G/dapper.md`; another database: `G/other-databases.md`).  h2: Requirements (new `requirements`); Add the package (new `add-the-package`), with the driver package in a tab for each database; Install the tool (new `install-the-tool-step`); What each package adds to your project (new `packages`).  Traps on this page: SDK older than 9.0.300 or Visual Studio before 17.14 is SQLSRC021; on SQL Server, EF Core 8 or 9 brings Microsoft.Data.SqlClient 5.1, which is SQLSRC267, with the `PackageReference` that fixes it, inside the SQL Server tab; the tool and the package must have one version, or SQLSRC206.  Sources: `README.md` 11-29; `tool.md` 18-29, 562-567; `sqlserver.md` 522-528; tool readme 5-12.  Owners restated: `R/msbuild.md#build-requirements`, `R/cli.md#install-the-tool`, `R/cli.md#requirements`, `D/index.md#supported-versions`, `D/sqlserver/types.md#limit-ef-core-sqlclient`.  The page says once why the flag is there: "The packages are published as previews, so the commands include `--prerelease`".

**`S/first-query.md`**.  Reader: wants one query running.  Steps, in the order a build needs them: 1. Write a query (new `write-a-query`).  2. Mark a partial class (new `mark-a-partial-class`), with the class in a tab: `NpgsqlDataSource` or `string connectionString`.  3. Name your database (new `name-your-database`): `"dialect": "postgres"` or `"dialect": "mssql"` in `sqlsource.json`, in a tab.  4. Give the tool your connection (new `connection`): the `.env` line in a tab, and `.gitignore`.  5. Describe (new `describe`): `dotnet build`, or `dotnet sqlsource describe`, then commit the sidecar.  6. Call the methods (new `call-the-methods`).  Traps on this page: no dialect set gives SQLSRC209 for every file (step 3); a query not yet described is SQLSRC241 (steps 2 and 5); a `.env` file at the root of the repository is not read, the file must be in the project's folder (step 4); SQL Server in a local container needs `TrustServerCertificate=True`, and the tool cannot sign in with Microsoft Entra ID (step 4, SQL Server tab); the projects must be restored first, or SQLSRC220 (step 5); a database that cannot be opened is SQLSRC224 (step 5).  Sources: `README.md` 33-130; `recipes.md` 416-439.  Owners restated: `R/sql-files.md`, `R/attributes.md#attribute`, `R/settings.md#setting-dialect`, `R/connections.md#env-file`, `R/describing.md#which-queries`, `R/repositories.md#repository-methods`, each database's `index.md#connecting`.

**`S/how-it-works.md`**.  Reader: has a working query and asks what happened.  h2: The picture (new `picture`), with the diagram; The words (new `words`): query, marker, sidecar, describe, repository, model, each one sentence and a link to the reference section that defines it; What needs a database and what does not (new `what-needs-a-database`).  This page introduces the few words a newcomer needs.  It is not a glossary.  Sources: `concepts.md` 1-17; `README.md` 107-110; `tool.md` 1-14.

**`S/write-queries.md`**.  Reader: writes the second and third query.  h2: Several queries in one file (new `several-queries`); One row, or no rows (new `result-shapes-in-short`); A parameter (new `parameters`); An optional filter (new `optional-filter`), with `-- param: @name text null`; A list after IN (new `lists`); A column that is never null (new `not-null-columns`), which shows `AS "total!"` and `-- column: total not null`, since the reader writes both in the `.sql` file; Document a query (new `summaries`).  Sources: `README.md` 53-56, 101, 226-228; `sql-files.md` 51-112; `parameters-and-tokens.md` 22-98; `generator.md` 413-450.

**`S/call-queries.md`**.  Reader: uses the generated code in an application.  h2: The repository and its connection (new `repository`), with a tab for each database; Register it (new `register`); The models (new `models`); What each method returns (new `what-a-method-returns`); Cancel a call (new `cancellation`); When a row is missing (new `missing-row`).  Sources: `README.md` 72, 91-103; `generator.md` 575-613, 235-268, 832-852, 1080-1097.

**`S/transactions.md`**.  Reader: must run two queries together.  This is where `methods` is introduced, by the need.  h2: Run queries in a transaction (`queries-in-a-transaction`, from `recipes.md`); Set a command timeout (new `timeout`); One file, two outputs (new `two-outputs`).  It ends with links to `G/many-rows.md` and `G/output-modes.md`.  Sources: `recipes.md` 258-300; `README.md` 191-193; `generator.md` 735-739, 743-774.

**`S/testing.md`**.  Reader: writes a unit test.  h2: Take the interface (new `interface`); Write a fake (new `fake`); Use a mocking library (new `mock`); Add a member of your own (new `your-own-member`).  Sources: `README.md` 187-189; `generator.md` 708-731.

**`S/keep-up-to-date.md`**.  Reader: commits the work, and a colleague pulls it.  h2: What to commit (new `what-to-commit`); The build describes for you (new `the-build-describes`); After a schema change (`after-a-migration`, from `recipes.md`); A merge conflict in a sidecar (new `merge-conflict`); In CI (new `in-ci`).  Next links: `G/describe-on-save.md`, `G/ci.md`.  Trap on this page: a build with no connection reads the committed sidecars and does not describe, so a changed query is SQLSRC244 there.  Sources: `README.md` 126-130; `recipes.md` 185-196; `tool.md` 172-187, 210-246, 250-259.

**`S/errors.md`**.  Reader: sees a red line.  h2: Read an SQLSRC error (new `read-an-error`); What an error costs you (new `what-an-error-costs`); The errors you meet first (new `first-errors`): SQLSRC241, SQLSRC244, SQLSRC209, SQLSRC226 and SQLSRC227, SQLSRC224, SQLSRC021, SQLSRC267, SQLSRC206, each one sentence and a link to its section; It built, but it fails when it runs (new `at-run-time`), which links `G/troubleshooting.md`.  Sources: `diagnostics.md` 3 by way of `R/errors.md`; `sql-files.md` 164-194.

### Guide pages

Fixed shape of every `guides/` page:

1. Title in the reader's words.  The first sentence carries the search words of the table in section 1.
2. "Use this when": two sentences.
3. The whole sample: every file the reader must write, complete enough to copy.
4. "What you get": the generated members the sample gives, and the call.
5. "The rules you need": each rule the task needs, restated in one or two sentences, each ending with a link to its reference owner.  The page is complete for its task without that click.
6. More h2 sections for variations, each a sample and its rules.
7. "If it does not work": what the reader sees, and a link to the limit's own anchor on the page that owns it, or to the error section.  This is the part that the outline's shape of a guide calls `## Limits`.  The heading `## Limits` itself stands only on a page that owns a limit, which among the guides is `G/dapper.md` alone: `SiteStructureTests` asks `G/troubleshooting.md` to link every page that has the heading.
8. A long difference between the databases is two anchored sections, never tabs.

A guide owns a sample and a procedure.  It owns no value, no default and no precedence.

**`G/lists.md`**.  h2: Pass a list after IN (new `list-after-in`); Filter by an optional list (`filter-by-a-list`); On PostgreSQL: one array with ANY (new `postgresql-arrays`), which says that an array parameter is another feature and links `D/postgresql/query-methods.md#array-parameters`; Large and empty lists (new `large-and-empty-lists`).  Sources: `recipes.md` 943-961; `README.md` 226-228.  Restates: `R/parameters.md#list-parameters`, `#list-padding`, `#list-limit`, `D/sqlserver/query-methods.md#list-parameters`.

**`G/nullability.md`**.  New page.  h2: Why a property is nullable (new `why`); Say that a column is never null (new `not-null`), with the alias mark and the `-- column:` line; Say that a column can be null (new `nullable`); Make a parameter nullable (new `parameters`); On PostgreSQL (new `on-postgresql`) and On SQL Server (new `on-sql-server`): the five most common causes for each, and a link to the full rules.  If it does not work: "Column 'name' is null." at run time.  Restates: `R/model-properties.md#model-nullability`, `#column-marker`, `R/parameters.md#param-marker`, `D/postgresql/nullability.md`, `D/sqlserver/nullability.md`.

**`G/dynamic-sql.md`**.  h2: Choose a table name at run time (`table-name-at-run-time`); Choose a clause (new `clause`); Keep it safe (new `safety`), as a docfx alert.  Sources: `recipes.md` 924-939; `README.md` 222-224.  Restates: `R/tokens.md`.

**`G/many-rows.md`**.  h2: Insert many rows in one call (`insert-many-rows`); The transaction it runs in (new `transaction`); When one item fails (new `failures`).  Sources: `recipes.md` 304-325.  Restates: `R/collection-methods.md`, `R/exceptions.md#collection-item-exception`.

**`G/sync-and-streaming.md`**.  Two jobs on one page, each an h2 with its own sample: Run a query from code that does not await (`run-without-awaiting`); Read rows one at a time (new `stream-rows`).  Sources: `recipes.md` 329-369; the samples of `settings.md` 366-393.  Restates: `R/query-methods.md#synchronous-methods`, `R/repositories.md#synchronous-repository-methods`, `R/return-types.md#streaming-rows`.

**`G/repository-connection.md`**.  h2: Give a repository its connection from a service of your own (`connection-from-a-provider`); Make a SQL Server repository from a connection string (`sql-server-repository`); Mark the member yourself (new `mark-the-member`).  Sources: `recipes.md` 373-439.  Restates: `R/repositories.md#repository-connection`.

**`G/naming-and-placement.md`**.  h2: Async names (new `async-names`); Model names and suffixes (new `model-names`); The namespace of the models (new `model-namespace-task`); Record or class (new `record-or-class`); A list in place of an array (new `collection-type-task`); Where the members go and who sees them (new `placement`).  Sources: `README.md` 210-212.  Restates: `R/generator-parameters.md`, `R/models.md`, `R/return-types.md#setting-collection-type`, `R/members.md`.

**`G/custom-types.md`**.  h2: Give a database type a C# type (new `type-override`); Give one column or one parameter its own type (new `one-column`); Give an id a type of its own (`strongly-typed-id`); A type made by Vogen or StronglyTypedId (new `vogen`), as steps; A C# enum (new `enum`).  If it does not work: `InvalidCastException`.  Sources: `README.md` 252-288; `recipes.md` 443-493; `settings.md` 1071-1086.  Restates: `R/type-mapping.md`, `R/model-properties.md#setting-column-types`, `R/converters.md`.

**`G/json.md`**.  h2: Turn JSON on (new `turn-json-on`); Read a class of your own from a JSON column (`class-in-a-json-column`); JSON in a text column (new `json-in-text`).  Sources: `recipes.md` 497-587; `README.md` 248.  Restates: `R/library-types-and-json.md`, each database's `libraries.md#json`.

**`G/nodatime-and-spatial.md`**.  h2: NodaTime on PostgreSQL (new `nodatime-postgresql`); NodaTime on SQL Server (new `nodatime-sql-server`); NetTopologySuite on PostgreSQL (new `nts-postgresql`); NetTopologySuite on SQL Server (new `nts-sql-server`); With Entity Framework Core or Dapper in the same project (new `ef-core-and-dapper`).  Sources: `README.md` 240-246, 250.  Restates: `R/library-types-and-json.md#setting-library-types`, each database's `libraries.md#nodatime` and `#nettopologysuite`.

**`G/several-databases.md`**.  h2: Use several databases (`use-several-databases`); A repository on two databases (new `repository-on-two`); Read one folder with another dialect (`folder-with-another-dialect`).  Sources: `recipes.md` 27-65, 93-121; `README.md` 214-216.  Restates: `R/settings.md#setting-database`, `#setting-dialect`, `R/connections.md#connections`.

**`G/folders-and-files.md`**.  h2: Configure one folder differently (`configure-a-folder`); Leave migration scripts out (`leave-migration-scripts-out`); One class for a whole folder (new `class-for-a-folder`).  Sources: `settings.md` 851-880; `recipes.md` 69-89; `README.md` 218-220.  Restates: `R/settings.md#precedence`, `R/settings-file.md#settings-files`, `R/msbuild.md#included-files`, `R/attributes.md#setting-source`.

**`G/several-projects.md`**.  h2: Let a test project see the internals of the main project (`test-project-sees-internals`); Share the exceptions between two data projects and an app (`share-the-exceptions`); One `.sql` file in two projects (new `one-file-two-projects`).  Sources: `recipes.md` 965-1030; `README.md` 230-232.  Restates: `R/exceptions.md`, `R/sidecars.md#limit-file-in-two-projects`.

**`G/describe-on-save.md`**.  h2: Start the watch (new `start-the-watch`); Start the watcher with your editor (`start-the-watcher-with-your-editor`); VS Code (new `vs-code`); Rider (new `rider`); Visual Studio (new `visual-studio`).  The three editors are h2 sections, not tabs, since `EditorRecipeTests` reads the VS Code task from this file.  Sources: `recipes.md` 200-254.  Restates: `R/describing.md#describe-on-save`, `#what-your-editor-shows`.

**`G/ci.md`**.  h2: Check the sidecars in CI (`check-sidecars-in-ci`), one sample job for PostgreSQL and one for SQL Server as two anchored sections (new `ci-postgresql`, `ci-sql-server`); Read the exit code (new `exit-code`).  Sources: `recipes.md` 125-181.  Restates: `R/describing.md#check-in-ci`, `R/cli.md#exit-codes`.

**`G/report-a-bug.md`**.  h2: Reporting a wrong model (`report-a-bug`); What is safe to post (new `what-is-safe`), a restatement of what a bundle renames and keeps; When a bundle cannot be made safe (`raw-bundle`).  The whole procedure is on this page, so the reader who decides what to post does not leave it.  Sources: `tool.md` 373-390, 409-437; `README.md` 234-236.  Restates: `R/bundles.md#bundle-contents`, `#diagnose`.

**`G/troubleshooting.md`**.  Reader: has a symptom and no `SQLSRC` id.  h2: An exception at run time (new `at-run-time`), with `InvalidCastException` (new `invalidcastexception`), "Column 'name' is null." and `SqlNullValueException`, `NotSupportedException` from a collection method; A model that looks wrong (new `wrong-model`); A method returns something you did not expect (new `wrong-result`); A query is not described, or a file is not found (new `not-described`); The watch does nothing (new `watch`); A secret in the output (new `secrets`).  It has one entry for each of the 36 limits of section 4, keyed by what the reader sees, each one or two sentences and a link to the limit's own anchor.  Sources: `README.md` 195-208; `site/README.md` 57.

**`G/output-modes.md`**.  h2: The four outputs (new `the-four-outputs`), a table in the order `repository`, `methods`, `models`, `sql`, with "use it when" for each; Set the output for a query, a file or a folder (new `set-the-output`); Which queries need a database (new `which-need-a-database`).  It states the frame of decision 4: `repository` is the default; `methods` is for your own code structure, above all your own transaction; `models` and `sql` are fallbacks, mainly for Dapper.  It also says the two cases where `sql` is used inside a described project.  Restates: `R/settings.md#setting-output`.

**`G/dapper.md`**.  Reader: another library runs the SQL.  A whole walkthrough on one page, both databases.  h2: Models for Dapper, start to end (new `models`); Use the queries with Dapper or your own ADO.NET code (`dapper-and-adonet`); Run the models through Dapper (`models-through-dapper`); With PostgreSQL (`dapper-with-postgresql`); With SQL Server (`dapper-with-sql-server`); Only the SQL (new `sql-only`); Limits (new `limits`, with `limit-parameter-names`).  The two database sections are h2, promoted from h3.  Sources: `README.md` 132-180; `recipes.md` 591-920; `generator.md` 1183.  Linked from `index.md`, `S/install.md` and `README.md`.

**`G/other-databases.md`**.  Reader: MySQL, MariaDB, SQLite, Oracle, CockroachDB, DuckDB or another database.  A whole short walkthrough: 1. Install the package alone (new `install`).  2. Write a query (new `write-a-query`).  3. Mark a partial class (new `mark-a-class`).  4. Set the dialect of your database and the output `sql` (new `dialect-and-output`).  5. Run the constant (new `run-the-constant`).  Then: What you do not get (new `what-you-do-not-get`), which names SQLSRC209 and says that DuckDB is read as `postgres` and needs the output `sql`.  Sources: `README.md` 9, 181; `dialects.md` 145-147.  Restates: `R/dialects.md`, `D/other.md`.  Linked from `index.md`, `S/install.md` and `README.md`.

**Written in: the guide pages.**  A move phase makes a guide when one of its units lands there, with the title and each heading and anchor that the unit stands under.  Phase 9 writes each section that has no text today, and the three pages that have none.  Phase 12 rewrites the moved text in plain language and the fixed shape, and merges the pairs.

| Page | Made in | Sections that hold moved text, and the phase that places it | Phase 9 writes |
|----|----|----|----|
| `G/lists.md` | 8 | `list-after-in`, `filter-by-a-list` (8) | `postgresql-arrays`, `large-and-empty-lists` |
| `G/nullability.md` | 9 | None | The whole page |
| `G/dynamic-sql.md` | 8 | `table-name-at-run-time` (8) | `clause`, `safety` |
| `G/many-rows.md` | 8 | `insert-many-rows` (8) | `transaction`, `failures` |
| `G/sync-and-streaming.md` | 8 | `run-without-awaiting` (8) | `stream-rows` |
| `G/repository-connection.md` | 8 | `connection-from-a-provider`, `sql-server-repository` (8) | `mark-the-member` |
| `G/naming-and-placement.md` | 8 | `model-namespace-task` (8) | `async-names`, `model-names`, `record-or-class`, `collection-type-task`, `placement` |
| `G/custom-types.md` | 8 | `type-override`, `one-column`, `strongly-typed-id` (8) | `vogen`, `enum` |
| `G/json.md` | 8 | `turn-json-on`, `class-in-a-json-column` (8) | `json-in-text` |
| `G/nodatime-and-spatial.md` | 8 | All five sections (8) | Nothing |
| `G/several-databases.md` | 8 | `use-several-databases`, `folder-with-another-dialect` (8) | `repository-on-two` |
| `G/folders-and-files.md` | 7 | `configure-a-folder` (7); `leave-migration-scripts-out` (8) | `class-for-a-folder` |
| `G/several-projects.md` | 8 | `test-project-sees-internals`, `share-the-exceptions` (8) | `one-file-two-projects` |
| `G/describe-on-save.md` | 5 | `start-the-watcher-with-your-editor`, `vs-code`, `rider`, `visual-studio` (5) | `start-the-watch` |
| `G/ci.md` | 5 | `check-sidecars-in-ci`, cut by the headings `ci-postgresql` and `ci-sql-server` (5) | `exit-code` |
| `G/report-a-bug.md` | 5 | `report-a-bug` (5, and `README.md` 234-236 in 8); `raw-bundle` (5) | `what-is-safe` |
| `G/troubleshooting.md` | 3 | The title, the six h2 headings and their anchors (3); the entry of each limit and pointer, in the phase that moves its bullet (3 to 7: section 4); `invalidcastexception` (8) | Each entry that is not a limit's, and a last check that all 36 limits and 3 pointers have one |
| `G/output-modes.md` | 9 | None | The whole page |
| `G/dapper.md` | 6 | `limits` and `limit-parameter-names` (6); `models`, `dapper-and-adonet`, `models-through-dapper`, `dapper-with-postgresql`, `dapper-with-sql-server`, `sql-only` (8) | The walkthrough under `models`, start to end, for both databases |
| `G/other-databases.md` | 8 | `dialect-and-output`, one bullet (8) | The rest of the page |

### Reference pages

Shape: a section opens with an example, then the rules.  A setting's section has, in this order: example, values, default, the places it can be written, what is special about its precedence, its errors.  A page that has limits ends with `## Limits` (anchor `limits`), and each limit is an h3 with its own anchor `limit-<name>`, written as what you see, why, what to do.

**`R/sql-files.md`**.  h2: What a marker is (new `marker`); Marker index (`marker-index`); Queries and names (`queries-and-names`); Result shapes (`result-shapes`); Summaries (`summaries`); The file header (`the-file-header`); A file with no `-- name:` (`a-file-with-no-name`); What reaches the generated SQL (`generated-sql`); Markers in the wrong place (`markers-in-the-wrong-place`).  Defines: query, marker, file header.  Sources: `sql-files.md` 1-194; `concepts.md` 27-41.

**`R/parameters.md`**.  h2: Query parameters (`query-parameters`); Giving a parameter a type (`param-marker`); List parameters (`list-parameters`) with Padding (`list-padding`) and The limit of parameters (`list-limit`); Variables declared with `DECLARE` (`declared-variables`); Names that are read as parameters (`names-read-as-parameters`); Limits (`limits`: `limit-parameter-taken-for-a-variable`).  Defines: query parameter.  Sources: `parameters-and-tokens.md` 22-156, 260-261; `concepts.md` 89-91.

**`R/tokens.md`**.  h2: Token or parameter (new `token-or-parameter`); Tokens (`tokens`); Defaults (`token-defaults`) with `-- token:` (`token-marker`); Text that looks like a token (`token-ignore`); Validation (`token-validation`).  Defines: token.  Sources: `parameters-and-tokens.md` 1-18, 160-254; `concepts.md` 101-103.

**`R/dialects.md`**.  Kept whole.  It opens: "If your database is PostgreSQL or SQL Server, set the dialect to `postgres` or `mssql` and stop reading.  The rest of this page is for the output `sql` on another database, and for SQL that SqlSource reads differently from your database."  h2: Dialect names (`choosing-a-dialect`, heading reworded); Options (`dialect-options`); What each dialect changes (`what-a-dialect-changes`) with Markers under `mysql` and `mariadb` (`markers-under-mysql`); What is still read differently (`still-read-differently`).  Sources: `dialects.md` 1-128, 153.

**`R/settings.md`**.  h2: The places a setting is written (new `places`); All settings (`all-settings`); Which place wins (`precedence`) with A value that is wrong (`a-value-that-is-wrong`); Output (`setting-output`); Database (`setting-database`); Dialect (`setting-dialect`).  Defines: setting, output, logical database, dialect.  It holds the reader-facing statement of "dialect or logical database" in `#setting-database`.  Its opening links `R/settings-file.md` for the file itself.  Sources: `settings.md` 1-189; `concepts.md` 61-63, 105-119.

**`R/settings-file.md`**.  Sidebar title "sqlsource.json".  Reader: has the file open, and asks what it may hold and which `.sql` files it reaches.  h2: The settings file (`settings-files`), whose five bold parts become h3 sections (new `file-name`, `file-contents`, `files-of-a-sql-file`, `which-files-are-checked`, `settings-file-in-the-build`); Limits (`limits`: `limit-symbolic-link`).  A bold part cannot become a heading without a change of words, so phase 7 puts the five new anchors above the five bold parts, and phase 14 makes each part an h3 under its anchor.  Its opening links `R/settings.md#all-settings` for the keys and `R/settings.md#precedence` for which file wins.  Defines: settings file.  Sources: `settings.md` 1011-1051, 1057; `concepts.md` 53-55.

**`R/generator-parameters.md`**.  h2: Generator parameters (`setting-generator-parameters`), with a new h3 for each row of the table: ten for the eleven parameters, since `sort-input` and `sort-output` share a row and so a heading, and one for the row `default` (new anchors `gp-keep-comments`, `gp-token-validation`, `gp-sort-input`, `gp-sort-output`, `gp-table-models`, `gp-async-method-suffix`, `gp-array-pool`, `gp-list-padding`, `gp-collection-method`, `gp-transaction`, `gp-method-type`, `gp-default`), so that no cell of the table is cut; Three kinds of parameter (new `three-kinds-of-parameter`), the reader-facing statement of that distinction.  Phase 7 makes the eleven headings and the twelve anchors and puts each row's Effect cell under its heading, word for word.  Phase 8 makes `three-kinds-of-parameter`, for `concepts.md` 83-85.  Defines: generator parameter.  Sources: `settings.md` 193-253; `concepts.md` 83-97.

**`R/attributes.md`**.  h2: The three attributes (new `attributes`), a table of each attribute and property with a link to its section; `[SqlSourceGenerate]` (`attribute`); Which files belong to a type (`files-of-a-type`); `Source` (`setting-source`); What the package adds to your project (new `what-the-package-adds`); Limits (`limits`: `limit-source-written-as-text`, `limit-embedded-attribute`).  Sources: `generator.md` 36-77, 1182, 1190; `settings.md` 822-847; `README.md` 26.

**`R/msbuild.md`**.  h2: The properties (new `properties`), a table of the five properties a user sets; What the build needs (new `build-requirements`); Which `.sql` files are included (`included-files`); Running in the build (`running-in-the-build`); `SqlSourceOnline` (`online-build`); The build's `.env` file (`setting-env-file`); Limits (`limits`: `limit-file-added-by-a-target`).  Sources: `settings.md` 884-910, 959-1007, 1058-1068; `tool.md` 210-246, 633; `README.md` 29.

**`R/members.md`**.  h2: What the generator writes (new `generator`); Where the members go (`locations` and `setting-member-location`: phase 6 places the two sections of today one after the other, each under its own anchor, and the merge of MP-01 in phase 16 leaves one heading with both `<a id>` lines above it) with A placement for a whole file or folder (`placement-for-a-file`); Visibility (`setting-visibility`); Name of the nested class (`setting-nested-class`); Constants (`constants`); Token methods (`token-methods`); Documentation in IntelliSense (`intellisense`).  Defines: generator, token method.  Sources: `generator.md` 1-32, 81-231; `settings.md` 716-818; `concepts.md` 79-81, 125-127.

**`R/models.md`**.  h2: Models (`models`); Which queries get models (`which-models`); Names (`model-names`, with `setting-model-suffix` and `setting-model-name`); Namespace (`model-namespace`, with `setting-model-namespace`); Accessibility (`model-accessibility`); Shape (`model-shape`, with `setting-model-type`); Documentation (`model-documentation`); Table models (`table-models`); Shared models (`shared-models`); Names that cannot be used (`unusable-model-names`).  Phase 6 places each section of `settings.md` under the heading of its pair, under its own `setting-` anchor.  The merges of MP-02, MP-03 and MP-04 in phase 16 keep all seven anchors of the three headings.  Sources: `generator.md` 235-352, 454-527; `settings.md` 257-331.

**`R/model-properties.md`**.  h2: The order of the properties (`property-order`); Property names (`property-names`); Which properties can be null (`model-nullability`) with Setting a column's nullability or type with a marker (`column-marker`); Column types (`setting-column-types`).  The page's opening links `R/type-mapping.md#property-types` for the order in which a type is chosen.  Sources: `generator.md` 356-381, 413-450; `settings.md` 572-619.

**`R/query-methods.md`**.  h2: Query methods (`query-methods`); Which queries get a method (`which-methods`); Synchronous methods (`synchronous-methods`); How a row is read (`reading-rows`); How a parameter is sent (`method-parameters`); Limits (`limits`: `limit-wrapped-connection`).  Defines: query method.  Sources: `generator.md` 531-571, 939-1000, 1058-1076, 1186; `concepts.md` 67-69.

**`R/return-types.md`**.  h2: What each shape returns (`method-shapes`); Collection type (`setting-collection-type`); Streaming rows (`streaming-rows`).  Sources: `generator.md` 832-852; `settings.md` 335-393.

**`R/repositories.md`**.  h2: Repository methods (`repository-methods`); The signature (`repository-signature`); The connection (`repository-connection`); The interface (`repository-interface` and `repository-interfaces`: phase 6 places the two sections of today one after the other, each under its own anchor, and the merge of MP-05 in phase 17 leaves one heading with both `<a id>` lines above it); Transactions and timeouts (`repository-transactions`); Synchronous repository methods (`synchronous-repository-methods`).  Defines: repository.  Sources: `generator.md` 575-739, 1004-1054; `settings.md` 930-955; `concepts.md` 73-75.

**`R/connection-methods.md`**.  h2: Methods that take a connection (`methods-that-take-a-connection`); Where the methods go (`method-location`); The signature (`method-signature`); The connection (`method-connection`).  Sources: `generator.md` 743-828.

**`R/collection-methods.md`**.  h2: Collection methods (`collection-methods`); Transactions (`collection-transactions`); What it throws (`collection-failures`); Limits (`limits`: `limit-wrapped-connection`).  Sources: `generator.md` 856-935, 1187.

**`R/exceptions.md`**.  h2: `UnexpectedRowCountException` (`unexpected-row-count`); `TransactionRolledBackException` (`transaction-rolled-back`); `CollectionItemException` (`collection-item-exception`); Public exceptions (`public-exceptions`); Projects that share internals (`projects-that-share-internals`); Limits (`limits`: `limit-caught-by-type`, `limit-cref`).  Sources: `generator.md` 1080-1176, 1184-1185; `settings.md` 914-926.

**`R/type-mapping.md`**.  h2: How a C# type is chosen (`property-types`), the one statement of the order; Type overrides (`setting-type-overrides`); Limits (`limits`: `limit-key-in-brackets`, `limit-nullable-type-argument`).  Sources: `generator.md` 385-409; `settings.md` 397-467, 1069-1070; `site/README.md` 101.

**`R/library-types-and-json.md`**.  h2: Library types (`setting-library-types`); A class in a JSON column (`json-class`); The JSON opt-in (`json-opt-in`).  Sources: `settings.md` 530-568, 623-712.

**`R/converters.md`**.  h2: Converters (`converters`); Limits (`limits`: `limit-vogen-and-stronglytypedid`, `limit-one-conversion-per-type`).  Sources: `settings.md` 471-526, 1071-1087.

**`R/cli.md`**.  h2: The `sqlsource` command (new `tool`); Install the tool (`install-the-tool`); Command reference (`command-reference`) with Arguments (`arguments`), Options (`options`), Environment variables (`environment-variables`), Exit codes (`exit-codes`), `--verbose` (`verbose`), Logs (`logs`), Requirements (`requirements`); The summary (`summary`); Limits (`limits`: `limit-path-through-a-symbolic-link`).  Its opening links `R/describing.md`, `R/msbuild.md#running-in-the-build`, `R/bundles.md` and `R/errors.md#the-tools-errors`.  Defines: tool.  Sources: `tool.md` 1-29, 355-369, 441-567, 631; `concepts.md` 131-133.

**`R/describing.md`**.  h2: What describe does (new `describe`); Which queries are described (`which-queries`); After a schema change (`after-a-schema-change`); Describe on save (`describe-on-save`) with What your editor shows (`what-your-editor-shows`); Check in CI (`check-in-ci`); Limits (`limits`: `limit-several-target-frameworks`, `limit-saves-not-seen`, `limit-editors-not-checked`, `limit-folder-changes-not-seen`).  Defines: describe.  Sources: `tool.md` 33-50, 250-351, 632, 635-637; `concepts.md` 137-139.

**`R/connections.md`**.  h2: Connections (`connections`); A `.env` file (`env-file`); Limits (`limits`: `limit-short-password`).  Sources: `tool.md` 54-127, 638.

**`R/sidecars.md`**.  h2: Sidecar files (`sidecar-files`); Sidecars in git (`sidecars-in-git`); Describing again (`describing-again`); Limits (`limits`: `limit-file-in-two-projects`).  Defines: sidecar.  Sources: `tool.md` 131-206, 634; `sql-files.md` 200; `concepts.md` 45-49; `diagnostics.md` 315.

**`R/bundles.md`**.  h2: What is renamed before you share it (`bundle-contents`); Making a bundle with `diagnose` (`diagnose`); Checking a bundle with `replay` (`replay`).  Its opening links `G/report-a-bug.md` for the procedure.  Sources: `tool.md` 394-405, 571-625.

**`D/index.md`**.  The one home of supported versions.  h2: Supported versions (new `supported-versions`): one table with a row for each described database and the columns database, server versions supported, server versions tested, driver package, driver versions supported, driver versions tested, `dialect` value, pages; Database support (`database-support`): the table of every dialect, and what "described" adds; Azure SQL (new `azure-sql`): accepted whatever version it reports, and not tested.  Built to take more databases: a new one is a row of each table and a folder of the five file names.  Sources: `dialects.md` 132-145; `postgresql.md` 25, 473; `sqlserver.md` 28; `README.md` 9, 29.

**`D/postgresql/index.md`** and **`D/sqlserver/index.md`**.  h2: Connecting (`connecting`); What describing does to your database (`what-describing-does`); Parameters (`parameters`); What can be described (`what-can-be-described`); What this database decides in a model (`models`); Limits (`limits`).  PostgreSQL limits: `limit-operator-on-oid`, `limit-many-partitions`.  SQL Server limits: `limit-several-result-sets`, `limit-type-name-unicode`, `limit-parameter-name-unicode`, `limit-parameter-two-spellings`.  Sources: `postgresql.md` 1-72, 379-405, 525-527; `sqlserver.md` 1-90, 369-393, 549, 567-571.

**`D/postgresql/nullability.md`** and **`D/sqlserver/nullability.md`**.  h2: Which columns can be null (`nullability`); Limits (`limits`).  PostgreSQL: `limit-columns-treated-as-nullable`.  SQL Server: `limit-columns-treated-as-nullable`, `limit-ansi-warnings-off`.  Sources: `postgresql.md` 76-148, 524; `sqlserver.md` 94-142, 530-532.

**`D/postgresql/types.md`** and **`D/sqlserver/types.md`**.  h2: Types (`type-map` on PostgreSQL, `types` on SQL Server); Which version of the driver (`which-npgsql`, `which-sqlclient`); The types (`type-table`); What to know about some types (`type-notes`); on SQL Server, Limits (`limits`: `limit-ef-core-sqlclient`).  The h3 sections of today become h2, so each is in the page's right-hand list.  Sources: `postgresql.md` 152-247; `sqlserver.md` 146-213, 522-528.

**`D/postgresql/libraries.md`** and **`D/sqlserver/libraries.md`**.  Sidebar title "NodaTime, NetTopologySuite and JSON".  h2: NodaTime (`nodatime`); NetTopologySuite (`nettopologysuite`); JSON (`json`).  Each says which types the library gives on this database, how it is turned on, and what the application must call.  The setting is owned by `R/library-types-and-json.md#setting-library-types`.  The h3 sections of today become h2.  A database with no library support has a short page that says so.  Sources: `postgresql.md` 251-375; `sqlserver.md` 217-365.

**`D/postgresql/query-methods.md`** and **`D/sqlserver/query-methods.md`**.  h2: Query methods (`query-methods`); How a parameter is sent (`method-parameters`); Array parameters (`array-parameters`, PostgreSQL) or Lists (`list-parameters`, SQL Server); What a method reads and throws (`method-results`); Collection methods (`collection-methods`); Limits (`limits`).  PostgreSQL: `limit-connection-string-repository`.  SQL Server: `limit-ansi-nulls-off`, `limit-hidden-rollback`, `limit-rowcount-after-nocount`, `limit-list-in-an-aggregate`.  On the SQL Server page alone, phase 4 cuts "What a method reads and throws" with five new h3 headings and anchors, placed between its bullets in today's order, so that no bullet moves and none is cut: Reading a row (new `reading-a-row`, `sqlserver.md` 476-480); Null values (new `null-values`, 481); Errors and rollbacks (new `errors-and-rollbacks`, 482-484); `rowcount` (new `rowcount`, 485); Timeouts, cancelling and the connection (new `timeout-and-cancel`, 486-488).  The two bullets on the transaction, 474-475, stay directly under the h2.  Sources: `postgresql.md` 409-518, 528; `sqlserver.md` 397-516, 534-565; `generator.md` 1188-1189.

**`D/other.md`**.  h2: Databases that are not described (new `not-described`): what such a database gets, and the error without the output `sql` (SQLSRC209); Which dialect to set (new `which-dialect`), a pointer to `R/dialects.md#choosing-a-dialect`; DuckDB (new `duckdb`).  It sends the reader to `G/other-databases.md` for the walkthrough.  Sources: `dialects.md` 138-147.

**`R/errors.md`**.  h2: Errors, warnings and information (new `severity`); Where an error is reported (new `where-an-error-is-reported`); What an error costs (new `what-an-error-costs`); Who reports an error: the build, the tool, or both (new `who-reports`); The tool's errors (new `the-tools-errors`); Errors by area (new `errors-by-area`): 29 rows, one for each area of today's index, with its ids as links, who reports them and where.  Sources: `diagnostics.md` 3-345.  Written in: phase 2, which removes the preamble, writes the page new and in plain language, as `site/errors.md`, at the root, since the folders do not exist yet; phase 3 moves it to `site/reference/errors.md`.  It has no rewrite phase.  The table of areas holds the 29 area names, each with its ids as links, and no titles: the table of the 161 error titles is not kept (section 9).

**`diagnostics.md`**.  `# Diagnostics`, then one short paragraph, and nothing else before `## SQLSRC001`.  Phase 2 writes the paragraph.  It holds exactly these four statements, in this order:

1. Each section of the page is one `SQLSRC` diagnostic, under its id.
2. The help link of a build error opens the section of that error.
3. How errors are reported, and the list of the errors by area, are on the page "How errors are reported".  This is the paragraph's one link, to `R/errors.md`: phase 2 writes it to `errors.md`, and phase 3 retargets it.
4. The samples of the tool's errors on the page leave out the `see:` line that the tool prints.  This is the second half of sentence 10 of today's line 3.

The paragraph holds no index, no range of ids, no rule about severity, no rule about which database a section is for, and no status.  `DiagnosticsDocumentTests` fails when more than the title and one paragraph stand before `## SQLSRC001`.

Then the 161 `## SQLSRCnnn` sections.  The headings, the `#sqlsrcnnn` anchors and the bold title lines do not change in any phase.  Phase 2 adds two lines directly under the bold title line: "Reported by" in every section, which says what reports the error (the build, the tool, or both), and "Database" in each of the 18 sections of correction C-04.  Phases 21 to 24 rewrite the sections in plain language to the one shape of the outline: the bold title line (kept word for word), the "Reported by" line, the "Database" line where the section has one, what happened, why, the fix as numbered steps, a sample where one helps, and the closing `See:` line.

**Written in: the reference pages.**  The table gives the phase that makes each page, and each piece of new text with the phase that writes it.  A page that is not listed is made by the phase that moves its text and holds no new text but its title: `R/models.md`, `R/return-types.md`, `R/repositories.md`, `R/connection-methods.md` and `R/library-types-and-json.md`.

| Page | Made in | New text, and the phase that writes it |
|----|----|----|
| `R/sql-files.md` | 3 | The heading and anchor `marker` (3) |
| `R/parameters.md` | 3 | The sentence of the first paragraph that links `R/tokens.md#token-or-parameter` (3); `limits` and its limit's h3 (3) |
| `R/tokens.md` | 3 | The heading and anchor `token-or-parameter` (3) |
| `R/dialects.md` | 3 | The first paragraph (3) |
| `R/sidecars.md` | 3, for the limit of `sql-files.md` 200 | `limits` and the limit's h3 (3).  Phase 5 fills the page |
| `D/index.md` | 3, for `dialects.md` 132-145 | The heading, anchor and table of `supported-versions`, and the heading and anchor `azure-sql` (4) |
| `D/other.md` | 3 | The opening sentence, the three headings and anchors, and the sentence of `which-dialect` that points (3); the sentence that sends the reader to `G/other-databases.md` (8, the phase that makes that page) |
| The ten pages of `D/postgresql/` and `D/sqlserver/` | 4 | `limits` and each limit's h3, on the seven pages that have limits (4); the five h3 headings and anchors of `D/sqlserver/query-methods.md` (4).  Phase 6 adds the text of `generator.md` 1188 and 1189 to two limits |
| `R/cli.md` | 5 | The heading and anchor `tool`, and the links of the opening (5); a second table under `options`, of six rows, for `--help`, `--version` and the four options that `diagnose` alone has, each a restatement of `R/bundles.md#diagnose` or of the paragraph above the tables, so that `CliReferenceTests` finds a row for every option (5); `limits` and its limit's h3 (5) |
| `R/describing.md` | 5 | The heading and anchor `describe`, with one sentence that says what `describe` does (5), since links need the anchor from phase 6 and the term's text comes in phase 8; `limits` and the four h3 (5) |
| `R/connections.md` | 5 | `limits` and its limit's h3 (5) |
| `R/bundles.md` | 5 | The sentence of the opening that links `G/report-a-bug.md` (5) |
| `R/msbuild.md` | 5, for `tool.md` 210-246 | The heading, anchor and table of `properties` (7); `limits` and its limit's h3 (7); the heading and anchor `build-requirements` (8, for `README.md` 29) |
| `R/attributes.md` | 6 | The heading, anchor and table of `attributes` (6); `limits` and the two h3 (6); the heading and anchor `what-the-package-adds` (8, for `README.md` 26) |
| `R/members.md` | 6 | The heading and anchor `generator` (6) |
| `R/model-properties.md` | 6 | The sentence of the opening that links `R/type-mapping.md#property-types` (6) |
| `R/query-methods.md`, `R/collection-methods.md`, `R/exceptions.md` | 6 | `limits` and each limit's h3 (6) |
| `R/type-mapping.md` | 6, for `generator.md` 385-409 | `limits` and the two h3 (7) |
| `R/settings.md` | 7 | The heading and anchor `places`, its table of the five places and the fourth sample (7); the matrix of 22 rows under `all-settings` (7); the sentence of the opening that links `R/settings-file.md` (7) |
| `R/settings-file.md` | 7 | The five anchors of the bold parts (7); `limits` and its limit's h3 (7); the two links of the opening (7) |
| `R/generator-parameters.md` | 7 | The eleven `gp-` headings and their twelve anchors (7); the heading and anchor `three-kinds-of-parameter` (8) |
| `R/converters.md` | 7 | `limits` and the two h3 (7) |
| `R/errors.md` | 2, at the root | The whole page (2) |

## 4. The map

One table for each current document.  One row for each heading, and one row for each part of a heading whose parts go to different places.  A row that starts with `h1`, `h2`, `h3` or `h4` is a heading, with the heading's own text.  A row with no such mark is a part of the heading above it.  "Stays" means the unit stays in the same file.  A row marked "navigation" holds no fact: its links are replaced by a sidebar entry or by the links at the foot of a page.

### Map: `README.md`

The README stays a packed readme: no raw HTML, absolute links, `--prerelease` on the commands, `nuget/vpre` in the badge.  Its new outline is in section 7.

The README changes in four phases, and the rows below do not repeat them:

- Phase 1 takes out each `removed-status` row.
- Phase 8 moves the sections of lines 132 to 288 (the two scenarios, the FAQ, the libraries and the custom type mapping) to the pages named, and leaves a pointer under each heading of the README.  It also places, word for word, the units of lines 9 and 19 to 29 that land on a reference page; the README keeps its own copy of those until phase 11.
- Phase 10 writes the `start/` pages and the landing page, and so restates or places each unit of lines 1 to 130 that lands on one of them.
- Phase 11 cuts the README to the outline of section 7.  A row that says "`README.md` keeps" or "Stays" describes the README after phase 11.

| Lines | Old unit | New location | Disposition | Note |
|----|----|----|----|----|
| 1-9 | h1 SqlSource | `README.md`; `index.md` | move-split | Four parts, below |
| 3 | Badges | Stays | move | |
| 5 | Paragraph: what SqlSource does and why | `index.md` item 1, whole.  `README.md` keeps four sentences of it | move | |
| 7 | "Status" paragraph | None | removed-status | Its link to `#what-works-today` goes.  What it says is generated is owned by `R/members.md#generator` |
| 9 | "Databases", sentence 1: constants work with any database | `D/index.md#database-support` | restated-from `D/index.md#database-support` | Said today by `dialects.md` 145 |
| 9 | "Databases", sentence 2: describing works with PostgreSQL 14 or later, SQL Server 2017 or later, and Azure SQL, "which is accepted and not tested" | `D/index.md#supported-versions`, `#azure-sql` | merge-pair MP-23 | "Not tested" is a support fact and stays.  Placed in phase 8 |
| 9 | "Databases", sentences 3 to 5: which steps to follow for which database | `index.md` item 4; `README.md` "Other ways to use it" | restated-from `G/other-databases.md`, `G/dapper.md` | navigation |
| 11-29 | h2 Installation | `S/install.md`; `README.md` "Install" | move-split | Parts below |
| 13 | Sentence: "Until the first release the packages are previews, so the commands include `--prerelease`" | None | removed-status | The flag stays on every command.  One neutral sentence replaces this one, on `S/install.md` and in both readmes: "The packages are published as previews, so the commands include `--prerelease`" |
| 13-17 | The command that adds the package, and its lead-in | `S/install.md#add-the-package`.  `README.md` keeps the command | move | The one statement that a `start/` page owns.  Phase 10 places it |
| 19-24 | The two tool commands, and their lead-in | `R/cli.md#install-the-tool`.  `README.md` keeps the commands | merge-pair MP-11 | Placed in phase 8.  Restated on `S/install.md#install-the-tool-step` |
| 26 | Bullet `SqlSource`: runs in the compiler, no runtime dependency, what is compiled into your project | `R/attributes.md#what-the-package-adds` | move | Its last sentence is corrected: C-07 (section 5).  Placed in phase 8, which makes the heading and the anchor.  Restated on `S/install.md#packages`.  `generator.md` 38 lists the same types; the rewrite of phase 14 joins them |
| 27 | Bullet `SqlSource.Tool`: commit `dotnet-tools.json`, keep the tool at the package's version, a project that only keeps SQL out of code needs no tool | `R/cli.md#install-the-tool` | merge-pair MP-11 | Placed in phase 8.  Restated on `S/install.md#packages` |
| 29 | "Supported environments", sentences 1 and 2: the project targets .NET 8 or later and C# 12 or later; it builds in the .NET SDK 9.0.300 or later, or Visual Studio 2022 17.14 or later; an older one is SQLSRC021 | `R/msbuild.md#build-requirements` | move | Placed in phase 8, which makes the heading and the anchor.  Restated on `S/install.md#requirements` and in `README.md` "Install" |
| 29 | Sentence 3, first half: the tool runs on .NET 8 or later with the SDK installed | `R/cli.md#requirements` | restated-from `R/cli.md#requirements` | `tool.md` 564 says it |
| 29 | Sentence 3, second half: it describes PostgreSQL 14 or later and SQL Server 2017 or later, and accepts Azure SQL, which is not tested | `D/index.md#supported-versions` | merge-pair MP-23 | |
| 29 | Sentence 4: Npgsql 8 or later; Microsoft.Data.SqlClient 6.1 or later; the methods need the reference; a SQL Server project on EF Core 8 or 9 references 6.1 itself | `D/index.md#supported-versions`; the EF Core clause to `D/sqlserver/types.md#limit-ef-core-sqlclient` | merge-pair MP-23 | First-hour trap: also on `S/install.md` |
| 29 | Sentence 5: the methods need a connection whose commands take the driver's own parameters | `R/query-methods.md#limit-wrapped-connection` | restated-from `R/query-methods.md#limit-wrapped-connection` | Its link is `generator.html#known-issues` today |
| 31 | h2 Getting started | `S/first-query.md`; `README.md` "Use" | move-split | Heading only.  The site owns the walkthrough (decision 2) |
| 33-58 | h3 1. Write your queries | `S/first-query.md#write-a-query` | move-split | |
| 35-51 | Sample `Queries/UserRepository.sql` | `S/first-query.md#write-a-query`; a shorter form in `README.md` "Use" | move | Use the file of `examples/PostgresExamples/Repository` where it serves |
| 53-56 | Four bullets: `-- name:` and the result shape, `-- summary:`, `@id`, `-- param:` | `S/first-query.md#write-a-query`, one sentence each; taught in `S/write-queries.md` | restated-from `R/sql-files.md#queries-and-names`, `#result-shapes`, `#summaries`, `R/parameters.md#query-parameters`, `#param-marker` | "SQL Server would say `nvarchar(100)`" goes into the SQL Server tab |
| 58 | "More:" links | The "Rules:" line of `S/first-query.md` | move | navigation |
| 60-72 | h3 2. Mark a partial type | `S/first-query.md#mark-a-partial-class` | move-split | |
| 62-70 | Sample `UserRepository.cs` | `S/first-query.md#mark-a-partial-class` | move | The SQL Server tab takes the class of `recipes.md` 416-439 |
| 72 | Paragraph: which file the type gets; it becomes a repository; the driver package; a SQL Server repository takes a `string connectionString`; an error for each query until it is described | `S/first-query.md#mark-a-partial-class`; the driver package sentence to `S/install.md#add-the-package` | restated-from `R/attributes.md#setting-source`, `R/repositories.md#repository-connection`, `R/settings.md#setting-output` | |
| 74-87 | h3 3. Set the dialect | `S/first-query.md#name-your-database` | move-split | Reframed by decision 5: the step is "Name your database" |
| 76-85 | Sample `sqlsource.json` | `S/first-query.md#name-your-database`, in a tab for each database | move | |
| 87 | Paragraph: where a settings file applies; why a dialect; the default `ansi`; "set the one named for your database, such as `postgres`, `mssql`, `mysql` or `sqlite`" | `S/first-query.md#name-your-database` | restated-from `R/settings-file.md#settings-files`, `R/settings.md#setting-dialect` | The step names `postgres` and `mssql` only.  `mysql` and `sqlite` are on `G/other-databases.md` |
| 89-103 | h3 4. Use the queries | `S/first-query.md#call-the-methods`; `S/call-queries.md` | move-split | Moves after step 5: describe, then call |
| 91 | Paragraph: the models and the method that the build generates | `S/first-query.md#call-the-methods` | restated-from `R/models.md#model-names`, `R/repositories.md#repository-methods` | |
| 93-99 | Sample: the calling code | `S/first-query.md#call-the-methods` | move | |
| 101 | Sentences 1 and 2: what each method returns; the cancellation token | `S/call-queries.md#what-a-method-returns`, `#cancellation` | restated-from `R/return-types.md#method-shapes`, `R/repositories.md#repository-signature` | |
| 101 | Sentence 3: the interface `IUserRepository` | `S/testing.md#interface` | restated-from `R/repositories.md#repository-interface` | |
| 101 | Sentences 4 and 5: a property can be null where its column can; `AS "total!"` and `-- column: total not null`; the method throws if the column is null after all | `S/write-queries.md#not-null-columns`; `G/nullability.md#not-null` | restated-from `R/model-properties.md#model-nullability`, `#column-marker` | |
| 101 | Sentence 6: a query that selects a whole table gets a model named after the table | `S/call-queries.md#models` | restated-from `R/models.md#table-models` | |
| 101 | Sentence 7: each parameter is sent as the database type it was described with | Its owner only | restated-from `R/query-methods.md#method-parameters` | Not needed before a first release, so not in `start/` |
| 101 | Sentence 8: `Async` names | `G/naming-and-placement.md#async-names` | restated-from `R/generator-parameters.md#gp-async-method-suffix` | |
| 101 | Sentence 9: a synchronous method | `G/sync-and-streaming.md#run-without-awaiting` | restated-from `R/query-methods.md#synchronous-methods` | |
| 103 | "More:" links | The "Rules:" line of `S/call-queries.md` | move | navigation |
| 105-130 | h3 5. Describe your queries | `S/first-query.md#connection`, `#describe`; `S/keep-up-to-date.md` | move-split | |
| 107 | Paragraph: the step works with PostgreSQL and SQL Server; the tool asks without running the query; the sidecar file | `S/first-query.md#describe`; `S/how-it-works.md#words` | restated-from `R/describing.md#describe`, `R/sidecars.md#sidecar-files` | |
| 109-110 | Two bullets: it checks every query against your schema; the build reads the sidecars | `S/how-it-works.md#what-needs-a-database` | restated-from `R/sidecars.md#sidecar-files` | |
| 112-116 | The `.env` paragraph and its sample | `S/first-query.md#connection` | move | The sample moves.  The rule is owned by `R/connections.md#env-file` |
| 118-124 | Running the tool, and the console sample | `S/first-query.md#describe` | move | |
| 126 | Sentences 1 and 2: `postgres` is the name of the database, the dialect's name until you name your databases | `S/first-query.md#describe`, one sentence | restated-from `R/settings.md#setting-database` | |
| 126 | Sentence 3: the tool reads the `.env` in the folder of the project or solution, never one above it, and only its `SQLSOURCE_CONNECTION` variables | `S/first-query.md#connection`, as the trap of that step | restated-from `R/connections.md#env-file` | First-hour trap |
| 126 | Sentence 4: `SqlSourceEnvFile` and `--env-file` for one `.env` at the root of a repository | `S/first-query.md#connection`, one sentence, as the fix of the trap | restated-from `R/msbuild.md#setting-env-file`, `R/connections.md#env-file` | |
| 126 | Sentence 5: the environment wins over the file; `--connection` | Its owner only | restated-from `R/connections.md#connections` | |
| 126 | Sentence 6: for SQL Server the name is `mssql`, the variable `SQLSOURCE_CONNECTION_MSSQL`, the connection string, `TrustServerCertificate=True` in a local container | `S/first-query.md#connection`, SQL Server tab | restated-from `D/sqlserver/index.md#connecting` | First-hour trap |
| 126 | Sentence 7: the tool needs your schema, not your data | `S/first-query.md#describe` | restated-from each database's `index.md#connecting` | |
| 126 | Sentences 8 and 9: commit the sidecars; the `.gitattributes` line; after a merge conflict run `describe` again | `S/keep-up-to-date.md#what-to-commit`, `#merge-conflict` | restated-from `R/sidecars.md#sidecars-in-git` | |
| 126 | Sentence 10: a project that sets `SqlSourceIncludeFiles` to `false` lists its sidecars itself | Its owner only | restated-from `R/msbuild.md#included-files` | |
| 128 | Paragraph: the build runs `describe` itself; a build with no connection, and every build in CI, reads the committed sidecars; `SqlSourceOnline`; `--watch` | `S/keep-up-to-date.md#the-build-describes`, `#in-ci` | restated-from `R/msbuild.md#running-in-the-build`, `R/describing.md#describe-on-save` | |
| 130 | Paragraph: `--force` after a schema change; `--check` on a build server; never edit a sidecar by hand; "More:" links | `S/keep-up-to-date.md#after-a-migration`, `#in-ci` | restated-from `R/describing.md#after-a-schema-change`, `#check-in-ci`, `R/sidecars.md#sidecar-files` | |
| 132-165 | h2 Only want to generate models? | `G/dapper.md#models` | merge-pair MP-22 | `README.md` keeps one bullet under "Other ways to use it" |
| 163 | Paragraph: the three rules that make Dapper read the models, and `datetime2` on SQL Server | `G/dapper.md#models-through-dapper` | merge-pair MP-12 | |
| 167-183 | h2 Just want to keep your SQL out of code files? | `G/dapper.md#sql-only`; `G/other-databases.md` | move-split | |
| 169-180 | Paragraph, sample, and the bullet "`sql` asks for each query's SQL and nothing else" | `G/dapper.md#sql-only` | move | |
| 181 | Bullet "Any dialect works" | `G/other-databases.md#dialect-and-output` | move | |
| 183 | "More:" links | The foot of `G/dapper.md#sql-only` | move | navigation.  Placed once.  Phase 9 writes the links of `G/other-databases.md` |
| 185 | h2 FAQ | None | move-split | Heading only.  Each question becomes a guide's title and a row of `index.md#find-what-you-need` |
| 187-189 | h3 How do I mock a repository? | `S/testing.md` | move | Its rules are owned by `R/repositories.md#repository-interface` (MP-05) |
| 191-193 | h3 How do I run queries in a transaction? | `S/transactions.md#queries-in-a-transaction` | merge-pair MP-13 | The sentence on `collection-method` becomes the link to `G/many-rows.md` |
| 195-208 | h3 I got an `InvalidCastException`; what does it mean and how do I fix it? | `G/troubleshooting.md#invalidcastexception` | move | Whole.  Each cause's rule is owned by the page its paragraph links.  Its link to `generator.html#known-issues` becomes `R/query-methods.md#limit-wrapped-connection` |
| 210-212 | h3 How do I change the namespace of the models? | `G/naming-and-placement.md#model-namespace-task` | move | Rules owned by `R/models.md#model-namespace` (MP-03) |
| 214-216 | h3 My project uses several databases | `G/several-databases.md#use-several-databases` | merge-pair MP-14 | |
| 218-220 | h3 How do I keep migration scripts out? | `G/folders-and-files.md#leave-migration-scripts-out` | merge-pair MP-15 | |
| 222-224 | h3 How do I choose a table name at run time? | `G/dynamic-sql.md#table-name-at-run-time` | merge-pair MP-16 | |
| 226-228 | h3 Can I pass a list to `IN`? | `G/lists.md#list-after-in` | merge-pair MP-17 | Restated on `S/write-queries.md#lists` |
| 230-232 | h3 My test project sees the internals of my main project | `G/several-projects.md#test-project-sees-internals` | merge-pair MP-18 | |
| 234-236 | h3 How do I report a wrong model? | `G/report-a-bug.md#report-a-bug` | merge-pair MP-19 | |
| 238-250 | h2 NodaTime and other libraries | `G/nodatime-and-spatial.md`; `G/json.md` | move-split | |
| 240 | Paragraph: NodaTime on PostgreSQL | `G/nodatime-and-spatial.md#nodatime-postgresql` | move | |
| 242 | Paragraph: NodaTime on SQL Server | `G/nodatime-and-spatial.md#nodatime-sql-server` | move | |
| 244 | Paragraph: NetTopologySuite, sentence 1 (PostgreSQL), sentence 2 (SQL Server, with NetTopologySuite 2.5.0 or later by name), sentence 3 (both: a shape by a type override) | `G/nodatime-and-spatial.md#nts-postgresql`, `#nts-sql-server` | move-split | Sentence 1 under `nts-postgresql`.  Sentences 2 and 3, and the closing "See" sentence with its two links, under `nts-sql-server`, in today's order.  Each is placed once, and phase 12 restates sentence 3 for PostgreSQL |
| 246 | Paragraph: the two things that catch a PostgreSQL project out (the EF Core packages; Dapper on a data source that calls `UseNodaTime()`) | `G/nodatime-and-spatial.md#ef-core-and-dapper` | move | |
| 248 | Paragraph: JSON | `G/json.md#turn-json-on` | merge-pair MP-20 | |
| 250 | Sentence: the deliveries of the two example projects show all three libraries | Foot of `G/nodatime-and-spatial.md` | move | Placed once.  Phase 12 may restate it on `G/json.md`, with the link |
| 252-288 | h2 Custom type mapping | `G/custom-types.md` | merge-pair MP-21 | Parts below |
| 254 | Paragraph 1: the C# type each column and parameter gets from its driver and libraries; what Npgsql 10 changes; a project with no driver; then what a type override does and where | `G/custom-types.md#type-override`, three sentences | restated-from `D/postgresql/types.md#type-map`, `#which-npgsql`, `D/sqlserver/types.md#types`, `#which-sqlclient`, `R/type-mapping.md#setting-type-overrides` | |
| 256-266 | Sample: `typeOverrides` | `G/custom-types.md#type-override` | move | |
| 268 | Paragraph: the type must be one the driver reads, or one your converters convert; the errors | `G/custom-types.md#type-override` | restated-from `R/type-mapping.md#setting-type-overrides`, `R/converters.md#converters`, `R/library-types-and-json.md#json-class` | Its link to the FAQ becomes `G/troubleshooting.md#invalidcastexception` |
| 270-286 | Paragraph and two samples: `columnTypes`, `-- column:`, `-- param: @author -> UserId` | `G/custom-types.md#one-column` | move | |
| 288 | Paragraph: a C# type by its short name | `G/custom-types.md#one-column` | restated-from `R/type-mapping.md#setting-type-overrides` | |
| 290-305 | h2 Where to next | `README.md` "Documentation"; `index.md#find-what-you-need` | move-split | |
| 292-303 | The table of ten rows | `README.md` "Documentation", five links; the needs are rows of `index.md#find-what-you-need` | move | navigation.  Every target survives at the address this map gives it |
| 305 | Contributing and licence | Stays | move | |

### Map: `src/SqlSource.Tool/README.md`

Phase 1 takes out the `removed-status` row, and phase 11 cuts the file to the outline of section 7.  No unit of it moves to the site.

| Lines | Old unit | New location | Disposition | Note |
|----|----|----|----|----|
| 1-3 | h1 SqlSource.Tool | Stays, three sentences | move | "It describes PostgreSQL and SQL Server queries" stays, with no version number: versions are on `D/index.md#supported-versions` |
| 5-12 | h2 Install | Stays | move-split | |
| 7 | Sentence 1: a local tool, from the root of the repository, at the version of the package | Stays | restated-from `R/cli.md#install-the-tool` | |
| 7 | Sentence 2: "Until the first release the package is a preview, so the command includes `--prerelease`" | None | removed-status | The flag stays.  The neutral sentence of `README.md` 13 replaces this one |
| 9-12 | The two commands | Stay | move | A test holds them equal to those of `S/install.md` |
| 14-50 | h2 Use | Stays, shorter | move-split | |
| 16-20 | The `.env` paragraph and sample | Stay | restated-from `R/connections.md#env-file` | |
| 22 | Paragraph: never a `.env` in a folder above; `--env-file`; the environment wins; `--connection`; where to run | One sentence stays: the tool never reads a `.env` in a folder above the project's.  The rest becomes the link to `R/connections.md` | restated-from `R/connections.md#env-file`, `#connections` | |
| 24-29 | The four `describe` commands | Stay | move | |
| 31 | Sentence: `postgres` is the name of the database | Goes; the link to `R/connections.md` covers it | restated-from `R/settings.md#setting-database` | |
| 33-36 | Four bullets, one for each command | Stay, one line each | restated-from `R/describing.md`, `R/cli.md#options` | The link `tool.html#describe-on-save` becomes `guides/describe-on-save.html` |
| 38 | Paragraph: the build runs `describe` itself; a build in CI never does | One sentence stays | restated-from `R/msbuild.md#running-in-the-build` | |
| 40-48 | `diagnose` and `replay`: the two commands and two bullets | One line and the link to `G/report-a-bug.md` | restated-from `R/bundles.md#diagnose`, `#replay` | |
| 50 | Links to the tool's manual page and the SqlSource readme | "Documentation": five links, four of them to the site (section 7) | move | navigation |

### Map: `site/README.md`

The file goes.  `index.md`, which today only redirects to `README.html`, becomes the landing page (`new`).  Phase 1 takes out "What works today".  Phase 3 moves the rest to `index.md`, word for word, as the first landing page, and removes the file.  Phase 10 writes the finished landing page, and the rows below say what it keeps.

| Lines | Old unit | New location | Disposition | Note |
|----|----|----|----|----|
| 1-3 | h1 SqlSource manual | `index.md` | move-split | |
| 3 | Sentence 1: who the manual is for | `index.md` item 1 | move | |
| 3 | Sentence 2: "If you have not used SqlSource yet, start with the repository README; this manual is the reference behind it" | None | corrected | C-06: decision 2 reverses it.  The site owns getting started.  The sentence is true until Get started is written, so it moves to `index.md` in phase 3 and goes in phase 10 |
| 3 | Sentence 3: the words are defined in concepts | None | restated-from `S/how-it-works.md#words` | navigation |
| 5-57 | h2 Find what you need | `index.md#find-what-you-need` | move-split | 49 rows, and a fiftieth that phase 2 adds for `errors.md`.  16 needs stay as rows of the landing page (section 3), from 19 of the rows.  Each other row is replaced by a sidebar title or by a row of `R/settings.md#all-settings`.  Every target of every row survives at the address this map gives it |
| 57 | The last row, "Find a problem that is not an error", with eight links to `#known-issues` | `index.md#find-what-you-need`, the row "It built, but something is wrong", to `G/troubleshooting.md` | move | The eight links become one |
| 61-105 | h2 What works today | None | removed-status | The anchor `what-works-today` retires |
| 63 | The introduction | None | removed-status | |
| 65-105 | The header and the 39 rows, with the Status column, but for the pointer of line 101 | None | removed-status | Each row's facts are stated in the section that its third column links.  The reviewer who checked the proposals against the pages confirmed it, and the no-loss reviewer of phase 1 confirms it again.  The new address of that section is in this map |
| 101 | Row "Custom type mapping", the pointer "for a PostgreSQL type that cannot be sent as a parameter, see SQLSRC232" | `R/type-mapping.md#setting-type-overrides` | move | The only pointer from type overrides to SQLSRC232.  Its table goes in phase 1, so phase 1 places it in `settings.md#setting-type-overrides`, and it moves on with that section in phase 7 |
| 107-119 | h2 The pages | `index.md#the-three-parts`; the three sidebars | move | navigation |

### Map: `site/concepts.md`

The file goes, in two phases.  There is no glossary page (decision 9).  Each term is defined once, in the reference section that documents the thing, and that section's own opening sentence is the other half of merge pair MP-10.  The term anchors retire: section 6 gives each link its new target.

- **Phase 8 moves the units.**  It places each term's text in its reference section, word for word, and the units of lines 83-85 and 119.  No term's anchor goes to a reference page: this is the one merge pair that keeps one anchor and not two.  It retargets the 28 links to a term or to `#glossary`, as section 6.3 says.  The rewrite phase of each reference page then merges the term into the section's opening.
- **The page stays until phase 10.**  The outline has `concepts.md` in the Get started sidebar until phase 10, and the root of `site/` empty of old pages after phase 8.  So phase 8 also moves the page itself, unchanged, to `site/start/concepts.md`.  From phase 8 to phase 10 a term's text stands there and in its reference section.
- **Phase 10 removes the page.**  It writes `S/how-it-works.md`, places the opening paragraph and the diagram of lines 3 to 15 there, and removes `site/start/concepts.md`.  The 18 anchors retire with the page.

| Lines | Old unit | New location | Disposition | Note |
|----|----|----|----|----|
| 1-17 | h1 Concepts | `S/how-it-works.md` | move-split | |
| 3 | Paragraph: the generator, the tool, the sidecar, what must be described | `S/how-it-works.md#picture` | move | Each sentence is also owned in reference: `R/members.md#generator`, `R/cli.md#tool`, `R/sidecars.md#sidecar-files`, `R/settings.md#setting-output` |
| 5-15 | The diagram | `S/how-it-works.md#picture` | move | |
| 17 | Sentence: "The rest of this page defines the words the manual uses" | None | restated-from `S/how-it-works.md#words` | navigation.  "Each is used in this one sense everywhere" becomes a writing rule of `site/AGENTS.md` |
| 21 | h2 Glossary | None | move-split | Heading only.  The anchor `glossary` retires |
| 23 | h3 The files | None | move-split | Group heading only |
| 27-29 | h4 Query | `R/sql-files.md#queries-and-names` | merge-pair MP-10 | Its third sentence is corrected: C-08 (section 5) |
| 33-35 | h4 Marker | `R/sql-files.md#marker` | merge-pair MP-10 | |
| 39-41 | h4 File header | `R/sql-files.md#the-file-header` | merge-pair MP-10 | |
| 45-49 | h4 Sidecar | `R/sidecars.md#sidecar-files` | merge-pair MP-10 | |
| 53-55 | h4 Settings file | `R/settings-file.md#settings-files` | merge-pair MP-10 | |
| 57 | h3 What a query gets | None | move-split | Group heading only |
| 61-63 | h4 Output | `R/settings.md#setting-output` | merge-pair MP-10 | Reworded for decision 4 |
| 67-69 | h4 Query method | `R/query-methods.md#query-methods` | merge-pair MP-10 | |
| 73-75 | h4 Repository | `R/repositories.md#repository-methods` | merge-pair MP-10 | |
| 79-81 | h4 Token method | `R/members.md#token-methods` | merge-pair MP-10 | |
| 83-85 | h3 Three kinds of parameter | `R/generator-parameters.md#three-kinds-of-parameter` | move | The reader-facing statement.  The rule "always say which" becomes a writing rule of `site/AGENTS.md` |
| 89-91 | h4 Query parameter | `R/parameters.md#query-parameters` | merge-pair MP-10 | |
| 95-97 | h4 Generator parameter | `R/generator-parameters.md#setting-generator-parameters` | merge-pair MP-10 | |
| 101-103 | h4 Token | `R/tokens.md#tokens` | merge-pair MP-10 | |
| 105 | h3 Two meanings of database | `R/settings.md#setting-database` | move-split | Heading only.  The statement is line 119 |
| 109-111 | h4 Dialect | `R/settings.md#setting-dialect` | merge-pair MP-10 | Reworded for decision 5.  `dialects.md` 15 says the same a third time |
| 115-117 | h4 Logical database | `R/settings.md#setting-database` | merge-pair MP-10 | This text is right where `settings.md` 153 is wrong: C-01 |
| 119 | Paragraph: "The two answer different questions" | `R/settings.md#setting-database` | move | The reader-facing statement of dialect against logical database.  Also a writing rule of `site/AGENTS.md` |
| 121 | h3 The packages | None | move-split | Group heading only |
| 125-127 | h4 Generator | `R/members.md#generator` | merge-pair MP-10 | |
| 131-133 | h4 Tool | `R/cli.md#tool` | merge-pair MP-10 | |
| 137-139 | h4 Describe | `R/describing.md#describe` | merge-pair MP-10 | |

### Map: `site/sql-files.md`

| Lines | Old unit | New location | Disposition | Note |
|----|----|----|----|----|
| 1-16 | h1 `.sql` files | `R/sql-files.md` | move-split | |
| 3-14 | Paragraph (UTF-8, SQLSRC271) and sample | `R/sql-files.md`, opening | move | |
| 16 | Paragraph: what a marker is | `R/sql-files.md#marker` | merge-pair MP-10 | |
| 20-47 | h2 Marker index | `R/sql-files.md#marker-index` | move | The `-- output:` row lists `repository` first (decision 4).  The `-- dialect:` row is reworded (decision 5).  The `-- column:` row links `R/model-properties.md#column-marker` and `#setting-column-types`.  Line 47 links the bare page `settings.md`: section 6 |
| 51-69 | h2 Queries and names | `R/sql-files.md#queries-and-names` | move | |
| 73-95 | h2 Result shapes | `R/sql-files.md#result-shapes` | move | The five words and their errors are owned here.  What a method returns for each is owned by `R/return-types.md#method-shapes` (`generator.md` 832-852) |
| 99-112 | h2 Summaries | `R/sql-files.md#summaries` | move | |
| 116-134 | h2 The file header | `R/sql-files.md#the-file-header` | move | |
| 138-147 | h2 A file with no `-- name:` | `R/sql-files.md#a-file-with-no-name` | move | |
| 151-160 | h2 What reaches the generated SQL | `R/sql-files.md#generated-sql` | move | |
| 164-194 | h2 Markers in the wrong place | `R/sql-files.md#markers-in-the-wrong-place` | move | Its last paragraph says what an error costs a file.  `R/errors.md#what-an-error-costs` becomes the owner of that rule, and this paragraph links it |
| 198-200 | h2 Known issues | None | move-split | The heading goes.  The anchor `known-issues` retires |
| 200 | Bullet: a `.sql` file that two projects list | `R/sidecars.md#limit-file-in-two-projects` | merge-pair MP-08 | |

### Map: `site/settings.md`

The page moves in two phases.  Phase 6 moves the sections that land in the Generated code group: lines 257-393 (the four model settings, the collection type and streaming rows), 572-619 (column types), 716-818 (where the members go, visibility, the nested class) and 914-955 (public exceptions, repository interfaces).  Phase 7 moves the rest, with the six known-issue bullets, and removes the page.

| Lines | Old unit | New location | Disposition | Note |
|----|----|----|----|----|
| 1-28 | h1 Settings | `R/settings.md` | move-split | |
| 3 | Paragraph: what a setting is | `R/settings.md`, opening | corrected | C-02: one definition of "setting" |
| 5-26 | Three samples: markers, a settings file, the attribute | `R/settings.md#places` | move | A fourth sample shows a property of the project |
| 28 | Paragraph: "Which of the three a setting accepts" | `R/settings.md#places` | corrected | C-02: five places, in a table of place, written as, applies to |
| 32-34 | h2 All settings | `R/settings.md#all-settings` | move | "for the two settings of the build and the repository interfaces, a property of the project" is corrected by C-02: five properties |
| 36-60 | h3 Settings that work today | `R/settings.md#all-settings` | move-split | |
| 36 | The heading itself | None | removed-status | |
| 38-58 | The 19 rows, columns "Setting" and "Written as" | `R/settings.md#all-settings`, the matrix: one row for each setting, one column for each place | move | The matrix has 22 rows: section 5 |
| 38-58 | The column "Values" | Each setting's own section | restated-from each `setting-<name>` section | The column goes in phase 7, with the matrix.  Before it goes, phase 7 checks each cell against its section.  The cell of row 43 lists the eleven generator parameters and their defaults, which `R/generator-parameters.md` holds and `DiagnosticsDocumentTests` checks |
| 38-58 | The column "Status" | None | removed-status | |
| 41 | The Status cell of Database: "Works today, for the tool alone" | `R/settings.md#setting-database` | corrected | C-01.  It is a fact, not status, and it is wrong.  The cell goes with its column in phase 1, so nothing of it is carried, and phase 7 makes the true statement at line 153 |
| 60 | Paragraph: a value from a list is matched ignoring case, hyphens and spaces | `R/settings.md#all-settings` | move | |
| 64-89 | h2 Which place wins | `R/settings.md#precedence` | move | |
| 93-98 | h3 A value that is wrong | `R/settings.md#a-value-that-is-wrong` | move | |
| 102-130 | h2 Output | `R/settings.md#setting-output` | move | The value table is reordered: `repository`, `methods`, `models`, `sql` (decision 4).  Lines 126-128 own "which queries must be described" |
| 134-155 | h2 Database | `R/settings.md#setting-database` | move-split | |
| 136-152 | Sample, definition, bullets 1 to 4 | `R/settings.md#setting-database` | move | |
| 153 | Bullet 5: "Only the tool reads this setting.  The build does not" | `R/settings.md#setting-database` | corrected | C-01 |
| 155 | Closing sentence and its link to `tool.md#command-reference` | `R/settings.md#setting-database` | move | The link becomes `R/connections.md#connections` |
| 159-189 | h2 Dialect | `R/settings.md#setting-dialect` | move | Reworded for decision 5.  It opens "Set it to your database: `postgres` or `mssql`", and sends every other name to `R/dialects.md`.  Line 183 links the bare page `dialects.md`: section 6 |
| 193-253 | h2 Generator parameters | `R/generator-parameters.md#setting-generator-parameters` | move | The Effect cell of each table row becomes that parameter's h3 (`gp-<name>`), so no cell is shortened |
| 257-273 | h2 Suffix of a model's name | `R/models.md#setting-model-suffix`, under `#model-names` | merge-pair MP-02 | |
| 277-294 | h2 Namespace of the models | `R/models.md#setting-model-namespace`, under `#model-namespace` | merge-pair MP-03 | |
| 298-313 | h2 Shape of a model | `R/models.md#setting-model-type`, under `#model-shape` | merge-pair MP-04 | |
| 317-331 | h2 Name of one query's model | `R/models.md#setting-model-name`, under `#model-names` | merge-pair MP-02 | |
| 335-362 | h2 Collection type | `R/return-types.md#setting-collection-type` | move | |
| 366-393 | h3 Streaming rows | `R/return-types.md#streaming-rows` | move | Its samples are also shown on `G/sync-and-streaming.md#stream-rows`.  "A row that the loop leaves unread is not checked or converted" is a rule, not status |
| 397-467 | h2 Type overrides | `R/type-mapping.md#setting-type-overrides` | move | |
| 471-526 | h3 Converters | `R/converters.md#converters` | move | |
| 530-568 | h4 A class in a JSON column | `R/library-types-and-json.md#json-class` | move | |
| 572-619 | h2 Column types | `R/model-properties.md#setting-column-types` | move | On one page with `generator.md` 430-450, the `-- column:` marker inside a query |
| 623-657 | h3 The JSON opt-in | `R/library-types-and-json.md#json-opt-in` | move | |
| 661-712 | h2 Library types | `R/library-types-and-json.md#setting-library-types` | move | Each library's type list is owned by each database's `libraries.md` |
| 716-755 | h2 Where the members go | `R/members.md#setting-member-location` | merge-pair MP-01 | |
| 759-781 | h3 Visibility | `R/members.md#setting-visibility` | move | |
| 785-818 | h2 Name of the nested class | `R/members.md#setting-nested-class` | move | |
| 822-847 | h2 Source | `R/attributes.md#setting-source` | merge-pair MP-07 | |
| 851-880 | h2 Configure one folder differently | `G/folders-and-files.md#configure-a-folder` | move | A worked example.  Its three "things to notice" restate `R/settings.md#precedence` |
| 884-910 | h2 Which `.sql` files are included | `R/msbuild.md#included-files` | move | `SqlSourceIncludeFiles` gets a row of the matrix |
| 914-926 | h2 Public exceptions | `R/exceptions.md#public-exceptions` | move | `SqlSourcePublicExceptions` gets a row of the matrix and of `R/msbuild.md#properties`.  "Not a setting of a settings file" is true and stays |
| 930-955 | h2 Repository interfaces | `R/repositories.md#repository-interfaces` | merge-pair MP-05 | Part below |
| 953 | Sentence: "The two properties of the attribute are not settings either: they belong to the type" | `R/repositories.md#repository-interfaces` | corrected | C-02: "cannot be written in a settings file or a marker: they belong to the type" |
| 959-980 | h2 Running the tool in the build | `R/msbuild.md#online-build` | merge-pair MP-06 | |
| 984-1007 | h2 The build's `.env` file | `R/msbuild.md#setting-env-file` | move | The file's format and where the tool finds it are owned by `R/connections.md#env-file` |
| 1011-1051 | h2 Settings files | `R/settings-file.md#settings-files` | move | Its link to `#known-issues` (line 1047) becomes `#limit-symbolic-link` on the same new page.  Two of its sentences are corrected in the move: the last of line 1047 (C-01) and the last two of line 1049 (C-02).  The section leaves `R/settings.md` by a call of the outline (section 9) |
| 1055-1087 | h2 Known issues | None | move-split | The heading goes.  The anchor `known-issues` retires.  Six bullets |
| 1057 | Bullet 1: a build that names the project through a symbolic link can miss a settings file or a `.sql` file | `R/settings-file.md#limit-symbolic-link` | move | |
| 1058-1068 | Bullet 2, with its sample: a `.sql` file that one of your own build targets adds is not described | `R/msbuild.md#limit-file-added-by-a-target` | move | |
| 1069 | Bullet 3: a type override's key cannot be written in brackets | `R/type-mapping.md#limit-key-in-brackets` | move | |
| 1070 | Bullet 4: a `?` inside a type argument of a generic C# type | `R/type-mapping.md#limit-nullable-type-argument` | move | |
| 1071-1086 | Bullet 5, with its sample: a type made by Vogen or StronglyTypedId needs converters | `R/converters.md#limit-vogen-and-stronglytypedid` | move | Also shown as steps on `G/custom-types.md#vogen` |
| 1087 | Bullet 6: a type has one conversion in a project | `R/converters.md#limit-one-conversion-per-type` | move | |

### Map: `site/parameters-and-tokens.md`

| Lines | Old unit | New location | Disposition | Note |
|----|----|----|----|----|
| 1-18 | h1 Parameters and tokens | `R/tokens.md#token-or-parameter` | move | The comparison of a parameter and a token.  `R/parameters.md` links it in its first paragraph |
| 22-37 | h2 Query parameters | `R/parameters.md#query-parameters` | move | |
| 41-66 | h2 Giving a parameter a type: `-- param:` | `R/parameters.md#param-marker` | move | The order in which a C# type is chosen is owned by `R/type-mapping.md#property-types`.  When an entry stops being current is owned by `R/sidecars.md#describing-again` |
| 70-98 | h2 List parameters | `R/parameters.md#list-parameters` | move | Its link to `#known-issues` (line 91) becomes `D/sqlserver/query-methods.md#limit-list-in-an-aggregate`.  Its last bullet, on the outputs `sql` and `models`, is restated on `G/dapper.md` |
| 102-113 | h3 Padding | `R/parameters.md#list-padding` | move | |
| 117-125 | h3 The limit of parameters | `R/parameters.md#list-limit` | move | |
| 129-142 | h2 Variables declared with `DECLARE` | `R/parameters.md#declared-variables` | move | |
| 146-156 | h2 Names that are read as parameters | `R/parameters.md#names-read-as-parameters` | move | Its link to `#known-issues` (line 156) becomes `#limit-parameter-taken-for-a-variable` |
| 160-185 | h2 Tokens | `R/tokens.md#tokens` | move | Where a token method goes is owned by `R/members.md#token-methods`.  Line 177 links the bare page `generator.md`: section 6 |
| 189-209 | h3 Defaults | `R/tokens.md#token-defaults` | move | |
| 213-222 | h4 `-- token:` | `R/tokens.md#token-marker` | move | |
| 226-238 | h3 Text that looks like a token: `-- token-ignore:` | `R/tokens.md#token-ignore` | move | |
| 242-254 | h3 Validation | `R/tokens.md#token-validation` | move | |
| 258-262 | h2 Known issues | None | move-split | The heading goes.  The anchor `known-issues` retires.  Three bullets |
| 260 | Bullet 1: under `mssql`, a parameter can be taken for a declared variable | `R/parameters.md#limit-parameter-taken-for-a-variable` | move | |
| 261 | Bullet 2: a user variable of MySQL or MariaDB, and a parameter of a procedure that the SQL creates, are listed as parameters | None | restated-from `R/parameters.md#names-read-as-parameters` | A pointer.  It gets a row of `G/troubleshooting.md` |
| 262 | Bullet 3: on SQL Server, a list after a `NOT` inside an aggregate of your own fails when it is empty or null | None | restated-from `D/sqlserver/query-methods.md#limit-list-in-an-aggregate` | A pointer to `sqlserver.md` 555-565 |

### Map: `site/dialects.md`

The page is kept whole as `R/dialects.md`, but for "Database support".

| Lines | Old unit | New location | Disposition | Note |
|----|----|----|----|----|
| 1-15 | h1 Dialects | `R/dialects.md`, opening | move | A new first paragraph says "set it to `postgres` or `mssql` and stop reading" (`new`).  Line 15 defines the dialect a third time: MP-10 |
| 19-38 | h2 Choosing a dialect | `R/dialects.md#choosing-a-dialect` | move | The heading becomes "Dialect names".  The anchor stays.  "Choose the dialect named for your database" becomes "Set the dialect named for your database" |
| 42-76 | h2 Options | `R/dialects.md#dialect-options` | move | |
| 80-101 | h2 What each dialect changes | `R/dialects.md#what-a-dialect-changes` | move | It has `mssql` and `postgres` columns, so it is not filed under other databases |
| 105-107 | h3 Markers under `mysql` and `mariadb` | `R/dialects.md#markers-under-mysql` | move | |
| 111-128 | h2 What is still read differently | `R/dialects.md#still-read-differently` | move | |
| 132-147 | h2 Database support | `D/index.md#database-support`; `D/other.md` | move-split | |
| 134-143 | The table of eight rows | `D/index.md#database-support` | merge-pair MP-23 | The two rows marked Yes also feed `#supported-versions` |
| 145 | Paragraph, sentences 1 to 3: every dialect is read; what "described" adds; the tool decides by the dialect | `D/index.md#database-support` | move | |
| 145 | Paragraph, sentences 4 and 5: DuckDB is read as `postgres`, cannot be described, and needs the output `sql` | `D/other.md#duckdb` | move | Restated on `G/other-databases.md#what-you-do-not-get` |
| 147 | Paragraph: SQLSRC209, and what the query still gets | `D/other.md#not-described` | move | The sentence on the default output is restated-from `R/settings.md#setting-output` |
| 151-153 | h2 Known issues | None | move-split | The heading goes.  The anchor `known-issues` retires |
| 153 | Bullet: constructs that are read differently are listed above | None | restated-from `R/dialects.md#still-read-differently` | A pointer.  The section gets rows of `G/troubleshooting.md` |

### Map: `site/generator.md`

| Lines | Old unit | New location | Disposition | Note |
|----|----|----|----|----|
| 1-32 | h1 The generator | `R/members.md#generator` | move-split | |
| 3 | Paragraph: what the generator writes for each output; what it needs (.NET 8 or later, C# 12 or later, SQLSRC003, SQLSRC012) | `R/members.md#generator` | merge-pair MP-10 | The other half is the term "Generator".  The sentence on .NET and C# is also stated by `R/msbuild.md#build-requirements`, its owner.  Line 3 links the bare page `tool.md`: section 6 |
| 5-30 | Two samples and the paragraph that explains them | `R/members.md#generator` | move | |
| 32 | Paragraph: what the same queries give under the output `models` | `R/members.md#generator` | move | Its link to `recipes.md#dapper-and-adonet` becomes `G/dapper.md#dapper-and-adonet` |
| 36-57 | h2 The attribute | `R/attributes.md#attribute` | move | Its link to `#known-issues` (line 38) becomes `#limit-embedded-attribute`.  Line 38 lists what the package adds, which `R/attributes.md#what-the-package-adds` owns |
| 61-77 | h2 Which files belong to a type | `R/attributes.md#files-of-a-type` | merge-pair MP-07 | Line 77 links the bare page `sql-files.md`: section 6 |
| 81-116 | h2 Where the members go | `R/members.md#locations` | merge-pair MP-01 | |
| 120-156 | h3 A placement for a whole file or folder | `R/members.md#placement-for-a-file` | merge-pair MP-01 | |
| 160-175 | h2 Constants | `R/members.md#constants` | move | Not on `G/dapper.md`: a constant serves any library and any database |
| 179-188 | h2 Token methods | `R/members.md#token-methods` | move | The signature and where the method goes.  A token's rules are owned by `R/tokens.md` |
| 192-231 | h2 Documentation in IntelliSense | `R/members.md#intellisense` | move | It is about generated C# members, not about the file format |
| 235-268 | h2 Models | `R/models.md#models` | move | |
| 272-287 | h3 Which queries get models | `R/models.md#which-models` | move | |
| 291-306 | h3 Names | `R/models.md#model-names` | merge-pair MP-02 | |
| 310-312 | h3 Namespace | `R/models.md#model-namespace` | merge-pair MP-03 | |
| 316-322 | h3 Accessibility | `R/models.md#model-accessibility` | move | |
| 326-352 | h3 Shape | `R/models.md#model-shape` | merge-pair MP-04 | Its link to `settings.md#known-issues` (line 352) becomes `R/type-mapping.md#limit-nullable-type-argument` |
| 356-360 | h3 The order of the properties | `R/model-properties.md#property-order` | move | |
| 364-381 | h3 Property names | `R/model-properties.md#property-names` | move | |
| 385-409 | h3 Property types | `R/type-mapping.md#property-types` | move | The one statement of the order in which a C# type is chosen.  One home, not two |
| 413-426 | h3 Which properties can be null | `R/model-properties.md#model-nullability` | move | Why a database reports a column as possibly null is owned by each database's `nullability.md` |
| 430-450 | h4 Setting a column's nullability or type with a marker | `R/model-properties.md#column-marker` | move | Whole: nullability and type are one marker.  Its last bullet, on the file header, is owned by `#setting-column-types` on the same page |
| 454-460 | h3 Documentation | `R/models.md#model-documentation` | move | |
| 464-492 | h3 Table models | `R/models.md#table-models` | move | |
| 496-514 | h3 Shared models | `R/models.md#shared-models` | move | |
| 518-527 | h3 Names that cannot be used | `R/models.md#unusable-model-names` | move | |
| 531-556 | h2 Query methods | `R/query-methods.md#query-methods` | merge-pair MP-10 | The other half is the term "Query method" |
| 560-571 | h3 Which queries get a method | `R/query-methods.md#which-methods` | move | |
| 575-613 | h3 Repository methods | `R/repositories.md#repository-methods` | merge-pair MP-10 | The other half is the term "Repository" |
| 617-639 | h4 The signature | `R/repositories.md#repository-signature` | move | |
| 643-704 | h4 The connection | `R/repositories.md#repository-connection` | move | The bullet "Several databases" (line 687) is the build's use of the database setting: C-01.  Its link to `#known-issues` (line 702) becomes `D/postgresql/query-methods.md#limit-connection-string-repository`.  `[SqlSourceConnection]` gets a row of the matrix |
| 708-731 | h4 The interface | `R/repositories.md#repository-interface` | merge-pair MP-05 | |
| 735-739 | h4 Transactions and timeouts | `R/repositories.md#repository-transactions` | move | Restated on `S/transactions.md` |
| 743-774 | h3 Methods that take a connection | `R/connection-methods.md#methods-that-take-a-connection` | move | |
| 778-791 | h4 Where the methods go | `R/connection-methods.md#method-location` | move | |
| 795-820 | h4 The signature | `R/connection-methods.md#method-signature` | move | |
| 824-828 | h4 The connection | `R/connection-methods.md#method-connection` | move | The bold words "Give the method the transaction" (line 828) are quoted by a test comment: section 8 |
| 832-852 | h3 What each shape returns | `R/return-types.md#method-shapes` | move | Its link to `#known-issues` (line 850) becomes `D/sqlserver/query-methods.md#limit-rowcount-after-nocount` |
| 856-902 | h3 Collection methods | `R/collection-methods.md#collection-methods` | move | |
| 906-926 | h4 Transactions | `R/collection-methods.md#collection-transactions` | move | |
| 930-935 | h4 What it throws | `R/collection-methods.md#collection-failures` | move | |
| 939-1000 | h3 Synchronous methods | `R/query-methods.md#synchronous-methods` | move | |
| 1004-1054 | h4 Synchronous repository methods | `R/repositories.md#synchronous-repository-methods` | move | Moves apart from its parent h3 |
| 1058-1066 | h3 How a row is read | `R/query-methods.md#reading-rows` | move | What holds for every database.  Each database's part stays on its `query-methods.md#method-results` |
| 1070-1076 | h3 How a parameter is sent | `R/query-methods.md#method-parameters` | move | The same division |
| 1080-1097 | h3 `UnexpectedRowCountException` | `R/exceptions.md#unexpected-row-count` | move | Its link to `#known-issues` (line 1097) becomes `#limit-caught-by-type` |
| 1101-1121 | h3 `TransactionRolledBackException` | `R/exceptions.md#transaction-rolled-back` | move | The same for line 1121 |
| 1125-1144 | h3 `CollectionItemException` | `R/exceptions.md#collection-item-exception` | move | The same for line 1144 |
| 1148-1176 | h2 Projects that share internals | `R/exceptions.md#projects-that-share-internals` | move | |
| 1180-1190 | h2 Known issues | None | move-split | The heading goes.  The anchor `known-issues` retires.  Nine bullets |
| 1182 | Bullet 1: the tool reads the attribute without compiling your code, so `Source` must be a string written out | `R/attributes.md#limit-source-written-as-text` | move | |
| 1183 | Bullet 2: an input model binds through Dapper only when the query writes its parameters as the model's properties are named | `G/dapper.md#limit-parameter-names` | merge-pair MP-12 | |
| 1184 | Bullet 3: the three exceptions are caught by their type only where they are seen | `R/exceptions.md#limit-caught-by-type` | move | |
| 1185 | Bullet 4: a `cref` to the exceptions can fail in a project that uses another project's | `R/exceptions.md#limit-cref` | move | |
| 1186 | Bullet 5: a query method with parameters works only on a connection whose commands take its driver's own parameters | `R/query-methods.md#limit-wrapped-connection` | move | The owner.  `postgresql.md` 529 and `sqlserver.md` 553 point here |
| 1187 | Bullet 6: a collection method works fully only on the driver's own connection | `R/collection-methods.md#limit-wrapped-connection` | move | |
| 1188 | Bullet 7: a PostgreSQL repository whose source is a connection string reads no plugin's types | `D/postgresql/query-methods.md#limit-connection-string-repository` | merge-pair MP-09 | `R/repositories.md#repository-connection` links it |
| 1189 | Bullet 8: on SQL Server, `rowcount` returns `-1` once `SET NOCOUNT ON` has run on the connection's session | `D/sqlserver/query-methods.md#limit-rowcount-after-nocount` | move | The owner's text.  `sqlserver.md` 551 points here today |
| 1190 | Bullet 9: a project that declares `Microsoft.CodeAnalysis.EmbeddedAttribute` itself | `R/attributes.md#limit-embedded-attribute` | move | |

### Map: `site/tool.md`

The lines 96, 100 and 175 of the heading inventory are comments inside code blocks, not headings.  They move with their blocks.

| Lines | Old unit | New location | Disposition | Note |
|----|----|----|----|----|
| 1-14 | h1 The tool | `R/cli.md#tool` | move-split | |
| 3-8 | Paragraph and sample: what the tool is and what `describe` does | `R/cli.md#tool` | merge-pair MP-10 | The other half is the term "Tool" |
| 10-12 | Two paragraphs: which queries need the tool; what the generator builds from the sidecars | `R/cli.md#tool` | restated-from `R/settings.md#setting-output`, `R/describing.md#which-queries` | "A project whose queries all have the output `sql` needs no tool" stays in `R/cli.md#install-the-tool` |
| 14 | Paragraph: "This page has two parts" | The opening links of `R/cli.md` | move | navigation |
| 18-29 | h2 Install the tool | `R/cli.md#install-the-tool` | merge-pair MP-11 | Restated on `S/install.md#install-the-tool-step`.  Part below |
| 20 | Sentence: "Until the first release the package is a preview, so the command includes `--prerelease`" | None | removed-status | The flag stays.  The neutral sentence of `README.md` 13 replaces this one on `S/install.md` |
| 33-50 | h2 Which queries are described | `R/describing.md#which-queries` | move | "Output decides which queries must be described" is owned by `R/settings.md#setting-output`.  Its link to `generator.md#known-issues` (line 48) becomes `R/attributes.md#limit-source-written-as-text`.  "`database` by the tool alone" (line 50) is about checking the value and is true |
| 54-89 | h2 Connections | `R/connections.md#connections` | move | Its link to `#known-issues` (line 89) becomes `#limit-short-password` |
| 93-127 | h3 A `.env` file | `R/connections.md#env-file` | move | First-hour trap: the file in the project's folder, never one above.  Restated on `S/first-query.md#connection` |
| 131-168 | h2 Sidecar files | `R/sidecars.md#sidecar-files` | move | Line 133 is the other half of the term "Sidecar": MP-10.  Two unique sentences of the `diagnostics.md` preamble (line 315) join this section |
| 172-187 | h3 Sidecars in git | `R/sidecars.md#sidecars-in-git` | move | Restated on `S/keep-up-to-date.md` |
| 191-206 | h2 Describing again | `R/sidecars.md#describing-again` | move | The owner of "when an entry stops being current".  `parameters-and-tokens.md` 41-66 and `sql-files.md` 73-95 restate parts of it |
| 210-246 | h2 Running in the build | `R/msbuild.md#running-in-the-build` | merge-pair MP-06 | Its two links to `#known-issues` (lines 238, 246) become `R/describing.md#limit-several-target-frameworks` and `R/connections.md#limit-short-password`.  "Have not been checked" (line 225) is a support fact and stays.  Part below |
| 235 | Sentence: "only the tool reads that setting", of `database` | `R/msbuild.md#running-in-the-build` | corrected | C-01: "only the tool checks that value" |
| 250-259 | h2 After a schema change | `R/describing.md#after-a-schema-change` | move | Restated on `S/keep-up-to-date.md#after-a-migration` |
| 263-313 | h2 Describe on save | `R/describing.md#describe-on-save` | move | "Checked with Rider on macOS alone" (line 313) is a support fact and stays.  Its link to `#known-issues` becomes `#limit-saves-not-seen` |
| 317-330 | h3 What your editor shows | `R/describing.md#what-your-editor-shows` | move | "This has not been checked" (line 330) stays |
| 334-351 | h2 Check in CI | `R/describing.md#check-in-ci` | move | The sample job is on `G/ci.md` |
| 355-369 | h2 The summary | `R/cli.md#summary` | move | |
| 373-390 | h2 Reporting a wrong model | `G/report-a-bug.md#report-a-bug` | merge-pair MP-19 | The procedure.  The options it names are owned by `R/bundles.md#diagnose` |
| 394-405 | h3 What is renamed before you share it | `R/bundles.md#bundle-contents` | move | Restated in short on `G/report-a-bug.md#what-is-safe`, so the reader who decides what to post stays on one page |
| 409-437 | h3 When a bundle cannot be made safe | `G/report-a-bug.md#raw-bundle` | move | A privacy procedure, so it is on the guide.  The `See:` lines of SQLSRC293 and SQLSRC294 follow it |
| 441-456 | h2 Command reference | `R/cli.md#command-reference` | move | |
| 460-472 | h3 Arguments | `R/cli.md#arguments` | move | |
| 476-492 | h3 Options | `R/cli.md#options` | move | |
| 496-506 | h3 Environment variables | `R/cli.md#environment-variables` | move | |
| 510-518 | h3 Exit codes | `R/cli.md#exit-codes` | move | `src/SqlSource.Tool/AGENTS.md` links this anchor and has 33 bytes of room |
| 522-536 | h3 `--verbose` | `R/cli.md#verbose` | move | |
| 540-558 | h3 Logs | `R/cli.md#logs` | move | Its link to `#known-issues` (line 552) becomes `R/connections.md#limit-short-password` |
| 562-567 | h3 Requirements | `R/cli.md#requirements` | move | Bullet 4 links each database's `#connecting` for versions; it links `D/index.md#supported-versions` instead |
| 571-589 | h3 Making a bundle with `diagnose` | `R/bundles.md#diagnose` | move | |
| 593-625 | h3 Checking a bundle with `replay` | `R/bundles.md#replay` | move | |
| 629-638 | h2 Known issues | None | move-split | The heading goes.  The anchor `known-issues` retires.  Eight bullets |
| 631 | Bullet 1: a project path or a `.sql` path through a symbolic link is not found | `R/cli.md#limit-path-through-a-symbolic-link` | move | |
| 632 | Bullet 2: a project with several target frameworks is read for its first framework alone | `R/describing.md#limit-several-target-frameworks` | move | `R/msbuild.md#running-in-the-build` links it |
| 633 | Bullet 3: a `.sql` file that one of your own build targets adds is not described | None | restated-from `R/msbuild.md#limit-file-added-by-a-target` | A pointer to `settings.md` 1058-1068, which already says "nor by the build of a project with several target frameworks" |
| 634 | Bullet 4: a `.sql` file that two projects list, when only one of them finds a settings file of it | `R/sidecars.md#limit-file-in-two-projects` | merge-pair MP-08 | |
| 635 | Bullet 5: a save in an editor other than Rider on macOS may not start a pass of `describe --watch` | `R/describing.md#limit-saves-not-seen` | move | "Have not been checked" is a support fact and stays |
| 636 | Bullet 6: in Visual Studio and VS Code, the recipes and what the editor shows have not been checked | `R/describing.md#limit-editors-not-checked` | move | Restated on `G/describe-on-save.md`, whose two recipes keep their own "has not been checked" sentences |
| 637 | Bullet 7: a few changes to folders far from your project start no pass | `R/describing.md#limit-folder-changes-not-seen` | move | |
| 638 | Bullet 8: a short password, or a connection string typed in the wrong place, can appear in the output or a log | `R/connections.md#limit-short-password` | move | |

### Map: `site/postgresql.md`

`P/` is `site/reference/databases/postgresql/`.

| Lines | Old unit | New location | Disposition | Note |
|----|----|----|----|----|
| 1-18 | h1 PostgreSQL | `P/index.md`, opening | move | Line 3 links the bare page `dialects.md` twice: section 6 |
| 22-27 | h2 Connecting | `P/index.md#connecting` | move-split | |
| 24 | Bullet: the connection string is Npgsql's; SQLSRC224 | `P/index.md#connecting` | move | |
| 25 | Bullet: PostgreSQL 14 and later are supported, and SqlSource is tested on every major from 14 to 18; an older server is SQLSRC235 | `D/index.md#supported-versions` | merge-pair MP-23 | The bullet leaves `P/index.md#connecting` whole.  Phase 4 writes one new bullet in its place, which links the table and names SQLSRC235 for an older server |
| 26-27 | Bullets: the tool needs your schema, not your data; connect directly or through a pooler | `P/index.md#connecting` | move | |
| 31-35 | h2 What describing does to your database | `P/index.md#what-describing-does` | move | It already says that a function marked `IMMUTABLE` can run |
| 39-57 | h2 Parameters | `P/index.md#parameters` | move | |
| 61-72 | h2 What can be described | `P/index.md#what-can-be-described` | move | |
| 76-148 | h2 Which columns can be null | `P/nullability.md#nullability` | move | Its five commonest causes are restated on `G/nullability.md#on-postgresql` |
| 152-163 | h2 Types | `P/types.md#type-map` | move | |
| 167-174 | h3 Which version of Npgsql | `P/types.md#which-npgsql` | move | What each version changes stays here.  "Npgsql 8 or later" is restated-from `D/index.md#supported-versions` |
| 178-233 | h3 The types | `P/types.md#type-table` | move | |
| 237-247 | h3 What to know about some types | `P/types.md#type-notes` | move | |
| 251-290 | h3 NodaTime | `P/libraries.md#nodatime` | move | |
| 294-323 | h3 NetTopologySuite | `P/libraries.md#nettopologysuite` | move | |
| 327-375 | h3 JSON | `P/libraries.md#json` | move | |
| 379-405 | h2 Models | `P/index.md#models` | move | A list of what PostgreSQL decides in a model.  A column with no name is SQLSRC253, from the build |
| 409-427 | h2 Query methods | `P/query-methods.md#query-methods` | move | Its link to `#known-issues` (line 427) becomes `#limit-connection-string-repository` |
| 431-441 | h3 How a parameter is sent | `P/query-methods.md#method-parameters` | move | |
| 445-465 | h3 Array parameters | `P/query-methods.md#array-parameters` | move | Not a pair with SQL Server's "Lists": it is `= ANY(@ids)` with one array.  `G/lists.md#postgresql-arrays` says how the two differ |
| 469-493 | h3 What a method reads and throws | `P/query-methods.md#method-results` | move | The bullet of line 473 stays whole on this page.  Its clause "SqlSource is tested on Npgsql 8 and 10" is the source of a cell of the table of `D/index.md#supported-versions`, and stands in the worksheet of MP-23.  The bold words of line 490 are quoted by a test comment: section 8.  Its link to `generator.md#known-issues` (line 493) becomes `R/query-methods.md#limit-wrapped-connection` |
| 497-518 | h3 Collection methods | `P/query-methods.md#collection-methods` | move | |
| 522-529 | h2 Known issues | None | move-split | The heading goes.  The anchor `known-issues` retires.  Six bullets |
| 524 | Bullet 1: many columns that are never null are treated as possibly null | `P/nullability.md#limit-columns-treated-as-nullable` | move | |
| 525 | Bullet 2: an operator `=` of your own on `oid` or `"char"` stops the tool from reading the database | `P/index.md#limit-operator-on-oid` | move | |
| 526 | Bullet 3: a query over a table with many partitions is slow to describe | `P/index.md#limit-many-partitions` | move | |
| 527 | Bullet 4: a function marked `IMMUTABLE` can run while a query is described | None | restated-from `P/index.md#what-describing-does` | A pointer.  It gets a row of `G/troubleshooting.md` |
| 528 | Bullet 5: a repository whose connection source is a connection string reads and sends no type that needs a plugin or a mapping | `P/query-methods.md#limit-connection-string-repository` | merge-pair MP-09 | |
| 529 | Bullet 6: a query method fails on a connection that wraps Npgsql's and creates parameters of its own | None | restated-from `R/query-methods.md#limit-wrapped-connection` | A pointer |

### Map: `site/sqlserver.md`

`M/` is `site/reference/databases/sqlserver/`.

| Lines | Old unit | New location | Disposition | Note |
|----|----|----|----|----|
| 1-20 | h1 SQL Server | `M/index.md`, opening | move | Line 3 links the bare page `dialects.md` twice: section 6.  Line 20, on `DECLARE`, has no twin on the PostgreSQL page |
| 24-30 | h2 Connecting | `M/index.md#connecting` | move-split | |
| 26-27 | Bullets: the connection string is Microsoft.Data.SqlClient's; `TrustServerCertificate=True` | `M/index.md#connecting` | move | First-hour trap: restated in the SQL Server tab of `S/first-query.md#connection` |
| 28 | Bullet: SQL Server 2017 and later are supported, tested on 2017, 2019, 2022 and 2025; an older server is SQLSRC235; Azure SQL Database and Azure SQL Managed Instance are accepted whatever version they report, and are not tested | `D/index.md#supported-versions`, `#azure-sql` | merge-pair MP-23 | The bullet leaves `M/index.md#connecting` whole: its first two sentences go beside the table and its third under `#azure-sql`.  Phase 4 writes one new bullet in its place, which links the table and `#azure-sql` and names SQLSRC235 for an older server.  "Not tested" stays |
| 29-30 | Bullets: Microsoft Entra ID cannot be used ("which SqlSource is not tested with" stays); the tool needs your schema, not your data | `M/index.md#connecting` | move | First-hour trap for Entra ID: one sentence in the SQL Server tab of `S/first-query.md#connection` |
| 34-36 | h2 What describing does to your database | `M/index.md#what-describing-does` | move | |
| 40-64 | h2 Parameters | `M/index.md#parameters` | move | |
| 68-90 | h2 What can be described | `M/index.md#what-can-be-described` | move | |
| 94-142 | h2 Which columns can be null | `M/nullability.md#nullability` | move | Its link to `#known-issues` (line 118) becomes `#limit-ansi-warnings-off` |
| 146-157 | h2 Types | `M/types.md#types` | move | |
| 161-168 | h3 Which version of Microsoft.Data.SqlClient | `M/types.md#which-sqlclient` | move | Its link to `#known-issues` (line 166) becomes `#limit-ef-core-sqlclient`.  "6.1 or later" is restated-from `D/index.md#supported-versions` |
| 172-201 | h3 The types | `M/types.md#type-table` | move | |
| 205-213 | h3 What to know about some types | `M/types.md#type-notes` | move | |
| 217-269 | h3 NodaTime | `M/libraries.md#nodatime` | move | |
| 273-319 | h3 NetTopologySuite | `M/libraries.md#nettopologysuite` | move | Its link to `generator.md#known-issues` (line 312) becomes `R/query-methods.md#limit-wrapped-connection` |
| 323-365 | h3 JSON | `M/libraries.md#json` | move | |
| 369-393 | h2 Models | `M/index.md#models` | move | A column with no name is SQLSRC266, from the tool: not the same as on PostgreSQL |
| 397-415 | h2 Query methods | `M/query-methods.md#query-methods` | move | |
| 419-438 | h3 How a parameter is sent | `M/query-methods.md#method-parameters` | move | |
| 442-468 | h3 Lists | `M/query-methods.md#list-parameters` | move | Its two links to `#known-issues` (lines 451, 452) become `#limit-list-in-an-aggregate` and `#limit-ansi-nulls-off` |
| 472-488 | h3 What a method reads and throws | `M/query-methods.md#method-results` | move | Its three links (lines 484, 485, 488) become `#limit-hidden-rollback`, `#limit-rowcount-after-nocount` and `R/query-methods.md#limit-wrapped-connection` |
| 492-516 | h3 Collection methods | `M/query-methods.md#collection-methods` | move | |
| 520-571 | h2 Known issues | None | move-split | The heading goes.  The anchor `known-issues` retires.  Twelve bullets |
| 522-528 | Bullet 1, with its sample: a project that uses EF Core 8 or 9 references Microsoft.Data.SqlClient 5.1 and gets SQLSRC267 | `M/types.md#limit-ef-core-sqlclient` | move | First-hour trap: restated with the `PackageReference` in the SQL Server tab of `S/install.md#add-the-package`, and in `S/errors.md#first-errors` |
| 530 | Bullet 2: some columns that are never null get a property that can be null | `M/nullability.md#limit-columns-treated-as-nullable` | move | |
| 532 | Bullet 3: a connection that turns `ANSI_WARNINGS` off can read null from a column whose property cannot be null | `M/nullability.md#limit-ansi-warnings-off` | move | |
| 534 | Bullet 4: a connection that turns `ANSI_NULLS` off changes what an empty list matches | `M/query-methods.md#limit-ansi-nulls-off` | move | |
| 536-547 | Bullet 5, with its sample: a transaction can be rolled back by an error hidden behind an error raised before the rows | `M/query-methods.md#limit-hidden-rollback` | move | |
| 549 | Bullet 6: a query that returns several result sets gets a model of the first alone | `M/index.md#limit-several-result-sets` | move | |
| 551 | Bullet 7: a `rowcount` method returns `-1` once `SET NOCOUNT ON` has run | None | restated-from `M/query-methods.md#limit-rowcount-after-nocount` | A pointer to `generator.md` 1189, whose text becomes the limit on this database's page |
| 553 | Bullet 8: a query method fails on a connection that wraps a `SqlConnection` and creates parameters of its own | None | restated-from `R/query-methods.md#limit-wrapped-connection` | A pointer |
| 555-565 | Bullet 9, with its sample: a list after a `NOT` inside an aggregate of your own fails when it is empty or null | `M/query-methods.md#limit-list-in-an-aggregate` | move | The owner.  `parameters-and-tokens.md` 262 points here |
| 567 | Bullet 10: a `-- param:` type whose name holds a character that Unicode added or reclassified after version 3.2 | `M/index.md#limit-type-name-unicode` | move | |
| 569 | Bullet 11: a parameter whose name holds such a character | `M/index.md#limit-parameter-name-unicode` | move | |
| 571 | Bullet 12: a parameter whose name is written in two ways that SQL Server reads as one | `M/index.md#limit-parameter-two-spellings` | move | |

### Map: `site/recipes.md`

The file goes.  Each recipe is a sample, so it opens the guide that needs it.  Section 8 lists the samples that tests mirror.  Phase 5 moves the three recipes of the tool, lines 125-254 (check the sidecars in CI, describe again after a schema change, start the watcher with your editor).  Phase 8 moves the rest and removes the page.

| Lines | Old unit | New location | Disposition | Note |
|----|----|----|----|----|
| 1-23 | h1 Recipes | None | move | navigation: the list of 19 links is the Guides sidebar |
| 27-65 | h2 Read one folder with another dialect | `G/several-databases.md#folder-with-another-dialect` | merge-pair MP-14 | |
| 69-89 | h2 Leave migration scripts out | `G/folders-and-files.md#leave-migration-scripts-out` | merge-pair MP-15 | |
| 93-121 | h2 Use several databases | `G/several-databases.md#use-several-databases` | merge-pair MP-14 | |
| 125-181 | h2 Check the sidecars in CI | `G/ci.md#check-sidecars-in-ci` | move | The rules are owned by `R/describing.md#check-in-ci` |
| 185-196 | h2 Describe again after a schema change | `S/keep-up-to-date.md#after-a-migration` | move | The sample.  The rule is owned by `R/describing.md#after-a-schema-change` |
| 200-202 | h2 Start the watcher with your editor | `G/describe-on-save.md#start-the-watcher-with-your-editor` | move | |
| 204-241 | h3 VS Code | `G/describe-on-save.md#vs-code` | move | An h2 with a new anchor, never a tab.  `EditorRecipeTests` reads its task.  "This recipe has not been checked in VS Code" (line 241) is a support fact and stays |
| 243-248 | h3 Rider | `G/describe-on-save.md#rider` | move | |
| 250-254 | h3 Visual Studio | `G/describe-on-save.md#visual-studio` | move | "This has not been checked in Visual Studio" (line 252) stays |
| 258-300 | h2 Run queries in a transaction | `S/transactions.md#queries-in-a-transaction` | merge-pair MP-13 | |
| 304-325 | h2 Insert many rows in one call | `G/many-rows.md#insert-many-rows` | move | |
| 329-369 | h2 Run a query from code that does not await | `G/sync-and-streaming.md#run-without-awaiting` | move | |
| 373-412 | h2 Give a repository its connection from a service of your own | `G/repository-connection.md#connection-from-a-provider` | move | Its link to `generator.md#known-issues` (line 410) becomes `D/postgresql/query-methods.md#limit-connection-string-repository` |
| 416-439 | h2 Make a SQL Server repository from a connection string | `G/repository-connection.md#sql-server-repository` | move | The class is also the SQL Server tab of `S/first-query.md#mark-a-partial-class` |
| 443-493 | h2 Give an id a type of its own | `G/custom-types.md#strongly-typed-id` | merge-pair MP-21 | |
| 497-587 | h2 Read a class of your own from a JSON column | `G/json.md#class-in-a-json-column` | merge-pair MP-20 | |
| 591-639 | h2 Use the queries with Dapper or your own ADO.NET code | `G/dapper.md#dapper-and-adonet` | merge-pair MP-22 | |
| 643-702 | h2 Run the models through Dapper | `G/dapper.md#models-through-dapper` | merge-pair MP-12 | Its link to `generator.md#known-issues` (line 702) becomes `#limit-parameter-names` on the same page.  "Each database needs more, below" stays true: both sections are on the page |
| 706-785 | h3 With PostgreSQL | `G/dapper.md#dapper-with-postgresql` | move | An h2 on the guide.  Mirrored by tests: section 8 |
| 789-920 | h3 With SQL Server | `G/dapper.md#dapper-with-sql-server` | move | An h2 on the guide.  Mirrored by tests: section 8 |
| 924-939 | h2 Choose a table name at run time | `G/dynamic-sql.md#table-name-at-run-time` | merge-pair MP-16 | |
| 943-961 | h2 Filter by an optional list of values | `G/lists.md#filter-by-a-list` | merge-pair MP-17 | |
| 965-990 | h2 Let a test project see the internals of the main project | `G/several-projects.md#test-project-sees-internals` | merge-pair MP-18 | |
| 994-1030 | h2 Share the exceptions between two data projects and an app | `G/several-projects.md#share-the-exceptions` | merge-pair MP-18 | Mirrored by a test: section 8 |

### The 46 known-issue bullets become 36 limits

Of the 46 bullets, 8 only point at another bullet or at a section, and 2 pairs say one thing twice.  So 36 limits remain, each with an anchor of its own.  `G/troubleshooting.md` has one entry for each, under the words in the second column.

No limit waits for a rewrite.  The phase that moves a page places each known-issue bullet of that page:

- The bullet goes under `## Limits` of the page in the first column, word for word, as an h3 with the anchor shown.  The words of the h3 heading are new text of that phase.
- The same phase adds the limit's entry to `G/troubleshooting.md`, which phase 3 creates.
- The phase creates the page if it does not exist yet: `R/sidecars.md` in phase 3, `G/dapper.md` in phase 6.  Section 10 lists every unit, a limit or not, that lands on a page which another phase fills.
- "3, then 5" marks a merge pair: the first phase makes the limit from its bullet, and the second places the other bullet beside it.
- A pointer bullet goes in the phase that moves its page, also when the bullet it points at moves later.  `sqlserver.md` 551 goes in phase 4, and the limit it points at is made in phase 6 from `generator.md` 1189.
- Until the phase that makes a limit, a link to it names the `#known-issues` anchor of the old page that still holds its bullet.  That phase retargets the link.  So no `known-issues` anchor outlives its page, and no link is dead between two phases.

The rewrite phase of the page then writes each limit as what the reader sees, why, and what to do.  By phase: 2 limits in phase 3, 14 in phase 4, 6 in phase 5, 8 in phase 6 and 6 in phase 7.

| Limit | What the reader sees | From | Moved in |
|----|----|----|----|
| `R/sidecars.md#limit-file-in-two-projects` | Entries vanish from a sidecar, or SQLSRC245, when two projects include one `.sql` file | `sql-files.md` 200; `tool.md` 634 | 3, then 5 |
| `R/settings-file.md#limit-symbolic-link` | A settings file or a `.sql` file is ignored when the project is built through a symbolic link | `settings.md` 1057 | 7 |
| `R/msbuild.md#limit-file-added-by-a-target` | SQLSRC241 for a `.sql` file that a build target of your own adds | `settings.md` 1058-1068; `tool.md` 633 | 7 |
| `R/type-mapping.md#limit-key-in-brackets` | SQLSRC019 or SQLSRC111 for a key written `[dbo].[Email]` | `settings.md` 1069 | 7 |
| `R/type-mapping.md#limit-nullable-type-argument` | A compiler warning in a model for a `?` inside a type argument | `settings.md` 1070 | 7 |
| `R/converters.md#limit-vogen-and-stronglytypedid` | `InvalidCastException` for an id type made by Vogen or StronglyTypedId | `settings.md` 1071-1086 | 7 |
| `R/converters.md#limit-one-conversion-per-type` | One C# type cannot be converted two ways in two files | `settings.md` 1087 | 7 |
| `R/parameters.md#limit-parameter-taken-for-a-variable` | A parameter is missing from the input model after a `DECLARE` with no `;` | `parameters-and-tokens.md` 260 | 3 |
| `R/attributes.md#limit-source-written-as-text` | SQLSRC208, and nothing described for a type whose `Source` is a constant or `nameof` | `generator.md` 1182 | 6 |
| `G/dapper.md#limit-parameter-names` | Dapper sends no value for `@account_id` | `generator.md` 1183 | 6 |
| `R/exceptions.md#limit-caught-by-type` | `catch (UnexpectedRowCountException)` does not catch, or SQLSRC022 | `generator.md` 1184 | 6 |
| `R/exceptions.md#limit-cref` | A compiler warning at a `cref` that names one of the exceptions | `generator.md` 1185 | 6 |
| `R/query-methods.md#limit-wrapped-connection` | `InvalidCastException` from a method with parameters on a connection that wraps the driver's | `generator.md` 1186; `postgresql.md` 529; `sqlserver.md` 553 | 6 |
| `R/collection-methods.md#limit-wrapped-connection` | `NotSupportedException`, "Specified method is not supported.", from a collection method | `generator.md` 1187 | 6 |
| `R/attributes.md#limit-embedded-attribute` | A compile error about `Microsoft.CodeAnalysis.EmbeddedAttribute` | `generator.md` 1190 | 6 |
| `R/cli.md#limit-path-through-a-symbolic-link` | SQLSRC207 or SQLSRC212 for a path that exists | `tool.md` 631 | 5 |
| `R/describing.md#limit-several-target-frameworks` | SQLSRC241 for a query that only a second target framework claims | `tool.md` 632 | 5 |
| `R/describing.md#limit-saves-not-seen` | Saving a file starts no pass of `describe --watch` | `tool.md` 635 | 5 |
| `R/describing.md#limit-editors-not-checked` | In Visual Studio or VS Code, the models do not change after the watch writes a sidecar | `tool.md` 636 | 5 |
| `R/describing.md#limit-folder-changes-not-seen` | After a folder is deleted and made again, saves in it start no pass | `tool.md` 637 | 5 |
| `R/connections.md#limit-short-password` | A short password shows in the output or a log | `tool.md` 638 | 5 |
| `D/postgresql/nullability.md#limit-columns-treated-as-nullable` | A property is nullable though the column is never null (PostgreSQL) | `postgresql.md` 524 | 4 |
| `D/postgresql/index.md#limit-operator-on-oid` | SQLSRC224 with "Npgsql cannot read the database's types" | `postgresql.md` 525 | 4 |
| `D/postgresql/index.md#limit-many-partitions` | Describing is slow on a table with thousands of partitions | `postgresql.md` 526 | 4 |
| `D/postgresql/query-methods.md#limit-connection-string-repository` | `InvalidCastException` for a NodaTime, PostGIS or enum type in a repository made from a connection string | `postgresql.md` 528; `generator.md` 1188 | 4, then 6 |
| `D/sqlserver/types.md#limit-ef-core-sqlclient` | SQLSRC267 in a project with EF Core 8 or 9 | `sqlserver.md` 522-528 | 4 |
| `D/sqlserver/nullability.md#limit-columns-treated-as-nullable` | A property is nullable though the column is never null (SQL Server) | `sqlserver.md` 530 | 4 |
| `D/sqlserver/nullability.md#limit-ansi-warnings-off` | "Data is Null." from a column whose property cannot be null, on a connection with `ANSI_WARNINGS` off | `sqlserver.md` 532 | 4 |
| `D/sqlserver/query-methods.md#limit-ansi-nulls-off` | An empty list after `IN` matches the rows whose value is null | `sqlserver.md` 534 | 4 |
| `D/sqlserver/query-methods.md#limit-hidden-rollback` | A `SqlException`, and the transaction is gone | `sqlserver.md` 536-547 | 4 |
| `D/sqlserver/index.md#limit-several-result-sets` | Only the first result set of a query has a model | `sqlserver.md` 549 | 4 |
| `D/sqlserver/query-methods.md#limit-rowcount-after-nocount` | A `rowcount` method returns `-1` | `generator.md` 1189; `sqlserver.md` 551 | 6 |
| `D/sqlserver/query-methods.md#limit-list-in-an-aggregate` | `SqlException` 130 for an empty list after `NOT` inside an aggregate of your own | `sqlserver.md` 555-565; `parameters-and-tokens.md` 262 | 4 |
| `D/sqlserver/index.md#limit-type-name-unicode` | SQLSRC228 for a type name that SQL Server accepts | `sqlserver.md` 567 | 4 |
| `D/sqlserver/index.md#limit-parameter-name-unicode` | SQLSRC231 for a parameter name with a rare character | `sqlserver.md` 569 | 4 |
| `D/sqlserver/index.md#limit-parameter-two-spellings` | Two parameters where SQL Server sees one | `sqlserver.md` 571 | 4 |

Three pointers lead to a section, not to a limit, and still get a troubleshooting entry, in the phase that moves the pointer's page: a MySQL user variable is listed as a parameter (`R/parameters.md#names-read-as-parameters`, phase 3), SQL that is read differently from the database (`R/dialects.md#still-read-differently`, phase 3), and a function marked `IMMUTABLE` that runs while a query is described (`D/postgresql/index.md#what-describing-does`, phase 4).

### Map: the preamble of `site/diagnostics.md` (lines 1 to 346)

The preamble is one general paragraph (line 3), 29 index tables under 29 bold area names (lines 5 to 309), and 18 paragraphs about ranges of ids (lines 311 to 345).  It goes in phase 2 (decision 3), after each fact that no section and no other page states has a home.  Those homes are `R/errors.md` and the sections themselves.

Phase 2 adds two lines to the sections, directly under the bold title line, in the pull request that removes the preamble, so that no section depends on a sentence that is gone:

- **"Reported by"**, in every one of the 161 sections.  It says what reports the error: the build, the tool, or both.
- **"Database"**, in each of the 18 sections of correction C-04, SQLSRC224 to 236 and 262 to 266.  It names the database that gives the error.

In the table below, a new location that names the "Reported by" of a section means that line, and under it each fact of the range paragraph that the section does not state today: where the error is reported, how often, and what it costs.  Phase 2 writes those sentences from the range paragraph, in plain language.  A rough scan of the 161 sections (a pattern match, not a reading) found no sentence about where the error is reported in about 60 of them, and none about its cost in about 100, so phase 2 reads every section against its range paragraph.  A spot check found these already missing: the build-alone fact in SQLSRC020; "SQLSRC209 or SQLSRC210 instead" in SQLSRC241; "parameters before columns" in SQLSRC246 and SQLSRC249; "an entry that no query uses is never an error" in SQLSRC250; "the later of two queries" in SQLSRC257; "one for each type, each naming the other" in SQLSRC274; "once for each column and parameter" in SQLSRC283 and SQLSRC284; and how to configure the warning in SQLSRC026.

The preamble goes early, in phase 2, before any page moves, so that its 204 same-page links and its paragraphs are never retargeted.  The page has 261 such links, and the 57 of the sections stay.  The folders do not exist in that phase, so it creates the page as `site/errors.md`, at the root of `site/`, and phase 3 moves it to `site/reference/errors.md`.  Every `R/errors.md` below is that page.  For the same reason, a row that names another page of `reference/` means that section on its page of today: `R/sidecars.md#sidecar-files` is `tool.md#sidecar-files` in phase 2, and `R/converters.md#converters` is `settings.md#converters`.  A sentence that phase 2 places there is placed word for word, and moves on with its section in phase 5 or 7.

Phase 2 writes `R/errors.md` new and in plain language, so in this table `move` means that the fact moves, not its words, and the accuracy reviewer checks each against the preamble.  The page has no rewrite phase.  Phases 21 to 24 rewrite the sections and keep both lines.

| Lines | Old unit | New location | Disposition | Note |
|----|----|----|----|----|
| 1 | h1 Diagnostics | Stays | move | One new paragraph follows it, of four statements: section 3 |
| 3 | Sentence 1: every problem is an error, and none can be turned off or made a warning, except eight ids (SQLSRC026, 031, 121, 240, 245, 248, 268, 284) | `R/errors.md#severity` | move | The only statement of the general rule.  Each of the eight says its own severity in its section |
| 3 | Sentence 2: the generator reports its errors and warnings in the build's output | `R/errors.md#where-an-error-is-reported` | move | |
| 3 | Sentence 3: information is not in the output of `dotnet build`; an IDE shows it, and the build log has it at the detailed verbosity | `R/errors.md#severity` | restated-from sections SQLSRC248, SQLSRC268 | One sentence stays on `R/errors.md` |
| 3 | Sentences 4, 8 and 9: an error in a `.sql` file, in a sidecar, or in a settings file is reported at its line and column in that file | `R/errors.md#where-an-error-is-reported` | move | The settings file half is also in `R/settings.md#a-value-that-is-wrong` |
| 3 | Sentence 5: an error in how a `.sql` file is written (101 to 120, or 126) costs the file its members; an error in where a query's members go (122 to 125) costs them for the type it is reported for | `R/errors.md#what-an-error-costs` | move | The owner of the cost rule.  `R/sql-files.md#markers-in-the-wrong-place` links it |
| 3 | Sentence 6: SQLSRC127 costs only its query the method, and only under `methods` or `repository` | `R/errors.md#what-an-error-costs`; section SQLSRC127 | move | |
| 3 | Sentence 7: an error about describing a query or about its sidecar leaves the file's members in place; under `models` the query keeps its constant or token method, under `methods` or `repository` it gets no method | `R/errors.md#what-an-error-costs` | move | |
| 3 | Sentence 10, first half: the tool prints its errors itself, in the format of a build error, and ends each with a `see:` line | `R/errors.md#the-tools-errors` | move | `R/cli.md` links it |
| 3 | Sentence 10, second half: the samples on this page leave that line out | Stays, in the one paragraph under the title | move | It is about this page |
| 3 | Sentence 11: in a build that runs the tool, each of its errors is one line, its other lines after `; ` | None | removed-preamble | restated-from `R/msbuild.md#running-in-the-build` ("Errors") |
| 5-309 | The 29 bold area names, each with the range of ids under it | `R/errors.md#errors-by-area` | move | 29 rows: the area, its ids as links, who reports them, where.  The names exist nowhere else |
| 5-309 | The 161 rows of id and title | None | removed-preamble | Not kept: a call of the outline, recorded in section 9.  Each title is the bold first line of its section, and a reader finds a title with the browser's find |
| 311 | Sentence 1: ids below 100 are about the type that carries `[SqlSourceGenerate]`, or about the project | `R/errors.md#errors-by-area` | move | |
| 311 | Sentence 2: the build alone reports SQLSRC021, before it compiles anything, and SQLSRC024 to 027, at the project file | `R/errors.md#who-reports`; "Reported by" of each section | move | |
| 311 | Sentence 3: SQLSRC028 to 037 are about a type that claims a `repository` query, at its attribute or the member they name, and never about a type whose queries are all of other outputs | `R/errors.md#errors-by-area`; "Reported by" of each section | move | |
| 311 | Sentence 4: each of SQLSRC028 to 034 but 031, and 036, leaves the repository queries of its databases without a method; their models are still generated | `R/errors.md#what-an-error-costs`; "Reported by" of each section | move | |
| 311 | Sentence 5: SQLSRC035 and 037 cost the type only its interface | None | removed-preamble | restated-from `R/repositories.md#repository-interfaces` and both sections |
| 311 | Sentence 6: ids from 101 to 127 are about the contents of a `.sql` file | `R/errors.md#errors-by-area` | move | |
| 311 | Sentence 7, first clause: the ids 200 to 236, 262 to 266, 271 and 285 to 296 are the tool's | `R/errors.md#who-reports` | move | Stated nowhere else |
| 311 | Sentence 7, rest: the `see:` line; the exit codes 1 and 2 | None | removed-preamble | restated-from `R/errors.md#the-tools-errors` (line 3) and `R/cli.md#exit-codes` |
| 311 | Sentence 8: the build reports SQLSRC209 and SQLSRC210 as well, at the same places | `R/errors.md#who-reports` | move | Both sections say "the build" today |
| 311 | Sentence 9: the tool also reports the errors of a `.sql` file, 101 to 120 and 126, under the generator's ids | `R/errors.md#who-reports` | move | Stated nowhere else |
| 311 | Sentence 10: the build alone reports the warning SQLSRC121, SQLSRC122 to 125, and SQLSRC127 | `R/errors.md#who-reports` | move | |
| 311 | Sentences 11 and 12: the tool reports the errors of a settings file (015 to 019) at the same line and column, for a settings file that applies to a file of the project, once in a run; it reports SQLSRC019 for `dialect`, `output` and `database` alone, and the build for every key but `database` | None | removed-preamble | restated-from `R/describing.md#which-queries` (`tool.md` 50).  One row of `R/errors.md#who-reports` keeps it.  See C-01 |
| 311 | Sentence 13: SQLSRC020 is the build's alone | `R/errors.md#who-reports`; section SQLSRC020 | move | The section does not say it today |
| 311 | Sentence 14: in a build that runs the tool, the tool leaves out each error that the build reports too, but not SQLSRC019 for `database` | None | removed-preamble | restated-from `R/msbuild.md#running-in-the-build` (`tool.md` 235, as corrected by C-01) |
| 313 | Sentence 1: SQLSRC224 to 236 and 262 to 266 are the errors of describing a query against its database | `R/errors.md#errors-by-area` | move | |
| 313 | Sentence 2: a section that names a database says which gives it and for what, and a section that names none is PostgreSQL's | The line "Database" of each of the 18 sections, SQLSRC224 to 236 and 262 to 266 | corrected | C-04: a reading rule for 18 sections.  Phase 2 gives each of the 18 its line before the sentence goes, so the rule is needed nowhere, and `R/errors.md` holds no note of it |
| 313 | Sentence 3: this version of the tool describes the `postgres` and `mssql` dialects | None | removed-preamble | restated-from `D/index.md#database-support` |
| 315 | Sentence 1: SQLSRC237 to 245 are reported by the build, about the sidecar of a `.sql` file that a type uses | `R/errors.md#errors-by-area` | move | |
| 315 | Sentences 2 and 3: which outputs need a current entry; the default output | None | removed-preamble | restated-from `R/settings.md#setting-output` |
| 315 | Sentence 4: a query that no entry can be written for gets SQLSRC209 or SQLSRC210 instead | Section SQLSRC241 | move | The section does not say it today |
| 315 | Sentence 5: a sidecar that exists is checked even when no query of its file needs an entry | `R/sidecars.md#sidecar-files` | move | Stated nowhere else |
| 315 | Sentence 6: while a `.sql` file has an error of its own, the build checks only the sidecar itself, SQLSRC237 to 240, and reports no entry of it as one that names no query | `R/sidecars.md#sidecar-files` | move | Stated nowhere else.  `R/errors.md#what-an-error-costs` links it |
| 317 | SQLSRC246 and SQLSRC249: the build reports them, about the C# types of a query with a current entry, under which dialects | `R/errors.md#errors-by-area`; "Reported by" of both sections | move | |
| 317 | Sentence 2: each column and parameter gets the type its driver reads it as, or the type you chose | None | removed-preamble | restated-from `R/type-mapping.md#property-types` |
| 317 | Sentence 3: at the query's name, once for a query, for its first column or parameter with no C# type, parameters before columns | "Reported by" of sections SQLSRC246, SQLSRC249 | move | Neither section says it today |
| 319 | SQLSRC247 and SQLSRC248: the build; the version of Npgsql; once for the project, with no file and line, only when a query needs an entry under `postgres` | `R/errors.md#errors-by-area`; "Reported by" of both sections | move | "The C# types of a PostgreSQL query depend on it" is restated-from `D/postgresql/types.md#which-npgsql` |
| 321 | SQLSRC267 and SQLSRC268: the build; the version of Microsoft.Data.SqlClient; once for the project, with no file and line | `R/errors.md#errors-by-area`; "Reported by" of both sections | move | |
| 323 | SQLSRC269 and SQLSRC270: the build, at a query of the `mssql` dialect, as SQLSRC249 is | `R/errors.md#errors-by-area`; "Reported by" of both sections | move | |
| 325 | Sentences 1 and 2: SQLSRC250 to 252 are about a C# type that you chose; where each is reported and what it names | `R/errors.md#errors-by-area`; "Reported by" of the three sections | move | |
| 325 | Sentence 3: an entry that no query uses is never an error | None | removed-preamble | restated-from `R/type-mapping.md#setting-type-overrides` (`settings.md` says it once) |
| 327 | SQLSRC261: where it is reported, what it names, once for the query | "Reported by" of section SQLSRC261 | move | |
| 329 | Sentence 1: SQLSRC253 to 260 are reported by the build, about the models of a query; SQLSRC259 and 260 whatever its C# types | `R/errors.md#errors-by-area` | move | Its link to `generator.md#models` becomes `R/models.md#models` |
| 329 | Sentence 2: each property is named by the PascalCase of its column or parameter | None | removed-preamble | restated-from `R/model-properties.md#property-names` |
| 329 | Sentences 3 and 4: which of them are about the query (once for the query, at its name or its marker) and which about a model (at the later of two queries, or at each query of the model) | "Reported by" of sections SQLSRC253 to 260 and SQLSRC251 | move | SQLSRC257 does not say "the later of two queries" today |
| 329 | Sentence 5: a query gets every error it has; under `models` its constant or token method is still generated, under `methods` or `repository` it gets no method until all are fixed | `R/errors.md#what-an-error-costs` | move | |
| 331 | SQLSRC272 to 276: the build, about a query's method; where each is reported and how often; no method until all are fixed, the models still generated; SQLSRC273 once for the project and database; SQLSRC276 at the type's attribute | `R/errors.md#errors-by-area`; "Reported by" of the five sections | move | Its link to `generator.md#query-methods` becomes `R/query-methods.md#query-methods`.  SQLSRC274 does not say "one for each type, each naming the other" today |
| 333 | SQLSRC277 to 280: the build, about converters; where each is reported; a converter in a referenced assembly; a project whose output is `models` uses no converter | `R/errors.md#errors-by-area`; "Reported by" of the four sections; the last sentence to `R/converters.md#converters` if that section does not say it | move | |
| 335 | SQLSRC281 and SQLSRC282: the build; library types; where, how often, and what each costs | `R/errors.md#errors-by-area`; "Reported by" of both sections | move | Its link to `settings.md#setting-library-types` becomes `R/library-types-and-json.md#setting-library-types` |
| 337 | SQLSRC283: the build, once for each column and parameter; no method until fixed; a `models` query gets none of it | "Reported by" of section SQLSRC283 | move | The section does not say "once for each column and parameter" today |
| 339 | SQLSRC284: the build, once for each column and parameter; a warning, and the query keeps the type | "Reported by" of section SQLSRC284 | move | The same |
| 341 | SQLSRC297: the build, once for the query; it costs only the collection method | "Reported by" of section SQLSRC297 | move | |
| 343 | SQLSRC298: the build, once for the query; no method and no collection method | "Reported by" of section SQLSRC298 | move | |
| 345 | SQLSRC299: the build, once for the query, beside its other errors; a query with no entry gets only SQLSRC241; it costs only the synchronous method | "Reported by" of section SQLSRC299 | move | |

The 161 sections (lines 347 to 3,707, 43,300 words) stay in place and in order.  Their 246 links to other pages are retargeted by this map.  154 of them are closing `See:` lines; seven sections have none (SQLSRC003, 012, 021, 221, 222, 223, 225), and the rewrite of phases 21 to 24 gives each one.  A move phase retargets the links of the sections to the pages it moves, and section 10 gives the four ranges of ids of the rewrite.

## 5. Merge pairs and corrections

### Merge pairs

Each pair lands on one new page.  The move places the texts of the pair side by side, untouched and in the order below, each under its own anchor: the first text's heading is the pair's heading, and each later text keeps its own heading, one level down, with its own `<a id>` line above it.  The rewrite merges them under one heading, removes the later heading and lifts its `<a id>` line to stand above the pair's.  The phase that places the first text of a pair starts the worksheet `merges/MP-<nn>.md`, in the form of the phase 0 brief, and each later phase that places a text adds it.  The texts of a pair can arrive in two phases.  The column "Placed in" gives the phase of each text, in the order of the second column, and one number when all arrive together.  The phase that comes first makes the heading and the page.  The rewrite phase in the column "Merged in" then merges the texts as a union, with a worksheet that shows them side by side.  Where they disagree, the named source settles it.

**A merge retires no anchor.**  Where two texts each have an explicit anchor today, the merged heading keeps both, as two `<a id>` lines above it, so a rewrite changes no link in another page.  This holds for `locations` and `setting-member-location` (MP-01), for `repository-interface` and `repository-interfaces` (MP-05), and for the seven anchors of MP-02, MP-03 and MP-04.  The pairs MP-06 and MP-07 stay two headings, each with its anchor.  The one exception is MP-10: a term's anchor belongs to `concepts.md`, is never put on a reference page, and retires with that page in phase 10.

| Id | The texts | Lands on | Where they disagree | Source of truth | Placed in | Merged in |
|----|----|----|----|----|----|----|
| MP-01 | `generator.md` 81-116 and 120-156; `settings.md` 716-755 (two sections named "Where the members go") | `R/members.md#locations` | No conflict known.  The default by output is stated in both and in the old table row | `src/SqlSource/Generation/Placement/QueryPlacement.cs`, `PlacementChecks.cs` | 6 | 16 |
| MP-02 | `generator.md` 291-306; `settings.md` 257-273 and 317-331 | `R/models.md#model-names` | Each has rules the other lacks.  `settings.md`: two equal suffixes are SQLSRC257; the suffix ends a table model's name.  `generator.md`: `UserDto` and `UserDTO` are two models | `src/SqlSource/Generation/Models/ModelCollector.cs`, `ModelNameChecker.cs`, `TypeModels.cs` | 6 | 16 |
| MP-03 | `generator.md` 310-312; `settings.md` 277-294 | `R/models.md#model-namespace` | `settings.md` has "two types in two namespaces that claim one file, or read one table" and the keywords `file`, `required`, `scoped`, `extension`.  `generator.md` has neither, and has the global namespace | `src/SqlSource/Generation/Models/ModelPlace.cs`, `TypeModels.cs`; `src/SqlSource/Settings/SettingValue.cs` | 6 | 16 |
| MP-04 | `generator.md` 326-352; `settings.md` 298-313 | `R/models.md#model-shape` | No conflict known | `src/SqlSource/Settings/ModelKind.cs`; `src/SqlSource/Generation/Models/ModelKinds.cs` | 6 | 16 |
| MP-05 | `generator.md` 708-731; `settings.md` 930-955 | `R/repositories.md#repository-interface` | The word "setting": correction C-02 | `src/SqlSource/Generation/Repositories/RepositoryInterfaces.cs`, `RepositoryShapeReader.cs`; `src/SqlSource/build/SqlSource.props` | 6 | 17 |
| MP-06 | `tool.md` 210-246; `settings.md` 959-980 | `R/msbuild.md#running-in-the-build`, then `#online-build` | `tool.md` 224 says a build runs the tool "when it finds a connection".  `settings.md` adds what that leaves out: a variable of a database's name alone is not found under `auto`; the `.env` file turns `auto` on whatever it holds; a `.env` above the project's folder turns nothing on; the list of CI variables, and that `CI=1` is not recognised.  Also correction C-01 at `tool.md` 235 | `src/SqlSource/build/SqlSource.targets` | 5; 7 | 14 |
| MP-07 | `generator.md` 61-77; `settings.md` 822-847 | `R/attributes.md#files-of-a-type`, then `#setting-source` | No conflict known | `src/SqlSource/Generation/TargetTypeReader.cs`; `src/SqlSource/Generation/Paths/` | 6; 7 | 14 |
| MP-08 | `sql-files.md` 200; `tool.md` 634 | `R/sidecars.md#limit-file-in-two-projects` | No conflict.  One says what the two projects need, the other what a run on one of them removes | The open item of `docs/tech-debt` that the limit stands for | 3; 5 | 18 |
| MP-09 | `generator.md` 1188; `postgresql.md` 528 | `D/postgresql/query-methods.md#limit-connection-string-repository` | No conflict | `src/SqlSource/Generation/Repositories/ConnectionSource.cs`, `ConnectionResolver.cs` | 6; 4 | 19 |
| MP-10 | Each of the 17 terms of `concepts.md` 27-139, and the opening of the reference section that now defines it (for example `concepts.md` 45-49 and `tool.md` 133; `concepts.md` 109-111, `settings.md` 183 and `dialects.md` 15; `concepts.md` 33-35 and `sql-files.md` 16; `concepts.md` 125-127 and `generator.md` 3; `concepts.md` 131-133 and `tool.md` 3) | The section named in the map of `concepts.md` | "Logical database" disagrees with `settings.md` 153: correction C-01.  "Output" and "Dialect" are reworded for decisions 4 and 5 | For C-01, see below.  For the others, the reference section wins | The terms, 8; the openings, with their pages, 3 to 7 | 13, 14, 16, 17 and 18, each with its page |
| MP-11 | `tool.md` 18-29; `README.md` 19-24 and 27 | `R/cli.md#install-the-tool` | No conflict.  The README has "a project that only keeps its SQL out of code files does not need it" | `docs/publishing.md` for the one version of package and tool | 5; 8 | 18 |
| MP-12 | `README.md` 163; `recipes.md` 643-702; `generator.md` 1183 | `G/dapper.md#models-through-dapper`, and `#limit-parameter-names` for the third | No conflict known | `tests/DatabaseTests/Postgres/EndToEnd/DapperRecipeTests.cs`, `tests/DatabaseTests/SqlServer/EndToEnd/DapperRecipeTests.cs` | 8; 8; 6 | 12 |
| MP-13 | `README.md` 191-193; `recipes.md` 258-300 | `S/transactions.md#queries-in-a-transaction` | No conflict known | `R/repositories.md#repository-transactions`, `R/connection-methods.md#method-connection` | 8 | 10: a `start/` page has no rewrite phase |
| MP-14 | `README.md` 214-216; `recipes.md` 93-121 and 27-65 | `G/several-databases.md` | No conflict known | `R/settings.md#setting-database`, after C-01 | 8 | 12 |
| MP-15 | `README.md` 218-220; `recipes.md` 69-89 | `G/folders-and-files.md#leave-migration-scripts-out` | No conflict known | `R/msbuild.md#included-files` | 8 | 12 |
| MP-16 | `README.md` 222-224; `recipes.md` 924-939 | `G/dynamic-sql.md#table-name-at-run-time` | No conflict known | `R/tokens.md` | 8 | 12 |
| MP-17 | `README.md` 226-228; `recipes.md` 943-961 | `G/lists.md` | No conflict known | `R/parameters.md#list-parameters` | 8 | 12 |
| MP-18 | `README.md` 230-232; `recipes.md` 965-990 and 994-1030 | `G/several-projects.md` | No conflict known | `tests/SqlSource.Tests/Generator/ExceptionReuseTests.cs` | 8 | 12 |
| MP-19 | `README.md` 234-236; `tool.md` 373-390 | `G/report-a-bug.md#report-a-bug` | No conflict known | `src/SqlSource.Tool/DiagnoseCommand.cs` | 8; 5 | 12 |
| MP-20 | `README.md` 248; `recipes.md` 497-587 | `G/json.md` | No conflict known | `R/library-types-and-json.md` | 8 | 12 |
| MP-21 | `README.md` 252-288; `recipes.md` 443-493 | `G/custom-types.md` | No conflict known | `R/type-mapping.md`, `R/converters.md` | 8 | 12 |
| MP-22 | `README.md` 132-165; `recipes.md` 591-639 | `G/dapper.md#models`, then `#dapper-and-adonet` | No conflict known | The `Models/` folders of the two example projects, which compile | 8 | 12 |
| MP-23 | Every statement of a supported version: `README.md` 9 and 29; `postgresql.md` 25, and the clause of 473, which stays on its page and is in the worksheet alone; `sqlserver.md` 28; `dialects.md` 134-143 | `D/index.md#supported-versions` | No conflict found.  The README gives minimum versions only; the database pages add what is tested | Root `AGENTS.md` rule 5 and `tests/DatabaseTests/TestHarness/DefaultImages.cs` for what is tested; the descriptors of SQLSRC235, SQLSRC247 and SQLSRC267 in `src/SqlSource/Diagnostics/` for the minimums | 8; 4; 4; 3 | 19 |

The two database pages are not merge pairs.  Their sections look alike and land on two pages of the same name, and phases 19 and 20 rewrite each database on its own.  Where they differ, the difference is the fact: arrays against lists; a column with no name is SQLSRC253 from the build on PostgreSQL and SQLSRC266 from the tool on SQL Server; library types are on by reference on PostgreSQL and off on SQL Server; SQL Server has no data source.

### One statement, several places: the owner afterwards

These are not merged on one page.  One section owns the rule, and every other place restates it in a sentence and links.

| Rule | Stated today in | Owner |
|----|----|----|
| Which queries must be described | `settings.md` 126-128, `tool.md` 10 and 33-50, `concepts.md` 3, `dialects.md` 147, `diagnostics.md` 315, `README.md` 9 and 180 | `R/settings.md#setting-output` |
| The `.env` file | `tool.md` 93-127, `settings.md` 984-1007, `README.md` 126, tool readme 16-22 | `R/connections.md#env-file` for the file; `R/msbuild.md#setting-env-file` for the property |
| When a sidecar entry stops being current | `tool.md` 156-164 and 191-198, `parameters-and-tokens.md` 41-66, `sql-files.md` 73-95 | `R/sidecars.md#describing-again` |
| What each result shape returns | `sql-files.md` 73-95, `generator.md` 832-852 | `R/return-types.md#method-shapes`; the five words are owned by `R/sql-files.md#result-shapes` |
| Tokens | `parameters-and-tokens.md` 160-254, `generator.md` 179-188, `concepts.md` 79-81 and 101-103 | `R/tokens.md`; where the method goes, `R/members.md#token-methods` |
| How a parameter is sent, how a row is read | `generator.md` 1058-1076, both database pages, `README.md` 101 | `R/query-methods.md` for what every database shares; each database's `query-methods.md` for its own types |
| A repository method takes no transaction | `README.md` 193, `generator.md` 735-739, `recipes.md` 258-300, the first bullet of both "What a method reads and throws" | `R/repositories.md#repository-transactions` |
| List parameters | `parameters-and-tokens.md` 70-125, `sqlserver.md` 442-468, `README.md` 228, `recipes.md` 943-961 | `R/parameters.md#list-parameters`; how SQL Server sends one, `D/sqlserver/query-methods.md#list-parameters` |
| Library types | `settings.md` 661-712, `README.md` 240-248, both database pages | The setting, `R/library-types-and-json.md#setting-library-types`; each library's types, each database's `libraries.md` |
| What an error costs a file or a query | `diagnostics.md` 3, `sql-files.md` 194, `dialects.md` 147, `settings.md` 97 | `R/errors.md#what-an-error-costs` |
| The causes of `InvalidCastException` | `README.md` 195-208, `generator.md` 1058-1066, both "What a method reads and throws" | From symptom to cause, `G/troubleshooting.md#invalidcastexception`; each rule, its own page |
| The dialect names | `dialects.md` 21-30, the old Dialect row, `README.md` 87, section SQLSRC019 | `R/dialects.md#choosing-a-dialect` |
| The generator parameters | The old Values cell, `settings.md` 212-224, sections SQLSRC109 and SQLSRC019 | `R/generator-parameters.md`; the sections keep their tested lists |
| Where a marker may stand | The marker index, the "Written as" column | Both tables stay, as indexes.  A test holds both to the code |

### Corrections

A correction is made by the phase that carries its sentence, so that no false sentence is moved.  The last column gives that phase.  Each is a `corrected` row of the phase's ledger, and the accuracy reviewer checks it against the source named.

| Id | Where | What is wrong | What it becomes | Source of truth | Phase |
|----|----|----|----|----|----|
| C-01 | `settings.md` 153 ("Only the tool reads this setting.  The build does not"); `settings.md` 41 (Status cell "Works today, for the tool alone"); `settings.md` 1047, last sentence ("every key but `database`, which only the tool reads"); `tool.md` 235 ("only the tool reads that setting") | The build reads the database setting too.  It uses it to choose the connection source of a repository method, and reports SQLSRC030 and SQLSRC031 by it | "The tool reads the setting to choose the connection it describes a query through.  The build reads it to choose the connection source of a repository method.  A `database` value that is not valid in a settings file is reported by the tool alone."  The last sentence is what stays true of the old text.  In line 1047 and in `tool.md` 235, "reads" becomes "checks" | `src/SqlSource/Generation/Methods/MethodPlanner.cs` (`DatabaseOf` calls `QuerySettings.ResolveDatabase`); `site/concepts.md` 115-117; `site/generator.md` 687 | 5 for `tool.md` 235; 7 for `settings.md` 153 and 1047.  The Status cell of line 41 goes in phase 1 with its column |
| C-02 | `settings.md` 3, 28, 34, 953 and 1049 | "Setting" has two meanings.  The page lists properties of the attribute and of the project as settings, then says of two of them that they "are not settings either".  Line 28 counts three places, line 34 lists five.  Line 1049 says "The project file holds no setting", and lists four MSBuild properties that a project sets, without `SqlSourceRepositoryInterfaces` | One definition, below.  Line 953 becomes "cannot be written in a settings file or a marker".  Line 1049 becomes "The project file holds no key of a settings file", and its list gains the fifth property.  "Not a setting of a settings file" (lines 924, 953) and "not a key of a settings file" (lines 980, 1007) are true and stay | The keys: `schemas/settings-v1.schema.json`, which is the list.  `SettingId` in `src/SqlSource/Settings/SettingId.cs` has 15 of the 16 and no `Dialect`.  The places: `SettingPlaceKind` in `src/SqlSource/Settings/SettingPlace.cs` has three, the two kinds of marker and the settings file; the attribute and the project are places for the reader and not for that enum.  The attribute's properties: `src/SqlSource/Generation/AttributeSource.cs`.  The project's properties: `src/SqlSource/build/SqlSource.props` for `SqlSourceIncludeFiles`, `SqlSourcePublicExceptions` and `SqlSourceRepositoryInterfaces`, and `src/SqlSource/build/SqlSource.targets` for `SqlSourceOnline` and `SqlSourceEnvFile` | 6 for line 953, which moves with "Repository interfaces"; 7 for lines 3, 28, 34 and 1049 |
| C-03 | `diagnostics.md` 3623, the `See:` line of SQLSRC294 | It links `tool.md#known-issues`, and none of the eight bullets there is about a bundle | The link is dropped.  The line keeps `G/report-a-bug.md#raw-bundle` | `site/tool.md` 631-638 | 5, which moves `tool.md` and so takes away the anchor that the link names |
| C-04 | `diagnostics.md` 313 | "A section that names none is PostgreSQL's" makes 18 sections depend on a sentence that goes | Phase 2 gives each of SQLSRC224 to 236 and 262 to 266 a line "Database", directly under its bold title line, in the pull request that removes the sentence.  The line names the database that gives the error: PostgreSQL, SQL Server, or both.  No note of the rule is kept on `R/errors.md`, and nothing waits for the rewrite.  Today every one of the 18 names a database at least once (14 name PostgreSQL, 13 name SQL Server), so the line says for the whole section what its sentences say in part | The files that make the errors: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerErrors.cs` and `src/SqlSource.Tool/Describing/Describers/DescribeFailures.cs`, which both call.  The descriptors of `ToolDiagnostics.cs` do not say which database gives an error | 2, which removes the sentence |
| C-05 | `docs/tech-debt/TD-0067-nullability-inside-a-mapped-type-argument-is-not-checked.md` 13 | It links `site/postgresql.md#known-issues`, and the bullet it means is `settings.md` 1070 | `site/reference/type-mapping.md#limit-nullable-type-argument` | `site/settings.md` 1070 | 4, which moves `postgresql.md`: the link then names `site/settings.md#known-issues`, where the bullet is.  Phase 7 moves the bullet and gives the link the target in this row |
| C-06 | `site/README.md` 3 | "Start with the repository README; this manual is the reference behind it" | It goes: the site owns getting started (decision 2) | The owner's decision | 10.  The sentence is true until Get started is written, so phase 3 moves it to `index.md` and phase 10 takes it out |
| C-07 | `README.md` 26, last sentence | "the attributes you mark your types and converters with, their enums and the exception its methods throw".  The package adds no enum, and three exceptions | "Only the code it generates, the attributes you mark your types and converters with, and the exceptions its methods throw, are compiled into your project." | `src/SqlSource/Generation/AttributeSource.cs`; `src/SqlSource/Generation/Exceptions/ExceptionSource.cs` | 8, which places the bullet on `R/attributes.md#what-the-package-adds`.  It corrects the README's own copy too, which phase 11 cuts |
| C-08 | `concepts.md` 29, the term "Query", third sentence | "Each query becomes one member of the generated code".  A query has two methods under `method-type=both`, and a collection method beside them | "Each query becomes one or more members of the generated code, named after the query." | `src/SqlSource/Settings/MethodType.cs`; `settings.md` 223 | 8, which places the term on `R/sql-files.md#queries-and-names`.  It corrects the copy on the parked page too |

Two rewordings are reframings and not corrections: the values of `output` are listed with `repository` first everywhere (decision 4), and no heading or step says "choose a dialect" (decision 5).  Both change words of today, so the rewrite phase of each page makes them, not its move: phase 13 on `R/sql-files.md` and `R/dialects.md`, phase 14 on `R/settings.md`.  A row of section 4 that says "reworded" or "reordered" means that phase.  The new first paragraph of `R/dialects.md` is new text and comes with the move, in phase 3.  One guard for new text: no page says that Azure SQL is supported or has "full support".  It is "accepted, and not tested".

### One definition of "setting", and the rows of the matrix

**A setting is a named choice that changes what SqlSource generates or does, and that you write in one of five places.**  The places are: a marker inside a query, a marker in the file header, a key of a settings file, a property of an attribute, and an MSBuild property of the project.  Which places a setting has is a fact about that setting.  A property of an attribute or of the project is a setting that has one place.

Not settings: the markers that describe one query's own content (`-- name:`, `-- summary:`, `-- param:`, `-- token:`, `-- token-ignore:`); the keys `root` and `$schema` of a settings file; `[SqlSourceConverter]`, which has no property; the options and environment variables of the `sqlsource` command; and the MSBuild properties that the package sets for itself.

`R/settings.md#all-settings` has one row for each setting and these columns: Setting (a link to its section), `sqlsource.json` key, Marker, In the file header (yes or no), Inside a query (yes or no), Attribute property, MSBuild property, Default.  It has no Values column and no Status column.  22 rows:

| # | Setting | Section |
|----|----|----|
| 1 | Output | `R/settings.md#setting-output` |
| 2 | Database | `R/settings.md#setting-database` |
| 3 | Dialect | `R/settings.md#setting-dialect` |
| 4 | Generator parameters | `R/generator-parameters.md#setting-generator-parameters` |
| 5 | Input and output model suffix | `R/models.md#setting-model-suffix` |
| 6 | Model namespace | `R/models.md#setting-model-namespace` |
| 7 | Input and output model type | `R/models.md#setting-model-type` |
| 8 | One query's model name | `R/models.md#setting-model-name` |
| 9 | Collection type | `R/return-types.md#setting-collection-type` |
| 10 | Type overrides | `R/type-mapping.md#setting-type-overrides` |
| 11 | Column types | `R/model-properties.md#setting-column-types` |
| 12 | Library types | `R/library-types-and-json.md#setting-library-types` |
| 13 | Member location | `R/members.md#setting-member-location` |
| 14 | Visibility | `R/members.md#setting-visibility` |
| 15 | Nested class | `R/members.md#setting-nested-class` |
| 16 | `Source` | `R/attributes.md#setting-source` |
| 17 | Repository interfaces (`GenerateInterface`, `InterfaceName`, `SqlSourceRepositoryInterfaces`) | `R/repositories.md#repository-interfaces` |
| 18 | A repository's connection (`[SqlSourceConnection]`, `Database`) | `R/repositories.md#repository-connection` |
| 19 | Running the tool in the build (`SqlSourceOnline`) | `R/msbuild.md#online-build` |
| 20 | The build's `.env` file (`SqlSourceEnvFile`) | `R/msbuild.md#setting-env-file` |
| 21 | Which `.sql` files are included (`SqlSourceIncludeFiles`) | `R/msbuild.md#included-files` |
| 22 | Public exceptions (`SqlSourcePublicExceptions`) | `R/exceptions.md#public-exceptions` |

Rows 1 to 17, 19 and 20 are today's 19 rows.  Rows 18, 21 and 22 are new: each is a property a user writes that today's table leaves out.  Rows 1 to 15 but row 8 cover the 16 keys of `schemas/settings-v1.schema.json` other than `root` and `$schema`.  Above the matrix, `R/settings.md#places` has the table of the five places: place, written as, applies to.  The settings file itself, its name, what it holds and which files it reaches, is on `R/settings-file.md`.

### Supported versions: one home

`D/index.md#supported-versions` is the one home.  Every other place that states a version today does this instead:

| Place today | What it states | Afterwards |
|----|----|----|
| `README.md` 9 and 29 | PostgreSQL 14 or later; SQL Server 2017 or later; Azure SQL accepted and not tested; Npgsql 8 or later; Microsoft.Data.SqlClient 6.1 or later | `README.md` "Supported databases" states the two minimum server versions and "Azure SQL: accepted, not tested", and links the table.  It states no driver version |
| `postgresql.md` 25 | 14 and later; tested on every major from 14 to 18 | The bullet moves whole, to stand beside the table.  `D/postgresql/index.md#connecting` gets one new bullet that links the table and names the error for an older server |
| `postgresql.md` 167-174 | Npgsql 8, 9 or 10; older is SQLSRC247; 11 or later is SQLSRC248 | Stays as "what each version of Npgsql changes".  The minimum is a link to the table |
| `postgresql.md` 473 | Tested on Npgsql 8 and 10 | The bullet stays whole on `D/postgresql/query-methods.md#method-results`.  A cell of the table restates the clause |
| `generator.md` 569 | The project references Npgsql 8.0 or later, or Microsoft.Data.SqlClient 6.1 or later | Stays, on `R/query-methods.md#which-methods`, as the condition of a method with its two errors.  The rewrite of phase 17 keeps the two numbers that the errors enforce and links the table |
| `sqlserver.md` 28 | 2017 and later; tested on 2017, 2019, 2022 and 2025; Azure SQL | The bullet moves whole: two sentences beside the table and one under `#azure-sql`.  `D/sqlserver/index.md#connecting` gets one new bullet that links both |
| None | The versions of Microsoft.Data.SqlClient that SqlSource is tested on, 6.1 and 7 | New in the table, in phase 4: no page states it today.  Its source is `tests/DatabaseTests/SqlServer.SqlClient61/SqlServer.SqlClient61.csproj` (6.1.7) and `Directory.Packages.props` (7.1.1) |
| `sqlserver.md` 161-168 | Microsoft.Data.SqlClient 6.1 or 7; older is SQLSRC267; 8 or later is SQLSRC268 | Stays as "what each version changes".  The minimum is a link to the table |
| `dialects.md` 134-143 | Which databases are described | Moves to `D/index.md#database-support` |
| `tool.md` 567 | "A database of a version it supports", with links to both `#connecting` | Links the table |
| Tool readme 3 | "It describes PostgreSQL and SQL Server queries" | Stays, with no version |
| `diagnostics.md`, sections SQLSRC235, SQLSRC247, SQLSRC267 | The minimum that the error enforces | A section may state the number its own error enforces, since its reader arrives with no context.  Its `See:` line links the table.  The rewrite phase of the section's range adds the link: phase 23 for SQLSRC235 and SQLSRC247, phase 24 for SQLSRC267 |
| `README.md` 29, `generator.md` 3, section SQLSRC021 | .NET 8, C# 12, the .NET SDK 9.0.300, Visual Studio 2022 17.14 | Not a database version: the owner is `R/msbuild.md#build-requirements`, and the tool's own needs are `R/cli.md#requirements` |
| `README.md` 244, `sqlserver.md`, `settings.md`, section SQLSRC282 | NetTopologySuite 2.5.0 and other library versions | Owned by each database's `libraries.md#nodatime` and `#nettopologysuite`.  Not in the table |

By phase: phase 3 places the table of `dialects.md` 134-143 under `#database-support` and so creates `D/index.md`.  Phase 4 writes the table of `#supported-versions`, as new text, and places the bullets of `postgresql.md` 25 and of `sqlserver.md` 28 beside it, word for word.  The bullet of `postgresql.md` 473 stays on its page.  Phase 8 adds the sentences of `README.md` 9 and 29.  Phase 19 merges them all into the table and its text (MP-23).

Three new places restate the two minimum server versions and nothing more: `index.md#databases`, `S/install.md#requirements` and `README.md`.  One test, `SupportedVersionsTests`, holds the table to the pinned images and the descriptors, and holds the three restatements to the table.  It lands in phase 4 and grows in phases 10 and 11, when the restatements are written.  Sentences such as "under Npgsql 8 and 9 a `date` is a `DateTime`" are behaviour by version, not support statements, and stay where they are.

## 6. Links that need a decision

A link whose anchor keeps its name needs no decision: the map gives the new page of every section, and the 439 same-page links of the pages that split are rewritten by the same rule (`#x` becomes the new page of `x` when that page is not the link's own).  This section lists the links for which that rule gives no answer.

The targets below are the final ones.  The moves run one after another, so a link can name a section that has not moved yet.  Such a link names the section's page of today, at the root of `site/`, and the phase that moves the section retargets it.  The same holds for a link to a limit whose bullet has not moved: it names the `#known-issues` anchor of the old page until the phase that makes the limit (section 4).  And it holds for a link to an anchor of a `start/` page: it names the nearest place that exists until phase 10.  The brief of each phase lists such links in two tables, "Interim links this phase leaves" and "Interim links this phase retargets".

Counts.  Links to a `#known-issues` anchor: 46 in `site/`, 2 in `README.md`, 8 links and 8 plain mentions elsewhere.  Links to a bare page with no anchor: 40 in `site/`, 8 in `README.md`, 1 in the tool readme, and 85 references elsewhere.  Links to a term or to `#glossary` of `concepts.md`: 25 in `site/` (one of them in `site/AGENTS.md`), 3 elsewhere.  References to `#what-works-today`: 1 link in `README.md`, 2 rules in `AGENTS.md` files, 4 test strings.  Links from `README.md` to its own headings: 6.

### 6.1 Links to `#known-issues` inside `site/` (46)

| Link at | Today | New target |
|----|----|----|
| `site/README.md` 57 (8 links) | All nine pages but `recipes.md` | One link to `G/troubleshooting.md`, from `index.md#find-what-you-need` |
| `diagnostics.md` 1777 (SQLSRC208, `See:`) | `generator.md` | `R/attributes.md#limit-source-written-as-text` |
| `diagnostics.md` 2611 (SQLSRC245) | `sql-files.md` | `R/sidecars.md#limit-file-in-two-projects` |
| `diagnostics.md` 3623 (SQLSRC294, `See:`) | `tool.md` | Dropped: C-03 |
| `generator.md` 38 | Same page | `R/attributes.md#limit-embedded-attribute` |
| `generator.md` 352 | `settings.md` | `R/type-mapping.md#limit-nullable-type-argument` |
| `generator.md` 702 | Same page | `D/postgresql/query-methods.md#limit-connection-string-repository` |
| `generator.md` 850 | Same page | `D/sqlserver/query-methods.md#limit-rowcount-after-nocount` |
| `generator.md` 1097, 1121, 1144 | Same page | `R/exceptions.md#limit-caught-by-type` |
| `parameters-and-tokens.md` 91 | Same page | `D/sqlserver/query-methods.md#limit-list-in-an-aggregate` |
| `parameters-and-tokens.md` 156 | Same page | `R/parameters.md#limit-parameter-taken-for-a-variable` |
| `parameters-and-tokens.md` 262 | `sqlserver.md` | Goes with its bullet, a pointer |
| `postgresql.md` 427 | Same page | `D/postgresql/query-methods.md#limit-connection-string-repository` |
| `postgresql.md` 493 | `generator.md` | `R/query-methods.md#limit-wrapped-connection` |
| `postgresql.md` 529 | `generator.md` | Goes with its bullet, a pointer |
| `recipes.md` 410 | `generator.md` | `D/postgresql/query-methods.md#limit-connection-string-repository` |
| `recipes.md` 702 | `generator.md` | `G/dapper.md#limit-parameter-names` |
| `settings.md` 1047 | Same page | `R/settings-file.md#limit-symbolic-link` |
| `sql-files.md` 200 | `settings.md` | `R/msbuild.md#limit-file-added-by-a-target` |
| `sqlserver.md` 118 | Same page | `D/sqlserver/nullability.md#limit-ansi-warnings-off` |
| `sqlserver.md` 166 | Same page | `D/sqlserver/types.md#limit-ef-core-sqlclient` |
| `sqlserver.md` 312 | `generator.md` | `R/query-methods.md#limit-wrapped-connection` |
| `sqlserver.md` 451 | Same page | `D/sqlserver/query-methods.md#limit-list-in-an-aggregate` |
| `sqlserver.md` 452 | Same page | `D/sqlserver/query-methods.md#limit-ansi-nulls-off` |
| `sqlserver.md` 484 | Same page | `D/sqlserver/query-methods.md#limit-hidden-rollback` |
| `sqlserver.md` 485 | `generator.md` | `D/sqlserver/query-methods.md#limit-rowcount-after-nocount` |
| `sqlserver.md` 488 | `generator.md` | `R/query-methods.md#limit-wrapped-connection` |
| `sqlserver.md` 551, 553 | `generator.md` | Go with their bullets, two pointers |
| `tool.md` 48 | `generator.md` | `R/attributes.md#limit-source-written-as-text` |
| `tool.md` 89, 246, 552 | Same page | `R/connections.md#limit-short-password` |
| `tool.md` 238 | Same page | `R/describing.md#limit-several-target-frameworks` |
| `tool.md` 313 | Same page | `R/describing.md#limit-saves-not-seen` |
| `tool.md` 633 | `settings.md` | Goes with its bullet, a pointer |
| `tool.md` 634 | `sql-files.md` | Goes: both texts are one limit now (MP-08) |

### 6.2 Links to `#known-issues` in the readmes and elsewhere

| Link at | Today | New target |
|----|----|----|
| `README.md` 29 | `generator.html#known-issues` | Phase 6 gives the link `reference/query-methods.html#limit-wrapped-connection`.  The sentence leaves the README in phase 11.  No `start/` page restates it, since a repository method opens its own connection: its owner is `R/query-methods.md#limit-wrapped-connection` |
| `README.md` 208 | `generator.html#known-issues` | The FAQ answer moves to `G/troubleshooting.md#invalidcastexception` and links `R/query-methods.md#limit-wrapped-connection` |
| `docs/architecture/sqlserver-describer.md` 289 | `site/sqlserver.md#known-issues` | `site/reference/databases/sqlserver/nullability.md#limit-ansi-warnings-off` |
| `docs/deferred/D-0007-converters-by-convention.md` 9 | `site/settings.md#known-issues` | `site/reference/converters.md#limit-vogen-and-stronglytypedid` |
| `docs/deferred/D-0008-converter-per-file-or-query.md` 9 | `site/settings.md#known-issues` | `site/reference/converters.md#limit-one-conversion-per-type` |
| `docs/tech-debt/TD-0016-settings-file-beside-a-late-targets-file-is-missed.md` 19 | `site/settings.md#known-issues` | `site/reference/msbuild.md#limit-file-added-by-a-target` |
| `docs/tech-debt/TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md` 25 | `site/settings.md#known-issues`, and the words "those of `site/tool.md` point there" | `site/reference/msbuild.md#limit-file-added-by-a-target`.  The words about `site/tool.md` go: the pointer is gone |
| `docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md` 25 | `site/tool.md#known-issues` | `site/reference/connections.md#limit-short-password` |
| `docs/tech-debt/TD-0067-nullability-inside-a-mapped-type-argument-is-not-checked.md` 13 | `site/postgresql.md#known-issues` | `site/reference/type-mapping.md#limit-nullable-type-argument`: C-05 |
| `docs/tech-debt/TD-0071-sql-server-answers-nullable-for-columns-that-cannot-be-null.md` 24 | `site/sqlserver.md#known-issues` | `site/reference/databases/sqlserver/nullability.md#limit-columns-treated-as-nullable` |
| `docs/architecture/tool.md` 283 (plain words) | "under its known issues" | A link to `site/reference/describing.md#limit-folder-changes-not-seen` |
| `docs/deferred/D-0006-multiple-result-sets-on-sql-server.md` 9 (plain words) | "lists it among its known issues" | A link to `site/reference/databases/sqlserver/index.md#limit-several-result-sets` |
| `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md` 37 (plain words) | "in its known issues", of `site/parameters-and-tokens.md` | `site/reference/parameters.md#names-read-as-parameters`: the bullet was a pointer to that section |
| `docs/deferred/AGENTS.md` 22, 31 and 40; `docs/tech-debt/AGENTS.md` 22, 31 and 40 (plain words) | The process: add a known issue to the "Known issues" section of the page; rewrite "its known issue in `site/`" after a partial fix; remove it with the item | Add a limit under `## Limits` on the page that owns the behaviour, with an anchor `limit-<name>`, and an entry in `site/guides/troubleshooting.md`; rewrite "its limit in `site/`"; remove both with the item.  Phase 3 changes the six rules, with the other rules of the skeleton.  Line 40 names "the "Known issues" of a page not yet moved" until phase 7 |
| `tests/DatabaseTests/SqlServer/EndToEnd/MethodShapeTests.cs` 538 (plain words) | "as the manual's known issue says" | "as the manual's limit says", with the path of `site/reference/databases/sqlserver/query-methods.md#limit-hidden-rollback`.  Phase 4 |

### 6.3 Other anchors that retire

No merge of a rewrite retires an anchor (section 5).  These retire with the text or the page they stand on: `what-works-today` in phase 1; the 18 anchors of `concepts.md` in phase 10, with their 28 links retargeted in phase 8, when the terms reach their sections; the four headings of `README.md` in phase 11, when the README is cut.

| Anchor | Links | New target |
|----|----|----|
| `site/README.md#what-works-today` | `README.md` 7; root `AGENTS.md` 18; `site/AGENTS.md` 27; four strings of `UserDocsReadingTests.cs` | None.  The links go with their sentences and rules.  The test strings change to an anchor that exists |
| `concepts.md#glossary` | `site/README.md` 3; `site/AGENTS.md` 24; one link from outside `site/` | `S/how-it-works.md#words` for a reader.  The rule of `site/AGENTS.md` names the writing rules in its place |
| `concepts.md#query-parameter` | 6 | `R/parameters.md#query-parameters` |
| `concepts.md#dialect` | 3 | `R/settings.md#setting-dialect` |
| `concepts.md#logical-database` | 3 | `R/settings.md#setting-database` |
| `concepts.md#sidecar` | 3 in `site/`, 2 from outside `site/` | `R/sidecars.md#sidecar-files` |
| `concepts.md#marker` | 2 | `R/sql-files.md#marker` |
| `concepts.md#file-header` | 2 | `R/sql-files.md#the-file-header` |
| `concepts.md#token` | 2 | `R/tokens.md#tokens` |
| `concepts.md#generator-parameter` | 1 | `R/generator-parameters.md#setting-generator-parameters` |
| `concepts.md#describe` | 1 | `R/describing.md#describe` |
| `concepts.md#output`, `#query-method`, `#repository`, `#settings-file`, `#token-method`, `#query`, `#generator`, `#tool` | Linked only from `concepts.md` itself (11 links), which goes | `R/settings.md#setting-output`, `R/query-methods.md#query-methods`, `R/repositories.md#repository-methods`, `R/settings-file.md#settings-files`, `R/members.md#token-methods`, `R/sql-files.md#queries-and-names`, `R/members.md#generator`, `R/cli.md#tool` |
| `README.md#just-want-to-keep-your-sql-out-of-code-files` | `README.md` 9, 107 | `guides/other-databases.html` in the README's "Other ways to use it"; on the site, `G/dapper.md#sql-only` |
| `README.md#only-want-to-generate-models` | `README.md` 9, 180 | `guides/dapper.html`; on the site, `G/dapper.md#models` |
| `README.md#5-describe-your-queries` | `README.md` 27 | `start/first-query.html#describe`; on the site, `S/first-query.md#describe` |
| `README.md#i-got-an-invalidcastexception-what-does-it-mean-and-how-do-i-fix-it` | `README.md` 268 | `G/troubleshooting.md#invalidcastexception` |

Every other heading of `README.md` is linked from nowhere in the repository.  A page outside the repository that links one of them loses its anchor and still reaches the README.

### 6.4 Links to a bare page

Inside `site/`, 40 links name a page and no anchor.  18 are in `site/README.md` (lines 9, 19, 56, 69, 78, 83, 87 and 109 to 119).  Their text lives on `index.md` from phase 3 to phase 10, so each move phase gives them a path that resolves: the new page of a page that moved whole, or the first page of what a page became.  They go in phase 10, when the landing page is written new.  The other 22:

| Link at | Today | New target |
|----|----|----|
| `concepts.md` 91, 103 | `parameters-and-tokens.md` | Go: each term is now defined on the page it pointed at |
| `concepts.md` 111 | `dialects.md` | `R/dialects.md`, from `R/settings.md#setting-dialect` |
| `concepts.md` 127 | `generator.md` | Goes, for the same reason |
| `concepts.md` 133 | `tool.md` | Goes, for the same reason |
| `diagnostics.md` 2583 (SQLSRC243, `See:`) | `dialects.md` | `D/index.md#database-support` |
| `dialects.md` 136, 137 | `postgresql.md`, `sqlserver.md` | `D/postgresql/index.md`, `D/sqlserver/index.md` |
| `generator.md` 3 | `tool.md` | `R/describing.md#describe` |
| `generator.md` 77 | `sql-files.md` | `R/errors.md#what-an-error-costs` |
| `parameters-and-tokens.md` 177 | `generator.md` | `R/members.md#token-methods` |
| `postgresql.md` 3, `sqlserver.md` 3 (first link of each) | `dialects.md` | `R/settings.md#setting-dialect` |
| `postgresql.md` 3, `sqlserver.md` 3 (second link of each) | `dialects.md` | `R/dialects.md#what-a-dialect-changes` |
| `settings.md` 183 | `dialects.md` | `R/dialects.md` |
| `sql-files.md` 28 | `dialects.md` | `R/dialects.md#choosing-a-dialect` |
| `sql-files.md` 47 | `settings.md` | `R/settings.md#all-settings` |
| `sql-files.md` 134 | `dialects.md` | `R/settings.md#setting-dialect` |
| `sql-files.md` 158 | `dialects.md` | `R/dialects.md#what-a-dialect-changes` |
| `sql-files.md` 160 | `parameters-and-tokens.md` | `R/parameters.md#list-parameters` |
| `sql-files.md` 194 | `diagnostics.md` | `diagnostics.md`: the page does not move |

In the readmes, nine links name a page and no anchor.  Most leave with the text they stand in, since both readmes are rewritten to the outlines of section 7:

| Link at | Today | The new readme links |
|----|----|----|
| `README.md` 58 | `parameters-and-tokens.html` | Nothing: the step links `start/first-query.html` |
| `README.md` 72 | `generator.html` | Nothing, as above |
| `README.md` 130 | `tool.html`, `postgresql.html`, `sqlserver.html` | `reference/databases/index.html` under "Supported databases" |
| `README.md` 298 | `sqlserver.html` | `reference/databases/index.html` |
| `README.md` 301 | `diagnostics.html` | `diagnostics.html`: unchanged |
| `README.md` 303 | The site's root | The site's root, which is now the landing page |
| Tool readme 50 | `tool.html` | `reference/cli.html` |

Elsewhere in the repository, 85 references name `site/<page>.md` with no anchor (`git grep`): `diagnostics.md` 21, `settings.md` 14, `recipes.md` 13, `README.md` 8, `generator.md` 7, `postgresql.md` 6, `tool.md` 5, `dialects.md` 4, `sqlserver.md` 3, `sql-files.md` 2, `parameters-and-tokens.md` 2.  36 `.cs` files hold 47 of all the references to a site page, in comments.  The 21 to `diagnostics.md` do not change.  The rest:

| Reference | Count and where | New target |
|----|----|----|
| `site/README.md`, "the manual" | 8: root `AGENTS.md` 22, `docs/architecture/AGENTS.md` 71, `docs/architecture/README.md` 3, both example READMEs, three test strings | `site/index.md`.  The two `AGENTS.md` rules about "What works today" are rewritten, not retargeted |
| `site/settings.md` as "the page of settings" | `src/SqlSource/Settings/AGENTS.md` 16, `schemas/AGENTS.md` 3, `docs/deferred/D-0001-dialects-not-delivered.md` 17, `DiagnosticsDocumentTests.cs` 59 | `site/reference/settings.md`, and `site/reference/settings-file.md` for `schemas/AGENTS.md`, which is about the `$schema` key of a settings file.  The rule "a setting is documented in `site/settings.md` alone" is rewritten to "Where rules live" in the outline.  The test comment names `site/reference/generator-parameters.md` |
| `site/settings.md`, "Converters" | Six `.cs` files of the two example projects | `site/reference/converters.md` |
| `site/settings.md`, "A class in a JSON column" | Two `.cs` files of the two example projects | `site/reference/library-types-and-json.md` |
| `site/generator.md`, "the generator page" | `docs/architecture/generator.md` 3, both example READMEs | `site/reference/members.md` |
| `site/generator.md`, "Query methods" | Two `.cs` files of the example projects | `site/reference/query-methods.md` |
| `site/generator.md`, "Give the method the transaction" | `tests/DatabaseTests/SqlServer.SyncAsync/KnownDifferences.cs` 12 and 22 | `site/reference/connection-methods.md` |
| `site/tool.md` | `docs/architecture/tool.md` 3, both example READMEs, `src/SqlSource.Tool/AGENTS.md` 15 | `site/reference/cli.md`.  The `AGENTS.md` rule names `cli.md`, `describing.md`, `connections.md`, `sidecars.md` and `bundles.md`, and the file has 33 bytes of room, so it is trimmed in the same commit |
| `site/postgresql.md`, `site/sqlserver.md` as a page | `docs/architecture/postgres-describer.md` 3, `sqlserver-describer.md` 3, `examples/SqlServerExamples/README.md` 3, the two `SyncAsync/AGENTS.md` | `site/reference/databases/postgresql/index.md`, `site/reference/databases/sqlserver/index.md` |
| `site/postgresql.md`, "What a method reads and throws" and "A synchronous method needs a data source without multiplexing" | `examples/PostgresExamples/Methods/StoreDataSource.cs` 9, `tests/DatabaseTests/Postgres.SyncAsync/KnownDifferences.cs` 10 and 18 | `site/reference/databases/postgresql/query-methods.md` |
| `site/recipes.md` | 13: section 8 lists each | The guide that the recipe moves to |
| `site/dialects.md`, `site/sql-files.md`, `site/parameters-and-tokens.md` as pages | `docs/deferred/D-0001-dialects-not-delivered.md` 17, `docs/architecture/generator.md` 3, `src/SqlSource/Settings/AGENTS.md` 16, `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md` 37 | `site/reference/dialects.md`, `site/reference/sql-files.md`, `site/reference/parameters.md` |

Eight links of `docs/architecture/` name a heading by its slug and not by its `<a id>`.  They resolve today, and each would break when a rewrite rewords its heading.  The phase that moves the target page gives each the explicit anchor:

| Link at | The slug | Becomes | Phase |
|----|----|----|----|
| `docs/architecture/generator.md` 490 | `sql-files.md#a-file-with-no----name` | `site/reference/sql-files.md#a-file-with-no-name` | 3 |
| `docs/architecture/generator.md` 500 | `dialects.md#what-each-dialect-changes` | `site/reference/dialects.md#what-a-dialect-changes` | 3 |
| `docs/architecture/generator.md` 540 | `sql-files.md#what-reaches-the-generated-sql` | `site/reference/sql-files.md#generated-sql` | 3 |
| `docs/architecture/generator.md` 552 | `parameters-and-tokens.md#variables-declared-with-declare` | `site/reference/parameters.md#declared-variables` | 3 |
| `docs/architecture/tool.md` 187 | `tool.md#the-summary` | `site/reference/cli.md#summary` | 5 |
| `docs/architecture/sidecar-format.md` 52 | `settings.md#column-types` | `site/reference/model-properties.md#setting-column-types` | 6 |
| `docs/architecture/sidecar-format.md` 52 | `settings.md#type-overrides` | `site/reference/type-mapping.md#setting-type-overrides` | 7 |
| `docs/architecture/generator.md` 382 | `settings.md#which-place-wins` | `site/reference/settings.md#precedence` | 7 |

One site address outside the user documents names a page: `.github/ISSUE_TEMPLATE/bug-report.yml` 12, `tool.html#report-a-bug`, which becomes `guides/report-a-bug.html#report-a-bug`.  No test reads it today.

## 7. The two readmes

### `README.md` (about 100 lines, from 305)

| # | Section | Holds | The site section it points to |
|----|----|----|----|
| 1 | `# SqlSource`, the badges | One paragraph of four sentences, from line 5 | `index.md` |
| 2 | (no heading) | One sample pair: a `.sql` file with one query, and the C# that calls it.  It is the pair of `index.md`, byte for byte (section 3) | `S/first-query.md` |
| 3 | `## Supported databases` | Three lines: PostgreSQL 14 or later and SQL Server 2017 or later get typed models and methods; Azure SQL is accepted and not tested; any other database gets its SQL as constants | `D/index.md#supported-versions`; `G/other-databases.md` |
| 4 | `## Install` | The command that adds the package and the two tool commands, with `--prerelease`, and the sentence "The packages are published as previews, so the commands include `--prerelease`".  One line: .NET 8 or later, built with the .NET SDK 9.0.300 or later | `S/install.md`; `R/msbuild.md#build-requirements` |
| 5 | `## Use` | Five numbered steps, each one or two lines: write a `.sql` file; mark a partial class; name your database in `sqlsource.json`; put the connection in `.env` and build; call the method.  Then: "The full walkthrough" | `S/first-query.md#write-a-query`, `#mark-a-partial-class`, `#name-your-database`, `#connection` and `#describe`, `#call-the-methods` |
| 6 | `## Other ways to use it` | Three bullets.  Your own connection, transaction or timeout (the output `methods`).  Dapper or your own ADO.NET code (the output `models`).  Only the SQL, on any database (the output `sql`) | `S/transactions.md`; `G/dapper.md`; `G/other-databases.md` |
| 7 | `## Documentation` | Five links: Get started, Guides, Reference, Errors, the two example projects | `S/install.md`, `G/lists.md` (the first page of Guides), `R/sql-files.md` (the first page of Reference), `diagnostics.md` |
| 8 | (last line) | Contributing and the licence, as today | |

The FAQ, the two "Only want" and "Just want" sections, "NodaTime and other libraries", "Custom type mapping" and "Where to next" leave.  Each is a page of the site, as the map of `README.md` says.  The link form becomes `https://mbcrawfo.github.io/SqlSource/<folder>/<page>.html#<anchor>`.

By phase: phase 8 moves the text of those sections to the site, and each heading keeps one sentence that points to its new page.  Phase 11 cuts the README to this outline, takes out the pointers, and adds `ReadmeTests`.  Every phase from 3 on retargets the README's links to the pages it moves, since the README on `main` must not link a page that the site lacks.

### `src/SqlSource.Tool/README.md` (about 35 lines, from 50)

| # | Section | Holds | The site section it points to |
|----|----|----|----|
| 1 | `# SqlSource.Tool` | Three sentences: what `describe` does; that you commit the sidecars; that it describes PostgreSQL and SQL Server | `R/describing.md#describe` |
| 2 | `## Install` | The two commands, with `--prerelease`, the same sentence about previews, and "at the version of the `SqlSource` package" | `R/cli.md#install-the-tool` |
| 3 | `## Use` | The `.env` line; one sentence that the file is in the project's folder; the four `describe` commands, one line each; one sentence that the build runs `describe` itself; one line for `diagnose` and `replay` | `R/connections.md#env-file`; `R/describing.md`; `R/msbuild.md#running-in-the-build`; `G/report-a-bug.md` |
| 4 | `## Documentation` | Five links: keep your queries up to date; the command reference; connections; report a wrong model; the SqlSource readme | `S/keep-up-to-date.md`; `R/cli.md#command-reference`; `R/connections.md`; `G/report-a-bug.md` |

## 8. Mirrored samples

A sample in this list has a twin in a test or an example.  A change to the sample is a change to the twin, and the move changes the path or the words that the twin names.  The phase that moves the sample changes the twin in the same pull request: phase 4 for `postgresql.md`, phase 5 for the VS Code task, phase 6 for `generator.md`, phase 7 for `settings.md`, and phase 8 for the other recipes.

| Sample today | Its twin | The sample moves to |
|----|----|----|
| `recipes.md` 204-241, the VS Code task | `tests/SqlSource.Repository.Tests/Watching/EditorRecipeTests.cs` reads the file (line 140) and names the anchor (line 16).  `src/SqlSource.Tool/Watching/AGENTS.md` 5 and `tests/README.md` 13 name the recipe | `G/describe-on-save.md#vs-code`.  The test reads `site/guides/describe-on-save.md`.  The task is never inside a tab |
| `recipes.md` 706-785, With PostgreSQL | `tests/DatabaseTests/Postgres/EndToEnd/Types/EmailHandler.cs` 6 and `TypedParameter.cs` 8 ("The same code as the sample of `site/recipes.md`"); `tests/DatabaseTests/Postgres/EndToEnd/DapperRecipeTests.cs` 18; `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md` 46 and 47; `examples/PostgresExamples/DapperSetup.cs` 10 | `G/dapper.md#dapper-with-postgresql` |
| `recipes.md` 789-920, With SQL Server | `tests/DatabaseTests/SqlServer/EndToEnd/Types/DateOnlyHandler.cs` 7, `TimeOnlyHandler.cs` 7 and `TypedParameter.cs` 8; `tests/DatabaseTests/SqlServer/EndToEnd/DapperRecipeTests.cs` 17; `tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md` 52; `examples/SqlServerExamples/DateOnlyHandler.cs` 9 and `TypedParameter.cs` 9 | `G/dapper.md#dapper-with-sql-server` |
| `recipes.md` 643-702, Run the models through Dapper | `tests/DatabaseTests/Postgres/EndToEnd/EndToEndSources.cs` 8 and `tests/DatabaseTests/SqlServer/EndToEnd/EndToEndSources.cs` 12 ("set up as the manual's recipe tells a project to"); `tests/DatabaseTests/Postgres/README.md` 140; `tests/DatabaseTests/SqlServer/README.md` 164; `examples/SqlServerExamples/DapperSetup.cs` 8 | `G/dapper.md#models-through-dapper` |
| `recipes.md` 591-639, your own ADO.NET code | `tests/DatabaseTests/QueryMethodAllocation/Postgres/AllocationTests.cs` 18 ("the manual's recipe for your own code") | `G/dapper.md#dapper-and-adonet` |
| `recipes.md` 994-1030, Share the exceptions | `tests/SqlSource.Tests/Generator/ExceptionReuseTests.cs` 547 ("The shared project of the manual's recipe") | `G/several-projects.md#share-the-exceptions` |
| `settings.md` 471-526, Converters | `examples/PostgresExamples/Methods/EmailAddress.cs`, `OrderId.cs`, `Price.cs`, and the same three of `examples/SqlServerExamples/Methods/` | `R/converters.md#converters` |
| `settings.md` 530-568, A class in a JSON column | `examples/PostgresExamples/Methods/DeliveryDetails.cs` 6; `examples/SqlServerExamples/Methods/DeliveryDetails.cs` 6 | `R/library-types-and-json.md#json-class` |
| `generator.md` 531-556, Query methods | `examples/PostgresExamples/Methods/OrderLineQueries.cs` 12; `examples/SqlServerExamples/Methods/OrderLineQueries.cs` 12 | `R/query-methods.md#query-methods` |
| `generator.md` 828, the bold words "Give the method the transaction" | `tests/DatabaseTests/SqlServer.SyncAsync/KnownDifferences.cs` 12 and 22 quote them | `R/connection-methods.md#method-connection`.  The rewrite of phase 17 keeps the words or changes the test's comment and message |
| `postgresql.md` 490, the bold words "A synchronous method needs a data source without multiplexing" | `tests/DatabaseTests/Postgres.SyncAsync/KnownDifferences.cs` 10 and 18 quote them | `D/postgresql/query-methods.md#method-results`.  The same, in phase 19 |
| `postgresql.md` 469-493, What a method reads and throws | `examples/PostgresExamples/Methods/StoreDataSource.cs` 9 | `D/postgresql/query-methods.md#method-results` |
| `settings.md` 43 and 212-224, the list of generator parameters | `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs` 59 says where the list is written | `R/generator-parameters.md#setting-generator-parameters`.  The Values cell is gone, so the comment names one place fewer |
| Every `sql` sample of every page | `tests/SqlSource.Repository.Tests/Documentation/UserDocsSamplesTests.cs` parses it by its info string | Wherever the sample goes.  The info string goes with it |
| `README.md` 35-51, 62-70 and 93-99, the walkthrough's samples | None today | `S/first-query.md`.  New rule: its samples are the files of `examples/PostgresExamples/Repository` and `examples/SqlServerExamples/Repository`, which compile |

## 9. Settled points

### Settled on the first draft

These points were open on the first draft of this map.  The outline now states each one, points 1 to 5 under "My calls", which the owner has not ruled on.  Each is applied in every section above.

1. **The settings file has its own page.**  `R/settings-file.md`, titled "sqlsource.json", in the Settings group, takes `#settings-files` and `#limit-symbolic-link`; `R/settings.md` keeps the places table, the matrix, precedence, and the output, database and dialect settings.
2. **Every database folder has five file names**: `index.md`, `nullability.md`, `types.md`, `libraries.md`, `query-methods.md`.  `libraries.md` takes NodaTime, NetTopologySuite and JSON, and the table of section 1 says what phases 4 and 20 must do about `D/sqlserver/query-methods.md`.
3. **`--prerelease` keeps one neutral sentence beside it**, on `S/install.md` and in both readmes: "The packages are published as previews, so the commands include `--prerelease`."  The first stable release takes out the flag and the sentence.
4. **The 18 anchors of `concepts.md` retire** (17 terms and `glossary`), in phase 10, with the page, and their 28 links are retargeted as section 6.3 says.  No reference heading takes a term's anchor as a second `<a id>`.  Every other merge keeps both anchors (section 5).
5. **The limit about Dapper stays on the guide**, as `G/dapper.md#limit-parameter-names`.
6. **Each rewrite phase recounts its pages** and reports the count.  Phase 25, `resize`, applies the splits that section 1 names where a page then passes 3,500 words.  No test holds a size.
7. **The preamble of `diagnostics.md` goes early**, in phase 2.  That phase adds the "Reported by" line to every section and the "Database" line to the 18 sections of C-04, so no section depends on a sentence that is gone, and no note waits on `R/errors.md` for the rewrite.  Phase 2 writes `site/errors.md` at the root of `site/`, since the folders do not exist yet, and phase 3 moves it to `site/reference/errors.md`.

### Settled by the outline: the table of the 161 error titles is not kept

The first draft left this to the owner.  The outline settles it under "My calls": no table of the 161 error titles is kept.

- `R/errors.md` keeps the unique facts of the preamble and the 29 area names, each with its ids as links (`#errors-by-area`, 29 rows).
- The row "the 161 rows of id and title" of the preamble map is `removed-preamble`, and `DiagnosticsDocumentTests.Document_Table_ListsEveryDescriptorWithItsTitleAndAnchor` goes with the tables in phase 2.
- A reader who knows a title and not an id uses the browser's find on `diagnostics.md`, where each title is a bold line.  The site's search has one entry for the whole page.
- The headings of `diagnostics.md` stay bare ids, which is also a call of the outline.

If the owner later wants the titles kept, this changes: a new page `R/error-index.md` in the Errors group takes the anchor `errors-by-area` and the 161 rows under the 29 areas (about 1,900 words), that row becomes `move`, and the test is retargeted and not deleted.

### Brought into line with the outline

The outline was revised after the first draft of this map, and the outline wins.  These things changed in the map:

- **Phase 2 adds both lines.**  "Reported by" goes into every section and "Database" into the 18 sections of C-04 in phase 2.  No note of the reading rule is kept on `R/errors.md`.
- **The one paragraph of `diagnostics.md`** is fixed at four statements (section 3).
- **A section of a diagnostic has the outline's order**: the title, "Reported by", "Database", what happened, why, the fix, a sample, `See:`.
- **Limits move with their pages.**  Each of the 36 has its phase (section 4), and no limit waits for a rewrite.
- **Each correction has its phase** (section 5).  C-06 lands in phase 10, since its sentence is true until Get started is written.
- **A merge keeps both anchors.**  The notes of `R/members.md` and `R/repositories.md` that kept a second anchor only up to the merge of MP-01 and MP-05 are gone.  The term anchors of `concepts.md` still retire, in phase 10.
- **A rewrite adds no anchor.**  The h3 anchors of `D/sqlserver/query-methods.md` are made by phase 4 and are now five, not four, since a heading among bullets takes every bullet below it.  The five anchors of `R/settings-file.md` are made by phase 7.
- **New text has its phase** (the "Written in" lines and tables of section 3).
- **The table of the 161 titles is not kept**, and the point is settled.
- **Sizes**: each rewrite phase recounts and reports, and phase 25 splits.  A tenth entry of a group is allowed in phase 25, and does not wait for the owner.
- **Rewordings for decisions 4 and 5** are made by the rewrite phases 13 and 14, since a move changes no word.
- **The numbers of the decisions** are those of the outline: no glossary is decision 9, and the help links are decision 7 and a constraint.  The rule on where a setting is documented is "Where rules live", not a numbered decision.
- **"The owner's decision"** on the settings file's page and on the five file names is now "a call of the outline", as the outline lists both under "My calls".
- **The shape of a Get started page** ends with "Next:", as the outline says, with "Rules:" before it.
- **The shape of a guide** names its last part as the outline's `## Limits`, and keeps the heading itself for a page that owns a limit.
- **"Checked by review 2"** names the reviewers of the outline: the reviewer of the proposals, and the no-loss reviewer of phase 1.
- **The count of kept anchors** is 184, with 27 retired, not 183 and 28.
- **Section 10** is new.

### Brought into line with the phase briefs

The briefs of phases 0 to 11 were written from this map, and each checked its rows against the pages and the source.  These things changed in the map:

- **Counts.**  "Find what you need" has 49 rows at the baseline, and 30 of them leave in phase 10.  "What works today" has 39 rows.  The preamble of `diagnostics.md` has 204 of the page's 261 same-page links.
- **Words that a move makes wrong** are mended by the move, with the note `position` (the outline's "How a phase is done").
- **A merge pair** is placed with the second text under its own heading, one level down, and the phase that places the first text starts the worksheet.
- **Two corrections are new**, C-07 and C-08, and two have one more sentence: C-01 at `settings.md` 1047 and C-02 at `settings.md` 1049.  The sources of C-02 and C-04 name the files that hold the facts.
- **Each unit is placed once**: `README.md` 183, 250 and the third sentence of 244.
- **`postgresql.md` 473 stays on its page**, and each `#connecting` gets a new bullet in place of the one that moves.
- **The sentence of `D/other.md` that links `G/other-databases.md`** is written by phase 8, which makes that page.
- **`R/cli.md` gains six rows of options**, and `R/generator-parameters.md` the anchor `gp-default`.
- **The table of supported versions** has two more rows of sources: `generator.md` 569, and the tested versions of Microsoft.Data.SqlClient, which no page states today.
- **Section 6** lists the eight links that name a heading's slug, the six rules of `docs/deferred` and `docs/tech-debt`, and `MethodShapeTests.cs` 538, and says where the 18 bare links of `site/README.md` point between phases 3 and 10.
- **The landing page's sample pair** is the README's, and is the example's query without its `-- generator:` line.

## 10. Units by phase

This section turns the map by phase, for the phase documents.  It adds no decision: each line follows from sections 3 to 5.

- Line ranges are those of the baseline, `28e20e3c`.
- A count of moved words is `wc -w` over the baseline lines whose row is `move`, `merge-pair` or `corrected`.  Each line counts for the most specific row that holds it, and counts are rounded to ten.  A unit that is only restated does not count.
- "Creates" means that the phase makes the page.  A move phase makes each page and anchor that its units need, also a page that a later phase fills.
- A link from moved text to a section that has not moved yet names that section's page of today, and the phase that moves the section retargets it (section 6).
- Text that feature work adds to a page after the baseline is inventoried by the phase that moves that page.

### Phases 1 and 2: before any page moves

| Phase | Units | What the phase does with them |
|----|----|----|
| 1, `status-language` | Every `removed-status` row: `README.md` 7 and 13; tool readme 7; `site/README.md` 61-105; `settings.md` 36 and the Status column of 38-58; `tool.md` 20 | Takes them out.  Puts the neutral sentence beside `--prerelease` in place of each of the three sentences about a first release.  Places the pointer of `site/README.md` 101 in `settings.md#setting-type-overrides` |
| 2, `diagnostics-preamble` | `diagnostics.md` 1-346, about 4,400 words | Writes `site/errors.md` new, about 2,000 words.  Writes the one paragraph of four statements under the title.  Adds "Reported by" to the 161 sections and "Database" to 18 of them (C-04).  Places sentences 5 and 6 of line 315 in `tool.md#sidecar-files`, and the last sentence of line 333 in `settings.md#converters` if that section does not say it.  Removes the preamble |

### Phase 3, `skeleton`: about 10,400 words moved

| Old page | Lines | Words | Lands on |
|----|----|----|----|
| `site/README.md` | 1-57, 107-119 | 1,260 | `index.md`, as the first landing page |
| `sql-files.md` | 1-194 | 2,490 | Creates `R/sql-files.md` |
| `sql-files.md` | 200 | 250 | Creates `R/sidecars.md`, for one limit.  Phase 5 fills the page |
| `parameters-and-tokens.md` | 22-156, 260 | 3,270 | Creates `R/parameters.md` |
| `parameters-and-tokens.md` | 1-18, 160-254 | 1,380 | Creates `R/tokens.md` |
| `dialects.md` | 1-128 | 1,450 | Creates `R/dialects.md` |
| `dialects.md` | 132-145 | 220 | Creates `D/index.md`, for `#database-support`.  Phase 4 fills the page |
| `dialects.md` | 145 (sentences 4 and 5), 147 | 100 | Creates `D/other.md`, whole |
| `site/errors.md`, which phase 2 wrote | All | 2,000 | `R/errors.md` |
| `parameters-and-tokens.md`, `dialects.md` | 261, 262; 153 | | Three pointers go |

The phase also creates, with no text of today: the three `toc.yml`; `G/troubleshooting.md`, with its title, its six h2 headings and four entries (two limits and two pointers).  The pages not yet moved stay at the root of `site/` and stand in the sidebar they will join.  It removes `site/README.md`, `sql-files.md`, `parameters-and-tokens.md` and `dialects.md`.

### Phase 4, `move-databases`: about 22,300 words moved

| Old page | Lines | Words | Lands on |
|----|----|----|----|
| `postgresql.md` | 1-72 but line 25, 379-405, 525-526 | 1,830 | Creates `D/postgresql/index.md` |
| `postgresql.md` | 76-148, 524 | 2,470 | Creates `D/postgresql/nullability.md` |
| `postgresql.md` | 152-247 | 1,930 | Creates `D/postgresql/types.md` |
| `postgresql.md` | 251-375 | 2,010 | Creates `D/postgresql/libraries.md` |
| `postgresql.md` | 409-518, 528 | 2,660 | Creates `D/postgresql/query-methods.md` |
| `sqlserver.md` | 1-90 but line 28, 369-393, 549, 567-571 | 1,970 | Creates `D/sqlserver/index.md` |
| `sqlserver.md` | 94-142, 530-532 | 1,560 | Creates `D/sqlserver/nullability.md` |
| `sqlserver.md` | 146-213, 522-528 | 1,150 | Creates `D/sqlserver/types.md` |
| `sqlserver.md` | 217-365 | 2,430 | Creates `D/sqlserver/libraries.md` |
| `sqlserver.md` | 397-516, 534-547, 555-565 | 4,180 | Creates `D/sqlserver/query-methods.md`, with its five new h3 headings |
| `postgresql.md`, `sqlserver.md` | 25; 28 | 60 | Adds to `D/index.md`, beside the new table of `#supported-versions`, and under `#azure-sql` |
| `postgresql.md`, `sqlserver.md` | 527, 529; 551, 553 | | Four pointers go |

The phase places 14 limits and one pointer's entry, makes correction C-05, and removes both pages.

### Phase 5, `move-tool`: about 12,900 words moved

| Old page | Lines | Words | Lands on |
|----|----|----|----|
| `tool.md` | 1-29, 355-369, 441-567, 631 | 2,410 | Creates `R/cli.md` |
| `tool.md` | 33-50, 250-351, 632, 635-637 | 2,340 | Creates `R/describing.md` |
| `tool.md` | 54-127, 638 | 2,000 | Creates `R/connections.md` |
| `tool.md` | 131-206, 634 | 1,410 | Adds to `R/sidecars.md`, which phase 3 made, and fills it |
| `tool.md` | 210-246 | 1,440 | Creates `R/msbuild.md`.  Phase 7 fills the page |
| `tool.md` | 394-405, 571-625 | 1,680 | Creates `R/bundles.md` |
| `tool.md` | 373-390, 409-437 | 860 | Creates `G/report-a-bug.md` |
| `recipes.md` | 125-181 | 260 | Creates `G/ci.md` |
| `recipes.md` | 185-196 | 60 | Creates `S/keep-up-to-date.md`.  Phase 10 writes the page |
| `recipes.md` | 200-254 | 460 | Creates `G/describe-on-save.md` |
| `tool.md` | 633 | | One pointer goes |

The phase places 6 limits, makes corrections C-01 (at `tool.md` 235) and C-03, writes the six rows of options of `R/cli.md#options`, and removes `tool.md`.  `recipes.md` stays, less its lines 125-254.

### Phase 6, `move-generated-code`: about 24,000 words moved

| Old page | Lines | Words | Lands on |
|----|----|----|----|
| `generator.md` | 1-32, 81-231 | 2,110 | Creates `R/members.md` |
| `settings.md` | 716-818 | 1,720 | `R/members.md` |
| `generator.md` | 235-352, 454-527 | 2,700 | Creates `R/models.md` |
| `settings.md` | 257-331 | 700 | `R/models.md` |
| `generator.md` | 356-381, 413-450 | 1,440 | Creates `R/model-properties.md` |
| `settings.md` | 572-619 | 1,260 | `R/model-properties.md` |
| `generator.md` | 531-571, 939-1000, 1058-1076, 1186 | 2,810 | Creates `R/query-methods.md` |
| `generator.md` | 832-852 | 540 | Creates `R/return-types.md` |
| `settings.md` | 335-393 | 1,220 | `R/return-types.md` |
| `generator.md` | 575-739, 1004-1054 | 2,850 | Creates `R/repositories.md` |
| `settings.md` | 930-955 | 260 | `R/repositories.md` |
| `generator.md` | 743-828 | 1,260 | Creates `R/connection-methods.md` |
| `generator.md` | 856-935, 1187 | 1,680 | Creates `R/collection-methods.md` |
| `generator.md` | 1080-1176, 1184-1185 | 1,560 | Creates `R/exceptions.md` |
| `settings.md` | 914-926 | 190 | `R/exceptions.md` |
| `generator.md` | 36-77, 1182, 1190 | 960 | Creates `R/attributes.md`, a page of the Settings group.  Phases 7 and 8 add to it |
| `generator.md` | 385-409 | 420 | Creates `R/type-mapping.md`.  Phase 7 fills the page |
| `generator.md` | 1183 | 90 | Creates `G/dapper.md`, for one limit.  Phase 8 fills the page |
| `generator.md` | 1188 | 130 | Adds to `D/postgresql/query-methods.md`, which phase 4 made |
| `generator.md` | 1189 | 110 | Adds to `D/sqlserver/query-methods.md`, which phase 4 made |

The phase places 8 limits, makes correction C-02 at `settings.md` 953, and removes `generator.md`.  `settings.md` stays, less the lines above.

### Phase 7, `move-settings`: about 14,900 words moved

| Old page | Lines | Words | Lands on |
|----|----|----|----|
| `settings.md` | 1-189 | 2,920 | Creates `R/settings.md` |
| `settings.md` | 193-253 | 1,380 | Creates `R/generator-parameters.md` |
| `settings.md` | 1011-1051, 1057 | 1,450 | Creates `R/settings-file.md` |
| `settings.md` | 397-467, 1069-1070 | 2,750 | Adds to `R/type-mapping.md`, which phase 6 made, and fills it |
| `settings.md` | 471-526, 1071-1087 | 1,870 | Creates `R/converters.md` |
| `settings.md` | 530-568, 623-712 | 2,530 | Creates `R/library-types-and-json.md` |
| `settings.md` | 822-847 | 350 | Adds to `R/attributes.md`, which phase 6 made |
| `settings.md` | 884-910, 959-1007, 1058-1068 | 1,340 | Adds to `R/msbuild.md`, which phase 5 made, and fills it |
| `settings.md` | 851-880 | 330 | Creates `G/folders-and-files.md`.  Phase 8 adds to it |

The phase places 6 limits, writes the table of places and the matrix of 22 rows, makes corrections C-01 (at lines 153 and 1047) and C-02 (at lines 3, 28, 34 and 1049), gives the link of C-05 its last target, and removes `settings.md`.

### Phase 8, `move-guides`: about 8,500 words moved

| Old page | Lines | Words | Lands on |
|----|----|----|----|
| `recipes.md` | 27-65, 93-121 | 280 | Creates `G/several-databases.md` |
| `recipes.md` | 69-89 | 80 | Adds to `G/folders-and-files.md`, which phase 7 made |
| `recipes.md` | 258-300 | 230 | Creates `S/transactions.md`.  Phase 10 writes the page |
| `recipes.md` | 304-325 | 160 | Creates `G/many-rows.md` |
| `recipes.md` | 329-369 | 260 | Creates `G/sync-and-streaming.md` |
| `recipes.md` | 373-439 | 280 | Creates `G/repository-connection.md` |
| `recipes.md` | 443-493 | 250 | Creates `G/custom-types.md` |
| `recipes.md` | 497-587 | 440 | Creates `G/json.md` |
| `recipes.md` | 591-920 | 1,800 | Adds to `G/dapper.md`, which phase 6 made, and fills it |
| `recipes.md` | 924-939 | 150 | Creates `G/dynamic-sql.md` |
| `recipes.md` | 943-961 | 130 | Creates `G/lists.md` |
| `recipes.md` | 965-1030 | 380 | Creates `G/several-projects.md` |
| `README.md` | 132-183 | 500 | `G/dapper.md`; line 181 creates `G/other-databases.md`, which phase 9 writes.  Phase 8 also writes the sentence of `D/other.md` that links the new page |
| `README.md` | 187-189 | 110 | Creates `S/testing.md`.  Phase 10 writes the page |
| `README.md` | 191-193 | 160 | `S/transactions.md` |
| `README.md` | 195-208 | 490 | Adds to `G/troubleshooting.md`, which phase 3 made |
| `README.md` | 210-236 | 570 | Creates `G/naming-and-placement.md`.  Adds to `G/several-databases.md`, `G/folders-and-files.md`, `G/dynamic-sql.md`, `G/lists.md`, `G/several-projects.md` and `G/report-a-bug.md` |
| `README.md` | 238-250 | 530 | Creates `G/nodatime-and-spatial.md`, which takes line 250 as its last line; line 248 goes to `G/json.md` |
| `README.md` | 252-288 | 120 | `G/custom-types.md` |
| `README.md` | 9, 19-29 | 490 | Adds to `D/index.md`, `R/cli.md`, `R/attributes.md` and `R/msbuild.md`.  The README keeps its own text until phase 11 |
| `concepts.md` | 27-139 | 1,060 | Adds each term to its section, on twelve pages of `reference/` that phases 3 to 7 made |
| `concepts.md` | The page, 1,360 words | | `site/start/concepts.md`, unchanged, until phase 10 |
| `recipes.md` | 1-23 | | Navigation; goes |

The phase places no limit.  It makes corrections C-07 (at `README.md` 26) and C-08 (at `concepts.md` 29), leaves a pointer under each README heading whose text it moves, retargets the 28 links to a term, and removes `recipes.md`.  After it, the root of `site/` holds `index.md` and `diagnostics.md` alone.

### Units that land apart from the rest of their page

Each of these is placed by a phase other than the one that fills its new page.

| Unit | Lands on | Placed in | The page's main phase |
|----|----|----|----|
| `site/README.md` 101 | `settings.md#setting-type-overrides`, then `R/type-mapping.md#setting-type-overrides` | 1, then 7 with its section | 7 |
| `diagnostics.md` 315, sentences 5 and 6 | `tool.md#sidecar-files`, then `R/sidecars.md#sidecar-files` | 2, then 5 with its section | 5 |
| `diagnostics.md` 333, last sentence | `settings.md#converters`, then `R/converters.md#converters` | 2, then 7 with its section | 7 |
| `sql-files.md` 200 | `R/sidecars.md#limit-file-in-two-projects` | 3, which creates the page | 5 |
| `dialects.md` 132-145 | `D/index.md#database-support` | 3, which creates the page | 4 |
| `tool.md` 210-246 | `R/msbuild.md#running-in-the-build` | 5, which creates the page | 7 |
| `recipes.md` 185-196 | `S/keep-up-to-date.md#after-a-migration` | 5, which creates the page | 10 |
| `generator.md` 385-409 | `R/type-mapping.md#property-types` | 6, which creates the page | 7 |
| `generator.md` 1183 | `G/dapper.md#limit-parameter-names` | 6, which creates the page | 8 |
| `generator.md` 1188 and 1189 | `D/postgresql/query-methods.md#limit-connection-string-repository`; `D/sqlserver/query-methods.md#limit-rowcount-after-nocount` | 6 | 4 |
| `settings.md` 822-847 | `R/attributes.md#setting-source` | 7 | 6 |
| `settings.md` 851-880 | `G/folders-and-files.md#configure-a-folder` | 7, which creates the page | 8 |
| `recipes.md` 258-300; `README.md` 187-193 | `S/transactions.md#queries-in-a-transaction`; `S/testing.md` | 8, which creates both pages | 10 |
| `README.md` 181 | `G/other-databases.md#dialect-and-output` | 8, which creates the page | 9 |
| `README.md` 9 and 19-29 | `D/index.md#supported-versions`; `R/cli.md#install-the-tool`; `R/attributes.md#what-the-package-adds`; `R/msbuild.md#build-requirements` | 8 | 4; 5; 6; 7 |
| `README.md` 234-236 | `G/report-a-bug.md#report-a-bug` | 8 | 5 |
| `concepts.md` 27-139 | The reference section of each term | 8 | 3 to 7 |

### Phase 9, `write-guides`: about 11,800 words written

The words are the page's size in section 1 less its moved text, so each is an upper bound: part of it is the text of the fixed shape that phase 12 adds around the moved sections.  The outline gives this phase at most 11,800 words.

| Page | Phase 9 writes | Words |
|----|----|----|
| `G/nullability.md` | The whole page | 1,000 |
| `G/output-modes.md` | The whole page | 800 |
| `G/other-databases.md` | The whole page but one bullet | 650 |
| `G/dapper.md` | The walkthrough under `models`, for both databases | 400, which the page's 2,300 words do not include |
| `G/custom-types.md` | `vogen`, `enum` | 1,050 |
| `G/naming-and-placement.md` | `async-names`, `model-names`, `record-or-class`, `collection-type-task`, `placement` | 1,000 |
| `G/lists.md` | `postgresql-arrays`, `large-and-empty-lists` | 900 |
| `G/sync-and-streaming.md` | `stream-rows` | 750 |
| `G/repository-connection.md` | `mark-the-member` | 700 |
| `G/many-rows.md` | `transaction`, `failures` | 650 |
| `G/json.md` | `json-in-text` | 550 |
| `G/several-databases.md` | `repository-on-two` | 550 |
| `G/describe-on-save.md` | `start-the-watch` | 550 |
| `G/ci.md` | `exit-code` | 550 |
| `G/dynamic-sql.md` | `clause`, `safety` | 450 |
| `G/folders-and-files.md` | `class-for-a-folder` | 450 |
| `G/several-projects.md` | `one-file-two-projects` | 350 |
| `G/report-a-bug.md` | `what-is-safe` | 350 |
| `G/troubleshooting.md` | The entries that are not a limit's | 100 |
| `G/nodatime-and-spatial.md` | Nothing: all five sections hold moved text | 0 |

### Phases 10 and 11

Phase 10, `get-started`, writes the nine pages of `start/`, about 8,500 words by section 1, and the finished `index.md`, 550 words.  It also does this:

- It rewrites the three units that phases 5 and 8 placed on `start/` pages, and merges MP-13.
- It places the samples and the diagram that a row moves to a `start/` page: `README.md` 13-17, 35-51, 62-70, 76-85, 93-99 and 112-124, and `concepts.md` 3-15.
- It takes out the sentence of correction C-06, the 30 rows of the baseline that leave the table of `index.md`, and the row that phase 2 added.
- It retargets the links that phases 3 to 8 parked for a `start/` page: `docs/architecture/README.md` 46 to `site/start/how-it-works.md#words`, and the "step 5" link of `R/cli.md#install-the-tool` to `S/first-query.md#describe`.
- It removes `site/start/concepts.md`.

Phase 11, `readmes`, cuts both readmes to the outlines of section 7, about 250 lines out.

### Phases 12 to 20: the rewrites of the pages

"Words" is the sum of the page sizes of section 1, at today's density.  "Text of today" is the moved text of those pages, measured: it is what a reviewer reads old against new.  New text of a move phase is in plain language already and is not rewritten.  For phase 12 both columns give the moved text, since phase 9 writes the rest of those pages.  The outline's limit for one rewrite is about 12,000 words.

| Phase | Pages, with the size of each | Words | Text of today | Merges |
|----|----|----|----|----|
| 12, `rewrite-guides` | The moved text of 18 guides: `dapper.md` 2,350; `report-a-bug.md` 970; `json.md` 550; `troubleshooting.md` 490; `several-projects.md` 470; `describe-on-save.md` 460; `folders-and-files.md` 460; `nodatime-and-spatial.md` 410; `several-databases.md` 370; `custom-types.md` 370; `repository-connection.md` 280; `ci.md` 260; `sync-and-streaming.md` 260; `dynamic-sql.md` 230; `lists.md` 210; `many-rows.md` 160; `naming-and-placement.md` 90; `other-databases.md` 40 | 8,430 | 8,430 | MP-12, MP-14 to MP-22 |
| 13, `rewrite-sql-files` | `R/sql-files.md` 2,500; `R/parameters.md` 3,350; `R/tokens.md` 1,450; `R/dialects.md` 1,450 | 8,750 | 8,870 | MP-10: query, marker, file header, query parameter, token |
| 14, `rewrite-settings-1` | `R/settings.md` 2,600; `R/settings-file.md` 1,500; `R/generator-parameters.md` 1,500; `R/attributes.md` 1,600; `R/msbuild.md` 3,200 | 10,400 | 10,500 | MP-06, MP-07; MP-10: output, dialect, logical database, settings file, generator parameter |
| 15, `rewrite-settings-2` | `R/type-mapping.md` 3,400; `R/library-types-and-json.md` 2,500; `R/converters.md` 2,050 | 7,950 | 7,610 | None |
| 16, `rewrite-generated-code-1` | `R/members.md` 3,850; `R/models.md` 3,350; `R/model-properties.md` 2,700; `R/return-types.md` 1,750 | 11,650 | 11,750 | MP-01 to MP-04; MP-10: generator, token method |
| 17, `rewrite-generated-code-2` | `R/query-methods.md` 3,000; `R/repositories.md` 3,150; `R/connection-methods.md` 1,250; `R/collection-methods.md` 1,800; `R/exceptions.md` 1,950 | 11,150 | 10,780 | MP-05; MP-10: query method, repository |
| 18, `rewrite-tool` | `R/cli.md` 2,600; `R/describing.md` 2,450; `R/connections.md` 1,900; `R/sidecars.md` 1,750; `R/bundles.md` 1,650 | 10,350 | 10,320 | MP-08, MP-11; MP-10: tool, describe, sidecar |
| 19, `rewrite-postgresql` | `D/postgresql/index.md` 1,900; `nullability.md` 2,450; `types.md` 1,900; `libraries.md` 2,000; `query-methods.md` 2,800; `D/index.md` 700; `D/other.md` 600 | 12,350 | 11,560 | MP-09, MP-23 |
| 20, `rewrite-sqlserver` | `D/sqlserver/index.md` 2,100; `nullability.md` 1,700; `types.md` 1,150; `libraries.md` 2,400; `query-methods.md` 4,450 | 11,800 | 11,400 | None |

Three notes on the table:

- **The cut of the Generated code group.**  The group is 22,800 words.  Cut in sidebar order it gives 12,900 and 9,900 words, whichever side `R/query-methods.md` is on.  So the cut is by subject.  Phase 16 takes what is generated and what a result looks like: members, models, model properties, return types.  Phase 17 takes the methods and what they throw.
- **Phase 19 is over the limit by the page sizes**, at 12,350 words, and under it by its text of today, at 11,560.  The difference is new text of phases 3 and 4, the table of `D/index.md` and most of `D/other.md`, which has no rewrite.  The phase reports its own count.
- **`R/errors.md`, the `start/` pages, `index.md` and the sections that phase 9 writes** are written new and are in no rewrite.

### Phases 21 to 24: the rewrites of `diagnostics.md`

The 161 sections are 43,336 words today, counted from each `## SQLSRCnnn` heading to the next.  The four ranges below are the most even cut into four runs of ids: the largest and the smallest differ by 610 words.

| Phase | Ids | Sections | Words |
|----|----|----|----|
| 21, `rewrite-diagnostics-1` | SQLSRC001 to SQLSRC121 | 55 | 10,928 |
| 22, `rewrite-diagnostics-2` | SQLSRC122 to SQLSRC225 | 32 | 11,175 |
| 23, `rewrite-diagnostics-3` | SQLSRC226 to SQLSRC260 | 35 | 10,668 |
| 24, `rewrite-diagnostics-4` | SQLSRC261 to SQLSRC299 | 39 | 10,565 |

Phase 2 adds one or two lines to each section before these phases run, so each phase recounts its range.  The outline leaves the ranges to the builder; these are the measured start.

### Phases 25 and 26

Phase 25, `resize`, takes the counts that phases 12 to 24 report and makes the splits that section 1 names.  Phase 26, `close`, removes the ledger, the inventory and the checker, and keeps this map as the record of the plan.
