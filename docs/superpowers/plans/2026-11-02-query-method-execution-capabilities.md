# Query-method execution capabilities and conversion kind Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The query-method writer asks a flag of `ExecutionCapabilities`, never an engine, and a conversion's way of converting is one `ConversionKind`, with every generated file and diagnostic byte-identical.

**Architecture:** One exhaustive switch over `DatabaseEngine`, in the output step and never cached, gives a method its `[Flags] ExecutionCapabilities` from its engine, shape and whether it streams.  The writer, the `<exception>` documentation and `QueryMethodLocals` read those flags, and the statements they turn on (the completed-transaction refusal, the rollback catch, the drain loops) move to one file, `ExecutionCapabilityWriter`.  `Conversion.IsSpatial` and `IsSerializer` become `ConversionKind Kind`, switched on once with no discard by `RowReaderWriter`, `ParameterBinder` and `SqlServerParameterBinder`.

**Tech Stack:** C# (`netstandard2.0` generator, `LangVersion` preview), xUnit v3 on Microsoft Testing Platform, Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-30-query-method-emission-refactor-design.md`, section "PR 4: execution capabilities", and its "Documentation" section for what PR 4 rewrites.

## Global Constraints

- Byte identity: `QueryMethodCorpusTests` passes unchanged in `tests/SqlSource.Tests` and `tests/SqlSource.Tests.RoslynFloor`; never run it with `SQLSOURCE_UPDATE_GOLDEN=1`.
- No switch expression over an enum under `src/SqlSource` has a `_ =>` arm; a `[Flags]` enum is never switched on (`src/SqlSource/AGENTS.md`).
- Every `DatabaseEngine` decision is a switch expression naming each member.
- `Conversion` is a cached record: keep value equality (an enum keeps it); `Caching*Tests` must still pass.
- Out of scope: method identity (PR 5), the context struct, phase files, `MethodHost` and the move to `Emitting/QueryMethods/` (PR 6).  `src/SqlSource/AGENTS.md` is at 8,191 of 8,192 bytes: do not add to it.
- The PR states the writer's time and allocation before and after (`src/SqlSource/AGENTS.md`).  Before, at `9b10d8dc`: 5.50 µs and 3,870 B per method written, the middle of three runs of unit 3's harness (four `GetUser` methods: each engine, buffered and streamed; 20,000 rounds after 5,000 warm-up; Release).  The harness stays in the scratchpad, never committed.

## Where the merged code differs from the spec

