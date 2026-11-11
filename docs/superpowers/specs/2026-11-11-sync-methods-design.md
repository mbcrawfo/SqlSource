# Synchronous methods design

## Intent

Every query method SqlSource writes today is asynchronous: it returns a `Task`, takes a `CancellationToken` and awaits the driver.  Code that cannot or does not want to await, such as a synchronous code path, a console tool or a library with a synchronous API, has to block on the task or write the ADO.NET by hand.  This change lets a query get a synchronous method instead of its asynchronous one, or beside it.

The owner's priority is that a synchronous method does what its asynchronous twin does, to the database and to the caller: the same rows, the same counts, the same exceptions and the same transactions.  Sync methods are tested like any other code, in every pull request, and a nightly suite runs the two side by side to catch drift between them.

## Goals

- A new generator parameter, `method-type`, with the values `async` (the default), `sync` and `both`, set wherever generator parameters are set: a settings file, a file header or a query.
- Under `sync` a query gets a synchronous method in place of its asynchronous one; under `both` it gets both.
- It applies to every kind of query method: methods that take a connection, collection methods and repository methods, with the repository's interface.
- A repository whose source returns a `Task` can still have synchronous methods, through a second source for the same database that does not.
- Every pull request tests the synchronous methods on both engines; a nightly suite compares each synchronous method with its asynchronous twin and goes red on any difference.

## Non-goals

