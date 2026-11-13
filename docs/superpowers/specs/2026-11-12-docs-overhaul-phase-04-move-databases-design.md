# Docs overhaul, phase 04: move-databases

The outline is [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md), and the content map is [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).  Both win over this brief.
Done by: an Opus agent.  Needs: phases 0 to 3.  Runs alone.

## Goal

`site/postgresql.md` and `site/sqlserver.md` become ten pages under `site/reference/databases/`, five for each database, with no word changed.  `databases/index.md` gains the one table of supported versions.  The 18 known-issue bullets of the two pages become 14 limits, and 4 pointers go.  `SupportedVersionsTests` holds the table to the code, and `SiteStructureTests` holds the two folders to the same five file names.

## Where things stand

How a phase is done, the ledger, the reviews and publishing are in the outline's [Delivery](2026-11-12-docs-overhaul-epic-design.md#delivery).  This brief adds only what is specific to phase 4.

At the baseline, `28e20e3c`:

- `site/postgresql.md` has 529 lines, 11,007 words, 19 `<a id>` anchors, 44 links to its own anchors and 6 known-issue bullets (lines 524 to 529).
- `site/sqlserver.md` has 571 lines, 11,427 words, 19 anchors, 47 links to its own anchors and 12 known-issue bullets (lines 522 to 571).
- The other pages of `site/` hold 162 links to the two pages.  Outside `site/` and `docs/superpowers`, 51 references in 17 files name them: 19 site URLs in `README.md` and 32 references in 16 other files.  `src/SqlSource.Tool/README.md` has none.

What phases 0 to 3 leave, taken as given:

- Both pages are at the root of `site/`, in the "Supported databases" group of `site/reference/toc.yml` as `../postgresql.md` and `../sqlserver.md`, and in `SiteStructureTests.PagesNotYetMoved`.
- `site/reference/databases/index.md` exists with `#database-support` (`dialects.md` 134-145), and `site/reference/databases/other.md` exists whole.  Phase 3 made both: this phase adds to the first and changes nothing in the second.
- `site/reference/databases/AGENTS.md` exists.  Its last bullet says that no database folder exists yet.
- `site/guides/troubleshooting.md` exists with its six h2 headings (`at-run-time`, `wrong-model`, `wrong-result`, `not-described`, `watch`, `secrets`), four entries, and one sentence that links the `#known-issues` of the five pages that still have them.
- `settings.md`, `generator.md`, `tool.md`, `recipes.md` and `concepts.md` are still at the root.  `sql-files.md`, `parameters.md`, `tokens.md`, `dialects.md` and `errors.md` are in `site/reference/`.
- Phase 3 retargeted the links of both pages to the pages it moved, so the pages differ from the baseline.  Line numbers in this brief are the baseline's.  In the working tree, find a section by its `<a id>`.

## In scope / Not in scope

In scope:

- The rows of the map's [Map: `site/postgresql.md`](2026-11-12-docs-overhaul/content-map.md#map-sitepostgresqlmd) and [Map: `site/sqlserver.md`](2026-11-12-docs-overhaul/content-map.md#map-sitesqlservermd), and the table "Phase 4" of its section 10.
- The 14 limits and the one pointer's entry that the map's limits table marks "4".
- The table of `#supported-versions` and the heading `#azure-sql` (map, section 5, "Supported versions: one home").
- Correction C-05.
- Every link and reference to the two pages, in `site/`, in `README.md` and in the rest of the repository.
- `SupportedVersionsTests`, the growth of `SiteStructureTests`, `site/reference/databases/AGENTS.md`.

Not in scope:

- Any change of words in moved text, but for the words that the split makes wrong (step 7).  Phases 19 and 20 rewrite these pages, and phase 19 merges MP-09 and MP-23.
- The text of `generator.md` 1188 and 1189, which phase 6 adds to two limits of these pages.
- The sentences of `README.md` 9 and 29, which phase 8 adds to `databases/index.md`.
- The restatements of the minimum versions on `index.md`, `start/install.md` and `README.md`, and the parts of `SupportedVersionsTests` that hold them (phases 10 and 11).
- `databases/other.md`.

## The work

### 1. Read the pages as they stand

Run `git diff 28e20e3c -- site/postgresql.md site/sqlserver.md`.  Each difference is a link that phases 1 to 3 retargeted, or text of feature work.  Add each unit of feature work to the phase's ledger and move it with the section it stands under.

### 2. Make the ten pages

`P/` is `site/reference/databases/postgresql/` and `M/` is `site/reference/databases/sqlserver/`.  Each block moves whole, in today's order within its page.  The words column is the map's count.

