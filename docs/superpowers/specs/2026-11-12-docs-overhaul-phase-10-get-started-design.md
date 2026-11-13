# Docs overhaul, phase 10: get-started

The outline is [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md), and the content map is [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).  Both win over this brief.
Done by: an Opus agent.  Needs: phases 0 to 9.  Runs alone.

## Goal

Write the nine pages of `site/start/` and the finished landing page, `site/index.md`, new and in plain language, about 9,000 words.  A junior developer who has one PostgreSQL or SQL Server database and has never used SqlSource reads these pages first, in order, and has a tested application at the end of them.  This brief is the writer's guide for that part of the site.

## Where things stand

After phase 9:

- Every page of `site/guides/` and `site/reference/` exists, with every anchor of the map's section 3.  A link from a `start/` page to any of them resolves.
- `site/index.md` is the first landing page of phase 3: what phase 1 left of `site/README.md` 1-57 and 107-119, word for word, with the anchors `find-what-you-need` and `the-three-parts`, the 49 rows of the old table, and the row and the bullet that phase 2 added for `errors.md`.  It has no front matter and no sample pair.
- `site/start/` holds `toc.yml`, `AGENTS.md` and four pages:

| Page | Holds | Placed by |
|----|----|----|
| `concepts.md` | Today's `site/concepts.md`, unchanged, with its 18 anchors | Phase 8 |
| `keep-up-to-date.md` | `recipes.md` 185-196, under `after-a-migration` | Phase 5 |
| `transactions.md` | `recipes.md` 258-300 and `README.md` 191-193, under `queries-in-a-transaction` | Phase 8 |
| `testing.md` | `README.md` 187-189 | Phase 8 |

- `README.md` still holds its introduction, installation and the five steps.  This phase does not edit it.  Phase 11 cuts it.
- A baseline page is read with `git show 28e20e3c:<path>`.  Every line number in this brief is a line of the baseline.

## In scope / Not in scope

In scope:

- The nine pages of `site/start/`, `site/start/toc.yml`, `site/start/AGENTS.md`.
- `site/index.md`, finished.
- `site/start/concepts.md` removed, and every link to it retargeted.
- `SupportedVersionsTests`: the restatements of `site/index.md` and `site/start/install.md`.
- `merges/MP-13.md` and `ledger/phase-10.tsv` in the epic folder.

Not in scope:

- `README.md` and `src/SqlSource.Tool/README.md` (phase 11).
- Any word of a page of `guides/` or `reference/`.  One link's target changes on `reference/cli.md` (step 7), and no word.  If a `start/` page needs an anchor that is missing there, that is a fault of an earlier phase: add the anchor alone, with no change of words, and say so in the pull request.
- The example projects.  The outline's non-goals keep them as they are.
- `site/diagnostics.md`.

## The work

How a phase is done, the ledger, the reviews and publishing are in the outline's "Delivery".  The shape of a `start/` page, the outline of each page and the table of traps are in the map's section 3, "Get started pages".  The steps below add what is specific to this phase.  As in the map, `S/` is `site/start/`, `G/` is `site/guides/`, `R/` is `site/reference/` and `D/` is `site/reference/databases/`.

### 1. The reader, and the line

The reader is a junior developer, alone, with basic C# and SQL.  The reader has one PostgreSQL or SQL Server database, and does not know the words "sidecar", "describe", "marker" or "output".  The reader does what the page says, in order, and does not read Reference.

The line, from the outline: a topic is in Get started only when an application with one PostgreSQL or SQL Server database and the default `repository` output needs it before its first release.  Apply it to every sentence.  A fact that does not meet the line is a link in the closing "Rules:" line, or is left to its owner.

Every page follows the outline's six rules ("The design", "Get started") and the map's fixed shape of eight parts.  In addition:

- The first sentence of a page says what the reader has at its end, and carries the search words of the map's section 1 for that page.
- A step is: what to do, one whole sample, what you see.  "What you see" is real output, copied from a run.
- A page states an error by its id, as a link to `../diagnostics.md#sqlsrcnnn`, and gives the fix in full on the page.
- No sentence names an internal.  Before the commit, run the grep of `site/AGENTS.md` on the ten pages.

### 2. The running example

The example is the users of the two example projects: the table `users` and the class `UserRepository`.  A reader who opens `examples/PostgresExamples/Repository` or `examples/SqlServerExamples/Repository` finds the same names as on the pages.

| What | Source | Used on |
|----|----|----|
| The table `users`: `id`, `name`, `email`, `created_at`, none of them nullable | `examples/PostgresExamples/schema.sql`, `examples/SqlServerExamples/schema.sql` | `first-query.md`, as one sentence that says which table the steps use |
| The query `GetUser` | `examples/PostgresExamples/Repository/UserRepository.sql` lines 5, 6 and 12-14 | `index.md`, `first-query.md` |
| The queries `ListUserNames` and `RenameUser` | The same file, lines 16, 17, 19-21 and 23, 24, 26-28 | `write-queries.md`, `call-queries.md` |
| The class, PostgreSQL | `examples/PostgresExamples/Repository/UserRepository.cs` lines 1, 2, 13 and 14 | `first-query.md` |
| The class, SQL Server | `examples/SqlServerExamples/Repository/UserRepository.cs` lines 1, 13 and 14 | `first-query.md` |
| Code that takes `IUserRepository` | `examples/PostgresExamples/Repository/UserGreeter.cs` lines 8-14 and 20, with `GetUser` for `GetUserAsync` | `testing.md` |
| `sqlsource.json` | `README.md` 78-85, and `"dialect": "mssql"` from `recipes.md` 418-423 | `first-query.md` |
| The `.env` line, the run of the tool and its summary line | `README.md` 114-116, 120-124 and sentence 6 of line 126 | `first-query.md` |
| The transaction: `AccountRepository` | `recipes.md` 260-296, already on `transactions.md` | `transactions.md` |

Rules for a sample:

