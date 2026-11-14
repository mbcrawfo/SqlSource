# Test cleanup phase 20: tests/SqlSource.Tests/Generation, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation`
- Findings: 1 (1 fold)
- Kind: one kind, fold
- Changed lines: about 7
- Guard: mutation (generator-generation), with covers and run time for 1 finding

## Files

- `tests/SqlSource.Tests/Generation/FileParseInputTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-1-14

- test: `tests/SqlSource.Tests/Generation/FileParseInputTests.cs` | `FileParseInputTests.Resolve_CommentsWanted_IsTheChainsStateOfKeepComments` | rows: `("sort-input keep-comments", true)` at line 43, `("keep-comments=on default", true)` at line 44, `("sort-input", false)` at line 46, `("default", false)` at line 48
- action: fold
- evidence: measured (7 rows, covers 1 mutants and kills none); read (`Resolve` takes `chain.Level.KeepsComments` and nothing else, src/SqlSource/Generation/FileParseInput.cs:37; `KeepsComments` is src/SqlSource/Settings/SettingsLevel.cs:97, outside the scope, so the rows are unmeasured there and test the reading and resolving of a parameter list: a word beside others is `GeneratorParameterListTests.ReadElements_Words_NameTheirStates`, `default` beside a word is `ReadElements_DefaultBesideOtherWords_ResetsThenAppliesThem` with the row `["keep-comments", "default"]`, a reset resolving to the defaults is `GeneratorParameterChoicesTests.Reset_Resolves_ToTheDefaults`, and a level that sorts but does not keep comments is `QuerySettingsTests.KeepsComments_OfALevel_IsTheLevelsResolvedKeepComments`, tests/SqlSource.Tests/Settings/QuerySettingsTests.cs:115-119; the rows `keep-comments`, `keep-comments=off` and `null` still prove that Resolve takes the chain's state, both ways, and the default of no chain; the comments above the rows only group them; no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names the test or FileParseInputTests); blind: no
- cover: `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs` | `QuerySettingsTests.KeepsComments_OfALevel_IsTheLevelsResolvedKeepComments`; `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs` | `GeneratorParameterListTests.ReadElements_DefaultBesideOtherWords_ResetsThenAppliesThem`; `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs` | `GeneratorParameterListTests.ReadElements_Words_NameTheirStates`; `tests/SqlSource.Tests/Settings/GeneratorParameterChoicesTests.cs` | `GeneratorParameterChoicesTests.Reset_Resolves_ToTheDefaults`
- proposal: Drop the four rows named; keep `keep-comments`, `keep-comments=off` and `null`.
- verdict: confirmed
- verifier's reason: `Resolve` takes `chain.Level.KeepsComments` alone, src/SqlSource/Generation/FileParseInput.cs:37, so the four rows test the reading and resolving of the list, which is outside the scope.  `kills` (generator-generation) shows 0 kills and no timeouts.  Each row has a cover:
  - `sort-input keep-comments` is `GeneratorParameterListTests.ReadElements_Words_NameTheirStates` (keep-comments beside other words), with `KeepsComments` true in tests/SqlSource.Tests/Settings/QuerySettingsTests.cs:115-119.
  - `keep-comments=on default` is `ReadElements_DefaultBesideOtherWords_ResetsThenAppliesThem`, whose rows `["keep-comments", "default"]` and `["default", " Keep-Comments=ON "]` give Reset with KeepComments on.
  - `sort-input` is `Sort.KeepsComments.ShouldBeFalse()` in QuerySettingsTests.
  - `default` is `ReadElements_Default_IsTheReset` with `Reset_Resolves_ToTheDefaults`, whose defaults leave KeepComments out.
  The comments above the rows, tests/SqlSource.Tests/Generation/FileParseInputTests.cs:41-49, only group them, and no listed document names the test.  The kept rows still prove that Resolve takes the chain's state both ways, and the default when there is no chain.  The rows share a name, so a filter cannot measure the fold: this is confirmed by reading, and the guard of the phase that drops the rows proves it.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-20/generator-generation/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.&FullyQualifiedName!~SqlSource.Tests.Generation.Chains.&FullyQualifiedName!~SqlSource.Tests.Generation.Converters.&FullyQualifiedName!~SqlSource.Tests.Generation.Emitting.&FullyQualifiedName!~SqlSource.Tests.Generation.Exceptions.&FullyQualifiedName!~SqlSource.Tests.Generation.Methods.&FullyQualifiedName!~SqlSource.Tests.Generation.Models.&FullyQualifiedName!~SqlSource.Tests.Generation.Paths.&FullyQualifiedName!~SqlSource.Tests.Generation.Placement.&FullyQualifiedName!~SqlSource.Tests.Generation.Repositories.&FullyQualifiedName!~SqlSource.Tests.Generation.Sidecars.&FullyQualifiedName!~SqlSource.Tests.Generation.Types.' 'Generation/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-20/generator-generation/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.&FullyQualifiedName!~SqlSource.Tests.Generation.Chains.&FullyQualifiedName!~SqlSource.Tests.Generation.Converters.&FullyQualifiedName!~SqlSource.Tests.Generation.Emitting.&FullyQualifiedName!~SqlSource.Tests.Generation.Exceptions.&FullyQualifiedName!~SqlSource.Tests.Generation.Methods.&FullyQualifiedName!~SqlSource.Tests.Generation.Models.&FullyQualifiedName!~SqlSource.Tests.Generation.Paths.&FullyQualifiedName!~SqlSource.Tests.Generation.Placement.&FullyQualifiedName!~SqlSource.Tests.Generation.Repositories.&FullyQualifiedName!~SqlSource.Tests.Generation.Sidecars.&FullyQualifiedName!~SqlSource.Tests.Generation.Types.' 'Generation/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-20/generator-generation/before/reports/mutation-report.json artifacts/test-cleanup/phase-20/generator-generation/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-20/generator-generation/before/reports/mutation-report.json FileParseInputTests.Resolve_CommentsWanted_IsTheChainsStateOfKeepComments
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-20/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-20/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-20/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-20/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-20/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-20/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests.Generation-1-14.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tests.Generation-1-14 | fold | `FileParseInputTests.Resolve_CommentsWanted_IsTheChainsStateOfKeepComments` (rows: `("sort-input keep-comments", true)` at line 43, `("keep-comments=on default", true)` at line 44, `("sort-input", false)` at line 46, `("default", false)` at line 48) | `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`: `QuerySettingsTests.KeepsComments_OfALevel_IsTheLevelsResolvedKeepComments`; `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs`: `GeneratorParameterListTests.ReadElements_DefaultBesideOtherWords_ResetsThenAppliesThem`; `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs`: `GeneratorParameterListTests.ReadElements_Words_NameTheirStates`; `tests/SqlSource.Tests/Settings/GeneratorParameterChoicesTests.cs`: `GeneratorParameterChoicesTests.Reset_Resolves_ToTheDefaults` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
