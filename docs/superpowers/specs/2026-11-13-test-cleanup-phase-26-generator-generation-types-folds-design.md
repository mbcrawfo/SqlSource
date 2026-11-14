# Test cleanup phase 26: tests/SqlSource.Tests/Generation/Types, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Types`
- Findings: 1 (1 fold)
- Kind: one kind, fold
- Changed lines: about 24
- Guard: mutation (generator-generation-types)

## Files

- `tests/SqlSource.Tests/Generation/Types/FileTypesInputTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation.Types-1-09

- test: `tests/SqlSource.Tests/Generation/Types/FileTypesInputTests.cs` | `FileTypesInputTests.TypeSettingsOf_LevelWithOnlyASqlServerTypeOverride_KeepsIt`; `tests/SqlSource.Tests/Generation/Types/FileTypesInputTests.cs` | `FileTypesInputTests.TypeSettingsOf_LevelWithOnlyAPostgreSqlTypeOverride_KeepsIt` | rows: all
- action: fold
- evidence: measured (each kills 5, alone 0, timeouts under it 0, and each is the other's cover) and read: the two tests (FileTypesInputTests.cs:18-33 and 36-51) are the same body with the engines swapped, under one comment, "whichever engine reads it" (line 16); `TypeSettingsOf` compares the whole `PerEngine` to `default` (src/SqlSource/Generation/Types/FileTypesInput.cs:96), and src/SqlSource/Generation/Types/AGENTS.md requires it to carry every engine's slot.  No AGENTS.md, readme, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names either test or the class; blind: no
- cover: `tests/SqlSource.Tests/Generation/Types/FileTypesInputTests.cs` | `FileTypesInputTests.TypeSettingsOf_LevelWithOnlyASqlServerTypeOverride_KeepsIt`; `tests/SqlSource.Tests/Generation/Types/FileTypesInputTests.cs` | `FileTypesInputTests.TypeSettingsOf_LevelWithOnlyAPostgreSqlTypeOverride_KeepsIt`
- proposal: Make them one theory over `DatabaseEngine`, `TypeSettingsOf_LevelWithOnlyOneEnginesTypeOverride_KeepsIt`, that keeps the engine's entries and leaves every other engine's slot null, so a new engine gets its row rather than a third copy.
- verdict: confirmed
- verifier's reason: Read FileTypesInputTests.cs:16-51: the two tests are one body with `DatabaseEngine.SqlServer` and `DatabaseEngine.PostgreSql` swapped, under the comment "whichever engine reads it" (line 16).  `TypeSettingsOf` compares the whole `PerEngine` with `default` (src/SqlSource/Generation/Types/FileTypesInput.cs:96), and src/SqlSource/Generation/Types/AGENTS.md requires it to carry every engine's slot.  A theory with a row for each engine keeps both cases, so nothing is lost and a new engine gets its row; each kills 5 with the other as cover, and no timeouts.  `git log -S` shows the pair came from the closed-sets change (b050fffc, "pin that TypeSettingsOf keeps a type override of either engine"), which names no record document; `grep -rn` of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred finds neither test.  A fold that merges two facts into one theory drops no row, so it is confirmed by reading.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-types`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-26/generator-generation-types/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Types.' 'Generation/Types/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-26/generator-generation-types/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Types.' 'Generation/Types/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-26/generator-generation-types/before/reports/mutation-report.json artifacts/test-cleanup/phase-26/generator-generation-types/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-26/generator-generation-types/before/reports/mutation-report.json FileTypesInputTests.TypeSettingsOf_LevelWithOnlyASqlServerTypeOverride_KeepsIt
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-26/generator-generation-types/before/reports/mutation-report.json FileTypesInputTests.TypeSettingsOf_LevelWithOnlyAPostgreSqlTypeOverride_KeepsIt
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-types`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-26/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-26/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-26/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-26/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-26/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-26/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
