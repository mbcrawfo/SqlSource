# TD-0109 generator, settings and diagnostics tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve items 9, 10, 13, 21–30, 42, 45, 47, 49, 50, 52 and 53 of TD-0109: one shared helper where the generator, settings and diagnostics tests copy one, and the missing case or assertion where a test's comment says more than it checks.

**Architecture:** Test-only.  Helpers that `tests/SqlSource.Tests/Generator/` needs go in `tests/SqlSource.Testing/Generator/` (RoslynFloor compiles that folder again on Roslyn 4.14, C# 12); helpers for the rest of `tests/SqlSource.Tests` stay in that project; a helper the tool's tests share goes in `tests/SqlSource.Testing`.  No test loses what it checks.

**Tech Stack:** C#, xunit, Shouldly, Roslyn incremental generator test harness.

**Spec:** `docs/tech-debt/TD-0109-duplicated-arrangements-and-uneven-conventions-in-tests.md` (items above), with `tests/AGENTS.md`, `tests/SqlSource.Tests/Generator/AGENTS.md` and `tests/README.md`.

## Global Constraints

- Test-only: nothing under `src/` changes.  No test loses an assertion; a replaced pair asserts at least what it did.
- `tests/SqlSource.Tests/Generator/**` must compile on Roslyn 4.14.0 and C# 12 (RoslynFloor); helpers it calls live in `tests/SqlSource.Testing/Generator/`.
- `tests/SqlSource.Testing` references nothing of the tool; its types stay `internal`.
- Every test class is `sealed`; assertions are Shouldly's; names are `Method_Scenario_Outcome`.  Rename a test only when this change rewrites it (items 19 and 54 belong to another unit).
- A new case that fails because the product is wrong: do not fix the product; report it to the controller with the failing case, and narrow the test meanwhile.
- Run `./format.sh` before committing; `./pre-commit-validation.sh` before every commit.

## Where the code differs from the spec

- Item 9: the 38 sites are as listed.  Two (`GeneratedSourceWarningTests` :76, :130) never assert the diagnostics: use `ShouldBeClean()` only if their diagnostics are empty, else `Problems().ShouldBe([...])` with what they are.
- Item 10: the Dialect/SettingsFile pair and the probe pair are one helper, `GeneratorHarness.RunSample`, in place of the spec's `RunWithReferences` (one name for both shapes).  `ListPaddingParameterTests`/`ArrayPoolParameterTests` also share `Method(GeneratorRun)`, which moves with `ListUsers`.
- Item 42: there are now five `ForParseError_` id facts (SQLSRC127 was added); `SqlSource.Tool.Testing/Failures.cs` `MessageOf` is a fourth copy of the format and delegates too.
- Item 45: 11 fields of `("/app/sqlsource.json", 1)` in the two folders, plus three more outside them (`Diagnostics/SettingsFileDiagnosticsTests`, `Generation/Types/FileTypesInputTests`, `Generation/Types/MappedTypes/MappedTypeNamesTests`) that take the same helper; inline uses stay.
- Item 21: adding NodaTime flips the typing probe's `NodaTime` flag, so `FileTypesInput` and `TypedFile` run again (Modified); what stays is the files' types, `FileTypes` (Unchanged).  The test asserts that, which is what it can truthfully check.

## Review Focus

1. A helper that swaps a local `Run` under `using static ModelRuns`: a bare `Run(...)` left behind binds silently to the unchecked `ModelRuns.Run`.  Every call is renamed (Task 1).
2. `Problems()` returns the diagnostics: a site that asserted diagnostics before the pair must assert them on `Problems()`'s result, not drop them (Task 2).
3. RoslynFloor: any new API used by `Generator/` must exist on Roslyn 4.14 (Tasks 1, 2, 5); build `tests/SqlSource.Tests.RoslynFloor` too.
4. Theory rows of internal enums: public theories take strings (`Enum.Parse`) or `SettingLevel` (Tasks 3, 6).
5. The schema walk (item 30) must find every `anyOf` at any depth, so a later one nested deeper still fails the test.

---

### Task 1: Shared run helpers of the generator tests (item 10)

**Files:**
- Modify: `tests/SqlSource.Testing/Generator/GeneratorHarness.cs`, `ModelRuns.cs`, `MethodRuns.cs`
- Modify: `tests/SqlSource.Tests/Generator/ModelNameTests.cs`, `ColumnMarkerTests.cs`, `NpgsqlProbeTests.cs`, `SqlClientProbeTests.cs`, `ListPaddingParameterTests.cs`, `ArrayPoolParameterTests.cs`, `DialectTests.cs`, `SettingsFileTests.cs`, `TokenValidationTests.cs`, `GeneratorHarnessTests.cs`, `GeneratorParameterLevelTests.cs`

**Interfaces (produces):**
- `GeneratorHarness.SampleSourceFile` — `public static SourceFile SampleSourceFile => new(SourcePath, SampleSource(null));`
- `GeneratorHarness.RunSample(OutputKind? output, SqlFile[] sqlFiles, MetadataReference[]? references = null, string? dialect = null, string? generatorParameters = null)` → `Run(output, [SampleSourceFile], sqlFiles, references: …, generatorParameters: …, dialect: …)`.
- `ModelRuns.CleanRun(SqlFile file, params SidecarEntry[] entries)` → `Run(file, entries)`, asserts `Problems()` (no compile error, no generated-code warning), returns the run.  Comment: every run compiles, with no warning in the generated code, whatever its diagnostics.
- `MethodRuns.ListUsers(SqlFile[] files, string? generatorParameters, bool hasRows = true)` — the body of `ArrayPoolParameterTests.Run` (CodeGen, `MethodRuns.Source`, sidecar entry `ListUsers` with columns `id` and nullable `name` text, or no columns when `!hasRows`, Npgsql 10, postgres).
- `MethodRuns.SingleMethod(GeneratorRun run)` — `StepOutputs.Single<TypeMethods>(run, TrackingNames.TypeMethods).Methods.ShouldHaveSingleItem()` (return the element type the two copies return today).

- [ ] **Step 1:** Add the helpers above.
- [ ] **Step 2:** `ModelNameTests`, `ColumnMarkerTests`: delete the private `Run`; rename every call to `CleanRun` (24 and 14 calls; both files have `using static ModelRuns`, so a missed rename compiles against the unchecked `Run` — grep for `\bRun(` afterwards).
- [ ] **Step 3:** Probe tests: delete both private `Run` overloads and the private `Source`; calls become `GeneratorHarness.RunSample(output, files, references: refs, dialect: "postgres"|"mssql")` with the class's dialect where none was passed.  Keep a one-line private `Run(OutputKind, MetadataReference[], params SqlFile[])` per class only if it is the class's dialect default and nothing else; prefer none.
- [ ] **Step 4:** `ListPaddingParameterTests`, `ArrayPoolParameterTests`: delete `Run`, `Entries`, `Method`; call `MethodRuns.ListUsers` / `MethodRuns.SingleMethod`.
- [ ] **Step 5:** `DialectTests`, `SettingsFileTests`: delete the private `Run`s; call `GeneratorHarness.RunSample(OutputKind.Sql, [..files], dialect: …, generatorParameters: …)`.
- [ ] **Step 6:** Replace every remaining `new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))` with `GeneratorHarness.SampleSourceFile` (`grep -rn "SampleSource(null))" tests/` gives none of that form afterwards).
- [ ] **Step 7:** Run `dotnet test tests/SqlSource.Tests` filtered to the touched classes, and build `tests/SqlSource.Tests.RoslynFloor`; same pass count as before.
- [ ] **Step 8:** `./format.sh`, `./pre-commit-validation.sh`, commit `test: shared run helpers of the generator tests (TD-0109 item 10)`.

### Task 2: One clean-run check (item 9)

**Files:** the 13 classes listed in item 9 (12 in `tests/SqlSource.Tests/Generator/`, and `tests/SqlSource.Tests/Generation/Converters/ConverterReaderTests.cs`), less the two wrappers Task 1 removed.

**Interfaces (consumes):** `GeneratorRun.ShouldBeClean()`, `GeneratorRun.Problems()`.

- [ ] **Step 1:** At each site where diagnostics are asserted before or after the pair, fold them: `run.Diagnostics.ShouldBe(X); run.CompilationErrors…; run.GeneratedCodeWarnings…;` → `run.Problems().ShouldBe(X);` (likewise `ShouldHaveSingleItem()`, and `SharedModelTests.AssertUnusable`).  Keep a `SeverityOf` line where it was.  `SettingLevelTests:115`: `run.Problems().ShouldBe(markedDiagnostics)` (leave `plain` as it is).  `SqlClientProbeTests` (`older`): `older.Problems().Where(…SQLSRC270…).ShouldBe([Expected])`.
- [ ] **Step 2:** `GeneratedSourceWarningTests` :76, :130: run once and read the diagnostics; `ShouldBeClean()` if empty, else `Problems().ShouldBe([...])`.
- [ ] **Step 3:** `grep -rn "GeneratedCodeWarnings.ShouldBeEmpty" tests/` → only `GeneratorHarness.cs`.
- [ ] **Step 4:** Run the touched classes and build RoslynFloor; `./format.sh`, `./pre-commit-validation.sh`, commit `test: generator tests check a clean run through GeneratorRun (TD-0109 item 9)`.

### Task 3: Diagnostic messages and options fakes (items 13, 42)

**Files:**
- Create: `tests/SqlSource.Testing/DiagnosticMessages.cs`; `tests/SqlSource.Testing/Generator/TestAnalyzerConfigOptions.cs`
- Modify: `tests/SqlSource.Testing/Generator/TestOptionsProvider.cs`; `tests/SqlSource.Tests/Generation/Types/MappedTypes/TransitivePackageAssembliesTests.cs`; `tests/SqlSource.Tests/Generation/Chains/BuildFoldersTests.cs`; `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, `SqlDiagnosticsTests.cs`; `tests/SqlSource.Tool.Tests/Reported.cs`; `tests/SqlSource.Tool.Testing/Failures.cs`

**Interfaces (produces):**
- `internal static class DiagnosticMessages` (namespace `SqlSource.Testing`): `public static string Format(DiagnosticDescriptor descriptor, params object?[] arguments)` — `string.Format(CultureInfo.InvariantCulture, descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture), arguments)`.
- `internal sealed class TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string?> values) : AnalyzerConfigOptions` — `TryGetValue` true only for a present, non-null value; keys as given (no prefix).

- [ ] **Step 1 (item 13):** Add `TestAnalyzerConfigOptions`; `TestOptionsProvider` uses it for its global and per-file options (it still prefixes `build_property.`); the two tests' private `Options` classes go, and they build `new TestAnalyzerConfigOptions(...)` with their full keys.
- [ ] **Step 2 (item 42):** Add `DiagnosticMessages.Format`.  `ToolDiagnosticsTests`: every hand-written `string.Format(CultureInfo.InvariantCulture, X.MessageFormat.ToString(...), …)` (41) becomes `Format(X, …)` (`using static SqlSource.Testing.DiagnosticMessages;`).  `SqlDiagnosticsTests`: delete its private `Format`.  `Reported.MessageOf` and `Failures.MessageOf` call `DiagnosticMessages.Format(…, [.. arguments])`.
- [ ] **Step 3 (item 42):** The help-link check and the id-shape check become theories over both classes' `All`, in `ToolDiagnosticsTests` beside `All_Ids_AreUniqueAcrossBothClasses`, with rows `"SqlDiagnostics"` and `"ToolDiagnostics"` and a private `AllOf(string name)`: `All_HelpLinks_PointAtTheDescriptorsSectionOfTheDiagnosticsDocument(string descriptors)` and `All_Ids_AreUniqueInOrderAndOfTheFormSqlsrcAndThreeDigits(string descriptors)` (same assertions as today).  Delete the copies; `ToolDiagnosticsTests.All_Ids_…From200` keeps only the ≥200 check, renamed `All_Ids_AreFrom200`.
- [ ] **Step 4 (item 42):** The five `ForParseError_<Kind>_IsSqlsrcNnn` facts become one theory `ForParseError_Kind_IsItsId(string kind, string id)` with rows (`InputModelWithoutParameters`, `SQLSRC119`), (`TokenCrossesComment`, `SQLSRC120`), (`ParameterInTwoSpellings`, `SQLSRC121`), (`ListParameterMisused`, `SQLSRC126`), (`NegatedListRefusedEmpty`, `SQLSRC127`), `Enum.Parse<SqlParseErrorKind>(kind)`.  The `_Message_` facts stay.
- [ ] **Step 5:** Run `tests/SqlSource.Tests` `Diagnostics` and the two option tests, and `tests/SqlSource.Tool.Tests` classes using `Reported`/`Failures`; same or more tests pass (count rows).
- [ ] **Step 6:** `./format.sh`, `./pre-commit-validation.sh`, commit `test: one diagnostic message format and one options fake (TD-0109 items 13, 42)`.

### Task 4: Settings tests' shared places and examples (items 45, 47, 50, 52)

**Files:**
- Create: `tests/SqlSource.Tests/Settings/TestPlaces.cs`; `tests/SqlSource.Tests/SettingsFiles/SettingsFileExamples.cs`
- Modify: the 11 + 3 classes of item 45 (and the three outside fields), `SettingsFiles/SettingsFileReaderTests.cs`, `SettingsFileReaderRobustnessTests.cs`, `SettingsFileSchemaTests.cs`, `Settings/TypeOverrides/TypeOverrideGrammarTests.cs`, `Settings/QuerySettingsTests.cs`

**Interfaces (produces):**
- `internal static class TestPlaces` (namespace `SqlSource.Tests.Settings`): `SettingsFile = SettingPlace.OfSettingsFile("/app/sqlsource.json", 1)`, `RootSettingsFile = SettingPlace.OfSettingsFile("/sqlsource.json", 0)`; comment: the project's settings file of the tests and the root's above it.
- `internal static class SettingsFileExamples` (namespace `SqlSource.Tests.SettingsFiles`): `public const string Design` — the text of `DesignsExample`, with its doc comment.

- [ ] **Step 1 (45):** Replace each private field equal to one of the two places (`Near`, `Far`, `Place`, `SettingsFile`, `Farther`) with `TestPlaces.SettingsFile` / `TestPlaces.RootSettingsFile` (a `using static` is fine).  `LibrarySettingsTests.Near` (`/app/Queries/…`, 2) stays.  Inline literals stay.
- [ ] **Step 2 (47):** Move `DesignsExample` to `SettingsFileExamples.Design`; update its five readers.
- [ ] **Step 3 (50):** The three facts (`QuotedKeyWithSpaces…`, `EscapedKey_IsItsQuotedForm`, `EscapedKey_IsItsWrittenForm`) become `InlineData` rows of `TryReadEntry_ValidEntry_GivesItsKeyAndTheCanonicalText` (keys and types as the facts give them); `TryReadEntry_SpacesAroundTheEquals_AreAllowed` keeps its rows and calls `Read(text).ShouldBe(new TypeOverride("numeric", type, SettingPlace.Query))`.
- [ ] **Step 4 (52):** Delete `QuerySettingsTests.Turn`; its six calls become `TestModels.ParametersOn(p)` / `TestModels.ParametersOff(p)` (`using SqlSource.Tests.Generation;`).
- [ ] **Step 5:** Run `tests/SqlSource.Tests` filtered to `SqlSource.Tests.Settings*` and `SqlSource.Tests.SettingsFiles*` and the three outside classes; test count unchanged (item 50 moves facts into rows).
- [ ] **Step 6:** `./format.sh`, `./pre-commit-validation.sh`, commit `test: shared settings places, the design example and theory rows (TD-0109 items 45, 47, 50, 52)`.

### Task 5: Build files and the attribute's name (items 49, 53)

**Files:** `tests/SqlSource.Tests/Package/BuildFileTests.cs`, `BuildFileTests.Online.cs`; `tests/SqlSource.Tests/EndToEnd/EndToEndTests.cs`; `tests/DatabaseTests/Postgres/EndToEnd/ModelShapeTests.cs`; `tests/DatabaseTests/SqlServer/EndToEnd/ModelShapeTests.cs`

- [ ] **Step 1 (49):** Keep `AttributesOf`, delete `Attributes`, its 7 callers call `AttributesOf`.  `Load(string folder, string file)`; `Props`/`Targets`/`AfterSdk` pass `"build"`, `Outer` is `Load("buildMultiTargeting", "SqlSource.targets")`.  Rename `BuildFile_EveryPropertyOfThePackage_StartsWithSqlSource` to `BuildFiles_EveryPropertyOfThePackage_StartsWithSqlSource`.
- [ ] **Step 2 (53):** The three literals `"SqlSource.SqlSourceGenerateAttribute"` become `AttributeSource.AttributeMetadataName` (`using SqlSource.Generation;` where missing).
- [ ] **Step 3:** Run `BuildFileTests` and `EndToEndTests`; build both container projects (`dotnet build tests/DatabaseTests/Postgres tests/DatabaseTests/SqlServer`) and run their `ModelShapeTests` (Docker).
- [ ] **Step 4:** `./format.sh`, `./pre-commit-validation.sh`, commit `test: one attribute reader and loader of the build files, and the attribute's name from the generator (TD-0109 items 49, 53)`.