| Page | h1 | Baseline lines, in order | Words |
|----|----|----|----|
| `P/index.md` | `# PostgreSQL`, moved | 1-18; `connecting` 22-27 but 25; `what-describing-does` 31-35; `parameters` 39-57; `what-can-be-described` 61-72; `models` 379-405; limits 525, 526 | 1,830 |
| `P/nullability.md` | new | `nullability` 76-148; limit 524 | 2,470 |
| `P/types.md` | new | `type-map` 152-163; `which-npgsql` 167-174; `type-table` 178-233; `type-notes` 237-247 | 1,930 |
| `P/libraries.md` | new | `nodatime` 251-290; `nettopologysuite` 294-323; `json` 327-375 | 2,010 |
| `P/query-methods.md` | new | `query-methods` 409-427; `method-parameters` 431-441; `array-parameters` 445-465; `method-results` 469-493; `collection-methods` 497-518; limit 528 | 2,660 |
| `M/index.md` | `# SQL Server`, moved | 1-20; `connecting` 24-30 but 28; `what-describing-does` 34-36; `parameters` 40-64; `what-can-be-described` 68-90; `models` 369-393; limits 549, 567, 569, 571 | 1,970 |
| `M/nullability.md` | new | `nullability` 94-142; limits 530, 532 | 1,560 |
| `M/types.md` | new | `types` 146-157; `which-sqlclient` 161-168; `type-table` 172-201; `type-notes` 205-213; limit 522-528 | 1,150 |
| `M/libraries.md` | new | `nodatime` 217-269; `nettopologysuite` 273-319; `json` 323-365 | 2,430 |
| `M/query-methods.md` | new | `query-methods` 397-415; `method-parameters` 419-438; `list-parameters` 442-468; `method-results` 472-488; `collection-methods` 492-516; limits 534, 536-547, 555-565 | 4,180 |

That is about 22,300 words with the 60 words of step 5.

- **Heading levels.**  Every heading in the table is `##` on its new page.  So the 20 headings that are `###` today become `##`: PostgreSQL's at lines 167, 178, 237, 251, 294, 327, 431, 445, 469 and 497, and SQL Server's at lines 161, 172, 205, 217, 273, 323, 419, 442, 472 and 492.  The words of a heading do not change, and each `<a id>` line stays above its heading.
- **The eight new h1 headings** are new text: the database's name, a colon, and the sidebar title of the map's section 1.  `# PostgreSQL: which columns can be null`, `# PostgreSQL: types`, `# PostgreSQL: NodaTime, NetTopologySuite and JSON`, `# PostgreSQL: running queries`, and the same four with `SQL Server`.
- **Sample blocks** keep their info strings (`sql postgres`, `sql mssql`, `jsonc`, `console`, `csharp`, `xml`) and their indentation inside a bullet.

### 3. Cut "What a method reads and throws" on the SQL Server page

On `M/query-methods.md` alone, place five new h3 headings, each with its `<a id>`, between the bullets of `method-results`.  No bullet moves and none is cut.

| New anchor | Heading | Stands above baseline lines |
|----|----|----|
| `reading-a-row` | `### Reading a row` | 476-480 |
| `null-values` | `### Null values` | 481 |
| `errors-and-rollbacks` | `### Errors and rollbacks` | 482-484 |
| `rowcount` | ``### `rowcount` `` | 485 |
| `timeout-and-cancel` | `### Timeouts, cancelling and the connection` | 486-488 |

The two bullets on the transaction, 474 and 475, stay directly under the h2.  The PostgreSQL page gets no such headings.

### 4. Place the limits

A page with a limit ends with `<a id="limits"></a>` and `## Limits`.  Each bullet goes under it word for word, in today's order, under an h3 with its own `<a id>`.  The bullet stays a bullet, as in the two limits that phase 3 placed, so that the checker finds the unit.  The h3's words are new text: the cell "What the reader sees" of the map's limits table, cut to a heading.  Each limit gets one entry in `site/guides/troubleshooting.md`, in the form of phase 3's entries, under the heading in the last column.

