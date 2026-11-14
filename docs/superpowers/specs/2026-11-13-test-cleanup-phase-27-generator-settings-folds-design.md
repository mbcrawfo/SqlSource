# Test cleanup phase 27: tests/SqlSource.Tests/Settings, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Settings`
- Findings: 1 (1 fold)
- Kind: one kind, fold
- Changed lines: about 28
- Guard: mutation (generator-settings)

## Files

- `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeyTextTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Settings-1-17

- test: `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeyTextTests.cs` | `TypeKeyTextTests.WritePart_Text_IsQuotedWhenItNeedsIt`; `TypeKeyTextTests.WritePart_WhiteSpaceThatIsNoControlCharacter_IsKeptInTheQuotes` | rows: of `WritePart_Text_IsQuotedWhenItNeedsIt`, `("a  b", "\"a  b\"")` at line 84, `("a b", "\"a b\"")` at line 86 and `("x y", "\"x y\"")` at line 87
- action: fold
- evidence: measured (`WritePart_Text_IsQuotedWhenItNeedsIt`: 19 rows, rows needed 5; `WritePart_WhiteSpaceThatIsNoControlCharacter_IsKeptInTheQuotes`: kills 19, alone 0); read (TypeKeyTextTests.cs:126-136 writes the same three texts and asserts the same quoted form, `"\"" + text + "\""`, then reads each back; the three rows of the general theory assert nothing more; the comment at line 125 states the rule, and `grep -rn` of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ names neither test nor `TypeKeyTextTests`); blind: no
- cover: `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeyTextTests.cs` | `TypeKeyTextTests.WritePart_WhiteSpaceThatIsNoControlCharacter_IsKeptInTheQuotes`
- proposal: Drop rows 84, 86 and 87 of `WritePart_Text_IsQuotedWhenItNeedsIt`, which the commented test proves with its read-back; row 85, `my  schema`, stays.
- verdict: confirmed
- verifier's reason: read TypeKeyTextTests.cs:74-95 and 125-136: rows 84 `a  b`, 86 `a b` and 87 `x y` are the three rows of `WritePart_WhiteSpaceThatIsNoControlCharacter_IsKeptInTheQuotes`, which asserts the same `"\"" + text + "\""` and reads each back; no comment marks those rows, and the grep of the record documents names neither test nor `TypeKeyTextTests`.  Ran `kills` for both: the commented test's kills are not all the theory's (it kills TypeKeyText.cs:65, 75, 81, 82, 87 mutants through its read-back), but the fold drops the theory's rows, not that test.  Rows of one theory share a name, so no filter measures the drop; it is confirmed by reading, and the cleanup phase's own guard proves it.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-settings`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-27/generator-settings/before --tests 'FullyQualifiedName~SqlSource.Tests.Settings.' 'Settings/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-27/generator-settings/after --tests 'FullyQualifiedName~SqlSource.Tests.Settings.' 'Settings/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-27/generator-settings/before/reports/mutation-report.json artifacts/test-cleanup/phase-27/generator-settings/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-27/generator-settings/before/reports/mutation-report.json TypeKeyTextTests.WritePart_Text_IsQuotedWhenItNeedsIt
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-27/generator-settings/before/reports/mutation-report.json TypeKeyTextTests.WritePart_WhiteSpaceThatIsNoControlCharacter_IsKeptInTheQuotes
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-settings`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-27/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-27/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-27/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-27/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-27/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-27/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
