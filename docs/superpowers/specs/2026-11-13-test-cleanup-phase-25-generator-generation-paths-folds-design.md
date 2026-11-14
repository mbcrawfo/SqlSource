# Test cleanup phase 25: tests/SqlSource.Tests/Generation/Paths, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Paths`
- Findings: 1 (1 fold)
- Kind: one kind, fold
- Changed lines: about 8
- Guard: mutation (generator-generation-paths)

## Files

- `tests/SqlSource.Tests/Generation/Paths/PathResolverTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-2-09

- test: `tests/SqlSource.Tests/Generation/Paths/PathResolverTests.cs` | `PathResolverTests.FindFiles_SourceOfAnAttribute_GivesTheFilesItNames` | rows: `(null, "app/Repo/UserRepository.sql")` at line 274, `(".", ...)` at lines 275-279, `("../Queries", ...)` at line 280, `("users.SQL", ...)` at line 281, `("Missing.sql", "")` at line 282
- action: fold
- evidence: measured (6 rows, kills 52, alone 1, rows needed 5, timeouts under it 2; its alone kill, src/SqlSource/Generation/Paths/PathResolver.cs:104:9 Block removal mutation, comes from row `("../../..", "")` at line 283, the one row whose source leaves the root without naming a `.sql` file); read (`Resolve` calls `FindFiles(type.FilePath, type.Source, type.Name, sqlPaths)`, PathResolver.cs:49, and `TestModels.Type` gives the file `/app/Repo/UserRepository.cs` and the name `UserRepository`, the arguments this theory passes, with the same `SqlPaths`; each named row is a Resolve test that asserts the same files exactly and the diagnostics besides: `Resolve_NoSource_TakesTheFileNamedForTheType` (line 25), `Resolve_SourceDot_TakesTheFilesInTheFolderOfTheSourceFileAndNotItsSubfolders` (line 39), `Resolve_FolderSource_TakesTheFilesInThatFolder` row `("../Queries")` (line 58), `Resolve_FileSource_TakesThatFile` row `("users.SQL", ...)` (line 77), `Resolve_SourceThatMatchesNothing_IsAnErrorAtTheAttribute` row `("Missing.sql")` (line 96); the timeouts, on SqlPath.cs:100 and 283, lie under those Resolve tests too; no comment above the test, and no record document names it or PathResolverTests); blind: no
- cover: `tests/SqlSource.Tests/Generation/Paths/PathResolverTests.cs` | `PathResolverTests.Resolve_NoSource_TakesTheFileNamedForTheType`; `tests/SqlSource.Tests/Generation/Paths/PathResolverTests.cs` | `PathResolverTests.Resolve_SourceDot_TakesTheFilesInTheFolderOfTheSourceFileAndNotItsSubfolders`; `tests/SqlSource.Tests/Generation/Paths/PathResolverTests.cs` | `PathResolverTests.Resolve_FolderSource_TakesTheFilesInThatFolder`; `tests/SqlSource.Tests/Generation/Paths/PathResolverTests.cs` | `PathResolverTests.Resolve_FileSource_TakesThatFile`; `tests/SqlSource.Tests/Generation/Paths/PathResolverTests.cs` | `PathResolverTests.Resolve_SourceThatMatchesNothing_IsAnErrorAtTheAttribute`
- proposal: Drop the five rows that repeat a Resolve test, keeping row `("../../..", "")` at line 283, which may then become a fact.
- verdict: confirmed
- verifier's reason: Resolve calls `FindFiles(type.FilePath, type.Source, type.Name, sqlPaths)` (PathResolver.cs:49).  `TestModels.Type` gives `/app/Repo/UserRepository.cs` (TestModels.cs:18, 41) and the name `UserRepository` (TestModels.cs:54, TargetType.cs:62), which are the theory's arguments, with the same `SqlPaths`.  Each dropped row matches a Resolve test that asserts the same files and also the diagnostics: :25, :39, :58, :77 and :96.  The cover's kills were checked: the kills of the FindFiles theory, less those of the five Resolve tests, leave only `PathResolver.cs:104:9 Block removal`, which the kept row `("../../..", "")` (:283) gives.  Both timeouts (SqlPath.cs:100:13, 283:17) are also under `Resolve_SourceDot_...` and `Resolve_FolderSource_...`.  No comment and no record document applies; the tool's use of FindFiles (ListedFiles.cs:46) keeps the kept row as a test of FindFiles itself.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-paths`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-25/generator-generation-paths/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Paths.' 'Generation/Paths/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-25/generator-generation-paths/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Paths.' 'Generation/Paths/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-25/generator-generation-paths/before/reports/mutation-report.json artifacts/test-cleanup/phase-25/generator-generation-paths/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-25/generator-generation-paths/before/reports/mutation-report.json PathResolverTests.FindFiles_SourceOfAnAttribute_GivesTheFilesItNames
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-paths`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-25/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-25/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-25/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-25/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-25/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-25/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
