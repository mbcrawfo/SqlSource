# List Parameters Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `IN @list` and `NOT IN @list` expand to one parameter per element in generated query methods on PostgreSQL and SQL Server, and PostgreSQL array parameters accept any `IEnumerable<T>` in `codegen` input models.

**Architecture:** The parser classifies each parameter use (a value, a list use after `IN`, or a null test before `IS [NOT] NULL`) and splits list uses into segments of their own. The tool describes a stand-in for them, and the generator gives list parameters `IEnumerable<E>` properties. A generated method counts and pads each list, checks the engine's parameter limit, builds the SQL with `string.Create`, and binds each element through the existing binders. PostgreSQL arrays are bound as `NpgsqlParameter<IList<E>>`.

**Tech Stack:** C# (netstandard2.0 generator, Roslyn 4.14 floor), Npgsql 8/9/10, Microsoft.Data.SqlClient, xUnit with Shouldly, golden files, Docker databases.

**Spec:** `docs/superpowers/specs/2026-11-04-list-parameters-design.md`. Read it before any task; this plan argues from it and does not repeat its reasons.

## Global Constraints

- **Two pull requests.** Part A (Tasks 1–4) is the PostgreSQL array change. Part B (Tasks 5–14) is list parameters, on a branch from Part A's merged `main` or stacked on it. The spec and this plan are committed in Part A's first commit.
- **Validation and review rules apply in full.**
  - Every commit passes `./pre-commit-validation.sh`, run after the last edit.
  - Every push passes `./pre-commit-validation.sh --full` on the exact tree.
  - Run `branch-review:review` until it is clean before offering a PR, and again before every later push.
  - A change to a describer or its tests also runs that describer's project on its floor image: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` or `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.
- **Docs, examples, AGENTS.md and the tests' READMEs change in the same PR as the behaviour they describe** (root `AGENTS.md`, rule 4). User docs describe effects, never internals.
- **The closed-sets rule.**
  - Every switch over an enum names every member and ends in `_ => throw UnexpectedEnumValueException.Of(value)`.
  - A per-engine value lives in `PerEngine<T>` or behind one switch over `DatabaseEngine` (`PostgreSql`, `SqlServer`).
  - New `SqlSegmentKind` and `SqlParameterUse` members are handled at every switch that the build reports.
- **No hash changes for any query without a list.** The pinned goldens in `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs` stay unchanged throughout.
- **The sidecar format stays at version 1**, with no new key.
- **Generated code uses no library of SqlSource's.** Helpers are static local functions inside the method that needs them. Numbers in generated text are formatted with `CultureInfo.InvariantCulture`.
- **Padding sizes:** 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, then each multiple of 512 (1024, 1536, 2048, …). The last element repeats to fill.
- **Parameter limits:** 2100 for SQL Server and 65535 for PostgreSQL, until Task 12 pins the exact values with a test.
- **The empty or null list form is `(SELECT @<name>0 WHERE 1 = 0)`**, with `<name>0` sent as a `NULL` of the element's type.
- **The null-test parameter** has the list's own name. It is `boolean` on PostgreSQL (`DataTypeName = "pg_catalog.bool"`) and `bit` on SQL Server, `NULL` when the list is null and `true` otherwise.
- **The new diagnostic is SQLSRC126**, `List parameter is used outside IN`, an error that can't be configured.
- **The new generator parameter is `no-list-padding`**, `GeneratorParameters.NoListPadding = 128`.
- **The version doesn't need changing.** `VersionPrefix` is `0.1.0` and the repository has no `v*` tags, so there is nothing to compare. Run the check again before each PR.

## Review Focus

1. **A lazy or single-use sequence must be enumerated exactly once**, whether it is bound as a list or as a PostgreSQL array. Covered by `ListQueryTests.Ids_LazySequence_IsEnumeratedOnce` (Task 12) and `AllTypesQueryTests.Int4Array_LazySequence_IsEnumeratedOnce` (Task 4).
2. **Expanded names must not collide** with another parameter whose name extends the list's, such as `@ids` beside `@ids_0`, or `@id` beside `@ids`. Covered by `ListNames_AnotherParameterMatchesTheSuffix_AddsAnUnderscore` (Task 11) and the corpus case `ListBesideItsExpansionName` (Task 11).
3. **Padding must never push a query over the limit.** For example, 2000 values plus a few ordinary parameters on SQL Server must be sent exactly, not padded to 2048. Covered by `Ids_PaddingWouldPassTheLimit_SendsExactCounts` (Task 12).
4. **A kept comment between `IN` and the list** (`keep-comments`, `IN /* ids */ @ids`) must still expand. Covered by `Split_KeptForm_SplitsTheSameUses` (Task 7) and the corpus case `ListKeepsComments` (Task 11).
5. **A streaming method must throw the limit's `ArgumentException` when called, not when enumerated**, like its argument checks. Covered by `StreamIds_OverTheLimit_ThrowsBeforeEnumeration` (Task 12).

---

# Part A: PostgreSQL arrays as `IEnumerable<T>`

### Task 1: Probe the remaining array kinds

**Files:**
- Modify (scratch only, never the repo): the probe at `/private/tmp/claude-501/-Users-michael-Projects-SqlSource--claude-worktrees-query-phase-9-4-close-982c6c/982a0e3b-9830-4a5d-ab15-8fa75e2c131d/scratchpad/npgsql-probe`. If it is gone, recreate it: three console projects pinning Npgsql 8.0.9, 9.0.5 and 10.0.3 against `postgres:18.6`, plus `postgis` for NetTopologySuite.

**Interfaces:**
- Produces: the list of array kinds that `NpgsqlParameter<IList<E>>` sends on every major, recorded in the PR description, and the set `PostgresArrayKinds.Excluded` used in Task 2 (empty when every kind passes).

- [ ] **Step 1: Bind each kind as `NpgsqlParameter<IList<E>>`**, with the `DataTypeName` that `PostgresParameterBinder.DataTypeNameOf` gives, and run `SELECT cardinality(@p)` on each major for:
  - an enum array sent as `IList<string>` under `public._<enum>`;
  - a domain over `integer[]`;
  - NodaTime's `IList<Instant>` for `timestamptz[]` and `IList<LocalDate>` for `date[]`, with `UseNodaTime()`;
  - NetTopologySuite's `IList<Geometry>` for `geometry[]`, with `UseNetTopologySuite()`;
  - `IList<JsonElement>` for `jsonb[]`;
  - `IList<string?>` holding serialized JSON for `jsonb[]`, which is the by-element path for a JSON class.
