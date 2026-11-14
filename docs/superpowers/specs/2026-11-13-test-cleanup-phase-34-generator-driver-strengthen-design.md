# Test cleanup phase 34: tests/SqlSource.Tests/Generator, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generator`
- Findings: 2 (2 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 16
- Guard: mutation (generator-root), with covers and run time for 2 findings

## Files

- `tests/SqlSource.Tests/Generator/GeneratedFileNameTests.cs`
- `tests/SqlSource.Tests/Generator/MethodDiagnosticsTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generator-4-02

- test: `tests/SqlSource.Tests/Generator/MethodDiagnosticsTests.cs` | `MethodDiagnosticsTests.RunSqlServer_NegatedListInsideAnAggregateWithoutAnEntry_IsSqlsrc127BesideTheMissingEntry` | rows: all
- action: strengthen
- evidence: read (MethodDiagnosticsTests.cs:162-165 asserts only `run.Problems().ShouldContain(RefusedEmpty)` and that no methods file is written; the name promises SQLSRC127 beside the missing entry's diagnostic, SQLSRC241, which nothing asserts, so a change that dropped SQLSRC241 when SQLSRC127 is found, or reported anything else beside it, would pass.  The subject is outside the scope generator-root, so it is unmeasured: kills 45, alone 0); blind: no
- cover: none
- proposal: Assert the whole list of problems, SQLSRC241 for CountOthers and `RefusedEmpty`, with `ShouldBe(..., ignoreOrder: true)` in place of `ShouldContain`.
- verdict: confirmed
- verifier's reason: Read tests/SqlSource.Tests/Generator/MethodDiagnosticsTests.cs:156-166.  The run passes no entries (`MethodRuns.RunSqlServer(file, [])`, :162), which writes an empty sidecar through `Sidecars.Of` (tests/SqlSource.Testing/Generator/MethodRuns.cs:41-45); tests/SqlSource.Tests/Generator/SidecarTests.cs:142-152 shows that an empty sidecar gives SQLSRC241 for the query.  The name promises SQLSRC127 "beside the missing entry", yet :164 asserts only `ShouldContain(RefusedEmpty)`, so a change that stopped reporting SQLSRC241 here, or added another problem, would still pass.  The finding reads the test rightly and the proposal (the whole list with `ShouldBe(..., ignoreOrder: true)`) asserts what the name promises.
- survivors to kill: none named by place; the missing assertion is the proposal's

### F-generator-01

- test: `tests/SqlSource.Tests/Generator/GeneratedFileNameTests.cs` | `GeneratedFileNameTests.Run_TypesAndNamespacesThatDifferOnlyByCase_GetFilesWhoseNamesDifferByMoreThanCase` | rows: all
- action: strengthen
- evidence: read (the twin of F-SqlSource.Tests.Generation.Emitting-1-01, confirmed, which finds that `HintNameTests.MakeUnique_AmbiguousName_GainsAHashOfItsExactSpelling` gives an ambiguous list of one clashing pair alone, so the survivor src/SqlSource/Generation/Emitting/HintName.cs:96:17 Linq method mutation (Any() to All()) passes it.  This test, GeneratedFileNameTests.cs:55-94, is the driver-level test of the same rule and has the same shape: its source, GeneratedFileNameTests.cs:62-82, declares `App.Sample`, `App.sample`, `App.Other` and `APP.Sample`, so `HintName.FindAmbiguous` (HintName.cs:73-85) gives one group, `APP.Sample.g.cs`, `App.Sample.g.cs`, `App.sample.g.cs`, every name of which is equal ignoring case to every other.  `ambiguous.All(...)` is then true for each of the three exactly where `Any(...)` is, and false for `App.Other.g.cs` as `Any` is, so under the mutant `MakeUnique` (HintName.cs:91-105, called at src/SqlSource/SqlSourceGenerator.cs:1327) hashes the same names and every assertion at GeneratedFileNameTests.cs:87-93 passes.  With a second clashing pair in the project the mutant leaves every ambiguous name unhashed, the compiler refuses the second of two names equal ignoring case, which is what the test's comment at lines 59-60 and its name guard against, and the run loses its sources.  The other driver test of the rule, `Run_TypeWhoseFileWouldBeNamedLikeTheAttributes_GetsAnotherName` (lines 97-116), has an ambiguous list of one name, and no other test of tests/SqlSource.Tests/Generator/ declares types that differ only by case (grep).  Measured: kills 48, alone 0, timeouts under it 10, but generator-root mutated only the root files of src/SqlSource, so HintName.cs is outside the scope and the test is unmeasured for its subject.  No `AGENTS.md`, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names the test; a plan, docs/superpowers/plans/2026-10-28-td-0080-2-names-and-texts.md:17, names the file only for keeping names written as literals, which the proposal keeps.  The unit test builds its ambiguous list by hand; this one is the only test that hands the pipeline's own `FindAmbiguous` result to `MakeUnique`); blind: no
- cover: none
- proposal: Add a second pair of types that differ only by case to the source, for instance `public partial class Extra` and `public partial class extra` in `namespace App`, each with `public static string Value => Sql.GetUser;`, and raise the two counts by two (9 sources, 6 constants), keeping `App.Other.g.cs` as the name that stays as it is.
- verdict: confirmed
- verifier's reason: The test's source (tests/SqlSource.Tests/Generator/GeneratedFileNameTests.cs:62-82) gives the pipeline the type names `App.Sample.g.cs`, `App.sample.g.cs`, `App.Other.g.cs` and `APP.Sample.g.cs` (src/SqlSource/SqlSourceGenerator.cs:348-352), so `HintName.FindAmbiguous` (src/SqlSource/Generation/Emitting/HintName.cs:74-86) yields one group of three names, each equal ignoring case to the others.  `MakeUnique` (HintName.cs:92-106, called at SqlSourceGenerator.cs:1327) under the mutant `HintName.cs:96:17 Any() to All()` therefore hashes exactly the same three names and leaves `App.Other.g.cs` alone, and every assertion at GeneratedFileNameTests.cs:87-94 passes.  With a second clashing pair as proposed, `All` is false for every ambiguous name, the names stay unhashed, and at least `ShouldNotContain("App.Sample.g.cs")` (line 92) and the counts fail, whatever the compiler does with the clash; the proposed counts (9 sources, 6 constants) follow from 3 fixed files plus 6 types.  `kills` on mutation/generator-root gives 48 kills, 0 alone, 10 timeouts and 2 survivors, all on the root files (SortedSearch.cs, SqlIdentifier.cs, SqlSourceGenerator.cs), none in HintName.cs; the generator-generation-emitting report holds no such test, so the test is unmeasured for its subject, as the finding says.  The twin unit finding, F-SqlSource.Tests.Generation.Emitting-1-01, is confirmed in verdicts/SqlSource.Tests.Generation.Emitting-1.md.  The only other driver test of the rule, `Run_TypeWhoseFileWouldBeNamedLikeTheAttributes_GetsAnotherName` (lines 97-116), has a single ambiguous name.  No AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names the test (grep); a strengthen removes nothing in any case.
- survivors to kill: `src/SqlSource/Generation/Emitting/HintName.cs:96:17`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-root`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-34/generator-root/before --tests 'FullyQualifiedName~SqlSource.Tests.Generator.|FullyQualifiedName~SqlSource.Tests.DatabaseNameTests.|FullyQualifiedName~SqlSource.Tests.EquatableArrayTests.|FullyQualifiedName~SqlSource.Tests.PackageVersionTests.|FullyQualifiedName~SqlSource.Tests.SortedSearchTests.|FullyQualifiedName~SqlSource.Tests.SqlIdentifierTests.|FullyQualifiedName~SqlSource.Tests.SqlServerNamesTests.|FullyQualifiedName~SqlSource.Tests.SystemSchemasTests.|FullyQualifiedName~SqlSource.Tests.TextLinesTests.|FullyQualifiedName~SqlSource.Tests.UnexpectedEnumValueExceptionTests.|FullyQualifiedName~SqlSource.Tests.VersionsTests.' '*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-34/generator-root/after --tests 'FullyQualifiedName~SqlSource.Tests.Generator.|FullyQualifiedName~SqlSource.Tests.DatabaseNameTests.|FullyQualifiedName~SqlSource.Tests.EquatableArrayTests.|FullyQualifiedName~SqlSource.Tests.PackageVersionTests.|FullyQualifiedName~SqlSource.Tests.SortedSearchTests.|FullyQualifiedName~SqlSource.Tests.SqlIdentifierTests.|FullyQualifiedName~SqlSource.Tests.SqlServerNamesTests.|FullyQualifiedName~SqlSource.Tests.SystemSchemasTests.|FullyQualifiedName~SqlSource.Tests.TextLinesTests.|FullyQualifiedName~SqlSource.Tests.UnexpectedEnumValueExceptionTests.|FullyQualifiedName~SqlSource.Tests.VersionsTests.' '*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-34/generator-root/before/reports/mutation-report.json artifacts/test-cleanup/phase-34/generator-root/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-34/generator-root/before/reports/mutation-report.json MethodDiagnosticsTests.RunSqlServer_NegatedListInsideAnAggregateWithoutAnEntry_IsSqlsrc127BesideTheMissingEntry
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-34/generator-root/before/reports/mutation-report.json GeneratedFileNameTests.Run_TypesAndNamespacesThatDifferOnlyByCase_GetFilesWhoseNamesDifferByMoreThanCase
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Generator-4-02: no survivor is named by place; the test fails when the assertion it adds is removed, checked once by hand
- F-generator-01: `src/SqlSource/Generation/Emitting/HintName.cs:96:17`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-root`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-34/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-34/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-34/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-34/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-34/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-34/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

`tests/SqlSource.Tests.RoslynFloor` compiles `tests/SqlSource.Tests/Generator/` a second time: run it too, before the change and after, and it passes after.

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests.Generator-4-02, F-generator-01.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
