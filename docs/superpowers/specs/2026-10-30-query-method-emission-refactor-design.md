# Query-method emission refactor: design

Date: 2026-10-30, rechecked on 2026-11-01 against `main` at `1911dbb0` after phase 7 closed, and on 2026-11-02 at `43da4753` after the Roslyn 4.14 floor merged ([#205](https://github.com/mbcrawfo/SqlSource/pull/205)).  Status: approved.

## When it starts

After the Roslyn 4.14 floor branch (`claude/roslyn-4-14-td-0006-7aa6a0`) merges into `main`, which it did on 2026-11-02.  At `43da4753`, `QueryMethodWriter` is 940 lines, and every other finding below still holds.
- **That branch changes the same code.**  Its task 5 writes one embedded row buffer per project, which removes the `rowBufferType` parameter from `QueryMethodWriter`, changes what `MethodEmitter.Emit` returns, removes `RowBuffer.NameFor`, and rewrites `Emitting/AGENTS.md`'s row-buffer rule and `generator.md`'s "The row buffer".  It has already changed the exceptions' crefs in `QueryMethodWriter` to documentation IDs.
- **It also changes generated output.**  The golden corpus of PR 1 would have to be recorded again if it landed first.
- **This design assumes the branch's end state:** the row buffer's type is the constant `global::SqlSource.SqlSourceRowBuffer`, and new code may target `ImmutableArray<T>` with a collection expression.

Phase 7 has closed, so nothing else is in flight on these files.

## Intent

The code that plans and writes a query's method works today, but it was built for one kind of method: an asynchronous static method on a `DbConnection`, for PostgreSQL or SQL Server.  Several features on the list would each cut across all of it:

1. Synchronous methods, instead of or beside the asynchronous ones, so one query can give two methods.
2. Repository-style methods: instance members of the containing type, whose connection comes from the instance.
3. Methods that run on something other than a `DbConnection`, such as a user's unit of work that supplies the connection and the transaction.
4. More database engines: MySQL and SQLite are on the short list, with no date.

None of these is designed here and none is scheduled.  The aim is to reshape the code *before* the first of them arrives, so that each lands as an addition in a known place rather than as another `if` in a dozen helpers.  The four features weigh equally.

The refactor also leaves a written guide, in `docs/architecture/`, to the patterns a contributor follows when adding an engine or a kind of method, so that the shape survives the people who did not take part in this decision.

## Goals

- **One home for each fact that a feature changes:** the method's argument list, where the method goes and how visible it is, each engine's execution behaviour, and what identifies a method.
- **A closed set of engines across the whole generator.**  Adding a member to `DatabaseEngine` fails the build at every place that has to decide something for it.  No branch may let a new engine silently behave as PostgreSQL.
- **A query's method can be read in sequence.**  The writer is split by phase into a few files, each readable top to bottom, not into many small strategies that a reader has to assemble in their head.
- **Proof that nothing changed.**  Every step leaves every generated file byte-identical, and a golden-output test proves it across the engines and the method kinds.
- **Documentation:** an architecture section that explains the new shape, a guide to extending it, and pointers to both from the AGENTS.md files and the code.

## Non-goals

- Designing or building any of the four features.
- Changing the generated code, a diagnostic, a setting, the attribute or anything else a user can see.
- Changing the describers, the sidecar or the tool.

## Anti-goals

The refactor fails, even if it works, if it:

- **Adds an abstraction that has one implementation and is shaped by a guess.**  A sync/async object, a receiver abstraction or a per-engine execution strategy built now would be designed against one case.
- **Gives a hook for each position of SQL Server code.**  SQL Server's transaction and drain code sits at about five places in a buffered method and seven in a streamed one, so the hooks would be named by position, would do nothing for PostgreSQL, and would still not cover the streamed loop.
- **Spreads one method's code across so many files that it cannot be followed.**
- **Allocates more than today, or makes a string per line.**
- **Uses golden files that get regenerated without anyone reading them.**

## Constraints

- The generator targets `netstandard2.0`, runs in the compiler and the IDE on every edit, and has allocation budgets.  A change to a hot path states its time and allocation before and after in its pull request (`src/SqlSource/AGENTS.md`).
- What flows between pipeline steps is a record with value equality.  Strategy objects never enter the pipeline.  They are chosen in the output step, as `ParameterBinder.For` is.
- Generated code stays at C# 12 at most.  Every local takes its name from `LocalNames`, so a token cannot hide it, and framework types are written from `global::`.
- `TreatWarningsAsErrors` is on, with `AnalysisLevel` `latest-all`.
- Each PR follows the root `AGENTS.md`: `./pre-commit-validation.sh --full` before a push, `branch-review:review` until it comes back clean, the version check, and the docs that a change makes wrong, updated in the same PR.

## What the code is today

These findings shape the design.  Each was checked against the code at `6f3002c9`, and the counts were updated at `1911dbb0`, after phase 7.  The plan of each PR checks them again.

Phase 7 left the query-method writer itself untouched: it is still 946 lines, and `Emitting/` still has 19 source files.  What it added sits in conversions, row reading and binding:
- **`Conversion` gained three kind flags and a library:** `IsSpatial`, `IsSerializer`, `MayReturnNull` and `Library`.  `RowReaderWriter` and both binders branch on them.  The spatial and serializer flags are mutually exclusive kinds; `IsByElement` combines with the serializer.
- **`MethodDrivers` grew from two bools to four:** `Postgres`, `SqlServer`, `PostgresAtLeast10` and `SqlServerSpatial`.  The last two are capabilities of a driver or a library, not engines.
- **SQL Server's spatial read goes through `GetStream`, which every `DbDataReader` has.**  It needs no cast and no local of its own, so reading still differs by conversion kind, not by engine.

- **[`QueryMethodWriter`](../../../src/SqlSource/Generation/Emitting/QueryMethodWriter.cs) is 946 lines** in one static class, passing up to eight parameters through about 25 helpers.
- **The fixed argument list is written in six places:**
  - In the writer: `AppendArguments`, `AppendCallArguments`, `AppendDocumentation` and `AppendChecks`.
  - `QueryMethodLocals.Seed`.
  - `MethodPlanner.FixedArguments`, a separate literal array that SQLSRC275 reads.
- **Async-ness is written by hand at about ten sites:** open, close, both executes, read, both next-result loops, dispose, `await using` and the signature.
- **`MethodPlacement` mixes two things:** where a method goes (extension class or the type) and how visible it is.  The writer takes a raw `"public static"` string and an `isExtension` bool.
- **A method's identity is its query's `NameLocation`.**  `MethodEmitter.Keep`, `DroppedMethod` and `MethodConflicts.DroppedOf` drop methods by it.  Two methods from one query share that place, so dropping one after a clash would drop both.
- **The generator has two engines, "SQL Server, or else PostgreSQL".**  The doc comment on `DatabaseEngine.PostgreSql` reads "PostgreSQL, and every dialect that is not SQL Server's".  After phase 7, 28 branches in 13 files under `src/SqlSource` are written as `engine == SqlServer ? … : <PostgreSQL>` or its negation, among them:
  - `LibraryProbe`, with 7;
  - `ParameterBinder.For` and `MethodDrivers.Allows`;
  - `DatabaseEngineExtensions`, `ConversionPlanner`, `ConverterTargets` and `PropertyNames` (the default schema);
  - `QueryTyper`, and the settings and parsing code.

  Five of them are in the writer's execution flow.
- **The tests cannot prove byte identity today.**
  - `QueryMethodWriterTests` has 50 tests, but only two compare a whole method, and both are PostgreSQL.  The rest look for fragments.
  - The generator-level tests compare three whole files, none with SQL Server code, and only check that SQL Server methods compile.
  - The riskiest part of any refactor, SQL Server's try/catch/drain and row tracking, has no output check.
- **`Emitting/AGENTS.md` is already stale:** it says `LocalNames` is in `QueryMethodLocals.cs`, but it is in `LocalNames.cs`.

## Approaches considered

Three designs were written and then reviewed adversarially.  The designs and the review are in the session's scratchpad and are summarised here.

**A. Strategy-composed emitters.**  Each part of the method has its own emitter (signature, connection, command, binding, execution, row reading, error handling, documentation), specialised by engine and settings, and a factory assembles a method's emitter from the configuration.
- **Holds:** it would be the best prepared for all three method features.
- **Fails on SQL Server:** its transaction and drain code is not wrapped around one part.  It is woven through three: before the SQL, around the row read, inside the streamed loop, after it, and in a `finally`.  That needs about ten paired hook points, all but the binder doing nothing for PostgreSQL, and the streamed loop still branches on the engine.
- **Cost:** about seven PRs, roughly +1,400 / −1,000 lines, and a SQL Server streamed method spread over five files.
- **Unsafe receivers:** it would re-read the receiver as an expression at each use.  A unit of work that clears its transaction on rollback would make the rollback check wrong.

**B. A light method plan with an execution "flavour".**  The cached plan stores only decisions: async or sync, where the method goes, what it runs on.  The argument list, engine behaviour and reserved names are derived where they are used.  The async spellings sit behind a swappable object shaped like `ParameterBinder`.
- **Holds:** it is the strongest on the planning side: identity by name, a clash key over required arguments, and receivers copied into locals once.
- **Speculative:** its flavour object is designed against one implementation.

**C. Targeted seams.**  Seven byte-identical extractions remove today's duplication, and the sync/async object, the receiver abstraction and one-query-many-methods are deferred to the features, each with a trigger.
- **Holds:** it is the cheapest and the least speculative.
- **Weaknesses:**
  - It postpones the identity fix, which is a latent bug.
  - It keeps receivers as expressions.
  - It leaves the cross product of all the axes for the sync feature to untangle.

**Chosen: a hybrid.**  It follows C's posture: do now what removes a hazard or duplication today, and let each feature bring its own abstraction.  It takes B's planner fixes: identity by type and name, one argument model, receivers as locals.  Adding engines then changes two things:
- The closed engine set becomes the first refactoring step, across the whole generator.
- Engine behaviour is named for the capability, not the engine: MySQL and SQLite probably share SQL Server's need to drain later results.

### Where the strategy pattern applies

The pattern is right where a behaviour varies wholesale, and wrong where a fixed skeleton varies by toggles or by data.  The architecture guide carries this rule.

| What varies | Pattern | Why |
|---|---|---|
| Binding a parameter, by engine | Strategy (exists: `ParameterBinder`) | Each engine writes different statements. |
| Reading a row, by engine | Strategy, when the first engine needs different reading statements | SQLite's loose typing will probably need its own reading rules.  Today only the SqlVector check differs by engine; phase 7's spatial and serializer reads differ by conversion kind, which one `ConversionKind` switch covers. |
| Async or sync spellings | Strategy, introduced with the sync feature | Two real implementations are needed to design it against.  Until then the spellings are only centralised. |
| Who owns the connection (borrowed or owned) | Strategy, introduced with the first receiver feature | It changes control flow, not just names. |
| Engine execution behaviour (drain, rollback check, completed transaction) | Capability flags over one skeleton | The engines toggle code within one structure.  A strategy per engine would copy the skeleton and let the copies drift. |
| Result shape, collection, documentation, signature, arguments | Data, read by static per-phase code | These vary in data, not behaviour. |

## The design

Six PRs, in order.  Each leaves every generated file byte-identical, proven by PR 1's corpus, and updates the docs and AGENTS.md files that name what it changes.  The extension guide grows with them (see Documentation).

### PR 1: the golden-output corpus

- **The corpus runs at the generator level**, so it can prove what PRs 2, 3, 5 and 6 change outside the writer: typing, settings, markers, SQLSRC275, the clash logic and the methods' file.
  - It drives the generator, through the existing `MethodRuns` and `SidecarFaker` harness, over `.sql` files and fake sidecars.
  - It records every generated file and every diagnostic of each run, and compares them with checked-in golden files.
  - Writer-level cases, built by hand as `QueryMethodWriterTests` builds them, may supplement it where a row is awkward to reach through the generator.  They do not replace it.
- **The matrix covers:**
  - engine;
  - shape (none, rowcount, one, one-optional, many);
  - collection type, streaming included, with and without the row buffer;
  - input or none;
  - no token, a token, and a token named `await`;
  - `ValidateTokens`;
  - placement (extension class, public, internal, private);
  - a not-null SqlVector column;
  - a by-element conversion in each direction;
  - the `value` / `value{N}` name collision;
  - phase 7's conversion kinds:
    - a library converter (NodaTime) on each engine;
    - the JSON serializer, whole and by element, with a not-null property that throws on a JSON null;
    - SQL Server's spatial read and write;
    - PostgreSQL's driver below and at Npgsql 10;
  - internal and public exceptions (`SqlSourcePublicExceptions`).
- **Not every cross product is needed.**  The matrix must reach every branch of the writer on each engine, especially SQL Server streaming.
- **Engine is a parameter of the matrix**, so a new engine is new rows, not a new test.
- **Golden files are updated only by an explicit opt-in**, such as an environment variable.  A run without it never writes them.  The procedure goes in `tests/README.md`.  A PR that changes a golden file says why in its description.
- **This PR fixes the stale `LocalNames` line in `Emitting/AGENTS.md`.**

### PR 2: a closed set of engines, across the generator

- **Every two-way branch on `DatabaseEngine` under `src/SqlSource` becomes a switch *expression* that names every engine.**  A `switch` statement gives no such error, so a decision written as a statement first takes its value from a switch expression.  An `if (engine != SqlServer) return;`, for example, reads a capability that a switch expression decides.  A missing member is then a compile error.  This was checked with a probe build.
- **How that is enforced:** a switch expression over an enum that names every member and has no discard gives CS8524, for an unnamed value.  One that misses a named member gives CS8509.  CS8524 is turned off for `src/SqlSource` in `.editorconfig`, with a comment saying why.  CS8509 stays an error under `TreatWarningsAsErrors`.  Adding an engine then fails the build at every switch.  No `_ =>` arm may be written over `DatabaseEngine`.
- **The `PostgreSql` doc comment says PostgreSQL alone.**
- **The dialect-to-engine mapping is the one place a switch alone cannot guard.**
  - `SqlDescribableDialects.EngineOf` names the engine whose rules read a file's type override.  It gives PostgreSQL's rules to every dialect but SQL Server's, including `MySql`, `MariaDb`, `Sqlite`, `Ansi` and `Oracle`, and users see that through the marker diagnostics.
  - It becomes a switch expression over `SqlDialect` that names every dialect, so a new dialect fails the build.  Its PostgreSQL arms for the dialects that cannot be described stay, so behaviour does not change, with a comment saying why.
  - A test asserts that each dialect in `SqlDescribableDialects.All` maps to an engine of its own.  When MySQL or SQLite becomes describable but still maps to PostgreSQL, the test fails.
  - The engine guide's first step is to give the dialect its engine there.
  - The `SqlDialect.SqlServer` checks in `SqlParameterList` and the other parsing code test the dialect, not the engine, so they are not part of this step.
- **The two-engine structures keep their shape:** `MethodDrivers`, `CodeGenEngines` (two bits) and `DatabaseEngineExtensions`.  Their lookups become exhaustive switches.  A new engine adds a field or a bit, and the switches show where.  Generalising them to tables now would be speculative.  `MethodDrivers`' driver and library capabilities, `PostgresAtLeast10` and `SqlServerSpatial`, stay fields named for what they say.
- **Scope:** every engine branch in `src/SqlSource`, not only the method code.  A missed branch in `QueryTyper` or a type map does the same damage as one in the writer.
- **Tests:** the builder decides whether any lookup also gets a test over `Enum.GetValues`.  It is not required, since the compiler enforces the set.

### PR 3: one argument model

- **A method's arguments are defined once**, in `Generation/Methods/`, as an ordered model of what the method takes.
  - Each entry has its role (receiver, input, token, transaction, command timeout, cancellation), its name, its type, its default and its documentation.
  - The model gives three forms: the method's declaration, the streaming iterator's declaration, and the call to the iterator.
- **Everything that spells arguments today reads this model:**
  - the signature, the iterator, the call and the `<param>` docs;
  - `AppendChecks`;
  - `QueryMethodLocals.Seed`;
  - SQLSRC275's reserved names, which replace `MethodPlanner.FixedArguments`;
  - `MethodSignature.ParameterKey`.
- **`ParameterKey` keeps its value.**  It still encodes the input type and one `,string` per token, so `MethodSignatures` caches as before.  Its doc comment says the key is made of the arguments a call must give, beyond the receiver, because an argument with a default cannot tell two overloads apart.
- **Building the model allocates nothing per line.**  The builder decides between filling a reused list and a struct enumerator.
- **The receiver is still the `connection` argument.**  How a later receiver feature changes it is a rule in the extension guide, not code here.

```csharp
internal enum ArgumentRole { Receiver, Input, Token, Transaction, CommandTimeout, Cancellation }

internal readonly record struct MethodArgument(
    ArgumentRole Role, string Name, string TypeText, string? DefaultText, string? DocumentationXml);
```

### PR 4: execution capabilities

- **The writer asks about a behaviour, never an engine.**  A small value, derived in the output step and never cached, says which of the SQL Server behaviours a method has:
  - it refuses a completed transaction;
  - it drains later results;
  - it checks for a rollback where it reads rows;
  - it drains in a `finally` when its loop is left at a row.
- **One exhaustive switch over the engine** decides those flags, from the shape and whether the method streams.  The long explanations of why each flag exists move there, written once.
- **The `<exception>` docs read the same flags**, so the docs and the code cannot drift apart.
- **`QueryMethodLocals` takes the flags** in place of its two bools.
- **A conversion's kind becomes one value.**  `Conversion`'s `IsSpatial` and `IsSerializer` are mutually exclusive ways of converting, beside the default, a converter method.  They become one `ConversionKind` enum on the record, so `RowReaderWriter` and the binders switch on it once, and a new kind fails the build where it must be handled.  `IsByElement` and `MayReturnNull` stay flags, since they combine with a kind.  This follows the same rule as the engines: a new axis is a value, not another bool.
- **The statements that the flags turn on go in one file named for what it writes, not for SQL Server**, because MySQL and SQLite may well need the drain.  That covers the completed-transaction refusal, the rollback catch and the drain loops.  The buffered and streamed structure stays in the row-reading code, which calls into this file at its flag points.

```csharp
[Flags]
internal enum ExecutionCapabilities
{
    None = 0,
    RefusesCompletedTransaction = 1,
    DrainsLaterResults = 2,
    ChecksRollbackAtRead = 4,
    DrainsWhenLeftAtARow = 8,
}
```

### PR 5: a method's identity is its type and name

- **`DroppedMethod`, `MethodConflicts.DroppedOf` and `MethodEmitter.Keep` drop by `(TypeKey, method name)`.**  Names are unique within a type once `MethodPlanner.AddPlanned` has run.  `NameLocation` stays only to place diagnostics.
- **`MethodPlanner`'s `Planned` holds a list of methods**, one today, so that a query giving several is a change in one place.
- **`CachingMethodTests` stay true.**  The conflict tests change shape with `DroppedMethod`.
- **Today's output and diagnostics do not change.**  The PR fixes a latent bug before a feature can trigger it, so it needs no tech-debt item.

### PR 6: the method's context, the phase files and the host

- **A `readonly` context struct, passed `in`, replaces the long parameter lists.**  It holds the builder, the method, the host, the tokens and their written arguments, the locals and the capabilities.  The row buffer's type is a constant once the Roslyn 4.14 work lands, so the context does not hold it.  It may also hold a small cached array of indents, which removes today's `indent + GeneratedText.Indent` concatenations.
- **The method's connection and transaction are names of locals or arguments, from `LocalNames`, never expressions.**  Today both are the arguments.  The extension guide says a later receiver copies its connection and transaction into locals once, at the start of the method.
- **`MethodHost` replaces the `"public static"` string and the `isExtension` bool.**  It holds the accessibility and whether the method extends its first argument.  `TypeMethods`, `MethodEmitter` and `MethodSignature.Of` read it.  `Settings.MethodPlacement` stays as it is: it mirrors the attribute's public enum.
- **The async spellings are centralised in one helper, with no sync mode:** the awaited-call tail, `await using` with the disposable's `ConfigureAwait`, and the `async Task<…>` return type.  The helper becomes the async strategy when the sync feature arrives.
- **The writer splits by phase into a new folder, `Generation/Emitting/QueryMethods/`**, with the namespace `SqlSource.Generation.Emitting.QueryMethods`:
  - Orchestration and the streaming split.
  - The header: documentation, signature and the iterator call, all from the argument model.
  - The command: SQL, open and close, the command and the binding loop.
  - The rows: buffered, one, streamed, and the result types and buffers.
  - The capability statements from PR 4.
  - The async helper.

  `QueryMethodLocals`, `LocalNames` and `RowReaderWriter` move with them if nothing outside query methods uses them.  `MethodWriter` (token methods), `MethodEmitter`, the binders and `RowBuffer` stay in `Emitting/` unless the builder finds a reason to move them.
- **Why the folder:** `Emitting/` already has 19 source files, and the split would take it past the maintainability skill's threshold.  The namespace changes with the folder, because `.editorconfig` requires that.  This PR updates every `using`, cref and relative link that names a moved type.

## What each feature brings with it

None of this is built now.  These are the rules the extension guide states.  A feature's own design may revisit them with reason.

- **A new axis is a value on the plan, never a `bool` threaded through helpers.**  It is an enum on `QueryMethod` or `TypeMethods`, small and equatable.  What it derives (arguments, spellings, capabilities) is worked out where it is used.
- **A second implementation of something that varies wholesale becomes a strategy**, chosen in the output step by one exhaustive switch, as `ParameterBinder.For` is.  It never flows through the pipeline.
- **One query, several methods:** `Planned` gains entries, each with a name unique in the type.  Calls to a sync and an async method that differ only by an argument with a default are ambiguous (CS0121), so they need distinct names.  Clash keys are built only from the arguments a call must give.
- **A receiver other than the connection argument is read once into locals** named from `LocalNames`, and the body only ever uses those locals.  What is thrown when it is null, and who opens and closes the connection, are the feature's decisions.  Connection ownership is the candidate for a strategy.
- **A new local gets a row in the four awkward-name tests** (`Emitting/AGENTS.md`).
- **A new axis or engine becomes a dimension or rows of the golden corpus.**
- **Which token names SQLSRC275 refuses is visible to users.**  A feature that changes the argument list decides whether reserved names vary with it, and updates `docs/manual/diagnostics.md`.

## Documentation

The user asked for the new design to be well documented, with guidance for extending it.

1. **`docs/architecture/generator.md`, `### Query methods`, is rewritten to describe the code as each PR leaves it:**
   - the argument model;
   - the capabilities and the one switch that decides them;
   - identity by type and name;
   - the context struct;
   - the phase files and what each holds;
   - why execution uses flags while binding uses a strategy.

   Each PR rewrites the parts it changes.
2. **A new `### Extending query methods` section under `## Emitting`** holds two guides:
   - **`#### Adding a database engine`:** what to add, in order.
     - The enum member, and the switches the compiler then lists.
     - The dialect and lexer.
     - The describer, linking `tool.md`.
     - The type map and driver probe.
     - A `ParameterBinder` subclass.
     - The engine's row of capability flags.  A new flag only for a new behaviour, named for the behaviour.
     - A row-reading strategy when the engine is the first to need different reading statements.
     - Golden-corpus rows, database tests and image pins (root `AGENTS.md` rule 5), and the manual.
   - **`#### Adding a kind of method`:** the rules of "What each feature brings with it", and the strategy-or-flags-or-data table.

   Both are phrased as rules for the code as it is.  Under `docs/architecture/AGENTS.md` they describe no unbuilt feature as present.
3. **AGENTS.md pointers, as rules with a link (root rules 4 and 13):**
   - **`Generation/Emitting/AGENTS.md`:** the writer asks capabilities, not engines.  The argument list comes only from the argument model.  A new kind of method follows the guide.  Every name it lists is updated.
   - **A new `Generation/Emitting/QueryMethods/AGENTS.md`**, if the folder needs rules of its own beyond those, with the maintenance footer.
   - **`Generation/AGENTS.md`:** no `_ =>` arm over `DatabaseEngine`.  Adding an engine follows the guide.
   - **`src/SqlSource/AGENTS.md`:** strategy objects are chosen in the output step and never enter the pipeline, if not already covered.
4. **Code comments that point to the guide:**
   - the `DatabaseEngine` enum;
   - the capabilities switch;
   - `ParameterBinder`;
   - the argument model;
   - the query-method writer's orchestration class;
   - the `.editorconfig` line that turns CS8524 off.
5. **`docs/architecture/AGENTS.md`:** a row for `src/SqlSource/Generation/Emitting/QueryMethods`, pointing to `### Query methods` and `### Extending query methods`.
6. **`tests/README.md`:** the golden corpus and how to update it.
7. **No user documentation, manual page or example changes**, because nothing a user sees changes.  No `docs/deferred` item is added: the features are not planned work this refactor defers.

## Decisions

Made by the user:

- The three method features weigh equally.  MySQL and SQLite are to be considered, with no date.
- The hybrid approach.
- The closed engine set covers the whole generator.
- The new design is documented in `docs/architecture/`, with pointers from the AGENTS.md files and comments, and a guide to extending it for engines and method features.

Made by me, to check while reading:

- **Golden files are permanent and checked in,** updated only by an explicit opt-in, rather than compared with the base commit's output in the same run.
- **The corpus runs at the generator level and records diagnostics**, not only the writer's output.  This makes PR 1 larger, but without it the promise of no output or diagnostic change would rest on the existing tests for PRs 2, 3, 5 and 6.
- **The dialect-to-engine mapping keeps PostgreSQL's rules for the dialects that cannot be described**, and a test catches a dialect that becomes describable without an engine of its own.
- **Engine exhaustiveness is enforced by the compiler**, CS8509 with CS8524 off, rather than by discard arms and a test.
- **Execution behaviour uses capability flags, not a per-engine strategy**, and its statements go in one file named for what it writes.
- **The writer moves to `Generation/Emitting/QueryMethods/`**, taking the namespace change.
- **The async spellings are centralised now with no sync mode.**  The strategy waits for the sync feature.
- **`MethodDrivers` and `CodeGenEngines` keep their two-engine shape** with exhaustive lookups.
- **The refactor starts after the Roslyn 4.14 floor branch merges**, and assumes its single embedded row buffer.
- **`Conversion`'s spatial and serializer flags become one `ConversionKind`**, added to PR 4 after the phase 7 recheck.
- **No `docs/tech-debt` or `docs/deferred` item is added:** the identity bug is fixed in PR 5, and the features are not deferred work.

## Left to the builder

- Exact type, file and member names, within the rules above.
- How far the golden matrix crosses its dimensions, as long as it reaches every writer branch on each engine.
- How the argument model avoids allocation.
- Which shared files move into `QueryMethods/`.
- Whether to split any PR further, or to merge PR 5 into PR 3 if that proves simpler.
- The PR-time checks the root `AGENTS.md` requires, including the version check and the timing statement for hot-path changes.
