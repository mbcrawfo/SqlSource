# Query generation, phase 7.3: JSON - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The `json` library and its name in the switch, which makes an unmapped JSON column a `JsonElement`; `JsonElement`, `JsonDocument`, `JsonNode`, `JsonObject` and `JsonArray` read and sent on both engines, through the driver where it can and through generated conversions where it cannot; a project's own class in a JSON column through System.Text.Json's default options, unless the project has a converter of that edge; the JSON opt-in for string columns with `SQLSRC283`; end to end on both engines and their floors.

**Architecture:** Built on 7.2's `Library` origin.  The JSON types are library edges of a generated `JsonConversions` class, scoped by `ConverterTargets`.  The serializer is a kind of conversion of its own: a branch of `ConversionPlanner.Plan` that gives a `Conversion` whose `Method` is `JsonSerializer.Deserialize<T>` or `Serialize<T>`, flagged `IsSerializer`, on a database type that holds JSON, where no converter touches the class.  The opt-in is a flag from the settings and markers through typing to the planner, which reports `SQLSRC283` for `codegen` queries only.

**Tech Stack:** C# netstandard2.0 source generator (Roslyn 4.8), System.Text.Json as emitted text only, xUnit v3, Shouldly, Npgsql 8 and 10, Microsoft.Data.SqlClient 6.1 and 7, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-10-26-query-generation-phase-7-library-maps-design.md`, sections "JSON", "The JSON opt-in", "Generated conversions" and "7.3 JSON"; spike `docs/superpowers/specs/2026-10-26-query-generation-phase-7-spike.md` question 3; hand-over notes `7-1-notes.md`, `7-2-notes.md` and the manager's `notes.md` ("Shared changes").

## Where merged code differs from the spec

- **`SQLSRC283`'s location.**  The spec puts it "at the place that says it".  Every typing and converter problem of a query (`SQLSRC250` to `SQLSRC252`, `SQLSRC279`, `SQLSRC280`) is reported at the query and names its place in the message, and a settings file's level keeps no member positions (only the library types have a side channel, `LibraryMember`).  This plan reports `SQLSRC283` at the query, naming the place, as `SQLSRC250` does; the owner was asked (blocks merge), and only Task 6's reporting step moves if the answer differs.
- **The serializer needs no generated class.**  The spec allows "a generic read and write between `string` and the class"; the method calls `global::System.Text.Json.JsonSerializer` itself, so the reflection warnings (`IL2026`, `IL3050`) fall in the generated method, as the anti-goal says.  `Conversion.Library` stays null for it: System.Text.Json has no floor and no class to emit.
- **`LibraryRows.Of` for more than one library** is this unit's (manager's notes): one cached instance per set of libraries.

## Global Constraints

- The analyzer targets `netstandard2.0`, Roslyn 4.8, and names no library type in its own code: System.Text.Json's types are text it emits, `global::`-qualified.
- Generated code needs .NET 8 and C# 12, compiles warning-free on .NET 8 and 10 against Npgsql 8 and 10 and SqlClient 6.1 and 7, `#nullable enable`.
- Default for `json` on both engines: `off`.  The library's row: PostgreSQL `json` and `jsonb` are `JsonElement`, `jsonb[]` is `JsonElement[]`; SQL Server `json` is `JsonElement`.  `jsonpath` and `xml` stay `string` with any library on.
- Edges, verbatim from the spec: `JsonElement` read by `JsonDocument.Parse(v)` and the root's `Clone()`, the document disposed, written by `GetRawText()`; `JsonDocument` read by `JsonDocument.Parse(v)`, written by `RootElement.GetRawText()`; `JsonNode`, `JsonObject`, `JsonArray` read by `JsonNode.Parse(v)` cast to the subtype, written by `ToJsonString()`.  Scope: `JsonElement` and `JsonDocument` `ConverterTargets.SqlServer`; `JsonNode`, `JsonObject`, `JsonArray` `SqlServer | NpgsqlBefore10`.
- The serializer, verbatim: read `JsonSerializer.Deserialize<T>(reader.GetFieldValue<string>(i))`, write `JsonSerializer.Serialize(v)`; System.Text.Json's default options.  It applies to `json` and `jsonb` on PostgreSQL and `json` on SQL Server, and to a `string` column by the opt-in.  A "class" is any type that is no JSON type and no type the base map gives (`PostgresTypeMap.Gives`, `SqlServerTypeMap.Gives`); a collection or a record is a class.
- A JSON `null` read into a not-null property throws `InvalidCastException("Column '<name>' holds a JSON null.")`, for a class and for `JsonNode`, `JsonObject` and `JsonArray`, whether a generated parse or the driver read it; into a nullable one it is null.  A SQL `NULL` never reaches the serializer or a converter.
- The opt-in: `"columnTypes": { "orders.metadata": { "type": "OrderMetadata", "json": true } }`; `-- column: orders.metadata -> json OrderMetadata` in a header; `-- column: metadata -> json OrderMetadata` in a query; `-- param: @metadata -> json OrderMetadata`.  `false` or no `json` member is the plain mapping.  Not in the type override.  Not in the query's hash.
- `SQLSRC283`, an error: the opt-in on a column or parameter whose base-map type is not `string` and that is not a JSON database type, or with a C# type that is a JSON type or a type the base map gives; `codegen` claims only.
- Diagnostic ids: this unit owns `SQLSRC283`; any further id comes from `SQLSRC285` to `SQLSRC289`.
- The existing end-to-end tests, the realistic corpora and the examples stay byte for byte unchanged in their generated code.
- Shared with 7.4: in `ConversionPlanner`, `QueryMethodWriter`, `RowReaderWriter`, the binders, `ConverterSet`, `LibraryProbe`, `LibraryConverters`, `LibraryRows` and `MethodPlanner` add arms and branches of the JSON kind only; never restructure a `NodaTime or _` switch beyond splitting it; docs get only this library's rows and subsections; end-to-end projects get only their own `Json/` folders and their own lines in `CheckTests`, `RepositoryPaths`, `tools/describe-end-to-end.sh` and the floor projects.
- Every commit passes `./pre-commit-validation.sh`; run the formatter (`dotnet csharpier format .`, as `CONTRIBUTING.md` says) before it.

