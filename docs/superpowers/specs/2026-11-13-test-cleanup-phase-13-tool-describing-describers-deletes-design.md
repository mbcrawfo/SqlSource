# Test cleanup phase 13: tests/SqlSource.Tool.Tests/Describing/Describers, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Describing/Describers`
- Findings: 2 (2 delete)
- Kind: one kind, delete
- Changed lines: about 26
- Guard: mutation (tool-describing-describers)

## Files

- `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberSessionTests.cs`
- `tests/SqlSource.Tool.Tests/Describing/Describers/TableMatchChecksTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Describing.Describers-1-03

- test: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberSessionTests.cs` | `DescriberSessionTests.ServerOf_NoVersion_Throws` | rows: all
- action: delete
- evidence: measured (kills 1, alone 0; cover `DescriberSessionTests.OpenAsync_AnswerWithNoVersion_ThrowsAndDisposesTheConnection`) and read (DescriberSessionTests.cs:81-84 calls `ServerOfVersion(null)` and asserts the type and the exact message; the cover at DescriberSessionTests.cs:67-78 reaches the same `ServerOf` through the opening at :136 and asserts the same type and the same exact message, and the disposal besides.  No comment marks it as a record; grep of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ finds neither the test nor its class); blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberSessionTests.cs` | `DescriberSessionTests.OpenAsync_AnswerWithNoVersion_ThrowsAndDisposesTheConnection`
- proposal: Delete the test; the `ServerOfVersion` shim stays, since the opening of `TestSession` calls it.
- verdict: confirmed
- verifier's reason: `kills`: its one kill, DescriberSession.cs:93:60 (the message made empty), is also killed by `OpenAsync_AnswerWithNoVersion_ThrowsAndDisposesTheConnection`, and there are no timeouts.  The cover at DescriberSessionTests.cs:67-78 gives `TestOpenAnswer(null, null)`, which reaches `ServerOfVersion(answered.Version)` (:136) with the same null version.  It asserts the same `InvalidOperationException` and the same exact message (:76), and the disposal as well.  Nothing marks the test as a record: the comment at :66 is the cover's.  The grep of the documents finds neither `ServerOf_NoVersion` nor `DescriberSessionTests`.  `ServerOfVersion` is still used at :136, as the proposal says.

### F-SqlSource.Tool.Tests.Describing.Describers-1-04

- test: `tests/SqlSource.Tool.Tests/Describing/Describers/TableMatchChecksTests.cs` | `TableMatchChecksTests.OfTheTable_CheckThatFails_RunsNoCheckAfterIt` | rows: all
- action: delete
- evidence: measured (kills 5, alone 0; cover `TableMatchChecksTests.OfTheTable_EachCheckThatFailsWithTheOnesBeforeItPassing_IsItsOwnValue`) and read (TableMatchChecksTests.cs:67-83 calls `OfTheTable(Passes, <fails>, MustNotRun, MustNotRun)` and expects `ColumnsDiffer`, the call and value of the cover's first statement at :50-52; that no later check runs is asserted by `MustNotRun` in the cover and in `OfTheTable_EveryCheckFails_IsNamesDiffer` and `OfTheOrigins_BothFail_IsNoOrigin`.  Its one addition, `ran.ShouldBe(1)`, is a count of calls of the failing check, which must run for its `false` to be the answer.  The class comment states the rule, which the other tests keep; grep of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ finds neither the test nor its class); blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/Describers/TableMatchChecksTests.cs` | `TableMatchChecksTests.OfTheTable_EachCheckThatFailsWithTheOnesBeforeItPassing_IsItsOwnValue`; `tests/SqlSource.Tool.Tests/Describing/Describers/TableMatchChecksTests.cs` | `TableMatchChecksTests.OfTheTable_EveryCheckFails_IsNamesDiffer`
- proposal: Delete the test; the `MustNotRun` delegates of the remaining tests hold the rule that no check runs after the first that fails.
- verdict: confirmed
- verifier's reason: `kills`: all five of its kills (TableMatchChecks.cs:73:10, 78:25, 78:44, 80:17 and 82:30) are also killed by `OfTheTable_EachCheckThatFailsWithTheOnesBeforeItPassing_IsItsOwnValue`, and each kill is explained by the inputs (Passes, Fails) → `ColumnsDiffer`.  The cover's first statement at TableMatchChecksTests.cs:50-52 is the same call, `(Passes, Fails, MustNotRun, MustNotRun)`, with the same expected value.  Its `MustNotRun` (:17) throws when a later check runs, which is the rule this test's name promises.  `OfTheTable_EveryCheckFails_IsNamesDiffer` (:42-45) holds the same rule from the first check.  The only extra assertion, `ran.ShouldBe(1)` at :83, counts calls of the failing check.  TableMatchChecks.cs:13-17 promises nothing about how often the failing check runs.  The class comment at :9-10 states the rule but names no test as its record, and the grep of the documents finds neither the test nor `TableMatchChecksTests`.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-describing-describers`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-13/tool-describing-describers/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.Describers.' 'Describing/Describers/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-13/tool-describing-describers/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.Describers.' 'Describing/Describers/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-13/tool-describing-describers/before/reports/mutation-report.json artifacts/test-cleanup/phase-13/tool-describing-describers/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-13/tool-describing-describers/before/reports/mutation-report.json DescriberSessionTests.ServerOf_NoVersion_Throws
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-13/tool-describing-describers/before/reports/mutation-report.json TableMatchChecksTests.OfTheTable_CheckThatFails_RunsNoCheckAfterIt
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-describing-describers`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-13/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-13/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-13/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-13/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-13/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-13/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tool.Tests.Describing.Describers-1-06 (keep, `DescriberSessionTests.IsBelow_Version_ComparesItsLeadingMajor`): a **keep**; the test stays as it is.
- F-SqlSource.Tool.Tests.Describing.Describers-1-07 (keep, `DescriberSessionTests.OpenAsync_AnswerWithAnError_IsTheFailureAndDisposesTheConnection`): a **keep**; the test stays as it is.
- F-SqlSource.Tool.Tests.Describing.Describers-1-08 (keep, `DescriberSessionTests.OpenAsync_ExchangeThatThrows_ThrowsAndDisposesTheConnection`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