- **The marker lines and the SQL of a query are the example file's, byte for byte.**  The three queries are the same text in both example projects, so a query needs no tab.  Its info string is `sql postgres`.
- **Lines that are left out**: the comment lines of the example files that are not markers (lines 1-3, 7-10, 18 and 25 of the `.sql` file; the comment above the attribute in each `.cs` file), the `namespace` line, and line 11, `-- generator: method-type=both`.
- **Without line 11 the method of `GetUser` is asynchronous and is named `GetUser`**: `Task<GetUserDto?> GetUser(GetUserParams parameters, CancellationToken cancellationToken = default)`.  `README.md` 97 and `generator.md` 585-611 show that form.  The example project compiles the other form, so the accuracy reviewer compiles this one (see "For the reviewers").
- **A sample that no example has** is written on the table `users`, in the form that today's text states, and is as small as its rule allows.  These are: the optional filter (`README.md` 44-50, `FindUsers`), the list after `IN`, and the column that is never null.  The ledger marks each as compiled by the reviewer.  Each has a twin to check it against: `ListUserDisplayNames` of `examples/PostgresExamples/Models/Orders.sql` and `ListOrderMonths` of `examples/SqlServerExamples/Models/Orders.sql` for `-- column:`; `ListOrdersByStatuses` of `Methods/OrderQueries.sql` in both examples for the list; `tests/DatabaseTests/Postgres/EndToEnd/Queries/Notes.sql` and its SQL Server twin for the mark at the end of an alias.
- **A page never shows a fragment where the reader must write a whole file.**  When a page adds a query to `UserRepository.sql`, it says "add this to `UserRepository.sql`".
- **Connection strings and paths are those of today's README**: `Queries/UserRepository.sql`, `Host=localhost;Database=app;Username=app;Password=secret`, `Server=localhost,1433;Database=app;User ID=app;Password=secret`.

### 3. The two databases

- A difference is a docfx tab group that holds a sample or at most three sentences.  The form:

  ```markdown
  # [PostgreSQL](#tab/postgresql)

  (the sample or the sentences)

  # [SQL Server](#tab/sqlserver)

  (the sample or the sentences)

  ---
  ```

- PostgreSQL is the first tab of every group, and every group has both tabs.  `SiteStructureTests` fails on a group that names one described database and not the other.
- No heading and no `<a id>` is inside a tab.  A link never names a place inside a tab.
- The choice of tab does not carry from page to page.  So each page works for a reader who lands on it with the PostgreSQL tab showing: the text outside the tabs is true for both databases, and never says "as you chose" or "the tab you picked".
- Text in a hidden tab is hidden from the browser's find.  So an error id of a trap that is inside a tab also stands on `errors.md`, outside any tab.
- A long difference is two anchored sections.  Get started has none in the map.  If the SQL Server part of a step needs more than a sample and three sentences, cut it to its common form and link the owner.

Where the pages differ by database: the driver package and the EF Core trap (`install.md`); the class, the dialect, the `.env` line, the summary line and the calling code (`first-query.md`); the type word of `-- param:` (`write-queries.md`); the repository's connection and its registration (`call-queries.md`); the sample of the transaction (`transactions.md`).

### 4. The language

The rule is the outline's "The language".  Three examples from today's README show what it asks.  In each, what the new text leaves out is owned by a reference section, which the page links.

**One instruction in a sentence** (`README.md` 72):

- Before: "The type gets the `.sql` file named for it, `UserRepository.sql`, in the folder of its C# file, and becomes a repository of its queries: once they are described, in step 5, the build gives it a method for each query, `GetUser` and `FindUsers`, which gets its connection from the data source."
- After: "The class reads the `.sql` file that has its name and is in its folder: `UserRepository.sql`.  The build gives the class one method for each query of that file.  Each method opens a connection from the data source."

**The trap as its own sentences, with the reader's case named** (`README.md` 126):

- Before: "The tool reads the `.env` in the folder of the project or solution it runs on, never one in a folder above it, and only its `SQLSOURCE_CONNECTION` variables, so the file can be your application's own too."
- After: "Put the `.env` file in the folder of your project.  The tool does not read a `.env` file in a folder above that one.  If your `.env` file is at the root of your repository, the tool does not read it."

**The condition first, and a list for the cases** (`README.md` 128):

- Before: "From then on, `dotnet build` and your IDE's build run `describe` themselves before they compile, on a machine where they find the project's `.env` file or `SQLSOURCE_CONNECTION` in the environment, and only when something changed: a query you change is described at the next build, and one your database rejects is a build error at the query."
- After: "If the folder of your project has a `.env` file, `dotnet build` describes your queries before it compiles."  Then a list of two items: "If you change a query, the next build describes it again." and "If your database rejects a query, the build shows an error at that query."

One term for one thing, on all ten pages: "class" for the reader's repository class (not "type"); "model" for a generated record; "query parameter" where "parameter" alone is unclear; "the tool" for the `sqlsource` command; "describe" for what the tool does; "sidecar" for the `.sql.json` file; "folder".  `first-query.md` uses "describe" and "sidecar" before `how-it-works.md` defines them, so it explains each in one clause at first use.

### 5. The nine pages

For each page: the reader's question; the sections, in order, with the anchors the map fixes; the sources, by the rows of the map of each old document; the owner that each rule links; the traps; and what the page must not hold.  The reference owners are also the targets of the page's "Rules:" line.  Every anchor below is declared in the map's section 3.

#### `install.md`: "What do I need, and what do I install?"

The first line under the title is the door for the two other readers, before the opening sentence: "You use Dapper: start with [Use Dapper](../guides/dapper.md).  Your database is not PostgreSQL or SQL Server: start with [another database](../guides/other-databases.md)."  They are the words of the landing page's line.

