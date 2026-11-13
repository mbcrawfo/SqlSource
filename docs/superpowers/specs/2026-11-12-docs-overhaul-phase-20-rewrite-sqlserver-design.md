# Docs overhaul, phase 20: rewrite-sqlserver

- The outline: [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md).
- The content map: [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).
- The rewrite guide: [`2026-11-12-docs-overhaul/rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md).  Read it first.  This brief holds only what is special to phase 20.
- Done by: an Opus agent.  Needs: phase 11.  Runs beside: the other phases of 12 to 24.

## Goal

The five pages of SQL Server's folder are in plain language and in the shape of a reference page.  Each page states only what SQL Server decides, and links the page that owns a rule of every database.  `query-methods.md`, the one page of a database folder over 3,500 words, comes under that size if it can without the loss of a fact about SQL Server.  The phase has no merge pair.

## The pages

All are in `site/reference/databases/sqlserver/`.  "Words" is the size of each page in section 1 of the map: 11,800 in all.  The old text is 11,400 words (section 10 of the map), close to the limit of one rewrite.

| Page | Words | Sections that need most work | Limits |
|----|----|----|----|
| `index.md` | 2,100 | `connecting`, `parameters`, `what-can-be-described` | `limit-several-result-sets`, `limit-type-name-unicode`, `limit-parameter-name-unicode`, `limit-parameter-two-spellings` |
| `nullability.md` | 1,700 | `nullability`, all of it | `limit-columns-treated-as-nullable`, `limit-ansi-warnings-off` |
| `types.md` | 1,150 | `which-sqlclient` | `limit-ef-core-sqlclient` |
| `libraries.md` | 2,400 | `nodatime`, `json` | None |
| `query-methods.md` | 4,450 | `method-results` and its five h3 parts, `method-parameters`, `collection-methods` | `limit-ansi-nulls-off`, `limit-hidden-rollback`, `limit-rowcount-after-nocount`, `limit-list-in-an-aggregate` |

The three hardest passages:

- **`query-methods.md#method-results`.**  1,400 words in fifteen bullets, eleven sentences over 40 words.  One sentence of 114 words, under `errors-and-rollbacks`, says which exception a method throws when a later error of the batch rolls a transaction back.
- **`nullability.md#nullability`.**  1,290 words, eleven sentences over 40, one of 108.  It lists when a column is never null, with the SQL Server settings that change the answer.  Its limit `limit-columns-treated-as-nullable` has the longest sentence of the folder, 119 words.
- **`query-methods.md#method-parameters`.**  810 words in fourteen bullets.  Each says how one kind of C# value is sent, and several name a type, a size and an exception in one sentence.

## Specific to this phase

### `query-methods.md` is over 3,500 words

The map gives this page two jobs (section 1 of the map, "Pages over 3,500 words").

**1. Cut each sentence that restates a rule of every database, and link the owner.**  Phase 4 cut `#method-results` into five h3 parts, each with an anchor: `reading-a-row`, `null-values`, `errors-and-rollbacks`, `rowcount`, `timeout-and-cancel`.  The two bullets on the transaction stand directly under the h2.  Go through every bullet of the page with this table.

| A sentence about | Its owner | What stays on this page |
|----|----|----|
| What a query method is, its signature, which queries get one | `reference/query-methods.md` | Nothing but a link |
| How a row is read: the property's C# type, converters, a JSON `null` | `reference/query-methods.md#reading-rows` | What Microsoft.Data.SqlClient does: it never widens or narrows a value; `SqlNullValueException`; the `vector` column |
| A repository method takes no transaction | `reference/repositories.md#repository-transactions` | That a connection it borrows must have none |
| What each result shape returns; `rowcount` is `-1` for a `SELECT` | `reference/return-types.md#method-shapes` | `SET NOCOUNT ON`, and the limit |
| What a list is, where it can stand, padding | `reference/parameters.md#list-parameters`, `#list-padding` | How SQL Server sends one; at most 2,098 parameters; one JSON array with `OPENJSON`; a table-valued parameter and the output `sql` |
| What a collection method is; 1,000 items to a round trip; the generator parameter `transaction` | `reference/collection-methods.md` | What a failed item leaves in SQL Server, and `XACT_ABORT` |
| What `TransactionRolledBackException` is, and its `InnerException` | `reference/exceptions.md#transaction-rolled-back` | When SQL Server makes a method throw it |
| A connection that wraps a `SqlConnection` | `reference/query-methods.md#limit-wrapped-connection` | Nothing but a link |

- **Check the owner before you cut**, by section 8 of the rewrite guide.  Much of what this page says of `TransactionRolledBackException`, of a batch of several statements and of `XACT_ABORT` is stated nowhere else.  It stays.
- **A fact that the owner lacks stays here and is reported**, in the ledger with a note that opens `no owner:` and in the pull request, as that section says.  Phase 25 places it with its owner.
- **A sentence that is half a rule of every database and half SQL Server's** is split.  The first half becomes the link.

**2. Write each of the four limits in its three parts.**  Two have a sample, which stays byte-identical.

**The target is under 3,500 words.**  No fact about SQL Server is cut to reach it.  No page is split, and no sixth file is added.  If the page is still over, report its count.  The map says that phase 25 does not split it, and that it stays the one listed exception.

### The reframings on these pages

- **Azure SQL.**  Phase 4 moved the sentence on the supported versions to `databases/index.md`.  `index.md#connecting` keeps "an older server is SQLSRC235" and links the table.  If the page names Azure SQL, it is "accepted, and not tested".
- **Three support facts stay.**  Windows authentication is one "which SqlSource is not tested with".  The tool cannot sign in with Microsoft Entra ID.  A LocalDB instance can be used on Windows alone.
- **A table-valued parameter** sends its query to `-- output: sql`.  That is a use of `sql` inside a described project.  Keep the sentence.
- **Every list of outputs** has `repository` first.
- **The dialect is `mssql`.**  You set it; you do not choose it.

### Facts most at risk

| Fact | Page | Check against |
|----|----|----|
| `TrustServerCertificate=True` for a server whose certificate is not trusted; SQLSRC224 | `index.md#connecting` | `src/SqlSource.Tool/Describing/SqlServer/ConnectionErrors.cs` |
| A name that `DECLARE @name` declares is a local variable | `index.md`, opening | `src/SqlSource/Parsing/SqlDeclaredVariables.cs` |
| A column with no name is SQLSRC266, from the tool | `index.md#models` | `src/SqlSource/Diagnostics/ToolDiagnostics.cs` |
| A character that Unicode added or reclassified after version 3.2; SQLSRC228, SQLSRC231 | `index.md`, three limits | `src/SqlSource/Parsing/SqlDialectRules.cs`; `src/SqlSource.Tool/Describing/SqlServer/Statements/DeclaredTypeText.cs` |
| Each case in which a column is never null; `ANSI_WARNINGS` | `nullability.md` | `src/SqlSource.Tool/Describing/SqlServer/Proofs/ColumnProofs.cs`, `ExpressionProofs.cs`; `tests/DatabaseTests/SqlServer/Describer/ColumnTests.cs`, `ProofTests.cs` |
| Microsoft.Data.SqlClient 6.1 or 7; older is SQLSRC267, 8 or later is SQLSRC268; EF Core 8 or 9 brings 5.1 | `types.md#which-sqlclient`, `#limit-ef-core-sqlclient` | `src/SqlSource/Generation/Types/Drivers/SqlClientProbe.cs`; `tests/SqlSource.Tests/Generator/SqlClientProbeTests.cs` |
| The C# type of each database type | `types.md#type-table` | `src/SqlSource/Generation/Types/Maps/SqlServerTypeMap.cs`; `tests/DatabaseTests/SqlServer/EndToEnd/MethodAllTypesTests.cs` |
| What the query methods convert for NodaTime; NetTopologySuite 2.5.0 or later, without Microsoft.SqlServer.Types | `libraries.md` | `src/SqlSource/Generation/Converters/NodaTimeConversions.cs`, `NetTopologySuiteConversions.cs`; `tests/SqlSource.Tests/Generator/LibraryConversionTests.cs`, `SpatialConversionTests.cs` |
| How each C# value is sent | `query-methods.md#method-parameters` | `src/SqlSource/Generation/Emitting/SqlServerParameterBinder.cs` |
| 2,098 parameters: 2,100 less the two that Microsoft.Data.SqlClient uses | `query-methods.md#list-parameters` | `src/SqlSource/Generation/Emitting/QueryMethods/ListWriter.cs` |
| "The transaction given to the query 'ListOrders' has completed; it is no longer usable." | `query-methods.md#method-results` | `src/SqlSource/Generation/Emitting/QueryMethods/ExecutionCapabilityWriter.cs` |
| The names of the refused calls: `ExecuteReader`, `ExecuteScalar`, `ExecuteNonQuery`, `BeginExecuteReader`, `BeginExecuteNonQuery` | `query-methods.md#method-results` | `tests/DatabaseTests/SqlServer.SyncAsync/KnownDifferences.cs` |
| Which error rolls a transaction back, with `XACT_ABORT` on and off | `#errors-and-rollbacks`, `#collection-methods` | `tests/DatabaseTests/SqlServer/EndToEnd/BatchQueryTests.cs`, `StreamingBatchTests.cs` |
| `rowcount` after `SET NOCOUNT ON` | `#rowcount`, `#limit-rowcount-after-nocount` | `tests/DatabaseTests/SqlServer/EndToEnd/RowCountTests.cs` |
| A timeout is `SqlException` with `Number` `-2`; a cancel is `TaskCanceledException` or `SqlException` with `Number` `0` | `#timeout-and-cancel` | `tests/DatabaseTests/SqlServer/EndToEnd/RunTimeArgumentTests.cs`, `StreamingQueryTests.cs` |

### Samples and tables

- **No sample of these pages has a twin in a test.**  Three limits have a sample: `limit-ef-core-sqlclient`, `limit-hidden-rollback`, `limit-list-in-an-aggregate`.  Byte-identical.
- **The `PackageReference` of `limit-ef-core-sqlclient`** is shown again on `start/install.md`, which phase 10 wrote.  The limit owns it.
- **The type table and the tables of `libraries.md`** are tables of facts.  Leave every row.

## Done when

- Each check of "The checks of a rewrite", in section 10 of the rewrite guide, passes on the last commit.  "check rewrite 20" exits with 0, so each unit has a row in `ledger/phase-20.tsv`, and it lists no fenced block as changed or gone.  The eleven limits have their three parts, by the loop of that section.
- The loop of that section for the anchors prints nothing: the five h3 anchors of `#method-results` are in place.
- Each cut of `query-methods.md` is a `restated-from` row with its owner in `arg`: `grep -c "restated-from" docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-20.tsv` prints 1 or more, and each fact that an owner lacks has a note that opens `no owner:`.
- `wc -w site/reference/databases/sqlserver/query-methods.md` is in the pull request, which says whether the page is under 3,500.
- `ls site/reference/databases/sqlserver/*.md | wc -l` prints 5: no page was split and no sixth file added.
- The lists of `nullability.md` have one case in each item: the reviewers read this.
- The description of the pull request holds what section 10 of the rewrite guide asks: the word count of each page, and "Facts with no owner", written as its section 8 says.

## For the reviewers

- **No-loss.**  `query-methods.md` is where this phase cuts.  For each cut sentence, open the owner's section at the commit the phase started from, and find the fact there.  A fact that the owner lacks must still be on this page.
- **Accuracy.**  Read `#errors-and-rollbacks` against `BatchQueryTests.cs`.  The old sentences join "unless", "when" and "in place of".  Check that each split sentence still says which exception a method throws, and in place of which.
- **One database.**  This phase has no tabs.  Check that no rule of PostgreSQL came in as a rule of SQL Server: Npgsql's `InvalidCastException` for a null is not SQL Server's `SqlNullValueException`.

## Traps

- **`TransactionRolledBackException` is SQL Server's alone.**  The owner page says what the exception is.  This page is the only one that says when the batch makes a method throw it.
- **"May have rolled the transaction back" is exact.**  It depends on the error and on `XACT_ABORT`.  Do not write "rolls back".
- **A list on SQL Server and an array on PostgreSQL are two features.**  This folder has `#list-parameters`.  Do not borrow a sentence from PostgreSQL's `#array-parameters`.
- **Library types are off on SQL Server** until `libraryTypes` turns one on, whatever the project references.  On PostgreSQL they are on by reference.  Do not copy across.
- **A cancelled method throws two different exceptions.**  `catch (OperationCanceledException)` does not catch the second.  The advice to check the token in a `catch (SqlException)` stays.
- **`ANSI_NULLS`, `ANSI_WARNINGS`, `NOCOUNT` and `XACT_ABORT` are four settings of a session.**  Each belongs to one limit or one bullet.  Keep each name with its own effect.
- **The limits of `index.md` on Unicode** are three, and they look alike: a type's name, a parameter's name, and a parameter written in two ways.  Each has its own error and its own way round.

## Calls made in this brief

- **The table of who owns what on `query-methods.md`.**  The map names the five owners and leaves the line between them and SQL Server's own facts to this phase.
- **A fact that the owner lacks stays**, even where the map names the owner, and is reported for phase 25.  The rewrites run at the same time, and this phase cannot add to a page of phase 17.
- **A sentence that is half general and half SQL Server's is split**, and the general half becomes the link.