- [ ] **Step 2: Record the results.** Pass means ✅ on 8, 9 and 10. A kind that fails on any major goes into `Excluded`, and the manual (Task 4) names it beside `bytea` and the multiranges.

### Task 2: `ParameterSequence` on model properties

**Files:**
- Create: `src/SqlSource/Generation/Models/ParameterSequence.cs`
- Modify: `src/SqlSource/Generation/Models/ModelProperty.cs`, `src/SqlSource/Generation/Models/ModelReader.cs:242-273` (`ReadParameters`), `src/SqlSource/Generation/Models/TypeModels.cs:195-198` (`Request`), `src/SqlSource/Generation/Models/ModelCollector.cs:224-292` (`FindDifference`), `src/SqlSource/Generation/Emitting/ModelEmitter.cs:156-160` (`AppendType`)
- Test: `tests/SqlSource.Tests/Generator/ModelPropertyTypeTests.cs` (create, or add to the existing model tests nearest in topic), `tests/SqlSource.Tests/Generator/SharedModelTests.cs`

**Interfaces:**
- Produces:
  - `internal enum ParameterSequence { None, Array, List }`. `Array` means a PostgreSQL array parameter whose `Type.Text` is `E[]`, written as `IEnumerable<E>`. `List` (used in Part B) means `Type.Text` is the element `E`, written as `IEnumerable<E>`.
  - A new last parameter on `ModelProperty`: `ParameterSequence Sequence = ParameterSequence.None`, compared by `FindDifference`.
  - `ModelEmitter.AppendType` writes `global::System.Collections.Generic.IEnumerable<` + element + `>` for both kinds, then `?` when `IsNullable`. For `Array`, the element is `Type.Text` without its last `[]`.

- [ ] **Step 1: Write the failing tests.**
  - `InputModel_PostgresArrayParameter_IsIEnumerableInCodeGen`: a `codegen` query `SELECT 1 WHERE 1 = ANY (@ids)` whose sidecar has `@ids` as `integer[]` gives `public sealed record …Params(global::System.Collections.Generic.IEnumerable<int> Ids)`.
  - `InputModel_PostgresArrayParameter_StaysAnArrayInModels`: the same query under `-- output: models` gives `int[] Ids`.
  - `InputModel_NullablePostgresArrayParameter_IsNullableIEnumerable`: with `-- param: @ids null` it gives `IEnumerable<int>? Ids`.
  - `InputModel_ArrayWithNullableElementOverride_IsIEnumerableOfNullable`: with `-- type-override: int4[]=int?[]` it gives `IEnumerable<int?>`.
  - `InputModel_ArrayParameterWithMarkerClrType_IsTakenAsWritten`: with `-- param: @ids -> int[]` it gives `int[] Ids` in `codegen`.
  - `InputModel_ByteaAndMultirangeParameters_AreNotSequences`: `bytea` stays `byte[]`, and `int4multirange` stays `NpgsqlRange<int>[]`.
  - `InputModel_DomainOverArrayParameter_IsIEnumerable`.
  - `OutputModel_ArrayColumn_StaysAnArray`.
  - In `SharedModelTests`: `SharedInputModel_CodeGenAndModelsWithAnArrayParameter_IsSqlsrc257`, where one `-- input-model:` name is shared by a `codegen` query and a `models` query, each with `@ids integer[]`, and the result is SQLSRC257.
- [ ] **Step 2: Run the tests and confirm they fail.**
  Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~InputModel_|FullyQualifiedName~SharedInputModel_CodeGenAndModels"`
  Expected: FAIL. The models still have `int[]`, and the shared model isn't reported.
- [ ] **Step 3: Implement.**
  - `ReadParameters` sets `Sequence = Array` when all of these hold:
    - the engine is PostgreSQL;
    - the sidecar type `IsArrayType`, the existing helper at `ModelReader.cs:322`;
    - `types[index].Text` ends in `[]` and holds no `[,`;
    - `query.Parameters[index].ClrType` is null;
    - the element kind is not in Task 1's `Excluded`.
  - `TypeModels.Request` resets `Array` to `None` when `settings.Output != OutputKind.CodeGen`, in the same pass as `WithoutLibrary`. `List` is never reset.
- [ ] **Step 4: Run the tests and confirm they pass.** Same command. Expected: PASS.
- [ ] **Step 5: Commit** with the message `PostgreSQL array parameters are IEnumerable<T> in codegen input models`. Commit this task's files, plus the spec and this plan in Part A's first commit.

### Task 3: Bind PostgreSQL arrays as `IList<E>`

**Files:**
- Modify: `src/SqlSource/Generation/Methods/QueryMethod.cs` (`MethodParameter`), `src/SqlSource/Generation/Methods/MethodPlanner.cs:493-539` (`TryReadInput`), `src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs:116-145` (`AppendBoundType`, `AppendValue`), `src/SqlSource/Generation/Emitting/ParameterBinder.cs:178-211` (`AppendByElement`)
- Test: golden files `tests/SqlSource.Tests/Generator/Golden/postgres/{binding,by-element,json,nodatime,driver-npgsql8,driver-npgsql10}.txt`, `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/QueryMethodWriterTests.cs`

**Interfaces:**
- Consumes: `ModelProperty.Sequence` (Task 2).
- Produces: a new last parameter on `MethodParameter`, `ParameterSequence Sequence = ParameterSequence.None`, copied from the property by `TryReadInput`.

- [ ] **Step 1: Write the failing emitter tests** in `QueryMethodWriterTests`:
  - `Bind_PostgresArraySequence_BindsAnIListOfTheElement`: the output holds `new global::Npgsql.NpgsqlParameter<global::System.Collections.Generic.IList<int>>()`, the unchanged `DataTypeName = "pg_catalog._int4"`, and `TypedValue = parameters.Ids is { } value ? value as global::System.Collections.Generic.IList<int> ?? global::System.Linq.Enumerable.ToArray(value) : null`.
  - `Bind_PostgresArraySequenceByElement_ConvertsAfterMaterialising`: the element converter runs through `global::System.Array.ConvertAll` over `value as E[] ?? global::System.Linq.Enumerable.ToArray(value)`, and the bound type is `IList<DbElem?>`.
