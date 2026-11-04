# Closed Sets PR 2: Per-Engine Structures Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** No code in `src/SqlSource` handles the engines by naming each one's field side by side, so adding a `DatabaseEngine` member fails the build at every per-engine value it must decide.

**Architecture:** A `PerEngine<T>` readonly struct holds one slot for each `DatabaseEngine` and compares by value, and `DatabaseEngines.All` lists the engines for every loop.  Values of one type for every engine move into it (approach A).  Values of a different type for each engine, the driver probes and `QueryTyper.OverrideLookup`'s delegates, keep their named fields, and every question asked of them is an exhaustive switch over the engine (approach B).

**Tech Stack:** C# (netstandard2.0 generator on the Roslyn 4.14 floor), xUnit, Shouldly.

**Spec:** `docs/superpowers/specs/2026-11-04-closed-sets-design.md`, section 2, `DatabaseEngines.All` from section 1, and the `AGENTS.md` and `generator.md` parts of section 7 that PR 2 owns.  PR 1 (mbcrawfo/SqlSource#226) is on `main`; PR 3 (sidecar records, describers, equality checks) has its own plan.

## Global Constraints

- **No change to generated code, sidecars, diagnostics or golden files**: the whole suite passes with no golden file updated.
- A test's source may change only where it compiles against an API this plan changes (`SettingsLevel.TypeOverrides`, `TypeKeys.TryNormalizeEach`, `TypeOverrideGrammar.ReadMembers`, the `MethodDrivers` constructor).  What a test asserts and its fixtures stay as they are.  New tests may be added.  No old member is kept for a test's sake.
- `PerEngine<T>` compares by value, so a pipeline value that holds one still caches.  Nothing in this plan adds work for each line of a query.
- **No helper gives a value for an engine it does not know.**  `default(PerEngine<T>)`, `PerEngine.Only` and `With` leave the other slots `default(T)`, so they are used only where `default(T)` means "says nothing" (a null list of entries).  A value each engine must decide is built by `PerEngine.Of` whose function reaches a switch over the engine.
- **No bool names an engine.**  A bool may name a capability of one engine's driver or library, as `MethodDrivers.PostgresAtLeast10` and `SqlServerSpatial` do; those stay.
- Every switch added follows PR 1's rule: it names every member and ends in `_ => throw UnexpectedEnumValueException.Of(value)`, or a `default:` of its own that throws.
- Each `AGENTS.md` rule is at most 80 words and each file at most 8 KB; `tests/SqlSource.Tests/Documentation/AgentsFileTests.cs` checks both.
- Every commit passes `./pre-commit-validation.sh`; every push passes `./pre-commit-validation.sh --full` on the tree as pushed; branch-review comes back clean before the PR.  No describer changes, so no floor-image run is needed.
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## The sites

Re-checked against `main` at `adb4c98d`, after the file-owned settings work (#224) and PR 1.  "Stays" means the field is read only by its own engine's code, which the design says is no gap.

| Site | Today | Approach | Task |
|---|---|---|---|
| `SettingsLevel.TypeOverrides` / `.SqlServerTypeOverrides`, `Over` | Two named nullable arrays | A: `PerEngine<EquatableArray<TypeOverride>?> TypeOverrides`, merged by `Zip` | 3 |
| `SettingsLevelReader.ReadTypeOverrides` | Out parameter for SQL Server's array | A: returns the container | 3 |
| `SqlMarkerScope.Build` | `isSqlServer` bool and two ternaries | A: `PerEngine.Only(Engine, …)` | 3 |
| `QuerySettings.ResolveTypeOverrides` | Switch naming each field | A: index by engine | 3 |
| `FileTypesInput.TypeSettingsOf` | Null checks and copies of each field | A: one check and one copy | 3 |
| `MappedTypeNames.Add` | Reads each field by name | A: loop over `DatabaseEngines.All` | 3 |
| `TypeOverrideLists` | Record of `PostgreSql`, `SqlServer` | A: deleted; `ReadMembers` returns `PerEngine<EquatableArray<TypeOverride>>` | 2 |
| `TypeOverrideGrammar.ReadMembers` | Two lists and two member lists by name | A: per-engine lists, loops | 2 |
| `TypeKeys.TryNormalizeEach` | Two out parameters; a shortcut naming both system schemas | A: `out PerEngine<string?>`, normalizer picked by a switch | 2 |
| `MethodDrivers.Postgres` / `.SqlServer` | Two bools read by `Allows`'s switch | A: `PerEngine<bool> Allowed` | 4 |
| `NpgsqlProbe` / `SqlClientProbe` beside each other (pipeline, `FileTypesInput`, `QueryTyper.Typing`) | Different types | B: `DriverProbes.Of(engine, …)` is the one switch; `IDriverProbe` gains `Referenced` and `IsBelowTheFloor` | 4 |
| `SqlSourceGenerator.ReportMissingDrivers` | Two `if`s on `CodeGenEngines` bits | B: loop; `ToCodeGenEngines`, `DriverProbes.Of`, `DatabaseName`, new `DriverName` | 4 |
| `SqlSourceGenerator.ReportDrivers` (**found by the sweep**) | Two nodes, `usesPostgresEntries` and `usesSqlServerEntries`, two registrations | B: loop; tracking name, dialect and probe each by a switch; same nodes and names | 4 |
| `MethodsInput.AllUses` | Four hand-written (library, engine) rows | A: built from every `SqlLibrary` × `DatabaseEngines.All` | 5 |
| `LibraryRows` `_rows`/`_sqlServerRows`, `_postgisRows`/`_sqlServerSpatialRows`, `RowsOf`'s tuple (**found by the sweep**) | Two pairs of dictionaries, a `(Postgres, SqlServer)` tuple per library | A: `PerEngine<Dictionary<…>>` and `PerEngine<Dictionary<…>?>`; each library's rows by an engine switch | 5 |
| `QueryTyper.OverrideLookup.OfPostgres` / `.OfSqlServer` | Different delegate types, each set by its own switch, each read only in its engine's arm | B, already conforming: no change | — |
| `QueryTyper.Typing`'s `Column` / `SqlClientColumn` / `SqlClientVersionText`, `FileTypesInput`'s two probes | Different types, read only in each engine's arm | B, plumbing: no change | — |
| `TypeKeyNames`' dictionaries, `SidecarTypeReader`'s key arrays, `ParameterBinder`'s binders, `SystemSchemas`, `QuoteReader.IsNameCharacterOf*`, per-driver `SqlDiagnostics` descriptors, `LibraryProbe`'s package flags | Read by their own engine's code | Stays | — |
| `TrackingNames.UsesPostgresEntries` / `UsesSqlServerEntries` | Constants that tests read | Stays; reached through the switch of Task 4 | — |
| `SidecarType` records, `SidecarValues.Engine`, `ConverterTargets` checks, `ToolHost`'s registry | | PR 3 | — |

The tool has no per-engine pair outside `Describing/Postgres/`, `Describing/SqlServer/` and `ToolHost.Create`'s registry, which is PR 3's.  No bool in either project names an engine but `SqlMarkerScope`'s `isSqlServer`.

## Review Focus

- **A level that says nothing must still be `SettingsLevel.None`.**  A settings file's type override whose every member is invalid gives all-null slots, and `SettingsLevelReader.Read` must still return the shared empty level, so `default(PerEngine<T>)` must equal a container of `default(T)` slots.  Task 1 pins the equality; Task 3 adds the reader test.
- **Equal containers must hash equally**, a default one and one built with every slot `default(T)` included, or the pipeline caches wrongly.  Task 1 tests it.
- **An engine value that is no member** reaching the indexer must throw `UnexpectedEnumValueException`, never `IndexOutOfRangeException` or `NullReferenceException`, on a default container too.  Task 1 tests it.
- **The order of the engines' diagnostics.**  `SQLSRC273` and the driver findings are reported PostgreSQL first today; the loops follow `DatabaseEngines.All`, which must be declaration order.  Task 1 tests the order; the generator tests pin the reports.
- **`TryNormalizeEach` without its shortcut** must give the same keys for a user's schema: an array is PostgreSQL's alone, and an escaped part is rewritten for both engines.  Task 2 adds both tests.

---

### Task 1: `DatabaseEngines.All` and `PerEngine<T>`

**Files:**
- Create: `src/SqlSource/Settings/DatabaseEngines.cs`, `src/SqlSource/Settings/PerEngine.cs`
- Test: `tests/SqlSource.Tests/Settings/DatabaseEnginesTests.cs`, `tests/SqlSource.Tests/Settings/PerEngineTests.cs`
- Modify: `docs/architecture/generator.md` ("Records between the steps")

They live beside `DatabaseEngine`, in namespace `SqlSource.Settings`, since `Settings/` may not use `Generation/`.

**Interfaces:**
- Produces:
  - `internal static class DatabaseEngines { public static ImmutableArray<DatabaseEngine> All { get; } }`
  - `internal readonly struct PerEngine<T> : IEquatable<PerEngine<T>>` with `T this[DatabaseEngine engine] { get; }`, `PerEngine<T> With(DatabaseEngine engine, T value)`, `PerEngine<TResult> Select<TResult>(Func<T, TResult> selector)`, `PerEngine<TResult> Zip<TOther, TResult>(PerEngine<TOther> other, Func<T, TOther, TResult> combine)`, `==`, `!=`, `Equals`, `GetHashCode`.
  - `internal static class PerEngine` with `PerEngine<T> Of<T>(Func<DatabaseEngine, T> valueOf)` and `PerEngine<T> Only<T>(DatabaseEngine engine, T value)`.

- [ ] **Step 1: Write the failing tests**

`DatabaseEnginesTests`:
- `All_IsEveryMemberInDeclarationOrder`: `DatabaseEngines.All.ShouldBe([DatabaseEngine.PostgreSql, DatabaseEngine.SqlServer])`.
- `All_MembersRunFromZeroWithNoGaps`: `DatabaseEngines.All.Select(engine => (int)engine).ShouldBe(Enumerable.Range(0, DatabaseEngines.All.Length))`.

`PerEngineTests` (use `string?` slots):
- `Of_EachEngine_HoldsItsValue`: `PerEngine.Of(engine => engine.ToString())[engine]` is `engine.ToString()` for each of `DatabaseEngines.All`.
- `Of_CallsTheFunctionOnceForEachEngineInOrder`: the engines passed, recorded in a list, equal `DatabaseEngines.All`.
- `Default_HoldsDefaultInEverySlot`: `default(PerEngine<string?>)[engine]` is null for each engine.
- `Default_EqualsEverySlotDefault_WithTheSameHash`: `PerEngine.Of<string?>(_ => null)` equals `default(PerEngine<string?>)` by `Equals` and `==`, with equal `GetHashCode`.
- `Only_FillsItsEngineAlone`: `PerEngine.Only<string?>(DatabaseEngine.SqlServer, "a")` gives `"a"` for SQL Server and null for PostgreSQL.
- `With_ReplacesOneSlot_AndLeavesTheOriginal`.
- `With_DefaultInEverySlot_EqualsDefault`: `PerEngine.Only<string?>(DatabaseEngine.SqlServer, "a").With(DatabaseEngine.SqlServer, null)` equals `default`.
- `Select_MapsEachSlot`: `PerEngine.Of(engine => (int)engine).Select(n => n * 10)` gives 0 and 10.
- `Zip_CombinesSlotBySlot`: `PerEngine.Of(engine => (int)engine).Zip(PerEngine.Of(_ => 10), (a, b) => a + b)` gives 10 and 11.
- `Equals_SameValues_IsEqualWithTheSameHash` and `Equals_OneSlotDiffers_IsNotEqual` (`!=` true).
- `Indexer_ValueOfNoMember_Throws`: `Should.Throw<UnexpectedEnumValueException>(() => PerEngine.Only<string?>(DatabaseEngine.SqlServer, "a")[(DatabaseEngine)7])`, and the same on `default(PerEngine<string?>)`.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~DatabaseEnginesTests|FullyQualifiedName~PerEngineTests"`
Expected: build error, `DatabaseEngines` and `PerEngine` not found.

- [ ] **Step 3: Implement `DatabaseEngines.All`**

Built once from `(DatabaseEngine[])Enum.GetValues(typeof(DatabaseEngine))`, which is declaration order while the members run from 0.  Doc comment: every loop over the engines reads it, and `PerEngine<T>` relies on the members running from 0 with no gaps, which `DatabaseEnginesTests` checks.

- [ ] **Step 4: Implement `PerEngine<T>` and `PerEngine`**

- Storage is a private `T[]?`, one slot for each engine by its number.  A default container has no array and holds `default(T)` in every slot.
- `Of`, `Only`, `With`, `Select` and `Zip` all build through one private factory that gives the default container, with no array, when every slot is `default(T)` by `EqualityComparer<T>.Default`.  So a level without a type override allocates nothing for it, as today.
- The indexer checks `(uint)(int)engine < (uint)DatabaseEngines.All.Length` and otherwise throws `UnexpectedEnumValueException.Of(engine)`; a default container gives `default(T)` after the check.
- `Equals` compares slot by slot with `EqualityComparer<T>.Default`; `GetHashCode` folds every slot as `EquatableArray` does (17, then `* 31 +`), a null slot as 0, so a default container hashes as its slots.
- `Of` calls the function once per engine, in the order of `DatabaseEngines.All`.
- Doc comments: the struct says a value of each engine goes here; that `Of`'s function reaches a switch over the engine so a new engine is IDE0072 there; and that `default`, `Only` and `With` leave other slots `default(T)`, for values where that means the source says nothing.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~DatabaseEnginesTests|FullyQualifiedName~PerEngineTests"`
Expected: all pass.

- [ ] **Step 6: Describe them in `docs/architecture/generator.md`**

Under "Records between the steps", add one paragraph: `PerEngine<T>` holds a value of each `DatabaseEngine`, compares by value so a pipeline value that holds one caches, a default container holds `default(T)` in every slot and equals one built so; `Of` builds it from a switch, `Only` and `With` set one slot, `Select` and `Zip` merge slot by slot; `DatabaseEngines.All` is the engines in declaration order, which every loop over them reads.  Link both files.

- [ ] **Step 7: Validate and commit**

```bash
./pre-commit-validation.sh
git add src/SqlSource/Settings/DatabaseEngines.cs src/SqlSource/Settings/PerEngine.cs tests/SqlSource.Tests/Settings/DatabaseEnginesTests.cs tests/SqlSource.Tests/Settings/PerEngineTests.cs docs/architecture/generator.md
git commit -m "refactor: add PerEngine<T> and DatabaseEngines.All"
```

---

### Task 2: The type override's keys and lists by engine

**Files:**
- Modify: `src/SqlSource/Settings/TypeOverrides/TypeKeys.cs` (`TryNormalizeEach`, around line 86-119)
- Modify: `src/SqlSource/Settings/TypeOverrides/TypeOverrideGrammar.cs` (`ReadMembers`, around line 53-112)
- Delete: `src/SqlSource/Settings/TypeOverrides/TypeOverrideLists.cs`
- Modify: `src/SqlSource/Settings/SettingsLevelReader.cs` (`ReadTypeOverrides`)
- Test: `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeysTests.cs`, `tests/SqlSource.Tests/Settings/TypeOverrides/TypeOverrideGrammarTests.cs`
- Modify: `docs/architecture/generator.md` ("The type override", the settings-file bullet that names `TryNormalizeEach` and `TypeOverrideLists`)

**Interfaces:**
- Consumes: `PerEngine<T>`, `PerEngine.Of`, `With`, `Select`, `Zip`, `DatabaseEngines.All` (Task 1).
- Produces:
  - `public static bool TypeKeys.TryNormalizeEach(ReadOnlySpan<char> key, out PerEngine<string?> keys)`: each engine's key, null for an engine that does not read it; returns whether any engine reads it.
  - `public static PerEngine<EquatableArray<TypeOverride>> TypeOverrideGrammar.ReadMembers(IReadOnlyList<KeyValuePair<string, string>> members, SettingPlace place, List<int> invalid, List<(int Index, int First)> repeated)`.

- [ ] **Step 1: Update the tests that call the two methods, and add the shortcut's tests**

Replace `out var postgreSql, out var sqlServer` with `out var keys` and read `keys[DatabaseEngine.PostgreSql]` and `keys[DatabaseEngine.SqlServer]`; replace `lists.PostgreSql` / `lists.SqlServer` likewise.  Change no expected value.  Add, if not already there:
- `TryNormalizeEach_ArrayOfAUsersSchema_IsPostgreSqlsAlone`: `public.tag[]` returns true, PostgreSQL `public.tag[]`, SQL Server null.
- `TryNormalizeEach_EscapedKeyOfAUsersSchema_IsRewrittenForEachEngine`: `public.U&"st\0061tus"` gives `public."status"` for both.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~TypeKeysTests|FullyQualifiedName~TypeOverrideGrammarTests"`
Expected: build error on the new signatures.

- [ ] **Step 3: Rewrite `TryNormalizeEach`**

Split the key once, as today, then for each engine of `DatabaseEngines.All` set its slot with `With` from a new private `TryNormalizeParts(DatabaseEngine engine, ReadOnlySpan<char> key, ReadOnlySpan<char> schema, ReadOnlySpan<char> name, bool isArray, out string normalized)`, a switch over the engine to `TryNormalizePostgresParts` and `TryNormalizeSqlServerParts`.  `With` and a loop rather than `Of`, since a lambda cannot capture a span.  Drop the user-schema shortcut, which named both system schemas; each engine's parts already give `UsersKey` for a user's schema, and SQL Server's refuses an array, so the keys are the same.  Return `keys != default`.  Update the doc comment: it no longer reads a key of a user's schema once for both.

- [ ] **Step 4: Rewrite `ReadMembers` over the engines**

- Keep each engine's entries and the index of the member that put each in `PerEngine.Of(_ => (Entries: new List<TypeOverride>(), Members: new List<int>()))`.
- For each member, the position of its key in each engine's list is `keys.Zip(lists, …)`, null for an engine that does not read the key.
- `first` is the least `MemberAt` over `DatabaseEngines.All`; a repeat goes in neither list, as today.
- Otherwise insert into each engine's list whose position is the complement of where it goes.
- Return `lists.Select(list => ToArray(list.Entries))`.  Update the doc comment's "each list" to "each engine's list".

- [ ] **Step 5: Delete `TypeOverrideLists` and adapt `SettingsLevelReader.ReadTypeOverrides`**

Until Task 3, it sets today's two fields from `overrides[DatabaseEngine.PostgreSql]` and `overrides[DatabaseEngine.SqlServer]`, each null when empty.

- [ ] **Step 6: Run the settings tests to verify they pass**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlSource.Tests.Settings|FullyQualifiedName~SqlSource.Tests.SettingsFiles"`
Expected: all pass.

- [ ] **Step 7: Update `generator.md`'s settings-file bullet**

`ReadMembers` reads each key through `TypeKeys.TryNormalizeEach`, which splits it once and reads it by each engine's rules, and puts the entry in the list of each engine that reads its key, a `PerEngine` of lists.  Remove "reads a key of a user's schema once for both" and `TypeOverrideLists`.

- [ ] **Step 8: Validate and commit**

```bash
./pre-commit-validation.sh
git add -A src/SqlSource/Settings tests/SqlSource.Tests/Settings docs/architecture/generator.md
git commit -m "refactor: read the type override's keys and lists by engine"
```

---

### Task 3: One per-engine type override on `SettingsLevel`

**Files:**
- Modify: `src/SqlSource/Settings/SettingsLevel.cs`, `src/SqlSource/Settings/SettingsLevelReader.cs`, `src/SqlSource/Settings/QuerySettings.cs` (`ResolveTypeOverrides`)
- Modify: `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs` (`Build`, around line 270-291)
- Modify: `src/SqlSource/Generation/Types/FileTypesInput.cs` (`TypeSettingsOf`), `src/SqlSource/Generation/Types/MappedTypes/MappedTypeNames.cs` (`Add`)
- Test: compile fixes in `QuerySettingsTests.cs`, `SettingsLevelReaderTests.cs`, `SettingsFiles/SettingsFileReaderTests.cs`, `Parsing/SqlFileParserMarkerTests.cs`, `Parsing/Markers/SqlMarkerScopeTests.cs`, `Generation/Types/QueryTyperTests.cs`, and any other test the build lists; create `tests/SqlSource.Tests/Settings/SettingsLevelTests.cs`
- Modify: `src/SqlSource/Settings/AGENTS.md`, `src/SqlSource/Generation/Types/AGENTS.md`, `docs/architecture/generator.md` ("The type override")

**Interfaces:**
- Consumes: Task 1's container; Task 2's `ReadMembers`.
- Produces: `public PerEngine<EquatableArray<TypeOverride>?> SettingsLevel.TypeOverrides { get; init; }`, a slot for each engine, keyed by that engine's rules; `SqlServerTypeOverrides` is removed.

- [ ] **Step 1: Write the failing tests**

- `SettingsLevelTests.Over_TypeOverrides_MergesEachEngineAlone`: a level whose SQL Server slot has `int=long` over one whose PostgreSQL slot has `int4=long` gives both slots, each its own entry.
- `SettingsLevelReaderTests.Read_TypeOverridesWithNoValidEntry_IsTheSharedEmptyLevel`: a source whose type override object has only members no engine reads returns `SettingsLevel.None` by reference (`ReferenceEquals(level, SettingsLevel.None).ShouldBeTrue()`), with the invalid members still reported.  Build the source as the neighbouring tests do.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SettingsLevelTests|FullyQualifiedName~SettingsLevelReaderTests"`
Expected: build error, `TypeOverrides` is not a `PerEngine`.

- [ ] **Step 3: Change `SettingsLevel`**

`TypeOverrides` becomes the container; delete `SqlServerTypeOverrides`.  `Over` sets `TypeOverrides = TypeOverrides.Zip(other.TypeOverrides, TypeOverrideMap.Over)`.  Doc comment: each engine's slot is keyed by its rules, sorted as `TypeOverrideMap` keeps them, merged per key; a settings file's object fills every slot and a marker its file's engine's.

- [ ] **Step 4: Change the readers and resolution**

- `SettingsLevelReader.ReadTypeOverrides` drops its out parameter and returns `default` when there are no members, else `overrides.Select(list => list.Count == 0 ? (EquatableArray<TypeOverride>?)null : list)`.  `Read` sets `TypeOverrides` alone.
- `SqlMarkerScope.Build`: delete `isSqlServer`; set `TypeOverrides = PerEngine.Only(Engine, BuildTypeOverrides())`.
- `QuerySettings.ResolveTypeOverrides`: `TypeOverrideMap.Over(markers.TypeOverrides[engine], file.TypeOverrides[engine]) ?? EquatableArray<TypeOverride>.Empty`, no switch.
- `FileTypesInput.TypeSettingsOf`: test `level.TypeOverrides == default` and copy `TypeOverrides` alone.
- `MappedTypeNames.Add`: `foreach (var engine in DatabaseEngines.All)` add `level.TypeOverrides[engine]`; its comment says each engine's entries.

- [ ] **Step 5: Fix the tests the build lists**

Set `TypeOverrides = PerEngine.Only(DatabaseEngine.PostgreSql, …)` (or `.With(DatabaseEngine.SqlServer, …)` for both), and read `level.TypeOverrides[DatabaseEngine.X]` where a test read a field.  Change no expected value.

- [ ] **Step 6: Run the suite**

Run: `dotnet test tests/SqlSource.Tests`
Expected: all pass, and `git status --short` lists only files this task edits.

- [ ] **Step 7: Update the docs**

- `src/SqlSource/Settings/AGENTS.md`, "The type override": replace the three bullets on two arrays with: a level's type override is a `PerEngine` of arrays, a slot for each `DatabaseEngine`, keyed by that engine's rules through `TypeKeys.TryNormalizeEach`, an entry absent from an engine that does not read its key; a settings file's object fills every slot and a marker only its file's engine's, by `SqlDescribableDialects.EngineOf`, a key that engine does not read being `SQLSRC111`; code that carries a level's entries carries the whole container and reads every slot through `DatabaseEngines.All`, as `MappedTypeNames` and `FileTypesInput.TypeSettingsOf` do.
- `src/SqlSource/Generation/Types/AGENTS.md`, the bullet "carries both engines' arrays": "carries every engine's slot".
- `docs/architecture/generator.md`, "The type override": "A level holds two arrays … `SqlServerTypeOverrides`" becomes a `PerEngine` with a slot for each engine; "`SqlMarkerScope` fills that engine's array" becomes its slot; "merges each engine's arrays" and "both engines' entries" become each engine's slot, slot by slot.

- [ ] **Step 8: Validate and commit**

```bash
./pre-commit-validation.sh
git add -A src/SqlSource tests/SqlSource.Tests docs/architecture/generator.md
git commit -m "refactor: hold a level's type override in PerEngine"
```

---

### Task 4: The drivers by engine

**Files:**
- Modify: `src/SqlSource/Generation/Types/Drivers/IDriverProbe.cs` (`IDriverProbe`, `DriverProbes`)
- Modify: `src/SqlSource/Generation/Methods/DatabaseEngineExtensions.cs`, `src/SqlSource/Parsing/SqlDescribableDialects.cs`
- Modify: `src/SqlSource/Generation/Methods/MethodDrivers.cs`
- Modify: `src/SqlSource/SqlSourceGenerator.cs` (`ReportDrivers`, around line 676-703; `ReportMissingDrivers`, around line 1094-1118)
- Test: `tests/SqlSource.Tests/Generation/Methods/DatabaseEngineExtensionsTests.cs`, `tests/SqlSource.Tests/Generation/Methods/MethodDriversTests.cs`, `tests/SqlSource.Tests/Parsing/SqlDescribableDialectsTests.cs`, `tests/SqlSource.Tests/Generation/Types/Drivers/DriverProbesTests.cs` (create; not in `Generator/`, which the Roslyn floor project compiles again)
- Modify: `docs/architecture/generator.md` (the `MethodDrivers` record, `SQLSRC273` and the drivers' reports)

**Interfaces:**
- Consumes: `PerEngine<bool>`, `PerEngine.Of`, `DatabaseEngines.All` (Task 1).
- Produces:
  - `IDriverProbe` gains `bool Referenced { get; }` and `bool IsBelowTheFloor { get; }`, which both probes already have.
  - `public static IDriverProbe DriverProbes.Of(DatabaseEngine engine, NpgsqlProbe npgsql, SqlClientProbe sqlClient)`, the one switch from an engine to its probe.
  - `public static string DatabaseEngineExtensions.DriverName(this DatabaseEngine engine)`: `"Npgsql"`, `"Microsoft.Data.SqlClient"`.
  - `public static SqlDialect SqlDescribableDialects.DialectOf(DatabaseEngine engine)`: the describable dialect of each engine, the inverse of `EngineOf` on `All`.
  - `internal readonly record struct MethodDrivers(PerEngine<bool> Allowed, bool PostgresAtLeast10, bool SqlServerSpatial = false)`; `Allows(engine)` is `Allowed[engine]`.

- [ ] **Step 1: Write the failing tests**

- `DatabaseEngineExtensionsTests.DriverName_EachEngine_IsItsDriversPackage`: PostgreSQL `"Npgsql"`, SQL Server `"Microsoft.Data.SqlClient"`.
- `SqlDescribableDialectsTests.DialectOf_EachEngine_IsTheDescribableDialectOfThatEngine`: for each of `DatabaseEngines.All`, `Contains(DialectOf(engine))` is true and `EngineOf(DialectOf(engine))` is `engine`.
- `DriverProbesTests.Of_EachEngine_IsItsDriversProbe`: PostgreSQL gives the `NpgsqlProbe` passed, SQL Server the `SqlClientProbe` (`ShouldBeSameAs`).
- `MethodDriversTests`: the existing theory builds `new MethodDrivers(PerEngine.Only(DatabaseEngine.PostgreSql, postgres).With(DatabaseEngine.SqlServer, sqlServer), false)`, its rows and expected values unchanged.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~DatabaseEngineExtensionsTests|FullyQualifiedName~SqlDescribableDialectsTests|FullyQualifiedName~DriverProbesTests|FullyQualifiedName~MethodDriversTests"`
Expected: build errors on the new members.

- [ ] **Step 3: Add the switches and the interface members**

`DriverProbes.Of`, `DriverName` and `DialectOf` are switch expressions under PR 1's rule.  Update `IDriverProbe`'s doc: the probe also says whether the project references its driver and whether below the floor; `DriverProbes`' doc: it also picks an engine's probe.

- [ ] **Step 4: Change `MethodDrivers`**

`Of` sets `Allowed = PerEngine.Of(engine => DriverProbes.Of(engine, npgsql, sqlClient) is { Referenced: true, IsBelowTheFloor: false })`, `PostgresAtLeast10` and `SqlServerSpatial` as today.  `Allows` indexes.  Doc: `Allowed` is whether each engine's methods can be generated; the two capabilities stay named.

- [ ] **Step 5: Rewrite `ReportMissingDrivers` as a loop**

For each engine of `DatabaseEngines.All`: when `(input.Engines & engine.ToCodeGenEngines()) != 0` and `!DriverProbes.Of(engine, input.Probes.Npgsql, input.Probes.SqlClient).Referenced`, report `DriverNotReferenced` with `engine.DatabaseName()` and `engine.DriverName()`.  The texts and the order are today's.

- [ ] **Step 6: Rewrite `ReportDrivers` as a loop with the same nodes**

The nodes, their tracking names and their `bool` outputs stay as `CachingParseTests` reads them; only the code that builds them becomes a loop:

```csharp
foreach (var engine in DatabaseEngines.All)
{
    var dialect = SqlDescribableDialects.DialectOf(engine);
    var usesEntries = filesWithNeeds
        .Select((input, _) => UsesEntriesOf(input.Left, input.Right, dialect))
        .WithTrackingName(UsesEntriesName(engine));
    switch (engine)
    {
        case DatabaseEngine.PostgreSql:
            RegisterDriverReport(context, npgsqlProbe, usesEntries);
            break;
        case DatabaseEngine.SqlServer:
            RegisterDriverReport(context, sqlClientProbe, usesEntries);
            break;
        default:
            throw UnexpectedEnumValueException.Of(engine);
    }
}
```

- `UsesEntriesName(DatabaseEngine)` is a private switch to `TrackingNames.UsesPostgresEntries` and `UsesSqlServerEntries`, which stay constants.
- `RegisterDriverReport<TProbe>(IncrementalGeneratorInitializationContext context, IncrementalValueProvider<TProbe> probe, IncrementalValueProvider<bool> usesEntries) where TProbe : IDriverProbe` registers `probe.Combine(usesEntries)` to `ReportDriver`, as each registration does today.
- The `Select` lambda captures `dialect`, so it is not `static`; it is made once per engine, at initialization.

- [ ] **Step 7: Run the suite**

Run: `dotnet test tests/SqlSource.Tests`
Expected: all pass, including `CachingParseTests` and `MethodDriverTests`, with no golden file changed.

- [ ] **Step 8: Update `generator.md`**

- The `MethodDrivers` record: `Allowed`, a `PerEngine<bool>` of whether each engine's driver is referenced and not below the floor, from `DriverProbes.Of`.
- The drivers' reports: one loop over `DatabaseEngines.All` builds each engine's `Uses…Entries` node and registers its probe's report, the probe picked by a switch; `SQLSRC273`'s output loops over the engines, asking each engine's probe through `DriverProbes.Of`.

- [ ] **Step 9: Validate and commit**

```bash
./pre-commit-validation.sh
git add -A src/SqlSource tests/SqlSource.Tests docs/architecture/generator.md
git commit -m "refactor: ask each engine's driver through one switch"
```

---

### Task 5: Library uses and library rows by engine

**Files:**
- Modify: `src/SqlSource/Generation/Methods/MethodsInput.cs` (`AllUses`, around line 55-61)
- Modify: `src/SqlSource/Generation/Types/Maps/LibraryRows.cs`
- Test: create `tests/SqlSource.Tests/Generation/Methods/MethodsInputTests.cs`; `Generation/Types/Maps/PostgresTypeMapTests.cs` and `SqlServerTypeMapTests.cs` pin the rows
- Modify: `docs/architecture/generator.md` ("The library rows")

**Interfaces:**
- Consumes: Task 1's container.
- Produces: no public change.  `LibraryRows` holds `PerEngine<Dictionary<string, TypeMapping>> _rows` and `PerEngine<Dictionary<string, TypeMapping>?> _spatialRows`; `RowsOf(SqlLibrary library)` returns `PerEngine<Dictionary<string, TypeMapping>>`.

- [ ] **Step 1: Write the order test**

`BelowTheFloorOf_EveryLibraryBelow_IsInTheOrderOfLibraryThenEngine`: a `LibraryProbe` with NodaTime 3.0.0, NetTopologySuite without `NetTopologySuiteAtFloor`, and NetTopologySuite.IO.SqlServerBytes 2.0.0 gives `[(NodaTime, PostgreSql), (NodaTime, SqlServer), (NetTopologySuite, PostgreSql), (NetTopologySuite, SqlServer)]`.

- [ ] **Step 2: Run it**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~BelowTheFloorOf"`
Expected: PASS today; it pins the order the next step must keep.

- [ ] **Step 3: Build `AllUses` from the two sets**

Every member of `(SqlLibrary[])Enum.GetValues(typeof(SqlLibrary))`, then each of `DatabaseEngines.All`.  `Json` is added, and `LibraryProbe.IsBelowTheFloor` is false for it, so `BelowTheFloorOf` gives what it gives today.

- [ ] **Step 4: Hold `LibraryRows`' dictionaries by engine**

- `PostgisRows` and `SqlServerSpatialRows` become one static `SpatialRows`, `PerEngine.Of` of a switch to `BuildPostgisRows()` and `BuildSqlServerSpatialRows()`, still declared before `Sets`.
- `RowsOf(library)` is `PerEngine.Of(engine => RowsOf(library, engine))`, where the private `RowsOf(SqlLibrary, DatabaseEngine)` switches over the library and then over the engine, each giving today's dictionary.
- `BuildSets` makes `PerEngine.Of(_ => new Dictionary<string, TypeMapping>(StringComparer.OrdinalIgnoreCase))`, copies each library's rows into each engine's dictionary, and passes `hasNetTopologySuite ? SpatialRows : default`.
- `Find(PostgresType)` reads the PostgreSQL slots, `Find(SqlServerType)` the SQL Server slots.  Comments say "each engine's" where they said both.

- [ ] **Step 5: Run the suite**

Run: `dotnet test tests/SqlSource.Tests`
Expected: all pass, no golden file changed.

- [ ] **Step 6: Update "The library rows" in `generator.md`**

`RowsOf` gives each library's rows for each engine, and the spatial rows, PostGIS's and SQL Server's of `sys`, are a `PerEngine` that every set with NetTopologySuite shares; drop "a third dictionary".

- [ ] **Step 7: Validate and commit**

```bash
./pre-commit-validation.sh
git add -A src/SqlSource tests/SqlSource.Tests docs/architecture/generator.md
git commit -m "refactor: hold library uses and rows by engine"
```

---

### Task 6: The engine guide and the rules

**Files:**
- Modify: `docs/architecture/generator.md` ("Adding a database engine")
- Modify: `src/AGENTS.md`

- [ ] **Step 1: Rewrite steps 1 and 2 of the engine guide as one step**

Step 2, "The structures that hold one entry per engine, which no switch guards", is gone.  The new step 1, to be worded in the guide's style:

> 1. **The member of `DatabaseEngine`**, then each switch that IDE0072 and IDE0010 list.  `DatabaseEngines.All` and every `PerEngine<T>` gain its slot by themselves, and each slot an engine must decide is filled by a switch, which the build lists too.  Among them are `ParameterBinder.For`, `DriverProbes.Of`, `DatabaseEngineExtensions`, `SqlDescribableDialects.DialectOf`, `TypeKeys`' choice of a normalizer, `LibraryRows`' rows and spatial rows, `LibraryProbe`, `QueryTyper`, `ConversionPlanner`, `ConverterTargetsExtensions.Of`, `PropertyNames`' default schema, `ReportDrivers`' tracking name and probe, and `ExecutionCapabilityRules.Of`, which gives the engine's flags (step 6).  No switch reaches the fields whose type differs by engine, which the engine adds beside the others: its driver's probe, an `IDriverProbe` beside `NpgsqlProbe` and `SqlClientProbe` in the pipeline, `FileTypesInput` and `QueryTyper`'s `Typing`, with its arm in `DriverProbes.Of`; and its delegate in `QueryTyper.OverrideLookup`, beside `OfPostgres` and `OfSqlServer`, set by a switch of its own.

Renumber steps 3 to 8 as 2 to 7.  `MethodDrivers.Allows` and the type override's reading in `QuerySettings`, `TypeOverrideGrammar` and `SqlMarkerScope` leave the list, since they no longer switch.

- [ ] **Step 2: Add the two rules to `src/AGENTS.md`**

After the switch rule, each within 80 words:

- **A value of each engine goes in a `PerEngine<T>`, never in fields named for engines.**  A value every engine must decide is built by `PerEngine.Of` with a switch, so a new engine is IDE0072 there; `default`, `Only` and `With` leave the other slots `default(T)`, only where that means "says nothing".  Values whose type differs by engine keep named fields, each question asked of them a switch ([adding an engine](../docs/architecture/generator.md#adding-a-database-engine)).
- **No bool names an engine**, as `isSqlServer` did: pass the `DatabaseEngine` and switch over it.  A bool may name one driver's or library's capability, as `MethodDrivers.PostgresAtLeast10` does.

- [ ] **Step 3: Check that nothing still names the old members**

Run: `grep -rn "SqlServerTypeOverrides\|TypeOverrideLists\|isSqlServer\|_sqlServerRows\|PostgisRows\b" src docs/architecture --include='*.cs' --include='*.md'`
Expected: no output.

- [ ] **Step 4: Validate and commit**

```bash
./pre-commit-validation.sh
git add docs/architecture/generator.md src/AGENTS.md
git commit -m "docs: the engine guide and the rules for per-engine values"
```

- [ ] **Step 5: Before the pull request**

- `./pre-commit-validation.sh --full` on the branch as it will be pushed.
- `git fetch --tags origin`, then compare `VersionPrefix` in `Directory.Build.props` with `git tag --list 'v*' --sort=-v:refname | head -n 1`; if it is not greater, ask the user for the version.
- Run `branch-review:review` until it comes back clean, then offer the PR.
