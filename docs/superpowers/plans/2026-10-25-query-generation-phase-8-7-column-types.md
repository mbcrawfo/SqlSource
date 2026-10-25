# Phase 8.7: column types - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One column or parameter of a plain database type gets a C# type of its own, by its source column (`SqlSourceColumnTypes`, a header `-- column:`), by a query's `-- column: name -> Type`, or by `-- param: @p -> Type`; and a C# type may be written by its short name everywhere SqlSource takes one.

**Architecture:** Column types are a new setting beside the type override: read from MSBuild and header markers into `SettingsLevel.ColumnTypes`, merged per key, and applied in `QueryTyper`, where each sidecar column's `Origin` is at hand; a query's `-- column:` and a `-- param:` carry their C# type on the parsed query.  A type so chosen gets its `PropertyType` the way a type override's does (`Place` and `Database` set), so converters, models, shared models and diagnostics need no new path.  Short names are resolved in `MappedTypeResolver` by the compiler's source lookup and a per-reference index cached by `MetadataReference`.

**Tech Stack:** C# (`netstandard2.0` generator, Roslyn 4.8.0 floor), xUnit + Shouldly, MSBuild props/targets, PostgreSQL and SQL Server end-to-end projects (Docker).

**Spec:** [`docs/superpowers/specs/2026-10-25-query-generation-phase-8-7-column-types-design.md`](../specs/2026-10-25-query-generation-phase-8-7-column-types-design.md).  Read it first; this plan argues from it.

## Global Constraints

- Every commit passes `./pre-commit-validation.sh`, run after the last edit, in the background (it takes minutes and needs Docker).  Never commit past a failure.  Root `AGENTS.md`, Commits.
- `src/SqlSource` is `netstandard2.0`, Roslyn pinned at 4.8.0: no newer Roslyn API; no collection expression targeting `ImmutableArray<T>`; cached values are records holding `EquatableArray<T>`.  `src/SqlSource/AGENTS.md`.
- A test in `tests/SqlSource.Tests/Generator/` is compiled again on Roslyn 4.8.0: C# 12 at most, starts with the `// Shared:` line, helpers go in `tests/SqlSource.Testing/Generator/`.  `tests/SqlSource.Tests/Generator/AGENTS.md`.
- Every MSBuild property of the package starts with `SqlSource`; a new one is listed where `BuildFileTests` checks.  Root rule 6.
- User docs describe effects, never internals; run the grep of `docs/manual/AGENTS.md` over each changed page.  Settings are documented in `settings.md` only.
- No new diagnostic id.  Next free stays `SQLSRC281`; parse ids unchanged.
- No change to a project that uses none of this: existing generated output byte for byte.
- `-> Type` on `-- param:` or `-- column:` never changes a query's hash.
- The sidecar check re-running on an edit of its file is fine; its value must come back equal.

## Review Focus

1. **A short name that is also a keyword or a type of `System` in a common package** (`Guid`, `DateTimeOffset`, `List<int>`): resolves to the one framework type, no ambiguity from a type of the same full name in two reference assemblies (facade and implementation).  Test in Task 2: `Resolve_ShortName_SameFullNameInTwoReferences_IsOneCandidate`.
2. **A short name in a project that also has a type of that name in the global namespace** (`UserId` at global scope and `App.UserId`): two candidates, `SQLSRC250`; `global::UserId` resolves to the global one.  Task 2: `Resolve_ShortName_GlobalAndNamespacedType_IsAmbiguous`.
3. **A query's `-- column:` naming a column whose result name differs only by case** (`-- column: Total -> X` for `total`): matches, as nullability markers do.  Task 7: `Type_QueryMarker_MatchesItsColumnIgnoringCase`.
4. **A column type on a column that the query also marks `!`**: the type applies and the nullability is the mark's.  Task 6: `Run_TypeOnlyMarker_OnMarkedColumn_IsNoError` and Task 7 asserts `UserId` not `UserId?`.
5. **A source key in a file of each engine** (`users.id` on a PostgreSQL and a SQL Server file of one project): one MSBuild value maps both engines' `users.id`.  Task 7: `Type_PropertyColumnType_MapsBothEngines`.

---

### Task 1: Short names in the grammar of a C# type

**Files:**
- Modify: `src/SqlSource/Settings/ClrTypes/ClrTypeName.cs` (grammar at ~:280-300, `Text` at ~:80-92)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (phase table: add row 8.7, In progress)
- Test: `tests/SqlSource.Tests/Settings/ClrTypes/ClrTypeNameTests.cs`, `tests/SqlSource.Tests/Settings/TypeOverrides/TypeOverrideGrammarTests.cs`

