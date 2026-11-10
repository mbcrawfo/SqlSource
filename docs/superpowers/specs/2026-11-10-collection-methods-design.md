# Collection methods design

## Intent

A query that inserts one row, such as `INSERT INTO users (...) VALUES (...) RETURNING id`, gets a method that runs it for one input model.  A user who has a list of rows to insert writes a loop, opens a transaction around it and collects what each call returns.  This change generates that method: a query that opts in also gets an overload that takes `IEnumerable<TParams>`, runs the query once for each item, in one transaction by default, and returns every row the items returned as one flat collection.

The owner expects most collections to hold a few items to a few dozen, and expects users to send thousands and expect it to work.  So the method must be fast at thousands, not only correct at a dozen.

"This is one call" means one transaction to the user: by default the whole collection commits or none of it does.

## Goals

- Generator parameters take a value from a list of words of each parameter's own, beyond `on` and `off`, so that this change and later ones can add parameters with values.
- A new generator parameter, `collection-method`, off by default, gives a query an overload of its query method that takes a collection of its input model.
- A new generator parameter, `transaction`, says whether that overload runs in a transaction it begins, and at which isolation level.  On by default.
- The overload sends its items as batches of commands, so that thousands of items take one round trip for each chunk of items, not one for each item.
- It returns one flat collection of every row, in the order of the items, and for `rowcount` the total.

## Non-goals

- **Running a SQL Server query whose statements return more than one result set over a collection.**  Its result sets cannot be matched to its items in a batch (see the spike).  A loop for such queries, approach C below, is not planned.
- **A setting for the chunk size.**  It is fixed (see Design).
- **Streaming the rows of a collection method.**  `IAsyncEnumerable` is not offered for it.
- **Grouping the rows by item**, or a row count for each item.  The owner chose one flat collection and a total.
- **Rewriting a query into one multi-row statement**, such as `INSERT ... VALUES (...), (...)`, `unnest`, `COPY` or `SqlBulkCopy`.  They fit only some `INSERT`s, rewrite the user's SQL, and the bulk paths return no rows.
- **A transaction for a single-item method.**  One statement is already atomic.  `transaction` affects only collection methods.
- **Generator parameters whose value is free text or a number.**  Each parameter with a value has a closed list of words.

## Anti-goals

- A collection method that commits some items and not others, by default.  Without `transaction=off`, a failure leaves nothing of the call in the database.
- A silent slow path: a query that cannot be batched is an error, never a quiet loop.
- A collection method that begins or ends a transaction the caller gave it.

## Constraints

- Projects target .NET 8 or later, Npgsql 8 or later and Microsoft.Data.SqlClient 6.1 or later.  `DbBatch` is in .NET 6, `NpgsqlBatch` in Npgsql 6, and `SqlBatch` in Microsoft.Data.SqlClient 5.2 (on .NET 6 and later), so every supported project has it.  The spike ran on the floors: Npgsql 8.0.8 and Microsoft.Data.SqlClient 6.1.1, on PostgreSQL 18.6 and on SQL Server 2025 and 2017.
- The generated code uses ADO.NET alone, as every query method does: no Dapper, no reflection, no library of SqlSource's.
- A PostgreSQL query is one statement: the tool describes no other.  A SQL Server query may hold several statements, and is described by its first result set ([D-0006](../../deferred/D-0006-multiple-result-sets-on-sql-server.md)).  The generator does not parse T-SQL into statements and cannot tell how many result sets a query returns.
- The rules of the root `AGENTS.md` apply: the manual, the diagnostics page, the settings schema, the architecture pages, the examples and `AnalyzerReleases.Unshipped.md` change in the same PR as the behaviour they describe.

## Spike findings

A throwaway program ran on Docker images of PostgreSQL 18.6, SQL Server 2025 (`2025-CU9-GDR1-ubuntu-24.04`) and SQL Server 2017 (`2017-CU31-GDR19-ubuntu-18.04`).  The two SQL Server versions behaved the same in every case.

**Dapper** runs a command once for each element of a collection, on the connection and the transaction it is given, and begins no transaction of its own.  Neither driver begins one either: without a transaction, each statement commits on its own.  "The provider's default" is `BeginTransaction()` with no isolation level, which is `READ COMMITTED` on both databases.

**Speed**, in milliseconds, inside one transaction, reading back the inserted rows, on a database on the same machine.  The SQL Server image ran under amd64 emulation, so its numbers compare with each other only.  Over a network the loop pays one round trip for each item, so the gap widens.

