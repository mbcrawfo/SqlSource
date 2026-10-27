# Pooled row buffer, phase 3: lists, `ImmutableArray`, and the close

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `many` method returning `List<T>`, `IList<T>`, `ICollection<T>` or `ImmutableArray<T>` reads its rows through the pooled row buffer unless its query has `no-array-pool`, an allocation test on each engine shows a pooled method of a few hundred rows allocating less than hand-written `List` + `ToArray()`, and the manual pages this run touched are read whole and true.

**Architecture:** Phase 2's `file struct SqlSourceRowBuffer<T>` (`src/SqlSource/Generation/Emitting/RowBuffer.cs`) gains `ToList()` and `ToImmutableArray()` beside `ToArray()`, sharing two private helpers.  `RowBuffer.IsUsedBy` widens to the four types, and `QueryMethodWriter.GetBuffer` picks the result member by collection type.  `ImmutableList<T>` and `IImmutableList<T>` keep their builder.

**Tech Stack:** C# source generator (netstandard2.0, Roslyn 4.8.0 floor), generated code for .NET 8+ / C# 12, xunit + Shouldly, PostgreSQL and SQL Server in Docker.

**Spec:** `docs/superpowers/specs/2026-10-26-pooled-row-buffer-design.md` (Decisions; Delivery, Phase 3).  Hand-over facts from phases 1 and 2 are in the implementation manager's `notes.md` and the shared notes `2-arrays-notes.md`; this plan copies what it needs.

## Where the merged code differs from the spec

- Phase 2's struct has `Add` and `ToArray` and is written whole whenever a method of the file uses it.  **Decision: the struct stays one text, written whole.**  Writing each member only when a method of the file uses it would need four variants of the generated text, of `GeneratedFile.RowBuffer`, of `RowBufferByHand` and of the drift test that holds the two together, to save some 25 identical lines in a file that has a pooled method.  The spec leaves the members to the builder.
- The allocation tests have two pairs per engine since phase 2 (owner decision R1-F1): a pooled generated method against a hand-written method reading into `RowBufferByHand`, and its `no-array-pool` twin against the old hand-written collection.  This phase adds both pairs for a method of each of its four types, and the spec's own "pooled allocates less than `List` + `ToArray()`" test.
- `CallAllocations.MeasureAsync<T>` takes two delegates of one result type.  The generated `IList`/`ICollection` methods return those interfaces and the hand-written one a `List<T>`, so it gains a second type parameter rather than a hand-written method per interface.

## Global Constraints

- The pool is `ArrayPool<object>.Shared`; buffers above 1,024 elements are plain arrays never returned to the pool.  Unchanged by this phase.
- Generated code uses nothing newer than C# 12 and names framework types from `global::`.  `CollectionsMarshal.SetCount` and `ImmutableCollectionsMarshal.AsImmutableArray` are .NET 8.
- Callers see the same types and contents as today: `List<T>`, `IList<T>` and `ICollection<T>` return a `List<T>`; `ImmutableArray<T>` an `ImmutableArray<T>`, `ImmutableArray<T>.Empty` for no rows, never `default`.
- `no-array-pool` gives today's text, unchanged, for every collection type.
- A compiler warning inside generated code is a bug (`GeneratorRun.GeneratedCodeWarnings`).
- Allocation margins are never raised to make a change pass.  If a new pair does not fit the existing margin, stop and report: it is an owner question.
- Every commit passes `./pre-commit-validation.sh`, after `./format.sh`.  A change to `tests/SqlSource.Tests.Postgres` or `tests/SqlSource.Tests.SqlServer` also runs that project on its floor image (`SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`, `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`).
- Sidecars of `EndToEnd/Queries/` are written by `tools/describe-end-to-end.sh` alone, never by hand.
- Test names are `Method_Scenario_Outcome`, `Method` being what the test calls; test classes are `sealed`; files in `tests/SqlSource.Tests/Generator/` start with the shared line.
- User docs describe effects, never internals (`docs/manual/AGENTS.md`).

## Review Focus

1. **An empty `ImmutableArray<T>`** must be `ImmutableArray<T>.Empty`, not `default` (whose `IsEmpty` throws).  Task 2 and 3's end-to-end tests assert `IsDefault` is false at count 0.
2. **A returned `List<T>` must be the caller's alone**: adding to it or clearing it changes nothing the next call returns, and its `Count` is the row count.  Task 2 and 3 assert `Capacity == count` for the pooled list and that a second call after mutating the first result returns the right rows.
3. **`IList<T>` and `ICollection<T>` still return a `List<T>`**, as today, not an array a caller could not add to.  Task 1's generator text and Tasks 2 and 3's `ShouldBeOfType<List<CountedNumber>>()`.
4. **A result past the cap** (1,025 and 3,000 rows) for a list and an immutable array: the end-to-end counts cover it.
5. **A file mixing a pooled list method and a pooled array method** writes the buffer once and compiles with no warning: Task 1's `Run_MethodOfEveryPooledType_CompilesWithOneBufferAndNoWarning`.