- The spec names the four flags; the writer today has five inline engine switches (2-engines' notes): the refusal, its `<exception>`, the drain, `ChecksTransaction` (the rollback catch and its `<exception>`) and `DrainsWhenLeftAtARow`.  All five become flag reads.
- The streamed loop's structure is keyed today on one bool, with the per-row rollback catch and the post-loop drain always inside it.  With independent flags, the loop declares a row local when `ChecksRollbackAtRead` or `DrainsWhenLeftAtARow` is set, wraps the row in the catch only under `ChecksRollbackAtRead`, drains after the loop under `DrainsLaterResults` and tracks `atRow` under `DrainsWhenLeftAtARow`.  Both combinations reached today (none, all four) write the same text as now.
- "Both binders" are the two that read the kind: `ParameterBinder.AppendCall` (the base class) and `SqlServerParameterBinder` (the `SqlBytes.Null`).  `PostgresParameterBinder` reads only `IsByElement`.
- `Generation/Emitting/` goes from 19 to 22 source files; PR 6 moves the query-method files out to `Emitting/QueryMethods/`.

## Review Focus

1. A future engine with a subset of the flags (MySQL might drain later results without the rollback check): the writer must still write valid C#.  Task 2 adds text tests of each single flag on a streamed method, through an internal overload of `Append` that takes the flags.
2. A SQL Server `None` or `RowCount` method: refuses a completed transaction and documents it, but never drains or catches.  Existing tests `Append_SqlServerShapeReadingNoRows_DoesNotCheckTheTransaction` and `Append_SqlServerEachShape_RefusesACompletedTransactionBeforeItOpensTheConnection` hold it; Task 2's `Of` test pins the flags.
3. A serializer conversion by element: the kind is `Serializer` with `IsByElement` set, read and bound as a converter's call.  The corpus' `json` case on PostgreSQL holds it.
4. The spatial read path's null on SQL Server is `SqlBytes.Null`, never `DBNull.Value`; held by the corpus' `spatial` case.
5. A token named `exception` or `atRow` still renames the local only on a method whose flags name it: held by `Append_SqlServerStreamsTokenNamedAtRow_RenamesTheFlag` and the awkward-name tests.

---

### Task 1: `ConversionKind`

**Files:**
- Create: `src/SqlSource/Generation/Methods/ConversionKind.cs`
- Modify: `src/SqlSource/Generation/Methods/Conversion.cs`, `ConversionPlanner.cs` (serializer ×2, spatial), `MethodPlanner.cs:542` (`ThrowsOnJsonNull`)
- Modify: `src/SqlSource/Generation/Emitting/RowReaderWriter.cs` (`AppendColumn`), `ParameterBinder.cs` (`AppendCall`), `SqlServerParameterBinder.cs:83`
- Modify: `tests/SqlSource.Tests/Generation/Methods/ConversionPlannerTests.cs` (named arguments and `IsSpatial` asserts)
- Modify: `docs/architecture/generator.md` (`Conversion.IsSpatial` at the reading paragraph, "with `IsSpatial`" and "with `IsSerializer` set" in the converters section)

**Interfaces:**
- Produces: `internal enum ConversionKind { Converter, Serializer, Spatial }` in `SqlSource.Generation.Methods`; `Conversion(..., SqlLibrary? Library = null, ConversionKind Kind = ConversionKind.Converter, bool MayReturnNull = false)` (the two bools removed, `Kind` in `IsSpatial`'s place).

- [ ] **Step 1:** Add the enum, each member documented: `Converter`, a converter's call `Method(value)`; `Serializer`, System.Text.Json's serializer with no `Library`; `Spatial`, SQL Server's spatial read path (move the `IsSpatial` doc there).  The type's summary says a new kind fails the build at each switch on it, and that `IsByElement` and `MayReturnNull` combine with a kind.
- [ ] **Step 2:** Replace the two record parameters and their docs with `Kind`; update the planner's three constructions and `ThrowsOnJsonNull` to `read is { Kind: ConversionKind.Serializer, IsByElement: false, MayReturnNull: true }`.
- [ ] **Step 3:** `RowReaderWriter.AppendColumn`: after the by-element branch, one switch on `read.Kind` (a `switch` statement, or an expression giving a bool) naming all three members with no discard, `Spatial` to `AppendSpatial`, `Converter` and `Serializer` to the call.  `ParameterBinder.AppendCall`: one switch on `write.Kind`, `Spatial` to the `SqlBytes` writer, the other two to `Method(value)`.  `SqlServerParameterBinder`: the null text from a switch expression on `bound.Write?.Kind`… use `bound.Write is { } write ? write.Kind switch { Spatial => SqlBytesNull, Converter or Serializer => DbNull } : DbNull`.
- [ ] **Step 4:** Update `ConversionPlannerTests` (`Kind: ConversionKind.Serializer`, `Kind: ConversionKind.Spatial`, `read!.Kind.ShouldBe(ConversionKind.Converter)`), and the generator.md names.
- [ ] **Step 5:** Run `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generator.QueryMethodCorpusTests"` and `--filter-class "*ConversionPlannerTests"`, `"*Caching*"`, `"*ParameterBinderTests"`.  Expected: all pass, no golden file changed (`git status`).
- [ ] **Step 6:** Commit `refactor(generator): one ConversionKind in place of IsSpatial and IsSerializer`.

### Task 2: `ExecutionCapabilities`

**Files:**
- Create: `src/SqlSource/Generation/Emitting/ExecutionCapabilities.cs` (the enum, exactly as the spec's code block, each member documented)
- Create: `src/SqlSource/Generation/Emitting/ExecutionCapabilityRules.cs`
- Create: `src/SqlSource/Generation/Emitting/ExecutionCapabilityWriter.cs`
- Modify: `src/SqlSource/Generation/Emitting/QueryMethodWriter.cs`, `QueryMethodLocals.cs`
- Test: `tests/SqlSource.Tests/Generation/Emitting/ExecutionCapabilityRulesTests.cs`, `QueryMethodWriterTests.cs`
- Docs: `docs/architecture/generator.md` (`### Query methods`: a **The capabilities.** paragraph after **A method.**'s list; rename the members it cites at steps 2 and 6, the streaming paragraph and **The exceptions and the drivers.**; `#### Adding a database engine` step 1 and a new step after the binder), `docs/tech-debt/TD-0090-*.md` (member names), `src/SqlSource/Generation/Emitting/AGENTS.md` (new section `## Engines in a query method`: the writer asks capabilities, never an engine, with a link).

**Interfaces:**
- Consumes: `QueryMethod.Engine`, `.Shape`, `.Streams`.
- Produces:
  - `internal static class ExecutionCapabilityRules` with `public static ExecutionCapabilities Of(QueryMethod method)` and `public static bool Has(this ExecutionCapabilities capabilities, ExecutionCapabilities flag)`.
  - `internal static class ExecutionCapabilityWriter`: `AppendDocumentation(StringBuilder, string indent, ExecutionCapabilities)`, `AppendCompletedTransactionCheck(StringBuilder, string indent, ExecutionCapabilities, string queryName)`, `AppendRollbackCheck(StringBuilder, string indent, string queryName, QueryMethodLocals)` (the catch), `AppendDrain(StringBuilder, string indent, ExecutionCapabilities, QueryMethodLocals)` (the loop and its blank line, under `DrainsLaterResults`), `AppendDrainLoop(StringBuilder, string indent, QueryMethodLocals, string token)`, `AppendLeftAtARowStart(StringBuilder, string indent, QueryMethodLocals)` and `AppendLeftAtARowEnd(StringBuilder, string indent, string loopIndent, QueryMethodLocals)`.
  - `QueryMethodLocals(MethodArguments arguments, MethodInput? input, MethodOutput? output, bool streams, ExecutionCapabilities capabilities)`: `AtRow` under `DrainsWhenLeftAtARow`, `Exception` under `ChecksRollbackAtRead`.
  - `QueryMethodWriter.Append(...)` unchanged; an `internal` overload with a trailing `ExecutionCapabilities capabilities` that the public one calls with `ExecutionCapabilityRules.Of(method)`, for the subset tests.

- [ ] **Step 1: Write the failing test** `ExecutionCapabilityRulesTests.Of_EachEngineShapeAndStreaming_GivesItsFlags`, a theory over engine × shape × collection: PostgreSQL gives `None` always; SQL Server gives `RefusesCompletedTransaction` for `None` and `RowCount`, `RefusesCompletedTransaction | DrainsLaterResults | ChecksRollbackAtRead` for `One`, `OneOptional` and a buffered `Many`, and all four for a `Many` of `IAsyncEnumerable`.  Plus `Has_EachFlag_IsTrueOnlyWhenSet`.
- [ ] **Step 2:** Run it: fails to compile.
- [ ] **Step 3:** Add the enum and `ExecutionCapabilityRules.Of`: `method.Engine switch { PostgreSql => None, SqlServer => RefusesCompletedTransaction | (reads rows ? DrainsLaterResults | ChecksRollbackAtRead : None) | (method.Streams ? DrainsWhenLeftAtARow : None) }`.  The long explanations now in the writer's comments on `DrainsWhenLeftAtARow`, `ChecksTransaction`, `AppendCompletedTransactionCheck`, `AppendDrain` and `AppendStream` move to the switch's comment, one per flag, written once; the comment points to `docs/architecture/generator.md`, "Adding a database engine".
- [ ] **Step 4:** Move the statements into `ExecutionCapabilityWriter` with the constants they use (`RolledBackType`, `RolledBackCref`, `CompletedTransactionType`, `NoCancellation`); the writer computes the flags once in `Append` and passes them to `QueryMethodLocals`, the documentation, the body and the execution.  The streamed loop follows "Where the merged code differs from the spec".  After this, the writer names `method.Engine` only for `ParameterBinder.For`.
- [ ] **Step 5: Subset tests** in `QueryMethodWriterTests`, through the internal overload on the `Stream` method: `Append_StreamsDrainingLaterResultsAlone_DrainsAfterTheLoopWithoutTrackingTheRow` (contains the `NextResultAsync(cancellationToken)` loop after the read loop, no `atRow`, no `catch`); `Append_StreamsCheckingRollbackAlone_CatchesAroundEachRowWithoutTrackingIt` (has the row local and the `catch`, no `atRow`, no `NextResultAsync`); `Append_StreamsDrainingWhenLeftAtARowAlone_TracksTheRowWithoutACatch` (has `atRow` and the `finally` drain, no `catch`).  Each also asserts the method has balanced braces: count of `{` equals count of `}`.
- [ ] **Step 6:** Run `ExecutionCapabilityRulesTests`, `QueryMethodWriterTests`, `QueryMethodCorpusTests` in both projects, `GeneratedSourceMethodTests`.  Expected: all pass, `git status` shows no golden file.
- [ ] **Step 7:** Update the docs listed above.
- [ ] **Step 8:** Commit `refactor(generator): the query-method writer asks execution capabilities, not engines`.

### Task 3: measure, validate, notes

- [ ] Copy the scratchpad harness `CapabilityMeasureTests.cs` into `tests/SqlSource.Tests/Generation/Emitting/`, run it three times in Release, delete it; record the middle run beside the before figure in the PR.
- [ ] Run `./format.sh -- <changed files>` (the repository's formatter), then `./pre-commit-validation.sh --full`.
- [ ] Write `4-capabilities-notes.md` in the shared notes folder: the new names, the file count, and the generator.md paragraphs PR 6 builds on.