| Items | PostgreSQL loop | PostgreSQL batch | PostgreSQL chunks of 1,000 | SQL Server loop | SQL Server batch | SQL Server chunks of 1,000 |
|---|---|---|---|---|---|---|
| 10 | 14 | 4 | 3 | 32 | 6 | 4 |
| 100 | 34 | 3 | 3 | 37 | 8 | 6 |
| 1,000 | 125 | 11 | 12 | 274 | 20 | 32 |
| 10,000 | 1,133 | 116 | 118 | 2,480 | 178 | 154 |
| 50,000 | 5,610 | 464 | 467 | 11,519 | 784 | 709 |

One batch of 50,000 commands with two parameters each ran on both, so the parameter limits apply to each command, not to the batch.

**Results.**  Each command of a single statement gives one result set, also when it returns no rows: an `UPDATE ... RETURNING` or `UPDATE ... OUTPUT` that matches nothing gives an empty one.  So with one statement for each item the result sets line up with the items.  `DbBatchCommand.RecordsAffected` holds each command's count on both, and the batch's total is their sum.

**A failure at item 3 of 5** (a duplicate key):

| Case | PostgreSQL | SQL Server |
|---|---|---|
| Where it is thrown | `NextResult` after item 2's rows | `Read` of item 3's result set |
| No transaction | The whole batch rolls back: Npgsql runs a batch in an implicit transaction, and skips what follows the error | Items 1, 2, 4 and 5 commit: the error ends item 3's statement, and the later commands still run.  Under `XACT_ABORT ON`, items 1 and 2 commit and 4 and 5 do not run |
| In a transaction | The transaction is aborted, and every later command fails until it is rolled back | `XACT_ABORT OFF`: the transaction is open, with items 1, 2, 4 and 5 in it.  `XACT_ABORT ON`, or an error that ends the batch such as a failed conversion: the server rolled the transaction back, and its `Connection` is null |
| Which item failed | `PostgresException.BatchCommand` | `SqlException.BatchCommand` |

A failure in the first command on SQL Server is thrown by `ExecuteReader` itself, and the later commands still ran.

**Several statements in one command.**  Npgsql refuses an `NpgsqlBatchCommand` of more than one statement with `NotSupportedException`.  It accepts a final `;` followed by white space or a block comment, and refuses one followed by a line comment, `SELECT @id; -- done`, which `keep-comments` can leave in the SQL.  On SQL Server a command of `UPDATE ...; SELECT ...` gives one result set, and one of `SELECT 1; SELECT 2` gives two, with nothing in the reader that says where one command's result sets end.

## Approaches

1. **A batch, sent in chunks, in one transaction.**  Chosen.  One round trip for each chunk, about ten times faster than a loop from a thousand items.  It is a new kind of method in the writer, and the SQL Server rollback handling has to be reworked for several commands.  A SQL Server query whose statements return several result sets cannot use it.
2. **A loop of one command for each item, in one transaction.**  The smallest change: today's method body inside a `foreach`.  Every query works.  One round trip for each item, which is slow at the thousands the owner expects.
3. **A batch, with a loop for SQL Server queries of several result sets.**  Every query works and most are fast, but it is both 1 and 2 to write and test, and the fallback is a slowdown nobody sees.

The owner chose 1.  3 stays possible on top of 1, and is not planned.

**The opt-in.**  The owner chose a generator parameter, so that a user can set it wherever they want, for a query, a file or a folder, though the usual use is one query.  Generator parameters take only `on` and `off` today, and `transaction` needs a value, so the owner chose to let generator parameters take values, each parameter from a list of words of its own, over a separate marker for the transaction.  The owner expects more parameters with values later.

**Both methods.**  The owner chose that a query that opts in keeps its single-item method and gains the collection method beside it.

## Design

The work is two pull requests, in this order.

### Part 1: generator parameters with values

No behaviour a user can see changes, but for the message of SQLSRC111 and the settings schema.