## Review Focus

1. **A domain over `jsonb`, and an array of a domain over `jsonb`, mapped to a class**: a user expects the serializer, as for `jsonb` itself.  Task 4 pins it.
2. **A class that is a value type** (`readonly record struct Point`) in a nullable and a not-null `jsonb` column and parameter: the generated code compiles warning-free and needs no JSON-null check.  Task 4 pins it.
3. **A converter of one direction only** (`string → OrderMetadata`) for a `jsonb` column and parameter: the column reads by it, the parameter is `SQLSRC280`, never the serializer.  Task 4 pins it.
4. **`-- param: @meta -> json OrderMetadata` on a `jsonb` parameter**: allowed and the same code as without the word.  Task 6 pins it.
5. **A type override `jsonb=App.OrderMetadata` reaching a `models` query and a `codegen` query of one file**: the `models` query's model keeps the class and nothing is reported for it; the `codegen` method serializes.  Task 6 pins it.

---

### Task 1: The `json` library in the switch, the probe and the rows

**Files:**
- Modify: `src/SqlSource/Settings/Libraries/SqlLibrary.cs`, `src/SqlSource/Generation/Types/Drivers/LibraryProbe.cs`, `src/SqlSource/Generation/Types/Maps/LibraryRows.cs`, `src/SqlSource/Generation/Methods/MethodPlanner.cs` (`LibraryName`), `schemas/settings-v1.schema.json` (library names), whatever `SettingsFileSchemaTests` and the `SQLSRC111` help of `-- library-types:` derive from the enum.
- Test: `tests/SqlSource.Tests/Generator/LibraryProbeTests.cs`, `tests/SqlSource.Tests/Generator/LibraryTypeTests.cs`, the `LibraryRows` unit tests beside the maps' (`tests/SqlSource.Tests/Generation/Types/Maps/`), `SettingsFileSchemaTests`.

**Interfaces:**
- Produces: `SqlLibrary.Json` (written `json`); `LibraryProbe(bool NodaTime, bool NpgsqlNodaTime, Version? NodaTimeVersion, bool Json = false)`, `Json` read by `DriverProbes.Has(compilation, "System.Text.Json.JsonElement")`; `LibraryRows.PlaceOf(SqlLibrary.Json) == "the System.Text.Json library"`; `MethodPlanner.LibraryName(SqlLibrary.Json) == "System.Text.Json"`; `LibraryProbe.MissingAssembliesOf(SqlLibrary.Json, _) == "System.Text.Json"`.

- [ ] **Step 1: Write the failing tests**
  - `LibraryProbeTests.Read_SystemTextJson_FindsJson`: a compilation with the runtime's System.Text.Json gives `Json == true`; one without it (`netstandard`-only references) gives false.
  - `LibraryProbeTests.DefaultOf_Json_IsOffOnBothEngines`: `DefaultOf(SqlLibrary.Json, engine)` is `Off` with `Json == true`, for both engines.  `IsAvailable(SqlLibrary.Json, engine) == Json` for both; `IsBelowTheFloor(SqlLibrary.Json)` is false.
  - `LibraryTypeTests.Type_JsonOn_GivesJsonElement` (theory): with `-- library-types: json=on`, a `codegen` query's `json` and `jsonb` columns and parameters are `global::System.Text.Json.JsonElement`, `jsonb[]` is `global::System.Text.Json.JsonElement[]`, `jsonpath` and `xml` stay `string`; SQL Server's `json` is `JsonElement` and `nvarchar(max)` stays `string`.
  - `LibraryTypeTests.Type_JsonUnset_KeepsString`: no setting, System.Text.Json referenced: both engines keep `string` (anti-goal).
  - `LibraryTypeTests.Type_JsonAndNodaTimeOn_GivesBoth`: both on in one query: `timestamptz` is `Instant` and `jsonb` is `JsonElement`.
  - `LibraryRowsTests.Of_SameSet_IsTheSameInstance`: `Of([Json, NodaTime])` and `Of([NodaTime, Json, Json])` are the same reference; `Of([])` is null; `Of([Json])` finds no NodaTime row.
  - A caching test beside 7.1's for `-- library-types:`: an edit of `json=on` to `json=off` in one file's marker re-types only that file's queries.
  - The schema test accepts `"json": "on"` under `libraryTypes`; a marker `-- library-types: json=maybe` is `SQLSRC111` whose help lists `json`.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~LibraryProbeTests|FullyQualifiedName~LibraryTypeTests|FullyQualifiedName~LibraryRows|FullyQualifiedName~SettingsFileSchemaTests"`
