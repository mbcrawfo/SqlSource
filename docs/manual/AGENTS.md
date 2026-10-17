# AGENTS.md - docs/manual

How to write and maintain the user documentation: the pages of this folder and the two packed readmes, `README.md` and `src/SqlSource.Tool/README.md`.  The documentation describes what works today and marks what is coming, so every change a user can see, and every phase of query generation that lands, updates it in the same PR.

## Who reads what

- **`README.md`** is for the average user, who should never need to read further: what SqlSource does and why, a status line, the databases it works with, installation and the supported environments, getting started in five steps, "Only want to generate models?", "Just want to keep your SQL out of code files?", a FAQ of five to ten items, "NodaTime and other libraries", "Custom type mapping" and "Where to next".  It shows the common path and links to the manual for every option.  It never repeats the manual's reference tables of markers, settings, dialects, generator parameters or diagnostics.  A FAQ answer is a few sentences that link to a recipe where one exists.
- **`src/SqlSource.Tool/README.md`** is the tool's page on nuget.org: what the tool is for, how to install it, the three `describe` commands, and links to `tool.md` and the README.  Keep it that short; the tool's behaviour goes in `tool.md`.
- **This folder** is for the user with a specific need.  Each topic has one home:

| Page | Holds |
|----|----|
| `README.md` | The entry page: "Find what you need" (a need to a page), the **"What works today"** table, and the list of every page |
| `concepts.md` | How the pieces fit together, and the glossary every page uses |
| `sql-files.md` | The `.sql` file format, opening with the marker index |
| `settings.md` | Every setting, once: the tables with their Status column, the precedence rule, one section per setting |
| `parameters-and-tokens.md` | Query parameters, `-- param:`, `DECLARE`, tokens and their validation |
| `dialects.md` | Reading rules of every dialect, and the database support table |
| `generator.md` | What the `SqlSource` package generates, and where |
| `tool.md` | The `sqlsource` tool: the workflow, then the command reference |
| `postgresql.md`, `sqlserver.md` | One page per database that can be described: describing it, and later its types, models and query methods.  `sqlserver.md` is a stub until SQL Server queries can be described |
| `recipes.md` | Short how-tos: a sample and a link to the rules |
| `diagnostics.md` | Every `SQLSRC` error, what it means and how to fix it |

## Write effects, never internals

A user needs to know what they write, what they get and what they see: the effect on their parameters, their generated models and their build.  They do not need to know how SqlSource gets there.  Never name the describer, a plan or a plan walk, `EXPLAIN`, a heuristic, a provenance value, a capture file, a type of the library, or a field of the sidecar (the one link to `schemas/sidecar-v1.schema.json` in `tool.md` is the way in for the curious).  Never link into `docs/tech-debt`, `docs/deferred`, `docs/superpowers` or `src/`, and never cite an item's id.

Examples of what was rewritten, from the old tool readme into `postgresql.md`:

- **Before:** "`true`, `outer-join`: an outer join of the plan fills the column's table, view or foreign table with `NULL`."  **After:** "An outer join can fill the column with null: a column of the optional side of a `LEFT`, `RIGHT` or `FULL` join."  Say what makes a column possibly null, not which value the sidecar records.
- **Before:** "It prepares each, describes the prepared statement without executing it, and asks for its plan with `EXPLAIN` and without `ANALYZE`, in a transaction that it rolls back ... the settings it changes for a plan are `SET LOCAL`."  **After:** "The tool never runs your queries.  For some queries it also asks PostgreSQL how it would run the statement, without running it ... in a transaction that the tool rolls back."  Say what the user's database experiences.
- **Before:** "... so such a function with an effect outside the transaction, a sequence it advances for one, has that effect (TD-0047)."  **After:** a known issue, "A function marked `IMMUTABLE` can run while a query is described", with the remedy: "mark it `VOLATILE`, which is what it is, or describe against a copy of the schema".  An effect and what to do, no id.

The same holds for what is coming.  A type map is a table of the database type and the C# type a user gets, with the driver version where that changes it; not how the driver is detected or what is set on a `DbParameter`.  A generated method is its signature and what it returns and throws; not how it reads a row.

Before a commit, run this over the pages you changed.  Rewrite every hit, unless it is text the user sees anyway: a diagnostic's own title, such as `SQLSRC216`'s, or a SQL command a user writes, such as `EXPLAIN` in `SQLSRC233`'s list of statements.

```console
$ grep -n -i -E "describer|EXPLAIN|heuristic|nullableSource|provenance|capture|tech-debt|superpowers|TD-0|D-0" <pages>
```

## Conventions

