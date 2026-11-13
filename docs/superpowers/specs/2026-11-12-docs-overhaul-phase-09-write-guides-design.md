# Docs overhaul, phase 09: write-guides

The outline is [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md), and the content map is [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).  This brief names their rows and does not copy them.  It is also the writer's guide for the guides of the epic: phase 12 writes to the same shape.
Done by: an Opus agent.  Needs: phases 0 to 8.  Runs alone.

## Goal

Write the guide text that does not exist today, in plain language from the start:

- Three whole pages: `G/nullability.md`, `G/output-modes.md`, and `G/other-databases.md` but its one moved bullet.
- The walkthrough of `G/dapper.md`, under `models`.
- Each section that the map's table "Phase 9, `write-guides`" (section 10) gives a guide, and the entries of `G/troubleshooting.md` that are not a limit's.

After this phase the 20 guides exist, every anchor of the map's section 3 for `guides/` exists, and a reader who uses Dapper, or a database other than PostgreSQL and SQL Server, has a whole path on one page.  The text is at most 11,800 words.  It has no rewrite phase, so it is final when this phase merges.

## Where things stand

- `S/`, `G/`, `R/` and `D/` are the map's short forms of `site/start/`, `site/guides/`, `site/reference/` and `site/reference/databases/`.
- Phase 8 left 18 guides.  Each has its title and the sections that hold text of today, word for word.  That text still reads as today's manual does, and some of it says "recipe", "below" and "step 5".
- `G/other-databases.md` is a title and the heading `dialect-and-output` with one bullet.  `D/other.md` links the page since phase 8.  `G/dapper.md` has every heading of the map, and `models` holds the README's text.
- Every reference page and anchor exists, with today's text under it, so every owner this brief names can be linked.
- In `start/` only `concepts.md`, `transactions.md`, `testing.md` and `keep-up-to-date.md` exist, each with one unit of today.  `S/install.md`, `S/first-query.md` and the others come in phase 10.
- No page of the site uses a docfx tab yet.  This phase writes the first.

## In scope / Not in scope

In scope: the map's section 3, "Guide pages", for the fixed shape and each page's h2 list; the table "Written in: the guide pages", last column; the table of section 10 for the words; section 5, "Corrections", last paragraph, for the guard on Azure SQL.

Not in scope:

- **The moved text.**  Do not change, move or delete a line that phase 8 or an earlier phase placed.  Phase 12 rewrites it, and writes the opening of each page that holds it.
- The pages of `start/`, the landing page and the README (phases 10 and 11).
- A change to a reference page, an example project or a test.  The example projects do not grow in this epic.
- A new anchor that the map does not name.

## The work

### 1. The shape of a guide

The map's section 3 gives the eight parts.  As a skeleton, for a page that this phase writes whole:

```markdown
# <The title of the map's section 1, in the reader's words>

<One sentence: what the reader can do after this page.  It carries the page's search words.>

**Use this when** <the situation, in two sentences.>

<a id="the-maps-anchor"></a>

## <The task, as the reader says it>

<The whole sample: every file the reader writes, each in its own fenced block, with the file's name in a comment on its first line.>

**What you get.**  <The generated members, by name, and the call.>

**The rules you need.**

- <One rule, in one or two sentences.>  See [<the owner's heading>](../reference/<page>.md#<anchor>).

<a id="next-anchor"></a>

## <A variation: a sample and its rules, in the same order>

## If it does not work

- **<What the reader sees.>**  <One sentence: why, and what to do.>  See [<the limit or the error>](<owner>#<anchor>).
```

- "Use this when", "What you get" and "The rules you need" are bold words that open a paragraph, not headings.  The list on the right of a page then shows the tasks alone.
- `## If it does not work` is the last h2 of a page and has no `<a id>`.  `## Limits` stands on `G/dapper.md` alone, and this phase adds nothing to it.
- A page is whole for its task.  The reader does not need the click, and each rule still links its owner.
- A guide shows a setting in use.  It does not give the setting's values, default or precedence.

**A new section inside a page of moved text** is an h2 with the map's anchor, placed in the order of the page's h2 list in the map's section 3.  It holds: one sentence that says when the reader needs it, the sample, "What you get", and "The rules you need".  It does not touch the text above and below it.  If one of its rules has a limit or an error, add the entry to `## If it does not work` at the foot of the page, and make that heading if the page has none.