Expected: FAIL, `SqlLibrary` has no `Json`.

- [ ] **Step 3: Implement**
  - `SqlLibrary.Json`, after `NodaTime`.
  - `LibraryProbe`: the flag and an arm in `IsAvailable` and `MissingAssembliesOf`; `DefaultOf` gives `Off` for `Json` on both engines (the spec: "for `json` on both, `off`"); `IsBelowTheFloor` gives false for it.  Leave `FloorOf` and `VersionTextOf` as they are: they are reached only below a floor.
  - `LibraryRows`: one instance for each set of libraries, built once and found by a bit mask of `SqlLibrary` values (an array of `1 << count` slots, filled at type initialization: there are at most eight sets with 7.4's library); each instance's dictionaries merge the rows of its libraries.  JSON rows: PostgreSQL `json`, `jsonb` → `global::System.Text.Json.JsonElement`, value type; SQL Server `json` → the same.  `Detail = PlaceOf(Json)`, `Library = SqlLibrary.Json`.  Split `PlaceOf`'s `NodaTime or _` arm into `NodaTime` and `Json`.
  - `MethodPlanner.LibraryName`: an arm for `Json`.

- [ ] **Step 4: Run the tests and see them pass** (same command).  Then run `CachingMethodTests` and `CachingTypeTests`, which must stay green.

- [ ] **Step 5: Commit** — `feat(libraries): the json library in the switch, with JsonElement for JSON columns`

### Task 2: The JSON types through generated conversions

**Files:**
- Create: `src/SqlSource/Generation/Converters/JsonConversions.cs`, `src/SqlSource/Generation/Converters/JsonTypes.cs`
- Modify: `src/SqlSource/Generation/Converters/LibraryConverters.cs`
- Test: `tests/SqlSource.Tests/Generator/LibraryConversionTests.cs`, `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs`, `tests/SqlSource.Tests/Generator/MethodCompileTests.cs` (or the compile tests 7.2 added for NodaTime)

**Interfaces:**
- Consumes: `SqlLibrary.Json`, `LibraryProbe.Json` (Task 1).
- Produces: `JsonTypes` (internal static): `const string Element = "global::System.Text.Json.JsonElement"`, `Document`, `Node`, `Object`, `Array` (the last three under `System.Text.Json.Nodes`); `static bool IsJsonType(string text)` (one of the five, compared ordinally to `ClrTypeName.KeywordTextOf` text); `static bool IsNode(string text)` (node, object, array).  `JsonConversions.Edges` (`EquatableArray<Converter>`, sorted by `ConverterSet.Comparer`, `Origin = Library`, `LibraryScope(SqlLibrary.Json, …)`), `JsonConversions.Source`, `ClassName = "global::SqlSource.JsonConversions"`, `MetadataName = "SqlSource.JsonConversions"`.

- [ ] **Step 1: Write the failing tests**
  - `LibraryConversionTests.Run_SqlServerJsonColumnWithJsonOn_ReadsAndWritesThroughJsonConversions`: a SQL Server `codegen` query with a nullable and a not-null `json` column and parameter, `json=on`: the method reads `global::SqlSource.JsonConversions.JsonElementFromString(reader.GetFieldValue<string>(i))` behind `IsDBNull` for the nullable one, binds `global::SqlSource.JsonConversions.StringFromJsonElement(value)` as `NVarChar` at `-1`, and `SqlSource-JsonConversions.g.cs` is emitted.
  - `LibraryConversionTests.Run_SqlServerOverridesToEachJsonType_ConvertWithJsonOff` (theory over `JsonDocument`, `JsonNode`, `JsonObject`, `JsonArray`, `JsonElement`): `-- column: c -> System.Text.Json.Nodes.JsonObject` on `nvarchar(max)` and `json`, no switch: converted by the matching edge.
  - `LibraryConversionTests.Run_PostgresJsonNode_ByEdgeOnNpgsql8AndByTheDriverOn10`: an override `jsonb=System.Text.Json.Nodes.JsonNode`: with Npgsql 8 metadata the read is `JsonNodeFromString(reader.GetFieldValue<string>(i))` and the parameter `NpgsqlParameter<string>` with `DataTypeName = "jsonb"`; with Npgsql 10 the read is `reader.GetFieldValue<global::System.Text.Json.Nodes.JsonNode>(i)` and no class is emitted.
  - `LibraryConversionTests.Run_PostgresJsonElementAndDocument_GoToTheDriver`: on Npgsql 8 and 10, `JsonElement` (library row) and `JsonDocument` (override) are read by `GetFieldValue<T>` and bound as `NpgsqlParameter<T>` with `DataTypeName` `json`/`jsonb`; no class emitted.
  - `LibraryConversionTests.Run_ProjectConverterOfJsonElement_ReplacesTheLibrarys`: a project's `[SqlSourceConverter]` `string → JsonElement` and back is called instead, on SQL Server, with no diagnostic.
  - `LibraryConversionTests.Run_NoCodeGenUse_EmitsNoJsonConversions`: `json=on` on a `models` query alone emits no class.
  - `LibraryConvertersTests.ClassMetadataNames_ComparedWithTheSources_AreEveryClassTheyDeclare` keeps passing with the new class (extend its data).
  - A compile test: every edge used by one SQL Server and one Npgsql 8 query compiles with no warning against SqlClient 6.1 and 7 and Npgsql 8 metadata, on .NET 8 and 10 reference assemblies, as 7.2's NodaTime compile test does.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~LibraryConversionTests|FullyQualifiedName~LibraryConvertersTests|FullyQualifiedName~MethodCompileTests"`
Expected: FAIL.

- [ ] **Step 3: Implement**
  - `JsonConversions`, following `NodaTimeConversions`: one table drives `Edges` and `Source`; names `<Return>From<Argument>` on short names (`JsonElementFromString`, `StringFromJsonElement`, …).  A body is an expression, except `JsonElementFromString`, which needs a block (`using var document = …Parse(value); return document.RootElement.Clone();`): let the table hold a full member body text (`=> expr;` or `{ … }`).  The node edges return `JsonNode?`, `JsonObject?`, `JsonArray?`; the object and array reads cast (`(global::System.Text.Json.Nodes.JsonObject?)global::System.Text.Json.Nodes.JsonNode.Parse(value)`), so a wrong shape throws `InvalidCastException` (spike).  Their `Converter.ReturnText` is the type without `?`.
  - `LibraryConverters`: `ClassMetadataNames` adds `JsonConversions.MetadataName`; `Of(probe)` gives NodaTime's edges when `probe.NodaTime` and JSON's when `probe.Json`, merged in `ConverterSet.Comparer` order (no allocation when only one library has edges); `SourceOf(Json)`.
  - `JsonTypes`, used by this task's tests and by Tasks 3 and 4.

- [ ] **Step 4: Run the tests and see them pass** (same command), and `ConverterMethodTests`, `CachingMethodTests`.

- [ ] **Step 5: Commit** — `feat(converters): the JSON types through generated conversions`

### Task 3: A JSON null into a not-null property throws

**Files:**
- Modify: `src/SqlSource/Generation/Methods/QueryMethod.cs` (`MethodColumn`), `src/SqlSource/Generation/Methods/Conversion.cs`, `src/SqlSource/Generation/Methods/MethodPlanner.cs` (where `MethodColumn` is built), `src/SqlSource/Generation/Emitting/RowReaderWriter.cs`
- Test: `tests/SqlSource.Tests/Generator/LibraryConversionTests.cs` (or a new `JsonMethodTests.cs` beside it)

**Interfaces:**
- Consumes: `JsonTypes.IsNode` (Task 2).
- Produces: `MethodColumn(..., Conversion? Read = null, bool ThrowsOnJsonNull = false)`: a not-null column whose property type is a reference type and whose read can give null for a value that is not SQL `NULL`: `JsonTypes.IsNode(TypeText)`, whether converted or read by the driver, or a whole-value read with `Read.IsSerializer` (Task 4 sets that).  `Conversion(..., bool MayReturnNull = false)`: the call can return null for a JSON `null`, set by the planner for a node edge and for the serializer of a reference type; the by-element read uses it.

- [ ] **Step 1: Write the failing tests**
  - `Run_NotNullJsonObjectColumn_ThrowsOnAJsonNull` (SQL Server edge and Npgsql 10 driver): the column reads `… ?? throw new global::System.InvalidCastException("Column 'meta' holds a JSON null.")`; a nullable one reads with no throw.
  - `Run_JsonNodeArrayByElement_ThrowsOnAJsonNullElement` (Npgsql 8, `jsonb[]` as `JsonNode[]`): the element lambda is `item is { } x ? (JsonNodeFromString(x) ?? throw new …("Column 'c' holds a JSON null.")) : null!`, and `JsonNode?[]` gives `null` for both.
  - `Run_JsonElementColumn_HasNoJsonNullCheck`: a value type never gets one.
  - The generated code compiles with no warning (nullable warnings included), in the compile test of Task 2.

- [ ] **Step 2: Run them and see them fail.**

- [ ] **Step 3: Implement**
  - The planner sets `MayReturnNull` on a node edge's conversion (by `JsonTypes.IsNode` of the converter's return text, on a read) and `MethodPlanner` sets `ThrowsOnJsonNull` when it builds the column.
  - `RowReaderWriter.AppendColumn` appends `?? throw new global::System.InvalidCastException(<literal>)` after the read of a column with `ThrowsOnJsonNull`, the message `"Column '" + column.Name + "' holds a JSON null."` through `SymbolDisplay.FormatLiteral`; `AppendByElement` does so inside the lambda for an element that is not nullable when `read.MayReturnNull`.  Nothing else in the writer changes.

