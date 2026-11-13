# Docs overhaul, phase 17: rewrite-generated-code-2

- The outline: [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md).
- The content map: [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).
- The rewrite guide: [`2026-11-12-docs-overhaul/rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md).  Read it first.  This brief holds only what is special to phase 17.
- Done by: an Opus agent.  Needs: phase 11.  Runs beside: the other phases of 12 to 24.

## Goal

The other five pages of the group "Generated code" are in plain language and in the shape of a reference page: the methods that run a query, and what they throw.  MP-05 is merged, and two terms are defined once.  Phase 16 takes the first four pages of the group.

## The pages

All are in `site/reference/`.  "Words" is the size of each page in section 1 of the map: 11,150 in all.  The old text is 10,780 words (section 10 of the map).

| Page | Words | Sections that need most work | Merge | Limits |
|----|----|----|----|----|
| `query-methods.md` | 3,000 | `which-methods`, `synchronous-methods`, `reading-rows` | MP-10: query method | `limit-wrapped-connection` |
| `repositories.md` | 3,150 | `repository-connection`, `repository-signature`, `repository-interface` | MP-05; MP-10: repository | None |
| `connection-methods.md` | 1,250 | `method-location`, `method-connection` | None | None |
| `collection-methods.md` | 1,800 | `collection-methods`, `collection-transactions` | None | `limit-wrapped-connection` |
| `exceptions.md` | 1,950 | `transaction-rolled-back`, `projects-that-share-internals` | None | `limit-caught-by-type`, `limit-cref` |

The three hardest passages:

- **`repositories.md#repository-connection`.**  1,200 words, eight sentences over 40.  It says which member of a type gives the connection, what a pair of members is, what `[SqlSourceConnection]` does, and what a repository on several databases needs.  Seven errors, SQLSRC028 to SQLSRC033 and SQLSRC036, each belong to one case.
- **`query-methods.md#which-methods`.**  Six conditions in one list, with seventeen error ids in brackets.  Then one paragraph of seven sentences says what each kind of error costs: the method, the method and the constant, or the whole file.
- **`query-methods.md#reading-rows`.**  640 words, eight sentences over 40.  The bullet "A JSON `null` is not a null" has one sentence of 77 words, which gives six kinds of C# type and four results.

## Specific to this phase

### MP-05: the interface of a repository

`generator.md` said what the interface is.  `settings.md` said what turns it on and names it.  Phase 6 put the two side by side, untouched: the text of `generator.md` under the heading "The interface" with `repository-interface`, and the section of `settings.md` under its own heading, one level down, with `repository-interfaces` above that heading.  The worksheet says to lift the second heading (section 9 of the guide).  After the merge, "The interface" is one heading with two `<a id>` lines above it: `repository-interface` and `repository-interfaces`.

- **The order of the merged section** is the order of a setting's section: the sample, what the interface holds, the three things that set it (the property of the project, `GenerateInterface`, `InterfaceName`), the default, the errors SQLSRC035 and SQLSRC037.
- **The conflict is the word "setting".**  Phase 6 made correction C-02 at one sentence: the two properties of the attribute "cannot be written in a settings file or a marker: they belong to the type".  Keep those words.  By the one definition, all three are settings with one place each.
- **"It is a property of the project file, and not a setting of a settings file"** is true, and stays.
- **Both samples stay.**  The section opens with the sample of the setting: the `PropertyGroup` and the attribute.
- **Settle a conflict by** `src/SqlSource/Generation/Repositories/RepositoryInterfaces.cs`, `RepositoryShapeReader.cs` and `src/SqlSource/build/SqlSource.props`.

### The terms of MP-10

| Term | Section | What the merged definition must say |
|----|----|----|
| Query method | `query-methods.md#query-methods` | The generated method that runs a query.  It is a repository method or a method that takes a connection.  A token method is not one |
| Repository | `repositories.md#repository-methods` | A type whose queries have the output `repository`: its methods are instance methods that get their own connection.  It is the default |

Write in `merges/MP-10.md` under these two terms only.

### Rules that another section owns, and rules owned here

| On this page | The rule | Its owner |
|----|----|----|
| `query-methods.md#reading-rows`, `#method-parameters` | What a driver reads and sends for its own types | Each database's `query-methods.md#method-results` and `#method-parameters`.  These two sections keep what holds for every database |
| `query-methods.md#which-methods`, last paragraph | SQLSRC209 for a dialect that is not described | `settings.md#setting-output`, and `databases/other.md#not-described` |
| `repositories.md#repository-connection`, "Several databases" | What a logical database is | `settings.md#setting-database`.  How a repository marks a connection source for each stays here: it is the build's use of the setting |
| `collection-methods.md#collection-transactions` | The values of the generator parameter `transaction` | `generator-parameters.md#gp-transaction` |

`repositories.md#repository-transactions` is an owner: a repository method takes no transaction and no command timeout.  Other pages restate it.  Keep it whole, with its second paragraph on the collection method and an ambient transaction.

**Two driver versions outside the one home.**  The last condition of `query-methods.md#which-methods` says "Npgsql 8.0 or later" with SQLSRC247 and "Microsoft.Data.SqlClient 6.1 or later" with SQLSRC267 (`generator.md` 569 at the baseline).  Supported versions have one home, `databases/index.md#supported-versions`.  The map gives the place a row of its table "Supported versions: one home": the sentence keeps its two numbers, each beside the error that enforces it, and this phase links the table from it.  A rewrite can add a link to an anchor that exists.  Check both numbers against the table.

### The reframings on these pages

- **`repositories.md#repository-methods`** says that `repository` is the default output.
- **`connection-methods.md#methods-that-take-a-connection`** says what `methods` is for: your own code structures, above all a transaction or a timeout that your code controls.
- **`repositories.md#repository-transactions`** sends a query that needs a transaction to the output `methods`.  It links `start/transactions.md#queries-in-a-transaction`.
- **Every list of outputs** has `repository` first: "whose output is `repository` or `methods`".
- **A query that cannot have a method wants `-- output: sql`.**  `#which-methods` says so of PostgreSQL's `CALL`, SQLSRC233, and of a dialect that is not described.  Keep both sentences: "fallback" hides nothing.

### Facts most at risk

| Fact | Page | Check against |
|----|----|----|
| The six conditions for a method, and what each error costs | `query-methods.md#which-methods` | `src/SqlSource/Generation/Methods/MethodPlanner.cs`, `MethodConflicts.cs`; `tests/SqlSource.Tests/Generator/MethodDiagnosticsTests.cs`, `MethodConflictTests.cs` |
| What `method-type` gives, the `Async` suffix, `Finalize`, SQLSRC298 and SQLSRC299 | `query-methods.md#synchronous-methods` | `src/SqlSource/Generation/Methods/MethodSynchrony.cs`, `MethodNames.cs`, `MethodPlanner.cs`; `tests/SqlSource.Tests/Generator/SynchronousMethodTests.cs` |
| The messages "Column 'metadata' holds a JSON null." and the others of a row | `query-methods.md#reading-rows` | `src/SqlSource/Generation/Emitting/QueryMethods/RowReaderWriter.cs` |
| Which member gives a repository its connection; SQLSRC028 to SQLSRC033, SQLSRC036 | `repositories.md#repository-connection` | `src/SqlSource/Generation/Repositories/ConnectionResolver.cs`, `ConnectionCandidateReader.cs`; `tests/SqlSource.Tests/Generator/RepositoryDiagnosticsTests.cs` |
| The names a repository method cannot have; SQLSRC274, SQLSRC275 | `repositories.md#repository-signature` | `MethodConflicts.cs`; `tests/SqlSource.Tests/Generator/RepositoryMethodTests.cs` |
| Which methods the interface has; an `internal` method is left out | `repositories.md#repository-interface` | `src/SqlSource/Generation/Methods/InterfaceSignature.cs`; `tests/SqlSource.Tests/Generator/RepositoryInterfaceTests.cs` |
| What Microsoft.Data.SqlClient and Npgsql each do with a command that is not given the transaction | `connection-methods.md#method-connection` | `tests/DatabaseTests/SqlServer.SyncAsync/KnownDifferences.cs`; `tests/DatabaseTests/SqlServer/EndToEnd/` |
| 1,000 items to a round trip; when a collection method begins a transaction; SQLSRC297 | `collection-methods.md` | `src/SqlSource/Generation/Emitting/QueryMethods/CollectionMethods/BatchWriter.cs`, `ChunkSize`; `src/SqlSource/Settings/TransactionMode.cs`; `tests/SqlSource.Tests/Generator/CollectionMethodTests.cs` |
| The members of the three exceptions: `QueryName`, `ItemIndex`, `InnerException` | `exceptions.md` | `src/SqlSource/Generation/Exceptions/ExceptionSource.cs` |
| When a project uses another project's exceptions; SQLSRC022; CS0419 and CS1574 | `exceptions.md#projects-that-share-internals`, `#limit-cref` | `tests/SqlSource.Tests/Generator/ExceptionReuseTests.cs` |

### Mirrored samples and quoted words

- **The bold words "Give the method the transaction"** open a paragraph of `connection-methods.md#method-connection`.  The comment and the message of `tests/DatabaseTests/SqlServer.SyncAsync/KnownDifferences.cs` quote them.  Keep the four words, bold, as the lead-in.
- **The sample of `query-methods.md#query-methods`** has a twin: `OrderLineQueries.cs` in `examples/PostgresExamples/Methods/` and in `examples/SqlServerExamples/Methods/`.  Byte-identical.
- **The heading "Query methods"** is quoted by the comments of those two files.  Keep its words, as section 6 of the rewrite guide rules: no rewrite changes a comment or a test.

### Sizes

`repositories.md` is at 3,150 words and `query-methods.md` at 3,000.  Report both counts.  If `repositories.md` passes 3,500, say that its clean cut is `repository-connection`, which is 1,200 words.

## Done when

- Each check of "The checks of a rewrite", in section 10 of the rewrite guide, passes on the last commit.  "check rewrite 17" exits with 0, so each unit has a row in `ledger/phase-17.tsv`, and it lists no fenced block as changed or gone.  The four limits have their three parts, by the loop of that section.
- The worksheet `MP-05.md` is finished, and the command of that section for `merges/MP-10.md` prints the h2 of Query method and Repository.
- `grep -B6 -E "^#+ The interface$" site/reference/repositories.md | grep -c '<a id="repository-interface'` prints 2: MP-05 is one heading with both `<a id>` lines.
- `grep -c -F "**Give the method the transaction" site/reference/connection-methods.md` prints 1, and `grep -c -E "^#+ Query methods$" site/reference/query-methods.md` prints 1 or more: the quoted words stand.
- `grep -c -F "index.md#supported-versions" site/reference/query-methods.md` prints 1 or more.
- `#which-methods` is a list of conditions, each with its own errors, and a list of what each kind of error costs: the reviewers read this.
- The description of the pull request holds what section 10 of the rewrite guide asks: the word count of each page, with `repositories.md` named if it is over 3,500, and "Facts with no owner", written as its section 8 says.

## For the reviewers

- **No-loss.**  Count the diagnostic ids of `#which-methods` and of `#repository-connection`, old against new.  The checker counts them by page; you check that each still stands with its own condition.
- **Accuracy.**  Check `#repository-connection` against `ConnectionResolver.cs`, case by case.  Check each cost of an error in `#which-methods`: "gets neither its methods nor a constant", "its models are still generated", "leaves the query's whole file", "only its synchronous methods".
- **Both databases.**  `#method-connection`, `#collection-transactions` and `exceptions.md#transaction-rolled-back` each say what PostgreSQL does and what SQL Server does.  `TransactionRolledBackException` is SQL Server's alone.

## Traps

- **Two limits have one name.**  `limit-wrapped-connection` is on `query-methods.md` and on `collection-methods.md`.  The first is an `InvalidCastException` from a method with parameters.  The second is a `NotSupportedException` from a collection method.  Keep each on its page, with its own symptom.
- **"Method" has five kinds on these pages**: a query method, a repository method, a method that takes a connection, a collection method, a synchronous method.  A token method is not a query method.  Name the kind each time.
- **One error has an exception.**  A query that fails a condition gets neither its methods nor a constant, "but for SQLSRC299, which costs a repository query only its synchronous methods".  Keep the exception with the rule.
- **"Opens the connection when its `State` is `Closed`, and closes it again"** has a second half: a connection that was open is left as it was.  Both halves are the rule.
- **The `Async` suffix has three cases.**  It is off by default.  A synchronous method's name never has it.  Under `method-type=both` an asynchronous method's name always has it.
- **A limit of `exceptions.md` is one sentence of 92 words.**  It has four conditions, and two compiler warnings that belong to two cases: CS0419 for the exception itself, CS1574 for a member.

## Calls made in this brief

- **The bold words that the test quotes stay.**  The map offers a choice: keep the words, or change the test's comment and message.  Keeping them touches no file outside the pages.
- **The link to the table of supported versions goes on the last condition of `#which-methods`**, around the words of the two minimums.  The map says that the sentence keeps its numbers and links the table, and names no words for the link.
- **The order of the merged section of MP-05.**  The map says that the pair becomes one heading, and gives no order.
- **The clean cut of `repositories.md`** is named for phase 25.  The map lists the page as close to the limit and names no cut.