**Interfaces:**
- Produces: `ClrTypeName.IsShort` (bool, true for a name of one part written without `global::`); `ClrTypeName.Text` writes a short name bare (`UserId`, `List<UserId>`), a `global::` one-part name `global::UserId`, a name of several parts as today.  `ClrTypeName.ShortParts` is not needed; the resolver walks `Parts` and arguments.  A short argument inside a full name keeps its `Text` bare: `global::System.Collections.Generic.List<UserId>`.

- [ ] **Step 1: Add the epic row.**  In the phase table after 8.6: `| 8.7 Column types | In progress | [query-generation-phase-8-7-column-types-design](2026-10-25-query-generation-phase-8-7-column-types-design.md) | A C# type for one column, by its source column at four levels or by a query's \`-- column:\`, and for one parameter by \`-- param: ... -> Type\`; C# types written by their short names. |`

- [ ] **Step 2: Write failing tests**

```csharp
[Theory]
[InlineData("UserId", "UserId")]
[InlineData("List<UserId>", "List<UserId>")]
[InlineData("System.Collections.Generic.List<UserId>", "global::System.Collections.Generic.List<UserId>")]
[InlineData("UserId[]", "UserId[]")]
[InlineData("global::UserId", "global::UserId")]
public void TryParse_ShortName_IsValid(string written, string text)
{
    ClrTypeName.TryParse(written.AsSpan(), out var name).ShouldBeTrue();
    name!.Text.ShouldBe(text);
}

[Fact]
public void TryParse_ShortAndGlobal_AreTwoNames()
{
    ClrTypeName.TryParse("UserId".AsSpan(), out var bare).ShouldBeTrue();
    ClrTypeName.TryParse("global::UserId".AsSpan(), out var global).ShouldBeTrue();
    bare!.IsShort.ShouldBeTrue();
    global!.IsShort.ShouldBeFalse();
    bare.Text.ShouldNotBe(global.Text);
}
```

In `TypeOverrideGrammarTests`, the case that asserts a one-part name without `global::` is not valid now asserts `int4=UserId` is valid with `Type` `UserId`.

- [ ] **Step 3: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~ClrTypeNameTests|FullyQualifiedName~TypeOverrideGrammarTests"` — expect the new cases to fail.

- [ ] **Step 4: Implement.**  Drop the `(qualified || name.Length > 1)` condition; record whether `global::` was written; `Text` prefixes `global::` only when it was written or the name has several parts.  Update the type's `<remarks>` (a name of one part without `global::` is a short name, resolved by the compilation).  `KeywordTextOf` must keep working on resolved text (Task 2 gives it full names).

- [ ] **Step 5: Run the two test classes and the whole `tests/SqlSource.Tests` project.**  Expected: pass.  A generator test that expected `SQLSRC111`/`SQLSRC014` for a one-part type now expects `SQLSRC250` (no such type) — update it in this task.

- [ ] **Step 6: `./pre-commit-validation.sh`, then commit** `Accept a C# type written by its short name`.

---

### Task 2: Resolve short names against the project and its references

**Files:**
- Create: `src/SqlSource/Generation/Types/ShortNameIndex.cs`
- Modify: `src/SqlSource/Generation/Types/MappedType.cs`, `MappedTypeResolver.cs`, `QueryTyper.cs` (`OverrideLookup.MappingOf`, ~:504), `src/SqlSource/Diagnostics/SqlDiagnostics.cs` (`MappedTypeNotFound`)
- Test: `tests/SqlSource.Tests/Generation/Types/ShortNameIndexTests.cs`, `tests/SqlSource.Tests/Generator/ShortNameTests.cs` (new), `tests/SqlSource.Tests/Generator/CachingMappedTypeTests.cs`