- **Example first.**  A reference section opens with an example of what to write, then gives the rules.
- **Terms**, defined in `concepts.md#glossary` and used everywhere: *query parameter* (`@id`), *generator parameter* (`keep-comments`), *token* (`{{table}}`), *dialect* (how a file is read), *logical database* (what the tool connects to), *file header* (the part of a `.sql` file before its first `-- name:` line), *inside a query*, *folder* (never "directory" in prose), *sidecar*, *describe*, *output*, *token method*.  Do not say "parameter" or "database" alone where it is ambiguous.
- **One status, in one place.**  The "What works today" table (`README.md#what-works-today` here) is the record of what is delivered: a feature is "Available" or "Coming".  The Status column of `settings.md` carries the same status for each setting: "Works today", or "Accepted; no effect yet", linked to the table, for a setting that is read and checked and changes no generated code.  Where only part works, either one says which part, as "Works today for the tool; no effect yet on generated code" does for Output.  No other page keeps a status of its own.
- **A placeholder is one standard callout**, never a description of the feature as if it worked:

  ```markdown
  > **Coming in a later release.**  <One sentence on what it will do.>  See [what works today](README.md#what-works-today).
  ```

  In the packed readmes the link is `https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today`.  A setting that is accepted and has no effect says so where it is mentioned, as the README's FAQ does for `async-method-suffix` and `SqlSourceModelNamespace`.
- **Settings are documented in `settings.md` only.**  Its two tables under `#all-settings`, the settings that work today and those for models and methods, have a row for each setting with every place it can be written, and each setting has a section anchored `setting-<name>`.  A setting moves to the first table when it starts to work.  Other pages link to that anchor and never repeat a setting's values or precedence.  A new setting or marker gets its row, its section, and, for a marker, its row in the marker index of `sql-files.md`.  A new generator parameter goes in the Values cell of the Generator parameters row, in the table of parameters under `#setting-generator-parameters`, and in the section of `SQLSRC109` in `diagnostics.md`, which a test checks.
- **Recipes hold examples, not rules.**  Each is a sample and a link to the section that gives the rules, so it cannot drift from them.
- **Anchors.**  On a page of this folder, a heading that is linked from anywhere gets an explicit anchor on the line above it, `<a id="configure-a-folder"></a>`, and a reworded heading keeps its anchor.  Each page's "Known issues" heading has `<a id="known-issues"></a>`.  The two packed readmes carry no raw HTML, since nuget.org shows a package readme with HTML turned off: their headings are linked by GitHub's slug, so renaming one means changing every link to it, which the link test finds.
- **Links.**  Every link in the two packed readmes is absolute, `https://github.com/mbcrawfo/SqlSource/blob/main/<path>[#anchor]`.  Pages of this folder link to each other relatively.  Write inline links and `#` headings only; the tests read no other form.
- **SQL samples** are fenced with `sql` (read as `ansi`), `sql <dialect>` (`sql postgres`, `sql mysql,ansi-quotes`), `sql fragment` (part of a file, not parsed) or `sql error` (shows a mistake, not parsed).  Mark a sample so; never weaken the test.  Put no fence inside a blockquote.
- **`--prerelease`.**  Install and update commands carry `--prerelease` while only previews are published, and the NuGet badge is `nuget/vpre`.  The first stable release takes them out, by the release step in `docs/publishing.md`.
- **Known issues.**  Each page that documents behaviour ends with a "Known issues" section: the problems a user can plausibly hit, from the open items of `docs/tech-debt` and `docs/deferred`, each by its effect in the user's words and what to do about it.  An item a user cannot see (a test gap, duplicated code, a cost with no visible effect) is left out.  Add one in the PR that opens such an item and remove it in the PR that closes it.  A known issue names no id, so a search for the id does not find it: search the "Known issues" sections for its effect.  A page that gains its first known issue gets the section, with its anchor, and a link from the "Find a problem that is not an error" row of the entry page.
- **`diagnostics.md`.**  A new diagnostic gets a row in the index, a `## SQLSRCnnn` section whose first bold line is its descriptor's title; and, where a manual section documents the rule, a closing `See: [section](page.md#anchor)` line.  The index is a table per area (project and attribute, `.sql` file, tool, PostgreSQL), and the rows of all the tables together must be in id order, since a test compares them with the descriptors: a new id above the last one goes in the last table, or in a new table after it (a SQL Server table for phase 6), never back in an earlier area's table.  The paragraph under the index that gives each area's range of ids is kept true.  The help link every error prints is the lower-case id's slug, so the heading is never given another anchor.
- **A new page** is listed under "The pages" of the entry page and gets a "Find what you need" row for each need it answers.  A test fails until the entry page links it, but a link anywhere on the page satisfies the test, so the list is yours to keep.

## The tests

In `tests/SqlSource.Tests/`, run by `./pre-commit-validation.sh`.  `Documentation/UserDocs.cs` decides which files are user documents: the two readmes and every page here but this file.

