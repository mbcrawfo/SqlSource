# TD-0053 PR 3, types, calls and the rest - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix items 6, 7 (but `RunSetOperation`), 8 (but what PR 1 fixed), the `Resolve` methods of 9, and 10 of TD-0053, then resolve the item, with no answer and no logged text changed.

**Architecture:** Named OIDs and the set of modifier types in a new `BuiltInTypes`; one generic walk over `typtype` that the build with a modifier and `PairsOf` share; one call helper and the call-name constants beside a `PostgresSession` split into three partial files; checked type rows and found origins carried as values from one step to the next; small helpers in `PostgresLive`, `LogValue` and `CaptureJson`.

**Tech Stack:** C# (.NET 8 tool, net10.0 tests), xUnit, Shouldly, Npgsql, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-10-17-td-0053-3-types-and-calls-design.md`, under the overview `docs/superpowers/specs/2026-10-17-td-0053-postgresql-describer-patterns-design.md`.

Paths are under `src/SqlSource.Tool/Describing/Postgres/` unless they say otherwise.

## Global Constraints

- No fixture, `capture.json`, `expected.json` or `expected-pg*.txt` changes; nothing is re-recorded.
- The text of every log event stays as it is; call names keep today's values: `open`, `prepare`, `columns`, `tables`, `types`, `spellings`, `check`, `explain`, `partitions`, `relations`, `deallocate`.
- The internal SQL of `PostgresLive` may change; the user's SQL in `check` keeps its named parameters.
- `PostgresTypes.Build`, `PairsOf`, `IsPseudo`, `ColumnTypes.OidOf`, `PostgresColumns.Build`, `PostgresBindNames.Of` keep taking the dictionary.
- `ColumnOrigin.Find` and `MissingFrom` stay; their tests do not change.
- A file renamed, moved or split updates any `AGENTS.md` that names it, in the same commit.
- Every commit passes `./pre-commit-validation.sh`; the container tests also pass with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.

## Review Focus

