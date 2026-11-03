# Query methods: the context, the phase files and the host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** PR 6 of the query-method emission refactor: a `readonly` context struct passed `in`, the connection and transaction as names, `MethodHost` in place of the modifiers string and the `isExtension` bool, the async spellings in one helper, and the writer split by phase into `src/SqlSource/Generation/Emitting/QueryMethods/`, with every generated file and diagnostic byte-identical.

**Architecture:** Six commits, each byte-identical against the golden corpus.  `MethodHost` first, in the old folder; then a pure move of the query-method files into `QueryMethods/`; then the context, the indents and the names; then the async helper; then the split of `QueryMethodWriter` into phase files; then the documentation.

**Tech Stack:** C# (generator on `netstandard2.0`, Roslyn 4.14 floor), xUnit v3 on Microsoft Testing Platform, Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-30-query-method-emission-refactor-design.md`, section "PR 6: the method's context, the phase files and the host" and the section "Documentation".

## Where the merged code differs from the spec

- `Generation/Emitting/` has 22 source files, not 19: PR 4 added `ExecutionCapabilities.cs`, `ExecutionCapabilityRules.cs` and `ExecutionCapabilityWriter.cs`, which move with the writer.
- `QueryMethodWriter` is 712 lines, its capabilities already a trailing parameter of five helpers (PR 4), and its argument names the constants of `MethodArguments` (PR 3).
- The row buffer's type is the constant `RowBuffer.TypeText`; the context does not hold it.
- `Generation/AGENTS.md`'s rules of the spec's Documentation item 3 (no `_ =>` arm, adding an engine follows the guide) live in `src/SqlSource/AGENTS.md`, by the owner's ruling on PR 2; nothing more is needed in `Generation/AGENTS.md`.
- The strategy rule of Documentation item 3 is not in `src/SqlSource/AGENTS.md` yet; Task 6 adds it.  That file is at 8,191 of its 8,192 bytes.
- No `AGENTS.md` and no `tests/README.md` names a moved test file.

## Global Constraints

- **Byte identity is the bar.**  `QueryMethodCorpusTests` passes unchanged in `tests/SqlSource.Tests` and `tests/SqlSource.Tests.RoslynFloor`, diagnostics included.  Never set `SQLSOURCE_UPDATE_GOLDEN`; a changed golden file means the code is wrong.
- Every existing test keeps its assertions; only the calls it makes change with the signatures.
- The generator targets `netstandard2.0`; "Allocates more than today, or makes a string per line" is an anti-goal.
- No switch expression over an enum under `src/SqlSource` has a `_ =>` arm (CS8524 is off; a missing member is CS8509).
- `.editorconfig` has `dotnet_style_namespace_match_folder = true`: a moved file takes the folder's namespace, `SqlSource.Generation.Emitting.QueryMethods`, and a moved test `SqlSource.Tests.Generation.Emitting.QueryMethods`.
- `Settings.MethodPlacement` does not change.
- An `AGENTS.md` or architecture page that names a renamed or moved file, type or member is updated in the same commit (root `AGENTS.md` guideline 6, `docs/architecture/AGENTS.md`).
- No `docs/tech-debt` or `docs/deferred` item, no manual page and no example changes: nothing a user sees changes.
- Each implementer runs the tests of what it changed plus the corpus; `./pre-commit-validation.sh` runs once on the finished branch.

## Review Focus

1. **A method on a type nested deeper than the indent cache** (more than 15 levels): its lines must still be indented by four spaces a level.  Task 3 pins it with `Deeper_IndentPastTheCache_IsBuilt`.
2. **A method placed `Internal`, `Private` or `Public` on the type, or in the extension class**: the modifiers and `this` must come out as today.  Task 1 pins `MethodHost.Of` for each placement and keeps the writer's three-placement theories.
3. **A type's plan caches as before**: `TypeMethods` with `Host` in place of `Placement` must still compare equal across edits.  The `CachingMethodTests` stay unchanged and pass; Task 1 adds `MethodHost` equality to `MethodHostTests`.
4. **An extension class that is blocked drops only the extension methods**: `MethodEmitter` must read `Host.IsExtension` where it read the placement.  The existing `MethodEmitterTests` pass.
5. **A token whose name is a local's name** (`sql`, `command`, `reader`, `ReadRows`): the context changes no name.  The awkward-name tests of `GeneratedSourceMethodTests` and `MethodWriterTests` pass unchanged.