| Limit | Bullet | Entry under |
|----|----|----|
| `P/nullability.md#limit-columns-treated-as-nullable` | pg 524 | `wrong-model` |
| `P/index.md#limit-operator-on-oid` | pg 525 | `not-described` |
| `P/index.md#limit-many-partitions` | pg 526 | `not-described` |
| `P/query-methods.md#limit-connection-string-repository` | pg 528 | `at-run-time` |
| `M/types.md#limit-ef-core-sqlclient` | ms 522-528 | `wrong-model`, as the nearest heading for a build error that is not about describing |
| `M/nullability.md#limit-columns-treated-as-nullable` | ms 530 | `wrong-model` |
| `M/nullability.md#limit-ansi-warnings-off` | ms 532 | `at-run-time` |
| `M/query-methods.md#limit-ansi-nulls-off` | ms 534 | `wrong-result` |
| `M/query-methods.md#limit-hidden-rollback` | ms 536-547 | `at-run-time` |
| `M/index.md#limit-several-result-sets` | ms 549 | `wrong-model` |
| `M/query-methods.md#limit-list-in-an-aggregate` | ms 555-565 | `at-run-time` |
| `M/index.md#limit-type-name-unicode` | ms 567 | `not-described` |
| `M/index.md#limit-parameter-name-unicode` | ms 569 | `not-described` |
| `M/index.md#limit-parameter-two-spellings` | ms 571 | `not-described` |

- **Four pointers go**: pg 527 and 529, ms 551 and 553.  Each only points, and its owner keeps the fact (row 9 of "The ledger").
- **One pointer still gets an entry**: "a function marked `IMMUTABLE` runs while a query is described", under `not-described`, linking `P/index.md#what-describing-does`.
- **Both `## Known issues` headings and both `known-issues` anchors go.**
- Seven pages end with `## Limits`.  `P/types.md`, `P/libraries.md` and `M/libraries.md` have none.

### 5. Fill `databases/index.md`

The order of the page becomes: Supported versions, Database support, Azure SQL.

1. Above `#database-support`, add `<a id="supported-versions"></a>`, `## Supported versions` and this table, as new text.

   | Database | Server versions supported | Server versions tested | Driver package | Driver versions supported | Driver versions tested | `dialect` | Pages |
   |----|----|----|----|----|----|----|----|
   | PostgreSQL | 14 or later | 14, 15, 16, 17, 18 | `Npgsql` | 8 or later | 8, 10 | `postgres` | [PostgreSQL](postgresql/index.md) |
   | SQL Server | 2017 or later | 2017, 2019, 2022, 2025 | `Microsoft.Data.SqlClient` | 6.1 or later | 6.1, 7 | `mssql` | [SQL Server](sqlserver/index.md) |

2. Under the table, place the bullet of pg 25 whole, and the first two sentences of the bullet of ms 28 ("SQL Server 2017 and later are supported" to the link of SQLSRC235), word for word.
3. After `#database-support`, add `<a id="azure-sql"></a>` and `## Azure SQL`, and under it the last sentence of ms 28, as a paragraph: "Azure SQL Database and Azure SQL Managed Instance are accepted whatever version they report, and are not tested."
4. In the table of `#database-support`, the two page links (`dialects.md` 136, 137) become `postgresql/index.md` and `sqlserver/index.md`.

No new sentence says that Azure SQL is supported.  It is accepted, and not tested.

Where each cell of the table comes from: the server cells from the bullets of pg 25 and ms 28; "8 or later" and "6.1 or later" from pg 167-174 and ms 161-168; "8, 10" from pg 473.  The cell "6.1, 7" has no sentence of today behind it: no page states the versions of Microsoft.Data.SqlClient that SqlSource is tested on.  Its source is the test projects: `tests/DatabaseTests/SqlServer.SqlClient61/SqlServer.SqlClient61.csproj`, which pins Microsoft.Data.SqlClient 6.1.7, and `Directory.Packages.props`, which gives every other test project 7.1.1.  The ledger's `new` row for the table names both files.

### 6. The bullet that stays under "Connecting"

The bullets of pg 25 and ms 28 leave their pages, so each `#connecting` gets one new bullet in their place, as new text:

- `P/index.md`: `- **The supported versions of PostgreSQL** are in [Supported versions](../index.md#supported-versions).  An older server is [SQLSRC235](../../../diagnostics.md#sqlsrc235).`
- `M/index.md`: `- **The supported versions of SQL Server** are in [Supported versions](../index.md#supported-versions), and what holds for Azure SQL is in [Azure SQL](../index.md#azure-sql).  An older server is [SQLSRC235](../../../diagnostics.md#sqlsrc235).`

### 7. Links inside `site/`

Every relative link of moved text is re-based: a page of the root is `../../../<page>.md`, a page of `reference/` is `../../<page>.md`, and `databases/index.md` is `../index.md`.  A link to an anchor of the old page names the new page of that anchor when it is not the link's own page:

| Page | Anchors of PostgreSQL | Anchors of SQL Server |
|----|----|----|
| `index.md` | `connecting`, `what-describing-does`, `parameters`, `what-can-be-described`, `models` | the same five |
| `nullability.md` | `nullability` | `nullability` |
| `types.md` | `type-map`, `which-npgsql`, `type-table`, `type-notes` | `types`, `which-sqlclient`, `type-table`, `type-notes` |
| `libraries.md` | `nodatime`, `nettopologysuite`, `json` | the same three |
| `query-methods.md` | `query-methods`, `method-parameters`, `array-parameters`, `method-results`, `collection-methods` | `query-methods`, `method-parameters`, `list-parameters`, `method-results`, `collection-methods` |

Links to `#known-issues`, each by the map's section 6.1:

| Link at | New target |
|----|----|
| pg 427 | `#limit-connection-string-repository`, same page |
| ms 118 | `#limit-ansi-warnings-off`, same page |
| ms 166 | `#limit-ef-core-sqlclient`, same page |
| ms 451, 452 | `#limit-list-in-an-aggregate`, `#limit-ansi-nulls-off`, same page |
| ms 484 | `#limit-hidden-rollback`, same page |
| pg 493; ms 312, 485, 488 | Stay `../../../generator.md#known-issues`: phase 6 makes their limits and retargets them |
| `generator.md` 702; `recipes.md` 410 | `reference/databases/postgresql/query-methods.md#limit-connection-string-repository` |
| `reference/parameters.md`, the link of `parameters-and-tokens.md` 91 | `databases/sqlserver/query-methods.md#limit-list-in-an-aggregate` |
| `guides/troubleshooting.md`, the sentence that links the known issues of the pages not yet moved | Its links to `../postgresql.md#known-issues` and `../sqlserver.md#known-issues` go |

**Words that the split makes wrong.**  Each page of today is five pages now, so "This page is about" (pg 3, ms 3), and each "above", "below" or "a later bullet" whose referent left its page (pg 161 and 433 among them), no longer says what is true.  Find them with `grep -n -E "This page|above|below|later bullet" site/reference/databases/*/*.md`.  Change the fewest words that make each phrase true, such as "These pages are about" or the name of the section in place of "above", and give the unit's row the note `position`.  Change no other word.

**Interim links this phase retargets**, each left by phase 3:

| Link | Parked at | Becomes |
|----|----|----|
| `parameters-and-tokens.md` 91, on `reference/parameters.md` | `../sqlserver.md#known-issues` | `databases/sqlserver/query-methods.md#limit-list-in-an-aggregate` |
| Two links of the one sentence of `guides/troubleshooting.md` | `../postgresql.md#known-issues`, `../sqlserver.md#known-issues` | They go: each limit has its entry |
| pg 3 and ms 3, the first link of each | `settings.md#setting-dialect` | `../../../settings.md#setting-dialect`, re-based.  Phase 7 gives it its last target |

**Interim links this phase leaves:**

| Link | Now on | Parked at | The map's target | Phase |
|----|----|----|----|----|
| pg 493; ms 312 and 488 | `P/query-methods.md`; `M/libraries.md` and `M/query-methods.md` | `../../../generator.md#known-issues` | `R/query-methods.md#limit-wrapped-connection` | 6 |
| ms 485 | `M/query-methods.md` | `../../../generator.md#known-issues` | `#limit-rowcount-after-nocount`, on its own page | 6 |
| `tool.md` 567, two links | `tool.md`, at the root | Each database's `index.md#connecting` | `D/index.md#supported-versions` | 5 |
| `docs/tech-debt/TD-0067-...md` 13 | The same file | `site/settings.md#known-issues` | `site/reference/type-mapping.md#limit-nullable-type-argument` | 7 |
| pg 3 and ms 3, the first link of each | Each database's `index.md` | `../../../settings.md#setting-dialect` | `R/settings.md#setting-dialect` | 7 |

Then retarget every other link to the two pages.  `git grep -n -E "\]\((\.\./)*(postgresql|sqlserver)\.md" -- site` lists them: at the baseline `diagnostics.md` 37, `generator.md` 37, `settings.md` 32, `parameters-and-tokens.md` 8, `recipes.md` 6, `tool.md` 4, `dialects.md` 2, and the 36 of `site/README.md` that phase 3 moved to `index.md`.  A link with no anchor names the database's `index.md`.

### 8. `README.md`

Its 19 site URLs to the two pages (baseline lines 101, 126, 130, 202, 204, 240, 242, 244, 246, 248, 254, 298) change address only, by the table of step 7: `https://mbcrawfo.github.io/SqlSource/reference/databases/<postgresql|sqlserver>/<page>.html#<anchor>`, and `.../index.html` for a URL with no anchor.  No word of the README changes.

### 9. References outside `site/`