### 2. The language

The rule is the outline's "The language".  Write to "you".  Use the names the reader types: `-- column:`, `sqlsource.json`, `dotnet sqlsource describe`.  Say *query parameter*, *generator parameter* and *logical database* where the short word is unclear.  Three examples, each from today's text:

| Today | Plain |
|----|----|
| "`-- generator: collection-method` gives `AddUser` a second method, which takes the input model of every user, adds them all in one transaction of its own, 1,000 to a round trip, and returns their ids in the order of the users." (`recipes.md` 323) | "`-- generator: collection-method` gives `AddUser` a second method.  The method takes a collection of `AddUserParams`, one for each user.  It adds all the users in one transaction of its own.  It sends 1,000 users in each round trip.  It returns the ids in the order of the users." |
| "Where you know a column is never null and the database cannot tell, an alias such as `AS "total!"`, or a `-- column: total not null` line, says so, and the method throws if the column is null after all" (`README.md` 101) | "If you know that a column is never null, you can say so in the `.sql` file.  Write `!` at the end of the alias, `AS "total!"`, or add the line `-- column: total not null`.  The build does not check this.  If the column is null when the query runs, the method throws an exception." |
| "A database that merely shares a dialect with one of them, such as DuckDB with `postgres`, is read by that dialect's rules and cannot be described.  The tool still tries to describe a file read as `postgres` whose output is `models`, `methods` or `repository`, and the build asks for its entries, so give a DuckDB file the output `sql`" (`dialects.md` 145) | "If your database is DuckDB, set the dialect to `postgres` and the output to `sql`.  SqlSource reads a DuckDB file by the rules of PostgreSQL.  The tool cannot describe a DuckDB query.  If the output is not `sql`, the tool tries to describe the file, and the build reports an error for each query." |

No sentence of a rewrite may lose "not", "only", "never" or "unless".  The same holds here: a restated rule keeps each such word of its owner.

### 3. Tabs

A tab holds a sample or at most three sentences.  It never holds a heading or an `<a id>`.  A difference between the databases that is longer is two h2 sections, each with its anchor, as `on-postgresql` and `on-sql-server` are.

```markdown
# [PostgreSQL](#tab/postgresql)

<a sample, or a few sentences>

# [SQL Server](#tab/sqlserver)

<the same for SQL Server>

---
```

The group ends with `---`, with a blank line above it, as the reading test of phase 0 and the pages of phase 10 have it.

`SiteStructureTests` fails a tab group that names one described database and not the other.  Phase 4 fixed the tab ids: each is the name of its database's folder, as `site/reference/databases/AGENTS.md` says.

### 4. Samples

- Take a sample from the file this brief names, in `examples/` or in a test.  A sample can leave out queries and comments of its source.  It does not change a line it keeps.
- A sample from `Repository/UserRepository.sql` of an example project leaves out its line 11, `-- generator: method-type=both`, as the samples of Get started and of the landing page do in phase 10.  The method is then asynchronous and is named for the query, `GetUser`.  The example project compiles the other form, so the ledger marks such a sample "written, to compile", and the accuracy reviewer compiles it.
- Name the source of each sample in its row of the ledger, so that the reviewer can compare.
- Where this brief says "none", write the sample, and say so in the ledger: the accuracy reviewer compiles it in a scratch project against the built package (`dotnet pack SqlSource.slnx --output artifacts/packages`).
- A `sql` block names its dialect: `sql postgres`, `sql mssql`, `sql mysql`, `sql sqlite`, or `fragment` or `error`.  `UserDocsSamplesTests` parses it.
- Put no fenced block inside an alert or another blockquote.

### 5. The three pages and the walkthrough

**`G/nullability.md`, "Fix a nullable property".**  Search words: nullable, not null, `?`, null reference warning.

