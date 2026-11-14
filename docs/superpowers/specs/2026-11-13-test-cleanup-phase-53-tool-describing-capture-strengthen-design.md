# Test cleanup phase 53: tests/SqlSource.Tool.Tests/Describing/Capture, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Describing/Capture`
- Findings: 1 (1 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 8
- Guard: mutation (tool-describing-capture), with covers and run time for 1 finding

## Files

- `tests/SqlSource.Tool.Tests/Describing/Capture/ReplayingExchangeTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Describing-1-01

- test: `tests/SqlSource.Tool.Tests/Describing/Capture/ReplayingExchangeTests.cs` | `ReplayingExchangeTests.AskAsync_AnswerWithANullWhereTheRecordHasNone_ThrowsNamingTheMember` | rows: all
- action: strengthen
- evidence: measured (4 rows, kills 21, alone 8, rows needed 1; survivor src/SqlSource.Tool/Describing/Capture/AnswerNulls.cs:62:80 PostIncrementExpression to PostDecrementExpression mutation, covered by 4 methods here).  Read: the name promises the member is named, and `AnswerNulls.Find` documents the path "as the capture's file names it" (AnswerNulls.cs:20).  The one row with an index, `"server.settings[0]"` (ReplayingExchangeTests.cs:233-237), puts the null in the first element, where `index++` and `index--` both give 0, so a null in any later element would be named with a wrong index and the test would pass.  The test is the record that docs/architecture/postgres-describer.md:62 names for these rules, which a new row keeps; blind: yes (AnswerNulls.cs 46-73 is listed as blind, does not compile, but this mutant compiled and survived)
- cover: none
- proposal: Add a row whose null is in the second element of a list, `settings` of `[{"name":"a","value":"b"}, null]` naming `server.settings[1]`, which fails on the survivor.
- verdict: confirmed
- verifier's reason: `kills` in tool-describing-capture gives 21 kills, 8 alone, and `SURVIVED src/SqlSource.Tool/Describing/Capture/AnswerNulls.cs:62:80 ... index--`.  `AnswerNulls.Check` (AnswerNulls.cs:59-62) writes `{index++}` into the path, and the only row with an index puts the null in the first element (ReplayingExchangeTests.cs:233-237), where `index++` and `index--` both print 0; the name promises the member is named, and `AnswerNulls.Find` documents the path as the capture's file names it (AnswerNulls.cs:19-23).  Row 4 (ReplayingExchangeTests.cs:238-242) shows a setting needs both `name` and `value`, so the proposed row `[{"name":"a","value":"b"}, null]` reaches the second element and names `server.settings[1]`, which the mutant would print as `server.settings[-1]`.  docs/architecture/postgres-describer.md says `ReplayingExchangeTests` holds these rules; adding a row keeps that record.
- survivors to kill: `src/SqlSource.Tool/Describing/Capture/AnswerNulls.cs:62:80`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-describing-capture`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-53/tool-describing-capture/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.Capture.' 'Describing/Capture/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-53/tool-describing-capture/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.Capture.' 'Describing/Capture/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-53/tool-describing-capture/before/reports/mutation-report.json artifacts/test-cleanup/phase-53/tool-describing-capture/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-53/tool-describing-capture/before/reports/mutation-report.json ReplayingExchangeTests.AskAsync_AnswerWithANullWhereTheRecordHasNone_ThrowsNamingTheMember
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests.Describing-1-01: `src/SqlSource.Tool/Describing/Capture/AnswerNulls.cs:62:80`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-describing-capture`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-53/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-53/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-53/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-53/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-53/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-53/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tool.Tests.Describing-1-01.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tool.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