`git grep -n -E "site/(postgresql|sqlserver)\.md|SqlSource/(postgresql|sqlserver)\.html" -- . ':!site' ':!docs/superpowers'` lists them.  Each takes the page of its anchor.  The ones that need more than that:

| Where (baseline line) | Change |
|----|----|
| `docs/architecture/postgres-describer.md` 3; `sqlserver-describer.md` 3; `examples/SqlServerExamples/README.md` 3 | The database's `index.md` |
| `docs/architecture/sqlserver-describer.md` 289 | `site/reference/databases/sqlserver/nullability.md#limit-ansi-warnings-off` |
| `docs/tech-debt/TD-0071-...md` 24 | `site/reference/databases/sqlserver/nullability.md#limit-columns-treated-as-nullable` |
| `docs/tech-debt/TD-0067-...md` 13 | C-05: the link and its text become `site/settings.md` and `../../site/settings.md#known-issues`, where the bullet is.  Phase 7 gives it its last target |
| `docs/deferred/D-0006-...md` 9 | Its two links take `index.md#what-can-be-described` and `query-methods.md#query-methods`.  The words "lists it among its known issues" become a link to `site/reference/databases/sqlserver/index.md#limit-several-result-sets` |
| `tests/DatabaseTests/Postgres.SyncAsync/KnownDifferences.cs` 10, 18 | The path becomes `site/reference/databases/postgresql/query-methods.md`, in the comment and in the string.  The quoted words do not change |
| `tests/DatabaseTests/Postgres.SyncAsync/AGENTS.md` 22; `SqlServer.SyncAsync/AGENTS.md` 24 | `site/reference/databases/postgresql/query-methods.md`; `site/reference/databases/sqlserver/query-methods.md` |
| `examples/PostgresExamples/Methods/StoreDataSource.cs` 9 | `site/reference/databases/postgresql/query-methods.md` |
| `tests/DatabaseTests/SqlServer/EndToEnd/MethodShapeTests.cs` 538 | "as the manual's known issue says" becomes "as the manual's limit says", with the path of `limit-hidden-rollback`.  No test checks this line |
| `src/SqlSource.Tool/AGENTS.md` 15 | "and in `postgresql.md` and `sqlserver.md`" becomes "and in `reference/databases/`".  The file goes from 8,159 bytes to 8,147, under its 8,192 |

Root `AGENTS.md` rule 5 names no page of the site, so it does not change.

### 10. The sidebar

In `site/reference/toc.yml`, the entries `../postgresql.md` and `../sqlserver.md` of "Supported databases" become two nested groups, between `databases/index.md` and `databases/other.md`:

```yaml
    - name: PostgreSQL
      items:
        - name: Connect and describe
          href: databases/postgresql/index.md
        - name: Which columns can be null
          href: databases/postgresql/nullability.md
        - name: Types
          href: databases/postgresql/types.md
        - name: NodaTime, NetTopologySuite and JSON
          href: databases/postgresql/libraries.md
        - name: Running queries
          href: databases/postgresql/query-methods.md
```

and the same for `SQL Server` with `databases/sqlserver/`.

### 11. Tests

| Test | Change | What fails it |
|----|----|----|
| `SiteStructureTests` | `postgresql.md` and `sqlserver.md` leave `PagesNotYetMoved` | A page at the root that is not in the list |
| `SiteStructureTests` | New: every folder directly under `site/reference/databases/` holds exactly `index.md`, `nullability.md`, `types.md`, `libraries.md` and `query-methods.md`, `AGENTS.md` aside, and at least two such folders are found | A database folder with a file missing or added |
| `SiteStructureTests` | New: a tab group in which one tab id (`#tab/<id>`) is the name of a database folder has a tab for every database folder.  Phase 3 left this check and the one above to this phase | A sample given for one described database alone |
| `SupportedVersionsTests` | New, in `tests/SqlSource.Repository.Tests/Documentation/` | Below |

`SupportedVersionsTests` reads the first table after `<a id="supported-versions"></a>` in `site/reference/databases/index.md`.  It finds a row by its first cell, `PostgreSQL` or `SQL Server`, and a cell by the text of its header.  It takes the first number of a "supported" cell and every number of a "tested" cell.

