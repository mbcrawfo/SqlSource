# Test cleanup phase 32: tests/SqlSource.Tool.Tests/Logging, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Logging`
- Findings: 2 (1 delete, 1 fold)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 15
- Guard: mutation (tool-logging), with covers and run time for 1 finding

## Files

- `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests-2-03

- test: `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs` | `RunLogTests.RunStart_Arguments_KeepTheOptionAndTheSeparatorOfAConnection` | rows: all
- action: delete
- evidence: measured (2 rows, kills 24, alone 0, timeouts under it 0; rows needed 1; cover: `RunLogRedactionTests.Run_ConnectionOnTheCommandLine_IsLoggedWithNeitherItsNameNorItsValue`).  Read: its rows `--connection=` and `--connection:` (RunLogTests.cs:142-143) with `billing=<value>` assert `arguments` is the option, its separator and `***`; the rows `"--connection=***"` and `"--connection:***"` of `Run_ConnectionOnTheCommandLine_…` (RunLogRedactionTests.cs:38-39) give the same two forms of the same option and assert the same list, and also that the value is nowhere in the file.  `LoggedArguments.Of` (src/SqlSource.Tool/Logging/LoggedArguments.cs:35-40) treats the database's name inside the value no differently.  No comment above the test, and no document of those the brief names mentions it.  blind: no (LoggedArguments.cs:39 is blind, which is why the reading, not the numbers, decides)
- cover: `tests/SqlSource.Tool.Tests/Logging/RunLogRedactionTests.cs` | `RunLogRedactionTests.Run_ConnectionOnTheCommandLine_IsLoggedWithNeitherItsNameNorItsValue`
- proposal: Delete the theory; its twin of `--database` (`RunStart_Arguments_KeepTheOptionAndTheSeparatorOfADatabase`) stays, since no other test writes `--database` with a separator.
- verdict: confirmed
- verifier's reason: Read RunLogTests.cs:141-150 beside RunLogRedactionTests.cs:35-56: the rows `--connection=` and `--connection:` are written with the same option and separators in both, and the cover asserts the same `option + "***"` token in the whole `arguments` list, plus exit code 0, the describer's connection and that the marker is nowhere in the file.  The differences are the database's name inside the value (`billing` against `postgres`) and the token's position (first against after `--log`); LoggedArguments.cs:35-40 cuts by `value.Text.Length` at `value.Position - 1` and reads neither the name nor depends on the position beyond that index, which both positions exercise.  `kills` (tool-logging): each of its LoggedArguments kills (38:30, 39:20, 39:51, 48:75, 54:18) also names `Run_ConnectionOnTheCommandLine_IsLoggedWithNeitherItsNameNorItsValue`; no timeouts under it.  LoggedArguments.cs:39 is blind, so the reading decides, and it agrees.  The comment at RunLogTests.cs:19-20 sends the values a log must not hold to RunLogRedactionTests; no comment is above the test and no document of those the brief names mentions it.  The `--database` twin (RunLogTests.cs:130-139) stays, as proposed.

### F-SqlSource.Tool.Tests-2-06

- test: `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs` | `RunLogTests.Sidecar_EachAction_IsItsWord` | rows: `("Unreadable", true, "failed", "could not be changed")` (line 330)
- action: fold
- evidence: measured (8 rows, kills 19, alone 4, timeouts under it 0; rows needed 5; no cover, its own kills are the words of `Write`, `Delete`, `HeldBack` and `Unchanged`, RunEvents.cs:289-292).  Read: the one caller passes `FileAction.Unreadable` only with `failed: false` (src/SqlSource.Tool/Describing/SidecarStore.cs:133), so the row is a call the run never makes; the `failed` flag's effect (`RunEvents.cs:206`) is shown by the rows `("Write", true, …)` and `("Delete", true, …)`, the two the parameter's documentation names, and `("Unreadable", false, …)` shows that the action alone is `failed`.  blind: yes (the words of `failed` are in the static field at RunEvents.cs:23)
- cover: `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs` | `RunLogTests.Sidecar_EachAction_IsItsWord` (its rows `("Write", true, "failed", "could not be changed")` and `("Unreadable", false, "failed", "could not be changed")`)
- proposal: Drop the `("Unreadable", true, …)` row; the other seven stay.
- verdict: confirmed
- verifier's reason: Read RunEvents.cs:190-207 and :286-297 and RunLogTests.cs:322-344: `failed` picks `SidecarFailed` before the action is looked at (RunEvents.cs:206), so `("Unreadable", true)` takes the same path as `("Write", true)` and `("Delete", true)`, and `("Unreadable", false)` already proves the action's own word (RunEvents.cs:294).  The only caller passes `FileAction.Unreadable` with `failed: false` (SidecarStore.cs:133; grep of src and tests finds no other call but the two of RunLogTests).  The comment at :322-323 is about a sidecar that was to be written or deleted, not about this row.  The words are in the static field at RunEvents.cs:23 (blind), so the reading decides.  A fold that drops a row of a theory cannot be measured by a filter: confirmed by reading, and the phase's own guard proves it.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-logging`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-32/tool-logging/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Logging.' 'Logging/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-32/tool-logging/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Logging.' 'Logging/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-32/tool-logging/before/reports/mutation-report.json artifacts/test-cleanup/phase-32/tool-logging/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-32/tool-logging/before/reports/mutation-report.json RunLogTests.RunStart_Arguments_KeepTheOptionAndTheSeparatorOfAConnection
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-32/tool-logging/before/reports/mutation-report.json RunLogTests.Sidecar_EachAction_IsItsWord
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-logging`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-32/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-32/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-32/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-32/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-32/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-32/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tool.Tests-2-06.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tool.Tests-2-06 | fold | `RunLogTests.Sidecar_EachAction_IsItsWord` (rows: `("Unreadable", true, "failed", "could not be changed")` (line 330)) | `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs`: `RunLogTests.Sidecar_EachAction_IsItsWord` (its rows `("Write", true, "failed", "could not be changed")` and `("Unreadable", false, "failed", "could not be changed")`) |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tool.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `src/SqlSource.Tool/Logging/AGENTS.md`: 8

## Left out

- F-SqlSource.Tool.Tests-2-10 (keep, `RunLogTests.Run_SidecarThatCannotBeDeleted_HasTheActionFailed`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
