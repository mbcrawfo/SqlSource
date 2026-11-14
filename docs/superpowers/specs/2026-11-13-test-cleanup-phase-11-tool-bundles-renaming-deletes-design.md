# Test cleanup phase 11: tests/SqlSource.Tool.Tests/Bundles/Renaming, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Bundles/Renaming`
- Findings: 2 (2 delete)
- Kind: one kind, delete
- Changed lines: about 18
- Guard: mutation (tool-bundles-renaming)

## Files

- `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.Fallback.cs`
- `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerRenamingTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Bundles.Renaming.SqlServer-1-01

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.Fallback.cs` | `SqlServerErrorRenamingTests.RenameAsync_AKeyThatFallsBackOnAParameter_IsRefused` | rows: all
- action: delete
- evidence: measured (kills 161, alone 0, timeouts under it 9; cover: `SqlServerErrorRenamingTests.RenameAsync_EveryKeyFallsBack_StopsAfterTheAttempts` alone) and read: the test renames `ParameterInThreeSpellingsAsync()` once with `KeyWhere(CutoffSpellings, NameClass.Parameter, fallsBack: true)` through the overload of one key (`SqlServerRenamings.RefusedAsync`, then `BundleRenamings.RefusedAsync`) and asserts the refusal `(RequestSql, "capture.json call 'parameters'", null)` (Fallback.cs:33-41).  `EveryKeyFallsBack_StopsAfterTheAttempts` (Fallback.cs:84-106) renames the same bundle with the same key through the source of keys, whose last attempt is that same overload of one key (`src/SqlSource.Tool/Bundles/Renaming/BundleRenamer.cs:67`), and asserts that same refusal and a null value, so it holds everything this test does.  The 9 timeouts under it are under the cover too (9), which runs the identical renaming; the test is not deleted on its numbers alone.  Search: no `AGENTS.md`, readme, `docs/tech-debt/` or `docs/deferred/` item names the test; `docs/architecture/bundles.md:378` says the class renames "under a key whose mapping falls back and one whose does not, and through the source of keys that renames again", which stays true through the cover.  No comment above the test or its group calls it a record.  blind: no
- cover: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.Fallback.cs` | `SqlServerErrorRenamingTests.RenameAsync_EveryKeyFallsBack_StopsAfterTheAttempts`
- proposal: Delete the test; the refusal under a key that falls back stays asserted by `RenameAsync_EveryKeyFallsBack_StopsAfterTheAttempts`, whose last attempt is the same renaming.
- verdict: confirmed
- verifier's reason: Read `SqlServerErrorRenamingTests.Fallback.cs:33-41` and the cover at `:84-106`.  Both rename `ParameterInThreeSpellingsAsync()` with the same `KeyWhere(CutoffSpellings, NameClass.Parameter, fallsBack: true)` key.  The cover goes through the overload that takes a source of keys, and its last attempt is `RenameAsync(raw, engine, describer, nextKey(), token)`, the overload of one key (`src/SqlSource.Tool/Bundles/Renaming/BundleRenamer.cs:67`, then `:89`).  That is the same call `SqlServerRenamings.RefusedAsync` makes through `BundleRenamings.RefusedAsync` (`tests/SqlSource.Tool.Testing/Bundles/BundleRenamings.cs:42-52`, which adds only `Value` null and `Failure` not null).  The cover asserts the same `RenamingRefusal(RequestSql, "capture.json call 'parameters'", null)` and a null `Value` (`Fallback.cs:104-105`).  `kills` on tool-bundles-renaming: the test's 161 kills are all in the cover's 166, and all 9 of its timeouts (`SqlRenamer.cs` 425/427/434/470/486/488, `TokenCheck.Sql.cs` 155/164) are also under the cover.  The kills spread over the whole pipeline, which is expected, since any mutant that refuses earlier or at another place changes the exact refusal asserted.  Search: no `AGENTS.md`, readme, `CONTRIBUTING.md`, `docs/tech-debt/` or `docs/deferred/` names the test.  `docs/architecture/bundles.md:378` describes the class's cases ("under a key whose mapping falls back"), and that stays true through the cover.  The group comment (`Fallback.cs:15-16`) describes the setup and calls the test no record.

### F-SqlSource.Tool.Tests.Bundles.Renaming.SqlServer-1-04

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerRenamingTests.cs` | `SqlServerRenamingTests.RuleOf_AKnownAndAnUnknownField_GivesTheFieldsRuleAndNull` | rows: all
- action: delete
- evidence: measured (kills 1, alone 0, timeouts under it 0; cover: `SqlServerErrorRenamingTests.RenameAsync_AColumnOfTheServersThroughALinkedServer_IsRenamed`) and read: `SqlServerRenaming.RuleOf` is the one line `=> SqlServerFields.RuleOf(method, path)` (`src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerRenaming.cs:80`).  The test asserts the rule of `open answer.database` and null for `columns answer.rows[].unknown` through the part (SqlServerRenamingTests.cs:116-120); both values are rows of `SqlServerFieldsTests`, `RuleOf_AMemberOfTheRecords_IsItsRule` (the member `open answer.database`, SqlServerFieldsTests.cs:157) and `RuleOf_AnyOtherMember_IsNull` (SqlServerFieldsTests.cs:136), and the delegation is exercised by every renaming through the part: `RenameAsync_MemberWithNoRule_IsRefusedAtItsPlace` needs the part's null for a member of no record, and every renaming that succeeds needs its rules.  Search: no document names the test, and it has no comment.  blind: no
- cover: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerFieldsTests.cs` | `SqlServerFieldsTests.RuleOf_AMemberOfTheRecords_IsItsRule`; `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerFieldsTests.cs` | `SqlServerFieldsTests.RuleOf_AnyOtherMember_IsNull`; `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.cs` | `SqlServerErrorRenamingTests.RenameAsync_MemberWithNoRule_IsRefusedAtItsPlace`; `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.cs` | `SqlServerErrorRenamingTests.RenameAsync_AColumnOfTheServersThroughALinkedServer_IsRenamed`
- proposal: Delete the test; the rules are held by `SqlServerFieldsTests` and the part's delegation by every renaming of a bundle through it.
- verdict: confirmed
- verifier's reason: `SqlServerRenaming.RuleOf` is the one expression `=> SqlServerFields.RuleOf(method, path)` (`src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerRenaming.cs:80`), which has no mutant of its own.  The test's two values (`SqlServerRenamingTests.cs:118-119`) are rows of `SqlServerFieldsTests`: `open answer.database` gives `Name(Database)` (`SqlServerFieldsTests.cs:157`, run by `RuleOf_AMemberOfTheRecords_IsItsRule`), and `("columns", "answer.rows[].unknown")` gives null (`:136`, `RuleOf_AnyOtherMember_IsNull`).  Every renaming through the part exercises the delegation.  `RenameAsync_MemberWithNoRule_IsRefusedAtItsPlace` (`SqlServerErrorRenamingTests.cs:303-317`) needs the part's null for an unknown member of `columns` rows to give its `Token` refusal at that place, and every successful renaming needs the part's rules.  `kills`: its one kill, `SqlServerFields.cs:91:36 String mutation` (the separator `" "` emptied, so every lookup is null), is killed by 25 other methods.  Search: no document names the test, and it has no comment.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-bundles-renaming`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-11/tool-bundles-renaming/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Bundles.Renaming.' 'Bundles/Renaming/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-11/tool-bundles-renaming/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Bundles.Renaming.' 'Bundles/Renaming/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-11/tool-bundles-renaming/before/reports/mutation-report.json artifacts/test-cleanup/phase-11/tool-bundles-renaming/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-11/tool-bundles-renaming/before/reports/mutation-report.json SqlServerErrorRenamingTests.RenameAsync_AKeyThatFallsBackOnAParameter_IsRefused
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-11/tool-bundles-renaming/before/reports/mutation-report.json SqlServerRenamingTests.RuleOf_AKnownAndAnUnknownField_GivesTheFieldsRuleAndNull
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-bundles-renaming`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-11/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-11/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-11/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-11/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-11/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-11/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/architecture/bundles.md`: 354, 364, 368, 369, 370, 371, 378

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