- [ ] **Step 4: Run the tests and see them pass**; `GeneratedSourceMethodTests` and `MethodTests` stay green.

- [ ] **Step 5: Commit** — `feat(methods): a JSON null into a not-null property throws`

### Task 4: A project's class through the serializer

**Files:**
- Modify: `src/SqlSource/Generation/Types/DatabaseType.cs`, `src/SqlSource/Generation/Types/QueryTyper.cs` (`DatabaseTypeOf`), `src/SqlSource/Generation/Methods/Conversion.cs`, `src/SqlSource/Generation/Methods/ConversionPlanner.cs`
- Test: `tests/SqlSource.Tests/Generator/JsonSerializerMethodTests.cs` (new), `tests/SqlSource.Tests/Generation/Methods/ConversionPlannerTests.cs`

**Interfaces:**
- Consumes: `JsonTypes.IsJsonType` (Task 2), `MethodColumn.ThrowsOnJsonNull` and `Conversion.MayReturnNull` (Task 3).
- Produces: `DatabaseType(string? Text, bool IsValueType, bool ElementIsValueType, bool HoldsJson = false)`: the database type is `json` or `jsonb` of `pg_catalog`, through a domain to its base, or an array whose element is one (through domains); or SQL Server's `json` (through an alias type).  `Conversion(..., bool IsSerializer = false)`.  `ConversionPlanner` gains a private serializer branch with a parameter `holdsJson` that Task 6 widens with the opt-in.

