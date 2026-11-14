# Test cleanup phase 10: tests/SqlSource.Tool.Tests/Commands, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Commands`
- Findings: 6 (6 delete)
- Kind: one kind, delete
- Changed lines: about 65
- Guard: mutation (tool-root)

## Files

- `tests/SqlSource.Tool.Tests/Commands/ExitCodeTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Commands-1-05

- test: `tests/SqlSource.Tool.Tests/Commands/ExitCodeTests.cs` | `ExitCodeTests.Run_NothingReported_ExitsZeroWithoutTheLine` | rows: all
- action: delete
- evidence: read (ExitCodeTests.cs:25-31 describes one query with nothing reported and asserts `CliResult(0, "postgres (postgres): 1 described, 0 skipped, 0 failed\n", "")`.  `DescribeRunTests.Run_QueriesWithoutASidecar_AreDescribedAndTheSidecarIsWritten` asserts the same whole result for the same run of two queries at DescribeRunTests.cs:34, an empty error being the absence of the line, and more.  No document names the test or `ExitCodeTests`, and it has no comment.  Measured: kills 53, alone 0, timeouts under it 54, cover `CheckTests.Check_SidecarsThatDescribeWrote_DescribesEveryQueryAgainAndChangesNoFile`; the named cover has 54 timeouts under it on the same run, so this is not a delete on numbers alone); blind: no
- cover: `tests/SqlSource.Tool.Tests/Commands/DescribeRunTests.cs` | `DescribeRunTests.Run_QueriesWithoutASidecar_AreDescribedAndTheSidecarIsWritten`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: ExitCodeTests.cs:25-31 asserts `CliResult(0, "postgres (postgres): 1 described, 0 skipped, 0 failed\n", "")` for one query; `DescribeRunTests.Run_QueriesWithoutASidecar_AreDescribedAndTheSidecarIsWritten` (DescribeRunTests.cs:29-44) asserts the whole result for two queries, exit 0, the summary and an empty error (no run-again line), and more.  `kills` in tool-root: 53 kills and 54 timeouts, none that is not also under the named cover.  Neither the test nor `ExitCodeTests` is named outside `docs/superpowers/plans/` and `docs/superpowers/specs/`; the class comment (:12) names its subject, the exit codes and the line, which the cover's whole result holds for this case.

### F-SqlSource.Tool.Tests.Commands-1-06

- test: `tests/SqlSource.Tool.Tests/Commands/ExitCodeTests.cs` | `ExitCodeTests.Run_ErrorWithoutALog_ExitsOneAndEndsWithTheLine` | rows: all
- action: delete
- evidence: read (ExitCodeTests.cs:33-44 fails one query with no log and asserts exit 1, the error's start, that it ends with the query's `see` line and the run-again line, and that the line comes once.  `DescribeRunTests.Run_OneQueryFails_TheOthersAreDescribedAndTheFileIsNotWritten` fails a query with no log and asserts the whole error, ending once with `DescribeScene.RunAgain`, and exit 1, at DescribeRunTests.cs:247-264.  No document names the test or `ExitCodeTests`, and it has no comment.  Measured: kills 57, alone 0, timeouts under it 54, cover `DescribeRunTests.Run_DescriberThatThrows_IsSqlsrc200WithoutTheConnectionAndNothingIsWritten`; the named cover has 54 timeouts under it, so this is not a delete on numbers alone); blind: no
- cover: `tests/SqlSource.Tool.Tests/Commands/DescribeRunTests.cs` | `DescribeRunTests.Run_OneQueryFails_TheOthersAreDescribedAndTheFileIsNotWritten`
- proposal: Delete the test; the tests of `ExitCodeTests` that a log or a check takes the line away keep their failure of a query.
- verdict: confirmed
- verifier's reason: ExitCodeTests.cs:33-44 fails one query with no log and asserts exit 1, the error's start, its end with the `see` line and `DescribeScene.RunAgain`, and the line once; `DescribeRunTests.Run_OneQueryFails_TheOthersAreDescribedAndTheFileIsNotWritten` (DescribeRunTests.cs:240-266) asserts the whole result with exit 1 and an error that ends once with `DescribeScene.RunAgain`.  `kills` in tool-root: 57 kills and 54 timeouts, none not also under the named cover.  The test has no comment and is named only in `docs/superpowers/plans/2026-10-10-query-generation-phase-2-6-check-and-logging.md`.

### F-SqlSource.Tool.Tests.Commands-1-07

- test: `tests/SqlSource.Tool.Tests/Commands/ExitCodeTests.cs` | `ExitCodeTests.Run_DescriberThatThrows_ExitsOneAndEndsWithTheLine` | rows: all
- action: delete
- evidence: read (ExitCodeTests.cs:46-56 makes the describer throw and asserts exit 1 and an error that ends with `Expected.See("SQLSRC200") + DescribeScene.RunAgain`.  `DescribeRunTests.Run_DescriberThatThrows_IsSqlsrc200WithoutTheConnectionAndNothingIsWritten` makes it throw and asserts the whole result, exit 1 and the SQLSRC200 error ending with the same two lines, at DescribeRunTests.cs:466-475.  No document names the test or `ExitCodeTests`; its comment, that an exception is an error of the run and a log is what a maintainer asks for, states the rule the cover proves.  Measured: kills 53, alone 0, timeouts under it 54, cover `DescribeRunTests.Run_DescriberThatThrows_IsSqlsrc200WithoutTheConnectionAndNothingIsWritten`, which has 54 under it, so this is not a delete on numbers alone); blind: no
- cover: `tests/SqlSource.Tool.Tests/Commands/DescribeRunTests.cs` | `DescribeRunTests.Run_DescriberThatThrows_IsSqlsrc200WithoutTheConnectionAndNothingIsWritten`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: ExitCodeTests.cs:46-56 makes the describer throw and asserts exit 1 and an error ending in `Expected.See("SQLSRC200") + DescribeScene.RunAgain`; `DescribeRunTests.Run_DescriberThatThrows_IsSqlsrc200WithoutTheConnectionAndNothingIsWritten` (DescribeRunTests.cs:459-477) asserts the whole result, the same ending among it.  `kills` in tool-root: 53 kills and 54 timeouts, none not also under the cover.  Its comment (:46) states the rule, that an exception is an error of the run and gets the line, which the cover's whole error proves; it marks no limit, regression or decision of its own.  Named only in a phase 2-6 plan.

### F-SqlSource.Tool.Tests.Commands-1-08

- test: `tests/SqlSource.Tool.Tests/Commands/ExitCodeTests.cs` | `ExitCodeTests.Run_CheckThatFindsADifference_ExitsTwoWithoutTheLine` | rows: all
- action: delete
- evidence: read (ExitCodeTests.cs:58-67 runs `--check` with no sidecar and asserts exit 2, a SQLSRC219 and no run-again line.  `CheckTests.Check_NoSidecar_IsOneDifferenceAtTheFirstQueryAndExitsTwo` runs `--check` with no sidecar and asserts exit 2 and the whole error, one SQLSRC219 with no other line, at CheckTests.cs:138-146.  No document names the test or `ExitCodeTests`; its comment, that the line is for a difference that could not be checked and not for one found, is the rule the cover's whole error proves.  Measured: kills 61, alone 0, timeouts under it 55, cover `CheckTests.Check_ColumnNowReadFromAnotherTable_NamesTheOriginAndTheTwoValues`; the named cover has 55 under it, so this is not a delete on numbers alone); blind: no
- cover: `tests/SqlSource.Tool.Tests/Commands/CheckTests.cs` | `CheckTests.Check_NoSidecar_IsOneDifferenceAtTheFirstQueryAndExitsTwo`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: ExitCodeTests.cs:58-67 runs `--check` with no sidecar and asserts exit 2, a SQLSRC219 and no "run again"; `CheckTests.Check_NoSidecar_IsOneDifferenceAtTheFirstQueryAndExitsTwo` (CheckTests.cs:137-146) runs the same and asserts exit 2 and the whole error, `Difference(...)` (:110-112), one SQLSRC219 with no other line.  `kills` in tool-root: 61 kills and 55 timeouts, none not also under the cover.  The comment (:58) is the rule the cover's whole error proves; named only in a phase 2-6 plan.

### F-SqlSource.Tool.Tests.Commands-1-09

- test: `tests/SqlSource.Tool.Tests/Commands/ExitCodeTests.cs` | `ExitCodeTests.Run_CancelledBeforeItStarted_ExitsOneAndPrintsNothing` | rows: all
- action: delete
- evidence: read (ExitCodeTests.cs:158-164 runs `describe` with a token cancelled already and asserts `CliResult(1, "", "")`.  The run stops at `cancellationToken.ThrowIfCancellationRequested()` before the command line is read, "whatever it was asked to do" (`src/SqlSource.Tool/Cli.cs:212-213`), which `CliTests.Run_CancelledBeforeItStarts_PrintsNothingAndExitsWithOne` asserts with the same result at CliTests.cs:270-275.  That test alone fails on removing the line, since `--version` never looks at the token, while `describe` would be cancelled later all the same; and a `describe` cancelled once it has started prints nothing, as `DescribeTests.Run_CancelledWhileMSBuildRuns_ExitsWithOneAndPrintsNothing` asserts at DescribeTests.cs:233-248.  No document names the test or `ExitCodeTests`, and it has no comment.  Measured: kills 7, alone 0, timeouts under it 1, cover `CliTests.Run_CancelledBeforeItStarts_PrintsNothingAndExitsWithOne`, which has the 1 under it too); blind: no
- cover: `tests/SqlSource.Tool.Tests/Commands/CliTests.cs` | `CliTests.Run_CancelledBeforeItStarts_PrintsNothingAndExitsWithOne`; `tests/SqlSource.Tool.Tests/Commands/DescribeTests.cs` | `DescribeTests.Run_CancelledWhileMSBuildRuns_ExitsWithOneAndPrintsNothing`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: ExitCodeTests.cs:158-164 runs `describe` on a token already cancelled and asserts `CliResult(1, "", "")`; `CliTests.Run_CancelledBeforeItStarts_PrintsNothingAndExitsWithOne` (CliTests.cs:269-275) asserts the same result for `--version`, and only it kills `Cli.cs:213:13 Statement mutation`, the `ThrowIfCancellationRequested` (`src/SqlSource.Tool/Cli.cs:212-213`) that holds for any command.  `kills` in tool-root: the test's 7 kills (`Cli.cs:186`, `:191`, `:196`, `:401`, `:402`) and its 1 timeout (`Cli.cs:201:13`) are all under the CliTests test; each is the exit-code and run-again path of a cancelled run, which a cancelled run of either command reaches.  `DescribeTests.Run_CancelledWhileMSBuildRuns_ExitsWithOneAndPrintsNothing` (DescribeTests.cs:233-248) holds that a cancelled `describe` prints nothing.  No comment; named only in a phase 2-6 plan.

### F-SqlSource.Tool.Tests.Commands-1-10

- test: `tests/SqlSource.Tool.Tests/Commands/ExitCodeTests.cs` | `ExitCodeTests.Run_NoUnit_ExitsOneWithTheLine` | rows: all
- action: delete
- evidence: read (ExitCodeTests.cs:166-177 deletes the project and asserts exit 1, a SQLSRC201 and an error that ends with the run-again line.  `DescribeTests.Run_NoUnit_ReportsItAndExitsWithOne` runs `describe` in a folder with no unit and asserts exit 1, an empty output and the whole error, the SQLSRC201 ending with `DescribeScene.RunAgain`, at DescribeTests.cs:250-262.  No document names the test or `ExitCodeTests`; its comment, that an error of a run that never reached a database is still an error of describe, is what the cover proves.  Measured: kills 52, alone 0, timeouts under it 53, cover `DescribeTests.Run_NoUnit_ReportsItAndExitsWithOne`, which has 53 under it, so this is not a delete on numbers alone); blind: no
- cover: `tests/SqlSource.Tool.Tests/Commands/DescribeTests.cs` | `DescribeTests.Run_NoUnit_ReportsItAndExitsWithOne`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: ExitCodeTests.cs:166-177 deletes the project and asserts exit 1, a SQLSRC201 and an error ending in `DescribeScene.RunAgain`; `DescribeTests.Run_NoUnit_ReportsItAndExitsWithOne` (DescribeTests.cs:250-262) asserts exit 1, empty output and the whole SQLSRC201 error ending in `DescribeScene.RunAgain`.  `kills` in tool-root: 52 kills and 53 timeouts, none not also under the cover.  The comment (:166) is the rule the cover proves; named only in a phase 2-6 plan.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-root`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-10/tool-root/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Commands.|FullyQualifiedName~SqlSource.Tool.Tests.OutcomeTests.|FullyQualifiedName~SqlSource.Tool.Tests.ToolHostTests.|FullyQualifiedName~SqlSource.Tool.Tests.UsageValueTests.' '*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-10/tool-root/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Commands.|FullyQualifiedName~SqlSource.Tool.Tests.OutcomeTests.|FullyQualifiedName~SqlSource.Tool.Tests.ToolHostTests.|FullyQualifiedName~SqlSource.Tool.Tests.UsageValueTests.' '*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-10/tool-root/before/reports/mutation-report.json artifacts/test-cleanup/phase-10/tool-root/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-10/tool-root/before/reports/mutation-report.json ExitCodeTests.Run_NothingReported_ExitsZeroWithoutTheLine
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-10/tool-root/before/reports/mutation-report.json ExitCodeTests.Run_ErrorWithoutALog_ExitsOneAndEndsWithTheLine
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-10/tool-root/before/reports/mutation-report.json ExitCodeTests.Run_DescriberThatThrows_ExitsOneAndEndsWithTheLine
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-10/tool-root/before/reports/mutation-report.json ExitCodeTests.Run_CheckThatFindsADifference_ExitsTwoWithoutTheLine
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-10/tool-root/before/reports/mutation-report.json ExitCodeTests.Run_CancelledBeforeItStarted_ExitsOneAndPrintsNothing
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-10/tool-root/before/reports/mutation-report.json ExitCodeTests.Run_NoUnit_ExitsOneWithTheLine
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-root`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-10/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-10/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-10/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-10/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-10/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-10/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
