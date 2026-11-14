# Test cleanup phase 41: tests/SqlSource.Tests/Generation/Placement, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Placement`
- Findings: 2 (2 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 16
- Guard: mutation (generator-generation-placement)

## Files

- `tests/SqlSource.Tests/Generation/Placement/TypePlacementTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-2-12

- test: `tests/SqlSource.Tests/Generation/Placement/TypePlacementTests.cs` | `TypePlacementTests.Of_Protected_IsAnErrorOnlyWhereCSharpForbidsIt` | rows: the four with a reason, lines 392-419
- action: strengthen
- evidence: measured (survivor src/SqlSource/Generation/Placement/PlacementChecks.cs:118:43 String mutation `"nested-class"` to `""`, covered by this test alone); read (the expected diagnostic takes its location text from `PlacementChecks.TextOf(location)`, TypePlacementTests.cs:462, the code under test, so a wrong text for a nested class is expected as well as produced; the text of `class` is pinned only because `Of_ProtectedRepositoryQueryOnAStruct_ReportsSqlsrc123WithTheVisibilityAlone` writes `"class"` as a literal, line 489); blind: no
- cover: none
- proposal: Give each row its location as a marker writes it, `"class"` or `"nested-class"`, and expect that literal in place of the call to `PlacementChecks.TextOf`.
- verdict: confirmed
- verifier's reason: The test expects `PlacementChecks.TextOf(location)` (TypePlacementTests.cs:462), which is the code under test.  So the survivor `PlacementChecks.cs:118:43 "nested-class" -> ""` changes the expected text as well as the actual one, and only this test covers it.  `class` is pinned only by the literal at :489 of another test.  src/SqlSource/Generation/Placement/AGENTS.md says that `TextOf` spells each value as a marker writes it, since the messages quote it.  Writing the literal in each row pins that text.
- survivors to kill: `src/SqlSource/Generation/Placement/PlacementChecks.cs:118:43`

### F-SqlSource.Tests.Generation-2-13

- test: `tests/SqlSource.Tests/Generation/Placement/TypePlacementTests.cs` | `TypePlacementTests.Of_NestedClassNamedLikeAMemberOfTheType_ReportsSqlsrc007OnceAndDropsItsFiles` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/Generation/Placement/TypePlacement.cs:252:24 LogicalNotExpression to un-LogicalNotExpression mutation, `!taken.Add(name)` to `taken.Add(name)`, covered by this test alone); read (TypePlacement.cs:249-253 reports the first query of a taken class name and skips the rest; with the mutant the first query is skipped and the second reported, so two queries in `Queries`, TypePlacementTests.cs:644-645, still give one diagnostic, while a third would give two; the name promises one report for the class however many queries it holds); blind: no
- cover: none
- proposal: Put a third query in the nested class `Queries` of Users.sql, keeping the one expected SQLSRC007 and the usable files `[false, true]`.
- verdict: confirmed
- verifier's reason: CheckTakenClassNames (TypePlacement.cs:243-266) skips a query when its name is not taken or `!taken.Add(name)` (:250-251).  Under the mutant `taken.Add(name)`, the first query of a taken class is skipped and every later one is reported.  With the two queries of `Queries` at TypePlacementTests.cs:644-645 that still gives one SQLSRC007, and `taken.Count > 0` still leaves the file out, so the mutant survives under this test alone.  A third query would give two diagnostics and fail the `ShouldBe` at :652.  The name promises one report.
- survivors to kill: `src/SqlSource/Generation/Placement/TypePlacement.cs:252:24`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-placement`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-41/generator-generation-placement/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Placement.' 'Generation/Placement/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-41/generator-generation-placement/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Placement.' 'Generation/Placement/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-41/generator-generation-placement/before/reports/mutation-report.json artifacts/test-cleanup/phase-41/generator-generation-placement/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-41/generator-generation-placement/before/reports/mutation-report.json TypePlacementTests.Of_Protected_IsAnErrorOnlyWhereCSharpForbidsIt
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-41/generator-generation-placement/before/reports/mutation-report.json TypePlacementTests.Of_NestedClassNamedLikeAMemberOfTheType_ReportsSqlsrc007OnceAndDropsItsFiles
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Generation-2-12: `src/SqlSource/Generation/Placement/PlacementChecks.cs:118:43`
- F-SqlSource.Tests.Generation-2-13: `src/SqlSource/Generation/Placement/TypePlacement.cs:252:24`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-placement`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-41/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-41/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-41/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-41/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-41/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-41/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