- [ ] **Step 1: Write the failing tests**, each on PostgreSQL (Npgsql 8 and 10 give the same text) and SQL Server where the type exists, a class `App.OrderMetadata` given by a `-- column:` marker, a column type and an override:
  - `Run_ClassOnJsonb_ReadsAndWritesThroughTheSerializer`: read `global::System.Text.Json.JsonSerializer.Deserialize<global::App.OrderMetadata>(reader.GetFieldValue<string>(i))`, with `?? throw … holds a JSON null.` when not null; write `global::System.Text.Json.JsonSerializer.Serialize<global::App.OrderMetadata>(value)` bound as `NpgsqlParameter<string>` with `DataTypeName = "jsonb"` (SQL Server: `NVarChar`, `-1`); no class emitted.
  - `Run_ClassArrayOnJsonbArray_IsConvertedByElement`: `jsonb[]` as `App.OrderMetadata[]` and `App.OrderMetadata?[]`.
  - `Run_ListOnJsonb_IsSerializedWhole`: `System.Collections.Generic.List<App.Tag>`, and `App.OrderMetadata[]` on a plain `jsonb`.
  - `Run_ValueTypeClassOnJsonb_HasNoJsonNullCheck` (Review Focus 2): `readonly record struct App.Point`, nullable and not null, column and parameter; compiles warning-free.
  - `Run_DomainOverJsonb_IsSerialized` (Review Focus 1): a domain over `jsonb`, and an array of it.
  - `Run_ProjectConverterOfTheClass_ReplacesTheSerializer`: `string ↔ OrderMetadata` converters are called.
  - `Run_ConverterOfOneDirection_ReadsByItAndIsSQLSRC280ForTheParameter` (Review Focus 3).
  - `Run_ClassWithConvertersOfAnotherType_IsSQLSRC279`: `OrderMetadata ↔ byte[]` only: `SQLSRC279` as 8.2 gives it, no serializer.
  - `Run_ClassOnText_KeepsTheDriversRead`: a class on `text` (PostgreSQL) and `nvarchar(50)` (SQL Server) without the opt-in, no converter: read by `GetFieldValue<global::App.OrderMetadata>`, as 8.2 does; no diagnostic.
  - `Run_JsonTypesAndBaseTypes_AreNeverSerialized`: `string`, `JsonElement`, `JsonNode` on `jsonb` take no serializer.
  - `Run_SerializedMethods_Compile`: the generated code compiles warning-free against both drivers' metadata.

- [ ] **Step 2: Run them and see them fail.**

