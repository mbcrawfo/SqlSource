# Test cleanup phase 54: tests/SqlSource.Tool.Tests/Describing/Describers, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Describing/Describers`
- Findings: 2 (1 delete, 1 strengthen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 22
- Guard: mutation (tool-describing-describers), with covers and run time for 1 finding

## Files

- `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Describing.Describers-1-02

- test: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberTests.cs` | `DescriberTests.DescribeAsync_QueryGivenAFailure_ReturnsItAndDoesNotThrow` | rows: all
- action: delete
- evidence: read (it tests the describer of the tests, `tests/SqlSource.Tool.Tests/FakeDescriber.cs`, outside the scope's source: reaches no tested mutant.  DescriberTests.cs:81-92 sets a failure in `Failures`, describes, and asserts `Value` is null and `Step` is `DescribeColumns`, the default of `FakeDescriber.Failure(query, lines)` at FakeDescriber.cs:163-164.  DescriberTests.cs:115-140 takes the same path, FakeDescriber.cs:271-272 and 287-290, with an explicit step, and asserts a null `Value` and every part of the failure, the step among them, without throwing.  The comment of the class says nothing of it; grep of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ finds neither the test nor `DescriberTests`); blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberTests.cs` | `DescriberTests.DescribeAsync_FailureGivenToTheDescriber_ComesBackWithEveryPart`
- proposal: Delete the test; the cover proves the same round trip of a failure through the test describer's session, part by part.
- verdict: confirmed
- verifier's reason: The test's subject, FakeDescriber of the test project, is outside the scope's source (`kills`: 0 kills, 0 survivors), so the named-cover rule applies.  I read the test and its cover.  DescriberTests.cs:81-92 puts `Failure("GetUser", line)` in `Failures`, which defaults to step `DescribeColumns` (FakeDescriber.cs:163-164), and asserts that nothing is thrown, that `Value` is null and that `Step` is `DescribeColumns`.  The cover at DescriberTests.cs:115-140 takes the same path: it opens the same way (the helper at :94-98), goes through the `Failures` lookup (FakeDescriber.cs:271-272) and comes back as a failure (FakeDescriber.cs:287-290).  It asserts that nothing is thrown, a null `Value`, the step and every other part.  The step goes through the exchange as `Step?.ToString()` and comes back by `Enum.Parse` (FakeCalls.cs:62, :107), whatever its value, so `Explain` in place of `DescribeColumns` proves the same.  Nothing marks the test as a record: the class comment at :16 does not, and a grep of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred finds neither the test's name nor `DescriberTests`.

### F-SqlSource.Tool.Tests.Describing.Describers-1-01

- test: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberTests.cs` | `DescriberTests.Constructor_TwoDescribersOfOneDialect_Throws` | rows: all
- action: strengthen
- evidence: measured (covers 6 mutants and kills none; survivors src/SqlSource.Tool/Describing/Describers/DescriberRegistry.cs:18:91 Equality mutation, src/SqlSource.Tool/Describing/Describers/DescriberRegistry.cs:21:13 Statement mutation and src/SqlSource.Tool/Describing/Describers/DescriberRegistry.cs:21:41 String mutation, the last two under this test alone) and read (DescriberTests.cs:36-37 asserts only `Should.Throw<ArgumentException>`; DescriberRegistry.cs:24 `ToDictionary` throws an `ArgumentException` of its own for a duplicate key, so the guard at DescriberRegistry.cs:18-22 can be removed and the test still passes); blind: no (line 18 also holds mutants that do not compile, but the three survivors named were run)
- cover: none
- proposal: Assert what the registry's own refusal says and the dictionary's does not: `ParamName` is `describers` and the message is `Two describers are given for the dialect PostgreSql.` (with its parameter suffix), which kills all three survivors.
- verdict: confirmed
- verifier's reason: DescriberTests.cs:36-37 asserts only `Should.Throw<ArgumentException>`.  `kills` in tool-describing-describers gives 0 kills and three SURVIVED under it: DescriberRegistry.cs:18:91 (`group.Count() < 1`), 21:13 (statement removed) and 21:41 (empty message).  Each one is explained by DescriberRegistry.cs:24: once the guard at :18-22 is gone or never fires, `ToDictionary` throws its own `ArgumentException` for the duplicate `PostgreSql` key, so the test still passes.  The proposed assertions (`ParamName` `describers`, and the message "Two describers are given for the dialect PostgreSql." with the parameter suffix) are what the guard's exception holds and the dictionary's exception does not, so they kill all three.
- survivors to kill: `src/SqlSource.Tool/Describing/Describers/DescriberRegistry.cs:18:91`, `src/SqlSource.Tool/Describing/Describers/DescriberRegistry.cs:21:13`, `src/SqlSource.Tool/Describing/Describers/DescriberRegistry.cs:21:41`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-describing-describers`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-54/tool-describing-describers/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.Describers.' 'Describing/Describers/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-54/tool-describing-describers/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.Describers.' 'Describing/Describers/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-54/tool-describing-describers/before/reports/mutation-report.json artifacts/test-cleanup/phase-54/tool-describing-describers/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-54/tool-describing-describers/before/reports/mutation-report.json DescriberTests.Constructor_TwoDescribersOfOneDialect_Throws
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-54/tool-describing-describers/before/reports/mutation-report.json DescriberTests.DescribeAsync_QueryGivenAFailure_ReturnsItAndDoesNotThrow
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests.Describing.Describers-1-01: `src/SqlSource.Tool/Describing/Describers/DescriberRegistry.cs:18:91`, `src/SqlSource.Tool/Describing/Describers/DescriberRegistry.cs:21:13`, `src/SqlSource.Tool/Describing/Describers/DescriberRegistry.cs:21:41`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-describing-describers`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-54/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-54/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-54/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-54/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-54/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-54/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tool.Tests.Describing.Describers-1-02.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tool.Tests.Describing.Describers-1-02 | delete | `DescriberTests.DescribeAsync_QueryGivenAFailure_ReturnsItAndDoesNotThrow` | `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberTests.cs`: `DescriberTests.DescribeAsync_FailureGivenToTheDescriber_ComesBackWithEveryPart` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tool.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