- [ ] **Step 2: Run the tests and confirm they fail.**
  Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Bind_PostgresArraySequence"`
  Expected: FAIL.
- [ ] **Step 3: Implement** the bound type and the value for `Sequence == Array` in `PostgresParameterBinder`. A null still gives `TypedValue = null`, as the probe showed works with `IList<E>`.
- [ ] **Step 4: Update the golden files and review the diff.**
  - Run: `SQLSOURCE_UPDATE_GOLDEN=1 dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~QueryMethodCorpusTests"`, then `git diff tests/SqlSource.Tests/Generator/Golden`.
  - Expected: only the bindings of array parameters and their model properties change, on PostgreSQL only.
- [ ] **Step 5: Run `dotnet test tests/SqlSource.Tests`.** Expected: PASS.
- [ ] **Step 6: Commit** with the message `Bind PostgreSQL array parameters as IList<T>`.

### Task 4: PostgreSQL arrays end to end, docs, and PR A

**Files:**
- Modify:
  - `tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs`;
  - `tests/DatabaseTests/TestHarness/EndToEnd/AllTypesRows.cs:128`, if the input type no longer accepts what it builds;
  - `tests/SqlSource.Tool.Testing/Realistic/RealisticInputs.cs:18-70`, whose `IsInstanceOfType` check must accept an array for an `IEnumerable<T>` property;
  - `docs/manual/postgresql.md` (the type table's array row and its notes, and "How a parameter is sent");
  - `docs/manual/generator.md` ("Property types", "Shared models");
  - `docs/architecture/generator.md` ("Models": the output split; "Binding a parameter.", PostgreSQL).
- Test: the files above. `tests/DatabaseTests/Postgres.Npgsql8` links `AllTypes.sql`, so it runs the same queries on Npgsql 8.

- [ ] **Step 1: Write the end-to-end tests** in `AllTypesQueryTests`:
  - `Int4Array_EachKindOfSequence_RoundTrips`, a theory over `int[]`, `List<int>`, `HashSet<int>`, `ImmutableArray<int>` and a lazy `Enumerable.Range(1, 3).Select(x => x)`. Insert with the generated method, read back, and check `[1, 2, 3]` (the set's order is not checked).
  - `Int4Array_LazySequence_IsEnumeratedOnce`: a counting `IEnumerable<int>` sees one `GetEnumerator` call.
  - `Int4Array_Null_IsNull`.
  - `TextArray_HashSet_RoundTrips`.
- [ ] **Step 2: Run them on both Npgsql projects.**
  Run: `dotnet test tests/DatabaseTests/Postgres --filter "FullyQualifiedName~AllTypesQueryTests"` and `dotnet test tests/DatabaseTests/Postgres.Npgsql8 --filter "FullyQualifiedName~AllTypesQueryTests"`
  Expected: PASS.
- [ ] **Step 3: Update the docs.**
  - The manual says:
    - which C# types pass through and which are copied once;
    - that the `models` output keeps `T[]`;
    - that `-> T[]` is taken as written;
    - which types are not sequences (`bytea`, multidimensional types, `int2vector`, `oidvector`, multiranges, and any kind from Task 1's `Excluded`).
  - Add a row to the "What works today" table in `docs/manual/README.md`: "PostgreSQL array parameters accept any `IEnumerable<T>` in query methods".
- [ ] **Step 4: Check and commit.**
  - Run `./pre-commit-validation.sh`. Expected: it passes.
  - Commit with the message `PostgreSQL arrays as IEnumerable<T>: end to end and docs`.
- [ ] **Step 5: Open PR A.**
  - Run the version check.
  - Run `./pre-commit-validation.sh --full`, then `branch-review:review` until it is clean.
  - Push, open the PR and turn on Auto-fix (`CLAUDE.local.md`).

---

# Part B: List parameters

### Task 5: Classify parameter uses

**Files:**
- Create: `src/SqlSource/Parsing/SqlParameterUse.cs`, `src/SqlSource/Parsing/SqlListUses.cs`
- Modify: `src/SqlSource/Parsing/SqlScannedQuery.cs`, `src/SqlSource/Parsing/SqlFileParser.cs:332-360` (`BuildQuery`)
- Test: `tests/SqlSource.Tests/Parsing/SqlListUsesTests.cs` (create)

**Interfaces:**
- Produces:
  - `internal enum SqlParameterUse { Value, List, NullTest }`.
  - `SqlListUses.Find(text, lexemes, start, end, rules) -> ImmutableArray<SqlParameterUse>`, whose parameters have exactly the types of `SqlDeclaredVariables.Find`'s. It returns one entry per `Parameter` lexeme in `[start, end)`, in order, which is the same order as `SqlBlockText.Parameters` in both the stripped form and the kept form. It returns `ImmutableArray<SqlParameterUse>.Empty` when no `Text` lexeme holds `in` ignoring case, and a caller then reads every use as `Value`.
  - `SqlScannedQuery` gains a last member, `ImmutableArray<SqlParameterUse> Uses`.
- How `Find` works: it walks the lexemes once, as `SqlDeclaredVariables.Find` does.
  - Words are runs of the dialect's name characters, through `QuoteReader.IsNameCharacterOfSqlServer` or `…OfPostgreSql` (ask `SqlDialectRules` which applies), compared `OrdinalIgnoreCase`.
  - Comment and hint lexemes are skipped as white space. A `Quoted` lexeme is a word that is not `IN`.
  - It tracks parentheses in `Text` lexemes with a stack that records whether each `(` follows the word `position`.
  - A `Parameter` lexeme is a `List` use when the last significant item before it is the whole word `IN` and the innermost open parenthesis is not a `position` call.
  - It is a `NullTest` when the next significant words after it are `IS NULL` or `IS NOT NULL`.
  - Otherwise it is a `Value` use.

- [ ] **Step 1: Write the failing tests**, as a theory `Find_Use_IsClassified(string dialect, string sql, string[] expected)` with these rows:

| sql | expected |
|---|---|
| `id IN @ids` | `List` |
| `id not in @ids` | `List` |
| `id IN /* c */ @ids` | `List` |
| `id IN\n-- c\n@ids` | `List` |
| `id IN /*+ hint */ @ids` | `List` |
| `id IN (@ids)` | `Value` |
| `a JOIN @t` | `Value` |
| `position('@' IN @email) > 0` | `Value` |
| `position('@' IN lower(@email))` | `Value` |
| `(id IN @ids)` | `List` |
| `@ids IS NULL OR id IN @ids` | `NullTest, List` |
| `@ids is not null` | `NullTest` |
| `@ids ISNULL` | `Value` |
| `'IN' @x` | `Value` |
| `"IN" @x` | `Value` |
| `[IN] @x` (mssql) | `Value` |
| `x#IN @y` (mssql, where `#` is a name character) | `Value` |
| `id IN @ids AND @other = 1` | `List, Value` |

  Add `Find_TextWithoutIn_IsEmptyAndAllocatesNothing`, which follows `SqlFileParserAllocationTests`' pattern.
- [ ] **Step 2: Run the tests and confirm they fail.**
  Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlListUsesTests"`
  Expected: FAIL to compile.
- [ ] **Step 3: Implement `SqlListUses.Find`**, and call it in `BuildQuery` only when `sql.Parameters.Length > 0`, passing the result into `SqlScannedQuery`.
- [ ] **Step 4: Run the tests and confirm they pass.** Same command, plus `--filter "FullyQualifiedName~SqlFileParserAllocationTests"`. Expected: PASS.
- [ ] **Step 5: Commit** with the message `Classify each parameter use as a value, a list use or a null test`.

### Task 6: List parameters in the parameter list, and SQLSRC126

**Files:**
- Modify:
  - `src/SqlSource/Parsing/SqlQueryParameter.cs`;
  - `src/SqlSource/Parsing/SqlParameterList.cs:31-111` (`Create`, `AddParameters`, `Declare`);
  - `src/SqlSource/Parsing/SqlParseErrorKind.cs`;
  - `src/SqlSource/Diagnostics/SqlDiagnostics.cs` (the descriptor, `All`, and `ForParseError` at :1290-1315);
  - `src/SqlSource/AnalyzerReleases.Unshipped.md`;
  - `docs/manual/diagnostics.md` (the section, the table row, and the intro's "an id from 101 to 120");
  - `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs:76-86`.
- Test: `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs`

**Interfaces:**
- Consumes: `SqlScannedQuery.Uses` (Task 5).
- Produces:
  - `SqlQueryParameter` gains two last parameters, `bool IsList = false` and `bool HasNullTest = false`.
  - `SqlParseErrorKind.ListParameterMisused`, appended last. Its arguments are the parameter with its prefix, and the reason text.
  - The descriptor `SqlDiagnostics.ListParameterMisused`, with the id `SQLSRC126`, the title `List parameter is used outside IN`, and the message format `The parameter '{0}' is a list, written after IN, and {1}.  A list can stand after IN or NOT IN, or before IS NULL or IS NOT NULL.`. The reasons, `{1}`, are exactly:
    - `is used here as one value`
    - `stands here in a token's default, which the method cannot expand`
    - `has a -- param: marker that says json, which a list cannot hold`
  - A bare `IN @x` inside a token's default reports with the reason `stands here in a token's default, which the method cannot expand` even when `@x` is not otherwise a list.

- [ ] **Step 1: Write the failing tests** in `SqlFileParserParameterTests`:
  - `Parse_ParameterAfterIn_IsAList`: `IsList` is true and `HasNullTest` is false.
  - `Parse_ListWithANullTest_HasNullTest`.
  - `Parse_ParameterWithOnlyANullTest_IsNotAList`.
  - `Parse_ListUsedAsAValue_IsSqlsrc126`, reported at the value use with the reason `is used here as one value`.
  - `Parse_ListInATokenDefault_IsSqlsrc126`, for both the SQL default and a `-- token:` marker.
  - `Parse_BareInInATokenDefault_IsSqlsrc126`.
  - `Parse_ListWithJsonMarker_IsSqlsrc126`.
  - `Parse_ListMarker_GivesTheElementsTypeAndTheListsNullability`.
  - `Parse_ListSpelledTwoWays_IsOneList`: `IN @IDS` with `@ids IS NULL` under postgres gives one parameter.
- [ ] **Step 2: Change the numbering test.** `ForParseError_EveryKind_HasItsOwnDescriptorInTheFileRange` now expects `SQLSRC101` + index for the first 21 kinds, and `SQLSRC126` + (index − 21) after them. Its comment says why: SQLSRC122 to SQLSRC125 are placement errors.
- [ ] **Step 3: Run the tests and confirm they fail.**
  Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlFileParserParameterTests|FullyQualifiedName~SqlDiagnosticsTests|FullyQualifiedName~DiagnosticsDocumentTests"`
  Expected: FAIL.
- [ ] **Step 4: Implement.**
  - `Create` reads `query.Uses[i]` for the i-th span (`Value` when `Uses` is empty), and records each use's kind with its span.
  - After the loop, a parameter with any `List` use gets `IsList`, and `HasNullTest` when it also has a `NullTest` use.
  - Each `Value` use of a list reports SQLSRC126 at that use, through `sql.ToSourceSpan(span)`.
  - Default parameters are checked against the final list.
  - `Declare` reports `-> json` on a list.
  - Add the `docs/manual/diagnostics.md` section with a `sql error` sample and a "See:" link to `parameters-and-tokens.md#list-parameters`.
- [ ] **Step 5: Run the tests and confirm they pass.** Same command. Expected: PASS.
- [ ] **Step 6: Commit** with the message `List parameters in the parameter list, and SQLSRC126`.

### Task 7: List segments

**Files:**
- Create: `src/SqlSource/Parsing/Tokens/SqlListSegments.cs`
- Modify:
  - `src/SqlSource/Parsing/Tokens/SqlSegmentKind.cs`;
  - `src/SqlSource/Parsing/SqlFileParser.cs`, in `BuildQuery` and `BuildKept` (:411-430);
  - `src/SqlSource/Parsing/SqlQueryHash.cs:37-50`;
  - `src/SqlSource/Generation/Emitting/MethodWriter.cs` (`GetSql`, `AppendSteps`, and the length in `AppendCreate`);
  - `src/SqlSource/Generation/Emitting/TypeEmitter.cs:207-232`.
- Test: `tests/SqlSource.Tests/Parsing/Tokens/SqlListSegmentsTests.cs` (create), `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs`, `tests/SqlSource.Tests/Generator/` constants tests

**Interfaces:**
- Consumes: `SqlParameterUse` (Task 5), and `SqlQueryParameter.IsList` (Task 6).
- Produces:
  - `SqlSegmentKind.List` and `SqlSegmentKind.ListNullTest`. Their `SqlSegment.Text` is the parameter exactly as written, with its prefix, for example `@IDS`.
  - `SqlListSegments.Split(EquatableArray<SqlSegment> segments, EquatableArray<SqlTokenOccurrence> occurrences, SqlBlockText sql, ImmutableArray<SqlParameterUse> uses, EquatableArray<SqlQueryParameter> parameters) -> EquatableArray<SqlSegment>`. It splits the `Literal` segments at the uses of parameters with `IsList`. It returns `segments` unchanged, with no allocation, when no parameter is a list. Literal offsets are tracked by walking the segments: a literal adds its length, and a token jumps to its occurrence's `Span.End`.
  - The hash, `MethodWriter.GetSql`, a constant, and the token method of the `sql` and `models` outputs all write a list segment's `Text` as literal text.
  - `MethodWriter.AppendCreate` gains a last parameter, `bool expandsLists`. With `false`, list segments are literal text.

- [ ] **Step 1: Write the failing tests.**
  - `Split_ListUse_IsASegmentOfItsOwn`: `WHERE id IN @ids;` gives `[Literal "WHERE id IN ", List "@ids", Literal ";"]`.
  - `Split_NullTestAndListUse`: gives `ListNullTest` and `List` segments.
  - `Split_NoList_ReturnsTheSameInstance`.
  - `Split_WithTokens_KeepsTokenSegments`.
  - `Split_KeptForm_SplitsTheSameUses`: `IN /* ids */ @ids` under `keep-comments`.
  - `Hash_QueryWithAList_IsTheHashOfItsText`: the hash equals the one computed over the same SQL as a single literal segment.
  - `Constant_QueryWithAList_IsTheSqlAsWritten` under `-- output: sql`.
  - `TokenMethod_QueryWithAListAndAToken_KeepsTheListAsWritten` under `-- output: models`.
- [ ] **Step 2: Run the tests and confirm they fail.**
  Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlListSegmentsTests|FullyQualifiedName~SqlQueryHashTests|FullyQualifiedName~Constant_QueryWithAList|FullyQualifiedName~TokenMethod_QueryWithAList"`
  Expected: FAIL.
- [ ] **Step 3: Implement.**
  - Call `Split` in `BuildQuery` after `SqlParameterList.Create`, with `scanned.Segments`. Call it in `BuildKept` with the kept scan's segments, the kept `SqlBlockText`, and its own `SqlListUses.Find` over the same lexemes (so kept spans line up).
  - `TypeEmitter` uses `MethodWriter.GetSql(segments)` in place of `segments[0].Text`.
  - `HasToken` stays tokens-only.
- [ ] **Step 4: Run the tests and confirm they pass.** Same command, and confirm the golden values in `SqlQueryHashTests` are untouched. Expected: PASS.
- [ ] **Step 5: Commit** with the message `Split the uses of a list parameter into segments of their own`.

### Task 8: The describe-time stand-in

**Files:**
- Modify: `src/SqlSource.Tool/Describing/SampleSql.cs`, `docs/architecture/tool.md` (the section on the sample SQL)
- Test: `tests/SqlSource.Tool.Tests/Describing/SampleSqlTests.cs`, and describer tests in `tests/DatabaseTests/Postgres/` and `tests/DatabaseTests/SqlServer/` (the describer test classes that describe parameters, beside their existing parameter cases)

**Interfaces:**
- Consumes: `SqlSegmentKind.List` and `ListNullTest` (Task 7).
- Produces: `SampleSql.Build` writes a `List` segment as `(` + `Text` + `)`, and a `ListNullTest` segment as `NULL`.

- [ ] **Step 1: Write the failing tests.**
  - `Build_ListUse_IsParenthesised`: `WHERE id IN @ids` gives `WHERE id IN (@ids)`.
  - `Build_NullTest_IsNull`: `(@ids IS NULL OR id IN @ids)` gives `(NULL IS NULL OR id IN (@ids))`.
  - On each engine, add `Describe_ListParameter_HasTheElementsType`: `SELECT id FROM accounts WHERE id IN @ids` describes `@ids` as `integer` on PostgreSQL and `int` on SQL Server.
  - Add `Describe_OptionalListFilter_IsDescribed`: `(@ids IS NULL OR id IN @ids)` describes without SQLSRC227.
  - Add `Describe_ListUsedTwice_IsDescribed`: `id IN @ids OR account_id IN @ids`.
- [ ] **Step 2: Run the tests and confirm they fail.**
  Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~SampleSqlTests"`
  Expected: FAIL.
- [ ] **Step 3: Implement `SampleSql.Build`.**
- [ ] **Step 4: Run the tests on each engine's default and floor images.**
  Run:
  - `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~SampleSqlTests"`
  - `dotnet test tests/DatabaseTests/Postgres --filter "FullyQualifiedName~Describe_ListParameter|FullyQualifiedName~Describe_OptionalListFilter|FullyQualifiedName~Describe_ListUsedTwice"`
  - the same filter on `tests/DatabaseTests/SqlServer`
  - the last two again with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` and `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`

  Expected: PASS everywhere.
- [ ] **Step 5: Commit** with the message `Describe a list parameter as one value of its element`.

### Task 9: `IEnumerable<E>` properties for lists

**Files:**
- Modify: `src/SqlSource/Generation/Models/ModelReader.cs` (`ReadParameters`)
- Test: the model tests file from Task 2, and `SharedModelTests.cs`

**Interfaces:**
- Consumes: `ParameterSequence.List` (Task 2), and `SqlQueryParameter.IsList` and `HasNullTest` (Task 6).
- Produces: a list parameter's property has `Sequence = List`, `Type` = the element's `PropertyType` exactly as an ordinary parameter of the described type would get it, and `IsNullable = sidecar.Nullable == true || query.Parameters[index].HasNullTest`.

- [ ] **Step 1: Write the failing tests.**
  - `InputModel_ListParameter_IsIEnumerableOfTheElement`, in both outputs: `IEnumerable<int> Ids`.
  - `InputModel_ListWithNullTest_IsNullable`: `IEnumerable<int>? Ids`.
  - `InputModel_ListWithNullMarker_IsNullable`.
  - `InputModel_ListWithMarkerClrType_IsIEnumerableOfThatType`: `-> AccountId` gives `IEnumerable<global::App.AccountId>`.
  - `InputModel_ListOfNodaTimeInCodeGen_TakesTheLibrarysElement`: a `timestamptz` list in `codegen` gives `IEnumerable<global::NodaTime.Instant>`, and `IEnumerable<global::System.DateTime>` in `models`.
  - `SharedInputModel_ListInBothOutputs_IsOneModel`: no SQLSRC257.
- [ ] **Step 2: Run the tests and confirm they fail.**
  Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~InputModel_List|FullyQualifiedName~SharedInputModel_List"`
  Expected: FAIL.
- [ ] **Step 3: Implement it in `ReadParameters`.**
- [ ] **Step 4: Run the tests and confirm they pass.** Same command. Expected: PASS.
- [ ] **Step 5: Commit** with the message `List parameters are IEnumerable<T> in input models`.

### Task 10: `no-list-padding`

**Files:**
- Modify:
  - `src/SqlSource/Settings/GeneratorParameters.cs`;
  - `src/SqlSource/Settings/GeneratorParameterList.cs:30-39`;
  - `src/SqlSource/Settings/QuerySettings.cs:70`;
  - `src/SqlSource/Generation/Methods/QueryMethod.cs`;
  - `src/SqlSource/Generation/Methods/MethodPlanner.cs:301`;
  - `schemas/settings-v1.schema.json:54-57`;
  - `docs/manual/settings.md` (the Values cell at :43, and the table under `#setting-generator-parameters`);
  - `docs/manual/diagnostics.md` (the name lists in SQLSRC019 and SQLSRC109).
- Test:
  - `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`;
  - `tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs:21-22`;
  - `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs:60-76`;
  - `tests/SqlSource.Tests/SettingsFiles/SettingsFileSchemaTests.cs`.

**Interfaces:**
- Produces: `GeneratorParameters.NoListPadding = 128`, the name `no-list-padding`, `QuerySettings.PadsLists => (Parameters & GeneratorParameters.NoListPadding) == 0`, and `QueryMethod` gains `bool PadsLists` after `UseArrayPool`.

- [ ] **Step 1: Add an `InlineData` row for `no-list-padding`** in each test that lists the generator parameters, and add `PadsLists_WithoutTheParameter_IsTrue` and `PadsLists_WithTheParameter_IsFalse` to `QuerySettingsTests`.
- [ ] **Step 2: Run the tests and confirm they fail.**
  Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~QuerySettingsTests|FullyQualifiedName~SqlGeneratorParameterScopeTests|FullyQualifiedName~DiagnosticsDocumentTests|FullyQualifiedName~SettingsFileSchemaTests"`
  Expected: FAIL.
- [ ] **Step 3: Implement**, following `no-array-pool` at every listed place.
- [ ] **Step 4: Run the tests and confirm they pass.** Same command. Expected: PASS.
- [ ] **Step 5: Commit** with the message `Add the generator parameter no-list-padding`.

### Task 11: Expand lists in query methods

**Files:**
- Create: `src/SqlSource/Generation/Emitting/QueryMethods/ListWriter.cs`, `src/SqlSource/Generation/Methods/ListNames.cs`
- Modify:
  - `src/SqlSource/Generation/Methods/QueryMethod.cs` (`MethodParameter`);
  - `src/SqlSource/Generation/Methods/MethodPlanner.cs` (`TryReadInput`);
  - `src/SqlSource/Generation/Emitting/BindingContext.cs`;
  - `src/SqlSource/Generation/Emitting/ParameterBinder.cs`, `PostgresParameterBinder.cs` and `SqlServerParameterBinder.cs`: every place that writes `context.Parameters + "." + bound.Property` or the literal `ParameterName`;
  - `src/SqlSource/Generation/Emitting/QueryMethods/CommandWriter.cs` (`AppendSql`, `AppendCommand`, `AppendBindings`);
  - `src/SqlSource/Generation/Emitting/QueryMethods/HeaderWriter.cs` (`AppendChecks`, and the `<exception>` documentation);
  - `src/SqlSource/Generation/Emitting/QueryMethods/QueryMethodLocals.cs`;
  - `src/SqlSource/Generation/Emitting/MethodWriter.cs` (`AppendCreate` with `expandsLists: true`).
- Test:
  - `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/QueryMethodWriterTests.cs`;
  - `tests/SqlSource.Tests/Generation/Methods/ListNamesTests.cs` (create);
  - a new corpus part `tests/SqlSource.Testing/Generator/CorpusListScenarios.cs`, listed in `QueryMethodCorpus.Shared` as `lists` and `lists-no-padding`, with its golden files `tests/SqlSource.Tests/Generator/Golden/{postgres,sqlserver}/lists{,-no-padding}.txt`.

**Interfaces:**
- Consumes: segments (Task 7), `Sequence` (Task 2), `PadsLists` (Task 10), and `HasNullTest` (Task 6).
- Produces:
  - `MethodParameter` gains `ParameterSequence Sequence` (from Task 3), `bool HasNullTest = false`, and `string? ExpansionPrefix = null`. The prefix is set for a list: its name and suffix, for example `ids_`.
  - `ListNames.PrefixOf(SqlQueryParameter list, int ordinal, EquatableArray<SqlQueryParameter> all, DatabaseEngine engine) -> string`. The base is `Name + "_"`. While any parameter of the query other than this list matches base + one or more digits (by `SqlParameterName.Comparer`), it appends another `_`. On SQL Server, when the base's length + 4 is over 127, it uses `"list" + ordinal + "_"` with the same loop.
  - `ListWriter.ParameterLimitOf(DatabaseEngine engine) -> int`: a switch over the engine, `PostgreSql => 65535`, `SqlServer => 2100`.
  - `BindingContext` gains `string? ValueExpression` and `string? NameExpression`. When they are set, the binders write them in place of `parameters.Property` and the literal name.
- The generated shape of a method with lists, in order:
  1. **Beside the argument checks**, before any await or iterator (`HeaderWriter`), for each list:
     - a local holding the property's value, replaced by `global::System.Linq.Enumerable.ToArray(value)` unless it is an `ICollection<E>` or an `IReadOnlyCollection<E>`;
     - its count, which is 0 for null;
     - its size: the padded count under `PadsLists`, otherwise the count, and 1 for an empty list, for the `<prefix>0` of the empty form.
  2. **The total**: the ordinary parameters, plus one for each list with a null test, plus each list's size. If padding makes it pass the limit, the sizes become the exact counts (1 when empty). If the total still passes the limit, the method throws `new global::System.ArgumentException("The query '<QueryName>' would send " + total.ToString(global::System.Globalization.CultureInfo.InvariantCulture) + " parameters; <Engine> accepts <limit>.  Pass fewer values in '" + <the property of the largest list> + "'<advice>.", "parameters")`.
     - `<Engine>` is `PostgreSQL` or `SQL Server`.
     - `<advice>` is `, or send them as an array with = ANY` on PostgreSQL, and `, or use a table-valued parameter` on SQL Server.
  3. **The SQL**: `string.Create`, whose state holds the tokens' text and the lists' counts and sizes. A `List` segment writes `(@<prefix>0, @<prefix>1, …)` up to its size, or `(SELECT @<prefix>0 WHERE 1 = 0)` when the count is 0. A `ListNullTest` segment writes its `Text`.
     - Digits are written with `TryFormat(span, out var written, default, CultureInfo.InvariantCulture)`.
     - The length uses a static local function `DigitsUpTo(int size)`, the total digits of 0 … size − 1.
     - The `CA2100` suppression covers a method with lists, with the reason `The text is the query's own, with a parameter for each value of a list.`
  4. **The bindings**:
     - An ordinary parameter is bound as today.
     - A list binds each element through the engine's binder, with `ValueExpression` set to the loop's element and `NameExpression` to `"<prefix>" + index.ToString(CultureInfo.InvariantCulture)`. It then repeats the last element up to the size.
     - When the count is 0, it binds `<prefix>0` with the element `MethodParameter` `with { IsNullable = true }` and the value expression `default(E?)`.
     - A list with a null test binds its own name: on PostgreSQL `NpgsqlParameter<bool?>` with `DataTypeName = "pg_catalog.bool"` and `TypedValue = parameters.X is null ? null : true`; on SQL Server `SqlDbType.Bit` with `Value = parameters.X is null ? DBNull.Value : true`.
  5. **The documentation** gains `<exception cref="global::System.ArgumentException">` with the text `The lists of <paramref name="parameters" /> hold more values than <Engine> accepts in one query.`

- [ ] **Step 1: Write the failing tests.**
  - `ListNamesTests`:
    - `ListNames_Plain_IsTheNameAndAnUnderscore`: `ids` gives `ids_`.
    - `ListNames_AnotherParameterMatchesTheSuffix_AddsAnUnderscore`: `ids` beside `ids_0` gives `ids__`.
    - `ListNames_ComparesIgnoringCase`: `ids` beside `IDS_3`.
    - `ListNames_TooLongForSqlServer_UsesTheOrdinal`.
  - The corpus scenario `lists`, with these queries (each `SELECT id FROM accounts …`):
    - `ListIds` (`WHERE id IN @ids`);
    - `ListExcept` (`NOT IN`);
    - `ListTwoLists`;
    - `ListUsedTwice`;
    - `ListOptional` (null test);
    - `ListWithToken`;
    - `ListBesideItsExpansionName` (`@ids` and `@ids_0`);
    - `ListKeepsComments` (`-- generator: keep-comments` with `IN /* c */ @ids`);
    - `StreamIds` (`-- collection-type: IAsyncEnumerable`);
    - `ListConverted` (an element converter);
    - `ListOneShape` (`-> one-optional`).
  - The corpus scenario `lists-no-padding`, with `ListIds` under `-- generator: no-list-padding`.
  - In `QueryMethodWriterTests`: `Lists_LimitCheck_ComesBeforeTheIterator`, which asserts that the `throw new global::System.ArgumentException(` line of `StreamIds` comes before `ReadRows(` in the output.
- [ ] **Step 2: Run the tests and confirm they fail.**
  Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~ListNamesTests|FullyQualifiedName~QueryMethodCorpusTests|FullyQualifiedName~Lists_"`
  Expected: FAIL, because the golden files are missing.
- [ ] **Step 3: Implement** `ListNames`, the `BindingContext` overrides, and `ListWriter`, which writes the counting, padding, limit and binding loops called from `HeaderWriter` and `CommandWriter`. Then wire `MethodPlanner`.
- [ ] **Step 4: Write the golden files, then read every one of them.**
  - Run: `SQLSOURCE_UPDATE_GOLDEN=1 dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~QueryMethodCorpusTests"`
  - Check each generated method against the shape above.
  - `git diff` must show no change to an existing golden file.
- [ ] **Step 5: Run `dotnet test tests/SqlSource.Tests` and `dotnet test tests/SqlSource.Tests.RoslynFloor`.** Expected: PASS.
- [ ] **Step 6: Commit** with the message `Expand list parameters in query methods`.

### Task 12: Lists end to end on both engines

**Files:**
- Create:
  - `tests/DatabaseTests/Postgres/EndToEnd/Queries/Lists.sql`, `ListQueries.cs` and `ListQueryTests.cs`;
  - the same three under `tests/DatabaseTests/SqlServer/EndToEnd/`.
- Modify:
  - `tests/DatabaseTests/Postgres.Npgsql8/Postgres.Npgsql8.csproj`, to link `Lists.sql` as it links `AllTypes.sql`;
  - the query-file tables in `tests/DatabaseTests/Postgres/README.md:104-118` and `tests/DatabaseTests/SqlServer/README.md`;
  - `src/SqlSource/Generation/Emitting/QueryMethods/ListWriter.cs`, if Step 3 changes a limit.
- Sidecars: `tools/describe-end-to-end.sh`.

**Interfaces:**
- Consumes: the methods generated by Task 11.

- [ ] **Step 1: Write the queries**, the same in each engine's `Lists.sql`:
  - `ListAccountsByIds` (`WHERE id IN @ids ORDER BY id`);
  - `ListAccountsExcept` (`NOT IN @ids`);
  - `ListAccountsOptional` (`(@ids IS NULL OR id IN @ids)`);
  - `ListAccountsByIdsAndStatuses` (two lists);
  - `ListAccountsUsedTwice` (`id IN @ids OR id IN @ids`);
  - `StreamAccountsByIds` (`-- collection-type: IAsyncEnumerable`);
  - `ListAccountsByIdsNoPadding` (`-- generator: no-list-padding`).
- [ ] **Step 2: Write the tests**, using the seeded accounts and the method named in each test:
  - `Ids_EachKindOfCollection_FindsTheRows`, a theory over an array, `List<int>`, `HashSet<int>` and a lazy LINQ sequence;
  - `Ids_LazySequence_IsEnumeratedOnce`;
  - `Ids_CountsAcrossEachPaddingStep_FindTheRows`, a theory over 1, 2, 3, 5, 512, 513 and 1025;
  - `Ids_Empty_FindsNoRows`;
  - `Except_Empty_FindsEveryRow`;
  - `Optional_Null_FindsEveryRow`;
  - `Optional_Empty_FindsNoRows`;
  - `Optional_Values_Filters`;
  - `TwoLists_Filter`;
  - `UsedTwice_Filters`;
  - `NoPadding_Filters`;
  - `Ids_AtTheLimit_Runs`, which sends exactly the limit minus the ordinary parameters;
  - `Ids_OneOverTheLimit_ThrowsArgumentException`, which checks the message and `ParamName == "parameters"`;
  - `Ids_PaddingWouldPassTheLimit_SendsExactCounts`, with 2000 values on SQL Server, and 65000 on PostgreSQL;
  - `StreamIds_OverTheLimit_ThrowsBeforeEnumeration`.
- [ ] **Step 3: Write the sidecars and run the tests.**
  - Run `tools/describe-end-to-end.sh` and read the diff.
  - Run `dotnet test tests/DatabaseTests/Postgres --filter "FullyQualifiedName~ListQueryTests"`, then the same on `Postgres.Npgsql8` and `SqlServer`.
  - Expected: PASS.
  - If `Ids_AtTheLimit_Runs` fails because the server refuses a count below the constant (SQL Server's `sp_executesql` takes parameters of its own), set `ParameterLimitOf` to the largest count that runs. Change the constant, the test and the docs together, and rerun Task 11's goldens.
- [ ] **Step 4: Run the SQL Server tests on the floor and nightly images**, and the PostgreSQL tests on `postgres:14.24`. Run: the same filters with `SQLSOURCE_TESTS_SQLSERVER_IMAGE` and `SQLSOURCE_TESTS_POSTGRES_IMAGE` set as in Global Constraints. Expected: PASS.
- [ ] **Step 5: Commit** with the message `List parameters end to end on PostgreSQL and SQL Server`.

### Task 13: Docs, examples and AGENTS.md

**Files:**
- Modify:
  - `docs/manual/parameters-and-tokens.md`: a new `<a id="list-parameters"></a>` section after "Giving a parameter a type", and known issues;
  - `docs/manual/generator.md`: "Property types", "Which properties can be null", "How a parameter is sent", and the method's exceptions;
  - `docs/manual/postgresql.md`: `= ANY` versus `IN @list`;
  - `docs/manual/sqlserver.md`: lists, the limit, and table-valued parameters or `OPENJSON` for large lists;
  - `docs/manual/settings.md`: what `no-list-padding` does;
  - `docs/manual/recipes.md`: an optional list filter;
  - `docs/manual/README.md`: a "What works today" row, "List parameters: `IN @list`, expanded by the query methods on PostgreSQL and SQL Server", and a "route by need" row;
  - `README.md`, if its FAQ or getting-started mentions filtering by a list (check first);
  - `docs/architecture/generator.md`: "Parameters" (classification), "Token methods" and "Query methods" (lists in the SQL built at run time, binding a list, the limit), and "Models" (`ParameterSequence`);
  - `src/SqlSource/Parsing/AGENTS.md`: a rule that `SqlListUses` and `SqlParameterList` share the uses' order with `SqlBlockText.Parameters`, and that list segments hash as text;
  - `src/SqlSource/Generation/Emitting/QueryMethods/AGENTS.md`, if `ListWriter` needs a rule.
- Create: none. The examples change in place:
  - `examples/PostgresExamples/CodeGen/OrderQueries.sql` and `examples/SqlServerExamples/CodeGen/OrderQueries.sql`: add `ListOrdersByStatuses` (an optional list filter) to each;
  - the "What is in it" tables of the examples' READMEs;
  - their sidecars, through `tools/describe-end-to-end.sh`.
- Test: `tests/SqlSource.Tests/Documentation/` (links, `sql` samples, AGENTS.md sizes), and the examples' build.

- [ ] **Step 1: Write the manual sections.**
  - Cover: the forms, the null test, markers on lists, padding and `no-list-padding`, empty and null lists, the limit and what to write instead, `position(… IN @p)`, lists in token defaults (SQLSRC126), and a null element of a reference type in `NOT IN`.
  - Each `sql` sample's info string names its dialect, or says `error`.
- [ ] **Step 2: Update the architecture and AGENTS.md files.**
- [ ] **Step 3: Update the examples and run `tools/describe-end-to-end.sh`.**
- [ ] **Step 4: Check and commit.**
  - Run `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Documentation"` and `./pre-commit-validation.sh`. Expected: PASS.
  - Commit with the message `Document list parameters, and show one in each example`.

### Task 14: PR B

- [ ] **Step 1: Run the version check.** Run `git fetch --tags origin`, then `git tag --list 'v*' --sort=-v:refname | head -n 1`. Expected: no tag, or one below `VersionPrefix`.
- [ ] **Step 2: Run `./pre-commit-validation.sh --full`.** Expected: PASS.
- [ ] **Step 3: Run `branch-review:review`** and address every finding, minor ones included (root `AGENTS.md`, rule 3). Repeat until it is clean.
- [ ] **Step 4: Bring the branch up to date with `main`, push, open PR B, and turn on Auto-fix.**