### Task 6: Generator tests' missing cases (items 21–27)

**Files:** in `tests/SqlSource.Tests/Generator/`: `CachingConverterTests.cs`, `CachingSettingsFileTests.cs`, `LibraryConversionTests.cs`, `MethodTests.cs`, `ParameterTypeTests.cs`, `SqlClientProbeTests.cs`, `TypeOverrideLevelTests.cs`.  Each test's comment is rewritten to say what it now checks.

- [ ] **Step 1 (21):** `Run_NodaTimeReferenceAdded_RunsLibraryConvertersAndConverters`: add `AllReasons(result, TrackingNames.FileTypesInput).ShouldAllBe(Modified)`, `AllReasons(result, TrackingNames.TypedFile).ShouldAllBe(Modified)` and `AllReasons(result, TrackingNames.FileTypes).ShouldAllBe(Unchanged)`; the comment says the files are typed again with the new probe and give the same types.  If `FileTypes` is not Unchanged, stop and report.
- [ ] **Step 2 (22):** `Run_BuildFolderChanged_ReachesTheSameSettingsFiles`: add `OutputReasons(result).ShouldAllBeCached();` (nothing is reported or emitted again).
- [ ] **Step 3 (23):** Rename to `Run_OverrideWithTheSwitchOnOrOff_GetsTheConversion(string databaseType, string type, string read, string write, bool switchOn)`; rows: each of the three with `false` and `true`; with `true` the text starts with `NodaTimeOn`.  Same assertions.
- [ ] **Step 4 (24):** `Run_MixedType_HasConstantsMethodsAndModels`: add `methods.ShouldStartWith(GeneratedFile.Header + "\nnamespace App\n{\n    partial class Sample\n    {\n")` and `methods.ShouldContain("        public static async global::System.Threading.Tasks.Task<…> GetUser(\n")` with the return type this query generates (copy from `Run_EachTypePlacement_PutsTheMethodOnTheType`).
- [ ] **Step 5 (25):** `Run_ParamMarker_WinsOverTheTypeOverride(SettingLevel level)`, `[MemberData]` over the four levels: `int8 = long` placed by `LevelSettings.At(level, Setting.TypeOverrides, …)`, the `-- param: @author -> UserId` marker in the query's text; same two assertions.
- [ ] **Step 6 (26):** `Run_VectorColumnBelowSqlClient61_IsSqlsrc270AndCompiles`: add `ModelRuns.ModelFiles(older).ShouldBeEmpty()` and `ModelRuns.ModelFiles(current).ShouldBe([ModelRuns.GetUserDtoFile])` (read the run if it also writes a Params file, and assert what it writes); rename to `…_IsSqlsrc270AndWritesNoModel` only if the name would otherwise mislead.
- [ ] **Step 7 (27):** `Run_ArrayKeySpelledTwoWaysAtTwoLevels_IsOneKey(string projectKey, string marker, string expected)`: rows keep today's two (`_int4` under `int4[]`) and add (`integer[]`, `-- type-override: _int4 = System.Collections.Generic.List<long>\n`, `global::System.Collections.Generic.List<long>`), (`int4[]`, `-- type-override: int[] = default\n`, `int[]`), (`pg_catalog._int4`, `-- type-override: INTEGER[] = System.Collections.Generic.List<long>\n`, same list).
- [ ] **Step 8:** Run the seven classes and build RoslynFloor; `./format.sh`, `./pre-commit-validation.sh`, commit `test: generator tests check what their comments say (TD-0109 items 21-27)`.