- [ ] **Step 3: Implement**
  - `QueryTyper.DatabaseTypeOf` sets `HoldsJson` (a small helper beside it; match `pg_catalog` `json`/`jsonb` by internal name ignoring case, SQL Server by `SqlServerTypeMap.BaseName` of the type the alias walk ends at).  It changes no model's C#, so `ModelCollector.FindDifference` does not compare it.
  - `ConversionPlanner.Plan`: after the whole-value edge is not found and before `ReportForeign`, when `holdsJson && databaseText == "string" && !decision.Touches(text) && IsSerializable(text)`, return the serializer's conversion of the whole value.  In the element path, after the element's edge is not found and before its `ReportForeign`, when `database.HoldsJson && databaseElement == "string" && !decision.Touches(element) && IsSerializable(element)`, return it by element (`ElementText = ElementOf(text)`).  `IsSerializable(t) = !JsonTypes.IsJsonType(t) && !Gives(t)` for the context's engine.  The method's text is `global::System.Text.Json.JsonSerializer.Deserialize<T>` for a read and `…Serialize<T>` for a write, `T` the type without `?`; `MayReturnNull` is set for a read of a reference type.  The branch never reports anything.
  - `MethodPlanner` sets `ThrowsOnJsonNull` for a not-null reference-type whole-value serializer read (Task 3's rule).

- [ ] **Step 4: Run the tests and see them pass**, and `ConverterMethodTests`, `ConversionPlannerTests`, `MethodDiagnosticsTests`.

- [ ] **Step 5: Commit** — `feat(methods): a project's class in a JSON column through System.Text.Json`

### Task 5: The opt-in's grammar

**Files:**
- Modify: `src/SqlSource/SettingsFiles/SettingsFileReader.cs`, `SettingsFileValue.cs`, `SettingsFileSource.cs`, `src/SqlSource/Settings/ISettingSource.cs` and its other implementations (the attribute's source in `Generation/TargetTypeReader.cs`), `src/SqlSource/Settings/SettingsLevelReader.cs`, `src/SqlSource/Settings/ColumnTypes/ColumnType.cs`, `ColumnTypeGrammar.cs`, `ColumnTypeMap.cs` (merge keeps the flag), `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs`, `SqlParameterDeclaration.cs`, `SqlColumnDeclaration.cs`, `src/SqlSource/Parsing/SqlQueryParameter.cs`, `SqlColumnMarker.cs`, `src/SqlSource/Generation/ColumnMarker.cs` and whatever copies a declaration into them, `schemas/settings-v1.schema.json`
- Test: the settings reader's, the column types grammar's and the marker scope's tests under `tests/SqlSource.Tests/` (`SettingsFiles/`, `Settings/ColumnTypes/`, `Parsing/`), `SettingsFileSchemaTests`, `SqlFileParserAllocationTests`, a hash test

**Interfaces:**
- Produces: `ColumnType(string Key, string? Type, SettingPlace Place, bool IsJson = false)`; `SqlQueryParameter(..., string? ClrType = null, bool IsJson = false)`; `SqlColumnMarker`/`ColumnMarker`/`SqlColumnDeclaration` with `bool IsJson`; `ISettingSource.IsJsonMember(SettingId setting, int index)`, true for a member of the column types written as an object with `"json": true`.

- [ ] **Step 1: Write the failing tests**
  - Settings file: `{ "columnTypes": { "orders.metadata": { "type": "OrderMetadata", "json": true }, "orders.note": { "type": "Note" }, "orders.created_by": "UserId" } }` gives three entries, the first `IsJson`.  An object member with no `type`, a non-string `type`, a non-boolean `json` or another member is the settings file's existing not-valid-member diagnostic at that member, and the others apply.  Any other setting's object member that is an object stays the wrong-type error.
  - Schema: the same file validates; `{ "type": "X", "extra": 1 }` does not.
  - Header `-- column: orders.metadata -> json OrderMetadata` gives an `IsJson` entry; `-> json default` and `-> json` followed by nothing are `SQLSRC111`; `-> json` alone names a type `json` (one word), as today.
  - Query `-- column: metadata -> json OrderMetadata` and `-- column: metadata not null -> json OrderMetadata`; `-- param: @metadata -> json OrderMetadata` and `-- param: @metadata jsonb null -> json OrderMetadata`: `IsJson` set, `ClrType` the type.  Two markers of one parameter or column that differ only in `json` are the existing conflicting-marker error.
  - The query's hash is the same with and without the word `json` (`SqlQueryHash` tests).
  - `SqlFileParserAllocationTests` stays within its budget; if the new field moves the per-query bytes, raise the measured figure in the test, never the budget, and report the before and after in the hand-back.

- [ ] **Step 2: Run them and see them fail.**

- [ ] **Step 3: Implement**
  - The reader's `ReadObject` takes an object member only for `columnTypes` (`SettingsFileKeys.ColumnTypes`); it records the member's value as the `type` text, or as the empty string for a malformed object so that the grammar's existing invalid-member path reports it; `SettingsFileValue` keeps a parallel list of the `json` flags.  Its value span is the object's.
  - `ColumnTypeGrammar.ReadMembers` takes the flags; `TryReadHeaderEntry` reads a leading `json` word followed by white space.  The marker scope reads the same leading word after `->` for `-- param:` and a query's `-- column:`.  Share one helper for "a leading `json` and the type after it" in `ColumnTypeGrammar` (the parser may call into `Settings/`, as it calls `ClrTypeName`).
  - The flag rides with the entry through `ColumnTypeMap.Over` and `Find`.

- [ ] **Step 4: Run the tests and see them pass**, with the parser's, settings' and caching tests.

- [ ] **Step 5: Commit** — `feat(settings): the JSON opt-in on column types and markers`

### Task 6: The opt-in in typing and the planner, and `SQLSRC283`

**Files:**
- Modify: `src/SqlSource/Generation/Types/PropertyType.cs`, `src/SqlSource/Generation/Types/QueryTyper.cs` (`Chosen` and its three callers), `src/SqlSource/Generation/Methods/ConversionPlanner.cs`, `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/Diagnostics/AGENTS.md` (ids taken), `src/SqlSource/AGENTS.md` if it lists ids, `docs/manual/diagnostics.md`
- Test: `tests/SqlSource.Tests/Generator/JsonSerializerMethodTests.cs`, `tests/SqlSource.Tests/Diagnostics/` (the document test)

**Interfaces:**
- Consumes: `ColumnType.IsJson`, `SqlQueryParameter.IsJson`, `ColumnMarker.IsJson` (Task 5); the serializer branch (Task 4).
- Produces: `PropertyType(..., bool IsJson = false)`, set exactly when the place that chose the type said `json`; not compared by `ModelCollector.FindDifference`.  `SqlDiagnostics.JsonOptInNotApplicable`, id `SQLSRC283`, error, `NotConfigurable`, help link `sqlsrc283`.

- [ ] **Step 1: Write the failing tests**
  - `Run_OptInOnNvarcharMax_ReadsAndWritesThroughTheSerializer` (SQL Server, each of the four places) and `Run_OptInOnText_…` (PostgreSQL): the serializer's code, bound as the column's type (`NVarChar` at `-1`; `DataTypeName = "text"`).
  - `Run_OptInOnJsonb_ChangesNothing` (Review Focus 4): identical generated text with and without the word.
  - `Run_OptInOnInt_IsSQLSRC283`, `Run_OptInWithJsonElement_IsSQLSRC283`, `Run_OptInWithString_IsSQLSRC283`, and `Run_OptInOnTextArray_IsSQLSRC283`: one error at the query naming the column or parameter, the C# type and the place (as `SQLSRC250` names it); the query gets no method.
  - `Run_OptInInAModelsQuery_ReportsNothing`: a `models` claim of the same query: no diagnostic, the model has the class.
  - `Run_OverrideToAClassSharedByModelsAndCodeGen` (Review Focus 5).
  - A converter `string ↔ OrderMetadata` replaces the serializer on an opt-in column too.

- [ ] **Step 2: Run them and see them fail.**

- [ ] **Step 3: Implement**
  - `Chosen` takes `bool isJson`, passed from the parameter's marker, the query's column marker and the column types' entry.
  - `ConversionPlanner.Plan`: an opt-in property is checked first, before any edge: its database type must hold JSON or be read as `string`, and its type must be serializable (Task 4's rule); otherwise `SQLSRC283` at `context.At`, blocked.  The serializer's `holdsJson` becomes `database.HoldsJson || (type.IsJson && databaseText == "string")`.
  - The message names the subject, the C# type, the place (`type.Place`) and why: the database type the key names is not read as a string, or the type is a JSON type, or a type the type map gives; its help says to remove `json`, or map a column of a string or JSON type.  Keep the descriptor's comment of arguments as its neighbours do.
  - `docs/manual/diagnostics.md`: the `SQLSRC283` section (what it means, how to fix it), so `DiagnosticsDocumentTests` passes; the ids-taken lines of `src/SqlSource/Diagnostics/AGENTS.md` (`SQLSRC200` to `SQLSRC284` taken).

- [ ] **Step 4: Run the tests and see them pass**, with `DiagnosticsDocumentTests` and `AgentsFileTests`.

- [ ] **Step 5: Commit** — `feat(diagnostics): SQLSRC283 for a JSON opt-in that does not apply`

### Task 7: End to end on PostgreSQL and Npgsql 8

**Files:**
- Create: `tests/DatabaseTests/Postgres/EndToEnd/Json/sqlsource.json` (`"libraryTypes": {"json": "on"}`), `Json/Queries/Json.sql` and its sidecar (written by `tools/describe-end-to-end.sh`), `Json/JsonQueries.cs`, `Json/JsonQueryTests.cs`, `Json/Types/` (the classes and the converter class)
- Modify: `tests/DatabaseTests/Postgres/EndToEnd/app/schema.sql` (table `json_values`), `tools/describe-end-to-end.sh` (this folder only), `tests/DatabaseTests/Postgres.Npgsql8/Postgres.Npgsql8.csproj` (links) and its own copy of the tests, `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md`, `tests/DatabaseTests/Postgres/README.md`, Postgres `CheckTests`/`RepositoryPaths` if they name folders

**Interfaces:**
- Consumes: Tasks 1 to 6.

- [ ] **Step 1: Write the table, queries and tests.**  `json_values(id int primary key, element jsonb, element_json json, elements jsonb[], document jsonb, node jsonb, obj jsonb, arr jsonb, meta jsonb, metas jsonb[], tags jsonb, camel jsonb, text_meta text)` with nulls allowed, no seed (each test writes in a rolled-back transaction).  Queries: an insert and a read of every column, through the library's `JsonElement` for `element`, `element_json` and `elements`; header `-- column:` entries for `JsonDocument`, `JsonNode`, `JsonObject`, `JsonArray`, `App.OrderMetadata`, `App.OrderMetadata[]`, `List<App.Tag>`, `App.CamelMetadata` (which has `[SqlSourceConverter]` methods that serialize with `JsonSerializerDefaults.Web`) and `-> json App.OrderMetadata` for `text_meta`; a query whose `-- column: meta not null` reads a stored JSON `null`.  Tests (on the existing `EndToEndSources.App`, which calls no `EnableDynamicJson()`): every column round-trips by structure, SQL `NULL` round-trips as null; a JSON `null` reads as a `JsonElement` of kind `Null`, as null into a nullable class and `JsonNode`, and throws `InvalidCastException` "Column 'meta' holds a JSON null." into the not-null one; the camelCase converter's text is stored camelCase (read back with `->> 'orderId'`).  The `JsonDocument` read is disposed by the test.
- [ ] **Step 2: Describe and run**: `tools/describe-end-to-end.sh` writes the sidecar; run `dotnet test tests/DatabaseTests/Postgres --filter FullyQualifiedName~Json` on the default image and on `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`; then `dotnet test tests/DatabaseTests/Postgres.Npgsql8 --filter FullyQualifiedName~Json` (the node types through the generated edges there).
- [ ] **Step 3: Check the anti-goal**: `git diff --stat` shows no sidecar or query outside `Json/` changed; the whole Postgres project's tests pass.
- [ ] **Step 4: Commit** — `test(e2e): JSON on PostgreSQL through the methods`

### Task 8: End to end on SQL Server and SqlClient 6.1

**Files:**
- Create: `tests/DatabaseTests/SqlServer/EndToEnd/Json/sqlsource.json` (`"libraryTypes": {"json": "on"}`, and `"typeOverrides": {"date": "default"}` only if a query of the folder reads a `date`), `Json/Queries/Json.sql` (on `nvarchar`, every major), `Json/Queries/JsonColumns.sql` (on 2025's `json`), their sidecars, `Json/JsonQueries.cs`, `Json/JsonQueryTests.cs`, `Json/Types/`
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/app/schema.sql` (`dbo.json_values`, `nvarchar(max)` columns and one `nvarchar(200)`), `EndToEnd/app/new-types.sql` (`dbo.json_columns` with `json` columns), `RepositoryPaths.cs` and `EndToEnd/CheckTests.cs` (the folder in the named list; `JsonColumns.sql` left out below 17 as `NewTypes.sql` is), `tools/describe-end-to-end.sh` (this folder only), `tests/DatabaseTests/SqlServer.SqlClient61/SqlServer.SqlClient61.csproj` (links) and its own copy of the tests, `tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md`, `tests/DatabaseTests/SqlServer/README.md`

- [ ] **Step 1: Write the tables, queries and tests.**  `Json.sql`: every JSON type by `-- column:` and `-- param:` markers on `nvarchar(max)` columns, a class by the opt-in in each place (settings file of the folder, header, query marker, parameter marker), a JSON `null` into a nullable and a not-null class and `JsonObject`, the converter class with its own options, and a class on the `nvarchar(200)` column without the opt-in, whose read throws the driver's `InvalidCastException` (8.2's rule).  `JsonColumns.sql`: the library's `JsonElement` for a `json` column, a class through the serializer, and the write of a `JsonElement` of kind `Null`, which the server refuses with error 13609 (assert `SqlException.Number`).  Tests run in rolled-back transactions; `JsonColumns` tests skip by `SkipBelow(17, …)`.
- [ ] **Step 2: Describe and run** on the default image and on `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`; then `tests/DatabaseTests/SqlServer.SqlClient61`.  `CheckTests` passes on both.
- [ ] **Step 3: Check the anti-goal** as in Task 7.
- [ ] **Step 4: Commit** — `test(e2e): JSON on SQL Server through the methods`

### Task 9: Documentation

**Files:**
- Modify: `README.md` (library section's callout: System.Text.Json out of it; JSON's line), `docs/manual/settings.md` (`libraryTypes`' `json` and its default; the column types' object form; the converters section: a converter of `string` and a class replaces the serializer), `docs/manual/sql-files.md` (the `json` word of `-- column:` and `-- param:`), `docs/manual/postgresql.md` and `docs/manual/sqlserver.md` (Types: the library's rows and the JSON types; Query methods: how each is read and sent, the null rules, normalization, SQL Server 2025's refusal of a top-level scalar, `JsonDocument` disposal), `docs/manual/recipes.md` (a class in a JSON column, with the default options and with options of its own; camelCase; a source-generated `JsonSerializerContext` in a trimmed or AOT project), `docs/manual/generator.md` if it lists where a type comes from, `docs/manual/README.md` ("What works today" row: JSON delivered, NetTopologySuite still coming), `docs/manual/AGENTS.md` (phase map row 7, if it names JSON as to come), `docs/architecture/generator.md` (the JSON edges, the serializer, the opt-in, `HoldsJson`, the JSON-null check, `LibraryRows.Of` per set; the placeholder names NetTopologySuite alone), `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (the 7.3 row: Done), `tests/DatabaseTests/README.md` if it lists the folders
- Test: `tests/SqlSource.Tests/Documentation/` (links, `sql` info strings), `AgentsFileTests`

- [ ] **Step 1: Write the documents.**  User documents describe effects only (root `AGENTS.md` rule 4): never name the planner, `HoldsJson` or a sidecar field.  A `sql` sample's info string names its dialect.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Documentation|FullyQualifiedName~DiagnosticsDocument"`.  Expected: PASS.
- [ ] **Step 3: Commit** — `docs: JSON columns, the json library and the opt-in`

### After the tasks

- Run the formatter, then `./pre-commit-validation.sh --full` (foreground, timeout 600000), and the describer projects need no floor run (no describer change).
- Write `7-3-notes.md` in the shared notes folder: `SqlLibrary.Json`, `LibraryProbe`'s new parameter, `LibraryRows.Of`'s per-set cache and how 7.4 adds its rows, `Conversion.IsSerializer` and `MayReturnNull`, `MethodColumn.ThrowsOnJsonNull`, `DatabaseType.HoldsJson`, `PropertyType.IsJson`, `JsonTypes`, `SQLSRC283`'s location, the end-to-end folders.
- `/branch-review:review --base origin/main`.
