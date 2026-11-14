# Test cleanup phase 52: tests/SqlSource.Tool.Tests/Bundles/Renaming, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Bundles/Renaming`
- Findings: 2 (1 delete, 1 strengthen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 11
- Guard: mutation (tool-bundles-renaming)

## Files

- `tests/SqlSource.Tool.Tests/Bundles/Renaming/NameMappingTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Bundles.Renaming-1-05

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/NameMappingTests.cs` | `NameMappingTests.IsMapped_UnknownSpelling_IsFalse` | rows: all
- action: delete
- evidence: measured (kills 2, alone 0; cover by the numbers `BundleRenamerTests.RenameAsync_DerivedRequestNotMade_IsRefusedAtTheRequestSql`).  Read: it asserts `Mapping().IsMapped("x", false)` is false (NameMappingTests.cs:187); `IsMapped_AnotherCaseOfAMappedName_IsTrue` asserts `IsMapped("x", false)` is false too (NameMappingTests.cs:308), on a mapping that already holds a name, which is the stronger case, and `IsMapped` looks up the fold alone (NameMapping.cs:126-130), whose unquoted fold is `ToUpperInvariant` in both mappings.  No comment marks it as a record, and the grep of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ finds neither its name nor its class's; only docs/superpowers/plans/2026-11-07-td-0102-renaming-keeps-case.md:96, which renamed it and wrote the same assertion into the cover.  blind: no
- cover: `tests/SqlSource.Tool.Tests/Bundles/Renaming/NameMappingTests.cs` | `NameMappingTests.IsMapped_AnotherCaseOfAMappedName_IsTrue`
- proposal: Delete it.
- verdict: confirmed
- verifier's reason: `kills` gives 2 kills, both the `RenamingKey.Of` length check (RenamingKey.cs:29:9), which nearly every test kills; it kills nothing of `IsMapped`.  The cover asserts the same `IsMapped("x", false).ShouldBeFalse()` (NameMappingTests.cs:308) on a mapping that already holds a name, and `IsMapped` is the same lookup of the fold in `_kept` and `_byKey` in both (NameMapping.cs:126-130), with an unquoted fold of `ToUpperInvariant` in both `Mapping()` and `CaseKeeping()` (NameMappingTests.cs:13, 30).  My grep of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ finds neither its name nor `NameMappingTests`; only docs/superpowers/plans/2026-11-07-td-0102-renaming-keeps-case.md:96 names it, as a rename.  No comment is above it.  The cover must stay, as F-07 says.

### F-SqlSource.Tool.Tests.Bundles.Renaming-1-01

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/NameMappingTests.cs` | `NameMappingTests.NameClasses_EveryMember_HasAPrefixAndAWord` | rows: all
- action: strengthen
- evidence: measured (covers 24 mutants and kills none; survivors src/SqlSource.Tool/Bundles/Renaming/NameClass.cs:48:33, 50:33, 51:32, 52:35, 53:31, 54:32, 55:36, 56:32, 57:32 and 58:35 String mutation -> `""`, the words of every class but table and literal, which only MappingFileTests kills).  Read: the test asserts only that the prefixes and the words are each distinct (NameMappingTests.cs:328-329), and one word emptied is still distinct from the other eleven, so a class with no word passes a test named for every class having one; the word is what the mapping file calls the class and what the hash is keyed by (NameClass.cs:44).  blind: no
- cover: none
- proposal: Assert of every member that its prefix is a lower-case letter and `_` and that its word is not empty, or is the member's name in lower case as every word is today, beside the distinct counts, so an emptied or swapped prefix or word fails.
- verdict: confirmed
- verifier's reason: `kills` on tool-bundles-renaming gives 0 kills and 10 survivors under `NameMappingTests.NameClasses_EveryMember_HasAPrefixAndAWord`, each a String mutation -> `""` of a word at src/SqlSource.Tool/Bundles/Renaming/NameClass.cs:48, 50-58.  The test asserts only that the prefixes and the words are distinct and that 99 throws (NameMappingTests.cs:328-331); an emptied word, or an emptied prefix, is still distinct from the other eleven, so a class without a word passes.  Table's and literal's words (NameClass.cs:49, 59) are killed alone by `MappingFileTests.Write_TwoNames_ListsEachRenamedName` (evidence line 71), as the finding says.  The proposed assertion (non-empty word or the member's name in lower case, prefix a lower-case letter and `_`) holds for every member today (NameClass.cs:29-40, 48-59).
- survivors to kill: `src/SqlSource.Tool/Bundles/Renaming/NameClass.cs:48:33`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-bundles-renaming`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-52/tool-bundles-renaming/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Bundles.Renaming.' 'Bundles/Renaming/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-52/tool-bundles-renaming/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Bundles.Renaming.' 'Bundles/Renaming/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-52/tool-bundles-renaming/before/reports/mutation-report.json artifacts/test-cleanup/phase-52/tool-bundles-renaming/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-52/tool-bundles-renaming/before/reports/mutation-report.json NameMappingTests.NameClasses_EveryMember_HasAPrefixAndAWord
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-52/tool-bundles-renaming/before/reports/mutation-report.json NameMappingTests.IsMapped_UnknownSpelling_IsFalse
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests.Bundles.Renaming-1-01: `src/SqlSource.Tool/Bundles/Renaming/NameClass.cs:48:33`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-bundles-renaming`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-52/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-52/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-52/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-52/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-52/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-52/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tool.Tests.Bundles.Renaming-1-04 (delete, `NameMappingTests.Keep_SpellingsTheEngineTakesForOne_MapToTheFirstKept`): refuted; the appendix keeps the reason.
- F-SqlSource.Tool.Tests.Bundles.Renaming-1-06 (keep, `NameMappingTests.KeepFromEngineRow_AnotherCase_IsKeptFromTheRow`): a **keep**; the test stays as it is.
- F-SqlSource.Tool.Tests.Bundles.Renaming-1-07 (keep, `NameMappingTests.IsMapped_AnotherCaseOfAMappedName_IsTrue`): refuted; the appendix keeps the reason.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