### Task 7: Parsing and schema tests' missing cases (items 28–30)

**Files:** `tests/SqlSource.Tests/Parsing/Quoting/QuoteReaderTests.cs`, `tests/SqlSource.Tests/Parsing/Markers/SqlMarkerScopeTests.cs`, `tests/SqlSource.Tests/SettingsFiles/SettingsFileSchemaTests.cs`

- [ ] **Step 1 (28):** Add `[InlineData('①')]` to `IsNameCharacterOfSqlServer_Character_IsNotAPartOfAName`; the comment names the circled digit.
- [ ] **Step 2 (29):** `Read_PlacementMarkers_AreTheSpansOfTheFirstMarkerThatSetThem` and `Read_ScopeWithoutPlacementMarkers_HasNoSpan` become theories over `"Preamble"`, `"Query"`, `"WholeFile"` (`Enum.Parse<SqlScopeKind>`), same assertions.
- [ ] **Step 3 (30):** Replace `Schema_AnyOf_IsOnTheChoicesTheDialectAndTheParametersAlone` with `Schema_EveryAnyOf_IsAChoiceListAndAPatternButAColumnTypesValue`: walk the whole schema recursively (every object member and array element), collect each `anyOf`'s JSON path; assert the paths are exactly the ten choice places (the eight top-level properties and `generatorParameters/items` as today's `ChoiceKeys` plus dialect give them, `libraryTypes/propertyNames`, `libraryTypes/additionalProperties`) and `columnTypes/additionalProperties`; each has two branches; for all but `columnTypes/additionalProperties`, branch 0 has `enum` and branch 1 has `pattern`.  The comment says the column type's string-or-object form is checked by `Schema_ColumnTypes_TakeTheObjectFormTheReaderReads`.
- [ ] **Step 4:** Run the three classes; `./format.sh`, `./pre-commit-validation.sh`, commit `test: parsing and schema tests check what their comments say (TD-0109 items 28-30)`.

### Task 8 (controller): Docs and the item file

- [ ] `tests/README.md`: name `DiagnosticMessages` (SqlSource.Testing) and `TestAnalyzerConfigOptions` beside `TestOptionsProvider`; `RunSample`/`SampleSourceFile` with `SampleSource`; `ModelRuns.CleanRun`, `MethodRuns.ListUsers`.  `tests/SqlSource.Tests/Generator/AGENTS.md` harness list: add `TestAnalyzerConfigOptions`.
- [ ] TD-0109: strike items 9, 10, 13, 21–30, 42, 45, 47, 49, 50, 52, 53 with "Fixed: …" and their proposed fixes with "Done."; update the README row's summary if it names counts that changed.
- [ ] `./format.sh`, `./pre-commit-validation.sh --full`, commit.
