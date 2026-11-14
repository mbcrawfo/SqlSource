# Test cleanup phase 61: tests/SqlSource.Tool.Tests/Projects, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Projects`
- Findings: 3 (3 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 24
- Guard: mutation (tool-projects)

## Files

- `tests/SqlSource.Tool.Tests/Projects/MSBuildPropertyTests.cs`
- `tests/SqlSource.Tool.Tests/Projects/ProjectEvaluatorTests.cs`
- `tests/SqlSource.Tool.Tests/Projects/RunUnitTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Projects-1-01

- test: `tests/SqlSource.Tool.Tests/Projects/MSBuildPropertyTests.cs` | `MSBuildPropertyTests.Escape_Value_IsWhatMSBuildReadsBackWhole` | rows: all
- action: strengthen
- evidence: measured (7 rows; kills 8, alone 0; survivor `src/SqlSource.Tool/Projects/MSBuildProperty.cs:25:9 Equality mutation` -> `value.AsSpan().IndexOfAny('%', ';', ',') <= 0`, covered by 46 methods); read (MSBuildPropertyTests.cs:10-17: no row's value starts with `%`, `;` or `,`, so the mutant, which returns such a value unescaped, passes every row; MSBuildProperty.cs:25 is the short cut that the name promises is safe for every value).  The other survivor at 25:9, Conditional (false), always escapes and gives the same text, so it is equivalent and asks nothing of the test.  blind: no
- cover: none
- proposal: Add a row whose value starts with one of the three characters, such as `(";a,b", "%3Ba%2Cb")` or `("%41", "%2541")`, which fails under the `<= 0` mutant.
- verdict: confirmed
- verifier's reason: MSBuildProperty.cs:25 takes the short cut on `IndexOfAny('%', ';', ',') < 0`; every row of MSBuildPropertyTests.cs:10-17 starts with `/`, `C` or is empty, so the `<= 0` mutant, which returns a value whose first character is `%`, `;` or `,` unescaped, passes all seven rows.  `kills` on tool-projects lists exactly 8 kills (25:9 Conditional true, 25:9 `> 0`, the six string mutants of :28-30), all explained by the rows, and SURVIVED for 25:9 `<= 0` and 25:9 Conditional (false); the latter always escapes and gives the same text, so it is equivalent.  The proposed row `(";a,b", "%3Ba%2Cb")` would fail under `<= 0`.
- survivors to kill: `src/SqlSource.Tool/Projects/MSBuildProperty.cs:25:9`

### F-SqlSource.Tool.Tests.Projects-1-02

- test: `tests/SqlSource.Tool.Tests/Projects/ProjectEvaluatorTests.cs` | `ProjectEvaluatorTests.Evaluate_NoProjects_StartsNothingAndMakesNoFolder` | rows: all
- action: strengthen
- evidence: measured (covers 1 mutant and kills none; survivor `src/SqlSource.Tool/Projects/ProjectEvaluator.cs:89:9 Block removal mutation` -> `{}`, covered by 7 methods); read (ProjectEvaluator.cs:88-91 returns before the run's folder is made; without it the run makes the folder at :94-96, asks nothing and deletes it at :125, so `Directory.EnumerateFileSystemEntries(_temp).ShouldBeEmpty()` at ProjectEvaluatorTests.cs:473 holds either way, and "MakesNoFolder" is not checked).  blind: no
- cover: none
- proposal: Give the host a temporary folder in which no folder can be made, a path below an existing file, so that the run passes only if it makes none; keep the assertions that nothing is started and the result is empty.
- verdict: confirmed
- verifier's reason: ProjectEvaluator.cs:88-91 returns `[]` before the folder is made; with the block removed, :94-96 creates the folder, `Parallel.ForEachAsync` over an empty range asks nothing, and the `finally` at :123-126 calls `Delete(folder)` (:448-458, recursive), so `_temp` is empty again and ProjectEvaluatorTests.cs:470-473 holds either way.  `kills` on tool-projects: 0 kills, the one SURVIVED is 89:9 Block removal.  The "MakesNoFolder" of the name is not checked; a host temp folder in which no folder can be made would make the mutant throw.
- survivors to kill: `src/SqlSource.Tool/Projects/ProjectEvaluator.cs:89:9`

### F-SqlSource.Tool.Tests.Projects-1-03

- test: `tests/SqlSource.Tool.Tests/Projects/RunUnitTests.cs` | `RunUnitTests.Find_DirectoryThatCannotBeRead_IsSqlsrc222` | rows: all
- action: strengthen
- evidence: measured (kills 5, alone 0; survivor `src/SqlSource.Tool/Projects/RunUnitFinder.cs:74:17 Conditional (true) mutation` -> `(true?"the system would not list its files" :exception.Messa…`, covered only by this test and `Find_DirectoryWithAnEqualsSignThatCannotBeRead_IsSqlsrc222WithoutTheSystemsMessage`); read (RunUnitTests.cs:299 asserts only `" cannot be read: "`, which the fixed text also satisfies, while its sibling at :309-327 is named for the case without the system's message; RunUnitFinder.cs:70-74 gives the system's message for a path printed in quotes).  The mutant was tested, though the evidence lists RunUnitFinder.cs:56-76 as blind for mutants that do not compile.  blind: no
- cover: none
- proposal: Assert that the reason of a directory named in quotes is not the fixed text, `ShouldNotContain("the system would not list its files")`, so the test guards the branch its sibling's name sets it against.
- verdict: confirmed
- verifier's reason: RunUnitFinder.cs:70-74 gives `exception.Message` for an argument that may be printed as a path and the fixed text otherwise; RunUnitTests.cs:284-305 asserts only SQLSRC222, the path and `" cannot be read: "`, which the fixed text satisfies too, so the Conditional (true) mutant at 74:17 survives (`kills` on tool-projects: 5 kills, all of the path-resolution and catch lines :24, :30, :33, :67, each explicable from a plain relative directory that cannot be listed; SURVIVED 74:17 Conditional true).  The sibling at :307-327 is named for the case without the system's message, so this test is the only one that could guard the other branch; `ShouldNotContain("the system would not list its files")` kills it.
- survivors to kill: `src/SqlSource.Tool/Projects/RunUnitFinder.cs:74:17`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-projects`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-61/tool-projects/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Projects.' 'Projects/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-61/tool-projects/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Projects.' 'Projects/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-61/tool-projects/before/reports/mutation-report.json artifacts/test-cleanup/phase-61/tool-projects/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-61/tool-projects/before/reports/mutation-report.json MSBuildPropertyTests.Escape_Value_IsWhatMSBuildReadsBackWhole
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-61/tool-projects/before/reports/mutation-report.json ProjectEvaluatorTests.Evaluate_NoProjects_StartsNothingAndMakesNoFolder
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-61/tool-projects/before/reports/mutation-report.json RunUnitTests.Find_DirectoryThatCannotBeRead_IsSqlsrc222
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests.Projects-1-01: `src/SqlSource.Tool/Projects/MSBuildProperty.cs:25:9`
- F-SqlSource.Tool.Tests.Projects-1-02: `src/SqlSource.Tool/Projects/ProjectEvaluator.cs:89:9`
- F-SqlSource.Tool.Tests.Projects-1-03: `src/SqlSource.Tool/Projects/RunUnitFinder.cs:74:17`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-projects`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-61/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-61/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-61/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-61/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-61/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-61/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
