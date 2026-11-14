# Test cleanup phase 50: tests/SqlSource.Tool.Tests/Commands, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Commands`
- Findings: 6 (2 delete, 3 fold, 1 strengthen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 79
- Guard: mutation (tool-root), with covers and run time for 4 findings

## Files

- `tests/SqlSource.Tool.Tests/Commands/DescribeTests.cs`
- `tests/SqlSource.Tool.Tests/Commands/FilterTests.cs`
- `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Commands-2-02

- test: `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs` | `ReplayTests.Replay_Failed_HasNoLineThatPointsToALog` | rows: all
- action: delete
- evidence: measured (1 row, kills 16, alone 0, timeouts under it 55; cover: `ReplayTests.Replay_BundleJsonOfANewerSidecarFormat_IsSqlsrc289`) and read (ReplayTests.cs:611-617 runs `replay missing` and asserts exit 1 and that the error lacks `Cli.RunAgainLine`; `Replay_NothingAtThePath_IsSqlsrc288` at ReplayTests.cs:432-445 runs `replay missing.zip`, the same SQLSRC288 road through `Cli.RunAsync`, and asserts the whole `CliResult`, whose error is the message and its `see:` line alone, so a run-again line would fail it, as it would `Replay_BundleVersionTwo_IsSqlsrc289` and `Replay_EngineWithNoDescriber_IsSqlsrc291`.  The line is written at src/SqlSource.Tool/Cli.cs:191, which is blind, so the delete rests on reading and not on the numbers; the timeouts under it are under the cover too, which reaches the same lines (timeouts under it 55).  No comment marks the test as a record; `grep` of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred names neither the test nor, for this, its class); blind: yes
- cover: `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs` | `ReplayTests.Replay_NothingAtThePath_IsSqlsrc288`; `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs` | `ReplayTests.Replay_EngineWithNoDescriber_IsSqlsrc291`
- proposal: Delete the test; the exact outputs of the replay failures already hold the rule that replay never ends with the run-again line.
- verdict: confirmed
- verifier's reason: ReplayTests.cs:610-617 runs `replay missing` and asserts exit 1 and no `Cli.RunAgainLine`.  The line is written at src/SqlSource.Tool/Cli.cs:191-194 only when `state.MayPoint`, which replay sets to false at src/SqlSource.Tool/ReplayCommand.cs:67.  `Replay_NothingAtThePath_IsSqlsrc288` (ReplayTests.cs:431-445) takes the same SQLSRC288 road with `missing.zip` and asserts the whole `CliResult`, so an appended run-again line fails it, as it does `Replay_EngineWithNoDescriber_IsSqlsrc291` (exact `Error` at :459-463).  `kills` in tool-root: all 16 kills of the test (Cli.cs:186, 191 x3, 196 x2, 304 x2, 352, 396 x2, 397, ReplayCommand.cs:67:36 `true`, 164, SharedOptions.cs:99 x2) are in the cover's 56, each explicable as either the run-again line appearing or the error changing; its 55 timeouts are exactly the cover's.  No comment above the test or the class makes it a record; grep of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred names the test nowhere, and `ReplayTests` only at docs/architecture/bundles.md:79, for the fixture replays.

### F-SqlSource.Tool.Tests.Commands-2-03

- test: `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs` | `ReplayTests.Replay_LogPathWithAnEqualsSign_IsAWrongCommandLine` | rows: all
- action: delete
- evidence: measured (1 row, kills 63, alone 0, timeouts under it 49; cover: `DiagnoseTests.Diagnose_WrongCommandLine_IsAUsageLineThatNamesNoToken`, `UsageCheckTests.CheckUsage_ReplayLogPathWithAnEqualsSignOrASemicolon_IsNamedByItsPosition`) and read (ReplayTests.cs:740-752 asserts the `CliResult` of `replay case --log a=s3cret`; the line comes from `ReplayCommand.CheckUsage` (src/SqlSource.Tool/ReplayCommand.cs:117-121), whose exact text for `=` and `;`, in both spellings of the option, `UsageCheckTests.CheckUsage_ReplayLogPathWithAnEqualsSignOrASemicolon_IsNamedByItsPosition` asserts at UsageCheckTests.cs:756-768; `Cli.ReadCommandLineAsync` (src/SqlSource.Tool/Cli.cs:245-258) writes every command's `CheckUsage` lines alike and returns before the log is opened, and `Replay_NoBundle_IsAWrongCommandLine` at ReplayTests.cs:732-738 holds that road for replay with an exact `CliResult`.  Not deleted on its numbers: the timeouts under it are reached by the unit test (48) and by `Replay_NoBundle_IsAWrongCommandLine` (46) on the same lines.  No comment marks it as a record, and the documents searched name it nowhere); blind: yes (src/SqlSource.Tool/ReplayCommand.cs:113 and src/SqlSource.Tool/Cli.cs:211-224 are blind)
- cover: `tests/SqlSource.Tool.Tests/Commands/UsageCheckTests.cs` | `UsageCheckTests.CheckUsage_ReplayLogPathWithAnEqualsSignOrASemicolon_IsNamedByItsPosition`; `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs` | `ReplayTests.Replay_NoBundle_IsAWrongCommandLine`
- proposal: Delete the test; the rule is held as a function by the unit test, and its printing by the other wrong command line of replay.
- verdict: confirmed
- verifier's reason: ReplayTests.cs:740-752 asserts the `CliResult` of `replay case --log a=s3cret`.  The text comes from `ReplayCommand.CheckUsage` (src/SqlSource.Tool/ReplayCommand.cs:106-123), held for `=` and `;` by `CheckUsage_ReplayLogPathWithAnEqualsSignOrASemicolon_IsNamedByItsPosition` (UsageCheckTests.cs:756-768), and `Cli.ReadCommandLineAsync` writes every command's lines alike (Cli.cs:250-258, 287-288) before `OpenLog`, which `Replay_NoBundle_IsAWrongCommandLine` (ReplayTests.cs:732-738) holds for replay with an exact `CliResult`.  `kills` in tool-root: of the test's 63 kills, 61 are under the two named covers and every timeout (49) is under them; the other two, src/SqlSource.Tool/DescribeCommand.cs:211:13 (describe's `CheckUsage` run on a replay line) and src/SqlSource.Tool/UsageCheck.cs:123:25 (the option's value read again as an argument), are generic reading of a command line, not the replay log rule, and both are killed by `DiagnoseTests.Diagnose_WrongCommandLine_IsAUsageLineThatNamesNoToken`, the evidence's measured cover; the cleanup should name it beside the two.  No comment or document names the test.
- condition: The verifier adds a third cover for two of the test's kills: `DiagnoseTests.Diagnose_WrongCommandLine_IsAUsageLineThatNamesNoToken` kills `src/SqlSource.Tool/DescribeCommand.cs:211:13` and `src/SqlSource.Tool/UsageCheck.cs:123:25`; name it beside the two covers.

### F-SqlSource.Tool.Tests.Commands-1-02

- test: `tests/SqlSource.Tool.Tests/Commands/DescribeTests.cs` | `DescribeTests.Run_Help_ShowsThePath`; `tests/SqlSource.Tool.Tests/Commands/DescribeTests.cs` | `DescribeTests.Run_Help_ShowsTheProjectOption`; `tests/SqlSource.Tool.Tests/Commands/DescribeTests.cs` | `DescribeTests.Help_Describe_ListsWatch`; `tests/SqlSource.Tool.Tests/Commands/FilterTests.cs` | `FilterTests.Run_Help_ShowsThePathsAndTheOptionsOfDescribe` | rows: all
- action: fold
- evidence: read (each of the four runs `describe --help` and asserts exit 0 and a few texts of the same output: DescribeTests.cs:29-36 the usage line `sqlsource describe [<path>...]` and an empty error; DescribeTests.cs:85-95 `--project <path>`, `--check`, `--log <path>`, `--verbose` and no process; DescribeTests.cs:98-105 `--watch` and no process; FilterTests.cs:236-247 the same usage line again at :241, `.sql`, `--database <name>`, `--connection <name=value>`, `--force` and `--env-file <path>`.  `Run_Help_ShowsThePath` asserts nothing the FilterTests one does not but the empty error.  No `AGENTS.md`, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names any of them, and no comment marks one as a record.  Measured: `Run_Help_ShowsThePath` kills 48, alone 0, cover `DescribeTests.Help_Describe_ListsWatch` and `CheckFilterTests.Check_AfterADescribeWithTheSameFilterThatSucceeded_ExitsZero`; `Help_Describe_ListsWatch` kills 47, alone 0, cover `DescribeTests.Run_Help_ShowsThePath`; `Run_Help_ShowsTheProjectOption` kills 49, alone 2 (src/SqlSource.Tool/SharedOptions.cs:46:24 String mutation, src/SqlSource.Tool/SharedOptions.cs:76:24 String mutation); `FilterTests.Run_Help_ShowsThePathsAndTheOptionsOfDescribe` kills 53, alone 6; each has 53 timeouts under it, all reached by the one command line they share, so the folded test keeps them); blind: no
- cover: `tests/SqlSource.Tool.Tests/Commands/FilterTests.cs` | `FilterTests.Run_Help_ShowsThePathsAndTheOptionsOfDescribe`
- proposal: Make one test of `describe --help` in `DescribeTests`, a theory over each expected text or one test that loops over them as `DiagnoseTests.Diagnose_Help_ListsTheOptions` does, with the union of the four's texts, exit 0, an empty error and no process started, and remove the other three; every kill of its own stays, since each text is still asserted.
- verdict: confirmed
- verifier's reason: All four run `describe --help` with no setup (DescribeTests.cs:29-36, :85-95, :98-105; FilterTests.cs:236-247) and assert texts of one output; the union keeps exit 0, the usage line, `--project <path>`, `--check`, `--log <path>`, `--verbose`, `--watch`, `.sql`, `--database <name>`, `--connection <name=value>`, `--force`, `--env-file <path>`, an empty error and no process.  `kills` in tool-root, compared with the FilterTests test's: `Run_Help_ShowsThePath` has one kill it lacks, `Cli.cs:191:17 Negate expression`, which the empty error catches and the fold keeps; `Run_Help_ShowsTheProjectOption`'s two alone, `SharedOptions.cs:46:24` and `:76:24` String mutations, are the `--project <path>` and `--log <path>` texts, which the fold keeps; `Help_Describe_ListsWatch` has none it lacks; no timeout under any of the three is missing from the FilterTests test's 53.  Each kill is a text or the error line of the help, as expected.  The four names appear only in `docs/superpowers/plans/` (phase 2-2, 2-3, 2-4, 2-5, 2-6, 9-3 plans), none of the documents that make a record; no comment marks any of them as one.

### F-SqlSource.Tool.Tests.Commands-2-04

- test: `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs` | `ReplayTests.Replay_ToolVersionWithAControlCharacter_PrintsItsEscape` | rows: `{ "\u0007", "\\u0007" }` (line 171), `{ "\u009b", "\\u009B" }` (line 172), `{ "\u007f", "\\u007F" }` (line 173)
- action: fold
- evidence: measured (4 rows, kills 55, alone 0, timeouts under it 54; rows needed 1) and read (the escaping is `BundleText.Of` (src/SqlSource.Tool/Bundles/BundleText.cs), outside the tool-root scope, so the rows are judged by reading: ReplayCommand.cs:228 passes the tool version through it, and one control character proves that the field is escaped by it and not by `OneLine.Of`, which changes line breaks alone; which kinds `BundleText` escapes, C0, DEL, C1 and the line breaks, `BundleTextTests.Of_ControlCharacter_IsWrittenAsItsEscape` holds for each at tests/SqlSource.Tool.Tests/Bundles/BundleTextTests.cs:11-26.  The comment at ReplayTests.cs:165-166 lists the kinds and makes no record of a regression; the documents searched name neither the test nor the rows); blind: no
- cover: `tests/SqlSource.Tool.Tests/Bundles/BundleTextTests.cs` | `BundleTextTests.Of_ControlCharacter_IsWrittenAsItsEscape`
- proposal: Keep the escape row, `{ "\u001b", "\\u001B" }`, and drop the other three, which test `BundleText` again through the whole command.
- verdict: confirmed
- verifier's reason: ReplayTests.cs:176-197 puts the control character in `ToolVersion`, which reaches the output only through `BundleText.Of(info.ToolVersion)` at src/SqlSource.Tool/ReplayCommand.cs:226-230; `BundleText.Of` (src/SqlSource.Tool/Bundles/BundleText.cs) treats every kind alike (`char.IsControl` but tab), and `BundleTextTests.Of_ControlCharacter_IsWrittenAsItsEscape` (tests/SqlSource.Tool.Tests/Bundles/BundleTextTests.cs:11-26) holds ESC, BEL, NUL, DEL, CSI, NEL, LF and CR.  The ESC row alone shows the field goes through `BundleText` and not `OneLine`.  The comment at ReplayTests.cs:165-166 lists the kinds and records no regression or decision; no document names the test.  The rows share one name, so no filter measures the drop: confirmed by reading, and the cleanup phase's own guard proves it.

### F-SqlSource.Tool.Tests.Commands-2-05

- test: `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs` | `ReplayTests.Replay_FailureWithAControlCharacter_PrintsItsEscape` | rows: `{ "\u0007", "\\u0007" }` (line 171), `{ "\u009b", "\\u009B" }` (line 172), `{ "\u007f", "\\u007F" }` (line 173)
- action: fold
- evidence: measured (4 rows, kills 57, alone 0, timeouts under it 54; rows needed 1) and read (the id and the step of the expected failure reach the output through `BundleText.Of(difference)` at src/SqlSource.Tool/ReplayCommand.cs:221, outside the tool-root scope; one row shows that the difference is escaped, and `BundleTextTests.Of_ControlCharacter_IsWrittenAsItsEscape` holds every kind at tests/SqlSource.Tool.Tests/Bundles/BundleTextTests.cs:11-26.  No comment or document makes the rows a record); blind: no
- cover: `tests/SqlSource.Tool.Tests/Bundles/BundleTextTests.cs` | `BundleTextTests.Of_ControlCharacter_IsWrittenAsItsEscape`
- proposal: Keep the escape row and drop the other three; with F-…-04 the shared `Controls` data becomes the one row.
- verdict: confirmed
- verifier's reason: ReplayTests.cs:199-224 puts the control character in the expected failure's id and step, which reach the output only through `BundleText.Of(difference)` at src/SqlSource.Tool/ReplayCommand.cs:219-222; as for F-…-04, the kinds are `BundleText`'s alone and `BundleTextTests.Of_ControlCharacter_IsWrittenAsItsEscape` holds each.  No comment or document makes the rows a record.  Rows share a name: confirmed by reading, and the cleanup phase's own guard proves it.

### F-SqlSource.Tool.Tests.Commands-1-01

- test: `tests/SqlSource.Tool.Tests/Commands/DescribeTests.cs` | `DescribeTests.Run_HelpOfTheTool_ListsTheCommand` | rows: all
- action: strengthen
- evidence: read (DescribeTests.cs:20-26 runs `--help` and asserts only `result.Out.ShouldContain("describe")`.  Shouldly's string `ShouldContain` ignores case unless it is given `Case.Sensitive`, as the repository's own `Case.Sensitive` calls rely on (`tests/SqlSource.Tests/Snapshot/SidecarWriterTests.cs:92`), and the same `--help` output holds the root command's description "Describes the SQL queries of a project for the SqlSource generator" (`src/SqlSource.Tool/Cli.cs:420`), which `CliTests.Run_Help_ListsTheOptionsThatDoSomething` already asserts at CliTests.cs:43.  So the test passes with the `describe` command taken out of the help.  Measured: kills 36, alone 0, timeouts under it 52, cover `CliTests.Run_Help_ListsTheOptionsThatDoSomething`); blind: no
- cover: none
- proposal: Assert the command's own entry in the help's list of commands, the line that starts with `describe` and is matched with `Case.Sensitive`, so that the root description no longer satisfies it.
- verdict: confirmed
- verifier's reason: DescribeTests.cs:20-26 asserts only exit 0 and `result.Out.ShouldContain("describe")`, with no `Case`; the repository passes `Case.Sensitive` where it means it (13 calls under `tests/`, e.g. `tests/SqlSource.Tests/Snapshot/SidecarWriterTests.cs:92`), since Shouldly 4.3.0 (`Directory.Packages.props:42`) compares a string's `ShouldContain` ignoring case by default.  The root command's description, "Describes the SQL queries of a project for the SqlSource generator" (`src/SqlSource.Tool/Cli.cs:420`), is in the same `--help` output, so the test still passes with the `describe` command taken out of the list.  `kills` in tool-root: 36 kills, 0 alone, 52 timeouts, 0 survivors; every kill is of the shared help path, which `CliTests.Run_Help_ListsTheOptionsThatDoSomething` (CliTests.cs:38-47) reaches too.  The test reads rightly as one whose name promises more than it asserts.
- survivors to kill: none named by place; the missing assertion is the proposal's

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-root`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-50/tool-root/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Commands.|FullyQualifiedName~SqlSource.Tool.Tests.OutcomeTests.|FullyQualifiedName~SqlSource.Tool.Tests.ToolHostTests.|FullyQualifiedName~SqlSource.Tool.Tests.UsageValueTests.' '*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-50/tool-root/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Commands.|FullyQualifiedName~SqlSource.Tool.Tests.OutcomeTests.|FullyQualifiedName~SqlSource.Tool.Tests.ToolHostTests.|FullyQualifiedName~SqlSource.Tool.Tests.UsageValueTests.' '*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-50/tool-root/before/reports/mutation-report.json artifacts/test-cleanup/phase-50/tool-root/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-50/tool-root/before/reports/mutation-report.json DescribeTests.Run_HelpOfTheTool_ListsTheCommand
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-50/tool-root/before/reports/mutation-report.json DescribeTests.Run_Help_ShowsThePath
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-50/tool-root/before/reports/mutation-report.json DescribeTests.Run_Help_ShowsTheProjectOption
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-50/tool-root/before/reports/mutation-report.json DescribeTests.Help_Describe_ListsWatch
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-50/tool-root/before/reports/mutation-report.json FilterTests.Run_Help_ShowsThePathsAndTheOptionsOfDescribe
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-50/tool-root/before/reports/mutation-report.json ReplayTests.Replay_Failed_HasNoLineThatPointsToALog
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-50/tool-root/before/reports/mutation-report.json ReplayTests.Replay_LogPathWithAnEqualsSign_IsAWrongCommandLine
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-50/tool-root/before/reports/mutation-report.json ReplayTests.Replay_ToolVersionWithAControlCharacter_PrintsItsEscape
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-50/tool-root/before/reports/mutation-report.json ReplayTests.Replay_FailureWithAControlCharacter_PrintsItsEscape
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests.Commands-1-01: no survivor is named by place; the test fails when the assertion it adds is removed, checked once by hand

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-root`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-50/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-50/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-50/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-50/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-50/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-50/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tool.Tests.Commands-2-02, F-SqlSource.Tool.Tests.Commands-2-03, F-SqlSource.Tool.Tests.Commands-2-04, F-SqlSource.Tool.Tests.Commands-2-05.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tool.Tests.Commands-2-02 | delete | `ReplayTests.Replay_Failed_HasNoLineThatPointsToALog` | `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs`: `ReplayTests.Replay_NothingAtThePath_IsSqlsrc288`; `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs`: `ReplayTests.Replay_EngineWithNoDescriber_IsSqlsrc291` |
| F-SqlSource.Tool.Tests.Commands-2-03 | delete | `ReplayTests.Replay_LogPathWithAnEqualsSign_IsAWrongCommandLine` | `tests/SqlSource.Tool.Tests/Commands/UsageCheckTests.cs`: `UsageCheckTests.CheckUsage_ReplayLogPathWithAnEqualsSignOrASemicolon_IsNamedByItsPosition`; `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs`: `ReplayTests.Replay_NoBundle_IsAWrongCommandLine` |
| F-SqlSource.Tool.Tests.Commands-2-04 | fold | `ReplayTests.Replay_ToolVersionWithAControlCharacter_PrintsItsEscape` (rows: `{ "\u0007", "\\u0007" }` (line 171), `{ "\u009b", "\\u009B" }` (line 172), `{ "\u007f", "\\u007F" }` (line 173)) | `tests/SqlSource.Tool.Tests/Bundles/BundleTextTests.cs`: `BundleTextTests.Of_ControlCharacter_IsWrittenAsItsEscape` |
| F-SqlSource.Tool.Tests.Commands-2-05 | fold | `ReplayTests.Replay_FailureWithAControlCharacter_PrintsItsEscape` (rows: `{ "\u0007", "\\u0007" }` (line 171), `{ "\u009b", "\\u009B" }` (line 172), `{ "\u007f", "\\u007F" }` (line 173)) | `tests/SqlSource.Tool.Tests/Bundles/BundleTextTests.cs`: `BundleTextTests.Of_ControlCharacter_IsWrittenAsItsEscape` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tool.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/architecture/bundles.md`: 79

## Left out

- F-SqlSource.Tool.Tests.Commands-2-15 (keep, `ReplayTests.Replay_EveryFixtureFolder_IsReproduced`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