| Section | Anchor | Holds | Owner |
|----|----|----|----|
| Requirements | `requirements` | The project targets .NET 8 or later and C# 12 or later.  It builds with the .NET SDK 9.0.300 or later, or Visual Studio 2022 17.14 or later.  The database is PostgreSQL 14 or later, or SQL Server 2017 or later (step 8).  The tool needs the .NET SDK | `R/msbuild.md#build-requirements`, `D/index.md#supported-versions`, `R/cli.md#requirements` |
| Add the package | `add-the-package` | `dotnet add package SqlSource --prerelease`, in the folder of each project that has queries.  The sentence "The packages are published as previews, so the commands include `--prerelease`", once on the page.  The driver package in a tab: `dotnet add package Npgsql`, or `dotnet add package Microsoft.Data.SqlClient` | This page owns the command.  The driver: `R/repositories.md#repository-connection` |
| Install the tool | `install-the-tool-step` | Once, at the root of the repository: `dotnet new tool-manifest`, which is left out when the repository has a manifest, then `dotnet tool install SqlSource.Tool --prerelease`.  Commit `dotnet-tools.json`.  A colleague runs `dotnet tool restore`.  When you update the package, run `dotnet tool update SqlSource.Tool --prerelease` | `R/cli.md#install-the-tool` |
| What each package adds to your project | `packages` | `SqlSource` runs in the compiler and adds no runtime dependency.  `SqlSource.Tool` adds the `sqlsource` command | `R/attributes.md#what-the-package-adds`, `R/cli.md#install-the-tool` |

- Sources: `README.md` rows 11-29; `tool.md` 18-29 and 562-567; `sqlserver.md` 522-528; tool readme 5-12.
- Traps: SQLSRC021 for an older SDK or Visual Studio (`requirements`).  SQLSRC267 on SQL Server with EF Core 8 or 9, inside the SQL Server tab, with the `PackageReference` of `sqlserver.md` 524-528 (`add-the-package`).  SQLSRC206 when the tool and the package have different versions (`install-the-tool-step`).
- Must not hold: a driver's minimum version beyond the 6.1 that the SQLSRC267 trap needs; Azure SQL; the sentence of `README.md` 29 about a connection that wraps the driver's (see "Calls"); any setting.
- "Next:" `first-query.md`.

#### `first-query.md`: "How do I get one query running?"

Six steps, as numbered h2 headings, in the order a build needs them.  The page opens with one sentence on the table the steps use.

| Step | Anchor | Holds | Owner |
|----|----|----|----|
| 1. Write a query | `write-a-query` | `Queries/UserRepository.sql` with `GetUser`.  One sentence each for `-- name:` with `-> one-optional`, `-- summary:` and `@userId` | `R/sql-files.md#queries-and-names`, `#result-shapes`, `#summaries`; `R/parameters.md#query-parameters` |
| 2. Mark a partial class | `mark-a-partial-class` | `UserRepository.cs` in the same folder, in a tab: `NpgsqlDataSource dataSource`, or `string connectionString`.  The class reads the `.sql` file that has its name.  The build now reports an error for the query, and steps 3 to 5 remove it | `R/attributes.md#attribute`, `#setting-source`; `R/repositories.md#repository-connection` |
| 3. Name your database | `name-your-database` | `sqlsource.json` beside the project file, in a tab: `"dialect": "postgres"`, or `"dialect": "mssql"`.  It tells SqlSource which database you use.  The file applies to the folder and the folders below it | `R/settings.md#setting-dialect`, `R/settings-file.md#settings-files` |
| 4. Give the tool your connection | `connection` | The `.env` file in the folder of the project, in a tab: `SQLSOURCE_CONNECTION_POSTGRES`, or `SQLSOURCE_CONNECTION_MSSQL`.  Add `.env` to `.gitignore` | `R/connections.md#env-file`, each database's `index.md#connecting` |
| 5. Describe | `describe` | `dotnet build`, or `dotnet sqlsource describe`, with the summary line it prints.  The tool asks the database about the query and does not run it.  It writes `UserRepository.sql.json`, the sidecar.  Commit it | `R/describing.md#describe`, `#which-queries`; `R/sidecars.md#sidecar-files` |
| 6. Call the methods | `call-the-methods` | The calling code in a tab, from `README.md` 94-97, with the connection string of sentence 6 of line 126 for SQL Server.  The build generated `GetUserParams`, `GetUserDto` and `GetUser` | `R/repositories.md#repository-methods`, `R/models.md#model-names` |

- Sources: `README.md` rows 31-130 that name this page; `recipes.md` 416-439.
- Traps, each in its step: SQLSRC209 for every file when no dialect is set (step 3).  SQLSRC241 for a query not described yet (steps 2 and 5).  A `.env` file at the root of the repository is not read, with the fix in one sentence: `SqlSourceEnvFile` in `Directory.Build.props` for the build and `--env-file` for a run of the tool, linked to `R/msbuild.md#setting-env-file` (step 4).  `TrustServerCertificate=True` for SQL Server in a local container, and no sign-in with Microsoft Entra ID, inside the SQL Server tab (step 4).  SQLSRC220 when the projects are not restored, and SQLSRC224 when the database cannot be opened (step 5).  A table that has exactly the three columns of the query, in that order, gives a model named `UsersDto` and not `GetUserDto` (step 6, linked to `R/models.md#table-models`).
- The summary line names the database: `postgres (postgres)` or `mssql (mssql)`.  One sentence says that the first word is the name SqlSource gives your database (`R/settings.md#setting-database`).
- Must not hold: the words "choose" or "pick" before "dialect" or "output"; any dialect name but `postgres` and `mssql`; the default `ansi`; the word "output" at all; `--connection`, `--force`, `--check` or `--watch`; `SqlSourceOnline`.
- "Next:" `how-it-works.md`.

#### `how-it-works.md`: "What just happened?"

| Section | Anchor | Holds | Owner |
|----|----|----|----|
| The picture | `picture` | The diagram of `concepts.md` 5-15, byte for byte, and the paragraph of line 3 in plain language: the build generates the code; the tool asks the database; the sidecar holds the answer; the build reads it | `R/members.md#generator`, `R/cli.md#tool`, `R/sidecars.md#sidecar-files` |
| The words | `words` | Six words, each one sentence and one link: query, marker, sidecar, describe, repository, model | `R/sql-files.md#queries-and-names`, `R/sql-files.md#marker`, `R/sidecars.md#sidecar-files`, `R/describing.md#describe`, `R/repositories.md#repository-methods`, `R/models.md#models` |
| What needs a database and what does not | `what-needs-a-database` | Describing needs it.  A build with no connection reads the committed sidecars and needs none.  The tool reads the schema and not the data | `R/msbuild.md#running-in-the-build`, each database's `index.md#what-describing-does` |

