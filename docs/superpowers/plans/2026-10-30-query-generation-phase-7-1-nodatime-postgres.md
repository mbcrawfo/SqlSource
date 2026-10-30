# Phase 7.1: the switch, and NodaTime on PostgreSQL - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `libraryTypes` in `sqlsource.json` and `-- library-types:` in a file's header and a query, merged per library; `LibraryProbe`; the library map as step 4 of a column's typing, for `codegen` queries only; `SQLSRC281`; and NodaTime on PostgreSQL through `Npgsql.NodaTime`, end to end.

**Architecture:** The switch is a setting of `SettingsLevel` merged per library, like the type override.  `QueryTyper` resolves it once for each distinct level of markers, against `LibraryProbe`, and passes the libraries that are on to `PostgresTypeMap.Map`.  The map asks them after the type override and before its own row, at every level of its walk.  A property whose type a library row chose keeps the type it would have without the library beside it.  `TypeModels` gives a non-`codegen` claim that type, so typing stays once per file.  A switch that says `on` for a library whose assemblies are missing travels with the query's models to `MethodPlanner`, which reports `SQLSRC281` for a `codegen` claim, at the place that said `on`.  On PostgreSQL the plugin reads every NodaTime row, so no conversion is generated.

**Tech Stack:** C# (`netstandard2.0` generator, Roslyn 4.8 floor), xunit v3, Shouldly, Npgsql 8.0.9 and 10.0.3, `Npgsql.NodaTime` 8.0.9 and 10.0.3, NodaTime 3.1.9 and 3.3.7, PostgreSQL 14.24 and 18.6.

**Spec:** [`docs/superpowers/specs/2026-10-26-query-generation-phase-7-library-maps-design.md`](../specs/2026-10-26-query-generation-phase-7-library-maps-design.md), section 7.1 and the design sections it relies on; [the spike](../specs/2026-10-26-query-generation-phase-7-spike.md), questions 1 and 6.

## Global Constraints

- Generator: `netstandard2.0`, Roslyn 4.8.0, no library type named in its own code; library types are text it emits, `global::`-qualified.
- Every pipeline record has value equality and holds collections in `EquatableArray<T>`; nothing a user writes may make the generator throw.
- Library names in the switch, matched as a choice is by `SettingValue.TryReadChoice`: `nodaTime`.  Values: `on`, `off`, `default`.
- Defaults: on PostgreSQL, `on` when `Npgsql.NodaTime` is referenced (`Npgsql.NpgsqlNodaTimeExtensions`, with `NodaTime.Instant`), else `off`; on SQL Server, `off`.
- Levels, most specific first: the query's `-- library-types:`, the file header's, the `.sql` file's chain of settings files, nearest first.  No attribute level, no MSBuild level.  Merged per library.
- The switch takes effect only in a `codegen` claim.  `-- library-types:` is not in `SqlQueryHash`, and the tool ignores it.
- NodaTime's PostgreSQL rows, exactly: `timestamptz` `global::NodaTime.Instant`; `timestamp` `global::NodaTime.LocalDateTime`; `date` `global::NodaTime.LocalDate`; `time` `global::NodaTime.LocalTime`; `timetz` `global::NodaTime.OffsetTime`; `interval` `global::NodaTime.Period` (a class); `tstzrange` `global::NpgsqlTypes.NpgsqlRange<global::NodaTime.Instant>`; `tsrange` `global::NpgsqlTypes.NpgsqlRange<global::NodaTime.LocalDateTime>`; `daterange` `global::NpgsqlTypes.NpgsqlRange<global::NodaTime.LocalDate>`; `tstzmultirange`, `tsmultirange`, `datemultirange` the arrays of those three.  An array of any row is `T[]`; a domain is its base's.
- `SQLSRC281`: an error, not configurable, at the settings file's `libraryTypes` member or at the marker's line.  It is reported once for each place that says `on` and each engine of a `codegen` query that place reaches.  It names the library and the missing assemblies: `Npgsql.NodaTime` on PostgreSQL, `NodaTime` on SQL Server.  The queries it reaches keep the base map's types.
- A bad name or value is `SQLSRC111` in a marker and the settings files' invalid-value diagnostic, `SQLSRC019`, in a settings file, in any project.
- Anti-goal: the existing end-to-end tests, the realistic corpora and the examples are unchanged, byte for byte, in their generated code.
- Repository rules: `./format.sh` before validating; `./pre-commit-validation.sh` before every commit and `--full` before every push; the floor image `postgres:14.24` for a PostgreSQL end-to-end change; docs per root `AGENTS.md` rule 4 and `docs/manual/AGENTS.md`; nothing recorded as tech debt or deferred without the owner.

## Where merged code differs from the design, and calls made here