---

### Task 1: The buffer's list and immutable-array results

**Files:**
- Modify: `src/SqlSource/Generation/Emitting/RowBuffer.cs` (the `Body` lines, `IsUsedBy`, the type's summary)
- Modify: `src/SqlSource/Generation/Emitting/QueryMethodWriter.cs` (`GetBuffer`)
- Modify: `src/SqlSource/Generation/Methods/QueryMethod.cs` (the `UseArrayPool` doc, if it names types)
- Modify: `tests/SqlSource.Testing/Generator/GeneratorHarness.cs` (`GeneratedFile.RowBuffer`)
- Modify: `tests/SqlSource.Testing/EndToEnd/RowBufferByHand.cs` (body and header comment)
- Test: `tests/SqlSource.Tests/Generation/Emitting/RowBufferTests.cs`, `tests/SqlSource.Tests/Generator/RowBufferTests.cs`, `tests/SqlSource.Tests/Generator/ArrayPoolParameterTests.cs`, and any of `QueryMethodWriterTests`, `MethodTests`, `ConverterMethodTests` whose pinned text changes

**Interfaces:**
- Produces: the generated struct's public members `void Add(T row)`, `T[] ToArray()`, `global::System.Collections.Generic.List<T> ToList()`, `global::System.Collections.Immutable.ImmutableArray<T> ToImmutableArray()`; the same members on `RowBufferByHand<T>`, which Tasks 2 and 3 call.

- [ ] **Step 1: Write the failing tests**
  - `RowBufferTests.IsUsedBy_EachShapeCollectionAndFlag_IsTrueOnlyForAPooledManyOfTheFourTypes` (Emitting) becomes `..._IsTrueOnlyForAPooledManyOfAllButTheImmutableLists`: true exactly for `Many`, `UseArrayPool`, and `Array`, `IEnumerable`, `IReadOnlyCollection`, `IReadOnlyList`, `List`, `IList`, `ICollection`, `ImmutableArray`.
  - Generator `RowBufferTests.Run_ManyMethodOfEachPooledType_ReadsItsRowsIntoTheBuffer`: add `List`, `IList`, `ICollection`, `ImmutableArray` rows; the method's buffer is `new global::<ns>.SqlSourceRowBuffer<Row>()` and its result `rows.ToList()` for the three list types and `rows.ToImmutableArray()` for `ImmutableArray` (keep the test's existing way of asserting; give it the expected result member per row).
  - `Run_ManyMethodOfEachPooledTypeUnderNoArrayPool_ReadsItsRowsIntoAList`: add the four with today's text (`new List<Row>()` returned as is; `ImmutableArray.CreateBuilder<Row>()` and `DrainToImmutable()`); rename if "into a List" stops being true for `ImmutableArray` (`..._ReadsItsRowsAsBeforeThePool`).
  - New `Run_MethodOfEveryPooledType_CompilesWithOneBufferAndNoWarning`: one file with a `many` query per pooled type (eight), compiled; `ShouldBeClean`, no `GeneratedCodeWarnings`, the buffer text once.
  - `ArrayPoolParameterTests.Run_NoArrayPool_LeavesTheGeneratedTextAsItIs` keeps only `ImmutableList` and `IImmutableList`; `Run_NoArrayPool_GivesTodaysTextForThePooledTypes` gains `List`, `IList`, `ICollection`, `ImmutableArray`.
  - `GeneratedFile.RowBuffer` is the new struct text (Step 3); `Append_HandWrittenCopyOfTheAllocationTests_IsTheGeneratedBufferButItsNameModifierAndDepth` then fails until `RowBufferByHand` matches.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~RowBuffer|FullyQualifiedName~ArrayPoolParameter"`
Expected: the new rows and the drift test fail.

