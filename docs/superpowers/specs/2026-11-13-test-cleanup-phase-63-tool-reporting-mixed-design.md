# Test cleanup phase 63: tests/SqlSource.Tool.Tests/Reporting, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Reporting`
- Findings: 2 (1 delete, 1 strengthen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 20
- Guard: mutation (tool-reporting)

## Files

- `tests/SqlSource.Tool.Tests/Reporting/ReporterTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests-4-05

- test: `tests/SqlSource.Tool.Tests/Reporting/ReporterTests.cs` | `ReporterTests.Count_AfterTwoErrors_IsTwo` | rows: all
- action: delete
- evidence: measured (kills 3, alone 0, timeouts under it 0; cover: `ReporterTests.ExitCodeOf_AWrittenErrorBesideOneReportedWithoutWriting_IsOne`); read (it reports two errors and asserts `Count` 0 before and 2 after, :245-254; `Report_FromManyThreads_KeepsEveryErrorWholeAndCountsIt` asserts `Count` 2000 after 2000 reports, :276, and `Report_Sqlsrc219_IsCountedAsAnErrorAndAsADifference` asserts 2 after two different errors, :304, both from a new reporter, so a count that did not start at 0 or did not add one per error fails them; no comment above it, and the grep over the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ names neither it nor its class, only the plan docs/superpowers/plans/2026-10-09-query-generation-phase-2-2-tool-shell.md that created it); blind: no
- cover: `tests/SqlSource.Tool.Tests/Reporting/ReporterTests.cs` | `ReporterTests.Report_FromManyThreads_KeepsEveryErrorWholeAndCountsIt`; `tests/SqlSource.Tool.Tests/Reporting/ReporterTests.cs` | `ReporterTests.Report_Sqlsrc219_IsCountedAsAnErrorAndAsADifference`
- proposal: Delete the test; the count of errors stays proved by the two named tests.
- verdict: confirmed
- verifier's reason: The test asserts `Count` is 0 on a new reporter and 2 after two `Report` calls (tests/SqlSource.Tool.Tests/Reporting/ReporterTests.cs:245-254).  `kills` on tool-reporting gives it 3 kills: src/SqlSource.Tool/Reporting/Reporter.cs:64:13 block removal, 131:13 statement and 131:13 `_count--`.  `Report_FromManyThreads_KeepsEveryErrorWholeAndCountsIt` (:276, 2000 from a new reporter) and `Report_Sqlsrc219_IsCountedAsAnErrorAndAsADifference` (:304, 2 after two errors from a new reporter) kill all three.  A count that started anywhere but 0 (Reporter.cs:51), or did not add one per error (:131), fails both.  The second error's path changes nothing the count reads.  There are no timeouts.  There is no comment above the test, and none above its group.  Grepping the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ for `Count_AfterTwoErrors_IsTwo` and `ReporterTests` finds nothing.  The only hits are plans and specs under docs/superpowers/, which are not records.

### F-SqlSource.Tool.Tests-4-04

- test: `tests/SqlSource.Tool.Tests/Reporting/ReporterTests.cs` | `ReporterTests.Report_TextWithLineBreaks_StaysOnItsLine` | rows: all
- action: strengthen
- evidence: measured (kills 15, alone 0; survivor under it and the other tests that write a text: src/SqlSource.Tool/Reporting/OneLine.cs:16:9 Equality mutation `text.AsSpan().IndexOfAny('\r', '\n') <= 0`); read (the mutant writes a text whose first character is a line break as it is, which starts a line a build reads as an error of its own; the texts at :162-163 hold their line breaks inside, at index 3 and 5, and no test of `OneLine` exists elsewhere); blind: no
- cover: none
- proposal: Give one text that starts with a line break, as a continuation line `"\nsecond"`, and expect it written `" second"` on its line.
- verdict: confirmed
- verifier's reason: src/SqlSource.Tool/Reporting/OneLine.cs:16 mutated to `<= 0` returns a text unchanged when its line break is at index 0.  `kills` on tool-reporting lists it `SURVIVED` under this test.  The texts at tests/SqlSource.Tool.Tests/Reporting/ReporterTests.cs:162-163 have their first break at index 3 and 5.  No other test calls `OneLine.Of`; `grep -rn 'OneLine\.Of'` finds only source.  The one leading-break text in the tool's tests, `Output = "\nBuild FAILED.  \n"` at tests/SqlSource.Tool.Tests/Projects/ProjectEvaluatorTests.cs:242, is trimmed into lines before any reporter sees it.  A continuation line "\nsecond" would put "second" in the first column, so the proposed row fails under the mutant.  The other survivor, 16:9 Conditional (false), is equivalent and is not claimed.
- survivors to kill: `src/SqlSource.Tool/Reporting/OneLine.cs:16:9`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-reporting`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-63/tool-reporting/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Reporting.' 'Reporting/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-63/tool-reporting/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Reporting.' 'Reporting/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-63/tool-reporting/before/reports/mutation-report.json artifacts/test-cleanup/phase-63/tool-reporting/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-63/tool-reporting/before/reports/mutation-report.json ReporterTests.Report_TextWithLineBreaks_StaysOnItsLine
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-63/tool-reporting/before/reports/mutation-report.json ReporterTests.Count_AfterTwoErrors_IsTwo
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests-4-04: `src/SqlSource.Tool/Reporting/OneLine.cs:16:9`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-reporting`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-63/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-63/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-63/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-63/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-63/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-63/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