- Reader: a property is `string?` or `int?` and the column is never null, or the compiler warns about a possible null.  The reader asks why, and how to change it.  This is the one task page for that question.
- h2, in order: `why`; `not-null`; `nullable`; `parameters`; `on-postgresql`; `on-sql-server`; then `## If it does not work`.
- `why`: SqlSource treats a column as possibly null unless it can show that it never is.  The cost is a null check, never a wrong value.  You can overrule it in the `.sql` file.
- `not-null`: the mark `!` at the end of an alias, and the line `-- column: <name> not null`.  The alias in a tab for each database: `AS "total!"` and `AS [total!]`.
- `nullable`: the mark `?`, and `-- column: <name> null`.
- `parameters`: `-- param: @name <type> null`.
- `on-postgresql`, `on-sql-server`: the five most common causes for each, in the reader's words, each taken from the list of that database's page, and one link to the full rules.
- If it does not work: "Column 'name' is null." at run time, and on SQL Server `SqlNullValueException`, "Data is Null."; SQLSRC260; SQLSRC227.
- Owners: `R/model-properties.md#model-nullability`, `#column-marker`; `R/parameters.md#param-marker`; `D/postgresql/nullability.md#nullability`, `#limit-columns-treated-as-nullable`; `D/sqlserver/nullability.md#nullability`, `#limit-columns-treated-as-nullable`, `#limit-ansi-warnings-off`.
- Samples: `ListUserDisplayNames` of `examples/PostgresExamples/Models/Orders.sql` and `ListOrderMonths` of `examples/SqlServerExamples/Models/Orders.sql` for `-- column:`; today's samples under `R/model-properties.md#model-nullability` and `D/sqlserver/nullability.md#nullability` for the alias; `README.md` 44-50 for the parameter.

**`G/output-modes.md`, "Choose what is generated (output modes)".**  Search words: output, repository, methods, models, sql.

- Reader: has seen `-- output:` or `"output"` and asks which one to use.  Most readers must leave this page knowing that they need not choose.
- h2: `the-four-outputs`; `set-the-output`; `which-need-a-database`.
- `the-four-outputs`: one table in the order `repository`, `methods`, `models`, `sql`, with the columns "You get" and "Use it when".  The frame is the owner's decision 4, and the page states it in these terms: `repository` is the default and the one to use; `methods` is for queries that you use in code structures of your own, above all to control a transaction, and for a command timeout; `models` and `sql` are fallbacks, mainly for a project that runs its SQL through Dapper or its own ADO.NET code.
- `set-the-output`: three samples, the setting in use for one query, one file and one folder.  One sentence says that the nearest place wins, with a link.
- `which-need-a-database`: `repository`, `methods` and `models` need each query described; `sql` does not.  Then the two cases where `sql` is used inside a project that is described (see the facts).
- It ends with links to `S/transactions.md#queries-in-a-transaction`, `G/dapper.md` and `G/other-databases.md`.
- Owners: `R/settings.md#setting-output`, `#precedence`; `R/repositories.md#repository-methods`, `#repository-transactions`; `R/connection-methods.md#methods-that-take-a-connection`; `R/members.md#constants`, `#token-methods`; `R/models.md#which-models`.
- Samples: the four folders `Repository/`, `Methods/`, `Models/` and `Sql/` of `examples/PostgresExamples`, one for each output.

**`G/other-databases.md`, "Use MySQL, SQLite, Oracle or another database".**  Search words: MySQL, MariaDB, SQLite, Oracle, CockroachDB, DuckDB.

- Reader: the database is not PostgreSQL or SQL Server.  This page is the only path for that reader, and it is whole: the reader needs no page of `start/`.
- h2, as numbered steps: 1. `install`; 2. `write-a-query`; 3. `mark-a-class`; 4. `dialect-and-output`, which holds the moved bullet: write around it and leave it as it is; 5. `run-the-constant`.  Then `what-you-do-not-get`.
- `what-you-do-not-get`: no models, no methods, no check of a query against the schema.  It names SQLSRC209, and says that DuckDB is read as `postgres` and needs the output `sql`.
- Owners: `R/dialects.md#choosing-a-dialect`, `#markers-under-mysql`, `#still-read-differently`; `R/settings.md#setting-dialect`, `#setting-output`; `D/other.md#not-described`, `#duckdb`; `D/index.md#database-support`; `R/members.md#constants`, `#token-methods`; `R/attributes.md#attribute`; `R/sql-files.md#generated-sql`.
- Sample: none for another dialect.  Model it on `examples/PostgresExamples/Sql/CountQueries.sql` and `CountQueries.cs`, written for SQLite with `Microsoft.Data.Sqlite`, so that the reviewer can run it with no server.

