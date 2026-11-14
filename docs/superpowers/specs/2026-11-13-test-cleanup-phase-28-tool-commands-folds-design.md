# Test cleanup phase 28: tests/SqlSource.Tool.Tests/Commands, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Commands`
- Findings: 2 (2 fold)
- Kind: one kind, fold
- Changed lines: about 21
- Guard: mutation (tool-root), with covers and run time for 2 findings

## Files

- `tests/SqlSource.Tool.Tests/Commands/CheckFilterTests.cs`
- `tests/SqlSource.Tool.Tests/Commands/DiagnoseTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Commands-1-03

- test: `tests/SqlSource.Tool.Tests/Commands/CheckFilterTests.cs` | `CheckFilterTests.Check_EntryOfAQueryThatIsGoneUnderDatabase_IsReportedWhenDescribeWouldWriteTheFile` | rows: `"postgres"` (line 130)
- action: fold
- evidence: read (the row runs a describe, adds an entry of a gone query whose database is `billing` (CheckFilterTests.cs:133-134), checks with `--database postgres` and asserts exit 2 and that the entry is reported (:136-139).  `Check_EntryOfAnotherDatabaseThatIsGone_AgreesWithDescribe` makes the same arrangement and the same check at :159-162 and asserts the same two at :166-167, and then that describe drops the entry and the check passes.  The rows `"billing"` and `"BILLING"` keep the named database and its case.  `src/SqlSource.Tool/Describing/AGENTS.md:12` names `CheckFilterTests.cs` as the holder of the rule that `--check` reports what `describe` with the same filters changes, which the class still holds; no document names the test, and its comment states the rule the other test proves for this row.  Measured: 3 rows, kills 86, alone 0, timeouts under it 57, rows needed 1, cover `CheckFilterTests.Check_EntryOfAQueryThatIsGoneWithNoDatabase_IsReportedUnderDatabase`; the comparison is in `src/SqlSource.Tool/Describing/SidecarCheck.cs`, outside the root scope, so the row is judged by reading); blind: no
- cover: `tests/SqlSource.Tool.Tests/Commands/CheckFilterTests.cs` | `CheckFilterTests.Check_EntryOfAnotherDatabaseThatIsGone_AgreesWithDescribe`
- proposal: Drop the `"postgres"` row and say in the theory's comment that the other database's case is the one `Check_EntryOfAnotherDatabaseThatIsGone_AgreesWithDescribe` takes.
- verdict: confirmed
- verifier's reason: The `"postgres"` row (CheckFilterTests.cs:130) does a describe, adds a gone entry of `billing` (:133-134), checks with `--database postgres` and asserts exit 2 and `ReportsGone` (:136-139); `Check_EntryOfAnotherDatabaseThatIsGone_AgreesWithDescribe` does the same arrangement and check (:159-162) and asserts the same two (:166-167), then more.  The rows `"billing"` and `"BILLING"` keep the named database and its case, which the theory's comment (:124-126) promises.  `kills` in tool-root: the theory's 86 kills and 57 timeouts are all under the cover's (0 not under it); the comparison itself is in `src/SqlSource.Tool/Describing/SidecarCheck.cs`, outside tool-root, so it is judged by reading.  `src/SqlSource.Tool/Describing/AGENTS.md:12` names the class, not the test or the row; the test is named only in `docs/superpowers/plans/`.  A fold that drops a row of a theory cannot be measured by a filter, since the rows share a name: confirmed by reading, and the cleanup phase's own guard proves it.

### F-SqlSource.Tool.Tests.Commands-1-04

- test: `tests/SqlSource.Tool.Tests/Commands/DiagnoseTests.cs` | `DiagnoseTests.Diagnose_OutEndsInSql_IsSQLSRC218`; `tests/SqlSource.Tool.Tests/Commands/DiagnoseTests.cs` | `DiagnoseTests.Diagnose_OutIsAFileOfTheRun_IsSQLSRC218` | rows: all
- action: fold
- evidence: read (`Diagnose_OutEndsInSql_IsSQLSRC218`, DiagnoseTests.cs:370-380, gives `--out report.sql` and asserts exit 1, a start of SQLSRC218 at the path and no file.  The theory at :347-368 gives `report.SQL.JSON` and `App.csproj`, asserts the whole message, "a bundle is not written over a .sql file, a sidecar, a project or a solution", and that the file is as it was, `null` when it did not exist, which `report.sql` is.  Both reach the one check, `RunLogFile.IsAFileOfTheRun` in `RefusedOutOf` (`src/SqlSource.Tool/Bundles/DiagnoseRun.cs:366-372`).  No document names either test, and no comment marks one as a record.  Measured: the fact kills 81, alone 0, timeouts under it 61, cover `DiagnoseScrubTests.Diagnose_ScrubOfARenamedBundle_IsSQLSRC295`; the theory 2 rows, kills 89, timeouts under it 61; the check is outside the root scope, so the rows are judged by reading); blind: no
- cover: `tests/SqlSource.Tool.Tests/Commands/DiagnoseTests.cs` | `DiagnoseTests.Diagnose_OutIsAFileOfTheRun_IsSQLSRC218`
- proposal: Add `[InlineData("report.sql")]` to the theory and remove the fact; the `.sql` case then asserts the whole message instead of its start.
- verdict: confirmed
- verifier's reason: `Diagnose_OutEndsInSql_IsSQLSRC218` (DiagnoseTests.cs:370-380) and the theory (:347-368) take the same setup and reach the one check, `RunLogFile.IsAFileOfTheRun` (`src/SqlSource.Tool/Logging/RunLogFile.cs:126-127`, an ending match ignoring case) through `RefusedOutOf` (`src/SqlSource.Tool/Bundles/DiagnoseRun.cs:366-373`).  The theory asserts the whole result, a stronger claim than the fact's start of error, and `(File.Exists(path) ? ReadText(path) : null).ShouldBe(before)` is `null` for a `report.sql` that did not exist, which is the fact's `File.Exists(...).ShouldBeFalse()`.  `kills` in tool-root: the fact's 81 kills and 61 timeouts are all under the theory's; the `.sql` ending is in `Logging/`, outside tool-root, so the row keeps the one test of that ending, as the proposal does.  The fact is named only in `docs/superpowers/plans/2026-11-05-query-generation-phase-10-4-diagnose-and-close.md`, and has no comment.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-root`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-28/tool-root/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Commands.|FullyQualifiedName~SqlSource.Tool.Tests.OutcomeTests.|FullyQualifiedName~SqlSource.Tool.Tests.ToolHostTests.|FullyQualifiedName~SqlSource.Tool.Tests.UsageValueTests.' '*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-28/tool-root/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Commands.|FullyQualifiedName~SqlSource.Tool.Tests.OutcomeTests.|FullyQualifiedName~SqlSource.Tool.Tests.ToolHostTests.|FullyQualifiedName~SqlSource.Tool.Tests.UsageValueTests.' '*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-28/tool-root/before/reports/mutation-report.json artifacts/test-cleanup/phase-28/tool-root/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-28/tool-root/before/reports/mutation-report.json CheckFilterTests.Check_EntryOfAQueryThatIsGoneUnderDatabase_IsReportedWhenDescribeWouldWriteTheFile
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-28/tool-root/before/reports/mutation-report.json DiagnoseTests.Diagnose_OutEndsInSql_IsSQLSRC218
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-28/tool-root/before/reports/mutation-report.json DiagnoseTests.Diagnose_OutIsAFileOfTheRun_IsSQLSRC218
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-root`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-28/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-28/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-28/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-28/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-28/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-28/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tool.Tests.Commands-1-03, F-SqlSource.Tool.Tests.Commands-1-04.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tool.Tests.Commands-1-03 | fold | `CheckFilterTests.Check_EntryOfAQueryThatIsGoneUnderDatabase_IsReportedWhenDescribeWouldWriteTheFile` (rows: `"postgres"` (line 130)) | `tests/SqlSource.Tool.Tests/Commands/CheckFilterTests.cs`: `CheckFilterTests.Check_EntryOfAnotherDatabaseThatIsGone_AgreesWithDescribe` |
| F-SqlSource.Tool.Tests.Commands-1-04 | fold | `DiagnoseTests.Diagnose_OutEndsInSql_IsSQLSRC218`, `DiagnoseTests.Diagnose_OutIsAFileOfTheRun_IsSQLSRC218` (rows: `DiagnoseTests.Diagnose_OutIsAFileOfTheRun_IsSQLSRC218` | rows: all) | `tests/SqlSource.Tool.Tests/Commands/DiagnoseTests.cs`: `DiagnoseTests.Diagnose_OutIsAFileOfTheRun_IsSQLSRC218` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tool.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `src/SqlSource.Tool/Describing/AGENTS.md`: 12

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
