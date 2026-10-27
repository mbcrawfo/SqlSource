# Pooled row buffer for `many` methods

## Intent

A generated method whose shape is `many` reads its rows into a `List<T>` and, for most collection types, returns `rows.ToArray()`.  That is what EF Core's `ToArrayAsync` and linq2db's `ToArrayAsync` do, and close to what hand-written ADO.NET does.  For each call it allocates the list, a backing array for every doubling (4, 8, 16, ... rows), and a final copy.  SqlSource's goal is to match hand-written ADO.NET.  Where it can be better at reasonable cost, it should be.

The method borrows its growing buffer from .NET's shared array pool and allocates only the result.  A `many` method of up to about 1,000 rows, the hot path, then allocates 23–30% fewer bytes per call (rows included), with no measurable change in time.  The generator parameter `no-array-pool` brings back today's code for a project, type, folder, file or query that wants it.

## Goals

- A `many` method returning `T[]`, `IEnumerable<T>`, `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, `ImmutableArray<T>`, `List<T>`, `IList<T>` or `ICollection<T>` reads its rows through a pooled buffer.  Up to 1,024 rows it allocates no array but its result; above that, the growth arrays past the cap are allocated as today's are.
- The pool holds a bounded, small amount of memory however many row types a project has.
- `no-array-pool`, a new generator parameter, gives the code generated today, unchanged, wherever it applies.
- The change cannot conflict between two projects that share internals.

## Non-goals

- `ImmutableList<T>` and `IImmutableList<T>` keep `ImmutableList.CreateBuilder` and `ToImmutable()`.  Their builder is a tree, not an array.
- `one`, `one-optional`, `none` and `rowcount` are not touched.
- No user-supplied row-count hint, like Dapper.AOT's `[RowCountHint]`.
- No remembered size of the last result.
- No benchmark project in the repository.  The numbers below were measured in a throwaway project; see Evidence.

## Anti-goals

- A pool per row type.  `ArrayPool<RowType>.Shared` held 68 MB after the workload below, against 0.07 MB for today's code.  Each closed `ArrayPool<T>` also brings its own code into a Native AOT app.
- A buffer on the stack or inline in the method.  `stackalloc` cannot hold reference types, and a span cannot live across `await`.  An inline array of 32 or 128 rows becomes part of the async state machine, which is boxed on the heap when `ExecuteReaderAsync` suspends.  That made a 0-row call allocate 472 B or 1,240 B, against 232 B today.
- A `LinkedList<T>`: 14–43% more bytes and up to twice the time, one node per row.
- Anything that changes what a method returns, as its type or its contents, or when it throws.

## Constraints

- The methods use no library of SqlSource's (`docs/manual/generator.md`, Query methods), so the buffer is generated code.
- A project targets .NET 8 or later and compiles C# 12 or later (`README.md`, Supported environments).  `file`-local types (C# 11), `CollectionsMarshal.SetCount` (.NET 8), `ImmutableCollectionsMarshal.AsImmutableArray` (.NET 8), `MemoryMarshal` and `Unsafe` are all available.
- Output models are always reference types: records or classes (`GeneratorModelType`, `MethodOutput.IsRecord`).
- Types the generator adds to a project as `internal` in the `SqlSource` namespace conflict across `InternalsVisibleTo` (CS0436, [TD-0006](../../tech-debt/TD-0006-attribute-conflicts-across-friend-assemblies.md)).  The buffer must not join them.
- The root `AGENTS.md` rules on documentation: user docs describe effects, not internals, and the "What works today" table in `docs/manual/README.md` is the one record of what is delivered.
- A new generator parameter is named in every place the Settings section of `src/SqlSource/AGENTS.md` lists, among them the `SQLSRC109` section of `docs/manual/diagnostics.md` and the parameter list of `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs`, which fails until the two agree.

## Approaches

- **A. A `file`-local buffer in each methods file, over one shared and capped pool.  Chosen.**  Each `-Methods.g.cs` file that has a pooled method gets its own private copy of a small struct.  Every copy rents `object[]` from `ArrayPool<object>.Shared` and pools only buffers of up to 1,024 elements.  No other file or assembly can see it, so it cannot conflict.  The cost is a few dozen identical lines in each methods file.
- B. The same buffer as one generated `internal` type beside the attributes.  One copy, but it joins the CS0436 set of TD-0006, needs a place in the suppressor's `GeneratedTypes`, and shows the warning where suppressors do not run.
- C. No pool: a growing array in the method, started at the size of the method's last result, kept in a static field.  It needs no helper and holds no pool memory, but it only saves when a query returns about the same number of rows each call.  With varying sizes it fell back to today's numbers.

## Decisions

### The buffer

A `file` struct, generic in the row type with `where T : class`, declared at namespace level in each methods file that has at least one method using it, and referred to by a name a user's type cannot capture.  It holds an `object[]?` and a count.

- **Empty start.**  It rents nothing until the first row.  A query with no rows rents nothing and returns the empty result: `[]` for an array, the same for the interfaces backed by one, `ImmutableArray<T>.Empty`, or an empty `List<T>`.
- **Growth.**  The first buffer is 16 elements; each next one is double the last.  A buffer of up to 1,024 elements is rented from `ArrayPool<object>.Shared`.  A larger one is a plain `new object[n]`, left to the GC.  On growth the rows are copied into the new buffer, and the old one, if rented, is cleared up to the count and returned.
- **Why one pool for all row types.**  The elements are references whatever the row type, so one pool of `object[]` serves every query.  The memory it holds is the app's, not multiplied by the number of row types, and Native AOT gets no pool code for each row type.
- **Why 1,024.**  Results under 1,000 rows are the hot path.  The cap bounds what the pool can hold; a larger result gets about the savings of today's code (2% at 10,000 rows).
- **The result.**  It allocates the result at its exact size and copies the rows into it once.  It reads the `object[]` as `T` (every element is a `T`), so the copy has no type check per element.  `new Span<T>(T[])` over an `object[]` throws and `MemoryMarshal.Cast` does not take references, so the read goes through `MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<object, T>(ref MemoryMarshal.GetArrayDataReference(buffer)), count)` or its equal; the throwaway benchmark did this.  Then it clears the buffer up to the count and returns it to the pool if it was rented.
- **Clearing.**  A returned buffer never holds a row, so the pool keeps no row alive.
- **Exceptions.**  When reading throws or is cancelled, the rented buffer is not returned: it becomes garbage, as today's list does, and the pool rents a new one next time.  No `try`/`finally`.

### What each collection type gets

| Collection type | Today | With the buffer |
|---|---|---|
| `T[]`, `IEnumerable<T>`, `IReadOnlyCollection<T>`, `IReadOnlyList<T>` | `List<T>`, `ToArray()` | exact `T[]` |
| `ImmutableArray<T>` | `ImmutableArray.CreateBuilder`, `DrainToImmutable()` | exact `T[]` wrapped by `ImmutableCollectionsMarshal.AsImmutableArray`, no second copy |
| `List<T>`, `IList<T>`, `ICollection<T>` | `List<T>` grown by doubling, returned | `new List<T>(count)`, `CollectionsMarshal.SetCount`, rows copied into `CollectionsMarshal.AsSpan` |
| `ImmutableList<T>`, `IImmutableList<T>` | `ImmutableList.CreateBuilder`, `ToImmutable()` | unchanged |

Callers see the same types and contents as today.  An interface result is still an array, never a list they could cast back and change.

### `no-array-pool`

- A new generator parameter, `no-array-pool`, `GeneratorParameters.NoArrayPool`, in `GeneratorParameterList`'s names.  It is read at every level a generator parameter is: `-- generator:` in a query and in a file header, `Parameters` on the attribute, `SqlSourceGeneratorParameters` as item metadata and as a property.  The nearest list replaces the farther ones whole, as for every parameter.
- A query under it gets today's code for every collection type, the same text as before this change.
- The buffer is written into a methods file only when at least one of its methods uses it.  A file whose methods all have `no-array-pool`, or have no `many` method that uses it, is as it is today.
- The settings-files design (`2026-10-25-settings-files-design.md`) takes generator parameters as an array of strings, so it needs no change.

## Evidence

The numbers come from a throwaway BenchmarkDotNet project, run on an Apple M3 Max with .NET 10.  An in-memory reader shaped like the generated async method was used, with a three-property record row.

Bytes per call, rows included:

| Rows | List + ToArray (today) | Shared pool, capped at 1,024 |
|---|---|---|
| 0 | 232 | 208 |
| 1 | 368 | 288 |
| 10 | 1,112 | 792 |
| 100 | 8,016 | 5,832 |
| 1,000 | 72,824 | 56,232 |
| 10,000 | 822,708 | 806,130 |

- **Time** was within noise of today's at every size.
- **Against PostgreSQL in Docker** (a pooled buffer, uncapped), whole calls allocated 9–16% less.  Time was all round trip.
- **Memory held by the pool** was measured with 20 row types, 32 concurrent callers and 20,000 calls: 70% of 1–50 rows, 25% of 51–1,000 and 5% of 1,001–10,000.
  - A pool per row type held 68 MB after the workload.
  - The shared pool, capped at 1,024, held 0.7 MB.
  - After 150 s with a full GC every 5 s, they held 1.3 MB and 0.08 MB.  The pool trims only on gen 2 GCs.
- **Prior art.**  Dapper and Dapper.AOT return the `List<T>`.  EF Core and linq2db do `List` then `ToArray()`.  None pools.  .NET 9's `Enumerable.ToArray` rents segments from `ArrayPool<T>`.

## Delivery

An implementation manager runs the work on an integration branch, `integration/pooled-row-buffer`.  Each phase below is one unit and one pull request into it, and the integration branch lands in `main` in one final pull request.  The phases run in order: each builds on the code of the one before it.

Each phase's pull request stands on its own.
- It passes `./pre-commit-validation.sh`.
- It keeps the documentation true for what it delivers (root `AGENTS.md`, rule 4).  A parameter that has no effect yet is written as the manual's "Coming in a later release" callout, and the phase that delivers the effect replaces the callout.
- It records anything it defers in `docs/deferred` and any debt in `docs/tech-debt`.

`VersionPrefix` is checked once, on the final pull request (root `AGENTS.md`, Pull requests).

### Phase 1 - the `no-array-pool` parameter

**Scope.**
- `GeneratorParameters.NoArrayPool` and the word `no-array-pool` in `GeneratorParameterList`, read at every level.
- The resolved value carried to the method writer on `QueryMethod`, for example as a flag that the rows go through the pool.
- The generated code does not change in this phase: every method, under the parameter or not, is written as today.

**Testing.**
- The word at each level, and that the nearest list replaces the farther ones, in `GeneratorParameterLevelTests` or beside it.
- Words are matched ignoring case.
- `default` beside it is invalid, as for any word.
- It is accepted, and an unknown word still rejected, both in MSBuild values and on the attribute (`SQLSRC014`) and in a `-- generator:` marker (`SQLSRC109`).
- `QueryMethod` carries the flag from each level, as the method tests show.
- The generated text is unchanged with and without it.

**Documentation.**
- `docs/manual/settings.md`: the word in the Values cell of the settings table's Generator parameters row, and a row in the parameter table with a "Coming in a later release" callout for its effect.
- Every other place the Settings section of `src/SqlSource/AGENTS.md` requires for a new parameter, among them the `SQLSRC109` section of `docs/manual/diagnostics.md` and the list in `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs`.
- `docs/architecture/generator.md`: the parameter and the flag.
- `README.md` only if it lists the parameters.

### Phase 2 - the buffer, for arrays and the read-only interfaces

**Scope.**
- The `file`-local buffer, as decided above, written into a methods file only when at least one of its methods uses it.
- The buffer used by `many` methods returning `T[]`, `IEnumerable<T>`, `IReadOnlyCollection<T>` and `IReadOnlyList<T>`, unless the query has `no-array-pool`.
- Every other collection type, and every method under `no-array-pool`, still written as today.

**Testing.**
- Generator tests: the method text for the four types with and without `no-array-pool`.  The buffer written once in a file with several pooled methods; not written in a file with none.  A file that mixes pooled and `no-array-pool` methods.
- Compile tests: the file compiles for each placement (extension class and partial type), and two projects that share internals compile with no CS0436.
- End-to-end on PostgreSQL and SQL Server: the rows and their order for each of the four types at 0, 1, 16, 17, 1,024, 1,025 and a few thousand rows, with and without `no-array-pool`.  The counts cross the first buffer, a growth and the cap; the exact numbers are the builder's.
- The allocation tests in `EndToEnd/AllocationTests.cs` of both engines keep passing.

**Documentation.**
- `docs/manual/settings.md`: the callout replaced by the effect.  A `many` method reads its rows into buffers borrowed from .NET's shared array pool, and for a result of up to about 1,000 rows allocates only the result.  With `no-array-pool` it reads them into a `List<T>` it allocates, as hand-written code usually does.  The row says when you would want that: to keep the pool out of a project, or to compare.  Until phase 3 it says which collection types this covers.
- `docs/manual/generator.md`: one sentence under `many` in "What each shape returns", linking to the parameter.
- `docs/manual/README.md`: `no-array-pool` in the "What works today" row for the query methods' settings, beside `async-method-suffix`.
- `docs/architecture/generator.md`: the buffer, where it is written, the cap and the results.

### Phase 3 - lists and `ImmutableArray`, and the close

**Scope.**
- `List<T>`, `IList<T>` and `ICollection<T>` on the buffer: an exact-size `List<T>` through `CollectionsMarshal.SetCount`.
- `ImmutableArray<T>` on the buffer: `ImmutableCollectionsMarshal.AsImmutableArray` over the exact array.
- Both unless the query has `no-array-pool`.
- `ImmutableList<T>` and `IImmutableList<T>` stay as they are.

**Testing.**
- Generator and end-to-end tests as in phase 2, for these four types on both engines.
- An allocation test on each engine: a pooled `many` method of a few hundred rows allocates less than the hand-written `List` + `ToArray()` method.  The measure and its margin follow the existing tests' pattern.  The builder sets the margin from what it measures and says why in the test.

**Documentation.**
- The phase 2 pages, with the note on which collection types are covered removed or updated.
- The manual's pages that this work touches, read whole, as each phase of the query-generation epic did at its close.

## Left to the builder

- The buffer's name and members, and where in the methods file it goes.
- How the methods writer knows that a file needs the buffer.
- The exact text of the generated code, within the decisions above.
- The row counts of the end-to-end tests, and the margin of the allocation test.
- Whether the examples need any change.  They show what a user writes; the generated code is not checked in.
- The wording of the documentation, within the rules above.
