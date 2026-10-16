# User documentation overhaul - design

Date: 2026-10-16

## Intent

SqlSource's user documentation is two long reference documents: the repository `README.md`, which is close to 500 lines and documents every marker, setting and dialect rule, and `src/SqlSource.Tool/README.md`, which mixes what a user of the tool needs with how the PostgreSQL describer works inside (plan walks, `EXPLAIN`, heuristics, links to tech-debt items).  A new user has to read a reference manual to find out what the project is for and how to start.

The overhaul splits the documentation by reader:

- **The repository `README.md`** is for the average user, who should never need to look further: what SqlSource does and why they would want it, how to install it, how to get started, the other ways to use it, and a short FAQ.
- **A user manual in `docs/manual/`** is for the user with a specific need beyond that: configuring one part of a project differently from the project defaults, complex queries, a database's limits, advanced types, a build error to look up.

**The documentation is user facing.**  It describes what a user writes, what they get and what they see, never how the library does it.  A user does not need to know that a PostgreSQL describer exists or how it walks a plan; they need to know the effect on their parameters and generated models: "a column that comes from a view is always nullable", "the tool never runs your query".

## Goals

1. A rewritten root `README.md`, structured as below.
2. A manual in `docs/manual/`, arranged by topic rather than by package: an entry page that routes a reader by need, pages for what SqlSource as a whole reads (the `.sql` file format, settings, parameters and tokens, dialects), a page each for the generator and the tool, a page per database that SqlSource can describe, recipes, and the diagnostics list.
3. The diagnostics list moved into the manual, with the help links that every error prints moved with it.
4. A short tool readme for the `SqlSource.Tool` package page.
5. AGENTS.md guidance that keeps the README and the manual current, in their roles, as query generation is completed and the project grows.

## Non-goals

- Documenting features that do not exist yet as if they did.  **The documentation describes current functionality, with placeholders for what is coming** (decided by the project owner).  Models and generated query methods do not exist today; their sections are placeholders.
- The detailed PostgreSQL model and query-generation content: the type map, model shapes and generated-method behaviour.  These are written by the phases that deliver them (epic phases 5 to 9).
- `CONTRIBUTING.md` and `docs/publishing.md`, which keep their roles.
- Any change to behaviour, apart from the diagnostics help-link URL.

## Anti-goals

These would make the overhaul a failure even if every page exists:

- Implementation detail in user documentation: type names of the library, the describer, plan walks, `EXPLAIN`, heuristics, provenance fields, capture files, links into `docs/tech-debt`, `docs/superpowers` or source files.  A known issue that a tech-debt item records is described by its effect on the user, in their words, with no link to the item.
- A README that has to be read past to get started, or that duplicates the manual's reference tables.  The README shows the common path and links to the manual for every option.
- A user reading that something works when it does not.  Every placeholder says plainly that the feature is coming in a later release.
- Relative links in a document that is packed into a NuGet package.

## Constraints

- **The root `README.md` is packed as the readme of the `SqlSource` package** (`src/SqlSource/SqlSource.csproj`), and `src/SqlSource.Tool/README.md` as that of `SqlSource.Tool`.  Every link in both is an absolute URL: `https://github.com/mbcrawfo/SqlSource/blob/main/...`.  Pages under `docs/manual/` are read on GitHub only and link to each other relatively.
- **The help link of every diagnostic** is `SqlDiagnostics.HelpLinkBase` (`src/SqlSource/Diagnostics/SqlDiagnostics.cs`) plus the lower-case id, and the tool prints it on its `see:` line.  Moving the list changes that base to `https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/diagnostics.md#`.  Code, tests and docs that name the old path move with it: `SqlDiagnostics.cs`, `ToolDiagnostics.cs`, `src/SqlSource.Tool/Reporting/ToolDiagnostic.cs`, `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs`, `SqlDiagnosticsTests.cs`, `ToolDiagnosticsTests.cs`, `tests/SqlSource.Tests/SqlSource.Tests.csproj` (the linked copy), `tests/SqlSource.Tool.Tests/Expected.cs`, the AGENTS.md files that name it, and the live docs under `docs/deferred/` and `docs/tech-debt/` that link to it.  Specs and plans under `docs/superpowers/` are history and keep the old path, except the query generation epic outline, which is kept current.  `git grep diagnostics.md` finds them all.  No version has been published, so the old URL breaks for nobody.
- `./pre-commit-validation.sh` passes, and the repository's other rules apply: the pull request stays under 150 files, and the version check in `AGENTS.md` runs before it is opened.
- `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` is kept current until the epic closes, so its Documentation section and the scope items of phases 7 and 9 that name README sections are updated to point at the new homes.