| Cell | Is compared with | Value today |
|----|----|----|
| PostgreSQL, "Server versions supported" | `PostgresSession.MinimumMajorVersion`, `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs` line 40, which the message of SQLSRC235 is built from | 14 |
| SQL Server, "Server versions supported" | The year in `SqlServerErrors.MinimumRelease`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerErrors.cs` line 26 | 2017 |
| PostgreSQL, "Server versions tested" | The majors of `DefaultImages.PostgresFloor`, of `DefaultImages.Postgres`, and of each `image: postgres:` line of `.github/workflows/nightly.yml` | 14, 15, 16, 17, 18 |
| SQL Server, "Server versions tested" | The years in the keys of `SqlServerContainer.ImageBuilds`, `tests/DatabaseTests/TestHarness/SqlServerContainer.cs` lines 34-41, one of which is `DefaultImages.SqlServer` | 2017, 2019, 2022, 2025 |
| PostgreSQL, "Driver versions supported" | The version that ends `SqlDiagnostics.NpgsqlTooOld.Title`, "Npgsql is older than 8.0" | 8 |
| SQL Server, "Driver versions supported" | The version that ends `SqlDiagnostics.SqlClientTooOld.Title`, "Microsoft.Data.SqlClient is older than 6.1" | 6.1 |

- The least tested version of each database equals its supported minimum.
- **It pulls no container.**  `tests/SqlSource.Repository.Tests` references the generator and the tool, whose internals it sees, and not `tests/DatabaseTests/TestHarness`, which brings Testcontainers.  So the test reads `DefaultImages.cs`, `SqlServerContainer.cs` and `nightly.yml` as text, as `tools/pull-test-images.sh` reads `DefaultImages.cs`.  It adds no project reference, and `TestLayoutTests` forbids compiling another project's file.
- The cells "Driver versions tested" are not tested.  The accuracy reviewer checks them.
- Give the test one method that returns the two server minimums of the table.  Phases 10 and 11 use it to hold "PostgreSQL *n* or later" and "SQL Server *yyyy* or later" on `index.md`, `start/install.md` and `README.md`.
- A comment on the test says what fails it, as the other tests of the folder do.

`UserDocsLinksTests`, `SiteTocTests` and `InboundSiteReferenceTests` need no change.  They fail until steps 7 to 10 are complete.

### 12. `AGENTS.md` files

- **`site/reference/databases/AGENTS.md`**, which phase 3 made.  Four changes, and its other words stay:
  - The last bullet, on `../../postgresql.md` and `../../sqlserver.md`, goes.
  - The first bullet gains, after the five names: "`SiteStructureTests` fails on a folder that differs."
  - New: `` - **`index.md#supported-versions` is the one home of the supported versions.**  A new statement of a supported version is a cell of its table, in the form `SupportedVersionsTests` reads, and every other page links the table.  Azure SQL is "accepted, and not tested", never "supported". ``
  - New: `` - **A tab for one database has the folder's name as its tab id**, `#tab/postgresql` or `#tab/sqlserver`.  `SiteStructureTests` fails on a tab group that has one and not the other. ``
- **`site/AGENTS.md`**: the row "Database that can be described" of the table takes the words of the outline's ["Where a new thing goes"](2026-11-12-docs-overhaul-epic-design.md#where-a-new-thing-goes), with `SiteStructureTests` in its last cell.  The bullet "Pages not yet moved" loses `postgresql.md` and `sqlserver.md`.  Run `grep -n -E "postgresql|sqlserver" site/AGENTS.md site/*/AGENTS.md` and rewrite any other hit to the folders.
- **`tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`**: its first bullet names `SupportedVersionsTests` and what it holds.
- **`tests/README.md`** line 13: the list of what `tests/SqlSource.Repository.Tests` checks gains "the supported versions of `site/reference/databases/index.md`".
- Check each file against `AgentsFileTests`: 8,192 bytes and 80 words a rule.

### 13. Remove the two pages

`git rm site/postgresql.md site/sqlserver.md`, once the checker finds every moved unit.

## The ledger

`docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-04.tsv` holds a row for each unit of the two pages.  Beyond the `move` rows:

1. `merge-pair MP-23`: pg 25, to `databases/index.md#supported-versions`.
2. `move-split`, then `merge-pair MP-23`: ms 28, sentences 1 and 2 to `#supported-versions` and sentence 3 to `#azure-sql`.
3. `merge-pair MP-09`: pg 528.
4. `move-split`: the headings "Known issues", pg 522 and ms 520, which go with their anchors.
5. `move`, with a note: the 20 headings that change level.  The checker compares a heading without its `#` marks, so none of them is left over.
6. `corrected`: C-05.
7. `new`: 8 h1 headings; 7 `## Limits` headings; 14 limit headings; the 5 h3 headings of step 3; `## Supported versions` and its table, with the sources of step 5 in the note; `## Azure SQL`; the 2 bullets of step 6; 15 entries of `troubleshooting.md`; the changed lines of the `AGENTS.md` files.
8. A note on pg 473, which stays whole on `P/query-methods.md#method-results`: its clause "tested on Npgsql 8 and 10" is restated by a cell of the table.
9. `restated-from`, with no new location: pg 527 (`P/index.md#what-describing-does`); pg 529 and ms 553 (`reference/query-methods.md#limit-wrapped-connection`, which phase 6 makes from `generator.md` 1186); ms 551 (`M/query-methods.md#limit-rowcount-after-nocount`, which phase 6 makes from `generator.md` 1189).
10. `move`, with the note `position` and the old and the new words: each unit whose words step 7 mended.

