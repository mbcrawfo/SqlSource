# Test cleanup phase 60: tests/SqlSource.Tool.Tests/Processes, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Processes`
- Findings: 2 (2 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 16
- Guard: mutation (tool-processes)

## Files

- `tests/SqlSource.Tool.Tests/Processes/ProcessRunnerTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests-3-04

- test: `tests/SqlSource.Tool.Tests/Processes/ProcessRunnerTests.cs` | `ProcessRunnerTests.Run_Cancelled_LeavesNeitherTheProgramNorWhatItStartedRunning` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource.Tool/Processes/ProcessRunner.cs:125:82 Boolean mutation -> `false`, covered by 3 methods, this one among them; kills 1, timeouts under it 4) and read: with `entireProcessTree: false` only the shell is killed, and the child it started, `sleep 30` or `ping -n 30` (ProcessRunnerTests.cs:45, 52), ends by itself after 30 seconds, well inside the two minutes of `Eventually.Patience` (tests/SqlSource.Testing/Eventually.cs:15) that the wait for it at line 145 allows.  So the half of the name that promises "nor what it started" holds whether the tree is killed or not; blind: no
- cover: none
- proposal: Make the child wait longer than `Eventually.Patience` (say ten minutes, through the same `sleep` and `ping` arguments), so that only the kill of the whole tree can end it before the wait gives up.
- verdict: confirmed
- verifier's reason: `kills` in tool-processes lists `SURVIVED src/SqlSource.Tool/Processes/ProcessRunner.cs:125:82 Boolean mutation -> false` under the test.  With `entireProcessTree: false` only the shell is killed and its child, `sleep 30` or `ping -n 30` (tests/SqlSource.Tool.Tests/Processes/ProcessRunnerTests.cs:45, 52), ends by itself within 30 seconds, inside the two minutes of `Eventually.Patience` (tests/SqlSource.Testing/Eventually.cs:15) that the wait at ProcessRunnerTests.cs:145 allows, so the half of the name, and of the comment at lines 124-126 ("Both must be gone"), about what the program started holds with or without the tree kill.  The reading of the test is right, and the proposal names the missing condition.
- survivors to kill: `src/SqlSource.Tool/Processes/ProcessRunner.cs:125:82`

### F-SqlSource.Tool.Tests-3-05

- test: `tests/SqlSource.Tool.Tests/Processes/ProcessRunnerTests.cs` | `ProcessRunnerTests.Run_ProgramThatFails_GivesItsExitCodeAndItsErrorOutput` | rows: all
- action: strengthen
- evidence: measured (covers 14 mutants and kills none; timeouts under it 1; 1.1 s, the slowest test of the class) and read: the test asserts only a non-zero exit code and that `Output + Error` is not empty (ProcessRunnerTests.cs:83-84), so it passes whether the program's error output reaches `Error`, reaches `Output`, or is lost while the program writes anything to its standard output.  No other test of the slice asserts what a program writes to its error output: `Run_Dotnet_GivesItsExitCodeAndItsOutput` asserts it empty (line 72), and the error of `Run_ProgramThatDoesNotExist_IsNotStartedAndSaysWhy` is the runner's own reason, not a stream (src/SqlSource.Tool/Processes/ProcessResult.cs:19); blind: no
- cover: none
- proposal: Run a program that writes a known text to its error output alone and ends with a known code (`sh -c` or `cmd /c` as the class already picks by system), and assert that `Error` holds the text, `Output` does not, and the exit code is that code.
- verdict: confirmed
- verifier's reason: The test (tests/SqlSource.Tool.Tests/Processes/ProcessRunnerTests.cs:75-85) asserts only `ExitCode.ShouldNotBe(0)` and that `Output + Error` is not empty, so nothing in it tells the error output from the standard output, though its name promises "ItsErrorOutput".  `kills` in tool-processes: 0 kills, a timeout at ProcessRunner.cs:89:13 (the closed input, which `sort` guards) and survivors ProcessResult.cs:14:42 and ProcessRunner.cs:41:30.  No other test of the slice asserts what a program writes to its error stream: `Run_Dotnet_GivesItsExitCodeAndItsOutput` asserts it empty (line 72), `Run_ProgramThatDoesNotExist_IsNotStartedAndSaysWhy` reads the runner's own reason (src/SqlSource.Tool/Processes/ProcessResult.cs:19), and the two tests of ProcessRunnerEnvironmentTests.cs read `Output` alone (lines 41-43, 71-74).
- survivors to kill: none named by place; the missing assertion is the proposal's

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-processes`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-60/tool-processes/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Processes.' 'Processes/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-60/tool-processes/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Processes.' 'Processes/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-60/tool-processes/before/reports/mutation-report.json artifacts/test-cleanup/phase-60/tool-processes/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-60/tool-processes/before/reports/mutation-report.json ProcessRunnerTests.Run_Cancelled_LeavesNeitherTheProgramNorWhatItStartedRunning
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-60/tool-processes/before/reports/mutation-report.json ProcessRunnerTests.Run_ProgramThatFails_GivesItsExitCodeAndItsErrorOutput
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests-3-04: `src/SqlSource.Tool/Processes/ProcessRunner.cs:125:82`
- F-SqlSource.Tool.Tests-3-05: no survivor is named by place; the test fails when the assertion it adds is removed, checked once by hand

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-processes`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-60/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-60/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-60/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-60/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-60/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-60/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tool.Tests-3-06 (keep, `ProcessRunnerTests.Run_ProgramThatReadsItsInput_FindsTheEndOfIt`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
