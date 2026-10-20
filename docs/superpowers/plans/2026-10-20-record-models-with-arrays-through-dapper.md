# Record models with arrays readable through Dapper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A record output model whose query has a PostgreSQL array column can be read through Dapper, so the default model shape works for such queries, resolving TD-0062.

**Architecture:** Fix (a) of TD-0062.  A model property learns whether its column is a PostgreSQL array (or a domain over one); a model that any of its queries gives such a column is marked; `ModelEmitter` gives a marked record a private constructor without parameters that chains to the positional one.  Dapper takes a constructor without parameters first, public or not, and then sets the `init` properties by name.

**Tech Stack:** C# source generator (netstandard2.0, Roslyn 4.8.0 floor, generated C# ≤ 12), xunit + Shouldly, Dapper 2.1.89, Npgsql 8 and 10, PostgreSQL in Docker.

**Spec:** `docs/tech-debt/TD-0062-record-models-with-arrays-and-dapper.md` (Problem and Proposed fix), with the model shapes of `docs/superpowers/specs/2026-10-18-query-generation-phase-5-3-models-design.md`.

## What the spike found (2026-10-20), and where the code differs from the TD

- A positional record with an added `private R() : this(default(int[]?)) { }` is read by Dapper 2.1.89 on Npgsql 10: `Int4ArrayValue` came back `[1,2,3]`.  So the constructor is **private**: users cannot call it and see no new public API, and no `<summary>` is needed.
- Of every column of `all_types`, Npgsql 10's `GetFieldType` gives `System.Array` only for the true arrays (`integer[]`, `text[]`, `date[]`, `account_status[]`).  `int2vector` (`Int16[]`), `oidvector` (`UInt32[]`), multiranges (`NpgsqlRange<T>[]`) and `bytea` (`Byte[]`) give exact types and need nothing.  So the trigger is the column's PostgreSQL kind, `array`, or a `domain` whose base is (recursively) an array; not the C# text ending in `[]`.
- The TD speaks of "a second constructor"; a single-property record also has the compiler's copy constructor, so the chained call must use typed defaults, `default(T)`, or `this(default!)` is ambiguous (CS0121).
- Only output models get it: Dapper never constructs an input model.  A class is unchanged.

## Global Constraints

- Generated code uses nothing newer than C# 12, and must give no compiler warning (`GeneratedCodeWarnings` empty).
- Tests in `tests/SqlSource.Tests/Generator/` also compile on Roslyn 4.8.0; start each file with the shared line; names are `Method_Scenario_Outcome`; Shouldly; every test class `sealed`.
- `ModelCollector.FindDifference` compares every member that changes the emitted C#: the new flag is not a difference between two queries; it is OR-ed across the queries that share a model.
- User docs (`README.md`, `docs/manual/`) describe effects, never internals: no "describer", no sidecar field, no TD id.
- `docs/tech-debt`: resolving deletes the item file, its README row, every `TD-0062` reference, and its known issue.
- Sidecars are written only by `tools/describe-end-to-end.sh`; run it only if `describe --check` (CheckTests) says one is stale.
- Every commit passes `./pre-commit-validation.sh`; after a change to `tests/SqlSource.Tests.Postgres`, also run it with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.
- Keep the change to `tests/SqlSource.Tests.Postgres/Realistic/Generation/RealisticQueries.cs` to the one attribute argument and its comment (another unit edits the realistic corpus).

## Review Focus

1. A record with exactly one property, an array: the chained call must not be ambiguous with the copy constructor.  Test in Task 1 (`Run_RecordWithOnlyAnArrayColumn_Compiles`).
2. A non-nullable reference array (`string[]` from a `"tags!"` column) and a nullable value type next to it: the defaults must compile without a nullable warning.  Covered by Task 1's exact-text test, which has `int`, `string[]` and `int[]?`.
3. Two queries sharing one model where only one has a true array column (the other a `text` column mapped to `string[]` by a type override): the model must still get the constructor.  Test in Task 1.
4. A domain over an array: Npgsql reports the base type, so it must get the constructor.  Test in Task 1.
5. User code calling `new GetTagsDto()` must not compile, while `new GetTagsDto(...)`, `with` and `==` still do.  Test in Task 1.

---

### Task 1: The generator gives a record with an array column a private constructor without parameters