- [ ] **Step 3: Implement**
  - `RowBuffer.IsUsedBy`: `Shape == Many && UseArrayPool && Collection is` the eight kinds (list them, so a new kind is off by default).  Update its summary and the type's summary ("when it returns an array, a list or an immutable array, or one of their interfaces", or similar; it no longer says "an array or a read-only interface").
  - `QueryMethodWriter.GetBuffer`, when `IsUsedBy`: buffer `new <rowBufferType><Row>()`; result `rows.ToList()` for `List`/`IList`/`ICollection`, `rows.ToImmutableArray()` for `ImmutableArray`, `rows.ToArray()` otherwise.  The `no-array-pool` switch stays as it is.
  - `RowBuffer.Body`: after `Add`, these members, then `Grow` and `Return` unchanged.  This is the generated text (the spec leaves it to the builder; this plan fixes it so that `GeneratedFile.RowBuffer` and `RowBufferByHand` can be written from it):

```csharp
    public T[] ToArray()
    {
        if (items is null)
        {
            return global::System.Array.Empty<T>();
        }

        var result = new T[count];
        Rows().CopyTo(result);
        Release();
        return result;
    }

    public global::System.Collections.Generic.List<T> ToList()
    {
        if (items is null)
        {
            return new global::System.Collections.Generic.List<T>();
        }

        var result = new global::System.Collections.Generic.List<T>(count);
        global::System.Runtime.InteropServices.CollectionsMarshal.SetCount(result, count);
        Rows().CopyTo(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(result));
        Release();
        return result;
    }

    public global::System.Collections.Immutable.ImmutableArray<T> ToImmutableArray()
    {
        if (items is null)
        {
            return global::System.Collections.Immutable.ImmutableArray<T>.Empty;
        }

        return global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(ToArray());
    }

    private global::System.ReadOnlySpan<T> Rows() =>
        global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(
            ref global::System.Runtime.CompilerServices.Unsafe.As<object, T>(
                ref global::System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(items!)),
            count);

    private void Release()
    {
        Return(items!);
        items = null;
        count = 0;
    }
```

  - `GeneratedFile.RowBuffer` and `RowBufferByHand<T>`: the same text (split long lines in the harness as the `Grow` line is).  `RowBufferByHand`'s header comment says it is the buffer of every pooled collection type, not "of an array or a read-only interface"; add any IDE rule the new members trip to its `#pragma` list, with the reason in the comment.

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~RowBuffer|FullyQualifiedName~ArrayPoolParameter|FullyQualifiedName~QueryMethodWriter|FullyQualifiedName~MethodTests"`
Expected: PASS.  Fix any other pinned text that changes (a test pinning the struct reads `GeneratedFile.RowBuffer`).

- [ ] **Step 5: Validate and commit**

Run `./format.sh`, then `./pre-commit-validation.sh` (in the background; it needs Docker).  Expected: passes.
Commit: "Read the rows of a list or an immutable array through the pooled buffer".

### Task 2: PostgreSQL end to end and allocations

**Files:**
- Modify: `tests/SqlSource.Tests.Postgres/EndToEnd/Queries/RowBuffers.sql` (and its sidecar `.sql.json`, by `tools/describe-end-to-end.sh`)
- Modify: `tests/SqlSource.Tests.Postgres/EndToEnd/RowBufferQueries.cs` (comment), `RowBufferQueryTests.cs`
- Modify: `tests/SqlSource.Testing/EndToEnd/CallAllocations.cs`
- Modify: `tests/SqlSource.Tests.Postgres/EndToEnd/AllocationTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/README.md` (the `RowBuffers.sql` row and the `AllocationTests` item)

**Interfaces:**
- Consumes: `RowBufferByHand<T>.ToList()` / `.ToImmutableArray()` (Task 1).
- Produces: `CallAllocations.MeasureAsync<TGenerated, TByHand>(Func<Task<TGenerated>> generated, Func<Task<TByHand>> byHand, int warmUpCalls, int pairs)`, which Task 3 uses; the measuring is unchanged (no allocation of its own per call; keep the alternating order and the per-call `GC.GetTotalAllocatedBytes(precise: true)`).

- [ ] **Step 1: The queries.**  In `RowBuffers.sql` add `NumbersAsList`, `NumbersAsIList`, `NumbersAsICollection`, `NumbersAsImmutableArray` and `NumbersNoPoolAsList`, `NumbersNoPoolAsIList`, `NumbersNoPoolAsICollection`, `NumbersNoPoolAsImmutableArray`, the pattern of the existing eight (`-- collection-type:`, `-- generator: no-array-pool` on the twins, `-- output-model: CountedNumber`, the same SQL).  The file's header says "each collection type that reads its rows through the pooled buffer", the eight, and their eight twins.  Run `tools/describe-end-to-end.sh` (see `tools/README.md` for its arguments) and read the sidecar's diff: only the eight new entries.