- **Each generator parameter has a list of words**, its values, and a default among them.  The on/off parameters of today have `on` and `off`.  A parameter's state at a level is one of its words, or unnamed.  The values are a closed set for each parameter: an enum, or a small index into the parameter's list, so that a level's choices and `QuerySettings` stay small, equatable values that cost the generator's caching nothing.  The flags enum `GeneratorParameters`, `GeneratorParameterChoices` (`Named`, `On`) and `QuerySettings.Parameters` change to hold a word for each parameter; how is the builder's choice, under these rules.
- **A word** is `name`, `name=value` or `name=default`, and `default` alone, as today.  `name` alone is `name=on`, and is SQLSRC111 for a parameter whose list has no `on`.  Names and values are matched ignoring case.  A value that is not in the parameter's list is SQLSRC111 in a `-- generator:` line and SQLSRC019 in a settings file, as a state that is not `on`, `off` or `default` is today.
- **SQLSRC111's message names the parameter's words**: "'transaction=snapshoot' ... ; its values are on, off, read-uncommitted, ...".  `docs/manual/diagnostics.md` says so.
- **A contradiction** is one parameter given two different values in one place, SQLSRC112 and SQLSRC020 as today.  `name=default` beside any other value of it is one, as today.
- **Merging** is unchanged: each place names only the parameters it changes, the nearer over the farther, and `default` names every parameter with its default.
- **The settings schema** (`schemas/settings-v1.schema.json`) gives each parameter its own pattern of values, and the test that compares the schema with `GeneratorParameterList.Names` compares the values too.
- **The manual** (`docs/manual/settings.md`, "Generator parameters") and the glossary of `docs/manual/concepts.md` say that a parameter takes a value from its list, of which `on` and `off` are the common case.  Part 1 adds no parameter with other values, so these pages may wait for Part 2; whichever PR changes the behaviour changes them.

### Part 2: collection methods

**Turning it on.**  `collection-method`, a generator parameter with `on` and `off`, default `off`.  For a query whose output is `methods` or `repository`, which gets a query method, it adds a collection method.  Under the outputs `sql` and `models` it does nothing, as `array-pool` does nothing there.  A query with no parameters, which has no input model, gets no collection method and no error, wherever `collection-method` was set: a folder that turns it on must not break its `SELECT`s without parameters, and the merged settings do not keep where a parameter was set; my call.

**The signature.**  The collection method is an overload of the query method, with the same name, `Async` included under `async-method-suffix`.  Its arguments are the query method's, in the same order, with `IEnumerable<TParams> parameters` in place of `TParams parameters`:

```csharp
// repository
public async Task<AddUserDto[]> AddUser(
    IEnumerable<AddUserParams> parameters,
    CancellationToken cancellationToken = default)

// methods
internal static async Task<AddUserDto[]> AddUser(
    this DbConnection connection,
    IEnumerable<AddUserParams> parameters,
    DbTransaction? transaction = null,
    int? commandTimeout = null,
    CancellationToken cancellationToken = default)
```

A token is one `string` argument, as in the query method, and its text is used for every item.  The repository's interface gains the overload beside the query method.  The argument keeps the name `parameters`, so no new name is reserved from tokens (SQLSRC275); my call.

**What it returns**, by the query's shape:

| Shape | Returns |
|---|---|
| `many` | Every row of every item, in the order of the items, in the query's collection type.  A query whose collection type is `IAsyncEnumerable` gets an array, since a collection method does not stream; my call |
| `one` | One row for each item, in the order of the items, in the collection type.  An item that returns no rows or more than one throws `UnexpectedRowCountException`, whose message also gives the item's position, counted from 0; my call |
| `one-optional` | The rows of the items that returned one, in order, so the collection may be shorter than the items.  An item that returns more than one throws `UnexpectedRowCountException` |
| `none` | `Task` |
| `rowcount` | `Task<int>`, the sum of every item's `RecordsAffected` |

Under `array-pool` the rows are read through the row buffer, as a `many` method's are.

**Arguments that are wrong.**  A null `parameters` throws `ArgumentNullException` before anything runs.  The collection is enumerated once, and is not buffered whole.  An empty collection returns an empty collection, `0` or a completed task, and opens no connection and begins no transaction.  Each item is checked as it is added to its chunk, as the query method checks its `parameters`: a null item throws `ArgumentNullException`, with the item's position in the message, and list counts and limits throw the query method's exceptions.  So a bad item is found before its chunk runs, but after earlier chunks have; under the default transaction they are rolled back, and under `transaction=off` or the caller's transaction they stay, which the manual says; my call, over buffering every item first.

**Running it.**