**The walkthrough of `G/dapper.md`**, under `models`, above the moved text.  About 400 words.

- Reader: another library runs the SQL.  The reader wants the models, or only the SQL, and arrives from the landing page with nothing installed.
- "Before you start": the three install commands, and `dotnet add package Dapper`, with the driver package in a tab.
- Numbered steps: 1. Write a query.  2. Mark a partial class.  3. Name your database and set the output, in `sqlsource.json`, with the dialect in a tab.  4. Give the tool your connection, in `.env`, in a tab.  5. Describe.  6. Run the query through Dapper.
- **Keep this numbering.**  The moved text below it says "as in step 5", "the `sqlsource.json` of step 3" and, under `sql-only`, "Follow steps 1 to 3 ... and skip the tool".  With these steps each is true.  Steps 1 to 3 must hold for the output `sql` too, and step 3 ends with a link to `#sql-only`.
- Owners: `R/settings.md#setting-output`, `#setting-dialect`; `R/connections.md#env-file`; `R/describing.md#describe`; `R/cli.md#install-the-tool`; `R/models.md#models`; `R/members.md#constants`; each database's `index.md#connecting`.
- Samples: `Models/Users.sql`, `Models/sqlsource.json`, `Models/StoreQueries.cs`, `DapperSetup.cs` and `.env.example` of both example projects.

### 6. The facts these pages must get right

Each row is checked against the source named.  Read the owner section before you restate it.

| Fact | Source |
|----|----|
| The default output is `repository`.  The values are `repository`, `methods`, `models`, `sql` | `src/SqlSource/Settings/OutputKind.cs` |
| `sql`: a constant, or a method that returns the SQL where the query has a token.  `models`: the same, and the models.  `methods` and `repository`: the models and a method that runs the query, and no constant | `OutputKindExtensions.cs`; today's `settings.md` 126-130 |
| A repository method takes no transaction and no command timeout.  A `methods` method takes the connection, a transaction, a timeout and a cancellation token, and is by default an `internal` extension method of `DbConnection` | Today's `generator.md` 109 and 733-775 |
| One file can hold queries of several outputs | `examples/PostgresExamples/Sql/CountQueries.sql`; today's `settings.md` 104-113 |
| A query whose output is `repository`, `methods` or `models` must be described: SQLSRC241 with no entry, SQLSRC244 after a change.  `sql` needs no tool, no sidecar and no connection | Today's `settings.md` 126 |
| Only `postgres` and `mssql` can be described.  Any other dialect with an output other than `sql` is SQLSRC209, in the build and from the tool.  That holds for `models` too, so do not offer `models` on another database | `src/SqlSource/Parsing/SqlDescribableDialects.cs`; section SQLSRC209 |
| With neither a dialect nor an output set, every file is SQLSRC209: the defaults are `ansi` and `repository` | Section SQLSRC209 |
| The dialect names: `mysql`, `mariadb`, `sqlite`, `oracle` (also Firebird), `cockroachdb`, `ansi` for any other, `postgres` for DuckDB | `src/SqlSource/Parsing/SqlDialect.cs`; today's `dialects.md` 23-30 |
| Under the output `sql` SqlSource binds no value and opens no connection.  The SQL is as written, less its comments | `R/sql-files.md#generated-sql`; today's `recipes.md` 593 and 637 |
| The constants are in a private class `Sql` nested in the marked type, so the type's own code uses them | Today's `generator.md` 109; `examples/*/Sql/CountQueries.cs` |
| The two cases for `sql` in a described project: a statement that the database cannot describe (PostgreSQL's `CALL`, SQLSRC233; SQL Server's `CREATE PROCEDURE`, SQLSRC225), and a query that a method cannot send (a table-valued parameter on SQL Server) | Today's `postgresql.md` 63-67; `sqlserver.md` 87 and 468 |
| Azure SQL is "accepted, and not tested".  No page says "supported" of it | Today's `sqlserver.md` 28; the map's section 5 |
| At `models` a project needs no driver for SqlSource, and gets no library types: NodaTime and the others reach query methods alone | Today's `README.md` 240 and 254 |
| Dapper: columns in snake_case need `MatchNamesWithUnderscores`; write parameters in camelCase; use `-- column:` and not `!`; on SQL Server send a `datetime2` as one | `G/dapper.md#models-through-dapper`, `#dapper-with-sql-server`; both `DapperRecipeTests.cs` |
| `!` and `?` stand at the end of an alias, in quotes.  The mark is not in the property's name and stays in the column's name.  The build does not check a `!` | `src/SqlSource/Generation/Models/ModelReader.cs`; today's `generator.md` 424 |
| `-- column:` stands inside the query and names the column as the query returns it.  It is a comment, so the query is not described again.  A change to an alias is a change to the SQL, so the query is | `src/SqlSource/Parsing/SqlColumnMarker.cs`; `tests/SqlSource.Tests/Generator/ColumnMarkerTests.cs`; today's `generator.md` 430-449 |
| `-- column: x not null` on a column whose alias has a mark is SQLSRC260 | Today's `generator.md` 448 |
| A query parameter can be null only when its `-- param:` says `null`, or, for a list, when the query tests the list for null.  `not null` means what no word means.  A change of the type or the nullability needs the query described again | Today's `parameters-and-tokens.md` 56 and 66; `generator.md` 426 |
| On PostgreSQL, `-- param: @name null` with no type can leave SQLSRC227: give the type too | Today's `parameters-and-tokens.md` 59 |