- **`SQLSRC284` is taken** (a short name that finds a package's type).  7.1 takes `SQLSRC281` alone.
- **A plugin row does not set `PropertyType.Place` in 7.1.**  `ConversionPlanner` converts only where `Place` is set, so a PostgreSQL plugin row goes to the driver with no change to the planner, and a project's `Instant` converter never touches a plugin-read column.  The row is marked by a new member, `PropertyType.Library`.  7.2 decides how the planner sees a library place.
- **`SQLSRC281` needs a position after typing.**  Today a setting's place carries no position, and the type override's errors are reported at the query's name.  So each switch entry carries its `TextSpan` and `LinePositionSpan`, computed when it is read.  A marker's position comes from the scope's text, and a settings file's from its member, by one shared helper, `TextLines`.  The path is the settings file's, or the query's file's for a marker.
- **The models pick by output in `TypeModels.Request`**, the one place a claim's output meets a model's properties.  `MethodPlanner` reads the library type directly from `QueryModels`, since a method is always `codegen`.
- **Owner questions sent on 2026-10-30, answers pending.**  Q1: what `default` means, built here as reading A: it hides farther levels and gives the engine's default, like a type override's `default`.  Q2: whether the switch knows `nodaTime` alone in 7.1, built here so.  Q3: whether `SQLSRC282` is 7.2's, built here so; the probe records NodaTime's version for it.  Rework follows any answer that differs.

## Review Focus

1. **An existing project with the plugin and no switch** has every `codegen` `timestamptz` change to `Instant`, which is the detection the owner asked for.  The root `off` of the end-to-end project must keep its generated code byte for byte.  Task 8 diffs the generated files.
2. **A model shared by a `codegen` and a `models` claim**, the realistic corpora's pattern, must be `SQLSRC257` when a library is on, and unchanged when it is off.  Task 6 tests both.
3. **A domain or an array over a library type, with an override on the domain or the element**: the override wins at its level.  Task 4 tests `mydomain=X` over `timestamptz`, `timestamptz[]=X`, and `timestamptz=X` under an array.
4. **`nodaTime=on` in a settings file that reaches `codegen` queries of both engines and several types** gives one `SQLSRC281` per engine, not one per query.  Task 7 tests two types, two files and both engines.
5. **A malformed marker**, `-- library-types: nodatime`, `nodatime=maybe`, `json=on`, `nodatime=on nodatime=off`, is `SQLSRC111` or `SQLSRC112`, never a throw.  An empty value is `SQLSRC111`.  Task 2 tests each.

---

### Task 1: The switch's data, the settings file's key and its schema

**Files:**
- Create: `src/SqlSource/TextLines.cs`, `src/SqlSource/Settings/Libraries/SqlLibrary.cs`, `LibraryState.cs`, `LibrarySetting.cs`, `LibrarySettings.cs`
- Modify: `src/SqlSource/Settings/SettingsLevel.cs`, `SettingId.cs`, `SettingsLevelReader.cs`, `ISettingSource.cs`, the attribute's `ISettingSource` (find with `grep -rn ': ISettingSource' src`), `src/SqlSource/SettingsFiles/SettingsFileKeys.cs`, `SettingsFileSource.cs`, `SettingsFileReader.cs` (hand the text to the source), `src/SqlSource/Diagnostics/SettingsFileDiagnostics.cs` (help of `libraryTypes`), `schemas/settings-v1.schema.json`, `src/SqlSource/Settings/AGENTS.md`, `src/SqlSource/SettingsFiles/AGENTS.md` if a rule changes
- Test: `tests/SqlSource.Tests/TextLinesTests.cs`, `tests/SqlSource.Tests/Settings/Libraries/LibrarySettingsTests.cs`, `tests/SqlSource.Tests/Settings/SettingsLevelReaderTests.cs`, `tests/SqlSource.Tests/SettingsFiles/SettingsFileReaderTests.cs`, `SettingsFileKeysTests.cs`, `SettingsFileSchemaTests.cs`

**Interfaces:**
- Produces:
  - `internal static class TextLines { public static LinePositionSpan LineSpanOf(string text, TextSpan span); }`.  Its line breaks are Roslyn's: `\r\n`, `\r`, `\n`, `\u0085`, ` `, ` `.  It allocates nothing.
  - `internal enum SqlLibrary { NodaTime }`.  The enum's lower-cased name is the switch's name: `SettingValue.NameOf` gives `nodatime`, and the manual writes `nodaTime`.
  - `internal enum LibraryState { On, Off, Default }`.
  - `internal sealed record LibrarySetting(SqlLibrary Library, LibraryState State, SettingPlace Place, TextSpan Span, LinePositionSpan LineSpan)`.
  - `internal static class LibrarySettings`:
    - `Over(EquatableArray<LibrarySetting>? near, EquatableArray<LibrarySetting>? far) -> EquatableArray<LibrarySetting>?`, which replaces `far`'s entry for each library `near` names and is sorted by `Library`.
    - `Find(EquatableArray<LibrarySetting> merged, SqlLibrary library) -> LibrarySetting?`.
    - `TryReadEntry(ReadOnlySpan<char> entry, out SqlLibrary library, out LibraryState state) -> bool`, which reads `name=value`, white space allowed around `=`, both by `TryReadChoice`.
  - `SettingsLevel.LibraryTypes : EquatableArray<LibrarySetting>?`, merged in `Over` by `LibrarySettings.Over`.
  - `SettingId.LibraryTypes`, after `ColumnTypes` and before `NestedClass`.  Check that nothing indexes `SettingId` by number, beyond `SettingsFileKeys.Settings`, which gains the key in the same place.
  - `ISettingSource.MemberSpans(SettingId setting, int index) -> (TextSpan Span, LinePositionSpan LineSpan)`, from the member's key to its value.  The attribute's source never has members for it and returns `default`.
  - `SettingsFileKeys.LibraryTypes = "libraryTypes"`, `ObjectOfStrings`, in `All` after `columnTypes`.

- [ ] **Step 1: Write the failing tests.**
  - `TextLinesTests.LineSpanOf_IsTheSourceTextsLineSpan`: a theory over `"a\nb"`, `"a\r\nb"`, `"a\rb"`, `"a b"`, `"x"` and spans at the start, at a line start and across a break.  It asserts equality with `SourceText.From(text).Lines.GetLinePositionSpan(span)`.
  - `LibrarySettingsTests`:
    - `Over_ReplacesOnlyTheLibrariesTheNearLevelNames`.
    - `Over_KeepsADefaultEntry`: a near `Default` hides a far `On`, and `Find` returns the `Default` entry.
    - `TryReadEntry_ReadsNamesAndValuesAsAChoice`: `NodaTime=on`, `node-time = OFF` (fails: not a name), `noda time=default`, `nodatime=`, `nodatime`, `json=on`.
  - `SettingsLevelReaderTests`:
    - `LibraryTypes_AreReadFromMembers`: entries carry the source's `Place` and `MemberSpans`.
    - `LibraryTypes_InvalidMemberIsReportedByIndex`: `{"nodaTime":"maybe","json":"on"}` gives `InvalidPart` 0 and 1.
    - `LibraryTypes_RepeatedLibraryIsRepeatedKey`: `{"nodaTime":"on","noda-time":"off"}` gives `RepeatedKey(1, 0)` and the first applies.
  - `SettingsFileReaderTests.LibraryTypes_EntryCarriesTheMembersLineSpan`: a file whose `libraryTypes` member is on line 3 gives `LineSpan.Start.Line == 2`, and the `Span` runs from the key's quote to the value's end.  `LibraryTypes_InvalidValue_IsSQLSRC019Help` checks that the help names `nodaTime` and `on`, `off`, `default`.
  - `SettingsFileKeysTests` and `SettingsFileSchemaTests`: their existing theories cover the new key once `All` and the schema have it.  The schema property is an object whose property names match the choice's pattern and whose values are `on`, `off` or `default`, matched as the existing choices' patterns are.

- [ ] **Step 2: Run** `dotnet test --project tests/SqlSource.Tests --filter-class '*LibrarySettingsTests' --filter-class '*TextLinesTests' --filter-class '*SettingsLevelReaderTests' --filter-class '*SettingsFile*Tests'`, or the repository's own filter form, from `CONTRIBUTING.md`.  Expected: the new tests fail to compile or fail.

- [ ] **Step 3: Implement** the interfaces above.  `SettingsLevelReader.ReadLibraryTypes` follows `ReadColumnTypes`: an invalid member goes by its index to `InvalidPart`, a repeat by `SqlLibrary` to `RepeatedKey`, and an empty list is not set.  `SettingsFileSource` keeps the text so that `MemberSpans` computes `TextLines.LineSpanOf`.  `SettingsFileDiagnostics.HelpOf` gives `libraryTypes` the names and values, as the help of a choice does.

- [ ] **Step 4: Run the same tests.**  Expected: PASS.  Then run `dotnet test --project tests/SqlSource.Tests`.  Expected: PASS, with `GeneratorParityTests` in `tests/SqlSource.Tool.Tests` also passing.

- [ ] **Step 5: Commit** after `./format.sh` and `./pre-commit-validation.sh`: `feat(settings): read libraryTypes from a settings file`.

### Task 2: The `-- library-types:` marker

**Files:**
- Modify: `src/SqlSource/Parsing/Markers/SqlMarkerKind.cs` (`LibraryTypes`, last), `SqlMarkerReader.cs` (keyword `library-types`), `SqlMarkerScope.cs` (allowed in the query and the preamble; `case`; `BuildLevel`), `src/SqlSource/Parsing/AGENTS.md` if a rule changes
- Test: `tests/SqlSource.Tests/Parsing/` marker tests beside those of `-- type-override:` (find with `grep -rln "type-override" tests/SqlSource.Tests/Parsing`), and `SqlQueryHash` tests

**Interfaces:**
- Consumes: `LibrarySettings.TryReadEntry`, `LibrarySetting`, `TextLines.LineSpanOf`, `SettingsLevel.LibraryTypes`.
- Produces: a query's `SqlQuery.Markers.LibraryTypes`, over its header's by `SettingsLevel.Over`, each entry at `SettingPlace.FileHeader` or `SettingPlace.Query` with the span of the whole marker and its line span.

- [ ] **Step 1: Write the failing tests:**
  - `LibraryTypes_InTheHeaderAndAQuery_MergePerLibrary`: the header says `nodatime=off` and the query says `nodatime=on`, so the query's entry is `On` at `Query`.  A second query with no marker of its own has the header's `Off` at `FileHeader`.
  - `LibraryTypes_SeveralEntriesOnOneLine`.  It can only hold one library today, so it checks `nodatime=on   nodatime=on`, which is not a conflict.
  - `LibraryTypes_Invalid_IsSQLSRC111`: a theory over `nodatime`, `nodatime=maybe`, `json=on`, `=on` and an empty value.
  - `LibraryTypes_ConflictInAScope_IsSQLSRC112`: `nodatime=on` then `nodatime=off` in one scope; the first stands.
  - `LibraryTypes_EntryCarriesTheMarkersLineSpan`.
  - `Marker_OfEveryKind_HasAKeywordAndAScope` passes with the new kind.
  - `Hash_IgnoresALibraryTypesMarker`: two files that differ by the marker alone give one `SqlQueryHash`.

- [ ] **Step 2: Run the parsing tests.**  Expected: the new tests fail.

- [ ] **Step 3: Implement.**  `ReadLibraryTypes(SqlMarker marker)` splits the value on white space and reads each part with `TryReadEntry`.  A part that fails is `AddInvalid(marker)` with a help that names the libraries and values.  The entries of a scope add up, one per library; the same value twice is fine, and another value is `AddConflict(marker)`.  The level's array is sorted by `Library`.  Check that `src/SqlSource.Tool/Planning/` reads nothing of the level that would change, so that the tool ignores the marker.

- [ ] **Step 4: Run** the parsing tests and `tests/SqlSource.Tool.Tests` `GeneratorParityTests`.  Expected: PASS.

- [ ] **Step 5: Commit**: `feat(parsing): read the -- library-types: marker`.

### Task 3: `LibraryProbe`

**Files:**
- Create: `src/SqlSource/Generation/Types/Drivers/LibraryProbe.cs`, `tests/SqlSource.Testing/Generator/LibraryReferences.cs`
- Modify: `src/SqlSource/SqlSourceGenerator.cs` (`TypeFiles`: a `CompilationProvider.Select` named `TrackingNames.LibraryProbe`, carried on `TypedFiles`), `src/SqlSource/Generation/TrackingNames.cs`, `tests/SqlSource.Testing/SqlSource.Testing.csproj` (`PackageDownload` of `NodaTime` 3.1.9 and 3.3.7, and `Npgsql.NodaTime` 8.0.3 and 10.0.3, which match the downloaded Npgsql), `tests/SqlSource.Tests/Generator/AGENTS.md` (the helper list names `LibraryReferences`)
- Test: `tests/SqlSource.Tests/Generator/LibraryProbeTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed record LibraryProbe(bool NodaTime, bool NpgsqlNodaTime, Version? NodaTimeVersion)`.  `NodaTime` is `NodaTime.Instant`, `NpgsqlNodaTime` is `Npgsql.NpgsqlNodaTimeExtensions`, and the version is that of the newest referenced assembly named `NodaTime`, kept for 7.2's floor.
    - `bool IsAvailable(SqlLibrary library, DatabaseEngine engine)`: PostgreSQL needs `NodaTime && NpgsqlNodaTime`, and SQL Server needs `NodaTime`.
    - `LibraryState DefaultOf(SqlLibrary library, DatabaseEngine engine)`: `On` when PostgreSQL and `IsAvailable`, else `Off`.
    - `static string MissingAssembliesOf(SqlLibrary library, DatabaseEngine engine)`: `"Npgsql.NodaTime"` or `"NodaTime"`.
    - `static LibraryProbe Read(Compilation compilation)`, which never throws.
  - `LibraryReferences`: `NodaTime(string version)`, `NpgsqlNodaTime(string version)`, downloaded as `NpgsqlReferences` does; and `Synthetic(...)`, as `NpgsqlReferences.Synthetic`.

- [ ] **Step 1: Write the failing tests** `LibraryProbeTests`:
  - `Read_NoReferences_IsUnavailableEverywhere`.
  - `Read_NodaTimeAlone_IsAvailableOnSqlServerOnly`.
  - `Read_NodaTimeAndPlugin_IsOnByDefaultOnPostgres` uses `NodaTime` 3.3.7 and `Npgsql.NodaTime` 10.0.3, with Npgsql 10.
  - `Read_Floor` uses 3.1.9 and plugin 8.0.3, with Npgsql 8.
  - `Read_RecordsNodaTimesVersion`.
  - `DefaultOf_SqlServer_IsOff`.
  - `MissingAssembliesOf_NamesThePluginOnPostgres`.

- [ ] **Step 2: Run.**  Expected: the tests fail.

- [ ] **Step 3: Implement** the probe and the step.  Do not use it in typing yet.

- [ ] **Step 4: Run.**  Expected: PASS.  Add a case to `CachingTypeTests`, `LibraryProbe_IsEqualAcrossEdits`: an edit to a `.sql` file leaves the step `Cached` or `Unchanged`.

- [ ] **Step 5: Commit**: `feat(generator): probe the libraries a project references`.

### Task 4: NodaTime's PostgreSQL rows in the map's walk

**Files:**
- Create: `src/SqlSource/Generation/Types/Maps/LibraryRows.cs`
- Modify: `src/SqlSource/Generation/Types/Maps/PostgresTypeMap.cs` (`Map` gains `LibraryRows? libraries = null`), `TypeMapping.cs` (gains `SqlLibrary? Library = null`, kept by `ArrayOf`'s `with`)
- Test: `tests/SqlSource.Tests/Generation/` beside `PostgresTypeMapTests` (find with `grep -rln PostgresTypeMapTests tests`)

**Interfaces:**
- Produces:
  - `internal sealed class LibraryRows`.
    - `static LibraryRows? Of(IReadOnlyList<SqlLibrary> on)`, null when the list is empty; the instance for each set of libraries is cached and static.
    - `TypeMapping? Find(PostgresType type)`, which matches a type of `pg_catalog` by `InternalName`, ignoring case as `Rows` does, and gives `TypeMapping.Mapped(key, text, isValueType) with { Library = SqlLibrary.NodaTime }`.
  - `PostgresTypeMap.Map(type, column, overrideOf, libraries)`: at each level, `overrideOf` first, then `libraries?.Find(type)`, then the base map's bits and rows, then the kind's rule, with arrays and domains recursing with `libraries`.

- [ ] **Step 1: Write the failing tests:**
  - `Map_NodaTimeOn_GivesEachRow`: a theory over the twelve keys of Global Constraints, with each text and `IsValueType`.  `Period` and the three multirange arrays are not value types.
  - `Map_NodaTimeOn_ArrayIsTheElementsArray`: `timestamptz[]` is `global::NodaTime.Instant[]`, with `Library` NodaTime.
  - `Map_NodaTimeOn_DomainIsItsBases`.
  - `Map_Override_WinsAtEachLevel`: `timestamptz=DateTimeOffset` gives `DateTimeOffset` and `DateTimeOffset[]`, with `Library` null; `timestamptz[]=X` gives `X`; `mydomain=Y` over `timestamptz` gives `Y`.
  - `Map_NodaTimeOff_IsTheBaseMap`: `libraries` null gives today's rows.
  - `Map_NodaTimeOn_OtherTypesUnchanged`: `int4`, `text`, `int4range` and `timestamptz` of another schema.
  - `Gives_LibraryTypeIsForeign`: `PostgresTypeMap.Gives("global::NodaTime.Instant")` is false.

- [ ] **Step 2: Run.**  Expected: FAIL.

- [ ] **Step 3: Implement.**  `Keys` and `TypeKeyNames` are unchanged: a library row adds no key to the map.

- [ ] **Step 4: Run** the map tests and `Keys_OfTheMap_AreTheKnownPostgresNamesOfTheSettings`.  Expected: PASS.

- [ ] **Step 5: Commit**: `feat(types): NodaTime's PostgreSQL rows`.

### Task 5: The library map in typing

**Files:**
- Modify: `src/SqlSource/Generation/Types/PropertyType.cs` (adds `SqlLibrary? Library = null` and `PropertyType? WithoutLibrary = null`), `QueryTypes.cs` (adds `EquatableArray<LibrarySetting> UnavailableLibraries`), `FileTypesInput.cs` (adds `LibraryProbe`, and `TypeSettingsOf` keeps `LibraryTypes`), `QueryTyper.cs` (resolves the switch in `LevelTypesOf`, passes `LibraryRows` through `Typing` to `TypeOf`), `src/SqlSource/Settings/QuerySettings.cs` (`ResolveLibraries`), `src/SqlSource/SqlSourceGenerator.cs` (feeds the probe to `FileTypesInput.Gather`), `src/SqlSource/Generation/Types/AGENTS.md`, `src/SqlSource/Settings/AGENTS.md`
- Test: `tests/SqlSource.Tests/Generator/LibraryTypeTests.cs`, `CachingTypeTests.cs`, `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`

**Interfaces:**
- Consumes: Tasks 1 to 4.
- Produces:
  - `QuerySettings.ResolveLibraries(SettingsLevel markers, SettingsLevel file) -> EquatableArray<LibrarySetting>`: the markers' over the file level's, merged per library, computed once for each distinct level of markers.
  - In `QueryTyper`, a library is on when its winning entry is `On`, or when it is `Default` or absent and `LibraryProbe.DefaultOf` gives `On`.
    - On and `IsAvailable`: its rows go to `PostgresTypeMap.Map`.
    - On and not available: the winning entry joins `QueryTypes.UnavailableLibraries`, and no row is used.
  - A property mapped with `Library` set gets `Library` and `WithoutLibrary`, the property of the same type mapped with `libraries` null.  When that mapping fails, the library row is not used and the base mapping, failure included, is the property's.
  - `Place`, `Database` and `Mapped` stay null for a library row, so `ConversionPlanner` leaves it to the plugin.
  - A type that a marker, a column type or an override chose is unchanged.
  - SQL Server typing passes no rows in 7.1.

- [ ] **Step 1: Write the failing tests** `LibraryTypeTests`, through `ModelRuns` or `MethodRuns`, with the plugin's references:
  - `Detected_PluginReferenced_TimestamptzIsInstant` and the same at `models` (`WithoutLibrary` is `DateTime`).
  - `NotDetected_NoPlugin_TimestamptzIsDateTime`.
  - `SettingsFileOff_TurnsADetectedLibraryOff`, `HeaderOn_OverSettingsFileOff`, `QueryOff_OverHeaderOn`.
  - `Default_HidesAFartherOff_GivesDetection`, which follows Q1, reading A.
  - `NearerSettingsFile_WinsOverFarther`.
  - `ColumnMarker_ColumnType_Override_WinOverTheLibrary`: each one alone on a `timestamptz` column.
  - `ParamMarker_WinsOverTheLibrary`.
  - `On_WithoutPlugin_KeepsTheBaseTypes_AndRecordsTheEntry`.
  - `SqlServer_NodaTimeOn_KeepsTheBaseTypes` and `SqlServer_NodaTimeOn_WithoutNodaTime_RecordsTheEntry`.
  - `QuerySettingsTests.ResolveLibraries_MarkersOverFile`.
  - `CachingTypeTests`:
    - `LibraryTypesMarker_Edit_RetypesOnlyItsFile`: two claimed files, an edit to one file's marker, so the other's `TypedFile` is `Cached` or `Unchanged`.
    - `LibraryReference_Added_RetypesAndReplansMethods`: `TypedFile`, the models and `MethodsInput` run again.
    - `SettingsFileLibraryTypes_Edit_RetypesOnlyItsFolder`.

- [ ] **Step 2: Run.**  Expected: FAIL.

- [ ] **Step 3: Implement.**  `LevelTypes` gains the resolved `LibraryRows?` and the unavailable entries.  Both are computed once for each distinct level of markers, as the type override's lookup is.

- [ ] **Step 4: Run** `tests/SqlSource.Tests` whole.  Expected: PASS, and no existing test changes, since the existing projects reference no plugin.

- [ ] **Step 5: Commit**: `feat(types): the library map, step 4 of a column's type`.

### Task 6: A claim's models pick by its output

**Files:**
- Modify: `src/SqlSource/Generation/Models/TypeModels.cs` (`Request`: when `settings.Output != OutputKind.CodeGen`, each property with `Type.WithoutLibrary` takes it; only then is an array allocated), and the models' architecture text in Task 10
- Test: `tests/SqlSource.Tests/Generator/SharedModelTests.cs`, `LibraryTypeTests.cs`

**Interfaces:**
- Consumes: `PropertyType.WithoutLibrary`.
- Produces: a `models` claim's models carry the base types, and a `codegen` claim's carry the library's.  `ModelCollector.FindDifference` is unchanged; it compares `Type.Text`, so a shared model that differs is `SQLSRC257`.

- [ ] **Step 1: Write the failing tests:**
  - `SharedModel_CodeGenWithLibrary_AndModels_IsSQLSRC257`: the realistic corpora's pattern, one file claimed by two types at `codegen` and `models`, with the library detected.
  - `SharedModel_LibraryOffAtRoot_IsShared`: the same with `"libraryTypes": {"nodaTime": "off"}`, which gives no diagnostic and one model.
  - `SharedModel_TwoCodeGenQueries_LibraryOnAndOff_IsSQLSRC257`.
  - `ModelsClaim_GetsTheBaseTypes_MethodsGetTheLibrarys`: the generated model of a `models` claim has `System.DateTime`, and the method of a `codegen` claim reads `GetFieldValue<global::NodaTime.Instant>` and binds `NpgsqlParameter<global::NodaTime.Instant>` with `DataTypeName` `timestamptz`.
  - `CodeGen_NodaTime_CompilesWithoutWarnings`: every row at `codegen`, compiled against Npgsql 10.0.3, `Npgsql.NodaTime` 10.0.3 and NodaTime 3.3.7, then Npgsql 8.0.3, plugin 8.0.3 and NodaTime 3.1.9.  `GeneratedCodeWarnings` and the errors are empty.

- [ ] **Step 2: Run.**  Expected: FAIL.

- [ ] **Step 3: Implement.**

- [ ] **Step 4: Run** `tests/SqlSource.Tests` whole.  Expected: PASS.

- [ ] **Step 5: Commit**: `feat(models): a models claim keeps the base map's types`.

### Task 7: `SQLSRC281`

**Files:**
- Modify:
  - `src/SqlSource/Diagnostics/SqlDiagnostics.cs`: `LibraryNotReferenced`, `SQLSRC281`, an error, `NotConfigurable`.  Title: `A library that is on is not referenced`.  Message: `{0} is on for the {1} query methods it reaches here, but the project does not reference {2}`.  Help: `Reference {2}, or turn {0} off for these queries`.  It joins `All`.
  - `src/SqlSource/AnalyzerReleases.Unshipped.md`, `src/SqlSource/Diagnostics/AGENTS.md`, with no change if `SQLSRC281` is already listed as taken.
  - `src/SqlSource/Generation/Models/QueryModels.cs`: `UnavailableLibraries`.
  - `src/SqlSource/Generation/Models/ModelReader.cs`, which copies it from `QueryTypes`.
  - `src/SqlSource/Generation/Methods/MethodPlanner.cs`, which reports for each `codegen` query of the type.
  - `docs/manual/diagnostics.md`.
- Test: `tests/SqlSource.Tests/Generator/LibraryDiagnosticsTests.cs`, `tests/SqlSource.Tests/Diagnostics/` (the document test catches the section)

**Interfaces:**
- Consumes: `QueryTypes.UnavailableLibraries`, `LibraryProbe.MissingAssembliesOf`.
- Produces: `SQLSRC281` at `new LocationInfo(path, entry.Span, entry.LineSpan)`.  `path` is `entry.Place.Path` for a settings file, and the query's `NameLocation.Path` for a marker.  The arguments are the library's display name, `NodaTime`; the engine, `PostgreSQL` or `SQL Server`; and the missing assemblies.  `MethodPlanner.Distinct` makes it once for each place and engine.

- [ ] **Step 1: Write the failing tests** `LibraryDiagnosticsTests`:
  - `On_InSettingsFile_WithoutPlugin_IsReportedAtTheMember`: the file, line and column of the member, with the message's text.
  - `On_InMarker_IsReportedAtTheMarkersLine`.
  - `On_ReachingTwoTypesAndFiles_IsReportedOnce`.
  - `On_ReachingBothEngines_IsReportedOncePerEngine`, naming `Npgsql.NodaTime` and `NodaTime`.
  - `On_ModelsClaimOnly_IsNotReported`.
  - `On_WithPlugin_IsNotReported`.
  - `Off_WithoutPlugin_IsNotReported`.
  - `On_QueriesKeepTheirMethods_WithBaseTypes`.

- [ ] **Step 2: Run.**  Expected: FAIL.

- [ ] **Step 3: Implement.**  `docs/manual/diagnostics.md` gets the row and the `## SQLSRC281` section, with what it means and how to fix it, and `See: [library types](settings.md#setting-library-types)`.

- [ ] **Step 4: Run** `tests/SqlSource.Tests` whole, the diagnostics document tests included.  Expected: PASS.

- [ ] **Step 5: Commit**: `feat(diagnostics): SQLSRC281 for a library that is on and not referenced`.

### Task 8: End to end on PostgreSQL

**Files:**
- Modify:
  - `Directory.Packages.props`: `PackageVersion` NodaTime 3.3.7 and `Npgsql.NodaTime` 10.0.3.
  - `tests/DatabaseTests/Postgres/Postgres.csproj`: references `Npgsql.NodaTime`.
  - `tests/DatabaseTests/Postgres.Npgsql8/Postgres.Npgsql8.csproj`: `Npgsql.NodaTime` `VersionOverride="8.0.9"` and NodaTime `VersionOverride="3.1.9"`, and the new queries and sidecars linked.
  - Every changed `packages.lock.json`.
  - `tests/DatabaseTests/Postgres/sqlsource.json`: `"libraryTypes": {"nodaTime": "off"}`, with a comment that the root turns the detected library off for everything but `EndToEnd/NodaTime/`.
  - `tests/DatabaseTests/Postgres/EndToEnd/app/schema.sql` and `seed.sql`: a new table, `noda_values`.
  - `EndToEnd/EndToEndSources.cs`: a NodaTime data source built with `UseNodaTime()`.
  - `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md`, `Postgres.Npgsql8/AGENTS.md`, `tests/DatabaseTests/Postgres/README.md`.
  - `tools/test-scopes.txt`, only if it lists `EndToEnd` subfolders.
- Create:
  - `tests/DatabaseTests/Postgres/EndToEnd/NodaTime/sqlsource.json`: `{"libraryTypes": {"nodaTime": "default"}}`, by Q1's reading A, with a comment that detection turns it on.
  - `EndToEnd/NodaTime/Queries/NodaTime.sql`, its sidecar written by `tools/describe-end-to-end.sh`, `EndToEnd/NodaTime/NodaTimeQueries.cs` and `NodaTimeQueryTests.cs`.
  - In `Postgres.Npgsql8`: `NodaTimeQueries.cs`, `NodaTimeQueryTests.cs`, and a NodaTime data source.

**Interfaces:**
- Consumes: everything above.
- Produces: `noda_values` with `id int`.
  - One column of each of the twelve types of Global Constraints, nullable.
  - An array column of each scalar type: `timestamptz[]`, `timestamp[]`, `date[]`, `time[]`, `timetz[]`, `interval[]`, `tstzrange[]`, `tsrange[]` and `daterange[]`.
  - The queries `InsertNodaValues`, which takes every column as a parameter, and `GetNodaValues`, which reads every column by `id`.

- [ ] **Step 1: Capture the generated code before the change.**  Build the PostgreSQL and SQL Server end-to-end, floor and example projects with `-p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=<scratchpad>/before/<project>`.  Do this on the commit before Task 8.

- [ ] **Step 2: Write the failing tests** `NodaTimeQueryTests`, which are in the `UsesPostgres` collection, write in a transaction rolled back, and take the source from `EndToEndSources`:
  - `EveryRow_RoundTrips`: each value, an empty range, an unbounded range, a `(a,b]` `tstzrange` whose bounds are kept, and arrays with a `NULL` element.
  - `Nulls_RoundTrip`: every column and array `NULL`.
  - `MethodTypes_AreNodaTimes`: reflection on the generated models' properties gives `Instant`, `NpgsqlRange<Instant>` and the rest.
  - `WithoutUseNodaTime_ThrowsInvalidCast`, on the plain source.
  - The same first two tests in `Postgres.Npgsql8`.

- [ ] **Step 3: Implement** the schema, the seed if a test reads seeded rows, the queries, the sources and the settings files, then run `tools/describe-end-to-end.sh` for the sidecars.

- [ ] **Step 4: Run** `tests/DatabaseTests/Postgres` and `tests/DatabaseTests/Postgres.Npgsql8` on the default image, then again with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.  Expected: PASS.  Then build again into `<scratchpad>/after/` and run `diff -r before after`.  Expected: only the new `NodaTime` files differ.

- [ ] **Step 5: Commit**: `test(e2e): NodaTime on PostgreSQL through the plugin`.

### Task 9: Documentation

**Files:**
- Modify:
  - `docs/manual/settings.md`: a row and a `setting-library-types` section, whose example comes first, with the levels, the merge per library, `default`, each engine's default, `codegen` only, and the shared-model `SQLSRC257` fix.  Its status is "Works today", for NodaTime on PostgreSQL, with the rest as a callout.
  - `docs/manual/sql-files.md`: the marker index row.
  - `docs/manual/postgresql.md`: the Types section's NodaTime rows and the override alternatives the plugin reads.  `ZonedDateTime` and `OffsetDateTime` are written in UTC, and anything else throws `ArgumentException`.  `Interval` and `DateInterval` by override, with the empty-range throw and the 1 ns shift, and Dapper's boxed read.  The Query methods section says the data source must call `UseNodaTime()` or the method throws `InvalidCastException`.  It names the EF Core and Dapper traps, and that a project mapping NodaTime without the plugin keeps today's behaviour.
  - `docs/manual/sqlserver.md`: a callout for NodaTime on SQL Server.
  - `docs/manual/generator.md`: the order of a column's type with the library map, and a model shared across outputs.
  - `docs/manual/diagnostics.md`: `SQLSRC111`'s and `SQLSRC019`'s lists, if they enumerate markers or keys.
  - `docs/manual/README.md`: the "What works today" row, "NodaTime on PostgreSQL available; the rest coming".
  - `README.md`: the FAQ on `InvalidCastException` gains the unregistered plugin, and the "NodaTime and other libraries" callout narrows to what is still to come, with a sentence and a link for NodaTime on PostgreSQL.
  - The epic outline's 7.1 row: `Done`, with its Delivered text.
  - `docs/architecture/generator.md`:
    - The switch under Settings and Parsing.
    - The probe, the library rows, `WithoutLibrary` and `TypeModels`' pick under Types and Models.
    - `SQLSRC281` under Diagnostics and Query methods.
    - The new step in the pipeline's list.
    - The "Library maps" placeholder narrowed to the generated conversions, which are not built.
  - `docs/manual/AGENTS.md`'s phase 7 row, only where it is now wrong.
- Test: `tests/SqlSource.Tests/Documentation/` (links, samples), `Diagnostics/DiagnosticsDocumentTests`

- [ ] **Step 1: Write the pages.**  Then run the greps of `docs/manual/AGENTS.md`, "Write effects, never internals" and "When a phase lands", and fix every hit.

- [ ] **Step 2: Run** `dotnet test --project tests/SqlSource.Tests --filter-namespace '*Documentation*'` and the diagnostics document tests.  Expected: PASS.

- [ ] **Step 3: Commit**: `docs: the library switch and NodaTime on PostgreSQL`.

### Task 10: Validate and hand over

- [ ] `./format.sh`, then `./pre-commit-validation.sh --full`.  Expected: every step passes.
- [ ] The floor-image run of Task 8, Step 4, on the final tree.
- [ ] Write `7-1-notes.md` in the shared notes folder:
  - `LibraryProbe`'s shape and how 7.2 to 7.4 extend it: a flag per marker and `IsAvailable` and `DefaultOf` cases.
  - `SqlLibrary`, which gains `Json` and `NetTopologySuite` by Q2.
  - `LibraryRows`, `TypeMapping.Library`, `PropertyType.Library` and `WithoutLibrary`, and the plugin rows' null `Place`.
  - `QueryTypes.UnavailableLibraries` and `QueryModels.UnavailableLibraries`.
  - The id taken, `SQLSRC281`, and the conflict at `SQLSRC284`.
  - The end-to-end folder and the root `off`.
- [ ] Then `/branch-review:review --base origin/main`.
