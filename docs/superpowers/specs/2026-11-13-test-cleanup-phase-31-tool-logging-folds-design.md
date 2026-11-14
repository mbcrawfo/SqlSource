# Test cleanup phase 31: tests/SqlSource.Tool.Tests/Logging, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Logging`
- Findings: 1 (1 fold)
- Kind: one kind, fold
- Changed lines: about 4
- Guard: mutation (tool-logging)

## Files

- `tests/SqlSource.Tool.Tests/Logging/RunLogDestinationTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests-2-05

- test: `tests/SqlSource.Tool.Tests/Logging/RunLogDestinationTests.cs` | `RunLogDestinationTests.Run_LogThatCannotBeWritten_IsSqlsrc218AndTheRunDoesNotStart` | rows: `"tmp"` (line 93)
- action: fold
- evidence: measured (2 rows, kills 1, alone 0, timeouts under it 0; rows needed 1; cover: `RunLogFileTests.Open_PathThatCannotBeWritten_GivesNoLogAndTheErrorWithTheReason`).  Read: both rows reach the one `catch` of `RunLogFile.Open` (src/SqlSource.Tool/Logging/RunLogFile.cs:113-119), and the test's assertions (exit code 1, no output, the line's start up to the system's reason, no describer opened) do not depend on which reason; `Open_PathThatCannotBeWritten_…` (RunLogFileTests.cs:226-249) has the rows `"folder"`, a directory as `tmp` is, and `"no-such-folder/run.log"`.  The comment above the row only says why `tmp` is a directory; no document of those the brief names mentions the test.  blind: no
- cover: `tests/SqlSource.Tool.Tests/Logging/RunLogFileTests.cs` | `RunLogFileTests.Open_PathThatCannotBeWritten_GivesNoLogAndTheErrorWithTheReason`
- proposal: Drop the `"tmp"` row and its comment, keeping `"no-such-folder/run.log"` to prove the run does not start; the unit test keeps both reasons.
- verdict: confirmed
- verifier's reason: Read RunLogDestinationTests.cs:91-105: both rows reach the one `catch` of RunLogFile.Open (RunLogFile.cs:113-119, filter FileErrors.cs:15-16 takes IOException, UnauthorizedAccessException, ArgumentException and NotSupportedException alike), and the assertions stop at the system's reason, so the row's reason never matters.  RunLogFileTests.cs:226-249 keeps the directory case (`"folder"`, created at :236) and the missing folder at unit level, asserting the descriptor, the full path and the reason's presence.  `kills` gives the theory 1 kill, rows needed 1, no timeouts.  The comment at :91 only explains why `tmp` is a directory; `git log -S` shows the row was written with the log feature (3dd03892) and moved (bc9c8e23), never added for a regression; the grep over the documents the brief names finds no mention of the test.  A fold that drops a row cannot be measured by a filter, since the rows share a name: this is confirmed by reading, and the phase's own guard proves it.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-logging`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-31/tool-logging/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Logging.' 'Logging/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-31/tool-logging/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Logging.' 'Logging/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-31/tool-logging/before/reports/mutation-report.json artifacts/test-cleanup/phase-31/tool-logging/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-31/tool-logging/before/reports/mutation-report.json RunLogDestinationTests.Run_LogThatCannotBeWritten_IsSqlsrc218AndTheRunDoesNotStart
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-logging`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-31/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-31/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-31/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-31/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-31/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-31/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md`: 21
- `src/SqlSource.Tool/Logging/AGENTS.md`: 8

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