## The root README

In this order:

1. **Title and badges.**  A build-status badge for the `CI` workflow on `main` (`.github/workflows/ci.yml`), and a NuGet badge for the `SqlSource` package linking to `https://www.nuget.org/packages/SqlSource/`.
2. **Introduction, one or two paragraphs.**  What SqlSource does: keeps SQL out of C# files, in `.sql` files; generates the code that uses them; and generates query methods and models from them.  Why that helps: SQL in its own files is cleaner than SQL in strings; SQL linters and formatters work on it; SQL is easier to search; generated models turn a type or name mismatch into a build error instead of a runtime one; generated queries run as fast as raw ADO.NET.  A short status line says which of these work today and which are coming (builder's wording).  Then, up front: query generation is the main scenario, and there are two others, generating only models and only keeping SQL out of code files, each linked to its section.
3. **Installation.**  The `SqlSource` package, and the `SqlSource.Tool` package installed as a local .NET tool.  When each is needed.  The supported environments: target frameworks, C# version, and the .NET SDK and Visual Studio versions, as the code and today's README establish them.
4. **Getting started.**  The main path as far as it works today: write a `.sql` file with the common markers (`-- name:`, `-- summary:`, and a `-- param:` where it helps), mark a partial type with `[SqlSourceGenerate]`, set the project's dialect, use the query, and run the tool to describe the queries and commit the sidecar files it writes.  Where the main path goes beyond today, generated models and query methods, a placeholder says what is coming.  Each step ends with a "More:" link to the manual page for its options.
5. **Other scenarios.**  "Only want to generate models?" (placeholder until models ship) and "Just want to keep your SQL out of code files?", which covers the settings that use SqlSource without models or query methods, and without the tool.
6. **FAQ, five to ten items.**  Short answers that link into the manual, a recipe where there is one.  Candidates, for the builder to choose and order: query methods with `Async` names; changing the model namespace; a project that uses several databases; a different dialect for some files; leaving migration scripts out; using the queries with Dapper or your own ADO.NET code; table names or clauses chosen at run time (tokens); seeing the SQL that was generated; a test project that sees the main project's internals.  An answer about a feature that is not delivered yet says so and names the setting it will use, where that setting exists today.
7. **Placeholder sections for NodaTime and other libraries, and for custom type mapping.**  Visible sections that say these are coming.
8. **Where to next.**  A small table that maps needs beyond the README to manual pages, the same way the manual's entry page does ("configure one part of a project differently", "look up a build error", "set up CI"), and a link to the manual itself.  Links to `CONTRIBUTING.md` and the licence.

What the README drops: the reference tables of markers, settings, dialects and generator parameters, the MSBuild details, the diagnostics list, and the internals-visible-to explanation.  Each moves to the manual.

## The manual

The manual is arranged by topic, not by package.  The `.sql` file format, the settings, parameters, tokens and dialects belong to SqlSource as a whole: the generator and the tool both read them (the tool reads `-- database:`, the output setting and the dialect, and describing a query needs `-- param:` and token defaults).  So they get pages of their own, and the generator and tool pages cover only what is particular to each (suggested by the project owner, and worked out in a review of this design).

`docs/manual/`:

| Page | Contents |
|----|----|
| `README.md` | The entry point.  A "find what you need" table that maps a need to a page ("configure part of a project differently", "look up a build error", "set up CI", "use another database"), a **"What works today" table** of every feature with its status, and the list of pages. |
| `concepts.md` | Short.  How the pieces fit together (`.sql` file, generator, tool, sidecar, build) in a paragraph and a small picture, and a glossary.  The glossary separates the three kinds of parameter (a query parameter such as `@id`, a generator parameter such as `keep-comments`, a token) and the two meanings of database (the dialect a file is read by, and the logical database the tool connects to), and names the **file header**: the part of a `.sql` file before its first `-- name:` line.  Every other page uses these terms. |
| `sql-files.md` | The `.sql` file format: queries and names, result shapes, summaries, the file header, what reaches the generated SQL.  It opens with a **marker index**: every marker with a one-line purpose, where it may stand, and a link to the section that documents it.  A marker that is a setting links to `settings.md`. |
| `settings.md` | **Every setting once.**  A table with a row for each setting: its values, its MSBuild property, its item metadata, its attribute property, its file-header marker, its in-query marker, and its status (works today, or accepted with no effect yet).  The precedence rule written once, with its exceptions stated plainly: a generator-parameter list or a dialect value replaces the one below it whole and never adds to it; the dialect has no attribute or in-query level; `SqlLocation` and `MethodLocation` exist only on the attribute.  A worked example of configuring one folder differently from the project.  Which `.sql` files the package includes and how to change that, and the rules for MSBuild values (white space, `;` and `#`).  Each setting has its own anchor, so the diagnostics of a bad setting can link to it.  This page replaces the separate "settings for models and methods" section of today's README. |
| `parameters-and-tokens.md` | Query parameters and `-- param:`, including `DECLARE` under `mssql` and the `@x` pitfall; tokens, their defaults and validation.  **The warning that a token is not escaped and must only carry trusted text stays prominent.** |
| `dialects.md` | Choosing a dialect, its options, what each dialect changes, what is still read differently, and a **database support table**: for each database, its dialect, whether its queries can be described today, and its manual page if it has one. |
| `generator.md` | What the `SqlSource` package generates: the `[SqlSourceGenerate]` attribute and which files belong to a type, where the generated members go, constants and token methods, and projects that share internals.  Placeholders for models and generated query methods. |
| `tool.md` | The `SqlSource.Tool` package, in two parts.  First the workflow: installing it, describing your queries, giving connections, sidecar files (see below), describing again after a change, `--check` on a build server.  Then a command reference: arguments, options, environment variables, exit codes, and `--verbose` and `--log`, with what a log holds before you share it.  Only what a user does and sees: no describer internals. |
| `postgresql.md` | Supported versions (14 and later, as `CONTRIBUTING.md` says).  What describing needs of the database and what it does to it, in user terms: it never runs a query, and the one effect it can have, through `IMMUTABLE` functions.  What the tool can and cannot describe (`CALL` and data definition statements, from D-0002).  Which columns SqlSource can tell are never null and which it treats as possibly null, in plain terms (for example, a column from a view, or from the outer side of a join), with a placeholder for how that will shape the generated models.  No sidecar fields are named.  Known issues (for example, describing through a connection pooler).  Placeholders for the type map, models and generated query methods. |
| `sqlserver.md` | A stub: the `mssql` dialect is read today, and describing SQL Server queries is coming; until then, a query that must be described is SQLSRC216. |
| `recipes.md` | Short, task-focused how-tos, each a code sample and a link to the reference for the rules: one folder with another dialect or settings; leaving migration scripts out; several databases; `--check` in CI; after a schema change; using the queries with Dapper or your own ADO.NET code; a table name chosen at run time; a test project that sees the main project's internals.  Recipes hold examples, not rules, so they do not drift from the reference. |
| `diagnostics.md` | Moved from `docs/diagnostics.md`.  Its index is grouped by area (project and attribute, `.sql` file, tool, PostgreSQL), and an entry ends with a link to the manual section that documents the rule it enforces, where there is one.  Otherwise unchanged, as long as `DiagnosticsDocumentTests` still passes. |

**How the pages are written:**

- **A reference section opens with an example, then gives the rules.**  The precision of today's README is kept, but a reader sees what to write before reading the rules.
- **One status, in one place.**  The "What works today" table on the entry page is the one list of what is delivered.  A placeholder anywhere else is one standard callout, "Coming in a later release", with a sentence on what it will do and a link to that table, and `settings.md` carries the same status in its column.  When a phase lands, its table cells flip and its callouts are replaced by the documentation.
- **Stable anchors.**  A heading that another page or a diagnostic links to gets an explicit anchor, so rewording the heading breaks no link.
- **Links are checked.**  A test, beside `DiagnosticsDocumentTests`, fails when a relative link or an anchor in `docs/manual/` does not resolve, so the pages stay connected as they change.

**Sidecar files are explained at a high level only** (decided by the project owner): what a sidecar is (the tool's record of what the database said about each query's parameters and columns, kept beside the `.sql` file), why it is committed with the project (the build reads it, and never needs a database), and how to maintain it: when to run `describe` again, `--force` after a schema change, `--check` to catch one that is out of date, and that a sidecar is never edited by hand.  The fields of the file are not documented in the manual; a user who wants them can follow one link to the sidecar's JSON Schema, `schemas/sidecar-v1.schema.json`.  `SQLSRC219`'s section in `diagnostics.md` keeps explaining how to read and fix that error.

Only the databases SqlSource can describe get their own page.  MySQL, MariaDB, SQLite, Oracle, CockroachDB and `ansi` are read but cannot be described; the support table in `dialects.md` says so.

**Reading rules stay in `dialects.md` for every dialect**, `postgres` and `mssql` included: the dialect table, the options, what each dialect changes and what is still read differently stay in one place, since they are about how SqlSource reads a `.sql` file.  The database pages cover describing, and later models and query methods, and link to the dialects section for reading rules; a pitfall a user of that database is likely to hit (PostgreSQL's `@x` absolute value, SQL Server's `DECLARE`) may get one sentence and a link there.