- Sources: `concepts.md` rows 1-17; `README.md` rows 107 and 109-110; `tool.md` 1-14.
- Must not hold: a seventh word; the four outputs; a definition longer than one sentence.  It is not a glossary.  The word "output" is introduced on `transactions.md` (see "Calls").
- "Next:" `write-queries.md`.

#### `write-queries.md`: "How do I write the second and third query?"

| Section | Anchor | Holds | Owner |
|----|----|----|----|
| Several queries in one file | `several-queries` | `ListUserNames` and `RenameUser` added to `UserRepository.sql`.  Each `-- name:` line starts a query | `R/sql-files.md#queries-and-names` |
| One row, or no rows | `result-shapes-in-short` | The five words after `->`, each one sentence, with the three queries as the cases: `one-optional`, none written (every row), `rowcount` | `R/sql-files.md#result-shapes`, `R/return-types.md#method-shapes` |
| A parameter | `parameters` | `@userId` stays in the SQL, and its value is a property of `GetUserParams` | `R/parameters.md#query-parameters` |
| An optional filter | `optional-filter` | `FindUsers` of `README.md` 44-50, with `-- param: @name text null`.  The type word in a tab: `text`, or `nvarchar(100)` | `R/parameters.md#param-marker` |
| A list after IN | `lists` | `WHERE id IN @ids`, with no parentheses.  The property takes an array or a list | `R/parameters.md#list-parameters`; the task, `G/lists.md` |
| A column that is never null | `not-null-columns` | Both forms, since the reader writes both in the `.sql` file: `AS "total!"`, and `-- column: total not null`.  The method throws when the column is null after all | `R/model-properties.md#model-nullability`, `#column-marker`; the task, `G/nullability.md` |
| Document a query | `summaries` | `-- summary:`, and where the editor shows it | `R/sql-files.md#summaries`, `R/members.md#intellisense` |

- Sources: `README.md` rows 53-56, sentences 4 and 5 of row 101, and 226-228; `sql-files.md` 51-112; `parameters-and-tokens.md` 22-98; `generator.md` 413-450.
- Traps: SQLSRC226 and SQLSRC227, when the database cannot work out a parameter's type, with `-- param:` as the fix (`optional-filter`).  A property is nullable though the column never is (`not-null-columns`).  After each change to a query, the build describes it again, or reports SQLSRC244: one sentence that links `keep-up-to-date.md#the-build-describes`.
- Must not hold: tokens; `DECLARE`; list padding or the limit of parameters; PostgreSQL arrays; a `-- column:` line in the file header.
- "Next:" `call-queries.md`.

#### `call-queries.md`: "How do I use the generated code in my application?"

| Section | Anchor | Holds | Owner |
|----|----|----|----|
| The repository and its connection | `repository` | Each method opens a connection, runs the query and disposes the connection.  In a tab: the data source, or the connection string | `R/repositories.md#repository-methods`, `#repository-connection` |
| Register it | `register` | `AddSingleton<IUserRepository, UserRepository>()`, in a tab with the data source or the connection string beside it (`README.md` 189; `recipes.md` 431-435) | `R/repositories.md#repository-interface` |
| The models | `models` | `GetUserParams` and `GetUserDto`: one property for each parameter and each column.  A query that selects a whole table gets a model named after the table | `R/models.md#model-names`, `#table-models` |
| What each method returns | `what-a-method-returns` | By the three queries: `GetUserDto?`, `ListUserNamesDto[]`, `int` | `R/return-types.md#method-shapes` |
| Cancel a call | `cancellation` | Each method takes a `CancellationToken` | `R/repositories.md#repository-signature` |
| When a row is missing | `missing-row` | `one-optional` returns null.  `one` throws `UnexpectedRowCountException` | `R/exceptions.md#unexpected-row-count` |

- Sources: `README.md` rows 72, 91-103; `generator.md` 575-613, 235-268, 832-852, 1080-1097.
- Must not hold: `Async` names; synchronous methods; the collection type; `[SqlSourceConnection]`; how a parameter is sent.  Each is a guide: link `G/naming-and-placement.md`, `G/sync-and-streaming.md` and `G/repository-connection.md` in the "Rules:" line.
- "Next:" `transactions.md`.

#### `transactions.md`: "How do I run two queries together?"

| Section | Anchor | Holds | Owner |
|----|----|----|----|
| Run queries in a transaction | `queries-in-a-transaction` | A repository method takes no transaction, and each call has its own connection.  So the queries that run together take `-- output: methods`: their methods take your connection and your transaction.  A method of your own on the repository opens the connection, begins the transaction, calls them and commits.  Give every method the transaction | `R/repositories.md#repository-transactions`, `R/connection-methods.md#method-connection`, `#method-signature` |
| Set a command timeout | `timeout` | A query that needs its own timeout takes `-- output: methods` in the same way, and the argument `commandTimeout` | `R/connection-methods.md#method-signature` |
| One file, two outputs | `two-outputs` | One file holds queries of both kinds.  `GetAccount` stays a repository method.  This is the page's one sentence on what "output" means, with the link to its owner | `R/settings.md#setting-output` |

- Sources: `recipes.md` 258-300 and `README.md` 191-193, both on the page since phase 8; `generator.md` 735-739 and 743-774.
- Merge MP-13 here, with the worksheet `merges/MP-13.md`: both texts, the merged text, and for each difference the owner that settles it.  No conflict is known.  The sentence of `README.md` 193 on `collection-method` becomes the closing link to `G/many-rows.md`.
- The sample is the recipe's `AccountRepository`, byte for byte, as the PostgreSQL tab.  One sentence introduces it as a second repository of the same application.  The SQL Server tab is new: the class takes `string connectionString`, the method makes and opens a `SqlConnection`, and the query that locks the row is written for SQL Server, with the info string `sql mssql`.
- The page introduces `methods` by the need, never as a choice.  It does not compare outputs.  It ends with links to `G/many-rows.md` and `G/output-modes.md`, above "Next:".
- Must not hold: `models`, `sql`; collection methods; `TransactionScope`; where the methods go or who sees them, beyond "by default they are `internal` extension methods of the connection".
- "Next:" `testing.md`.