Open or extend `merges/MP-09.md` and `merges/MP-23.md` in the epic folder with each text this phase places and its new address.  For MP-23 that is pg 25, ms 28 and the clause of pg 473.  The merged text is phase 19's.

## Done when

- [ ] `test ! -e site/postgresql.md && test ! -e site/sqlserver.md`.
- [ ] `ls site/reference/databases/postgresql site/reference/databases/sqlserver` prints the same five names twice.
- [ ] "check move 04" finds every unit that the ledger marks moved.  What it leaves over is rows 2, 4, 9 and 10 of "The ledger".  Nothing else is left over.
- [ ] `grep -n -E "This page|above|below|later bullet" site/reference/databases/*/*.md` has been read hit by hit, and each hit is true of the page it stands on.
- [ ] "check coverage" passes: no unit of the two pages is without a disposition.
- [ ] `git grep -n -E "\]\((\.\./)*(postgresql|sqlserver)\.md" -- site` prints nothing.
- [ ] `git grep -n -E "site/(postgresql|sqlserver)\.md|SqlSource/(postgresql|sqlserver)\.html" -- . ':!docs/superpowers'` prints nothing.
- [ ] `git grep -n 'id="known-issues"' -- site/reference` prints nothing, and `git grep -n -o -E "\]\([^)]*#known-issues\)" -- site/reference/databases` prints four links, each to `../../../generator.md#known-issues`.
- [ ] `git grep -c '<a id="limit-' -- site/reference/databases` prints 2, 1 and 1 for PostgreSQL's `index.md`, `nullability.md` and `query-methods.md`, and 4, 2, 1 and 3 for SQL Server's `index.md`, `nullability.md`, `types.md` and `query-methods.md`: 14.
- [ ] `grep -c -E 'id="(reading-a-row|null-values|errors-and-rollbacks|rowcount|timeout-and-cancel)"' site/reference/databases/sqlserver/query-methods.md` prints 5.
- [ ] `grep -c -E 'id="(supported-versions|database-support|azure-sql)"' site/reference/databases/index.md` prints 3.
- [ ] `wc -c src/SqlSource.Tool/AGENTS.md` prints 8147.
- [ ] `SupportedVersionsTests` passes, and fails when a number of the table is changed by hand.  `SiteStructureTests` fails when a page of one database folder is renamed.
- [ ] `tools/build-site.sh` exits 0, with no docfx warning.
- [ ] The content reviews, `branch-review:review` and `./pre-commit-validation.sh --full` are clean on the last commit.
- [ ] After a change under `tests/DatabaseTests/SqlServer`, that project also passes on its floor image, as root `AGENTS.md` asks: `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.
- [ ] The pull request tells the owner to publish the site by hand after the merge.

## For the reviewers

The no-loss reviewer, beyond the general kit:

- **The limits with a sample**: ms 522-528, 536-547 and 555-565.  Each bullet and its indented block are one limit, whole.
- **ms 28**, cut after its second sentence.  Read the three sentences on `databases/index.md` against the baseline.
- **`M/query-methods.md#method-results`**: 15 bullets (474 to 488) in today's order, with five headings between them and none inside a bullet.
- **The four pointers that go.**  Confirm that each owner states the fact: pg 31-35 for the `IMMUTABLE` function; `generator.md` 1186 and 1189, still at the root, for the other three.
- **The 20 headings that change level**, each with its anchor directly above.
- **Each unit with the note `position`**: the fewest words changed, the phrase is now true, and no fact changed with it.
- **pg 490**: the bold words "A synchronous method needs a data source without multiplexing" are quoted by `tests/DatabaseTests/Postgres.SyncAsync/KnownDifferences.cs`.  They must be byte-identical.

The accuracy reviewer checks the new text against these sources:

- **The table's server cells** against root `AGENTS.md` rule 5, `DefaultImages.cs`, `SqlServerContainer.ImageBuilds`, `.github/workflows/build.yml` lines 209 and 246, and `nightly.yml` lines 106-110 and 155-157.
- **"Driver versions tested"**: `8, 10` against `tests/DatabaseTests/Postgres.Npgsql8/Postgres.Npgsql8.csproj` (8.0.9) and `Directory.Packages.props` (Npgsql 10.0.3); `6.1, 7` against `tests/DatabaseTests/SqlServer.SqlClient61/SqlServer.SqlClient61.csproj` (6.1.7) and `Directory.Packages.props` (Microsoft.Data.SqlClient 7.1.1).  The second is stated on no page today.
- **"8 or later" and "6.1 or later"** against `NpgsqlProbe.cs` and `SqlClientProbe.cs` in `src/SqlSource/Generation/Types/Drivers/`: a newer major than the map knows is accepted, with SQLSRC248 or SQLSRC268.
- **Azure SQL** against `SqlServerSession.Builders.cs` line 108: the two Azure editions skip the version check.
- **Each limit's heading and each entry of `troubleshooting.md`** against its bullet: the symptom is the one the bullet gives, and the entry promises no fix the bullet lacks.
- **The two bullets of step 6**, and the link of C-05 against `site/settings.md`, where the bullet on a `?` inside a type argument is (baseline line 1070).

## Traps

- **Anchor ids repeat.**  `models`, `query-methods`, `method-parameters` and `collection-methods` are on both pages and on `generator.md`; `nullability`, `type-table`, `json` and others are on both pages.  A scripted rewrite that keys on the anchor alone sends a link to the wrong database.  Key on the page and the anchor.
- **A README URL is three folders deep.**  If `UserDocsLinksTests` accepts one folder only in a site URL, extend the reader and its reading test here.  Do not flatten the folders.
- **The link of ms 485 looks like the others and is not.**  It names `generator.md`, and so do pg 493, ms 312 and ms 488.  They stay until phase 6.
- **`tool.md` 567 and `recipes.md` 177 link `#connecting` of these pages.**  Retarget them to each `index.md#connecting`.  Phase 5 gives the first its last target.
- **A heading among bullets takes every bullet below it.**  That is why step 3 has five headings and not four.
- **The string of `KnownDifferences.cs` grows.**  Run `./format.sh` after the change: CSharpier wraps it.
- **No bare mention is caught by a test.**  `InboundSiteReferenceTests` reads `site/<path>.md`, so "`postgresql.md`" alone in `src/SqlSource.Tool/AGENTS.md` passes when it is wrong.
- **Do not weaken `SupportedVersionsTests` to make it pass.**  If a pinned image and the table disagree, the table or the image is wrong.

## Left to the agent

- The words of the 14 limit headings and of the 15 entries of `troubleshooting.md`.
- The names of the test methods, and how each test reads its files.
- The heading of `troubleshooting.md` for an entry, where phase 3 put a like symptom elsewhere: follow phase 3, and keep the six headings.
- Whether the links are rewritten by a script or by hand.  The checker and the docfx build judge the result.

## Calls made in this brief

- **`databases/index.md`, `databases/other.md` and `databases/AGENTS.md` exist before this phase.**  The map's section 10 and the brief of phase 3 have phase 3 make them, so this phase adds to the first and the third and leaves the second.
- **pg 25 and ms 28 leave `#connecting` whole**, as the map's section 10 says ("1-72 but line 25"), and each section gets one new bullet that links the table.  The map's row says the section "keeps" the error for an older server: the new bullet restates it.
- **pg 473 is not cut.**  The map's section 5 says its sentence is placed beside the table word for word, and its rows and section 10 keep the bullet on `P/query-methods.md`.  The bullet stays whole, and the table's cell restates the clause.
- **The cell "6.1, 7" is a new statement.**  The map asks for a column of tested driver versions, and no page states SQL Server's today.  Its source is `SqlServer.SqlClient61.csproj` and `Directory.Packages.props`, as step 5 says.
- **The table says "or later"**, the words of the three restatements that phases 10 and 11 hold to it.
- **An h1 of a database page names its database**, where every other new page of the epic takes its sidebar title.  The site's search has one entry for each page, and ten pages would share five titles.
- **`SupportedVersionsTests` reads `nightly.yml`** for PostgreSQL's majors between the floor and the default.  The outline names the floor and default images alone, and they would not hold "15, 16, 17".
- **Words that the split makes wrong are mended**, in the fewest words, each with the note `position`, as the outline rules.  "Decided above", of a section that is now one page away, names the section.  Phases 19 and 20 still rewrite the sentences.
- **`MethodShapeTests.cs` 538** is not in the map's section 6.2.  It is a plain mention of a known issue that this phase makes a limit.
- **`generator.md` 702 and `recipes.md` 410 are retargeted here**, by the map's rule that the phase which makes a limit retargets the links to it.
- **A tab id is the database's folder name**, so that the tab-group check needs no list of its own.  The brief of phase 10 uses the same ids.