### 7. The sections

Each row: the reader's question; what the section holds; the owners to link; the sample's source.

| Section | Question | Holds | Owners | Sample |
|----|----|----|----|----|
| `G/lists.md#postgresql-arrays` (search: ANY, array) | "On PostgreSQL, do I write `IN @ids` or `= ANY(@ids)`?" | An array parameter is another feature than a list; when each fits | `D/postgresql/query-methods.md#array-parameters`; `R/parameters.md#list-parameters` | Today's sample under `#array-parameters`; `tests/DatabaseTests/Postgres/EndToEnd/ListQueryTests.cs` |
| `G/lists.md#large-and-empty-lists` | "What if the list is empty, null, or has thousands of values?" | Empty and null lists; padding; the limit of parameters | `R/parameters.md#list-padding`, `#list-limit`; `D/sqlserver/query-methods.md#list-parameters`, `#limit-ansi-nulls-off`, `#limit-list-in-an-aggregate` | `ListQueryTests.cs` of both `EndToEnd` folders |
| `G/dynamic-sql.md#clause` (search: ORDER BY) | "How do I choose the order at run time?" | A token for a clause, with a default | `R/tokens.md#tokens`, `#token-defaults`, `#token-marker`; `R/members.md#token-methods` | `ListUsersSortedBy` of `examples/*/Methods/UserQueries.sql` |
| `G/dynamic-sql.md#safety` (search: SQL injection) | "Is this safe?" | A docfx alert: nothing is escaped; pass only text your code chooses; a value goes in a query parameter | `R/tokens.md#tokens`, `#token-validation` | No sample |
| `G/many-rows.md#transaction` | "Which transaction does it run in?" | Its own transaction; an ambient one; yours at `methods` | `R/collection-methods.md#collection-transactions`; `R/repositories.md#repository-transactions`; `R/generator-parameters.md#gp-transaction` | `AddUser` of `examples/*/Methods/UserQueries.sql` and `UserQueries.cs` |
| `G/many-rows.md#failures` | "What happens when one row fails?" | `CollectionItemException` and `ItemIndex`; nothing is added | `R/collection-methods.md#collection-failures`, `#limit-wrapped-connection`; `R/exceptions.md#collection-item-exception`, `#transaction-rolled-back` | `CollectionQueryTests.cs`, `BatchQueryTests.cs` of `tests/DatabaseTests/Postgres/EndToEnd/` |
| `G/sync-and-streaming.md#stream-rows` (search: IAsyncEnumerable, large result) | "The result is too large for an array" | `-- collection-type: IAsyncEnumerable`; the loop; the connection stays open until the loop ends | `R/return-types.md#streaming-rows`, `#setting-collection-type`; `R/query-methods.md#which-methods` for SQLSRC298 | `StreamUsersOldestFirst` of `examples/*/Methods/UserQueries.sql` and `UserQueries.cs` |
| `G/repository-connection.md#mark-the-member` | "The build cannot find my connection, or finds two" | `[SqlSourceConnection]` on the member; one for each database | `R/repositories.md#repository-connection`; `R/attributes.md#attributes` | The moved samples of the page; `tests/SqlSource.Tests/Generator/RepositoryMethodTests.cs` |
| `G/naming-and-placement.md#async-names` (search: Async suffix) | "I want `GetUserAsync`" | `-- generator: async-method-suffix` | `R/generator-parameters.md#gp-async-method-suffix`; `R/query-methods.md#synchronous-methods` | The header of `examples/*/Methods/OrderQueries.sql` |
| `#model-names` (search: Dto) | "I do not want `Dto` and `Params`" | The suffix settings; one query's model name | `R/models.md#model-names`, `#setting-model-suffix`, `#setting-model-name` | Today's samples under those anchors |
| `#record-or-class` | "I need a class, not a record" | The model type setting | `R/models.md#model-shape`, `#setting-model-type` | Today's `recipes.md` 679-686, on `G/dapper.md` |
| `#collection-type-task` (search: `List<T>`) | "I want a list, not an array" | `-- collection-type:` | `R/return-types.md#setting-collection-type` | `ListUsersNewestFirst` of `examples/*/Methods/UserQueries.sql` |
| `#placement` (search: internal) | "Where are the methods, and who can see them?" | Member location, visibility, the nested class | `R/members.md#locations`, `#setting-member-location`, `#setting-visibility`, `#setting-nested-class` | `examples/*/Methods/OrderQueries.sql`; `examples/*/Sql/CountQueries.sql` |
| `G/custom-types.md#vogen` (search: Vogen, StronglyTypedId) | "My id type is made by Vogen" | Numbered steps: write two converters that call the type's own members | `R/converters.md#converters`, `#limit-vogen-and-stronglytypedid` | The block under that limit, byte for byte |
| `G/custom-types.md#enum` (search: enum, InvalidCastException) | "I want a C# enum" | PostgreSQL: a type override and `MapEnum<T>()` on the data source the methods run on.  SQL Server: an enum over the column's own type | `R/type-mapping.md#setting-type-overrides`; `D/postgresql/query-methods.md#method-results`, `#limit-connection-string-repository`; `D/sqlserver/query-methods.md#reading-a-row` | `tests/DatabaseTests/Postgres/EndToEnd/EndToEndSources.cs`; `README.md` 256-266 |
| `G/json.md#json-in-text` | "My JSON is in a `text` or `nvarchar(max)` column" | `-> json Type` in `-- column:`, `-- param:` and `columnTypes` | `R/library-types-and-json.md#json-opt-in`; `R/model-properties.md#column-marker`; each database's `libraries.md#json` | `tests/DatabaseTests/*/EndToEnd/Json/` |
| `G/several-databases.md#repository-on-two` | "One class, two databases" | A connection member for each logical database; SQLSRC030 and SQLSRC031 | `R/repositories.md#repository-connection`; `R/settings.md#setting-database`; `R/connections.md#connections` | `tests/DatabaseTests/Postgres/EndToEnd/Queries/Billing/`; `RepositoryMethodTests.cs` |
| `G/folders-and-files.md#class-for-a-folder` | "One class for every file of a folder" | `Source = "."` | `R/attributes.md#setting-source`, `#files-of-a-type`, `#limit-source-written-as-text` | `examples/*/Models/StoreQueries.cs` |
| `G/several-projects.md#one-file-two-projects` | "Two projects include one `.sql` file" | What each project needs; what a run on one of them does | `R/sidecars.md#limit-file-in-two-projects`; `R/msbuild.md#included-files` | None |
| `G/describe-on-save.md#start-the-watch` (search: watch) | "Describe each time I save" | `dotnet sqlsource describe --watch`, what it prints, how to stop it | `R/describing.md#describe-on-save`, `#what-your-editor-shows`, `#limit-saves-not-seen` | The console sample under `R/describing.md#describe-on-save` |
| `G/ci.md#exit-code` | "How does my pipeline know it failed?" | The exit codes of `describe --check` | `R/cli.md#exit-codes`; `R/describing.md#check-in-ci` | None: a shell fragment |
| `G/report-a-bug.md#what-is-safe` | "Can I post this zip?" | What a bundle renames and what it keeps, as a list | `R/bundles.md#bundle-contents`, `#diagnose` | No sample |

