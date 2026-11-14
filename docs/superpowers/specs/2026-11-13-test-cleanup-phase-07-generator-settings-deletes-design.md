# Test cleanup phase 07: tests/SqlSource.Tests/Settings, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Settings`
- Findings: 1 (1 delete)
- Kind: one kind, delete
- Changed lines: about 13
- Guard: mutation (generator-settings)

## Files

- `tests/SqlSource.Tests/Settings/ColumnTypes/ColumnTypeMapTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Settings-1-02

- test: `tests/SqlSource.Tests/Settings/ColumnTypes/ColumnTypeMapTests.cs` | `ColumnTypeMapTests.Find_HigherPlaceWins_OverASchemaBelow` | rows: all
- action: delete
- evidence: measured (kills 55, alone 0, cover `ColumnTypeMapTests.Find_HeaderWins_OverASchemaInASettingsFile`, which in turn has this test as its cover); read (ColumnTypeMapTests.cs:50-60 puts a header's two-part `users.id` over a settings file's three-part `auth.users.id` and finds the header's; ColumnTypeMapTests.cs:64-74 does the same with `public.users.id` in the project's settings file in place of the root's, so both prove that the header's place beats the schema below; no comment above this test; `grep -rn` of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ names neither the test nor `ColumnTypeMapTests`); blind: no
- cover: `tests/SqlSource.Tests/Settings/ColumnTypes/ColumnTypeMapTests.cs` | `ColumnTypeMapTests.Find_HeaderWins_OverASchemaInASettingsFile`
- proposal: Delete it, keeping `Find_HeaderWins_OverASchemaInASettingsFile`, whose comment states the rule.
- verdict: confirmed
- verifier's reason: read ColumnTypeMapTests.cs:49-60 and 62-74: both put the header's two-part `users.id` over a settings file's three-part key (`auth.users.id` in the root file at depth 0, `public.users.id` in the project's file at depth 1, tests/SqlSource.Tests/Settings/TestPlaces.cs:8-10) and find the header's entry; the depth plays no part, since `SettingPlaces.Specificity` puts the header above every settings file (src/SqlSource/Settings/SettingPlaces.cs:13-15).  Ran `kills` in generator-settings for both: the same 55 mutants each, the difference of the kill sets empty, and the one timeout under it, TypeKeyText.cs:202:17, is under the cover too.  No comment above `Find_HigherPlaceWins_OverASchemaBelow`; grep of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred names neither the test nor `ColumnTypeMapTests` (only a plan of docs/superpowers/plans does, which is no record).  The kept test is the commented one.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-settings`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-07/generator-settings/before --tests 'FullyQualifiedName~SqlSource.Tests.Settings.' 'Settings/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-07/generator-settings/after --tests 'FullyQualifiedName~SqlSource.Tests.Settings.' 'Settings/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-07/generator-settings/before/reports/mutation-report.json artifacts/test-cleanup/phase-07/generator-settings/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-07/generator-settings/before/reports/mutation-report.json ColumnTypeMapTests.Find_HigherPlaceWins_OverASchemaBelow
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-settings`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-07/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-07/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-07/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-07/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-07/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-07/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tests.Settings-1-03 (keep, `ColumnTypeMapTests.Over_OneSideNull_IsTheOther`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