**Files:**
- Modify: `src/SqlSource/Generation/Models/ModelProperty.cs` (new member)
- Modify: `src/SqlSource/Generation/Models/Model.cs` (new member)
- Modify: `src/SqlSource/Generation/Models/ModelReader.cs:235-286` (set the flag)
- Modify: `src/SqlSource/Generation/Models/ModelCollector.cs:150-180` (`Models`, OR the flag over `joined`)
- Modify: `src/SqlSource/Generation/Emitting/ModelEmitter.cs` (`AppendRecord`)
- Modify: `docs/architecture/generator.md` ("A model's file": the record bullet; "Models": the flag and that it is OR-ed, not compared)
- Test: `tests/SqlSource.Tests/Generator/ModelTests.cs`

**Interfaces:**
- Produces: `ModelProperty(string Name, PropertyType Type, bool IsNullable, string Documentation, string Subject, bool IsArray)` — `IsArray`: the column's PostgreSQL type is an array, or a domain whose base is one; always false for a parameter.  `IsSameShape` is not changed.
- Produces: `Model(..., EquatableArray<ModelProperty> Properties, string SummaryXml, bool HasArrayColumn)` — true when a property of any request joined into the model `IsArray`.

- [ ] **Step 1: Write the failing tests** in `ModelTests`, using the class's `Run`, `Entry`, `Column`, `AssertClean` helpers and `TestSidecars.Postgres(kind: SidecarValues.Kind.Array, element: Int4, name: "integer[]", internalName: "_int4")`:
  - `Run_RecordWithAnArrayColumn_HasAPrivateConstructorWithoutParameters`: columns `id` (`integer`, not null), `tags!`-style not-null `text[]` named `tags`, nullable `integer[]` named `scores`; default output kind (sealed record).  `AssertClean(run)`, and the model's source `ShouldBe` exactly:

    ```csharp
            public sealed record GetUserDto(int Id, string[] Tags, int[]? Scores)
            {
                // Dapper reads a row with an array column through this constructor, then sets each property.
                private GetUserDto() : this(default(int), default(string[])!, default(int[]?)) { }
            }
    ```
    (inside the usual header, summary and `<param>` lines, as `OutputSealedRecord` shows them).
  - `Run_RecordWithOnlyAnArrayColumn_Compiles`: one nullable `integer[]` column; `AssertClean`, source contains `: this(default(int[]?)) { }`.
  - `Run_RecordWithADomainOverAnArray_HasTheConstructor`: column of kind `domain` with `baseType` the `integer[]` type; source contains `private GetUserDto() : this(`.
  - `Run_RecordWithoutAnArrayColumn_HasNoConstructor`: the existing `int2vector`-free case `Column("id")`; source contains `public sealed record GetUserDto(int Id);` and not `private GetUserDto()`.
  - `Run_ClassOrInputModelWithAnArray_IsUnchanged` (theory over `SqlSourceOutputModelType=class` with an array column, and an input model with an array parameter): no `private ` in either model's source.
  - `Run_SharedModelWhereOneQueryHasAnArray_HasTheConstructor`: two queries with `-- output-model: TagsRow`, one column `tags`: one `text[]`, the other `text` with `-- type-override: text = string[]` on that query; `AssertClean`, `TagsRow` source contains `private TagsRow() : this(default(string[]?)) { }`.
  - `Run_RecordWithAnArrayColumn_KeepsItsPositionalConstructorEqualityAndWith`: user source alongside the model, compiled by the harness: `var a = new GetUserDto(1, null); var b = a with { Id = 2 }; bool same = a == b with { Id = 1 };` compiles with no error; a second run whose source also has `var c = new GetUserDto();` has a `CS0122` among `run.CompilationErrors`.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Generator.ModelTests"`
Expected: the new tests FAIL (no constructor emitted); the old ones pass.

