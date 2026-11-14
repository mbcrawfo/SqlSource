# Test cleanup phase 69: tests/SqlSource.Tests/Generation/Converters, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Converters`
- Findings: 5 (3 delete, 1 strengthen, 1 loosen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 44
- Guard: mutation (generator-generation-converters), with covers and run time for 2 findings

## Files

- `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-1-10

- test: `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs` | `LibraryConvertersTests.Of_WithNodaTime_IsSortedAsTheSetSortsThem` | rows: all
- action: delete
- evidence: measured (covers 6 mutants and kills none); read (the order is made by `edges.Sort(ConverterSet.Comparer)` in `NodaTimeConversions.BuildEdges`, src/SqlSource/Generation/Converters/NodaTimeConversions.cs:91, a static initialiser no run can mutate, and the 6 mutants covered are Merge's early returns, equivalent for one library; `Of_WithNodaTime_HasTheFourteenEdges` asserts the same edges in the order of argument then return, ordinal, LibraryConvertersTests.cs:73-80, which is the comparer's order since no two NodaTime edges share an argument and a return, and `Of_WithNodaTimeAndJson_IsBothSortedAsTheSetSortsThem` fails too when the NodaTime edges are out of the comparer's order, since the merge then cannot equal the sorted concatenation, LibraryConvertersTests.cs:255-257; no comment above the test, and no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names it or LibraryConvertersTests); blind: yes
- cover: `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs` | `LibraryConvertersTests.Of_WithNodaTime_HasTheFourteenEdges`; `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs` | `LibraryConvertersTests.Of_WithNodaTimeAndJson_IsBothSortedAsTheSetSortsThem`
- proposal: Delete it; the fourteen-edges test pins the order and the merged test pins it against the comparer.
- verdict: confirmed
- verifier's reason: The order comes from `edges.Sort(ConverterSet.Comparer)` in a static initialiser, src/SqlSource/Generation/Converters/NodaTimeConversions.cs:69-91.  The comparer orders ordinally by argument, then return, then method, src/SqlSource/Generation/Converters/ConverterSet.cs:15 and 203.
  - No two of the 14 NodaTime edges share both an argument and a return, tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs:24-40.  The argument-then-return order that `Of_WithNodaTime_HasTheFourteenEdges` asserts (73-80) is therefore the comparer's order.
  - `Of_WithNodaTimeAndJson_IsBothSortedAsTheSetSortsThem` (255-257) compares the merged result with the concatenation sorted by the comparer.
  - `kills` (generator-generation-converters): 0 kills, 0 timeouts.
  - No comment above the test, and no listed document names it.

### F-SqlSource.Tests.Generation-1-11

- test: `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs` | `LibraryConvertersTests.Of_EveryEdge_ServesSqlServerAlone` | rows: all
- action: delete
- evidence: measured (kills 5, alone 0, timeouts under it 0; cover: `ConverterTargetsTests.Serves_SqlServerEdge_DoesNotServeNpgsql10`, `LibraryConvertersTests.Of_EveryEdge_IsALibraryEdgeWithoutAFaultOrALocation`); read (`Serves` is `Library is null || (Library.Targets & target) != 0`, src/SqlSource/Generation/Converters/Converter.cs:51; `Of_EveryEdge_IsALibraryEdgeWithoutAFaultOrALocation` asserts that every NodaTime edge's `Library` is `new LibraryScope(SqlLibrary.NodaTime, ConverterTargets.SqlServer)`, LibraryConvertersTests.cs:103, and `Serves_SqlServerEdge_DoesNotServeNpgsql10` asserts that a converter of exactly that scope serves SQL Server and neither Npgsql target, tests/SqlSource.Tests/Generation/Converters/ConverterTargetsTests.cs:36-43; no comment above the test, and no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names it); blind: no
- cover: `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs` | `LibraryConvertersTests.Of_EveryEdge_IsALibraryEdgeWithoutAFaultOrALocation`; `tests/SqlSource.Tests/Generation/Converters/ConverterTargetsTests.cs` | `ConverterTargetsTests.Serves_SqlServerEdge_DoesNotServeNpgsql10`
- proposal: Delete it; the edges' scope and what that scope serves are each asserted already.
- verdict: confirmed
- verifier's reason: `Serves` is `Library is null || (Library.Targets & target) != 0`, src/SqlSource/Generation/Converters/Converter.cs:51.
  - `Of_EveryEdge_IsALibraryEdgeWithoutAFaultOrALocation` asserts that every NodaTime edge has the scope `LibraryScope(NodaTime, SqlServer)`, LibraryConvertersTests.cs:103.  `ConverterTargetsTests.Serves_SqlServerEdge_DoesNotServeNpgsql10` asserts the three answers of `Serves` for that exact scope, tests/SqlSource.Tests/Generation/Converters/ConverterTargetsTests.cs:36-43.
  - `kills` (generator-generation-converters): 5 kills, 0 timeouts.  The covers explain each one: the Converter.cs:51 mutants by the ConverterTargets test, and the 34:13 inclusion of Json by the library-edge test, whose scope check fails on a Json edge.
  - No comment above the test.  The only document that names it is a plan under docs/superpowers/plans.

### F-SqlSource.Tests.Generation-1-12

- test: `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs` | `LibraryConvertersTests.Of_WithOneLibrary_IsThatLibrarysEdges` | rows: all
- action: delete
- evidence: measured (kills 5, alone 0, timeouts under it 0; cover: `LibraryConvertersTests.Of_WithJson_HasTheTenEdges`, `LibraryConvertersTests.Of_WithNodaTime_HasTheFourteenEdges`); read (its second line, `LibraryConverters.Of(JsonProbe()).ShouldBe(JsonConversions.Edges)` at LibraryConvertersTests.cs:264, is the last line of `Of_WithJson_HasTheTenEdges`, line 216; its first line, the NodaTime probe giving `NodaTimeConversions.Edges`, is held edge by edge in order by `Of_WithNodaTime_HasTheFourteenEdges`, lines 71-81, with the edges' other fields in `Of_EveryEdge_IsALibraryEdgeWithoutAFaultOrALocation`; no comment above the test, and no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names it); blind: no
- cover: `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs` | `LibraryConvertersTests.Of_WithJson_HasTheTenEdges`; `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs` | `LibraryConvertersTests.Of_WithNodaTime_HasTheFourteenEdges`
- proposal: Delete it; each library's test already asserts that one library gives its own edges.
- verdict: confirmed
- verifier's reason: Line 264, `Of(JsonProbe()).ShouldBe(JsonConversions.Edges)`, is the same assertion as line 216 of `Of_WithJson_HasTheTenEdges`.  Line 263 is held edge by edge by `Of_WithNodaTime_HasTheFourteenEdges` (71-81), with the count, and the other fields by `Of_EveryEdge_IsALibraryEdgeWithoutAFaultOrALocation`.
  - `kills` (generator-generation-converters): 5 kills, 0 timeouts.  33:13 true/false and 34:13 true/false change the set of edges, which the count and table tests fail on.  61:13 `!= 0` returns Empty for Json alone, which line 216 fails on.
  - No comment above the test, and no listed document names it.

### F-SqlSource.Tests.Generation-1-04

- test: `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs` | `LibraryConvertersTests.Of_TwiceOverEqualProbes_IsEqual` | rows: all
- action: strengthen
- evidence: measured (covers 6 mutants and kills none); read (with the NodaTime probe alone `Of` returns `NodaTimeConversions.Edges`, the one static array, unmerged, LibraryConverters.cs:31-35 and 61-64, so the test compares an object with itself and would pass whatever the result's equality is; the 6 mutants it covers are Merge's early returns, which are equivalent for one library; the name promises that two calls over equal probes give equal values, which only a call that builds its own array tests); blind: no
- cover: none
- proposal: Probe with both libraries, `Probe() with { Json = true }`, so that each call merges a new array and the two are compared by value.
- verdict: confirmed
- verifier's reason: `NodaTimeConversions.Edges` is a static property set once, src/SqlSource/Generation/Converters/NodaTimeConversions.cs:60.  For a NodaTime-only probe, `Of` returns it unchanged through Merge's early return, src/SqlSource/Generation/Converters/LibraryConverters.cs:31-35 and 61-64.  The test, tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs:130-131, therefore compares one instance with itself.  `kills` (generator-generation-converters) shows 0 kills and the 62:9 block removal surviving.  With `Json = true`, each call builds a new array in Merge (71-84), which the equality of `Converter` and `EquatableArray` must then compare by value.
- survivors to kill: none named by place; the missing assertion is the proposal's

### F-SqlSource.Tests.Generation-1-05

- test: `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs` | `LibraryConvertersTests.SourceOf_NetTopologySuite_IsTheClassOfTheFourInstances` | rows: all
- action: loosen
- evidence: read (reaches no tested mutant; the test asserts the whole file, header, blank lines and the two lines of the comment above the static constructor included, LibraryConvertersTests.cs:356-368, and its first assertion, `source.ShouldBe(NetTopologySuiteConversions.Source)` at line 355, compares the value with itself; the same whole text is in the golden corpus, tests/SqlSource.Tests/Generator/Golden/sqlserver/spatial.txt:782-802, the one place where a whole generated text is the assertion; a rewording of the comment or a change of spacing breaks this test while the four instances and their first-use construction stay); blind: no
- cover: none
- proposal: Assert what the comment above the test names, from the parsed source: the four `internal static readonly` fields with their types and initialisers, the static constructor that delays them, and nothing else declared; leave the comment and spacing to the golden corpus.
- verdict: confirmed
- verifier's reason: tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs:356-368 asserts the whole file, the two comment lines and the blank lines included.  The same text is in the golden corpus at tests/SqlSource.Tests/Generator/Golden/sqlserver/spatial.txt:782-802.  The comment above the test, 344-345, names only the four instances and their construction at first use, which the proposal keeps.  A rewording of the generated comment would break the test while the class stayed the same.  One small misreading does not change the verdict: line 355 does not compare the value with itself.  `SourceOf(SqlLibrary.NetTopologySuite)` against `NetTopologySuiteConversions.Source` checks the arm of the switch at LibraryConverters.cs:45, so it should stay.  No document names the test.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-converters`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-69/generator-generation-converters/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Converters.' 'Generation/Converters/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-69/generator-generation-converters/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Converters.' 'Generation/Converters/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-69/generator-generation-converters/before/reports/mutation-report.json artifacts/test-cleanup/phase-69/generator-generation-converters/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-69/generator-generation-converters/before/reports/mutation-report.json LibraryConvertersTests.Of_TwiceOverEqualProbes_IsEqual
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-69/generator-generation-converters/before/reports/mutation-report.json LibraryConvertersTests.SourceOf_NetTopologySuite_IsTheClassOfTheFourInstances
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-69/generator-generation-converters/before/reports/mutation-report.json LibraryConvertersTests.Of_WithNodaTime_IsSortedAsTheSetSortsThem
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-69/generator-generation-converters/before/reports/mutation-report.json LibraryConvertersTests.Of_EveryEdge_ServesSqlServerAlone
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-69/generator-generation-converters/before/reports/mutation-report.json LibraryConvertersTests.Of_WithOneLibrary_IsThatLibrarysEdges
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Generation-1-04: no survivor is named by place; the test fails when the assertion it adds is removed, checked once by hand

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-converters`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-69/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-69/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-69/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-69/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-69/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-69/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests.Generation-1-05, F-SqlSource.Tests.Generation-1-10.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tests.Generation-1-10 | delete | `LibraryConvertersTests.Of_WithNodaTime_IsSortedAsTheSetSortsThem` | `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs`: `LibraryConvertersTests.Of_WithNodaTime_HasTheFourteenEdges`; `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs`: `LibraryConvertersTests.Of_WithNodaTimeAndJson_IsBothSortedAsTheSetSortsThem` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tests.Generation-1-23 (keep, `LibraryConvertersTests.Of_EveryEdge_IsNamedByItsMethodAndItsArgument`): a **keep**; the test stays as it is.
- F-SqlSource.Tests.Generation-1-24 (keep, `LibraryConvertersTests.Of_WithJson_IsNamedByItsMethodAndItsArgument`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
