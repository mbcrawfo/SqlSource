# Pooled row buffer, phase 2: arrays and the read-only interfaces — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `many` method returning `T[]`, `IEnumerable<T>`, `IReadOnlyCollection<T>` or `IReadOnlyList<T>` reads its rows through a `file`-local buffer over `ArrayPool<object>.Shared`, unless the query has `no-array-pool`, which keeps today's text.

**Architecture:** A new `Generation/Emitting/RowBuffer.cs` decides which methods use the buffer, names it, and writes it.  `MethodEmitter` writes it once at namespace level in a methods file that has at least one method using it; `QueryMethodWriter` declares the buffer local and returns `ToArray()` for those methods.  Everything else is written as today.

**Tech Stack:** C# source generator (`netstandard2.0`, Roslyn 4.8.0 floor), generated C# 12 on .NET 8+, xunit + Shouldly, Npgsql and Microsoft.Data.SqlClient end-to-end projects.

**Spec:** `docs/superpowers/specs/2026-10-26-pooled-row-buffer-design.md` (Decisions, and Delivery → Phase 2).

## What the merged code says, where it differs from the spec

- Phase 1 merged as `GeneratorParameters.NoArrayPool = 64`, the word `no-array-pool`, `QuerySettings.UseArrayPool` and the positional `QueryMethod.UseArrayPool` right after `ValidateTokens`.  The flag is true for every shape and collection type unless `no-array-pool`, so the writer checks `Shape == Many` and the collection type itself.
- The attribute's invalid word is `SQLSRC006`, not `SQLSRC014` as the spec says.
- `ArrayPoolParameterTests.Run_NoArrayPool_LeavesTheGeneratedTextAsItIs` asserts identical text for every collection type; this phase narrows it to the six types the buffer does not cover and adds the four that now differ.

## Global Constraints

- The pool is `ArrayPool<object>.Shared`, shared by every row type; never `ArrayPool<T>`.
- The first buffer is 16 elements; each next one is double the last.  A buffer of up to 1,024 elements is rented; a larger one is `new object[n]`, never returned to the pool.
- No `try`/`finally` around the reads: a buffer rented when reading throws is left to the GC.
- A returned buffer is cleared up to the count first, so the pool keeps no row alive.
- The struct is `file`, generic in the row type with `where T : class`, at namespace level, and is named in the method from `global::` (`global::App.SqlSourceRowBuffer<global::App.Row>`), so no nested or same-named type of the user's captures it (proved by a prototype: a public `App.SqlSourceRowBuffer<T>` in another file and a nested one in the partial type both compile).
- An empty result is `global::System.Array.Empty<T>()`; a non-empty one an exact-size `T[]` filled by one copy through `MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<object, T>(ref MemoryMarshal.GetArrayDataReference(items)), count)`.
- Generated code uses nothing newer than C# 12 and must compile without a warning (`GeneratedCodeWarnings`) on the Roslyn floor too.
- `List<T>`, `IList<T>`, `ICollection<T>`, `ImmutableArray<T>`, `ImmutableList<T>`, `IImmutableList<T>`, the shapes `one`, `one-optional`, `none` and `rowcount`, and every method under `no-array-pool` keep today's text exactly.  A file with no method that uses the buffer is as it is today.
- Repository rules: root `AGENTS.md`, `src/SqlSource/AGENTS.md`, `tests/AGENTS.md`, `tests/SqlSource.Tests/Generator/AGENTS.md` (every file there starts with the `// Shared:` line, names end in `Tests.cs`, Roslyn 4.8.0 API only), `docs/manual/AGENTS.md`, `docs/architecture/AGENTS.md`.  Test names are `Method_Scenario_Outcome`, classes `sealed`, assertions Shouldly's, every generator run states its `OutputKind`.
- Every commit passes `./format.sh` then `./pre-commit-validation.sh` (needs Docker; long, run it in the background).

## Review Focus