**Every page that documents behaviour has a "Known issues" section** where there is something to say: the topic pages, `generator.md`, `tool.md` and each database page.  It lists the problems a user can plausibly hit, taken from the open items in `docs/tech-debt` and `docs/deferred` (for example a `.sql` file that is not UTF-8, one listed by two projects, a project path through a symbolic link, a multi-targeted project), each in the user's words with what to do about it, and with no link to the item.  An item a user cannot see (a test gap, a duplicated pattern, a performance detail with no visible effect) is left out.

## The tool readme

`src/SqlSource.Tool/README.md` becomes a short page for the NuGet package: what the tool is for in a paragraph, how to install it, the two or three commands a user runs most, and absolute links to `docs/manual/tool.md` and the repository README.  Its current content moves to `tool.md`, `settings.md` (the logical database setting) and `postgresql.md`, rewritten to drop the internals.

## AGENTS.md

- **Root `AGENTS.md`, core rule 4**, is rewritten: the root README is for the average user and the manual for specific needs; documentation is user facing and describes effects, never internals; each page's role in one line; links in the two packed readmes are absolute; a change to user-visible behaviour updates the page that documents it in the same PR, and the README too when it changes the common path or an FAQ answer; a delivered feature replaces its placeholders.  The `docs/diagnostics.md` item names the new path.
- **A new `docs/manual/AGENTS.md`** holds the detailed guidance for writing these pages: the audience of the README and of each page; the no-internals rule with examples of what to write instead; the placeholder convention; that the README links to the manual instead of repeating its tables; the manual's conventions (example first, the one status table and the placeholder callout, stable anchors, recipes hold examples and not rules, settings documented only in `settings.md`); which page each future phase updates (phase 5 models: the README's getting started and scenarios, `generator.md`, `postgresql.md`, the status columns of `settings.md`; phase 6: `sqlserver.md` and the support table of `dialects.md`; phase 7: the README's libraries section and the database pages; phase 8: the README's main path and FAQ, `generator.md`, `settings.md`; phase 9: `tool.md`, `settings.md` and the README's getting started; phase 10: `tool.md`; every phase: the "What works today" table, its recipes and its known issues).  It ends with the maintenance footer, as an AGENTS.md that names files must.
- **`src/SqlSource/AGENTS.md`, `src/SqlSource.Tool/AGENTS.md` and `tests/AGENTS.md`** follow the move of the diagnostics list.  `src/SqlSource.Tool/AGENTS.md`'s readme bullet says the readme is the short package page and that the tool's behaviour is documented in `docs/manual/tool.md`, `settings.md` and the database pages.
- **The query generation epic outline**: see Constraints.

## Calls made in the design

These were not discussed with the project owner and are the designer's choices:

- The introduction states all three purposes and adds a status line, so a reader is not misled about what works today.
- The getting-started section includes running the tool and committing the sidecars, though nothing reads them yet, because the owner asked for it there and it is the path models will take; the step says what the sidecars are for.
- Placeholders are short visible sections, "Coming in a later release" with one sentence on what it will do, never hidden or left out.
- The manual's reference pages keep the precise style of the current README; the root README is friendlier and shorter.
- A `docs/manual/AGENTS.md` holds the detailed writing guidance, so the root rule stays short.
- Reading rules for every dialect stay in `dialects.md`; the database pages cover describing and link there.
- Of the review's suggestions, the tool keeps one page, `tool.md`, with a workflow part and a command-reference part, rather than splitting into two pages, because the owner asked for a document for the tool.  The topic pages, `settings.md` as the one home of every setting, the marker index, `concepts.md`, `recipes.md`, the status table, the grouped diagnostics index, the README's "Where to next", supported environments in the README, example-first reference sections, stable anchors and the link test are adopted.
- The one link from `tool.md` to the sidecar's JSON Schema, as the way into the technical detail the owner said curious users can explore.
- Known issues are surfaced on every page that documents behaviour, not only the database pages, and the `docs/manual/AGENTS.md` guidance tells later work to keep them current as tech-debt and deferred items open and close.

## Left to the builder

- Wording, examples, section order inside each page, and which FAQ items make the cut.
- The exact page names, as long as each topic in the table above has one home and the entry page lists every page.
- Which recipes are written, and what goes in the glossary.
- Checking every statement against the code: the documentation describes what SqlSource does today, and the current README, tool readme and tests are the source to check it against.  In particular, the settings the "Just want to keep your SQL out of code files?" section recommends are checked against how the generator and tool treat `SqlSourceOutput` today.