---

### Task 1: `MethodHost` in place of the modifiers and the `isExtension` bool

**Files:**
- Create: `src/SqlSource/Generation/Methods/MethodAccessibility.cs`, `src/SqlSource/Generation/Methods/MethodHost.cs`
- Modify: `src/SqlSource/Generation/Methods/TypeMethods.cs` (the `Placement` parameter becomes `MethodHost Host`), `MethodPlanner.cs` (builds `MethodHost.Of(type.MethodPlacement)`), `MethodSignature.cs`, `MethodArguments.cs`, `src/SqlSource/Generation/Emitting/MethodEmitter.cs`, `QueryMethodWriter.cs`
- Modify tests: `tests/SqlSource.Tests/Generation/Emitting/QueryMethodWriterTests.cs`, `MethodEmitterTests.cs`, `tests/SqlSource.Tests/Generation/Methods/MethodSignatureTests.cs`, `tests/SqlSource.Tests/Generation/TestModels.cs`, and any other test that builds a `TypeMethods`
- Test: `tests/SqlSource.Tests/Generation/Methods/MethodHostTests.cs`

**Interfaces:**
- Produces:
  - `internal enum MethodAccessibility { Public, Internal, Private }`
  - `internal readonly record struct MethodHost(MethodAccessibility Accessibility, bool IsExtension)` with `public static MethodHost Of(MethodPlacement placement)`: `ExtensionClass` → `(Public, true)`, `Public` → `(Public, false)`, `Internal` → `(Internal, false)`, `Private` → `(Private, false)`, a switch expression naming every member.  Its summary: where a query's method goes and how visible it is, which `Settings.MethodPlacement` mixes; a later kind of method changes it here (`docs/architecture/generator.md`, "Adding a kind of method").
  - `TypeMethods.Host` (third positional parameter, in `Placement`'s place, documented "Where the methods go and how visible they are.").
  - `QueryMethodWriter.Append(StringBuilder builder, string indent, QueryMethod method, MethodHost host)` and the `internal` overload `Append(StringBuilder, string, QueryMethod, MethodHost, ExecutionCapabilities)`.
  - `MethodArguments.AppendDeclaration(StringBuilder builder, string indent, MethodHost host)`: `this` before the receiver when `host.IsExtension`.
  - The modifiers are spelled in the writer from `host.Accessibility`: `public static`, `internal static`, `private static`, by a switch expression with no discard.  `MethodEmitter.ModifiersOf` is removed.

- [ ] **Step 1: Write `MethodHostTests`**: a theory `Of_EachPlacement_GivesItsHost` with the four rows above, and `Equality_TwoHostsOfOnePlacement_AreEqual` (`MethodHost.Of(MethodPlacement.Internal).ShouldBe(MethodHost.Of(MethodPlacement.Internal))`).
- [ ] **Step 2: Run it; it fails to compile** (`MethodHost` is missing).
- [ ] **Step 3: Add the two types, replace `TypeMethods.Placement` with `Host`, and change every reader**: `MethodEmitter` (`methods.Host.IsExtension`), `MethodSignature.Of` (extension class when `Host.IsExtension`), the writer and `MethodArguments`.  The writer tests' helpers take a `MethodHost` (default `MethodHost.Of(MethodPlacement.ExtensionClass)`); each `[InlineData("public static")]` theory becomes `[InlineData(MethodPlacement.Public, "public static")]` and its siblings, keeping its expected text.
- [ ] **Step 4: Run** `dotnet test --project tests/SqlSource.Tests -- --filter-namespace "SqlSource.Tests.Generation*"` and `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generator.QueryMethodCorpusTests"`; both pass, no golden file changed (`git status` shows none).
- [ ] **Step 5: Commit** `refactor(generator): a query method's host in place of its modifiers and isExtension`.

### Task 2: move the query-method files into `Emitting/QueryMethods/`

A move alone: no line of code changes beyond namespaces, `using`s, crefs and links.

**Files:**
- Move (`git mv`) from `src/SqlSource/Generation/Emitting/` to `src/SqlSource/Generation/Emitting/QueryMethods/`: `QueryMethodWriter.cs`, `RowReaderWriter.cs`, `QueryMethodLocals.cs`, `LocalNames.cs`, `ExecutionCapabilities.cs`, `ExecutionCapabilityRules.cs`, `ExecutionCapabilityWriter.cs`.  They stay in `Emitting/`: `MethodWriter`, `MethodEmitter`, `ParameterBinder` and both binders, `BindingContext`, `ConversionNames` (the binders take it), `RowBuffer`, `GeneratedText`, `XmlDocWriter` and the type and model emitters.
- Move tests from `tests/SqlSource.Tests/Generation/Emitting/` to `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/`: `QueryMethodWriterTests.cs`, `ExecutionCapabilityRulesTests.cs`, `LocalNamesTests.cs`.
- Modify: every file that names a moved type gets `using SqlSource.Generation.Emitting.QueryMethods;` (or `SqlSource.Tests.Generation.Emitting.QueryMethods` for `QueryMethodWriterTests.GetUser`): `MethodEmitter.cs`, `SqlServerParameterBinder.cs`, `ConversionNames.cs` (its cref to `LocalNames`), `Generation/Methods/QueryMethod.cs` (the cref `Emitting.QueryMethodWriter.VectorType`), `MethodEmitterTests.cs`, `RowBufferTests.cs`, `MethodSignatureTests.cs`.  A moved file that names a type left in `Emitting/` gets `using SqlSource.Generation.Emitting;`.
- Modify docs, links only: `docs/architecture/generator.md` (every link to a moved file), `src/SqlSource/Generation/Emitting/AGENTS.md` (each moved file's path, `Generation/Emitting/QueryMethods/…`), `docs/tech-debt/TD-0090-sql-server-error-at-a-row-hides-a-rollback.md` and `TD-0095-a-cref-to-a-reused-exception-can-be-ambiguous.md` (their links to `QueryMethodWriter.cs`).

- [ ] **Step 1: Move the files and fix namespaces, `using`s, crefs and links.**  `grep -rn "Emitting/QueryMethodWriter\|Emitting/RowReaderWriter\|Emitting/QueryMethodLocals\|Emitting/LocalNames\|Emitting/ExecutionCapabilit" --include='*.md' --include='*.cs' . | grep -v superpowers/` gives nothing but `QueryMethods/` paths.
- [ ] **Step 2: Build and run** `dotnet build SqlSource.slnx` (no warning), then the tests of `SqlSource.Tests.Generation*`, the corpus in both projects, and `--filter-class "SqlSource.Tests.Documentation.AgentsFileTests"`; all pass, no golden file changed.
- [ ] **Step 3: Commit** `refactor(generator): the query-method writer moves to Emitting/QueryMethods`.

### Task 3: the context, the indents and the connection and transaction as names

**Files:**
- Create: `src/SqlSource/Generation/Emitting/QueryMethods/QueryMethodContext.cs`
- Modify: `src/SqlSource/Generation/Emitting/GeneratedText.cs`, `TypeEmitter.cs` (`GetIndent` returns `GeneratedText.IndentOf(depth)`), and in `QueryMethods/`: `QueryMethodWriter.cs`, `RowReaderWriter.cs`, `ExecutionCapabilityWriter.cs`, `QueryMethodLocals.cs`
- Test: `tests/SqlSource.Tests/Generation/Emitting/GeneratedTextTests.cs`, `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/QueryMethodLocalsTests.cs`

**Interfaces:**
- Consumes: `MethodHost` (Task 1).
- Produces:
  - `internal readonly struct QueryMethodContext`, built once in `QueryMethodWriter.Append`, with get-only properties `StringBuilder Builder`, `QueryMethod Method`, `MethodHost Host`, `List<string> Tokens` (from `MethodWriter.GetTokens`), `List<string> SpelledTokens` (from `MethodArguments.SpellTokens`), `MethodArguments Arguments`, `QueryMethodLocals Locals`, `ExecutionCapabilities Capabilities`, `bool Streams`.  No row-buffer type and no indents.
  - Every helper of `QueryMethodWriter`, and every public method of `RowReaderWriter` and `ExecutionCapabilityWriter`, takes `in QueryMethodContext context` first, in place of the builder and of each value the context holds, then its indent(s) and the values specific to the call (an `output`, `moreThanOne`, a drain's token).  `AppendIf` and `AppendEnd` keep `(StringBuilder, string, …)`.
  - `GeneratedText.IndentOf(int depth)`: the cached indent of `depth` levels (cache levels 0 to 15, built once), and one built for a deeper level.
  - `GeneratedText.Deeper(string indent)`: the indent one level deeper, the cached string when `indent` is the cached one of its level (by reference), else `indent + Indent`.  It replaces every `indent + GeneratedText.Indent` under `QueryMethods/`.
  - `QueryMethodLocals.Connection` and `QueryMethodLocals.Transaction`: the names the body runs the query on, today `MethodArguments.Connection` and `MethodArguments.Transaction`; their comment says a receiver other than the connection takes local names here from the method's `LocalNames` and copies into them once, at the start of the method ("Adding a kind of method").  Every use of `MethodArguments.Connection` and `MethodArguments.Transaction` in the body, in the writer and in `ExecutionCapabilityWriter`, reads these instead.  The argument model's declaration and checks still read the arguments.

- [ ] **Step 1: Write the tests.**  `GeneratedTextTests`: `IndentOf_ThreeLevels_IsTwelveSpaces`; `Deeper_ACachedIndent_GivesTheCachedNext` (`ReferenceEquals(GeneratedText.Deeper(GeneratedText.IndentOf(2)), GeneratedText.IndentOf(3))` is true); `Deeper_IndentNotFromTheCache_AddsOneLevel` (`Deeper(new string(' ', 8))` is 12 spaces); `Deeper_IndentPastTheCache_IsBuilt` (`Deeper(IndentOf(20))` is 84 spaces).  `QueryMethodLocalsTests`: `ConnectionAndTransaction_AreTheArguments` (`"connection"`, `"transaction"`).
- [ ] **Step 2: Run them; they fail to compile.**
- [ ] **Step 3: Add the context, the indents and the names; thread the context through the writer, `RowReaderWriter` and `ExecutionCapabilityWriter`.**
- [ ] **Step 4: Run** the tests of `SqlSource.Tests.Generation*` and the corpus in both projects; all pass, no golden file changed.
- [ ] **Step 5: Commit** `refactor(generator): a query method is written from one context passed in`.

### Task 4: the async spellings in one helper

**Files:**
- Create: `src/SqlSource/Generation/Emitting/QueryMethods/AsyncSpelling.cs`
- Modify: `QueryMethodWriter.cs`, `ExecutionCapabilityWriter.cs` in `QueryMethods/`
- Test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/AsyncSpellingTests.cs`

**Interfaces:**
- Produces `internal static class AsyncSpelling`, whose summary says it holds every spelling that makes a query's method asynchronous and is the one place a synchronous kind of method would change ("Adding a kind of method"); it has no sync mode:
  - `AppendAwaitedCall(StringBuilder builder, string target, string member)`: `await <target>.<member>(`.
  - `AppendAwaitedEnd(StringBuilder builder)`: `).ConfigureAwait(false)`.
  - `AppendUsing(StringBuilder builder, string indent, string scope, string disposable)`: `<indent>await using var <scope> = global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(<disposable>, false);\n` (takes `ConfigureAwaitOfDisposable` and its comment from the writer).
  - `AppendTaskType(StringBuilder builder, string? resultType)`: ` async global::System.Threading.Tasks.Task`, with `<resultType>` when it is not null.
  - `const string IteratorModifiers = "static async "`.
- Every awaited call goes through the first two: `OpenAsync`, `CloseAsync`, `ExecuteNonQueryAsync`, `ExecuteReaderAsync`, `ReadAsync`, `NextResultAsync` (the drain loop) and `DisposeAsync` (the rollback catch); the writer's `AppendScope` becomes `AppendUsing`, the signature's task type `AppendTaskType`, and the iterator's `static async ` the constant.  `grep -n "ConfigureAwait\|async \|await " src/SqlSource/Generation/Emitting/QueryMethods/*.cs` finds them in `AsyncSpelling.cs` alone (and in comments).

- [ ] **Step 1: Write `AsyncSpellingTests`**, one test for each member with its exact text, e.g. `AppendAwaitedCall` then `Append("token")` then `AppendAwaitedEnd` on target `reader`, member `ReadAsync` gives `await reader.ReadAsync(token).ConfigureAwait(false)`; `AppendTaskType(builder, null)` gives ` async global::System.Threading.Tasks.Task`.
- [ ] **Step 2: Run it; it fails to compile.**
- [ ] **Step 3: Add the helper and route every site through it.**
- [ ] **Step 4: Run** the writer tests, the new tests and the corpus in both projects; all pass, no golden file changed.
- [ ] **Step 5: Commit** `refactor(generator): a query method's async spellings in one helper`.

### Task 5: the writer split by phase

**Files:**
- Create in `src/SqlSource/Generation/Emitting/QueryMethods/`: `HeaderWriter.cs`, `CommandWriter.cs`, `RowsWriter.cs`
- Modify: `QueryMethodWriter.cs`, `RowReaderWriter.cs`, `QueryMethodLocals.cs`, `src/SqlSource/Generation/Emitting/SqlServerParameterBinder.cs`, `src/SqlSource/Generation/Methods/QueryMethod.cs`, `docs/architecture/generator.md` and `src/SqlSource/Generation/Emitting/AGENTS.md` where they name a member that moves, `docs/tech-debt/TD-0095-a-cref-to-a-reused-exception-can-be-ambiguous.md` (the constant's file)

**Interfaces:**
- Consumes: `QueryMethodContext` (Task 3), `AsyncSpelling` (Task 4).
- Produces, each class `internal static`, each file readable top to bottom in the order the method is written:
  - `QueryMethodWriter`: the two `Append` overloads, which build the context and write the header, the checks and either the body or the streaming split (the `return` of the iterator and its declaration, then the body inside it); `AppendIf` and `AppendEnd`.  Its summary keeps what the method does and adds that the phases are `HeaderWriter`, `CommandWriter` and `RowsWriter`, and that a new kind of method or engine follows `docs/architecture/generator.md`, "Extending query methods".
  - `HeaderWriter`: `ExceptionCref` and its comment, `AppendDocumentation`, `AppendSignature` (with the modifiers' switch of Task 1), `AppendChecks`, `AppendIteratorCall` (the `return ReadRows(…);`, the iterator's signature and its `{`).
  - `CommandWriter`: `SqlFromTokensStart` and `End`, `AppendBody` (the completed-transaction call, the SQL, the open, the `try`, the command, the binding loop, the execution, the `finally` and the close), `AppendSql`, `AppendCommand`, `AppendBindings` (the loop over the input's parameters with the binder of `ParameterBinder.For`).
  - `RowsWriter`: `ExceptionType`, `AppendExecution`, `AppendRows`, `AppendStream`, `AppendRowIntoLocal`, `AppendDrain`, `AppendRead`, `AppendThrow`, `internal GetResultType` (the header's signature calls it), `GetCollectionType`, `GetBuffer`.
  - `RowReaderWriter.VectorType` in place of `QueryMethodWriter.VectorType`, with its summary.
- Each method keeps its comment.  A helper called from one file only is `private`.

- [ ] **Step 1: Split the file**; no statement changes beyond the class a call names.
- [ ] **Step 2: Run** `dotnet build SqlSource.slnx` (no warning), the tests of `SqlSource.Tests.Generation*`, the corpus in both projects and `AgentsFileTests`; all pass, no golden file changed.
- [ ] **Step 3: Commit** `refactor(generator): the query-method writer split by phase`.

### Task 6: the documentation

**Files:**
- Modify: `docs/architecture/generator.md` (`### Query methods`, `### Extending query methods`), `src/SqlSource/Generation/Emitting/AGENTS.md`, `docs/architecture/AGENTS.md`, `src/SqlSource/AGENTS.md`
- Create: `src/SqlSource/Generation/Emitting/QueryMethods/AGENTS.md`

- [ ] **Step 1: `### Query methods`.**  Its opening names `QueryMethodWriter` in `Emitting/QueryMethods/` and the phase files.  New paragraphs, before **A method.**: **The host.** (`MethodHost`, its two facts, read by `TypeMethods`, `MethodEmitter` and `MethodSignature.Of`, why `MethodPlacement` stays); **The context.** (`QueryMethodContext`, what it holds, passed `in`, built once, the connection and transaction as names from `QueryMethodLocals`, the indents from `GeneratedText`); **The phase files.** (what each file holds, in the order a method is written, and `AsyncSpelling`, with no sync mode).  **A method.** and **A method that streams.** name the file of each step.  Link every file; check each claim against the code.
- [ ] **Step 2: `#### Adding a kind of method`**, after `#### Adding a database engine`: the rules of the spec's "What each feature brings with it", as rules for the code as it is, describing no unbuilt feature as present, and the spec's table "Where the strategy pattern applies" (what varies, pattern, why).  Fold into it the receiver rule from the arguments paragraph before `#### Adding a database engine`, so it is said once.
- [ ] **Step 3: `AGENTS.md` files.**
  - New `Generation/Emitting/QueryMethods/AGENTS.md`, with a link to query methods: the rules of `Emitting/AGENTS.md` that name its files move here (`## A query method's arguments`, `## Engines in a query method`, the locals rule), each updated for the context and the names; plus a rule that a new kind of method follows `#### Adding a kind of method`, that a new axis is a value on the plan, never a `bool` threaded through helpers, and that an awaited call is spelled only by `AsyncSpelling`.  The maintenance footer.
  - `Generation/Emitting/AGENTS.md` keeps what is its own and names no moved file.
  - `docs/architecture/AGENTS.md`: the row `src/SqlSource/Generation/Emitting/QueryMethods` | `generator.md` | `### Query methods` and `### Extending query methods`, under `## Emitting`.
  - `src/SqlSource/AGENTS.md`: the rule that a strategy object, as a `ParameterBinder`, is chosen in the output step by one exhaustive switch and never enters the pipeline, linking `#adding-a-kind-of-method`.  The file is at 8,191 of 8,192 bytes: free room by tightening wording without changing any rule, and list what was reworded in the hand-back.
- [ ] **Step 4: Run** `dotnet test --project tests/SqlSource.Tests -- --filter-namespace "SqlSource.Tests.Documentation"`; it passes.
- [ ] **Step 5: Commit** `docs(architecture): the query-method phase files and adding a kind of method`.