1. A claimed type at the top of a methods file named `SqlSourceRowBuffer` (methods on the type) would clash with the struct in the same file and namespace: the buffer takes the other name, `SqlSourceRowBufferOfRows`, and the file compiles (test in Task 1).
2. A type in the global namespace, or under an escaped keyword namespace (`@class.Rows`), names the buffer `global::SqlSourceRowBuffer<...>` and `global::@class.Rows.SqlSourceRowBuffer<...>` and compiles (Task 1).
3. Two projects in the same namespace, one seeing the other's internals, both with pooled methods, compile with no CS0436 (Task 2).
4. A result that crosses the first buffer, a growth and the cap (0, 1, 16, 17, 1,024, 1,025 and 3,000 rows) keeps every row in order, on both engines, with and without `no-array-pool` (Tasks 3 and 4).
5. A token whose name is `rows`, `T`-like or the buffer's name does not hide the buffer: the local still comes from `LocalNames` and the type is written from `global::`; the existing awkward-token tests in a query method keep passing with the buffer in the file (Task 1, run them).

---

### Task 1: The buffer, and its use by the four collection types

**Files:**
- Create: `src/SqlSource/Generation/Emitting/RowBuffer.cs`
- Modify: `src/SqlSource/Generation/Emitting/QueryMethodWriter.cs` (`Append`, `GetBuffer`, `AppendExecution`)
- Modify: `src/SqlSource/Generation/Emitting/MethodEmitter.cs` (`Emit`)
- Modify: `src/SqlSource/AGENTS.md` only if a rule needs the new file named (for example beside the `LocalNames` rule: the buffer is named from `global::`)
- Create: `tests/SqlSource.Tests/Generator/RowBufferTests.cs`
- Create: `tests/SqlSource.Tests/Generation/Emitting/RowBufferTests.cs` (the `IsUsedBy` and `NameFor` rules, not shared with RoslynFloor)
- Modify: `tests/SqlSource.Tests/Generator/ArrayPoolParameterTests.cs`, `tests/SqlSource.Tests/Generator/MethodTests.cs`, `tests/SqlSource.Tests/Generator/ConverterMethodTests.cs`, `tests/SqlSource.Tests/Generation/Emitting/QueryMethodWriterTests.cs`, and any other test the new text breaks

**Interfaces:**
- Produces, in `internal static class RowBuffer` (namespace `SqlSource.Generation.Emitting`):
  - `public static bool IsUsedBy(QueryMethod method)`: `method.Shape == ResultShape.Many && method.UseArrayPool && method.Collection is CollectionKind.Array or IEnumerable or IReadOnlyCollection or IReadOnlyList`.  Phase 3 widens this list.
  - `public const string Name = "SqlSourceRowBuffer";` and `public const string OtherName = "SqlSourceRowBufferOfRows";`
  - `public static string NameFor(TypeMethods methods)`: `OtherName` when the placement is not `ExtensionClass` and `methods.Types[0].ValueName == Name`; otherwise `Name`.
  - `public static string TypeTextFor(TypeMethods methods)`: `"global::" + (NamespaceText.Length > 0 ? NamespaceText + "." : "") + NameFor(methods)`.
  - `public static void Append(StringBuilder builder, string indent, string name)`: the struct, exact text below, each line prefixed by `indent`, `\n` line endings.
- Changes: `QueryMethodWriter.Append(StringBuilder builder, string indent, QueryMethod method, string modifiers, bool isExtension, string rowBufferType)`; `rowBufferType` is `RowBuffer.TypeTextFor(methods)` and is read only when `RowBuffer.IsUsedBy(method)`.
- Text in a pooled method (only these two lines differ from today):
  - `var rows = new global::App.SqlSourceRowBuffer<global::App.GetUserDto>();` (the local still `locals.Rows`)
  - `return rows.ToArray();`
- Where the struct goes: after the last method's type closes and before the namespace closes, one blank line before it, at the namespace's depth (`TypeEmitter.GetIndent(namespace ? 1 : 0)`).  For a nested placement every type is closed first.

The struct, as written at depth 1 (`indent` = four spaces), `SqlSourceRowBuffer` replaced by `name`:

```csharp
    /// <summary>
    /// The rows of a query method, read into buffers from the shared array pool: one of up to 1,024 elements is
    /// rented and returned cleared, and a larger one is left to the garbage collector.
    /// </summary>
    file struct SqlSourceRowBuffer<T>
        where T : class
    {
        private object[]? items;

        private int count;

        public void Add(T row)
        {
            if (items is null || count == items.Length)
            {
                Grow();
            }

            items![count++] = row;
        }

        public T[] ToArray()
        {
            if (items is null)
            {
                return global::System.Array.Empty<T>();
            }

            var result = new T[count];
            global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(
                ref global::System.Runtime.CompilerServices.Unsafe.As<object, T>(
                    ref global::System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(items)),
                count).CopyTo(result);
            Return(items);
            items = null;
            count = 0;
            return result;
        }

        private void Grow()
        {
            var length = items is null ? 16 : items.Length * 2;
            var next = length <= 1024 ? global::System.Buffers.ArrayPool<object>.Shared.Rent(length) : new object[length];
            if (items is not null)
            {
                global::System.Array.Copy(items, next, count);
                Return(items);
            }

            items = next;
        }

        private void Return(object[] buffer)
        {
            if (buffer.Length <= 1024)
            {
                global::System.Array.Clear(buffer, 0, count);
                global::System.Buffers.ArrayPool<object>.Shared.Return(buffer);
            }
        }
    }
```

(The shared pool rents exactly 16 << k for these sizes, so `Length <= 1024` is the same as "rented".  `ToArray` tests `items is null`, not `count == 0`, since rows exist exactly when a buffer does.)