**`G/troubleshooting.md`.**  Add the entries that are not a limit's, at most six, each one or two sentences and one link: "Column 'name' is null." and "Data is Null." to `G/nullability.md#nullable`; a property that is nullable to `G/nullability.md`; a wrong model that is SqlSource's mistake to `G/report-a-bug.md#report-a-bug`; SQLSRC209 on another database to `G/other-databases.md`; SQLSRC241 or SQLSRC244 after a pull to `R/sidecars.md#describing-again`.  Then check that each of the 36 limits and the 3 pointers of the map's section 4 has its entry, and add one that is missing.

### 8. Sidebar, ledger, count

Add `nullability.md` and `output-modes.md` to `guides/toc.yml`, in the groups and the order of the map's section 1.  Count the words of each page with `wc -w` and report the counts in the pull request.

## The ledger

`ledger/phase-09.tsv` holds one row, `new`, for each section and page of steps 5 and 7 and each entry of `G/troubleshooting.md`.  A row names the anchor, the owners restated, the source of the sample or "written, to compile", and the count of words.  No unit of the inventory changes its row.

**Interim links this phase leaves:** none.  Every link of this phase names an anchor that exists, and no link of an earlier phase waits for this one.  A guide that would send the reader to a `start/` page of phase 10 gives the step whole and writes no link.