**Interfaces:**
- Consumes: `ClrTypeName.IsShort`, `ClrTypeName.Text` (Task 1).
- Produces:
  - `MappedType(string Name, MappedTypeStatus Status, bool IsValueType, bool IsPublic, EquatableArray<string> WarningIds, string Text, EquatableArray<string> Candidates)` — `Text` is the name with every short part replaced by the candidate's `global::` full name (equal to `Name` for a name with no short part); `Candidates` the full names of an ambiguous short part, ordinal order, else empty.
  - `TypeMapping.Text` for a mapped entry is `mapped.Text`, so `PropertyType.Text` is the resolved text.
  - `internal static class ShortNameIndex { public static ImmutableArray<string> MetadataNamesOf(Compilation compilation, string name, int arity); }` — metadata names (`Ns.UserId`, ``Ns.List`1``) of the top-level types of that name and arity in the project's source (`GetSymbolsWithName(name, SymbolFilter.Type)`, top-level only) and in every referenced assembly, each reference's types indexed once in a `ConditionalWeakTable<MetadataReference, …>` and reused while the reference is the same.  Duplicates removed.  Only types accessible to the project's assembly count (`compilation.IsSymbolAccessibleWithin`), checked on the symbol re-found by `GetTypesByMetadataName`.

- [ ] **Step 1: Failing tests.**  `ShortNameIndexTests`: `MetadataNamesOf_ReferenceUnchanged_IndexesOnce` (two compilations sharing one `MetadataReference`; assert the per-reference index object is the same instance, through an `internal` test hook `ShortNameIndex.IndexOf(MetadataReference)`).  `ShortNameTests` (Generator folder, `ModelRuns` harness, Npgsql 10, `int8` column, `SqlSourceTypeOverrides` property):

```csharp
[Fact] public void Run_ShortName_OfTheProject_IsItsFullName()
// source "namespace App { public readonly record struct UserId(long Value); }", "int8=UserId"
// → property type text "global::App.UserId", no diagnostics
[Fact] public void Run_ShortName_OfAReference_IsItsFullName()
[Fact] public void Run_ShortName_InATypeArgument()            // "_int8=System.Collections.Generic.List<UserId>" → "global::System.Collections.Generic.List<global::App.UserId>"
[Fact] public void Run_ShortName_TwoCandidates_IsSQLSRC250()  // App.UserId and Other.UserId; message names both
[Fact] public void Resolve_ShortName_GlobalAndNamespacedType_IsAmbiguous()
[Fact] public void Run_GlobalName_IsTheGlobalNamespaceAlone()  // "global::UserId" with App.UserId only → SQLSRC250
[Fact] public void Run_ShortName_NotAccessibleInAReference_IsNotACandidate()
[Fact] public void Run_ShortName_NestedType_IsNotFound()       // App.Outer.UserId only → SQLSRC250
[Fact] public void Resolve_ShortName_SameFullNameInTwoReferences_IsOneCandidate() // "int8=Guid"-style: System.Guid seen through two refs → found
[Fact] public void Run_ShortAndFullName_ShareAModel()          // two queries, -- output-model: Row, int8=UserId vs int8=App.UserId → one model
```

`CachingMappedTypeTests`: `Run_EditWithShortNames_ResolvedTypesUnchanged` (an edit of an unrelated C# file gives `ResolvedTypes` an `Unchanged` reason).

- [ ] **Step 2: Run them; expect failures.**

- [ ] **Step 3: Implement.**  In `MappedTypeResolver.ResolveType`, a short part asks `ShortNameIndex.MetadataNamesOf`; one candidate is resolved as a full name (today's rule, project's own over a reference's of that full name); two or more give `Ambiguous` with `Candidates`; none `NotFound`.  Build `Text` as the resolution walks.  `SQLSRC250`'s message gains a last argument, the candidates (`", and it could be App.UserId or Other.UserId"` — wording the builder's), empty for none; keep the help to "write the full name".  Update `MappedTypeResolver`'s `<remarks>`.

- [ ] **Step 4: Run** the new tests and the whole `tests/SqlSource.Tests`.  Expected: pass, and no existing generated-source test changes.

- [ ] **Step 5: Validate, commit** `Resolve short C# names against the project and its references`.

---

### Task 3: A marker that describes nothing leaves the hash alone

**Files:**
- Modify: `src/SqlSource/Parsing/SqlQueryHash.cs:54-71`
- Test: `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs`

**Interfaces:**
- Produces: the hash adds a declared parameter only when its `Type` or its `Nullable` is set.

- [ ] **Step 1: Failing tests**

```csharp
[Fact]
public void Of_BareParamMarker_IsTheHashWithoutIt()
{
    Hash("-- name: Q\n-- param: @id\nSELECT @id;").ShouldBe(Hash("-- name: Q\nSELECT @id;"));
}

[Fact]
public void Of_ParamMarkerWithAType_ChangesTheHash()
{
    Hash("-- name: Q\n-- param: @id int4\nSELECT @id;").ShouldNotBe(Hash("-- name: Q\nSELECT @id;"));
}
```

- [ ] **Step 2: Run, expect the first to fail.**  **Step 3: Implement** (skip when both are null; update the type's comment).  **Step 4: Run** `SqlQueryHashTests` and every project's tests in the validation; a committed sidecar whose query has a bare `-- param:` would now be stale — there are none (`git grep -nE -- '-- param: @[A-Za-z0-9_]+ *$' -- '*.sql'` prints nothing).
- [ ] **Step 5: Validate, commit** `Leave a -- param: marker that gives no type out of the hash`.

---

### Task 4: `-- param: ... -> Type`

**Files:**
- Modify: `src/SqlSource/Parsing/Markers/SqlParameterDeclaration.cs`, `SqlMarkerScope.cs` (`ReadParam`, ~:420-471), `src/SqlSource/Parsing/SqlQueryParameter.cs`, `SqlParameterList.cs` (`Declare`, ~:108), `src/SqlSource/Generation/Types/MappedTypeNames.cs`, `QueryTyper.cs` (`TypeQuery`, `Type`)
- Test: `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs`, `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs`, `tests/SqlSource.Tests/Generator/ParameterTypeTests.cs` (new)

**Interfaces:**
- Produces:
  - `SqlParameterDeclaration(string Name, string? Type, bool? Nullable, string? ClrType, SqlMarker Marker)` — `ClrType` as `ClrTypeName.Text` writes it.
  - `SqlQueryParameter(string Name, string? Type, bool? Nullable, bool IsDeclared, string? ClrType = null)`.
  - In `QueryTyper`, a sidecar parameter whose query parameter (by name, ignoring case) has a `ClrType` is typed by it before the type override: a `PropertyType` with `Place` `"the -- param: marker of the parameter"` and `Database` from `DatabaseTypeOf`.  Its failures are `SQLSRC250`-`SQLSRC252` with that place.  A shared helper `PropertyType? Chosen(string clrType, string place, string key, SidecarType type, string owner, string name, Typing typing)` serves this task and Task 7; `key` is what the message names as mapped (`@author`, or the column's key).
  - `MappedTypeNames.Collect` adds each query parameter's `ClrType`.

- [ ] **Step 1: Failing parser tests**

```csharp
[Theory]
[InlineData("-- param: @a -> UserId", null, null, "UserId")]
[InlineData("-- param: @a null -> UserId", null, true, "UserId")]
[InlineData("-- param: @a int4 not null -> App.UserId", "int4", false, "global::App.UserId")]
public void Parse_ParamMarker_WithACSharpType(string marker, string? type, bool? nullable, string clrType)

[Theory]
[InlineData("-- param: @a -> default")]
[InlineData("-- param: @a -> int?")]
[InlineData("-- param: @a ->")]
public void Parse_ParamMarker_BadCSharpType_IsSQLSRC111(string marker)

[Fact] public void Parse_TwoParamMarkers_DifferentCSharpTypes_IsSQLSRC112()
[Fact] public void Of_ParamMarkerOfACSharpTypeAlone_IsTheHashWithoutIt()      // in SqlQueryHashTests
[Fact] public void Of_ParamMarker_AddingACSharpType_KeepsTheHash()            // "@id int4" vs "@id int4 -> UserId"
```

- [ ] **Step 2: Failing generator tests** (`ParameterTypeTests`, `ModelRuns`/`MethodRuns`):

```csharp
[Fact] public void Run_ParamMarker_GivesTheParameterItsType()             // ListPostsParams.Author is global::App.UserId
[Fact] public void Run_ParamMarker_WinsOverTheTypeOverride()             // int8=long project-wide
[Fact] public void Run_ParamMarker_Null_IsNullable()                     // App.UserId?
[Fact] public void Run_ParamMarker_UsesTheConverter()                    // CodeGen: emitted bind calls UserId → long converter
[Fact] public void Run_ParamMarker_MissingType_IsSQLSRC250AtItsPlace()   // message contains "the -- param: marker of the parameter"
```

- [ ] **Step 3: Implement.**  In `ReadParam`, split the value at the first `->` that stands after white space or at the start of the rest; the left side is read as today; the right, trimmed, must be a `ClrTypeName` (else `AddInvalid`).  Compare `ClrType` in the conflict check.  `Declare` copies `ClrType`.  `SqlQueryHash` already ignores it (assert by test).

- [ ] **Step 4: Run** parser and generator tests, then the project.  **Step 5: Validate, commit** `Give a parameter a C# type of its own with -- param:`.

---

### Task 5: The `SqlSourceColumnTypes` setting

**Files:**
- Create: `src/SqlSource/Settings/ColumnTypes/ColumnType.cs`, `ColumnTypeKey.cs`, `ColumnTypeGrammar.cs`, `ColumnTypeMap.cs`
- Modify: `src/SqlSource/Settings/SettingId.cs`, `SettingsLevel.cs`, `SettingsLevelReader.cs`, `QuerySettings.cs`, `src/SqlSource/Generation/MSBuild/MSBuildSettings.cs`, `src/SqlSource/Generation/TargetTypeReader.cs:251`, `src/SqlSource/build/SqlSource.props`, `src/SqlSource/build/SqlSource.targets` (every place that names `SqlSourceTypeOverrides`, except the tool's manifest lines), `src/SqlSource/Generation/Types/MappedTypeNames.cs`
- Test: `tests/SqlSource.Tests/Settings/ColumnTypes/ColumnTypeKeyTests.cs`, `ColumnTypeGrammarTests.cs`, `ColumnTypeMapTests.cs`, `tests/SqlSource.Tests/Settings/SettingsLevelReaderTests.cs`, `QuerySettingsTests.cs`, `tests/SqlSource.Tests/Package/BuildFileTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed record ColumnType(string Key, string? Type, SettingPlace Place)` — `Key` normalized: each part as `TypeKeyText.WritePart` writes its text for a quoted part, and the plain part as written (compared ignoring case); `Type` as `ClrTypeName.Text`, null for `default`.
  - `ColumnTypeKey.TryNormalize(ReadOnlySpan<char> key, out string normalized)` — `table.column` or `schema.table.column`, each part by `TypeKeyText.TryReadPart`; one part, four parts or `[]` false.
  - `ColumnTypeGrammar.ReadList(string text, SettingPlace place, List<string> invalid) -> EquatableArray<ColumnType>` (MSBuild, `key=type`, the type override's white-space and quote rules; last entry of a key wins) and `ColumnTypeGrammar.TryReadHeaderEntry(ReadOnlySpan<char> value, out ColumnType? entry)` (`key -> type` or `key -> default`, place `FileHeader`).
  - `ColumnTypeMap.Over(EquatableArray<ColumnType>? higher, EquatableArray<ColumnType>? lower)` and `ColumnTypeMap.Find(EquatableArray<ColumnType> map, string? schema, string table, string column) -> ColumnType?` — of the entries whose parts match (plain ignoring case, quoted exactly; a two-part key matches any schema), the most specific place wins, then one with a schema, then the one spelled exactly as the origin; `default` winning gives null.
  - `SettingId.ColumnTypes` after `TypeOverrides`; `MSBuildSettings.ColumnTypesName = "SqlSourceColumnTypes"`, not a manifest key; `SettingsLevel.ColumnTypes` (`EquatableArray<ColumnType>?`, merged per key in `Over`); `QuerySettings.ResolveColumnTypes(SettingsLevel markers, SettingsLevel metadata, SettingsLevel property) -> EquatableArray<ColumnType>`.
  - `MappedTypeNames.Collect` adds every level's `ColumnTypes` types.

- [ ] **Step 1: Failing tests**, among them:

```csharp
// ColumnTypeKeyTests
[Theory] [InlineData("users.id")] [InlineData("public.users.id")] [InlineData("\"my schema\".\"Users\".id")] [InlineData("U&\"us\\0065rs\".id")]
public void TryNormalize_ValidKey(string key)
[Theory] [InlineData("id")] [InlineData("a.b.c.d")] [InlineData("users.id[]")] [InlineData("[dbo].[users].[id]")]
public void TryNormalize_NotAKey(string key)
// ColumnTypeMapTests
[Fact] public void Find_TwoPartKey_MatchesAnySchema()
[Fact] public void Find_ThreePartKey_MatchesItsSchemaAlone()
[Fact] public void Find_SchemaWinsOverNoSchema_InOnePlace()
[Fact] public void Find_HigherPlaceWins_OverASchemaBelow()
[Fact] public void Find_QuotedPart_ComparesExactly()
[Fact] public void Over_Default_HidesTheKeyBelow()
// ColumnTypeGrammarTests
[Fact] public void ReadList_Entries_SeparatedByWhiteSpace()
[Fact] public void ReadList_InvalidEntry_IsReportedAndTheOthersApply()
[Fact] public void ReadList_LastEntryOfAKey_Wins()
[Fact] public void TryReadHeaderEntry_Arrow_IsValid()            // "posts.id -> PostId", "posts.id->PostId"
[Fact] public void TryReadHeaderEntry_Nullability_IsNotValid()   // "posts.id not null -> PostId"
// SettingsLevelReaderTests: the property and the metadata read SqlSourceColumnTypes; an invalid entry is SQLSRC014's value
// BuildFileTests: SqlSourceColumnTypes is compiler-visible as a property and as metadata
```

- [ ] **Step 2: Run, expect failures.**  **Step 3: Implement.**  Mirror `SqlSourceTypeOverrides` in `SqlSource.props` (comment, `CompilerVisibleProperty`, `CompilerVisibleItemMetadata`) and `SqlSource.targets` (white-space normalising, the `Outputs`/conditions lists, the trimmed metadata), not in the manifest lines (`SqlSourceManifestLine`), which the tool reads and which have no type override either.
- [ ] **Step 4: Run** settings, package and MSBuild tests; the package-install check runs in the validation.  **Step 5: Validate, commit** `Add the SqlSourceColumnTypes setting`.

---

### Task 6: One `-- column:` marker for types

**Files:**
- Modify: `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs` (`ReadColumn`, `IsAllowedInPreamble`, `BuildLevel`), `SqlColumnDeclaration.cs`, `src/SqlSource/Parsing/SqlColumnMarker.cs`, `SqlFileParser.cs:602-615`, `src/SqlSource/Generation/ColumnMarker.cs`, `SqlFileReader.cs:107-121`, `src/SqlSource/Generation/Models/ModelReader.cs` (`AddUnusableColumnMarkers` ~:205, `IsNullable` ~:390), `MappedTypeNames.cs`
- Test: `tests/SqlSource.Tests/Parsing/SqlFileParserMarkerTests.cs`, `tests/SqlSource.Tests/Generator/ColumnMarkerTests.cs`, `FileDiagnosticsTests.cs`

**Interfaces:**
- Consumes: `ColumnTypeGrammar.TryReadHeaderEntry`, `ColumnType` (Task 5).
- Produces:
  - Header (`SqlScopeKind.Preamble`, and a key of two or three parts in `WholeFile`): an entry in the scope's `_columnTypes`, sorted as `ColumnTypeMap` keeps them, exposed as `Level.ColumnTypes`; another type for one key is `ConflictingSettings`.
  - Query: `SqlColumnDeclaration(string Name, bool? IsNullable, string? ClrType, bool ResetsType, SqlMarker Marker)`; `SqlColumnMarker` and `ColumnMarker` gain the same `bool? IsNullable`, `string? ClrType`, `bool ResetsType` (`-> default`).  At least one of `IsNullable`, `ClrType`, `ResetsType` is set.  A dotted name outside quotes is `InvalidMarkerValue` with the help "a column's source is mapped in the file header".  Two markers of one column conflict when both set the nullability differently or both set a type differently.
  - `ModelReader.IsNullable` uses a marker only when its `IsNullable` is set; `AddUnusableColumnMarkers` reports a marked column only for a marker that sets the nullability.
  - `MappedTypeNames.Collect` adds each query's `ColumnMarkers` `ClrType`.

- [ ] **Step 1: Failing tests**

```csharp
// SqlFileParserMarkerTests
[Theory]
[InlineData("-- column: total -> PostCount", null, "PostCount", false)]
[InlineData("-- column: note null -> NoteText", true, "NoteText", false)]
[InlineData("-- column: id -> default", null, null, true)]
public void Parse_ColumnMarker_InAQuery_WithAType(string marker, bool? isNullable, string? clrType, bool resetsType)
[Fact] public void Parse_ColumnMarker_InTheHeader_IsAColumnType()            // Level.ColumnTypes has posts.id → PostId, place FileHeader
[Fact] public void Parse_ColumnMarker_InTheHeader_WithNullability_IsSQLSRC111()
[Fact] public void Parse_ColumnMarker_InAQuery_DottedName_IsSQLSRC111()
[Fact] public void Parse_ColumnMarker_InAWholeFile_DottedIsTheHeaders()
[Fact] public void Parse_ColumnMarkers_TypeAndNullabilityApart_AreNoConflict()
[Fact] public void Parse_ColumnMarkers_TwoTypes_IsSQLSRC112()
// ColumnMarkerTests
[Fact] public void Run_TypeOnlyMarker_OnMarkedColumn_IsNoError()             // "display_name!" with -- column: display_name -> Name
[Fact] public void Run_TypeOnlyMarker_KeepsTheEntrysNullability()
[Fact] public void Run_TypeMarker_NamingNoColumn_IsSQLSRC260()
```

The existing test that `-- column:` in the header is `SQLSRC116` changes to the header form.

- [ ] **Step 2: Run, expect failures.**  **Step 3: Implement.**  `ReadColumn` splits at `->` as Task 4's `ReadParam` does (share the helper `TrySplitArrow`); the right side is `default` (ignoring case) or a `ClrTypeName`.  **Step 4: Run** parser and generator tests.  **Step 5: Validate, commit** `Read a C# type on the -- column: marker, in a query and in the file header`.

---

### Task 7: Type columns by their source and by their marker

**Files:**
- Modify: `src/SqlSource/Generation/Types/QueryTyper.cs` (`Type`, `TypeQuery`, `Typing`, `LookupOf`), `src/SqlSource/Generation/Types/FileTypesInput.cs` if the metadata and property levels are not already in it
- Test: `tests/SqlSource.Tests/Generator/ColumnTypeTests.cs` (new), `tests/SqlSource.Tests/Generator/CachingTypeTests.cs`, `SharedModelTests.cs`, `ConverterMethodTests.cs`

**Interfaces:**
- Consumes: `QuerySettings.ResolveColumnTypes`, `ColumnTypeMap.Find` (Task 5); `ColumnMarker.ClrType`/`ResetsType` (Task 6); the `Chosen` helper (Task 4).
- Produces: a column's type, in order: its query `ColumnMarker` by name, ignoring case (`ResetsType` skips the column types); `ColumnTypeMap.Find(merged, origin.Schema, origin.Table, origin.Column)` when the column has an origin with a table and a column; then today's path.  Places: `"the property SqlSourceColumnTypes"`, `"the metadata SqlSourceColumnTypes of the file"`, `"a -- column: marker in the file header"`, `"a -- column: marker of the query"`.  The merged column types are made once per distinct markers level, as `LookupOf` does for the type override.

- [ ] **Step 1: Failing tests** (`ModelRuns`, origins by `TestSidecars.Column(..., origin: new SidecarOrigin("public", "users", "id"))`):

```csharp
[Fact] public void Type_PropertyColumnType_TypesItsSourceColumn()
[Fact] public void Type_MetadataColumnType_OverTheProperty()
[Fact] public void Type_HeaderColumnType_OverTheMetadata()
[Fact] public void Type_QueryMarker_OverTheHeader()
[Fact] public void Type_QueryMarker_MatchesItsColumnIgnoringCase()
[Fact] public void Type_QueryMarkerDefault_FreesTheColumnForTheTypeOverride()   // int8=long and users.id=UserId → long
[Fact] public void Type_ColumnType_WinsOverATypeOverrideOfAHigherPlace()       // query -- type-override: int8=long, property users.id=UserId → UserId
[Fact] public void Type_ColumnWithoutOrigin_IsUntouchedByAColumnType()
[Fact] public void Type_ViewColumn_IsTheViewsKey()                             // origin public.user_summaries.id not matched by users.id
[Fact] public void Type_ColumnType_KeepsTheNullability()                       // nullable column → UserId?; "id!" → UserId
[Fact] public void Type_PropertyColumnType_MapsBothEngines()                   // one postgres and one mssql file
[Fact] public void Type_ColumnType_MissingType_IsSQLSRC250AtItsPlace()
[Fact] public void Type_ColumnType_ArrayOfAForeignType_ConvertsByElement()     // MethodRuns, CodeGen, int8[] → UserId[]
[Fact] public void Type_ColumnType_UsesTheConverter()                          // MethodRuns, CodeGen
[Fact] public void Type_NoColumnTypes_GeneratesTheSameSource()                 // a project of each feature-free shape vs a fixed expected text
// SharedModelTests
[Fact] public void Run_TwoQueries_OneColumnTypedByAMarker_IsSQLSRC257()
// CachingTypeTests
[Fact] public void Run_ColumnTypeEdited_RetypesOnlyThatFile()
[Fact] public void Run_ArrowEdited_SidecarCheckUnchanged()
```

- [ ] **Step 2: Run, expect failures.**  **Step 3: Implement.**  **Step 4: Run** the whole `tests/SqlSource.Tests` (both Roslyn versions via the validation).  **Step 5: Validate, commit** `Type a column by its source column and by its -- column: marker`.

---

### Task 8: End to end on both engines

**Files:**
- Modify: `tests/SqlSource.Tests.Postgres/EndToEnd/Queries/Notes.sql`, `tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`, `tests/SqlSource.Tests.Postgres/EndToEnd/NoteQueryTests.cs`, `ModelShapeTests.cs` as affected; the same in `tests/SqlSource.Tests.SqlServer` (`EndToEnd/Queries/Notes.sql`, its `.csproj`, `NoteQueryTests.cs`)
- Create: `tests/SqlSource.Tests.Postgres/EndToEnd/Types/AccountId.cs`, `NoteCount.cs`; the same under `tests/SqlSource.Tests.SqlServer/EndToEnd/Types/`
- Regenerate: the end-to-end sidecars, by `tools/describe-end-to-end.sh`

**Interfaces:**
- Consumes: everything above.
- Produces: in each engine's `Notes.sql`: `accounts.id` mapped to `AccountId` by `SqlSourceColumnTypes` metadata of `Notes.sql` in the `.csproj`; `notes.account_id` by a header `-- column: notes.account_id -> AccountId`; `CountAccountNotes`'s `-- column: note_count -> NoteCount`; a new query `GetAccountNoteCount -> one` with `-- param: @id -> AccountId` and `WHERE a.id = @id`.  `AccountId(long Value)` (or `int`, by the column's type on each engine) and `NoteCount(long Value)` with `[SqlSourceConverter]` pairs, written by short name in every marker and entry.

- [ ] **Step 1: Write the tests** in `NoteQueryTests` of each engine: `ListAccountNotes_IdsAreAccountIds`, `CountAccountNotes_CountIsANoteCount`, `GetAccountNoteCount_TakesAnAccountId`, each running the method over the seed and asserting values.
- [ ] **Step 2: Edit the queries and project files; run `tools/describe-end-to-end.sh`** (background, Docker); read the sidecar diff: only `Notes.sql.json` should change beyond the tool version, since `->` and `-- column:` leave the other hashes alone.
- [ ] **Step 3: Run** `dotnet test tests/SqlSource.Tests.Postgres` and `tests/SqlSource.Tests.SqlServer` on the default images, then on the floor images: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` and `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.
- [ ] **Step 4: Update** `tests/SqlSource.Tests.Postgres/README.md` / `tests/SqlSource.Tests.SqlServer/README.md` only if they list the end-to-end queries or types.  **Step 5: Validate, commit** `Run column types end to end on both engines`.

---

### Task 9: The examples

**Files:**
- Modify: `examples/PostgresExamples/CodeGen/OrderLines.sql`, `OrderLineQueries.cs` as needed, `examples/PostgresExamples/PostgresExamples.csproj`, `examples/PostgresExamples/README.md`; the same in `examples/SqlServerExamples`
- Create: an id type with converters in each `CodeGen/` folder (beside `Price.cs`)
- Regenerate: the examples' sidecars, by `tools/describe-end-to-end.sh`

- [ ] **Step 1:** In each example project, map one id column by `SqlSourceColumnTypes` in the `.csproj` (short name), one computed column by a query's `-- column: ... ->`, and one parameter by `-- param: @x -> Id`, with the id type's converters; the README of the example says what each shows, effects only.  Read `examples/AGENTS.md` first.
- [ ] **Step 2:** Run `tools/describe-end-to-end.sh` if a query's SQL changed; build the solution.  **Step 3: Validate, commit** `Show column types in the example projects`.

---

### Task 10: The documents

**Files:**
- Modify: `docs/manual/settings.md`, `generator.md`, `parameters-and-tokens.md`, `sql-files.md`, `recipes.md`, `diagnostics.md`, `docs/manual/README.md`, `postgresql.md` (Parameters: the domain line now has an alternative), `README.md`, `docs/architecture/generator.md`, `src/SqlSource/AGENTS.md` (only if it names what changed), `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, `docs/deferred/D-0008-converter-per-file-or-query.md` (its "Delivered instead" names the manual's known issue; keep true)

- [ ] **Step 1: `settings.md`.**  A row in "Settings that work today" (Column types; `table.column=type` or `schema.table.column=type`, or `=default`; `SqlSourceColumnTypes` property and metadata; `-- column:` in the file header and inside a query; Works today); a section `<a id="setting-column-types"></a>` example first, with the rules of the spec's "Column types", "`-- column:` inside a query" and "Precedence with the type override"; the precedence exceptions gain the column types' merge per key; the type override's "The type" says a short name is accepted and how it is resolved, its `SQLSRC250` sentence gains ambiguity; "Converters apply only where a type override chose the type" becomes "where a type override, a column type or a `-- param:` marker chose the type".
- [ ] **Step 2: the other manual pages.**  `generator.md` column-marker section: the type, `-> default`, the header form linking to the setting; `parameters-and-tokens.md` `-- param:`: `->`, the shorthand, the first-use case on PostgreSQL, and that `->` needs no describe (and the bare-marker hash change: a marker that gives no type and no nullability changes nothing to describe); `sql-files.md` marker index: `-- column:` yes in the file header; `recipes.md` "Give an id a type of its own" leads with `SqlSourceColumnTypes` and `-- param: @id -> CustomerId`, the domain kept second; `diagnostics.md` sections of `SQLSRC014`, `SQLSRC111`, `SQLSRC112`, `SQLSRC116` (and its per-place table of markers, where `-- column:` is now yes in the file header), `SQLSRC250`-`SQLSRC252`, `SQLSRC260`; `docs/manual/README.md` "What works today" row; `README.md` "Custom type mapping" names column types and short names.  Run the internals grep of `docs/manual/AGENTS.md` over each changed page.
- [ ] **Step 3: `docs/architecture/generator.md`:** the column types in the settings step and in typing, the short-name index and its cache.
- [ ] **Step 4: the epic outline:** row 8.7 Done; rewrite the Decided item "A per-column C# type override ... is not in this epic" and "A C# type is fully qualified or a keyword" to this design.
- [ ] **Step 5: Validate, commit** `Document column types and short C# names`.

---

## Shared with 8.5 and 8.6

8.5 edits the realistic corpora's tests only; nothing here touches them.  8.6 edits `examples/*/CodeGen`, the manual and the epic outline, all of which Tasks 9 and 10 edit: whichever merges second resolves the text.  The implementation manager has been told and asked to order 8.7 before 8.6.  No shared generator file with either.