- **A synchronous stream.**  A query whose collection type is `IAsyncEnumerable` gets no synchronous method.  `IEnumerable<T>` already means a collection read whole, so a lazy one would have two meanings (the owner's call).
- **Blocking on a task.**  No generated method calls `.Result`, `.Wait()` or `GetAwaiter().GetResult()`.  A synchronous method never calls a `Task`-returning connection source.
- **Cancellation in a synchronous method.**  It takes no `CancellationToken`, as ADO.NET's synchronous calls take none.  `commandTimeout` still applies.
- **Proving equivalence from the generated text.**  The owner rejected comparing the sync text with the async text: the drivers run different code for each, so only behaviour counts.

## Anti-goals

- A synchronous method that differs from its asynchronous twin in what it leaves in the database or in what a caller's `catch` sees, under the rule of "When a driver differs" below.
- Sync-over-async anywhere in generated code.
- A drift between sync and async that blocks pull requests on a flaky signal: the nightly suite warns, and the per-PR tests gate.

## Constraints

- The root `AGENTS.md` rules apply: the manual, the diagnostics page, the settings schema, the architecture pages, the examples, `AnalyzerReleases.Unshipped.md` and the test readmes change in the same PR as the behaviour they describe.  A feature still to come in a later part is a "Coming in a later release" callout.
- The generator's rules in `src/SqlSource/AGENTS.md` and `src/SqlSource/Generation/Emitting/QueryMethods/AGENTS.md`: a new axis is a value on the plan, read from `QueryMethodContext`; asynchronous spellings live only in `AsyncSpelling`, which the first synchronous kind of method makes a strategy (`docs/architecture/generator.md`, "Adding a kind of method").
- The driver floors are unchanged: .NET 8, Npgsql 8, Microsoft.Data.SqlClient 6.1.  Every call a synchronous method needs exists on them: `Open`, `ExecuteReader`, `ExecuteNonQuery`, `ExecuteScalar`, `Read`, `NextResult`, `BeginTransaction`, `Commit`, `Rollback`, `DbDataSource.OpenConnection`, `DbBatch.ExecuteReader` and `ExecuteNonQuery`.
- An Npgsql data source with `Multiplexing=true` refuses synchronous I/O with `NotSupportedException`.  That is the driver's limit, a note in the manual's PostgreSQL page, not tech debt.

## Approaches

**How the code is written.**  There is one sensible way, which the architecture page already prescribes.  `QueryMethod` gains a small equatable value saying whether the method is synchronous or asynchronous.  `AsyncSpelling` becomes a strategy with two implementations, chosen in the output step by one exhaustive switch over that value, as `ParameterBinder.For` chooses a binder.  The strategy spells `await`, `await using` with `ConfigureAwait(false)`, `async Task<T>` and the driver's calls (`ReadAsync` or `Read`, `OpenAsync` or `Open`, and the other thirteen `*Async` names the phase files pass today).  Under `both` the planner adds a second `QueryMethod`, as it adds a collection method.  Separate synchronous writers were rejected because they copy every phase file, which drifts by construction.  Rewriting the asynchronous output as text was rejected as fragile and as the text-based approach the owner ruled out.

**How it is tested.**  Three ways were considered:

1. Turn every end-to-end test into a theory over sync and async.  Full coverage, but a rewrite of about 1,900 `await`s across four test projects, and it makes the nightly suite redundant.
2. **Chosen.**  Sync tests of their own in the end-to-end projects, written as the existing tests are, covering what a synchronous method changes; and a new nightly project that runs each pair with the same inputs and compares what they do, with no expected values.
3. One list of scenarios with expected outcomes, run against sync per PR and as pairs nightly.  Keeps the two in step, but is a new style of test, and expected exceptions and table contents are awkward as data.

The owner chose 2.

## Design

### The parameter

- `method-type`, a generator parameter with the values `async`, `sync` and `both`, default `async`, defined in `GeneratorParameterList.Definitions` as `transaction` is.  `method-type` alone (which reads as `on`) is SQLSRC111, since its list has no `on`.
- It applies to queries whose output is `methods` or `repository`.  Under `sql` and `models` it does nothing, as `array-pool` does nothing there.
- The settings schema gains its pattern, and the manual's settings page its row.

### Names

- A synchronous method has the query's name, never with `Async` after it.  Under `sync`, `async-method-suffix` has no effect, and no diagnostic says so, since levels set the two independently (my call).
- Under `both`, the asynchronous method always ends in `Async`, whatever `async-method-suffix` says, so that the two never share a name: they would differ only by an argument with a default, which makes every call ambiguous (CS0121).  This is the owner's call.
- This holds for an asynchronous method with no synchronous twin under `both` too, such as a stream's, so that a query's names do not depend on its shape (my call).
- So under `both` a query `GetUser` gets `GetUser` and `GetUserAsync`, and its collection method the overloads `GetUser(IEnumerable<…>)` and `GetUserAsync(IEnumerable<…>, …)`.
- Each name is checked as today's names are: a clash with another query's method or a member (SQLSRC274), and a nested class's name (SQLSRC009) against both names.

### The synchronous method

- **Its arguments** are the asynchronous method's without `cancellationToken`: `methods` takes the connection, the input model, the tokens, `transaction` and `commandTimeout`; a repository's takes the input model and the tokens.  `MethodArguments.Of` derives this from the new value.  `MethodArguments.IsReserved`, and so SQLSRC275, does not change: `cancellationToken` stays reserved, since the same token may be on an asynchronous method elsewhere, and so does SQLSRC036 for a repository's primary-constructor parameter of that name, whatever its methods' `method-type` (my call).
- **It returns** what the asynchronous method's task holds: `T`, `T?`, the collection, `int`, or `void` for `none`.
- **Its body** is the asynchronous method's structure with the synchronous spellings: the same checks, the same opening and closing of a closed connection, the same command, bindings, lists, row reading, row buffer, conversions, drains of later results, rollback checks and exceptions.  `using` replaces `await using`.
- **Its documentation** is the asynchronous method's, without `cancellationToken` and without `OperationCanceledException`.

### Streaming

- Under `both`, a query whose collection type is `IAsyncEnumerable` gets its asynchronous streaming method only.  Its collection method, when `collection-method` is on, returns an array and does not stream, so it gets both.  The manual says that a stream has no synchronous method.
- Under `sync`, such a query is a new error at the query, the next free number of the methods' range (SQLSRC298): a stream needs `async` or `both`.  The query gets no method (my call: its collection method goes too, so that the error is not half-applied).

### Collection methods

- They follow `method-type` as the query method does.  The synchronous one fills `DbBatch`es in chunks of 1,000 and runs them with `ExecuteReader` or `ExecuteNonQuery`; it begins its transaction with `BeginTransaction`, at the isolation level `transaction` gives, and commits or rolls back with `Commit` and `Rollback`.
- Everything else is the asynchronous collection method's: an empty collection opens nothing, items are checked as their chunk is filled, a caller's or an ambient transaction is used and never ended, `CollectionItemException` carries the item's position, the PostgreSQL batch command's trailing comment is cut, and the SQL Server checks of several result sets apply.

### Repositories

- A repository's synchronous methods, and their members of the interface, follow `method-type` as the extension methods do.  Under `both` the interface has both members.
- **Which source a method can use.**  A connection string, a data source, a borrowed connection and a factory method that returns a `DbConnection` (not a task) serve both kinds of method.  A factory that returns `Task<…>` or `ValueTask<…>` serves asynchronous methods only.
- The synchronous method gets its connection with `new …Connection(string)` and `Open`, `DbDataSource.OpenConnection()`, the factory's call and `Open` when closed, or the borrowed connection and `Open` when closed.  A factory that takes a `CancellationToken` is passed `default` by a synchronous method (my call).
- **Two sources for one database.**  A database may have two marked sources when exactly one of them returns a task.  Asynchronous methods use the one that returns a task, and synchronous methods the other.  Two sources that both return a task, or both do not, are SQLSRC029 as today.  Since a factory is found only when marked, such a pair is always two marked members.  A pair whose non-task source no synchronous method uses is not an error, since `method-type` can differ by query and the type cannot tell it is unused without the queries (my call).
- **A synchronous method whose database has only a task-returning source** is a new error at the query (SQLSRC299, or the next free number), naming the source and saying to add one that does not return a task.  The query keeps its asynchronous method under `both` and loses only the synchronous one; under `sync` it gets none (my call).

### When a driver differs

A driver's synchronous call may behave otherwise than its asynchronous one with no code of SqlSource's involved.  None is known today; the owner's rule for one found later:

- **The method compensates** when the difference would change what a caller's `catch` sees, the exception's type, its inner exception, its error code or SQL state, or `ItemIndex`, or what is left in the database.  The precedent is `FindsFailedItemByCompletion`, which finds a collection method's failed item when Npgsql 10 names no batch command.  Each such fix is raised with the owner when it is found, not assumed.
- **Anything else is a driver limit**, such as a message's wording or which call an error after the rows is thrown from: a note in the manual's page of the engine, the synchronous test asserting what the driver does, and an entry in the nightly suite's list of known differences, which says why.  It is not tech debt.

### The generator's structure

- The value on `QueryMethod` is read by the phase files from `QueryMethodContext`, as the rules require.  `MethodArguments.Of`, `ExecutionCapabilityRules.Of` and `RowsWriter.GetResultType` derive what differs from it.
- `AsyncSpelling` becomes the strategy.  How it is shaped (an abstract class with two singletons, a struct, a pair of static classes) is the builder's choice, under the rule that no phase file spells an asynchronous or synchronous call itself.
- `CommandWriter.AppendAcquire`'s switch over `ConnectionSourceKind` gains the synchronous forms, and the resolver of a repository's source picks a source for each method from its kind.
- `docs/architecture/generator.md` replaces the row "The asynchronous spellings, or synchronous ones" of its table with the strategy as built, describes the value on `QueryMethod`, and the repository's source selection.

### Diagnostics

New, each with its section in `docs/manual/diagnostics.md` and its row in `AnalyzerReleases.Unshipped.md`:

- A stream under `method-type=sync` (methods range).
- A synchronous repository method whose database has only a task-returning source (methods range).

Changed: SQLSRC029's explanation, which allows one pair of a task-returning and a non-task source.

## Testing

### In every pull request

- **The golden corpus** gains a dimension: `method-type=sync` and `both` for each shape, a list, a token, a conversion, a collection method and a repository with a pair of sources, on both engines.
- **Generator tests**: the parameter's words, the names under `both` and their clashes, the arguments and documentation of a synchronous method, the two new diagnostics, SQLSRC029 with a pair, the interface's members, and the caching tests of any step whose inputs change.
- **End-to-end tests on both engines**, at the default and floor images, as the existing tests run.  The projects gain query copies under `-- generator: method-type=sync` (or `both`), claimed by types of their own, and test classes written as today's are, covering what a synchronous method changes:
  - each shape, with the rows it allows and the counts it refuses, on an open and a closed connection, which a synchronous method opens and closes again after a return and after an exception;
  - a caller's transaction and its rollback, `commandTimeout` expiring, and SQL Server's rollback checks (`TransactionRolledBackException`, the refusal of a completed transaction, the error after the rows);
  - lists, tokens and conversions, once each;
  - collection methods: each shape over one item, a few and more than 1,000; a failure in the second chunk under each `transaction` value and the caller's transaction, with what is left in the table; `CollectionItemException`'s position; an ambient `TransactionScope`;
  - repositories: each source kind, a pair of sources where each kind of method uses its own, and a factory that returns a task beside a synchronous factory.
- **The driver floor projects**, `Postgres.Npgsql8` and `SqlServer.SqlClient61`, gain synchronous copies of the tests whose driver behaviour differs by version: the collection method's failed item, and the row reading of each shape (my call on the selection).

### Nightly: allocation

- `tests/DatabaseTests/QueryMethodAllocation` gains a synchronous pair for each query it measures that is not a stream: the generated synchronous method against a hand-written synchronous ADO.NET method of the same calls, under the same margins and the same rules for moving them (the owner's call).  It comes with Part 1.

### Nightly: sync and async side by side

- A new project under `tests/DatabaseTests/`, built with the solution and run only by its own jobs in `.github/workflows/nightly.yml`, one per engine on its default image, as `QueryMethodAllocation` is.  A red job is a signal, not a gate; its README says how to read and reproduce a failure.
- It links the end-to-end projects' queries and sidecars under a settings file that sets `method-type=both`, with types of its own that claim them, so that every query that can have a pair has one.
- **Each scenario** names a query, its inputs and its setup: a closed or open connection, the caller's transaction or none, the `transaction` value of a collection method.  It runs the asynchronous method, records the outcome, puts the database back, runs the synchronous method, records its outcome, and compares the two.  It holds no expected values.  A difference in its declared list of known differences, each a driver limit with its reason, is reported and does not fail.
- **An outcome** is the result, compared structurally (rows in order, counts); the exception, if any, by its type, its inner exception's type, the driver's error code or SQL state, `ItemIndex` and its message; the state of the connection and of the caller's transaction afterwards; and the rows of the tables the query touches.
- **Coverage**: every query of the end-to-end projects with a pair, with the inputs that give each shape's ordinary result, its refused row counts, a database error, and for collection methods an empty collection, a chunk boundary and a failure in a later chunk.
- How a scenario is declared, how the database is put back between the two runs, and how messages that hold changing text are compared are the builder's choice.

## Phases

The work is four pull requests, each one session, in this order.  Parts 2 and 3 do not depend on each other, but both change `MethodPlanner`, `MethodEmitter` and `docs/manual/generator.md`, so they run one after the other.  The implementation-manager skill can run them through child sessions.

1. **Synchronous query methods.**  The parameter, the strategy, the value on `QueryMethod`, the names, the streaming error, the golden corpus dimension, synchronous methods at the output `methods`, their end-to-end tests on both engines and the floor driver projects, the synchronous pairs of the allocation tests, the manual, the architecture page and an example in each example project.  Collection methods and repository methods stay asynchronous under `sync` and `both` in this part, which the manual says in a "Coming in a later release" callout on each.  The streaming error applies to every query from this part on, a repository's included (my call).
2. **Synchronous collection methods.**  Their emission, tests and docs; the callout goes.
3. **Synchronous repository methods.**  The synchronous connection forms, the pair of sources, SQLSRC029's change, the new diagnostic, the interface, tests and docs; the callout goes.
4. **The nightly suite.**  The project, its jobs, its README and `tests/DatabaseTests/README.md`, `.github/workflows/README.md` and `CONTRIBUTING.md` where they list projects.

## Left to the builder

- The shape of the spelling strategy and the name of the value on `QueryMethod`.
- The exact messages of the new diagnostics.
- The names of the new query files, types and test classes, and of the nightly project.
- How the nightly suite declares its scenarios, resets the database and compares messages.

## Calls made in this design that the owner did not make

Marked "my call" above: no diagnostic for `async-method-suffix` under `sync`; `cancellationToken` stays reserved; a stream under `sync` loses its collection method too; `default` passed to a factory's `CancellationToken`; an unused non-task source in a pair is not an error; a synchronous method with only a task source keeps the asynchronous method under `both`; the selection of floor-driver tests; and the split into four parts with the interim callouts.