- [ ] **Step 3: Implement**
  - `ModelReader.ReadColumns` sets `IsArray` from `column.Type`: a `PostgresType` whose `TryGetKind` gives `Array`, or `Domain` with a `Base` that is (recursively) an array.  `ReadParameters` passes `false`.
  - `ModelCollector.Models` sets `HasArrayColumn` to whether any request in `joined` has an `IsArray` property.
  - `ModelEmitter.AppendRecord`: when `model.HasArrayColumn`, end the declaration with `\n{indent}{\n`, the comment line and the constructor at `indent + Indent`, then `{indent}}\n`; otherwise `);\n` as today.  Each argument is `default(` + `AppendType` + `)`, plus `!` when the property is neither nullable nor a value type (the class's `default!` rule).

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Generator"` and `dotnet test tests/SqlSource.Tests.RoslynFloor`
Expected: PASS.

- [ ] **Step 5: Update `docs/architecture/generator.md`**, then run `./pre-commit-validation.sh` (expect the Postgres end-to-end tests to still pass: `AllTypesDto` is still a class) and commit: `Give a record with an array column a constructor Dapper can use`.

### Task 2: The end-to-end tests, the realistic corpus and the example read records with arrays

**Files:**
- Modify: `tests/SqlSource.Tests.Postgres/EndToEnd/Queries/AllTypes.sql:1-4` (drop `-- output-model-type: class`, reword the header comment)
- Modify: `tests/SqlSource.Tests.Postgres/EndToEnd/AllTypesQueryTests.cs:189-214,250-271`
- Modify: `tests/SqlSource.Tests.Postgres/EndToEnd/ModelShapeTests.cs:406-410`
- Modify: `tests/SqlSource.Tests.Postgres/Realistic/Generation/RealisticQueries.cs:30` and the attribute below it
- Modify: `tests/SqlSource.Tests.Postgres/README.md:56` (drop the sentence on `array-and-string-agg`'s class)
- Modify: `examples/PostgresExamples/Models/Orders.sql:23`, `examples/PostgresExamples/Models/StoreQueries.cs:24-25`, `examples/PostgresExamples/README.md:12`

**Interfaces:**
- Consumes: Task 1's emitted constructor.  `AllTypesDto` becomes a `sealed record`, positional in column order.

- [ ] **Step 1: Change the tests**
  - Replace `QueryAsync_RecordWithAnArray_DapperCannotConstructIt` and the three hand-written records with `QueryAsync_RecordWithAnArray_ReadsIt`: read `AllTypesQueries.ListAllTypes` into `AllTypesDto` and assert, for the full row, `Int4ArrayValue` `[1, 2, 3]`, `TextArrayValue` and `DateArrayValue` per `AllTypesRows`, and for the null row each is `null`; then `(full with { Id = 9 }).Int4ArrayValue.ShouldBeSameAs(full.Int4ArrayValue)` and `(full with { }).ShouldBe(full)`.  Its comment says Npgsql gives an array column as `System.Array`, so Dapper reads the record through its constructor without parameters.
  - `ModelShapeTests.Models_OutputTypeByHeaderMarker_IsAClassOfEveryColumn` becomes `Models_AllTypes_IsASealedRecordOfEveryColumn` with `Shape.SealedRecord`.
  - In `ArgsFrom` and any other use, nothing changes: they read `AllTypesDto` by property.

- [ ] **Step 2: Remove the workarounds**: the marker in `AllTypes.sql` (header comment: its rows are read into the default record model), `OutputModelType = GeneratorModelType.Class` and its comment in `RealisticQueries.cs`, the marker in `Orders.sql` and the comment in `StoreQueries.cs` (and its README row: `GetOrderTags` has an array column, read into the default record).

- [ ] **Step 3: Run**

Run: `dotnet test tests/SqlSource.Tests.Postgres` then `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test tests/SqlSource.Tests.Postgres` and `dotnet test tests/SqlSource.Tests.Postgres.Npgsql8`
Expected: PASS, `CheckTests` included (markers outside the SQL leave the hash alone; if a sidecar is stale, run `tools/describe-end-to-end.sh` and read the diff).  Realistic Dapper tests read `array-and-string-agg` as a record.

- [ ] **Step 4: Commit** after `./pre-commit-validation.sh`: `Read records with arrays through Dapper in the tests and the example`.

### Task 3: User docs and the tech-debt item

**Files:**
- Modify: `docs/manual/generator.md` (Shape: the record bullet; Known issues: remove the Dapper/array item)
- Modify: `docs/manual/recipes.md:240-249,311` (remove the "A model with an array column is a class" bullet and the rules line's "and for records with an array")
- Modify: `docs/manual/settings.md:294` (drop "or into a model with an array column")
- Modify: `README.md:105` (drop the array sentence; "Both are in" still names snake_case and camelCase)
- Delete: `docs/tech-debt/TD-0062-record-models-with-arrays-and-dapper.md`; Modify: `docs/tech-debt/README.md:45` (remove the row)

- [ ] **Step 1: Edit.** Shape's record bullet gains, in effects: a record whose query has an array column, such as `integer[]`, also has a private constructor without parameters, so that Dapper can read rows into it; you cannot call it, and the record's equality, `with` and deconstruction are as for any record.
- [ ] **Step 2: Verify** `git grep -n -i -E "TD-0062|array column|with an array" -- . ':!docs/superpowers'` finds nothing stale, and the manual's grep (`docs/manual/AGENTS.md`) is clean on the changed pages.
- [ ] **Step 3: Commit** after `./pre-commit-validation.sh`: `Say that a record with an array reads through Dapper; resolve TD-0062`.
