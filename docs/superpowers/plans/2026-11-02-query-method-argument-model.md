# Argument model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A query method's arguments are defined once, as an ordered model in `Generation/Methods/`, and everything that spells them reads it, with every generated file and diagnostic unchanged.

**Architecture:** `MethodArguments`, a `readonly struct` holding the method's input, its token names and whether it streams, enumerates `MethodArgument` values through a struct enumerator, so building and reading it allocates nothing.  It writes the method's declaration, the iterator's declaration and the call to the iterator.  The writer's `<param>` docs and checks, `QueryMethodLocals.Seed`, SQLSRC275's reserved names and `MethodSignature.ParameterKey` all read it.

**Tech Stack:** C# (`netstandard2.0` generator), xUnit v3 with Shouldly, the golden corpus of PR 1.

**Spec:** `docs/superpowers/specs/2026-10-30-query-method-emission-refactor-design.md`, section "PR 3: one argument model", and "Documentation".

## Global Constraints

- Byte identity: `tests/SqlSource.Tests/Generator/QueryMethodCorpusTests.cs` passes unchanged in `tests/SqlSource.Tests` and `tests/SqlSource.Tests.RoslynFloor`, diagnostics included.  A changed golden file means the code is wrong; never run with `SQLSOURCE_UPDATE_GOLDEN`.
- `ParameterKey` keeps its exact value: the input model's type or empty, then `,string` for each token.
- Building and enumerating the model allocates nothing; the three forms write into the builder and allocate nothing.
- The receiver is still the `connection` argument, of type `global::System.Data.Common.DbConnection`.
- No `_ =>` arm on any switch expression over an enum under `src/SqlSource` (unit 2's rule): a switch over `ArgumentRole` names all six members.
- No other unit's scope: no method identity (5), no capabilities or `ConversionKind` (4), no context struct, `MethodHost`, async helper or writer split (6).
- No user documentation, manual, example, `docs/deferred` or `docs/tech-debt` change.

## Where the code differs from the spec

- **The spec lists six places that spell the arguments; the code has them as described,** plus the writer's five name constants (`QueryMethodWriter.Connection` and the rest), which the body also uses.  They move to the model, so a name is written once.
- **A token named `await` is written `@await`** in the method and its iterator.  The model's `Name` is the identifier's value, `await`, which the `<param>` names and `LocalNames` takes; the forms and the checks write it through `MethodArguments.AppendName`, and `MethodWriter.AppendCreate` still takes the written list, from `MethodArguments.SpellTokens` (today's `GetArguments`).
- **`LocalNames` is seeded with this method's arguments**, so `parameters` is seeded only for a method with an input, where today it is always seeded.  No local's name starts from `parameters` (`parameter` is a different name), so no output changes; the corpus proves it.  SQLSRC275's reserved names stay all five, input or not, since the diagnostic is what a user sees.
- **Whether a method streams moves to `QueryMethod.Streams`**, a computed property (no field, so equality is unchanged), since both the writer and the model need it: the receiver's documentation differs for a stream.

## Review Focus

- A token named `await`: declared, passed to the iterator and checked as `@await`, documented as `await`, and built into the SQL as `@await`: the corpus `tokens` case, and `AppendDeclaration_TokenNamedAwait_IsVerbatim` (Task 1).
- A token named `parameters` on a query with no input still gets SQLSRC275: `IsReserved_FixedName_IsTrue` (Task 1) and the existing `MethodDiagnosticsTests`.
- A token whose name differs from a fixed argument's by case only, `Connection`, is not reserved and compiles: `IsReserved_NameDifferingByCase_IsFalse` (Task 1).
- Two methods of one name with no input, one token each, in two extension classes still clash, and keep their `,string` key: `Of_NoInputWithTokens_KeyIsCommaStringPerToken` (Task 1) and the existing `MethodConflictsTests`.
- A token named as a local (`sql`, `reader`, `ReadRows`) still renames the local: the existing awkward-name tests.

---

### Task 1: The argument model and its readers

**Files:**
- Create: `src/SqlSource/Generation/Methods/ArgumentRole.cs`
- Create: `src/SqlSource/Generation/Methods/MethodArgument.cs`
- Create: `src/SqlSource/Generation/Methods/MethodArguments.cs`
- Modify: `src/SqlSource/Generation/Methods/QueryMethod.cs` (add `Streams`)
- Modify: `src/SqlSource/Generation/Emitting/QueryMethodWriter.cs`
- Modify: `src/SqlSource/Generation/Emitting/QueryMethodLocals.cs`
- Modify: `src/SqlSource/Generation/Methods/MethodPlanner.cs` (remove `FixedArguments`)
- Modify: `src/SqlSource/Generation/Methods/MethodSignature.cs`
- Test: `tests/SqlSource.Tests/Generation/Methods/MethodArgumentsTests.cs`, `tests/SqlSource.Tests/Generation/Methods/MethodSignatureTests.cs` (new)

**Interfaces:**
- Produces:
  - `internal enum ArgumentRole { Receiver, Input, Token, Transaction, CommandTimeout, Cancellation }`, a doc comment on each member.
  - `internal readonly record struct MethodArgument(ArgumentRole Role, string Name, string TypeText, string? DefaultText, string? DocumentationXml)`.  `Name` is the identifier's value; `DefaultText` is null for an argument a call must give; `DocumentationXml` is null for a token, which `XmlDocWriter.AppendParam(builder, indent, name)` documents.
  - `internal readonly struct MethodArguments` with:
    - `public const string Connection = "connection"`, `Parameters = "parameters"`, `Transaction = "transaction"`, `CommandTimeout = "commandTimeout"`, `CancellationToken = "cancellationToken"`.
    - `public MethodArguments(MethodInput? input, List<string> tokens, bool streams)` and `public static MethodArguments Of(QueryMethod method, List<string> tokens)`.
    - `public Enumerator GetEnumerator()`; `public struct Enumerator { public MethodArgument Current { get; } public bool MoveNext(); }`.
    - `public void AppendDeclaration(StringBuilder builder, string indent, bool isExtension)`, `public void AppendIteratorDeclaration(StringBuilder builder, string indent)`, `public void AppendCall(StringBuilder builder)`.
    - `public static bool IsReserved(string name)`, `public static void AppendName(StringBuilder builder, string name)`, `public static List<string> SpellTokens(List<string> tokens)`.
  - `QueryMethod.Streams`: `Shape == ResultShape.Many && Collection == CollectionKind.IAsyncEnumerable`.
  - `QueryMethodLocals(MethodArguments arguments, MethodInput? input, MethodOutput? output, bool streams, bool tracksRow, bool checksTransaction)`.

The model's entries, in order, with today's exact text:

| Role | Name | TypeText | DefaultText | DocumentationXml |
|---|---|---|---|---|
| Receiver | `connection` | `global::System.Data.Common.DbConnection` | null | streams: `The connection to run the query on.  When it is closed, it is opened when the loop starts and closed again when the loop ends.`; otherwise `The connection to run the query on.  It is opened when it is closed, and closed again when the method opened it.` |
| Input, only with an input | `parameters` | `input.TypeText` | null | `The query's parameters.` |
| Token, one per token, in order | the token's name | `string` | null | null |
| Transaction | `transaction` | `global::System.Data.Common.DbTransaction?` | `null` | `The transaction to run the query in, or null.` |
| CommandTimeout | `commandTimeout` | `int?` | `null` | `The command's timeout in seconds, or null for the driver's.` |
| Cancellation | `cancellationToken` | `global::System.Threading.CancellationToken` | `default` | `Cancels the query.` |

Every entry but the input's and a token's is a `static readonly` value (two for the receiver), so the enumerator only copies them.

- [ ] **Step 1: Write the failing tests**

`MethodArgumentsTests`, built from `new MethodArguments(new MethodInput("global::App.P", <empty>), ["orderBy", "await"], streams: false)` unless said:
- `Enumerate_WithInputAndTokens_GivesEachArgumentInOrder`: roles `Receiver, Input, Token, Token, Transaction, CommandTimeout, Cancellation`; names `connection, parameters, orderBy, await, transaction, commandTimeout, cancellationToken`; the input's `TypeText` is `global::App.P`.
- `Enumerate_WithoutInput_LeavesTheInputOut`: roles `Receiver, Token, Token, Transaction, CommandTimeout, Cancellation`.
- `Enumerate_Streams_DocumentsTheReceiverForTheLoop`: the receiver's `DocumentationXml` is the stream text of the table, and without `streams` the other.
- `AppendDeclaration_TokenNamedAwait_IsVerbatim`, indent `"    "`, `isExtension: true`; exactly:
  ```
          this global::System.Data.Common.DbConnection connection,\n
          global::App.P parameters,\n
          string orderBy,\n
          string @await,\n
          global::System.Data.Common.DbTransaction? transaction = null,\n
          int? commandTimeout = null,\n
          global::System.Threading.CancellationToken cancellationToken = default)\n
  ```
  (eight spaces each; `isExtension: false` drops `this `).
- `AppendIteratorDeclaration_TakesNoDefaultsAndMarksTheToken`: as above without `this ` or defaults, and the last line `        [global::System.Runtime.CompilerServices.EnumeratorCancellation] global::System.Threading.CancellationToken cancellationToken)\n`.
- `AppendCall_GivesEachNameInOrder`: `connection, parameters, orderBy, @await, transaction, commandTimeout, cancellationToken`.
- `IsReserved_FixedName_IsTrue` (theory over the five names) and `IsReserved_NameDifferingByCase_IsFalse` (`Connection`, `orderBy`).
- `Enumerate_AndAppendEachForm_AllocatesNothing`: into a `StringBuilder` of capacity 4,096, cleared each round, after a warm-up, `GC.GetAllocatedBytesForCurrentThread` over 1,000 rounds of a `foreach` and the three forms is 0.

`MethodSignatureTests` (`MethodSignature.Of` over a `TypeMethods` of one method, from `QueryMethodWriterTests.GetUser` with its segments and input changed):
- `Of_InputWithTokens_KeyIsTheInputThenStringPerToken`: `global::App.GetUserParams,string`.
- `Of_NoInputWithTokens_KeyIsCommaStringPerToken`: two tokens, no input: `,string,string`.
- `Of_NoInputNoTokens_KeyIsEmpty`: empty.

- [ ] **Step 2: Run them to see them fail to compile**

Run: `dotnet build tests/SqlSource.Tests`
Expected: FAIL, `MethodArguments` not defined.  (The `MethodSignatureTests` pass on today's code once it builds; they pin the value.)

- [ ] **Step 3: Add the role, the record and the model**

The enumerator walks positions 0 (receiver), 1 (input, skipped without one), the tokens, then the last three.  `AppendName` writes `@` before `await`, which an asynchronous method cannot use as an identifier, and `SpellTokens` returns the list itself unless a token is `await`, as `GetArguments` did.  The forms write each argument on its own line at `indent + GeneratedText.Indent`, `,\n` between them and `)\n` after the last; the iterator's form writes no `this` or default, and `[global::System.Runtime.CompilerServices.EnumeratorCancellation] ` before the cancellation's type.  The call's form writes the names joined by `, `.  `IsReserved` is `Array.IndexOf` over a `static readonly string[]` of the five names.  The type's summary links the extension guide, `docs/architecture/generator.md#extending-query-methods`, and says the receiver is the connection.

- [ ] **Step 4: Make every reader read it**

- `QueryMethodWriter`: delete the five name constants, `GetArguments`, `AppendArguments`, `AppendCallArguments` and `Streams`; build `var arguments = MethodArguments.Of(method, tokens)` once in `Append` and pass it where `arguments` went; `AppendSql` takes `MethodArguments.SpellTokens(tokens)`.  The signature calls `AppendDeclaration`, the iterator `AppendIteratorDeclaration`, the return `AppendCall`.  `AppendDocumentation` writes one `<param>` per argument, the token form for a null `DocumentationXml`.  `AppendChecks` switches on `Role`, naming all six: `ThrowIfNull` for the receiver and the input, `ThrowIfNullOrWhiteSpace` for a token under `ValidateTokens`, nothing for the rest, each name through `AppendName`.  The body's names come from `MethodArguments.Connection` and the rest.
- `QueryMethodLocals`: takes `MethodArguments` in place of the tokens; `Seed` yields each argument's `Name`.  Its `QueryMethodWriter.VectorType` use is unchanged.
- `MethodPlanner.FindTokenProblems`: `MethodArguments.IsReserved(token.Name)`; delete `FixedArguments`.
- `MethodSignature.ParameterKeyOf`: iterates `MethodArguments.Of(method, MethodWriter.GetTokens(method.Segments))`, skipping the receiver and every argument with a `DefaultText`: the input's `TypeText` starts the key, and each other argument adds `,` and its `TypeText`; a `StringBuilder` only when one does, as today.  The `ParameterKey` `<param>` says the key is made of the arguments a call must give, beyond the receiver, because an argument with a default cannot tell two overloads apart.

- [ ] **Step 5: Run the tests that cover the change**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-namespace "SqlSource.Tests.Generation.*"` then `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generator.*"` and `dotnet test --project tests/SqlSource.Tests.RoslynFloor`.
Expected: PASS, the corpus unchanged (`git status` shows no file under `tests/SqlSource.Tests/Generator/Golden/`).

- [ ] **Step 6: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: PASS.

```bash
git add src/SqlSource/Generation tests/SqlSource.Tests/Generation/Methods
git commit -m "refactor: define a query method's arguments once"
```

### Task 2: Documentation

**Files:**
- Modify: `docs/architecture/generator.md` (`### Query methods`, `### Extending query methods`)
- Modify: `src/SqlSource/Generation/Emitting/AGENTS.md`
- Modify: `tests/AGENTS.md` (the zero-allocation test joins the list of budgets; trim wording to stay within 8,192 bytes)
- Modify: `docs/superpowers/plans/2026-11-02-query-method-argument-model.md` (the "After" row of Measurements, filled in by the controller)

- [ ] **Step 1: `### Query methods`**

Add a paragraph, **The arguments.**, before **A method.**: the model, its six roles in order, the three forms, who reads it (the docs, the checks, the locals' seed, SQLSRC275, `ParameterKey`), that it allocates nothing, and how `await` is written.  Correct every sentence the change makes wrong: step 2 of the plan ("a token named as one of the fixed arguments", now `MethodArguments.IsReserved`), **Across types** (the key is the arguments a call must give beyond the receiver), **A method that streams** (`QueryMethod.Streams`), and the paragraph on `LocalNames` (seeded with the method's arguments).

- [ ] **Step 2: `### Extending query methods`**

A short paragraph, as rules for the code as it is: an argument is added as a role and an entry of `MethodArguments`, never spelled in a writer; the receiver is the `Receiver` entry, the connection, and a receiver other than the connection changes that entry and copies its connection into a local once; a change to the fixed names changes which tokens SQLSRC275 refuses, which `docs/manual/diagnostics.md` documents; the key is built only from arguments a call must give.

- [ ] **Step 3: `Emitting/AGENTS.md`**

A rule: a query method's argument list comes only from `MethodArguments` of `Generation/Methods/MethodArguments.cs`; a writer never spells an argument's name, type or default; link `generator.md#query-methods`.

- [ ] **Step 4: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: PASS, including `AgentsFileTests` and the documentation link tests.

```bash
git add docs/architecture/generator.md src/SqlSource/Generation/Emitting/AGENTS.md tests/AGENTS.md
git commit -m "docs: describe the argument model of a query method"
```

## Measurements

Release build, `Stopwatch` loop over `QueryMethodWriter.Append` of four methods (`GetUser` on each engine, buffered and streamed) and `MethodSignature.Of` of a type holding them, 20,000 rounds after 5,000 of warm-up, the middle of three runs.  The measuring test is never committed.

| | Write, per method | Signature, per method |
|---|---|---|
| Before (`e158a5fe`) | 5.71 µs, 4,052 B | 0.178 µs, 488 B |
| After (`2d5f18ea`), the middle of six runs | 5.62 µs, 3,870 B | 0.202 µs, 488 B |