#### `testing.md`: "How do I test code that uses a repository?"

| Section | Anchor | Holds | Owner |
|----|----|----|----|
| Take the interface | `interface` | The build generates `IUserRepository` with the public methods.  `UserGreeter` takes it | `R/repositories.md#repository-interface` |
| Write a fake | `fake` | A class of the test project that implements `IUserRepository`, whole | The same |
| Use a mocking library | `mock` | The same test with a mock, in at most ten lines.  The page names no library as required | The same |
| Add a member of your own | `your-own-member` | `public partial interface IUserRepository` with the member, as `generator.md` 718-722 shows | The same |

- Sources: `README.md` 187-189, on the page since phase 8, and sentence 3 of row 101; `generator.md` 708-731.
- No tab is needed: the code is the same for both databases.
- Must not hold: `GenerateInterface`, `InterfaceName`, `SqlSourceRepositoryInterfaces`.  They are settings: one link to `R/repositories.md#repository-interfaces` in the "Rules:" line.  Also not: `InternalsVisibleTo`, which is `G/several-projects.md`.
- "Next:" `keep-up-to-date.md`.

#### `keep-up-to-date.md`: "What do I commit, and what happens when a query or the database changes?"

| Section | Anchor | Holds | Owner |
|----|----|----|----|
| What to commit | `what-to-commit` | The sidecars and `dotnet-tools.json`, yes.  `.env`, no.  The line `*.sql.json linguist-generated=true` for `.gitattributes`.  Never edit a sidecar by hand | `R/sidecars.md#sidecars-in-git`, `#sidecar-files` |
| The build describes for you | `the-build-describes` | With a `.env` file in the folder of the project, `dotnet build` describes a changed query before it compiles | `R/msbuild.md#running-in-the-build` |
| After a schema change | `after-a-migration` | `dotnet sqlsource describe --force`, then commit the sidecars.  No build sees a change to the schema | `R/describing.md#after-a-schema-change` |
| A merge conflict in a sidecar | `merge-conflict` | Leave the conflict in the file, run `dotnet sqlsource describe`, commit what it writes | `R/sidecars.md#sidecars-in-git` |
| In CI | `in-ci` | A build in CI never describes: it reads the committed sidecars.  `dotnet sqlsource describe --check` is the check | `R/msbuild.md#running-in-the-build`, `R/describing.md#check-in-ci` |

- Sources: `README.md` sentences 8 and 9 of row 126, rows 128 and 130; `recipes.md` 185-196, on the page since phase 5; `tool.md` 172-187, 210-246, 250-259.
- Traps: a build with no connection, and every build in CI, does not describe, so a query changed there is SQLSRC244 (`in-ci`, and one sentence in `the-build-describes`).  A `.env` file at the root of the repository does not turn the build's run on: one sentence that links `first-query.md#connection`.
- The console sample of `after-a-migration` keeps its numbers or the ledger lists the change.  Its database name `billing` is not the running example's: change it to the summary line of `first-query.md` and list the block.
- Must not hold: `SqlSourceOnline` and its values; the list of CI variables; `--watch` beyond one sentence.  Above "Next:", the links to `G/describe-on-save.md` and `G/ci.md`.
- "Next:" `errors.md`.

#### `errors.md`: "I see a red line.  What now?"

| Section | Anchor | Holds | Owner |
|----|----|----|----|
| Read an SQLSRC error | `read-an-error` | One real error line, taken apart: the file and line, the id, the message, the help link that opens its section of `../diagnostics.md` | `R/errors.md#where-an-error-is-reported` |
| What an error costs you | `what-an-error-costs` | An error in how a file is written costs the file its code.  An error about describing leaves the query without its method | `R/errors.md#what-an-error-costs` |
| The errors you meet first | `first-errors` | SQLSRC241, SQLSRC244, SQLSRC209, SQLSRC226 and SQLSRC227, SQLSRC224, SQLSRC021, SQLSRC267, SQLSRC206: each one sentence on the cause, one on the fix, and the link to its section.  Outside any tab | Each section of `../diagnostics.md` |
| It built, but it fails when it runs | `at-run-time` | Two sentences and the link to `G/troubleshooting.md` | `G/troubleshooting.md#at-run-time` |

- Sources: `diagnostics.md` 3, by way of `R/errors.md`; `sql-files.md` 164-194.
- The cause in each row of `first-errors` agrees with the bold title line of the section, which a test holds to the descriptor.
- Must not hold: a table of all errors; severity; which error is a warning.
- Last line: "Next: [What do you want to do?](../index.md#find-what-you-need)".

### 6. `index.md`, finished

Write the eight items of the map's section 3, "`index.md`, the landing page", in that order.  Phase 3 left the page with no front matter: add front matter only if the layout of this phase needs it.  The tests read it since phase 0.

- **The paragraph**: `README.md` 5, in plain language, with no status sentence.
- **The sample pair**: the query `GetUser` of "The running example", five lines without its `-- generator:` line, with the info string `sql postgres`, and the calling code of `first-query.md` step 6 for PostgreSQL, at most three lines.  No tab on this page.  Phase 11 puts the same pair in `README.md`, byte for byte.
- **`## Does it work with my database?`** (`databases`): the four rows of the map.  Azure SQL is "accepted, and not tested", never "supported".
- **The start link**: "Start here: Install", to `start/install.md`, as a Markdown link so that the link tests follow it.  Under it, the map's line for the two other readers, word for word.
- **`## What do you want to do?`** (`find-what-you-need`): the 16 rows of the map, in the reader's words, each to one page.  The table below says which rows of today they come from.
- **`## A build error?`** (`errors`), **`## The three parts`** (`the-three-parts`), and the links to the two example projects.
- **Correction C-06**: the sentence "If you have not used SqlSource yet, start with the repository README; this manual is the reference behind it" goes.  So does the sentence that links `concepts.md#glossary`.

