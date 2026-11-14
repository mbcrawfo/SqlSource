# Test cleanup phase 66: tests/SqlSource.Tests/Generator, loosening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generator`
- Findings: 9 (9 loosen)
- Kind: one kind, loosen
- Changed lines: about 90
- Guard: mutation (generator-root), with covers and run time for 3 findings

## Files

- `tests/SqlSource.Tests/Generator/CachingConverterTests.cs`
- `tests/SqlSource.Tests/Generator/ColumnMarkerTests.cs`
- `tests/SqlSource.Tests/Generator/ConverterMethodTests.cs`
- `tests/SqlSource.Tests/Generator/GeneratedSourceEscapingTests.cs`
- `tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs`
- `tests/SqlSource.Tests/Generator/LibraryConversionTests.cs`
- `tests/SqlSource.Tests/Generator/RepositoryInterfaceTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generator-1-03

- test: `tests/SqlSource.Tests/Generator/CachingConverterTests.cs` | `CachingConverterTests.Run_NodaTimeReferenceAdded_RunsLibraryConvertersAndConverters` | rows: all
- action: loosen
- evidence: read (CachingConverterTests.cs:293 asserts `NodaTimeEdgesOf(result).Count().ShouldBe(14)`, the number of rows in `NodaTimeConversions.Edges`, src/SqlSource/Generation/Converters/NodaTimeConversions.cs:60, which `LibraryConvertersTests` already holds at tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs:81 and :200.  A new NodaTime conversion, which changes nothing of the caching, fails this caching test; measured kills 55, alone 0, so the literal guards nothing of its own.); blind: no
- cover: none
- proposal: Assert that the edges were joined into the set without their number, `ShouldBe(NodaTimeConversions.Edges.Count)` or `ShouldNotBeEmpty()`, beside the reasons the test is about.
- verdict: confirmed
- verifier's reason: tests/SqlSource.Tests/Generator/CachingConverterTests.cs:293 asserts `NodaTimeEdgesOf(result).Count().ShouldBe(14)`, the size of `NodaTimeConversions.Edges` (src/SqlSource/Generation/Converters/NodaTimeConversions.cs:59-60, "The fourteen edges"), which tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs:81 and :200 already hold.  The test's comment (CachingConverterTests.cs:277-279) is about the edges being found and joined while the project's converters are not, not about their number, so a new NodaTime edge would fail it with nothing of the caching changed.  `NodaTimeConversions` is internal and visible to both SqlSource.Tests and SqlSource.Tests.RoslynFloor (src/SqlSource/SqlSource.csproj:22-23), so the proposed comparison compiles in both.

### F-SqlSource.Tests.Generator-1-04

- test: `tests/SqlSource.Tests/Generator/CachingConverterTests.cs` | `CachingConverterTests.Run_UnrelatedEdit_LeavesLibraryConvertersCached` | rows: all
- action: loosen
- evidence: read (CachingConverterTests.cs:317 asserts `NodaTimeEdgesOf(result).Count().ShouldBe(14)`, the same count of `NodaTimeConversions.Edges` that tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs:81 holds; a new NodaTime conversion fails this test of caching though nothing it is about changed; measured kills 48, alone 0.); blind: no
- cover: none
- proposal: Compare with `NodaTimeConversions.Edges.Count`, or assert the edges are not empty, so the test checks that the cached set still holds the library's edges and not how many there are.
- verdict: confirmed
- verifier's reason: tests/SqlSource.Tests/Generator/CachingConverterTests.cs:317 holds the same literal 14 in a test whose comment (:301-302) is that an edit of a converter's body leaves the library's edges cached; the reasons at :315-316 are the point, and the count is the size of `NodaTimeConversions.Edges` that LibraryConvertersTests.cs:81 owns.  The reading is right, and comparing with `NodaTimeConversions.Edges.Count` keeps the check that the cached set still holds every edge.

### F-SqlSource.Tests.Generator-2-01

- test: `tests/SqlSource.Tests/Generator/ConverterMethodTests.cs` | `ConverterMethodTests.Run_ProjectWithoutConverters_GeneratesAsPhase81ApartFromTheRowBuffer` | rows: all
- action: loosen
- evidence: read (the test, at ConverterMethodTests.cs:328-343, compares the whole methods file with `FindEmailMethods`, ConverterMethodTests.cs:83-170: the summary and parameter documentation, the open and close of the connection, the `await using` scopes, the command's timeout and the row buffer, where the rule its comment states is only that an override with no converter is read and bound as the driver does, lines 139-141 and 152-153.  Its own name and comment, "apart from the row buffer that came later", record that a change to the emitter that left that rule alone already broke it, and the plans `docs/superpowers/plans/2026-10-26-pooled-row-buffer-phase-3-lists.md:50` and `2026-10-31-roslyn-4-14-floor.md:421` list this file among those whose pinned text had to be rewritten.  The whole text of a query method is the golden corpus's assertion (tests/README.md, "The golden corpus of query methods").  Measured: kills 62, alone 0, timeouts under it 12, but its subject, the conversion plan and the emitter, is outside the generator-root scope, which mutated the root files alone.  No `AGENTS.md`, readme, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names the test); blind: no
- cover: none
- proposal: Assert the rule alone: the parameter is `new global::Npgsql.NpgsqlParameter<global::MyApp.Email>()` with `parameter.TypedValue = parameters.Email;`, the column is read as `reader.IsDBNull(0) ? default(global::MyApp.Email?) : reader.GetFieldValue<global::MyApp.Email>(0)`, and no `FromDatabase` or `ToDatabase` is called, and drop `FindEmailMethods` and the phase from the name.
- verdict: confirmed
- verifier's reason: The finding reads the test rightly.  `ConverterMethodTests.Run_ProjectWithoutConverters_GeneratesAsPhase81ApartFromTheRowBuffer` (tests/SqlSource.Tests/Generator/ConverterMethodTests.cs:325-343) compares the whole methods file with `FindEmailMethods` (lines 83-170).  Its comment (lines 325-326) states one rule: an override with no converter is read and bound as the driver reads and binds the type.  That rule is in lines 139-141 (`NpgsqlParameter<global::MyApp.Email>`, `TypedValue = parameters.Email`) and lines 152-153 (`reader.IsDBNull(0) ? default(global::MyApp.Email?) : reader.GetFieldValue<global::MyApp.Email>(0)`).  The rest of the text is documentation, connection handling and the row buffer, which a change to the emitter can alter while the rule holds.  The test's own comment already notes one such change ("apart from the row buffer"), and docs/superpowers/plans/2026-10-31-roslyn-4-14-floor.md:421 rewrote this file's pinned `SqlSourceRowBuffer` text.  The phase-8.2 plan (docs/superpowers/plans/2026-10-25-query-generation-phase-8-2-converters.md:175) pinned the whole text only to show that the phase left the output unchanged.  The golden corpus is now the one whole-text guard (tests/README.md:173).  `kills` in generator-root gives 62 kills, 0 alone, 12 timeouts and 3 survivors, all in root files (SqlSourceGenerator.cs, SqlIdentifier.cs and the like) and none in the converter planner or the emitter.  So the numbers say nothing about this rule.  A loosen keeps the test, so its timeouts do not stand in the way.  The docs search (the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred) finds no mention of the test.  The proposed assertions keep both lines of the rule and the absence of `FromDatabase` and `ToDatabase`.

### F-SqlSource.Tests.Generator-2-02

- test: `tests/SqlSource.Tests/Generator/ColumnMarkerTests.cs` | `ColumnMarkerTests.Run_ColumnMarkers_LeaveTheColumnNamesAlone` | rows: all
- action: loosen
- evidence: read (ColumnMarkerTests.cs:73-88 compares the whole model file, its header, namespace and the `<summary>` line "A row of the query ... from ...", where the rule its comment states, ColumnMarkerTests.cs:56-57, is that the property and its documentation name the plain column: the two `<param>` lines and the record's parameters.  A change to the model's summary or header, which leaves every column's name alone, fails it.  Measured: kills 44, alone 0, timeouts under it 12; the marker's handling is outside the generator-root scope.  No governing document names the test); blind: no
- cover: none
- proposal: Assert the record's declaration, `public sealed record GetTotalsDto(int Total, string? Note);`, and the two `<param name="Total">The column <c>total</c>` and `<param name="Note">The column <c>note</c>` lines, not the whole file.
- verdict: confirmed
- verifier's reason: The finding reads the test rightly.  `ColumnMarkerTests.Run_ColumnMarkers_LeaveTheColumnNamesAlone` (tests/SqlSource.Tests/Generator/ColumnMarkerTests.cs:56-89) compares the whole model file with `ShouldBe`: `GeneratedFile.ModelHeader`, the namespace and the `<summary>` "A row of the query ... from ..." (line 80).  Its comment (lines 56-57) states only that the property and its documentation name the plain column.  That rule is in the two `<param>` lines (82-83) and the record declaration (84).  A change to the model's header or summary would fail the test and leave every column name alone.  The proposed assertion keeps the declaration `GetTotalsDto(int Total, string? Note)`, which also carries the nullability the markers set, and keeps both `<param>` lines.  This is a loosen, so no cover is needed.  The kills are generic root mutants (44, 0 alone) and the marker's own code is outside the generator-root scope.  The docs search finds the class only in the list of driver tests at tests/SqlSource.Tests/Generator/AGENTS.md:17, which says where the class lives and is no record of this test.

### F-SqlSource.Tests.Generator-3-01

- test: `tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs` | `GeneratedSourceTests.Run_NestedType_RepeatsEachContainingType` | rows: all
- action: loosen
- evidence: read (tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs:252-282 compares the whole file, including the constant's `<summary>`/`<remarks>`/`<code>` comment written by `src/SqlSource/Generation/Emitting/XmlDocWriter.cs`, where the name promises only the chain `namespace App.Data` > `partial class Outer` > `partial interface IMiddle<TKey, TValue>` > `partial record Inner` > `private static partial class Sql`; a change to the wording or layout of a constant's documentation fails it with the nesting unchanged.  `Run_EachKindOfType_RepeatsItsKeywordNameAndTypeParameters` at GeneratedSourceTests.cs:213-216 already asserts the same kind of rule by the declaration lines alone, and the layout of a whole Sql-output file stays recorded by `Run_NestedLocation_PutsTheConstantsInAPrivateSqlClass` (GeneratedSourceTests.cs:81-117) and `GeneratedSourceMethodTests.Run_QueryWithTokens_IsAMethodBesideTheConstantsOfItsFile` (GeneratedSourceMethodTests.cs:45-104)); blind: no
- cover: none
- proposal: Assert with `ShouldContain` the run of declaration lines from `namespace App.Data` down to `private static partial class Sql`, and the constant's own line, as `Run_EachKindOfType_RepeatsItsKeywordNameAndTypeParameters` does, instead of the whole file.
- verdict: confirmed
- verifier's reason: tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs:252-282 compares the whole of `App.Data.Outer.IMiddle-2.Inner.g.cs` with `ShouldBe`, including the constant's `<summary>`/`<remarks>`/`<code>` lines at 266-273, while the name promises only that each containing type is repeated.  The rule of repeating each kind of declaration is asserted by lines alone in `Run_EachKindOfType_RepeatsItsKeywordNameAndTypeParameters` (GeneratedSourceTests.cs:213-216, `ShouldContain` of the declaration run), and the constant's documentation is fixed by `TypeEmitterTests.Emit_NestedGenericTypeInTheGlobalNamespace_RepeatsEachDeclarationAsWritten` (tests/SqlSource.Tests/Generation/Emitting/TypeEmitterTests.cs:96-140) and `XmlDocWriterTests`, so a change to the doc's wording breaks this test with the nesting unchanged.  No AGENTS.md calls these driver-level whole-file comparisons deliberate (tests/AGENTS.md, tests/SqlSource.Tests/AGENTS.md, tests/SqlSource.Tests/Generator/AGENTS.md read); the brief names the golden corpus as the one place a whole generated text is the assertion.  No document names the test (grep over docs/architecture, AGENTS.md files, docs/tech-debt, docs/deferred).  `kills` in generator-root: 44 kills, 10 timeouts, all in SqlSourceGenerator.cs pipeline code that the proposed `ShouldContain` of the declaration run and the constant line would still fail on, since each breaks the clean run or the file.  The proposal keeps the declaration lines with their indentation and the clean run (which catches unbalanced braces).

### F-SqlSource.Tests.Generator-3-02

- test: `tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs` | `GeneratedSourceTests.Run_ClassLocation_PutsTheConstantsOnTheType` | rows: all
- action: loosen
- evidence: read (tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs:143-164 compares the whole file, documentation comment included, where the rule is that the constant is a member of `Sample` itself and public; the source's `Consumer.Value = Sample.GetUser` at GeneratedSourceTests.cs:134-137 already fails the clean run if the constant is not on the type or not reachable.  A change to the constant's documentation breaks the test with the placement unchanged; the whole-file layout stays recorded by `Run_NestedLocation_PutsTheConstantsInAPrivateSqlClass` and by `GeneratedSourceMethodTests.Run_QueryWithTokens_IsAMethodBesideTheConstantsOfItsFile`, which is itself a class-location file); blind: no
- cover: none
- proposal: Keep the clean run and assert that the file holds `partial class Sample\n    {\n` followed by the constant's line `public const string GetUser = "SELECT 1;";` and no `class Sql`, instead of the whole text.
- verdict: confirmed
- verifier's reason: tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs:143-164 compares the whole file, the doc comment at 151-158 included, while the test's comment (line 120) and name promise only that the constant goes on the type and is public.  The clean run with `Consumer.Value = Sample.GetUser` (lines 134-137) already proves the member is on `Sample`; the proposal keeps `public const string GetUser = "SELECT 1;";` (the public part, which a same-assembly consumer could not prove) and adds no `class Sql`.  Dropping of `static` from the repeated declaration stays asserted by the `"public static partial class Sample"` row of `Run_EachKindOfType_RepeatsItsKeywordNameAndTypeParameters` (GeneratedSourceTests.cs:169).  The whole layout of a direct-placement file stays recorded by `GeneratedSourceMethodTests.Run_QueryWithTokens_IsAMethodBesideTheConstantsOfItsFile` (GeneratedSourceMethodTests.cs:45-104) and `TypeEmitterTests.Emit_DirectPlacement_PutsTheConstantsOnTheType` (TypeEmitterTests.cs:69).  No document or comment names the test as a record.  One note for the writer: the doc comment sits between `{` and the constant, so "followed by" in the proposal means later in the file, not adjacent.

### F-SqlSource.Tests.Generator-3-03

- test: `tests/SqlSource.Tests/Generator/GeneratedSourceEscapingTests.cs` | `GeneratedSourceEscapingTests.Run_TypeInTheGlobalNamespaceNamedWithAKeyword_HasNoNamespaceBlockAndKeepsTheEscape` | rows: all
- action: loosen
- evidence: read (tests/SqlSource.Tests/Generator/GeneratedSourceEscapingTests.cs:29-47 compares the whole of `class.g.cs`, documentation comment included, where the name promises two things: no `namespace` block and the declaration `partial struct @class`.  A change to how a constant is documented fails it with neither rule touched); blind: no
- cover: none
- proposal: Assert that the file starts with the generated header followed by `partial struct @class\n{\n`, that it holds no `namespace`, and the constant's line, instead of the whole text.
- verdict: confirmed
- verifier's reason: tests/SqlSource.Tests/Generator/GeneratedSourceEscapingTests.cs:29-47 compares the whole of `class.g.cs`, doc comment (35-42) included, where the name promises no namespace block and the escaped `partial struct @class`.  The proposal (header then `partial struct @class\n{\n`, no `namespace`, the constant's line) keeps both rules: the header, `TypeEmitter.HeaderLines` (src/SqlSource/Generation/Emitting/TypeEmitter.cs:25-31), holds no word `namespace`, so a `ShouldNotContain("namespace")` is sound, and the constant's line with its four-space indent keeps the top-level layout.  The global-namespace layout with documentation is fixed at the unit level by `TypeEmitterTests.Emit_NestedGenericTypeInTheGlobalNamespace_RepeatsEachDeclarationAsWritten` (TypeEmitterTests.cs:96-140).  No AGENTS.md or document names this test; the class comment (line 10) describes the class's topic only.

### F-SqlSource.Tests.Generator-4-03

- test: `tests/SqlSource.Tests/Generator/LibraryConversionTests.cs` | `LibraryConversionTests.Run_SqlServerJsonColumnWithJsonOn_ReadsAndWritesThroughJsonConversions` | rows: all
- action: loosen
- evidence: read (LibraryConversionTests.cs:394-396 asserts `"parameter.SqlDbType = global::System.Data.SqlDbType.NVarChar;\n                    parameter.Size = -1;\n"`, two statements joined by twenty spaces of indentation: a change to the nesting of the method's binding block breaks it while the parameter is still bound as nvarchar(max).  `JsonSerializerMethodTests.cs:129` and `:576` assert `"parameter.Size = -1;\n"` alone.  The subject is outside the scope generator-root, so it is unmeasured); blind: no
- cover: none
- proposal: Assert `"parameter.SqlDbType = global::System.Data.SqlDbType.NVarChar;\n"` and `"parameter.Size = -1;\n"` as two separate `ShouldContain` calls, without the indentation between them.
- verdict: confirmed
- verifier's reason: Read tests/SqlSource.Tests/Generator/LibraryConversionTests.cs:372-398.  :394-396 asserts `"parameter.SqlDbType = global::System.Data.SqlDbType.NVarChar;\n                    parameter.Size = -1;\n"`, the two statements joined by twenty spaces of indentation; it is the only assertion of tests/SqlSource.Tests/Generator/*.cs that carries a statement's indentation (`grep` of `;\n` followed by twenty spaces finds this one line).  The indentation follows the nesting of the binding block, which differs between shapes (24 and 32 spaces in Golden/sqlserver/lists-sync.txt:2802 and :2822 against 20 in Golden/sqlserver/binding.txt:579), so a change to nesting breaks the test while the parameter is still bound as nvarchar(max).  JsonSerializerMethodTests.cs:128-129 and :575-576 assert the same two statements separately, as the proposal does.  The only parameters of FindDocs are the two json ones, so the separate form loses no check of which parameter gets the size.

### F-SqlSource.Tests.Generator-6-06

- test: `tests/SqlSource.Tests/Generator/RepositoryInterfaceTests.cs` | `RepositoryInterfaceTests.Run_RepositoryType_ImplementsItsGeneratedInterface` | rows: all
- action: loosen
- evidence: read (RepositoryInterfaceTests.cs:48-79 asserts with `ShouldEndWith` the interface's whole text: its summary and the member's `<summary>`, `<remarks>`, `<param>`, `<returns>` and `<exception>` documentation, line by line.  The behaviour the name promises is that the type's part implements `global::App.IUserRepository` (:45-47) and that the interface declares the public method's signature.  A change to the wording of a query method's documentation breaks this test though the interface is unchanged.  The interface's whole text, documentation included, is already in the golden corpus, for example tests/SqlSource.Tests/Generator/Golden/postgres/repository-data-source.txt:250-266.  Measured: kills 57, alone 0, timeouts under it 12); blind: no
- cover: none
- proposal: Keep the base-list assertion.  In place of the tail, assert `public partial interface IUserRepository` and the member's signature lines, `Task<global::App.GetUserDto?> GetUser(`, its two parameters and the closing `;`.  Leave the documentation's text to the corpus.
- verdict: confirmed
- verifier's reason: RepositoryInterfaceTests.cs:38-80 asserts the base list at :45-47.  At :48-79 it asserts with `ShouldEndWith` the interface's whole tail: its summary and the member's `<summary>`, `<remarks>`, `<param>`, `<returns>` and `<exception>` lines.  The test's name and the class comment (:15-17) promise that the type implements its generated interface, which the base list and the interface's declaration and signature prove.  The same documentation text is in the golden corpus, tests/SqlSource.Tests/Generator/Golden/postgres/repository-data-source.txt:247-266.  A change to the wording of a method's documentation would break this test with the interface unchanged.  The finding reads the test rightly.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-root`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-66/generator-root/before --tests 'FullyQualifiedName~SqlSource.Tests.Generator.|FullyQualifiedName~SqlSource.Tests.DatabaseNameTests.|FullyQualifiedName~SqlSource.Tests.EquatableArrayTests.|FullyQualifiedName~SqlSource.Tests.PackageVersionTests.|FullyQualifiedName~SqlSource.Tests.SortedSearchTests.|FullyQualifiedName~SqlSource.Tests.SqlIdentifierTests.|FullyQualifiedName~SqlSource.Tests.SqlServerNamesTests.|FullyQualifiedName~SqlSource.Tests.SystemSchemasTests.|FullyQualifiedName~SqlSource.Tests.TextLinesTests.|FullyQualifiedName~SqlSource.Tests.UnexpectedEnumValueExceptionTests.|FullyQualifiedName~SqlSource.Tests.VersionsTests.' '*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-66/generator-root/after --tests 'FullyQualifiedName~SqlSource.Tests.Generator.|FullyQualifiedName~SqlSource.Tests.DatabaseNameTests.|FullyQualifiedName~SqlSource.Tests.EquatableArrayTests.|FullyQualifiedName~SqlSource.Tests.PackageVersionTests.|FullyQualifiedName~SqlSource.Tests.SortedSearchTests.|FullyQualifiedName~SqlSource.Tests.SqlIdentifierTests.|FullyQualifiedName~SqlSource.Tests.SqlServerNamesTests.|FullyQualifiedName~SqlSource.Tests.SystemSchemasTests.|FullyQualifiedName~SqlSource.Tests.TextLinesTests.|FullyQualifiedName~SqlSource.Tests.UnexpectedEnumValueExceptionTests.|FullyQualifiedName~SqlSource.Tests.VersionsTests.' '*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-66/generator-root/before/reports/mutation-report.json artifacts/test-cleanup/phase-66/generator-root/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-66/generator-root/before/reports/mutation-report.json CachingConverterTests.Run_NodaTimeReferenceAdded_RunsLibraryConvertersAndConverters
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-66/generator-root/before/reports/mutation-report.json CachingConverterTests.Run_UnrelatedEdit_LeavesLibraryConvertersCached
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-66/generator-root/before/reports/mutation-report.json ConverterMethodTests.Run_ProjectWithoutConverters_GeneratesAsPhase81ApartFromTheRowBuffer
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-66/generator-root/before/reports/mutation-report.json ColumnMarkerTests.Run_ColumnMarkers_LeaveTheColumnNamesAlone
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-66/generator-root/before/reports/mutation-report.json GeneratedSourceTests.Run_NestedType_RepeatsEachContainingType
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-66/generator-root/before/reports/mutation-report.json GeneratedSourceTests.Run_ClassLocation_PutsTheConstantsOnTheType
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-66/generator-root/before/reports/mutation-report.json GeneratedSourceEscapingTests.Run_TypeInTheGlobalNamespaceNamedWithAKeyword_HasNoNamespaceBlockAndKeepsTheEscape
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-66/generator-root/before/reports/mutation-report.json LibraryConversionTests.Run_SqlServerJsonColumnWithJsonOn_ReadsAndWritesThroughJsonConversions
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-66/generator-root/before/reports/mutation-report.json RepositoryInterfaceTests.Run_RepositoryType_ImplementsItsGeneratedInterface
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-root`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-66/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-66/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-66/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-66/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-66/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-66/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

`tests/SqlSource.Tests.RoslynFloor` compiles `tests/SqlSource.Tests/Generator/` a second time: run it too, before the change and after, and it passes after.

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests.Generator-2-01, F-SqlSource.Tests.Generator-2-02, F-SqlSource.Tests.Generator-4-03.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