## Done when

- [ ] `ls site/guides/*.md | grep -v AGENTS | wc -l` prints 20.
- [ ] Each of the 36 anchors that the map gives this phase is declared once: 6 on `nullability.md`, 3 on `output-modes.md`, 5 on `other-databases.md`, and the 22 of step 7.
- [ ] `git diff --numstat <merge base> -- site` shows no deleted line.
- [ ] `git diff <merge base> -- site/guides | grep '^+[^+]' | wc -w` prints at most 11,800.
- [ ] `grep -o -E '\([^)]*#limit-[a-z-]+\)' site/guides/troubleshooting.md | sort -u | wc -l` prints 36.
- [ ] `grep -n -i "azure" site/guides/*.md`: each hit says "accepted" and "not tested", and none says "supported".
- [ ] The grep of "Write effects, never internals" in `site/AGENTS.md`, run over the pages changed, finds nothing to rewrite.
- [ ] `git grep -n -E "\]\(\.\./start/(install|first-query|how-it-works|write-queries|call-queries|errors)\.md" -- site/guides` finds nothing.
- [ ] "check coverage" passes, and "check move" for phases 3 to 8 prints what it printed before this phase.
- [ ] `dotnet test tests/SqlSource.Repository.Tests` passes: `SiteTocTests`, `SiteStructureTests`, `UserDocsLinksTests`, `UserDocsSamplesTests`, `StatusLanguageTests`, `EditorRecipeTests`.
- [ ] `tools/build-site.sh` passes with no warning.
- [ ] The content reviews, `branch-review:review` and `./pre-commit-validation.sh --full` pass, in the outline's order, on the last commit.

## For the reviewers

There is no old text to compare, so the accuracy review is the whole review of the words.

The accuracy reviewer checks each page against four things:

- **The owner.**  Each sentence of "The rules you need" against the section it links, as that section stands, which is today's text.  The link must open the section that states the rule.  A rule with no link is a finding.
- **The source.**  Each row of step 6 against the file it names, and each error id against its descriptor in `src/SqlSource/Diagnostics/` and its section of `diagnostics.md`.
- **The sample.**  Each sample against the example or test file in its ledger row, line by line.  Each sample marked "written, to compile" is compiled against the built package, and the SQLite sample of `G/other-databases.md` is run.  The reviewer says in the review which samples it compiled.
- **Each database.**  Each tab group is read once as a PostgreSQL user and once as a SQL Server user.  The walkthrough of `G/dapper.md` is followed for each, against `examples/PostgresExamples` and `examples/SqlServerExamples`.