- The method walks the items once and fills a `DbBatch` of the connection with a `DbBatchCommand` for each item, built as the query method builds its command: its SQL, with its tokens and with each list parameter written and padded for that item, and its parameters bound by the engine's binder into the batch command's `Parameters`.
- **Chunks of 1,000 items.**  When a batch holds 1,000 commands, or the items end, it is run, its result sets read in order, one for each command, and the next batch begun.  The size is a constant; my call, from the spike, where chunks of 1,000 ran as fast as one batch and bound the memory a batch of tens of thousands would hold.
- `commandTimeout` is each batch's `Timeout`.  `cancellationToken` is passed to every call.
- **PostgreSQL's SQL.**  A batch command's text must not end in a line comment after its final `;`, which Npgsql refuses.  The collection method writes the SQL without what follows the last `;` when that is only comments and white space; how is the builder's choice.
- **The connection** is opened and closed as the query method opens and closes it: under `methods`, opened when it is closed and closed again before the method returns or throws; under `repository`, from the repository's connection source.

**Transactions.**  `transaction`, a generator parameter with the values `on` (the default), `off`, `read-uncommitted`, `read-committed`, `repeatable-read`, `serializable` and `snapshot`.

| Value | The collection method |
|---|---|
| `on` | Begins a transaction with `BeginTransactionAsync()`, the database's default isolation level |
| An isolation level | Begins one at that `IsolationLevel` |
| `off` | Begins none: each chunk runs as the database runs a batch outside a transaction |

- **A transaction the caller gives** (the output `methods`) is used for every batch, whatever `transaction` says, and the method neither begins, commits nor rolls back a transaction.  The SQL Server refusal of a completed transaction (`RefusesCompletedTransaction`) applies to it, as to the query method.
- **An ambient transaction**, `System.Transactions.Transaction.Current` not null when the method starts, is the caller's too: the method begins none, whatever `transaction` says, and the connection enlists in it as the driver enlists it.  It is the only way to put a repository's collection method in a larger transaction, and the anti-goal forbids beginning a transaction inside the caller's; my call.  The tests cover a `TransactionScope` on both databases.
- **Its own transaction** is committed after the last chunk's rows are read, and rolled back when anything throws: a database error, an exception of the method's own such as `UnexpectedRowCountException`, or cancellation.  The rollback is skipped when the server has already rolled the transaction back, which leaves its `Connection` null on SQL Server, so that the first exception is the one thrown.
- **With the caller's transaction**, the query method's rule holds on SQL Server: an exception of the method's own, or a database error, after which a *later* error of the batch rolled the transaction back is thrown as `TransactionRolledBackException`, with the first exception inside it.  A database error that rolled the transaction back itself, such as a failed item under `XACT_ABORT ON` or an error that ends the batch, is thrown as its own `SqlException`, as the query method throws it.
- **An isolation level the query's database does not have** is a new error: PostgreSQL has no `read-uncommitted` and no `snapshot`; SQL Server has all five.  It is reported where the method is planned, since the place that names it does not know the database.
- `transaction` changes nothing for a query without `collection-method`.
- **With `transaction=off`**, PostgreSQL runs each chunk in an implicit transaction, so a chunk commits or rolls back whole, and earlier chunks stay committed.  SQL Server commits each item on its own, and items after a failed one still run.  The manual says both plainly.

**A database error at an item** is thrown as a new `CollectionItemException`, the owner's choice over attaching the item to the driver's exception's `Data`, which nobody reads.

- It derives from `System.Data.Common.DbException`, the base of `SqlException` and `PostgresException`, which are both sealed (Microsoft.Data.SqlClient 6.1.1 and 7.1.1, Npgsql 8.0.8 and 10.0.3), so `catch (DbException)` still catches it.  One type for both engines, since the exceptions are generated into projects that reference neither driver.
- `QueryName`, the query; `ItemIndex`, the item's position counted from 0, from the driver's exception's `BatchCommand` and the chunk's offset; `InnerException`, the driver's exception, unchanged.  Its message names the query and the position, then gives the database's message.
- It passes on what `DbException` lets it override: `SqlState`, `IsTransient`, `DbBatchCommand` (so `BatchCommand`) and the `ErrorCode` it is constructed with, so that retry policies built on `IsTransient` work unchanged.
- It is generated as `UnexpectedRowCountException` and `TransactionRolledBackException` are: in the namespace `SqlSource`, `internal` unless `SqlSourcePublicExceptions` is set, and taken from a referenced project that has it (`docs/manual/generator.md`, "Projects that share internals").
- Only a database error is wrapped.  `UnexpectedRowCountException`, a converter's exception or a null in a column that cannot be null are thrown as the query method throws them, with the item's position in `UnexpectedRowCountException`'s message.
- Inside `TransactionRolledBackException`, under the rule above, the first exception is the `CollectionItemException`.
- The manual shows the catch that replaces a driver's: `catch (CollectionItemException e) when e.InnerException is SqlException { Number: 2627 }`.