- A server that resolves more `$n` than the tool's lexer found: `EXPLAIN EXECUTE` gets `resolved.Count` arguments, not the request's count (existing `PostgresSessionStatementTests` and the container tests; Task 4 keeps the count from the prepare's answer).
- An enum whose row has no labels: the build with a modifier still throws, `PairsOf` still answers none (existing `PostgresTypesTests`; Task 2 must not read the labels in the pairs walk).
- A pseudo-type parameter or column: `PairsOf` gives none and runs before SQLSRC226 and SQLSRC229, so the refusal stays the answer (existing session tests).
- A column whose table was dropped between `columns` and `tables`: `FindAll` gives the first missing column's message in column order (Task 4's `FindAll` tests).
- A caller that changes `CaptureJson.Options`: it throws `InvalidOperationException` (Task 3's test).

---

### Task 1: `BuiltInTypes` and the types that carry a modifier

**Files:**
- Create: `BuiltInTypes.cs`, `tests/SqlSource.Tool.Tests/Describing/Postgres/BuiltInTypesTests.cs`
- Modify: `Walk/PlanExpression.cs` (`TypesByName` and its `["boolean"]` reads), `Walk/SetOperationTypes.cs`, `PostgresTypes.cs` (`Int2vector`, `Oidvector`), `PostgresTypeNames.cs` (`InCatalog` to `internal`), `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/SetOperationTypesTests.cs`, `.../Walk/PlanExpressionTests.cs`, `tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs`

**Interfaces:**
- Produces: `internal static class BuiltInTypes` with `public const long Bool = 16, Int8 = 20, Int2 = 21, Int2vector = 22, Int4 = 23, Text = 25, Oidvector = 30, Float4 = 700, Float8 = 701, Bpchar = 1042, Varchar = 1043, Date = 1082, Timestamp = 1114, Timestamptz = 1184, Numeric = 1700` (each named so that its lower-cased name is its `typname`), and `public static IReadOnlyList<string> WithModifier` (`bpchar`, `varchar`, `bit`, `varbit`, `numeric`, `time`, `timetz`, `timestamp`, `timestamptz`, `interval`); `internal static IReadOnlyList<IReadOnlyList<long>> SetOperationTypes.Families`, from which `FamilyOf` is built.

- [ ] **Step 1:** Unit theory `BuiltInTypesTests.WithModifier_EachMember_HasFacetsInEverySwitch` over `WithModifier`: `TypeModifiers.Decode(typname, 10)` is not `PostgresFacets.None`; `ColumnFacets.Of(typname, column with Length 5, Precision 5, Scale 2)` is not `None`; `PostgresTypeNames.InCatalog(row of pg_catalog with that typname and FormatType, new PostgresFacets(5, 5, 2))` is not the row's `FormatType`.  Container tests in `PlanOutputTests`: `Catalog_BuiltInTypes_AreNamedSo` sends every `public const long` of `BuiltInTypes` (by reflection) as one `oid[]` and checks each row is `pg_catalog` and the constant's name lower-cased; `Catalog_TypesWithAModifier_AreTheBuiltInOnesWithATypmodInput` checks `WithModifier` equals the `typname`s of `pg_catalog` types with `typmodin <> 0` and `typcategory <> 'A'`.  `Catalog_CastFamilies_AreBuiltInBothWays` takes its families from `SetOperationTypes.Families` (`unnest($1::int4[], $2::oid[])`) and expects `Σ n(n−1)` pairs, which is 42, and none missing.
- [ ] **Step 2:** Run them; they fail to compile.
- [ ] **Step 3:** Add `BuiltInTypes`; use the constants in `TypesByName` (and `BuiltInTypes.Bool` where it reads `["boolean"]`), in `Families`, in `IsArray`, and in `SetOperationTypesTests` and `PlanExpressionTests`' `InlineData`.
- [ ] **Step 4:** `dotnet test tests/SqlSource.Tool.Tests` and the three container tests pass.
- [ ] **Step 5:** Commit.

### Task 2: One walk for the build and its pairs, one lookup of a row

**Files:**
- Modify: `PostgresTypes.cs`, `ColumnTypes.cs`, `PostgresColumns.cs`
- Test: existing `PostgresTypesTests`, `ColumnTypesTests`, `PostgresColumnsTests`

**Interfaces:**
- Produces: `internal static PostgresTypeRow PostgresTypes.Row(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows, string? owner = null)`, whose message is today's with ` of {owner}` before the full stop when given.

- [ ] **Step 1:** One private generic walk, `Walk<T>(long oid, rows, int modifier, ITypeWalk<T> walk)`, holds today's `switch` of the build with a modifier (array, base, domain with `BaseModifierOf`, enum, range, multirange, composite, else).  A private `ITypeWalk<T>` has a member for each arm.  The build's implementation does what `Build` does today, the throw included; the pairs' gives the pair at a base with a modifier, passes the inner pairs through an array, a domain, a range and a multirange, and gives none for an enum, a composite and the rest, reading no labels.  The facets `Build` keeps its switch, with a remark: a domain passes its facets down and an array's element gets none, which the walk's one modifier cannot say.
- [ ] **Step 2:** `ColumnTypes.RowOf` goes; `OidOf` calls `PostgresTypes.Row`.  `PostgresColumns.FacetsOf` calls `PostgresTypes.Row(column.TypeOid, rows, $"the column '{column.Name}'")`.
- [ ] **Step 3:** `dotnet test tests/SqlSource.Tool.Tests` passes unchanged.
- [ ] **Step 4:** Commit.

### Task 3: Small repeats, the `Resolve` remarks and read-only options

**Files:**
- Modify: `src/SqlSource.Tool/Logging/LogValue.cs`, `PostgresEvents.cs`, `PostgresLive.cs`, `PostgresSession.cs` (`VersionOf` out), `RowDescriptions.cs`, `BatchRewrites.cs`, `src/SqlSource.Tool/Describing/Capture/CaptureJson.cs`
- Test: create `tests/SqlSource.Tool.Tests/Logging/LogValueTests.cs`; modify `.../Describing/Postgres/PostgresDescriberTests.cs` (the `VersionOf` theory moves to `PostgresLiveTests.cs`), `.../Describing/Capture/CaptureJsonTests.cs`

**Interfaces:**
- Produces: `public static LogValue LogValue.Of(bool? flag)`; `public static string PostgresLive.VersionOf(string serverVersion)`.

- [ ] **Step 1:** Tests: `LogValueTests.Of_Flag_IsItsValueOrNull` (`true` → `new LogValue.Flag(true)`, `false` → `Flag(false)`, `null` → `LogValue.Null`); `CaptureJsonTests.Options_AreReadOnly` (`IsReadOnly` is true, and setting `WriteIndented` throws `InvalidOperationException`); `PostgresLiveTests.VersionOf_ServerVersion_IsCutAtItsFirstSpace`, today's theory on `PostgresLive`.
- [ ] **Step 2:** Run them; they fail.
- [ ] **Step 3:** Add `Of(bool?)` and use it at the six conversions of `PostgresEvents`.  Move `VersionOf`.  In `PostgresLive`: `IsDriverRefusal(Exception)` (`NotSupportedException`, `InvalidCastException`, `ArgumentException`) as a `when` filter after the `NpgsqlException` catch, in `CheckAsync` and `RefusalOfAsync`; `OidArray(IEnumerable<long>)` for `tables`, `types` and the OIDs of `spellings`; `AddRelations(NpgsqlCommand, IEnumerable<PostgresRelationName>)` for `relations` and `partitions`; `TablesSql` and `TypesSql` on `$1`, `ParameterTypesSql` on `$1` and `$2`, with unnamed parameters; `OpenSql` a `static readonly` built from `SettingNames` after `server_version`.  Add the `<remarks>` to both `Resolve`s: only the tests call it, to pin the reflection against the pinned Npgsql without a server.  `CaptureJson.Options` calls `MakeReadOnly(populateMissingResolver: true)` in its initializer.
- [ ] **Step 4:** `dotnet test tests/SqlSource.Tool.Tests` passes; the container tests pass.
- [ ] **Step 5:** Commit.

### Task 4: `PostgresSession`, its calls and its values

**Files:**
- Create: `PostgresCallNames.cs`, `PostgresSession.Calls.cs`, `PostgresSession.Builders.cs`
- Modify: `PostgresSession.cs`, `PostgresEvents.cs` (event names), `ColumnOrigin.cs`, `src/SqlSource.Tool/AGENTS.md` (line on `ColumnOrigin.MissingFrom`)
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/ColumnOriginTests.cs`

**Interfaces:**
- Produces: `internal static class PostgresCallNames` (`const string` for each call name above, with a remark that a value never changes since every capture records it); `public static Outcome<ColumnOrigin?[], string> ColumnOrigin.FindAll(IReadOnlyList<PostgresColumnRow> columns, IReadOnlyDictionary<long, PostgresTableRow> tables)`; in the session, `AskAsync<TRequest, TAnswer>(string method, TRequest request, Func<TRequest, CancellationToken, Task<TAnswer>> live, Action<TRequest, TAnswer, long> log, CancellationToken)` and `TimedAskAsync` that returns `(TAnswer Answer, long DurationMs)` and logs nothing, for `check`; a private nested `CheckedTypes(IReadOnlyDictionary<long, PostgresTypeRow> Rows)`; a private nested `WalkInput(string Statement, int Arguments, IReadOnlyList<SidecarColumn> Columns, ColumnOrigin?[] Origins)`.

- [ ] **Step 1:** Tests: `FindAll_EveryColumnFound_IsEachOriginInOrder` (a column of `users`, an expression, a system column give `[origin, null, null]`); `FindAll_TwoColumnsMissing_IsTheFirstOnesMessage` (columns `a` of a missing table and `b` of a missing attribute give `MissingFrom`'s message for `a`).
- [ ] **Step 2:** Run them; they fail to compile.
- [ ] **Step 3:** Add `FindAll` (per column, `MissingFrom` else `Find`) and `PostgresCallNames`; `PostgresEvents` writes the ten call events' names from it.
- [ ] **Step 4:** Every call goes through the helper with a constant: the six wrappers (in `PostgresSession.Calls.cs`, with `ElapsedSince`), `explain`, `partitions` and `relations` inline; `check` through `TimedAskAsync`, its verdict read before the log; `open` through the exchange with `PostgresCallNames.Open`, untimed and unlogged.
- [ ] **Step 5:** `MissingType` becomes `CheckTypes(…)` returning `Outcome<CheckedTypes, string>`; `SpellingsOfAsync`, `TypeParameters`, `BuildColumns` and `CheckAsync` take `CheckedTypes`.  `ParameterOids` returns the parameters' OIDs and `resolved.Count`, which reaches the walk through `WalkInput.Arguments`; `ArgumentCount` and its throw go.  `WalkAsync(DescribeRequest, WalkInput, WalkScope, CancellationToken)`.  The origins come from `FindAll`.
- [ ] **Step 6:** Move `RecordedColumns`, `MissingType`'s successor, `ParameterOids`, `TypeParameters`, `CheckParameterOf`, `Described`, `ServerOf` and `MajorVersionOf` to `PostgresSession.Builders.cs`.  Update the `AGENTS.md` line that says `PostgresSession` calls `MissingFrom` before it finds the origins.
- [ ] **Step 7:** `wc -l PostgresSession.cs` is near 450; `dotnet test tests/SqlSource.Tool.Tests` passes unchanged but for `ColumnOriginTests`; the container tests pass on both images.
- [ ] **Step 8:** Commit.

### Task 5: Resolve TD-0053

**Files:**
- Delete: `docs/tech-debt/TD-0053-duplicated-and-uneven-patterns-in-the-postgresql-describer.md`
- Modify: `docs/tech-debt/README.md`

- [ ] **Step 1:** Delete the file and its row (`docs/tech-debt/AGENTS.md`, "Resolving an item").
- [ ] **Step 2:** `grep -rn "TD-0053" . --exclude-dir={bin,obj,.git}` finds nothing outside `docs/superpowers/`.
- [ ] **Step 3:** `./pre-commit-validation.sh` passes; `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test tests/SqlSource.Tests.Postgres` passes; `git diff main --stat -- '*capture.json' '*expected*'` is empty.
- [ ] **Step 4:** Commit.