| Row of the new table | From the rows of `site/README.md` at lines |
|----|----|
| Run queries in a transaction, or with a timeout | 25, 30 |
| Pass a list to `IN` | 16 |
| Stop a property being nullable | 47 |
| Mock a repository in a test | 24 |
| Insert many rows in one call | 31 |
| Use an enum or an id type of my own | 53, 54 |
| Read a JSON column | 52 |
| Use a second database | 37 |
| Keep the sidecars right in CI | 42 |
| Describe as I save | 41 |
| Choose a table name at run time | 17 |
| Use Dapper | 32, 33 |
| Look up a setting and where to write it | 11 |
| Look up a `-- word:` line | 10 |
| Look up a command or an option | 43 |
| It built, but something is wrong | 57 |

Each of the other 30 rows of the baseline leaves the table, and so does the row that phase 2 added for `errors.md`, whose need `## A build error?` meets.  Its need is met by a sidebar title, by a row of `reference/settings.md#all-settings`, or by another item of the page: line 9 by `start/how-it-works.md`, line 19 by the table of databases, line 55 by the links to the examples, line 56 by `## A build error?`.  The ledger names the replacement of each.

### 7. Remove `site/start/concepts.md`

- Place lines 3 and 5-15 on `how-it-works.md` (step 5), then remove the page and its `toc.yml` entry.
- Phase 8 retargeted the 28 links to a term.  A link to `#glossary` could not get its final target then, since `how-it-works.md#words` did not exist.  The table below has each link that an earlier phase parked for this one.  The brief of phase 9 leaves none.
- Check: `git grep -n "concepts\.md" -- . ':!docs/superpowers'` finds nothing.

**Interim links this phase retargets:**

| Link | Left by | Parked at | Becomes |
|----|----|----|----|
| `docs/architecture/README.md` 46 | Phase 8 | `site/start/concepts.md#glossary` | `site/start/how-it-works.md#words` |
| The link "step 5" of `README.md` 27, on `reference/cli.md#install-the-tool` | Phase 8 | `describing.md#describe` | `../start/first-query.md#describe`.  The words stay: step 5 of that page is "Describe".  Phase 18 merges the pair |
| The links of `index.md` to `start/concepts.md#glossary` and to `start/concepts.md`, from `site/README.md` 3, 9 and 109 | Phases 3 and 8 | The parked page | They go with their sentences and rows.  The new page links `start/how-it-works.md` in "The three parts" |
| The links of `index.md` to a bare page, such as `guides/lists.md`, `reference/cli.md` and `reference/members.md`, from `site/README.md` 109-119 | Phases 3, 5, 6 and 8 | The first page of what the old page became | They go with "The pages".  The new page links the first page of each folder |

After this phase every anchor of the map exists, and no link of `site/` has an interim target.

### 8. The two minimum versions, and `SupportedVersionsTests`

`site/index.md` (`#databases`) and `site/start/install.md` (`#requirements`) each restate the two minimum server versions of `reference/databases/index.md#supported-versions`, and nothing more of that table.  Each links the table.

- Write each as "PostgreSQL 14 or later" and "SQL Server 2017 or later".
- `SupportedVersionsTests` gains both pages.  Use the way phase 4 gave the test to read a restatement.  If phase 4 left none: the test finds every "PostgreSQL *n* or later" and "SQL Server *yyyy* or later" in the page, fails when a page has none of either, and fails when a number is not the table's minimum.
- Check that the test can fail: change 14 to 13 on one page, see the test fail, and change it back.

### 9. `toc.yml` and `AGENTS.md`

`site/start/toc.yml`, in full:

```yaml
- name: Install
  href: install.md
- name: Your first query
  href: first-query.md
- name: How SqlSource works
  href: how-it-works.md
- name: Write queries
  href: write-queries.md
- name: Call your queries
  href: call-queries.md
- name: Run queries in a transaction (or with a timeout)
  href: transactions.md
- name: Test code that uses a repository (mock or fake)
  href: testing.md
- name: Keep your queries up to date
  href: keep-up-to-date.md
- name: When something goes wrong
  href: errors.md
```

Each page's `# ` title is its sidebar title.  Check in the built site that "Get started" in the top bar opens `install.html`.

`site/start/AGENTS.md` keeps the rules of phase 3.  This phase makes these final, in these words, and removes any line about `concepts.md`:

- **The line.**  A topic is here only when an application with one PostgreSQL or SQL Server database and the default `repository` output needs it before its first release.  Anything else is a guide or a reference section.
- **The order is `toc.yml`'s.**  Each page ends with a line that starts "Next:" and links the next page; `errors.md` links the landing page's table of needs.
- **One running example**: `UserRepository` and its queries, whose text is that of `examples/PostgresExamples/Repository` and `examples/SqlServerExamples/Repository`.  A change to those files changes the pages.
- **The `repository` output only.**  `transactions.md` alone shows `-- output: methods`, by the need for a transaction or a timeout.  No page asks the reader to choose an output or a dialect.
- **Each step is whole.**  No step sends the reader to Reference; a page links its owners in its closing "Rules:" line.
- **A rule in its common form.**  No `## Limits`, no `setting-<name>` anchor, no table of a setting's values or defaults.
- **A first-hour trap stands in the step or the tab where it bites**, in full, with its error id and the fix, and its id also stands on `errors.md`.
- **Both databases on every page.**  A tab holds a sample or at most three sentences, and each page reads right with the first tab showing.
- **`SupportedVersionsTests` holds the two minimum database versions of `install.md`** to `../reference/databases/index.md`.