- [ ] **Step 2: The end-to-end tests.**  In `RowBufferQueryTests` add `NumbersAsList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`, `NumbersAsIList_...`, `NumbersAsICollection_...`, `NumbersAsImmutableArray_...` over `Counts`.  The shared helper takes the expected runtime type (make it generic in it, or add a parameter), so that:
  - the list types: both results `ShouldBeOfType<List<CountedNumber>>()`, equal to `1..count`; the pooled list's `Capacity` is `count`; then the test clears the pooled list, calls the pooled method again and gets `1..count` again.
  - `ImmutableArray`: both `ShouldBeOfType<ImmutableArray<CountedNumber>>()` (boxed), `IsDefault` false, equal to `1..count`.
  - the existing four still assert `CountedNumber[]`.
  Update the class comment to name the eight types.

- [ ] **Step 3: `CallAllocations`.**  Give `MeasureAsync` the two type parameters above; the existing callers compile unchanged by inference.  Update its comment if it names one type.

- [ ] **Step 4: The pairs.**  In `AllocationTests` add, for the generated methods of `RowBufferQueries` at a count of 3 (`CountedRows`, a constant, the same rows as the existing tests' three):
  - `NumbersAsList_PerCallThroughThePool_AllocatesNoMoreThanHandWritten`, `NumbersAsIList_...`, `NumbersAsICollection_...` against `NumbersAsListPooledByHand` (rows into `RowBufferByHand<CountedNumber>`, `ToList()`); `NumbersAsImmutableArray_PerCallThroughThePool_...` against `NumbersAsImmutableArrayPooledByHand` (`ToImmutableArray()`).
  - `NumbersNoPoolAsList_PerCall_AllocatesNoMoreThanHandWritten`, `NumbersNoPoolAsIList_...`, `NumbersNoPoolAsICollection_...` against `NumbersAsListUnpooledByHand` (rows into `new List<CountedNumber>()`, returned); `NumbersNoPoolAsImmutableArray_PerCall_...` against `NumbersAsImmutableArrayUnpooledByHand` (`ImmutableArray.CreateBuilder<CountedNumber>()`, `DrainToImmutable()`).
  - Each asserts what the existing tests do: both > 0 bytes and `GeneratedBeyondByHand <= MarginInBytesPerCall` (64, unchanged).
  - The hand-written methods make the generated method's calls: read the generated `NumbersAs*` methods (build with `-p:EmitCompilerGeneratedFiles=true`, or the generator test harness) for the SQL text, the binding of `@count` and the read of `n` (`IsDBNull` then `GetFieldValue<int>`), and write them as the existing hand-written methods are.  The SQL is a constant beside the others.
  - Add the measured differences of the new pairs to `MarginInBytesPerCall`'s comment, as the existing ones are.

- [ ] **Step 5: The spec's test.**  `NumbersAsArray_PerCallOfAFewHundredRows_AllocatesLessThanAListCopiedToAnArray`: the generated pooled `NumbersAsArray` at `FewHundredRows = 300` against `NumbersAsArrayUnpooledByHand` (rows into a `List`, then `ToArray()`, as the hand-written unpooled methods do), asserting both > 0 and `GeneratedBeyondByHand <= -LeastSavingInBytesPerCall`.  Measure first (write the pairs to the output and run it several times), then set `LeastSavingInBytesPerCall` below the commonest difference you measure and say in its comment what was measured and why that number: the list's growth arrays for 300 rows (4 to 512 elements) are about 8.4 KB, so a pooled method that allocated even its largest growth array (512 elements, about 4.1 KB) must fail.

- [ ] **Step 6: Readme.**  `tests/SqlSource.Tests.Postgres/README.md`: the `RowBuffers.sql` row names every pooled type, not "the array and the three read-only interfaces"; the `AllocationTests` item names the numbers pairs and the few-hundred-rows test.

- [ ] **Step 7: Run, validate, commit**

Run: `dotnet test tests/SqlSource.Tests.Postgres --filter "FullyQualifiedName~RowBufferQueryTests|FullyQualifiedName~AllocationTests|FullyQualifiedName~CheckTests"`, then the same project on `postgres:14.24`, then `./format.sh` and `./pre-commit-validation.sh`.  Expected: all pass.
Commit: "Prove the pooled lists and immutable arrays end to end on PostgreSQL, and that a pooled method allocates less".

### Task 3: SQL Server end to end and allocations

**Files:**
- Modify: `tests/SqlSource.Tests.SqlServer/EndToEnd/Queries/RowBuffers.sql` (+ sidecar by `tools/describe-end-to-end.sh`), `RowBufferQueries.cs` (comment), `RowBufferQueryTests.cs`, `AllocationTests.cs`
- Modify: `tests/SqlSource.Tests.SqlServer/README.md`

**Interfaces:**
- Consumes: `CallAllocations.MeasureAsync<TGenerated, TByHand>` (Task 2), `RowBufferByHand<T>.ToList()` / `.ToImmutableArray()` (Task 1).

- [ ] **Step 1: The queries**, as Task 2 Step 1, with the file's digits SQL.
- [ ] **Step 2: The end-to-end tests**, as Task 2 Step 2.
- [ ] **Step 3: The pairs**, as Task 2 Step 4, in this file's pattern: theories over `open` (`[InlineData(false)]`, `[InlineData(true)]`), named `NumbersAsList_ManyCallsThroughThePool_AllocatesNoMoreThanByHand(bool open)` and `NumbersNoPoolAsList_ManyCalls_AllocatesNoMoreThanByHand(bool open)` and so on for `IList`, `ICollection`, `ImmutableArray`; hand-written methods named as in Task 2 with the `Async` suffix this file uses (`NumbersAsListPooledByHandAsync`); asserting `> LeastBytesPerCall` and `<= MarginInBytesPerCall` (16, unchanged).  Bind `@count` as the generated method does (read it).
- [ ] **Step 4: The spec's test**, `NumbersAsArray_ManyCallsOfAFewHundredRows_AllocatesLessThanAListCopiedToAnArray(bool open)`, as Task 2 Step 5, with its own `LeastSavingInBytesPerCall` set from what you measure on this engine.
- [ ] **Step 5: Readme**, as Task 2 Step 6 (`RowBuffers.sql` row, `AllocationTests` item).
- [ ] **Step 6: Run, validate, commit**

Run: `dotnet test tests/SqlSource.Tests.SqlServer --filter "FullyQualifiedName~RowBufferQueryTests|FullyQualifiedName~AllocationTests|FullyQualifiedName~CheckTests"`, then on the 2017 floor image, then `./format.sh` and `./pre-commit-validation.sh`.
Commit: "Prove the pooled lists and immutable arrays end to end on SQL Server, and that a pooled method allocates less".

### Task 4: The documentation and the close

**Files:**
- Modify: `docs/manual/settings.md`, `docs/manual/README.md`, `docs/manual/generator.md`; read `docs/manual/diagnostics.md`
- Modify: `docs/architecture/generator.md`
- Modify: `tests/AGENTS.md`, `tests/README.md`

- [ ] **Step 1: Flip the manual.**
  - `settings.md`, the `no-array-pool` row: the first sentence no longer lists four types; a `many` method reads its rows into buffers borrowed from .NET's shared array pool, except one that returns an `ImmutableList<T>` or `IImmutableList<T>`.  Remove the in-cell "Coming in a later release" callout.  Check the Collection type table's Holding column is still true.
  - `README.md` (manual): remove the "Reading the rows of a `List<T>` ... | Coming" row.
  - `generator.md`, "What each shape returns", `many`: the sentence covers every collection type but the two immutable lists.
- [ ] **Step 2: Read whole** `settings.md`, `README.md`, `generator.md` and `diagnostics.md` of `docs/manual/`, the pages this run touched, and fix every sentence the run made untrue; run both greps of `docs/manual/AGENTS.md` (the internals grep over the changed pages, and the "When a phase lands" grep) and read each hit.  No callout of this run may remain (`git grep -n "Coming in a later release" -- docs/manual README.md`).
- [ ] **Step 3: Architecture.**  `docs/architecture/generator.md`, `### The row buffer`: the eight types, `ToList` (exact `List<T>` by `SetCount`, rows copied into `AsSpan`) and `ToImmutableArray` (`AsImmutableArray` over the exact array, `ImmutableArray<T>.Empty` for none), the struct written whole; step 5 of "A method", `GetBuffer`'s cases.
- [ ] **Step 4: Test docs.**  `tests/AGENTS.md`, "Allocation budgets are tests": add the rule for the few-hundred-rows test (a pooled method against hand-written `List` + `ToArray()`, which must allocate less by at least its `LeastSavingInBytesPerCall`, set from a measure and never lowered to make a change pass).  `tests/README.md`: `RowBufferByHand`'s line covers every pooled type.
- [ ] **Step 5: Validate and commit**

Run `./format.sh`, `./pre-commit-validation.sh`.  Commit: "Document the pooled lists and immutable arrays, and read the manual's pages of this run whole".
