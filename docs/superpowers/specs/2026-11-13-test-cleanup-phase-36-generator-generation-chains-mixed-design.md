# Test cleanup phase 36: tests/SqlSource.Tests/Generation/Chains, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Chains`
- Findings: 5 (3 delete, 2 strengthen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 58
- Guard: mutation (generator-generation-chains)

## Files

- `tests/SqlSource.Tests/Generation/Chains/ListedSettingsFileTests.cs`
- `tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-1-07

- test: `tests/SqlSource.Tests/Generation/Chains/ListedSettingsFileTests.cs` | `ListedSettingsFileTests.Read_InvalidDatabase_IsReportedByTheToolAndNotByTheBuild` | rows: all
- action: delete
- evidence: measured (kills 19, alone 0, timeouts under it 0; cover: `ListedSettingsFileTests.Read_InvalidValue_IsReportedByTheSidesThatReadItsSetting`); read (the theory's row `{ SettingsFileKeys.Database, "\" a b \"", false, true }`, ListedSettingsFileTests.cs:115, reads the same text on both sides and asserts no diagnostic from the build and one from the tool; this test adds only the id SQLSRC019, which is `SettingsFileDiagnosticsTests.Create_EachKind_GivesItsDescriptor` row `(nameof(SettingsFileErrorKind.InvalidValue), "SQLSRC019")`, tests/SqlSource.Tests/Diagnostics/SettingsFileDiagnosticsTests.cs:26; no comment above the test; no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names it or ListedSettingsFileTests); blind: no
- cover: `tests/SqlSource.Tests/Generation/Chains/ListedSettingsFileTests.cs` | `ListedSettingsFileTests.Read_InvalidValue_IsReportedByTheSidesThatReadItsSetting` row `{ SettingsFileKeys.Database, "\" a b \"", false, true }`; `tests/SqlSource.Tests/Diagnostics/SettingsFileDiagnosticsTests.cs` | `SettingsFileDiagnosticsTests.Create_EachKind_GivesItsDescriptor`
- proposal: Delete it; the theory's database row holds the split between the sides, and the descriptor test holds the id.
- verdict: confirmed
- verifier's reason: The test, tests/SqlSource.Tests/Generation/Chains/ListedSettingsFileTests.cs:96-107, reads `{"database": " a b "}` on both sides.  The theory row at 115 reads the same text and asserts 0 diagnostics from the build and 1 from the tool (161-162).
  - The only extra assertion is the id `SQLSRC019`, which other tests already hold.  The reader making ` a b ` for `database` an `InvalidValue` is tests/SqlSource.Tests/SettingsFiles/SettingsFileReaderTests.cs:298 (`Read_ValueThatIsNotValid_IsInvalidValueAndSetsNothing`).  `InvalidValue` mapping to SQLSRC019 is tests/SqlSource.Tests/Diagnostics/SettingsFileDiagnosticsTests.cs:26.
  - ListedSettingsFile.cs:104-107 only chooses errors by side and passes them to `SettingsFileDiagnostics.Create`.
  - `kills` (generator-generation-chains): 19 kills, 0 timeouts.  Every kill is in the theory's own kills (compared with `comm`).
  - No comment above the test.  The only documents that name the test or its class are under docs/superpowers/plans, which are not among the documents the brief lists.

### F-SqlSource.Tests.Generation-1-08

- test: `tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs` | `SettingsFileSetTests.ChainOf_FileThatIsNotJson_IsPassedThrough` | rows: all
- action: delete
- evidence: measured (kills 37, alone 0, timeouts under it 0; cover: `SettingsFileSetTests.ChainOf_FileEditedToTextThatIsNotJson_PassesThrough`); read (the cover's `after` set, SettingsFileSetTests.cs:343-348, puts a file that is not JSON under one that sets the output and asserts that the farther file's output and suffix reach the path, which is this test's check, SettingsFileSetTests.cs:84-95, and more, since its broken text even says `root: true`; no comment above the test; tests/AGENTS.md and src/SqlSource/Generation/Chains/AGENTS.md name SettingsFileSetTests only for the allocation test, and no readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names it); blind: no
- cover: `tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs` | `SettingsFileSetTests.ChainOf_FileEditedToTextThatIsNotJson_PassesThrough`
- proposal: Delete it; the edited-file test proves a file that is not JSON is passed through.
- verdict: confirmed
- verifier's reason: The test, tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs:83-95, asserts that a file that is not JSON, under a parent that sets `output`, lets the parent's output through.  The cover's `after` set, 343-348, does the same with a broken text that even holds `root: true`, and asserts the output and the suffix as well.
  - `kills` (generator-generation-chains): 37 kills, 0 timeouts, all inside the cover's kills (compared with `comm`).  Each kill can be explained from the inputs: Read's guards, the ordering in Of, and the parent's chain at line 65.
  - No comment above the test.  tests/AGENTS.md and src/SqlSource/Generation/Chains/AGENTS.md name `SettingsFileSetTests` only for the allocation test.  No other listed document names the test.

### F-SqlSource.Tests.Generation-1-09

- test: `tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs` | `SettingsFileSetTests.ChainOf_PathInAnotherCaseOrWithBackslashes_FindsTheChain` | rows: all
- action: delete
- evidence: measured (kills 30, alone 0, timeouts under it 1; cover: `SettingsFileSetTests.ChainOf_FileEditedToTextThatIsNotJson_PassesThrough`); read (the test normalises the query's path itself with `SqlPath.Normalize` before `ChainOf`, SettingsFileSetTests.cs:182, so `ChainOf` never sees a backslash; the settings file's backslashes become `NormalizedFolder` in `ListedSettingsFile.Read`, which `ListedSettingsFileTests.Read_PathWithBackslashes_IsReadAtItsNormalisedFolder` asserts as `C:/App/Queries`, ListedSettingsFileTests.cs:66-76; a path in another case finding its chain is `ChainOf_FoldersNotInAscii_AreFoundInTheOrderTheyAreSorted`, whose `ÄPP/queries/a.sql` finds `Äpp` and `äpp/Queries`, SettingsFileSetTests.cs:259-262; the timeout under it is SettingsFileSet.cs:179, in `NearestIndex`, which every `ChainOf` test runs, the cover among them; no comment above the test, and no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names it); blind: no
- cover: `tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs` | `SettingsFileSetTests.ChainOf_FoldersNotInAscii_AreFoundInTheOrderTheyAreSorted`; `tests/SqlSource.Tests/Generation/Chains/ListedSettingsFileTests.cs` | `ListedSettingsFileTests.Read_PathWithBackslashes_IsReadAtItsNormalisedFolder`
- proposal: Delete it; the case is held by the non-ASCII test, and the backslashes by the reading of the file and by `SqlPath.Normalize`'s own tests.
- verdict: needs measurement; measured: confirmed | nothing lost (131 detected before, 131 still detected, 0 lost, 0 not comparable)
- verifier's reason: The kills are settled, but a timeout under the test is under neither named cover, so the kill matrix cannot settle the delete.
  - By reading, the covers hold the behaviour.  The test normalises the query path itself, tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs:182.  `ChainOf_FoldersNotInAscii_AreFoundInTheOrderTheyAreSorted` (257-263) finds `äpp/Queries` from `ÄPP/queries`, which differ in ASCII case too.  `Read_PathWithBackslashes_IsReadAtItsNormalisedFolder` gives `C:/App/Queries`, ListedSettingsFileTests.cs:66-76.
  - The 30 kills of the generator-generation-chains `kills` lie inside the two named covers' kills (compared with `comm`).
  - The timeout is TIMEOUT src/SqlSource/Generation/Chains/SettingsFileSet.cs:179:57 Arithmetic mutation `length + 1`.  It is under neither named cover: `kills` gives `ChainOf_FoldersNotInAscii_AreFoundInTheOrderTheyAreSorted` 0 timeouts, since all its lookups succeed on the first search, and the ListedSettingsFile test never reaches SettingsFileSet.  The finding's evidence says "the cover among them", which is wrong: the measured cover `ChainOf_FileEditedToTextThatIsNotJson_PassesThrough` also has 0 timeouts.
  - The mutant is probably an endless loop that any test running line 179 hangs on.  `ChainOf_FolderWithNoFileAbove_IsNone`, `ChainOf_FileAtTheRoot_AppliesToEveryFolder` and `ChainOf_FilesOfOneFolder_ShareOneChain` all carry it.  A run without this test should confirm it.
  - Scope to run again: generator-generation-chains, without SqlSource.Tests.Generation.Chains.SettingsFileSetTests.ChainOf_PathInAnotherCaseOrWithBackslashes_FindsTheChain.
- condition: Measured again: the scope without this test lost nothing (131 detected before and after, 0 lost, 0 not comparable).  The verifier corrects the evidence: the measured cover `ChainOf_FileEditedToTextThatIsNotJson_PassesThrough` has no timeouts either, so "the cover among them" is wrong; the delete stands.

### F-SqlSource.Tests.Generation-1-01

- test: `tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs` | `SettingsFileSetTests.Reached_FileBeyondEveryRoot_IsNotReached` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/Generation/Chains/SettingsFileSet.cs:108:62 UnaryMinusExpression to UnaryPlusExpression mutation `-1` to `+1`, covered by this test, `FilesIn_FoldersThatAreReached_AreTheirFilesInTheOrderOfTheSet` and `Reached_FileReachedThroughASiblingFolder_IsReached`, none killing it); read (SettingsFileSet.cs:108 ends a chain at a root file by setting the index to -1; the mutant sends the walk on to file 1 instead; in this test the set sorts as `""`, `app`, so the root file `app` is file 1 itself and the mutant's step lands on a file already reached, SettingsFileSetTests.cs:384-395; the two other tests that reach the line also hold their root at index 1, `a`/`a/b`, SettingsFileSetTests.cs:400-409 and 431-443; the test's name promises that nothing beyond a root is reached, which the mutant breaks whenever the root is not file 1); blind: no
- cover: none
- proposal: Add a settings file that sorts between the root's parent and the root, such as `("/a/sqlsource.json", "{}")`, so that the root file `app` is file 2 and the expected `["app"]` fails when the walk goes on to file 1.
- verdict: confirmed
- verifier's reason: `Reached` sets the index to -1 after a root file, src/SqlSource/Generation/Chains/SettingsFileSet.cs:108; in the test the set sorts `""` (0), `app` (1), so the `+1` mutant steps to file 1, which is already reached, and the loop ends with the same `["app"]`, tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs:384-395.  The other two tests that reach the line also put their root at index 1: `a`/`a/b` at 400-409, and `a`, `a/b`, `b`, `c` at 431-443.  `kills` (generator-generation-chains) lists 108:62 as SURVIVED under this test.  With the proposed `("/a/sqlsource.json", "{}")` the set sorts `""`, `a`, `app`; the mutant then walks to `a` and on to `""`, and gives `["", "a", "app"]`, so the expected `["app"]` fails.
- survivors to kill: `src/SqlSource/Generation/Chains/SettingsFileSet.cs:108:62`

### F-SqlSource.Tests.Generation-1-03

- test: `tests/SqlSource.Tests/Generation/Chains/ListedSettingsFileTests.cs` | `ListedSettingsFileTests.Read_FileNotNamedSettingsFile_IsNull` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/Generation/Chains/ListedSettingsFile.cs:47:16 Equality mutation `before <= 0`, covered by 34 methods here, none killing it); read (ListedSettingsFile.cs:46-47 takes the character before `sqlsource.json` and accepts the path when there is none or it is a separator; with `<= 0` a path with exactly one character before the name, `xsqlsource.json`, is taken as a settings file; the rows, ListedSettingsFileTests.cs:47-49, have no path whose name has one character before it, and the test asserts `IsSettingsFile` directly, so a row of a bare relative name pins the bound); blind: no (line 47 is also listed blind for other mutants, which do not compile; this one compiled and survived)
- cover: none
- proposal: Add the row `[InlineData("xsqlsource.json")]`, for which `IsSettingsFile` is false and `Read` is null.
- verdict: confirmed
- verifier's reason: src/SqlSource/Generation/Chains/ListedSettingsFile.cs:46-47 accepts a path when `before < 0` or the character at `before` is a separator.  `xsqlsource.json` has `before == 0` and `path[0] == 'x'`, so the original gives false and `before <= 0` gives true.  None of the rows at tests/SqlSource.Tests/Generation/Chains/ListedSettingsFileTests.cs:47-49 has exactly one character before the name.  The test asserts `IsSettingsFile` and a null `Read` directly (52-53), and the name promises that a file not named `sqlsource.json` is not read.
- survivors to kill: `src/SqlSource/Generation/Chains/ListedSettingsFile.cs:47:16`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-chains`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-36/generator-generation-chains/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Chains.' 'Generation/Chains/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-36/generator-generation-chains/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Chains.' 'Generation/Chains/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-36/generator-generation-chains/before/reports/mutation-report.json artifacts/test-cleanup/phase-36/generator-generation-chains/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-36/generator-generation-chains/before/reports/mutation-report.json SettingsFileSetTests.Reached_FileBeyondEveryRoot_IsNotReached
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-36/generator-generation-chains/before/reports/mutation-report.json ListedSettingsFileTests.Read_FileNotNamedSettingsFile_IsNull
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-36/generator-generation-chains/before/reports/mutation-report.json ListedSettingsFileTests.Read_InvalidDatabase_IsReportedByTheToolAndNotByTheBuild
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-36/generator-generation-chains/before/reports/mutation-report.json SettingsFileSetTests.ChainOf_FileThatIsNotJson_IsPassedThrough
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-36/generator-generation-chains/before/reports/mutation-report.json SettingsFileSetTests.ChainOf_PathInAnotherCaseOrWithBackslashes_FindsTheChain
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Generation-1-01: `src/SqlSource/Generation/Chains/SettingsFileSet.cs:108:62`
- F-SqlSource.Tests.Generation-1-03: `src/SqlSource/Generation/Chains/ListedSettingsFile.cs:47:16`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-chains`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-36/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-36/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-36/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-36/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-36/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-36/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `src/SqlSource/Generation/Chains/AGENTS.md`: 6
- `tests/AGENTS.md`: 44

## Left out

- F-SqlSource.Tests.Generation-1-15 (keep, `SettingsFileSetTests.ChainOf_FolderWithNoFileAbove_IsNone`): a **keep**; the test stays as it is.
- F-SqlSource.Tests.Generation-1-16 (keep, `SettingsFileSetTests.ChainOf_RootFalse_GoesOn`): a **keep**; the test stays as it is.
- F-SqlSource.Tests.Generation-1-17 (keep, `SettingsFileSetTests.Of_SameFiles_AreEqual`): a **keep**; the test stays as it is.
- F-SqlSource.Tests.Generation-1-18 (keep, `ListedSettingsFileTests.Read_PathThatDoesNotNormalise_IsNull`): a **keep**; the test stays as it is.
- F-SqlSource.Tests.Generation-1-19 (keep, `ListedSettingsFileTests.Read_PathWithBackslashes_IsReadAtItsNormalisedFolder`): a **keep**; the test stays as it is.
- F-SqlSource.Tests.Generation-1-20 (keep, `ListedSettingsFileTests.Read_TextTheCompilerCannotGive_IsInvalidJson`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
