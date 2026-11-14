# Test cleanup phase 02: tests/SqlSource.Tests/Diagnostics, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Diagnostics`
- Findings: 2 (2 delete)
- Kind: one kind, delete
- Changed lines: about 18
- Guard: mutation (generator-diagnostics), with covers and run time for 2 findings

## Files

- `tests/SqlSource.Tests/Diagnostics/SettingsFileDiagnosticsTests.cs`
- `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests-1-01

- test: `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs` | `SqlDiagnosticsTests.LibraryBelowTheFloor_Descriptor_IsAnErrorThatCannotBeConfigured` | rows: all
- action: delete
- evidence: read (SqlDiagnosticsTests.cs:194-203 asserts SQLSRC282's id, its title, severity Error, the NotConfigurable tag and that `All` holds it.  SqlDiagnosticsTests.cs:32-54 asserts severity Error, enabled, category and NotConfigurable for every descriptor of `All` not in `Configurable`, which SQLSRC282 is not.  DiagnosticsDocumentTests.cs:30-31 fails unless `All` holds SQLSRC282, since site/diagnostics.md has its section, and DiagnosticsDocumentTests.cs:47-55 holds the id and the title to the document's row, site/diagnostics.md:257, "A library is older than SqlSource supports".  LibraryDiagnosticsTests.cs:240-274 asserts the build reports this descriptor as `SQLSRC282` with its message.  No other descriptor has a `_Descriptor_` test.  No comment above the test; grep of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred finds no mention of it.  The descriptor is static, reaches no tested mutant); blind: yes
- cover: `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs` | `SqlDiagnosticsTests.All_EveryDescriptor_IsAnErrorThatCannotBeConfiguredButTheListedOnes`; `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs` | `DiagnosticsDocumentTests.Document_Sections_AreTheDescriptorsInOrder`; `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs` | `DiagnosticsDocumentTests.Document_Table_ListsEveryDescriptorWithItsTitleAndAnchor`; `tests/SqlSource.Tests/Generator/LibraryDiagnosticsTests.cs` | `LibraryDiagnosticsTests.Run_NodaTimeBelowTheFloorAndAMethodsUse_Reports282Once`
- proposal: Delete the test.  Its `_Message_` neighbour stays, as the TD-0109 plan decided for every message fact.
- verdict: confirmed
- verifier's reason: SqlDiagnosticsTests.cs:194-203 asserts SQLSRC282's id, title, Error, NotConfigurable and membership of `All`.  Each fact has a cover that I read.  SqlDiagnosticsTests.cs:32-54 holds every descriptor of `All` that is not in `Configurable` (SqlDiagnosticsTests.cs:19-29, which has no SQLSRC282) to Error, enabled, category "SqlSource" and NotConfigurable.  DiagnosticsDocumentTests.cs:30-31 fails unless `All` holds an SQLSRC282, since site/diagnostics.md has its section, and DiagnosticsDocumentTests.cs:47-55 (and :33-42) holds that descriptor's title to "A library is older than SqlSource supports" (site/diagnostics.md:256).  ToolDiagnosticsTests.cs:39-44 keeps ids unique, so the SQLSRC282 of `All` is this descriptor.  LibraryDiagnosticsTests.cs:27-29,240-277 holds the build's report of `SqlDiagnostics.LibraryBelowTheFloor` (SqlSourceGenerator.cs:1121) to `SQLSRC282` with its message.  So removing it from `All`, renumbering it or changing its severity, tag or title fails a cover.  The descriptor is a static field (SqlDiagnostics.cs:1325-1335), and no scope ran a mutant there.  There is no comment above the test or its group.  A grep of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred does not find the test or `LibraryBelowTheFloor`.  The only document that names it is a plan, docs/superpowers/plans/2026-10-30-query-generation-phase-7-2-nodatime-sqlserver.md, which does not make it a record.  src/SqlSource/Diagnostics/AGENTS.md names SQLSRC282 only for its positionless reporting, which LibraryDiagnosticsTests covers.  It is the only `_Descriptor_` test in tests/.

### F-SqlSource.Tests-1-02

- test: `tests/SqlSource.Tests/Diagnostics/SettingsFileDiagnosticsTests.cs` | `SettingsFileDiagnosticsTests.Create_DefaultBesideAnotherParameter_IsNoError` | rows: all
- action: delete
- evidence: read (SettingsFileDiagnosticsTests.cs:93-100 calls only `SettingsFileReader.Read` on `["keep-comments", "Default"]` and asserts no errors; it never calls `SettingsFileDiagnostics.Create`, so the scope of src/SqlSource/Diagnostics never sees it, "reaches no tested mutant".  SettingsFileReaderTests.cs:326-358 reads `["default", "sort-input"]` through the same reader and asserts no errors and the reset with the other word applied; GeneratorParameterListTests.cs:42-56 reads `["keep-comments", "default"]`, the same order as this test, and asserts no invalid and no repeated element, which are what the reader turns into errors; GeneratorParameterListTests.cs:28-40 holds `"DEFAULT"` to be the reset, the case this test writes.  No comment above the test; grep of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred finds no mention of it; it came with the per-option merge, 54b98dc1); blind: no
- cover: `tests/SqlSource.Tests/SettingsFiles/SettingsFileReaderTests.cs` | `SettingsFileReaderTests.Read_GeneratorParameters_FollowTheElementRules`; `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs` | `GeneratorParameterListTests.ReadElements_DefaultBesideOtherWords_ResetsThenAppliesThem`
- proposal: Delete the test; the reader's and the list's tests already prove that a default beside another word is neither an invalid value nor a repeat.
- verdict: confirmed
- verifier's reason: SettingsFileDiagnosticsTests.cs:93-100 calls only `SettingsFileReader.Read` and never `SettingsFileDiagnostics.Create`.  The reader's only source of generator-parameter errors is `ReportParts` over the `invalid` and `repeated` lists of `GeneratorParameterList.ReadElements` (SettingsLevelReader.cs:56-67, GeneratorParameterList.cs:151-196).  The named covers prove the rest.  SettingsFileReaderTests.cs:326-358 sends `["default", "sort-input"]` through the same reader and asserts no errors.  GeneratorParameterListTests.cs:42-56 reads `["keep-comments", "default"]`, the test's order, and `["default", " Keep-Comments=ON "]` (mixed case), with empty `invalid` and `repeated`.  GeneratorParameterListTests.cs:28-40 holds `"DEFAULT"` to be the reset.  Together they cover a capitalised `Default` beside another word.  There is no comment above the test.  A grep of the listed documents finds nothing.  Only plans name it: docs/superpowers/plans/2026-10-26-settings-files-1-reader.md:188, where it began as `_SaysItStandsAlone`, and the per-option merge plan.  Plans are not among the documents that make a test a record.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-diagnostics`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-02/generator-diagnostics/before --tests 'FullyQualifiedName~SqlSource.Tests.Diagnostics.' 'Diagnostics/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-02/generator-diagnostics/after --tests 'FullyQualifiedName~SqlSource.Tests.Diagnostics.' 'Diagnostics/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-02/generator-diagnostics/before/reports/mutation-report.json artifacts/test-cleanup/phase-02/generator-diagnostics/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-02/generator-diagnostics/before/reports/mutation-report.json SqlDiagnosticsTests.LibraryBelowTheFloor_Descriptor_IsAnErrorThatCannotBeConfigured
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-02/generator-diagnostics/before/reports/mutation-report.json SettingsFileDiagnosticsTests.Create_DefaultBesideAnotherParameter_IsNoError
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-diagnostics`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-02/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-02/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-02/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-02/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-02/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-02/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests-1-01, F-SqlSource.Tests-1-02.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tests-1-01 | delete | `SqlDiagnosticsTests.LibraryBelowTheFloor_Descriptor_IsAnErrorThatCannotBeConfigured` | `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs`: `SqlDiagnosticsTests.All_EveryDescriptor_IsAnErrorThatCannotBeConfiguredButTheListedOnes`; `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs`: `DiagnosticsDocumentTests.Document_Sections_AreTheDescriptorsInOrder`; `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs`: `DiagnosticsDocumentTests.Document_Table_ListsEveryDescriptorWithItsTitleAndAnchor`; `tests/SqlSource.Tests/Generator/LibraryDiagnosticsTests.cs`: `LibraryDiagnosticsTests.Run_NodaTimeBelowTheFloorAndAMethodsUse_Reports282Once` |
| F-SqlSource.Tests-1-02 | delete | `SettingsFileDiagnosticsTests.Create_DefaultBesideAnotherParameter_IsNoError` | `tests/SqlSource.Tests/SettingsFiles/SettingsFileReaderTests.cs`: `SettingsFileReaderTests.Read_GeneratorParameters_FollowTheElementRules`; `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs`: `GeneratorParameterListTests.ReadElements_DefaultBesideOtherWords_ResetsThenAppliesThem` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `src/SqlSource/Diagnostics/AGENTS.md`: 5

## Left out

- F-SqlSource.Tests-1-07 (keep, `SettingsFileDiagnosticsTests.Create_EachKind_GivesItsDescriptor`): a **keep**; the test stays as it is.
- F-SqlSource.Tests-1-08 (keep, `SettingsFileDiagnosticsTests.Create_ColumnTypeKeyInBrackets_HasNoHelp`): a **keep**; the test stays as it is.
- F-SqlSource.Tests-1-09 (keep, `SettingsFileDiagnosticsTests.Create_RepeatedKey_QuotesBothKeysAndTheSetting`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