The file ends with the maintenance footer, and `AgentsFileTests` holds its size.  In `site/AGENTS.md`, change only a line that this phase makes wrong.  The row "Feature that meets the line for Get started" of its table takes the outline's words: "A part of one `start/` page; its rules in Reference".  A line that names `concepts.md` goes.

## The ledger

`ledger/phase-10.tsv`, in the format of phase 0.  It holds:

- **Each unit of `README.md` 1-130 that the map sends to a `start/` page or to `index.md`**, with the map's disposition and the new anchor.  The README still holds its own copy, which phase 11 cuts.
- **Each other unit of `README.md` 1-130** that phases 1 and 8 gave no row: the title, the badges, the headings, the commands that stay.  The inventory makes them due in this phase, so each has a row here: `move`, to `README.md`, with the note "phase 11".  A sentence that the map sends to "its owner only" is a part of a `move-split` unit: its part is phase 11's row, and the unit's row is here.
- **Each unit of `site/README.md` 1-57 and 107-119**, now on `index.md`: the 49 table rows (49 at the baseline), each either kept under its new row or `restated-from` its replacement; the row and the bullet of phase 2, by `@site/index.md:<line>`; sentence 2 of line 3 as `corrected`, C-06; sentence 3 as `restated-from start/how-it-works.md#words`.
- **`concepts.md` 3 and 5-15** as `move`, and 17 as `restated-from`.  The headings of lines 1, 21 and 23 are `move-split`, heading only.  The 17 terms have their rows in `ledger/phase-08.tsv`, and get no second row.  Their second copies, and the headings above them, go with the parked page: one row for the page as it stands at this phase's base, `@site/start/concepts.md`, `restated-from`, with `site/start/how-it-works.md#words` as its owner and a note that each term stands in its reference section.
- **The three placed units**: `recipes.md` 185-196, `recipes.md` 258-300 with `README.md` 191-193 (`merge-pair MP-13`), and `README.md` 187-189.  Each is rewritten in this phase, since `start/` has no rewrite phase: say so in the row.
- **One `new` row for each section of each of the ten pages**, with its anchor, the owners it restates, and the source of its sample: an example file with its lines, a fenced block of today, or "compiled by the reviewer".
- **Each fenced block of today that changes**, with the reason.  Known, by the map's ranges: `README.md` 35-51 (the example's queries take its place), 62-70 (the example's class), 93-99 (the calls of the example's queries), 112-124 (the count in the summary line), and the console block of `recipes.md` 189-194.

## Done when

- [ ] `ls site/start` prints `AGENTS.md`, `toc.yml` and the nine pages of step 9, and nothing else.  `ls site/*.md` prints `site/AGENTS.md`, `site/diagnostics.md` and `site/index.md`.
- [ ] Every anchor that this brief names for a `start/` page or for `index.md` is an `<a id>` of that page.  For each page: `grep -o 'id="[^"]*"' site/start/<page>.md` prints the anchors of its table in step 5, in order, and no other.  For `site/index.md` it prints `databases`, `find-what-you-need`, `errors` and `the-three-parts`.
- [ ] `for f in site/start/*.md; do [ "$f" = site/start/AGENTS.md ] || tail -n 1 "$f"; done` prints nine lines that start with "Next:".
- [ ] `grep -n -E '^## Limits|id="setting-' site/start/[a-z]*.md` finds nothing.
- [ ] `grep -n -i -E '(choose|pick|select) (a|an|the|your) (dialect|output)' site/start/[a-z]*.md site/index.md` finds nothing.  ``grep -n -E -e '-- output:|output `|` output|outputs' site/start/[a-z]*.md`` finds lines of `transactions.md` alone.
- [ ] `grep -n '"dialect"' site/start/[a-z]*.md` finds two lines, both in `first-query.md`: `postgres` and `mssql`.
- [ ] `grep -c 'tab/postgresql' <page>` and `grep -c 'tab/sqlserver' <page>` print the same number for each of the ten pages.
- [ ] `git grep -n "concepts\.md" -- . ':!docs/superpowers'` finds nothing.
- [ ] `grep -c "start/first-query.md#describe" site/reference/cli.md` prints 1, and `git diff --word-diff <base> -- site/reference site/guides` shows that link target and no other change.
- [ ] `grep -n -i -E 'start with the .*README|reference behind it|full support|Azure SQL.*is supported' site/index.md site/start/[a-z]*.md` finds nothing.
- [ ] The grep of `site/AGENTS.md` for internals finds nothing on the ten pages.
- [ ] `tools/build-site.sh` ends with no docfx warning.  In `artifacts/site`, each of the ten pages has both tabs in every group, and the diagram of `how-it-works.html` is drawn.
- [ ] `dotnet test tests/SqlSource.Repository.Tests` passes, with `SupportedVersionsTests`, `SiteStructureTests`, `SiteTocTests`, `UserDocsLinksTests`, `UserDocsSamplesTests`, `StatusLanguageTests`, `InboundSiteReferenceTests` and `AgentsFileTests` in the run.
- [ ] "check coverage" reports no unit without a disposition.  "check rewrite 10" has been run, and each sentence it lists as having lost "not", "only", "never" or "unless", and each code span, id or numeral that it lists as gone, has an owner named in the ledger.
- [ ] `wc -w site/start/[a-z]*.md site/index.md` is in the pull request, page by page.  The map's sizes are the guide: about 8,500 words in `start/` and 550 in `index.md`.  A page over 3,500 words is cut before the push.
- [ ] The pull request lists each sentence of a step that is over 20 words, and each other sentence over 25, with the reason it stays.
- [ ] The content reviews, `branch-review:review` and `./pre-commit-validation.sh --full` have passed, in the outline's order, on the last commit.
- [ ] The pull request says that the site is published by hand after the merge, as the outline's "Publishing" says.

## For the reviewers

There is little old text to compare, so the accuracy review is the main one.

**The accuracy reviewer** checks, beyond the outline's kit:

- **The walk.**  Pack the packages (`dotnet pack SqlSource.slnx --output artifacts/packages`).  In a scratch folder outside the repository, follow `install.md`, `first-query.md`, `write-queries.md`, `call-queries.md`, `transactions.md` and `testing.md` to the letter, with those packages: once against PostgreSQL with the PostgreSQL tabs, and once against SQL Server with the SQL Server tabs, on the images of `DefaultImages` in `tests/DatabaseTests/TestHarness/DefaultImages.cs`.  Do only what the page says.  The walk ends with code that compiles and a call that returns a row.  `tools/check-package-install.sh` shows how a project takes the packed packages.
- **Every command** is one the walk ran, or is a command of the README at the baseline, byte for byte.
- **Every output shown** is what the walk printed.
- **Every sample** is one of three things, and the ledger says which: lines of an example file (show the diff, which is only the left-out lines of step 2); a fenced block of today, byte for byte; or new, and compiled in the walk for each database.
- **Every trap** that is cheap to cause is caused in the walk, and the page's words are checked against what is printed: no dialect (SQLSRC209); no sidecar (SQLSRC241); a query changed with no connection (SQLSRC244); no restore (SQLSRC220); a wrong password (SQLSRC224); the `.env` file moved to the root; EF Core 8 or 9 on SQL Server (SQLSRC267); a table with exactly the query's columns.  SQLSRC021, SQLSRC206, SQLSRC226 and SQLSRC227 are checked against their sections of `site/diagnostics.md` and their descriptors.
- **Every restated rule** against the owner that its ledger row names.  A restatement can leave out; it cannot say more than its owner, or other than its owner.
- **Each page twice**, once for each database.  The text outside the tabs must be true for both.
- **`index.md`**: the two versions against the table; each of the 16 rows opens a page that answers it.

**The no-loss reviewer** checks:

- Each of the 49 rows of the old table, and the row of phase 2: its target is reachable from the new page, from a sidebar, or from `reference/settings.md#all-settings`.
- Each fact of the three placed units and of `README.md` 191-193 is on its page or has an owner in the ledger.  `merges/MP-13.md` shows the merge.
- Each of the 17 terms of the removed `concepts.md` stands in its reference section, as `ledger/phase-08.tsv` says.
- Each row of the map of `README.md`, lines 1-130, that names a `start/` page: the fact is on that page.

## Traps

- **The first page a reader sees after a build error is step 2 of `first-query.md`.**  The build fails there by design.  Say so in the step, or the reader stops.
- **`"total!"` on SQL Server.**  The alias mark is written in quotes.  Check in the walk which quotes the SQL Server sample needs, and show what the walk used.
- **`README.md` 91 assumes that `users` has more columns than the query selects.**  With exactly those columns the model is `UsersDto`, and the calling code of step 6 does not compile.  The sentence on the table, and the trap of step 6, cover it.
- **The example's `.env.example` uses `SQLSOURCE_CONNECTION`**, and the page uses `SQLSOURCE_CONNECTION_POSTGRES`, as the map's rows say.  Both are right.  Do not mix them on one page.
- **A link to an anchor inside a tab is dead** when the other tab shows.  docfx does not warn.
- **The link tests read Markdown links only.**  A start "button" written as raw HTML is not followed by any test.
- **The parked units carry links of their old pages.**  Retarget each to its page of today, and keep none that sends the reader into Reference in the middle of a step.
- **`first-query.md` is the largest page**, 1,500 words in the map.  Six steps with tabs grow fast.  Cut explanation, never a step.
- **A rule that this phase restates is still in old prose on its owner page**, since phases 12 to 20 come later.  Restate from what the owner says, not from how it says it.

## Left to the agent

- The words of each page, and the order of sentences inside a section.
- The layout of `index.md` inside docfx's modern template, and the form of the start link.
- Whether `first-query.md` shows a `CREATE TABLE` for `users`, in a tab, or only names the columns.
- The fake and the mock of `testing.md`, and which mocking library the sample uses.
- The exact SQL of the new samples of `write-queries.md` and of the SQL Server tab of `transactions.md`, within step 2's rules.
- The names of the test methods that `SupportedVersionsTests` gains.

## Calls made in this brief

- **A sample is the example file's text less its comment lines and less `-- generator: method-type=both`.**  The map says the samples "are the files" of the two `Repository` folders, which compile.  Those files give `GetUser` a synchronous twin, which is a guide's topic, and the outline keeps the examples as they are.  So the first query is the example's query without that line, and the reviewer compiles it.
- **`first-query.md` has one query, `GetUser`.**  The map's step is "Write a query", and the landing page shows one query.  The README's second query, `FindUsers`, is the sample of `write-queries.md#optional-filter`, and the sentence on `-- param:` (`README.md` 56) goes there with it.
- **`transactions.md` keeps the recipe's `AccountRepository`** in place of a transaction on `users`.  It is today's sample, it is a real case for a transaction, and it stays byte for byte.
- **The six words of `how-it-works.md#words` are the map's**: query, marker, sidecar, describe, repository, model.  "Output" is not among them.  It is introduced on `transactions.md#two-outputs`, where the reader first writes `-- output:`.
- **The table-model trap is added** to `first-query.md` step 6.  The map's table of traps does not have it, and `README.md` 91 and 101 show that it bites in the first hour.
- **The sentence of `README.md` 29 on a connection that wraps the driver's is not restated on `install.md`**, as the map's sections 3 and 6.2 have it.  With the `repository` output the method opens its own connection, so it is no first-hour trap.  Its ledger row is `restated-from reference/query-methods.md#limit-wrapped-connection`.
- **The tab ids are `postgresql` and `sqlserver`**, the names of the database folders, which `SiteStructureTests` reads since phase 4.
- **`errors.md` ends with "Next:" to `index.md#find-what-you-need`**, since it is the last page of the path.
- **The old table has 49 rows at the baseline**, and 19 of them stand behind the 16 new rows.  So 30 leave, and the row of phase 2 with them.  The ledger's count is the one that holds.
- **Each link that an earlier phase parked for a `start/` page is retargeted here**, the "step 5" link of `reference/cli.md` among them.  Phase 8 left that one to phase 18, and the outline gives it to the phase that makes the anchor.