**SQL Server queries with several result sets.**  The generator cannot tell them at build time, so the collection method checks while it reads: a result set whose `FieldCount` is not the output model's column count, or more result sets than items, throws `InvalidOperationException` with a message that names the query and says it cannot run over a collection.  The transaction it began is rolled back.  A query whose shape is `none` or `rowcount` reads no rows and runs with `ExecuteNonQueryAsync`, so its result sets do not matter, and a statement without a result set, such as `SET NOCOUNT ON` or an `UPDATE` without `OUTPUT`, never misaligns.  The manual's SQL Server page says which queries can run over a collection.

**The generator's structure.**  The collection method is a new kind of method (`docs/architecture/generator.md`, "Adding a kind of method"): a value on `QueryMethod`, as the plan's rules for a new axis say, read by the phase files.  Building and binding a command for one item is shared with the query method; running batches in chunks, reading result sets in order and handling the transaction are its own.  The SQL Server execution capabilities are decided for it by `ExecutionCapabilityRules.Of`: `DrainsLaterResults` does not apply, since every result set belongs to an item, and the rollback checks become the checks above.

### Diagnostics

New, with its section in `docs/manual/diagnostics.md`, its row in `AnalyzerReleases.Unshipped.md`, and the next free number in its range:

- An isolation level that the query's database does not have, on a query that gets a collection method.  The query loses only its collection method, and keeps its query method; my call.

### Documentation and examples

- `docs/manual/generator.md`: a section on collection methods, its signature, returns, transactions, chunks and exceptions, and a section on `CollectionItemException` beside the other exceptions; the repository's "Transactions and timeouts" section says that a collection method begins its own transaction.
- `docs/manual/settings.md`: the two parameters, and generator parameters with values.
- `docs/manual/sqlserver.md` and `docs/manual/postgresql.md`: what a failure does with and without a transaction, and, for SQL Server, which queries can run over a collection.
- `docs/manual/recipes.md`: inserting many rows.
- `docs/manual/README.md`: a row in "What works today".
- `README.md`: the FAQ on transactions, if it names them.
- `docs/architecture/generator.md`: the collection method's part of query methods, and Part 1's generator parameters.
- `examples/PostgresExamples` and `examples/SqlServerExamples`: an insert of many rows that returns their ids, with sidecars written by `tools/describe-end-to-end.sh`.

### Tests

- Part 1: reading words with values in a `-- generator:` line and a settings file, contradictions, `default`, the schema's patterns.
- Part 2, generator: the overload's signature at both outputs and in the repository's interface, each shape's return type, the new diagnostic, and `CollectionItemException` generated, internal and public, and taken from a referenced project.
- Part 2, on both databases, at the default and floor images: each shape over 0, 1, a few and more than 1,000 items; a failure in the middle of a chunk and in a later chunk, with each `transaction` value and with the caller's transaction, checking what is left in the table; `XACT_ABORT ON` and an error that ends a batch on SQL Server; a SQL Server query of two result sets throwing; cancellation rolling back.

## Left to the builder

- How the parameters' values are stored in Part 1, under the rules above.
- The exact messages of the new diagnostic and exceptions.
- How the phase files share building a command for one item between the query method and the collection method.
- How the trailing comment is cut from PostgreSQL's SQL.
- Whether fewer SQL Server result sets than items is also rejected, as more are, and what a `rowcount` total does with a command whose `RecordsAffected` is `-1` under `SET NOCOUNT ON`.
- How the planner's check of a taken name lets the collection method share its query method's name.

## Calls made in this design that the owner did not make

Marked "my call" above: the argument's name `parameters`; an array for a collection type of `IAsyncEnumerable`; the item's position in exception messages; a fixed chunk size of 1,000; no error for a query without parameters, which replaces the error proposed in conversation; items checked chunk by chunk, not all before the first runs; an ambient transaction counted as the caller's; an unsupported isolation level costing only the collection method; and the runtime check for SQL Server queries of several result sets, which replaces the build error proposed in conversation, since the generator cannot count a T-SQL query's result sets.