It also checks, page by page:

- `G/output-modes.md` states the frame of decision 4 and does not ask a reader with the default to choose.
- `G/other-databases.md` can be followed with no other page, and never offers `models`, `methods` or `repository`.
- The walkthrough's step numbers make the moved sentences of `G/dapper.md` true.
- `G/nullability.md` gives each fix whole, and each of its ten causes is in the list of its database's page.
- The shape, the language rule and the tab rule of steps 1 to 3, and that no guide gives a setting's values, default or precedence.

The no-loss reviewer confirms that no moved line changed: the `numstat` check, and the unchanged output of "check move".

## Traps

- **A link to a `start/` page that phase 10 makes is a dead link**, and docfx fails on it.  Link only `S/transactions.md#queries-in-a-transaction`, `S/testing.md` and `S/keep-up-to-date.md#after-a-migration`.  Phase 10 adds the links from `start/` to these guides.
- **`EditorRecipeTests` reads the one `jsonc` block of `G/describe-on-save.md` that holds a background pattern.**  Add no `jsonc` block to that page.  Quote the watch's two marker lines exactly as `R/describing.md#describe-on-save` does.
- **`models` on another database still generates the constant, with an error.**  Today's `dialects.md` 147 says so.  The guide offers `sql` alone.
- **The alias mark is written differently**: `AS "total!"` on PostgreSQL, `AS [total!]` on SQL Server.
- **`not null` on a `-- param:` is not a way to make anything not null.**  It means what no word means, and adding it makes the sidecar out of date.
- **"Supported" is a claim.**  Say what was tested only where the owner section says it, and link `D/index.md#supported-versions` for a version.
- **A guide that lists values becomes a second owner.**  `G/output-modes.md` names the four outputs because the choice is its task.  No other section lists a setting's values.
- **Status language.**  `StatusLanguageTests` fails on "works today" and "no effect yet".  Write what SqlSource does.
- **The word "recipe"** is gone from the site's structure.  New text says "guide", or names the page.

## Left to the agent

- The words of each page, and the heading of each section, as long as the anchor is the map's.
- Which five causes each database's section of `G/nullability.md` gives, from that database's list.
- The tables and queries of a written sample.
- How many entries `G/troubleshooting.md` gains, up to six.
- The order of the commits.

## Calls made in this brief

- **The parts of the shape are not all headings.**  "Use this when", "What you get" and "The rules you need" are bold words, and `## If it does not work` is an h2 with no `<a id>`.  The map names no anchor for them, and a rewrite adds none.
- **This phase does not write the opening of a page that holds moved text.**  The map says that part of each page's count is "the text of the fixed shape that phase 12 adds around the moved sections".  A section's own search words go in its heading or its first sentence.
- **This phase makes `## If it does not work`** on a page where one of its sections needs an entry, and phase 12 adds the entries of the moved text.
- **The walkthrough of `G/dapper.md` keeps the README's step numbers** for steps 1 to 3 and 5, so that the moved text is true before phase 12 rewrites it.
- **No link of this phase has an interim target.**  A guide that would send the reader to `S/install.md` or `S/first-query.md` gives the step whole and writes no link.  Phase 10 adds the links between the new `start/` pages and these guides.
- **The two cases of `sql` in a described project** are the two of step 6.  The map says "the two cases" and does not name them.
- **`G/output-modes.md` names the four values and shows three places.**  The rule that a guide lists no values is for a guide that uses a setting, and this guide's task is the choice.  It gives no precedence but "the nearest place wins", with a link.
- **The sample of `G/other-databases.md` is SQLite.**  No example project has another dialect, and SQLite runs with no server.
- **The tab ids are `postgresql` and `sqlserver`**, the names of the database folders, as phase 4 fixed them.
- **A sample of the example's repository leaves out `-- generator: method-type=both`**, as in phase 10, so that every page of the site shows `GetUser` in one form.
- **A sample from an example can be shortened** and is otherwise unchanged.
- **The limit is 11,800 words**, the figure of the outline and of the map.
- **The no-loss reviewer has a task in this phase**: that no moved line changed.
