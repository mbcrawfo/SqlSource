# Test cleanup phase 67: tests/SqlSource.Tests/Generator, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generator`
- Findings: 10 (2 delete, 5 fold, 3 loosen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 97
- Guard: mutation (generator-root), with covers and run time for 5 findings

## Files

- `tests/SqlSource.Tests/Generator/MethodTests.cs`
- `tests/SqlSource.Tests/Generator/PlacementTests.cs`
- `tests/SqlSource.Tests/Generator/QueryTypesTests.cs`
- `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generator-6-01

- test: `tests/SqlSource.Tests/Generator/PlacementTests.cs` | `PlacementTests.Run_MethodsQueryWithNoLocation_IsAnInternalMethodOfTheExtensionClass` | rows: all
- action: delete
- evidence: read (PlacementTests.cs:65-76 runs GetUser at methods with no location.  It asserts a clean run, `internal static partial class SampleExtensions` and the internal method with `this global::System.Data.Common.DbConnection connection`.  MethodTests.cs:73 runs the same unmarked query at methods and asserts that whole file: the internal extension class, the internal method and its `this DbConnection` parameter.  SettingLevelTests.cs:219 (row `""`) also asserts the internal extension class and internal method for a file with no location.  There is no comment above the test or its group that makes it a record, and the doc search found nothing.  Measured: kills 58, alone 0, timeouts under it 12, all in root files; its subject is outside the scope); blind: no
- cover: `tests/SqlSource.Tests/Generator/MethodTests.cs` | `MethodTests.Run_MethodsQuery_IsAnExtensionMethodOfTheTypesExtensionClass`; `tests/SqlSource.Tests/Generator/SettingLevelTests.cs` | `SettingLevelTests.Run_MemberLocationInTheHeader_PlacesTheMethod` (row `""`, line 219)
- proposal: Delete the test.  The default placement of a methods query is asserted whole by MethodTests and by the row with no header of the header-location theory.
- verdict: confirmed
- verifier's reason: PlacementTests.cs:64-76 has no comment and runs `MethodRuns.Run(Users, ...)` with no marker; it asserts a clean run, `internal static partial class SampleExtensions` and the internal method with `this DbConnection connection`.  MethodTests.cs:73-112 runs the same unmarked GetUser through `MethodRuns.Run` on the same `MethodRuns.Source` and asserts the whole file with `ShouldBe`: the internal extension class (:88), the internal method (:106) and `this global::System.Data.Common.DbConnection connection` (:107).  SettingLevelTests.cs:219 (row `""`) asserts the extension class and `internal static async` for a header with no location.  `kills` gives 58 kills, 0 alone, 12 timeouts, all under MethodTests' and SettingLevelTests' theory.

### F-SqlSource.Tests.Generator-6-03

- test: `tests/SqlSource.Tests/Generator/QueryTypesTests.cs` | `QueryTypesTests.Run_ColumnOfTheBaseMap_HasNoBaseMapTypeOfItsOwn` | rows: all
- action: delete
- evidence: read (QueryTypesTests.cs:696-705 runs one `int4` column under Npgsql 10 through the class's `Run` helper and asserts that the column's `Database` is null.  QueryTypesTests.cs:49 (row `"10"`) runs the same input, an `int4` column under Npgsql 10 through the same helper.  At :61-64 it asserts that the whole `QueryTypes` equals `Int`, which is `new PropertyType("int", true, true, NoWarnings, "int4", null)` with `Database` left at its default, null.  `PropertyType` is a sealed record (src/SqlSource/Generation/Types/PropertyType.cs:58), so that equality includes `Database`.  There is no comment on the test, and the doc search found nothing.  Measured: kills 35, alone 0, timeouts under it 12); blind: no
- cover: `tests/SqlSource.Tests/Generator/QueryTypesTests.cs` | `QueryTypesTests.Run_Int4_IsIntUnderEachDriver` (row `"10"`, line 49)
- proposal: Delete the test.  The `int4` row under Npgsql 10 already proves that a column the base map types carries no base-map type.
- verdict: confirmed
- verifier's reason: QueryTypesTests.cs:695-705 has no comment.  It runs one `int4` column through the class's `Run` helper (:717-718) on `[NpgsqlReferences.Npgsql10]` and asserts only `Database` is null.  QueryTypesTests.cs:46-65, row `"10"` (:49), runs `int4` through the same helper with `Driver("10")` = `[NpgsqlReferences.Npgsql10]` (:713) and asserts the whole `QueryTypes` equals `TestSidecars.Of(Int)` for parameters and columns.  `Int` (:44) leaves `Database` at its default, null, and `PropertyType` is a sealed positional record (src/SqlSource/Generation/Types/PropertyType.cs) whose generated equality includes `Database`.  `kills` gives 35 kills, 0 alone, 12 timeouts, all under Run_Int4_IsIntUnderEachDriver.

### F-SqlSource.Tests.Generator-5-02

- test: `tests/SqlSource.Tests/Generator/MethodTests.cs` | `MethodTests.Run_MethodOnTheType_TakesThePlaceOfTheConstant`; `MethodTests.Run_EachTypePlacement_PutsTheMethodOnTheType` | rows: all
- action: fold
- evidence: read (`Run_MethodOnTheType_TakesThePlaceOfTheConstant`, MethodTests.cs:193-207, runs `-- member-location: class` and `-- visibility: public`, the input of the theory's `"Public"` row at :173 and :178, and asserts the same `public static async ... GetUser(` line that row asserts at :184-189; its one assertion of its own is that the type's file has no `GetUser`, :202.  No `AGENTS.md`, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names either test; the comment above the fact says the method takes the constant's place, which is that one assertion.  Measured: kills 70 and 66, alone 0 each, timeouts under each 12, all with the same input, so the timeouts stay reached; the subject, method placement, is outside the root scope); blind: no
- cover: `tests/SqlSource.Tests/Generator/MethodTests.cs` | `MethodTests.Run_EachTypePlacement_PutsTheMethodOnTheType`
- proposal: Add `run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("GetUser")` to the theory, with the fact's comment, and remove the fact: each placement then also proves that the method takes the place of the constant.
- verdict: confirmed
- verifier's reason: The fact at MethodTests.cs:193-207 runs `-- member-location: class` and `-- visibility: public`. The theory's `"Public"` row (:173, :178) runs the same input with the value capitalised. The theory accepts that spelling and gives `public static` (:184-189). Lowercase `public` on the type is still run by `Run_MethodNamedLikeTheNestedClassOfNoQuery_Compiles` (:214) and `Run_MethodNamedLikeTheNestedClassBesideAFileLeftOut_Compiles` (:229), so the case makes no difference. The fact's one check the theory lacks is `run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("GetUser")` (:202). In generator-root, the fact kills four mutants the theory does not: `SqlSourceGenerator.cs:65:9`, `:837:9`, `:843:21` and `:845:21`. These are the registration and `AddSource` of the type's file (`RegisterTypeSources`, :826-848), and the fact kills them because it reads `Sources[SampleFile]`. The proposal adds exactly that read to every row, so the fold keeps those kills. Fact and theory reach the same 12 timeouts (identical TIMEOUT lists). The comment above the fact (:193) states the very check that moves into the theory. A grep of every AGENTS.md, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` finds neither test name. (TD-0089 names a different `MethodTests` theory.)

### F-SqlSource.Tests.Generator-6-04

- test: `tests/SqlSource.Tests/Generator/QueryTypesTests.cs` | `QueryTypesTests.Run_TypeNoNpgsqlReads_IsSqlsrc246WithTheCastToText` | rows: `("pg_snapshot", "pg_snapshot", "text")` line 157, `("txid_snapshot", "txid_snapshot", "text")` line 158, `("pg_node_tree", "pg_node_tree", "text")` line 159
- action: fold
- evidence: read (The five rows differ only in which type the map sends to its cast-to-text failure.  The diagnostic's wording for that failure, with the type's name and `text` or `text[]`, is the same for each.  Which types are cast to text is the map's rule.  PostgresTypeMapTests.cs:238-240 holds `pg_snapshot`, `txid_snapshot` and `pg_node_tree` as `!CastToText` under every driver, and :374-384 checks the cast of `pg_snapshot` and `pg_node_tree` and of their arrays.  The rows `aclitem` (:160) and `aclitem[]` (:161) keep the wording of both ranks and the case the comment at :155 names, a base type of pg_catalog that SqlSource does not know.  The comment at :154-155 records no decision about the three other types, and the doc search found nothing.  Measured: 5 rows, rows needed 1, kills 45, alone 0, timeouts under it 12, all in root files); blind: no
- cover: `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs` | `PostgresTypeMapTests.Map_EveryRowOfTheTable_GivesItsType`; `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs` | `PostgresTypeMapTests.Map_TypeReadByACastToText_NamesTheCastOfItsRank`; `tests/SqlSource.Tests/Generator/QueryTypesTests.cs` | `QueryTypesTests.Run_TypeNoNpgsqlReads_IsSqlsrc246WithTheCastToText` (rows `aclitem`, `aclitem[]`)
- proposal: Drop the rows `pg_snapshot`, `txid_snapshot` and `pg_node_tree`, and keep the two `aclitem` rows, which give the diagnostic's text at each rank.
- verdict: confirmed
- verifier's reason: The three rows `pg_snapshot`, `txid_snapshot` and `pg_node_tree` (QueryTypesTests.cs:157-159) take the map's row path: `Rows.TryGetValue` (src/SqlSource/Generation/Types/Maps/PostgresTypeMap.cs:76-79) on one shared `Row.Refused(TypeMappingKind.CastToText, TextType)` for all three names (:245).  `aclitem` takes the `PostgresTypeKind.Base when IsSystemType` path (:100-104), which also gives `CastToText` with `text`.  `TypeDiagnostics.Diagnose` (src/SqlSource/Generation/Types/TypeDiagnostics.cs:50-56) builds the message from the kind and `Detail` alone, so every row gives the same text but for the name.  PostgresTypeMapTests.cs:238-240 holds the three as `!CastToText` under each driver through `ShouldMapTo` (:785-792), and :373-384 checks `Detail` `text` and `text[]` for `pg_snapshot` and `pg_node_tree`.  The comment at :154-155 records only the aclitem case, which the two kept rows hold.  site/postgresql.md:220 and site/diagnostics.md:2633 document the types for users but name no test.  It is a fold of rows, which a filter cannot measure; the phase's own guard proves it.

### F-SqlSource.Tests.Generator-6-05

- test: `tests/SqlSource.Tests/Generator/QueryTypesTests.cs` | `QueryTypesTests.Run_TypeTheToolNeverWrites_IsSqlsrc246WithDescribe` | rows: `("void", "base")` line 234
- action: fold
- evidence: read (The rows `record` (:233) and `void` (:234) are both of kind `base` and both reach the map's not-written-by-the-tool failure, so they give the same diagnostic but for the name.  The map's table has `record`, `unknown` and `void` as `!NotWrittenByTheTool` (PostgresTypeMapTests.cs:241-243).  The row `users` of kind `table` (:235) is the other path, a kind this version does not know.  The comment at :230-231 records no decision about void, and the doc search found nothing.  Measured: 3 rows, rows needed 1, kills 45, alone 0, timeouts under it 12); blind: no
- cover: `tests/SqlSource.Tests/Generator/QueryTypesTests.cs` | `QueryTypesTests.Run_TypeTheToolNeverWrites_IsSqlsrc246WithDescribe` (row `("record", "base")`, line 233); `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs` | `PostgresTypeMapTests.Map_EveryRowOfTheTable_GivesItsType`
- proposal: Drop the row `void`, and keep `record` for a base type and `users` for an unknown kind.
- verdict: confirmed
- verifier's reason: The rows `record` (QueryTypesTests.cs:233) and `void` (:234) both build a `pg_catalog` base type (`TestSidecars.Postgres` defaults to schema `pg_catalog`, tests/SqlSource.Testing/Snapshot/TestSidecars.cs:17).  So both reach the map's row path (PostgresTypeMap.cs:76-79) and its `!NotWrittenByTheTool` row, which PostgresTypeMapTests.cs:241-243 checks for `record`, `unknown` and `void` under each driver.  `TypeDiagnostics.cs:64-69` gives the same "Run 'sqlsource describe'" text for that kind whatever the type.  The row `users`/`table` (:235) keeps the other path, an unknown kind (PostgresTypeMap.cs:82-85).  The comment at :230-231 records nothing about `void` alone.  It is a fold of rows, confirmed by reading; the phase's own guard proves it.

### F-SqlSource.Tests.Generator-8-01

- test: `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs` | `TypeDiagnosticsTests.Run_ProjectOnALanguageVersionOlderThanCSharp12_IsAnErrorAtEachAttribute` | rows: `(LanguageVersion.CSharp8, "8.0")` at line 165 and `(LanguageVersion.CSharp9, "9.0")` at line 166
- action: fold
- evidence: measured (4 rows, kills 43, alone 0, timeouts under it 9, rows needed 1; the gate is in `src/SqlSource/Generation/Emitting/LanguageSupport.cs`, outside the generator-root scope, so the rows are unmeasured for it) and read (`LanguageSupport.cs:16-17` is one comparison, `LanguageVersion: < LanguageVersion.CSharp12`, and Roslyn's `ToDisplayString()` for the text; rows 8.0 and 9.0 are interior points of that comparison, between the lowest row 7.3 at line 164 and the boundary row 11.0 at line 167, and go through the same formatting).  No comment above the test or its class records these rows, and a search of the `AGENTS.md` files, readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the test's and the class's names found nothing (only old plans in `docs/superpowers/plans/`); blind: no
- cover: `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs` | `TypeDiagnosticsTests.Run_ProjectOnALanguageVersionOlderThanCSharp12_IsAnErrorAtEachAttribute` rows `(LanguageVersion.CSharp7_3, "7.3")` (line 164) and `(LanguageVersion.CSharp11, "11.0")` (line 167)
- proposal: Drop the 8.0 and 9.0 rows and keep 7.3, the oldest, and 11.0, the last version below the gate.
- verdict: confirmed
- verifier's reason: Read `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs:163-207`: all four rows run one source and assert the same two SQLSRC012 texts with the version string, the same three files and no compiler errors.  The gate is one comparison, `src/SqlSource/Generation/Emitting/LanguageSupport.cs:16-17` (`LanguageVersion: < LanguageVersion.CSharp12`, text by `ToDisplayString()`), and `PathResolver.Resolve` adds the diagnostic the same way for any version (`src/SqlSource/Generation/Paths/PathResolver.cs:36-46`); 8.0 and 9.0 are interior points between the kept rows 7.3 (line 164) and 11.0 (line 167).  The other things a row could guard are proved elsewhere: the formatting of 8.0 and 9.0 by `LanguageSupportTests.FindUnsupportedVersion_OlderThanCSharp12_IsTheVersionAsLangVersionWritesIt` rows at `tests/SqlSource.Tests/Generation/Emitting/LanguageSupportTests.cs:12-13`, and the attribute and exceptions compiling on C# 8 and 9 by `GeneratedSourceWarningTests.Run_ProjectOnAnOlderLanguageVersion_CompilesTheAttributeAndTheExceptions` rows at `tests/SqlSource.Tests/Generator/GeneratedSourceWarningTests.cs:56-57`.  `kills` on `mutation/generator-root/reports/mutation-report.json` gives 43 kills, all in the shared machinery any generator run reaches (`SortedSearch`, `SqlIdentifier`, the registrations of `SqlSourceGenerator`), each explainable as such; its 9 timeouts (SortedSearch.cs:66, 104; SqlSourceGenerator.cs:1093, 1103, 1199, 1210 twice, 1218, 1223) are the same generic sort and search lines and are run by the kept rows as well, since the rows share a name and one path.  The rows share one name, so a filter cannot measure the fold: confirmed by reading, and the phase's own guard proves it.  No comment above the test (line 163) or the class (lines 12-13) records the rows; a search of the `AGENTS.md` files, readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the test's and the class's names found only plans in `docs/superpowers/plans/`, and `site/diagnostics.md:466-479` describes SQLSRC012 without naming versions 8 or 9.  Pure unit test, no engine, no wording lost (the kept rows assert the same text).

### F-SqlSource.Tests.Generator-8-02

- test: `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs` | `TypeDiagnosticsTests.Run_NestedLocationAndAMemberNamedSql_IsAnErrorAtTheAttribute` | rows: `"private const int Sql = 1;"` at line 333 and `"public static void Sql() { }"` at line 334
- action: fold
- evidence: measured (4 rows, kills 45, alone 0, timeouts under it 9, rows needed 1; the names are gathered in `src/SqlSource/Generation/TargetTypeReader.cs` and searched in `src/SqlSource/Generation/Placement/TypePlacement.cs`, outside the generator-root scope) and read (`TargetTypeReader.GetTakenNames`, `TargetTypeReader.cs:150-159`, takes every member's name from `symbol.MemberNames` with no regard to its kind, and the nested types from `GetTypeMembers()`; a property, a constant and a method named `Sql` are all one entry of `MemberNames`, so the property row at line 332 proves what the constant and method rows do, and the nested class row at line 335 is the one that reaches the other source).  No comment above the test or its class records the rows, and the search of the `AGENTS.md` files, readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` found nothing; blind: no
- cover: `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs` | `TypeDiagnosticsTests.Run_NestedLocationAndAMemberNamedSql_IsAnErrorAtTheAttribute` row `"private string Sql { get; set; } = string.Empty;"` (line 332)
- proposal: Drop the constant and method rows and keep the property row, for a member, and the nested class row, for a nested type.
- verdict: confirmed
- verifier's reason: Read `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs:331-355`: the four rows differ only in the declaration and assert the same SQLSRC007 text, no `GetUser` and no compiler errors.  `TargetTypeReader.GetTakenNames` (`src/SqlSource/Generation/TargetTypeReader.cs:150-159`) takes `symbol.MemberNames`, which holds a property's, a constant's and a method's name alike as one string (its comment at lines 146-149 says it leaves out nested types), then `GetTypeMembers()` for the nested types; nothing downstream sees the member's kind, since only the names are kept.  So the constant row (line 333) and the method row (line 334) prove what the property row (line 332) does, and the nested class row (line 335) stays for `GetTypeMembers()`; type parameters and the type's own name are in the next theory (lines 357-381).  `kills` on the generator-root report gives 45 kills, all in shared machinery (`SortedSearch`, `SqlIdentifier`, registrations), and timeouts on the same generic lines as the other theories, which the kept rows run too; the rows share one name, so the fold is confirmed by reading, and the phase's own guard proves it.  No comment above the test or the class records the rows, the document search found only old plans, and the manual (`site/generator.md:113`, `site/settings.md:818`) speaks of "a member, a nested type or a type parameter", which the kept rows still prove.

### F-SqlSource.Tests.Generator-5-01

- test: `tests/SqlSource.Tests/Generator/MethodTests.cs` | `MethodTests.Run_MethodsQuery_IsAnExtensionMethodOfTheTypesExtensionClass` | rows: all
- action: loosen
- evidence: read (MethodTests.cs:72-170 holds the whole methods file with `ShouldBe`, every line of the method's body from the null checks to the `finally`, and then the whole row buffer against `GeneratedFile.RowBuffer` at :169; the name promises only that the query's method is an extension method of `SampleExtensions`.  The body is the golden corpus's to pin, `Golden/postgres/shapes.txt:150-160` holds the same extension class and its methods, and the row buffer's text is pinned by `Generation/Emitting/RowBufferTests.cs:95` and emitted as `RowBuffer.Source` by `Generator/RowBufferTests.cs:67`.  `docs/superpowers/plans/2026-10-26-pooled-row-buffer-phase-3-lists.md:50` and `docs/superpowers/plans/2026-10-31-roslyn-4-14-floor.md:421` each had to rewrite this test's pinned text for a change to the body and the buffer, not to the extension class.  Measured: kills 67, alone 0, timeouts under it 12, whose subject, `Generation/Emitting/`, is outside the root scope); blind: no
- cover: none
- proposal: Assert what the name promises, the summary line and `internal static partial class SampleExtensions`, the method's `internal static` modifiers and its `this global::System.Data.Common.DbConnection connection` parameter, and drop the whole-file `ShouldBe` and the row buffer's line, which the golden corpus and `RowBufferTests` already hold.  A change to the method's body or the buffer's text that keeps the extension class would no longer break it.
- verdict: confirmed
- verifier's reason: The finding reads the test correctly. MethodTests.cs:72-170 has no comment and compares the whole methods file with `ShouldBe`: the doc comments, the parameter binding, the reader loop and the `finally` block, and at :169 the row buffer's whole text. Its name promises only that the method is an extension method of `SampleExtensions`. The same extension class and `GetUser` method with parameters are in the golden corpus, `tests/SqlSource.Tests/Generator/Golden/postgres/shapes.txt:148-160` and :342-350, and the corpus is compiled again by RoslynFloor through `QueryMethodCorpusTests.cs` in the same folder. The row buffer's text is held by `Generation/Emitting/RowBufferTests.cs:95` (`RowBuffer.Source.ShouldBe(GeneratedFile.RowBuffer)`) and `Generator/RowBufferTests.cs:67` (`run.Sources[RowBuffer.HintName].ShouldBe(RowBuffer.Source)`). The two plans cited, `docs/superpowers/plans/2026-10-26-pooled-row-buffer-phase-3-lists.md:50` and `2026-10-31-roslyn-4-14-floor.md:421`, list this file among the pinned texts to rewrite; neither keeps the test as the record of anything. No AGENTS.md, readme, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names the test. In generator-root, `kills` gives 67 kills, 0 alone and 12 timeouts. All of them are in the pipeline (`SqlSourceGenerator.cs`, `SortedSearch.cs`), and hundreds of other tests share each one, so the kills fit any test that runs the whole generator. The loosened asserts keep what the name promises.
- a cover: `MethodTests.Run_MethodsQuery_IsAnExtensionMethodOfTheTypesExtensionClass` is the cover of F-SqlSource.Tests.Generator-6-01 (phase 67).  The change keeps every assertion and row that makes it so.

### F-SqlSource.Tests.Generator-6-02

- test: `tests/SqlSource.Tests/Generator/PlacementTests.cs` | `PlacementTests.Run_FileOfSeveralLocations_PutsEachQueryWhereItSays` | rows: all
- action: loosen
- evidence: read (PlacementTests.cs:432-492 asserts the type's whole generated file with `ShouldBe`, including each constant's `<summary>`/`<remarks>`/`<code>` documentation.  The rule, as the comment at :393-394 states it, is that each query goes to the type, `Sql`, `Users` or `Orders`, and that the classes come in the order of their first queries.  A change to the wording or layout of a constant's documentation breaks this test, though no query moves.  Measured: kills 44, alone 0, timeouts under it 10, all in root files); blind: no
- cover: none
- proposal: Assert that each constant is inside its class: `OnType` on the type, `GetUser` in `Users`, `InSql` in `Sql` and `GetOrder` in `Orders`.  Assert the order of the three class declarations by their indexes.  Leave the documentation's text to the tests of documentation.
- verdict: confirmed
- verifier's reason: PlacementTests.cs:431-492 asserts the type's whole file with `ShouldBe`, including each constant's `<summary>`, `<remarks>` and `<code>` lines.  The comment at :393-394 states the rule as where each query goes and the order of the classes.  The C# source at :408 already makes `ShouldBeClean` fail if a constant is missing from its class.  A change to the wording of a constant's documentation would break the test though nothing moves, and the shared brief makes the golden corpus the one place where a whole generated text is the assertion.  The finding reads the test rightly.

### F-SqlSource.Tests.Generator-8-03

- test: `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs` | `TypeDiagnosticsTests.Run_TypeWithSeveralProblems_ReportsThemAll` | rows: all
- action: loosen
- evidence: read (`TypeDiagnosticsTests.cs:439-441` asserts SQLSRC001 at index 0 and SQLSRC004 at index 1, an order that follows neither the source, since SQLSRC001 is at line 4 and SQLSRC004 at line 3, nor any stated rule: it is the order in which `PathResolver.Resolve` appends the type's own problems before its path problems, `src/SqlSource/Generation/Paths/PathResolver.cs:31` and `:54-55`.  A change that reported the path problem first would break the test while every problem is still reported, which is all its name promises; the next test, `Run_TypeWithAProblem_StillReportsTheErrorsOfItsSqlFiles` at lines 459-463, already asserts its two diagnostics without an order); blind: no
- cover: none
- proposal: Assert the two diagnostics as a set, by `ShouldContain` of each prefix beside the count of two, or `ShouldBe(..., ignoreOrder: true)` on the full texts.
- verdict: confirmed
- verifier's reason: Read `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs:425-443`: lines 440-441 pin SQLSRC001 at index 0 and SQLSRC004 at index 1.  The harness keeps the generator's report order unsorted (`tests/SqlSource.Testing/Generator/GeneratorHarness.cs:199`), and that order is `PathResolver.Resolve` adding `type.Diagnostics` first (`src/SqlSource/Generation/Paths/PathResolver.cs:31`) and the path problem after (`:50-55`), reported by `ReportAll` in array order (`src/SqlSource/SqlSourceGenerator.cs:1185-1191`).  No document states this order as behaviour: `docs/architecture/generator.md:224` says of the report orders it does describe that "which diagnostics are reported does not depend on it", and the test's name promises only that all are reported.  Swapping the two appends would break the test with both errors still reported.  The neighbour `Run_TypeWithAProblem_StillReportsTheErrorsOfItsSqlFiles` (lines 459-463) asserts its two by `ShouldContain`, the form the proposal asks for.  The finding reads the test rightly.
- condition: Wording corrected (run-notes, mirrors): the evidence says no rule states the order of SQLSRC001 then SQLSRC004, but `PathResolverTests.Resolve_TypeWithProblems_KeepsThemBeforeItsOwn` names that order at the unit level.  The loosen holds: this driver test's name promises only that every problem is reported, and the unit test keeps the order.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-root`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-67/generator-root/before --tests 'FullyQualifiedName~SqlSource.Tests.Generator.|FullyQualifiedName~SqlSource.Tests.DatabaseNameTests.|FullyQualifiedName~SqlSource.Tests.EquatableArrayTests.|FullyQualifiedName~SqlSource.Tests.PackageVersionTests.|FullyQualifiedName~SqlSource.Tests.SortedSearchTests.|FullyQualifiedName~SqlSource.Tests.SqlIdentifierTests.|FullyQualifiedName~SqlSource.Tests.SqlServerNamesTests.|FullyQualifiedName~SqlSource.Tests.SystemSchemasTests.|FullyQualifiedName~SqlSource.Tests.TextLinesTests.|FullyQualifiedName~SqlSource.Tests.UnexpectedEnumValueExceptionTests.|FullyQualifiedName~SqlSource.Tests.VersionsTests.' '*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-67/generator-root/after --tests 'FullyQualifiedName~SqlSource.Tests.Generator.|FullyQualifiedName~SqlSource.Tests.DatabaseNameTests.|FullyQualifiedName~SqlSource.Tests.EquatableArrayTests.|FullyQualifiedName~SqlSource.Tests.PackageVersionTests.|FullyQualifiedName~SqlSource.Tests.SortedSearchTests.|FullyQualifiedName~SqlSource.Tests.SqlIdentifierTests.|FullyQualifiedName~SqlSource.Tests.SqlServerNamesTests.|FullyQualifiedName~SqlSource.Tests.SystemSchemasTests.|FullyQualifiedName~SqlSource.Tests.TextLinesTests.|FullyQualifiedName~SqlSource.Tests.UnexpectedEnumValueExceptionTests.|FullyQualifiedName~SqlSource.Tests.VersionsTests.' '*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-67/generator-root/before/reports/mutation-report.json artifacts/test-cleanup/phase-67/generator-root/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-67/generator-root/before/reports/mutation-report.json MethodTests.Run_MethodsQuery_IsAnExtensionMethodOfTheTypesExtensionClass
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-67/generator-root/before/reports/mutation-report.json MethodTests.Run_MethodOnTheType_TakesThePlaceOfTheConstant
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-67/generator-root/before/reports/mutation-report.json MethodTests.Run_EachTypePlacement_PutsTheMethodOnTheType
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-67/generator-root/before/reports/mutation-report.json PlacementTests.Run_MethodsQueryWithNoLocation_IsAnInternalMethodOfTheExtensionClass
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-67/generator-root/before/reports/mutation-report.json PlacementTests.Run_FileOfSeveralLocations_PutsEachQueryWhereItSays
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-67/generator-root/before/reports/mutation-report.json QueryTypesTests.Run_ColumnOfTheBaseMap_HasNoBaseMapTypeOfItsOwn
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-67/generator-root/before/reports/mutation-report.json QueryTypesTests.Run_TypeNoNpgsqlReads_IsSqlsrc246WithTheCastToText
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-67/generator-root/before/reports/mutation-report.json QueryTypesTests.Run_TypeTheToolNeverWrites_IsSqlsrc246WithDescribe
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-67/generator-root/before/reports/mutation-report.json TypeDiagnosticsTests.Run_ProjectOnALanguageVersionOlderThanCSharp12_IsAnErrorAtEachAttribute
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-67/generator-root/before/reports/mutation-report.json TypeDiagnosticsTests.Run_NestedLocationAndAMemberNamedSql_IsAnErrorAtTheAttribute
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-67/generator-root/before/reports/mutation-report.json TypeDiagnosticsTests.Run_TypeWithSeveralProblems_ReportsThemAll
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-root`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-67/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-67/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-67/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-67/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-67/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-67/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

`tests/SqlSource.Tests.RoslynFloor` compiles `tests/SqlSource.Tests/Generator/` a second time: run it too, before the change and after, and it passes after.

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests.Generator-5-01, F-SqlSource.Tests.Generator-5-02, F-SqlSource.Tests.Generator-6-01, F-SqlSource.Tests.Generator-8-01, F-SqlSource.Tests.Generator-8-02.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tests.Generator-5-02 | fold | `MethodTests.Run_MethodOnTheType_TakesThePlaceOfTheConstant`, `MethodTests.Run_EachTypePlacement_PutsTheMethodOnTheType` (rows: all) | `tests/SqlSource.Tests/Generator/MethodTests.cs`: `MethodTests.Run_EachTypePlacement_PutsTheMethodOnTheType` |
| F-SqlSource.Tests.Generator-6-01 | delete | `PlacementTests.Run_MethodsQueryWithNoLocation_IsAnInternalMethodOfTheExtensionClass` | `tests/SqlSource.Tests/Generator/MethodTests.cs`: `MethodTests.Run_MethodsQuery_IsAnExtensionMethodOfTheTypesExtensionClass`; `tests/SqlSource.Tests/Generator/SettingLevelTests.cs`: `SettingLevelTests.Run_MemberLocationInTheHeader_PlacesTheMethod` (row `""`, line 219) |
| F-SqlSource.Tests.Generator-8-01 | fold | `TypeDiagnosticsTests.Run_ProjectOnALanguageVersionOlderThanCSharp12_IsAnErrorAtEachAttribute` (rows: `(LanguageVersion.CSharp8, "8.0")` at line 165 and `(LanguageVersion.CSharp9, "9.0")` at line 166) | `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs`: `TypeDiagnosticsTests.Run_ProjectOnALanguageVersionOlderThanCSharp12_IsAnErrorAtEachAttribute` rows `(LanguageVersion.CSharp7_3, "7.3")` (line 164) and `(LanguageVersion.CSharp11, "11.0")` (line 167) |
| F-SqlSource.Tests.Generator-8-02 | fold | `TypeDiagnosticsTests.Run_NestedLocationAndAMemberNamedSql_IsAnErrorAtTheAttribute` (rows: `"private const int Sql = 1;"` at line 333 and `"public static void Sql() { }"` at line 334) | `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs`: `TypeDiagnosticsTests.Run_NestedLocationAndAMemberNamedSql_IsAnErrorAtTheAttribute` row `"private string Sql { get; set; } = string.Empty;"` (line 332) |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md`: 13
- `tests/SqlSource.Tests/Generator/AGENTS.md`: 17

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
