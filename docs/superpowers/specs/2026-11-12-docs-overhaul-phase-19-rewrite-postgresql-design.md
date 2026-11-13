# Docs overhaul, phase 19: rewrite-postgresql

- The outline: [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md).
- The content map: [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).
- The rewrite guide: [`2026-11-12-docs-overhaul/rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md).  Read it first.  This brief holds only what is special to phase 19.
- Done by: an Opus agent.  Needs: phase 11.  Runs beside: the other phases of 12 to 24.

## Goal

The five pages of PostgreSQL's folder, and the two pages that stand beside the database folders, are in plain language and in the shape of a reference page.  The supported versions of both databases are in one table, merged from the sentences of four old pages (MP-23).  Each page of the folder states only what PostgreSQL decides.

## The pages

`D/` is `site/reference/databases/`, and `P/` is `D/postgresql/`.  "Words" is the size of each page in section 1 of the map: 12,350 in all.  The old text is 11,560 words (section 10 of the map), under the limit of one rewrite: phases 3 and 4 wrote the table of `D/index.md` and most of `D/other.md` new, and new text has no rewrite.

| Page | Words | Sections that need most work | Merge | Limits |
|----|----|----|----|----|
| `P/index.md` | 1,900 | `parameters`, `what-describing-does`, `models` | None | `limit-operator-on-oid`, `limit-many-partitions` |
| `P/nullability.md` | 2,450 | `nullability`, all of it | None | `limit-columns-treated-as-nullable` |
| `P/types.md` | 1,900 | `which-npgsql`, `type-notes` | None | None |
| `P/libraries.md` | 2,000 | `nodatime`, `json` | None | None |
| `P/query-methods.md` | 2,800 | `method-results`, `array-parameters` | MP-09 | `limit-connection-string-repository` |
| `D/index.md` | 700 | `supported-versions`, `azure-sql` | MP-23 | None |
| `D/other.md` | 600 | `not-described`, `duckdb` | None | None |

The three hardest passages:

- **`P/nullability.md#nullability`.**  2,300 words, 22 sentences over 40.  Its lists say when SqlSource can tell that a column is never null, and when it treats one as a column that can be null.  Each item names SQL functions and types, with exceptions inside brackets: "`length` of a `text` or a `varchar` (not of a `character(n)`)".
- **`P/query-methods.md#method-results`.**  Sixteen bullets in 960 words.  Some restate a rule that another page owns, one opens with bold words that a test quotes, and one sample has a twin in an example.
- **`D/index.md#supported-versions`.**  MP-23 folds sentences from the README, `postgresql.md`, `sqlserver.md` and `dialects.md` into one table that `SupportedVersionsTests` holds.

## Specific to this phase

### MP-23: the supported versions

`D/index.md#supported-versions` is the one home.  Phase 4 wrote the table, as new text, and put the old sentences beside it, word for word.  Phase 8 added the sentences of the README.  Merge them all into the table and a short text under it.

- **No heading is lifted** (section 9 of the guide): the sentences stand loose under the one heading, beside the table.
- **Every fact of every sentence is in the table, in the text under it, or under `#azure-sql`.**  Then the loose sentences go.  The worksheet lists the facts first.
- **Two sentences of `README.md` 29 came whole**, each with a part that is no version of a database: what the tool runs on, and that a project on EF Core 8 or 9 references Microsoft.Data.SqlClient 6.1 itself.  Phase 8 could not cut a sentence.  Cut both parts here, as restatements by section 8 of the guide.  The owners are `cli.md#requirements`, in phase 18, and `databases/sqlserver/types.md#limit-ef-core-sqlclient`, in phase 20.  If an owner lacks the fact, the part stays and is reported.
- **The facts**, each with what settles it:

| Fact | Settle it by |
|----|----|
| PostgreSQL 14 and later; tested on every major from 14 to 18 | Root `AGENTS.md` rule 5; `tests/DatabaseTests/TestHarness/DefaultImages.cs`; `.github/workflows/nightly.yml`.  An older server is SQLSRC235, in `src/SqlSource/Diagnostics/ToolDiagnostics.cs` |
| SQL Server 2017 and later; tested on 2017, 2019, 2022 and 2025 | The same files, and `SqlServerContainer.ImageBuilds` |
| Npgsql 8, 9 or 10; tested on 8 and 10; older is SQLSRC247, 11 or later is SQLSRC248 | `src/SqlSource/Diagnostics/SqlDiagnostics.cs`; `tests/DatabaseTests/Postgres.Npgsql8/` |
| Microsoft.Data.SqlClient 6.1 or 7; older is SQLSRC267, 8 or later is SQLSRC268 | `SqlDiagnostics.cs`; `tests/DatabaseTests/SqlServer.SqlClient61/` |
| Azure SQL Database and Azure SQL Managed Instance are accepted whatever version they report, and are not tested | `ToolDiagnostics.cs`, SQLSRC235 |

- **Do not change a cell of the table** unless a source above shows that it is wrong.  A test reads it, and three other pages restate its two minimum versions.
- **The table of `#database-support`** has the row "SQL Server, Azure SQL", with "Yes" under "Can be described".  That is true.  It must not read as "supported": keep the row, and let the text point at `#azure-sql`.
- **This page holds facts about SQL Server.**  They are yours here.  Phase 20 does not touch `D/index.md`.

### MP-09: a repository made from a connection string

`P/query-methods.md#limit-connection-string-repository` holds two bullets under the one heading of the limit: one of `postgresql.md`, one of `generator.md`.  No heading is lifted.  The map knows no conflict.  Write the merged limit in three parts.  "What you see" is an `InvalidCastException` for a NodaTime type, a PostGIS type or an enum.  Settle a conflict by `src/SqlSource/Generation/Repositories/ConnectionSource.cs` and `ConnectionResolver.cs`.

### A page states only what PostgreSQL decides

This is the rule of `site/reference/databases/AGENTS.md`.  Cut each of these to a restatement and a link, by section 8 of the rewrite guide.

| On this page | The rule | Its owner |
|----|----|----|
| `P/index.md#connecting` | "PostgreSQL 14 and later" | `D/index.md#supported-versions`.  The section keeps "an older server is SQLSRC235" |
| `P/types.md#which-npgsql` | "Npgsql 8 or later" | `D/index.md#supported-versions`.  What each version of Npgsql changes stays here, with SQLSRC247, SQLSRC248 and SQLSRC249 |
| `P/query-methods.md#method-results`, first bullet | A repository method takes no transaction | `repositories.md#repository-transactions` |
| `P/query-methods.md#method-results`, first bullet | What Microsoft.Data.SqlClient does with a command that is not given the transaction | `connection-methods.md#method-connection`.  Keep what Npgsql does, and the advice to pass the transaction |
| `P/query-methods.md#method-results`, last bullet | A connection that wraps Npgsql's | `query-methods.md#limit-wrapped-connection` |
| `P/query-methods.md#collection-methods` | What a collection method is, and 1,000 items to a round trip | `collection-methods.md#collection-methods`.  What a failed item leaves in PostgreSQL stays here |
| `P/libraries.md`, each section | How a library is turned on or off | `library-types-and-json.md#setting-library-types`.  Which types a library gives on PostgreSQL, and what the application must call, stay here |
| `D/other.md#not-described` | The default output is `repository` | `settings.md#setting-output` |

These sections are owners.  Keep each whole: `P/nullability.md#nullability`, `P/query-methods.md#array-parameters`, `P/types.md#type-table`, `P/index.md#what-describing-does`.

### The reframings on these pages

- **`D/index.md` and `D/other.md` never say "choose a dialect".**  A reader sets the dialect to the database.  `D/other.md` sends a reader of another database to `guides/other-databases.md`, and says that the output there is `sql`.
- **DuckDB** is read as `postgres`, cannot be described, and needs the output `sql`.  Keep all three facts in `D/other.md#duckdb`.
- **Azure SQL** is "accepted, and not tested".
- **Every list of outputs** has `repository` first.  `#database-support` says "whose output is `sql` or `models`": it becomes "`models` or `sql`".

### Facts most at risk

| Fact | Page | Check against |
|----|----|----|
| Each case in which a column is never null, and each in which it can be null | `P/nullability.md` | `src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.cs` and its two partial files; `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs`, `ExpressionTests.cs`, `SetOperationTests.cs`, `WalkTests.cs` |
| "It never gives you a wrong value" | `P/nullability.md` | The same tests.  Keep "never" |
| The C# type of each database type, for Npgsql 8 and 9 and for Npgsql 10 | `P/types.md#type-table` | `src/SqlSource/Generation/Types/Maps/PostgresTypeMap.cs`; `tests/DatabaseTests/Postgres/EndToEnd/EveryTypeTests.cs` |
| What a project with no Npgsql gets; SQLSRC249, SQLSRC273 | `P/types.md#which-npgsql` | `src/SqlSource/Generation/Types/Drivers/NpgsqlProbe.cs`; `tests/SqlSource.Tests/Generator/NpgsqlProbeTests.cs` |
| What the tool does to your database while it describes; a function marked `IMMUTABLE` can run | `P/index.md#what-describing-does` | `tests/DatabaseTests/Postgres/Describer/TransactionTests.cs`, `SessionTests.cs` |
| A pooler in session or transaction mode; statement mode and `Multiplexing=true` are SQLSRC236 | `P/index.md#connecting` | `src/SqlSource.Tool/Describing/Postgres/ConnectionErrors.cs` |
| When a parameter's type cannot be worked out; SQLSRC226, SQLSRC227 | `P/index.md#parameters` | `tests/DatabaseTests/Postgres/Describer/ParameterTests.cs` |
| `id = ANY(@ids)` sends one array; `IN (@ids)` "is not what you want" | `P/query-methods.md#array-parameters` | `src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs` |
| What a method throws for a null, a timeout, a cancel, a loop that ends early | `P/query-methods.md#method-results` | `tests/DatabaseTests/Postgres/EndToEnd/RunTimeTests.cs`, `StreamingQueryTests.cs` |
| Which queries need the output `sql` | `D/other.md` | `src/SqlSource/Parsing/SqlDescribableDialects.cs` |

### Mirrored samples and quoted words

- **The bold words "A synchronous method needs a data source without multiplexing"** open a bullet of `P/query-methods.md#method-results`.  The comment and the message of `tests/DatabaseTests/Postgres.SyncAsync/KnownDifferences.cs` quote them.  Keep the words, bold, as the lead-in.
- **The heading "What a method reads and throws"** is quoted by `examples/PostgresExamples/Methods/StoreDataSource.cs`.  Keep its words.  Section 6 of the rewrite guide rules both: no rewrite changes a comment or a test.
- **The `UseNodaTime()` sample of `#method-results`** has its twin in that file.  Byte-identical.
- **The type table** is a table of facts, about 950 words.  Leave every row.

### Sizes

No page is over 3,500 words.  The phase is over 12,000 by its page sizes and under it by its old text.  Count the old text that you rewrite, and report it with the count of each page.

## Done when

- Each check of "The checks of a rewrite", in section 10 of the rewrite guide, passes on the last commit.  "check rewrite 19" exits with 0, so each unit has a row in `ledger/phase-19.tsv`, and it lists no fenced block as changed or gone.  The four limits have their three parts, by the loop of that section.
- The worksheets `MP-09.md` and `MP-23.md` are finished.  MP-23 is one table, a short text and `#azure-sql`, with no loose sentence that states a version: the reviewers read this.
- `SupportedVersionsTests` passes.
- `grep -c -F "**A synchronous method needs a data source without multiplexing" site/reference/databases/postgresql/query-methods.md` prints 1, and `grep -c -E "^#+ What a method reads and throws$"` on the same page prints 1: the quoted words stand.
- `git grep -n -i -E "choos(e|ing) (a|one|the) dialect|full support" -- site/reference/databases/index.md site/reference/databases/other.md` prints nothing.
- "check rewrite 19" prints, for `P/nullability.md`, no code span whose count fell without a note: each list keeps every function and type name of its old items.
- The description of the pull request holds what section 10 of the rewrite guide asks: the word count of each page, the count of the old text that the phase rewrote, and "Facts with no owner", written as its section 8 says.

## For the reviewers

- **No-loss.**  For `P/nullability.md`, the checker's sets of code spans are the best tool: each function name must be there as often as before.  Then read the lists item by item.
- **Accuracy.**  Check each cell of the table of versions against the files of MP-23.  Check that no page of the folder states a rule of SQL Server as its own.
- **One database.**  This phase has no tabs.  A sentence that names SQL Server on a `P/` page is a pointer or a mistake.

## Traps

- **The nullability page is where "effects, never internals" is hardest.**  Say what makes a column one that can be null: an outer join, a view, an expression.  Never say how the tool finds out.  Run the `grep` of `site/AGENTS.md`.
- **"Can tell that a column is never null" is not "the column is never null".**  SqlSource treats a column as one that can be null unless it can show otherwise.  Keep "unless it can show".
- **Arrays are not lists.**  `P/query-methods.md#array-parameters` is `= ANY(@ids)` with one array.  A list after `IN` is another feature, on `parameters.md`.  The last bullet tells the two apart.  Keep it.
- **A column with no name** is SQLSRC253 on PostgreSQL, from the build.  On SQL Server it is another error, from the tool.  Do not copy across.
- **Library types are on by reference on PostgreSQL.**  A project that references `Npgsql.NodaTime` gets NodaTime's types with no setting.  The application must still call `UseNodaTime()`.
- **`D/other.md` is short and stays short.**  Most of it is new text of phase 3.  Rewrite the two old paragraphs alone.

## Calls made in this brief

- **The bold words that the test quotes stay.**  The map offers a choice; keeping them touches no file outside the pages.
- **The sentence on Microsoft.Data.SqlClient in `#method-results` becomes a link.**  The rule of the database folders says that a page states only what is true of its database.
- **The row "SQL Server, Azure SQL" of `#database-support` stays one row.**  "Can be described" is true of both; `#azure-sql` says what is not tested.
- **The limit of 12,000 words is counted on the old text**, as the map's note on this phase says.
- **The two parts of `README.md` 29 that are no version of a database are cut here**, as restatements.  The brief of phase 8 carries them whole and leaves the cut to this merge.