- **`Documentation/UserDocsLinksTests.cs`** follows every link: a relative link or a `https://github.com/mbcrawfo/SqlSource/blob/main/...` URL must reach a file and an anchor that exist.  It also fails on a relative link in a packed readme, a link into `docs/tech-debt`, `docs/deferred`, `docs/superpowers` or `src/`, a link form or heading form it does not read, and a page of this folder that the entry page does not list.
- **`Documentation/UserDocsSamplesTests.cs`** parses every `sql` block by the dialect its info string names, and fails on an error, so a sample a user copies is SQL that SqlSource reads.
- **`Diagnostics/DiagnosticsDocumentTests.cs`** fails when the sections of `diagnostics.md` and the descriptors disagree: the order of the sections, each section's first bold line, and the index's rows.  It also fails when the section of `SQLSRC109`, an unknown generator parameter, does not list every generator parameter.

No test checks what a page says.  Whether a sentence is true today, and free of internals, is yours to keep.

## When a phase lands

Every phase of query generation (`docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`), and every later feature, does all of this in its PR:

1. **Flips its rows of the "What works today" table**, and its Status cells in `settings.md`, to what is now true.  A row that is only partly delivered says which part.
2. **Replaces its callouts with documentation**, example first.  `git grep -n "Coming in a later release" -- README.md src/SqlSource.Tool/README.md docs/manual` lists them all.  A callout for a part still to come stays, and one that covers two phases, such as the README's step 4, is narrowed to the part still to come.
3. **Rewrites the sentences that say it is missing or still to come**, such as "Nothing in a build reads them today" or "It takes effect when generated models arrive".  This finds most of them, among many lines to read past; it is not exhaustive, so read each page the table below names:

   ```console
   $ git grep -n -i -E "no effect yet|today|coming|arrive|later release|until|growing|(^|[^[:alnum:]_])(yet|will)([^[:alnum:]_]|$)" -- README.md src/SqlSource.Tool/README.md docs/manual
   ```

4. **Adds or removes its known issues** as its `docs/tech-debt` and `docs/deferred` items open and close.
5. **Adds its diagnostics** to `diagnostics.md`, with their See: lines, and its recipes to `recipes.md`, where a task is new.
6. **Updates the README** where it changes the common path: the status line in the introduction, getting started, a scenario, a FAQ answer, the supported environments.

Which pages each phase touches, besides those six steps:

| Phase | Pages |
|----|----|
| 5. Models (PostgreSQL) | README: the introduction's paragraph on what SqlSource generates and why, the status line, "steps 1 to 4 ... give you a constant" in the databases paragraph, step 4's callout, step 5's "Nothing in a build reads the sidecars yet", "when generated models arrive" under "Just want to keep your SQL out of code files?", "Only want to generate models?", the FAQ on the models' namespace, and "Custom type mapping" for the project-level type override.  `generator.md` (Models).  `postgresql.md` (the nullability callout, Types, Models).  `settings.md`: Output's `models`, the model suffixes, namespace, model type and name, `sort-input`, `sort-output`, `no-table-models`, and a row for the override's property.  `concepts.md`: its opening paragraph, its callouts about sidecars, and the glossary's Output entry.  The tool readme's callout (`src/SqlSource.Tool/README.md`).  `tool.md`: its opening, its callout, and "Commit the sidecars with the project" under sidecar files.  The marker index's callout in `sql-files.md` for the model markers |
| 6. SQL Server | `sqlserver.md` becomes a page like `postgresql.md`.  `dialects.md`: the database support table and its callout.  Every sentence that says PostgreSQL alone is described: the README's status line, supported environments, the opening of step 5 and "Use a database other than PostgreSQL" row; the same row of the entry page's "Find what you need"; the tool readme's first paragraph; `tool.md`'s "Which queries are described"; `recipes.md`'s "Read one folder with another dialect"; and in `diagnostics.md`, `SQLSRC209`, `SQLSRC216` and the paragraph under the index on the ids of describing.  The phase 6 finder below the table finds most of them |
| 7. Library maps | README: "NodaTime and other libraries", with the call each library needs on the data source.  The Types section of each database page.  `settings.md`: a row for each library's property |
| 8. Query methods | README: the introduction, step 4, and the FAQ on `Async` names.  `generator.md` (Query methods).  `settings.md`: Output's `codegen`, the collection type, `MethodLocation`, `async-method-suffix`.  `sql-files.md` (result shapes), `parameters-and-tokens.md` (query parameters), the Query methods section of each database page, and the "query methods of a later release" in `diagnostics.md`'s `SQLSRC231`, `SQLSRC232` and `SQLSRC234` |
| 9. Build integration | `tool.md`: running in the build, the `.env` file and what to keep out of git, watch mode, and the new options in the command reference.  `settings.md`: the property that turns it on.  README: step 5 |
| 10. Bug reports | `tool.md`: making a bundle, what it holds before you share it, and the new commands in the command reference |

The phase 6 finder, kept out of the table because its pipes would split the row:

```console
$ git grep -n -i -E "SQLSRC216|cannot describe|can be described|PostgreSQL" -- README.md src/SqlSource.Tool/README.md docs/manual
```

A database that becomes describable gets its own page, a row in the support table of `dialects.md`, and the README's supported environments.  A dialect that is added gets its row in `dialects.md` and in `settings.md`'s Dialect row.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