- [ ] **Step 1: Write the failing tests.**  In `tests/SqlSource.Tests/Generator/RowBufferTests.cs` (shared header line; harness `MethodRuns`/`ModelRuns`, `OutputKind.CodeGen` as `MethodRuns.Run` does; every run `ShouldBeClean()`, which includes no generated-code warning):
  - `Run_ManyMethodOfEachPooledType_ReadsItsRowsIntoTheBuffer(string collection)` for `Array`, `IEnumerable`, `IReadOnlyCollection`, `IReadOnlyList`: the methods file contains `"var rows = new global::App.SqlSourceRowBuffer<global::App.ListUsersDto>();\n"` and `"return rows.ToArray();\n"`, and contains `"file struct SqlSourceRowBuffer<T>\n"` exactly once.
  - `Run_ManyMethodOfEachPooledTypeUnderNoArrayPool_ReadsItsRowsIntoAList(string collection)`, same four: contains `"new global::System.Collections.Generic.List<global::App.ListUsersDto>()"` and `"return rows.ToArray();\n"`, and does not contain `"SqlSourceRowBuffer"`.
  - `Run_SeveralPooledMethods_WriteTheBufferOnce`: three `many` queries in one file (one each of `Array`, `IEnumerable`, `IReadOnlyList` by `-- collection-type:`) — the struct once, three `new global::App.SqlSourceRowBuffer<`.
  - `Run_FileWithNoPooledMethod_HasNoBuffer`: a `one`, a `rowcount` and a `List` `many` query — no `"SqlSourceRowBuffer"`.
  - `Run_FileMixingPooledAndNoArrayPoolMethods_PoolsOnlyThePooled`: two `many` array queries, the second with `-- generator: no-array-pool` inside the query — the struct once, one `SqlSourceRowBuffer<` local, one `List<` local.
  - `Run_PooledMethod_WritesTheBufferAfterTheClassInTheNamespace`: the whole methods file of one `ListUsers` array query `ShouldBe` the expected text (extension class, then a blank line, then the struct at four spaces, then `}`), pinning the struct's exact text above.
  - `Run_EachPlacement_CompilesWithTheBuffer(string placement)` for `ExtensionClass`, `Public`, `Internal`, `Private`, with a nested generic type case (`public partial class Outer<TKey> { public partial class Sample; }`): clean, and the struct's line is at the namespace's depth (four spaces).
  - `Run_TypeNamedLikeTheBuffer_NamesTheBufferOtherwise`: claimed type `public partial class SqlSourceRowBuffer<T>` with methods on the type (`MethodLocation = MethodLocation.Public`) — clean, contains `"file struct SqlSourceRowBufferOfRows<T>\n"` and `"new global::App.SqlSourceRowBufferOfRows<"`.
  - `Run_TypeInTheGlobalNamespaceOrUnderAnEscapedKeyword_NamesTheBufferFromGlobal(string namespaceLine, string buffer)`: `("", "global::SqlSourceRowBuffer<")`, `("namespace @class.Rows;\n\n", "global::@class.Rows.SqlSourceRowBuffer<")` — clean and contains `buffer`.
  In `tests/SqlSource.Tests/Generation/Emitting/RowBufferTests.cs`: `IsUsedBy_EachShapeCollectionAndFlag_IsTrueOnlyForAPooledManyOfTheFourTypes` (every `ResultShape` × every `CollectionKind` × `UseArrayPool` true/false, from `QueryMethodWriterTests`' `GetUser` shape), and `NameFor_*` cases for an extension class whose type is named `SqlSourceRowBuffer` (keeps `Name`) and a type placement of it (gives `OtherName`).
  Update `ArrayPoolParameterTests`: `Run_NoArrayPool_LeavesTheGeneratedTextAsItIs` keeps only `ICollection`, `IList`, `List`, `ImmutableArray`, `ImmutableList`, `IImmutableList`, and also asserts the pooled text has no `SqlSourceRowBuffer`; add `Run_NoArrayPool_GivesTodaysTextForThePooledTypes` for the four, asserting the texts differ, the unpooled one has `List<` and no `SqlSourceRowBuffer`; update the class comment (the flag now changes the text).  Update the expected text of `MethodTests.Run_CodeGenQuery_IsAnExtensionMethodOfTheTypesExtensionClass`, `ConverterMethodTests` (line ~206) and the four pooled rows of `QueryMethodWriterTests.Append_EachCollectionType_BuffersAndMaterialisesAsDecided` (add a `useArrayPool` column or a sibling theory for `UseArrayPool = false` giving the `List` text).

- [ ] **Step 2: Run them to see them fail.**
  Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~RowBuffer|FullyQualifiedName~ArrayPoolParameter"`
  Expected: FAIL (no `RowBuffer` type / text still `List`).

- [ ] **Step 3: Implement `RowBuffer`, and its use in `QueryMethodWriter.GetBuffer` and `MethodEmitter.Emit`.**  `GetBuffer` gains the `rowBufferType` and returns `("new " + rowBufferType + "<" + row + ">()", rows + ".ToArray()")` when `RowBuffer.IsUsedBy(method)`; every other case is unchanged.  `MethodEmitter.Emit` computes `kept.Any(RowBuffer.IsUsedBy)` once, passes `RowBuffer.TypeTextFor(methods)` to every `QueryMethodWriter.Append`, and closes the types down to the namespace's depth before appending `'\n'` and the struct.  Update every other caller of `QueryMethodWriter.Append` (tests) to pass a buffer type.

- [ ] **Step 4: Run the generator tests on both Roslyns.**
  Run: `dotnet test tests/SqlSource.Tests && dotnet test tests/SqlSource.Tests.RoslynFloor`
  Expected: PASS, including `Run_TokenWithAnAwkwardNameInAQueryMethod_Compiles` and its `CompilesOnTheNewestCSharp` sibling.

- [ ] **Step 5: Format, validate and commit.**
  Run: `./format.sh && ./pre-commit-validation.sh` — every step `passed`.
  `git commit -m "Read the rows of an array or a read-only interface through a pooled buffer"`

### Task 2: Two projects that share internals

**Files:**
- Modify: `tests/SqlSource.Tests/Generator/RowBufferTests.cs` (or `AttributeConflictTests.cs`, beside its other two-project tests)
- Modify, only if the harness cannot build a second project at `CodeGen` with sidecar entries: `tests/SqlSource.Testing/Generator/GeneratorHarness.cs`

**Interfaces:**
- Consumes: Task 1's generated text.  `GeneratorHarness.BuildAsync(OutputKind, source, files, otherProjects, packageAnalyzers)` builds a project referencing others, as `AttributeConflictTests` does.

- [ ] **Step 1: Write the failing-if-broken test** `Build_TwoProjectsSharingInternalsWithPooledMethods_HaveNoConflict`: two projects, both with type `App.Sample` (same namespace) at `CodeGen` and an array `many` query with its entry, the referenced one with `[assembly: InternalsVisibleTo("TestAssembly")]`; build with `packageAnalyzers: false` (so no suppressor hides a CS0436) and assert no diagnostic at all, and that both methods files contain `file struct SqlSourceRowBuffer<T>`.
- [ ] **Step 2: Run it.**  `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~TwoProjectsSharingInternals"` — PASS (it guards against a later change to a non-`file` helper; if it fails, the struct is not `file`).
- [ ] **Step 3: Run on the floor.**  `dotnet test tests/SqlSource.Tests.RoslynFloor --filter "FullyQualifiedName~TwoProjectsSharingInternals"` — PASS.
- [ ] **Step 4: Format, validate and commit.**  `./format.sh && ./pre-commit-validation.sh`; `git commit -m "Prove that two projects sharing internals compile their row buffers"`

### Task 3: End to end on PostgreSQL

**Files:**
- Create: `tests/SqlSource.Tests.Postgres/EndToEnd/Queries/RowBuffers.sql` and its sidecar `RowBuffers.sql.json` (written by `tools/describe-end-to-end.sh` alone, its diff read before commit)
- Create: `tests/SqlSource.Tests.Postgres/EndToEnd/RowBufferQueries.cs`, `tests/SqlSource.Tests.Postgres/EndToEnd/RowBufferQueryTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/EndToEnd/AllocationTests.cs` (comments only), `tests/SqlSource.Tests.Postgres/README.md` if it lists the end-to-end files, `tests/AGENTS.md` only if a list there must name the new files

**Interfaces:**
- Queries, eight, sharing `-- output-model: CountedNumber`, each `SELECT n FROM generate_series(1, @count) AS n ORDER BY n;`: `NumbersAsArray`, `NumbersAsIEnumerable`, `NumbersAsIReadOnlyCollection`, `NumbersAsIReadOnlyList` (by `-- collection-type:`), and the same four with `NoPool` after `Numbers` (`NumbersNoPoolAsArray`, ...) with `-- generator: no-array-pool` inside the query.  `[SqlSourceGenerate(Path = "Queries/RowBuffers.sql")] internal static partial class RowBufferQueries;` gives extension methods.

- [ ] **Step 1: Write the queries and the class; run `tools/describe-end-to-end.sh`** (read its usage first) to write the sidecar; read the diff.
- [ ] **Step 2: Write `RowBufferQueryTests`** (`[Collection(UsesPostgres.Name)]`, data source from `EndToEndSources.App(postgres)`), one theory per method pair or one theory over a method selector: for each count in `0, 1, 16, 17, 1024, 1025, 3000`, the result's `n` values are `1..count` in order (`Enumerable.Range(1, count)`), the pooled and unpooled methods give equal rows, and the runtime type is `CountedNumber[]` for all four types (the interfaces are still arrays).  Mix open and closed connections as `CollectionQueryTests` does.
- [ ] **Step 3: Update the comments of `AllocationTests`** where they say the hand-written method makes the generated method's calls or builds the same collection: the generated `many` method now reads through the pooled buffer and allocates less, so the margin holds; the hand-written method stays `List` + `ToArray()`, which is what the test compares with (phase 3 adds the test that the pool allocates less).  No number changes.
- [ ] **Step 4: Run.**  `dotnet test tests/SqlSource.Tests.Postgres --filter "FullyQualifiedName~RowBuffer|FullyQualifiedName~AllocationTests|FullyQualifiedName~CheckTests"` — PASS.  Then on the floor image: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test tests/SqlSource.Tests.Postgres --filter "FullyQualifiedName~RowBuffer|FullyQualifiedName~CheckTests"` — PASS.
- [ ] **Step 5: Format, validate and commit.**  `./format.sh && ./pre-commit-validation.sh`; `git commit -m "Run the pooled and unpooled array methods end to end on PostgreSQL"`

### Task 4: End to end on SQL Server

**Files:**
- Create: `tests/SqlSource.Tests.SqlServer/EndToEnd/Queries/RowBuffers.sql` and sidecar (by `tools/describe-end-to-end.sh`)
- Create: `tests/SqlSource.Tests.SqlServer/EndToEnd/RowBufferQueries.cs`, `tests/SqlSource.Tests.SqlServer/EndToEnd/RowBufferQueryTests.cs`
- Modify: `tests/SqlSource.Tests.SqlServer/EndToEnd/AllocationTests.cs` (comments only), `tests/SqlSource.Tests.SqlServer/README.md` if it lists the files

**Interfaces:**
- The same eight query names and `CountedNumber` model as Task 3.  The SQL runs on SQL Server 2017 and describes the same on every major (no `GENERATE_SERIES`): a digits table from `VALUES (0), ..., (9)` cross-joined four times, `d1.v + 10 * d2.v + 100 * d3.v + 1000 * d4.v + 1 AS n`, filtered by `n <= @count` and ordered by `n`; declare `@count` with `-- param:` if its described type differs between majors.

- [ ] **Step 1: Write the queries and class; describe with `tools/describe-end-to-end.sh`; read the diff.**
- [ ] **Step 2: Write `RowBufferQueryTests`** (`[Collection(UsesSqlServer.Name)]`, connections from `EndToEndSources`), the same counts and assertions as Task 3.
- [ ] **Step 3: Update `AllocationTests`' comments** as in Task 3 (its header says the hand-written method builds "the same rows in the same collection ... by copying a List": say the generated one now reads through the pooled buffer and allocates less, and the hand-written one is today's `List` + `ToArray()`).
- [ ] **Step 4: Run.**  `dotnet test tests/SqlSource.Tests.SqlServer --filter "FullyQualifiedName~RowBuffer|FullyQualifiedName~AllocationTests|FullyQualifiedName~CheckTests"`, then the same with `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` (without `AllocationTests`) — PASS.
- [ ] **Step 5: Format, validate and commit.**  `git commit -m "Run the pooled and unpooled array methods end to end on SQL Server"`

### Task 5: Documentation

**Files:**
- Modify: `docs/manual/settings.md` (Generator parameters row's Status; the `no-array-pool` row of the parameter table)
- Modify: `docs/manual/README.md` ("What works today")
- Modify: `docs/manual/generator.md` ("What each shape returns", `many`)
- Modify: `docs/architecture/generator.md` ("The settings have two stages", step 4 of the method plan, "Query methods")

- [ ] **Step 1: `settings.md`.**  Status of the Generator parameters row: `Works today`.  The `no-array-pool` row's Effect, replacing the callout: a `many` method reads its rows into buffers borrowed from .NET's shared array pool, and for a result of up to about 1,000 rows allocates only the result; with `no-array-pool` it reads them into a `List<T>` it allocates, as hand-written code usually does — for a project that keeps the pool out, or to compare.  It covers an array, `IEnumerable<T>`, `IReadOnlyCollection<T>` and `IReadOnlyList<T>`; then the callout `**Coming in a later release.**  `List<T>`, `IList<T>`, `ICollection<T>` and `ImmutableArray<T>` will read their rows through the pool too.  See [what works today](README.md#what-works-today).` (no leading `>` in a table cell, as phase 1 did).
- [ ] **Step 2: `README.md`.**  Remove the row "The generator parameter `no-array-pool`" and, if no other row is "Accepted; no effect yet", the sentence beneath the table that explains that status.  Add `no-array-pool` to the query methods' settings row beside `async-method-suffix` ("the generator parameters `async-method-suffix` and `no-array-pool`").  Add a row "Reading the rows of a `List<T>`, `IList<T>`, `ICollection<T>` or `ImmutableArray<T>` through the shared array pool" — Coming — linking to the parameter.  Follow `docs/manual/AGENTS.md` for the status words.
- [ ] **Step 3: `generator.md`.**  One sentence under `many`: it reads the rows into buffers from .NET's shared array pool and allocates only the result for up to about 1,000 rows, unless the query has [`no-array-pool`](settings.md#setting-generator-parameters).
- [ ] **Step 4: `docs/architecture/generator.md`.**  Drop "does not read yet" from step 4; in "Query methods" describe `RowBuffer`: which methods use it (`IsUsedBy`), that `MethodEmitter` writes the `file` struct once at namespace level in a file that has one, its name and the other name, why `file` (TD-0006) and why `object[]` from one pool, the 16-element start, doubling, the 1,024 cap, the exact-size result through `MemoryMarshal`, the clearing, and that a throw leaves the buffer to the GC.
- [ ] **Step 5: Run the documentation tests and validate.**  `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Documentation"` — PASS; `./format.sh && ./pre-commit-validation.sh`; `git commit -m "Document the pooled row buffer of arrays and the read-only interfaces"`
